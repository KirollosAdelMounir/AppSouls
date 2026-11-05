namespace TicketingCRM.Domain.Persistence.Models;
public sealed class CustomerInvitation:TenantRecord
{
 public Guid CustomerId{get;set;}
 public Guid OpportunityId{get;set;}
 public Guid ContractId{get;set;}
 public Guid UserId{get;set;}
 public Guid CreatedByUserId{get;set;}
 public string TokenHash{get;set;}="";
 public string SecurityStamp{get;set;}="";
 public DateTimeOffset ExpiresAtUtc{get;set;}
 public DateTimeOffset? AcceptedAtUtc{get;set;}
}
