using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
using TicketingCRM.Security;

namespace TicketingCRM.Controllers;

[ApiController, Route("api/database-maintenance"), Authorize(Policy = "PlatformAdministrator")]
[AutoValidateAntiforgeryToken, EnableRateLimiting("licensing"), RequestSizeLimit(32768)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DatabaseMaintenanceController(IDatabaseMaintenanceService maintenance, ServerActivationSignal activation,
    IHostApplicationLifetime lifetime) : ControllerBase
{
    [HttpPost("inspect")]
    public async Task<ActionResult<DatabaseInspection>> Inspect([FromForm] DatabaseMaintenanceRequest request, CancellationToken cancellationToken)
        => Ok(await maintenance.InspectAsync(request, cancellationToken));
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromForm] DatabaseMaintenanceRequest request, CancellationToken cancellationToken)
    {
        await maintenance.ScheduleAsync(request, cancellationToken);
        RestartAfterResponse();
        return Accepted(new { message = "Entering maintenance mode. Reconnecting to progress…" });
    }
    [HttpGet("progress"), EnableRateLimiting("maintenance-progress")]
    public async Task<ActionResult<DatabaseTransferProgress>> Progress(CancellationToken cancellationToken)
        => Ok(await maintenance.GetProgressAsync(cancellationToken));
    [HttpPost("finish")]
    public async Task<IActionResult> Finish(CancellationToken cancellationToken)
    {
        await maintenance.FinishAsync(cancellationToken);
        RestartAfterResponse();
        return Accepted(new { message = "Restarting application services…" });
    }
    private void RestartAfterResponse() => Response.OnCompleted(() =>
    {
        activation.Request(); lifetime.StopApplication(); return Task.CompletedTask;
    });
}
