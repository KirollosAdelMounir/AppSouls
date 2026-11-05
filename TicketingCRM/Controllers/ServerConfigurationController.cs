using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
namespace TicketingCRM.Controllers;

[ApiController]
[Route("api/server-configuration")]
[Authorize(Policy="PlatformAdministrator")]
[EnableRateLimiting("licensing")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ServerConfigurationController(IServerSettingsService settings) : ControllerBase
{
    /// <summary>Read nonsecret server configuration and secret-presence indicators.</summary>
    [HttpGet]
    [ProducesResponseType<ServerSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ServerSettingsDto>> Get(CancellationToken cancellationToken)=>Ok(await settings.GetAsync(cancellationToken));
    /// <summary>Stage the database configuration for the next server restart. Null connection strings preserve stored secrets.</summary>
    [HttpPut("database")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateDatabase(UpdateDatabaseSettingsRequest request,CancellationToken cancellationToken){await settings.UpdateDatabaseAsync(request,cancellationToken);return NoContent();}
    /// <summary>Save the SMTP configuration in the encrypted vault.</summary>
    [HttpPut("email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateEmail(UpdateEmailSettingsRequest request,CancellationToken cancellationToken){await settings.UpdateEmailAsync(request,cancellationToken);return NoContent();}
    /// <summary>Configure an integration's endpoint and named credential. Secret values are never returned.</summary>
    [HttpPut("integrations")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateIntegration(UpdateIntegrationSettingsRequest request,CancellationToken cancellationToken){await settings.UpdateIntegrationAsync(request,cancellationToken);return NoContent();}
}
