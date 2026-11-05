using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;

namespace TicketingCRM.DataConnection.Transactions;

/// <summary>Own one context per operation and discard it after any ambiguous commit or failed operation.</summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        // Retry externally with a fresh context and an operation id; replaying tracked mutations is unsafe.
        if (context.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Retrying transaction workflows must use a fresh context and idempotent operation coordinator.");
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await operation(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
