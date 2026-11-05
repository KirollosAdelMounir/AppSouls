using TicketingCRM.Domain.Customers.DTOs;
namespace TicketingCRM.Domain.Customers.Interfaces;
public interface ICustomerLifecycleService
{
 Task<CustomerLifecycleSnapshot> GetAsync(Guid customerId,CancellationToken cancellationToken=default);
 Task RecordActivityAsync(RecordCustomerActivityRequest request,CancellationToken cancellationToken=default);
 Task ReissueInvitationAsync(ReissueCustomerInvitationRequest request,CancellationToken cancellationToken=default);
 Task FinalizeSaleAsync(FinalizeCustomerSaleRequest request,CancellationToken cancellationToken=default);
}
public interface ICustomerInvitationService
{
 Task AcceptAsync(AcceptCustomerInvitationRequest request,CancellationToken cancellationToken=default);
}
