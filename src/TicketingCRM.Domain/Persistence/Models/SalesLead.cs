using TicketingCRM.Domain.Sales.Models;
namespace TicketingCRM.Domain.Persistence.Models;
public sealed class SalesLead : TenantRecord
{
    public string ContactName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
    public LeadStage Stage { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTimeOffset? FollowUpAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid CreationOperationId { get; set; }
    public string CreationDigest { get; set; } = "";
    public Guid? CustomerId { get; set; }
    public Guid? ConversionOperationId { get; set; }
    public string? ConversionDigest { get; set; }
}
