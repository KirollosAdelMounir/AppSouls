using TicketingCRM.Domain.Sales.DTOs;
namespace TicketingCRM.Domain.Sales.Interfaces;
public interface ISalesPipelineService
{
    Task<SalesPipelinePage> GetAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task RecordActivityAsync(RecordSalesActivityRequest request,CancellationToken cancellationToken=default);
    Task<Guid> CreateLeadAsync(CreateLeadRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateOpportunityAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default);
    Task<LeadConversionReceipt> ConvertLeadAsync(ConvertLeadRequest request, CancellationToken cancellationToken = default);
    Task ChangeLeadStageAsync(ChangeLeadStageRequest request, CancellationToken cancellationToken = default);
    Task ChangeOpportunityStageAsync(ChangeOpportunityStageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesHistoryDto>> HistoryAsync(Guid id, bool opportunity, CancellationToken cancellationToken = default);
}
