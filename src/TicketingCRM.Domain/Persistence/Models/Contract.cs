namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Contract : TenantRecord
{
    public Guid CustomerId { get; set; }
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
}
