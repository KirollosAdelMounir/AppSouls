using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Customers.Interfaces;
namespace TicketingCRM.Controllers;
[Route("account/activate"),AllowAnonymous,AutoValidateAntiforgeryToken,EnableRateLimiting("login"),ApiExplorerSettings(IgnoreApi=true)]
public sealed class CustomerInvitationController(ICustomerInvitationService invitations):ControllerBase
{
 [HttpPost]
 public async Task<IActionResult> Accept([FromForm]AcceptCustomerInvitationRequest request,[FromForm]string ConfirmPassword,CancellationToken ct)
 {
  Response.Headers.CacheControl="no-store";
  if(request.Password!=ConfirmPassword)return LocalRedirect("/account/activate?error=invalid");
  try{await invitations.AcceptAsync(request,ct);return LocalRedirect("/account/login?activated=true");}
  catch(ArgumentException){return LocalRedirect("/account/activate?error=invalid");}
 }
}
