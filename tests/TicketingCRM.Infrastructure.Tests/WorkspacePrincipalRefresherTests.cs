using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Infrastructure.Security;
using Xunit;

namespace TicketingCRM.Infrastructure.Tests;

public sealed class WorkspacePrincipalRefresherTests
{
    private static SecurityStampRefreshingPrincipalContext Context(Guid user, Guid tenant, bool mfa)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()), new("appsouls:tenant", tenant.ToString()), new("appsouls:stamp", "valid-stamp"), new("appsouls:role", "forged-old-role") };
        if (mfa) claims.Add(new("appsouls:mfa", "true"));
        return new() { CurrentPrincipal = new(new ClaimsIdentity(claims, "Identity.Application")), NewPrincipal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "Identity.Application")) };
    }
    [Fact]
    public async Task RefreshRevalidatesSelectedMembershipAndRestoresCurrentScopeAndVerifiedMfa()
    {
        var user = Guid.NewGuid(); var tenant = Guid.NewGuid(); var customer = Guid.NewGuid(); var context = Context(user, tenant, true);
        var directory = new Directory(new(user, "valid-stamp", true, [new(Guid.NewGuid(), tenant, "Organization", "Customer", customer)]));
        await WorkspacePrincipalRefresher.RefreshAsync(context, directory);
        var principal = Assert.IsType<ClaimsPrincipal>(context.NewPrincipal);
        Assert.Equal(user, directory.SeenUser); Assert.Equal("valid-stamp", directory.SeenStamp);
        Assert.Equal(tenant.ToString(), principal.FindFirstValue("appsouls:tenant"));
        Assert.Equal("Customer", principal.FindFirstValue("appsouls:role"));
        Assert.Equal(customer.ToString(), principal.FindFirstValue("appsouls:customer"));
        Assert.True(principal.HasClaim("appsouls:mfa", "true"));
        Assert.DoesNotContain(principal.Claims, c => c.Value == "forged-old-role");
    }
    [Theory]
    [InlineData("revoked")]
    [InlineData("other-tenant")]
    [InlineData("mfa-required")]
    public async Task RevokedForeignOrUnverifiedMfaSessionsAreRejected(string reason)
    {
        var user = Guid.NewGuid(); var tenant = Guid.NewGuid(); var context = Context(user, tenant, false);
        SignInAccount? account = reason == "revoked" ? null : new(user, "valid-stamp", reason == "mfa-required",
            [new(Guid.NewGuid(), reason == "other-tenant" ? Guid.NewGuid() : tenant, "Organization", "Owner", null)]);
        await WorkspacePrincipalRefresher.RefreshAsync(context, new Directory(account));
        Assert.Null(context.NewPrincipal);
    }
    private sealed class Directory(SignInAccount? account) : ISignInDirectory
    {
        public Guid SeenUser; public string? SeenStamp;
        public Task<SignInAccount?> FindAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default)
        { SeenUser = userId; SeenStamp = securityStamp; return Task.FromResult(account); }
    }
}
