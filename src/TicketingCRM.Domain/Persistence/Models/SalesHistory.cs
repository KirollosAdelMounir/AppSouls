namespace TicketingCRM.Domain.Persistence.Models;
public sealed class SalesHistory : TenantRecord
{
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid OperationId { get; set; }
    public string RequestDigest { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string FromStage { get; set; } = "";
    public string ToStage { get; set; } = "";
    public string Reason { get; set; } = "";
}
