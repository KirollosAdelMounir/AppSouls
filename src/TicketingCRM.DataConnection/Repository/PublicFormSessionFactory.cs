using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Tenancy.Interfaces;
namespace TicketingCRM.DataConnection.Repository;

public sealed class PublicFormSessionFactory(DatabaseProvider provider, string connectionString, bool maintenanceReadOnly = false) : IPublicFormSessionFactory
{
    private sealed record Scope(Guid TenantId,Guid FormId) : ITenantContext,IPublicFormRowScope;
    public async Task<IWorkspaceSession?> OpenAsync(string publicKey, CancellationToken cancellationToken = default)
    {
        // This narrow pre-authentication lookup exposes no private form or submission data.
        await using var locator = new TenantDbContextFactory(provider, connectionString, null, maintenanceReadOnly, administrative: true).CreateDbContext();
        var tenant = await locator.CollectionForms.IgnoreQueryFilters().Where(x => x.PublicKey == publicKey && x.Status == "Published")
            .Where(x => locator.Tenants.IgnoreQueryFilters().Any(t => t.Id == x.TenantId && t.IsActive))
            .Select(x => new {x.TenantId,x.Id}).SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? null : await new WorkspaceSessionFactory(new TenantDbContextFactory(provider, connectionString, new Scope(tenant.TenantId,tenant.Id), maintenanceReadOnly)).OpenAsync(cancellationToken);
    }
}
