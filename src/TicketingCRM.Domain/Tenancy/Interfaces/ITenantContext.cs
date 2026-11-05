namespace TicketingCRM.Domain.Tenancy.Interfaces;

/// <summary>Resolved from authenticated membership, never from untrusted request data alone.</summary>
public interface ITenantContext
{
    Guid TenantId { get; }
}
