using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Customers.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Sales.Models;
using TicketingCRM.Infrastructure.Customers;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class CustomerLifecycleServiceTests
{
 [PostgreSqlFact]
 public async Task FinalizationAtomicallyPreparesDisabledAccountAndEncryptedInvitationThenActivatesOnce()
 {
  using var f=await CustomerDeliveryFixture.Create();var request=new FinalizeCustomerSaleRequest(Guid.NewGuid(),f.Customer,f.Opportunity,f.Contract);
  await f.Lifecycle().FinalizeSaleAsync(request);await f.Lifecycle().FinalizeSaleAsync(request);
  Guid userId;string token;
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant))
  {
   var invite=Assert.Single(await db.CustomerInvitations.ToListAsync());var job=Assert.Single(await db.OutboxMessages.ToListAsync());
   var user=await db.Users.SingleAsync(x=>x.Id==invite.UserId);userId=user.Id;Assert.Null(user.PasswordHash);Assert.False(user.EmailConfirmed);Assert.False((await db.TenantMemberships.SingleAsync(x=>x.UserId==user.Id)).IsActive);
   using var payload=JsonDocument.Parse(job.PayloadJson);token=f.Protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);
   Assert.DoesNotContain(token,job.PayloadJson);Assert.NotEqual(token,invite.TokenHash);Assert.Equal(request.OperationId,invite.Id);Assert.Equal("CustomerInvitation",job.Kind);
  }
  var service=new CustomerInvitationService(f.Config);await service.AcceptAsync(new(request.OperationId,token,"Choose-123!"));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant))
  {var user=await db.Users.SingleAsync(x=>x.Id==userId);Assert.True(user.EmailConfirmed);Assert.True((await db.TenantMemberships.SingleAsync(x=>x.UserId==userId)).IsActive);Assert.Equal(PasswordVerificationResult.Success,new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user,user.PasswordHash!,"Choose-123!"));Assert.NotNull((await db.CustomerInvitations.SingleAsync()).AcceptedAtUtc);}
  await Assert.ThrowsAsync<ArgumentException>(()=>service.AcceptAsync(new(request.OperationId,token,"Choose-123!")));
 }
 [PostgreSqlFact]
 public async Task FinalizationRejectsIncompleteSalesForeignRelationshipsRolesAndExistingIdentity()
 {
  using var f=await CustomerDeliveryFixture.Create();using var other=await CustomerDeliveryFixture.Create();var request=new FinalizeCustomerSaleRequest(Guid.NewGuid(),f.Customer,f.Opportunity,f.Contract);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Lifecycle(f.TechnicalActor).FinalizeSaleAsync(request));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Lifecycle(f.CustomerActor).FinalizeSaleAsync(request));
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Lifecycle().FinalizeSaleAsync(request with{ContractId=other.Contract}));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){(await db.SalesOpportunities.SingleAsync()).Stage=OpportunityStage.Negotiation;await db.SaveChangesAsync();}
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Lifecycle().FinalizeSaleAsync(request));
  var existingEmail="existing-"+Guid.NewGuid().ToString("N")+"@global.test";var normalized=existingEmail.ToUpperInvariant();
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){(await db.SalesOpportunities.SingleAsync()).Stage=OpportunityStage.Won;var c=await db.Customers.SingleAsync(x=>x.Id==f.Customer);c.Email=existingEmail;db.Users.Add(new ApplicationUser{Id=Guid.NewGuid(),UserName=existingEmail,NormalizedEmail=normalized,NormalizedUserName=normalized,PasswordHash="untouched"});await db.SaveChangesAsync();}
  await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Lifecycle().FinalizeSaleAsync(request));
  await using var check=CustomerDeliveryFixture.Open(f.Tenant);Assert.Empty(await check.CustomerInvitations.ToListAsync());Assert.Empty(await check.OutboxMessages.ToListAsync());Assert.Equal("untouched",(await check.Users.SingleAsync(x=>x.NormalizedEmail==normalized)).PasswordHash);
 }
 [PostgreSqlFact]
 public async Task WrongExpiredAndWeakInvitationProofCannotActivateAnAccount()
 {
  using var f=await CustomerDeliveryFixture.Create();var id=Guid.NewGuid();await f.Lifecycle().FinalizeSaleAsync(new(id,f.Customer,f.Opportunity,f.Contract));
  var service=new CustomerInvitationService(f.Config);await Assert.ThrowsAsync<ArgumentException>(()=>service.AcceptAsync(new(id,new string('A',64),"Choose-123!")));
  string token;Guid userId;
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){var invite=await db.CustomerInvitations.SingleAsync();userId=invite.UserId;invite.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);using var payload=JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).PayloadJson);token=f.Protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);await db.SaveChangesAsync();}
  await Assert.ThrowsAsync<ArgumentException>(()=>service.AcceptAsync(new(id,token,"Choose-123!")));await Assert.ThrowsAsync<ArgumentException>(()=>service.AcceptAsync(new(id,token,"weak")));
  await using var check=CustomerDeliveryFixture.Open(f.Tenant);Assert.False((await check.Users.SingleAsync(x=>x.Id==userId)).EmailConfirmed);Assert.Null((await check.Users.SingleAsync(x=>x.Id==userId)).PasswordHash);
 }
 [PostgreSqlFact]
 public async Task ActivitiesAreImmutableIdempotentTenantScopedAndHiddenFromCustomers()
 {
  using var f=await CustomerDeliveryFixture.Create();using var other=await CustomerDeliveryFixture.Create();var request=new RecordCustomerActivityRequest(Guid.NewGuid(),f.Customer,"Call","Requirements","Internal pricing detail",DateTimeOffset.UtcNow.AddDays(1));
  await f.Lifecycle().RecordActivityAsync(request);await f.Lifecycle().RecordActivityAsync(request);
  await Assert.ThrowsAsync<ArgumentException>(()=>f.Lifecycle().RecordActivityAsync(request with{Notes="Different"}));
  await Assert.ThrowsAsync<ArgumentException>(()=>other.Lifecycle().RecordActivityAsync(request));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Lifecycle(f.CustomerActor).RecordActivityAsync(request));
  Assert.Single((await f.Lifecycle().GetAsync(f.Customer)).Activities);Assert.Empty((await f.Lifecycle(f.CustomerActor).GetAsync(f.Customer)).Activities);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Lifecycle(f.OtherActor).GetAsync(f.Customer));
  await using var db=CustomerDeliveryFixture.Open(f.Tenant);var activity=await db.CustomerActivities.SingleAsync();Assert.Equal(f.Owner,activity.ActorUserId);Assert.Equal(TimeSpan.Zero,activity.OccurredAtUtc.Offset);activity.Notes="Tampered";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
 }
 [PostgreSqlFact]
 public async Task ReissuedInvitationInvalidatesPreviousProofAndRetriesWithoutDuplicatingMail()
 {
  using var f=await CustomerDeliveryFixture.Create();var id=Guid.NewGuid();await f.Lifecycle().FinalizeSaleAsync(new(id,f.Customer,f.Opportunity,f.Contract));
  string oldToken;
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){using var payload=JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).PayloadJson);oldToken=f.Protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);}
  var request=new ReissueCustomerInvitationRequest(Guid.NewGuid(),id);await f.Lifecycle().ReissueInvitationAsync(request);await f.Lifecycle().ReissueInvitationAsync(request);
  var acceptance=new CustomerInvitationService(f.Config);await Assert.ThrowsAsync<ArgumentException>(()=>acceptance.AcceptAsync(new(id,oldToken,"Choose-123!")));
  string newToken;
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){var jobs=await db.OutboxMessages.ToListAsync();Assert.Equal(2,jobs.Count);var latest=Assert.Single(jobs,x=>x.IdempotencyKey.Contains("reissue"));using var payload=JsonDocument.Parse(latest.PayloadJson);newToken=f.Protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);Assert.Equal(10,Assert.Single(jobs,x=>!x.IdempotencyKey.Contains("reissue")).Attempts);Assert.NotEqual(oldToken,newToken);}
  await acceptance.AcceptAsync(new(id,newToken,"Choose-123!"));await Assert.ThrowsAsync<ArgumentException>(()=>f.Lifecycle().ReissueInvitationAsync(new(Guid.NewGuid(),id)));
 }

 [PostgreSqlFact]
 public async Task ConcurrentInvitationClaimsAllowExactlyOnePasswordSetup()
 {
  using var f=await CustomerDeliveryFixture.Create();var id=Guid.NewGuid();await f.Lifecycle().FinalizeSaleAsync(new(id,f.Customer,f.Opportunity,f.Contract));string token;
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){using var payload=JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).PayloadJson);token=f.Protection.CreateProtector("AppSouls.CustomerInvitation.v1").Unprotect(payload.RootElement.GetProperty("ProtectedToken").GetString()!);}
  async Task<bool> Claim(){try{await new CustomerInvitationService(f.Config).AcceptAsync(new(id,token,"Choose-123!"));return true;}catch(ArgumentException){return false;}}
  var results=await Task.WhenAll(Claim(),Claim());Assert.Equal(1,results.Count(x=>x));
  await using var check=CustomerDeliveryFixture.Open(f.Tenant);var invitation=await check.CustomerInvitations.SingleAsync();Assert.NotNull(invitation.AcceptedAtUtc);Assert.True((await check.TenantMemberships.SingleAsync(x=>x.UserId==invitation.UserId)).IsActive);
 }
 [PostgreSqlFact]
 public async Task DatabaseRejectsInvitationLinkedToAnotherCustomersOpportunity()
 {
  using var f=await CustomerDeliveryFixture.Create();await using var db=CustomerDeliveryFixture.Open(f.Tenant);var opportunity=Guid.NewGuid();
  db.SalesOpportunities.Add(new SalesOpportunity{Id=opportunity,TenantId=f.Tenant,CustomerId=f.OtherCustomer,OwnerUserId=f.Owner,CreatedByUserId=f.Owner,Title="Other customer",CreationOperationId=Guid.NewGuid()});await db.SaveChangesAsync();
  db.CustomerInvitations.Add(new CustomerInvitation{TenantId=f.Tenant,CustomerId=f.Customer,OpportunityId=opportunity,ContractId=f.Contract,UserId=f.CustomerUser,CreatedByUserId=f.Owner,TokenHash=new string('A',64),SecurityStamp=f.Stamp,ExpiresAtUtc=DateTimeOffset.UtcNow.AddDays(1)});
  await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
 }

}
