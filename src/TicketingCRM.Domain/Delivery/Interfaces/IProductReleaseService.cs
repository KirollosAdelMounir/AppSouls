using TicketingCRM.Domain.Delivery.DTOs;
namespace TicketingCRM.Domain.Delivery.Interfaces;
public interface IProductReleaseService
{
 Task<ProductReleaseSnapshot> GetAsync(CancellationToken cancellationToken=default);
 Task UploadAsync(UploadProductReleaseRequest request,Stream content,CancellationToken cancellationToken=default);
 Task PublishAsync(Guid releaseId,CancellationToken cancellationToken=default);
 Task RevokeAsync(Guid releaseId,CancellationToken cancellationToken=default);
 Task<ProductReleaseDownload> DownloadAsync(Guid releaseId,CancellationToken cancellationToken=default);
 Task<ProductUpdateManifest> ManifestAsync(Guid releaseId,CancellationToken cancellationToken=default);
}
public interface IArtifactScanner
{
 Task<ArtifactScanVerdict> ScanAsync(Stream content,CancellationToken cancellationToken=default);
}
