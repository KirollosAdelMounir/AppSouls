using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Domain.Delivery.DTOs;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class ProductReleaseServiceTests
{
 private static readonly byte[] Package=Encoding.UTF8.GetBytes("fixture package content - never an executable");
 private static UploadProductReleaseRequest Request(CustomerDeliveryFixture f)=>new(Guid.NewGuid(),f.Product,"1.0.0","Windows","suite.zip","Release notes");
 [PostgreSqlFact]
 public async Task TechnicalUploadStaysQuarantinedUntilCleanScanThenOwnedSubscribersCanDownload()
 {
  using var f=await CustomerDeliveryFixture.Create();var request=Request(f);await f.Releases(f.TechnicalActor).UploadAsync(request,new MemoryStream(Package));
  Assert.Equal("Quarantined",Assert.Single((await f.Releases().GetAsync()).Releases).Status);Assert.Empty((await f.Releases(f.CustomerActor).GetAsync()).Releases);
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases(verdict:ArtifactScanVerdict.Unavailable).PublishAsync(request.OperationId));
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases(verdict:ArtifactScanVerdict.Rejected).PublishAsync(request.OperationId));
  await f.Releases().PublishAsync(request.OperationId);var manifest=await f.Releases(f.CustomerActor).ManifestAsync(request.OperationId);
  using var key=RSA.Create();key.ImportFromPem(manifest.IssuerPublicKeyPem);Assert.True(key.VerifyData(Encoding.UTF8.GetBytes(manifest.SignatureContext+"\n"+manifest.Json),Convert.FromBase64String(manifest.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
  var file=await f.Releases(f.CustomerActor).DownloadAsync(request.OperationId);await using(file.Content){using var bytes=new MemoryStream();await file.Content.CopyToAsync(bytes);Assert.Equal(Package,bytes.ToArray());}
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Releases(f.OtherActor).DownloadAsync(request.OperationId));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Releases(f.CustomerActor).UploadAsync(Request(f),new MemoryStream(Package)));
  await f.Releases().RevokeAsync(request.OperationId);await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases(f.CustomerActor).DownloadAsync(request.OperationId));
 }
 [PostgreSqlFact]
 public async Task TamperingRevokedEntitlementAndForeignTenantCannotGrantDownloads()
 {
  using var f=await CustomerDeliveryFixture.Create();using var other=await CustomerDeliveryFixture.Create();var request=Request(f);await f.Releases().UploadAsync(request,new MemoryStream(Package));await f.Releases().PublishAsync(request.OperationId);
  await Assert.ThrowsAsync<ArgumentException>(()=>other.Releases().DownloadAsync(request.OperationId));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){(await db.Entitlements.SingleAsync()).IsRevoked=true;await db.SaveChangesAsync();}
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Releases(f.CustomerActor).DownloadAsync(request.OperationId));
  await System.IO.File.WriteAllBytesAsync(Path.Combine(f.Root,request.OperationId.ToString("N")+".bin"),Encoding.UTF8.GetBytes("tampered"));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Releases().DownloadAsync(request.OperationId));
 }
 [PostgreSqlFact]
 public async Task UploadRejectsPathTraversalEmptyFilesDuplicateVersionsAndForgedMembership()
 {
  using var f=await CustomerDeliveryFixture.Create();var request=Request(f);
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases().UploadAsync(request with{Filename="../suite.zip"},new MemoryStream(Package)));
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases().UploadAsync(request,new MemoryStream()));
  Assert.Empty(Directory.GetFiles(f.Root));await f.Releases().UploadAsync(request,new MemoryStream(Package));
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases().UploadAsync(request with{OperationId=Guid.NewGuid()},new MemoryStream(Package)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Releases(f.TechnicalActor with{Role="Owner"}).PublishAsync(request.OperationId));
  Assert.Single(Directory.GetFiles(f.Root));
 }
 [PostgreSqlFact]
 public async Task PublishedManifestAndMetadataAreImmutableAndTamperingIsRejected()
 {
  using var f=await CustomerDeliveryFixture.Create();var request=Request(f);await f.Releases().UploadAsync(request,new MemoryStream(Package));await f.Releases().PublishAsync(request.OperationId);
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){var release=await db.ProductReleases.SingleAsync();release.Filename="forged.zip";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());}
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProductReleases\" SET \"ManifestSignature\" = {Convert.ToBase64String(new byte[384])} WHERE \"Id\" = {request.OperationId}");}
  await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Releases(f.CustomerActor).DownloadAsync(request.OperationId));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Releases(f.CustomerActor).ManifestAsync(request.OperationId));
 }

 [PostgreSqlFact]
 public async Task WorkspaceStorageQuotaRejectsNewPackageAndCleansItsPrivateFile()
 {
  using var f=await CustomerDeliveryFixture.Create();var first=Request(f);await f.Releases().UploadAsync(first,new MemoryStream(Package));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){(await db.ProductReleases.SingleAsync()).Size=10L*1024*1024*1024;await db.SaveChangesAsync();}
  var next=Request(f) with{Version="1.0.1"};await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases().UploadAsync(next,new MemoryStream(Package)));
  Assert.Single(Directory.GetFiles(f.Root));await using var check=CustomerDeliveryFixture.Open(f.Tenant);Assert.Single(await check.ProductReleases.ToListAsync());
 }

}
