using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Domain.Administration.Interfaces;
public interface IServerSetupService
{
    Task<ServerSetupState> GetStateAsync(CancellationToken cancellationToken = default);
    Task<SetupConnectionResult> TestDatabaseAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken = default);
    Task<SetupConnectionResult> TestEmailAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task PrepareAdministratorAsync(PrepareAdministratorRequest request, CancellationToken cancellationToken = default);
    Task MigrateAndActivateAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken = default);
}
