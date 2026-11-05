namespace TicketingCRM.Domain.Licensing.Models;

/// <summary>Schema definitions contain data only. Published revisions must be immutable in persistence.</summary>
public sealed record LicenseFieldDefinition(
    string AttributeId,
    string Label,
    LicenseFieldType Type,
    bool IsRequired,
    bool AllowsNull,
    LicenseValueSource Source = LicenseValueSource.Manual);
