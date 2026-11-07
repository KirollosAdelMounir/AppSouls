using TicketingCRM.Domain.Support.Models;

namespace TicketingCRM.Domain.Support.DTOs;

public sealed record ReportProgress(
    Guid TicketId, string Reference, string Title, SupportStage Stage,
    DateTimeOffset SubmittedAt, Guid Version, IReadOnlyList<ReportStageHistory> History, string Urgency = "Medium");

// Customer-facing history only; staff identities and internal notes stay private.
public sealed record ReportStageHistory(
    SupportStage Stage, DateTimeOffset OccurredAt, string CustomerVisibleMessage);

public sealed record ReportSubmissionReceipt(
    Guid TicketId, string Reference, SupportStage Stage, bool AcknowledgementQueued);

public sealed record ChangeReportStageRequest(
    Guid TicketId, Guid OperationId, Guid ExpectedVersion,
    SupportStage Stage, Guid? AssignedTechnicalMemberId, string CustomerVisibleMessage,
    string? Resolution, string? ClosureReason);
