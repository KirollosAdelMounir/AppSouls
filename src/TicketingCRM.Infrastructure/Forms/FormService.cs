using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using TicketingCRM.DataConnection.Repository.Queries;

using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Forms.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Forms;

public sealed partial class FormService(IWorkspaceSessionFactory sessions, ICurrentActor actor) : IFormService
{
    public async Task<FormPage> ListAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 10000);
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        var rows = await s.Repository<CollectionForm>().GetQuery(new RepositoryQuery<CollectionForm> {
            OrderBy = query => query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id), Skip = (page - 1) * 20, Take = 21, Tracking = false }).Select(x => new FormSummary(x.Id, x.Version, x.Title, x.Status, x.PublicKey, x.SubmissionCount)).ToListAsync(cancellationToken);
        return new(rows.Take(20).ToArray(), page, rows.Count > 20);
    }
    public async Task<Guid> CreateAsync(CreateFormRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160 || request.Description is null || request.Description.Length > 2000)
            throw new ArgumentException("Enter a title up to 160 characters and description up to 2,000 characters.");
        ValidateFields(request.Fields);
        var json = JsonSerializer.Serialize(request.Fields);
        if (json.Length > 24000) throw new ArgumentException("The form is too large. Shorten the questions or choices.");
        var form = new CollectionForm { TenantId = actor.TenantId, Title = request.Title.Trim(), Description = request.Description.Trim(), FieldsJson = json,
            PublicKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(), CreatedByUserId = actor.UserId, CreatedAtUtc = DateTimeOffset.UtcNow };
        s.Repository<CollectionForm>().Add(form); await s.CommitAsync(cancellationToken); return form.Id;
    }
    public async Task ChangeStateAsync(Guid id, ChangeFormStateRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        var form = await s.Repository<CollectionForm>().GetAllQueryable().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ArgumentException("Form not found.");
        if (form.Version != request.Version) throw new InvalidOperationException("Reload the latest form before changing its state.");
        form.Status = request.AcceptingResponses ? "Published" : "Closed";
        if (request.AcceptingResponses) form.PublishedAtUtc ??= DateTimeOffset.UtcNow;
        form.StateChangedByUserId = actor.UserId; form.StateChangedAtUtc = DateTimeOffset.UtcNow;
        await s.CommitAsync(cancellationToken);
    }
    public async Task<FormSubmissionsPage> SubmissionsAsync(Guid id, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 10000);
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        var form = await s.Repository<CollectionForm>().GetAllQueryable().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ArgumentException("Form not found.");
        var rows = await s.Repository<FormSubmission>().GetAllQueryable().Where(x => x.FormId == id).OrderByDescending(x => x.SubmittedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 20).Take(21).ToListAsync(cancellationToken);
        return new(form.Title, JsonSerializer.Deserialize<FormField[]>(form.FieldsJson)!, rows.Take(20).Select(x => new FormSubmissionDto(x.Id, x.SubmittedAtUtc, JsonSerializer.Deserialize<Dictionary<string, string>>(x.AnswersJson)!)).ToArray(), page, rows.Count > 20);
    }
    private static void ValidateFields(IReadOnlyList<FormField>? fields)
    {
        if (fields is null || fields.Count is < 1 or > 20) throw new ArgumentException("Add between 1 and 20 questions.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field is null || string.IsNullOrEmpty(field.Key) || field.Key.Length > 40 || !field.Key.All(char.IsAsciiLetterOrDigit) || !keys.Add(field.Key) ||
                string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 200 || field.Type is not ("Text" or "Long text" or "Email" or "Choice" or "Number" or "Phone" or "Website" or "Rating" or "Yes / No") || field.Options is null)
                throw new ArgumentException("Each question needs a unique key, a label up to 200 characters and a supported field type.");
            if (field.Type == "Choice" ? field.Options.Length is < 2 or > 20 || field.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 120 || x != x.Trim()) || field.Options.Distinct(StringComparer.Ordinal).Count() != field.Options.Length : field.Options.Length != 0)
                throw new ArgumentException("Choice questions need 2–20 distinct choices, up to 120 characters each.");
        }
    }
}

