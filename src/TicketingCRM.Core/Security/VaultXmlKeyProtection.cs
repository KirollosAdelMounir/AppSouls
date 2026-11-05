using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.Core.SystemConfiguration;

namespace TicketingCRM.Core.Security;

/// <summary>Encrypts authentication key XML with purpose-separated key material derived from the external vault key.</summary>
public sealed class VaultXmlKeyEncryptor(VaultBootstrapOptions bootstrap) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var key = VaultXmlKeyMaterial.Read(bootstrap);
        try
        {
            if (plaintext.Length > VaultXmlKeyMaterial.Limit) throw new InvalidOperationException("Authentication key data exceeds its size limit.");
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, VaultXmlKeyMaterial.Context);
            var encrypted = new XElement("appsouls-key", new XAttribute("version", "1"),
                new XElement("nonce", Convert.ToBase64String(nonce)), new XElement("tag", Convert.ToBase64String(tag)),
                new XElement("ciphertext", Convert.ToBase64String(ciphertext)));
            return new EncryptedXmlInfo(encrypted, typeof(VaultXmlKeyDecryptor));
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); CryptographicOperations.ZeroMemory(key); }
    }
}

public sealed class VaultXmlKeyDecryptor : IXmlDecryptor
{
    private readonly VaultBootstrapOptions bootstrap;
    // Data Protection activates decryptors using its IServiceProvider constructor contract.
    public VaultXmlKeyDecryptor(IServiceProvider services) => bootstrap = services.GetRequiredService<VaultBootstrapOptions>();
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        if (encryptedElement.Name != "appsouls-key" || (string?)encryptedElement.Attribute("version") != "1" ||
            encryptedElement.ToString(SaveOptions.DisableFormatting).Length > VaultXmlKeyMaterial.Limit * 2)
            throw new InvalidOperationException("Unsupported authentication key envelope.");
        byte[]? plaintext = null;
        var key = VaultXmlKeyMaterial.Read(bootstrap);
        try
        {
            var nonce = Convert.FromBase64String(encryptedElement.Element("nonce")?.Value ?? "");
            var tag = Convert.FromBase64String(encryptedElement.Element("tag")?.Value ?? "");
            var ciphertext = Convert.FromBase64String(encryptedElement.Element("ciphertext")?.Value ?? "");
            if (nonce.Length != 12 || tag.Length != 16 || ciphertext.Length > VaultXmlKeyMaterial.Limit)
                throw new FormatException();
            plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, VaultXmlKeyMaterial.Context);
            using var stream = new MemoryStream(plaintext, false);
            using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = VaultXmlKeyMaterial.Limit
            });
            return XElement.Load(reader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or System.Xml.XmlException)
        { throw new InvalidOperationException("Authentication key envelope could not be authenticated."); }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }
}

internal static class VaultXmlKeyMaterial
{
    internal const int Limit = 1_048_576;
    internal static readonly byte[] Context = Encoding.ASCII.GetBytes("AppSouls:AuthenticationKeyRing:AES256GCM:v1");
    internal static byte[] Read(VaultBootstrapOptions bootstrap)
    {
        var path = bootstrap.KeyPath;
        PrivateFileSystem.CheckFile(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length != 32) throw new InvalidOperationException("The external protection key is invalid.");
        var master = new byte[32];
        try
        {
            file.ReadExactly(master);
            return HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, salt: Encoding.ASCII.GetBytes("AppSouls:HKDF:v1"), info: Context);
        }
        finally { CryptographicOperations.ZeroMemory(master); }
    }
}
