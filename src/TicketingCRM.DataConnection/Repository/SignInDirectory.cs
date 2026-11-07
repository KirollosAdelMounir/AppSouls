using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.Domain.Identity.Interfaces;
namespace TicketingCRM.DataConnection.Repository;
// Identity-directory exception: scope is a password-verified user, never a client-supplied tenant.
public sealed class SignInDirectory(IDbContextFactory<AppDbContext> factory) : ISignInDirectory
{
    public async Task<SignInAccount?> FindAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrEmpty(securityStamp)) return null;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.SecurityStamp == securityStamp && x.EmailConfirmed, cancellationToken);
        if (user is null || user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow) return null;
        var choices = await (from member in db.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
                             join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on member.TenantId equals tenant.Id
                             where member.UserId == userId && member.IsActive && tenant.IsActive
                             orderby tenant.Name, member.Id
                             select new SignInWorkspace(member.Id, member.TenantId, tenant.Name, member.Role, member.CustomerId)).Take(100).ToListAsync(cancellationToken);
        return choices.Count == 0 ? null : new(user.Id, securityStamp, user.TwoFactorEnabled, choices);
    }
}
