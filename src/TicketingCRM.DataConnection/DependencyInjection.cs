using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;

namespace TicketingCRM.DataConnection;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConnection(this IServiceCollection services)
    {
        services.AddOptions<DataConnectionOptions>();
        services.TryAdd(ServiceDescriptor.Scoped(typeof(TicketingCRM.DataConnection.Repository.Interfaces.IRepository<,>), typeof(TicketingCRM.DataConnection.Repository.Implementations.Repository<,>)));
        services.TryAddScoped<TicketingCRM.DataConnection.Transactions.IUnitOfWork, TicketingCRM.DataConnection.Transactions.UnitOfWork>();
        return services;
    }

    public static IServiceCollection AddDataConnection(this IServiceCollection services, DatabaseProvider provider, string connectionString)
        => services.AddDataConnection(provider, connectionString, false);

    public static IServiceCollection AddDataConnection(this IServiceCollection services, DatabaseProvider provider, string connectionString, bool maintenanceReadOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDataConnection();
        services.TryAddScoped<TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSessionFactory, TicketingCRM.DataConnection.Repository.WorkspaceSessionFactory>();
        services.AddScoped<TicketingCRM.DataConnection.Repository.Interfaces.IPublicFormSessionFactory>(_ => new TicketingCRM.DataConnection.Repository.PublicFormSessionFactory(provider, connectionString, maintenanceReadOnly));
        services.AddScoped<TicketingCRM.Domain.Licensing.Interfaces.IMachineLicenseLookup>(_ => new TicketingCRM.DataConnection.Repository.MachineLicenseLookup(new TenantDbContextFactory(provider, connectionString, null, maintenanceReadOnly, administrative: true)));
        services.AddScoped<TicketingCRM.Domain.Identity.Interfaces.ISignInDirectory>(_ => new TicketingCRM.DataConnection.Repository.SignInDirectory(new TenantDbContextFactory(provider, connectionString, null, maintenanceReadOnly, administrative: true)));
        services.AddScoped<IDbContextFactory<AppDbContext>>(sp => new TenantDbContextFactory(provider, connectionString, sp.GetService<TicketingCRM.Domain.Tenancy.Interfaces.ITenantContext>(), maintenanceReadOnly));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        return services;
    }

    public static void ConfigureProvider(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString, bool administrative = false)
    {
        if (!administrative) options.AddInterceptors(RowSecurityConnectionInterceptor.Instance);
        switch (provider)
        {
            case DatabaseProvider.PostgreSql:
                options.UseNpgsql(connectionString, sql => sql.MigrationsHistoryTable("__PostgreSqlMigrationsHistory"));
                break;
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__SqlServerMigrationsHistory"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }
    }
}
