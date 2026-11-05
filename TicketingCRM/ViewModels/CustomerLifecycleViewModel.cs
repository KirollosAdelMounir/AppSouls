using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Customers.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class CustomerLifecycleViewModel(ICustomerLifecycleService service,Guid customerId):OperationViewModel
{
 public CustomerLifecycleSnapshot? Data{get;private set;}
 public string Kind{get;set;}="Note";public string Subject{get;set;}="";public string Notes{get;set;}="";public DateTime? FollowUp{get;set;}
 public string Opportunity{get;set;}="";public string Contract{get;set;}="";
 private Guid ActivityOperation=Guid.NewGuid(),FinalizeOperation=Guid.NewGuid();
 public static IReadOnlyList<string> Kinds{get;}=["Call","Meeting","Email","Task","Note","Technical"];
 public IReadOnlyList<string> Opportunities=>Data?.Opportunities.Where(x=>x.Stage==TicketingCRM.Domain.Sales.Models.OpportunityStage.Won).Select(x=>$"{x.Title} · {x.Id}").ToArray()??[];
 public IReadOnlyList<string> Contracts=>Data?.Contracts.Where(x=>x.Status=="Active").Select(x=>$"{x.Reference} · {x.Id}").ToArray()??[];
 public Task LoadAsync()=>RunAsync(async()=>Data=await service.GetAsync(customerId));
 public Task RecordAsync()=>RunAsync(async()=>{await service.RecordActivityAsync(new(ActivityOperation,customerId,Kind,Subject,Notes,RecordChoice.Utc(FollowUp)));ActivityOperation=Guid.NewGuid();Subject="";Notes="";Data=await service.GetAsync(customerId);Message="Activity recorded.";});
 public Task ReissueAsync(Guid id)=>RunAsync(async()=>{await service.ReissueInvitationAsync(new(Guid.NewGuid(),id));Data=await service.GetAsync(customerId);Message="A fresh password-setup invitation is queued. Previous setup links are invalidated.";});
 public Task FinalizeAsync()=>RunAsync(async()=>{await service.FinalizeSaleAsync(new(FinalizeOperation,customerId,RecordChoice.Id(Opportunity),RecordChoice.Id(Contract)));FinalizeOperation=Guid.NewGuid();Data=await service.GetAsync(customerId);Message="Customer account prepared. The password-setup email is queued for delivery.";});
}
