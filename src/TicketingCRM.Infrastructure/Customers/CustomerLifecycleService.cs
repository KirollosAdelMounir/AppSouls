using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Customers.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Customers;
public sealed partial class CustomerLifecycleService(IWorkspaceSessionFactory sessions,ICurrentActor actor,IDataProtectionProvider protection):ICustomerLifecycleService
{
 public async Task<CustomerLifecycleSnapshot> GetAsync(Guid customerId,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:cancellationToken);
  if(member.CustomerId is {} own&&own!=customerId)throw new UnauthorizedAccessException();
  var c=await s.Query<Customer>().SingleOrDefaultAsync(x=>x.Id==customerId,cancellationToken)??throw new ArgumentException("Customer not found.");
  var staff=member.CustomerId is null;var commercial=staff&&member.Role is "Owner" or "Admin";
  var opportunities=commercial?await s.Query<SalesOpportunity>().Where(x=>x.CustomerId==customerId).OrderByDescending(x=>x.CreatedAtUtc).Take(50).Select(x=>new TicketingCRM.Domain.Sales.DTOs.OpportunityDto(x.Id,x.Version,x.Title,x.CustomerId,c.Name,x.Stage,x.OwnerUserId,x.ExpectedValue,x.Currency,x.FollowUpAtUtc)).ToListAsync(cancellationToken):[];
  var activities=staff?await s.Query<CustomerActivity>().Where(x=>x.CustomerId==customerId&&(commercial||x.Kind=="Technical"||x.Kind=="Note")).OrderByDescending(x=>x.OccurredAtUtc).Take(100).Select(x=>new CustomerActivityDto(x.Id,x.Kind,x.Subject,x.Notes,x.OccurredAtUtc,x.FollowUpAtUtc)).ToListAsync(cancellationToken):[];
  if(commercial)
  {
   var sales=await s.Query<SalesHistory>().Where(x=>(x.OpportunityId!=null&&s.Query<SalesOpportunity>().Any(o=>o.Id==x.OpportunityId&&o.CustomerId==customerId))||(x.LeadId!=null&&s.Query<SalesLead>().Any(l=>l.Id==x.LeadId&&l.CustomerId==customerId))).OrderByDescending(x=>x.OccurredAtUtc).Take(100).Select(x=>new CustomerActivityDto(x.Id,"Sales",x.FromStage+" → "+x.ToStage,x.Reason,x.OccurredAtUtc,null)).ToListAsync(cancellationToken);
   activities=activities.Concat(sales).OrderByDescending(x=>x.OccurredAtUtc).Take(100).ToList();
  }
  return new(new(c.Id,c.Name,c.Email,c.CompanyNumber),opportunities,
   await s.Query<Contract>().Where(x=>x.CustomerId==customerId).OrderByDescending(x=>x.StartsAtUtc).Take(100).Select(x=>new TicketingCRM.Domain.Workspace.DTOs.ContractDto(x.Id,x.CustomerId,x.Reference,x.Status,x.StartsAtUtc,x.EndsAtUtc)).ToListAsync(cancellationToken),
   await s.Query<Entitlement>().Where(x=>x.CustomerId==customerId&&!x.IsRevoked).Take(100).Select(x=>new TicketingCRM.Domain.Workspace.DTOs.EntitlementDto(x.Id,x.CustomerId,x.ProductId,x.ContractId,x.InstallationLimit,x.StartsAtUtc,x.ExpiresAtUtc)).ToListAsync(cancellationToken),
   activities,
   await s.Query<SupportTicket>().Where(x=>x.CustomerId==customerId).OrderByDescending(x=>x.CreatedAtUtc).Take(100).Select(x=>new CustomerSupportDto(x.Id,x.Reference,x.Title,x.Stage.ToString())).ToListAsync(cancellationToken),
   member.Role is "Owner" or "Admin"?await s.Query<CustomerInvitation>().Where(x=>x.CustomerId==customerId).Take(50).Select(x=>new CustomerInvitationDto(x.Id,x.ExpiresAtUtc,x.AcceptedAtUtc!=null)).ToListAsync(cancellationToken):[],member.CustomerId is null&&member.Role is "Owner" or "Admin");
 }
 public async Task RecordActivityAsync(RecordCustomerActivityRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await WorkspaceAccess.RequireAsync(s,actor,technical:true,ct:cancellationToken);
  if(request.OperationId==Guid.Empty||request.Kind is not ("Call" or "Meeting" or "Email" or "Task" or "Note" or "Technical"))throw new ArgumentException("Choose an activity type.");
  if(actor.Role=="Technical"&&request.Kind is not ("Technical" or "Note"))throw new UnauthorizedAccessException();
  var subject=request.Subject?.Trim()??"";var notes=request.Notes?.Trim()??"";
  if(subject.Length is <1 or >200||notes.Length>4000||request.FollowUpAtUtc?.Offset!=TimeSpan.Zero&&request.FollowUpAtUtc is not null)throw new ArgumentException("Enter an activity subject and a UTC follow-up.");
  if(!await s.Query<Customer>().AnyAsync(x=>x.Id==request.CustomerId,cancellationToken))throw new ArgumentException("Customer not found.");
  var followUp=request.FollowUpAtUtc is {} due?new DateTimeOffset(due.UtcTicks/10*10,TimeSpan.Zero):(DateTimeOffset?)null;
  var existing=await s.Query<CustomerActivity>().SingleOrDefaultAsync(x=>x.Id==request.OperationId,cancellationToken);
  if(existing is not null){if(existing.CustomerId!=request.CustomerId||existing.ActorUserId!=actor.UserId||existing.Kind!=request.Kind||existing.Subject!=subject||existing.Notes!=notes||existing.FollowUpAtUtc!=followUp)throw new ArgumentException("This operation was already used.");return;}
  s.Add(new CustomerActivity{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=request.CustomerId,ActorUserId=actor.UserId,Kind=request.Kind,Subject=subject,Notes=notes,FollowUpAtUtc=followUp,OccurredAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(cancellationToken);
 }
 public async Task FinalizeSaleAsync(FinalizeCustomerSaleRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await WorkspaceAccess.RequireAsync(s,actor,administrator:true,ct:cancellationToken);
  if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
  var existing=await s.Query<CustomerInvitation>().SingleOrDefaultAsync(x=>x.Id==request.OperationId,cancellationToken);
  if(existing is not null){if(existing.CustomerId!=request.CustomerId||existing.OpportunityId!=request.OpportunityId||existing.ContractId!=request.ContractId||existing.CreatedByUserId!=actor.UserId)throw new ArgumentException("This operation was already used.");return;}
  var now=DateTimeOffset.UtcNow;var customer=await s.Query<Customer>().SingleOrDefaultAsync(x=>x.Id==request.CustomerId,cancellationToken)??throw new ArgumentException("Customer not found.");
  if(!await s.Query<SalesOpportunity>().AnyAsync(x=>x.Id==request.OpportunityId&&x.CustomerId==customer.Id&&x.Stage==OpportunityStage.Won,cancellationToken)||!await s.Query<Contract>().AnyAsync(x=>x.Id==request.ContractId&&x.CustomerId==customer.Id&&x.Status=="Active"&&x.StartsAtUtc<=now&&(x.EndsAtUtc==null||x.EndsAtUtc>now),cancellationToken)||!await s.Query<Entitlement>().AnyAsync(x=>x.CustomerId==customer.Id&&x.ContractId==request.ContractId&&!x.IsRevoked&&x.StartsAtUtc<=now&&(x.ExpiresAtUtc==null||x.ExpiresAtUtc>now),cancellationToken))throw new ArgumentException("Finalize a won opportunity with an active contract and current purchased software.");
  if(!System.Net.Mail.MailAddress.TryCreate(customer.Email,out var email)||email.Address!=customer.Email)throw new ArgumentException("Set a valid customer contact email first.");
  var normalized=customer.Email.ToUpperInvariant();
  if(await s.Query<ApplicationUser>().AnyAsync(x=>x.NormalizedEmail==normalized||x.NormalizedUserName==normalized,cancellationToken))throw new InvalidOperationException("This email already belongs to an account. Existing identities cannot be adopted through onboarding.");
  var user=new ApplicationUser{Id=Guid.NewGuid(),DisplayName=customer.Name,Email=customer.Email,UserName=customer.Email,NormalizedEmail=normalized,NormalizedUserName=normalized,EmailConfirmed=false,SecurityStamp=Guid.NewGuid().ToString("N"),ConcurrencyStamp=Guid.NewGuid().ToString("N"),LockoutEnabled=true};
  var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
  s.Add(user);s.Add(new TenantMembership{TenantId=actor.TenantId,UserId=user.Id,CustomerId=customer.Id,Role="Customer",IsActive=false});
  s.Add(new CustomerInvitation{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=customer.Id,OpportunityId=request.OpportunityId,ContractId=request.ContractId,UserId=user.Id,CreatedByUserId=actor.UserId,TokenHash=hash,SecurityStamp=user.SecurityStamp,ExpiresAtUtc=now.AddDays(2)});
  s.Add(new OutboxMessage{TenantId=actor.TenantId,Kind="CustomerInvitation",Recipient=user.Id.ToString(),IdempotencyKey=$"customer-invitation:{request.OperationId:N}",CreatedAtUtc=now,PayloadJson=JsonSerializer.Serialize(new{InvitationId=request.OperationId,ProtectedToken=protection.CreateProtector("AppSouls.CustomerInvitation.v1").Protect(token)})});
  s.Add(new CustomerActivity{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=customer.Id,ActorUserId=actor.UserId,Kind="Note",Subject="Customer account prepared",Notes="Password-setup invitation queued.",OccurredAtUtc=now});
  await s.CommitAsync(cancellationToken);
 }
}
