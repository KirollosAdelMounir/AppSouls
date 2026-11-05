using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Delivery.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Delivery;
public sealed class ProductReleaseService(IWorkspaceSessionFactory sessions,ICurrentActor actor,PrivateArtifactStore files,IArtifactScanner scanner,ServerConfiguration configuration):IProductReleaseService
{
 private async Task Access(IWorkspaceSession s,ProductRelease release,bool manage,CancellationToken ct)
 {
  var member=await WorkspaceAccess.RequireAsync(s,actor,technical:manage,ct:ct);
  if(member.CustomerId is {} customer)
  {
   var now=DateTimeOffset.UtcNow;
   if(release.Status!="Published"||!await s.Query<Product>().AnyAsync(x=>x.Id==release.ProductId&&x.IsActive,ct)||!await s.Query<Entitlement>().AnyAsync(x=>x.CustomerId==customer&&x.ProductId==release.ProductId&&!x.IsRevoked&&x.StartsAtUtc<=now&&(x.ExpiresAtUtc==null||x.ExpiresAtUtc>now)&&s.Query<Contract>().Any(c=>c.Id==x.ContractId&&c.Status=="Active"&&c.StartsAtUtc<=now&&(c.EndsAtUtc==null||c.EndsAtUtc>now)),ct))throw new UnauthorizedAccessException();
  }
 }
 public async Task<ProductReleaseSnapshot> GetAsync(CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:cancellationToken);var query=s.Query<ProductRelease>();
  if(member.CustomerId is {} customer){var now=DateTimeOffset.UtcNow;query=query.Where(r=>r.Status=="Published"&&s.Query<Product>().Any(p=>p.Id==r.ProductId&&p.IsActive)&&s.Query<Entitlement>().Any(e=>e.CustomerId==customer&&e.ProductId==r.ProductId&&!e.IsRevoked&&e.StartsAtUtc<=now&&(e.ExpiresAtUtc==null||e.ExpiresAtUtc>now)&&s.Query<Contract>().Any(c=>c.Id==e.ContractId&&c.Status=="Active"&&c.StartsAtUtc<=now&&(c.EndsAtUtc==null||c.EndsAtUtc>now))));}
  var rows=await(from r in query join p in s.Query<Product>() on r.ProductId equals p.Id orderby r.UploadedAtUtc descending select new ProductReleaseDto(r.Id,r.ProductId,p.Name,r.ReleaseVersion,r.Platform,r.Filename,r.Notes,r.Sha256,r.Size,r.Status,r.PublishedAtUtc)).Take(100).ToListAsync(cancellationToken);
  return new(rows,member.CustomerId is null&&member.Role is "Owner" or "Admin" or "Technical");
 }
 public async Task UploadAsync(UploadProductReleaseRequest request,Stream content,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await WorkspaceAccess.RequireAsync(s,actor,technical:true,ct:cancellationToken);
  if(request.OperationId==Guid.Empty||!Regex.IsMatch(request.Version??"",@"\A[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?\z")||(request.Version?.Length??0)>80||request.Platform is not ("Windows" or "Linux" or "FreeBSD" or "Cross-platform")||(request.Notes?.Length??0)>8000)throw new ArgumentException("Enter a semantic version, supported platform and release notes up to 8000 characters.");
  var name=Path.GetFileName(request.Filename??"");if(name.Length is <1 or >160||name!=request.Filename||name.Any(c=>char.IsControl(c)||c is '/' or '\\')||!new[]{".zip",".msi",".exe",".deb",".rpm",".pkg",".gz"}.Contains(Path.GetExtension(name).ToLowerInvariant()))throw new ArgumentException("Choose a supported installation package filename.");
  if(!await s.Query<Product>().AnyAsync(x=>x.Id==request.ProductId&&x.IsActive,cancellationToken))throw new ArgumentException("Choose an active product.");
  if(await s.Query<ProductRelease>().AnyAsync(x=>x.Id==request.OperationId||x.ProductId==request.ProductId&&x.ReleaseVersion==request.Version&&x.Platform==request.Platform,cancellationToken))throw new ArgumentException("This release already exists. Use a new version.");
  var stored=await files.SaveAsync(request.OperationId,content,cancellationToken);
  try{
   var tenant=await s.Query<Tenant>().SingleAsync(x=>x.Id==actor.TenantId,cancellationToken);
   var allocated=await s.Query<ProductRelease>().SumAsync(x=>(long?)x.Size,cancellationToken)??0;
   if(allocated>10L*1024*1024*1024-stored.Size)throw new ArgumentException("The workspace installation storage limit has been reached. Contact the platform administrator to review retention.");
   tenant.Version=Guid.NewGuid(); // Serialize quota allocation through optimistic concurrency on this tenant.
   s.Add(new ProductRelease{Id=request.OperationId,TenantId=actor.TenantId,ProductId=request.ProductId,UploadedByUserId=actor.UserId,ReleaseVersion=request.Version!,Platform=request.Platform,Filename=name,Notes=request.Notes??"",Sha256=stored.Sha256,Size=stored.Size,UploadedAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(cancellationToken);}
  catch{files.Delete(request.OperationId);throw;}
 }
 public async Task PublishAsync(Guid releaseId,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var r=await s.Query<ProductRelease>().SingleOrDefaultAsync(x=>x.Id==releaseId,cancellationToken)??throw new ArgumentException("Release not found.");await Access(s,r,true,cancellationToken);
  if(r.Status!="Quarantined")throw new ArgumentException("Only quarantined releases can be published.");
  if(!await files.VerifyAsync(r.Id,r.Sha256,cancellationToken))throw new InvalidOperationException("The installation file failed integrity verification.");
  await using(var content=files.Open(r.Id))
  {
   var result=await scanner.ScanAsync(content,cancellationToken);
   if(result!=ArtifactScanVerdict.Clean)throw new ArgumentException(result==ArtifactScanVerdict.Rejected?"Malware was detected. The installation remains quarantined.":"The malware scanner is unavailable. Configure the local scanner before publishing.");
  }
  if(!await files.VerifyAsync(r.Id,r.Sha256,cancellationToken))throw new InvalidOperationException("The installation file changed during scanning.");
  var now=DateTimeOffset.UtcNow;r.ManifestJson=JsonSerializer.Serialize(new{FormatVersion=1,ReleaseId=r.Id,TenantId=r.TenantId,r.ProductId,Version=r.ReleaseVersion,r.Platform,r.Filename,r.Sha256,r.Size,PublishedAtUtc=now});
  using var rsa=RSA.Create();rsa.ImportFromPem(configuration.LicenseSigningPrivateKeyPem);r.ManifestSignature=Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes("AppSouls.ReleaseManifest.v1\n"+r.ManifestJson),HashAlgorithmName.SHA256,RSASignaturePadding.Pss));r.Status="Published";r.PublishedAtUtc=now;r.PublishedByUserId=actor.UserId;await s.CommitAsync(cancellationToken);
 }
 public async Task RevokeAsync(Guid releaseId,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var r=await s.Query<ProductRelease>().SingleOrDefaultAsync(x=>x.Id==releaseId,cancellationToken)??throw new ArgumentException("Release not found.");await Access(s,r,true,cancellationToken);if(r.Status=="Revoked")return;r.Status="Revoked";r.RevokedByUserId=actor.UserId;r.RevokedAtUtc=DateTimeOffset.UtcNow;await s.CommitAsync(cancellationToken);
 }
 private bool ManifestValid(ProductRelease r)
 {
  using var rsa=RSA.Create();rsa.ImportFromPem(configuration.LicenseSigningPrivateKeyPem);
  try
  {
   if(!rsa.VerifyData(Encoding.UTF8.GetBytes("AppSouls.ReleaseManifest.v1\n"+r.ManifestJson),Convert.FromBase64String(r.ManifestSignature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))return false;
   using var doc=JsonDocument.Parse(r.ManifestJson);var manifest=doc.RootElement;
   return manifest.GetProperty("ReleaseId").GetGuid()==r.Id&&manifest.GetProperty("TenantId").GetGuid()==r.TenantId&&manifest.GetProperty("ProductId").GetGuid()==r.ProductId&&manifest.GetProperty("Version").GetString()==r.ReleaseVersion&&manifest.GetProperty("Platform").GetString()==r.Platform&&manifest.GetProperty("Filename").GetString()==r.Filename&&manifest.GetProperty("Sha256").GetString()==r.Sha256&&manifest.GetProperty("Size").GetInt64()==r.Size;
  }
  catch(Exception e)when(e is FormatException or JsonException or KeyNotFoundException or InvalidOperationException){return false;}
 }
 public async Task<ProductReleaseDownload> DownloadAsync(Guid releaseId,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var r=await s.Query<ProductRelease>().SingleOrDefaultAsync(x=>x.Id==releaseId&&x.Status=="Published",cancellationToken)??throw new ArgumentException("Release not found.");await Access(s,r,false,cancellationToken);
  if(!ManifestValid(r))throw new InvalidOperationException("The release failed integrity verification.");
  var stream=files.Open(r.Id);
  try{var digest=await SHA256.HashDataAsync(stream,cancellationToken);if(!CryptographicOperations.FixedTimeEquals(digest,Convert.FromHexString(r.Sha256))||stream.Length!=r.Size)throw new InvalidOperationException("The release failed integrity verification.");stream.Position=0;return new(r.Filename,stream,r.Size);}
  catch{await stream.DisposeAsync();throw;}
 }
 public async Task<ProductUpdateManifest> ManifestAsync(Guid releaseId,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var r=await s.Query<ProductRelease>().SingleOrDefaultAsync(x=>x.Id==releaseId&&x.Status=="Published",cancellationToken)??throw new ArgumentException("Release not found.");await Access(s,r,false,cancellationToken);
  if(!ManifestValid(r))throw new InvalidOperationException("The release failed integrity verification.");using var rsa=RSA.Create();rsa.ImportFromPem(configuration.LicenseSigningPrivateKeyPem);return new(r.ManifestJson,r.ManifestSignature,rsa.ExportSubjectPublicKeyInfoPem());
 }
}
