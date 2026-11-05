using TicketingCRM.Domain.Support.Models;

namespace TicketingCRM.Domain.Support.DTOs;

// Tenant, reporter and customer are resolved by the service from authorized identity.
public sealed record SubmitReportRequest(
    Guid OperationId,
    Guid ProductId,
    SupportReportKind Kind,
    string Title,
    string Description,
    string StepsToReproduce,
    string ExpectedResult,
    string ActualResult,
    string ApplicationVersion,
    string Environment);
