using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Customers;
public sealed partial class CustomerLifecycleService
{
 public async Task ReissueInvitationAsync(ReissueCustomerInvitationRequest request,CancellationToken cancellationToken=default)
 {
  await using var s=await sessions.OpenAsync(cancellationToken);await WorkspaceAccess.RequireAsync(s,actor,administrator:true,ct:cancellationToken);
  if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
  var idempotency=$"customer-invitation-reissue:{request.InvitationId:N}:{request.OperationId:N}";
  if(await s.Query<OutboxMessage>().AnyAsync(x=>x.IdempotencyKey==idempotency,cancellationToken))return;
  var invite=await s.Query<CustomerInvitation>().SingleOrDefaultAsync(x=>x.Id==request.InvitationId&&x.AcceptedAtUtc==null,cancellationToken)??throw new ArgumentException("Pending invitation not found.");
  if(!await s.Query<ApplicationUser>().AnyAsync(x=>x.Id==invite.UserId&&!x.EmailConfirmed&&x.PasswordHash==null&&x.SecurityStamp==invite.SecurityStamp,cancellationToken)||!await s.Query<TenantMembership>().AnyAsync(x=>x.UserId==invite.UserId&&x.CustomerId==invite.CustomerId&&!x.IsActive&&x.Role=="Customer",cancellationToken))throw new ArgumentException("Only the original pending customer account can receive a fresh invitation.");
  var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));invite.TokenHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));invite.ExpiresAtUtc=DateTimeOffset.UtcNow.AddDays(2);
  var recipient=invite.UserId.ToString();
  var old=await s.Query<OutboxMessage>().Where(x=>x.Kind=="CustomerInvitation"&&x.Recipient==recipient&&x.DeliveredAtUtc==null).ToListAsync(cancellationToken);
  foreach(var job in old){job.Attempts=10;job.LastError="SupersededInvitation";}
  s.Add(new OutboxMessage{TenantId=actor.TenantId,Kind="CustomerInvitation",Recipient=recipient,IdempotencyKey=idempotency,CreatedAtUtc=DateTimeOffset.UtcNow,PayloadJson=JsonSerializer.Serialize(new{InvitationId=invite.Id,ProtectedToken=protection.CreateProtector("AppSouls.CustomerInvitation.v1").Protect(token)})});
  s.Add(new CustomerActivity{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=invite.CustomerId,ActorUserId=actor.UserId,Kind="Note",Subject="Customer invitation reissued",Notes="Previous password-setup proofs invalidated; replacement email queued.",OccurredAtUtc=DateTimeOffset.UtcNow});
  await s.CommitAsync(cancellationToken);
 }
}
