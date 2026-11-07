using Microsoft.AspNetCore.Identity;
using TicketingCRM.DataConnection.Repository.Queries;

using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Accounts.DTOs;
using TicketingCRM.Domain.Accounts.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Accounts;

public sealed class AccountSettingsService(IWorkspaceSessionFactory sessions, ICurrentActor actor) : IAccountSettingsService
{
    public async Task<AccountProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await WorkspaceAccess.RequireAsync(s, actor, ct: cancellationToken);
        var user = await s.Repository<ApplicationUser>().GetAllQueryable().SingleAsync(x => x.Id == actor.UserId, cancellationToken);
        return new(user.DisplayName, user.Email ?? "", actor.Role,user.TwoFactorEnabled);
    }
    public async Task UpdateNameAsync(UpdateAccountNameRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await WorkspaceAccess.RequireAsync(s, actor, ct: cancellationToken);
        var name = request.DisplayName?.Trim() ?? ""; if (name.Length is < 1 or > 200 || name.Any(char.IsControl)) throw new ArgumentException("Enter a name of up to 200 characters.");
        var user = await s.Repository<ApplicationUser>().GetAllQueryable().SingleAsync(x => x.Id == actor.UserId, cancellationToken); user.DisplayName = name; user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await s.CommitAsync(cancellationToken);
    }
    public async Task ChangePasswordAsync(ChangeAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await WorkspaceAccess.RequireAsync(s, actor, ct: cancellationToken);
        TicketingCRM.Core.Security.PasswordPolicy.Validate(request.NewPassword);
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 1024) throw new ArgumentException("Check your current password.");
        var user = await s.Repository<ApplicationUser>().GetAllQueryable().SingleAsync(x => x.Id == actor.UserId, cancellationToken); var hasher = new PasswordHasher<ApplicationUser>();
        if (user.PasswordHash is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed) throw new ArgumentException("The current password was rejected.");
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword); user.SecurityStamp = Guid.NewGuid().ToString("N"); user.ConcurrencyStamp = Guid.NewGuid().ToString("N"); await s.CommitAsync(cancellationToken);
    }
}
