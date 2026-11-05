using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.Domain.Tenancy.Interfaces;

namespace TicketingCRM.DataConnection.DbContexts;

internal sealed class TenantDbContextFactory(DatabaseProvider provider, string connectionString, ITenantContext? tenant, bool maintenanceReadOnly = false) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (maintenanceReadOnly) options.AddInterceptors(new MaintenanceReadOnlyInterceptor());
        DependencyInjection.ConfigureProvider(options, provider, connectionString);
        return provider == DatabaseProvider.PostgreSql
            ? new PostgreSqlAppDbContext(options.Options, tenant)
            : new SqlServerAppDbContext(options.Options, tenant);
    }
}
