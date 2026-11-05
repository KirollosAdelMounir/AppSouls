using TicketingCRM.Domain.Sales.Models;
namespace TicketingCRM.Domain.Persistence.Models;
public sealed class SalesOpportunity : TenantRecord
{
    public string Title { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Guid? SourceLeadId { get; set; }
    public OpportunityStage Stage { get; set; }
    public Guid OwnerUserId { get; set; }
    public decimal ExpectedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset? FollowUpAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid CreationOperationId { get; set; }
    public string CreationDigest { get; set; } = "";
}
