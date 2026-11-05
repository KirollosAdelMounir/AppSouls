using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Customers.Interfaces;
namespace TicketingCRM.Controllers;
[ApiController,Authorize,Route("api/customer-lifecycle"),AutoValidateAntiforgeryToken,EnableRateLimiting("licensing"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CustomerLifecycleController(ICustomerLifecycleService service):ControllerBase
{
 [HttpGet("{id:guid}")]
 public Task<CustomerLifecycleSnapshot> Get(Guid id,CancellationToken ct)=>service.GetAsync(id,ct);
 [HttpPost("activities")]
 public async Task<IActionResult> Record(RecordCustomerActivityRequest request,CancellationToken ct){await service.RecordActivityAsync(request,ct);return NoContent();}
 [HttpPost("invitations/reissue")]
 public async Task<IActionResult> Reissue(ReissueCustomerInvitationRequest request,CancellationToken ct){await service.ReissueInvitationAsync(request,ct);return NoContent();}
 [HttpPost("finalize")]
 public async Task<IActionResult> FinalizeSale(FinalizeCustomerSaleRequest request,CancellationToken ct){await service.FinalizeSaleAsync(request,ct);return NoContent();}
}
