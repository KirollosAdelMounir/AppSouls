using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository.Interfaces;
namespace TicketingCRM.DataConnection.Repository.Implementations;

public sealed class Repository<TEntity, TKey>(AppDbContext context) : IRepository<TEntity, TKey> where TEntity : class
{
    public IQueryable<TEntity> GetAllQueryable(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includes)
    {
        IQueryable<TEntity> query = context.Set<TEntity>();
        if (predicate is not null) query = query.Where(predicate);
        foreach (var include in includes) query = query.Include(include);
        return query;
    }
    public IQueryable<TEntity> GetReadOnlyQueryable(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includes)
        => GetAllQueryable(predicate, includes).AsNoTracking();
    public IQueryable<TEntity> GetQueryableWithIncludes(Expression<Func<TEntity, bool>>? predicate, params string[] includePaths)
    {
        var query = GetAllQueryable(predicate);
        foreach (var path in includePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An include path cannot be empty.", nameof(includePaths));
            query = query.Include(path);
        }
        return query;
    }
    public IQueryable<TEntity> GetQuery(RepositoryQuery<TEntity> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Skip is < 0 || query.Take is <= 0) throw new ArgumentOutOfRangeException(nameof(query));
        if (query.Skip is not null && query.OrderBy is null) throw new ArgumentException("Paged queries require a stable ordering.", nameof(query));
        var result = query.Tracking ? GetAllQueryable(query.Predicate) : GetReadOnlyQueryable(query.Predicate);
        if (query.Includes is not null) result = query.Includes(result);
        if (query.OrderBy is not null) result = query.OrderBy(result);
        if (query.Skip is { } skip) result = result.Skip(skip);
        if (query.Take is { } take) result = result.Take(take);
        return result;
    }
    public Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
    public Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes)
    {
        var key = context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey();
        if (key is null || key.Properties.Count != 1 || key.Properties[0].ClrType != typeof(TKey))
            throw new NotSupportedException("This repository requires a matching single-column primary key.");
        var keyName = key.Properties[0].Name;
        // DbSet.Find can return an already tracked entity without applying tenant filters. Always query.
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(TKey)], entity, Expression.Constant(keyName));
        var predicate = Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(property, Expression.Constant(id, typeof(TKey))), entity);
        return GetAllQueryable(predicate, includes).SingleOrDefaultAsync(cancellationToken);
    }
    public Task<TEntity?> GetByUniqueDetailAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes)
        => GetAllQueryable(predicate, includes).SingleOrDefaultAsync(cancellationToken);
    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => GetAllQueryable(predicate).AnyAsync(cancellationToken);
    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => GetAllQueryable(predicate).CountAsync(cancellationToken);
    public void Add(TEntity entity) => context.Set<TEntity>().Add(entity);
    public async ValueTask<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => (await context.Set<TEntity>().AddAsync(entity, cancellationToken)).Entity;
    public void AddRange(IEnumerable<TEntity> entities) => context.Set<TEntity>().AddRange(entities);
    public void Update(TEntity entity)
    {
        if (context.Entry(entity).State == EntityState.Detached) context.Set<TEntity>().Update(entity);
        // Tracked entities retain original concurrency values and change only the modified properties.
    }
    public void Remove(TEntity entity) => context.Set<TEntity>().Remove(entity);
    public void RemoveRange(IEnumerable<TEntity> entities) => context.Set<TEntity>().RemoveRange(entities);
}
