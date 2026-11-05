using System.ComponentModel.DataAnnotations;

namespace TicketingCRM.ViewModels;

public sealed class BugReportViewModel : IValidatableObject
{
    public string Kind { get; set; } = "Bug";
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [Required, StringLength(160)] public string Product { get; set; } = "";
    [Required, StringLength(4000, MinimumLength = 10)] public string Description { get; set; } = "";
    [StringLength(2000)] public string Steps { get; set; } = "";
    [StringLength(1000)] public string Expected { get; set; } = "";
    [StringLength(1000)] public string Actual { get; set; } = "";
    [Required, StringLength(100)] public string Version { get; set; } = "";
    [Required, StringLength(300)] public string Environment { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Kind != "Bug") yield break;
        if (string.IsNullOrWhiteSpace(Steps)) yield return new("Add steps to reproduce the bug.", [nameof(Steps)]);
        if (string.IsNullOrWhiteSpace(Expected)) yield return new("Describe the expected result.", [nameof(Expected)]);
        if (string.IsNullOrWhiteSpace(Actual)) yield return new("Describe the actual result.", [nameof(Actual)]);
    }
}
