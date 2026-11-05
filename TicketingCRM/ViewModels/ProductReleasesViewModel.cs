using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Delivery.Interfaces;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;

public sealed class ProductReleasesViewModel(IProductReleaseService releases, IWorkspaceService workspace) : OperationViewModel
{
    public ProductReleaseSnapshot? Data { get; private set; }
    public WorkspaceSnapshot? Workspace { get; private set; }
    public string Product { get; set; } = ""; public string Version { get; set; } = ""; public string Platform { get; set; } = "Windows"; public string Notes { get; set; } = "";
    public IReadOnlyList<string> Products => Workspace?.Products.Select(x => $"{x.Name} · {x.Id}").ToArray() ?? [];
    public static IReadOnlyList<string> Platforms { get; } = ["Windows", "Linux", "FreeBSD", "Cross-platform"];
    public Task LoadAsync() => RunAsync(async () => { Data = await releases.GetAsync(); Workspace = await workspace.GetAsync(); });
    public Task UploadAsync(string filename, Stream content, CancellationToken cancellationToken = default) => RunAsync(async () => { await releases.UploadAsync(new(Guid.NewGuid(), RecordChoice.Id(Product), Version, Platform, filename, Notes), content, cancellationToken); Data = await releases.GetAsync(); Message = "Installation uploaded into quarantine. Scan and publish it before customers can download it."; });
    public void UploadInterrupted() { HasError = true; Message = "Upload interrupted. Check the release list before retrying."; }
    public Task PublishAsync(Guid id) => RunAsync(async () => { await releases.PublishAsync(id); Data = await releases.GetAsync(); Message = "Release scanned, signed and published."; });
    public Task RevokeAsync(Guid id) => RunAsync(async () => { await releases.RevokeAsync(id); Data = await releases.GetAsync(); Message = "Release revoked. Downloads are blocked."; });
}
