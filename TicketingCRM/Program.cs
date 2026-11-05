using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Components;
using TicketingCRM.Core;
using TicketingCRM.Core.Security;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Support.Interfaces;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Domain.Workspace.Interfaces;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using TicketingCRM.Infrastructure;
using TicketingCRM.Infrastructure.Email;
using TicketingCRM.Infrastructure.Support;
using TicketingCRM.Infrastructure.Workspace;
using TicketingCRM.Security;
using TicketingCRM.Domain.Administration.Interfaces;
using TicketingCRM.Infrastructure.Administration;

var dataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"appsouls");
if(Environment.GetEnvironmentVariable("APPSOULS_SETUP_PROFILE")=="guided")dataRoot=Path.Combine(dataRoot,"guided-setup");
var setupPresetPath=Environment.GetEnvironmentVariable("APPSOULS_SETUP_PRESET_PATH")??Path.Combine(dataRoot,"setup","server-preset.json");
var bootstrap=new VaultBootstrapOptions(Environment.GetEnvironmentVariable("APPSOULS_VAULT_PATH")??Path.Combine(dataRoot,"vault","server.vault"),Environment.GetEnvironmentVariable("APPSOULS_KEY_PATH")??Path.Combine(dataRoot,"keys","server.key"));
var vault=new FileConfigurationVault(bootstrap);
if(args.Length==2&&args[0] is "--vault-init" or "--vault-update")
{
    var jsonOptions=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};jsonOptions.Converters.Add(new JsonStringEnumConverter());
    if(!OperatingSystem.IsWindows()&&(File.GetUnixFileMode(args[1])&(UnixFileMode.GroupRead|UnixFileMode.OtherRead|UnixFileMode.GroupWrite|UnixFileMode.OtherWrite))!=0)throw new InvalidOperationException("Configuration input must be private.");
    var input=JsonSerializer.Deserialize<ServerConfiguration>(await File.ReadAllTextAsync(args[1]),jsonOptions)??throw new InvalidOperationException("Invalid configuration input.");
    if(args[0]=="--vault-init")await vault.InitializeAsync(input);else await vault.SaveAsync(input);Console.WriteLine("Configuration vault saved.");return;
}
if(args.Length==2&&args[0]=="--vault-export"){await vault.ExportRecoveryAsync(args[1]);Console.WriteLine("Encrypted recovery copy exported.");return;}
if(args.Length==2&&args[0]=="--vault-restore"){await vault.RestoreRecoveryAsync(args[1]);Console.WriteLine("Vault restored from authenticated recovery copy.");return;}
ServerConfiguration? configuration=File.Exists(bootstrap.VaultPath)?await vault.LoadAsync():null;
if(args.Length>0&&args[0]=="--migrate")
{
    if(configuration is null)throw new InvalidOperationException("Initialize the vault first.");
    var pending=new PendingAdministratorFile(bootstrap);
    if(pending.Exists)
    {
        await PreparedAdministratorSeeder.MigrateAndSeedAsync(configuration,await pending.LoadAsync());pending.Complete();
        Console.WriteLine("Database migrations applied and the prepared administrator activated.");
    }
    else{await using var db=Provisioning.Open(configuration);await db.Database.MigrateAsync();Console.WriteLine("Database migrations applied.");}
    return;
}
if(args.Length==2&&args[0]=="--provision-member")
{if(configuration is null)throw new InvalidOperationException("Initialize the vault first.");await Provisioning.CreateMemberAsync(configuration,args[1]);return;}
if(args.Length==2&&args[0]=="--grant-platform-admin")
{
 if(configuration is null)throw new InvalidOperationException("Initialize the vault first.");
 if(!OperatingSystem.IsWindows()&&(File.GetUnixFileMode(args[1])&(UnixFileMode.GroupRead|UnixFileMode.OtherRead))!=0)throw new InvalidOperationException("Administrative input must be private.");
 using var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));var email=input.RootElement.GetProperty("Email").GetString();
 await using var db=Provisioning.Open(configuration);var user=await db.Users.SingleAsync(x=>x.Email==email);user.IsPlatformAdministrator=true;await db.SaveChangesAsync();Console.WriteLine("Platform administration enabled for the specified account.");return;
}
var restartForActivation=false;
do
{
configuration=File.Exists(bootstrap.VaultPath)?await vault.LoadAsync():null;
var pendingAdministrator=new PendingAdministratorFile(bootstrap);
var builder=WebApplication.CreateBuilder(args);
foreach(var secretPath in new[]{bootstrap.VaultPath,bootstrap.KeyPath,setupPresetPath}.Where(x=>!string.IsNullOrEmpty(x)))
{
    var rootPath=Path.GetFullPath(builder.Environment.ContentRootPath);
    if(Path.GetFullPath(secretPath!)==rootPath||Path.GetFullPath(secretPath!).StartsWith(rootPath+Path.DirectorySeparatorChar,StringComparison.Ordinal))
        throw new InvalidOperationException("Setup and key material must be outside the application content root.");
}
var activationSignal=new ServerActivationSignal();
builder.Configuration.Sources.Clear(); // All application configuration comes from the validated vault.
builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=1_048_576);
builder.Configuration.AddInMemoryCollection(); // Host bootstrap settings only; no application secrets.
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("APPSOULS_LISTEN_URLS") ?? (builder.Environment.IsDevelopment()?"http://localhost:5176":"http://127.0.0.1:5000"));

