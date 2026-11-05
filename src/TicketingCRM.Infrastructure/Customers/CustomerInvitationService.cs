using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Customers.Interfaces;
using TicketingCRM.Infrastructure.Administration;
namespace TicketingCRM.Infrastructure.Customers;
public sealed class CustomerInvitationService(ServerConfiguration configuration):ICustomerInvitationService
{
 public async Task AcceptAsync(AcceptCustomerInvitationRequest request,CancellationToken cancellationToken=default)
 {
  PasswordPolicy.Validate(request.Password);
  if(request.InvitationId==Guid.Empty||request.Token is null||request.Token.Length!=64)throw new ArgumentException("The activation link is invalid or expired.");
  var location=await CustomerInvitationLocator.LocateAsync((TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider,configuration.DatabaseConnectionString,request.InvitationId,cancellationToken);
  var hash=SHA256.HashData(Encoding.UTF8.GetBytes(request.Token));
  if(location is null||!CryptographicOperations.FixedTimeEquals(hash,Convert.FromHexString(location.Value.TokenHash)))throw new ArgumentException("The activation link is invalid or expired.");
  // Only the verified invitation proof establishes scope. Recheck all state in the one scoped unit of work.
  await using var s=PreparedAdministratorSeeder.Open(configuration,location.Value.TenantId);
  var invitation=await s.CustomerInvitations.SingleOrDefaultAsync(x=>x.Id==request.InvitationId,cancellationToken);
  if(invitation is null||invitation.AcceptedAtUtc is not null||invitation.ExpiresAtUtc<=DateTimeOffset.UtcNow||!CryptographicOperations.FixedTimeEquals(hash,Convert.FromHexString(invitation.TokenHash))||!await s.Tenants.AnyAsync(x=>x.IsActive,cancellationToken))throw new ArgumentException("The activation link is invalid or expired.");
  var user=await s.Users.SingleOrDefaultAsync(x=>x.Id==invitation.UserId,cancellationToken);
  var member=await s.TenantMemberships.SingleOrDefaultAsync(x=>x.UserId==invitation.UserId&&x.CustomerId==invitation.CustomerId&&x.Role=="Customer",cancellationToken);
  if(user is null||user.EmailConfirmed||user.PasswordHash is not null||user.SecurityStamp!=invitation.SecurityStamp||member is null||member.IsActive)throw new ArgumentException("The activation link is invalid or expired.");
  user.PasswordHash=new PasswordHasher<ApplicationUser>().HashPassword(user,request.Password);user.EmailConfirmed=true;user.SecurityStamp=Guid.NewGuid().ToString("N");user.ConcurrencyStamp=Guid.NewGuid().ToString("N");member.IsActive=true;invitation.AcceptedAtUtc=DateTimeOffset.UtcNow;
  try{await s.SaveChangesAsync(cancellationToken);}catch(DbUpdateConcurrencyException){throw new ArgumentException("The activation link is invalid or expired.");}
 }
}
