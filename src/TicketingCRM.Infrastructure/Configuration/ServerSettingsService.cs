using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;

namespace TicketingCRM.Infrastructure.Configuration;
public sealed class ServerSettingsService(FileConfigurationVault vault,ServerConfiguration active,IWorkspaceSessionFactory sessions,ICurrentActor actor,ILogger<ServerSettingsService> logger) : IServerSettingsService
{
 private static readonly SemaphoreSlim Changes=new(1,1);
 private async Task Authorize(CancellationToken ct)
 {
  await using var s=await sessions.OpenAsync(ct);
  if(actor.UserId==Guid.Empty||actor.TenantId==Guid.Empty||string.IsNullOrWhiteSpace(actor.SecurityStamp)
   ||!await s.Query<ApplicationUser>().AnyAsync(x=>x.Id==actor.UserId&&x.IsPlatformAdministrator&&x.SecurityStamp==actor.SecurityStamp,ct)
   ||!await s.Query<TenantMembership>().AnyAsync(x=>x.UserId==actor.UserId&&x.TenantId==actor.TenantId&&x.IsActive,ct)
   ||!await s.Query<Tenant>().AnyAsync(x=>x.IsActive,ct))throw new UnauthorizedAccessException("Platform administrator access is required.");
 }
 public async Task<ServerSettingsDto> GetAsync(CancellationToken cancellationToken=default)
 {
  await Authorize(cancellationToken);var saved=await vault.LoadAsync(cancellationToken);
  return new(saved.DatabaseProvider.ToString(),!string.IsNullOrEmpty(saved.DatabaseConnectionString),saved.PublicBaseUrl,saved.Smtp.Enabled,saved.Smtp.Host,saved.Smtp.Port,saved.Smtp.FromAddress,saved.Smtp.UserName,!string.IsNullOrEmpty(saved.Smtp.Password),saved.Integrations.Select(x=>new IntegrationSettingsDto(x.Key,x.Value.BaseUrl,x.Value.Credentials.Keys.ToList())).ToList());
 }
 public Task UpdateDatabaseAsync(UpdateDatabaseSettingsRequest request,CancellationToken cancellationToken=default)=>Change(async current=>
 {
  if(!Enum.TryParse<DatabaseProvider>(request.Provider,out var provider)||!Enum.IsDefined(provider))throw new ArgumentException("Choose a supported database provider.");
  var connection=string.IsNullOrWhiteSpace(request.ConnectionString)?current.DatabaseConnectionString:request.ConnectionString;
  if(connection.Length>8192)throw new ArgumentException("Connection string is too long.");
  if(provider!=current.DatabaseProvider&&string.IsNullOrWhiteSpace(request.ConnectionString))throw new ArgumentException("Supply a new connection string when changing provider.");
  var updated=Copy(current,provider,connection,current.Smtp,current.Integrations);await vault.SaveAsync(updated,cancellationToken);logger.LogInformation("Platform administrator {UserId} staged database settings; restart required",actor.UserId);
 },cancellationToken);
 public Task UpdateEmailAsync(UpdateEmailSettingsRequest request,CancellationToken cancellationToken=default)=>Change(async current=>
 {
  if(request.Host.Length>253||request.UserName.Length>254||request.Password?.Length>4096||request.Port is <1 or >65535)throw new ArgumentException("Check the email settings.");
  if(request.Enabled&&(!MailAddress.TryCreate(request.SenderAddress,out _)||string.IsNullOrWhiteSpace(request.Host)))throw new ArgumentException("An enabled email service needs a sender and host.");
  var smtp=new SmtpConfiguration{Enabled=request.Enabled,Host=request.Host.Trim(),Port=request.Port,FromAddress=request.SenderAddress.Trim(),UserName=request.UserName,Password=request.Password??current.Smtp.Password,RequireTls=true};
  var updated=Copy(current,current.DatabaseProvider,current.DatabaseConnectionString,smtp,current.Integrations);updated.Validate();await vault.SaveAsync(updated,cancellationToken);active.Smtp=smtp;logger.LogInformation("Platform administrator {UserId} changed email settings",actor.UserId);
 },cancellationToken);
 public Task UpdateIntegrationAsync(UpdateIntegrationSettingsRequest request,CancellationToken cancellationToken=default)=>Change(async current=>
 {
  if(!Regex.IsMatch(request.Name,@"^[A-Za-z][A-Za-z0-9_.-]{0,63}$"))throw new ArgumentException("Use a short integration identifier.");
  var integrations=new Dictionary<string,IntegrationConfiguration>(current.Integrations);
  if(request.Remove){integrations.Remove(request.Name);}
  else
  {
   if(!string.IsNullOrWhiteSpace(request.BaseUrl)&&(!Uri.TryCreate(request.BaseUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.Query)))throw new ArgumentException("Use an HTTPS base URL without credentials, query or fragment.");
   if(!string.IsNullOrEmpty(request.CredentialName)&&!Regex.IsMatch(request.CredentialName,@"^[A-Za-z][A-Za-z0-9_.-]{0,63}$"))throw new ArgumentException("Use a short credential identifier.");
   if(request.CredentialValue?.Length>8192)throw new ArgumentException("Credential is too long.");
   if(integrations.Count>=100&&!integrations.ContainsKey(request.Name))throw new ArgumentException("Integration limit reached.");
   var credentials=integrations.TryGetValue(request.Name,out var existing)?new Dictionary<string,string>(existing.Credentials):new();
   if(request.CredentialValue is not null){if(string.IsNullOrEmpty(request.CredentialName))throw new ArgumentException("Name the credential.");credentials[request.CredentialName]=request.CredentialValue;}
   integrations[request.Name]=new IntegrationConfiguration{BaseUrl=request.BaseUrl,Credentials=credentials};
  }
  await vault.SaveAsync(Copy(current,current.DatabaseProvider,current.DatabaseConnectionString,current.Smtp,integrations),cancellationToken);logger.LogInformation("Platform administrator {UserId} changed integration configuration",actor.UserId);
 },cancellationToken);
 private async Task Change(Func<ServerConfiguration,Task> action,CancellationToken ct){await Authorize(ct);await Changes.WaitAsync(ct);try{await action(await vault.LoadAsync(ct));}finally{Changes.Release();}}
 private static ServerConfiguration Copy(ServerConfiguration old,DatabaseProvider provider,string connection,SmtpConfiguration smtp,Dictionary<string,IntegrationConfiguration> integrations)=>new(){DatabaseProvider=provider,DatabaseConnectionString=connection,PublicBaseUrl=old.PublicBaseUrl,LicenseSigningPrivateKeyPem=old.LicenseSigningPrivateKeyPem,DataProtectionKeyDirectory=old.DataProtectionKeyDirectory,Smtp=smtp,Integrations=integrations};
}
