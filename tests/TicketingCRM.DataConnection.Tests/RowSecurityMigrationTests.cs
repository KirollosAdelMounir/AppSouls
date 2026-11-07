using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.Domain.Persistence.Models;
using Xunit;
namespace TicketingCRM.DataConnection.Tests;

public sealed class RowSecurityMigrationTests
{
 [Theory]
 [InlineData(DatabaseProvider.PostgreSql)]
 [InlineData(DatabaseProvider.SqlServer)]
 public void EveryTenantTableHasNativeReadAndWritePoliciesInItsProviderMigration(DatabaseProvider provider)
 {
  var options=new DbContextOptionsBuilder<AppDbContext>();DependencyInjection.ConfigureProvider(options,provider,provider==DatabaseProvider.PostgreSql?"Host=localhost;Database=policy_script;Username=script":"Server=localhost;Database=policy_script;Integrated Security=true;TrustServerCertificate=true");
  using AppDbContext db=provider==DatabaseProvider.PostgreSql?new PostgreSqlAppDbContext(options.Options):new SqlServerAppDbContext(options.Options);
  var sql=db.GetService<IMigrator>().GenerateScript();var tables=db.Model.GetEntityTypes().Where(x=>typeof(TenantRecord).IsAssignableFrom(x.ClrType)).Select(x=>x.GetTableName()!).Distinct().ToArray();Assert.NotEmpty(tables);
  foreach(var table in tables)
  {
   if(provider==DatabaseProvider.PostgreSql){Assert.Contains($"ALTER TABLE \"{table}\" FORCE ROW LEVEL SECURITY",sql);Assert.Contains($"CREATE POLICY appsouls_scope_v1 ON \"{table}\" USING",sql);}
   else{Assert.Contains($"CREATE SECURITY POLICY dbo.[AppSouls_{table}_V1]",sql);Assert.Contains($"ON dbo.[{table}] AFTER INSERT",sql);Assert.Contains($"ON dbo.[{table}] AFTER UPDATE",sql);}
  }
  if(provider==DatabaseProvider.PostgreSql){Assert.Contains("NOBYPASSRLS",sql);Assert.Contains("WITH CHECK",sql);}else{Assert.Contains("WITHOUT LOGIN",sql);Assert.Contains("TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT",sql);}
 }
}
