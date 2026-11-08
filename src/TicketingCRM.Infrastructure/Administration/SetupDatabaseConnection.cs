using System.Net;
using Microsoft.Data.SqlClient;
using Npgsql;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;

namespace TicketingCRM.Infrastructure.Administration;

// Provider builders quote values; credentials are never returned to the browser.
public static class SetupDatabaseConnection
{
    public static SetupDatabaseFields? Read(ServerConfiguration? configuration)
    {
        if (configuration is null) return null;
        try
        {
            if (configuration.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                var value = new NpgsqlConnectionStringBuilder(configuration.DatabaseConnectionString);
                return new(value.Host ?? "", value.Port, value.Database ?? "", value.Username ?? "",
                    value.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull, !string.IsNullOrEmpty(value.Password));
            }
            var sql = new SqlConnectionStringBuilder(configuration.DatabaseConnectionString);
            var source = sql.DataSource;
            int? port = null;
            if (!source.StartsWith("np:", StringComparison.OrdinalIgnoreCase) && !source.StartsWith("lpc:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = source.LastIndexOf(',');
                if (comma >= 0)
                {
                    if (!int.TryParse(source[(comma + 1)..], out var parsed)) return null;
                    port = parsed;
                    source = source[..comma];
                    if (source.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) source = source[4..];
                    if (source.StartsWith('[') && source.IndexOf(']') is var closing && closing > 0)
                        source = source[1..closing] + source[(closing + 1)..];
                }
            }
            return new(source, port, sql.InitialCatalog, sql.IntegratedSecurity ? "" : sql.UserID,
                sql.Encrypt != SqlConnectionEncryptOption.Optional, !sql.IntegratedSecurity && !string.IsNullOrEmpty(sql.Password),
                sql.IntegratedSecurity, sql.TrustServerCertificate);
        }
        catch (ArgumentException) { return null; }
    }

    public static string Build(DatabaseProvider provider, CompleteServerSetupRequest request, ServerConfiguration? preset = null)
    {
        if (!Enum.IsDefined(provider)) throw new ArgumentException("Choose a supported database provider.");
        var host = (request.DatabaseServer ?? "").Trim();
        var catalog = (request.DatabaseCatalog ?? "").Trim();
        var username = (request.DatabaseUserName ?? "").Trim();
        var password = request.DatabasePassword ?? "";
        var integrated = provider == DatabaseProvider.SqlServer && request.DatabaseIntegratedSecurity;
        if (catalog.Length is < 1 or > 128 || (!integrated && username.Length is < 1 or > 256) || password.Length > 8192)
            throw new ArgumentException("Enter the database/catalog and authentication details.");

        string source;
        bool local;
        if (provider == DatabaseProvider.PostgreSql)
        {
            if (host.Length is < 1 or > 253 || Uri.CheckHostName(host) == UriHostNameType.Unknown || request.DatabasePort is null or < 1 or > 65535)
                throw new ArgumentException("Enter a valid database server and port.");
            if (request.DatabaseIntegratedSecurity || request.DatabaseTrustServerCertificate)
                throw new ArgumentException("The selected SQL Server options do not apply to PostgreSQL.");
            source = host;
            local = IsLocal(host);
        }
        else (source, local) = SqlSource(host, request.DatabasePort, integrated);

        if (host.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) && request.DatabaseUseTls)
            throw new ArgumentException("LocalDB does not provide TLS. Turn off Require TLS for this local development connection.");

        if (!request.DatabaseUseTls && !local) throw new ArgumentException("Remote database servers require TLS.");
        if (request.DatabaseTrustServerCertificate && !local)
            throw new ArgumentException("Remote SQL Server connections require certificate validation. Install a trusted server certificate.");
        if (request.DatabaseTrustServerCertificate && !request.DatabaseUseTls)
            throw new ArgumentException("Certificate trust override requires TLS.");

        var saved = preset?.DatabaseProvider == provider ? Read(preset) : null;
        if (!integrated && string.IsNullOrEmpty(password))
        {
            if (saved is not null && saved.Server.Equals(host, StringComparison.OrdinalIgnoreCase) && saved.Port == request.DatabasePort &&
                saved.Catalog == catalog && saved.UserName == username && saved.UseTls == request.DatabaseUseTls &&
                saved.IntegratedSecurity == integrated && saved.TrustServerCertificate == request.DatabaseTrustServerCertificate)
                return preset!.DatabaseConnectionString;
            throw new ArgumentException("Enter a password for the selected database connection.");
        }
        if (provider == DatabaseProvider.PostgreSql)
            return new NpgsqlConnectionStringBuilder { Host = source, Port = request.DatabasePort!.Value, Database = catalog,
                Username = username, Password = password, SslMode = request.DatabaseUseTls ? SslMode.VerifyFull : SslMode.Disable, Timeout = 10 }.ConnectionString;

        var sql = new SqlConnectionStringBuilder { DataSource = source, InitialCatalog = catalog, IntegratedSecurity = integrated,
            Encrypt = request.DatabaseUseTls ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = request.DatabaseTrustServerCertificate, ConnectTimeout = 10 };
        if (!integrated) { sql.UserID = username; sql.Password = password; }
        return sql.ConnectionString;
    }

