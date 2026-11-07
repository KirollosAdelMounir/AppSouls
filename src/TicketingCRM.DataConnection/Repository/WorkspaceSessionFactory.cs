using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.DataConnection.Transactions;
namespace TicketingCRM.DataConnection.Repository;
public sealed class WorkspaceSessionFactory(IDbContextFactory<AppDbContext> factory) : IWorkspaceSessionFactory
{
    public async Task<IWorkspaceSession> OpenAsync(CancellationToken cancellationToken = default)
        => new UnitOfWork(await factory.CreateDbContextAsync(cancellationToken), ownsContext: true);
}
