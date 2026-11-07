using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Configuration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class ServerSettingsServiceTests
{
 private sealed record Actor(Guid UserId,Guid TenantId,Guid? CustomerId,string Role,string? SecurityStamp):ICurrentActor;
 private sealed record Scope(Guid TenantId):ITenantContext;
 private sealed class Factory(Guid tenant):IDbContextFactory<AppDbContext>{public AppDbContext CreateDbContext()=>Open(tenant);}
 private static AppDbContext Open(Guid tenant,bool administrative=false){var o=new DbContextOptionsBuilder<AppDbContext>();TicketingCRM.DataConnection.DependencyInjection.ConfigureProvider(o,TicketingCRM.DataConnection.Configuration.DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!,administrative);return new PostgreSqlAppDbContext(o.Options,new Scope(tenant));}
 [PostgreSqlFact]
 public async Task PlatformSettingsKeepSecretsMaskedAndPreserveUnsetValues()
 {
  var tenant=Guid.NewGuid();var user=Guid.NewGuid();var stamp=Guid.NewGuid().ToString();
  await using(var db=Open(tenant,administrative:true)){await db.Database.MigrateAsync();db.Users.Add(new ApplicationUser{Id=user,UserName=user.ToString(),NormalizedUserName=user.ToString(),EmailConfirmed=true,IsPlatformAdministrator=true,SecurityStamp=stamp});db.Tenants.Add(new Tenant{Id=tenant,TenantId=tenant,Name="Settings test"});db.TenantMemberships.Add(new TenantMembership{TenantId=tenant,UserId=user,Role="Owner"});await db.SaveChangesAsync();}
  var root=Path.Combine(Path.GetTempPath(),"appsouls-settings-"+Guid.NewGuid().ToString("N"));
  try{
   var vault=new FileConfigurationVault(new(Path.Combine(root,"vault","settings"),Path.Combine(root,"key","master")));
   var configuration=new ServerConfiguration{DatabaseConnectionString=Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!,PublicBaseUrl="https://example.test",DataProtectionKeyDirectory=Path.Combine(root,"ring")};await vault.InitializeAsync(configuration);
   var service=new ServerSettingsService(vault,configuration,new WorkspaceSessionFactory(new Factory(tenant)),new Actor(user,tenant,null,"Owner",stamp),NullLogger<ServerSettingsService>.Instance);
   var secret="test-secret-"+Guid.NewGuid();await service.UpdateEmailAsync(new(false,"smtp.example.test",587,"support@example.test","smtp-user",secret));
   await service.UpdateEmailAsync(new(false,"smtp.example.test",587,"support@example.test","smtp-user",null));
   Assert.Equal(secret,(await vault.LoadAsync()).Smtp.Password);Assert.Equal(secret,configuration.Smtp.Password);
   await service.UpdateIntegrationAsync(new("Payments","https://api.example.test","ApiKey",secret));await service.UpdateIntegrationAsync(new("Payments","https://api.example.test","ApiKey",null));
   var snapshot=await service.GetAsync();Assert.True(snapshot.SmtpPasswordConfigured);Assert.True(snapshot.DatabaseConnectionConfigured);Assert.Equal("ApiKey",Assert.Single(snapshot.Integrations).ConfiguredCredentialNames.Single());Assert.DoesNotContain(secret,JsonSerializer.Serialize(snapshot));Assert.Equal(secret,(await vault.LoadAsync()).Integrations["Payments"].Credentials["ApiKey"]);
   await service.UpdateIntegrationAsync(new("DatabaseAdapter","","ConnectionString",secret));
   Assert.Equal(secret,(await vault.LoadAsync()).Integrations["DatabaseAdapter"].Credentials["ConnectionString"]);
   await service.UpdateDatabaseAsync(new("PostgreSql",null));Assert.Equal(configuration.DatabaseConnectionString,(await vault.LoadAsync()).DatabaseConnectionString);
   await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateDatabaseAsync(new("SqlServer",null)));
   await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateIntegrationAsync(new("Unsafe","http://api.example.test","Key",secret)));
   await using(var db=Open(tenant)){var record=await db.Users.SingleAsync(x=>x.Id==user);record.IsPlatformAdministrator=false;await db.SaveChangesAsync();}
   await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.GetAsync());
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 private static async Task WithSettings(Func<ServerSettingsService,FileConfigurationVault,ServerConfiguration,Guid,Guid,Task> action, TicketingCRM.Domain.Delivery.Interfaces.IArtifactScannerProbe? scanner = null)
 {
  var tenant=Guid.NewGuid();var user=Guid.NewGuid();var stamp=Guid.NewGuid().ToString();
  await using(var db=Open(tenant,administrative:true)){await db.Database.MigrateAsync();db.Users.Add(new ApplicationUser{Id=user,UserName=user.ToString(),EmailConfirmed=true,IsPlatformAdministrator=true,SecurityStamp=stamp});db.Tenants.Add(new Tenant{Id=tenant,TenantId=tenant,Name="Settings fixture"});db.TenantMemberships.Add(new TenantMembership{TenantId=tenant,UserId=user,Role="Owner"});await db.SaveChangesAsync();}
  var root=Path.Combine(Path.GetTempPath(),"appsouls-settings-"+Guid.NewGuid().ToString("N"));
  try
  {
   var vault=new FileConfigurationVault(new(Path.Combine(root,"vault","settings"),Path.Combine(root,"key","master")));
   var config=new ServerConfiguration{DatabaseConnectionString=Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!,PublicBaseUrl="https://example.test",DataProtectionKeyDirectory=Path.Combine(root,"ring")};await vault.InitializeAsync(config);
   var service=new ServerSettingsService(vault,config,new WorkspaceSessionFactory(new Factory(tenant)),new Actor(user,tenant,null,"Owner",stamp),NullLogger<ServerSettingsService>.Instance,scanner:scanner);
   await action(service,vault,config,tenant,user);
  }
  finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 [PostgreSqlFact]
 public Task SettingsExposeStructuredConnectionWithoutSecretsAndKeepOrganizationScoped() => WithSettings(async(service,vault,active,tenant,user)=>
 {
  var snapshot=await service.GetAsync();Assert.NotNull(snapshot.Database);Assert.True(snapshot.Database.PasswordConfigured);Assert.Equal("Settings fixture",snapshot.OrganizationName);
  var password=new Npgsql.NpgsqlConnectionStringBuilder(active.DatabaseConnectionString).Password!;
  Assert.DoesNotContain(password,JsonSerializer.Serialize(snapshot));
  var other=Guid.NewGuid();await using(var db=Open(other)){db.Tenants.Add(new Tenant{Id=other,TenantId=other,Name="Other organization"});await db.SaveChangesAsync();}
  await service.UpdateOrganizationAsync(new("New organization"));Assert.Equal("New organization",(await service.GetAsync()).OrganizationName);
  await using(var db=Open(other))Assert.Equal("Other organization",(await db.Tenants.SingleAsync()).Name);
  await service.UpdatePublicAddressAsync(new("https://crm.example.test/"));Assert.Equal("https://crm.example.test",active.PublicBaseUrl);Assert.Equal(active.PublicBaseUrl,(await vault.LoadAsync()).PublicBaseUrl);
  foreach(var url in new[]{"http://crm.example.test","https://user:private@crm.example.test","https://crm.example.test?token=private","https://crm.example.test/#fragment"})
   await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdatePublicAddressAsync(new(url)));
  Assert.Equal("https://crm.example.test",(await vault.LoadAsync()).PublicBaseUrl);
  await using(var db=Open(tenant)){(await db.Users.SingleAsync(x=>x.Id==user)).SecurityStamp="revoked";await db.SaveChangesAsync();}
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.UpdateOrganizationAsync(new("Rejected")));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.TestEmailAsync(new(false,"",587,"","",null)));
 });
 [PostgreSqlFact]
 public Task EmailCheckRequiresTlsDoesNotSendAndNeverPersistsTestValuesOrForwardsSavedSecretsToChangedTarget() => WithSettings(async(service,vault,active,tenant,user)=>
 {
  using var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));var commands=new List<string>();
  var server=Task.Run(async()=>
  {
   using var socket=await listener.AcceptTcpClientAsync(timeout.Token);using var stream=socket.GetStream();using var reader=new StreamReader(stream);using var writer=new StreamWriter(stream){NewLine="\r\n",AutoFlush=true};
   await writer.WriteLineAsync("220 fixture ESMTP");commands.Add(await reader.ReadLineAsync(timeout.Token)??"");await writer.WriteLineAsync("250 fixture");
   try{string? command;while((command=await reader.ReadLineAsync(timeout.Token))is not null)commands.Add(command);}catch(IOException){}
  },timeout.Token);
  var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;var secret="test-only-private";
  await service.UpdateEmailAsync(new(false,"127.0.0.1",port,"sender@example.test","smtp-user",secret));
  var before=JsonSerializer.Serialize(await vault.LoadAsync());
  var result=await service.TestEmailAsync(new(false,"127.0.0.1",port,"sender@example.test","smtp-user",null));await server;
  Assert.False(result.Success);Assert.DoesNotContain(secret,result.Message);Assert.DoesNotContain(commands,c=>c.StartsWith("AUTH")||c.StartsWith("MAIL")||c.StartsWith("DATA"));
  Assert.Equal(before,JsonSerializer.Serialize(await vault.LoadAsync()));
  var changed=await service.TestEmailAsync(new(false,"different.example.test",port,"sender@example.test","smtp-user",null));
  Assert.False(changed.Success);Assert.Contains("password",changed.Message);
  await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateEmailAsync(new(false,"different.example.test",port,"sender@example.test","smtp-user",null)));
  Assert.Equal(before,JsonSerializer.Serialize(await vault.LoadAsync()));
 });
 [PostgreSqlFact]
 public Task StructuredDatabaseChecksReuseOnlyExactConnectionPasswordsAndNeverModifyVault() => WithSettings(async(service,vault,active,tenant,user)=>
 {
  var snapshot=await service.GetAsync();var fields=snapshot.Database!;var coordinator=new DatabaseMaintenanceCoordinator();
  var maintenance=new DatabaseMaintenanceService(service,vault,active,new Actor(user,tenant,null,"Owner",(await OpenStamp(user,tenant))),coordinator);
  var request=new TicketingCRM.Domain.Configuration.DTOs.DatabaseMaintenanceRequest{Provider="PostgreSql",Mode="Switch",DatabaseServer=fields.Server,DatabasePort=fields.Port,DatabaseCatalog=fields.Catalog,DatabaseUserName=fields.UserName,DatabaseUseTls=fields.UseTls};
  var before=JsonSerializer.Serialize(await vault.LoadAsync());var state=await maintenance.InspectAsync(request);Assert.Equal("Current",state.State);Assert.True(state.HasAccounts);Assert.Equal(before,JsonSerializer.Serialize(await vault.LoadAsync()));
  request.DatabaseCatalog="changed_target";request.ConnectionString="";
  await Assert.ThrowsAsync<ArgumentException>(()=>maintenance.InspectAsync(request));Assert.False(coordinator.MaintenanceMode);Assert.Equal(before,JsonSerializer.Serialize(await vault.LoadAsync()));
 });
 private static async Task<string> OpenStamp(Guid user,Guid tenant){await using var db=Open(tenant);return(await db.Users.SingleAsync(x=>x.Id==user)).SecurityStamp!;}

 private sealed class Probe : TicketingCRM.Domain.Delivery.Interfaces.IArtifactScannerProbe
 {
  public int Calls;
  public Task<TicketingCRM.Domain.Delivery.DTOs.ArtifactScannerAvailability> CheckAsync(CancellationToken ct=default){Calls++;return Task.FromResult(new TicketingCRM.Domain.Delivery.DTOs.ArtifactScannerAvailability(true,3310,"scanner-connected","Scanner fixture"));}
 }
 [PostgreSqlFact]
 public async Task ScannerCheckRevalidatesAdministratorBeforeInvokingProbe()
 {
  var probe=new Probe();await WithSettings(async(service,vault,active,tenant,user)=>
  {
   Assert.True((await service.CheckScannerAsync()).Available);Assert.Equal(1,probe.Calls);
   await using(var db=Open(tenant)){(await db.Users.SingleAsync(x=>x.Id==user)).IsPlatformAdministrator=false;await db.SaveChangesAsync();}
   await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CheckScannerAsync());Assert.Equal(1,probe.Calls);
  },probe);
 }
 [PostgreSqlFact]
 public Task ScannerPortOverridesReplaceTheRunningSnapshotWithoutRestartAndInvalidUpdatesPreserveThePreviousValue() => WithSettings(async(service,vault,active,tenant,user)=>
 {
  var original=active.Integrations;
  await service.UpdateIntegrationAsync(new("malware-scanner","","Port","4242"));Assert.NotSame(original,active.Integrations);Assert.Empty(original);
  Assert.Equal(4242,new TicketingCRM.Core.FileProcessing.ClamAvArtifactScanner(active).LocalPort);
  await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateIntegrationAsync(new("malware-scanner","","Port","invalid")));
  await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateIntegrationAsync(new("malware-scanner","https://remote.example.test","Port","4242")));
  Assert.Equal(4242,new TicketingCRM.Core.FileProcessing.ClamAvArtifactScanner(active).LocalPort);
  Assert.Equal("4242",(await vault.LoadAsync()).Integrations["malware-scanner"].Credentials["Port"]);
 });

}
