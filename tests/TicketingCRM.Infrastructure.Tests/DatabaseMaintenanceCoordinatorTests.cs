using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Infrastructure.Configuration;
using Xunit;

namespace TicketingCRM.Infrastructure.Tests;

public sealed class DatabaseMaintenanceCoordinatorTests
{
    [Fact]
    public void HostHandoffKeepsMaintenanceGateUntilDisposalAndPreservesMeasuredProgress()
    {
        var coordinator = new DatabaseMaintenanceCoordinator();
        var source = new ServerConfiguration { DatabaseConnectionString = "opaque" };
        var request = new DatabaseMaintenanceRequest { Mode = "Copy", ConnectionString = "other" };
        coordinator.Schedule(request, source, Guid.NewGuid(), Guid.NewGuid(), "stamp");
        request.ConnectionString = "changed";
        Assert.Equal("other", coordinator.Request!.ConnectionString);
        Assert.Throws<InvalidOperationException>(() => coordinator.Schedule(request, source, Guid.NewGuid(), Guid.NewGuid(), "stamp"));
        Assert.True(coordinator.TryStart()); Assert.False(coordinator.TryStart());
        Assert.Throws<InvalidOperationException>(() => coordinator.Finish());
        coordinator.Report(new("Copying records", 50, "Customers", 20, 40, 5));
        coordinator.Report(new("Validating", 99));
        Assert.Equal(20, coordinator.Progress.RowsCopied); Assert.Equal(40, coordinator.Progress.TotalRows);
        Assert.Equal(2, coordinator.Progress.Stages!.Count);
        coordinator.Report(coordinator.Progress with { Stage = "Complete", Finished = true, Success = true, Percentage = 100 });
        coordinator.Finish(); Assert.True(coordinator.MaintenanceMode);
        coordinator.ResetAfterHostStopped(); Assert.False(coordinator.MaintenanceMode); Assert.Null(coordinator.Request);
    }
}
