using TicketingCRM.Infrastructure.Configuration;
using TicketingCRM;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Security;

var paths = BootstrapPaths.FromEnvironment();
var bootstrap = paths.Vault;
var setupPresetPath = paths.PresetPath;
var vault = new FileConfigurationVault(bootstrap);
if (await BootstrapCommands.TryRunAsync(args, vault, bootstrap)) return;
bool restartForActivation;
var maintenance = new DatabaseMaintenanceCoordinator();
do
{
    var configuration = File.Exists(bootstrap.VaultPath) ? await vault.LoadAsync() : null;
    var pendingAdministrator = new PendingAdministratorFile(bootstrap);
    var builder = WebApplication.CreateBuilder(args);
    paths.ValidateOutsideContentRoot(builder.Environment.ContentRootPath, configuration);
    var activationSignal = new ServerActivationSignal();
    builder.Configuration.Sources.Clear(); // All application configuration comes from the validated vault.
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
    builder.Configuration.AddInMemoryCollection(); // Host bootstrap settings only; no application secrets.
    builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("APPSOULS_LISTEN_URLS") ?? (builder.Environment.IsDevelopment() ? "http://localhost:5176" : "http://127.0.0.1:5000"));

    builder.Services.AddAppSoulsApplication(builder.Environment, configuration, vault, bootstrap, pendingAdministrator, setupPresetPath, activationSignal, maintenance);
    await using var app = builder.Build();
    app.UseAppSouls(configuration is not null, maintenance);
    await app.RunAsync();
    restartForActivation = activationSignal.Requested;
    await app.DisposeAsync();
    maintenance.ResetAfterHostStopped();
} while (restartForActivation);
public partial class Program;
