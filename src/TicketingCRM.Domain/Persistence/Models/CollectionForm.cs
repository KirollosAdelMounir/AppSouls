namespace TicketingCRM.Domain.Persistence.Models;

public sealed class CollectionForm : TenantRecord
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string FieldsJson { get; set; } = "[]";
    public string PublicKey { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public int SubmissionCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public Guid? StateChangedByUserId { get; set; }
    public DateTimeOffset? StateChangedAtUtc { get; set; }
}
public sealed class FormSubmission : TenantRecord
{
    public Guid FormId { get; set; }
    public Guid OperationId { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public DateTimeOffset SubmittedAtUtc { get; set; }
}
