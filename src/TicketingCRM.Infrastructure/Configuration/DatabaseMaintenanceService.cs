using TicketingCRM.DataConnection.Repository.Queries;

using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using Provider = TicketingCRM.DataConnection.Configuration.DatabaseProvider;

namespace TicketingCRM.Infrastructure.Configuration;

public sealed class DatabaseMaintenanceService(IServerSettingsService settings, FileConfigurationVault vault,
    ServerConfiguration active, ICurrentActor actor, DatabaseMaintenanceCoordinator coordinator) : IDatabaseMaintenanceService
{
    public async Task<DatabaseInspection> InspectAsync(DatabaseMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        await settings.GetAsync(cancellationToken);
        request = ResolveCurrent(request, await vault.LoadAsync(cancellationToken));
        return await DatabaseLifecycle.InspectAsync((Provider)(int)ProviderFor(request), request.ConnectionString, cancellationToken);
    }
    public async Task ScheduleAsync(DatabaseMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        await settings.GetAsync(cancellationToken);
        if (!request.ConfirmMaintenance) throw new ArgumentException("Confirm a backup and stop every other application instance before database maintenance.");
        if (request.Mode is not ("Copy" or "Switch" or "Upgrade")) throw new ArgumentException("Choose Copy, Switch or Upgrade.");
        request = ResolveCurrent(request, await vault.LoadAsync(cancellationToken));
        var provider = ProviderFor(request);
        var state = await DatabaseLifecycle.InspectAsync((Provider)(int)provider, request.ConnectionString, cancellationToken);
        if (request.Mode != "Copy")
        {
            if (!state.HasAccounts) throw new ArgumentException("Switch requires an existing App Souls database. Use Copy for an empty target.");
            // Verify the authenticated installation identity through the target persistence boundary.
            if (!await TicketingCRM.DataConnection.Repository.InstallationRepository.HasMembershipAsync((Provider)(int)provider, request.ConnectionString, actor.UserId, actor.TenantId, actor.SecurityStamp, false, cancellationToken))
                throw new UnauthorizedAccessException("The target does not contain your authenticated installation membership.");
        }
        else if (state.HasAccounts) throw new ArgumentException("Copy requires an empty target. Existing accounts are never overwritten.");
        var saved = await vault.LoadAsync(cancellationToken);
        if (saved.DatabaseProvider != active.DatabaseProvider || saved.DatabaseConnectionString != active.DatabaseConnectionString)
            throw new InvalidOperationException("Restart into the saved database configuration before starting another transfer.");
        coordinator.Schedule(request, saved, actor.UserId, actor.TenantId, actor.SecurityStamp);
    }
    public async Task<DatabaseTransferProgress> GetProgressAsync(CancellationToken cancellationToken = default)
    { await settings.GetAsync(cancellationToken); return coordinator.Progress; }
    public async Task FinishAsync(CancellationToken cancellationToken = default)
    { await settings.GetAsync(cancellationToken); coordinator.Finish(); }
    private DatabaseMaintenanceRequest ResolveCurrent(DatabaseMaintenanceRequest request, ServerConfiguration saved)
    {
        if (request.Mode == "Upgrade") return new() { Mode = "Upgrade", Provider = active.DatabaseProvider.ToString(), ConnectionString = active.DatabaseConnectionString, ConfirmMaintenance = request.ConfirmMaintenance };
        if (!string.IsNullOrWhiteSpace(request.DatabaseServer) || !string.IsNullOrWhiteSpace(request.DatabaseCatalog) || !string.IsNullOrWhiteSpace(request.DatabaseUserName) || !string.IsNullOrEmpty(request.DatabasePassword))
        {
            if (!Enum.TryParse<DatabaseProvider>(request.Provider, out var provider) || !Enum.IsDefined(provider)) throw new ArgumentException("Choose a supported provider.");
            request.ConnectionString = TicketingCRM.Infrastructure.Administration.SetupDatabaseConnection.Build(provider,
                new TicketingCRM.Domain.Administration.DTOs.CompleteServerSetupRequest { DatabaseServer = request.DatabaseServer, DatabasePort = request.DatabasePort,
                    DatabaseCatalog = request.DatabaseCatalog, DatabaseUserName = request.DatabaseUserName, DatabasePassword = request.DatabasePassword, DatabaseUseTls = request.DatabaseUseTls }, saved);
        }
        return request;
    }
    private static DatabaseProvider ProviderFor(DatabaseMaintenanceRequest request)
    {
        if (!Enum.TryParse<DatabaseProvider>(request.Provider, out var value) || !Enum.IsDefined(value) || string.IsNullOrWhiteSpace(request.ConnectionString) || request.ConnectionString.Length > 8192)
            throw new ArgumentException("Choose a supported provider and a valid target connection.");
        return value;
    }
}
