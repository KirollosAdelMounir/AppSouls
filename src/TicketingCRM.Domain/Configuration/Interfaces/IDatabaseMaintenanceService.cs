using TicketingCRM.Domain.Configuration.DTOs;

namespace TicketingCRM.Domain.Configuration.Interfaces;

public interface IDatabaseMaintenanceService
{
    Task<DatabaseInspection> InspectAsync(DatabaseMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task ScheduleAsync(DatabaseMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task<DatabaseTransferProgress> GetProgressAsync(CancellationToken cancellationToken = default);
    Task FinishAsync(CancellationToken cancellationToken = default);
}
