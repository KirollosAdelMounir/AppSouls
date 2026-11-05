namespace TicketingCRM.Domain.Workspace.DTOs;

public sealed record CustomerDto(Guid Id, string Name, string Email, string CompanyNumber);
public sealed record ProductDto(Guid Id, string Name, string Code, string LicenseExtension);
public sealed record ContractDto(Guid Id, Guid CustomerId, string Reference, string Status, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);
public sealed record EntitlementDto(Guid Id, Guid CustomerId, Guid ProductId, Guid ContractId, int InstallationLimit, DateTimeOffset StartsAtUtc, DateTimeOffset? ExpiresAtUtc);
public sealed record WorkspaceSnapshot(IReadOnlyList<CustomerDto> Customers, IReadOnlyList<ProductDto> Products, IReadOnlyList<ContractDto> Contracts, IReadOnlyList<EntitlementDto> Entitlements);
public sealed record CreateCustomerRequest(string Name, string Email, string CompanyNumber);
public sealed record CreateProductRequest(string Name, string Code, string LicenseExtension);
public sealed record CreateContractRequest(Guid CustomerId, string Reference, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);
public sealed record GrantEntitlementRequest(Guid ContractId, Guid ProductId, int InstallationLimit, DateTimeOffset? ExpiresAtUtc);
