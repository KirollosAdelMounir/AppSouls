namespace TicketingCRM.DataConnection.Repository.Interfaces;

// Only business services use this repository query surface; never pass it to presentation code.
public interface IWorkspaceSession : IAsyncDisposable
{
    IQueryable<T> Query<T>() where T : class;
    void Add<T>(T entity) where T : class;
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
public interface IWorkspaceSessionFactory
{
    Task<IWorkspaceSession> OpenAsync(CancellationToken cancellationToken = default);
}
