namespace TicketingCRM.Core.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 1024;

    public static void Validate(string? password)
    {
        if (password is null || password.Length is < MinimumLength or > MaximumLength ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Use 8–1024 characters with uppercase, lowercase, numbers and symbols.");
    }
}
