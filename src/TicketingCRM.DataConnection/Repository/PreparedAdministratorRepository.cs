using TicketingCRM.DataConnection;
using System.Data;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
namespace TicketingCRM.DataConnection.Repository;

public static class PreparedAdministratorRepository
{
    public static AppDbContext Open(TicketingCRM.DataConnection.Configuration.DatabaseProvider provider, string connection, Guid tenant = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        TicketingCRM.DataConnection.DependencyInjection.ConfigureProvider(options, provider, connection, administrative: true);
        return provider == TicketingCRM.DataConnection.Configuration.DatabaseProvider.PostgreSql ? new PostgreSqlAppDbContext(options.Options, new Scope(tenant)) : new SqlServerAppDbContext(options.Options, new Scope(tenant));
    }
    public static async Task MigrateAndSeedAsync(TicketingCRM.DataConnection.Configuration.DatabaseProvider provider, string connection, PreparedAdministrator prepared, CancellationToken cancellationToken = default)
    {
        if (prepared.UserId == Guid.Empty || prepared.TenantId == Guid.Empty || string.IsNullOrEmpty(prepared.PasswordHash) || string.IsNullOrEmpty(prepared.SecurityStamp) || prepared.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Prepared administrator is invalid or expired.");
        if (prepared.DisplayName.Length is < 1 or > 200 || prepared.OrganizationName.Length is < 1 or > 200 || prepared.Email.Length > 254 ||
            !MailAddress.TryCreate(prepared.Email, out var address) || address.Address != prepared.Email || prepared.PasswordHash.Length > 1024 || prepared.SecurityStamp.Length > 128)
            throw new InvalidOperationException("Prepared administrator identity is invalid.");
        await using var db = Open(provider, connection, prepared.TenantId);
        var inspection = await DatabaseLifecycle.InspectAsync(provider, connection, cancellationToken);
        if (inspection.HasAccounts && await db.Users.AnyAsync(x => x.Id != prepared.UserId || x.PasswordHash != prepared.PasswordHash, cancellationToken))
            throw new InvalidOperationException("This database already has accounts. Initial setup cannot replace or adopt them.");
        // Catalog creation and schema updates are explicit. A current schema causes no migration writes.
        await DatabaseLifecycle.ApplyAsync(provider, connection, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(470337881)", cancellationToken);
        else await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'AppSouls.InitialAdministrator', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 50000, 'Setup is busy.', 1;", cancellationToken);
        var existing = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        if (existing.Count != 0)
        {
            // Resume only the exact prepared identity after a crash between database commit and vault activation.
            if (existing.Count != 1 || existing[0].Id != prepared.UserId || existing[0].PasswordHash != prepared.PasswordHash ||
                !existing[0].IsPlatformAdministrator || !await db.TenantMemberships.AnyAsync(x => x.UserId == prepared.UserId && x.TenantId == prepared.TenantId && x.Role == "Owner" && x.IsActive, cancellationToken))
                throw new InvalidOperationException("This database already has accounts. Initial setup cannot replace or adopt them.");
            await transaction.CommitAsync(cancellationToken); return;
        }
        var email = prepared.Email.Trim(); var normalized = email.ToUpperInvariant();
        db.Users.Add(new ApplicationUser
        {
            Id = prepared.UserId,
            UserName = email,
            NormalizedUserName = normalized,
            Email = email,
            NormalizedEmail = normalized,
            EmailConfirmed = true,
            IsPlatformAdministrator = true,
            DisplayName = prepared.DisplayName,
            PasswordHash = prepared.PasswordHash,
            SecurityStamp = prepared.SecurityStamp,
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true
        });
        db.Tenants.Add(new Tenant { Id = prepared.TenantId, TenantId = prepared.TenantId, Name = prepared.OrganizationName });
        db.TenantMemberships.Add(new TenantMembership { TenantId = prepared.TenantId, UserId = prepared.UserId, Role = "Owner" });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private sealed record Scope(Guid TenantId) : ITenantContext;
}
