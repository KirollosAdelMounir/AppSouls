using TicketingCRM.Domain.Licensing.DTOs;

namespace TicketingCRM.Domain.Licensing.Interfaces;

public interface IMachineLicenseLookup
{
    Task<RenewedLicenseArtifactDto?> FindRenewalAsync(string credentialToken, int currentRevision, CancellationToken cancellationToken = default);
}
