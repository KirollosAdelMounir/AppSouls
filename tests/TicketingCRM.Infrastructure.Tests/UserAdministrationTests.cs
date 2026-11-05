using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Administration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class UserAdministrationTests
{
    private sealed record Scope(Guid TenantId) : ITenantContext;
    private sealed record Actor(Guid UserId, Guid TenantId, Guid? CustomerId, string Role, string? SecurityStamp) : ICurrentActor;
    private sealed class Factory(Guid tenant) : IDbContextFactory<AppDbContext> { public AppDbContext CreateDbContext() => Open(tenant); }
    private static AppDbContext Open(Guid tenant) => new PostgreSqlAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"), x => x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options, new Scope(tenant));
    private sealed class Fixture
    {
        public Guid Tenant = Guid.NewGuid(), Owner = Guid.NewGuid(), Customer = Guid.NewGuid();
        public string Stamp = Guid.NewGuid().ToString();
        public Actor Actor => new(Owner, Tenant, null, "Owner", Stamp);
        public UserAdministrationService Service(Actor? actor = null) => new(new WorkspaceSessionFactory(new Factory(Tenant)), actor ?? Actor);
        public CreateManagedUserRequest Request(string role = "Technical") => new() { DisplayName = "A user", Email = $"user-{Guid.NewGuid():N}@example.test", InitialPassword = "A-valid-initial-Password-123!", Role = role, CustomerId = role == "Customer" ? Customer : null, EmailOwnershipVerified = true };
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await using var db = Open(f.Tenant); await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant { Id = f.Tenant, TenantId = f.Tenant, Name = "Administration tests" });
            db.Users.Add(new ApplicationUser { Id = f.Owner, UserName = f.Owner.ToString(), EmailConfirmed = true, SecurityStamp = f.Stamp });
            db.TenantMemberships.Add(new TenantMembership { TenantId = f.Tenant, UserId = f.Owner, Role = "Owner" });
            db.Customers.Add(new Customer { Id = f.Customer, TenantId = f.Tenant, Name = "Customer" });
            await db.SaveChangesAsync(); return f;
        }
    }
    [PostgreSqlFact]
    public async Task OwnerCreatesHashedCustomerIdentityWithCorrectOwnershipAndNoPlatformPrivileges()
    {
        var f = await Fixture.Create(); var request = f.Request("Customer"); var id = await f.Service().CreateAsync(request);
        var member = Assert.Single(await f.Service().ListAsync(), x => x.MembershipId == id);
        Assert.Equal(f.Customer, member.CustomerId); Assert.Equal("Customer", member.Role); Assert.True(member.IsActive);
        await using var db = Open(f.Tenant); var user = await db.Users.SingleAsync(x => x.Id == member.UserId);
        Assert.NotEqual(request.InitialPassword, user.PasswordHash); Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, request.InitialPassword));
        Assert.False(user.IsPlatformAdministrator); Assert.False(user.TwoFactorEnabled); Assert.True(user.EmailConfirmed);
    }
    [PostgreSqlFact]
    public async Task ValidationRejectsUnverifiedEmailWeakPasswordOwnerRoleAndForeignCustomerWithoutPartialWrites()
    {
        var f = await Fixture.Create(); var service = f.Service(); var before = (await service.ListAsync()).Count;
        var request = f.Request(); request.EmailOwnershipVerified = false; await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        request = f.Request(); request.InitialPassword = "weak"; await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        request = f.Request(); request.Role = "Owner"; await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        var other = await Fixture.Create(); request = f.Request("Customer"); request.CustomerId = other.Customer;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        Assert.Equal(before, (await service.ListAsync()).Count);
    }
    [PostgreSqlFact]
    public async Task ExistingIdentityCannotBeAdoptedOrHaveItsPasswordResetByAnotherTenant()
    {
        var f = await Fixture.Create(); var other = await Fixture.Create(); var original = other.Request(); await other.Service().CreateAsync(original);
        var attack = f.Request(); attack.Email = original.Email;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service().CreateAsync(attack));
        Assert.DoesNotContain(await f.Service().ListAsync(), x => x.Email == original.Email);
        await using var db = Open(other.Tenant); var user = await db.Users.SingleAsync(x => x.Email == original.Email);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, original.InitialPassword));
    }
    [PostgreSqlFact]
    public async Task TechnicalForgedRoleAndStaleSessionCannotAdministerUsers()
    {
        var f = await Fixture.Create(); var record = await f.Service().CreateAsync(f.Request());
        var technical = (await f.Service().ListAsync()).Single(x => x.MembershipId == record);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service(new(technical.UserId, f.Tenant, null, "Technical", null)).ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service(new(technical.UserId, f.Tenant, null, "Admin", null)).CreateAsync(f.Request()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service(f.Actor with { SecurityStamp = "stale" }).ListAsync());
    }
    [PostgreSqlFact]
    public async Task AccessChangesProtectOwnerDetectStaleVersionAndStopDisabledUserSignIn()
    {
        var f = await Fixture.Create(); var service = f.Service(); var id = await service.CreateAsync(f.Request());
        var list = await service.ListAsync(); var member = list.Single(x => x.MembershipId == id); var owner = list.Single(x => x.UserId == f.Owner);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAccessAsync(new(owner.MembershipId, owner.Version, false)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeAccessAsync(new(member.MembershipId, Guid.NewGuid(), false)));
        var directory = new SignInDirectory(new Factory(f.Tenant));
        await using var db = Open(f.Tenant); var user = await db.Users.SingleAsync(x => x.Id == member.UserId);
        Assert.NotNull(await directory.FindAsync(user.Id, user.SecurityStamp!));
        await service.ChangeAccessAsync(new(member.MembershipId, member.Version, false));
        Assert.Null(await directory.FindAsync(user.Id, user.SecurityStamp!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeAccessAsync(new(member.MembershipId, member.Version, true)));
        var current = (await service.ListAsync()).Single(x => x.MembershipId == id); Assert.False(current.IsActive);
        await service.ChangeAccessAsync(new(current.MembershipId, current.Version, true));
        Assert.NotNull(await directory.FindAsync(user.Id, user.SecurityStamp!));
    }
    [PostgreSqlFact]
    public async Task SignInDirectoryReturnsOnlyActiveNamedMembershipsAndRejectsRevokedCredentials()
    {
        var f = await Fixture.Create(); var other = await Fixture.Create();
        await using (var db = Open(other.Tenant)) { db.TenantMemberships.Add(new TenantMembership { TenantId = other.Tenant, UserId = f.Owner, Role = "Technical" }); await db.SaveChangesAsync(); }
        var directory = new SignInDirectory(new Factory(Guid.Empty)); var account = await directory.FindAsync(f.Owner, f.Stamp);
        Assert.NotNull(account); Assert.Equal(2, account.Workspaces.Count); Assert.Contains(account.Workspaces, x => x.TenantId == other.Tenant && x.Role == "Technical");
        Assert.All(account.Workspaces, x => Assert.Equal("Administration tests", x.Name)); Assert.False(account.TwoFactorEnabled);
        Assert.Null(await directory.FindAsync(f.Owner, "wrong-stamp"));
        await using (var db = Open(other.Tenant)) { var tenant = await db.Tenants.SingleAsync(); tenant.IsActive = false; await db.SaveChangesAsync(); }
        Assert.Single((await directory.FindAsync(f.Owner, f.Stamp))!.Workspaces);
        await using (var db = Open(f.Tenant)) { var user = await db.Users.SingleAsync(x => x.Id == f.Owner); user.EmailConfirmed = false; await db.SaveChangesAsync(); }
        Assert.Null(await directory.FindAsync(f.Owner, f.Stamp));
    }
    [PostgreSqlTheory]
    [InlineData("Ab1!xyz",false)]
    [InlineData("Ab1!xyzz",true)]
    public async Task PasswordMinimumAcceptsEightCharactersAndRejectsSeven(string password,bool accepted)
    {
        var f=await Fixture.Create();var request=f.Request();request.InitialPassword=password;
        if(accepted){var id=await f.Service().CreateAsync(request);Assert.Contains(await f.Service().ListAsync(),x=>x.MembershipId==id);}
        else{await Assert.ThrowsAsync<ArgumentException>(()=>f.Service().CreateAsync(request));Assert.Single(await f.Service().ListAsync());}
    }
}
