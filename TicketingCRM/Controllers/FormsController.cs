using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Forms.Interfaces;
namespace TicketingCRM.Controllers;

/// <summary>Manage public collection forms and read private organization submissions. Owner/Admin only.</summary>
[ApiController, Route("api/forms"), Authorize(Policy = "FormsManager"), AutoValidateAntiforgeryToken, EnableRateLimiting("licensing")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FormsController(IFormService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FormPage>> List(int page = 1, CancellationToken cancellationToken = default) => Ok(await service.ListAsync(page, cancellationToken));
    [HttpGet("{id:guid}")]
    public Task<FormDefinition> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateFormRequest request, CancellationToken ct) { await service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) { var file = await service.ExportAsync(id, ct); return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.Filename); }
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateFormRequest request, CancellationToken cancellationToken) => Ok(await service.CreateAsync(request, cancellationToken));
    [HttpPost("{id:guid}/state")]
    public async Task<IActionResult> State(Guid id, ChangeFormStateRequest request, CancellationToken cancellationToken)
    { await service.ChangeStateAsync(id, request, cancellationToken); return NoContent(); }
    [HttpGet("{id:guid}/submissions")]
    public async Task<ActionResult<FormSubmissionsPage>> Submissions(Guid id, int page = 1, CancellationToken cancellationToken = default) => Ok(await service.SubmissionsAsync(id, page, cancellationToken));
}
