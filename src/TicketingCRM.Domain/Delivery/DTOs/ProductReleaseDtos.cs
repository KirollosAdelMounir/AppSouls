namespace TicketingCRM.Domain.Delivery.DTOs;
public sealed record ProductReleaseDto(Guid Id,Guid ProductId,string ProductName,string Version,string Platform,string Filename,string Notes,string Sha256,long Size,string Status,DateTimeOffset? PublishedAtUtc,string? StoreUrl = null);
public sealed record ProductReleaseSnapshot(IReadOnlyList<ProductReleaseDto> Releases,bool CanManage);
public sealed record UploadProductReleaseRequest(Guid OperationId,Guid ProductId,string Version,string Platform,string Filename,string Notes);
public sealed record ProductReleaseDownload(string Filename,Stream Content,long Size);
public sealed record ProductUpdateManifest(string Json,string Signature,string IssuerPublicKeyPem,string SignatureContext="AppSouls.ReleaseManifest.v1",string SignatureAlgorithm="RSA-PSS-SHA256");
public enum ArtifactScanVerdict{Clean,Rejected,Unavailable}

public sealed record ArtifactScannerAvailability(bool Available,int? Port,string Code,string Message);

public sealed record CreateStoreReleaseRequest(Guid OperationId,Guid ProductId,string Version,string Platform,string StoreUrl,string Notes);
