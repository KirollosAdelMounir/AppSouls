using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class ProtectedIdentityStoreTests
{
 [PostgreSqlFact]
 public async Task AuthenticatorAndRecoveryValuesAreEncryptedAndRoundTrip()
 {
  var options=new DbContextOptionsBuilder<AppDbContext>();TicketingCRM.DataConnection.DependencyInjection.ConfigureProvider(options,TicketingCRM.DataConnection.Configuration.DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!,administrative:true);
  await using var db=new PostgreSqlAppDbContext(options.Options);await db.Database.MigrateAsync();var user=new ApplicationUser{Id=Guid.NewGuid(),UserName=Guid.NewGuid().ToString()};db.Users.Add(user);await db.SaveChangesAsync();
  var provider=new EphemeralDataProtectionProvider();using var store=new ProtectedUserStore(db,provider,new IdentityErrorDescriber());
  var secret="AUTHENTICATOR-"+Guid.NewGuid();await store.SetAuthenticatorKeyAsync(user,secret,CancellationToken.None);await store.ReplaceCodesAsync(user,["recovery-a","recovery-b"],CancellationToken.None);await db.SaveChangesAsync();
  var tokens=await db.Set<IdentityUserToken<Guid>>().AsNoTracking().Where(x=>x.UserId==user.Id).ToListAsync();Assert.Equal(2,tokens.Count);Assert.All(tokens,x=>{Assert.StartsWith("protected:v1:",x.Value);Assert.DoesNotContain(secret,x.Value);Assert.DoesNotContain("recovery-a",x.Value);});
  Assert.Equal(secret,await store.GetAuthenticatorKeyAsync(user,CancellationToken.None));Assert.True(await store.RedeemCodeAsync(user,"recovery-a",CancellationToken.None));Assert.False(await store.RedeemCodeAsync(user,"recovery-a",CancellationToken.None));
 }
}
