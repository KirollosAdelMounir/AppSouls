using TicketingCRM.Core.Security;
using System.Security.Cryptography;
using System.Text.Json;

namespace TicketingCRM.Core.SystemConfiguration;

/// <summary>Operator-controlled, short-lived capability kept outside web content beside the protected bootstrap key.</summary>
public sealed class InitialSetupAuthorization(VaultBootstrapOptions options)
{
    public string TokenPath => options.KeyPath + ".setup";
    public async Task EnsurePreparedAsync(CancellationToken cancellationToken = default)
    {
        CheckDirectory();
        if (File.Exists(TokenPath))
        {
            var info = new FileInfo(TokenPath);
            if (info.LinkTarget is not null || info.Length > 1024)
                throw new InvalidOperationException("Invalid setup authorization file.");
            PrivateFileSystem.CheckFile(TokenPath);
            var value = JsonSerializer.Deserialize<SetupCapability>(await File.ReadAllTextAsync(TokenPath, cancellationToken));
            if (value is null) throw new InvalidOperationException("Invalid setup authorization file.");
            if (value.ExpiresAtUtc > DateTimeOffset.UtcNow) return;
            File.Delete(TokenPath);
        }
        await PrepareAsync(cancellationToken);
    }
    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        CheckDirectory();
        var input = new SetupCapability { Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
        await using var stream = PrivateFileSystem.Open(TokenPath, FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(stream, input, cancellationToken: cancellationToken);
    }
    public async Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64 || !File.Exists(TokenPath)) return false;
        CheckDirectory();
        var info = new FileInfo(TokenPath);
        if (info.LinkTarget is not null || info.Length > 1024) throw new InvalidOperationException("Invalid setup authorization file.");
        PrivateFileSystem.CheckFile(TokenPath);
        var capability = JsonSerializer.Deserialize<SetupCapability>(await File.ReadAllTextAsync(TokenPath, cancellationToken));
        if (capability is null || capability.ExpiresAtUtc <= DateTimeOffset.UtcNow) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(token), Convert.FromHexString(capability.Token)); }
        catch (FormatException) { return false; }
    }
    public void Complete() { if (File.Exists(TokenPath)) File.Delete(TokenPath); }
    private void CheckDirectory()
    {
        var directory = Path.GetDirectoryName(TokenPath)!;
        // The vault initializes this directory. Setup cannot create an unprotected bootstrap location.
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.LinkTarget is not null) throw new InvalidOperationException("Setup paths cannot contain symbolic links.");
        if (!Directory.Exists(directory)) throw new InvalidOperationException("Initialize the server vault first.");
        PrivateFileSystem.PrepareDirectory(TokenPath);
    }
    private sealed class SetupCapability
    {
        public string Token { get; set; } = "";
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
