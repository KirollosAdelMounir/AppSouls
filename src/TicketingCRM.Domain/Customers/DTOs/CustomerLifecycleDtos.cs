using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Sales.DTOs;
namespace TicketingCRM.Domain.Customers.DTOs;
public sealed record CustomerActivityDto(Guid Id,string Kind,string Subject,string Notes,DateTimeOffset OccurredAtUtc,DateTimeOffset? FollowUpAtUtc);
public sealed record CustomerSupportDto(Guid Id,string Reference,string Title,string Stage);
public sealed record CustomerInvitationDto(Guid Id,DateTimeOffset ExpiresAtUtc,bool Accepted);
public sealed record CustomerLifecycleSnapshot(CustomerDto Customer,IReadOnlyList<OpportunityDto> Opportunities,IReadOnlyList<ContractDto> Contracts,IReadOnlyList<EntitlementDto> Subscriptions,IReadOnlyList<CustomerActivityDto> Activities,IReadOnlyList<CustomerSupportDto> Support,IReadOnlyList<CustomerInvitationDto> Invitations,bool CanManage,IReadOnlyList<CustomerSoftwareDto>? Software = null,IReadOnlyList<CustomerLicenseDto>? Licenses = null,bool CanRecordActivity = false);
public sealed record RecordCustomerActivityRequest(Guid OperationId,Guid CustomerId,string Kind,string Subject,string Notes,DateTimeOffset? FollowUpAtUtc);
public sealed record FinalizeCustomerSaleRequest(Guid OperationId,Guid CustomerId,Guid OpportunityId,Guid ContractId);
public sealed record AcceptCustomerInvitationRequest(Guid InvitationId,string Token,string Password);
public sealed record ReissueCustomerInvitationRequest(Guid OperationId,Guid InvitationId);

public sealed record CustomerSoftwareDto(Guid EntitlementId,Guid ProductId,string Name,string ProductCode,string ContractReference,DateTimeOffset StartsAtUtc,DateTimeOffset? LicenseExpiresAtUtc,DateTimeOffset? SupportExpiresAtUtc,int InstallationLimit,bool Revoked);
public sealed record CustomerLicenseDto(Guid Id,string ProductName,string InstallationName,int Revision,string Status,DateTimeOffset IssuedAtUtc,DateTimeOffset? ExpiresAtUtc);
