namespace TicketingCRM.Domain.Shared.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
