using TicketingCRM.DataConnection;
using System.Net.Mail;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;
using MailKit.Security;
using TicketingCRM.DataConnection.Repository.Queries;

using Microsoft.Extensions.Logging;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Infrastructure.Administration;

public sealed partial class ServerSetupService
{
    public async Task<SetupConnectionResult> TestScannerAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = pending.AcquireSetupLock();
        if (!await IsOpen(cancellationToken)) throw new InvalidOperationException("Initial setup is closed.");
        var result = await new TicketingCRM.Core.FileProcessing.ClamAvArtifactScanner(await Preset(cancellationToken) ?? new()).CheckAsync(cancellationToken);
        return new(result.Available, result.Code, result.Message);
    }

    public async Task<SetupConnectionResult> TestDatabaseAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var gate = pending.AcquireSetupLock();
            if (!await IsOpen(cancellationToken)) throw new InvalidOperationException("Initial setup is closed.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            if (!Enum.TryParse<DatabaseProvider>(request.DatabaseProvider, out var provider) || !Enum.IsDefined(provider)) throw new ArgumentException("Choose a database provider.");
            var connection = SetupDatabaseConnection.Build(provider, request, await Preset(timeout.Token));
            var inspection = await DatabaseLifecycle.InspectAsync((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)provider, connection, timeout.Token);
            if (inspection.HasAccounts)
            {
                var prepared = pending.Exists ? await pending.LoadAsync(timeout.Token, allowExpired: true) : null;
                if (prepared is null || await TicketingCRM.DataConnection.Repository.InstallationRepository.HasUnexpectedAccountsAsync((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)provider, connection, prepared.UserId, prepared.PasswordHash, timeout.Token))
                    throw new InvalidOperationException("This database already has accounts. Initial setup cannot replace or adopt them.");
            }
            return new(true, "database-connected", inspection.State == "Missing"
                ? "Database server authentication succeeded. The catalog will be created only when you apply migrations; CREATE DATABASE permission is required. No changes were made."
                : $"Server and catalog are accessible. App Souls schema: {inspection.State}; {inspection.PendingMigrations} pending migrations. No changes were made.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { return ConnectionFailure(exception, "database"); }
    }
    public async Task<SetupConnectionResult> TestEmailAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var gate = pending.AcquireSetupLock();
            if (!await IsOpen(cancellationToken)) throw new InvalidOperationException("Initial setup is closed.");
            if (string.IsNullOrWhiteSpace(request.EmailHost) || request.EmailHost.Length > 253 || Uri.CheckHostName(request.EmailHost.Trim()) == UriHostNameType.Unknown || request.EmailPort is < 1 or > 65535)
                throw new ServerSetupException("email-configuration", "Enter a valid SMTP host and port.");
            if (!MailAddress.TryCreate(request.SenderAddress, out _)) throw new ArgumentException("Enter a valid sender email.");
            if ((request.EmailUserName?.Length ?? 0) > 320 || (request.EmailPassword?.Length ?? 0) > 8192) throw new ArgumentException("Email configuration exceeds its limits.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(request.EmailHost.Trim(), request.EmailPort, request.EmailPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, timeout.Token);
            if (!string.IsNullOrWhiteSpace(request.EmailUserName)) await client.AuthenticateAsync(request.EmailUserName, request.EmailPassword ?? "", timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            return new(true, "email-connected", "SMTP connection and configured authentication succeeded. No email was sent; inbox delivery was not tested.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { return ConnectionFailure(exception, "email"); }
    }
    private SetupConnectionResult ConnectionFailure(Exception exception, string kind)
    {
        var failure = SetupFailures.Classify(exception);
        logger?.LogWarning("Setup {ConnectionKind} check failed: {FailureType}, {FailureCode}. Location: {Location}", kind, exception.GetType().FullName, failure.Code, exception.StackTrace);
        return failure.Code == "activation" ? new(false, kind + "-check", $"The {kind} connection check failed. Server diagnostics now contain the failure type and code location; no configuration was saved.") : new(false, failure.Code, failure.Message);
    }
}
