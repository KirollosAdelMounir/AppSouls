using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.Licensing;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Models;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Licensing;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;

public sealed class LicensingServiceTests
{
    private sealed record Scope(Guid TenantId) : ITenantContext;
    private sealed record Actor(Guid UserId,Guid TenantId,Guid? CustomerId,string Role) : ICurrentActor;
    private static PostgreSqlAppDbContext Open(Guid tenant)=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"),x=>x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options,new Scope(tenant));
    private sealed class Factory(Guid tenant):IDbContextFactory<AppDbContext>{public AppDbContext CreateDbContext()=>Open(tenant);}
    private sealed class Fixture:IDisposable
    {
        public Guid Tenant=Guid.NewGuid(),Owner=Guid.NewGuid(),User=Guid.NewGuid(),Other=Guid.NewGuid(),Customer=Guid.NewGuid(),OtherCustomer=Guid.NewGuid(),Product=Guid.NewGuid(),Contract=Guid.NewGuid(),Entitlement=Guid.NewGuid();
        public RSA Signer=RSA.Create(3072),Recipient=RSA.Create(3072);
        public LicensingService Service(bool customer=false,bool other=false)=>new(new WorkspaceSessionFactory(new Factory(Tenant)),new Actor(customer?other?Other:User:Owner,Tenant,customer?other?OtherCustomer:Customer:null,customer?"Customer":"Owner"),new ServerConfiguration{LicenseSigningPrivateKeyPem=Signer.ExportPkcs8PrivateKeyPem()});
        public async Task Initialize()
        {
            await using(var migration=Open(Guid.Empty))await migration.Database.MigrateAsync();
            await using var db=Open(Tenant);
            db.Tenants.Add(new Tenant{Id=Tenant,TenantId=Tenant,Name="Licensing integration"});
            foreach(var id in new[]{Owner,User,Other})db.Users.Add(new ApplicationUser{Id=id,UserName="lic-"+id.ToString("N"),EmailConfirmed=true});
            db.Customers.AddRange(new Customer{Id=Customer,TenantId=Tenant,Name="Authorized"},new Customer{Id=OtherCustomer,TenantId=Tenant,Name="Other"});
            db.Products.Add(new Product{Id=Product,TenantId=Tenant,Name="Test product",Code="TEST"});
            db.Contracts.Add(new Contract{Id=Contract,TenantId=Tenant,CustomerId=Customer,Reference="LIC",Status="Active",StartsAtUtc=DateTimeOffset.UtcNow.AddDays(-1),EndsAtUtc=DateTimeOffset.UtcNow.AddYears(1)});
            db.Entitlements.Add(new Entitlement{Id=Entitlement,TenantId=Tenant,CustomerId=Customer,ProductId=Product,ContractId=Contract,StartsAtUtc=DateTimeOffset.UtcNow.AddDays(-1),ExpiresAtUtc=DateTimeOffset.UtcNow.AddMonths(1),InstallationLimit=1});
            db.TenantMemberships.AddRange(new TenantMembership{TenantId=Tenant,UserId=Owner,Role="Owner"},new TenantMembership{TenantId=Tenant,UserId=User,CustomerId=Customer,Role="Customer"},new TenantMembership{TenantId=Tenant,UserId=Other,CustomerId=OtherCustomer,Role="Customer"});await db.SaveChangesAsync();
        }
        public Task<Guid> Schema()=>Service().PublishSchemaAsync(new(Product,"Model",".soullic",[new LicenseFieldDefinition("Edition","Edition",LicenseFieldType.Text,true,false)]));
        public EnrollInstallationRequest Enrollment(Guid? id=null)=>new(id??Guid.NewGuid(),Entitlement,"Primary",Recipient.ExportSubjectPublicKeyInfoPem());
        public void Dispose(){Signer.Dispose();Recipient.Dispose();}
    }
    [PostgreSqlFact]
    public async Task PublishEnrollIssueDownloadAndExportRoundtripWithProtectedRights()
    {
        using var f=new Fixture();await f.Initialize();var schema=await f.Schema();var enrollment=await f.Service(true).EnrollAsync(f.Enrollment());
        var request=new IssueLicenseRequest(Guid.NewGuid(),enrollment.InstallationId,schema,"{\"Edition\":\"Enterprise\"}");
        var id=await f.Service().IssueAsync(request);Assert.Equal(id,await f.Service().IssueAsync(request));
        var file=await f.Service(true).DownloadAsync(id);Assert.EndsWith(".soullic",file.Filename);
        var payload=new LicenseArtifactProtector().Unprotect(file.Content,f.Recipient.ExportPkcs8PrivateKey(),f.Signer.ExportSubjectPublicKeyInfo());
        using var doc=JsonDocument.Parse(payload);Assert.Equal(f.Tenant,doc.RootElement.GetProperty("TenantId").GetGuid());Assert.Equal(f.Customer,doc.RootElement.GetProperty("CustomerId").GetGuid());Assert.Equal(enrollment.InstallationId,doc.RootElement.GetProperty("InstallationId").GetGuid());Assert.Equal("Enterprise",doc.RootElement.GetProperty("Custom").GetProperty("Edition").GetString());Assert.Equal(1,doc.RootElement.GetProperty("InstallationLimit").GetInt32());
        var export=await f.Service().ExportModelAsync(schema);using var zip=new System.IO.Compression.ZipArchive(new MemoryStream(export.Content));Assert.Equal(5,zip.Entries.Count);
        await using var db=Open(f.Tenant);Assert.Equal(1,await db.LicenseRevisions.CountAsync());var install=await db.Installations.SingleAsync();Assert.NotEqual(enrollment.CredentialToken,install.CredentialHash);Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(enrollment.CredentialToken!))).ToLowerInvariant(),install.CredentialHash);
    }
    [PostgreSqlFact]
    public async Task CustomerCannotPublishIssueExportOrAccessAnotherCustomerInstallation()
    {
        using var f=new Fixture();await f.Initialize();var schema=await f.Schema();var enrollment=await f.Service(true).EnrollAsync(f.Enrollment());var id=await f.Service().IssueAsync(new(Guid.NewGuid(),enrollment.InstallationId,schema,"{\"Edition\":\"Enterprise\"}"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true).PublishSchemaAsync(new(f.Product,"Blocked",".soullic",[])));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true).IssueAsync(new(Guid.NewGuid(),enrollment.InstallationId,schema,"{}")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true).ExportModelAsync(schema));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true,true).EnrollAsync(f.Enrollment()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true,true).DownloadAsync(id));
        var snapshot=await f.Service(true,true).GetAsync();Assert.Empty(snapshot.Installations);Assert.Empty(snapshot.Licenses);
    }
    [PostgreSqlFact]
    public async Task EnrollmentRetryDoesNotDiscloseCredentialAndCapacityCannotBeExceeded()
    {
        using var f=new Fixture();await f.Initialize();var request=f.Enrollment();var first=await f.Service(true).EnrollAsync(request);Assert.NotNull(first.CredentialToken);
        var retry=await f.Service(true).EnrollAsync(request);Assert.True(retry.AlreadyRegistered);Assert.Null(retry.CredentialToken);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service(true).EnrollAsync(f.Enrollment()));
        await using var db=Open(f.Tenant);Assert.Single(await db.Installations.ToListAsync());Assert.Equal(1,(await db.Entitlements.SingleAsync()).EnrollmentCounter);
    }
    [PostgreSqlTheory]
    [InlineData("{}")][InlineData("{\"Edition\":null}")][InlineData("{\"Edition\":5}")][InlineData("{\"Edition\":\"A\",\"Edition\":\"B\"}")][InlineData("{\"Edition\":\"A\",\"InstallationLimit\":99}")]
    public async Task MalformedOrMissingCustomValuesCannotIssueRights(string json)
    {
        using var f=new Fixture();await f.Initialize();var schema=await f.Schema();var install=await f.Service(true).EnrollAsync(f.Enrollment());
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service().IssueAsync(new(Guid.NewGuid(),install.InstallationId,schema,json)));
        await using var db=Open(f.Tenant);Assert.Empty(await db.LicenseRevisions.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task RemovedMembershipAndPrivateRecipientKeysAreRejected()
    {
        using var f=new Fixture();await f.Initialize();await Assert.ThrowsAsync<ArgumentException>(()=>f.Service(true).EnrollAsync(f.Enrollment() with{PublicKeyPem=f.Recipient.ExportPkcs8PrivateKeyPem()}));
        await using(var db=Open(f.Tenant)){(await db.TenantMemberships.SingleAsync(x=>x.UserId==f.User)).IsActive=false;await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true).GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true).EnrollAsync(f.Enrollment()));
    }
    [PostgreSqlFact]
    public async Task MappedAttributesAreResolvedFromOwnedCustomerAndCannotBeOverridden()
    {
        using var f=new Fixture();await f.Initialize();
        var fields=new LicenseFieldDefinition[]{new("Company","Company",LicenseFieldType.Text,true,false,LicenseValueSource.CustomerName),new("Customer","Customer",LicenseFieldType.Identifier,true,false,LicenseValueSource.CustomerId),new("Seats","Seats",LicenseFieldType.Integer,true,false,LicenseValueSource.InstallationLimit),new("Expires","Expires",LicenseFieldType.UtcDateTime,true,true,LicenseValueSource.ExpiresAtUtc)};
        var schema=await f.Service().PublishSchemaAsync(new(f.Product,"Mapped model",".soullic",fields));
        var install=await f.Service(true).EnrollAsync(f.Enrollment());
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service().IssueAsync(new(Guid.NewGuid(),install.InstallationId,schema,"{\"Company\":\"Forged customer\"}")));
        var id=await f.Service().IssueAsync(new(Guid.NewGuid(),install.InstallationId,schema,"{}"));
        var download=await f.Service(true).DownloadAsync(id);using var payload=JsonDocument.Parse(new LicenseArtifactProtector().Unprotect(download.Content,f.Recipient.ExportPkcs8PrivateKey(),f.Signer.ExportSubjectPublicKeyInfo()));
        var custom=payload.RootElement.GetProperty("Custom");Assert.Equal("Authorized",custom.GetProperty("Company").GetString());Assert.Equal(f.Customer,custom.GetProperty("Customer").GetGuid());Assert.Equal("1",custom.GetProperty("Seats").GetString());Assert.NotEqual(JsonValueKind.Null,custom.GetProperty("Expires").ValueKind);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service(true,true).DownloadAsync(id));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service().PublishSchemaAsync(new(f.Product,"Bad mapping",".soullic",[new("Customer","Customer",LicenseFieldType.Boolean,true,false,LicenseValueSource.CustomerName)])));
    }

}
