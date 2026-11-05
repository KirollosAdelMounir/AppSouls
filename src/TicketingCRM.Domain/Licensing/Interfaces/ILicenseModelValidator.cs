using TicketingCRM.Domain.Licensing.DTOs;

namespace TicketingCRM.Domain.Licensing.Interfaces;

public interface ILicenseModelValidator
{
    IReadOnlyList<string> Validate(LicenseModelDefinitionDto definition);
}
