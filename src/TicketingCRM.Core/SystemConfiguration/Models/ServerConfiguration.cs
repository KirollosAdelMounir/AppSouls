using System.Security.Cryptography;

namespace TicketingCRM.Core.SystemConfiguration.Models;

public enum DatabaseProvider { PostgreSql, SqlServer }

// Deliberately not a record: generated ToString must never expose credentials.
public sealed class ServerConfiguration
{
    public DatabaseProvider DatabaseProvider { get; init; }
    public string DatabaseConnectionString { get; init; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string LicenseSigningPrivateKeyPem { get; init; } = "";
    public string DataProtectionKeyDirectory { get; init; } = "";
    public Dictionary<string, IntegrationConfiguration> Integrations { get; set; } = new();
    public SmtpConfiguration Smtp { get; set; } = new();

    public void Validate()
    {
        if (!Enum.IsDefined(DatabaseProvider) || string.IsNullOrWhiteSpace(DatabaseConnectionString))
            throw new InvalidOperationException("A supported database provider and connection are required.");
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("The public base address must be an absolute HTTPS address without credentials.");
        if (!string.IsNullOrEmpty(DataProtectionKeyDirectory) && !Path.IsPathFullyQualified(DataProtectionKeyDirectory))
            throw new InvalidOperationException("Data protection keys require an absolute protected directory.");
        if (!string.IsNullOrWhiteSpace(LicenseSigningPrivateKeyPem))
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(LicenseSigningPrivateKeyPem);
                if (rsa.KeySize < 3072) throw new CryptographicException();
                var privateBytes = rsa.ExportRSAPrivateKey();
                CryptographicOperations.ZeroMemory(privateBytes);
            }
            catch (Exception exception) when (exception is ArgumentException or CryptographicException)
            {
                throw new InvalidOperationException("License signing requires a valid RSA private key of at least 3072 bits.");
            }
        }
        if (Smtp is null || Smtp.Port is < 1 or > 65535 || (Smtp.Enabled &&
            (string.IsNullOrWhiteSpace(Smtp.Host) || string.IsNullOrWhiteSpace(Smtp.FromAddress) || !Smtp.RequireTls)))
            throw new InvalidOperationException("Enabled email requires a host, sender, valid port and TLS.");
    }
}

public sealed class SmtpConfiguration
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string FromAddress { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public bool RequireTls { get; init; } = true;
}

public sealed class IntegrationConfiguration
{
    public string BaseUrl { get; init; } = "";
    public Dictionary<string,string> Credentials { get; init; } = new();
}
