namespace TicketingCRM.Domain.Persistence.Models;
public sealed class CommerceOrder : TenantRecord
{
 public Guid CustomerId { get; set; }
 public Guid? ContractId { get; set; }
 public Guid CreatedByUserId { get; set; }
 public string Reference { get; set; } = "";
 public string Currency { get; set; } = "USD";
 public string Status { get; set; } = "Recorded";
 public string LinesJson { get; set; } = "[]";
 public string Notes { get; set; } = "";
 public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class InvoiceDocument : TenantRecord
{
 public Guid CustomerId { get; set; }
 public Guid? OrderId { get; set; }
 public Guid? ContractId { get; set; }
 public Guid CreatedByUserId { get; set; }
 public string Number { get; set; } = "";
 public string RequestDigest { get; set; } = "";
 public string SnapshotJson { get; set; } = "";
 public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class ProductPricing : TenantRecord
{
 public Guid ProductId { get; set; }
 public string Currency { get; set; } = "USD";
 public decimal UnitPrice { get; set; }
 public string Description { get; set; } = "";
 public Guid UpdatedByUserId { get; set; }
 public Guid? ApprovedByUserId { get; set; }
 public DateTimeOffset? ApprovedAtUtc { get; set; }
}
