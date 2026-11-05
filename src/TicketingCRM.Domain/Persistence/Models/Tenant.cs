namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Tenant : TenantRecord
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
