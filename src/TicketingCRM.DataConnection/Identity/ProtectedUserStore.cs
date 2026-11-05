using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TicketingCRM.DataConnection.DbContexts;

namespace TicketingCRM.DataConnection.Identity;

/// <summary>Protect authenticator secrets, recovery-code sets and other Identity token values at rest.</summary>
public sealed class ProtectedUserStore(AppDbContext context, IDataProtectionProvider protection, IdentityErrorDescriber errors)
    : UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid>(context, errors)
{
    private IDataProtector Protector(ApplicationUser user,string provider,string name)=>protection.CreateProtector("AppSouls.IdentityTokens.v1",user.Id.ToString("N"),provider,name);
    public override Task SetTokenAsync(ApplicationUser user,string loginProvider,string name,string? value,CancellationToken cancellationToken=default)
        =>base.SetTokenAsync(user,loginProvider,name,value is null?null:"protected:v1:"+Protector(user,loginProvider,name).Protect(value),cancellationToken);
    public override async Task<string?> GetTokenAsync(ApplicationUser user,string loginProvider,string name,CancellationToken cancellationToken=default)
    {
        var value=await base.GetTokenAsync(user,loginProvider,name,cancellationToken);
        if(value is null)return null;
        if(!value.StartsWith("protected:v1:",StringComparison.Ordinal))throw new InvalidOperationException("Unprotected Identity tokens require an explicit administrative migration.");
        return Protector(user,loginProvider,name).Unprotect(value[13..]);
    }
}
