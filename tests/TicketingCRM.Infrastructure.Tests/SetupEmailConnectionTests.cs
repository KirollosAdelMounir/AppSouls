using System.Net;
using System.Net.Sockets;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Infrastructure.Administration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class SetupEmailConnectionTests
{
    [Fact]
    public async Task EmailCheckRejectsMissingHostAndNeverCreatesSetupMaterial()
    {
        var root=Path.Combine(Path.GetTempPath(),"appsouls-email-check-"+Guid.NewGuid().ToString("N"));
        var bootstrap=new VaultBootstrapOptions(Path.Combine(root,"vault/server"),Path.Combine(root,"keys/key"));
        try
        {
            var service=new ServerSetupService(null,new(bootstrap),bootstrap,new(bootstrap));
            var result=await service.TestEmailAsync(new(){SenderAddress="sender@example.test"});
            Assert.False(result.Success);Assert.Equal("email-configuration",result.Code);Assert.False(File.Exists(bootstrap.VaultPath));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task EmailCheckDoesNotAuthenticateOrSendWhenServerCannotProvideRequiredTls()
    {
        var root=Path.Combine(Path.GetTempPath(),"appsouls-email-check-"+Guid.NewGuid().ToString("N"));
        var bootstrap=new VaultBootstrapOptions(Path.Combine(root,"vault/server"),Path.Combine(root,"keys/key"));
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var commands=new List<string>();
        var server=Task.Run(async()=>
        {
            using var socket=await listener.AcceptTcpClientAsync(timeout.Token);using var stream=socket.GetStream();
            using var reader=new StreamReader(stream);using var writer=new StreamWriter(stream){NewLine="\r\n",AutoFlush=true};
            await writer.WriteLineAsync("220 fixture ESMTP");
            var command=await reader.ReadLineAsync(timeout.Token);commands.Add(command??"");
            await writer.WriteLineAsync("250 fixture"); // No STARTTLS: credentials must never be sent.
            try{while((command=await reader.ReadLineAsync(timeout.Token)) is not null)commands.Add(command);}catch(IOException){}
        },timeout.Token);
        try
        {
            var service=new ServerSetupService(null,new(bootstrap),bootstrap,new(bootstrap));
            var result=await service.TestEmailAsync(new(){EmailHost="127.0.0.1",EmailPort=((IPEndPoint)listener.LocalEndpoint).Port,SenderAddress="sender@example.test",EmailUserName="private-user",EmailPassword="private-secret"},timeout.Token);
            await server;Assert.False(result.Success);Assert.Equal("email-tls",result.Code);
            Assert.DoesNotContain("private-secret",result.Message);Assert.DoesNotContain(commands,x=>x.StartsWith("AUTH")||x.StartsWith("MAIL")||x.StartsWith("DATA"));
            Assert.Contains(commands,x=>x.StartsWith("EHLO"));Assert.False(File.Exists(bootstrap.VaultPath));
        }
        finally{listener.Stop();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
