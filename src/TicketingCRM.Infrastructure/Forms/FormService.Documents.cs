using System.Runtime.CompilerServices;
using System.Text.Json;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Forms;
public sealed partial class FormService
{
    public async Task<FormDefinition> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        var form = await s.Repository<CollectionForm>().GetAllQueryable().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ArgumentException("Form not found.");
        return new(form.Id, form.Version, form.Title, form.Description, form.Status, JsonSerializer.Deserialize<FormField[]>(form.FieldsJson)!);
    }
    public async Task UpdateAsync(Guid id, UpdateFormRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        var form = await s.Repository<CollectionForm>().GetAllQueryable().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ArgumentException("Form not found.");
        if (form.Version != request.Version) throw new InvalidOperationException("Reload the latest draft before editing.");
        if (form.Status != "Draft" || form.SubmissionCount != 0) throw new InvalidOperationException("Published questions are fixed to preserve existing responses. Create a new form for a different questionnaire.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160 || request.Description is null || request.Description.Length > 2000) throw new ArgumentException("Check the title and description lengths.");
        ValidateFields(request.Fields); var json = JsonSerializer.Serialize(request.Fields);
        if (json.Length > 24000) throw new ArgumentException("The form is too large.");
        form.Title = request.Title.Trim(); form.Description = request.Description.Trim(); form.FieldsJson = json;
        await s.CommitAsync(cancellationToken);
    }
    public async Task<FormExport> ExportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken);
        await WorkspaceAccess.RequireAsync(s, actor, sales: true, ct: cancellationToken);
        var form = await s.Repository<CollectionForm>().GetAllQueryable().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ArgumentException("Form not found.");
        var fields = JsonSerializer.Deserialize<FormField[]>(form.FieldsJson)!;
        var cutoff = DateTimeOffset.UtcNow;
        var rows = Rows(s, form.Id, fields, cutoff, cancellationToken);
        return new($"form-{id:N}.xlsx", await SpreadsheetWriter.WriteAsync(rows, cancellationToken,fields.Select((field,index)=>(field,index)).Where(x=>x.field.Type is "Number" or "Rating").Select(x=>x.index+2).ToHashSet()));
    }
    private static async IAsyncEnumerable<IReadOnlyList<string>> Rows(IWorkspaceSession session, Guid id, FormField[] fields, DateTimeOffset cutoff, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new[] { "Submission ID", "Submitted at (UTC)" }.Concat(fields.Select(x => x.Label)).ToArray();
        var query = session.Repository<FormSubmission>().GetAllQueryable().Where(x => x.FormId == id && x.SubmittedAtUtc <= cutoff).OrderBy(x => x.SubmittedAtUtc).ThenBy(x => x.Id).Take(10000);
        await foreach (var row in query.Stream().WithCancellation(ct))
        {
            var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(row.AnswersJson)!;
            yield return new[] { row.Id.ToString(), row.SubmittedAtUtc.ToString("O") }.Concat(fields.Select(x => answers.GetValueOrDefault(x.Key) ?? "")).ToArray();
        }
    }
}
