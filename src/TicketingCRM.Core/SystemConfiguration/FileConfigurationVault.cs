using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TicketingCRM.Core.SystemConfiguration.Models;

namespace TicketingCRM.Core.SystemConfiguration;

public sealed class VaultBootstrapOptions
{
    public VaultBootstrapOptions(string vaultPath, string keyPath)
    {
        if (!Path.IsPathFullyQualified(vaultPath) || !Path.IsPathFullyQualified(keyPath))
            throw new ArgumentException("Vault and key locations must be absolute paths.");
        VaultPath = Path.GetFullPath(vaultPath);
        KeyPath = Path.GetFullPath(keyPath);
        if (Path.GetDirectoryName(VaultPath) == Path.GetDirectoryName(KeyPath))
            throw new ArgumentException("The unlock key must have a separate protected directory.");
    }
    public string VaultPath { get; }
    public string KeyPath { get; }
}

/// <summary>Local single-host vault. File permissions protect the external key, not a compromised host administrator.</summary>
public sealed class FileConfigurationVault(VaultBootstrapOptions options)
{
    private const int Limit = 1_048_576;
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("APPSOULS-VAULT-1");
    private const UnixFileMode FileModePrivate = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode DirectoryModePrivate = FileModePrivate | UnixFileMode.UserExecute;

