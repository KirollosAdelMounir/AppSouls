using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.Domain.Tenancy.Interfaces;

namespace TicketingCRM.DataConnection.DbContexts;

public sealed class SqlServerAppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : AppDbContext(options, tenantContext);

public sealed class SqlServerDesignFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("APPSOULS_DB_CONNECTION") ?? "Server=localhost;Database=appsouls_design;Integrated Security=true;TrustServerCertificate=false";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureProvider(options, DatabaseProvider.SqlServer, connection);
        return new SqlServerAppDbContext(options.Options);
    }
}
