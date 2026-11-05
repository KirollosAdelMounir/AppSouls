using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Infrastructure.Administration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class ServerSetupServiceTests
{
    private sealed class Fixture:IAsyncDisposable
    {
        public string Root=Path.Combine(Path.GetTempPath(),"appsouls-setup-flow-"+Guid.NewGuid().ToString("N"));
        public string Database="appsouls_wizard_"+Guid.NewGuid().ToString("N");
        public NpgsqlConnection Administration=default!;
        public ServerConfiguration Configuration=default!;
        public VaultBootstrapOptions Bootstrap=default!;
        public FileConfigurationVault Vault=default!;
        public PendingAdministratorFile Pending=default!;
        public ServerSetupService Service=default!;
        public PrepareAdministratorRequest Request()=>new(){OrganizationName="Prepared vendor",DisplayName="Prepared owner",Email="prepared@example.test",Password="Prepared-Administrator-123!"};
        public CompleteServerSetupRequest Completion()=>new(){DatabaseProvider="PostgreSql",DatabaseConnectionString=Configuration.DatabaseConnectionString,PublicBaseUrl="https://example.test",ConfirmMigration=true};
        public static async Task<Fixture> Create()
        {
            var f=new Fixture();var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"));
            f.Administration=new(builder.ConnectionString);await f.Administration.OpenAsync();
            await using(var command=new NpgsqlCommand($"CREATE DATABASE {f.Database}",f.Administration))await command.ExecuteNonQueryAsync();
            builder.Database=f.Database;f.Configuration=new(){DatabaseConnectionString=builder.ConnectionString,PublicBaseUrl="https://example.test"};
            f.Bootstrap=new(Path.Combine(f.Root,"vault","server"),Path.Combine(f.Root,"keys","master"));
            PendingAdministratorFile.PreparePrivateDirectory(f.Bootstrap.KeyPath);
            f.Vault=new(f.Bootstrap);f.Pending=new(f.Bootstrap);
            f.Service=new(null,f.Vault,f.Bootstrap,f.Pending);return f;
        }
        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();await using var command=new NpgsqlCommand($"DROP DATABASE {Database} WITH (FORCE)",Administration);await command.ExecuteNonQueryAsync();
            await Administration.DisposeAsync();if(Directory.Exists(Root))Directory.Delete(Root,true);
        }
    }
    [PostgreSqlFact]
    public async Task PrepareFilePrecedesMigrationThenSeedsExactAdministratorActivatesVaultAndCleansTemporaryMaterial()
    {
        await using var f=await Fixture.Create();var input=f.Request();input.Password="Ab1!xyzz";await f.Service.PrepareAdministratorAsync(input);
        var prepared=await f.Pending.LoadAsync();Assert.NotEqual(input.Password,prepared.PasswordHash);
        await using(var empty=new NpgsqlConnection(f.Configuration.DatabaseConnectionString))
        {
            await empty.OpenAsync();await using var command=new NpgsqlCommand("SELECT to_regclass('public.\"AspNetUsers\"') IS NULL",empty);Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
        var state=await f.Service.GetStateAsync();Assert.True(state.AdministratorPrepared);Assert.True(state.IsOpen);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.PrepareAdministratorAsync(input));
        await f.Service.MigrateAndActivateAsync(f.Completion());
        var configuration=await f.Vault.LoadAsync();await using var db=PreparedAdministratorSeeder.Open(configuration,prepared.TenantId);
        var user=Assert.Single(await db.Users.ToListAsync());Assert.Equal(prepared.UserId,user.Id);Assert.True(user.IsPlatformAdministrator);Assert.True(user.EmailConfirmed);Assert.False(user.TwoFactorEnabled);
        Assert.Equal(PasswordVerificationResult.Success,new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user,user.PasswordHash!,input.Password));
        var member=Assert.Single(await db.TenantMemberships.ToListAsync());Assert.Equal("Owner",member.Role);Assert.True(member.IsActive);Assert.Equal(prepared.TenantId,member.TenantId);
        Assert.False(f.Pending.Exists);Assert.False(File.Exists(f.Pending.KeyPath));
        Assert.False((await f.Service.GetStateAsync()).IsOpen);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.PrepareAdministratorAsync(f.Request()));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ResumeAsync());
        await Assert.ThrowsAsync<ServerSetupException>(()=>f.Service.MigrateAndActivateAsync(f.Completion()));
    }
    [PostgreSqlFact]
    public async Task MissingConsentAndInvalidConfigurationKeepPreparedIdentityAndDoNotMigrate()
    {
        await using var f=await Fixture.Create();await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.PrepareAdministratorAsync(new()));
        Assert.False(f.Pending.Exists);await f.Service.PrepareAdministratorAsync(f.Request());
        var request=f.Completion();request.ConfirmMigration=false;await Assert.ThrowsAsync<ServerSetupException>(()=>f.Service.MigrateAndActivateAsync(request));
        request=f.Completion();request.PublicBaseUrl="http://unsafe.example.test";await Assert.ThrowsAsync<ServerSetupException>(()=>f.Service.MigrateAndActivateAsync(request));
        Assert.True(f.Pending.Exists);Assert.False(File.Exists(f.Bootstrap.VaultPath));
        await using var empty=new NpgsqlConnection(f.Configuration.DatabaseConnectionString);await empty.OpenAsync();await using var command=new NpgsqlCommand("SELECT to_regclass('public.\"AspNetUsers\"') IS NULL",empty);Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }
    [PostgreSqlFact]
    public async Task ExactSeedCanResumeAfterVaultFailureButAnotherPreparedIdentityCannotAdoptDatabase()
    {
        await using var f=await Fixture.Create();await f.Service.PrepareAdministratorAsync(f.Request());var prepared=await f.Pending.LoadAsync();
        await PreparedAdministratorSeeder.MigrateAndSeedAsync(f.Configuration,prepared);await PreparedAdministratorSeeder.MigrateAndSeedAsync(f.Configuration,prepared);
        var other=new PreparedAdministrator{UserId=Guid.NewGuid(),TenantId=Guid.NewGuid(),Email="prepared@example.test",DisplayName="Other",OrganizationName="Other",PasswordHash=prepared.PasswordHash,SecurityStamp=prepared.SecurityStamp,ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(1)};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>PreparedAdministratorSeeder.MigrateAndSeedAsync(f.Configuration,other));
        Assert.True(f.Pending.Exists);await f.Service.MigrateAndActivateAsync(f.Completion());
        await using var db=PreparedAdministratorSeeder.Open(await f.Vault.LoadAsync(),prepared.TenantId);Assert.Equal(prepared.UserId,Assert.Single(await db.Users.ToListAsync()).Id);
    }
    [PostgreSqlFact]
    public async Task ResumeExtendsPreparedIdentityWithoutChangingPasswordOrCreatingSchema()
    {
        await using var f=await Fixture.Create();await f.Service.PrepareAdministratorAsync(f.Request());
        var prepared=await f.Pending.LoadAsync();prepared.ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(-1);await f.Pending.SaveAsync(prepared);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Pending.LoadAsync());
        await f.Service.ResumeAsync();var resumed=await f.Pending.LoadAsync();
        Assert.Equal(prepared.UserId,resumed.UserId);Assert.Equal(prepared.PasswordHash,resumed.PasswordHash);Assert.True(resumed.ExpiresAtUtc>DateTimeOffset.UtcNow);
        Assert.False(File.Exists(f.Bootstrap.VaultPath));
    }
    [PostgreSqlFact]
    public async Task OptionalBlankFormFieldsUseProtectedPresetAndNeverReturnConnectionSecrets()
    {
        await using var f=await Fixture.Create();var preset=Path.Combine(f.Root,"preset","server.json");PendingAdministratorFile.PreparePrivateDirectory(preset);
        await File.WriteAllTextAsync(preset,JsonSerializer.Serialize(f.Configuration));if(!OperatingSystem.IsWindows())File.SetUnixFileMode(preset,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        var service=new ServerSetupService(null,f.Vault,f.Bootstrap,f.Pending,preset);
        var state=await service.GetStateAsync();Assert.True(state.HasDatabasePreset);Assert.DoesNotContain(f.Configuration.DatabaseConnectionString,JsonSerializer.Serialize(state));
        await service.PrepareAdministratorAsync(f.Request());var request=f.Completion();request.DatabaseConnectionString=null!;request.EmailHost=null!;request.EmailUserName=null!;request.EmailPassword=null!;request.SenderAddress=null!;
        var fields=state.Database!;request.DatabaseServer=fields.Server;request.DatabasePort=fields.Port;request.DatabaseCatalog=fields.Catalog;request.DatabaseUserName=fields.UserName;request.DatabaseUseTls=fields.UseTls;
        await service.MigrateAndActivateAsync(request);Assert.False(File.Exists(preset));Assert.False(f.Pending.Exists);Assert.Equal(f.Configuration.DatabaseConnectionString,(await f.Vault.LoadAsync()).DatabaseConnectionString);
    }
    [PostgreSqlFact]
    public async Task DatabaseTestIsReadOnlyAndInvalidPublicAddressKeepsPreparedAdministrator()
    {
        await using var f=await Fixture.Create();await f.Service.PrepareAdministratorAsync(f.Request());
        var parsed=new NpgsqlConnectionStringBuilder(f.Configuration.DatabaseConnectionString);
        var request=f.Completion();request.DatabaseServer=parsed.Host!;request.DatabasePort=parsed.Port;request.DatabaseCatalog=parsed.Database!;
        request.DatabaseUserName=parsed.Username!;request.DatabasePassword=parsed.Password!;request.DatabaseUseTls=false;
        var result=await f.Service.TestDatabaseAsync(request);Assert.True(result.Success,result.Message);Assert.Equal("database-connected",result.Code);
        await using(var db=new NpgsqlConnection(f.Configuration.DatabaseConnectionString))
        {await db.OpenAsync();await using var command=new NpgsqlCommand("SELECT to_regclass('public.\"AspNetUsers\"') IS NULL",db);Assert.True((bool)(await command.ExecuteScalarAsync())!);}
        Assert.True(f.Pending.Exists);Assert.False(File.Exists(f.Bootstrap.VaultPath));
        request.PublicBaseUrl="http://localhost:5176";
        var failure=await Assert.ThrowsAsync<ServerSetupException>(()=>f.Service.MigrateAndActivateAsync(request));
        Assert.Equal("public-address",failure.Code);Assert.Contains("HTTPS",failure.Message);Assert.DoesNotContain(request.DatabasePassword,failure.Message);
        Assert.True(f.Pending.Exists);request.PublicBaseUrl="https://example.test";await f.Service.MigrateAndActivateAsync(request);
        Assert.False(f.Pending.Exists);Assert.False((await f.Service.TestDatabaseAsync(request)).Success);Assert.False((await f.Service.TestEmailAsync(request)).Success);
    }
    [PostgreSqlFact]
    public async Task DatabaseTestExplainsAuthenticationFailureAndExistingAccountsWithoutLeakingSecrets()
    {
        await using var f=await Fixture.Create();await f.Service.PrepareAdministratorAsync(f.Request());
        var parsed=new NpgsqlConnectionStringBuilder(f.Configuration.DatabaseConnectionString);
        var request=f.Completion();request.DatabaseServer=parsed.Host!;request.DatabasePort=parsed.Port;request.DatabaseCatalog=parsed.Database!;
        request.DatabaseUserName=parsed.Username!;request.DatabasePassword="wrong-private-secret";request.DatabaseUseTls=false;
        var failed=await f.Service.TestDatabaseAsync(request);Assert.False(failed.Success);Assert.Equal("database-authentication",failed.Code);Assert.DoesNotContain(request.DatabasePassword,failed.Message);
        var prepared=await f.Pending.LoadAsync();await PreparedAdministratorSeeder.MigrateAndSeedAsync(f.Configuration,prepared);
        prepared.UserId=Guid.NewGuid();await f.Pending.SaveAsync(prepared);request.DatabasePassword=parsed.Password!;
        var existing=await f.Service.TestDatabaseAsync(request);Assert.False(existing.Success);Assert.Equal("database-existing",existing.Code);
        Assert.True(f.Pending.Exists);Assert.False(File.Exists(f.Bootstrap.VaultPath));
    }

}
