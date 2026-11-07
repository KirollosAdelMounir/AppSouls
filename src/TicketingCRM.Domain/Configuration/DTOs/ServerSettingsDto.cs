using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Domain.Configuration.DTOs;
public sealed record IntegrationSettingsDto(string Name,string BaseUrl,IReadOnlyList<string> ConfiguredCredentialNames);
public sealed record ServerSettingsDto(string DatabaseProvider,bool DatabaseConnectionConfigured,string PublicBaseUrl,bool EmailEnabled,string SmtpHost,int SmtpPort,string SenderAddress,string SmtpUserName,bool SmtpPasswordConfigured,IReadOnlyList<IntegrationSettingsDto> Integrations, SetupDatabaseFields? Database = null, string OrganizationName = "");
// Null secrets preserve the stored value; they are never returned by read operations.
public sealed record UpdateDatabaseSettingsRequest(string Provider,string? ConnectionString);
public sealed record UpdateEmailSettingsRequest(bool Enabled,string Host,int Port,string SenderAddress,string UserName,string? Password);
public sealed record UpdateIntegrationSettingsRequest(string Name,string BaseUrl,string CredentialName,string? CredentialValue,bool Remove=false);

public sealed record UpdatePublicAddressRequest(string PublicBaseUrl);
public sealed record UpdateOrganizationRequest(string Name);
