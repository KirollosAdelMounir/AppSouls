using TicketingCRM.Domain.Accounts.DTOs;
namespace TicketingCRM.Domain.Accounts.Interfaces;
public interface IAccountSettingsService
{
    Task<AccountProfile> GetAsync(CancellationToken cancellationToken=default);
    Task UpdateNameAsync(UpdateAccountNameRequest request,CancellationToken cancellationToken=default);
    Task ChangePasswordAsync(ChangeAccountPasswordRequest request,CancellationToken cancellationToken=default);
}
