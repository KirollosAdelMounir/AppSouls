using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Delivery.DTOs;
using Xunit;
namespace TicketingCRM.Core.Tests;
public sealed class ClamAvArtifactScannerTests
{
 [Theory]
 [InlineData("stream: OK",ArtifactScanVerdict.Clean)]
 [InlineData("stream: Fixture FOUND",ArtifactScanVerdict.Rejected)]
 [InlineData("stream: Scan failed ERROR",ArtifactScanVerdict.Unavailable)]
 [InlineData("unrecognized: OK",ArtifactScanVerdict.Unavailable)]
 public async Task OnlyExplicitCleanVerdictPassesAndCompleteBytesUseBoundedInstreamProtocol(string response,ArtifactScanVerdict expected)
 {
  using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
  var payload=Encoding.UTF8.GetBytes("Harmless package fixture");byte[]? received=null;
  var server=Task.Run(async()=>
  {
   using var client=await listener.AcceptTcpClientAsync(timeout.Token);await using var stream=client.GetStream();var command=new byte[10];await stream.ReadExactlyAsync(command,timeout.Token);Assert.Equal("zINSTREAM\0",Encoding.ASCII.GetString(command));
   using var content=new MemoryStream();var header=new byte[4];
   while(true){await stream.ReadExactlyAsync(header,timeout.Token);var count=BinaryPrimitives.ReadUInt32BigEndian(header);if(count==0)break;Assert.InRange(count,1u,65536u);var chunk=new byte[count];await stream.ReadExactlyAsync(chunk,timeout.Token);await content.WriteAsync(chunk,timeout.Token);}
   received=content.ToArray();await stream.WriteAsync(Encoding.UTF8.GetBytes(response+"\0"),timeout.Token);
  },timeout.Token);
  var config=new ServerConfiguration();config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]=((IPEndPoint)listener.LocalEndpoint).Port.ToString()}};
  var verdict=await new ClamAvArtifactScanner(config).ScanAsync(new MemoryStream(payload),timeout.Token);await server;
  Assert.Equal(expected,verdict);Assert.Equal(payload,received);
 }
 [Fact]
 public async Task InvalidExplicitConfigurationFailsClosedWithoutReadingTheArtifact()
 {
  using var content=new MemoryStream(Encoding.UTF8.GetBytes("package"));
  var config=new ServerConfiguration();config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]="invalid"}};
  Assert.Equal(ArtifactScanVerdict.Unavailable,await new ClamAvArtifactScanner(config).ScanAsync(content));Assert.Equal(0,content.Position);
 }
 [Fact]
 public async Task CallerCancellationIsForwardedInsteadOfReportingClean()
 {
  var config=new ServerConfiguration();config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]="3310"}};using var cancelled=new CancellationTokenSource();cancelled.Cancel();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new ClamAvArtifactScanner(config).ScanAsync(new MemoryStream(),cancelled.Token));
 }
 [Fact]
 public void BuiltInLocalEndpointRequiresNoIntegrationAndPreservesExplicitOverrides()
 {
  var config=new ServerConfiguration();var scanner=new ClamAvArtifactScanner(config);
  Assert.Equal(3310,scanner.LocalPort);Assert.Empty(config.Integrations);
  config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]="4242"}};Assert.Equal(4242,scanner.LocalPort);
  config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]="65536"}};Assert.Null(scanner.LocalPort);
 }
 [Theory]
 [InlineData("PONG",true)]
 [InlineData("OK",false)]
 public async Task ProbeRequiresClamAvPongAndSendsNoArtifact(string reply,bool available)
 {
  using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
  var server=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync(timeout.Token);await using var stream=client.GetStream();var command=new byte[6];await stream.ReadExactlyAsync(command,timeout.Token);Assert.Equal("zPING\0",Encoding.ASCII.GetString(command));await stream.WriteAsync(Encoding.ASCII.GetBytes(reply+"\0"),timeout.Token);},timeout.Token);
  var port=((IPEndPoint)listener.LocalEndpoint).Port;var config=new ServerConfiguration();config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]=port.ToString()}};
  var result=await new ClamAvArtifactScanner(config).CheckAsync(timeout.Token);await server;
  Assert.Equal(available,result.Available);Assert.Equal(port,result.Port);Assert.Equal(available?"scanner-connected":"scanner-protocol",result.Code);
 }
 [Fact]
 public async Task ProbeHonorsCallerCancellationAndInvalidOverrideWithoutConnecting()
 {
  var config=new ServerConfiguration();config.Integrations["malware-scanner"]=new(){Credentials=new(){["Port"]="-1"}};
  var scanner=new ClamAvArtifactScanner(config);var result=await scanner.CheckAsync();Assert.False(result.Available);Assert.Equal("scanner-configuration",result.Code);
  using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>scanner.CheckAsync(cancelled.Token));
 }

}
