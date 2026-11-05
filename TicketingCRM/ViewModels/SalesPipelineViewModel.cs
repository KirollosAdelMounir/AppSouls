using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Sales.DTOs;
using TicketingCRM.Domain.Sales.Interfaces;
using TicketingCRM.Domain.Sales.Models;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;

public sealed class SalesPipelineViewModel(ISalesPipelineService service, IWorkspaceService workspace, ICurrentActor actor)
{
    public SalesPipelinePage? Data { get; private set; }
    public IReadOnlyList<CustomerDto> Customers { get; private set; } = [];
    public IReadOnlyList<SalesHistoryDto>? History { get; private set; }
    public string HistoryTitle { get; private set; } = "";
    public bool Loading { get; private set; } = true;
    public bool Busy { get; private set; }
    public bool Allowed { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public string Tab { get; private set; } = "Leads";
    public int Page { get; private set; } = 1;
    public LeadDraft Lead { get; private set; } = new() { OwnerUserId = actor.UserId };
    public OpportunityDraft Opportunity { get; private set; } = new() { OwnerUserId = actor.UserId };
    public Guid? ActivityRecord{get;private set;}
    public Guid ActivityVersion{get;private set;}
    public bool ActivityOpportunity{get;private set;}
    public string ActivityKind{get;set;}="Call";
    public string ActivitySubject{get;set;}="";
    public string ActivityNotes{get;set;}="";
    public DateTime? ActivityFollowUp{get;set;}
    private Guid ActivityOperation=Guid.NewGuid();
    public static IReadOnlyList<string> ActivityKinds{get;}=["Call","Meeting","Email","Task","Note"];
    public void BeginActivity(LeadDto record){ActivityRecord=record.Id;ActivityVersion=record.Version;ActivityOpportunity=false;ActivityFollowUp=record.FollowUpAtUtc?.UtcDateTime;ActivitySubject="";ActivityNotes="";ActivityOperation=Guid.NewGuid();}
    public void BeginActivity(OpportunityDto record){ActivityRecord=record.Id;ActivityVersion=record.Version;ActivityOpportunity=true;ActivityFollowUp=record.FollowUpAtUtc?.UtcDateTime;ActivitySubject="";ActivityNotes="";ActivityOperation=Guid.NewGuid();}
    public void CancelActivity()=>ActivityRecord=null;
    public Task SaveActivityAsync()=>Run(async()=>{await service.RecordActivityAsync(new(ActivityOperation,ActivityRecord??Guid.Empty,ActivityOpportunity,ActivityVersion,ActivityKind,ActivitySubject,ActivityNotes,Utc(ActivityFollowUp)));ActivityRecord=null;await Refresh();Message="Sales activity recorded.";});
    public LeadDto? Converting { get; private set; }
    public LeadDto? StageLead { get; private set; }
    public OpportunityDto? StageOpportunity { get; private set; }
    public string NextStage { get; set; } = "";
    public string Reason { get; set; } = "";
    private Guid StageOperation = Guid.NewGuid();
    public string StageTitle => StageLead?.CompanyName ?? StageOpportunity?.Title ?? "";
    public IReadOnlyList<string> StageChoices => StageLead is { } l ? l.Stage switch
    {
        LeadStage.New => ["Qualified", "Disqualified"], LeadStage.Qualified => ["Disqualified"], LeadStage.Disqualified => ["New"], _ => []
    } : StageOpportunity?.Stage switch
    {
        OpportunityStage.New => ["Qualified", "Lost"], OpportunityStage.Qualified => ["Proposal", "Lost"],
        OpportunityStage.Proposal => ["Negotiation", "Lost"], OpportunityStage.Negotiation => ["Won", "Lost"], OpportunityStage.Lost => ["Qualified"], _ => []
    };
    public bool More => Data is not null && Page * Data.PageSize < (Tab == "Leads" ? Data.LeadCount : Data.OpportunityCount);
    public IReadOnlyList<string> OwnerOptions => Data?.Owners.Select(OwnerLabel).ToArray() ?? [];
    public IReadOnlyList<string> CustomerOptions => Customers.Select((x, i) => $"{i + 1}. {x.Name} · {x.Email}").ToArray();
    private static string OwnerLabel(SalesOwnerDto owner) => string.IsNullOrWhiteSpace(owner.Name) ? owner.Email : $"{owner.Name} · {owner.Email}";
    public string OwnerLabel(Guid id) => Data?.Owners.Where(x => x.UserId == id).Select(OwnerLabel).FirstOrDefault() ?? "Choose an owner";
    public Guid OwnerId(string label) => Data?.Owners.FirstOrDefault(x => OwnerLabel(x) == label)?.UserId ?? Guid.Empty;
    public string CustomerLabel => Opportunity.CustomerId is { } id && Customers.Any(x => x.Id == id) ? CustomerOptions[Customers.ToList().FindIndex(x => x.Id == id)] : "Choose a customer";
    public void ChooseCustomer(string value) { var index = CustomerOptions.ToList().IndexOf(value); Opportunity.CustomerId = index < 0 ? null : Customers[index].Id; }
    public async Task InitializeAsync() => await Run(async () => { await Refresh(); Allowed = true; }, false);
    private async Task Refresh()
    {
        Data = await service.GetAsync(Page, 20);
        Customers = (await workspace.GetAsync()).Customers;
    }
    public Task ChangeTabAsync(string tab) => Run(async () => { if (tab is not ("Leads" or "Opportunities")) return; Tab = tab; Page = 1; await Refresh(); });
    public Task NavigateAsync(int delta) => Run(async () => { Page = Math.Max(1, Page + delta); await Refresh(); });
    public Task CreateLeadAsync() => Run(async () =>
    {
        await service.CreateLeadAsync(new(Lead.OperationId, Lead.ContactName, Lead.CompanyName, Lead.Email, Lead.Phone, Lead.Source, Lead.Notes, Lead.OwnerUserId, Utc(Lead.FollowUp)));
        Lead = new() { OwnerUserId = actor.UserId }; Page = 1; Message = "Lead added."; await Refresh();
    });
    public void BeginConversion(LeadDto lead)
    {
        Converting = lead; Opportunity = new() { OwnerUserId = lead.OwnerUserId, Title = lead.CompanyName, FollowUp = lead.FollowUpAtUtc?.UtcDateTime };
        Error = null; Message = null;
    }
    public void CancelConversion() { Converting = null; Opportunity = new() { OwnerUserId = actor.UserId }; }
    public Task CreateOpportunityAsync() => Run(async () =>
    {
        if (Converting is { } lead)
        {
            await service.ConvertLeadAsync(new(Opportunity.OperationId, lead.Id, lead.Version, Opportunity.Title, Opportunity.OwnerUserId, Opportunity.Value, Opportunity.Currency, Utc(Opportunity.FollowUp)));
            Message = "Lead converted. Its customer and opportunity are ready.";
        }
        else
        {
            await service.CreateOpportunityAsync(new(Opportunity.OperationId, Opportunity.Title, Opportunity.CustomerId ?? Guid.Empty, Opportunity.OwnerUserId, Opportunity.Value, Opportunity.Currency, Utc(Opportunity.FollowUp)));
            Message = "Opportunity added.";
        }
        CancelConversion(); Tab = "Opportunities"; Page = 1; await Refresh();
    });
    public void EditStage(LeadDto lead) { StageLead = lead; StageOpportunity = null; BeginStage(); }
    public void EditStage(OpportunityDto opportunity) { StageOpportunity = opportunity; StageLead = null; BeginStage(); }
    private void BeginStage() { NextStage = StageChoices.FirstOrDefault() ?? ""; Reason = ""; StageOperation = Guid.NewGuid(); Error = null; }
    public void CancelStage() { StageLead = null; StageOpportunity = null; }
    public Task SaveStageAsync() => Run(async () =>
    {
        if (StageLead is { } lead)
        {
            if (!Enum.TryParse<LeadStage>(NextStage, out var stage)) throw new ArgumentException("Choose a stage.");
            await service.ChangeLeadStageAsync(new(StageOperation, lead.Id, lead.Version, stage, Reason));
        }
        else if (StageOpportunity is { } opportunity)
        {
            if (!Enum.TryParse<OpportunityStage>(NextStage, out var stage)) throw new ArgumentException("Choose a stage.");
            await service.ChangeOpportunityStageAsync(new(StageOperation, opportunity.Id, opportunity.Version, stage, Reason));
        }
        else throw new ArgumentException("Choose a sales record.");
        CancelStage(); Message = "Stage updated."; await Refresh();
    });
    public Task ShowHistoryAsync(Guid id, bool opportunity, string title) => Run(async () => { History = await service.HistoryAsync(id, opportunity); HistoryTitle = title; });
    private static DateTimeOffset? Utc(DateTime? value) => value is null ? null : new(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    private async Task Run(Func<Task> action, bool clearMessage = true)
    {
        if (Busy) return; Busy = true; Error = null; if (clearMessage) Message = null;
        try { await action(); }
        catch (ArgumentException e) { Error = e.Message; }
        catch (UnauthorizedAccessException) { Allowed = false; Error = "Your organization administrator manages the sales pipeline."; }
        catch (InvalidOperationException e) { Error = e.Message; }
        catch { Error = "This operation could not be completed. Refresh to check the latest state before retrying."; }
        finally { Busy = false; Loading = false; }
    }
}
public sealed class LeadDraft
{
    public Guid OperationId { get; } = Guid.NewGuid();
    public string ContactName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
    public Guid OwnerUserId { get; set; }
    public DateTime? FollowUp { get; set; }
}
public sealed class OpportunityDraft
{
    public Guid OperationId { get; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public Guid? CustomerId { get; set; }
    public Guid OwnerUserId { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? FollowUp { get; set; }
}
