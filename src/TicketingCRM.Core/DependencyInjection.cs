using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TicketingCRM.Core.Security;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Domain.Security.Interfaces;
using TicketingCRM.Domain.Shared.Interfaces;

namespace TicketingCRM.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IContentHasher, Sha256ContentHasher>();
        return services;
    }
}
