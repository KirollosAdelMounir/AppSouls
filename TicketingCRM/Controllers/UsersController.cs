using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
namespace TicketingCRM.Controllers;
[ApiController, Route("api/users"), Authorize, AutoValidateAntiforgeryToken, EnableRateLimiting("licensing")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(IUserAdministrationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedUserDto>>> List(CancellationToken cancellationToken) => Ok(await service.ListAsync(cancellationToken));
    [HttpPost]
    public async Task<ActionResult<CreatedRecordDto>> Create(CreateManagedUserRequest request, CancellationToken cancellationToken)
        => Created("/users", new CreatedRecordDto(await service.CreateAsync(request, cancellationToken)));
    [HttpPost("access")]
    public async Task<IActionResult> ChangeAccess(ChangeMemberAccessRequest request, CancellationToken cancellationToken)
    { await service.ChangeAccessAsync(request, cancellationToken); return NoContent(); }
}
