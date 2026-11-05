namespace TicketingCRM.ViewModels;
/// <summary>Shared presentation operation state. Resource authorization remains in services.</summary>
public abstract class OperationViewModel
{
    public bool Busy { get; protected set; }
    public bool HasError { get; protected set; }
    public string? Message { get; protected set; }
    protected async Task RunAsync(Func<Task> action)
    {
        if (Busy) return; Busy = true; HasError = false; Message = null;
        try { await action(); }
        catch (UnauthorizedAccessException) { HasError = true; Message = "Your current membership does not allow this operation."; }
        catch (ArgumentException e) { HasError = true; Message = e.Message; }
        catch (NotSupportedException e) { HasError = true; Message = e.Message; }
        catch (InvalidOperationException) { HasError = true; Message = "The operation could not be completed. Reload the latest state and retry."; }
        catch (Exception) { HasError = true; Message = "The service is unavailable. Please retry."; }
        finally { Busy = false; }
    }
}
public static class RecordChoice
{
    public static Guid Id(string value) => ParseId(value) ?? throw new ArgumentException("Choose a record.");
    public static Guid TryId(string value) => ParseId(value) ?? Guid.Empty;
    private static Guid? ParseId(string value) => Guid.TryParse(value.Split(" · ").Last(), out var id) ? id : null;
    public static DateTimeOffset? Utc(DateTime? date) => date is null ? null : new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc));
}
