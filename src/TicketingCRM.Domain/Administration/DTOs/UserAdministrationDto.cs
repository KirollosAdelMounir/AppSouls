namespace TicketingCRM.Domain.Administration.DTOs;

public sealed record ManagedUserDto(Guid MembershipId, Guid UserId, string DisplayName, string Email,
    string Role, Guid? CustomerId, bool IsActive, Guid Version);
// A class prevents generated ToString from exposing the initial password.
public sealed class CreateManagedUserRequest
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string InitialPassword { get; set; } = "";
    public string Role { get; set; } = "Technical";
    public Guid? CustomerId { get; set; }
    public bool EmailOwnershipVerified { get; set; }
}
public sealed record ChangeMemberAccessRequest(Guid MembershipId, Guid ExpectedVersion, bool IsActive);
public sealed class InitialAdministratorRequest
{
    public string SetupToken { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
