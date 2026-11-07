using System.Linq.Expressions;
namespace TicketingCRM.DataConnection.Repository.Interfaces;

/// <summary>Composes filtered queries and stages mutations. Only the shared unit of work commits changes.</summary>
public interface IRepository<TEntity, in TKey> where TEntity : class
{
    IQueryable<TEntity> GetAllQueryable(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includes);
    IQueryable<TEntity> GetReadOnlyQueryable(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includes);
    IQueryable<TEntity> GetQuery(RepositoryQuery<TEntity> query);
    IQueryable<TEntity> GetQueryableWithIncludes(Expression<Func<TEntity, bool>>? predicate, params string[] includePaths);
    Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default);
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes);
    Task<TEntity?> GetByUniqueDetailAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    ValueTask<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void AddRange(IEnumerable<TEntity> entities);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
}

/// <summary>Query composition for filtering, nested Include/ThenInclude, ordering, paging and tracking. Tenant filters remain enabled.</summary>
public sealed class RepositoryQuery<TEntity> where TEntity : class
{
    public Expression<Func<TEntity, bool>>? Predicate { get; init; }
    public Func<IQueryable<TEntity>, IQueryable<TEntity>>? Includes { get; init; }
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; init; }
    public int? Skip { get; init; }
    public int? Take { get; init; }
    public bool Tracking { get; init; } = true;
}
