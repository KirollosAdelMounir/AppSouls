using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Commerce.DTOs;
using TicketingCRM.Domain.Commerce.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Commerce;
public sealed partial class CommerceService(IWorkspaceSessionFactory sessions,ICurrentActor actor):ICommerceService
{
 private static string Text(string? text,int length,bool required=true){var value=(text??"").Trim();if((required&&value.Length==0)||value.Length>length||value.Any(c=>char.IsControl(c)&&c is not '\n' and not '\r' and not '\t'))throw new ArgumentException($"Enter text up to {length} characters.");return value;}
 private static string Currency(string value){var code=Text(value,3).ToUpperInvariant();if(code is not("USD" or "EUR" or "GBP" or "EGP" or "SAR" or "AED"))throw new ArgumentException("Choose a supported currency.");return code;}
 private async Task<TenantMembership> Access(IWorkspaceSession session,bool manager,CancellationToken ct)
 {
  var member=await WorkspaceAccess.RequireAsync(session,actor,ct:ct);
  if(member.CustomerId is not null||member.Role is not("Owner" or "Admin" or "Sales" or "Sales Manager"))throw new UnauthorizedAccessException();
  if(manager&&(member.CustomerId is not null||member.Role is not("Owner" or "Admin" or "Sales Manager")))throw new UnauthorizedAccessException();
  return member;
 }
 private static OrderDto Order(CommerceOrder item){var parsed=CommercialPricing.Calculate(JsonSerializer.Deserialize<CommercialLine[]>(item.LinesJson)!);return new(item.Id,item.CustomerId,item.ContractId,item.Reference,item.Currency,item.Status,parsed.Lines,parsed.Totals,item.Notes,item.CreatedAtUtc);}
 public async Task<IReadOnlyList<OrderDto>> OrdersAsync(Guid? customerId=null,int page=1,CancellationToken ct=default)
 {
  if(page is <1 or >10000)throw new ArgumentException("Choose a valid page.");
  await using var s=await sessions.OpenAsync(ct);var member=await Access(s,false,ct);if(member.CustomerId is {} own){if(customerId is {} requested&&requested!=own)throw new UnauthorizedAccessException();customerId=own;}
  var query=s.Repository<CommerceOrder>().GetReadOnlyQueryable();if(customerId is {} customer)query=query.Where(x=>x.CustomerId==customer);
  return (await query.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip((page-1)*100).Take(100).ToListAsync(ct)).Select(Order).ToArray();
 }
 public async Task<OrderDto> OrderAsync(Guid id,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await Access(s,false,ct);var row=await s.Repository<CommerceOrder>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Order not found.");if(member.CustomerId is {} own&&row.CustomerId!=own)throw new UnauthorizedAccessException();return Order(row);
 }
 private static async Task CheckLines(IWorkspaceSession s,IReadOnlyList<CommercialLine> lines,CancellationToken ct)
 {
  var ids=lines.Where(x=>x.ProductId.HasValue).Select(x=>x.ProductId!.Value).Distinct().ToArray();
  if(await s.Repository<Product>().GetReadOnlyQueryable().CountAsync(x=>ids.Contains(x.Id)&&x.IsActive,ct)!=ids.Length)throw new ArgumentException("Select active products in this organization.");
 }
 public async Task<Guid> CreateOrderAsync(CreateOrderRequest request,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await Access(s,false,ct);if(member.CustomerId is not null)throw new UnauthorizedAccessException();
  if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation identifier is required.");
  var reference=Text(request.Reference,100);var currency=Currency(request.Currency);var notes=Text(request.Notes,4000,false);var priced=CommercialPricing.Calculate(request.Lines);await CheckLines(s,request.Lines,ct);
  if(!await s.Repository<Customer>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==request.CustomerId,ct))throw new ArgumentException("Customer not found.");
  if(request.ContractId is {} contract&&!await s.Repository<Contract>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==contract&&x.CustomerId==request.CustomerId,ct))throw new ArgumentException("Contract does not belong to the customer.");
  var json=JsonSerializer.Serialize(request.Lines);if(json.Length>64000)throw new ArgumentException("Shorten the order lines.");
  var prior=await s.Repository<CommerceOrder>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,ct);
  if(prior is not null){if(prior.CreatedByUserId!=actor.UserId||prior.CustomerId!=request.CustomerId||prior.ContractId!=request.ContractId||prior.Reference!=reference||prior.Currency!=currency||prior.LinesJson!=json||prior.Notes!=notes)throw new InvalidOperationException("The operation identifier was already used with different order details.");return prior.Id;}
  var order=new CommerceOrder{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=request.CustomerId,ContractId=request.ContractId,CreatedByUserId=actor.UserId,Reference=reference,Currency=currency,LinesJson=json,Notes=notes,CreatedAtUtc=DateTimeOffset.UtcNow};s.Repository<CommerceOrder>().Add(order);await s.CommitAsync(ct);return order.Id;
 }
 public async Task<IReadOnlyList<InvoiceDto>> InvoicesAsync(Guid? customerId=null,int page=1,CancellationToken ct=default)
 {
  if(page is <1 or >10000)throw new ArgumentException("Choose a valid page.");
  await using var s=await sessions.OpenAsync(ct);var member=await Access(s,false,ct);if(member.CustomerId is {} own){if(customerId is {} requested&&requested!=own)throw new UnauthorizedAccessException();customerId=own;}
  var query=s.Repository<InvoiceDocument>().GetReadOnlyQueryable();if(customerId is {} customer)query=query.Where(x=>x.CustomerId==customer);
  var rows=await query.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip((page-1)*100).Take(100).ToListAsync(ct);return rows.Select(x=>JsonSerializer.Deserialize<InvoiceDto>(x.SnapshotJson)!).ToArray();
 }
 public async Task<InvoiceDto> InvoiceAsync(Guid id,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await Access(s,false,ct);var row=await s.Repository<InvoiceDocument>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Invoice not found.");if(member.CustomerId is {} own&&row.CustomerId!=own)throw new UnauthorizedAccessException();return JsonSerializer.Deserialize<InvoiceDto>(row.SnapshotJson)!;
 }
 public async Task<Guid> CreateInvoiceAsync(CreateInvoiceRequest request,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);await Access(s,true,ct);if(request.OperationId==Guid.Empty||request.Invoice is null||(request.OrderId is null&&request.ContractId is null))throw new ArgumentException("Select an order or contract and enter invoice details.");
  var customer=await s.Repository<Customer>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.CustomerId,ct)??throw new ArgumentException("Customer not found.");
  Guid? contract=request.ContractId;
  if(request.OrderId is {} orderId){var order=await s.Repository<CommerceOrder>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==orderId&&x.CustomerId==customer.Id,ct)??throw new ArgumentException("Order not found for this customer.");if(contract is {} explicitContract&&order.ContractId!=explicitContract)throw new ArgumentException("The contract does not match the order.");contract=order.ContractId;}
  if(contract is {} contractId&&!await s.Repository<Contract>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==contractId&&x.CustomerId==customer.Id,ct))throw new ArgumentException("Contract not found for this customer.");
  var input=request.Invoice;var details=input with{Number=Text(input.Number,100),SupplierName=Text(input.SupplierName,200),SupplierTaxId=Text(input.SupplierTaxId,100,false),SupplierAddress=Text(input.SupplierAddress,2000),BuyerName=Text(input.BuyerName,200),BuyerTaxId=Text(input.BuyerTaxId,100,false),BuyerAddress=Text(input.BuyerAddress,2000),Currency=Currency(input.Currency),PaymentTerms=Text(input.PaymentTerms,2000,false),Notes=Text(input.Notes,4000,false),IssueDateUtc=input.IssueDateUtc.ToUniversalTime(),DueDateUtc=input.DueDateUtc?.ToUniversalTime()};
  if(details.DueDateUtc<details.IssueDateUtc)throw new ArgumentException("Due date must not precede the invoice date.");
  var computed=CommercialPricing.Calculate(details.Lines);await CheckLines(s,details.Lines,ct);
  var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request with{Invoice=details,ContractId=contract}))));
  var prior=await s.Repository<InvoiceDocument>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,ct);if(prior is not null){if(prior.CreatedByUserId!=actor.UserId||prior.RequestDigest!=digest)throw new InvalidOperationException("The operation identifier was already used with different invoice details.");return prior.Id;}
  var snapshot=new InvoiceDto(request.OperationId,customer.Id,request.OrderId,contract,customer.Name,details,computed.Lines,computed.Totals,DateTimeOffset.UtcNow);var json=JsonSerializer.Serialize(snapshot);if(json.Length>128000)throw new ArgumentException("Shorten the invoice details.");
  s.Repository<InvoiceDocument>().Add(new InvoiceDocument{Id=snapshot.Id,TenantId=actor.TenantId,CustomerId=customer.Id,OrderId=request.OrderId,ContractId=contract,CreatedByUserId=actor.UserId,Number=details.Number,RequestDigest=digest,SnapshotJson=json,CreatedAtUtc=snapshot.CreatedAtUtc});await s.CommitAsync(ct);return snapshot.Id;
 }
 public async Task<InvoiceFile> DownloadInvoiceAsync(Guid id,CancellationToken ct=default){var invoice=await InvoiceAsync(id,ct);ct.ThrowIfCancellationRequested();return new($"invoice-{id:N}.pdf",InvoicePdfWriter.Write(invoice));}
}
