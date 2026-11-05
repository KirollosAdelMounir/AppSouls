using Microsoft.AspNetCore.Http;
using TicketingCRM.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketingCRM.Core;
using TicketingCRM.Core.Security;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Administration.Interfaces;
using TicketingCRM.Domain.Configuration.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Licensing.Interfaces;
using TicketingCRM.Domain.Support.Interfaces;
using TicketingCRM.Domain.Workspace.Interfaces;
using TicketingCRM.Infrastructure.Administration;
using TicketingCRM.Infrastructure.Configuration;
using TicketingCRM.Infrastructure.Email;
using TicketingCRM.Infrastructure.Licensing;
using TicketingCRM.Infrastructure.Support;
using TicketingCRM.Infrastructure.Workspace;

namespace TicketingCRM.Infrastructure;

public static class AppSoulsServiceCollectionExtensions
{
    public static IServiceCollection AddAppSoulsInfrastructure(this IServiceCollection services,
        ServerConfiguration? configuration, FileConfigurationVault vault, VaultBootstrapOptions bootstrap,
        PendingAdministratorFile pendingAdministrator, string? setupPresetPath, DatabaseMaintenanceCoordinator? maintenance = null)
    {
        maintenance ??= new DatabaseMaintenanceCoordinator();
        services.AddSingleton(maintenance);
        services.AddCore().AddInfrastructure();
        services.AddScoped<IServerSetupService>(sp => new ServerSetupService(configuration, vault, bootstrap, pendingAdministrator, setupPresetPath, sp.GetRequiredService<ILogger<ServerSetupService>>()));
        services.AddScoped<IInitialAdministratorService>(_ => new InitialAdministratorService(configuration, new InitialSetupAuthorization(bootstrap)));

        if (configuration is null) return services;
        PrivateFileSystem.PrepareDirectory(Path.Combine(configuration.DataProtectionKeyDirectory, ".check"));
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddSalesPipeline().AddAccountSettings().AddCustomerDelivery(bootstrap);
        services.AddScoped<ISignInDirectory, SignInDirectory>();
        services.AddSingleton(configuration);
        services.AddSingleton(bootstrap);
        services.AddSingleton(vault);
        AddProtectedPersistence(services, configuration, bootstrap, maintenance.MaintenanceMode);
        services.AddScoped<IWorkspaceSessionFactory, WorkspaceSessionFactory>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<ISupportReportService, SupportReportService>();
        services.AddScoped<ISupportInboxService, SupportReportService>();
        services.AddScoped<ILicensingService, LicensingService>();
        services.AddScoped<IServerSettingsService, ServerSettingsService>();
        services.AddSingleton<EmailOutboxProcessor>();
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        if (maintenance.MaintenanceMode) services.AddHostedService<DatabaseMaintenanceWorker>();
        else services.AddHostedService<EmailOutboxWorker>();
        return services;
    }

    private static void AddProtectedPersistence(IServiceCollection services, ServerConfiguration configuration, VaultBootstrapOptions bootstrap, bool maintenanceReadOnly)
    {
        services.AddDataProtection().SetApplicationName("AppSouls").PersistKeysToFileSystem(new DirectoryInfo(configuration.DataProtectionKeyDirectory));
        services.AddSingleton<IXmlEncryptor, VaultXmlKeyEncryptor>();
        services.AddSingleton<VaultXmlKeyDecryptor>();
        services.AddOptions<KeyManagementOptions>().Configure<IXmlEncryptor>((options, encryptor) => options.XmlEncryptor = encryptor);
        services.AddDataConnection((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider, configuration.DatabaseConnectionString, maintenanceReadOnly);
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = PasswordPolicy.MinimumLength;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();
        services.AddScoped<IUserStore<ApplicationUser>, ProtectedUserStore>();
        services.AddHttpContextAccessor();
        services.AddOptions<SecurityStampValidatorOptions>().Configure<IHttpContextAccessor>((options, accessor) =>
        {
            options.OnRefreshingPrincipal = context =>
            {
                var http = accessor.HttpContext;
                if (http is null) { context.NewPrincipal = null!; return Task.CompletedTask; }
                return WorkspacePrincipalRefresher.RefreshAsync(context, http.RequestServices.GetRequiredService<ISignInDirectory>(), http.RequestAborted);
            };
        });

    }
}
