using TicketingCRM.DataConnection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.Security;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Infrastructure.Administration;

namespace TicketingCRM.Security;

internal static class BootstrapCommands
{
    internal static async Task<bool> TryRunAsync(string[] args, FileConfigurationVault vault, VaultBootstrapOptions bootstrap)
    {
        if (args.Length == 0 || args[0] is not ("--vault-init" or "--vault-update" or "--vault-export" or "--vault-restore" or "--migrate" or "--provision-member" or "--grant-platform-admin")) return false;
        if (args.Length == 2 && args[0] is "--vault-init" or "--vault-update")
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; jsonOptions.Converters.Add(new JsonStringEnumConverter());
            PrivateFileSystem.CheckFile(Path.GetFullPath(args[1]));
            var input = JsonSerializer.Deserialize<ServerConfiguration>(await File.ReadAllTextAsync(args[1]), jsonOptions) ?? throw new InvalidOperationException("Invalid configuration input.");
            if (args[0] == "--vault-init") await vault.InitializeAsync(input); else await vault.SaveAsync(input); Console.WriteLine("Configuration vault saved."); return true;
        }
        if (args.Length == 2 && args[0] == "--vault-export") { await vault.ExportRecoveryAsync(args[1]); Console.WriteLine("Encrypted recovery copy exported."); return true; }
        if (args.Length == 2 && args[0] == "--vault-restore") { await vault.RestoreRecoveryAsync(args[1]); Console.WriteLine("Vault restored from authenticated recovery copy."); return true; }
        ServerConfiguration? configuration = File.Exists(bootstrap.VaultPath) ? await vault.LoadAsync() : null;
        if (args.Length > 0 && args[0] == "--migrate")
        {
            if (configuration is null) throw new InvalidOperationException("Initialize the vault first.");
            var pending = new PendingAdministratorFile(bootstrap);
            if (pending.Exists)
            {
                await PreparedAdministratorSeeder.MigrateAndSeedAsync(configuration, await pending.LoadAsync()); pending.Complete();
                Console.WriteLine("Database migrations applied and the prepared administrator activated.");
            }
            else { await DatabaseLifecycle.ApplyAsync((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider, configuration.DatabaseConnectionString); Console.WriteLine("Database migrations applied."); }
            return true;
        }
        if (args.Length == 2 && args[0] == "--provision-member")
        { if (configuration is null) throw new InvalidOperationException("Initialize the vault first."); await Provisioning.CreateMemberAsync(configuration, args[1]); return true; }
        if (args.Length == 2 && args[0] == "--grant-platform-admin")
        {
            if (configuration is null) throw new InvalidOperationException("Initialize the vault first.");
            PrivateFileSystem.CheckFile(Path.GetFullPath(args[1]));
            using var input = JsonDocument.Parse(await File.ReadAllTextAsync(args[1])); var email = input.RootElement.GetProperty("Email").GetString();
            await using var db = Provisioning.Open(configuration); var user = await db.Users.SingleAsync(x => x.Email == email); user.IsPlatformAdministrator = true; await db.SaveChangesAsync(); Console.WriteLine("Platform administration enabled for the specified account."); return true;
        }
        return false;
    }
}
