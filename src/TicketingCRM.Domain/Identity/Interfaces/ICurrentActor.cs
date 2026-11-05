namespace TicketingCRM.Domain.Identity.Interfaces;

public interface ICurrentActor
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid? CustomerId { get; }
    string Role { get; }
    // Non-interactive internal callers may omit this. Authenticated host sessions always provide a claim snapshot.
    string? SecurityStamp => null;
}
