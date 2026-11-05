namespace TicketingCRM.Core.FileProcessing;

/// <summary>Reports application payload bytes actually read, with cancellation and backpressure. Does not own the input.</summary>
public sealed class TransferReadStream : Stream
{
    private Stream? input;
    private readonly Func<Stream>? create;
    private readonly Func<long, CancellationToken, ValueTask> report;
    private readonly bool ownsInput;
    private bool disposed;
    public TransferReadStream(Stream input, Func<long, CancellationToken, ValueTask> report) { this.input = input; this.report = report; }
    public TransferReadStream(Func<Stream> create, Func<long, CancellationToken, ValueTask> report) { this.create = create; this.report = report; ownsInput = true; }
    private Stream Input { get { ObjectDisposedException.ThrowIf(disposed, this); return input ??= create!(); } }
    private long transferred;
    public override bool CanRead => !disposed && (input?.CanRead ?? true);
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => transferred; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use asynchronous transfer reads.");
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var read = await Input.ReadAsync(buffer, cancellationToken);
        if (read > 0) { transferred += read; await report(transferred, cancellationToken); }
        return read;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    { if (disposing && ownsInput) input?.Dispose(); disposed = true; base.Dispose(disposing); }
    public override async ValueTask DisposeAsync()
    {
        if (ownsInput && input is { } opened) { input = null; await opened.DisposeAsync(); }
        disposed = true; GC.SuppressFinalize(this);
    }
}
