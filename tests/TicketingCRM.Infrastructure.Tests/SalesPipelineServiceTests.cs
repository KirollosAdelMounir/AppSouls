using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.DTOs;
using TicketingCRM.Domain.Sales.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Sales;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class SalesPipelineServiceTests
{
    private sealed record Scope(Guid TenantId) : ITenantContext;
    private sealed record Actor(Guid UserId, Guid TenantId, Guid? CustomerId, string Role, string? SecurityStamp) : ICurrentActor;
    private sealed class Factory(Guid tenant) : IDbContextFactory<AppDbContext> { public AppDbContext CreateDbContext() => Open(tenant); }
    private static AppDbContext Open(Guid tenant) => new PostgreSqlAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"), x => x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options, new Scope(tenant));
    private static readonly Lazy<Task> Migration = new(async () => { await using var db = Open(Guid.Empty); await db.Database.MigrateAsync(); });
    private sealed class Fixture
    {
        public Guid Tenant = Guid.NewGuid(), Owner = Guid.NewGuid(), Customer = Guid.NewGuid(), Technical = Guid.NewGuid();
        public string Stamp = Guid.NewGuid().ToString();
        public Actor Actor => new(Owner, Tenant, null, "Owner", Stamp);
        public SalesPipelineService Service(Actor? actor = null) => new(new WorkspaceSessionFactory(new Factory(Tenant)), actor ?? Actor);
        public CreateLeadRequest LeadRequest() => new(Guid.NewGuid(), "Contact", "Prospect company", "prospect@example.test", "", "Referral", "Private commercial note", Owner, DateTimeOffset.UtcNow.AddDays(2));
        public CreateOpportunityRequest OpportunityRequest() => new(Guid.NewGuid(), "Suite purchase", Customer, Owner, 1250.50m, "USD", null);
        public static async Task<Fixture> Create()
        {
            await Migration.Value; var f = new Fixture(); await using var db = Open(f.Tenant);
            db.Tenants.Add(new Tenant { Id = f.Tenant, TenantId = f.Tenant, Name = "Sales tests" });
            db.Users.AddRange(new ApplicationUser { Id = f.Owner, UserName = f.Owner.ToString(), EmailConfirmed = true, SecurityStamp = f.Stamp, DisplayName = "Owner" },
                new ApplicationUser { Id = f.Technical, UserName = f.Technical.ToString(), EmailConfirmed = true });
            db.TenantMemberships.AddRange(new TenantMembership { TenantId = f.Tenant, UserId = f.Owner, Role = "Owner" },
                new TenantMembership { TenantId = f.Tenant, UserId = f.Technical, Role = "Technical" });
            db.Customers.Add(new Customer { Id = f.Customer, TenantId = f.Tenant, Name = "Existing customer" });
            await db.SaveChangesAsync(); return f;
        }
        public async Task<LeadDto> QualifiedLead()
        {
            var service = Service(); var id = await service.CreateLeadAsync(LeadRequest());
            var lead = Assert.Single((await service.GetAsync()).Leads, x => x.Id == id);
            await service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.Qualified, "Qualified by sales"));
            return Assert.Single((await service.GetAsync()).Leads, x => x.Id == id);
        }
        public ConvertLeadRequest Conversion(LeadDto lead) => new(Guid.NewGuid(), lead.Id, lead.Version, "Converted opportunity", Owner, 100m, "EUR", null);
    }
    [PostgreSqlFact]
    public async Task LeadCreationIsTenantScopedIdempotentAndWritesActorAndUtcHistory()
    {
        var f = await Fixture.Create(); var request = f.LeadRequest(); var service = f.Service(); var id = await service.CreateLeadAsync(request);
        Assert.Equal(id, await service.CreateLeadAsync(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeadAsync(request with { Notes = "Changed payload" }));
        var lead = Assert.Single((await service.GetAsync()).Leads); Assert.Equal(id, lead.Id); Assert.Equal(LeadStage.New, lead.Stage); Assert.Equal(f.Owner, lead.OwnerUserId);
        Assert.Equal(request.FollowUpAtUtc!.Value.UtcTicks / 10 * 10, lead.FollowUpAtUtc!.Value.UtcTicks);
        var history = Assert.Single(await service.HistoryAsync(id, false)); Assert.Equal(f.Owner, history.ActorUserId); Assert.Equal(TimeSpan.Zero, history.OccurredAtUtc.Offset); Assert.Equal("New", history.ToStage);
        var other = await Fixture.Create(); Assert.Empty((await other.Service().GetAsync()).Leads);
        await Assert.ThrowsAsync<ArgumentException>(() => other.Service().HistoryAsync(id, false));
    }
    [PostgreSqlFact]
    public async Task SalesRejectsTechnicalForgedRolesRevokedStampsAndInactiveMemberships()
    {
        var f = await Fixture.Create();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service(new(f.Technical, f.Tenant, null, "Technical", null)).GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service(new(f.Technical, f.Tenant, null, "Admin", null)).CreateLeadAsync(f.LeadRequest()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service(f.Actor with { SecurityStamp = "revoked" }).GetAsync());
        await using (var db = Open(f.Tenant)) { var member = await db.TenantMemberships.SingleAsync(x => x.UserId == f.Owner); member.IsActive = false; await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service().CreateLeadAsync(f.LeadRequest()));
    }
    [PostgreSqlFact]
    public async Task ForeignCustomerAndOwnerCannotBeAttachedToSalesRecords()
    {
        var f = await Fixture.Create(); var other = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service().CreateOpportunityAsync(f.OpportunityRequest() with { CustomerId = other.Customer }));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service().CreateLeadAsync(f.LeadRequest() with { OwnerUserId = other.Owner }));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service().CreateLeadAsync(f.LeadRequest() with { OwnerUserId = f.Technical }));
        await using var db = Open(f.Tenant); Assert.Empty(await db.SalesLeads.ToListAsync()); Assert.Empty(await db.SalesOpportunities.ToListAsync());
        // Composite foreign keys also reject a bypass of the business service.
        db.SalesLeads.Add(new SalesLead { TenantId = f.Tenant, OwnerUserId = other.Owner, CreatedByUserId = f.Owner, CreationOperationId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [PostgreSqlTheory]
    [InlineData("-0.01")]
    [InlineData("0.001")]
    [InlineData("1000000000000")]
    public async Task InvalidExpectedValuesNeverPersist(string value)
    {
        var f = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service().CreateOpportunityAsync(f.OpportunityRequest() with { ExpectedValue = decimal.Parse(value, CultureInfo.InvariantCulture) }));
        Assert.Empty((await f.Service().GetAsync()).Opportunities);
    }
    [PostgreSqlFact]
    public async Task LeadValidationAndCurrencyValidationRejectMalformedRequests()
    {
        var f = await Fixture.Create(); var service = f.Service();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeadAsync(f.LeadRequest() with { ContactName = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeadAsync(f.LeadRequest() with { Email = "invalid" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeadAsync(f.LeadRequest() with { Notes = new string('N', 4001) }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeadAsync(f.LeadRequest() with { OperationId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOpportunityAsync(f.OpportunityRequest() with { Currency = "XYZ" }));
        Assert.Empty((await service.GetAsync()).Leads);
    }
    [PostgreSqlFact]
    public async Task QualifiedConversionAtomicallyCreatesCustomerOpportunityAndHistoriesAndCanBeRetried()
    {
        var f = await Fixture.Create(); var lead = await f.QualifiedLead(); var request = f.Conversion(lead); var service = f.Service();
        var receipt = await service.ConvertLeadAsync(request); Assert.Equal(receipt, await service.ConvertLeadAsync(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConvertLeadAsync(request with { OpportunityTitle = "Changed" }));
        await using var db = Open(f.Tenant);
        var customer = await db.Customers.SingleAsync(x => x.Id == receipt.CustomerId); Assert.Equal(lead.CompanyName, customer.Name); Assert.Equal(lead.Email, customer.Email);
        var opportunity = Assert.Single(await db.SalesOpportunities.ToListAsync()); Assert.Equal(receipt.OpportunityId, opportunity.Id); Assert.Equal(customer.Id, opportunity.CustomerId); Assert.Equal(lead.Id, opportunity.SourceLeadId); Assert.Equal(OpportunityStage.Qualified, opportunity.Stage);
        var converted = Assert.Single(await db.SalesLeads.ToListAsync()); Assert.Equal(LeadStage.Converted, converted.Stage); Assert.Equal(customer.Id, converted.CustomerId);
        Assert.Equal(3, (await service.HistoryAsync(lead.Id, false)).Count); Assert.Single(await service.HistoryAsync(opportunity.Id, true));
        Assert.Empty(await db.Entitlements.ToListAsync()); Assert.Empty(await db.LicenseRevisions.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task FailedConversionRollsBackCustomerLeadAndBothHistoryWrites()
    {
        var f = await Fixture.Create(); var lead = await f.QualifiedLead(); var request = f.Conversion(lead); var service = f.Service();
        await service.CreateOpportunityAsync(f.OpportunityRequest() with { OperationId = request.OperationId });
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ConvertLeadAsync(request));
        await using var db = Open(f.Tenant); Assert.Single(await db.Customers.ToListAsync()); Assert.Single(await db.SalesOpportunities.ToListAsync());
        var current = Assert.Single(await db.SalesLeads.ToListAsync()); Assert.Equal(LeadStage.Qualified, current.Stage); Assert.Null(current.CustomerId); Assert.Null(current.ConversionOperationId); Assert.Equal(lead.Version, current.Version);
        Assert.Equal(2, (await service.HistoryAsync(lead.Id, false)).Count);
    }
    [PostgreSqlFact]
    public async Task ConcurrentConversionProducesExactlyOneCustomerAndOpportunity()
    {
        var f = await Fixture.Create(); var lead = await f.QualifiedLead(); var request = f.Conversion(lead);
        async Task<LeadConversionReceipt?> Attempt()
        { try { return await f.Service().ConvertLeadAsync(request); } catch (Exception e) when (e is DbUpdateException or InvalidOperationException) { return null; } }
        var results = await Task.WhenAll(Attempt(), Attempt()); Assert.Contains(results, x => x is not null);
        var receipt = await f.Service().ConvertLeadAsync(request);
        Assert.All(results.Where(x => x is not null), x => Assert.Equal(receipt, x));
        await using var db = Open(f.Tenant); Assert.Equal(2, await db.Customers.CountAsync()); Assert.Single(await db.SalesOpportunities.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task LeadStagesEnforceReasonsVersionsAndSeparateConversionCommand()
    {
        var f = await Fixture.Create(); var service = f.Service(); var id = await service.CreateLeadAsync(f.LeadRequest()); var lead = Assert.Single((await service.GetAsync()).Leads);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.Disqualified, "")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.Converted, "")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConvertLeadAsync(f.Conversion(lead)));
        var change = new ChangeLeadStageRequest(Guid.NewGuid(), id, lead.Version, LeadStage.Qualified, "Reviewed");
        await service.ChangeLeadStageAsync(change); await service.ChangeLeadStageAsync(change);
        Assert.Equal(2, (await service.HistoryAsync(id, false)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeLeadStageAsync(change with { Reason = "Changed" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.Disqualified, "Lost fit")));
        lead = Assert.Single((await service.GetAsync()).Leads); await service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.Disqualified, "Not a fit"));
        lead = Assert.Single((await service.GetAsync()).Leads); await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.New, "")));
        await service.ChangeLeadStageAsync(new(Guid.NewGuid(), id, lead.Version, LeadStage.New, "Requirements changed"));
        Assert.Equal(LeadStage.New, Assert.Single((await service.GetAsync()).Leads).Stage);
    }
    [PostgreSqlFact]
    public async Task OpportunityStagesReachWonWithHistoryAndNeverGrantAnEntitlement()
    {
        var f = await Fixture.Create(); var service = f.Service(); var request = f.OpportunityRequest() with { ExpectedValue = 1.0m, Currency = "usd" };
        var id = await service.CreateOpportunityAsync(request); Assert.Equal(id, await service.CreateOpportunityAsync(request with { ExpectedValue = 1.00m, Currency = "USD" }));
        foreach (var stage in new[] { OpportunityStage.Qualified, OpportunityStage.Proposal, OpportunityStage.Negotiation, OpportunityStage.Won })
        {
            var item = Assert.Single((await service.GetAsync()).Opportunities); var change = new ChangeOpportunityStageRequest(Guid.NewGuid(), id, item.Version, stage, "");
            await service.ChangeOpportunityStageAsync(change); await service.ChangeOpportunityStageAsync(change);
        }
        var won = Assert.Single((await service.GetAsync()).Opportunities); Assert.Equal(OpportunityStage.Won, won.Stage); Assert.Equal("USD", won.Currency); Assert.Equal(1m, won.ExpectedValue);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeOpportunityStageAsync(new(Guid.NewGuid(), id, won.Version, OpportunityStage.Lost, "Late loss")));
        Assert.Equal(5, (await service.HistoryAsync(id, true)).Count);
        await using var db = Open(f.Tenant); Assert.Empty(await db.Entitlements.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task LostOpportunityRequiresReasonsToCloseAndReopenAndCannotSkipStages()
    {
        var f = await Fixture.Create(); var service = f.Service(); var id = await service.CreateOpportunityAsync(f.OpportunityRequest()); var item = Assert.Single((await service.GetAsync()).Opportunities);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeOpportunityStageAsync(new(Guid.NewGuid(), id, item.Version, OpportunityStage.Won, "")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeOpportunityStageAsync(new(Guid.NewGuid(), id, item.Version, OpportunityStage.Lost, "")));
        await service.ChangeOpportunityStageAsync(new(Guid.NewGuid(), id, item.Version, OpportunityStage.Lost, "Budget changed"));
        item = Assert.Single((await service.GetAsync()).Opportunities);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeOpportunityStageAsync(new(Guid.NewGuid(), id, item.Version, OpportunityStage.Qualified, "")));
        await service.ChangeOpportunityStageAsync(new(Guid.NewGuid(), id, item.Version, OpportunityStage.Qualified, "New budget"));
        Assert.Equal(OpportunityStage.Qualified, Assert.Single((await service.GetAsync()).Opportunities).Stage);
    }
    [PostgreSqlFact]
    public async Task HistoryCannotBeEditedAndPaginationIsBoundedStableAndTenantScoped()
    {
        var f = await Fixture.Create(); var service = f.Service();
        for (var i = 0; i < 21; i++) await service.CreateLeadAsync(f.LeadRequest() with { CompanyName = "Company " + i });
        var first = await service.GetAsync(1, 10); var second = await service.GetAsync(2, 10); var third = await service.GetAsync(3, 10);
        Assert.Equal(21, first.LeadCount); Assert.Equal(10, first.Leads.Count); Assert.Equal(10, second.Leads.Count); Assert.Single(third.Leads);
        Assert.Empty(first.Leads.Select(x => x.Id).Intersect(second.Leads.Select(x => x.Id)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(0)); await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(1, 51));
        await using var db = Open(f.Tenant); var history = await db.SalesHistories.FirstAsync(); history.Reason = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [PostgreSqlFact]
    public async Task LeadActivitiesPersistBeforeConversionWithIdempotencyOwnershipAndFollowUp()
    {
        var f=await Fixture.Create();var service=f.Service();var id=await service.CreateLeadAsync(f.LeadRequest());var lead=Assert.Single((await service.GetAsync()).Leads);
        var request=new RecordSalesActivityRequest(Guid.NewGuid(),id,false,lead.Version,"Call","Requirements call","Internal sales details",DateTimeOffset.UtcNow.AddDays(3));
        await service.RecordActivityAsync(request);await service.RecordActivityAsync(request);
        var updated=Assert.Single((await service.GetAsync()).Leads);Assert.NotNull(updated.FollowUpAtUtc);Assert.Equal(LeadStage.New,updated.Stage);
        var history=await service.HistoryAsync(id,false);Assert.Equal(2,history.Count);Assert.Contains(history,x=>x.Reason.Contains("[Call] Requirements call"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RecordActivityAsync(request with{Subject="Changed"}));
        var other=await Fixture.Create();await Assert.ThrowsAsync<ArgumentException>(()=>other.Service().RecordActivityAsync(request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(new(f.Technical,f.Tenant,null,"Technical",null)).RecordActivityAsync(request));
    }

}
