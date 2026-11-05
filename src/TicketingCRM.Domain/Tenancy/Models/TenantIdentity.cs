namespace TicketingCRM.Domain.Tenancy.Models;

public sealed record TenantIdentity(Guid TenantId, string DisplayName);
