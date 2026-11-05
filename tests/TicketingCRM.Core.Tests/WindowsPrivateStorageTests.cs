using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.Core.Security;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;
using Xunit;

namespace TicketingCRM.Core.Tests;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires native Windows ACL support.";
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsPrivateStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "appsouls-windows-" + Guid.NewGuid().ToString("N"));
    private VaultBootstrapOptions Bootstrap => new(Path.Combine(root, "vault", "server"), Path.Combine(root, "keys", "master"));

    [WindowsFact]
    public async Task PreparedAdministratorSurvivesRetryAndRejectsTamperingAndExpiryWithoutCreatingSetupTokens()
    {
        var store = new PendingAdministratorFile(Bootstrap);
        var input = new PreparedAdministrator
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "private@example.test",
            PasswordHash = "test-opaque-hash",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        using (store.AcquireSetupLock())
            Assert.Throws<IOException>(() => store.AcquireSetupLock());
        await store.SaveAsync(input);
        var bytes = await File.ReadAllBytesAsync(store.PathName);
        Assert.DoesNotContain(input.Email, Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain(input.PasswordHash, Encoding.UTF8.GetString(bytes));
        AssertOwnerOnly(store.PathName);
        AssertOwnerOnly(store.KeyPath);
        var loaded = await store.LoadAsync();
        Assert.Equal(input.UserId, loaded.UserId);
        Assert.Equal(input.TenantId, loaded.TenantId);
        Assert.Equal(input.PasswordHash, loaded.PasswordHash);
        bytes[^1] ^= 1;
        await File.WriteAllBytesAsync(store.PathName, bytes);
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => store.LoadAsync());
        await store.SaveAsync(input);
        Assert.Equal(input.UserId, (await store.LoadAsync()).UserId);
        input.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.SaveAsync(input);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync());
        Assert.Equal(input.UserId, (await store.LoadAsync(allowExpired: true)).UserId);
        Assert.False(File.Exists(Bootstrap.KeyPath + ".setup"));
        store.Complete();
        Assert.False(store.Exists);
        Assert.False(File.Exists(store.KeyPath));
    }

    [WindowsFact]
    public async Task VaultAndAuthenticationKeysRoundTripAndFailedRecoveryPreservesPreviousConfiguration()
    {
        var vault = new FileConfigurationVault(Bootstrap);
        var original = new ServerConfiguration { DatabaseConnectionString = "test-only-placeholder", PublicBaseUrl = "https://example.test" };
        await vault.InitializeAsync(original);
        AssertOwnerOnly(Bootstrap.KeyPath);
        AssertOwnerOnly(Bootstrap.VaultPath);
        Assert.Equal(original.PublicBaseUrl, (await vault.LoadAsync()).PublicBaseUrl);
        var encrypted = new VaultXmlKeyEncryptor(Bootstrap).Encrypt(new XElement("key", "test-key-material"));
        using var provider = new ServiceCollection().AddSingleton(Bootstrap).BuildServiceProvider();
        Assert.Equal("test-key-material", new VaultXmlKeyDecryptor(provider).Decrypt(encrypted.EncryptedElement).Value);
        var recovery = Path.Combine(root, "recovery", "backup");
        await vault.ExportRecoveryAsync(recovery);
        original = new ServerConfiguration { DatabaseConnectionString = "test-only-placeholder", PublicBaseUrl = "https://updated.example.test" };
        await vault.SaveAsync(original);
        var damaged = await File.ReadAllBytesAsync(recovery);
        damaged[^1] ^= 1;
        await File.WriteAllBytesAsync(recovery, damaged);
        await Assert.ThrowsAsync<InvalidOperationException>(() => vault.RestoreRecoveryAsync(recovery));
        Assert.Equal(original.PublicBaseUrl, (await vault.LoadAsync()).PublicBaseUrl);
    }

    [WindowsFact]
    public void PublicFileAndDirectoryAclsAreRejectedWithoutRewritingExistingPermissions()
    {
        var path = Path.Combine(root, "private", "key");
        using (PrivateFileSystem.Open(path, FileMode.CreateNew)) { }
        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        var publicRead = new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Read, AccessControlType.Allow);
        security.AddAccessRule(publicRead);
        file.SetAccessControl(security);
        Assert.Throws<InvalidOperationException>(() => PrivateFileSystem.CheckFile(path));
        Assert.Throws<InvalidOperationException>(() => PrivateFileSystem.Open(path, FileMode.OpenOrCreate));
        Assert.Contains(file.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),
            rule => rule.IdentityReference.Equals(publicRead.IdentityReference));
        security.RemoveAccessRuleSpecific(publicRead);
        file.SetAccessControl(security);
        var directory = file.Directory!;
        var directorySecurity = directory.GetAccessControl();
        directorySecurity.AddAccessRule(publicRead);
        directory.SetAccessControl(directorySecurity);
        Assert.Throws<InvalidOperationException>(() => PrivateFileSystem.PrepareDirectory(path));
        Assert.Throws<InvalidOperationException>(() => PrivateFileSystem.CheckFile(path));
    }

    [WindowsFact]
    public async Task ArtifactPublicationClosesTheWindowsHandleAndRetainsPrivatePermissions()
    {
        var store = new PrivateArtifactStore(Path.Combine(root, "artifacts"));
        var id = Guid.NewGuid();
        byte[] bytes = [1, 2, 3, 4];
        var result = await store.SaveAsync(id, new MemoryStream(bytes), CancellationToken.None);
        Assert.Equal(bytes.Length, result.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), result.Sha256);
        Assert.True(await store.VerifyAsync(id, result.Sha256, CancellationToken.None));
        AssertOwnerOnly(Path.Combine(root, "artifacts", id.ToString("N") + ".bin"));
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(id, new MemoryStream([5]), CancellationToken.None));
        Assert.True(await store.VerifyAsync(id, result.Sha256, CancellationToken.None));
        store.Delete(id);
        Assert.Throws<FileNotFoundException>(() => store.Open(id));
    }

    [WindowsFact]
    public void DirectoryJunctionsAndAlternateDataStreamsAreRejected()
    {
        PrivateFileSystem.PrepareDirectory(Path.Combine(root, "target", "file"));
        var junction = Path.Combine(root, "junction");
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c mklink /J \"{junction}\" \"{Path.Combine(root, "target")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        try { Assert.Throws<InvalidOperationException>(() => PrivateFileSystem.PrepareDirectory(Path.Combine(junction, "file"))); }
        finally { Directory.Delete(junction); }
        Assert.Throws<InvalidOperationException>(() => PrivateFileSystem.Open(Path.Combine(root, "target", "file:secret"), FileMode.CreateNew));
    }

    private static void AssertOwnerOnly(string path)
    {
        var owner = WindowsIdentity.GetCurrent().User!;
        var security = new FileInfo(path).GetAccessControl();
        Assert.True(new FileInfo(path).Directory!.GetAccessControl().AreAccessRulesProtected);
        Assert.Equal(owner, security.GetOwner(typeof(SecurityIdentifier)));
        Assert.All(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),
            rule => Assert.Equal(owner, rule.IdentityReference));
        PrivateFileSystem.CheckFile(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
