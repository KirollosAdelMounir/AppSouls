namespace TicketingCRM.DataConnection.Transactions;

/// <summary>One operation shares one context. External effects must use a durable outbox.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
