using System.Data;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.DataConnection.Repository;

public static class InitialAdministratorRepository
{
    public static async Task CreateAsync(DatabaseProvider provider, string connection, Guid tenant, string organization, ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var db = PreparedAdministratorRepository.Open(provider, connection, tenant);
        // Both providers serialize first-owner creation across processes. No request tenant establishes authority.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(470337881)", cancellationToken);
        else
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'AppSouls.InitialAdministrator', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 50000, 'Setup is busy.', 1;", cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken)) throw new InvalidOperationException("Initial administration is already configured. Sign in instead.");
        db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = organization });
        db.Users.Add(user);
        db.TenantMemberships.Add(new TenantMembership { TenantId = tenant, UserId = user.Id, Role = "Owner" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
