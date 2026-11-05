namespace TicketingCRM.Domain.Persistence.Models;

public sealed class Entitlement : TenantRecord
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ContractId { get; set; }
    public int EnrollmentCounter { get; set; }
    public int InstallationLimit { get; set; } = 1;
    public string FeaturesJson { get; set; } = "{}";
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
