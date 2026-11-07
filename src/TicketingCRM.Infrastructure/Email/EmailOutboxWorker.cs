using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TicketingCRM.Infrastructure.Email;

public sealed class EmailOutboxWorker(EmailOutboxProcessor processor, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                try { await processor.SendPendingAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    // Provider and SMTP errors may contain connection strings, addresses or credentials.
                    logger.LogError("Email outbox processing failed; persisted jobs will be retried. Check service configuration and database availability.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A canceled timer wait is normal host shutdown, just like cancellation during processing.
        }
    }
}
