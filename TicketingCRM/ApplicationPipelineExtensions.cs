using TicketingCRM.Infrastructure.Configuration;
using Scalar.AspNetCore;
using TicketingCRM.Components;

namespace TicketingCRM;

internal static class ApplicationPipelineExtensions
{
    internal static async Task ApplyExistingDatabaseUpdatesAsync(this WebApplication app,
        TicketingCRM.Core.SystemConfiguration.Models.ServerConfiguration? configuration, bool maintenanceMode)
    {
        if (configuration is null || maintenanceMode) return;
        app.Logger.LogInformation("Checking the existing installation for database updates before starting application services");
        try
        {
            await TicketingCRM.DataConnection.DatabaseLifecycle.UpgradeExistingAsync(
                (TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider,
                configuration.DatabaseConnectionString);
            app.Logger.LogInformation("The existing database is current; application services can start");
        }
        catch (Exception exception)
        {
            app.Logger.LogError("Automatic database upgrade failed ({FailureType}); request handling and outbox processing were not started", exception.GetType().Name);
            // Provider exception messages may contain connection details. Keep bootstrap output nonsecret.
            throw new InvalidOperationException("The existing database could not be upgraded or verified. App Souls was not started. Check database availability, migration permissions and compatible migration history. First-time activation requires guided setup.");
        }
    }

    private static bool IsMaintenanceAsset(PathString path)
    {
        if (path.StartsWithSegments("/api")) return false;
        var value = path.Value ?? "";
        return path.StartsWithSegments("/Components") && value.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            value.Count(c => c == '/') == 1 && new[] { ".js", ".css", ".png", ".ico" }.Any(ext => value.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
    internal static WebApplication UseAppSouls(this WebApplication app, bool activated, DatabaseMaintenanceCoordinator maintenance)
    {
        // Only the installer-configured loopback proxy can supply scheme/client headers.
        // IIS supplies its own integration; never clear trusted proxy restrictions globally.
        if (Environment.GetEnvironmentVariable("APPSOULS_BEHIND_NGINX") == "1") app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
            var path = context.Request.Path;
            if (!activated && !path.StartsWithSegments("/setup") && !path.StartsWithSegments("/_framework") &&
                !path.StartsWithSegments("/_blazor") && !IsMaintenanceAsset(path))
            {
                context.Response.Redirect("/setup");
                return;
            }
            await next();
        });
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true) context.Response.Headers.CacheControl = "no-store, private";
            await next();
        });
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (maintenance.MaintenanceMode && !path.StartsWithSegments("/database-maintenance") &&
                !path.StartsWithSegments("/api/database-maintenance") && !path.StartsWithSegments("/_framework") &&
                !path.StartsWithSegments("/_blazor") && !(path.Value?.Contains('.') ?? false))
            {
                if (HttpMethods.IsGet(context.Request.Method) && !path.StartsWithSegments("/api")) context.Response.Redirect("/database-maintenance");
                else { context.Response.StatusCode = 503; await context.Response.WriteAsync("Database maintenance is active."); }
                return;
            }
            await next();
        });
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseAntiforgery();
        app.MapControllers();
        if (activated)
        {
            var specification = app.MapOpenApi();
            var reference = app.MapScalarApiReference("/scalar", options => options.WithTitle("App Souls API").DisableAgent());
            if (!app.Environment.IsDevelopment())
            {
                specification.RequireAuthorization("PlatformAdministrator");
                reference.RequireAuthorization("PlatformAdministrator");
            }
        }
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }
}
