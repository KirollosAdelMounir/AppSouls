namespace TicketingCRM.Domain.Support.Models;

/// <summary>Four progress stages followed by final closure. Numeric values are stable.</summary>
public enum SupportStage
{
    Submitted = 0,
    ReceivedByTechnicalMember = 1,
    SolutionInProgress = 2,
    Completed = 3,
    Closed = 4
}
