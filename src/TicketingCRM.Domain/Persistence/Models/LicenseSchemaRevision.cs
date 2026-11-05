namespace TicketingCRM.Domain.Persistence.Models;

public sealed class LicenseSchemaRevision : TenantRecord
{
    public Guid ProductId { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public string SchemaJson { get; set; } = "{}";
    public string Extension { get; set; } = ".license";
    public string EncryptionProfile { get; set; } = "AES-256-GCM";
    public bool IsPublished { get; set; }
}
