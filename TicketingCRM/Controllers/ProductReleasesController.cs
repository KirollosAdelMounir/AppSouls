using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Delivery.Interfaces;
namespace TicketingCRM.Controllers;
[ApiController,Authorize,Route("api/product-releases"),AutoValidateAntiforgeryToken,EnableRateLimiting("licensing"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ProductReleasesController(IProductReleaseService service):ControllerBase
{
 [HttpGet]public Task<ProductReleaseSnapshot> Get(CancellationToken ct,string view="published",int page=1)=>service.ListAsync(view,page,ct);
 [HttpPost("store")]public async Task<IActionResult> Store(CreateStoreReleaseRequest request,CancellationToken ct){await service.CreateStoreReleaseAsync(request,ct);return NoContent();}
 [HttpPost("upload"),RequestSizeLimit(538968064),RequestFormLimits(MultipartBodyLengthLimit=538968064)]
 public async Task<IActionResult> Upload([FromForm]Guid OperationId,[FromForm]Guid ProductId,[FromForm]string ReleaseVersion,[FromForm]string Platform,[FromForm]string? Notes,IFormFile Package,CancellationToken ct)
 {await using var stream=Package.OpenReadStream();await service.UploadAsync(new(OperationId,ProductId,ReleaseVersion,Platform,Path.GetFileName(Package.FileName),Notes??""),stream,ct);return LocalRedirect("/downloads?uploaded=true");}
 [HttpPost("{id:guid}/publish")]public async Task<IActionResult> Publish(Guid id,CancellationToken ct){await service.PublishAsync(id,ct);return NoContent();}
 [HttpPost("{id:guid}/revoke")]public async Task<IActionResult> Revoke(Guid id,CancellationToken ct){await service.RevokeAsync(id,ct);return NoContent();}
 [HttpGet("{id:guid}/download")]public async Task<IActionResult> Download(Guid id,CancellationToken ct){var file=await service.DownloadAsync(id,ct);return File(file.Content,"application/octet-stream",file.Filename);}
 [HttpGet("{id:guid}/manifest")]public Task<ProductUpdateManifest> Manifest(Guid id,CancellationToken ct)=>service.ManifestAsync(id,ct);
}
