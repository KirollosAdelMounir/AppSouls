using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.Domain.Configuration.DTOs;

namespace TicketingCRM.DataConnection;

/// <summary>Explicit installation-wide catalog operations. Never registered as a tenant repository or run at startup.</summary>
public static class DatabaseLifecycle
{
    public static AppDbContext Open(DatabaseProvider provider, string connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureProvider(options, provider, connection, administrative: true);
        return provider == DatabaseProvider.PostgreSql ? new PostgreSqlAppDbContext(options.Options) : new SqlServerAppDbContext(options.Options);
    }

    public static string ServerConnection(DatabaseProvider provider, string connection) => provider switch
    {
        DatabaseProvider.PostgreSql => new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false }.ConnectionString,
        DatabaseProvider.SqlServer => new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", Pooling = false, AttachDBFilename = "" }.ConnectionString,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static string Catalog(DatabaseProvider provider, string connection) => provider == DatabaseProvider.PostgreSql
        ? new NpgsqlConnectionStringBuilder(connection).Database ?? "" : new SqlConnectionStringBuilder(connection).InitialCatalog;

    public static void ValidateHistory(IReadOnlyList<string> known, IReadOnlyList<string> applied)
    {
        if (applied.Count == 0 || applied.Count > known.Count || !applied.SequenceEqual(known.Take(applied.Count), StringComparer.Ordinal))
            throw new InvalidOperationException("The catalog has unknown, newer or inconsistent App Souls migrations. No schema changes were applied.");
    }

