using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.Domain.Configuration.DTOs;

namespace TicketingCRM.DataConnection;

/// <summary>Offline, streaming copy into an empty catalog. Target data commits as one transaction; source is never changed.</summary>
public static class DatabaseCopy
{
    public static async Task CopyAsync(DatabaseProvider sourceProvider, string sourceConnection,
        DatabaseProvider targetProvider, string targetConnection, Action<DatabaseTransferProgress> progress, CancellationToken ct = default)
    {
        var timer = Stopwatch.StartNew();
        progress(new("Inspecting source", null));
        var sourceState = await DatabaseLifecycle.InspectAsync(sourceProvider, sourceConnection, ct);
        if (sourceState.State != "Current") throw new InvalidOperationException("Upgrade the source App Souls database before copying it.");
        var targetState = await DatabaseLifecycle.InspectAsync(targetProvider, targetConnection, ct);
        if (targetState.HasAccounts) throw new InvalidOperationException("Copy requires an empty target. Existing records are never overwritten or merged.");
        await using var source = DatabaseLifecycle.Open(sourceProvider, sourceConnection);
        await using var target = DatabaseLifecycle.Open(targetProvider, targetConnection);
        var tables = OrderedTables(source.Model.GetRelationalModel().Tables.ToArray());
        var destinationTables = target.Model.GetRelationalModel().Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);
        progress(new("Preparing target catalog and schema", null));
        await DatabaseLifecycle.ApplyAsync(targetProvider, targetConnection, ct);
        await source.Database.OpenConnectionAsync(ct); await target.Database.OpenConnectionAsync(ct);
        await using var readTransaction = await source.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var writeTransaction = await target.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var counts = new Dictionary<string, long>();
        long total = 0, copied = 0;
        foreach (var table in tables)
        {
            // Lock all source tables before copying. A second writer cannot silently change a counted table.
            if (sourceProvider == DatabaseProvider.PostgreSql)
                await Execute(source.Database.GetDbConnection(), readTransaction.GetDbTransaction(), $"LOCK TABLE {Name(table, sourceProvider)} IN SHARE MODE", ct);
            var count = await Count(source.Database.GetDbConnection(), readTransaction.GetDbTransaction(), Name(table, sourceProvider), sourceProvider, ct, true);
            counts[table.Name] = count; total += count;
            if (await Count(target.Database.GetDbConnection(), writeTransaction.GetDbTransaction(), Name(destinationTables[table.Name], targetProvider), targetProvider, ct, true) != 0)
                throw new InvalidOperationException("Copy requires an empty target. Existing records are never overwritten or merged.");
        }
        long totalBytes = 0, copiedBytes = 0;
        progress(new("Measuring logical payload", null, TotalRows: total));
        foreach (var table in tables)
        {
            await using var measure = source.Database.GetDbConnection().CreateCommand(); measure.Transaction = readTransaction.GetDbTransaction(); measure.CommandTimeout = 120;
            measure.CommandText = $"SELECT {string.Join(',', table.Columns.Select(c => Quote(c.Name, sourceProvider)))} FROM {Name(table, sourceProvider)}";
            await using var scan = await measure.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            while (await scan.ReadAsync(ct)) for (var i = 0; i < scan.FieldCount; i++) totalBytes += LogicalSize(scan.GetValue(i));
        }
        var copyTimer = Stopwatch.StartNew();
        foreach (var table in tables)
        {
            Report("Copying records", table.Name);
            var destination = destinationTables[table.Name];
            var columns = table.Columns.ToArray();
            var names = columns.Select(c => Quote(c.Name, sourceProvider));
            await using var select = source.Database.GetDbConnection().CreateCommand();
            select.Transaction = readTransaction.GetDbTransaction(); select.CommandTimeout = 120;
            select.CommandText = $"SELECT {string.Join(',', names)} FROM {Name(table, sourceProvider)}";
            await using var rows = await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var identity = targetProvider == DatabaseProvider.SqlServer && destination.Columns.Any(c => c.PropertyMappings.Any(m => m.Property.FindAnnotation("SqlServer:ValueGenerationStrategy")?.Value?.ToString() == "IdentityColumn"));
            if (identity) await Execute(target.Database.GetDbConnection(), writeTransaction.GetDbTransaction(), $"SET IDENTITY_INSERT {Name(destination, targetProvider)} ON", ct);
            await using var insert = target.Database.GetDbConnection().CreateCommand();
            insert.Transaction = writeTransaction.GetDbTransaction(); insert.CommandTimeout = 120;
            insert.CommandText = $"INSERT INTO {Name(destination, targetProvider)} ({string.Join(',', columns.Select(c => Quote(c.Name, targetProvider)))}) VALUES ({string.Join(',', columns.Select((_, i) => "@p" + i))})";
            var properties = columns.Select(c => destination.Columns.Single(d => d.Name == c.Name).PropertyMappings.First().Property).ToArray();
            long tableCopied = 0;
            while (await rows.ReadAsync(ct))
            {
                insert.Parameters.Clear();
                for (var i = 0; i < columns.Length; i++)
                {
                    object? value = await rows.IsDBNullAsync(i, ct) ? null : rows.GetValue(i);
                    copiedBytes += LogicalSize(value);
                    var property = properties[i];
                    var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    if (value is not null && type.IsEnum) value = Enum.ToObject(type, value);
                    if (value is DateTime date && type == typeof(DateTimeOffset)) value = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc));
                    insert.Parameters.Add(property.GetRelationalTypeMapping().CreateParameter(insert, "p" + i, value, property.IsNullable));
                }
                await insert.ExecuteNonQueryAsync(ct); copied++; tableCopied++;
                if (tableCopied % 128 == 0) Report("Copying records", table.Name);
            }
            if (identity) await Execute(target.Database.GetDbConnection(), writeTransaction.GetDbTransaction(), $"SET IDENTITY_INSERT {Name(destination, targetProvider)} OFF", ct);
            // Explicit integer identity values also need their PostgreSQL sequences advanced.
            if (targetProvider == DatabaseProvider.PostgreSql)
                foreach (var column in destination.Columns.Where(c => c.PropertyMappings.Any(m => m.Property.FindAnnotation("Npgsql:ValueGenerationStrategy")?.Value?.ToString()?.StartsWith("Identity", StringComparison.Ordinal) == true)))
                {
                    await using var reset = target.Database.GetDbConnection().CreateCommand(); reset.Transaction = writeTransaction.GetDbTransaction();
                    reset.CommandText = $"SELECT setval(pg_get_serial_sequence(@table, @column), COALESCE(MAX({Quote(column.Name, targetProvider)}), 1), COUNT(*)>0) FROM {Name(destination, targetProvider)}";
                    DatabaseLifecycle.Add(reset, "table", Name(destination, targetProvider)); DatabaseLifecycle.Add(reset, "column", column.Name);
                    await reset.ExecuteScalarAsync(ct);
                }
            Report("Copied table", table.Name);
        }
        progress(new("Verifying row counts", 92, RowsCopied: copied, TotalRows: total, ElapsedSeconds: timer.Elapsed.TotalSeconds));
        foreach (var table in tables)
            if (await Count(target.Database.GetDbConnection(), writeTransaction.GetDbTransaction(), Name(destinationTables[table.Name], targetProvider), targetProvider, ct, false) != counts[table.Name])
                throw new InvalidOperationException("Target verification failed. The target data transaction was rolled back.");
        progress(new("Committing target data", 97, RowsCopied: copied, TotalRows: total, ElapsedSeconds: timer.Elapsed.TotalSeconds));
        await writeTransaction.CommitAsync(ct);
        await readTransaction.CommitAsync(ct);
        progress(new("Copy verified", 99, RowsCopied: copied, TotalRows: total, ElapsedSeconds: timer.Elapsed.TotalSeconds, BytesCopied: copiedBytes, TotalBytes: totalBytes, BytesPerSecond: copiedBytes / Math.Max(copyTimer.Elapsed.TotalSeconds, .001)));
        void Report(string stage, string table) => progress(new(stage, 10 + (total == 0 ? 80 : 80.0 * copied / total), table, copied, total, copied / Math.Max(copyTimer.Elapsed.TotalSeconds, .001), timer.Elapsed.TotalSeconds, BytesCopied: copiedBytes, TotalBytes: totalBytes, BytesPerSecond: copiedBytes / Math.Max(copyTimer.Elapsed.TotalSeconds, .001)));
    }

    // Canonical field sizes, not wire traffic, database/index size or protocol overhead.
    private static long LogicalSize(object? value) => value switch
    {
        null or DBNull => 0,
        byte[] bytes => bytes.LongLength,
        string text => System.Text.Encoding.UTF8.GetByteCount(text),
        bool or byte or sbyte => 1,
        short or ushort or char => 2,
        int or uint or float => 4,
        long or ulong or double or DateTime or DateTimeOffset => 8,
        decimal or Guid => 16,
        _ => throw new NotSupportedException("A source field type needs an explicit database-transfer mapping.")
    };

    public static IReadOnlyList<ITable> OrderedTables(IReadOnlyList<ITable> tables)
    {
        var result = new List<ITable>(); var remaining = tables.ToList();
        while (remaining.Count != 0)
        {
            var ready = remaining.FirstOrDefault(t => t.ForeignKeyConstraints.All(f => f.PrincipalTable == t || result.Contains(f.PrincipalTable)));
            if (ready is null) throw new InvalidOperationException("The schema contains cyclic dependencies requiring a dedicated copy strategy.");
            result.Add(ready); remaining.Remove(ready);
        }
        return result;
    }
    private static string Quote(string name, DatabaseProvider provider) => provider == DatabaseProvider.PostgreSql ? "\"" + name.Replace("\"", "\"\"") + "\"" : "[" + name.Replace("]", "]]") + "]";
    private static string Name(ITable table, DatabaseProvider provider) => Quote(table.Schema ?? (provider == DatabaseProvider.PostgreSql ? "public" : "dbo"), provider) + "." + Quote(table.Name, provider);
    private static async Task Execute(DbConnection connection, DbTransaction transaction, string sql, CancellationToken ct)
    { await using var cmd = connection.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = sql; cmd.CommandTimeout = 120; await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<long> Count(DbConnection connection, DbTransaction transaction, string table, DatabaseProvider provider, CancellationToken ct, bool locked)
    {
        await using var cmd = connection.CreateCommand(); cmd.Transaction = transaction; cmd.CommandTimeout = 120;
        cmd.CommandText = $"SELECT {(provider == DatabaseProvider.SqlServer ? "COUNT_BIG" : "COUNT")}(*) FROM {table}{(locked && provider == DatabaseProvider.SqlServer ? " WITH (TABLOCK,HOLDLOCK)" : "")}";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }
}
