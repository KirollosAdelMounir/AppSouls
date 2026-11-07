using TicketingCRM.DataConnection.Transactions;
namespace TicketingCRM.DataConnection.Repository.Interfaces;

/// <summary>An operation-owned unit of work. All data access is through its typed repositories.</summary>
public interface IWorkspaceSession : IUnitOfWork, IAsyncDisposable
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
public interface IWorkspaceSessionFactory
{
    Task<IWorkspaceSession> OpenAsync(CancellationToken cancellationToken = default);
}
