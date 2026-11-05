using System.Security.Cryptography;
using System.Text;
using TicketingCRM.Core.Licensing;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Models;
using Xunit;

namespace TicketingCRM.Core.Tests;

public sealed class LicenseArtifactProtectorTests : IDisposable
{
    private readonly RSA issuer = RSA.Create(3072);
    private readonly RSA recipient = RSA.Create(3072);
    private readonly LicenseArtifactProtector codec = new();
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("{\n \"license\": \"é\", \"quantity\": \"9007199254740993\" }");
    private byte[] Protect() => codec.Protect(Payload, issuer.ExportPkcs8PrivateKey(), recipient.ExportSubjectPublicKeyInfo());
    private byte[] Open(byte[] bytes) => codec.Unprotect(bytes, recipient.ExportPkcs8PrivateKey(), issuer.ExportSubjectPublicKeyInfo());

    [Fact] public void RoundtripPreservesExactSignedBytesAndUsesFreshEncryption()
    {
        var first = Protect(); var second = Protect();
        Assert.Equal(Payload, Open(first)); Assert.Equal(Payload, Open(second)); Assert.NotEqual(first, second);
    }
    [Theory] [InlineData(0)] [InlineData(17)] [InlineData(25)] [InlineData(405)] [InlineData(417)] [InlineData(-1)]
    public void TamperedHeaderWrappedKeyNonceTagOrCiphertextIsRejected(int offset)
    {
        var bytes = Protect(); bytes[offset < 0 ? bytes.Length - 1 : offset] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => Open(bytes));
    }
    [Fact] public void WrongRecipientCannotDecrypt()
    {
        using var other = RSA.Create(3072);
        Assert.ThrowsAny<CryptographicException>(() => codec.Unprotect(Protect(), other.ExportPkcs8PrivateKey(), issuer.ExportSubjectPublicKeyInfo()));
    }
    [Fact] public void UntrustedIssuerCannotAuthorizePayload()
    {
        using var other = RSA.Create(3072);
        Assert.ThrowsAny<CryptographicException>(() => codec.Unprotect(Protect(), recipient.ExportPkcs8PrivateKey(), other.ExportSubjectPublicKeyInfo()));
    }
    [Fact] public void TruncatedAndOversizeArtifactsAreRejected()
    {
        var bytes = Protect();
        Assert.ThrowsAny<CryptographicException>(() => Open(bytes[..30]));
        Assert.ThrowsAny<CryptographicException>(() => Open(new byte[LicenseArtifactProtector.MaximumPayloadBytes + 4097]));
    }
    [Fact] public void EmptyAndOversizePayloadsCannotBeIssued()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Protect([], issuer.ExportPkcs8PrivateKey(), recipient.ExportSubjectPublicKeyInfo()));
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Protect(new byte[LicenseArtifactProtector.MaximumPayloadBytes + 1], issuer.ExportPkcs8PrivateKey(), recipient.ExportSubjectPublicKeyInfo()));
    }
    [Fact] public void WeakIssuerAndRecipientKeysAreRejected()
    {
        using var weak = RSA.Create(2048);
        Assert.ThrowsAny<CryptographicException>(() => codec.Protect(Payload, weak.ExportPkcs8PrivateKey(), recipient.ExportSubjectPublicKeyInfo()));
        Assert.ThrowsAny<CryptographicException>(() => codec.Protect(Payload, issuer.ExportPkcs8PrivateKey(), weak.ExportSubjectPublicKeyInfo()));
    }
    [Fact] public void MaximumPayloadRoundtripsAndTrailingKeyBytesAreRejected()
    {
        var payload = RandomNumberGenerator.GetBytes(LicenseArtifactProtector.MaximumPayloadBytes);
        var artifact = codec.Protect(payload, issuer.ExportPkcs8PrivateKey(), recipient.ExportSubjectPublicKeyInfo());
        Assert.Equal(payload, Open(artifact));
        var malformed = recipient.ExportPkcs8PrivateKey().Concat(new byte[] { 0 }).ToArray();
        Assert.ThrowsAny<CryptographicException>(() => codec.Unprotect(artifact, malformed, issuer.ExportSubjectPublicKeyInfo()));
    }
    public void Dispose() { issuer.Dispose(); recipient.Dispose(); }
}

public sealed class LicenseModelCodeGeneratorTests
{
    private static LicenseModelDefinitionDto Model(params LicenseFieldDefinition[] fields) => new(Guid.NewGuid(), Guid.NewGuid(), "Example", fields);
    [Fact] public void ExportsAllFourLanguagesAndManifestWithWireIdentifiers()
    {
        var exports = new LicenseModelCodeGenerator().Export(Model(new LicenseFieldDefinition("class", "Caption", LicenseFieldType.Integer, true, false)));
        Assert.Equal(5, exports.Count);
        foreach (var file in new[] { "LicenseModel.cs", "license-model.js", "LicenseModel.java", "license_model.py", "manifest.json" }) Assert.Contains("class", exports[file]);
        Assert.Contains("Integer", exports["manifest.json"]);
    }
    [Theory] [InlineData("x; process.exit()")] [InlineData("../key")] [InlineData("")]
    public void UnsafeIdentifiersCannotBecomeSourceOrPaths(string name) => Assert.Throws<ArgumentException>(() => new LicenseModelCodeGenerator().Export(Model(new LicenseFieldDefinition(name, "Label", LicenseFieldType.Text, true, false))));
    [Fact] public void DuplicateIdentifiersAreRejected() => Assert.Throws<ArgumentException>(() => new LicenseModelCodeGenerator().Export(Model(new LicenseFieldDefinition("Name", "Label", LicenseFieldType.Text, true, false), new LicenseFieldDefinition("Name", "Other", LicenseFieldType.Text, true, false))));
    [Theory] [InlineData(LicenseFieldType.Object)] [InlineData(LicenseFieldType.List)] [InlineData(LicenseFieldType.Enumeration)]
    public void TypesWithoutSufficientMetadataAreExplicitlyRejected(LicenseFieldType type) => Assert.Throws<NotSupportedException>(() => new LicenseModelCodeGenerator().Export(Model(new LicenseFieldDefinition("Value", "Label", type, false, true))));
}
