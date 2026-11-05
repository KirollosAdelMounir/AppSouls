using System.Security.Cryptography;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Interfaces;
using TicketingCRM.Domain.Licensing.Models;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;

public sealed class LicensingWorkspaceViewModel(ILicensingService licenses,IWorkspaceService workspace):OperationViewModel
{
    public LicensingSnapshot? Snapshot { get; private set; }
    public WorkspaceSnapshot? Workspace { get; private set; }
    public string Entitlement { get; set; }="";
    public string InstallationName { get; set; }="";
    public string PublicKey { get; set; }="";
    public string SelectedInstallation { get; set; }="";
    public string SelectedSchema { get; set; }="";
    public Dictionary<string,string> Values{get;}=[];
    public IReadOnlyList<LicenseFieldDefinition> SelectedFields=>Snapshot?.Schemas.FirstOrDefault(x=>x.Id==TryId(SelectedSchema))?.Fields??[];
    private static Guid TryId(string value)=>Guid.TryParse(value.Split(" · ").Last(),out var id)?id:Guid.Empty;
    public void ChooseSchema(string value){SelectedSchema=value;Values.Clear();}
    private string CustomValues()=>System.Text.Json.JsonSerializer.Serialize(SelectedFields.Where(x=>x.Source==LicenseValueSource.Manual&&Values.ContainsKey(x.AttributeId)).ToDictionary(x=>x.AttributeId,x=>x.Type==LicenseFieldType.Boolean?(object)bool.Parse(Values[x.AttributeId]):Values[x.AttributeId]));
    public InstallationEnrollment? Enrollment { get; private set; }
    private Guid EnrollmentOperation=Guid.NewGuid();
    private Guid IssueOperation=Guid.NewGuid();


    public IReadOnlyList<string> Entitlements => Workspace?.Entitlements.Select(x=>$"{Workspace.Products.FirstOrDefault(p=>p.Id==x.ProductId)?.Name??"Product"} · {x.Id}").ToArray()??[];
    public IReadOnlyList<string> Installations => Snapshot?.Installations.Where(x=>!x.IsRevoked).Select(x=>$"{x.Name} · {x.Id}").ToArray()??[];
    public IReadOnlyList<string> Schemas => Snapshot?.Schemas.Select(x=>$"{x.Name} v{x.Revision} · {x.Id}").ToArray()??[];
    private static Guid Id(string value)=>Guid.TryParse(value.Split(" · ").Last(),out var id)?id:throw new ArgumentException("Choose a record.");
    public Task LoadAsync()=>Run(async()=>{Snapshot=await licenses.GetAsync();Workspace=await workspace.GetAsync();});
    public Task EnrollAsync()=>Run(async()=>{Enrollment=await licenses.EnrollAsync(new(EnrollmentOperation,Id(Entitlement),InstallationName,PublicKey));EnrollmentOperation=Guid.NewGuid();Snapshot=await licenses.GetAsync();Message=Enrollment.AlreadyRegistered?"This installation is already registered. Its credential is not displayed again.":"Installation registered. Save its renewal credential securely now; it will not be displayed again.";});
    public Task IssueAsync()=>Run(async()=>{await licenses.IssueAsync(new(IssueOperation,Id(SelectedInstallation),Id(SelectedSchema),CustomValues()));IssueOperation=Guid.NewGuid();Snapshot=await licenses.GetAsync();Message="The signed and encrypted license revision is ready to download.";});
    private Task Run(Func<Task> action)=>RunAsync(action);
}
