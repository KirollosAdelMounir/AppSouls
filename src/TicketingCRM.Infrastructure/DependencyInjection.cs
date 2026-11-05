using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TicketingCRM.Domain.Licensing.Interfaces;
using TicketingCRM.Infrastructure.Licensing;

namespace TicketingCRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddScoped<ILicenseModelValidator, LicenseModelValidator>();
        return services;
    }
    public static IServiceCollection AddCustomerDelivery(this IServiceCollection services,TicketingCRM.Core.SystemConfiguration.VaultBootstrapOptions bootstrap)
    {
        services.TryAddScoped<TicketingCRM.Domain.Customers.Interfaces.ICustomerLifecycleService,TicketingCRM.Infrastructure.Customers.CustomerLifecycleService>();
        services.TryAddScoped<TicketingCRM.Domain.Customers.Interfaces.ICustomerInvitationService,TicketingCRM.Infrastructure.Customers.CustomerInvitationService>();
        services.TryAddScoped<TicketingCRM.Domain.Delivery.Interfaces.IProductReleaseService,TicketingCRM.Infrastructure.Delivery.ProductReleaseService>();
        services.TryAddSingleton(new TicketingCRM.Core.FileProcessing.PrivateArtifactStore(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bootstrap.VaultPath)!,"..","artifacts"))));
        services.TryAddSingleton<TicketingCRM.Domain.Delivery.Interfaces.IArtifactScanner,TicketingCRM.Core.FileProcessing.ClamAvArtifactScanner>();
        return services;
    }
    public static IServiceCollection AddAccountSettings(this IServiceCollection services)
    {
        services.TryAddScoped<TicketingCRM.Domain.Accounts.Interfaces.IAccountSettingsService,TicketingCRM.Infrastructure.Accounts.AccountSettingsService>();
        return services;
    }
    public static IServiceCollection AddSalesPipeline(this IServiceCollection services)
    {
        services.TryAddScoped<TicketingCRM.Domain.Sales.Interfaces.ISalesPipelineService, TicketingCRM.Infrastructure.Sales.SalesPipelineService>();
        return services;
    }
}
