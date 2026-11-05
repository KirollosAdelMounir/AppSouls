namespace TicketingCRM.Domain.Persistence.Models;
public sealed class CustomerActivity:TenantRecord
{
 public Guid CustomerId{get;set;}
 public Guid ActorUserId{get;set;}
 public string Kind{get;set;}="Note";
 public string Subject{get;set;}="";
 public string Notes{get;set;}="";
 public DateTimeOffset OccurredAtUtc{get;set;}
 public DateTimeOffset? FollowUpAtUtc{get;set;}
}
