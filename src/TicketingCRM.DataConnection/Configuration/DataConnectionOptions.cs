namespace TicketingCRM.DataConnection.Configuration;

/// <summary>Runtime configuration from the server vault. Never commit connection strings.</summary>
public sealed class DataConnectionOptions
{
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.PostgreSql;
}
