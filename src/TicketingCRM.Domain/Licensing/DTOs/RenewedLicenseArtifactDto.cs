namespace TicketingCRM.Domain.Licensing.DTOs;

public sealed record RenewedLicenseArtifactDto(Guid Id, int Revision, string Extension, byte[] Artifact);
