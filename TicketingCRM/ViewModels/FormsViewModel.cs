using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Forms.Interfaces;
namespace TicketingCRM.ViewModels;

public sealed class FormQuestionModel
{
    public string Key { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "";
    public string Type { get; set; } = "Text";
    public bool Required { get; set; }
    public string Choices { get; set; } = "";
    public FormField ToField() => new(Key, Label, Type, Required, Type == "Choice" ? Choices.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : []);
}
public sealed class FormsViewModel(IFormService service) : OperationViewModel
{
    public FormPage? Forms { get; private set; }
    public FormSubmissionsPage? Submissions { get; private set; }
    public Guid? SelectedId { get; private set; }
    public FormDefinition? Definition { get; private set; }
    public Guid? LastSavedId { get; private set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<FormQuestionModel> Questions { get; } = [new()];
    public static readonly string[] FieldTypes = ["Text", "Long text", "Email", "Choice", "Number", "Phone", "Website", "Rating", "Yes / No"];
    public Task LoadAsync(int page = 1) => RunAsync(async () => Forms = await service.ListAsync(page));
    public Task CreateAsync() => RunAsync(async () =>
    {
        LastSavedId = await service.CreateAsync(new(Title, Description, Questions.Select(q => q.ToField()).ToArray()));
        Title = ""; Description = ""; Questions.Clear(); Questions.Add(new());
        Forms = await service.ListAsync(); Message = "Draft saved. Publish it when you are ready to collect responses.";
    });
    public Task LoadEditorAsync(Guid id) => RunAsync(async () =>
    {
        Definition = await service.GetAsync(id); SelectedId = id; Title = Definition.Title; Description = Definition.Description;
        Questions.Clear(); Questions.AddRange(Definition.Fields.Select(f => new FormQuestionModel { Key = f.Key, Label = f.Label, Type = f.Type, Required = f.Required, Choices = string.Join('\n', f.Options) }));
    });
    public Task UpdateAsync() => RunAsync(async () =>
    {
        if (Definition is null) throw new ArgumentException("Load a form first.");
        await service.UpdateAsync(Definition.Id, new(Definition.Version, Title, Description, Questions.Select(q => q.ToField()).ToArray()));
        LastSavedId = Definition.Id; Definition = await service.GetAsync(Definition.Id); Message = "Draft updated.";
    });
    public Task ChangeStateAsync(FormSummary form) => RunAsync(async () =>
    {
        await service.ChangeStateAsync(form.Id, new(form.Version, form.Status != "Published"));
        Forms = await service.ListAsync(Forms?.Page ?? 1); Message = form.Status == "Published" ? "Form closed. Existing submissions are retained." : "Form published. The public link is ready to share.";
    });
    public Task ReadAsync(Guid id, int page = 1) => RunAsync(async () => { Submissions = await service.SubmissionsAsync(id, page); SelectedId = id; });
    public void AddQuestion() { if (Questions.Count < 20) Questions.Add(new()); }
    public void RemoveQuestion(FormQuestionModel question) { if (Questions.Count > 1) Questions.Remove(question); }
    public void MoveQuestion(FormQuestionModel question, int direction)
    {
        var index = Questions.IndexOf(question); var next = index + direction;
        if (index < 0 || next < 0 || next >= Questions.Count) return;
        (Questions[index], Questions[next]) = (Questions[next], Questions[index]);
    }
}
