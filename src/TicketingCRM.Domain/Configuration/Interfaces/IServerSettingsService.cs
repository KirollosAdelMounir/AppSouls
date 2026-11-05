using TicketingCRM.Domain.Configuration.DTOs;
namespace TicketingCRM.Domain.Configuration.Interfaces;
public interface IServerSettingsService
{
 Task<ServerSettingsDto> GetAsync(CancellationToken cancellationToken=default);
 Task UpdateDatabaseAsync(UpdateDatabaseSettingsRequest request,CancellationToken cancellationToken=default);
 Task UpdateEmailAsync(UpdateEmailSettingsRequest request,CancellationToken cancellationToken=default);
 Task UpdateIntegrationAsync(UpdateIntegrationSettingsRequest request,CancellationToken cancellationToken=default);
}
