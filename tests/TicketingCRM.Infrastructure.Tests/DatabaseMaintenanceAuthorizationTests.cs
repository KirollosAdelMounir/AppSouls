using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Infrastructure.Configuration;
using Xunit;

namespace TicketingCRM.Infrastructure.Tests;

public sealed class DatabaseMaintenanceAuthorizationTests
{
    [Fact]
    public async Task UnauthorizedCallerCannotInspectScheduleReadProgressOrResumeServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "appsouls-denied-" + Guid.NewGuid().ToString("N"));
        var vault = new FileConfigurationVault(new(Path.Combine(root, "vault", "server"), Path.Combine(root, "key", "master")));
        var coordinator = new DatabaseMaintenanceCoordinator();
        var service = new DatabaseMaintenanceService(new DeniedSettings(), vault, new ServerConfiguration(), new Actor(), coordinator);
        var request = new DatabaseMaintenanceRequest { Provider = "untrusted", ConnectionString = "invalid", ConfirmMaintenance = true };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.InspectAsync(request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ScheduleAsync(request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetProgressAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.FinishAsync());
        Assert.False(coordinator.MaintenanceMode); Assert.Null(coordinator.Request); Assert.False(Directory.Exists(root));
    }
    private sealed record Actor : ICurrentActor
    {
        public Guid UserId => Guid.Empty; public Guid TenantId => Guid.Empty; public Guid? CustomerId => null; public string Role => ""; public string? SecurityStamp => null;
    }
    private sealed class DeniedSettings : IServerSettingsService
    {
        public Task<TicketingCRM.Domain.Delivery.DTOs.ArtifactScannerAvailability> CheckScannerAsync(CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task<ServerSettingsDto> GetAsync(CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task<TicketingCRM.Domain.Administration.DTOs.SetupConnectionResult> TestEmailAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task UpdatePublicAddressAsync(UpdatePublicAddressRequest request, CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task UpdateOrganizationAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task UpdateDatabaseAsync(UpdateDatabaseSettingsRequest request, CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task UpdateEmailAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
        public Task UpdateIntegrationAsync(UpdateIntegrationSettingsRequest request, CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
    }
}
