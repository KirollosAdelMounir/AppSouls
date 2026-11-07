using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Infrastructure.Administration;

public static class PreparedAdministratorSeeder
{
    public static Task MigrateAndSeedAsync(ServerConfiguration configuration, PreparedAdministrator prepared, CancellationToken cancellationToken = default)
        => TicketingCRM.DataConnection.Repository.PreparedAdministratorRepository.MigrateAndSeedAsync(
            (TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider,
            configuration.DatabaseConnectionString, prepared, cancellationToken);
}
