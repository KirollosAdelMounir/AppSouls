using TicketingCRM.Domain.Support.Models;

namespace TicketingCRM.Domain.Persistence.Models;

public sealed class SupportTicket : TenantRecord
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ReporterUserId { get; set; }
    public Guid OperationId { get; set; }
    public string Reference { get; set; } = "";
    public SupportReportKind Kind { get; set; }
    public SupportStage Stage { get; set; } = SupportStage.Submitted;
    public string Urgency {get;set;} = "Medium";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string StepsToReproduce { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string ActualResult { get; set; } = "";
    public string ApplicationVersion { get; set; } = "";
    public string Environment { get; set; } = "";
    public Guid? AssignedTechnicalUserId { get; set; }
    public string? Resolution { get; set; }
    public string? ClosureReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
