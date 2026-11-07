using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
namespace TicketingCRM.DataConnection.Repository;

/// <summary>Installation-only persistence. Callers supply protected configuration, never a request-established tenant scope.</summary>
public static class InstallationRepository
{
    private sealed record Scope(Guid TenantId) : ITenantContext;
    public static IWorkspaceSession OpenSession(DatabaseProvider provider, string connection, Guid verifiedTenant)
        => new TicketingCRM.DataConnection.Transactions.UnitOfWork(new TenantDbContextFactory(provider, connection, new Scope(verifiedTenant)).CreateDbContext(), ownsContext: true);
    public static async Task<bool> HasAccountsAsync(DatabaseProvider provider, string connection, CancellationToken ct)
    {
        await using var db = DatabaseLifecycle.Open(provider, connection);
        return await db.Users.AnyAsync(ct);
    }
    public static async Task<bool> HasUnexpectedAccountsAsync(DatabaseProvider provider, string connection, Guid preparedUser, string passwordHash, CancellationToken ct)
    {
        await using var db = DatabaseLifecycle.Open(provider, connection);
        return await db.Users.AnyAsync(x => x.Id != preparedUser || x.PasswordHash != passwordHash, ct);
    }
    public static async Task<bool> HasMembershipAsync(DatabaseProvider provider, string connection, Guid userId, Guid tenantId, string? stamp, bool platformAdministrator, CancellationToken ct)
    {
        await using var db = DatabaseLifecycle.Open(provider, connection);
        return await db.Users.AnyAsync(x => x.Id == userId && x.SecurityStamp == stamp && (!platformAdministrator || x.IsPlatformAdministrator), ct)
            && await db.TenantMemberships.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId && x.TenantId == tenantId && x.IsActive, ct);
    }
    public static async IAsyncEnumerable<Guid> ActiveTenantsAsync(DatabaseProvider provider, string connection, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = DatabaseLifecycle.Open(provider, connection);
        await foreach (var id in db.Tenants.IgnoreQueryFilters().AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => x.Id).AsAsyncEnumerable().WithCancellation(ct)) yield return id;
    }
    public sealed record ProtectedIdentityToken(Guid UserId, string LoginProvider, string Name, string Value);
    public static async IAsyncEnumerable<ProtectedIdentityToken> ProtectedTokensAsync(DatabaseProvider provider, string connection, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = DatabaseLifecycle.Open(provider, connection);
        await foreach (var token in db.UserTokens.AsNoTracking().Where(x => x.Value != null).Select(x => new ProtectedIdentityToken(x.UserId, x.LoginProvider, x.Name, x.Value!)).AsAsyncEnumerable().WithCancellation(ct)) yield return token;
    }
    public static async IAsyncEnumerable<string> PendingInvitationPayloadsAsync(DatabaseProvider provider, string connection, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = DatabaseLifecycle.Open(provider, connection);
        await foreach (var payload in db.OutboxMessages.IgnoreQueryFilters().AsNoTracking().Where(x => x.Kind == "CustomerInvitation" && x.DeliveredAtUtc == null).Select(x => x.PayloadJson).AsAsyncEnumerable().WithCancellation(ct)) yield return payload;
    }
}
