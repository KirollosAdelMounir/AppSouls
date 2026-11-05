using TicketingCRM.Domain.Sales.Models;
namespace TicketingCRM.Domain.Sales.DTOs;
public sealed record SalesOwnerDto(Guid UserId, string Name, string Email);
public sealed record LeadDto(Guid Id, Guid Version, string ContactName, string CompanyName, string Email,
    string Phone, string Source, string Notes, LeadStage Stage, Guid OwnerUserId, DateTimeOffset? FollowUpAtUtc, Guid? CustomerId);
public sealed record OpportunityDto(Guid Id, Guid Version, string Title, Guid CustomerId, string CustomerName,
    OpportunityStage Stage, Guid OwnerUserId, decimal ExpectedValue, string Currency, DateTimeOffset? FollowUpAtUtc);
public sealed record SalesPipelinePage(IReadOnlyList<LeadDto> Leads, IReadOnlyList<OpportunityDto> Opportunities,
    IReadOnlyList<SalesOwnerDto> Owners, int LeadCount, int OpportunityCount, int Page, int PageSize);
public sealed record SalesHistoryDto(Guid Id, Guid ActorUserId, DateTimeOffset OccurredAtUtc, string FromStage, string ToStage, string Reason);
public sealed record CreateLeadRequest(Guid OperationId, string ContactName, string CompanyName, string Email,
    string Phone, string Source, string Notes, Guid OwnerUserId, DateTimeOffset? FollowUpAtUtc);
public sealed record CreateOpportunityRequest(Guid OperationId, string Title, Guid CustomerId, Guid OwnerUserId,
    decimal ExpectedValue, string Currency, DateTimeOffset? FollowUpAtUtc);
public sealed record ConvertLeadRequest(Guid OperationId, Guid LeadId, Guid ExpectedVersion, string OpportunityTitle,
    Guid OwnerUserId, decimal ExpectedValue, string Currency, DateTimeOffset? FollowUpAtUtc);
public sealed record LeadConversionReceipt(Guid CustomerId, Guid OpportunityId);
public sealed record ChangeLeadStageRequest(Guid OperationId, Guid LeadId, Guid ExpectedVersion, LeadStage Stage, string Reason);
public sealed record ChangeOpportunityStageRequest(Guid OperationId, Guid OpportunityId, Guid ExpectedVersion, OpportunityStage Stage, string Reason);
public sealed record RecordSalesActivityRequest(Guid OperationId,Guid RecordId,bool IsOpportunity,Guid ExpectedVersion,string Kind,string Subject,string Notes,DateTimeOffset? FollowUpAtUtc);
