using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.DataConnection.Identity;
namespace TicketingCRM.Controllers;

[Route("account/mfa")]
[Authorize]
[EnableRateLimiting("login")]
[AutoValidateAntiforgeryToken]
[ApiExplorerSettings(IgnoreApi=true)]
public sealed class MfaController(IAntiforgery antiforgery, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : ControllerBase
{
    [HttpPost("begin")]
    public async Task<IActionResult> Begin()
    {
        var context=HttpContext;
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var user = await users.GetUserAsync(context.User);
            if (user is null || !await CheckPassword(user, form["Password"].ToString(), signIn)) return Failure(context);
            if (await users.GetTwoFactorEnabledAsync(user)) return Failure(context);
            if (!(await users.ResetAuthenticatorKeyAsync(user)).Succeeded) return Failure(context);
            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key)) return Failure(context);
            var token = antiforgery.GetAndStoreTokens(context);
            var body = $"<p>Add an account manually in your authenticator app. Choose a time-based code.</p><p><strong>Account:</strong> {Encode(user.Email ?? user.UserName ?? "App Souls")}</p><p><strong>Setup key:</strong></p><p class=\"feedback\"><code>{Encode(key)}</code></p><p>Keep this key private. Enter a current code to finish setup.</p><form method=\"post\" action=\"/account/mfa/enable\" data-enhance=\"false\">{Token(token)}{Password()}<div class=\"soul-field\"><label for=\"code\">Authenticator code</label><input class=\"soul-input\" id=\"code\" name=\"Code\" required inputmode=\"numeric\" maxlength=\"12\" autocomplete=\"one-time-code\" /></div><button class=\"button primary\" type=\"submit\">Enable two-step verification</button></form>";
            return Page(context, "Connect your authenticator", body);
    }
    [HttpPost("enable")]
    public async Task<IActionResult> Enable()
    {
        var context=HttpContext;
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var user = await users.GetUserAsync(context.User);
            if (user is null || await users.GetTwoFactorEnabledAsync(user) || !await CheckPassword(user, form["Password"].ToString(), signIn)) return Failure(context);
            var code = NormalizeCode(form["Code"].ToString());
            if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code))
            { await users.AccessFailedAsync(user); return Failure(context); }
            if (!(await users.SetTwoFactorEnabledAsync(user, true)).Succeeded) return Failure(context);
            var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            await users.UpdateSecurityStampAsync(user);
            await signIn.SignOutAsync();
            return RecoveryPage(context, codes, "Two-step verification enabled");
    }
    [HttpPost("recovery-codes")]
    public async Task<IActionResult> ReplaceRecoveryCodes()
    {
        var context=HttpContext;
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var user = await users.GetUserAsync(context.User);
            if (user is null || !await users.GetTwoFactorEnabledAsync(user) || !await CheckPassword(user, form["Password"].ToString(), signIn)) return Failure(context);
            if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(form["Code"].ToString())))
            { await users.AccessFailedAsync(user); return Failure(context); }
            var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            return RecoveryPage(context, codes, "New recovery codes");
    }
    private static async Task<bool> CheckPassword(ApplicationUser user, string password, SignInManager<ApplicationUser> signIn) =>
        password.Length is > 0 and <= 1024 && (await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded;
    private static string NormalizeCode(string code) => code.Length <= 32 ? code.Replace(" ", "").Replace("-", "") : "";
    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
    private static string Password() => "<div class=\"soul-field\"><label for=\"password\">Current password</label><input class=\"soul-input\" id=\"password\" type=\"password\" name=\"Password\" required maxlength=\"1024\" autocomplete=\"current-password\" /></div>";
    private static string Token(AntiforgeryTokenSet token) => $"<input type=\"hidden\" name=\"{Encode(token.FormFieldName)}\" value=\"{Encode(token.RequestToken ?? "")}\" />";
    private static IActionResult Failure(HttpContext context) => Page(context, "Verification could not be completed", "<p>Check your password and authenticator code, then try again. An enabled authenticator cannot be reset here.</p><a class=\"button secondary\" href=\"/account/security\">Return to account security</a>");
    private static IActionResult RecoveryPage(HttpContext context, IEnumerable<string>? codes, string title)
    {
        var items = codes?.Select(x => $"<li><code>{Encode(x)}</code></li>") ?? [];
        return Page(context, title, "<p>Save these recovery codes in a private password manager. Each code can be used once. Previous recovery codes are no longer valid.</p><ul>" + string.Join("", items) + "</ul><p>These codes are shown only in this response.</p><a class=\"button primary\" href=\"/account/login\">Continue to sign in</a>");
    }
    private static IActionResult Page(HttpContext context, string title, string body)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return new ContentResult { Content = $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{Encode(title)} | App Souls</title><script src=\"/theme.js\"></script><link rel=\"stylesheet\" href=\"/app.css\"><link rel=\"stylesheet\" href=\"/TicketingCRM.styles.css\"></head><body><main class=\"account-shell\"><section class=\"panel account-panel page-enter\"><p class=\"eyebrow\">APP SOULS ACCOUNT SECURITY</p><h1>{Encode(title)}</h1>{body}</section></main></body></html>", ContentType = "text/html; charset=utf-8", StatusCode = 200 };
    }
}
