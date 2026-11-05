using TicketingCRM.Domain.Shared.Interfaces;

namespace TicketingCRM.Core.SystemConfiguration;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
