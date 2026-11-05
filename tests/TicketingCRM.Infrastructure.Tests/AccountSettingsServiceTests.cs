using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Infrastructure.Accounts;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class AccountSettingsServiceTests
{
 [PostgreSqlFact]
 public async Task SelfProfileAndPasswordChangesRespectMembershipAndRevokeOldSessionStamp()
 {
  using var f=await CustomerDeliveryFixture.Create();var services=new ServiceCollection();services.AddLogging();services.AddDbContext<AppDbContext>(_=>{});
  services.AddScoped<AppDbContext>(_=>CustomerDeliveryFixture.Open(f.Tenant));services.AddIdentityCore<ApplicationUser>(o=>o.Password.RequiredLength=8).AddEntityFrameworkStores<AppDbContext>();
  await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
  var user=await manager.FindByIdAsync(f.Owner.ToString());Assert.True((await manager.AddPasswordAsync(user!,"Original-123!")).Succeeded);
  // Adding a password changes the security stamp, so use the fresh authenticated snapshot.
  var actor=f.OwnerActor with{SecurityStamp=user!.SecurityStamp};var account=new AccountSettingsService(new WorkspaceSessionFactory(new CustomerDeliveryFixture.Factory(f.Tenant)),actor);
  await account.UpdateNameAsync(new("Updated name"));Assert.Equal("Updated name",(await account.GetAsync()).DisplayName);
  await Assert.ThrowsAsync<ArgumentException>(()=>account.ChangePasswordAsync(new("incorrect","New-Password-123!")));
  await account.ChangePasswordAsync(new("Original-123!","New-Password-123!"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>account.GetAsync());await using var db=CustomerDeliveryFixture.Open(f.Tenant);var updated=await db.Users.SingleAsync(x=>x.Id==f.Owner);var hasher=new PasswordHasher<ApplicationUser>();Assert.Equal(PasswordVerificationResult.Success,hasher.VerifyHashedPassword(updated,updated.PasswordHash!,"New-Password-123!"));Assert.Equal(PasswordVerificationResult.Failed,hasher.VerifyHashedPassword(updated,updated.PasswordHash!,"Original-123!"));
 }
}
