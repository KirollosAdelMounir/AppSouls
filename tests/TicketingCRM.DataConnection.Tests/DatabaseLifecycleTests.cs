using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using Xunit;

namespace TicketingCRM.DataConnection.Tests;

public sealed class DatabaseLifecycleTests
{
    [Fact]
    public void RemoteTransfersRequireVerifiedTlsButLocalDevelopmentConnectionsRemainAvailable()
    {
        Assert.Throws<ArgumentException>(() => DatabaseLifecycle.ValidateTransport(DatabaseProvider.PostgreSql, "Host=db.example.test;Database=app;SSL Mode=Prefer"));
        DatabaseLifecycle.ValidateTransport(DatabaseProvider.PostgreSql, "Host=db.example.test;Database=app;SSL Mode=VerifyFull");
        DatabaseLifecycle.ValidateTransport(DatabaseProvider.PostgreSql, "Host=127.0.0.1;Database=app;SSL Mode=Disable");
        Assert.Throws<ArgumentException>(() => DatabaseLifecycle.ValidateTransport(DatabaseProvider.SqlServer, "Server=db.example.test;Database=app;Encrypt=false"));
        Assert.Throws<ArgumentException>(() => DatabaseLifecycle.ValidateTransport(DatabaseProvider.SqlServer, "Server=db.example.test;Database=app;Encrypt=true;TrustServerCertificate=true"));
        DatabaseLifecycle.ValidateTransport(DatabaseProvider.SqlServer, "Server=db.example.test;Database=app;Encrypt=true;TrustServerCertificate=false");
    }
    [PostgreSqlFact]
    public async Task MaintenanceContextsAllowReadsAndRejectWritesBelowTheHttpAndBlazorBoundary()
    {
        await using var fixture = new Fixture(); var connection = fixture.NewConnection();
        await DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, connection);
        using var services = new ServiceCollection().AddDataConnection(DatabaseProvider.PostgreSql, connection, true).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TicketingCRM.DataConnection.DbContexts.AppDbContext>>().CreateDbContext();
        Assert.False(await db.Users.AnyAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUsers\""));
        db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = "blocked" });
        var failure = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        Assert.NotNull(failure); Assert.True(failure is InvalidOperationException || failure is DbUpdateException { InnerException: InvalidOperationException });
        await using var verify = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, connection); Assert.Empty(await verify.Users.ToListAsync());
    }
    [Fact]
    public void HistoryAcceptsOnlyAnOrderedKnownPrefixAndRejectsUnknownNewerOrGappedSchemas()
    {
        string[] known = ["001_App", "002_App", "003_App"];
        DatabaseLifecycle.ValidateHistory(known, ["001_App"]);
        DatabaseLifecycle.ValidateHistory(known, known);
        Assert.Throws<InvalidOperationException>(() => DatabaseLifecycle.ValidateHistory(known, []));
        Assert.Throws<InvalidOperationException>(() => DatabaseLifecycle.ValidateHistory(known, ["001_App", "003_App"]));
        Assert.Throws<InvalidOperationException>(() => DatabaseLifecycle.ValidateHistory(known, ["001_Other"]));
        Assert.Throws<InvalidOperationException>(() => DatabaseLifecycle.ValidateHistory(known, [.. known, "004_Future"]));
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void ServerProbeUsesSystemCatalogAndModelTablesHaveDependencyOrder(DatabaseProvider provider)
    {
        var connection = provider == DatabaseProvider.PostgreSql ? "Host=127.0.0.1;Database=missing;Username=test;Password=opaque" : "Server=127.0.0.1;Database=missing;User Id=test;Password=opaque;Encrypt=true";
        var server = DatabaseLifecycle.ServerConnection(provider, connection);
        Assert.Equal(provider == DatabaseProvider.PostgreSql ? "postgres" : "master", DatabaseLifecycle.Catalog(provider, server));
        Assert.Equal("missing", DatabaseLifecycle.Catalog(provider, connection));
        using var db = DatabaseLifecycle.Open(provider, connection);
        var tables = DatabaseCopy.OrderedTables(db.Model.GetRelationalModel().Tables.ToArray());
        Assert.Equal(db.Model.GetRelationalModel().Tables.Count(), tables.Count);
        var seen = new HashSet<Microsoft.EntityFrameworkCore.Metadata.ITable>();
        foreach (var table in tables)
        {
            Assert.All(table.ForeignKeyConstraints, fk => Assert.True(fk.PrincipalTable == table || seen.Contains(fk.PrincipalTable)));
            seen.Add(table);
        }
    }

    [PostgreSqlFact]
    public async Task MissingCatalogCheckIsReadOnlyAndExplicitApplyCreatesThenLeavesCurrentHistoryUnchanged()
    {
        await using var fixture = new Fixture(); var connection = fixture.NewConnection();
        Assert.Equal("Missing", (await DatabaseLifecycle.InspectAsync(DatabaseProvider.PostgreSql, connection)).State);
        Assert.Equal("Missing", (await DatabaseLifecycle.InspectAsync(DatabaseProvider.PostgreSql, connection)).State);
        Assert.Equal("Current", (await DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, connection)).State);
        await using var db = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, connection);
        await db.Database.OpenConnectionAsync();
        var before = await HistoryTransaction(db);
        await DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, connection);
        Assert.Equal(before, await HistoryTransaction(db));
    }

    [PostgreSqlFact]
    public async Task OlderAppSchemaUpgradesWithoutLosingAccountsAndForeignSchemaIsNeverModified()
    {
        await using var fixture = new Fixture(); var connection = fixture.NewConnection();
        var userId = Guid.NewGuid();
        await using (var old = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, connection))
        {
            var migrations = old.Database.GetMigrations().ToArray();
            await old.GetService<IMigrator>().MigrateAsync(migrations[^2]);
            old.Users.Add(new ApplicationUser { Id = userId, UserName = "retained", NormalizedUserName = "RETAINED", PasswordHash = "opaque-hash" });
            await old.SaveChangesAsync();
        }
        Assert.Equal("UpgradeRequired", (await DatabaseLifecycle.InspectAsync(DatabaseProvider.PostgreSql, connection)).State);
        Assert.Equal("Current", (await DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, connection)).State);
        await using (var current = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, connection))
            Assert.Equal("opaque-hash", (await current.Users.SingleAsync(x => x.Id == userId)).PasswordHash);
        var foreign = fixture.NewConnection();
        await using (var db = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, foreign))
        {
            await db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE unrelated (value integer); INSERT INTO unrelated VALUES (42)");
            await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, foreign));
            await using var command = db.Database.GetDbConnection().CreateCommand(); await db.Database.OpenConnectionAsync();
            command.CommandText = "SELECT value FROM unrelated"; Assert.Equal(42, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
    }

    [PostgreSqlFact]
    public async Task CopyPreservesIdentityTenantDataAndProgressAndRefusesAnOccupiedTarget()
    {
        await using var fixture = new Fixture(); var source = fixture.NewConnection(); var target = fixture.NewConnection();
        await DatabaseLifecycle.ApplyAsync(DatabaseProvider.PostgreSql, source);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var customer = Guid.NewGuid();
        await using (var db = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, source))
        {
            // Maintenance fixtures deliberately insert through SQL, outside normal tenant repositories.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Tenants\" (\"Id\",\"TenantId\",\"Version\",\"Name\",\"IsActive\") VALUES ({tenant},{tenant},{Guid.NewGuid()},'Tenant',true)");
            db.Users.Add(new ApplicationUser { Id = user, UserName = "preserved", PasswordHash = "opaque-hash", SecurityStamp = "same-stamp", IsPlatformAdministrator = true });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Customers\" (\"Id\",\"TenantId\",\"Version\",\"Name\",\"Email\",\"CompanyNumber\",\"CreatedAtUtc\") VALUES ({customer},{tenant},{Guid.NewGuid()},'Retained','private@example.test','C',{DateTimeOffset.UtcNow})");
        }
        var progress = new List<DatabaseTransferProgress>();
        await DatabaseCopy.CopyAsync(DatabaseProvider.PostgreSql, source, DatabaseProvider.PostgreSql, target, progress.Add);
        await using var check = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, target);
        Assert.Equal("same-stamp", (await check.Users.SingleAsync(x => x.Id == user)).SecurityStamp);
        Assert.Equal("private@example.test", (await check.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == customer)).Email);
        Assert.Equal(tenant, (await check.Tenants.IgnoreQueryFilters().SingleAsync()).Id);
        Assert.Equal(3, progress[^1].RowsCopied); Assert.Equal(3, progress[^1].TotalRows);
        Assert.Contains(progress, p => p.Stage == "Verifying row counts");
        Assert.All(progress.Where(p => p.Percentage.HasValue), p => Assert.InRange(p.Percentage!.Value, 0, 100));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseCopy.CopyAsync(DatabaseProvider.PostgreSql, source, DatabaseProvider.PostgreSql, target, _ => { }));
        Assert.Equal("opaque-hash", (await check.Users.SingleAsync(x => x.Id == user)).PasswordHash);
        var failedTarget = fixture.NewConnection();
        await Assert.ThrowsAsync<IOException>(() => DatabaseCopy.CopyAsync(DatabaseProvider.PostgreSql, source, DatabaseProvider.PostgreSql, failedTarget,
            value => { if (value.RowsCopied > 0) throw new IOException("Injected transfer interruption"); }));
        await using var rolledBack = DatabaseLifecycle.Open(DatabaseProvider.PostgreSql, failedTarget);
        Assert.Empty(await rolledBack.Users.ToListAsync());
        Assert.Empty(await rolledBack.Tenants.IgnoreQueryFilters().ToListAsync());
    }

    private static async Task<string> HistoryTransaction(TicketingCRM.DataConnection.DbContexts.AppDbContext db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT string_agg(xmin::text, ',' ORDER BY \"MigrationId\") FROM \"__PostgreSqlMigrationsHistory\"";
        return Convert.ToString(await command.ExecuteScalarAsync())!;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly List<string> catalogs = [];
        public string NewConnection()
        {
            var catalog = "appsouls_lifecycle_" + Guid.NewGuid().ToString("N"); catalogs.Add(catalog);
            return new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")) { Database = catalog, Pooling = false }.ConnectionString;
        }
        public async ValueTask DisposeAsync()
        {
            await using var server = new NpgsqlConnection(DatabaseLifecycle.ServerConnection(DatabaseProvider.PostgreSql, Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!));
            await server.OpenAsync();
            foreach (var catalog in catalogs) { await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{catalog}\" WITH (FORCE)", server); await command.ExecuteNonQueryAsync(); }
        }
    }
}
