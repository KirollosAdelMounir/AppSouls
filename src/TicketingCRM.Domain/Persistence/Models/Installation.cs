namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Installation : TenantRecord
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public Guid EntitlementId { get; set; }
    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string CredentialHash { get; set; } = "";
    public bool IsRevoked { get; set; }
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
}
