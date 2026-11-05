using TicketingCRM.Domain.Support.Models;

namespace TicketingCRM.Domain.Persistence.Models;

public sealed class SupportTicketHistory : TenantRecord
{
    public Guid? OperationId { get; set; }
    public string? RequestDigest { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorUserId { get; set; }
    public SupportStage Stage { get; set; }
    public string Message { get; set; } = "";
    public bool IsCustomerVisible { get; set; } = true;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
