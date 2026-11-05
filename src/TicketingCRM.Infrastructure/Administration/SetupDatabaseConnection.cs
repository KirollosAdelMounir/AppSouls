using System.Net;
using Microsoft.Data.SqlClient;
using Npgsql;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Infrastructure.Administration;

// Provider builders quote values correctly; credentials never need to be concatenated or sent back to the page.
public static class SetupDatabaseConnection
{
    public static SetupDatabaseFields? Read(ServerConfiguration? configuration)
    {
        if(configuration is null)return null;
        try
        {
            if(configuration.DatabaseProvider==DatabaseProvider.PostgreSql)
            {
                var value=new NpgsqlConnectionStringBuilder(configuration.DatabaseConnectionString);
                return new(value.Host??"",value.Port,value.Database??"",value.Username??"",value.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull,!string.IsNullOrEmpty(value.Password));
            }
            var sql=new SqlConnectionStringBuilder(configuration.DatabaseConnectionString);
            if(sql.IntegratedSecurity)return null;
            var source=sql.DataSource;
            if(source.StartsWith("tcp:",StringComparison.OrdinalIgnoreCase))source=source[4..];
            var comma=source.LastIndexOf(',');var port=1433;
            if(comma>=0){if(!int.TryParse(source[(comma+1)..],out port))return null;source=source[..comma];}
            // The guided form uses explicit host/port and SQL authentication, not named-instance/integrated discovery.
            if(source.Contains('\\'))return null;
            return new(source.Trim('[',']'),port,sql.InitialCatalog,sql.UserID,sql.Encrypt!=SqlConnectionEncryptOption.Optional,!string.IsNullOrEmpty(sql.Password));
        }
        catch(ArgumentException){return null;}
    }
    public static string Build(DatabaseProvider provider,CompleteServerSetupRequest request,ServerConfiguration? preset=null)
    {
        if(!Enum.IsDefined(provider))throw new ArgumentException("Choose a supported database provider.");
        var host=(request.DatabaseServer??"").Trim();var catalog=(request.DatabaseCatalog??"").Trim();var username=(request.DatabaseUserName??"").Trim();var password=request.DatabasePassword??"";
        if(host.Length is <1 or >253 || Uri.CheckHostName(host)==UriHostNameType.Unknown || request.DatabasePort is <1 or >65535)
            throw new ArgumentException("Enter a valid database server and port.");
        if(catalog.Length is <1 or >128 || username.Length is <1 or >256 || password.Length>8192)
            throw new ArgumentException("Enter the database/catalog and username, with a valid password.");
        var loopback=host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||IPAddress.TryParse(host,out var ip)&&IPAddress.IsLoopback(ip);
        if(!request.DatabaseUseTls&&!loopback)throw new ArgumentException("Remote database servers require TLS.");
        var saved=preset?.DatabaseProvider==provider?Read(preset):null;
        if(string.IsNullOrEmpty(password) && saved is not null && saved.Server.Equals(host,StringComparison.OrdinalIgnoreCase) && saved.Port==request.DatabasePort &&
            saved.Catalog==catalog && saved.UserName==username && saved.UseTls==request.DatabaseUseTls)
            return preset!.DatabaseConnectionString;
        if(string.IsNullOrEmpty(password))throw new ArgumentException("Enter a password for the selected database connection.");
        if(provider==DatabaseProvider.PostgreSql)
            return new NpgsqlConnectionStringBuilder{Host=host,Port=request.DatabasePort,Database=catalog,Username=username,Password=password,
                SslMode=request.DatabaseUseTls?SslMode.VerifyFull:SslMode.Disable,Timeout=10}.ConnectionString;
        if(provider!=DatabaseProvider.SqlServer)throw new ArgumentException("Choose a supported database provider.");
        var server=IPAddress.TryParse(host,out var address)&&address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6?$"[{host}]":host;
        return new SqlConnectionStringBuilder{DataSource=$"tcp:{server},{request.DatabasePort}",InitialCatalog=catalog,UserID=username,Password=password,
            IntegratedSecurity=false,Encrypt=request.DatabaseUseTls?SqlConnectionEncryptOption.Mandatory:SqlConnectionEncryptOption.Optional,
            TrustServerCertificate=false,ConnectTimeout=10}.ConnectionString;
    }
}
