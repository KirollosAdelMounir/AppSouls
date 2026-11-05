using TicketingCRM.Core.Security;
using System.Data;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;

namespace TicketingCRM.Infrastructure.Administration;

public sealed class InitialAdministratorService(ServerConfiguration? configuration, InitialSetupAuthorization authorization) : IInitialAdministratorService
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (configuration is null) return false;
        await using var db = Open(Guid.Empty);
        return !await db.Users.AnyAsync(cancellationToken);
    }

    public async Task CreateAsync(InitialAdministratorRequest request, CancellationToken cancellationToken = default)
    {
        if (!await authorization.ValidateAsync(request.SetupToken, cancellationToken)) throw new UnauthorizedAccessException("Setup authorization is invalid or expired.");
        PasswordPolicy.Validate(request.Password);
        var name = request.DisplayName.Trim(); var organization = request.OrganizationName.Trim(); var email = request.Email.Trim();
        if (name.Length is < 1 or > 200 || organization.Length is < 1 or > 200 || email.Length > 254 ||
            !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email) throw new ArgumentException("Enter your name, organization and a valid email address.");
        var tenant = Guid.NewGuid();
        await using var db = Open(tenant);
        // Both providers serialize first-owner creation across processes. No request tenant establishes authority.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(470337881)", cancellationToken);
        else
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'AppSouls.InitialAdministrator', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 50000, 'Setup is busy.', 1;", cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken)) throw new InvalidOperationException("Initial administration is already configured. Sign in instead.");
        var normalized = email.ToUpperInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            Email = email,
            UserName = email,
            NormalizedEmail = normalized,
            NormalizedUserName = normalized,
            EmailConfirmed = true,
            IsPlatformAdministrator = true,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, request.Password);
        db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = organization });
        db.Users.Add(user);
        db.TenantMemberships.Add(new TenantMembership { TenantId = tenant, UserId = user.Id, Role = "Owner" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // The committed identity is the authoritative replay guard even if local cleanup fails.
        try { authorization.Complete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private AppDbContext Open(Guid tenant)
    {
        if (configuration is null) throw new InvalidOperationException("Initialize the server vault first.");
        var options = new DbContextOptionsBuilder<AppDbContext>();
        TicketingCRM.DataConnection.DependencyInjection.ConfigureProvider(options,
            (TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider, configuration.DatabaseConnectionString);
        return configuration.DatabaseProvider == DatabaseProvider.PostgreSql
            ? new PostgreSqlAppDbContext(options.Options, new Scope(tenant)) : new SqlServerAppDbContext(options.Options, new Scope(tenant));
    }
    private sealed record Scope(Guid TenantId) : ITenantContext;
}
