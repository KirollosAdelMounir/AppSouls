namespace TicketingCRM.Domain.Accounts.DTOs;
public sealed record AccountProfile(string DisplayName,string Email,string Role,bool TwoFactorEnabled = false);
public sealed record UpdateAccountNameRequest(string DisplayName);
public sealed record ChangeAccountPasswordRequest(string CurrentPassword,string NewPassword);