builder.Services.AddCore().AddInfrastructure();
builder.Services.AddSingleton(activationSignal);
builder.Services.AddScoped<IServerSetupService>(sp=>new ServerSetupService(configuration,vault,bootstrap,pendingAdministrator,setupPresetPath,sp.GetRequiredService<ILogger<ServerSetupService>>()));
builder.Services.AddScoped<IInitialAdministratorService>(_=>new InitialAdministratorService(configuration,new InitialSetupAuthorization(bootstrap)));
builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<CurrentSession>();
builder.Services.AddScoped<ICurrentActor>(sp=>sp.GetRequiredService<CurrentSession>());builder.Services.AddScoped<ITenantContext>(sp=>sp.GetRequiredService<CurrentSession>());
builder.Services.AddCascadingAuthenticationState();builder.Services.AddAuthorization(options=>options.AddPolicy("PlatformAdministrator",policy=>policy.RequireAuthenticatedUser().AddRequirements(new PlatformAdministratorRequirement())));
builder.Services.AddControllersWithViews(options=>options.Filters.Add<TicketingCRM.Middlewares.ApiExceptionFilter>()).AddJsonOptions(options=>options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi("v1",options=>options.AddDocumentTransformer((document,context,cancellationToken)=>
{
    document.Info.Title="App Souls API";document.Info.Version="v1";
    document.Info.Description="CRM, support, licensing and server configuration by Tetrarchia. Sign in through /account/login for cookie-authenticated operations. Mutations require the RequestVerificationToken header and antiforgery cookie from a rendered form. Installation renewal uses its own Bearer credential. Stored secrets are never returned.";
    document.Components??=new();document.Components.SecuritySchemes??=new Dictionary<string,IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["SessionCookie"]=new OpenApiSecurityScheme{Type=SecuritySchemeType.ApiKey,In=ParameterLocation.Cookie,Name="AppSouls.Session",Description="Authenticate through the App Souls sign-in page."};
    document.Components.SecuritySchemes["Antiforgery"]=new OpenApiSecurityScheme{Type=SecuritySchemeType.ApiKey,In=ParameterLocation.Header,Name="RequestVerificationToken",Description="Required on cookie-authenticated mutation requests; obtain it from a rendered App Souls form."};
    document.Components.SecuritySchemes["InstallationCredential"]=new OpenApiSecurityScheme{Type=SecuritySchemeType.Http,Scheme="bearer",Description="Installation credential issued once during authorized enrollment; keep it in the consuming application's secret store."};
    foreach(var path in document.Paths)
        foreach(var operation in path.Value.Operations??[])
        {
            var security=new OpenApiSecurityRequirement();
            security[new OpenApiSecuritySchemeReference(path.Key.EndsWith("/latest",StringComparison.Ordinal)?"InstallationCredential":"SessionCookie",document)]=[];
            if(operation.Key!=HttpMethod.Get)security[new OpenApiSecuritySchemeReference("Antiforgery",document)]=[];
            operation.Value.Security=[security];
        }
    return Task.CompletedTask;
}));
builder.Services.AddAuthentication(options=>{options.DefaultScheme=IdentityConstants.ApplicationScheme;options.DefaultSignInScheme=IdentityConstants.ExternalScheme;}).AddCookie(TicketingCRM.Controllers.AccountController.PendingScheme,options=>{options.Cookie.Name="AppSouls.PendingSignIn";options.Cookie.Path="/account";options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Strict;options.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;options.ExpireTimeSpan=TimeSpan.FromMinutes(5);options.SlidingExpiration=false;}).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options=>{options.LoginPath="/account/login";options.AccessDeniedPath="/account/access-denied";
options.Events.OnRedirectToLogin=context=>{if(context.Request.Path.StartsWithSegments("/api")){context.Response.StatusCode=401;return Task.CompletedTask;}context.Response.Redirect(context.RedirectUri);return Task.CompletedTask;};
options.Events.OnRedirectToAccessDenied=context=>{if(context.Request.Path.StartsWithSegments("/api")){context.Response.StatusCode=403;return Task.CompletedTask;}context.Response.Redirect(context.RedirectUri);return Task.CompletedTask;};options.Cookie.Name="AppSouls.Session";options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Lax;options.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;options.ExpireTimeSpan=TimeSpan.FromHours(8);options.SlidingExpiration=false;});
builder.Services.ConfigureHttpJsonOptions(options=>options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRateLimiter(options=>{options.RejectionStatusCode=429;options.AddPolicy("login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddRateLimiter(options=>options.AddPolicy("licensing",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0})));
builder.Services.AddRazorComponents().AddInteractiveServerComponents(options=>options.DetailedErrors=false).AddHubOptions(options=>options.MaximumReceiveMessageSize=128*1024);
if(configuration is null){builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();builder.Services.Configure<HostOptions>(options=>options.ShutdownTimeout=TimeSpan.FromSeconds(3));}
if(configuration is not null)
{
    foreach(var path in new[]{bootstrap.VaultPath,bootstrap.KeyPath,configuration.DataProtectionKeyDirectory})
        if(string.IsNullOrWhiteSpace(path)||Path.GetFullPath(path).StartsWith(Path.GetFullPath(builder.Environment.ContentRootPath)+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("Secret and key paths must be outside the application content root.");
    Directory.CreateDirectory(configuration.DataProtectionKeyDirectory);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(configuration.DataProtectionKeyDirectory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    builder.Services.AddScoped<IAuthorizationHandler,PlatformAdministratorHandler>();
    builder.Services.AddScoped<IUserAdministrationService,UserAdministrationService>();
    builder.Services.AddSalesPipeline().AddAccountSettings().AddCustomerDelivery(bootstrap);
    builder.Services.AddScoped<ISignInDirectory,SignInDirectory>();
    builder.Services.AddSingleton(configuration);builder.Services.AddSingleton(bootstrap);builder.Services.AddSingleton(vault);
    builder.Services.AddDataProtection().SetApplicationName("AppSouls").PersistKeysToFileSystem(new DirectoryInfo(configuration.DataProtectionKeyDirectory));
    builder.Services.AddSingleton<IXmlEncryptor,VaultXmlKeyEncryptor>();builder.Services.AddSingleton<VaultXmlKeyDecryptor>();builder.Services.AddOptions<KeyManagementOptions>().Configure<IXmlEncryptor>((options,encryptor)=>options.XmlEncryptor=encryptor);
    builder.Services.AddDataConnection((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider,configuration.DatabaseConnectionString);
    builder.Services.AddIdentityCore<ApplicationUser>(options=>{options.User.RequireUniqueEmail=true;options.Password.RequiredLength=8;options.Lockout.MaxFailedAccessAttempts=5;options.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);options.SignIn.RequireConfirmedEmail=true;}).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();
    builder.Services.AddScoped<IUserStore<ApplicationUser>,ProtectedUserStore>();
    builder.Services.AddScoped<IWorkspaceSessionFactory,WorkspaceSessionFactory>();builder.Services.AddScoped<IWorkspaceService,WorkspaceService>();builder.Services.AddScoped<ISupportReportService,SupportReportService>();builder.Services.AddScoped<ISupportInboxService,SupportReportService>();
    builder.Services.AddScoped<TicketingCRM.Domain.Licensing.Interfaces.ILicensingService,TicketingCRM.Infrastructure.Licensing.LicensingService>();
    builder.Services.AddScoped<TicketingCRM.Domain.Configuration.Interfaces.IServerSettingsService,TicketingCRM.Infrastructure.Configuration.ServerSettingsService>();
    builder.Services.AddSingleton<EmailOutboxProcessor>();builder.Services.AddHostedService<EmailOutboxWorker>();
}
var app=builder.Build();
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Error",createScopeForErrors:true);app.UseHsts();app.UseHttpsRedirection();}
app.Use(async(context,next)=>{context.Response.Headers["X-Content-Type-Options"]="nosniff";context.Response.Headers["Referrer-Policy"]="same-origin";context.Response.Headers["Content-Security-Policy"]="frame-ancestors 'none'; base-uri 'self'; object-src 'none'";if(configuration is null&&!context.Request.Path.StartsWithSegments("/setup")&&!context.Request.Path.StartsWithSegments("/_framework")&&!context.Request.Path.StartsWithSegments("/_blazor")&&!context.Request.Path.Value!.Contains('.')){context.Response.Redirect("/setup");return;}await next();});
app.UseAuthentication();
app.Use(async(context,next)=>
{
    if(context.User.Identity?.IsAuthenticated==true)context.Response.Headers.CacheControl="no-store, private";
    await next();
});
app.UseAuthorization();app.UseRateLimiter();app.UseAntiforgery();
app.MapControllers();
if(configuration is not null)
{
    var specification=app.MapOpenApi();
    var reference=app.MapScalarApiReference("/scalar",options=>options.WithTitle("App Souls API").DisableAgent());
    if(!app.Environment.IsDevelopment()){specification.RequireAuthorization("PlatformAdministrator");reference.RequireAuthorization("PlatformAdministrator");}
}
app.MapStaticAssets();app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await app.RunAsync();
restartForActivation=activationSignal.Requested;
await app.DisposeAsync();
}while(restartForActivation);
public partial class Program;
