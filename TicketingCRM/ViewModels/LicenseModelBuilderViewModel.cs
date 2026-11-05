using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Interfaces;
using TicketingCRM.Domain.Licensing.Models;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class LicenseModelBuilderViewModel(ILicensingService licenses,IWorkspaceService workspace):OperationViewModel
{
 public LicensingSnapshot? Snapshot{get;private set;}
 public WorkspaceSnapshot? Workspace{get;private set;}
    public string Product { get; set; }="";
    public string ModelName { get; set; }="";
    public string Extension { get; set; }=".license";
    public List<LicenseFieldDefinition> Fields { get; }=[];
    public string FieldId { get; set; }="";
    public string FieldLabel { get; set; }="";
    public string FieldType { get; set; }="Text";
    public bool Required { get; set; }=true;
    public bool Nullable { get; set; }

 public string Source{get;private set;}="Manual";
 public void ChooseSource(string value)
 {
  Source=value;var source=Enum.Parse<LicenseValueSource>(value);
  if(source==LicenseValueSource.Manual)return;
  FieldType=source switch{LicenseValueSource.CustomerId or LicenseValueSource.ProductId or LicenseValueSource.InstallationId=>"Identifier",LicenseValueSource.InstallationLimit=>"Integer",LicenseValueSource.StartsAtUtc or LicenseValueSource.ExpiresAtUtc=>"UtcDateTime",_=>"Text"};
  Nullable=source==LicenseValueSource.ExpiresAtUtc;
 }
 public static IReadOnlyList<string> Sources{get;}=Enum.GetNames<LicenseValueSource>();
 public static IReadOnlyList<string> FieldTypes{get;}=["Text","Boolean","Integer","Decimal","UtcDateTime","Identifier"];
 public IReadOnlyList<string> Products=>Workspace?.Products.Select(x=>$"{x.Name} · {x.Id}").ToArray()??[];
 public Task LoadAsync()=>RunAsync(async()=>{Snapshot=await licenses.GetAsync();Workspace=await workspace.GetAsync();});
    public void AddField()
    {
        if(string.IsNullOrWhiteSpace(FieldId)||string.IsNullOrWhiteSpace(FieldLabel)||Fields.Count>=128){Message="Enter an attribute ID and label; the limit is 128 fields.";HasError=true;return;}
        Fields.Add(new(FieldId.Trim(),FieldLabel.Trim(),Enum.Parse<LicenseFieldType>(FieldType),Required,Nullable,Enum.Parse<LicenseValueSource>(Source)));FieldId="";FieldLabel="";Message=null;
    }
    public void RemoveField(LicenseFieldDefinition field)=>Fields.Remove(field);
    public Task PublishAsync()=>RunAsync(async()=>{await licenses.PublishSchemaAsync(new(RecordChoice.Id(Product),ModelName,Extension,Fields.ToArray()));Fields.Clear();Snapshot=await licenses.GetAsync();Message="The immutable schema revision was published.";});

}
