using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.DTOs;
namespace TicketingCRM.Infrastructure.Sales;
public sealed partial class SalesPipelineService
{
 public async Task<LeadDto> GetLeadAsync(Guid id, CancellationToken cancellationToken = default)
 {
  await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken);
  return await s.Repository<SalesLead>().GetReadOnlyQueryable().Where(x => x.Id == id).Select(x => new LeadDto(x.Id,x.Version,x.ContactName,x.CompanyName,x.Email,x.Phone,x.Source,x.Notes,x.Stage,x.OwnerUserId,x.FollowUpAtUtc,x.CustomerId)).SingleOrDefaultAsync(cancellationToken) ?? throw new ArgumentException("Lead not found.");
 }
 public async Task<OpportunityDto> GetOpportunityAsync(Guid id, CancellationToken cancellationToken = default)
 {
  await using var s = await sessions.OpenAsync(cancellationToken); await Authorize(s, cancellationToken);
  return await (from x in s.Repository<SalesOpportunity>().GetReadOnlyQueryable() join c in s.Repository<Customer>().GetReadOnlyQueryable() on x.CustomerId equals c.Id where x.Id == id select new OpportunityDto(x.Id,x.Version,x.Title,x.CustomerId,c.Name,x.Stage,x.OwnerUserId,x.ExpectedValue,x.Currency,x.FollowUpAtUtc)).SingleOrDefaultAsync(cancellationToken) ?? throw new ArgumentException("Opportunity not found.");
 }
}
