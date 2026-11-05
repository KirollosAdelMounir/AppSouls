using TicketingCRM.Domain.Support.DTOs;
namespace TicketingCRM.Domain.Support.Interfaces;
public interface ISupportInboxService
{
    Task<IReadOnlyList<ReportProgress>> ListAsync(CancellationToken cancellationToken = default);
}
