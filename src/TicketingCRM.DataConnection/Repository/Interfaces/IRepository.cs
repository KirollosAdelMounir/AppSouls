namespace TicketingCRM.DataConnection.Repository.Interfaces;

/// <summary>Mutations stage changes; IUnitOfWork owns the commit. Implementations must enforce tenant scope.</summary>
public interface IRepository<TEntity, in TKey> where TEntity : class
{
    Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    void Remove(TEntity entity);
}
