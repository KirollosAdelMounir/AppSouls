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
 private static AppDbContext Open(Guid tenant){var o=new DbContextOptionsBuilder<AppDbContext>();TicketingCRM.DataConnection.DependencyInjection.ConfigureProvider(o,TicketingCRM.DataConnection.Configuration.DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!);return new PostgreSqlAppDbContext(o.Options,new Scope(tenant));}
 [PostgreSqlFact]
 public async Task PlatformSettingsKeepSecretsMaskedAndPreserveUnsetValues()
 {
  var tenant=Guid.NewGuid();var user=Guid.NewGuid();var stamp=Guid.NewGuid().ToString();
  await using(var db=Open(tenant)){await db.Database.MigrateAsync();db.Users.Add(new ApplicationUser{Id=user,UserName=user.ToString(),NormalizedUserName=user.ToString(),EmailConfirmed=true,IsPlatformAdministrator=true,SecurityStamp=stamp});db.Tenants.Add(new Tenant{Id=tenant,TenantId=tenant,Name="Settings test"});db.TenantMemberships.Add(new TenantMembership{TenantId=tenant,UserId=user,Role="Owner"});await db.SaveChangesAsync();}
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
}
