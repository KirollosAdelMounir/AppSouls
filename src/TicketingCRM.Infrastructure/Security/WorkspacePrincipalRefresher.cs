using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TicketingCRM.Core.Security;
using TicketingCRM.Domain.Identity.Interfaces;

namespace TicketingCRM.Infrastructure.Security;

public static class WorkspacePrincipalRefresher
{
    // Identity has already validated its security stamp. Scope still requires a currently active membership.
    public static async Task RefreshAsync(SecurityStampRefreshingPrincipalContext context, ISignInDirectory directory, CancellationToken ct = default)
    {
        var previous = context.CurrentPrincipal;
        if (!Guid.TryParse(previous?.FindFirstValue(ClaimTypes.NameIdentifier), out var user) ||
            !Guid.TryParse(previous.FindFirstValue("appsouls:tenant"), out var tenant) ||
            context.NewPrincipal?.FindFirstValue(ClaimTypes.NameIdentifier) != user.ToString())
        { context.NewPrincipal = null!; return; }
        var account = await directory.FindAsync(user, previous.FindFirstValue("appsouls:stamp") ?? "", ct);
        var workspace = account?.Workspaces.SingleOrDefault(x => x.TenantId == tenant);
        var mfa = previous.HasClaim("appsouls:mfa", "true");
        if (account is null || workspace is null || account.TwoFactorEnabled && !mfa || context.NewPrincipal.Identity is not ClaimsIdentity identity)
        { context.NewPrincipal = null!; return; }
        foreach (var claim in identity.Claims.Where(c => c.Type.StartsWith("appsouls:", StringComparison.Ordinal)).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaims(WorkspaceSessionClaims.Create(workspace, account.SecurityStamp, mfa));
    }
}
