using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
namespace TicketingCRM.DataConnection.Repository.Queries;

/// <summary>Executes composed repository queries in the data layer while the operation's unit of work remains alive.</summary>
public static class RepositoryQueryExtensions
{
    public static IAsyncEnumerable<T> Stream<T>(this IQueryable<T> query) => EntityFrameworkQueryableExtensions.AsAsyncEnumerable(query);
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> query, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.ToListAsync(query, ct);
    public static Task<T> SingleAsync<T>(this IQueryable<T> query, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.SingleAsync(query, ct);
    public static Task<T> SingleAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.SingleAsync(query, predicate, ct);
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(query, ct);
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(query, predicate, ct);
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, ct);
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, predicate, ct);
    public static Task<bool> AnyAsync<T>(this IQueryable<T> query, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.AnyAsync(query, ct);
    public static Task<bool> AnyAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.AnyAsync(query, predicate, ct);
    public static Task<int> CountAsync<T>(this IQueryable<T> query, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.CountAsync(query, ct);
    public static Task<int> CountAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.CountAsync(query, predicate, ct);
    public static Task<TResult> MaxAsync<T, TResult>(this IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.MaxAsync(query, selector, ct);
    public static Task<long?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, long?>> selector, CancellationToken ct = default) => EntityFrameworkQueryableExtensions.SumAsync(query, selector, ct);
}
