using Microsoft.Data.SqlClient;
using Npgsql;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Infrastructure.Administration;
using Xunit;

namespace TicketingCRM.Infrastructure.Tests;

public sealed class SetupDatabaseConnectionTests
{
    private static CompleteServerSetupRequest Input() => new()
    {
        DatabaseServer="db.example.test", DatabasePort=5432, DatabaseCatalog="catalog;custom",
        DatabaseUserName="user;name", DatabasePassword=" pass;\"word='secret' ", DatabaseUseTls=true
    };

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void BuildsProviderConnectionWithoutChangingOrInjectingCredentialValues(DatabaseProvider provider)
    {
        var input=Input();var result=SetupDatabaseConnection.Build(provider,input);
        if(provider==DatabaseProvider.PostgreSql)
        {
            var parsed=new NpgsqlConnectionStringBuilder(result);
            Assert.Equal(input.DatabaseServer,parsed.Host);Assert.Equal(input.DatabasePort,parsed.Port);
            Assert.Equal(input.DatabaseCatalog,parsed.Database);Assert.Equal(input.DatabaseUserName,parsed.Username);
            Assert.Equal(input.DatabasePassword,parsed.Password);Assert.Equal(SslMode.VerifyFull,parsed.SslMode);
        }
        else
        {
            var parsed=new SqlConnectionStringBuilder(result);
            Assert.Equal("tcp:db.example.test,5432",parsed.DataSource);Assert.Equal(input.DatabaseCatalog,parsed.InitialCatalog);
            Assert.Equal(input.DatabaseUserName,parsed.UserID);Assert.Equal(input.DatabasePassword,parsed.Password);
            Assert.Equal(SqlConnectionEncryptOption.Mandatory,parsed.Encrypt);Assert.False(parsed.TrustServerCertificate);Assert.False(parsed.IntegratedSecurity);
        }
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void PreservesPreparedPasswordOnlyForUnchangedTargetAndReturnsNoSecretMetadata(DatabaseProvider provider)
    {
        var input=Input();var connection=SetupDatabaseConnection.Build(provider,input);
        var preset=new ServerConfiguration{DatabaseProvider=provider,DatabaseConnectionString=connection};
        var fields=SetupDatabaseConnection.Read(preset)!;
        Assert.Equal(input.DatabaseServer,fields.Server);Assert.Equal(input.DatabaseCatalog,fields.Catalog);
        Assert.Equal(input.DatabaseUserName,fields.UserName);Assert.True(fields.PasswordConfigured);Assert.True(fields.UseTls);
        Assert.DoesNotContain(input.DatabasePassword!,System.Text.Json.JsonSerializer.Serialize(fields));
        input.DatabasePassword="";Assert.Equal(connection,SetupDatabaseConnection.Build(provider,input,preset));
        input.DatabaseServer="other.example.test";
        Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(provider,input,preset));
        input.DatabaseServer=fields.Server;input.DatabaseCatalog="other";
        Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(provider,input,preset));
        input.DatabaseCatalog=fields.Catalog;input.DatabaseUserName="other";
        Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(provider,input,preset));
        input.DatabaseUserName=fields.UserName;input.DatabasePort++;
        Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(provider,input,preset));
        input.DatabasePort--;Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(provider==DatabaseProvider.PostgreSql?DatabaseProvider.SqlServer:DatabaseProvider.PostgreSql,input,preset));
    }

    [Fact]
    public void RejectsRemotePlaintextAndInvalidPortsButAllowsLocalDevelopment()
    {
        var input=Input();input.DatabaseUseTls=false;
        Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(DatabaseProvider.PostgreSql,input));
        input.DatabaseServer="127.0.0.1";
        Assert.Equal(SslMode.Disable,new NpgsqlConnectionStringBuilder(SetupDatabaseConnection.Build(DatabaseProvider.PostgreSql,input)).SslMode);
        input.DatabasePort=0;Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(DatabaseProvider.PostgreSql,input));
        input.DatabasePort=65536;Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build(DatabaseProvider.SqlServer,input));
        input.DatabasePort=5432;Assert.Throws<ArgumentException>(()=>SetupDatabaseConnection.Build((DatabaseProvider)99,input));
    }

    [Fact]
    public void SqlServerIpv6HostRoundTripsThroughSafeFields()
    {
        var input=Input();input.DatabaseServer="::1";input.DatabasePort=1433;
        var preset=new ServerConfiguration{DatabaseProvider=DatabaseProvider.SqlServer,DatabaseConnectionString=SetupDatabaseConnection.Build(DatabaseProvider.SqlServer,input)};
        Assert.Equal("::1",SetupDatabaseConnection.Read(preset)!.Server);
        Assert.Equal("tcp:[::1],1433",new SqlConnectionStringBuilder(preset.DatabaseConnectionString).DataSource);
    }

    [Theory]
    [InlineData("db.example.test", null, "db.example.test")]
    [InlineData("db.example.test\\SALES", null, "db.example.test\\SALES")]
    [InlineData("tcp:db.example.test\\SALES", null, "tcp:db.example.test\\SALES")]
    [InlineData("db.example.test\\SALES", 1444, "tcp:db.example.test\\SALES,1444")]
    [InlineData("tcp:db.example.test,1444", null, "tcp:db.example.test,1444")]
    [InlineData("[2001:db8::1]", 1444, "tcp:[2001:db8::1],1444")]
    [InlineData("[2001:db8::1]\\SALES", 1444, "tcp:[2001:db8::1]\\SALES,1444")]
    public void IntegratedConnectionsUseServiceIdentityWithoutCredentials(string server, int? port, string expected)
    {
        var input = Input(); input.DatabaseServer = server; input.DatabasePort = port;
        input.DatabaseIntegratedSecurity = true;
        var connection = SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input);
        var sql = new SqlConnectionStringBuilder(connection);
        TicketingCRM.DataConnection.DatabaseLifecycle.ValidateTransport(TicketingCRM.DataConnection.Configuration.DatabaseProvider.SqlServer, connection);
        Assert.Equal(expected, sql.DataSource); Assert.True(sql.IntegratedSecurity);
        Assert.Empty(sql.UserID); Assert.Empty(sql.Password);
        Assert.DoesNotContain(input.DatabasePassword!, connection);
        var fields = SetupDatabaseConnection.Read(new ServerConfiguration { DatabaseProvider = DatabaseProvider.SqlServer, DatabaseConnectionString = connection })!;
        Assert.True(fields.IntegratedSecurity); Assert.False(fields.PasswordConfigured); Assert.Empty(fields.UserName);
        var rebuilt = SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, new CompleteServerSetupRequest
        {
            DatabaseServer = fields.Server, DatabasePort = fields.Port, DatabaseCatalog = fields.Catalog,
            DatabaseIntegratedSecurity = fields.IntegratedSecurity, DatabaseUseTls = fields.UseTls
        });
        Assert.Equal(expected, new SqlConnectionStringBuilder(rebuilt).DataSource);
    }

    [Theory]
    [InlineData(@"(localdb)\MSSQLLocalDB")]
    [InlineData(@"(localdb)\.\SharedInstance")]
    public void LocalDbRequiresWindowsServiceIdentityWithoutPortOrTls(string endpoint)
    {
        var input = Input(); input.DatabaseServer = endpoint; input.DatabasePort = null;
        input.DatabaseIntegratedSecurity = true; input.DatabaseUseTls = false;
        if (!OperatingSystem.IsWindows())
        {
            Assert.Contains("Windows", Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input)).Message);
            return;
        }
        var result = new SqlConnectionStringBuilder(SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
        Assert.Equal(input.DatabaseServer, result.DataSource); Assert.True(result.IntegratedSecurity);
        Assert.Equal(SqlConnectionEncryptOption.Optional, result.Encrypt);
        input.DatabaseUseTls = true;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
        input.DatabaseUseTls = false; input.DatabasePort = 1433;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
        input.DatabasePort = null; input.DatabaseIntegratedSecurity = false;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
    }

    [Theory]
    [InlineData(@"np:\\localhost\pipe\sql\query")]
    [InlineData(@"np:localhost\SQLEXPRESS")]
    [InlineData(@"lpc:.\SQLEXPRESS")]
    public void WindowsLocalProtocolsArePreservedOrRejectedOnOtherHosts(string endpoint)
    {
        var input = Input(); input.DatabaseServer = endpoint; input.DatabasePort = null;
        input.DatabaseIntegratedSecurity = true; input.DatabaseUseTls = false;
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
            return;
        }
        var connection = SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input);
        Assert.Equal(endpoint, new SqlConnectionStringBuilder(connection).DataSource);
        TicketingCRM.DataConnection.DatabaseLifecycle.ValidateTransport(TicketingCRM.DataConnection.Configuration.DatabaseProvider.SqlServer, connection);
        Assert.Equal(endpoint, SetupDatabaseConnection.Read(new ServerConfiguration { DatabaseProvider = DatabaseProvider.SqlServer, DatabaseConnectionString = connection })!.Server);
        input.DatabasePort = 1433;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
    }

    [Theory]
    [InlineData("db.example.test;Integrated Security=true", null)]
    [InlineData("db.example.test\\", null)]
    [InlineData("db.example.test\\one\\two", null)]
    [InlineData("db.example.test,1433", 1444)]
    [InlineData("db.example.test,0", null)]
    [InlineData("db.example.test", 65536)]
    public void RejectsMalformedOrAmbiguousSqlEndpoints(string endpoint, int? port)
    {
        var input = Input(); input.DatabaseServer = endpoint; input.DatabasePort = port;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
    }

    [Fact]
    public void TrustOverrideIsExplicitAndCannotReusePasswordAfterAuthenticationOrTrustChanges()
    {
        var input = Input();
        input.DatabaseTrustServerCertificate = true;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
        input.DatabaseTrustServerCertificate = false; input.DatabaseServer = "localhost";
        var preset = new ServerConfiguration { DatabaseProvider = DatabaseProvider.SqlServer,
            DatabaseConnectionString = SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input) };
        input.DatabasePassword = ""; input.DatabaseTrustServerCertificate = true;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input, preset));
        input.DatabasePassword = "replacement";
        var result = SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input);
        Assert.True(new SqlConnectionStringBuilder(result).TrustServerCertificate);
        Assert.True(SetupDatabaseConnection.Read(new ServerConfiguration { DatabaseProvider = DatabaseProvider.SqlServer, DatabaseConnectionString = result })!.TrustServerCertificate);
        input.DatabaseIntegratedSecurity = true;
        var integratedPreset = new ServerConfiguration { DatabaseProvider = DatabaseProvider.SqlServer,
            DatabaseConnectionString = SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input) };
        input.DatabaseIntegratedSecurity = false; input.DatabasePassword = "";
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input, integratedPreset));
        input.DatabasePassword = "replacement"; input.DatabaseServer = "localhost"; input.DatabaseUseTls = false;
        Assert.Throws<ArgumentException>(() => SetupDatabaseConnection.Build(DatabaseProvider.SqlServer, input));
    }
}
