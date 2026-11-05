using Microsoft.AspNetCore.Identity;

namespace TicketingCRM.DataConnection.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsPlatformAdministrator { get; set; }
    public string DisplayName { get; set; } = "";
}