    public async Task InitializeAsync(ServerConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.Validate();
        PrepareDirectory(options.VaultPath);
        PrepareDirectory(options.KeyPath);
        await using var gate = AcquireLock();
        if (File.Exists(options.VaultPath) || File.Exists(options.KeyPath))
            throw new InvalidOperationException("Vault or key already exists. Use explicit recovery; initialization will not replace it.");
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            await WriteNewAsync(options.KeyPath, key, cancellationToken);
            // A failed initialization intentionally preserves its key for explicit operator recovery.
            await AtomicWriteAsync(options.VaultPath, Encrypt(configuration, key), false, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>Explicit authorized recovery when the key was persisted but the first vault write was interrupted.</summary>
    public async Task ResumeInitializationAsync(ServerConfiguration configuration,CancellationToken cancellationToken=default)
    {
        configuration.Validate();PrepareDirectory(options.VaultPath);PrepareDirectory(options.KeyPath);
        await using var gate=AcquireLock();
        if(File.Exists(options.VaultPath))throw new InvalidOperationException("An existing vault cannot be replaced by initialization recovery.");
        var key=await ReadKeyAsync(cancellationToken);
        try{await AtomicWriteAsync(options.VaultPath,Encrypt(configuration,key),false,cancellationToken);}
        finally{CryptographicOperations.ZeroMemory(key);}
    }

    public async Task<ServerConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        CheckFile(options.VaultPath);
        var key = await ReadKeyAsync(cancellationToken);
        try { return Decrypt(await ReadBoundedAsync(options.VaultPath, cancellationToken), key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task SaveAsync(ServerConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.Validate();
        CheckFile(options.VaultPath);
        await using var gate = AcquireLock();
        var key = await ReadKeyAsync(cancellationToken);
        try
        {
            // Refuse to overwrite a corrupted vault or a vault belonging to a different key.
            Decrypt(await ReadBoundedAsync(options.VaultPath, cancellationToken), key);
            await AtomicWriteAsync(options.VaultPath, Encrypt(configuration, key), true, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task ExportRecoveryAsync(string destination, CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken); // Validate before exporting; export contains ciphertext only.
        RequireSeparateRecoveryPath(destination);
        PrepareDirectory(destination);
        await AtomicWriteAsync(destination, await ReadBoundedAsync(options.VaultPath, cancellationToken), false, cancellationToken);
    }

    public async Task RestoreRecoveryAsync(string source, CancellationToken cancellationToken = default)
    {
        RequireSeparateRecoveryPath(source);
        CheckFile(source);
        PrepareDirectory(options.VaultPath);
        await using var gate = AcquireLock();
        var key = await ReadKeyAsync(cancellationToken);
        try
        {
            var bytes = await ReadBoundedAsync(source, cancellationToken);
            Decrypt(bytes, key); // Never replace current state unless the backup authenticates and validates.
            await AtomicWriteAsync(options.VaultPath, bytes, true, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private void RequireSeparateRecoveryPath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) == options.VaultPath || Path.GetFullPath(path) == options.KeyPath)
            throw new ArgumentException("Recovery requires a distinct absolute file location.");
    }

    private static byte[] Encrypt(ServerConfiguration configuration, byte[] key)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(configuration);
        try
        {
            if (plain.Length > Limit - Header.Length - 28) throw new InvalidOperationException("Configuration exceeds the vault size limit.");
            var bytes = new byte[Header.Length + 28 + plain.Length];
            Header.CopyTo(bytes, 0);
            var nonce = bytes.AsSpan(Header.Length, 12);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, bytes.AsSpan(Header.Length + 28), bytes.AsSpan(Header.Length + 12, 16), Header);
            return bytes;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static ServerConfiguration Decrypt(byte[] bytes, byte[] key)
    {
        if (bytes.Length < Header.Length + 28 || !bytes.AsSpan(0, Header.Length).SequenceEqual(Header))
            throw new InvalidOperationException("Unsupported or damaged configuration vault.");
        var plain = new byte[bytes.Length - Header.Length - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(Header.Length, 12), bytes.AsSpan(Header.Length + 28), bytes.AsSpan(Header.Length + 12, 16), plain, Header);
            ServerConfiguration configuration;
            try { configuration = JsonSerializer.Deserialize<ServerConfiguration>(plain) ?? throw new JsonException(); }
            catch (JsonException) { throw new InvalidOperationException("Invalid configuration vault payload."); }
            configuration.Validate();
            return configuration;
        }
        catch (CryptographicException) { throw new InvalidOperationException("Configuration vault authentication failed."); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private async Task<byte[]> ReadKeyAsync(CancellationToken cancellationToken)
    {
        CheckFile(options.KeyPath);
        var bytes = await ReadBoundedAsync(options.KeyPath, cancellationToken);
        if (bytes.Length == 32) return bytes;
        CryptographicOperations.ZeroMemory(bytes);
        throw new InvalidOperationException("The external vault key is invalid.");
    }

    private FileStream AcquireLock()
    {
        var path = options.VaultPath + ".lock";
        if (File.Exists(path)) CheckFile(path);
        return OpenPrivate(path, FileMode.OpenOrCreate);
    }

    private static void EnsurePlatform()
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The file-key vault requires a qualified Windows ACL or native key provider before Windows deployment.");
    }

    private static void CheckAncestors(string path)
    {
        EnsurePlatform();
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Vault locations cannot traverse symbolic links.");
    }

    private static void PrepareDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows vault protection is not yet qualified.");
        CheckAncestors(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory, DirectoryModePrivate);
        if (File.GetUnixFileMode(directory) != DirectoryModePrivate)
            throw new InvalidOperationException("Vault directories must permit access only to their owner (0700).");
    }

    private static void CheckFile(string path)
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows vault protection is not yet qualified.");
        CheckAncestors(path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || File.GetUnixFileMode(path) != FileModePrivate)
            throw new InvalidOperationException("Vault files must be regular owner-only files (0600), without symbolic links.");
        var directory = Path.GetDirectoryName(path)!;
        if (File.GetUnixFileMode(directory) != DirectoryModePrivate)
            throw new InvalidOperationException("Vault directories must permit access only to their owner (0700).");
    }

    private static FileStream OpenPrivate(string path, FileMode mode)
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows vault protection is not yet qualified.");
        return new FileStream(path, new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough, UnixCreateMode = FileModePrivate });
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > Limit) throw new InvalidOperationException("Vault file exceeds the size limit.");
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static async Task WriteNewAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var file = OpenPrivate(path, FileMode.CreateNew);
        await file.WriteAsync(bytes, cancellationToken);
        file.Flush(true);
    }

    private static async Task AtomicWriteAsync(string path, byte[] bytes, bool overwrite, CancellationToken cancellationToken)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await WriteNewAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
