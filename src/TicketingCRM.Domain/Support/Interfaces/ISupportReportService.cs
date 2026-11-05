using TicketingCRM.Domain.Support.DTOs;

namespace TicketingCRM.Domain.Support.Interfaces;

/// <summary>
/// Implementations must enforce authenticated tenant/customer ownership and permissions.
/// Submission commits report, history and notification outbox atomically and idempotently.
/// </summary>
public interface ISupportReportService
{
    Task<ReportSubmissionReceipt> SubmitAsync(SubmitReportRequest request, CancellationToken cancellationToken = default);
    Task<ReportProgress?> GetProgressAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task ChangeStageAsync(ChangeReportStageRequest request, CancellationToken cancellationToken = default);
}
