using TicketingCRM.Domain.Commerce.DTOs;
using TicketingCRM.Domain.Commerce.Interfaces;
using TicketingCRM.Domain.Customers.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class CommercialLineDraft
{
 public Guid Key {get;}=Guid.NewGuid();
 public Guid? ProductId{get;set;}
 public string Description{get;set;}="";
 public decimal Quantity{get;set;}=1;
 public decimal UnitPrice{get;set;}
 public decimal Discount{get;set;}
 public decimal Tax{get;set;}
 public CommercialLine ToLine()=>new(ProductId,Description,Quantity,UnitPrice,Discount,Tax);
}
public sealed class CommerceViewModel(ICommerceService service,IWorkspaceService workspace,ICustomerLifecycleService customers,ICurrentActor actor):OperationViewModel
{
 public IReadOnlyList<OrderDto>? Orders{get;private set;}
 public IReadOnlyList<InvoiceDto>? Invoices{get;private set;}
 public IReadOnlyList<CatalogPriceDto>? Catalog{get;private set;}
 public WorkspaceSnapshot? Workspace{get;private set;}
 public ContractDto? Contract{get;private set;}
 public OrderDto? Order{get;private set;}
 public InvoiceDto? Invoice{get;private set;}
 public CatalogPriceDto? Pricing{get;private set;}
 public Guid? SavedId{get;private set;}
 public bool CanSell=>actor.CustomerId is null&&actor.Role is "Owner" or "Admin" or "Sales" or "Sales Manager";
 public bool CanInvoice=>actor.CustomerId is null&&actor.Role is "Owner" or "Admin" or "Sales Manager";
 public bool CanApprovePricing=>actor.CustomerId is null&&actor.Role is "Owner" or "Admin";
 public int Page{get;private set;}=1;
 public string Customer{get;set;}="";
 public string Reference{get;set;}="";
 public string Currency{get;set;}="USD";
 public string Notes{get;set;}="";
 public string SupplierName{get;set;}="";
 public string SupplierTaxId{get;set;}="";
 public string SupplierAddress{get;set;}="";
 public string BuyerName{get;set;}="";
 public string BuyerTaxId{get;set;}="";
 public string BuyerAddress{get;set;}="";
 public string PaymentTerms{get;set;}="";
 public DateTime? IssueDate{get;set;}=DateTime.UtcNow;
 public DateTime? DueDate{get;set;}
 public decimal UnitPrice{get;set;}
 public string PricingDescription{get;set;}="";
 public bool Approve{get;set;}
 public string TargetLabel{get;private set;}="";
 private Guid? TargetOrder,TargetContract;
 private Guid TargetCustomer;
 private readonly Guid OperationId=Guid.NewGuid();
 public List<CommercialLineDraft> Lines{get;}=[new()];
 public static IReadOnlyList<string> Currencies{get;}=["USD","EUR","GBP","EGP","SAR","AED"];
 public IReadOnlyList<string> CustomerOptions=>Workspace?.Customers.Select((x,i)=>$"{i+1}. {x.Name} · {x.Email}").ToArray()??[];
 public Task LoadAsync(string area,string mode,Guid? id,Guid? orderId,Guid? contractId,int page=1)=>RunAsync(async()=>
 {
  Page=Math.Clamp(page,1,10000);
  if(area=="catalog"){Catalog=await service.CatalogAsync(Page);if(id is {} product){Pricing=Catalog.SingleOrDefault(x=>x.ProductId==product)??throw new ArgumentException("Reload the catalog page to select this product.");Currency=Pricing.Currency;UnitPrice=Pricing.UnitPrice??0;PricingDescription=Pricing.Description;}return;}
  if(area=="contracts"&&id is {} contract){Contract=await workspace.ContractAsync(contract);TargetCustomer=Contract.CustomerId;return;}
  if(mode=="list"){if(area=="orders")Orders=await service.OrdersAsync(page:Page);else Invoices=await service.InvoicesAsync(page:Page);return;}
  if(mode=="detail"&&id is {} record){if(area=="orders")Order=await service.OrderAsync(record);else Invoice=await service.InvoiceAsync(record);return;}
  if(mode!="new")return;
  if(!CanSell||area=="invoices"&&!CanInvoice)throw new UnauthorizedAccessException();
  Workspace=await workspace.GetAsync();
  if(area=="invoices")
  {
   TargetOrder=orderId;TargetContract=contractId;
   if(orderId is {} order){Order=await service.OrderAsync(order);TargetCustomer=Order.CustomerId;TargetContract=Order.ContractId;Currency=Order.Currency;TargetLabel="Order "+Order.Reference;Lines.Clear();Lines.AddRange(Order.Lines.Select(x=>new CommercialLineDraft{ProductId=x.ProductId,Description=x.Description,Quantity=x.Quantity,UnitPrice=x.UnitPrice,Discount=x.DiscountPercent,Tax=x.TaxPercent}));}
   else if(contractId is {} target){Contract=await workspace.ContractAsync(target);TargetCustomer=Contract.CustomerId;TargetLabel="Contract "+Contract.Reference;}
   else throw new ArgumentException("Open an order or contract and choose Create invoice.");
   var customer=await customers.GetAsync(TargetCustomer);BuyerName=customer.Customer.Name;
  }
 });
 public IReadOnlyList<string> ProductOptions => new[]{"Custom line"}.Concat(Workspace?.Products.Select((x,i)=>$"{i+1}. {x.Name}")??[]).ToArray();
 public string ProductLabel(CommercialLineDraft line) { var index=Workspace?.Products.ToList().FindIndex(x=>x.Id==line.ProductId)??-1;return index>=0?ProductOptions[index+1]:"Custom line"; }
 public void ChooseProduct(CommercialLineDraft line,string label) { var index=ProductOptions.ToList().IndexOf(label)-1;line.ProductId=index<0?null:Workspace!.Products[index].Id;if(index>=0&&string.IsNullOrWhiteSpace(line.Description))line.Description=Workspace!.Products[index].Name; }
 public Task SaveOrderAsync()=>RunAsync(async()=>
 {
  var index=CustomerOptions.ToList().IndexOf(Customer);if(index<0)throw new ArgumentException("Choose a customer.");
  SavedId=await service.CreateOrderAsync(new(OperationId,Workspace!.Customers[index].Id,null,Reference,Currency,Lines.Select(x=>x.ToLine()).ToArray(),Notes));Message="Order recorded.";
 });
 public Task SaveInvoiceAsync()=>RunAsync(async()=>
 {
  var details=new InvoiceInput(Reference,SupplierName,SupplierTaxId,SupplierAddress,BuyerName,BuyerTaxId,BuyerAddress,Currency,RecordChoice.Utc(IssueDate)??throw new ArgumentException("Choose an invoice date."),RecordChoice.Utc(DueDate),PaymentTerms,Notes,Lines.Select(x=>x.ToLine()).ToArray());
  SavedId=await service.CreateInvoiceAsync(new(OperationId,TargetCustomer,TargetOrder,TargetContract,details));Message="Invoice issued. Download the PDF from its detail view.";
 });
 public Task SavePricingAsync()=>RunAsync(async()=>{if(Pricing is null)throw new ArgumentException("Choose a product.");await service.UpdatePricingAsync(new(Pricing.ProductId,Pricing.Version,Currency,UnitPrice,PricingDescription,Approve));Message=Approve?"Pricing approved.":"Pricing saved for leadership review.";Catalog=await service.CatalogAsync(Page);Pricing=Catalog.Single(x=>x.ProductId==Pricing.ProductId);});
 public void AddLine(){if(Lines.Count<100)Lines.Add(new());}
 public void RemoveLine(CommercialLineDraft line){if(Lines.Count>1)Lines.Remove(line);}
}
