using System.Text.RegularExpressions;
using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Delivery;
public sealed partial class ProductReleaseService
{
 public async Task CreateStoreReleaseAsync(CreateStoreReleaseRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await WorkspaceAccess.RequireAsync(s,actor,developer:true,ct:cancellationToken);
  if(request.OperationId==Guid.Empty||!Regex.IsMatch(request.Version??"",@"\A[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?\z")||(request.Version?.Length??0)>80||(request.Notes?.Length??0)>8000||!ValidStoreUrl(request.Platform,request.StoreUrl))throw new ArgumentException("Use a semantic version and the official Play Store or App Store product URL for Android or IOS.");
  if(!await s.Repository<Product>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==request.ProductId&&x.IsActive,cancellationToken))throw new ArgumentException("Choose an active product.");
  if(await s.Repository<ProductRelease>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==request.OperationId||x.ProductId==request.ProductId&&x.ReleaseVersion==request.Version&&x.Platform==request.Platform,cancellationToken))throw new InvalidOperationException("The release already exists. Use a new version.");
  var now=DateTimeOffset.UtcNow;s.Repository<ProductRelease>().Add(new ProductRelease{Id=request.OperationId,TenantId=actor.TenantId,ProductId=request.ProductId,ReleaseVersion=request.Version!,Platform=request.Platform,StoreUrl=request.StoreUrl,Notes=request.Notes??"",Status="Published",UploadedByUserId=actor.UserId,PublishedByUserId=actor.UserId,UploadedAtUtc=now,PublishedAtUtc=now});await s.CommitAsync(cancellationToken);
 }
 public static bool ValidStoreUrl(string platform,string? url)
 {
  if(url is null||url.Length>2048||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||!uri.IsDefaultPort||uri.Fragment.Length>0)return false;
  return platform switch
  {
   "Android"=>uri.IdnHost=="play.google.com"&&uri.AbsolutePath=="/store/apps/details"&&uri.Query.TrimStart('?').Split('&').Any(x=>Regex.IsMatch(x,@"\Aid=[A-Za-z0-9_.]+\z")),
   "IOS"=>uri.IdnHost=="apps.apple.com"&&Regex.IsMatch(uri.AbsolutePath,@"\A/(?:[a-z]{2}/)?app/(?:[^/]+/)?id[0-9]+/?\z"),
   _=>false
  };
 }
}
