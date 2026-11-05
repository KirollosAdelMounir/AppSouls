namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Customer : TenantRecord
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string CompanyNumber { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
