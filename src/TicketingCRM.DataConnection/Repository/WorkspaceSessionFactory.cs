using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository.Interfaces;
namespace TicketingCRM.DataConnection.Repository;
public sealed class WorkspaceSessionFactory(IDbContextFactory<AppDbContext> factory) : IWorkspaceSessionFactory
{
    public async Task<IWorkspaceSession> OpenAsync(CancellationToken cancellationToken = default)
        => new Session(await factory.CreateDbContextAsync(cancellationToken));
    private sealed class Session(AppDbContext context) : IWorkspaceSession
    {
        public IQueryable<T> Query<T>() where T : class => context.Set<T>();
        public void Add<T>(T entity) where T : class => context.Add(entity);
        public Task<int> CommitAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
        public ValueTask DisposeAsync() => context.DisposeAsync();
    }
}
