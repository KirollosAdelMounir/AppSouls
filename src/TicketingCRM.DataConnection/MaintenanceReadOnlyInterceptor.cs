using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TicketingCRM.DataConnection;

/// <summary>Defense in depth for tenant/Identity contexts in the maintenance host, including Blazor circuit calls.</summary>
public sealed class MaintenanceReadOnlyInterceptor : DbCommandInterceptor
{
    private static void RequireRead(DbCommand command)
    {
        var sql = command.CommandText.AsSpan().TrimStart();
        while (sql.StartsWith("--", StringComparison.Ordinal))
        { var newline = sql.IndexOf('\n'); if (newline < 0) throw new InvalidOperationException("Application writes are paused for database maintenance."); sql = sql[(newline + 1)..].TrimStart(); }
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Application writes are paused for database maintenance.");
    }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        => throw new InvalidOperationException("Application writes are paused for database maintenance.");
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Application writes are paused for database maintenance.");
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { RequireRead(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { RequireRead(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    { RequireRead(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { RequireRead(command); return ValueTask.FromResult(result); }
}
