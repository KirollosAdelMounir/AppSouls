using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Interfaces;

namespace TicketingCRM.DataConnection.Repository;

/// <summary>Audited machine-authentication path. Scope comes solely from possession of a high entropy credential.</summary>
public sealed class MachineLicenseLookup(IDbContextFactory<AppDbContext> factory) : IMachineLicenseLookup
{
    public async Task<RenewedLicenseArtifactDto?> FindRenewalAsync(string credentialToken, int currentRevision, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialToken) || credentialToken.Length is < 43 or > 512 || currentRevision < 0)
            return null;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(credentialToken)));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // Filter bypass is confined to this credential-anchored join; never take a tenant ID from headers.
        var query = from installation in db.Installations.IgnoreQueryFilters().AsNoTracking()
                    join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on installation.TenantId equals tenant.Id
                    join product in db.Products.IgnoreQueryFilters().AsNoTracking()
                        on new { installation.TenantId, Id = installation.ProductId } equals new { product.TenantId, product.Id }
                    join entitlement in db.Entitlements.IgnoreQueryFilters().AsNoTracking()
                        on new { installation.TenantId, Id = installation.EntitlementId, installation.CustomerId, installation.ProductId }
                        equals new { entitlement.TenantId, entitlement.Id, entitlement.CustomerId, entitlement.ProductId }
                    join license in db.LicenseRevisions.IgnoreQueryFilters().AsNoTracking()
                        on new { installation.TenantId, InstallationId = installation.Id, installation.CustomerId, installation.ProductId, installation.EntitlementId }
                        equals new { license.TenantId, license.InstallationId, license.CustomerId, license.ProductId, license.EntitlementId }
                    where installation.CredentialHash == hash && !installation.IsRevoked && tenant.IsActive && product.IsActive && !entitlement.IsRevoked
                        && license.Status == "Ready" && license.Artifact != null && license.Revision > currentRevision
                    orderby license.Revision descending
                    select new RenewedLicenseArtifactDto(license.Id, license.Revision, license.Extension, license.Artifact!);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }
}
