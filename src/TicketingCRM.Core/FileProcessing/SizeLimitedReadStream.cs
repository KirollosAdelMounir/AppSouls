namespace TicketingCRM.Core.FileProcessing;
public sealed class SizeLimitedReadStream(Stream input,long maximumBytes):Stream
{
 private long count;
 public override bool CanRead=>input.CanRead;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>count;set=>throw new NotSupportedException();}
 private int Check(int read){count=checked(count+read);if(count>maximumBytes)throw new ArgumentException("The attachment exceeds its permitted size.");return read;}
 public override int Read(byte[] buffer,int offset,int length)=>Check(input.Read(buffer,offset,length));
 public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)=>Check(await input.ReadAsync(buffer,ct));
 public override void Flush()=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}
