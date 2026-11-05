namespace TicketingCRM.Domain.Administration.DTOs;

/// <summary>A nonsecret outcome of an explicit setup connection check.</summary>
public sealed record SetupConnectionResult(bool Success, string Code, string Message);

/// <summary>A setup failure whose fixed message is safe to show to the operator.</summary>
public sealed class ServerSetupException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
