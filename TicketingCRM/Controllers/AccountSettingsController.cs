using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Accounts.DTOs;
using TicketingCRM.Domain.Accounts.Interfaces;
namespace TicketingCRM.Controllers;
[Authorize,Route("account/settings"),AutoValidateAntiforgeryToken,EnableRateLimiting("login"),ApiExplorerSettings(IgnoreApi=true)]
public sealed class AccountSettingsController(IAccountSettingsService accounts):ControllerBase
{
    [HttpPost("name")]
    public async Task<IActionResult> Name([FromForm]UpdateAccountNameRequest request,CancellationToken ct)
    {try{await accounts.UpdateNameAsync(request,ct);return LocalRedirect("/account/settings?result=saved");}catch(ArgumentException){return LocalRedirect("/account/settings?result=invalid");}}
    [HttpPost("password")]
    public async Task<IActionResult> Password([FromForm]ChangeAccountPasswordRequest request,[FromForm]string ConfirmPassword,CancellationToken ct)
    {
        if(request.NewPassword!=ConfirmPassword)return LocalRedirect("/account/settings/password?result=invalid");
        try{await accounts.ChangePasswordAsync(request,ct);await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);return LocalRedirect("/account/login?passwordChanged=true");}
        catch(ArgumentException){return LocalRedirect("/account/settings/password?result=invalid");}
    }
}
