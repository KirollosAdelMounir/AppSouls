using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Commerce.DTOs;
using TicketingCRM.Domain.Commerce.Interfaces;
namespace TicketingCRM.Controllers;
[ApiController,Route("api/commerce"),Authorize,AutoValidateAntiforgeryToken,EnableRateLimiting("licensing"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CommerceController(ICommerceService service):ControllerBase
{
 [HttpGet("orders")]public Task<IReadOnlyList<OrderDto>> Orders(Guid? customerId,int page=1,CancellationToken ct=default)=>service.OrdersAsync(customerId,page,ct);
 [HttpGet("orders/{id:guid}")]public Task<OrderDto> Order(Guid id,CancellationToken ct)=>service.OrderAsync(id,ct);
 [HttpPost("orders"),RequestSizeLimit(131072)]public async Task<ActionResult<Guid>> CreateOrder(CreateOrderRequest request,CancellationToken ct)=>Ok(await service.CreateOrderAsync(request,ct));
 [HttpGet("invoices")]public Task<IReadOnlyList<InvoiceDto>> Invoices(Guid? customerId,int page=1,CancellationToken ct=default)=>service.InvoicesAsync(customerId,page,ct);
 [HttpGet("invoices/{id:guid}")]public Task<InvoiceDto> Invoice(Guid id,CancellationToken ct)=>service.InvoiceAsync(id,ct);
 [HttpPost("invoices"),RequestSizeLimit(262144)]public async Task<ActionResult<Guid>> CreateInvoice(CreateInvoiceRequest request,CancellationToken ct)=>Ok(await service.CreateInvoiceAsync(request,ct));
 [HttpGet("invoices/{id:guid}/pdf")]public async Task<IActionResult> Pdf(Guid id,CancellationToken ct){var file=await service.DownloadInvoiceAsync(id,ct);return File(file.Content,"application/pdf",file.Filename);}
 [HttpGet("catalog"),Authorize(Policy="SalesWorkspace")]public Task<IReadOnlyList<CatalogPriceDto>> Catalog(int page=1,CancellationToken ct=default)=>service.CatalogAsync(page,ct);
 [HttpPut("catalog"),Authorize(Policy="SalesWorkspace")]public async Task<IActionResult> UpdatePricing(UpdatePricingRequest request,CancellationToken ct){await service.UpdatePricingAsync(request,ct);return NoContent();}
}
