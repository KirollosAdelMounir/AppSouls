namespace TicketingCRM.Domain.Persistence.Models;

public sealed class OutboxMessage : TenantRecord
{
    public Guid? TicketId { get; set; }
    public string Kind { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public string? LastError { get; set; }
}
