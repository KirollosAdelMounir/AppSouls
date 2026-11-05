using TicketingCRM.Domain.Licensing.DTOs;
namespace TicketingCRM.Domain.Licensing.Interfaces;
public interface ILicensingService
{
    Task<LicensingSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<Guid> PublishSchemaAsync(PublishLicenseSchemaRequest request, CancellationToken cancellationToken = default);
    Task<InstallationEnrollment> EnrollAsync(EnrollInstallationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> IssueAsync(IssueLicenseRequest request, CancellationToken cancellationToken = default);
    Task<LicenseDownload> DownloadAsync(Guid revisionId, CancellationToken cancellationToken = default);
    Task<LicenseDownload> ExportModelAsync(Guid schemaId, CancellationToken cancellationToken = default);
}
