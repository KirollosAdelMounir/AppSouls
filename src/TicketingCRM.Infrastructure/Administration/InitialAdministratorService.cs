using TicketingCRM.Core.Security;
using System.Data;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using TicketingCRM.DataConnection.Repository.Queries;

using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
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
        return !await TicketingCRM.DataConnection.Repository.InstallationRepository.HasAccountsAsync((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider, configuration.DatabaseConnectionString, cancellationToken);
    }

    public async Task CreateAsync(InitialAdministratorRequest request, CancellationToken cancellationToken = default)
    {
        if (!await authorization.ValidateAsync(request.SetupToken, cancellationToken)) throw new UnauthorizedAccessException("Setup authorization is invalid or expired.");
        PasswordPolicy.Validate(request.Password);
        var name = request.DisplayName.Trim(); var organization = request.OrganizationName.Trim(); var email = request.Email.Trim();
        if (name.Length is < 1 or > 200 || organization.Length is < 1 or > 200 || email.Length > 254 ||
            !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email) throw new ArgumentException("Enter your name, organization and a valid email address.");
        var tenant = Guid.NewGuid();
        if (configuration is null) throw new InvalidOperationException("Initialize the server vault first.");
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
        await TicketingCRM.DataConnection.Repository.InitialAdministratorRepository.CreateAsync(
            (TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider,
            configuration.DatabaseConnectionString, tenant, organization, user, cancellationToken);
        // The committed identity is the authoritative replay guard even if local cleanup fails.
        try { authorization.Complete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

}