    private static (string Source, bool Local) SqlSource(string endpoint, int? port, bool integrated)
    {
        if (endpoint.Length is < 1 or > 512 || endpoint.Any(char.IsControl) || endpoint.Contains(';') || port is < 1 or > 65535)
            throw new ArgumentException("Enter a valid SQL Server endpoint and optional port.");
        if (endpoint.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
        {
            if (!OperatingSystem.IsWindows()) throw new ArgumentException("LocalDB requires a Windows application host. Use a SQL Server TCP endpoint on Linux.");
            var instance = endpoint[10..];
            if (instance.StartsWith(".\\", StringComparison.Ordinal)) instance = instance[2..];
            if (!integrated || port is not null || !ValidInstance(instance))
                throw new ArgumentException("LocalDB requires integrated authentication, an instance name and a blank port.");
            return (endpoint, true);
        }
        if (endpoint.StartsWith("np:", StringComparison.OrdinalIgnoreCase))
        {
            if (!OperatingSystem.IsWindows()) throw new ArgumentException("SQL Server named pipes require a Windows application host. Use TCP on Linux.");
            if (!endpoint[3..].StartsWith("\\\\", StringComparison.Ordinal))
            {
                var instance = endpoint[3..].Split('\\');
                if (port is not null || instance.Length > 2 || !ValidHost(instance[0]) || instance.Length == 2 && !ValidInstance(instance[1]))
                    throw new ArgumentException("Use np:server\\instance or np:\\\\server\\pipe\\name with a blank port.");
                return (endpoint, IsLocal(instance[0]));
            }
            var parts = endpoint[3..].Split('\\');
            if (port is not null || parts.Length < 5 || parts[0] != "" || parts[1] != "" || !ValidHost(parts[2]) ||
                !parts[3].Equals("pipe", StringComparison.OrdinalIgnoreCase) || parts.Skip(4).Any(p => !ValidInstance(p)))
                throw new ArgumentException("Use np:\\\\server\\pipe\\name with a blank port.");
            return (endpoint, IsLocal(parts[2]));
        }
        var shared = endpoint.StartsWith("lpc:", StringComparison.OrdinalIgnoreCase);
        if (shared && !OperatingSystem.IsWindows()) throw new ArgumentException("SQL Server shared memory requires a Windows application host.");
        var tcp = endpoint.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase);
        var address = shared || tcp ? endpoint[4..] : endpoint;
        if (address.Contains(','))
        {
            var comma = address.LastIndexOf(',');
            if (port is not null || !int.TryParse(address[(comma + 1)..], out var embedded) || embedded is < 1 or > 65535)
                throw new ArgumentException("Specify the SQL Server port once, in Server or Port.");
            port = embedded;
            address = address[..comma];
        }
        var instanceParts = address.Split('\\');
        var host = instanceParts[0].Trim('[', ']');
        if (!ValidHost(host) || instanceParts.Length > 2 || (instanceParts.Length == 2 && !ValidInstance(instanceParts[1])))
            throw new ArgumentException("Use a hostname, IP address, server\\instance or (localdb)\\instance.");
        var local = IsLocal(host);
        if (shared && (!local || port is not null)) throw new ArgumentException("Shared memory requires a local server and a blank port.");
        if (port is not null)
        {
            var formatted = IPAddress.TryParse(host, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{host}]" : host;
            return ($"tcp:{formatted}{(instanceParts.Length == 2 ? "\\" + instanceParts[1] : "")},{port}", local);
        }
        return (endpoint, local);
    }

    private static bool ValidInstance(string value) => value.Length is >= 1 and <= 128 && value.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or '$' or '#');
    private static bool ValidHost(string value) => value.Length is >= 1 and <= 253 && (value == "." || value.Equals("(local)", StringComparison.OrdinalIgnoreCase) || Uri.CheckHostName(value) != UriHostNameType.Unknown);
    private static bool IsLocal(string host) => host is "." || host.Equals("(local)", StringComparison.OrdinalIgnoreCase) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
}
