using Npgsql;
using Microsoft.Data.SqlClient;
using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Infrastructure.Administration;

public static class SetupFailures
{
    public static ServerSetupException Classify(Exception error)
    {
        if(error is ServerSetupException known)return known;
        // EF execution strategies wrap transient provider failures in InvalidOperationException.
        // Classify known inner failures without ever returning their raw messages.
        var cause=error;
        for(var depth=0;depth<8 && cause.InnerException is not null;depth++)
        {
            cause=cause.InnerException;
            if(cause is NpgsqlException or SqlException or System.Security.Authentication.AuthenticationException or System.Net.Sockets.SocketException or TimeoutException)
                return Classify(cause);
        }
        if(error is PostgresException pg)return pg.SqlState switch
        {
            "28P01" or "28000" => new("database-authentication","The database rejected the username or password."),
            "3D000" => new("database-missing","The database/catalog does not exist. Create it on the server, then retry."),
            "42501" => new("database-permissions","The database user lacks required permissions. Grant access and schema migration permissions."),
            _ => new("database-operation","The database rejected an operation. Check provider compatibility and migration permissions.")
        };
        if(error is SqlException sql)return sql.Number switch
        {
            -2 => new("timeout","SQL Server did not respond in time. Check its listener, port and firewall."),
            18456 => new("database-authentication","The database rejected the username or password."),
            4060 or 911 => new("database-missing","The database/catalog could not be opened. Check its name and the user's access."),
            229 or 262 or 2760 => new("database-permissions","The database user lacks required permissions. Grant access and schema migration permissions."),
            _ => new("database-connection","Could not connect to SQL Server. Check the server, port, credentials and TLS certificate.")
        };
        if(error is NpgsqlException)return new("database-connection","Could not connect to PostgreSQL. Check the server, port and TLS certificate.");
        if(error is MailKit.Security.AuthenticationException or MailKit.ServiceNotAuthenticatedException)return new("email-authentication","The email server rejected authentication. Check the username, password and provider's SMTP authentication settings.");
        if(error is MailKit.Security.SslHandshakeException or System.Security.Authentication.AuthenticationException)return new("tls","TLS validation failed. Check the server name, certificate and TLS port.");
        if(error is NotSupportedException)return new("email-tls","The SMTP server does not support required TLS. Check the SMTP host and TLS port (usually 587 or 465).");
        if(error is MailKit.CommandException or MailKit.ProtocolException)return new("email-protocol","The email server rejected the connection or authentication. Check SMTP settings and TLS support.");
        if(error is TimeoutException or OperationCanceledException)return new("timeout","The connection timed out. Check the server, port and firewall, then retry.");
        // Only application-owned fixed validation messages are allowed through; provider/OS messages can contain credentials.
        var validation=error.Message;
        string[] safe=["Enter a valid database server and port.","Enter the database/catalog and username, with a valid password.","Remote database servers require TLS.","Enter a password for the selected database connection.","Choose a supported database provider.","Choose a database provider.","Confirm database migration to activate the system.","Enter a valid database connection.","Enter a valid sender email.","Email configuration exceeds its limits.","The public base address must be an absolute HTTPS address without credentials.","Enabled email requires a host, sender, valid port and TLS.","This database already has accounts. Initial setup cannot replace or adopt them.","An initialized vault cannot switch databases during initial setup.","Initial setup is closed."];
        if(error is ArgumentException or InvalidOperationException && safe.Contains(validation))
            return new(validation.StartsWith("This database already")?"database-existing":validation.StartsWith("The public base")?"public-address":"configuration",validation);
        if(error is System.Net.Sockets.SocketException)return new("connection","Could not reach the server. Check the host, port and firewall.");
        if(error is IOException or UnauthorizedAccessException)return new("storage-or-network","A setup file or network connection could not be accessed. Check server connectivity and private vault/key directory permissions.");
        return new("activation","Activation failed. Your prepared administrator is retained. Check the server setup diagnostics before retrying.");
    }
}
