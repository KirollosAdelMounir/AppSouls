namespace TicketingCRM.Domain.Persistence.Models;

public sealed class LicenseRevision : TenantRecord
{
    public Guid InstallationId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public Guid EntitlementId { get; set; }
    public Guid SchemaRevisionId { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; } = "Pending";
    public byte[]? Artifact { get; set; }
    public string Extension { get; set; } = ".license";
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
