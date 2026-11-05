namespace TicketingCRM.Domain.Security.Interfaces;

public interface IContentHasher
{
    string ComputeSha256(ReadOnlySpan<byte> content);
}
