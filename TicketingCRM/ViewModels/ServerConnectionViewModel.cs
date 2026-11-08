using TicketingCRM.Domain.Administration.DTOs;

namespace TicketingCRM.ViewModels;

/// <summary>Editable setup fields only. Connection validation and secret preservation remain in the setup service.</summary>
public sealed class ServerConnectionViewModel
{
    private readonly ServerSetupState state;

    public ServerConnectionViewModel(ServerSetupState state)
    {
        this.state = state;
        Provider = state.DatabaseProvider;
        if (state.Database is { } saved)
        {
            Server = saved.Server;
            Port = saved.Port;
            Catalog = saved.Catalog;
            UserName = saved.UserName;
            UseTls = saved.UseTls;
            IntegratedSecurity = saved.IntegratedSecurity;
            TrustServerCertificate = saved.TrustServerCertificate;
        }
        else Port = DefaultPort(Provider);
    }

    public static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    public string Provider { get; private set; }
    private string server = "";
    public string Server
    {
        get => server;
        set
        {
            // Discovery and local protocols do not use the form's default TCP port.
            if (IsSqlServer && !server.Contains('\\') && value.Contains('\\')) Port = null;
            server = value;
        }
    }
    public string Catalog { get; set; } = "";
    public string UserName { get; set; } = "";
    public int? Port { get; set; }
    public bool IsSqlServer => Provider == "SqlServer";
    public bool IntegratedSecurity { get; set; }
    public bool TrustServerCertificate { get; set; }
    public string Authentication => IntegratedSecurity ? "Service identity (integrated)" : "SQL Server credentials";
    public static readonly string[] AuthenticationOptions = ["SQL Server credentials", "Service identity (integrated)"];
    public void ChangeAuthentication(string value) => IntegratedSecurity = value == AuthenticationOptions[1];
    public bool UseTls { get; set; } = true;
    public bool CanKeepPassword => state.Database is { PasswordConfigured: true } saved &&
        Provider == state.DatabaseProvider && Server.Trim().Equals(saved.Server, StringComparison.OrdinalIgnoreCase) &&
        Port == saved.Port && Catalog.Trim() == saved.Catalog && UserName.Trim() == saved.UserName && UseTls == saved.UseTls &&
        IntegratedSecurity == saved.IntegratedSecurity && TrustServerCertificate == saved.TrustServerCertificate;

    public void ChangeProvider(string value) { Provider = value; Port = DefaultPort(value); IntegratedSecurity = false; TrustServerCertificate = false; }
    private static int DefaultPort(string provider) => provider == "SqlServer" ? 1433 : 5432;
}
