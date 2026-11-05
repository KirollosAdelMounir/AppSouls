using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.DataConnection.DbContexts;
public partial class AppDbContext
{
 public DbSet<CustomerActivity> CustomerActivities=>Set<CustomerActivity>();
 public DbSet<CustomerInvitation> CustomerInvitations=>Set<CustomerInvitation>();
 public DbSet<ProductRelease> ProductReleases=>Set<ProductRelease>();
 private void ConfigureCustomerDelivery(ModelBuilder model)
 {
  ConfigureTenant(model.Entity<CustomerActivity>());ConfigureTenant(model.Entity<CustomerInvitation>());ConfigureTenant(model.Entity<ProductRelease>());
  var a=model.Entity<CustomerActivity>();a.Property(x=>x.Kind).HasMaxLength(30);a.Property(x=>x.Subject).HasMaxLength(200);a.Property(x=>x.Notes).HasMaxLength(4000);
  a.HasIndex(x=>new{x.TenantId,x.CustomerId,x.OccurredAtUtc});
  a.HasOne<Customer>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  a.HasOne<TenantMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ActorUserId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
  var i=model.Entity<CustomerInvitation>();i.Property(x=>x.TokenHash).HasMaxLength(64);i.Property(x=>x.SecurityStamp).HasMaxLength(128);
  i.HasIndex(x=>new{x.TenantId,x.UserId}).IsUnique();i.HasOne<Customer>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  model.Entity<SalesOpportunity>().HasAlternateKey(x=>new{x.TenantId,x.CustomerId,x.Id});
  i.HasOne<TenantMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.UserId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
  i.HasOne<SalesOpportunity>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.OpportunityId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  i.HasOne<Contract>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.ContractId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  i.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  i.HasOne<TenantMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CreatedByUserId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
  var r=model.Entity<ProductRelease>();r.Property(x=>x.ReleaseVersion).HasMaxLength(80);r.Property(x=>x.Platform).HasMaxLength(30);r.Property(x=>x.Filename).HasMaxLength(160);r.Property(x=>x.Notes).HasMaxLength(8000);r.Property(x=>x.Sha256).HasMaxLength(64);r.Property(x=>x.Status).HasMaxLength(30);
  r.HasIndex(x=>new{x.TenantId,x.ProductId,x.ReleaseVersion,x.Platform}).IsUnique();
  r.HasOne<TenantMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.PublishedByUserId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
  r.HasOne<TenantMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.RevokedByUserId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
  r.HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProductId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  r.HasOne<TenantMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.UploadedByUserId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
 }
}
