using TicketingCRM.Domain.Administration.DTOs;
namespace TicketingCRM.Domain.Administration.Interfaces;

public interface IUserAdministrationService
{
    Task<IReadOnlyList<ManagedUserDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateManagedUserRequest request, CancellationToken cancellationToken = default);
    Task ChangeAccessAsync(ChangeMemberAccessRequest request, CancellationToken cancellationToken = default);
}
public interface IInitialAdministratorService
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(InitialAdministratorRequest request, CancellationToken cancellationToken = default);
}
