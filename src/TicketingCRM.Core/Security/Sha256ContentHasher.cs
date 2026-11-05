using System.Security.Cryptography;
using TicketingCRM.Domain.Security.Interfaces;

namespace TicketingCRM.Core.Security;

internal sealed class Sha256ContentHasher : IContentHasher
{
    public string ComputeSha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));
}
