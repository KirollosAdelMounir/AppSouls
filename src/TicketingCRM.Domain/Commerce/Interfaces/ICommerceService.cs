using TicketingCRM.Domain.Commerce.DTOs;
namespace TicketingCRM.Domain.Commerce.Interfaces;
public interface ICommerceService
{
 Task<IReadOnlyList<OrderDto>> OrdersAsync(Guid? customerId = null,int page=1,CancellationToken ct=default);
 Task<OrderDto> OrderAsync(Guid id,CancellationToken ct=default);
 Task<Guid> CreateOrderAsync(CreateOrderRequest request,CancellationToken ct=default);
 Task<IReadOnlyList<InvoiceDto>> InvoicesAsync(Guid? customerId = null,int page=1,CancellationToken ct=default);
 Task<InvoiceDto> InvoiceAsync(Guid id,CancellationToken ct=default);
 Task<Guid> CreateInvoiceAsync(CreateInvoiceRequest request,CancellationToken ct=default);
 Task<InvoiceFile> DownloadInvoiceAsync(Guid id,CancellationToken ct=default);
 Task<IReadOnlyList<CatalogPriceDto>> CatalogAsync(int page=1,CancellationToken ct=default);
 Task UpdatePricingAsync(UpdatePricingRequest request,CancellationToken ct=default);
}
