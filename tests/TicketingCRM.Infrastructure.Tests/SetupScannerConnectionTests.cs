using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TicketingCRM.Core.Security;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Infrastructure.Administration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;

public sealed class SetupScannerConnectionTests
{
    [Fact]
    public async Task InitialSetupChecksTheTrustedLocalScannerWithoutCreatingVaultOrAdministratorMaterial()
    {
        var root=Path.Combine(Path.GetTempPath(),"appsouls-scanner-setup-"+Guid.NewGuid().ToString("N"));
        var bootstrap=new VaultBootstrapOptions(Path.Combine(root,"vault/server"),Path.Combine(root,"keys/key"));
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync(timeout.Token);await using var stream=client.GetStream();var command=new byte[6];await stream.ReadExactlyAsync(command,timeout.Token);Assert.Equal("zPING\0",Encoding.ASCII.GetString(command));await stream.WriteAsync(Encoding.ASCII.GetBytes("PONG\0"),timeout.Token);},timeout.Token);
        try
        {
            var configuration=new ServerConfiguration();configuration.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]=((IPEndPoint)listener.LocalEndpoint).Port.ToString()}};
            var preset=Path.Combine(root,"preset/server.json");PrivateFileSystem.PrepareDirectory(preset);
            await using(var file=PrivateFileSystem.Open(preset,FileMode.CreateNew))await JsonSerializer.SerializeAsync(file,configuration,cancellationToken:timeout.Token);
            var pending=new PendingAdministratorFile(bootstrap);var service=new ServerSetupService(null,new(bootstrap),bootstrap,pending,preset);
            var result=await service.TestScannerAsync(timeout.Token);await server;
            Assert.True(result.Success);Assert.Equal("scanner-connected",result.Code);
            Assert.False(File.Exists(bootstrap.VaultPath));Assert.False(pending.Exists);Assert.False(File.Exists(pending.KeyPath));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
