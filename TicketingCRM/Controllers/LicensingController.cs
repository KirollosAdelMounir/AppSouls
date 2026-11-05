using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Interfaces;
namespace TicketingCRM.Controllers;

[ApiController]
[Route("api/licensing")]
[Authorize]
[EnableRateLimiting("licensing")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[AutoValidateAntiforgeryToken]
public sealed class LicensingController(ILicensingService service,IMachineLicenseLookup lookup) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<LicensingSnapshot>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LicensingSnapshot>> Get(CancellationToken cancellationToken)=>Ok(await service.GetAsync(cancellationToken));
    [HttpGet("schemas/{id:guid}/export")]
    [Produces("application/zip")]
    public async Task<IActionResult> Export(Guid id,CancellationToken cancellationToken){var file=await service.ExportModelAsync(id,cancellationToken);return File(file.Content,file.ContentType,file.Filename);}
    [HttpGet("revisions/{id:guid}/download")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Download(Guid id,CancellationToken cancellationToken){var file=await service.DownloadAsync(id,cancellationToken);return File(file.Content,file.ContentType,file.Filename);}
    [HttpPost("schemas")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatedRecordDto>> Publish(PublishLicenseSchemaRequest request,CancellationToken cancellationToken)=>Ok(new CreatedRecordDto(await service.PublishSchemaAsync(request,cancellationToken)));
    [HttpPost("installations")]
    [ProducesResponseType<InstallationEnrollment>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InstallationEnrollment>> Enroll(EnrollInstallationRequest request,CancellationToken cancellationToken)=>Ok(await service.EnrollAsync(request,cancellationToken));
    [HttpPost("issue")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatedRecordDto>> Issue(IssueLicenseRequest request,CancellationToken cancellationToken)=>Ok(new CreatedRecordDto(await service.IssueAsync(request,cancellationToken)));
    /// <summary>Retrieve an approved newer license using the installation bearer credential, including after license expiry.</summary>
    [HttpGet("latest")]
    [AllowAnonymous]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Latest([FromQuery]int currentRevision=0,CancellationToken cancellationToken=default)
    {
        Response.Headers.CacheControl="no-store";
        var authorization=Request.Headers.Authorization.ToString();
        if(!authorization.StartsWith("Bearer ",StringComparison.Ordinal)||currentRevision<0)return Unauthorized();
        var artifact=await lookup.FindRenewalAsync(authorization[7..],currentRevision,cancellationToken);
        if(artifact is null)return NoContent();
        Response.Headers["X-License-Revision"]=artifact.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return File(artifact.Artifact,"application/octet-stream",$"license-{artifact.Id:N}{artifact.Extension}");
    }
}
