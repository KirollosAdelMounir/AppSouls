using TicketingCRM.Domain.Workspace.DTOs;
namespace TicketingCRM.Domain.Workspace.Interfaces;
public interface IWorkspaceService
{
    Task<ContractDto> ContractAsync(Guid id,CancellationToken cancellationToken=default);
    Task<WorkspaceSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ActivateContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> GrantEntitlementAsync(GrantEntitlementRequest request, CancellationToken cancellationToken = default);
}
