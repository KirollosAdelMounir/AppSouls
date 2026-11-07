using TicketingCRM.DataConnection.Repository.Queries;

using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.Infrastructure.Security;

/// <summary>Shared membership/session checks; callers still enforce resource-specific ownership.</summary>
public static class WorkspaceAccess
{
    public static async Task<TenantMembership> RequireAsync(IWorkspaceSession session,ICurrentActor actor,bool administrator=false,bool technical=false,CancellationToken ct=default,bool sales=false,bool developer=false)
    {
        if(actor.UserId==Guid.Empty||actor.TenantId==Guid.Empty)throw new UnauthorizedAccessException();
        var member=await session.Repository<TenantMembership>().GetAllQueryable().SingleOrDefaultAsync(x=>x.UserId==actor.UserId&&x.TenantId==actor.TenantId&&x.IsActive,ct);
        if(member is null)throw new UnauthorizedAccessException("Your account is disabled or no longer has access. Please contact the company to restore your access.");
        if(member.Role!=actor.Role||member.CustomerId!=actor.CustomerId||!await session.Repository<Tenant>().GetAllQueryable().AnyAsync(x=>x.Id==actor.TenantId&&x.IsActive,ct))throw new UnauthorizedAccessException();
        if(actor.SecurityStamp is {} stamp&&(string.IsNullOrWhiteSpace(stamp)||!await session.Repository<ApplicationUser>().GetAllQueryable().AnyAsync(x=>x.Id==actor.UserId&&x.SecurityStamp==stamp,ct)))throw new UnauthorizedAccessException();
        if(member.CustomerId is null&&member.Role is not ("Owner" or "Admin" or "Technical" or "Technical Developer" or "Technical Support" or "Sales" or "Sales Manager"))throw new UnauthorizedAccessException();
        if(member.CustomerId is not null&&member.Role!="Customer")throw new UnauthorizedAccessException();
        if(administrator&&(member.CustomerId is not null||member.Role is not ("Owner" or "Admin")))throw new UnauthorizedAccessException();
        if(technical&&(member.CustomerId is not null||member.Role is not ("Owner" or "Admin" or "Technical" or "Technical Developer" or "Technical Support")))throw new UnauthorizedAccessException();
        if(sales&&(member.CustomerId is not null||member.Role is not ("Owner" or "Admin" or "Sales" or "Sales Manager")))throw new UnauthorizedAccessException();
        if(developer&&(member.CustomerId is not null||member.Role is not ("Owner" or "Admin" or "Technical" or "Technical Developer")))throw new UnauthorizedAccessException();
        return member;
    }
}
