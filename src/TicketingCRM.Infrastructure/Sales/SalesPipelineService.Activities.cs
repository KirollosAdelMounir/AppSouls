using Microsoft.EntityFrameworkCore;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.DTOs;
namespace TicketingCRM.Infrastructure.Sales;
public sealed partial class SalesPipelineService
{
 public async Task RecordActivityAsync(RecordSalesActivityRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await Authorize(s,cancellationToken);Operation(request.OperationId);
  if(request.Kind is not ("Call" or "Meeting" or "Email" or "Task" or "Note"))throw new ArgumentException("Choose a sales activity type.");
  var subject=Text(request.Subject,200);var notes=Text(request.Notes,1500,false);var followUp=Utc(request.FollowUpAtUtc);
  var digest=Digest(new{Activity=true,request.RecordId,request.IsOpportunity,request.Kind,subject,notes,followUp});
  if(await Replayed(s,request.IsOpportunity?null:request.RecordId,request.IsOpportunity?request.RecordId:null,request.OperationId,digest,cancellationToken))return;
  string stage;
  if(request.IsOpportunity){var item=await s.Query<SalesOpportunity>().SingleOrDefaultAsync(x=>x.Id==request.RecordId,cancellationToken)??throw new ArgumentException("Opportunity not found.");if(item.Version!=request.ExpectedVersion)throw new InvalidOperationException("The opportunity changed. Reload before recording activity.");stage=item.Stage.ToString();item.FollowUpAtUtc=followUp;}
  else{var item=await s.Query<SalesLead>().SingleOrDefaultAsync(x=>x.Id==request.RecordId,cancellationToken)??throw new ArgumentException("Lead not found.");if(item.Version!=request.ExpectedVersion)throw new InvalidOperationException("The lead changed. Reload before recording activity.");stage=item.Stage.ToString();item.FollowUpAtUtc=followUp;}
  History(s,request.IsOpportunity?null:request.RecordId,request.IsOpportunity?request.RecordId:null,request.OperationId,digest,stage,stage,$"[{request.Kind}] {subject}\n{notes}");await s.CommitAsync(cancellationToken);
 }
}
