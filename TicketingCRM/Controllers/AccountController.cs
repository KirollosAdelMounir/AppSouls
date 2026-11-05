using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Identity.Interfaces;
namespace TicketingCRM.Controllers;

[Route("account"), ApiExplorerSettings(IgnoreApi = true), AutoValidateAntiforgeryToken]
public sealed class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ISignInDirectory directory) : ControllerBase
{
    public const string PendingScheme = "AppSouls.PendingSignIn";
    [HttpPost("sign-in"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> SignIn()
    {
        await HttpContext.SignOutAsync(PendingScheme);
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var email = form["Email"].ToString().Trim(); var password = form["Password"].ToString();
        if (email.Length > 254 || password.Length is < 1 or > 1024) return Failure();
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed || !(await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded) return Failure();
        var account = await directory.FindAsync(user.Id, user.SecurityStamp ?? "", HttpContext.RequestAborted);
        if (account is null) return Failure();
        var returnUrl = LocalReturn(form["ReturnUrl"].ToString());
        if (!account.TwoFactorEnabled && account.Workspaces.Count == 1) return await Complete(user, account.Workspaces[0], returnUrl, false);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim("appsouls:stamp", account.SecurityStamp),
            new Claim("appsouls:return", returnUrl)
        }, PendingScheme));
        await HttpContext.SignInAsync(PendingScheme, principal, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
        return LocalRedirect("/account/continue");
    }
    [HttpPost("complete-sign-in"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Continue()
    {
        var pending = await HttpContext.AuthenticateAsync(PendingScheme);
        if (!pending.Succeeded || !Guid.TryParse(pending.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Failure();
        var account = await directory.FindAsync(id, pending.Principal.FindFirstValue("appsouls:stamp") ?? "", HttpContext.RequestAborted);
        var user = await users.FindByIdAsync(id.ToString());
        if (account is null || user is null) return Failure();
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var verifiedMfa = pending.Principal.HasClaim("appsouls:mfa", "true");
        if (account.TwoFactorEnabled && !verifiedMfa)
        {
            var code = form["Code"].ToString(); var recovery = form["Recovery"].ToString();
            if (code.Length > 32 || recovery.Length > 100) return Failure();
            var valid = string.IsNullOrEmpty(recovery)
                ? await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code.Replace(" ", "").Replace("-", ""))
                : (await users.RedeemTwoFactorRecoveryCodeAsync(user, recovery.Replace(" ", ""))).Succeeded;
            if (!valid) { await users.AccessFailedAsync(user); return Failure(); }
            verifiedMfa = true;
        }
        var selected = form["MembershipId"].ToString();
        var workspace = account.Workspaces.Count == 1 ? account.Workspaces[0] : account.Workspaces.SingleOrDefault(x => x.MembershipId.ToString() == selected);
        if (workspace is null)
        {
            // Preserve the original five-minute expiry across optional MFA and workspace choice.
            if (verifiedMfa && !pending.Principal.HasClaim("appsouls:mfa", "true"))
            {
                ((ClaimsIdentity)pending.Principal.Identity!).AddClaim(new("appsouls:mfa", "true"));
                await HttpContext.SignInAsync(PendingScheme, pending.Principal, pending.Properties);
            }
            return LocalRedirect("/account/continue");
        }
        return await Complete(user, workspace, LocalReturn(pending.Principal.FindFirstValue("appsouls:return") ?? "/"), verifiedMfa);
    }
    private async Task<IActionResult> Complete(ApplicationUser user, SignInWorkspace workspace, string returnUrl, bool mfa)
    {
        var claims = new List<Claim> { new("appsouls:tenant", workspace.TenantId.ToString()), new("appsouls:role", workspace.Role), new("appsouls:stamp", user.SecurityStamp ?? "") };
        if (mfa) claims.Add(new("appsouls:mfa", "true"));
        if (workspace.CustomerId is { } customer) claims.Add(new("appsouls:customer", customer.ToString()));
        await signIn.SignInWithClaimsAsync(user, isPersistent: false, claims);
        await HttpContext.SignOutAsync(PendingScheme);
        return LocalRedirect(returnUrl);
    }
    private static string LocalReturn(string value) => value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\') ? value : "/";
    private IActionResult Failure() => LocalRedirect("/account/login?error=invalid");
    [HttpPost("sign-out"), Authorize]
    public async Task<IActionResult> SignOutAccount()
    {
        await signIn.SignOutAsync(); await HttpContext.SignOutAsync(PendingScheme);
        return LocalRedirect("/account/login");
    }
}
