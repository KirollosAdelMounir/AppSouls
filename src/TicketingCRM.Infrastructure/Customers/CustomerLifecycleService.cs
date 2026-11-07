using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TicketingCRM.DataConnection.Repository.Queries;

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
  if(member.CustomerId is not null)throw new UnauthorizedAccessException();
  var c=await s.Repository<Customer>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==customerId,cancellationToken)??throw new ArgumentException("Customer not found.");
  var staff=member.CustomerId is null;var commercial=staff&&member.Role is "Owner" or "Admin" or "Sales" or "Sales Manager";
  var opportunities=commercial?await s.Repository<SalesOpportunity>().GetAllQueryable().Where(x=>x.CustomerId==customerId).OrderByDescending(x=>x.CreatedAtUtc).Take(50).Select(x=>new TicketingCRM.Domain.Sales.DTOs.OpportunityDto(x.Id,x.Version,x.Title,x.CustomerId,c.Name,x.Stage,x.OwnerUserId,x.ExpectedValue,x.Currency,x.FollowUpAtUtc)).ToListAsync(cancellationToken):[];
  var activities=staff?await s.Repository<CustomerActivity>().GetAllQueryable().Where(x=>x.CustomerId==customerId&&(commercial||x.Kind=="Technical"||x.Kind=="Note")).OrderByDescending(x=>x.OccurredAtUtc).Take(100).Select(x=>new CustomerActivityDto(x.Id,x.Kind,x.Subject,x.Notes,x.OccurredAtUtc,x.FollowUpAtUtc)).ToListAsync(cancellationToken):[];
  if(commercial)
  {
   var sales=await s.Repository<SalesHistory>().GetAllQueryable().Where(x=>(x.OpportunityId!=null&&s.Repository<SalesOpportunity>().GetAllQueryable().Any(o=>o.Id==x.OpportunityId&&o.CustomerId==customerId))||(x.LeadId!=null&&s.Repository<SalesLead>().GetAllQueryable().Any(l=>l.Id==x.LeadId&&l.CustomerId==customerId))).OrderByDescending(x=>x.OccurredAtUtc).Take(100).Select(x=>new CustomerActivityDto(x.Id,"Sales",x.FromStage+" → "+x.ToStage,x.Reason,x.OccurredAtUtc,null)).ToListAsync(cancellationToken);
   activities=activities.Concat(sales).OrderByDescending(x=>x.OccurredAtUtc).Take(100).ToList();
  }
  return new(new(c.Id,c.Name,c.Email,c.CompanyNumber,c.CreatedAtUtc),opportunities,
   await s.Repository<Contract>().GetAllQueryable().Where(x=>x.CustomerId==customerId).OrderByDescending(x=>x.StartsAtUtc).Take(100).Select(x=>new TicketingCRM.Domain.Workspace.DTOs.ContractDto(x.Id,x.CustomerId,x.Reference,x.Status,x.StartsAtUtc,x.EndsAtUtc)).ToListAsync(cancellationToken),
   await s.Repository<Entitlement>().GetAllQueryable().Where(x=>x.CustomerId==customerId&&!x.IsRevoked).Take(100).Select(x=>new TicketingCRM.Domain.Workspace.DTOs.EntitlementDto(x.Id,x.CustomerId,x.ProductId,x.ContractId,x.InstallationLimit,x.StartsAtUtc,x.ExpiresAtUtc,x.SupportExpiresAtUtc)).ToListAsync(cancellationToken),
   activities,
   await s.Repository<SupportTicket>().GetAllQueryable().Where(x=>x.CustomerId==customerId).OrderByDescending(x=>x.CreatedAtUtc).Take(100).Select(x=>new CustomerSupportDto(x.Id,x.Reference,x.Title,x.Stage.ToString())).ToListAsync(cancellationToken),
   member.Role is "Owner" or "Admin"?await s.Repository<CustomerInvitation>().GetAllQueryable().Where(x=>x.CustomerId==customerId).Take(50).Select(x=>new CustomerInvitationDto(x.Id,x.ExpiresAtUtc,x.AcceptedAtUtc!=null)).ToListAsync(cancellationToken):[],member.CustomerId is null&&member.Role is "Owner" or "Admin",
   await (from e in s.Repository<Entitlement>().GetReadOnlyQueryable() join p in s.Repository<Product>().GetReadOnlyQueryable() on e.ProductId equals p.Id join contract in s.Repository<Contract>().GetReadOnlyQueryable() on e.ContractId equals contract.Id where e.CustomerId==customerId orderby p.Name select new CustomerSoftwareDto(e.Id,p.Id,p.Name,p.Code,contract.Reference,e.StartsAtUtc,e.ExpiresAtUtc,e.SupportExpiresAtUtc,e.InstallationLimit,e.IsRevoked)).Take(100).ToListAsync(cancellationToken),
   await (from license in s.Repository<LicenseRevision>().GetReadOnlyQueryable() join product in s.Repository<Product>().GetReadOnlyQueryable() on license.ProductId equals product.Id join installation in s.Repository<Installation>().GetReadOnlyQueryable() on license.InstallationId equals installation.Id where license.CustomerId==customerId orderby license.IssuedAtUtc descending select new CustomerLicenseDto(license.Id,product.Name,installation.Name,license.Revision,license.Status,license.IssuedAtUtc,license.ExpiresAtUtc)).Take(100).ToListAsync(cancellationToken),staff);

 }
 public async Task RecordActivityAsync(RecordCustomerActivityRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:cancellationToken);
  if(member.CustomerId is not null)throw new UnauthorizedAccessException();
  if(request.OperationId==Guid.Empty||request.Kind is not ("Call" or "Meeting" or "Email" or "Task" or "Note" or "Technical"))throw new ArgumentException("Choose an activity type.");
  if(actor.Role is "Technical" or "Technical Support" or "Technical Developer" &&request.Kind is not ("Technical" or "Note"))throw new UnauthorizedAccessException();
  var subject=request.Subject?.Trim()??"";var notes=request.Notes?.Trim()??"";
  if(subject.Length is <1 or >200||notes.Length>4000||request.FollowUpAtUtc?.Offset!=TimeSpan.Zero&&request.FollowUpAtUtc is not null)throw new ArgumentException("Enter an activity subject and a UTC follow-up.");
  if(!await s.Repository<Customer>().GetAllQueryable().AnyAsync(x=>x.Id==request.CustomerId,cancellationToken))throw new ArgumentException("Customer not found.");
  var followUp=request.FollowUpAtUtc is {} due?new DateTimeOffset(due.UtcTicks/10*10,TimeSpan.Zero):(DateTimeOffset?)null;
  var existing=await s.Repository<CustomerActivity>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,cancellationToken);
  if(existing is not null){if(existing.CustomerId!=request.CustomerId||existing.ActorUserId!=actor.UserId||existing.Kind!=request.Kind||existing.Subject!=subject||existing.Notes!=notes||existing.FollowUpAtUtc!=followUp)throw new ArgumentException("This operation was already used.");return;}
  s.Repository<CustomerActivity>().Add(new CustomerActivity{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=request.CustomerId,ActorUserId=actor.UserId,Kind=request.Kind,Subject=subject,Notes=notes,FollowUpAtUtc=followUp,OccurredAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(cancellationToken);
 }
 public async Task FinalizeSaleAsync(FinalizeCustomerSaleRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await WorkspaceAccess.RequireAsync(s,actor,administrator:true,ct:cancellationToken);
  if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
  var existing=await s.Repository<CustomerInvitation>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,cancellationToken);
  if(existing is not null){if(existing.CustomerId!=request.CustomerId||existing.OpportunityId!=request.OpportunityId||existing.ContractId!=request.ContractId||existing.CreatedByUserId!=actor.UserId)throw new ArgumentException("This operation was already used.");return;}
  var now=DateTimeOffset.UtcNow;var customer=await s.Repository<Customer>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==request.CustomerId,cancellationToken)??throw new ArgumentException("Customer not found.");
  if(!await s.Repository<SalesOpportunity>().GetAllQueryable().AnyAsync(x=>x.Id==request.OpportunityId&&x.CustomerId==customer.Id&&x.Stage==OpportunityStage.Won,cancellationToken)||!await s.Repository<Contract>().GetAllQueryable().AnyAsync(x=>x.Id==request.ContractId&&x.CustomerId==customer.Id&&x.Status=="Active"&&x.StartsAtUtc<=now&&(x.EndsAtUtc==null||x.EndsAtUtc>now),cancellationToken)||!await s.Repository<Entitlement>().GetAllQueryable().AnyAsync(x=>x.CustomerId==customer.Id&&x.ContractId==request.ContractId&&!x.IsRevoked&&x.StartsAtUtc<=now&&(x.ExpiresAtUtc==null||x.ExpiresAtUtc>now),cancellationToken))throw new ArgumentException("Finalize a won opportunity with an active contract and current purchased software.");
  if(!System.Net.Mail.MailAddress.TryCreate(customer.Email,out var email)||email.Address!=customer.Email)throw new ArgumentException("Set a valid customer contact email first.");
  var normalized=customer.Email.ToUpperInvariant();
  if(await s.Repository<ApplicationUser>().GetAllQueryable().AnyAsync(x=>x.NormalizedEmail==normalized||x.NormalizedUserName==normalized,cancellationToken))throw new InvalidOperationException("This email already belongs to an account. Existing identities cannot be adopted through onboarding.");
  var user=new ApplicationUser{Id=Guid.NewGuid(),DisplayName=customer.Name,Email=customer.Email,UserName=customer.Email,NormalizedEmail=normalized,NormalizedUserName=normalized,EmailConfirmed=false,SecurityStamp=Guid.NewGuid().ToString("N"),ConcurrencyStamp=Guid.NewGuid().ToString("N"),LockoutEnabled=true};
  var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
  s.Repository<ApplicationUser>().Add(user);s.Repository<TenantMembership>().Add(new TenantMembership{TenantId=actor.TenantId,UserId=user.Id,CustomerId=customer.Id,Role="Customer",IsActive=false});
  s.Repository<CustomerInvitation>().Add(new CustomerInvitation{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=customer.Id,OpportunityId=request.OpportunityId,ContractId=request.ContractId,UserId=user.Id,CreatedByUserId=actor.UserId,TokenHash=hash,SecurityStamp=user.SecurityStamp,ExpiresAtUtc=now.AddDays(2)});
  s.Repository<OutboxMessage>().Add(new OutboxMessage{TenantId=actor.TenantId,Kind="CustomerInvitation",Recipient=user.Id.ToString(),IdempotencyKey=$"customer-invitation:{request.OperationId:N}",CreatedAtUtc=now,PayloadJson=JsonSerializer.Serialize(new{InvitationId=request.OperationId,ProtectedToken=protection.CreateProtector("AppSouls.CustomerInvitation.v1").Protect(token)})});
  s.Repository<CustomerActivity>().Add(new CustomerActivity{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=customer.Id,ActorUserId=actor.UserId,Kind="Note",Subject="Customer account prepared",Notes="Password-setup invitation queued.",OccurredAtUtc=now});
  await s.CommitAsync(cancellationToken);
 }
}
