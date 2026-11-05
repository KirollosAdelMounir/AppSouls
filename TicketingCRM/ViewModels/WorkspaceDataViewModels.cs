using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;
public abstract class WorkspaceDataViewModel(IWorkspaceService service,ICurrentActor actor):OperationViewModel
{
 protected IWorkspaceService Service{get;}=service;
 public WorkspaceSnapshot? Data{get;private set;}
 public bool CanManage=>actor.CustomerId is null&&actor.Role is "Owner" or "Admin";
 protected Task RefreshAsync()=>LoadDataAsync();
 private async Task LoadDataAsync()=>Data=await Service.GetAsync();
 public Task LoadAsync()=>RunAsync(LoadDataAsync);
}
public sealed class CustomerDirectoryViewModel(IWorkspaceService service,ICurrentActor actor):WorkspaceDataViewModel(service,actor)
{
 public string Name{get;set;}="";public string Email{get;set;}="";public string CompanyNumber{get;set;}="";
 public Task CreateAsync()=>RunAsync(async()=>{await Service.CreateCustomerAsync(new(Name,Email,CompanyNumber));Name="";Email="";CompanyNumber="";await RefreshAsync();Message="Customer created.";});
}
public sealed class ProductCatalogViewModel(IWorkspaceService service,ICurrentActor actor):WorkspaceDataViewModel(service,actor)
{
 public string Name{get;set;}="";public string Code{get;set;}="";public string Extension{get;set;}=".license";
 public Task CreateAsync()=>RunAsync(async()=>{await Service.CreateProductAsync(new(Name,Code,Extension));Name="";Code="";await RefreshAsync();Message="Software added.";});
}
public sealed class ContractsViewModel(IWorkspaceService service,ICurrentActor actor):WorkspaceDataViewModel(service,actor)
{
 public string Reference{get;set;}="";public string Customer{get;set;}="";public string Product{get;set;}="";public string Contract{get;set;}="";
 public DateTime? Starts{get;set;}=DateTime.UtcNow;public DateTime? Ends{get;set;}public DateTime? Expires{get;set;}public int Capacity{get;set;}=1;
 public IReadOnlyList<string> Customers=>Data?.Customers.Select(x=>$"{x.Name} · {x.Id}").ToArray()??[];
 public IReadOnlyList<string> Products=>Data?.Products.Select(x=>$"{x.Name} · {x.Id}").ToArray()??[];
 public IReadOnlyList<string> Contracts=>Data?.Contracts.Where(x=>x.Status=="Active").Select(x=>$"{x.Reference} · {x.Id}").ToArray()??[];
 public Task CreateAsync()=>RunAsync(async()=>{await Service.CreateContractAsync(new(RecordChoice.Id(Customer),Reference,RecordChoice.Utc(Starts)??throw new ArgumentException("Choose a start date."),RecordChoice.Utc(Ends)));Reference="";await RefreshAsync();Message="Contract created.";});
 public Task ActivateAsync(Guid id)=>RunAsync(async()=>{await Service.ActivateContractAsync(id);await RefreshAsync();Message="Contract activated.";});
 public Task GrantAsync()=>RunAsync(async()=>{await Service.GrantEntitlementAsync(new(RecordChoice.Id(Contract),RecordChoice.Id(Product),Capacity,RecordChoice.Utc(Expires)));await RefreshAsync();Message="Purchased software granted.";});
}
