using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Delivery.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Customers;
using TicketingCRM.Infrastructure.Delivery;
namespace TicketingCRM.Infrastructure.Tests;
internal sealed class CustomerDeliveryFixture:IDisposable
{
 internal sealed record Actor(Guid UserId,Guid TenantId,Guid? CustomerId,string Role,string? SecurityStamp):ICurrentActor;
 private sealed record Scope(Guid TenantId):ITenantContext;
 internal sealed class Factory(Guid tenant):IDbContextFactory<AppDbContext>{public AppDbContext CreateDbContext()=>Open(tenant);}
 internal static AppDbContext Open(Guid tenant)=>new PostgreSqlAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"),x=>x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options,new Scope(tenant));
 internal Guid Tenant=Guid.NewGuid(),Owner=Guid.NewGuid(),Technical=Guid.NewGuid(),CustomerUser=Guid.NewGuid(),Customer=Guid.NewGuid(),OtherCustomer=Guid.NewGuid(),OtherCustomerUser=Guid.NewGuid(),Product=Guid.NewGuid(),Contract=Guid.NewGuid(),Entitlement=Guid.NewGuid(),Opportunity=Guid.NewGuid();
 internal string Stamp=Guid.NewGuid().ToString("N"),Root=Path.Combine(Path.GetTempPath(),"appsouls-delivery-"+Guid.NewGuid().ToString("N"));
 internal IDataProtectionProvider Protection=new EphemeralDataProtectionProvider();internal RSA Signer=RSA.Create(3072);
 internal PrivateArtifactStore Files=default!;internal ServerConfiguration Config=default!;
 internal Actor OwnerActor=>new(Owner,Tenant,null,"Owner",Stamp);
 internal Actor TechnicalActor=>new(Technical,Tenant,null,"Technical",Stamp);
 internal Actor CustomerActor=>new(CustomerUser,Tenant,Customer,"Customer",Stamp);
 internal Actor OtherActor=>new(OtherCustomerUser,Tenant,OtherCustomer,"Customer",Stamp);
 internal CustomerLifecycleService Lifecycle(Actor? actor=null)=>new(new WorkspaceSessionFactory(new Factory(Tenant)),actor??OwnerActor,Protection);
 internal sealed class Scanner(ArtifactScanVerdict result):IArtifactScanner{public Task<ArtifactScanVerdict> ScanAsync(Stream content,CancellationToken cancellationToken=default)=>Task.FromResult(result);}
 internal ProductReleaseService Releases(Actor? actor=null,ArtifactScanVerdict verdict=ArtifactScanVerdict.Clean)=>new(new WorkspaceSessionFactory(new Factory(Tenant)),actor??OwnerActor,Files,new Scanner(verdict),Config);
 internal static async Task<CustomerDeliveryFixture> Create()
 {
  var f=new CustomerDeliveryFixture();f.Files=new(f.Root);f.Config=new(){DatabaseConnectionString=Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!,PublicBaseUrl="https://example.test",LicenseSigningPrivateKeyPem=f.Signer.ExportPkcs8PrivateKeyPem()};
  await using(var db=Open(Guid.Empty))await db.Database.MigrateAsync();
  await using var s=Open(f.Tenant);s.Tenants.Add(new Tenant{Id=f.Tenant,TenantId=f.Tenant,Name="Delivery fixture"});
  foreach(var id in new[]{f.Owner,f.Technical,f.CustomerUser,f.OtherCustomerUser})s.Users.Add(new ApplicationUser{Id=id,UserName=id.ToString(),Email=id+"@existing.test",EmailConfirmed=true,SecurityStamp=f.Stamp,DisplayName="Fixture member",ConcurrencyStamp=Guid.NewGuid().ToString("N")});
  s.Customers.AddRange(new Customer{Id=f.Customer,TenantId=f.Tenant,Name="Subscribed company",Email=f.Customer+"@onboard.test"},new Customer{Id=f.OtherCustomer,TenantId=f.Tenant,Name="Other customer",Email=f.OtherCustomer+"@onboard.test"});
  s.TenantMemberships.AddRange(new TenantMembership{TenantId=f.Tenant,UserId=f.Owner,Role="Owner"},new TenantMembership{TenantId=f.Tenant,UserId=f.Technical,Role="Technical"},new TenantMembership{TenantId=f.Tenant,UserId=f.CustomerUser,CustomerId=f.Customer,Role="Customer"},new TenantMembership{TenantId=f.Tenant,UserId=f.OtherCustomerUser,CustomerId=f.OtherCustomer,Role="Customer"});
  s.Products.Add(new Product{Id=f.Product,TenantId=f.Tenant,Name="Suite",Code="SUITE"});s.Contracts.Add(new Contract{Id=f.Contract,TenantId=f.Tenant,CustomerId=f.Customer,Reference="Agreement",Status="Active",StartsAtUtc=DateTimeOffset.UtcNow.AddDays(-1)});
  s.Entitlements.Add(new Entitlement{Id=f.Entitlement,TenantId=f.Tenant,CustomerId=f.Customer,ProductId=f.Product,ContractId=f.Contract,StartsAtUtc=DateTimeOffset.UtcNow.AddDays(-1),InstallationLimit=2});
  s.SalesOpportunities.Add(new SalesOpportunity{Id=f.Opportunity,TenantId=f.Tenant,CustomerId=f.Customer,Title="Won purchase",Stage=OpportunityStage.Won,OwnerUserId=f.Owner,CreatedByUserId=f.Owner,CreationOperationId=Guid.NewGuid(),CreatedAtUtc=DateTimeOffset.UtcNow});await s.SaveChangesAsync();return f;
 }
 public void Dispose(){Signer.Dispose();if(Directory.Exists(Root))Directory.Delete(Root,true);}
}
