namespace TicketingCRM.Domain.Identity.Interfaces;
public sealed record SignInWorkspace(Guid MembershipId, Guid TenantId, string Name, string Role, Guid? CustomerId);
public sealed record SignInAccount(Guid UserId, string SecurityStamp, bool TwoFactorEnabled, IReadOnlyList<SignInWorkspace> Workspaces);
public interface ISignInDirectory
{
    Task<SignInAccount?> FindAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default);
}
