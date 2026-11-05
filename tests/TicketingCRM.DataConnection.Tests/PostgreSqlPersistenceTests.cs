using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using Xunit;

namespace TicketingCRM.DataConnection.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")))
            Skip = "Set APPSOULS_TEST_DB_CONNECTION to an isolated PostgreSQL test database to run live-engine verification.";
    }
}

public sealed class PostgreSqlPersistenceTests
{
    private sealed record Scope(Guid TenantId) : ITenantContext;
    private static PostgreSqlAppDbContext Open(Guid tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"), x => x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options;
        return new PostgreSqlAppDbContext(options, new Scope(tenant));
    }

    [PostgreSqlFact]
    public async Task IdentityMembershipAndTicketHistoryOutboxCommitTogetherAndRollbackTogether()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        await using (var db = Open(tenant))
        {
            await db.Database.MigrateAsync();
            db.Users.Add(new ApplicationUser { Id = user, UserName = $"reporter-{user:N}", Email = "reporter@example.test" });
            db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "Integration tenant" });
            db.Customers.Add(new Customer { Id = customer, TenantId = tenant, Name = "Integration customer", CreatedAtUtc = DateTimeOffset.UtcNow });
            db.Products.Add(new Product { Id = product, TenantId = tenant, Name = "Product", Code = "P" });
            db.TenantMemberships.Add(new TenantMembership { TenantId = tenant, CustomerId = customer, UserId = user, Role = "Customer" });
            await db.SaveChangesAsync();
        }
        var rollbackTicket = Guid.NewGuid();
        await using (var db = Open(tenant))
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            Stage(db, rollbackTicket); await db.SaveChangesAsync(); await tx.RollbackAsync();
        }
        await using (var db = Open(tenant))
        {
            Assert.False(await db.SupportTickets.AnyAsync(x => x.Id == rollbackTicket));
            Assert.False(await db.SupportTicketHistories.AnyAsync(x => x.TicketId == rollbackTicket));
            Assert.False(await db.OutboxMessages.AnyAsync(x => x.TicketId == rollbackTicket));
        }
        var committedTicket = Guid.NewGuid();
        await using (var db = Open(tenant))
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            Stage(db, committedTicket); await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        await using (var db = Open(tenant))
        {
            Assert.Equal(user, (await db.TenantMemberships.SingleAsync()).UserId);
            Assert.Equal(SupportStage.Submitted, (await db.SupportTickets.SingleAsync()).Stage);
            Assert.Equal(user, (await db.SupportTicketHistories.SingleAsync()).ActorUserId);
            Assert.Null((await db.OutboxMessages.SingleAsync()).DeliveredAtUtc);
        }
        await using (var other = Open(Guid.NewGuid())) Assert.Empty(await other.SupportTickets.ToListAsync());

        void Stage(AppDbContext db, Guid ticket)
        {
            db.SupportTickets.Add(new SupportTicket { Id = ticket, TenantId = tenant, CustomerId = customer, ProductId = product, ReporterUserId = user, OperationId = Guid.NewGuid(), Reference = $"TEST-{ticket:N}", Title = "Example", Kind = SupportReportKind.Bug, CreatedAtUtc = DateTimeOffset.UtcNow });
            db.SupportTicketHistories.Add(new SupportTicketHistory { TenantId = tenant, TicketId = ticket, ActorUserId = user, Stage = SupportStage.Submitted, Message = "Submitted", OccurredAtUtc = DateTimeOffset.UtcNow });
            db.OutboxMessages.Add(new OutboxMessage { TenantId = tenant, TicketId = ticket, Kind = "SupportAcknowledgement", Recipient = "reporter@example.test", IdempotencyKey = $"ticket-{ticket:N}", CreatedAtUtc = DateTimeOffset.UtcNow });
        }
    }

    private sealed class UnscopedFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => Open(Guid.Empty);
    }

    [PostgreSqlFact]
    public async Task MachineCredentialReturnsOnlyItsArtifactAndRevocationBlocksRenewal()
    {
        var tenant = Guid.NewGuid(); var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        var contract = Guid.NewGuid(); var entitlement = Guid.NewGuid(); var installation = Guid.NewGuid(); var schema = Guid.NewGuid();
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        await using (var db = Open(tenant))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "License test" });
            db.Customers.Add(new Customer { Id = customer, TenantId = tenant, Name = "Customer" });
            db.Products.Add(new Product { Id = product, TenantId = tenant, Name = "Product", Code = "P" });
            db.Contracts.Add(new Contract { Id = contract, TenantId = tenant, CustomerId = customer, Reference = "C", StartsAtUtc = DateTimeOffset.UtcNow });
            db.Entitlements.Add(new Entitlement { Id = entitlement, TenantId = tenant, CustomerId = customer, ProductId = product, ContractId = contract, StartsAtUtc = DateTimeOffset.UtcNow.AddYears(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1) });
            db.Installations.Add(new Installation { Id = installation, TenantId = tenant, CustomerId = customer, ProductId = product, EntitlementId = entitlement, CredentialHash = hash, RegisteredAtUtc = DateTimeOffset.UtcNow });
            db.LicenseSchemaRevisions.Add(new LicenseSchemaRevision { Id = schema, TenantId = tenant, ProductId = product, Revision = 1, IsPublished = true });
            db.LicenseRevisions.Add(new LicenseRevision { TenantId = tenant, CustomerId = customer, ProductId = product, EntitlementId = entitlement, InstallationId = installation, SchemaRevisionId = schema, Revision = 2, Status = "Ready", Artifact = [1, 2, 3], IssuedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var lookup = new TicketingCRM.DataConnection.Repository.MachineLicenseLookup(new UnscopedFactory());
        var renewal = await lookup.FindRenewalAsync(token, 1);
        Assert.NotNull(renewal); Assert.Equal(2, renewal.Revision); Assert.Equal(new byte[] { 1, 2, 3 }, renewal.Artifact);
        Assert.Null(await lookup.FindRenewalAsync(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), 0));
        Assert.Null(await lookup.FindRenewalAsync(token, 2));
        await using (var db = Open(tenant))
        {
            (await db.Installations.SingleAsync()).IsRevoked = true;
            await db.SaveChangesAsync();
        }
        Assert.Null(await lookup.FindRenewalAsync(token, 0));
    }

    [PostgreSqlFact]
    public async Task ConcurrentEnrollmentCounterUpdateRollsBackLosingInstallation()
    {
        var tenant = Guid.NewGuid(); var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        var contract = Guid.NewGuid(); var entitlement = Guid.NewGuid();
        await using (var db = Open(tenant))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "Capacity test" });
            db.Customers.Add(new Customer { Id = customer, TenantId = tenant, Name = "Customer" });
            db.Products.Add(new Product { Id = product, TenantId = tenant, Name = "Product", Code = "P" });
            db.Contracts.Add(new Contract { Id = contract, TenantId = tenant, CustomerId = customer, Reference = "C", StartsAtUtc = DateTimeOffset.UtcNow });
            db.Entitlements.Add(new Entitlement { Id = entitlement, TenantId = tenant, CustomerId = customer, ProductId = product, ContractId = contract, InstallationLimit = 1, StartsAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var first = Open(tenant); await using var second = Open(tenant);
        var a = await first.Entitlements.SingleAsync(); var b = await second.Entitlements.SingleAsync();
        Assert.True(a.EnrollmentCounter < a.InstallationLimit); Assert.True(b.EnrollmentCounter < b.InstallationLimit);
        a.EnrollmentCounter++; b.EnrollmentCounter++;
        first.Installations.Add(NewInstallation()); second.Installations.Add(NewInstallation());
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = Open(tenant);
        Assert.Single(await check.Installations.ToListAsync());
        Assert.Equal(1, (await check.Entitlements.SingleAsync()).EnrollmentCounter);
        Installation NewInstallation() => new() { TenantId = tenant, CustomerId = customer, ProductId = product, EntitlementId = entitlement, CredentialHash = Guid.NewGuid().ToString("N"), RegisteredAtUtc = DateTimeOffset.UtcNow };
    }
}
