using Microsoft.AspNetCore.Authorization;
using TicketingCRM.Domain.Configuration.Interfaces;
namespace TicketingCRM.Security;
public sealed class PlatformAdministratorRequirement : IAuthorizationRequirement;
public sealed class PlatformAdministratorHandler(IServerSettingsService settings) : AuthorizationHandler<PlatformAdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,PlatformAdministratorRequirement requirement)
    {
        if(context.User.Identity?.IsAuthenticated!=true)return;
        try{await settings.GetAsync();context.Succeed(requirement);}
        catch(UnauthorizedAccessException){}
        catch(InvalidOperationException){}
    }
}