public sealed class PublicFormService(IPublicFormSessionFactory sessions) : IPublicFormService
{
    public async Task<PublicFormDto?> GetAsync(string publicKey, CancellationToken cancellationToken = default)
    {
        if (!ValidKey(publicKey)) return null;
        await using var s = await sessions.OpenAsync(publicKey, cancellationToken);
        if (s is null) return null;
        var form = await PublishedAsync(s, publicKey, cancellationToken);
        return form is null ? null : new(form.Title, form.Description, JsonSerializer.Deserialize<FormField[]>(form.FieldsJson)!);
    }
    public async Task SubmitAsync(string publicKey, SubmitFormRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidKey(publicKey)) throw new ArgumentException("This form is unavailable.");
        await using var s = await sessions.OpenAsync(publicKey, cancellationToken);
        if (s is null) throw new ArgumentException("This form is unavailable.");
        var form = await PublishedAsync(s, publicKey, cancellationToken) ?? throw new ArgumentException("This form is unavailable.");
        if (request.OperationId == Guid.Empty || request.Answers is null || request.Answers.Count > 20) throw new ArgumentException("Check the submitted answers.");
        var fields = JsonSerializer.Deserialize<FormField[]>(form.FieldsJson)!;
        if (request.Answers.Keys.Any(key => !fields.Any(f => f.Key == key))) throw new ArgumentException("The response contains an unknown question.");
        var answers = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            var value = request.Answers.GetValueOrDefault(field.Key)?.Trim() ?? "";
            if ((field.Required && value.Length == 0) || value.Length > (field.Type == "Long text" ? 2000 : field.Type == "Email" ? 254 : 500))
                throw new ArgumentException($"Check the answer for {field.Label}.");
            if (value.Length > 0 && ((field.Type == "Email" && (!MailAddress.TryCreate(value, out var address) || address.Address != value)) || (field.Type == "Choice" && !field.Options.Contains(value, StringComparer.Ordinal))))
                throw new ArgumentException($"Check the answer for {field.Label}.");
            if (value.Length > 0 && !ValidTypedAnswer(field.Type, value)) throw new ArgumentException($"Check the answer for {field.Label}.");
            answers[field.Key] = value;
        }
        var json = JsonSerializer.Serialize(answers);
        if (json.Length > 64000) throw new ArgumentException("The response is too large. Shorten the answers.");
        var prior = await s.Repository<FormSubmission>().GetAllQueryable().SingleOrDefaultAsync(x => x.FormId == form.Id && x.OperationId == request.OperationId, cancellationToken);
        if (prior is not null)
        {
            if (prior.AnswersJson != json) throw new InvalidOperationException("This submission was already received with different answers.");
            return;
        }
        if (form.SubmissionCount >= 10000) throw new ArgumentException("This form has reached its response limit.");
        // Updating the form serializes receipt against closure and bounds the response quota.
        form.SubmissionCount++;
        s.Repository<FormSubmission>().Add(new FormSubmission { TenantId = form.TenantId, FormId = form.Id, OperationId = request.OperationId, AnswersJson = json, SubmittedAtUtc = DateTimeOffset.UtcNow });
        await s.CommitAsync(cancellationToken);
    }
    private static bool ValidTypedAnswer(string type, string value) => type switch
    {
        "Number" => decimal.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out _),
        "Rating" => int.TryParse(value, out var rating) && rating is >= 1 and <= 5,
        "Yes / No" => value is "Yes" or "No",
        "Website" => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo),
        "Phone" => value.Length is >= 3 and <= 50 && value.All(c => char.IsAsciiDigit(c) || "+- ().".Contains(c)),
        _ => true
    };
    private static bool ValidKey(string? key) => key is { Length: 64 } && key.All(c => char.IsAsciiHexDigitLower(c));
    private static Task<CollectionForm?> PublishedAsync(IWorkspaceSession s, string key, CancellationToken ct) => s.Repository<CollectionForm>().GetAllQueryable()
        .Where(x => x.PublicKey == key && x.Status == "Published" && s.Repository<Tenant>().GetAllQueryable().Any(t => t.Id == x.TenantId && t.IsActive)).SingleOrDefaultAsync(ct);
}
