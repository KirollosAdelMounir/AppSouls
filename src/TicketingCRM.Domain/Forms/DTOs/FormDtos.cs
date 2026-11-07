namespace TicketingCRM.Domain.Forms.DTOs;

public sealed record FormField(string Key, string Label, string Type, bool Required, string[] Options);
public sealed record CreateFormRequest(string Title, string Description, IReadOnlyList<FormField> Fields);
public sealed record ChangeFormStateRequest(Guid Version, bool AcceptingResponses);
public sealed record FormSummary(Guid Id, Guid Version, string Title, string Status, string PublicKey, int SubmissionCount);
public sealed record FormPage(IReadOnlyList<FormSummary> Items, int Page, bool HasMore);
public sealed record PublicFormDto(string Title, string Description, IReadOnlyList<FormField> Fields);
public sealed record SubmitFormRequest(Guid OperationId, Dictionary<string, string> Answers);
public sealed record FormSubmissionDto(Guid Id, DateTimeOffset SubmittedAtUtc, Dictionary<string, string> Answers);
public sealed record FormSubmissionsPage(string Title, IReadOnlyList<FormField> Fields, IReadOnlyList<FormSubmissionDto> Items, int Page, bool HasMore);
public sealed record FormDefinition(Guid Id, Guid Version, string Title, string Description, string Status, IReadOnlyList<FormField> Fields);
public sealed record UpdateFormRequest(Guid Version, string Title, string Description, IReadOnlyList<FormField> Fields);
public sealed record FormExport(string Filename, Stream Content);
