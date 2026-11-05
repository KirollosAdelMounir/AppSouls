using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Interfaces;
using TicketingCRM.Domain.Support.Models;
using TicketingCRM.Infrastructure.Workspace;

namespace TicketingCRM.Infrastructure.Support;
public sealed class SupportReportService(IWorkspaceSessionFactory sessions,ICurrentActor actor,ServerConfiguration configuration) : ISupportReportService, ISupportInboxService
{
    private async Task<TenantMembership> AuthorizeAsync(IWorkspaceSession s,CancellationToken ct)
    {
        var member=await s.Query<TenantMembership>().SingleOrDefaultAsync(x=>x.UserId==actor.UserId&&x.IsActive,ct);
        if(member is null||actor.UserId==Guid.Empty||actor.TenantId==Guid.Empty||member.TenantId!=actor.TenantId||member.Role!=actor.Role||member.CustomerId!=actor.CustomerId||!await s.Query<Tenant>().AnyAsync(x=>x.IsActive,ct))throw new UnauthorizedAccessException();
        if (actor.SecurityStamp is { } stamp && (string.IsNullOrWhiteSpace(stamp) ||
            !await s.Query<TicketingCRM.DataConnection.Identity.ApplicationUser>().AnyAsync(x => x.Id == actor.UserId && x.SecurityStamp == stamp, ct)))
            throw new UnauthorizedAccessException("Your session has expired. Sign in again.");
        return member;
    }
    public async Task<IReadOnlyList<ReportProgress>> ListAsync(CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);var member=await AuthorizeAsync(s,cancellationToken);
        var query=s.Query<SupportTicket>();if(member.CustomerId is {} customer)query=query.Where(x=>x.CustomerId==customer);
        return await query.OrderByDescending(x=>x.CreatedAtUtc).Take(100).Select(x=>new ReportProgress(x.Id,x.Reference,x.Title,x.Stage,x.CreatedAtUtc,x.Version,new List<ReportStageHistory>())).ToListAsync(cancellationToken);
    }
    public async Task<ReportSubmissionReceipt> SubmitAsync(SubmitReportRequest request,CancellationToken cancellationToken=default)
    {
        if(request.OperationId==Guid.Empty||!Enum.IsDefined(request.Kind))throw new ArgumentException("Invalid report request.");
        await using var s=await sessions.OpenAsync(cancellationToken);var member=await AuthorizeAsync(s,cancellationToken);
        if(member.CustomerId is not {} customer)throw new InvalidOperationException("Report submission requires a customer membership.");
        var existing=await s.Query<SupportTicket>().SingleOrDefaultAsync(x=>x.OperationId==request.OperationId&&x.ReporterUserId==actor.UserId,cancellationToken);
        if(existing is not null){if(existing.ProductId!=request.ProductId||existing.Kind!=request.Kind||existing.Title!=(request.Title?.Trim()??"")||existing.Description!=(request.Description?.Trim()??"")||existing.StepsToReproduce!=(request.StepsToReproduce?.Trim()??"")||existing.ExpectedResult!=(request.ExpectedResult?.Trim()??"")||existing.ActualResult!=(request.ActualResult?.Trim()??"")||existing.ApplicationVersion!=(request.ApplicationVersion?.Trim()??"")||existing.Environment!=(request.Environment?.Trim()??""))throw new InvalidOperationException("Operation ID already used for a different report.");return new(existing.Id,existing.Reference,existing.Stage,true);}
        if(!await s.Query<Entitlement>().AnyAsync(x=>x.CustomerId==customer&&x.ProductId==request.ProductId,cancellationToken))throw new UnauthorizedAccessException("You can report issues only for your purchased software.");
        // Recipient must be the verified reporter, supplied by the authenticated user directory.
        var recipient=await s.Query<TenantMembership>().Where(x=>x.UserId==actor.UserId&&x.IsActive).Select(x=>x.UserId).SingleAsync(cancellationToken);
        var bug=request.Kind==SupportReportKind.Bug;
        var ticket=new SupportTicket{TenantId=actor.TenantId,CustomerId=customer,ProductId=request.ProductId,ReporterUserId=actor.UserId,OperationId=request.OperationId,Reference="AS-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),Kind=request.Kind,Title=WorkspaceService.Text(request.Title,160),Description=WorkspaceService.Text(request.Description,4000),StepsToReproduce=WorkspaceService.Text(request.StepsToReproduce,2000,bug),ExpectedResult=WorkspaceService.Text(request.ExpectedResult,1000,bug),ActualResult=WorkspaceService.Text(request.ActualResult,1000,bug),ApplicationVersion=WorkspaceService.Text(request.ApplicationVersion,100),Environment=WorkspaceService.Text(request.Environment,300),CreatedAtUtc=DateTimeOffset.UtcNow};
        s.Add(ticket);s.Add(new SupportTicketHistory{TenantId=actor.TenantId,TicketId=ticket.Id,ActorUserId=actor.UserId,Stage=SupportStage.Submitted,Message="Your report was submitted.",IsCustomerVisible=true,OccurredAtUtc=ticket.CreatedAtUtc});
        s.Add(new OutboxMessage{TenantId=actor.TenantId,TicketId=ticket.Id,Kind="SupportAcknowledgement",Recipient=recipient.ToString(),IdempotencyKey=$"support:{ticket.Id}:submitted",PayloadJson=JsonSerializer.Serialize(new {Reference=ticket.Reference,ProgressUrl=configuration.PublicBaseUrl.TrimEnd('/')+"/support/"+ticket.Id}),CreatedAtUtc=ticket.CreatedAtUtc});
        await s.CommitAsync(cancellationToken);return new(ticket.Id,ticket.Reference,ticket.Stage,true);
    }
    public async Task<ReportProgress?> GetProgressAsync(Guid ticketId,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);var member=await AuthorizeAsync(s,cancellationToken);
        var query=s.Query<SupportTicket>().Where(x=>x.Id==ticketId);if(member.CustomerId is {} c)query=query.Where(x=>x.CustomerId==c);
        var ticket=await query.SingleOrDefaultAsync(cancellationToken);if(ticket is null)return null;
        var history=await s.Query<SupportTicketHistory>().Where(x=>x.TicketId==ticketId&&x.IsCustomerVisible).OrderBy(x=>x.OccurredAtUtc).Select(x=>new ReportStageHistory(x.Stage,x.OccurredAtUtc,x.Message)).ToListAsync(cancellationToken);
        return new(ticket.Id,ticket.Reference,ticket.Title,ticket.Stage,ticket.CreatedAtUtc,ticket.Version,history);
    }
    public async Task ChangeStageAsync(ChangeReportStageRequest request,CancellationToken cancellationToken=default)
    {
        if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
        var digest=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{request.TicketId,request.Stage,request.AssignedTechnicalMemberId,request.CustomerVisibleMessage,request.Resolution,request.ClosureReason})));
        await using var s=await sessions.OpenAsync(cancellationToken);var member=await AuthorizeAsync(s,cancellationToken);
        var prior=await s.Query<SupportTicketHistory>().SingleOrDefaultAsync(x=>x.OperationId==request.OperationId&&x.ActorUserId==actor.UserId,cancellationToken);
        if(prior is not null){if(prior.RequestDigest!=digest)throw new InvalidOperationException("Operation ID was used for another update.");return;}
        var ticket=await s.Query<SupportTicket>().SingleOrDefaultAsync(x=>x.Id==request.TicketId,cancellationToken)??throw new ArgumentException("Report not found.");
        if(ticket.Version!=request.ExpectedVersion)throw new InvalidOperationException("Report changed; reload before updating.");
        if((int)request.Stage!=(int)ticket.Stage+1)throw new InvalidOperationException("Reports must follow adjacent stages.");
        if(member.CustomerId is {} c){if(ticket.CustomerId!=c||request.Stage!=SupportStage.Closed)throw new UnauthorizedAccessException();}
        else if(member.Role is not ("Owner" or "Admin" or "Technical"))throw new UnauthorizedAccessException();
        if(request.Stage==SupportStage.ReceivedByTechnicalMember){if(request.AssignedTechnicalMemberId is not {} assigned||!await s.Query<TenantMembership>().AnyAsync(x=>x.UserId==assigned&&x.IsActive&&(x.Role=="Technical"||x.Role=="Owner"||x.Role=="Admin"),cancellationToken))throw new ArgumentException("Assign a technical member.");ticket.AssignedTechnicalUserId=assigned;}
        if(request.Stage==SupportStage.SolutionInProgress&&ticket.AssignedTechnicalUserId is null)throw new InvalidOperationException("A technical member must accept this report.");
        if(request.Stage==SupportStage.Completed)ticket.Resolution=WorkspaceService.Text(request.Resolution,4000);
        if(request.Stage==SupportStage.Closed)ticket.ClosureReason=WorkspaceService.Text(request.ClosureReason,1000);
        ticket.Stage=request.Stage;
        s.Add(new SupportTicketHistory{OperationId=request.OperationId,RequestDigest=digest,TenantId=actor.TenantId,TicketId=ticket.Id,ActorUserId=actor.UserId,Stage=ticket.Stage,Message=WorkspaceService.Text(request.CustomerVisibleMessage,2000),IsCustomerVisible=true,OccurredAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(cancellationToken);
    }
}
