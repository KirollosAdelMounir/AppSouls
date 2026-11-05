using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TicketingCRM.Core.Licensing;

/// <summary>
/// Version-one license protection. Keys are supplied by the calling secret-store adapter.
/// This codec authenticates bytes; business scope, dates and entitlements require separate validation.
/// </summary>
public sealed class LicenseArtifactProtector
{
    public const int MaximumPayloadBytes = 1024 * 1024;
    public const string Profile = "rsa-oaep-sha256+a256gcm+rsa-pss-sha256/v1";
    private static ReadOnlySpan<byte> Header => "AppSoulsLicense\0\x01"u8;
    private static ReadOnlySpan<byte> SignatureContext => "AppSoulsLicensePayload/v1\0"u8;

    public byte[] Protect(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> issuerPkcs8, ReadOnlySpan<byte> recipientSpki)
    {
        if (payload.IsEmpty || payload.Length > MaximumPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(payload));
        using var issuer = RSA.Create();
        issuer.ImportPkcs8PrivateKey(issuerPkcs8, out var issuerRead);
        if (issuerRead != issuerPkcs8.Length) throw new CryptographicException("Invalid issuer key.");
        using var recipient = RSA.Create();
        recipient.ImportSubjectPublicKeyInfo(recipientSpki, out var recipientRead);
        if (recipientRead != recipientSpki.Length) throw new CryptographicException("Invalid recipient key.");
        RequireKey(issuer); RequireKey(recipient);
        var signature = issuer.SignData(SignatureInput(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var plaintext = new byte[4 + signature.Length + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(plaintext, signature.Length);
        signature.CopyTo(plaintext, 4);
        payload.CopyTo(plaintext.AsSpan(4 + signature.Length));
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var wrapped = recipient.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
            var prefixLength = Header.Length + 4 + wrapped.Length + 12 + 16;
            var result = new byte[prefixLength + plaintext.Length];
            Header.CopyTo(result);
            BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(Header.Length), wrapped.Length);
            wrapped.CopyTo(result, Header.Length + 4);
            var nonce = result.AsSpan(Header.Length + 4 + wrapped.Length, 12);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, result.AsSpan(prefixLength), result.AsSpan(prefixLength - 16, 16), result.AsSpan(0, prefixLength - 28));
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
    }

    public byte[] Unprotect(ReadOnlySpan<byte> artifact, ReadOnlySpan<byte> recipientPkcs8, ReadOnlySpan<byte> trustedIssuerSpki)
    {
        if (artifact.Length < Header.Length + 4 + 384 + 28 + 4 + 384 + 1 ||
            artifact.Length > MaximumPayloadBytes + 4096 || !artifact[..Header.Length].SequenceEqual(Header))
            throw new CryptographicException("Unsupported or malformed license artifact.");
        var wrappedLength = BinaryPrimitives.ReadInt32BigEndian(artifact.Slice(Header.Length, 4));
        if (wrappedLength is < 384 or > 1024 || wrappedLength % 128 != 0)
            throw new CryptographicException("Invalid key envelope.");
        var prefixLength = Header.Length + 4 + wrappedLength + 28;
        if (artifact.Length <= prefixLength + 4) throw new CryptographicException("Invalid artifact length.");
        using var recipient = RSA.Create();
        recipient.ImportPkcs8PrivateKey(recipientPkcs8, out var recipientRead);
        using var issuer = RSA.Create();
        issuer.ImportSubjectPublicKeyInfo(trustedIssuerSpki, out var issuerRead);
        if (recipientRead != recipientPkcs8.Length || issuerRead != trustedIssuerSpki.Length)
            throw new CryptographicException("Invalid key encoding.");
        RequireKey(recipient); RequireKey(issuer);
        if (wrappedLength != recipient.KeySize / 8) throw new CryptographicException("Invalid recipient key.");
        var key = recipient.Decrypt(artifact.Slice(Header.Length + 4, wrappedLength), RSAEncryptionPadding.OaepSHA256);
        var plaintext = new byte[artifact.Length - prefixLength];
        try
        {
            if (key.Length != 32) throw new CryptographicException("Invalid content key.");
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(artifact.Slice(prefixLength - 28, 12), artifact[prefixLength..], artifact.Slice(prefixLength - 16, 16), plaintext, artifact[..(prefixLength - 28)]);
            var signatureLength = BinaryPrimitives.ReadInt32BigEndian(plaintext);
            if (signatureLength != issuer.KeySize / 8 || plaintext.Length <= 4 + signatureLength || plaintext.Length - 4 - signatureLength > MaximumPayloadBytes)
                throw new CryptographicException("Invalid signed payload.");
            var payload = plaintext.AsSpan(4 + signatureLength);
            if (!issuer.VerifyData(SignatureInput(payload), plaintext.AsSpan(4, signatureLength), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new CryptographicException("License signature verification failed.");
            return payload.ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static byte[] SignatureInput(ReadOnlySpan<byte> payload)
    {
        var input = new byte[SignatureContext.Length + payload.Length];
        SignatureContext.CopyTo(input); payload.CopyTo(input.AsSpan(SignatureContext.Length)); return input;
    }
    private static void RequireKey(RSA key)
    {
        if (key.KeySize is not (3072 or 4096 or 8192))
            throw new CryptographicException("RSA key size must be 3072, 4096 or 8192 bits.");
    }
}
