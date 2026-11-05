using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Configuration.DTOs;
using Xunit;

namespace TicketingCRM.DataConnection.Tests;

public sealed class CrossProviderFactAttribute : FactAttribute
{
    public CrossProviderFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPSOULS_TEST_SQL_CONNECTION")))
            Skip = "Isolated PostgreSQL and SQL Server test connections are required.";
    }
}

public sealed class CrossProviderDatabaseCopyTests
{
    [CrossProviderFact]
    public async Task PostgreSqlToSqlServerAndBackPreservesIdentityClaimsEncryptedTokensAndTenantRelationships()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var pg1 = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")) { Database = "appsouls_cross_a_" + suffix, Pooling = false }.ConnectionString;
        var pg2 = new NpgsqlConnectionStringBuilder(pg1) { Database = "appsouls_cross_b_" + suffix }.ConnectionString;
        var sql = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("APPSOULS_TEST_SQL_CONNECTION")) { InitialCatalog = "appsouls_cross_" + suffix, Pooling = false }.ConnectionString;
        var user = Guid.NewGuid(); var tenant = Guid.NewGuid(); var customer = Guid.NewGuid();
        try
        {
            await DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, pg1);
            await using (var source = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, pg1))
            {
                source.Users.Add(new ApplicationUser { Id = user, UserName = "preserved", PasswordHash = "opaque-password-hash", SecurityStamp = "unchanged", IsPlatformAdministrator = true });
                await source.SaveChangesAsync();
                await source.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Tenants\" (\"Id\",\"TenantId\",\"Version\",\"Name\",\"IsActive\") VALUES ({tenant},{tenant},{Guid.NewGuid()},'Tenant',true)");
                await source.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"TenantMemberships\" (\"Id\",\"TenantId\",\"Version\",\"UserId\",\"CustomerId\",\"Role\",\"IsActive\") VALUES ({Guid.NewGuid()},{tenant},{Guid.NewGuid()},{user},NULL,'Owner',true)");
                await source.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Customers\" (\"Id\",\"TenantId\",\"Version\",\"Name\",\"Email\",\"CompanyNumber\",\"CreatedAtUtc\") VALUES ({customer},{tenant},{Guid.NewGuid()},'عَميل Unicode','retained@example.test','C',{DateTimeOffset.UtcNow})");
                source.UserClaims.Add(new() { UserId = user, ClaimType = "test", ClaimValue = "retained" });
                source.UserTokens.Add(new() { UserId = user, LoginProvider = "Authenticator", Name = "key", Value = "protected:v1:opaque-encrypted-token" });
                await source.SaveChangesAsync();
            }
            var progress = new List<DatabaseTransferProgress>();
            await DatabaseCopy.CopyAsync(DatabaseProvider.PostgreSql, pg1, DatabaseProvider.SqlServer, sql, progress.Add);
            Assert.Equal("Current", (await DatabaseLifecycle.InspectAsync(DatabaseProvider.SqlServer, sql)).State);
            await DatabaseCopy.CopyAsync(DatabaseProvider.SqlServer, sql, DatabaseProvider.PostgreSql, pg2, progress.Add);
            await using var returned = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, pg2);
            Assert.Equal("opaque-password-hash", (await returned.Users.SingleAsync(x => x.Id == user)).PasswordHash);
            Assert.Equal("unchanged", (await returned.Users.SingleAsync(x => x.Id == user)).SecurityStamp);
            Assert.Equal("عَميل Unicode", (await returned.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == customer)).Name);
            var member = await returned.TenantMemberships.IgnoreQueryFilters().SingleAsync(); Assert.Equal(tenant, member.TenantId); Assert.Equal(user, member.UserId);
            Assert.Equal("protected:v1:opaque-encrypted-token", (await returned.UserTokens.SingleAsync()).Value);
            Assert.Equal("retained", (await returned.UserClaims.SingleAsync()).ClaimValue);
            returned.UserClaims.Add(new() { UserId = user, ClaimType = "next", ClaimValue = "sequence advanced" }); await returned.SaveChangesAsync();
            Assert.Equal(2, await returned.UserClaims.CountAsync());
            Assert.Equal(2, progress.Count(p => p.Stage == "Copy verified"));
        }
        finally
        {
            foreach (var connection in new[] { pg1, pg2 })
            {
                await using var admin = new NpgsqlConnection(DatabaseLifecycle.ServerConnection(DatabaseProvider.PostgreSql, connection)); await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{new NpgsqlConnectionStringBuilder(connection).Database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
            }
            await using var master = new SqlConnection(DatabaseLifecycle.ServerConnection(DatabaseProvider.SqlServer, sql)); await master.OpenAsync();
            var name = new SqlConnectionStringBuilder(sql).InitialCatalog;
            await using var command = new SqlCommand($"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END", master); await command.ExecuteNonQueryAsync();
        }
    }
}
