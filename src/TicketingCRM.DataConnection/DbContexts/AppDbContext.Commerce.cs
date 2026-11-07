using Microsoft.EntityFrameworkCore;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.DataConnection.DbContexts;
public partial class AppDbContext
{
 private void ConfigureCommerce(ModelBuilder model)
 {
  ConfigureTenant(model.Entity<CommerceOrder>());ConfigureTenant(model.Entity<InvoiceDocument>());ConfigureTenant(model.Entity<ProductPricing>());
  var order=model.Entity<CommerceOrder>();order.Property(x=>x.Reference).HasMaxLength(100);order.Property(x=>x.Currency).HasMaxLength(3);order.Property(x=>x.Status).HasMaxLength(30);order.Property(x=>x.LinesJson).HasMaxLength(64000);order.Property(x=>x.Notes).HasMaxLength(4000);
  order.HasIndex(x=>new{x.TenantId,x.Reference}).IsUnique();order.HasIndex(x=>new{x.TenantId,x.CustomerId,x.CreatedAtUtc});order.HasAlternateKey(x=>new{x.TenantId,x.CustomerId,x.Id});
  order.HasOne<Customer>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  order.HasOne<Contract>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.ContractId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  var invoice=model.Entity<InvoiceDocument>();invoice.Property(x=>x.Number).HasMaxLength(100);invoice.Property(x=>x.RequestDigest).HasMaxLength(64);invoice.Property(x=>x.SnapshotJson).HasMaxLength(128000);invoice.HasIndex(x=>new{x.TenantId,x.Number}).IsUnique();invoice.HasIndex(x=>new{x.TenantId,x.CustomerId,x.CreatedAtUtc});
  invoice.HasOne<Customer>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  invoice.HasOne<CommerceOrder>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.OrderId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  invoice.HasOne<Contract>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.ContractId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  var price=model.Entity<ProductPricing>();price.HasIndex(x=>new{x.TenantId,x.ProductId}).IsUnique();price.Property(x=>x.UnitPrice).HasPrecision(18,2);price.Property(x=>x.Currency).HasMaxLength(3);price.Property(x=>x.Description).HasMaxLength(8000);
  price.HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProductId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  model.Entity<Entitlement>().HasIndex(x=>new{x.TenantId,x.CustomerId,x.ProductId,x.ExpiresAtUtc});
  model.Entity<Contract>().HasIndex(x=>new{x.TenantId,x.CustomerId,x.StartsAtUtc});
  model.Entity<ProductRelease>().Property(x=>x.StoreUrl).HasMaxLength(2048);
  model.Entity<ProductRelease>().HasIndex(x=>new{x.TenantId,x.Status,x.PublishedAtUtc});
 }
}
