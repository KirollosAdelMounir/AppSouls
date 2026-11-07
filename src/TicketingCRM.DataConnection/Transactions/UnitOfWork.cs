using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;

namespace TicketingCRM.DataConnection.Transactions;

/// <summary>Own one context per operation and discard it after any ambiguous commit or failed operation.</summary>
public sealed class UnitOfWork(AppDbContext context, bool ownsContext = false) : TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSession
{
    private readonly Dictionary<Type, object> repositories = [];
    private bool disposed;
    public TicketingCRM.DataConnection.Repository.Interfaces.IRepository<TEntity, Guid> Repository<TEntity>() where TEntity : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!repositories.TryGetValue(typeof(TEntity), out var repository))
            repositories[typeof(TEntity)] = repository = new TicketingCRM.DataConnection.Repository.Implementations.Repository<TEntity, Guid>(context);
        return (TicketingCRM.DataConnection.Repository.Interfaces.IRepository<TEntity, Guid>)repository;
    }
    public void ClearTracking() { ObjectDisposedException.ThrowIf(disposed, this); context.ChangeTracker.Clear(); }
    public Task<int> CommitAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(cancellationToken);
    public async ValueTask DisposeAsync() { if (disposed) return; disposed = true; repositories.Clear(); if (ownsContext) await context.DisposeAsync(); }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { ObjectDisposedException.ThrowIf(disposed, this); return context.SaveChangesAsync(cancellationToken); }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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
