using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;

namespace TicketingCRM.DataConnection.DbContexts;

public partial class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly Guid _tenantId;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : base(options)
    {
        _tenantId = tenantContext?.TenantId ?? Guid.Empty;
    }
    public DbSet<SalesLead> SalesLeads => Set<SalesLead>();
    public DbSet<SalesOpportunity> SalesOpportunities => Set<SalesOpportunity>();
    public DbSet<SalesHistory> SalesHistories => Set<SalesHistory>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<LicenseRevision> LicenseRevisions => Set<LicenseRevision>();
    public DbSet<LicenseSchemaRevision> LicenseSchemaRevisions => Set<LicenseSchemaRevision>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketHistory> SupportTicketHistories => Set<SupportTicketHistory>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        ConfigureTenant(model.Entity<SalesLead>());
        ConfigureTenant(model.Entity<SalesOpportunity>());
        ConfigureTenant(model.Entity<SalesHistory>());
        ConfigureSales(model);
        ConfigureCustomerDelivery(model);
        ConfigureTenant(model.Entity<Contract>());
        ConfigureTenant(model.Entity<Customer>());
        ConfigureTenant(model.Entity<Entitlement>());
        ConfigureTenant(model.Entity<Installation>());
        ConfigureTenant(model.Entity<LicenseRevision>());
        ConfigureTenant(model.Entity<LicenseSchemaRevision>());
        ConfigureTenant(model.Entity<OutboxMessage>());
        ConfigureTenant(model.Entity<Product>());
        ConfigureTenant(model.Entity<SupportTicket>());
        ConfigureTenant(model.Entity<SupportTicketHistory>());
        ConfigureTenant(model.Entity<Tenant>());
        ConfigureTenant(model.Entity<TenantMembership>());

        model.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(200);
        model.Entity<TenantMembership>().HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        model.Entity<TenantMembership>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Customer>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<Customer>().Property(x => x.Email).HasMaxLength(320);
        model.Entity<Product>().Property(x => x.Code).HasMaxLength(80);
        model.Entity<Product>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        model.Entity<Contract>().Property(x => x.Reference).HasMaxLength(100);
        model.Entity<Contract>().HasIndex(x => new { x.TenantId, x.Reference }).IsUnique();
        model.Entity<SupportTicket>().Property(x => x.Reference).HasMaxLength(100);
        model.Entity<SupportTicket>().Property(x => x.Title).HasMaxLength(300);
        model.Entity<SupportTicket>().HasIndex(x => new { x.TenantId, x.Reference }).IsUnique();
        model.Entity<SupportTicket>().HasIndex(x => new { x.TenantId, x.ReporterUserId, x.OperationId }).IsUnique();
        model.Entity<OutboxMessage>().Property(x => x.IdempotencyKey).HasMaxLength(200);
        model.Entity<OutboxMessage>().HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        model.Entity<LicenseSchemaRevision>().HasIndex(x => new { x.TenantId, x.ProductId, x.Revision }).IsUnique();
        model.Entity<Installation>().Property(x => x.CredentialHash).HasMaxLength(64);
        model.Entity<Installation>().HasIndex(x => x.CredentialHash).IsUnique();
        model.Entity<LicenseRevision>().HasIndex(x => new { x.TenantId, x.InstallationId, x.Revision }).IsUnique();
        model.Entity<Installation>().HasAlternateKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.Id });
        model.Entity<LicenseRevision>().HasOne<Installation>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.InstallationId }).HasPrincipalKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<TenantMembership>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Contract>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Entitlement>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Entitlement>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Entitlement>().HasOne<Contract>().WithMany().HasForeignKey(x => new { x.TenantId, x.ContractId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SupportTicket>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SupportTicket>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SupportTicketHistory>().Property(x => x.RequestDigest).HasMaxLength(64);
        model.Entity<SupportTicketHistory>().HasIndex(x => new { x.TenantId, x.ActorUserId, x.OperationId }).IsUnique().HasFilter(Database.IsSqlServer()?"[OperationId] IS NOT NULL":"\"OperationId\" IS NOT NULL");
        model.Entity<SupportTicketHistory>().HasOne<SupportTicket>().WithMany().HasForeignKey(x => new { x.TenantId, x.TicketId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OutboxMessage>().HasOne<SupportTicket>().WithMany().HasForeignKey(x => new { x.TenantId, x.TicketId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LicenseSchemaRevision>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LicenseRevision>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LicenseRevision>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LicenseRevision>().HasOne<Entitlement>().WithMany().HasForeignKey(x => new { x.TenantId, x.EntitlementId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LicenseRevision>().HasOne<LicenseSchemaRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.SchemaRevisionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Installation>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Installation>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Installation>().HasOne<Entitlement>().WithMany().HasForeignKey(x => new { x.TenantId, x.EntitlementId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        // A customer-specific entitlement cannot reference another customer's contract.
        model.Entity<Contract>().HasAlternateKey(x => new { x.TenantId, x.CustomerId, x.Id });
        model.Entity<Entitlement>().HasOne<Contract>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId, x.ContractId }).HasPrincipalKey(x => new { x.TenantId, x.CustomerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Entitlement>().HasAlternateKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.Id });
        model.Entity<LicenseRevision>().HasOne<Entitlement>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.EntitlementId }).HasPrincipalKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Installation>().HasOne<Entitlement>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.EntitlementId }).HasPrincipalKey(x => new { x.TenantId, x.CustomerId, x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSales(ModelBuilder model)
    {
        var lead = model.Entity<SalesLead>();
        lead.Property(x => x.ContactName).HasMaxLength(160); lead.Property(x => x.CompanyName).HasMaxLength(160);
        lead.Property(x => x.Email).HasMaxLength(254); lead.Property(x => x.Phone).HasMaxLength(50);
        lead.Property(x => x.Source).HasMaxLength(100); lead.Property(x => x.Notes).HasMaxLength(4000);
        lead.Property(x => x.CreationDigest).HasMaxLength(64); lead.Property(x => x.ConversionDigest).HasMaxLength(64);
        lead.HasIndex(x => new { x.TenantId, x.CreatedByUserId, x.CreationOperationId }).IsUnique();
        lead.HasIndex(x => new { x.TenantId, x.CreatedAtUtc, x.Id });
        lead.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        lead.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.OwnerUserId }).HasPrincipalKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        lead.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var opportunity = model.Entity<SalesOpportunity>();
        opportunity.Property(x => x.Title).HasMaxLength(200); opportunity.Property(x => x.Currency).HasMaxLength(3);
        opportunity.Property(x => x.ExpectedValue).HasPrecision(18, 2); opportunity.Property(x => x.CreationDigest).HasMaxLength(64);
        opportunity.HasIndex(x => new { x.TenantId, x.CreatedByUserId, x.CreationOperationId }).IsUnique();
        opportunity.HasIndex(x => new { x.TenantId, x.SourceLeadId }).IsUnique().HasFilter(Database.IsSqlServer()?"[SourceLeadId] IS NOT NULL":"\"SourceLeadId\" IS NOT NULL");
        opportunity.HasIndex(x => new { x.TenantId, x.CreatedAtUtc, x.Id });
        opportunity.HasOne<SalesLead>().WithMany().HasForeignKey(x => new { x.TenantId, x.SourceLeadId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        opportunity.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        opportunity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.OwnerUserId }).HasPrincipalKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        opportunity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var history = model.Entity<SalesHistory>();
        history.Property(x => x.FromStage).HasMaxLength(40); history.Property(x => x.ToStage).HasMaxLength(40);
        history.Property(x => x.Reason).HasMaxLength(2000); history.Property(x => x.RequestDigest).HasMaxLength(64);
        history.HasOne<SalesLead>().WithMany().HasForeignKey(x => new { x.TenantId, x.LeadId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        history.HasOne<SalesOpportunity>().WithMany().HasForeignKey(x => new { x.TenantId, x.OpportunityId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        history.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.ActorUserId }).HasPrincipalKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        history.HasIndex(x => new { x.TenantId, x.ActorUserId, x.LeadId, x.OperationId }).IsUnique().HasFilter(Database.IsSqlServer()?"[LeadId] IS NOT NULL":"\"LeadId\" IS NOT NULL");
        history.HasIndex(x => new { x.TenantId, x.ActorUserId, x.OpportunityId, x.OperationId }).IsUnique().HasFilter(Database.IsSqlServer()?"[OpportunityId] IS NOT NULL":"\"OpportunityId\" IS NOT NULL");
        history.ToTable(t => t.HasCheckConstraint("CK_SalesHistory_Subject", Database.IsSqlServer()
            ? "([LeadId] IS NOT NULL AND [OpportunityId] IS NULL) OR ([LeadId] IS NULL AND [OpportunityId] IS NOT NULL)"
            : "(\"LeadId\" IS NOT NULL AND \"OpportunityId\" IS NULL) OR (\"LeadId\" IS NULL AND \"OpportunityId\" IS NOT NULL)"));
    }

    private void ConfigureTenant<TEntity>(EntityTypeBuilder<TEntity> entity) where TEntity : TenantRecord
    {
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasQueryFilter(x => _tenantId != Guid.Empty && x.TenantId == _tenantId);
        if (typeof(TEntity) != typeof(Tenant))
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceTenantWrites()
    {
        foreach (var entry in ChangeTracker.Entries<TenantRecord>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (_tenantId == Guid.Empty || entry.Entity.TenantId != _tenantId)
                throw new UnauthorizedAccessException("A valid tenant scope is required for this operation.");
            if (entry.State != EntityState.Added && entry.Property(x => x.TenantId).OriginalValue != _tenantId)
                throw new UnauthorizedAccessException("Changing tenant ownership is forbidden.");
            if (entry.Entity is Tenant && entry.Entity.Id != _tenantId)
                throw new InvalidOperationException("Tenant identity must match its scope.");
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if(entry.Entity is ProductRelease && entry.OriginalValues.GetValue<string>(nameof(ProductRelease.Status)) is "Published" or "Revoked")
                {
                    if(entry.OriginalValues.GetValue<string>(nameof(ProductRelease.Status))=="Revoked"||entry.State==EntityState.Deleted||entry.Properties.Any(p=>p.IsModified&&p.Metadata.Name!=nameof(ProductRelease.Status)&&p.Metadata.Name!=nameof(ProductRelease.RevokedByUserId)&&p.Metadata.Name!=nameof(ProductRelease.RevokedAtUtc))||entry.Entity is ProductRelease release&&release.Status!="Revoked")throw new InvalidOperationException("Published release metadata is immutable; only revocation is allowed.");
                }
                if (entry.Entity is CustomerActivity) throw new InvalidOperationException("Customer activities are immutable.");
                if (entry.Entity is SalesHistory) throw new InvalidOperationException("Sales history is immutable.");
                if (entry.Entity is LicenseSchemaRevision && entry.OriginalValues.GetValue<bool>(nameof(LicenseSchemaRevision.IsPublished)))
                    throw new InvalidOperationException("Published license schema revisions are immutable.");
                if (entry.Entity is LicenseRevision && entry.OriginalValues.GetValue<string>(nameof(LicenseRevision.Status)) == "Ready")
                    throw new InvalidOperationException("Ready license revisions are immutable.");
                if (entry.State == EntityState.Modified) entry.Entity.Version = Guid.NewGuid();
            }
        }
    }
}
