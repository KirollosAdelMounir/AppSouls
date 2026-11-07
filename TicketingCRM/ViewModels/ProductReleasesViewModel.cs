using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Delivery.Interfaces;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;

public sealed class ProductReleasesViewModel(IProductReleaseService releases, IWorkspaceService workspace) : OperationViewModel
{
    public string View {get;set;} = "published";
    public int Page {get;private set;} = 1;
    public Task ChangePageAsync(int page)=>RunAsync(async()=>{Page=Math.Max(1,page);Data=await releases.ListAsync(View,Page);});
    public ProductReleaseSnapshot? Data { get; private set; }
    public WorkspaceSnapshot? Workspace { get; private set; }
    public string StoreUrl {get;set;} = "";
    public Task SaveStoreAsync() => RunAsync(async () => { await releases.CreateStoreReleaseAsync(new(Guid.NewGuid(),RecordChoice.Id(Product),Version,Platform,StoreUrl,Notes));Data=await releases.ListAsync(View,Page);Message="Store release published. Customers open the official store to install the application."; });
    public string Product { get; set; } = ""; public string Version { get; set; } = ""; public string Platform { get; set; } = "Windows"; public string Notes { get; set; } = "";
    public IReadOnlyList<string> Products => Workspace?.Products.Select(x => $"{x.Name} · {x.Id}").ToArray() ?? [];
    public static IReadOnlyList<string> Platforms { get; } = ["Windows", "Linux", "MacOS", "Android", "IOS", "FreeBSD"];
    public Task LoadAsync() => RunAsync(async () => { Data = await releases.ListAsync(View,Page); Workspace = await workspace.GetAsync(); });
    public Task UploadAsync(string filename, Stream content, CancellationToken cancellationToken = default) => RunAsync(async () => { await releases.UploadAsync(new(Guid.NewGuid(), RecordChoice.Id(Product), Version, Platform, filename, Notes), content, cancellationToken); Data = await releases.ListAsync(View,Page); Message = "Installation uploaded into quarantine. Scan and publish it before customers can download it."; });
    public void UploadInterrupted() { HasError = true; Message = "Upload interrupted. Check the release list before retrying."; }
    public Task PublishAsync(Guid id) => RunAsync(async () => { await releases.PublishAsync(id); Data = await releases.ListAsync(View,Page); Message = "Release scanned, signed and published."; });
    public Task RevokeAsync(Guid id) => RunAsync(async () => { await releases.RevokeAsync(id); Data = await releases.ListAsync(View,Page); Message = "Release revoked. Downloads are blocked."; });
}
