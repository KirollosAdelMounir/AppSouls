using TicketingCRM.Domain.Commerce.DTOs;
using TicketingCRM.Domain.Commerce.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class CustomerOrdersViewModel(ICommerceService service):OperationViewModel
{
 public IReadOnlyList<OrderDto>? Orders{get;private set;}
 public Task LoadAsync(Guid customerId)=>RunAsync(async()=>Orders=await service.OrdersAsync(customerId));
}
