using TicketingCRM.DataConnection.Repository.Queries;

using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection;
using TicketingCRM.Domain.Configuration.DTOs;
using Provider = TicketingCRM.DataConnection.Configuration.DatabaseProvider;

namespace TicketingCRM.Infrastructure.Configuration;

/// <summary>Host-owned handoff. The normal host is disposed before the maintenance worker starts.</summary>
public sealed class DatabaseMaintenanceCoordinator
{
    private readonly object gate = new();
    private DatabaseTransferProgress progress = new("Idle", null);
    private bool running, resumeRequested;
    private readonly List<DatabaseStageEvent> stages = [];
    private readonly System.Diagnostics.Stopwatch elapsed = new();
    private volatile bool maintenanceMode;
    public bool MaintenanceMode => maintenanceMode;
    public DatabaseMaintenanceRequest? Request { get; private set; }
    public ServerConfiguration? Source { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid TenantId { get; private set; }
    public string? SecurityStamp { get; private set; }
    public DatabaseTransferProgress Progress
    {
        get
        {
            lock (gate) return progress with
            {
                ElapsedSeconds = progress.Finished ? progress.ElapsedSeconds : elapsed.Elapsed.TotalSeconds,
                Source = Source is null ? null : Label(Source.DatabaseProvider.ToString(), Source.DatabaseConnectionString),
                Target = Request is null ? null : Label(Request.Provider, Request.ConnectionString),
                Mode = Request?.Mode
            };
        }
    }
    private static string Label(string provider, string connection)
    {
        try { return provider + " / " + DatabaseLifecycle.Catalog(Enum.Parse<Provider>(provider), connection); }
        catch (ArgumentException) { return provider + " / selected catalog"; }
    }
    public void Schedule(DatabaseMaintenanceRequest request, ServerConfiguration source, Guid actor, Guid tenant, string? stamp)
    {
        lock (gate)
        {
            if (MaintenanceMode) throw new InvalidOperationException("Database maintenance is already active.");
            Request = new() { Mode = request.Mode, Provider = request.Provider, ConnectionString = request.ConnectionString, ConfirmMaintenance = true };
            Source = source; ActorId = actor; TenantId = tenant; SecurityStamp = stamp;
            elapsed.Restart(); stages.Clear();
            maintenanceMode = true; progress = new("Entering maintenance mode", null, Message: "Application writes and email processing are stopping.");
        }
    }
    public bool TryStart() { lock (gate) { if (!MaintenanceMode || running || progress.Finished) return false; running = true; return true; } }
    public void Report(DatabaseTransferProgress value)
    {
        lock (gate)
        {
            if (stages.LastOrDefault() is not { } last || last.Stage != value.Stage || last.Table != value.Table)
            {
                stages.Add(new(DateTimeOffset.UtcNow, value.Stage, value.Table));
                if (stages.Count > 200) stages.RemoveAt(0);
            }
            if (value.TotalRows == 0 && progress.TotalRows > 0) value = value with { RowsCopied = progress.RowsCopied, TotalRows = progress.TotalRows, RowsPerSecond = progress.RowsPerSecond };
            if (value.RowsPerSecond == 0 && progress.RowsPerSecond > 0) value = value with { RowsPerSecond = progress.RowsPerSecond };
            if (value.TotalBytes == 0 && progress.TotalBytes > 0) value = value with { BytesCopied = progress.BytesCopied, TotalBytes = progress.TotalBytes, BytesPerSecond = progress.BytesPerSecond };
            progress = value with { ElapsedSeconds = elapsed.Elapsed.TotalSeconds, Stages = stages.ToArray() };
        }
    }
    public void Finish()
    {
        lock (gate)
        {
            if (!progress.Finished) throw new InvalidOperationException("Wait for the database operation to finish.");
            resumeRequested = true;
        }
    }
    public void ResetAfterHostStopped()
    {
        lock (gate) { if (!resumeRequested) return; maintenanceMode = false; running = false; resumeRequested = false; Request = null; Source = null; progress = new("Idle", null); }
    }
    public static ServerConfiguration WithDatabase(ServerConfiguration old, DatabaseProvider provider, string connection) => new()
    {
        DatabaseProvider = provider,
        DatabaseConnectionString = connection,
        PublicBaseUrl = old.PublicBaseUrl,
        LicenseSigningPrivateKeyPem = old.LicenseSigningPrivateKeyPem,
        DataProtectionKeyDirectory = old.DataProtectionKeyDirectory,
        Smtp = old.Smtp,
        Integrations = old.Integrations
    };
}

public sealed class DatabaseMaintenanceWorker(DatabaseMaintenanceCoordinator coordinator, FileConfigurationVault vault, IDataProtectionProvider protection,
    ILogger<DatabaseMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!coordinator.TryStart()) return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var request = coordinator.Request!; var source = coordinator.Source!;
        try
        {
            var provider = Enum.Parse<DatabaseProvider>(request.Provider);
            // Revalidate the initiating platform administrator immediately before the offline operation.
            if (!await TicketingCRM.DataConnection.Repository.InstallationRepository.HasMembershipAsync((Provider)(int)source.DatabaseProvider, source.DatabaseConnectionString, coordinator.ActorId, coordinator.TenantId, coordinator.SecurityStamp, true, stoppingToken))
                throw new UnauthorizedAccessException();
            if (request.Mode == "Copy")
                await DatabaseCopy.CopyAsync((Provider)(int)source.DatabaseProvider, source.DatabaseConnectionString,
                    (Provider)(int)provider, request.ConnectionString, coordinator.Report, stoppingToken);
            else
            {
                coordinator.Report(new("Checking target catalog", null));
                // A switch can reuse accounts, but cannot use a new administrator to adopt another database.
                if (!await TicketingCRM.DataConnection.Repository.InstallationRepository.HasMembershipAsync((Provider)(int)provider, request.ConnectionString, coordinator.ActorId, coordinator.TenantId, coordinator.SecurityStamp, false, stoppingToken))
                    throw new UnauthorizedAccessException();
                await DatabaseLifecycle.ApplyAsync((Provider)(int)provider, request.ConnectionString, stoppingToken);
            }
            coordinator.Report(new("Validating target administrator", 99));
            if (!await TicketingCRM.DataConnection.Repository.InstallationRepository.HasMembershipAsync((Provider)(int)provider, request.ConnectionString, coordinator.ActorId, coordinator.TenantId, coordinator.SecurityStamp, true, stoppingToken))
                throw new InvalidOperationException("The target does not contain this installation's authorized administrator. The original connection was retained.");
            // Installation keys remain unchanged; verify stored protected values before switching.
            await foreach (var token in TicketingCRM.DataConnection.Repository.InstallationRepository.ProtectedTokensAsync((Provider)(int)provider, request.ConnectionString, stoppingToken))
            {
                if (!token.Value.StartsWith("protected:v1:", StringComparison.Ordinal)) throw new InvalidOperationException("Target token protection is incompatible.");
                protection.CreateProtector("AppSouls.IdentityTokens.v1", token.UserId.ToString("N"), token.LoginProvider, token.Name).Unprotect(token.Value[13..]);
            }
            await foreach (var payloadJson in TicketingCRM.DataConnection.Repository.InstallationRepository.PendingInvitationPayloadsAsync((Provider)(int)provider, request.ConnectionString, stoppingToken))
            {
                using var payload = JsonDocument.Parse(payloadJson);
                protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);
            }
            await vault.SaveAsync(DatabaseMaintenanceCoordinator.WithDatabase(source, provider, request.ConnectionString), stoppingToken);
            var prior = coordinator.Progress;
            coordinator.Report(prior with
            {
                Stage = "Complete",
                Percentage = 100,
                ElapsedSeconds = clock.Elapsed.TotalSeconds,
                Message = "Target verified and configuration saved. Restart into the selected database to resume application work.",
                Finished = true,
                Success = true
            });
        }
        catch (Exception error)
        {
            logger.LogWarning("Database maintenance failed: {FailureType}; original connection remains active unless configuration commit completed.", error.GetType().Name);
            coordinator.Report(coordinator.Progress with
            {
                Stage = "Failed",
                Percentage = null,
                ElapsedSeconds = clock.Elapsed.TotalSeconds,
                Message = "The operation failed. The original connection is retained. Copy errors before commit roll back copied records. A committed copy, created catalog or completed schema migrations can remain after a later verification/configuration failure. Check operator diagnostics before retrying.",
                Finished = true,
                Success = false
            });
        }
    }
}
