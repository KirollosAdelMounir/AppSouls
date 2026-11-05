using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Delivery.Interfaces;
namespace TicketingCRM.Core.FileProcessing;
public sealed class ClamAvArtifactScanner(ServerConfiguration configuration):IArtifactScanner
{
 public async Task<ArtifactScanVerdict> ScanAsync(Stream content,CancellationToken cancellationToken=default)
 {
  if(!configuration.Integrations.TryGetValue("malware-scanner",out var integration)||!integration.Credentials.TryGetValue("Port",out var portText)||!int.TryParse(portText,out var port)||port is <1 or >65535)return ArtifactScanVerdict.Unavailable;
  try
  {
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromMinutes(2));
   // Only a local scanner is supported; never expose plaintext clamd traffic across networks.
   using var client=new TcpClient();await client.ConnectAsync(IPAddress.Loopback,port,timeout.Token);await using var stream=client.GetStream();
   await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"),timeout.Token);
   var buffer=new byte[65536];var header=new byte[4];int read;
   while((read=await content.ReadAsync(buffer,timeout.Token))>0){BinaryPrimitives.WriteUInt32BigEndian(header,(uint)read);await stream.WriteAsync(header,timeout.Token);await stream.WriteAsync(buffer.AsMemory(0,read),timeout.Token);}
   Array.Clear(header);await stream.WriteAsync(header,timeout.Token);
   var reply=new byte[1024];var length=0;
   while(length<reply.Length){var n=await stream.ReadAsync(reply.AsMemory(length,1),timeout.Token);if(n==0)break;if(reply[length++]==0)break;}
   var response=Encoding.UTF8.GetString(reply,0,length).TrimEnd('\0','\r','\n');
   return response=="stream: OK"?ArtifactScanVerdict.Clean:response.EndsWith(" FOUND",StringComparison.Ordinal)?ArtifactScanVerdict.Rejected:ArtifactScanVerdict.Unavailable;
  }
  catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
  catch(Exception e)when(e is IOException or SocketException or OperationCanceledException){return ArtifactScanVerdict.Unavailable;}
 }
}
