using TicketingCRM.Domain.Configuration.DTOs;
namespace TicketingCRM.Domain.Configuration.Interfaces;
public interface IServerSettingsService
{
 Task<TicketingCRM.Domain.Delivery.DTOs.ArtifactScannerAvailability> CheckScannerAsync(CancellationToken cancellationToken=default);
 Task<ServerSettingsDto> GetAsync(CancellationToken cancellationToken=default);
 Task<TicketingCRM.Domain.Administration.DTOs.SetupConnectionResult> TestEmailAsync(UpdateEmailSettingsRequest request,CancellationToken cancellationToken=default);
 Task UpdatePublicAddressAsync(UpdatePublicAddressRequest request,CancellationToken cancellationToken=default);
 Task UpdateOrganizationAsync(UpdateOrganizationRequest request,CancellationToken cancellationToken=default);
 Task UpdateDatabaseAsync(UpdateDatabaseSettingsRequest request,CancellationToken cancellationToken=default);
 Task UpdateEmailAsync(UpdateEmailSettingsRequest request,CancellationToken cancellationToken=default);
 Task UpdateIntegrationAsync(UpdateIntegrationSettingsRequest request,CancellationToken cancellationToken=default);
}
