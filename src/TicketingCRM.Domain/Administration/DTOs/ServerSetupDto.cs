namespace TicketingCRM.Domain.Administration.DTOs;
public sealed record ServerSetupState(bool IsOpen, bool AdministratorPrepared, bool HasDatabasePreset, string DatabaseProvider, string PublicBaseUrl, SetupDatabaseFields? Database = null);
public sealed class PreparedAdministrator
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string OrganizationName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
public sealed class CompleteServerSetupRequest
{
    public string DatabaseProvider { get; set; } = "PostgreSql";
    // Retained for private CLI/smoke inputs. The guided UI sends only the structured fields below.
    public string? DatabaseConnectionString { get; set; } = "";
    public string DatabaseServer {get;set;}="";
    public int DatabasePort {get;set;}=5432;
    public string DatabaseCatalog {get;set;}="";
    public string DatabaseUserName {get;set;}="";
    public string? DatabasePassword {get;set;}="";
    public bool DatabaseUseTls {get;set;}=true;
    public string PublicBaseUrl { get; set; } = "";
    public bool EmailEnabled { get; set; }
    public string? EmailHost { get; set; } = "";
    public int EmailPort { get; set; } = 587;
    public string? SenderAddress { get; set; } = "";
    public string? EmailUserName { get; set; } = "";
    public string? EmailPassword { get; set; } = "";
    public bool ConfirmMigration { get; set; }
}

public sealed class PrepareAdministratorRequest
{
    public string OrganizationName {get;set;}="";
    public string DisplayName {get;set;}="";
    public string Email {get;set;}="";
    public string Password {get;set;}="";
}

public sealed record SetupDatabaseFields(string Server,int Port,string Catalog,string UserName,bool UseTls,bool PasswordConfigured);
