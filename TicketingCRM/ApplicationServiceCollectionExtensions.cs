using TicketingCRM.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure;
using TicketingCRM.Security;

namespace TicketingCRM;

internal static class ApplicationServiceCollectionExtensions
{
    internal static IServiceCollection AddAppSoulsApplication(this IServiceCollection services,
        IWebHostEnvironment environment, ServerConfiguration? configuration, FileConfigurationVault vault,
        VaultBootstrapOptions bootstrap, PendingAdministratorFile pendingAdministrator, string setupPresetPath,
        ServerActivationSignal activationSignal, DatabaseMaintenanceCoordinator maintenance)
    {
        services.AddAppSoulsInfrastructure(configuration, vault, bootstrap, pendingAdministrator, setupPresetPath, maintenance);
        services.AddSingleton(activationSignal);
        services.AddHttpContextAccessor();
        if (Environment.GetEnvironmentVariable("APPSOULS_BEHIND_NGINX") == "1")
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                    Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.RequireHeaderSymmetry = true;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.KnownProxies.Add(System.Net.IPAddress.Loopback);
                options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
            });
        }
        services.AddScoped<CurrentSession>();
        services.AddScoped<ICurrentActor>(sp => sp.GetRequiredService<CurrentSession>());
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<CurrentSession>());
        services.AddCascadingAuthenticationState();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("PlatformAdministrator", policy => policy.RequireAuthenticatedUser().AddRequirements(new PlatformAdministratorRequirement()));
            options.AddPolicy("StaffWorkspace", policy => policy.RequireAuthenticatedUser().RequireClaim("appsouls:role", "Owner", "Admin", "Technical", "Technical Developer", "Technical Support", "Sales", "Sales Manager").RequireAssertion(context => !context.User.HasClaim(c => c.Type == "appsouls:customer")));
            options.AddPolicy("SalesWorkspace", policy => policy.RequireAuthenticatedUser().RequireClaim("appsouls:role", "Owner", "Admin", "Sales", "Sales Manager").RequireAssertion(context => !context.User.HasClaim(c => c.Type == "appsouls:customer")));
            options.AddPolicy("DeveloperWorkspace", policy => policy.RequireAuthenticatedUser().RequireClaim("appsouls:role", "Owner", "Admin", "Technical", "Technical Developer").RequireAssertion(context => !context.User.HasClaim(c => c.Type == "appsouls:customer")));
            options.AddPolicy("AdministratorWorkspace", policy => policy.RequireAuthenticatedUser().RequireClaim("appsouls:role", "Owner", "Admin").RequireAssertion(context => !context.User.HasClaim(c => c.Type == "appsouls:customer")));
            options.AddPolicy("FormsManager", policy => policy.RequireAuthenticatedUser().RequireClaim("appsouls:role", "Owner", "Admin", "Sales", "Sales Manager").RequireAssertion(context => !context.User.HasClaim(c => c.Type == "appsouls:customer")));
        });
        services.AddControllersWithViews(options => options.Filters.Add<TicketingCRM.Middlewares.ApiExceptionFilter>()).AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        AddApiDocumentation(services);
        AddSessionAuthentication(services, environment);
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            AddFixedWindowPolicy(options, "login", 10);
            AddFixedWindowPolicy(options, "public-forms", 20);
            AddFixedWindowPolicy(options, "licensing", 60);
            AddFixedWindowPolicy(options, "maintenance-progress", 180);
        });
        services.AddRazorComponents().AddInteractiveServerComponents(options => options.DetailedErrors = false).AddHubOptions(options => options.MaximumReceiveMessageSize = 128 * 1024);

        if (configuration is null)
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(3));
        }
        else { services.AddScoped<IAuthorizationHandler, PlatformAdministratorHandler>(); }
        return services;
    }

    private static void AddApiDocumentation(IServiceCollection services)
    {
        services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "App Souls API"; document.Info.Version = "v1";
            document.Info.Description = "CRM, support, licensing and server configuration by Tetrarchia. Sign in through /account/login for cookie-authenticated operations. Mutations require the RequestVerificationToken header and antiforgery cookie from a rendered form. Installation renewal uses its own Bearer credential. Stored secrets are never returned.";
            document.Components ??= new(); document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["SessionCookie"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "AppSouls.Session", Description = "Authenticate through the App Souls sign-in page." };
            document.Components.SecuritySchemes["Antiforgery"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "RequestVerificationToken", Description = "Required on cookie-authenticated mutation requests; obtain it from a rendered App Souls form." };
            document.Components.SecuritySchemes["InstallationCredential"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", Description = "Installation credential issued once during authorized enrollment; keep it in the consuming application's secret store." };
            foreach (var path in document.Paths)
                foreach (var operation in path.Value.Operations ?? [])
                {
                    var security = new OpenApiSecurityRequirement();
                    if (!path.Key.StartsWith("/f/", StringComparison.Ordinal)) security[new OpenApiSecuritySchemeReference(path.Key.EndsWith("/latest", StringComparison.Ordinal) ? "InstallationCredential" : "SessionCookie", document)] = [];
                    if (operation.Key != HttpMethod.Get) security[new OpenApiSecuritySchemeReference("Antiforgery", document)] = [];
                    operation.Value.Security = [security];
                }
            return Task.CompletedTask;
        }));

    }

    private static void AddSessionAuthentication(IServiceCollection services, IWebHostEnvironment environment)
    {
        var securePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddCookie(TicketingCRM.Controllers.AccountController.PendingScheme, options =>
        {
            options.Cookie.Name = "AppSouls.PendingSignIn";
            options.Cookie.Path = "/account";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        }).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/account/access-denied";
            options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
            options.Cookie.Name = "AppSouls.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
        });
    }

    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = status;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    private static void AddFixedWindowPolicy(Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, string name, int permitLimit)
    {
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    }
}
