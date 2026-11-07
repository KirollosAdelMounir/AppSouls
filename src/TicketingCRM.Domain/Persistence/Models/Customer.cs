namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Customer : TenantRecord
{
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string CompanyNumber { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
