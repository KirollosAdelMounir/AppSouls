using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.Controllers;

[ApiController]
[Route("api/workspace")]
[Authorize]
[EnableRateLimiting("licensing")]
[AutoValidateAntiforgeryToken]
public sealed class WorkspaceController(IWorkspaceService service) : ControllerBase
{
    /// <summary>Read the current member's authorized workspace records.</summary>
    [HttpGet]
    [ProducesResponseType<WorkspaceSnapshot>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceSnapshot>> Get(CancellationToken cancellationToken)=>Ok(await service.GetAsync(cancellationToken));
    [HttpPost("customers")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedRecordDto>> CreateCustomer(CreateCustomerRequest request,CancellationToken cancellationToken)=>Created("/customers",new CreatedRecordDto(await service.CreateCustomerAsync(request,cancellationToken)));
    [HttpPost("products")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedRecordDto>> CreateProduct(CreateProductRequest request,CancellationToken cancellationToken)=>Created("/products",new CreatedRecordDto(await service.CreateProductAsync(request,cancellationToken)));
    [HttpPost("contracts")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedRecordDto>> CreateContract(CreateContractRequest request,CancellationToken cancellationToken)=>Created("/contracts",new CreatedRecordDto(await service.CreateContractAsync(request,cancellationToken)));
    [HttpPost("contracts/{id:guid}/activate")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatedRecordDto>> ActivateContract(Guid id,CancellationToken cancellationToken)=>Ok(new CreatedRecordDto(await service.ActivateContractAsync(id,cancellationToken)));
    [HttpPost("entitlements")]
    [ProducesResponseType<CreatedRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedRecordDto>> GrantEntitlement(GrantEntitlementRequest request,CancellationToken cancellationToken)=>Created("/licenses",new CreatedRecordDto(await service.GrantEntitlementAsync(request,cancellationToken)));
}
/// <summary>The identifier of a created or updated record.</summary>
public sealed record CreatedRecordDto(Guid Id);
