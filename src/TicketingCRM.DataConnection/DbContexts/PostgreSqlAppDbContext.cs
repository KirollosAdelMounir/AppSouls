using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.Domain.Tenancy.Interfaces;

namespace TicketingCRM.DataConnection.DbContexts;

public sealed class PostgreSqlAppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : AppDbContext(options, tenantContext);

public sealed class PostgreSqlDesignFactory : IDesignTimeDbContextFactory<PostgreSqlAppDbContext>
{
    public PostgreSqlAppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("APPSOULS_DB_CONNECTION") ?? "Host=localhost;Database=appsouls_design";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureProvider(options, DatabaseProvider.PostgreSql, connection, administrative: true);
        return new PostgreSqlAppDbContext(options.Options);
    }
}