    public static async Task<DatabaseInspection> InspectAsync(DatabaseProvider provider, string connection, CancellationToken ct = default)
    {
        ValidateTransport(provider, connection);
        var catalog = Catalog(provider, connection);
        if (string.IsNullOrWhiteSpace(catalog) || (provider == DatabaseProvider.PostgreSql ? System.Text.Encoding.UTF8.GetByteCount(catalog) : catalog.Length) > (provider == DatabaseProvider.PostgreSql ? 63 : 128))
            throw new ArgumentException("Enter a catalog name within the database provider's identifier limit.");
        if (provider == DatabaseProvider.PostgreSql && catalog is "postgres" or "template0" or "template1" ||
            provider == DatabaseProvider.SqlServer && new[] { "master", "model", "msdb", "tempdb" }.Contains(catalog, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an application catalog, not a system database.");
        await using var server = Open(provider, ServerConnection(provider, connection));
        await server.Database.OpenConnectionAsync(ct);
        await using var exists = server.Database.GetDbConnection().CreateCommand();
        exists.CommandText = provider == DatabaseProvider.PostgreSql ? "SELECT COUNT(*) FROM pg_database WHERE datname=@name" : "SELECT COUNT(*) FROM sys.databases WHERE name=@name";
        Add(exists, "name", catalog);
        var present = Convert.ToInt64(await exists.ExecuteScalarAsync(ct)) != 0;
        await using var db = Open(provider, connection);
        var known = db.Database.GetMigrations().ToArray();
        if (!present) return new("Missing", 0, known.Length, false);
        await db.Database.OpenConnectionAsync(ct);
        await using var tables = db.Database.GetDbConnection().CreateCommand();
        tables.CommandText = provider == DatabaseProvider.PostgreSql
            ? "SELECT table_name,table_schema FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_schema NOT LIKE 'pg_toast%' AND table_type='BASE TABLE'"
            : "SELECT TABLE_NAME,TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'";
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await tables.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct))
        {
            if (reader.GetString(1) != (provider == DatabaseProvider.PostgreSql ? "public" : "dbo"))
                throw new InvalidOperationException("The catalog contains unrecognized application tables. No schema changes were applied.");
            names.Add(reader.GetString(0));
        }
        if (names.Count == 0) return new("Empty", 0, known.Length, false);
        var history = provider == DatabaseProvider.PostgreSql ? "__PostgreSqlMigrationsHistory" : "__SqlServerMigrationsHistory";
        if (!names.Contains(history)) throw new InvalidOperationException("This is not a recognized App Souls catalog. No schema changes were applied.");
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if (applied.Length == 0 && names.SetEquals([history])) return new("Empty", 0, known.Length, false);
        ValidateHistory(known, applied);
        // Compare against the model for the APPLIED revision, not today's model: old valid schemas are upgradeable.
        var assembly = db.GetService<IMigrationsAssembly>();
        var appliedModel = assembly.CreateMigration(assembly.Migrations[applied[^1]], db.Database.ProviderName!).TargetModel;
        var expected = appliedModel.GetEntityTypes().Select(t => t.GetTableName()!).Append(history).ToHashSet(StringComparer.Ordinal);
        if (!names.SetEquals(expected))
            throw new InvalidOperationException("The catalog does not match the App Souls schema. No schema changes were applied.");
        await using var columns = db.Database.GetDbConnection().CreateCommand();
        columns.CommandText = "SELECT table_name,column_name FROM information_schema.columns WHERE table_schema=@schema";
        Add(columns, "schema", provider == DatabaseProvider.PostgreSql ? "public" : "dbo");
        var actualColumns = new HashSet<(string, string)>();
        await using (var reader = await columns.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) actualColumns.Add((reader.GetString(0), reader.GetString(1)));
        var expectedColumns = new HashSet<(string, string)> { (history, "MigrationId"), (history, "ProductVersion") };
        foreach (var entity in appliedModel.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties()) expectedColumns.Add((table.Name, property.GetColumnName(table)!));
        }
        if (!actualColumns.SetEquals(expectedColumns)) throw new InvalidOperationException("The catalog has schema drift. No schema changes were applied.");
        var accounts = await db.Users.AsNoTracking().AnyAsync(ct);
        return new(applied.Length == known.Length ? "Current" : "UpgradeRequired", applied.Length, known.Length - applied.Length, accounts);
    }

    /// <summary>Startup upgrades are limited to an already initialized, recognized installation.</summary>
    public static async Task<DatabaseInspection> UpgradeExistingAsync(DatabaseProvider provider, string connection, CancellationToken ct = default)
    {
        var inspection = await InspectAsync(provider, connection, ct);
        if (inspection.AppliedMigrations == 0 || !inspection.HasAccounts || inspection.State is not ("Current" or "UpgradeRequired"))
            throw new InvalidOperationException("First-time database activation must be completed through guided server setup.");
        if (inspection.State == "Current") return inspection;
        // EF uses the provider migration lock. No request host or outbox worker is running at this point.
        var upgraded = await ApplyAsync(provider, connection, ct);
        if (upgraded.State != "Current" || !upgraded.HasAccounts)
            throw new InvalidOperationException("The updated database could not be verified. Application services were not started.");
        return upgraded;
    }

    public static async Task<DatabaseInspection> ApplyAsync(DatabaseProvider provider, string connection, CancellationToken ct = default)
    {
        var inspection = await InspectAsync(provider, connection, ct);
        if (inspection.State == "Current") return inspection; // No DDL or migration history writes for a current catalog.
        await using var db = Open(provider, connection);
        await db.Database.MigrateAsync(ct); // Provider creates the catalog when missing. Never EnsureCreated/EnsureDeleted.
        return await InspectAsync(provider, connection, ct);
    }

    internal static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter);
    }

    public static void ValidateTransport(DatabaseProvider provider, string connection)
    {
        if (provider == DatabaseProvider.PostgreSql)
        {
            var pg = new NpgsqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(pg.Host)) throw new ArgumentException("Specify the PostgreSQL server explicitly.");
            if (!pg.Host.Split(',').All(Local) && pg.SslMode != SslMode.VerifyFull)
                throw new ArgumentException("Remote PostgreSQL transfers require certificate-validated TLS (VerifyFull).");
        }
        else if (provider == DatabaseProvider.SqlServer)
        {
            var sql = new SqlConnectionStringBuilder(connection);
            var host = sql.DataSource;
            if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) host = host[4..];
            if (!Local(host.Split('\\')[0].Split(',')[0]) && !host.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) &&
                (sql.Encrypt == SqlConnectionEncryptOption.Optional || sql.TrustServerCertificate))
                throw new ArgumentException("Remote SQL Server transfers require certificate-validated TLS.");
        }
        else throw new ArgumentOutOfRangeException(nameof(provider));
        static bool Local(string host) => host.Trim('[', ']').Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "." or "(local)" ||
            System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var ip) && System.Net.IPAddress.IsLoopback(ip);
    }
}
