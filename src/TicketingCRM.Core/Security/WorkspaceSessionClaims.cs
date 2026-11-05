using System.Security.Claims;
using TicketingCRM.Domain.Identity.Interfaces;

namespace TicketingCRM.Core.Security;

public static class WorkspaceSessionClaims
{
    public static IReadOnlyList<Claim> Create(SignInWorkspace workspace, string securityStamp, bool mfaVerified)
    {
        var claims = new List<Claim> { new("appsouls:tenant", workspace.TenantId.ToString()), new("appsouls:role", workspace.Role), new("appsouls:stamp", securityStamp) };
        if (workspace.CustomerId is { } customer) claims.Add(new("appsouls:customer", customer.ToString()));
        if (mfaVerified) claims.Add(new("appsouls:mfa", "true"));
        return claims;
    }
}
