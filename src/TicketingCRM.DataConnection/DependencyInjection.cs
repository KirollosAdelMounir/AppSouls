using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;

namespace TicketingCRM.DataConnection;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConnection(this IServiceCollection services)
    {
        services.AddOptions<DataConnectionOptions>();
        return services;
    }

    public static IServiceCollection AddDataConnection(this IServiceCollection services, DatabaseProvider provider, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddScoped<TicketingCRM.Domain.Licensing.Interfaces.IMachineLicenseLookup, TicketingCRM.DataConnection.Repository.MachineLicenseLookup>();
        services.AddScoped<IDbContextFactory<AppDbContext>>(sp => new TenantDbContextFactory(provider, connectionString, sp.GetService<TicketingCRM.Domain.Tenancy.Interfaces.ITenantContext>()));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        return services;
    }

    public static void ConfigureProvider(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
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
