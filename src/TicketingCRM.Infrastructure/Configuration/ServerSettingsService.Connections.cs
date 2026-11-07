using System.Net.Mail;
using MailKit.Security;
using TicketingCRM.DataConnection.Repository.Queries;

using Microsoft.Extensions.Logging;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Administration;
namespace TicketingCRM.Infrastructure.Configuration;

public sealed partial class ServerSettingsService
{
    public async Task<TicketingCRM.Domain.Delivery.DTOs.ArtifactScannerAvailability> CheckScannerAsync(CancellationToken cancellationToken = default)
    {
        await Authorize(cancellationToken);
        return await (scanner ?? new TicketingCRM.Core.FileProcessing.ClamAvArtifactScanner(active)).CheckAsync(cancellationToken);
    }

    public Task UpdatePublicAddressAsync(UpdatePublicAddressRequest request, CancellationToken cancellationToken = default) => Change(async current =>
    {
        if (string.IsNullOrWhiteSpace(request.PublicBaseUrl) || request.PublicBaseUrl.Length > 2048 ||
            !Uri.TryCreate(request.PublicBaseUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Enter a public HTTPS address without credentials, query or fragment.");
        var updated = Copy(current, current.DatabaseProvider, current.DatabaseConnectionString, current.Smtp, current.Integrations);
        updated.PublicBaseUrl = request.PublicBaseUrl.Trim().TrimEnd('/');
        updated.Validate(); await vault.SaveAsync(updated, cancellationToken); active.PublicBaseUrl = updated.PublicBaseUrl;
        logger.LogInformation("Platform administrator {UserId} changed the public address", actor.UserId);
    }, cancellationToken);

    public async Task UpdateOrganizationAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken);
        await AuthorizeSession(s, cancellationToken);
        if (maintenance?.MaintenanceMode == true) throw new InvalidOperationException("Configuration writes are paused for database maintenance.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200) throw new ArgumentException("Enter an organization name up to 200 characters.");
        var tenant = await s.Repository<Tenant>().GetAllQueryable().SingleAsync(x => x.Id == actor.TenantId, cancellationToken);
        tenant.Name = request.Name.Trim(); await s.CommitAsync(cancellationToken);
    }

    public async Task<SetupConnectionResult> TestEmailAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default)
    {
        await Authorize(cancellationToken);
        try
        {
            var saved = await vault.LoadAsync(cancellationToken);
            var smtp = EmailConfiguration(request, saved.Smtp);
            if (string.IsNullOrWhiteSpace(smtp.Host) || Uri.CheckHostName(smtp.Host) == UriHostNameType.Unknown || !MailAddress.TryCreate(smtp.FromAddress, out _))
                throw new ArgumentException("Enter a valid SMTP host, port and sender email.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, timeout.Token);
            if (!string.IsNullOrWhiteSpace(smtp.UserName)) await client.AuthenticateAsync(smtp.UserName, smtp.Password, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            return new(true, "email-connected", "SMTP connection and configured authentication succeeded. No email was sent; inbox delivery was not tested.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ArgumentException) { return new(false, "email-configuration", "Check the SMTP host, port, sender and password for this target."); }
        catch (Exception e)
        {
            var failure = SetupFailures.Classify(e);
            logger.LogWarning("Settings email check failed: {FailureType}, {FailureCode}", e.GetType().Name, failure.Code);
            return new(false, failure.Code, failure.Code == "activation" ? "The email connection check failed. No settings were saved." : failure.Message);
        }
    }
    private static SmtpConfiguration EmailConfiguration(UpdateEmailSettingsRequest request, SmtpConfiguration saved)
    {
        if (request.Host is null || request.UserName is null || request.SenderAddress is null || request.Host.Length > 253 || request.UserName.Length > 320 || request.SenderAddress.Length > 254 || request.Password?.Length > 8192 || request.Port is < 1 or > 65535)
            throw new ArgumentException("Check the email settings.");
        var sameTarget = request.Host.Trim().Equals(saved.Host, StringComparison.OrdinalIgnoreCase) && request.Port == saved.Port && request.UserName == saved.UserName;
        if (request.Password is null && !sameTarget && !string.IsNullOrWhiteSpace(request.UserName))
            throw new ArgumentException("Enter an SMTP password when changing the host, port or username.");
        return new() { Enabled = request.Enabled, Host = request.Host.Trim(), Port = request.Port, FromAddress = request.SenderAddress.Trim(), UserName = request.UserName,
            Password = request.Password ?? (sameTarget ? saved.Password : ""), RequireTls = true };
    }
}
