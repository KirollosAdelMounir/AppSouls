using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.Infrastructure.Security;

/// <summary>Shared membership/session checks; callers still enforce resource-specific ownership.</summary>
public static class WorkspaceAccess
{
    public static async Task<TenantMembership> RequireAsync(IWorkspaceSession session,ICurrentActor actor,bool administrator=false,bool technical=false,CancellationToken ct=default)
    {
        if(actor.UserId==Guid.Empty||actor.TenantId==Guid.Empty)throw new UnauthorizedAccessException();
        var member=await session.Query<TenantMembership>().SingleOrDefaultAsync(x=>x.UserId==actor.UserId&&x.TenantId==actor.TenantId&&x.IsActive,ct);
        if(member is null||member.Role!=actor.Role||member.CustomerId!=actor.CustomerId||!await session.Query<Tenant>().AnyAsync(x=>x.Id==actor.TenantId&&x.IsActive,ct))throw new UnauthorizedAccessException();
        if(actor.SecurityStamp is {} stamp&&(string.IsNullOrWhiteSpace(stamp)||!await session.Query<ApplicationUser>().AnyAsync(x=>x.Id==actor.UserId&&x.SecurityStamp==stamp,ct)))throw new UnauthorizedAccessException();
        if(member.CustomerId is null&&member.Role is not ("Owner" or "Admin" or "Technical"))throw new UnauthorizedAccessException();
        if(member.CustomerId is not null&&member.Role!="Customer")throw new UnauthorizedAccessException();
        if(administrator&&(member.CustomerId is not null||member.Role is not ("Owner" or "Admin")))throw new UnauthorizedAccessException();
        if(technical&&(member.CustomerId is not null||member.Role is not ("Owner" or "Admin" or "Technical")))throw new UnauthorizedAccessException();
        return member;
    }
}
