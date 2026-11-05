namespace TicketingCRM.Domain.Persistence.Models;

public abstract class TenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
