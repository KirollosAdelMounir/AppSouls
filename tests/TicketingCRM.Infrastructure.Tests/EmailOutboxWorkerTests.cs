using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Infrastructure.Email;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;

public sealed class EmailOutboxWorkerTests
{
    [Fact]
    public async Task ShutdownDuringTimerWaitCompletesNormallyAndPreservesRedactedProcessingFailureLog()
    {
        // Invalid configuration fails validation before any database or SMTP connection is made.
        var configuration = new ServerConfiguration
        {
            Smtp = new SmtpConfiguration { Enabled = true, Host = "", Password = "must-not-appear-in-logs" }
        };
        var logger = new WorkerLogger();
        var processor = new EmailOutboxProcessor(configuration, NullLogger<EmailOutboxProcessor>.Instance);
        using var worker = new EmailOutboxWorker(processor, logger);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            // Proves ExecuteAsync has started and the failed pass reached the timer wait.
            await logger.FailureObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.NotNull(worker.ExecuteTask);
        await worker.ExecuteTask;
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
        var message = Assert.Single(logger.Errors);
        Assert.Contains("persisted jobs will be retried", message);
        Assert.DoesNotContain(configuration.Smtp.Password, message);
    }

    private sealed class WorkerLogger : ILogger<EmailOutboxWorker>
    {
        public TaskCompletionSource FailureObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Error) return;
            Errors.Add(formatter(state, exception));
            FailureObserved.TrySetResult();
        }
    }
}
