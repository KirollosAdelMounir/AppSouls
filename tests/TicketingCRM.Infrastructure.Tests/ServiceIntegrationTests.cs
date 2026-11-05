using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Infrastructure.Support;
using TicketingCRM.Infrastructure.Workspace;
using Xunit;

namespace TicketingCRM.Infrastructure.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"))) Skip = "An isolated PostgreSQL test database is required."; }
}
public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"))) Skip = "An isolated PostgreSQL test database is required."; }
}

public sealed class ServiceIntegrationTests
{
    private static readonly Lazy<Task> Migration = new(async () => { await using var db = Open(Guid.Empty); await db.Database.MigrateAsync(); });
    private sealed record Actor(Guid UserId, Guid TenantId, Guid? CustomerId, string Role, string? SecurityStamp = null) : ICurrentActor;
    private sealed record TenantScope(Guid TenantId) : ITenantContext;
    private sealed class Factory(Guid tenant) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => Open(tenant);
    }
    private static PostgreSqlAppDbContext Open(Guid tenant) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"), x => x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options, new TenantScope(tenant));

    private sealed class Fixture
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid OtherCustomer { get; } = Guid.NewGuid();
        public Guid Product { get; } = Guid.NewGuid();
        public Guid Contract { get; } = Guid.NewGuid();
        public Guid Reporter { get; } = Guid.NewGuid();
        public Guid OtherReporter { get; } = Guid.NewGuid();
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Technical { get; } = Guid.NewGuid();
        public Actor CustomerActor => new(Reporter, Tenant, Customer, "Customer");
        public Actor TechnicalActor => new(Technical, Tenant, null, "Technical");
        public WorkspaceService Workspace(Actor? actor = null) => new(new WorkspaceSessionFactory(new Factory(Tenant)), actor ?? new Actor(Owner,Tenant,null,"Owner"));
        public SupportReportService Support(Actor? actor = null) => new(new WorkspaceSessionFactory(new Factory(Tenant)), actor ?? CustomerActor,
            new ServerConfiguration { PublicBaseUrl = "https://appsouls.example.test" });
        public SubmitReportRequest Request() => new(Guid.NewGuid(),Product,SupportReportKind.Bug,"Crash on opening","A controlled test description","Open the example","A successful opening","An error","1.0","Linux test");
        public static async Task<Fixture> CreateAsync()
        {
            await Migration.Value;
            var f = new Fixture();
            await using var db = Open(f.Tenant);
            // Migrations are explicitly applied by the isolated test runner before these cases run.
            db.Tenants.Add(new Tenant { Id=f.Tenant,TenantId=f.Tenant,Name="Service integration tenant" });
            foreach (var id in new[]{f.Reporter,f.OtherReporter,f.Technical,f.Owner})
                db.Users.Add(new ApplicationUser { Id=id, UserName="test-"+id.ToString("N"),Email="reporter@example.test",EmailConfirmed=true });
            db.TenantMemberships.Add(new TenantMembership{TenantId=f.Tenant,UserId=f.Owner,Role="Owner"});
            db.Customers.AddRange(new Customer{Id=f.Customer,TenantId=f.Tenant,Name="Authorized customer"},new Customer{Id=f.OtherCustomer,TenantId=f.Tenant,Name="Other customer"});
            db.Products.Add(new Product{Id=f.Product,TenantId=f.Tenant,Name="Purchased product",Code="PURCHASED"});
            db.Contracts.Add(new Contract{Id=f.Contract,TenantId=f.Tenant,CustomerId=f.Customer,Reference="C1",Status="Active",StartsAtUtc=DateTimeOffset.UtcNow.AddDays(-1),EndsAtUtc=DateTimeOffset.UtcNow.AddYears(1)});
            db.Entitlements.Add(new Entitlement{TenantId=f.Tenant,CustomerId=f.Customer,ProductId=f.Product,ContractId=f.Contract,InstallationLimit=3,StartsAtUtc=DateTimeOffset.UtcNow.AddDays(-1),ExpiresAtUtc=DateTimeOffset.UtcNow.AddMonths(1)});
            db.TenantMemberships.AddRange(new TenantMembership{TenantId=f.Tenant,UserId=f.Reporter,CustomerId=f.Customer,Role="Customer"},new TenantMembership{TenantId=f.Tenant,UserId=f.OtherReporter,CustomerId=f.OtherCustomer,Role="Customer"},new TenantMembership{TenantId=f.Tenant,UserId=f.Technical,Role="Technical"});
            await db.SaveChangesAsync();return f;
        }
        public async Task<ReportProgress> SubmitAsync(){var receipt=await Support().SubmitAsync(Request());return (await Support().GetProgressAsync(receipt.TicketId))!;}
        public ChangeReportStageRequest Transition(ReportProgress progress,SupportStage stage,Guid? assigned=null,string? resolution=null,string? closure=null) =>
            new(progress.TicketId,Guid.NewGuid(),progress.Version,stage,assigned,"Customer visible update",resolution,closure);
        public async Task<ReportProgress> AdvanceAsync(ReportProgress progress,SupportStage stage,string? resolution=null)
        {await Support(TechnicalActor).ChangeStageAsync(Transition(progress,stage,Technical,resolution));return (await Support().GetProgressAsync(progress.TicketId))!;}
    }

    [PostgreSqlFact]
    public async Task CustomerCannotWriteCrmButSeesOnlyOwnedRecords()
    {
        var f=await Fixture.CreateAsync();var service=f.Workspace(f.CustomerActor);
        var snapshot=await service.GetAsync();Assert.Equal(f.Customer,Assert.Single(snapshot.Customers).Id);Assert.Equal(f.Product,Assert.Single(snapshot.Products).Id);Assert.All(snapshot.Contracts,x=>Assert.Equal(f.Customer,x.CustomerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateCustomerAsync(new("Blocked","new@example.test","")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateProductAsync(new("Blocked","NEW",".lic")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateContractAsync(new(f.Customer,"blocked",DateTimeOffset.UtcNow,null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ActivateContractAsync(f.Contract));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.GrantEntitlementAsync(new(f.Contract,f.Product,1,null)));
        await using var db=Open(f.Tenant);Assert.Equal(2,await db.Customers.CountAsync());Assert.Single(await db.Products.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task DisabledOrForgedMembershipCannotReadOrSubmit()
    {
        var f=await Fixture.CreateAsync();await using(var db=Open(f.Tenant)){(await db.TenantMemberships.SingleAsync(x=>x.UserId==f.Reporter)).IsActive=false;await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Workspace(f.CustomerActor).GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support().SubmitAsync(f.Request()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Workspace(new(f.Technical,f.Tenant,null,"Owner")).GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Workspace(new(f.Technical,Guid.NewGuid(),null,"Technical")).GetAsync());
    }

    [PostgreSqlTheory]
    [InlineData(".dll")][InlineData(".exe")][InlineData("../license")][InlineData(".js")]
    public async Task UnsafeProductExtensionsAreRejected(string extension)
    {var f=await Fixture.CreateAsync();await Assert.ThrowsAsync<ArgumentException>(()=>f.Workspace().CreateProductAsync(new("Unsafe","UNSAFE",extension)));await using var db=Open(f.Tenant);Assert.Single(await db.Products.ToListAsync());}

    [PostgreSqlFact]
    public async Task ContractAndEntitlementValidationRejectsInvalidTermsAndCapacity()
    {
        var f=await Fixture.CreateAsync();var service=f.Workspace();var now=DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateContractAsync(new(f.Customer,"invalid",now,now)));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GrantEntitlementAsync(new(f.Contract,f.Product,0,now.AddDays(1))));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GrantEntitlementAsync(new(f.Contract,f.Product,100001,now.AddDays(1))));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GrantEntitlementAsync(new(f.Contract,f.Product,1,now.AddYears(2))));
        var draft=await service.CreateContractAsync(new(f.Customer,"Draft",now,now.AddMonths(2)));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GrantEntitlementAsync(new(draft,f.Product,1,now.AddMonths(1))));
        await service.ActivateContractAsync(draft);var grant=await service.GrantEntitlementAsync(new(draft,f.Product,1,now.AddMonths(1)));Assert.NotEqual(Guid.Empty,grant);
    }

    [PostgreSqlTheory]
    [InlineData("steps")][InlineData("expected")][InlineData("actual")]
    public async Task BugReportsRequireReproductionFields(string missing)
    {
        var f=await Fixture.CreateAsync();var request=f.Request();request=missing switch{"steps"=>request with{StepsToReproduce=""},"expected"=>request with{ExpectedResult=""},_=>request with{ActualResult=""}};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Support().SubmitAsync(request));await using var db=Open(f.Tenant);Assert.Empty(await db.SupportTickets.ToListAsync());Assert.Empty(await db.OutboxMessages.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task SubmissionPersistsHistoryAndQueuedOutboxExactlyOnceForRepeatedOperation()
    {
        var f=await Fixture.CreateAsync();var request=f.Request();var first=await f.Support().SubmitAsync(request);var repeated=await f.Support().SubmitAsync(request);
        Assert.Equal(first.TicketId,repeated.TicketId);Assert.True(first.AcknowledgementQueued);
        await using var db=Open(f.Tenant);var ticket=Assert.Single(await db.SupportTickets.ToListAsync());var history=Assert.Single(await db.SupportTicketHistories.ToListAsync());var job=Assert.Single(await db.OutboxMessages.ToListAsync());
        Assert.Equal(first.TicketId,ticket.Id);Assert.Equal(f.Reporter,history.ActorUserId);Assert.Equal(SupportStage.Submitted,history.Stage);Assert.Equal(TimeSpan.Zero,history.OccurredAtUtc.Offset);
        Assert.Equal(ticket.Id,job.TicketId);Assert.Equal(f.Reporter.ToString(),job.Recipient);Assert.Null(job.DeliveredAtUtc);Assert.DoesNotContain(request.Description,job.PayloadJson);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Support().SubmitAsync(request with{Title="Different request"}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Support().SubmitAsync(request with{StepsToReproduce="Different steps"}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Support().SubmitAsync(request with{ApplicationVersion="2.0"}));
    }

    [PostgreSqlFact]
    public async Task UnpurchasedProductAndCrossCustomerProgressAreDenied()
    {
        var f=await Fixture.CreateAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support().SubmitAsync(f.Request() with{ProductId=Guid.NewGuid()}));
        var progress=await f.SubmitAsync();Assert.Null(await f.Support(new(f.OtherReporter,f.Tenant,f.OtherCustomer,"Customer")).GetProgressAsync(progress.TicketId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support(new(f.Reporter,Guid.NewGuid(),f.Customer,"Customer")).GetProgressAsync(progress.TicketId));
    }

    [PostgreSqlFact]
    public async Task ProgressHidesInternalNotesAndStaffIdentity()
    {
        var f=await Fixture.CreateAsync();var progress=await f.SubmitAsync();await using(var db=Open(f.Tenant)){db.SupportTicketHistories.Add(new(){TenantId=f.Tenant,TicketId=progress.TicketId,ActorUserId=f.Technical,Message="Internal diagnostic note",IsCustomerVisible=false,Stage=SupportStage.Submitted,OccurredAtUtc=DateTimeOffset.UtcNow});await db.SaveChangesAsync();}
        var visible=await f.Support().GetProgressAsync(progress.TicketId);Assert.Single(visible!.History);Assert.DoesNotContain(visible.History,x=>x.CustomerVisibleMessage.Contains("Internal"));
    }

    [PostgreSqlFact]
    public async Task ReceiptRequiresTechnicalAssignmentAndAdjacentStage()
    {
        var f=await Fixture.CreateAsync();var progress=await f.SubmitAsync();var staff=f.Support(f.TechnicalActor);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>staff.ChangeStageAsync(f.Transition(progress,SupportStage.SolutionInProgress)));
        await Assert.ThrowsAsync<ArgumentException>(()=>staff.ChangeStageAsync(f.Transition(progress,SupportStage.ReceivedByTechnicalMember)));
        await Assert.ThrowsAsync<ArgumentException>(()=>staff.ChangeStageAsync(f.Transition(progress,SupportStage.ReceivedByTechnicalMember,f.Reporter)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support().ChangeStageAsync(f.Transition(progress,SupportStage.ReceivedByTechnicalMember,f.Technical)));
        var next=await f.AdvanceAsync(progress,SupportStage.ReceivedByTechnicalMember);Assert.Equal(SupportStage.ReceivedByTechnicalMember,next.Stage);Assert.Equal(2,next.History.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>staff.ChangeStageAsync(f.Transition(progress,SupportStage.SolutionInProgress)));
        await using var db=Open(f.Tenant);Assert.Equal(f.Technical,(await db.SupportTickets.SingleAsync()).AssignedTechnicalUserId);
    }

    [PostgreSqlFact]
    public async Task CompletionNeedsResolutionAndCustomerClosureNeedsReason()
    {
        var f=await Fixture.CreateAsync();var p=await f.SubmitAsync();p=await f.AdvanceAsync(p,SupportStage.ReceivedByTechnicalMember);p=await f.AdvanceAsync(p,SupportStage.SolutionInProgress);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Support(f.TechnicalActor).ChangeStageAsync(f.Transition(p,SupportStage.Completed)));
        p=await f.AdvanceAsync(p,SupportStage.Completed,"Fixed in version 1.1");Assert.Equal(SupportStage.Completed,p.Stage);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Support().ChangeStageAsync(f.Transition(p,SupportStage.Closed)));
        await f.Support().ChangeStageAsync(f.Transition(p,SupportStage.Closed,closure:"Confirmed resolved"));
        var closed=await f.Support().GetProgressAsync(p.TicketId);Assert.Equal(SupportStage.Closed,closed!.Stage);Assert.Equal(5,closed.History.Count);
        await using var db=Open(f.Tenant);var ticket=await db.SupportTickets.SingleAsync();Assert.Equal("Fixed in version 1.1",ticket.Resolution);Assert.Equal("Confirmed resolved",ticket.ClosureReason);Assert.All(await db.SupportTicketHistories.ToListAsync(),x=>{Assert.NotEqual(Guid.Empty,x.ActorUserId);Assert.Equal(TimeSpan.Zero,x.OccurredAtUtc.Offset);});
    }
    [PostgreSqlFact]
    public async Task IssueReportsPermitMissingBugFieldsButRejectEmptyOperation()
    {
        var f=await Fixture.CreateAsync();var request=f.Request() with{Kind=SupportReportKind.Issue,StepsToReproduce="",ExpectedResult="",ActualResult=""};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Support().SubmitAsync(request with{OperationId=Guid.Empty}));
        var receipt=await f.Support().SubmitAsync(request);Assert.Equal(SupportStage.Submitted,receipt.Stage);
        await using var db=Open(f.Tenant);Assert.Single(await db.SupportTickets.ToListAsync());Assert.Single(await db.OutboxMessages.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task InactiveTenantBlocksWorkspaceAndReportProgress()
    {
        var f=await Fixture.CreateAsync();var p=await f.SubmitAsync();await using(var db=Open(f.Tenant)){(await db.Tenants.SingleAsync()).IsActive=false;await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Workspace().GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support().GetProgressAsync(p.TicketId));
    }

    [PostgreSqlFact]
    public async Task ReceiptRejectsInactiveTechnicalAssignmentWithoutHistoryChange()
    {
        var f=await Fixture.CreateAsync();var p=await f.SubmitAsync();await using(var db=Open(f.Tenant)){(await db.TenantMemberships.SingleAsync(x=>x.UserId==f.Technical)).IsActive=false;await db.SaveChangesAsync();}
        var owner=Guid.NewGuid();await using(var db=Open(f.Tenant)){db.Users.Add(new(){Id=owner,UserName="test-"+owner});db.TenantMemberships.Add(new(){TenantId=f.Tenant,UserId=owner,Role="Owner"});await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Support(new(owner,f.Tenant,null,"Owner")).ChangeStageAsync(f.Transition(p,SupportStage.ReceivedByTechnicalMember,f.Technical)));
        await using var check=Open(f.Tenant);Assert.Equal(SupportStage.Submitted,(await check.SupportTickets.SingleAsync()).Stage);Assert.Single(await check.SupportTicketHistories.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task AnotherCustomerCannotCloseCompletedReport()
    {
        var f=await Fixture.CreateAsync();var p=await f.SubmitAsync();p=await f.AdvanceAsync(p,SupportStage.ReceivedByTechnicalMember);p=await f.AdvanceAsync(p,SupportStage.SolutionInProgress);p=await f.AdvanceAsync(p,SupportStage.Completed,"Resolution");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support(new(f.OtherReporter,f.Tenant,f.OtherCustomer,"Customer")).ChangeStageAsync(f.Transition(p,SupportStage.Closed,closure:"Wrong customer")));
        Assert.Equal(SupportStage.Completed,(await f.Support().GetProgressAsync(p.TicketId))!.Stage);
    }

    [PostgreSqlFact]
    public async Task StaleSecurityStampDeniesWorkspaceSupportAndLicensingSessions()
    {
        var f=await Fixture.CreateAsync();await using(var db=Open(f.Tenant)){var user=await db.Users.SingleAsync(x=>x.Id==f.Reporter);user.SecurityStamp="current-stamp";await db.SaveChangesAsync();}
        var stale=new Actor(f.Reporter,f.Tenant,f.Customer,"Customer","previous-stamp");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Workspace(stale).GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Support(stale).ListAsync());
        var sessions=new WorkspaceSessionFactory(new Factory(f.Tenant));
        var licensing=new TicketingCRM.Infrastructure.Licensing.LicensingService(sessions,stale,new ServerConfiguration());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>licensing.GetAsync());
        var current=stale with{SecurityStamp="current-stamp"};Assert.Single((await f.Workspace(current).GetAsync()).Customers);
        var blank=stale with{SecurityStamp=""};await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Workspace(blank).GetAsync());
    }

    [PostgreSqlFact]
    public async Task StageUpdateRetryDoesNotDuplicateHistoryAndRejectsChangedPayload()
    {
        var f=await Fixture.CreateAsync();var report=await f.SubmitAsync();
        var request=f.Transition(report,SupportStage.ReceivedByTechnicalMember,f.Technical);
        var technical=f.Support(f.TechnicalActor);await technical.ChangeStageAsync(request);await technical.ChangeStageAsync(request);
        await using var db=Open(f.Tenant);Assert.Equal(2,await db.SupportTicketHistories.CountAsync());
        Assert.Equal(SupportStage.ReceivedByTechnicalMember,(await db.SupportTickets.SingleAsync()).Stage);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>technical.ChangeStageAsync(request with{CustomerVisibleMessage="Different update"}));
        await Assert.ThrowsAsync<ArgumentException>(()=>technical.ChangeStageAsync(request with{OperationId=Guid.Empty}));
    }

    [PostgreSqlFact]
    public async Task TechnicalMembersCannotGrantCommercialRights()
    {
        var f=await Fixture.CreateAsync();var service=f.Workspace(f.TechnicalActor);
        Assert.NotEmpty((await service.GetAsync()).Customers);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.GrantEntitlementAsync(new(f.Contract,f.Product,100,null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateCustomerAsync(new("Blocked","blocked@example.test","")));
    }

}
