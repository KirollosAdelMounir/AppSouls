using System.Security.Cryptography;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using Xunit;
namespace TicketingCRM.Core.Tests;
public sealed class VaultInitializationRecoveryTests
{
    [Fact]
    public async Task InterruptedInitializationRetainsExternalKeyAndCannotOverwriteAnActiveVault()
    {
        if(OperatingSystem.IsWindows())return;
        var root=Path.Combine(Path.GetTempPath(),"appsouls-vault-resume-"+Guid.NewGuid().ToString("N"));
        try
        {
            var options=new VaultBootstrapOptions(Path.Combine(root,"vault","server"),Path.Combine(root,"keys","master"));
            PendingAdministratorFile.PreparePrivateDirectory(options.KeyPath);var key=RandomNumberGenerator.GetBytes(32);await File.WriteAllBytesAsync(options.KeyPath,key);File.SetUnixFileMode(options.KeyPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            var vault=new FileConfigurationVault(options);var configuration=new ServerConfiguration{DatabaseConnectionString="test-only-placeholder",PublicBaseUrl="https://example.test"};
            await vault.ResumeInitializationAsync(configuration);Assert.Equal(key,await File.ReadAllBytesAsync(options.KeyPath));Assert.Equal(configuration.PublicBaseUrl,(await vault.LoadAsync()).PublicBaseUrl);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>vault.ResumeInitializationAsync(configuration));Assert.Equal(key,await File.ReadAllBytesAsync(options.KeyPath));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
