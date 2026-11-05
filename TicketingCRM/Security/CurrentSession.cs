using System.Security.Claims;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Tenancy.Interfaces;

namespace TicketingCRM.Security;

// Snapshot the authenticated principal when the request/circuit scope is created.
// Business services independently recheck active membership on each operation.
public sealed class CurrentSession(IHttpContextAccessor accessor) : ICurrentActor, ITenantContext
{
    private ClaimsPrincipal? snapshot;
    private ClaimsPrincipal principal
    {
        get
        {
            // Identity can construct its store while cookie authentication is still running.
            // Never freeze that temporary anonymous principal for the request/circuit.
            if(snapshot is null && accessor.HttpContext?.User is { Identity.IsAuthenticated: true } authenticated)
                snapshot=authenticated;
            return snapshot??new ClaimsPrincipal();
        }
    }
    public Guid UserId => Parse(ClaimTypes.NameIdentifier);
    public Guid TenantId => Parse("appsouls:tenant");
    public Guid? CustomerId => Guid.TryParse(principal.FindFirstValue("appsouls:customer"),out var value)?value:null;
    public string? SecurityStamp => principal.FindFirstValue("appsouls:stamp") ?? "";
    public string Role => principal.FindFirstValue("appsouls:role")??"";
    private Guid Parse(string claim)=>principal.Identity?.IsAuthenticated==true&&Guid.TryParse(principal.FindFirstValue(claim),out var value)?value:Guid.Empty;
}
public sealed record FixedTenant(Guid TenantId) : ITenantContext;
