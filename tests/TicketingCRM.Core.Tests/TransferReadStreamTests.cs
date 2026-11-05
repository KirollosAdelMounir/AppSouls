using TicketingCRM.Core.FileProcessing;
using Xunit;

namespace TicketingCRM.Core.Tests;

public sealed class TransferReadStreamTests
{
    [Fact]
    public async Task RejectedMetadataDoesNotOpenOrStartAnUnconsumedBrowserStream()
    {
        var opened = false;
        await using (var transfer = new TransferReadStream(() => { opened = true; return new MemoryStream([1]); }, (_, _) => ValueTask.CompletedTask))
            Assert.True(transfer.CanRead);
        Assert.False(opened);
    }
    [Fact]
    public async Task ReportsOnlyConsumedBytesAndLeavesInputOpen()
    {
        using var input = new MemoryStream([1, 2, 3, 4, 5]); var counts = new List<long>();
        await using (var transfer = new TransferReadStream(input, (bytes, _) => { counts.Add(bytes); return ValueTask.CompletedTask; }))
        {
            var buffer = new byte[3];
            Assert.Equal(3, await transfer.ReadAsync(buffer)); Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
            Assert.Equal(2, await transfer.ReadAsync(buffer)); Assert.Equal(new byte[] { 4, 5 }, buffer[..2]);
            Assert.Equal(0, await transfer.ReadAsync(buffer)); Assert.Equal(5, transfer.Position);
        }
        Assert.Equal(new long[] { 3, 5 }, counts); Assert.True(input.CanRead);
    }
    [Fact]
    public async Task CancellationDoesNotAdvanceReportedProgress()
    {
        using var input = new MemoryStream([1, 2, 3]); var reports = 0;
        await using var transfer = new TransferReadStream(input, (_, _) => { reports++; return ValueTask.CompletedTask; });
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transfer.ReadAsync(new byte[2], cancelled.Token).AsTask());
        Assert.Equal(0, reports); Assert.Equal(0, transfer.Position);
    }
}
