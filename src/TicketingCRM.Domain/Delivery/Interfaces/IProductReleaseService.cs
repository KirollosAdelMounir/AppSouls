using TicketingCRM.Domain.Delivery.DTOs;
namespace TicketingCRM.Domain.Delivery.Interfaces;
public interface IProductReleaseService
{
 Task CreateStoreReleaseAsync(CreateStoreReleaseRequest request,CancellationToken cancellationToken=default);
 Task<ProductReleaseSnapshot> GetAsync(CancellationToken cancellationToken=default);
 Task<ProductReleaseSnapshot> ListAsync(string view,int page=1,CancellationToken cancellationToken=default);
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

public interface IArtifactScannerProbe
{
 Task<ArtifactScannerAvailability> CheckAsync(CancellationToken cancellationToken=default);
}
