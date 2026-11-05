using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Sales.DTOs;
using TicketingCRM.Domain.Sales.Interfaces;
namespace TicketingCRM.Controllers;

[ApiController, Route("api/sales"), Authorize, AutoValidateAntiforgeryToken, EnableRateLimiting("licensing")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SalesController(ISalesPipelineService service) : ControllerBase
{
    /// <summary>Read the current organization's paginated leads and opportunities. Owner/Admin access required.</summary>
    [HttpGet]
    public async Task<ActionResult<SalesPipelinePage>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await service.GetAsync(page, pageSize, cancellationToken));
    [HttpPost("activities")]
    public async Task<IActionResult> Activity(RecordSalesActivityRequest request,CancellationToken cancellationToken){await service.RecordActivityAsync(request,cancellationToken);return NoContent();}
    [HttpPost("leads")]
    public async Task<ActionResult<CreatedRecordDto>> CreateLead(CreateLeadRequest request, CancellationToken cancellationToken)
        => Created("/sales", new CreatedRecordDto(await service.CreateLeadAsync(request, cancellationToken)));
    [HttpPost("opportunities")]
    public async Task<ActionResult<CreatedRecordDto>> CreateOpportunity(CreateOpportunityRequest request, CancellationToken cancellationToken)
        => Created("/sales", new CreatedRecordDto(await service.CreateOpportunityAsync(request, cancellationToken)));
    /// <summary>Atomically convert a qualified lead into a new customer and opportunity. Safe to retry with the same operation and details.</summary>
    [HttpPost("leads/convert")]
    public async Task<ActionResult<LeadConversionReceipt>> Convert(ConvertLeadRequest request, CancellationToken cancellationToken)
        => Ok(await service.ConvertLeadAsync(request, cancellationToken));
    [HttpPost("leads/stage")]
    public async Task<IActionResult> LeadStage(ChangeLeadStageRequest request, CancellationToken cancellationToken)
    { await service.ChangeLeadStageAsync(request, cancellationToken); return NoContent(); }
    [HttpPost("opportunities/stage")]
    public async Task<IActionResult> OpportunityStage(ChangeOpportunityStageRequest request, CancellationToken cancellationToken)
    { await service.ChangeOpportunityStageAsync(request, cancellationToken); return NoContent(); }
    [HttpGet("leads/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<SalesHistoryDto>>> LeadHistory(Guid id, CancellationToken cancellationToken)
        => Ok(await service.HistoryAsync(id, false, cancellationToken));
    [HttpGet("opportunities/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<SalesHistoryDto>>> OpportunityHistory(Guid id, CancellationToken cancellationToken)
        => Ok(await service.HistoryAsync(id, true, cancellationToken));
}
