using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Interfaces;

namespace TicketingCRM.Infrastructure.Licensing;

/// <summary>Initial metadata checks. Full schema publication validation belongs to a later milestone.</summary>
internal sealed class LicenseModelValidator : ILicenseModelValidator
{
    public IReadOnlyList<string> Validate(LicenseModelDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        if (definition.TenantId == Guid.Empty) errors.Add("A tenant is required.");
        if (definition.ProductId == Guid.Empty) errors.Add("A product is required.");
        if (string.IsNullOrWhiteSpace(definition.Name)) errors.Add("A model name is required.");
        if (definition.Fields is null)
        {
            errors.Add("A field collection is required.");
            return errors;
        }
        var attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields)
        {
            if (field is null)
            {
                errors.Add("Fields cannot contain null entries.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(field.AttributeId)) errors.Add("Each field requires a stable attribute ID.");
            else if (!attributes.Add(field.AttributeId)) errors.Add($"Duplicate attribute ID: {field.AttributeId}.");
            if (string.IsNullOrWhiteSpace(field.Label)) errors.Add("Each field requires a label.");
            if (!Enum.IsDefined(field.Type)) errors.Add("The field type is unsupported.");
        }
        return errors;
    }
}
