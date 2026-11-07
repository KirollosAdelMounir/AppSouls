using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using MailKit.Net.Smtp;
using MailKit.Security;
using TicketingCRM.DataConnection.Repository.Queries;
using DbUpdateConcurrencyException = Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException;

using Microsoft.Extensions.Logging;
using MimeKit;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using Provider = TicketingCRM.DataConnection.Configuration.DatabaseProvider;

namespace TicketingCRM.Infrastructure.Email;

/// <summary>Claimed, bounded SMTP delivery. SMTP acceptance and database commit cannot be one atomic operation.</summary>
public sealed class EmailOutboxProcessor(ServerConfiguration configuration, ILogger<EmailOutboxProcessor> logger, IDataProtectionProvider? protection = null)
{
    private readonly string _worker = Guid.NewGuid().ToString("N");
    private const int MaxAttempts = 10;

    public async Task<int> SendPendingAsync(CancellationToken cancellationToken = default)
    {
        var smtp=configuration.Smtp; // One immutable snapshot per processing pass; never mix hosts and credentials during a settings change.
        if (!smtp.Enabled) return 0;
        configuration.Validate();
        var sent = 0;
        // Audited platform operation: discover active tenant identifiers only. All message and user authorization follows in tenant scope.
        var provider = (Provider)(int)configuration.DatabaseProvider;
        await foreach (var tenantId in TicketingCRM.DataConnection.Repository.InstallationRepository.ActiveTenantsAsync(provider, configuration.DatabaseConnectionString, cancellationToken))
        {
            await using var context = TicketingCRM.DataConnection.Repository.InstallationRepository.OpenSession(provider, configuration.DatabaseConnectionString, tenantId);
            var now = DateTimeOffset.UtcNow;
            var ids = await context.Repository<OutboxMessage>().GetReadOnlyQueryable()
                .Where(x => x.DeliveredAtUtc == null && x.Attempts < MaxAttempts &&
                    (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                    (x.LeaseExpiresAtUtc == null || x.LeaseExpiresAtUtc <= now))
                .OrderBy(x => x.CreatedAtUtc).Select(x => x.Id).Take(20).ToListAsync(cancellationToken);
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.ClearTracking();
                var job = await context.Repository<OutboxMessage>().GetAllQueryable().SingleAsync(x => x.Id == id, cancellationToken);
                now = DateTimeOffset.UtcNow;
                if (job.DeliveredAtUtc != null || job.Attempts >= MaxAttempts || job.NextAttemptAtUtc > now || job.LeaseExpiresAtUtc > now) continue;
                job.LeaseOwner = _worker;
                job.LeaseExpiresAtUtc = now.AddMinutes(5);
                job.Attempts++;
                try { await context.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateConcurrencyException) { continue; }
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(45));
                    using var message = await ComposeAuthorizedAsync(context, job, smtp, timeout.Token);
                    using var client = new SmtpClient { Timeout = 30_000 };
                    await client.ConnectAsync(smtp.Host, smtp.Port,
                        smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, timeout.Token);
                    if (!string.IsNullOrWhiteSpace(smtp.UserName))
                        await client.AuthenticateAsync(smtp.UserName, smtp.Password, timeout.Token);
                    await client.SendAsync(message, timeout.Token);
                    // SendAsync success means SMTP accepted; it is not proof of inbox delivery.
                    job.DeliveredAtUtc = DateTimeOffset.UtcNow;
                    job.LastError = null;
                    job.NextAttemptAtUtc = null;
                    job.LeaseOwner = null;
                    job.LeaseExpiresAtUtc = null;
                    await context.SaveChangesAsync(cancellationToken);
                    sent++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (DbUpdateConcurrencyException)
                {
                    // Ownership/version was changed; never overwrite another worker's state.
                    logger.LogWarning("Email outbox delivery state changed concurrently; operator review may be required.");
                }
                catch (Exception exception) when (exception is IOException or MailKit.CommandException or MailKit.ProtocolException
                    or System.Security.Authentication.AuthenticationException or InvalidOperationException or FormatException
                    or System.Security.Cryptography.CryptographicException or JsonException or KeyNotFoundException or ArgumentException or System.Net.Sockets.SocketException
                    or MailKit.Security.AuthenticationException or MailKit.Security.SslHandshakeException
                    or OperationCanceledException or MailKit.ServiceNotAuthenticatedException or MailKit.ServiceNotConnectedException)
                {
                    job.LastError = exception is InvalidOperationException or FormatException or JsonException
                        ? "RecipientOrPayloadValidationFailed" : "SmtpDeliveryFailed";
                    job.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, job.Attempts - 1)) + Random.Shared.Next(0, 16));
                    job.LeaseOwner = null;
                    job.LeaseExpiresAtUtc = null;
                    try { await context.SaveChangesAsync(cancellationToken); }
                    catch (DbUpdateConcurrencyException) { /* Another owner won; preserve its state. */ }
                    logger.LogWarning("Email outbox attempt failed. Details are intentionally redacted; failed jobs remain persisted.");
                }
            }
        }
        return sent;
    }

    private async Task<MimeMessage> ComposeAuthorizedAsync(TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSession context, OutboxMessage job, SmtpConfiguration smtp, CancellationToken ct)
    {
        if(job.Kind=="CustomerInvitation")return await ComposeInvitationAsync(context,job,smtp,ct);
        if (job.Kind != "SupportAcknowledgement" || job.TicketId is not { } ticketId || !Guid.TryParse(job.Recipient, out var userId))
            throw new InvalidOperationException("Unsupported email job.");
        if (!await context.Repository<Tenant>().GetAllQueryable().AnyAsync(x => x.IsActive, ct)) throw new InvalidOperationException("Inactive tenant.");
        var ticket = await context.Repository<SupportTicket>().GetReadOnlyQueryable().SingleOrDefaultAsync(x => x.Id == ticketId && x.ReporterUserId == userId, ct)
            ?? throw new InvalidOperationException("Invalid report owner.");
        if (!await context.Repository<TenantMembership>().GetAllQueryable().AnyAsync(x => x.UserId == userId && x.CustomerId == ticket.CustomerId && x.IsActive, ct))
            throw new InvalidOperationException("Reporter membership unavailable.");
        var user = await context.Repository<TicketingCRM.DataConnection.Identity.ApplicationUser>().GetReadOnlyQueryable().SingleOrDefaultAsync(x => x.Id == userId && x.EmailConfirmed, ct);
        if (user?.Email is null) throw new InvalidOperationException("Verified reporter email unavailable.");
        if (job.PayloadJson.Length > 8192) throw new InvalidOperationException("Invalid email payload.");
        using var payload = JsonDocument.Parse(job.PayloadJson);
        var reference = payload.RootElement.GetProperty("Reference").GetString();
        var progress = payload.RootElement.GetProperty("ProgressUrl").GetString();
        var expected = configuration.PublicBaseUrl.TrimEnd('/') + "/support/" + ticket.Id;
        if (reference != ticket.Reference || progress != expected || reference is null || reference.Length > 100 || reference.Any(char.IsControl))
            throw new InvalidOperationException("Invalid report notification.");
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(user.Email));
        message.MessageId = $"appsouls-{job.TenantId:N}-{job.Id:N}@{new Uri(configuration.PublicBaseUrl).IdnHost}";
        message.Subject = $"App Souls report submitted: {reference}";
        message.Body = new TextPart("plain") { Text = $"Your report {reference} was submitted.\n\nSign in to follow its progress:\n{progress}\n\nThis email intentionally excludes private report details.\nApp Souls by Tetrarchia" };
        return message;
    }

    private async Task<MimeMessage> ComposeInvitationAsync(TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSession context,OutboxMessage job,SmtpConfiguration smtp,CancellationToken ct)
    {
        if(protection is null||job.PayloadJson.Length>8192||!Guid.TryParse(job.Recipient,out var userId))throw new InvalidOperationException("Invalid invitation job.");
        using var payload=JsonDocument.Parse(job.PayloadJson);var id=payload.RootElement.GetProperty("InvitationId").GetGuid();
        var invitation=await context.Repository<CustomerInvitation>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==userId&&x.AcceptedAtUtc==null&&x.ExpiresAtUtc>DateTimeOffset.UtcNow,ct)??throw new InvalidOperationException("Invitation unavailable.");
        var user=await context.Repository<TicketingCRM.DataConnection.Identity.ApplicationUser>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==userId&&!x.EmailConfirmed&&x.SecurityStamp==invitation.SecurityStamp&&x.PasswordHash==null,ct)??throw new InvalidOperationException("Invitation identity unavailable.");
        if(!await context.Repository<TenantMembership>().GetAllQueryable().AnyAsync(x=>x.UserId==userId&&x.CustomerId==invitation.CustomerId&&x.Role=="Customer"&&!x.IsActive,ct)||!await context.Repository<Tenant>().GetAllQueryable().AnyAsync(x=>x.IsActive,ct))throw new InvalidOperationException("Invitation membership unavailable.");
        var token=protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);
        if(token.Length!=64||Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)))!=invitation.TokenHash)throw new InvalidOperationException("Invitation proof unavailable.");
        var url=configuration.PublicBaseUrl.TrimEnd('/')+"/account/activate?id="+id+"#token="+Uri.EscapeDataString(token);
        var message=new MimeMessage();message.From.Add(MailboxAddress.Parse(smtp.FromAddress));message.To.Add(MailboxAddress.Parse(user.Email!));
        message.MessageId=$"appsouls-{job.TenantId:N}-{job.Id:N}@{new Uri(configuration.PublicBaseUrl).IdnHost}";message.Subject="Set up your App Souls customer account";
        message.Body=new TextPart("plain"){Text=$"Your subscribed products are ready. Verify your email and choose your password using this link before {invitation.ExpiresAtUtc:u}:\n\n{url}\n\nAfter signing in, open Software downloads and Licenses. If you did not request this account, ignore this message.\nApp Souls by Tetrarchia"};return message;
    }

}
