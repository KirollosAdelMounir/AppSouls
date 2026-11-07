using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Commerce.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Commerce;
public sealed partial class CommerceService
{
 public async Task<IReadOnlyList<CatalogPriceDto>> CatalogAsync(int page=1,CancellationToken ct=default)
 {
  if(page is <1 or >10000)throw new ArgumentException("Choose a valid page.");
  await using var s=await sessions.OpenAsync(ct);await WorkspaceAccess.RequireAsync(s,actor,sales:true,ct:ct);
  return await (from product in s.Repository<Product>().GetReadOnlyQueryable() join price in s.Repository<ProductPricing>().GetReadOnlyQueryable() on product.Id equals price.ProductId into prices from price in prices.DefaultIfEmpty() where product.IsActive orderby product.Name select new CatalogPriceDto(product.Id,price==null?null:price.Version,product.Name,product.Code,price==null?"USD":price.Currency,price==null?null:price.UnitPrice,price==null?"":price.Description,price!=null&&price.ApprovedByUserId!=null)).Skip((page-1)*100).Take(100).ToListAsync(ct);
 }
 public async Task UpdatePricingAsync(UpdatePricingRequest request,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,sales:true,ct:ct);
  if(request.Approve&&member.Role is not("Owner" or "Admin"))throw new UnauthorizedAccessException();
  if(request.UnitPrice<0||request.UnitPrice>999999999.99m||decimal.Round(request.UnitPrice,2)!=request.UnitPrice)throw new ArgumentException("Enter a nonnegative price with up to two decimal places.");
  if(!await s.Repository<Product>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==request.ProductId&&x.IsActive,ct))throw new ArgumentException("Product not found.");
  var row=await s.Repository<ProductPricing>().GetAllQueryable().SingleOrDefaultAsync(x=>x.ProductId==request.ProductId,ct);
  if(row is not null&&row.Version!=request.Version)throw new InvalidOperationException("Pricing changed. Reload before saving.");
  if(row is null){if(request.Version is not null)throw new InvalidOperationException("Reload product pricing.");row=new ProductPricing{TenantId=actor.TenantId,ProductId=request.ProductId};s.Repository<ProductPricing>().Add(row);}
  row.Currency=Currency(request.Currency);row.UnitPrice=request.UnitPrice;row.Description=Text(request.Description,8000);row.UpdatedByUserId=actor.UserId;row.ApprovedByUserId=request.Approve?actor.UserId:null;row.ApprovedAtUtc=request.Approve?DateTimeOffset.UtcNow:null;await s.CommitAsync(ct);
 }
}
