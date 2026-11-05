using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using Xunit;

namespace TicketingCRM.DataConnection.Tests;

public sealed class AppDbContextTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid tenantA = Guid.NewGuid();
    private readonly Guid tenantB = Guid.NewGuid();
    public AppDbContextTests()
    {
        connection.Open();
        using var db = Open(tenantA);
        db.Database.EnsureCreated();
        db.Tenants.Add(new Tenant { Id = tenantA, TenantId = tenantA, Name = "A" });
        db.Customers.Add(new Customer { TenantId = tenantA, Name = "A customer" });
        db.SaveChanges();
        using var other = Open(tenantB);
        other.Tenants.Add(new Tenant { Id = tenantB, TenantId = tenantB, Name = "B" });
        other.Customers.Add(new Customer { TenantId = tenantB, Name = "B customer" });
        other.SaveChanges();
    }
    private AppDbContext Open(Guid? tenant) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, tenant is null ? null : new Scope(tenant.Value));
    private sealed record Scope(Guid TenantId) : ITenantContext;

    [Fact] public void QueriesReturnOnlyCurrentTenantAndMissingScopeReturnsNothing()
    {
        using var a = Open(tenantA); using var b = Open(tenantB); using var missing = Open(null);
        Assert.Equal("A customer", Assert.Single(a.Customers).Name);
        Assert.Equal("B customer", Assert.Single(b.Customers).Name);
        Assert.Empty(missing.Customers);
    }
    [Fact] public void MissingScopeRejectsWrites()
    {
        using var db = Open(null);
        db.Customers.Add(new Customer { TenantId = tenantA, Name = "intruder" });
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
    }
    [Fact] public void CrossTenantWritesAndOwnershipChangesAreRejected()
    {
        using var db = Open(tenantA);
        var customer = Assert.Single(db.Customers);
        customer.TenantId = tenantB;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        db.Products.Add(new Product { TenantId = tenantB, Code = "x" });
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
    }
    [Fact] public void DatabaseRejectsCrossTenantRelationship()
    {
        using var b = Open(tenantB);
        var foreignCustomer = Assert.Single(b.Customers).Id;
        using var db = Open(tenantA);
        db.Contracts.Add(new Contract { TenantId = tenantA, CustomerId = foreignCustomer, Reference = "cross" });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
    [Fact] public void StaleUpdateIsRejectedByConcurrencyToken()
    {
        using var first = Open(tenantA); using var second = Open(tenantA);
        var original = Assert.Single(first.Customers); var stale = Assert.Single(second.Customers);
        original.Name = "changed"; first.SaveChanges();
        stale.Name = "stale";
        Assert.Throws<DbUpdateConcurrencyException>(() => second.SaveChanges());
    }
    public void Dispose() => connection.Dispose();
}
