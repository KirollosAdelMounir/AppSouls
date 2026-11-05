namespace TicketingCRM.ViewModels;

// Presentation metadata only. Counts remain unavailable until authorized services are connected.
public sealed class OverviewViewModel
{
    public IReadOnlyList<WorkspaceMetric> Metrics { get; } = [
        new("Customers", "customers", "Your customer relationships", "/customers"),
        new("Active contracts", "contracts", "Agreements and entitlements", "/contracts"),
        new("Issued licenses", "licenses", "Access to your software", "/licenses"),
        new("Installations", "installations", "Online and offline activations", "/installations")];
}
public sealed record WorkspaceMetric(string Title, string Icon, string Description, string Link);
