using System.Net.Mail;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.DTOs;
using TicketingCRM.Domain.Sales.Interfaces;
using TicketingCRM.Domain.Sales.Models;
namespace TicketingCRM.Infrastructure.Sales;

public sealed partial class SalesPipelineService(IWorkspaceSessionFactory sessions, ICurrentActor actor) : ISalesPipelineService
{
    private static readonly string[] Currencies = ["USD", "EUR", "GBP", "EGP", "SAR", "AED"];
    private async Task Authorize(IWorkspaceSession session,CancellationToken ct)=>await TicketingCRM.Infrastructure.Security.WorkspaceAccess.RequireAsync(session,actor,administrator:true,ct:ct);
    private static string Text(string? value, int length, bool required = true)
    {
        var text = value?.Trim() ?? "";
        if ((required && text.Length == 0) || text.Length > length) throw new ArgumentException($"Enter {(required ? "1" : "0")}–{length} characters.");
        return text;
    }
    // PostgreSQL timestamps use microsecond precision; normalize before hashing for provider-compatible retries.
    private static DateTimeOffset? Utc(DateTimeOffset? value) => value is { } date ? new DateTimeOffset(date.UtcTicks - date.UtcTicks % 10, TimeSpan.Zero) : null;
    private static void Operation(Guid operation) { if (operation == Guid.Empty) throw new ArgumentException("A request identifier is required."); }
    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string Money(decimal value, string currency)
    {
        if (value is < 0 or > 999999999999.99m || decimal.Round(value, 2) != value) throw new ArgumentException("Expected value must be nonnegative with at most two decimal places.");
        var code = (currency ?? "").Trim().ToUpperInvariant();
        if (!Currencies.Contains(code)) throw new ArgumentException("Choose USD, EUR, GBP, EGP, SAR or AED.");
        return code;
    }
    private static async Task Owner(IWorkspaceSession session, Guid id, CancellationToken ct)
    {
        if (!await session.Query<TenantMembership>().AnyAsync(x => x.UserId == id && x.IsActive && x.CustomerId == null && (x.Role == "Owner" || x.Role == "Admin"), ct))
            throw new ArgumentException("Choose an active sales owner in this organization.");
    }
    private void History(IWorkspaceSession s, Guid? lead, Guid? opportunity, Guid operation, string digest, string from, string to, string reason = "")
        => s.Add(new SalesHistory { TenantId = actor.TenantId, LeadId = lead, OpportunityId = opportunity, ActorUserId = actor.UserId,
            OperationId = operation, RequestDigest = digest, OccurredAtUtc = DateTimeOffset.UtcNow, FromStage = from, ToStage = to, Reason = reason });
    private static void Same(string actual, string expected)
    { if (actual != expected) throw new InvalidOperationException("This request identifier was already used with different details."); }
    public async Task<SalesPipelinePage> GetAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50) throw new ArgumentException("Choose a valid page and a page size from 1 to 50.");
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken);
        var leadQuery = s.Query<SalesLead>().AsNoTracking(); var opportunityQuery = s.Query<SalesOpportunity>().AsNoTracking();
        var leads = await leadQuery.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new LeadDto(x.Id, x.Version, x.ContactName, x.CompanyName, x.Email, x.Phone, x.Source, x.Notes, x.Stage, x.OwnerUserId, x.FollowUpAtUtc, x.CustomerId)).ToListAsync(cancellationToken);
        var opportunities = await (from item in opportunityQuery join customer in s.Query<Customer>() on item.CustomerId equals customer.Id
            orderby item.CreatedAtUtc descending, item.Id
            select new OpportunityDto(item.Id, item.Version, item.Title, item.CustomerId, customer.Name, item.Stage, item.OwnerUserId, item.ExpectedValue, item.Currency, item.FollowUpAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var owners = await (from member in s.Query<TenantMembership>() join user in s.Query<ApplicationUser>() on member.UserId equals user.Id
            where member.IsActive && member.CustomerId == null && (member.Role == "Owner" || member.Role == "Admin")
            orderby user.DisplayName, member.UserId select new SalesOwnerDto(member.UserId, user.DisplayName, user.Email ?? "")).Take(100).ToListAsync(cancellationToken);
        return new(leads, opportunities, owners, await leadQuery.CountAsync(cancellationToken), await opportunityQuery.CountAsync(cancellationToken), page, pageSize);
    }
    public async Task<Guid> CreateLeadAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken); Operation(request.OperationId);
        var name = Text(request.ContactName, 160); var company = Text(request.CompanyName, 160); var email = Text(request.Email, 254);
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email) throw new ArgumentException("Enter a valid contact email.");
        var phone = Text(request.Phone, 50, false); var source = Text(request.Source, 100, false); var notes = Text(request.Notes, 4000, false);
        var followUp = Utc(request.FollowUpAtUtc);
        var digest = Digest(new { name, company, email, phone, source, notes, request.OwnerUserId, followUp });
        var existing = await s.Query<SalesLead>().SingleOrDefaultAsync(x => x.CreatedByUserId == actor.UserId && x.CreationOperationId == request.OperationId, cancellationToken);
        if (existing is not null) { Same(existing.CreationDigest, digest); return existing.Id; }
        await Owner(s, request.OwnerUserId, cancellationToken);
        var lead = new SalesLead { TenantId = actor.TenantId, ContactName = name, CompanyName = company, Email = email, Phone = phone,
            Source = source, Notes = notes, OwnerUserId = request.OwnerUserId, FollowUpAtUtc = followUp, CreatedByUserId = actor.UserId,
            CreatedAtUtc = DateTimeOffset.UtcNow, CreationOperationId = request.OperationId, CreationDigest = digest };
        s.Add(lead); History(s, lead.Id, null, request.OperationId, digest, "", LeadStage.New.ToString());
        await s.CommitAsync(cancellationToken); return lead.Id;
    }
    public async Task<Guid> CreateOpportunityAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken); Operation(request.OperationId);
        var title = Text(request.Title, 200); var currency = Money(request.ExpectedValue, request.Currency); var followUp = Utc(request.FollowUpAtUtc);
        var digest = Digest(new { title, request.CustomerId, request.OwnerUserId, value = request.ExpectedValue.ToString("F2", CultureInfo.InvariantCulture), currency, followUp });
        var existing = await s.Query<SalesOpportunity>().SingleOrDefaultAsync(x => x.CreatedByUserId == actor.UserId && x.CreationOperationId == request.OperationId, cancellationToken);
        if (existing is not null) { Same(existing.CreationDigest, digest); return existing.Id; }
        await Owner(s, request.OwnerUserId, cancellationToken);
        if (!await s.Query<Customer>().AnyAsync(x => x.Id == request.CustomerId, cancellationToken)) throw new ArgumentException("Choose a customer in this organization.");
        var opportunity = new SalesOpportunity { TenantId = actor.TenantId, Title = title, CustomerId = request.CustomerId, OwnerUserId = request.OwnerUserId,
            ExpectedValue = request.ExpectedValue, Currency = currency, FollowUpAtUtc = followUp, CreatedByUserId = actor.UserId,
            CreatedAtUtc = DateTimeOffset.UtcNow, CreationOperationId = request.OperationId, CreationDigest = digest };
        s.Add(opportunity); History(s, null, opportunity.Id, request.OperationId, digest, "", OpportunityStage.New.ToString());
        await s.CommitAsync(cancellationToken); return opportunity.Id;
    }
    public async Task<LeadConversionReceipt> ConvertLeadAsync(ConvertLeadRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken); Operation(request.OperationId);
        var title = Text(request.OpportunityTitle, 200); var currency = Money(request.ExpectedValue, request.Currency); var followUp = Utc(request.FollowUpAtUtc);
        var digest = Digest(new { request.LeadId, title, request.OwnerUserId, value = request.ExpectedValue.ToString("F2", CultureInfo.InvariantCulture), currency, followUp });
        var lead = await s.Query<SalesLead>().SingleOrDefaultAsync(x => x.Id == request.LeadId, cancellationToken) ?? throw new ArgumentException("Lead not found.");
        if (lead.ConversionOperationId == request.OperationId)
        {
            Same(lead.ConversionDigest ?? "", digest);
            if (!await Replayed(s, lead.Id, null, request.OperationId, digest, cancellationToken)) throw new InvalidOperationException("This conversion belongs to a different request actor.");
            var converted = await s.Query<SalesOpportunity>().SingleAsync(x => x.SourceLeadId == lead.Id, cancellationToken);
            return new(converted.CustomerId, converted.Id);
        }
        if (lead.Version != request.ExpectedVersion) throw new InvalidOperationException("The lead changed. Refresh before converting it.");
        if (lead.Stage != LeadStage.Qualified) throw new InvalidOperationException("Qualify the lead before converting it.");
        await Owner(s, request.OwnerUserId, cancellationToken);
        if (await s.Query<SalesHistory>().AnyAsync(x => x.LeadId == lead.Id && x.ActorUserId == actor.UserId && x.OperationId == request.OperationId, cancellationToken))
            throw new InvalidOperationException("Use a new request identifier for conversion.");
        var customer = new Customer { TenantId = actor.TenantId, Name = lead.CompanyName, Email = lead.Email, CreatedAtUtc = DateTimeOffset.UtcNow };
        var opportunity = new SalesOpportunity { TenantId = actor.TenantId, Title = title, CustomerId = customer.Id, SourceLeadId = lead.Id,
            Stage = OpportunityStage.Qualified, OwnerUserId = request.OwnerUserId, ExpectedValue = request.ExpectedValue, Currency = currency,
            FollowUpAtUtc = followUp, CreatedByUserId = actor.UserId, CreatedAtUtc = DateTimeOffset.UtcNow, CreationOperationId = request.OperationId, CreationDigest = digest };
        lead.CustomerId = customer.Id; lead.Stage = LeadStage.Converted; lead.ConversionOperationId = request.OperationId; lead.ConversionDigest = digest;
        s.Add(customer); s.Add(opportunity);
        History(s, lead.Id, null, request.OperationId, digest, LeadStage.Qualified.ToString(), LeadStage.Converted.ToString());
        History(s, null, opportunity.Id, request.OperationId, digest, "", OpportunityStage.Qualified.ToString());
        // SaveChanges owns one transaction: the customer, opportunity, lead and both history rows succeed together.
        await s.CommitAsync(cancellationToken); return new(customer.Id, opportunity.Id);
    }
    public async Task ChangeLeadStageAsync(ChangeLeadStageRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken); Operation(request.OperationId);
        var reason = Text(request.Reason, 2000, false); var digest = Digest(new { request.LeadId, request.Stage, reason });
        var lead = await s.Query<SalesLead>().SingleOrDefaultAsync(x => x.Id == request.LeadId, cancellationToken) ?? throw new ArgumentException("Lead not found.");
        if (await Replayed(s, lead.Id, null, request.OperationId, digest, cancellationToken)) return;
        if (lead.Version != request.ExpectedVersion) throw new InvalidOperationException("The lead changed. Refresh before changing its stage.");
        var allowed = (lead.Stage, request.Stage) is (LeadStage.New, LeadStage.Qualified) or (LeadStage.New, LeadStage.Disqualified)
            or (LeadStage.Qualified, LeadStage.Disqualified) or (LeadStage.Disqualified, LeadStage.New);
        if (!allowed) throw new InvalidOperationException("This lead stage transition is unavailable.");
        if ((request.Stage == LeadStage.Disqualified || lead.Stage == LeadStage.Disqualified) && reason.Length == 0) throw new ArgumentException("Give a reason for disqualifying or reopening a lead.");
        History(s, lead.Id, null, request.OperationId, digest, lead.Stage.ToString(), request.Stage.ToString(), reason); lead.Stage = request.Stage;
        await s.CommitAsync(cancellationToken);
    }
    public async Task ChangeOpportunityStageAsync(ChangeOpportunityStageRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken); Operation(request.OperationId);
        var reason = Text(request.Reason, 2000, false); var digest = Digest(new { request.OpportunityId, request.Stage, reason });
        var item = await s.Query<SalesOpportunity>().SingleOrDefaultAsync(x => x.Id == request.OpportunityId, cancellationToken) ?? throw new ArgumentException("Opportunity not found.");
        if (await Replayed(s, null, item.Id, request.OperationId, digest, cancellationToken)) return;
        if (item.Version != request.ExpectedVersion) throw new InvalidOperationException("The opportunity changed. Refresh before changing its stage.");
        var allowed = (item.Stage, request.Stage) is (OpportunityStage.New, OpportunityStage.Qualified) or (OpportunityStage.Qualified, OpportunityStage.Proposal)
            or (OpportunityStage.Proposal, OpportunityStage.Negotiation) or (OpportunityStage.Negotiation, OpportunityStage.Won) or (OpportunityStage.Lost, OpportunityStage.Qualified);
        allowed |= request.Stage == OpportunityStage.Lost && item.Stage is not (OpportunityStage.Lost or OpportunityStage.Won);
        if (!allowed) throw new InvalidOperationException("This opportunity stage transition is unavailable.");
        if ((request.Stage == OpportunityStage.Lost || item.Stage == OpportunityStage.Lost) && reason.Length == 0) throw new ArgumentException("Give a reason for losing or reopening an opportunity.");
        History(s, null, item.Id, request.OperationId, digest, item.Stage.ToString(), request.Stage.ToString(), reason); item.Stage = request.Stage;
        await s.CommitAsync(cancellationToken);
    }
    private async Task<bool> Replayed(IWorkspaceSession s, Guid? lead, Guid? opportunity, Guid operation, string digest, CancellationToken ct)
    {
        var previous = await s.Query<SalesHistory>().SingleOrDefaultAsync(x => x.LeadId == lead && x.OpportunityId == opportunity && x.ActorUserId == actor.UserId && x.OperationId == operation, ct);
        if (previous is null) return false; Same(previous.RequestDigest, digest); return true;
    }
    public async Task<IReadOnlyList<SalesHistoryDto>> HistoryAsync(Guid id, bool opportunity, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken);
        if (opportunity ? !await s.Query<SalesOpportunity>().AnyAsync(x => x.Id == id, cancellationToken) : !await s.Query<SalesLead>().AnyAsync(x => x.Id == id, cancellationToken))
            throw new ArgumentException("Sales record not found.");
        return await s.Query<SalesHistory>().AsNoTracking().Where(x => opportunity ? x.OpportunityId == id : x.LeadId == id)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Take(50)
            .Select(x => new SalesHistoryDto(x.Id, x.ActorUserId, x.OccurredAtUtc, x.FromStage, x.ToStage, x.Reason)).ToListAsync(cancellationToken);
    }
}
