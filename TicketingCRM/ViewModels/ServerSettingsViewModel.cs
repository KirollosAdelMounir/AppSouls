using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class ServerSettingsViewModel(IServerSettingsService Service)
{
 public ServerSettingsDto? Settings;public bool Loading=true,Busy,EmailEnabled;public string? Error,Message;
 public static readonly string[] Providers=["PostgreSql","SqlServer"];
 public string Provider="PostgreSql",Connection="",Host="",Sender="",UserName="",Password="",IntegrationName="",IntegrationUrl="",CredentialName="",CredentialValue="";public int Port=587;
 public async Task InitializeAsync(){try{await Load();}catch(UnauthorizedAccessException){}catch{Error="Server configuration is unavailable.";}finally{Loading=false;}}
 private async Task Load(){Settings=await Service.GetAsync();Provider=Settings.DatabaseProvider;Host=Settings.SmtpHost;Sender=Settings.SenderAddress;UserName=Settings.SmtpUserName;EmailEnabled=Settings.EmailEnabled;Port=Settings.SmtpPort;}
 public Task SaveDatabase()=>Save(async()=>{await Service.UpdateDatabaseAsync(new(Provider,string.IsNullOrEmpty(Connection)?null:Connection));Connection="";Message="Database settings saved. Restart the server after the required migration and backup steps.";});
 public Task SaveEmail()=>Save(async()=>{await Service.UpdateEmailAsync(new(EmailEnabled,Host,Port,Sender,UserName,string.IsNullOrEmpty(Password)?null:Password));Password="";Message="Email settings saved. Queued notifications will use the configured provider.";});
 public Task SaveIntegration()=>Save(async()=>{await Service.UpdateIntegrationAsync(new(IntegrationName,IntegrationUrl,CredentialName,string.IsNullOrEmpty(CredentialValue)?null:CredentialValue));CredentialValue="";Message="Integration configuration saved securely.";});
 private async Task Save(Func<Task> action){if(Busy)return;Busy=true;Error=null;Message=null;try{await action();await Load();}catch(ArgumentException e){Error=e.Message;}catch(UnauthorizedAccessException){Error="Platform administrator access is required.";}catch{Error="Settings could not be saved. The previous configuration has been retained.";}finally{Connection="";Password="";CredentialValue="";Busy=false;}}

}
