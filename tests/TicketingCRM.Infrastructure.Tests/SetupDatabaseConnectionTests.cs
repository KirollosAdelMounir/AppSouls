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
}
