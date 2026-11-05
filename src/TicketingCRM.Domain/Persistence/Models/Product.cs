namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Product : TenantRecord
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string LicenseExtension { get; set; } = ".license";
    public bool IsActive { get; set; } = true;
}
