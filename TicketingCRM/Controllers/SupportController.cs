using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Interfaces;
namespace TicketingCRM.Controllers;

[ApiController]
[Route("api/support")]
[Authorize]
[EnableRateLimiting("licensing")]
[AutoValidateAntiforgeryToken]
public sealed class SupportController(ISupportReportService reports,ISupportInboxService inbox) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportProgress>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportProgress>>> List(CancellationToken cancellationToken)=>Ok(await inbox.ListAsync(cancellationToken));
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReportProgress>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportProgress>> Get(Guid id,CancellationToken cancellationToken){var report=await reports.GetProgressAsync(id,cancellationToken);return report is null?NotFound():Ok(report);}
    [HttpPost]
    [ProducesResponseType<ReportSubmissionReceipt>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReportSubmissionReceipt>> Submit(SubmitReportRequest request,CancellationToken cancellationToken){var receipt=await reports.SubmitAsync(request,cancellationToken);return Created("/support/"+receipt.TicketId,receipt);}
    [HttpPost("stage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeStage(ChangeReportStageRequest request,CancellationToken cancellationToken){await reports.ChangeStageAsync(request,cancellationToken);return NoContent();}
}
