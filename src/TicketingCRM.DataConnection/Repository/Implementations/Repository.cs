using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Persistence.Models;

namespace TicketingCRM.DataConnection.Repository.Implementations;

public sealed class Repository<TEntity>(AppDbContext context) : IRepository<TEntity, Guid> where TEntity : TenantRecord
{
    public Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Set<TEntity>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(TEntity entity) => context.Set<TEntity>().Add(entity);
    public void Remove(TEntity entity) => context.Set<TEntity>().Remove(entity);
}
