using TicketingCRM.Domain.Licensing.Models;

namespace TicketingCRM.Domain.Licensing.DTOs;

public sealed record LicenseModelDefinitionDto(
    Guid TenantId,
    Guid ProductId,
    string Name,
    IReadOnlyList<LicenseFieldDefinition> Fields);
