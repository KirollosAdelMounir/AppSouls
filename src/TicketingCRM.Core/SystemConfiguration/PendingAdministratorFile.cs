using TicketingCRM.Core.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Core.SystemConfiguration;

/// <summary>Encrypted, bounded setup material. It contains an Identity password hash, never the password.</summary>
public sealed class PendingAdministratorFile(VaultBootstrapOptions bootstrap)
{
    private static readonly byte[] Purpose = Encoding.ASCII.GetBytes("AppSouls:PendingAdministrator:v1");
    private const int Limit = 16384;
    public string PathName => bootstrap.VaultPath + ".administrator.pending";
    public string KeyPath => bootstrap.KeyPath + ".administrator.key";
    public bool Exists => File.Exists(PathName);
    public FileStream AcquireSetupLock()
    {
        PreparePrivateDirectory(PathName);
        var path = PathName + ".operation.lock"; if (File.Exists(path)) PrivateFileSystem.CheckFile(path);
        return PrivateFileSystem.Open(path, FileMode.OpenOrCreate);
    }
    public static void PreparePrivateDirectory(string path) => PrivateFileSystem.PrepareDirectory(path);
    public async Task SaveAsync(PreparedAdministrator administrator, CancellationToken cancellationToken = default)
    {
        PreparePrivateDirectory(PathName); PreparePrivateDirectory(KeyPath);
        var gatePath = PathName + ".lock"; if (File.Exists(gatePath)) PrivateFileSystem.CheckFile(gatePath);
        await using var gate = PrivateFileSystem.Open(gatePath, FileMode.OpenOrCreate);
        byte[] key;
        if (File.Exists(KeyPath)) { PrivateFileSystem.CheckFile(KeyPath); if (new FileInfo(KeyPath).Length != 32) throw new InvalidOperationException("Invalid pending administrator key."); key = await File.ReadAllBytesAsync(KeyPath, cancellationToken); }
        else
        {
            key = RandomNumberGenerator.GetBytes(32);
            await using var keyFile = PrivateFileSystem.Open(KeyPath, FileMode.CreateNew); await keyFile.WriteAsync(key, cancellationToken); keyFile.Flush(true);
        }
        if (key.Length != 32) { CryptographicOperations.ZeroMemory(key); throw new InvalidOperationException("Invalid pending administrator key."); }
        var plain = JsonSerializer.SerializeToUtf8Bytes(administrator); var temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (plain.Length > Limit) throw new InvalidOperationException("Administrator setup data exceeds its limit.");
            var bytes = new byte[28 + plain.Length]; RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
            using var aes = new AesGcm(key, 16); aes.Encrypt(bytes.AsSpan(0, 12), plain, bytes.AsSpan(28), bytes.AsSpan(12, 16), Purpose);
            await using (var file = PrivateFileSystem.Open(temporary, FileMode.CreateNew)) { await file.WriteAsync(bytes, cancellationToken); file.Flush(true); }
            if (File.Exists(PathName)) PrivateFileSystem.CheckFile(PathName);
            File.Move(temporary, PathName, true);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task<PreparedAdministrator> LoadAsync(CancellationToken cancellationToken = default, bool allowExpired = false)
    {
        PrivateFileSystem.CheckFile(PathName); PrivateFileSystem.CheckFile(KeyPath);
        if (new FileInfo(PathName).Length is < 28 or > Limit + 28 || new FileInfo(KeyPath).Length != 32) throw new InvalidOperationException("Invalid pending administrator file.");
        var bytes = await File.ReadAllBytesAsync(PathName, cancellationToken); var key = await File.ReadAllBytesAsync(KeyPath, cancellationToken); var plain = new byte[bytes.Length - 28];
        try
        {
            using var aes = new AesGcm(key, 16); aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain, Purpose);
            var value = JsonSerializer.Deserialize<PreparedAdministrator>(plain) ?? throw new InvalidOperationException("Invalid prepared administrator.");
            if (!allowExpired && value.ExpiresAtUtc <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Prepared administrator expired. Prepare it again before migration.");
            return value;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public void Complete()
    {
        if (File.Exists(PathName)) { PrivateFileSystem.CheckFile(PathName); File.Delete(PathName); }
        if (File.Exists(KeyPath)) { PrivateFileSystem.CheckFile(KeyPath); File.Delete(KeyPath); }
        var gate = PathName + ".lock"; if (File.Exists(gate)) { PrivateFileSystem.CheckFile(gate); File.Delete(gate); }
    }
}
