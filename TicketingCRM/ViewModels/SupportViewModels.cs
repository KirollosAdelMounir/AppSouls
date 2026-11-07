using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Interfaces;
using TicketingCRM.Domain.Support.Models;
namespace TicketingCRM.ViewModels;
public static class SupportStageLabel
{
 public static string Name(SupportStage stage)=>stage switch{SupportStage.ReceivedByTechnicalMember=>"Received by a technical member",SupportStage.SolutionInProgress=>"Solution in progress",_=>stage.ToString()};
}
public sealed class SupportInboxViewModel(ISupportInboxService service):OperationViewModel
{
 public IReadOnlyList<ReportProgress>? Reports{get;private set;}
 public Task LoadAsync()=>RunAsync(async()=>Reports=await service.ListAsync());
}
public sealed class SupportReportViewModel(ISupportReportService service,ICurrentActor actor,Guid ticketId):OperationViewModel
{
 public ReportProgress? Report{get;private set;}
 public Guid? AssignedTechnicalMemberId {get;set;} = actor.UserId;
 public string UpdateMessage{get;set;}="";public string Reason{get;set;}="";
 public SupportStage Next=>(SupportStage)((int)(Report?.Stage??SupportStage.Submitted)+1);
 private Guid Operation=Guid.NewGuid();
 public Task LoadAsync()=>RunAsync(async()=>{Report=await service.GetProgressAsync(ticketId);if(Report is null){HasError=true;Message="Report not found or access unavailable.";}});
 public Task AdvanceAsync()=>RunAsync(async()=>{if(Report is null)return;await service.ChangeStageAsync(new(ticketId,Operation,Report.Version,Next,Next==SupportStage.ReceivedByTechnicalMember?AssignedTechnicalMemberId:null,UpdateMessage,Next==SupportStage.Completed?Reason:null,Next==SupportStage.Closed?Reason:null));Operation=Guid.NewGuid();Report=await service.GetProgressAsync(ticketId);UpdateMessage="";Reason="";Message="Report updated.";});
}
