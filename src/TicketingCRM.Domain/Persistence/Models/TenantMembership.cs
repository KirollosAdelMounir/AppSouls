namespace TicketingCRM.Domain.Persistence.Models;

public sealed class TenantMembership : TenantRecord
{
    public Guid UserId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
}
