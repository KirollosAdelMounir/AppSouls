using TicketingCRM.Core.FileProcessing;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Support;
public sealed partial class SupportConversationService
{
 public async Task<TicketThread> TicketAsync(Guid id,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var ticket=await s.Repository<SupportTicket>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Ticket not found.");TicketAccess(member,ticket);
  var rows=await s.Repository<SupportReply>().GetReadOnlyQueryable().Where(x=>x.TicketId==id).OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Take(100).ToListAsync(ct);
  var attachments=await s.Repository<SupportAttachment>().GetReadOnlyQueryable().Where(x=>x.TicketId==id).OrderBy(x=>x.CreatedAtUtc).Take(50).Select(x=>new SupportAttachmentDto(x.Id,x.ReplyId,x.Filename,x.Size,x.CreatedAtUtc)).ToListAsync(ct);
  var assignees=member.CustomerId is null?await(from m in s.Repository<TenantMembership>().GetReadOnlyQueryable() join u in s.Repository<ApplicationUser>().GetReadOnlyQueryable() on m.UserId equals u.Id where m.IsActive&&m.CustomerId==null&&(m.Role=="Owner"||m.Role=="Admin"||m.Role=="Technical"||m.Role=="Technical Developer"||m.Role=="Technical Support") orderby u.DisplayName select new SupportAssignee(u.Id,u.DisplayName)).Take(100).ToListAsync(ct):[];
  return new(ticket.Id,ticket.Reference,ticket.Title,ticket.Description,ticket.Urgency,ticket.Kind.ToString(),ticket.ApplicationVersion,ticket.Environment,ticket.StepsToReproduce,ticket.ExpectedResult,ticket.ActualResult,rows.AsEnumerable().Reverse().Select(x=>new ConversationMessage(x.Id,Author(member,x.AuthorName,x.AuthorRole,x.AuthorUserId==actor.UserId),x.Body,x.CreatedAtUtc,x.AuthorUserId==actor.UserId)).ToArray(),attachments,assignees,ticket.Stage!=SupportStage.Closed,ticket.ReporterUserId==actor.UserId);
 }
 public async Task ReplyTicketAsync(Guid id,SendConversationMessage request,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var ticket=await s.Repository<SupportTicket>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Ticket not found.");TicketAccess(member,ticket);var text=Body(request.Body);if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
  var prior=await s.Repository<SupportReply>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,ct);if(prior is not null){if(prior.TicketId!=id||prior.AuthorUserId!=actor.UserId||prior.Body!=text)throw new InvalidOperationException("This reply operation ID was already used.");return;}
  if(ticket.Stage==SupportStage.Closed)throw new InvalidOperationException("The ticket is closed.");if(await s.Repository<SupportReply>().GetReadOnlyQueryable().CountAsync(x=>x.TicketId==id,ct)>=1000)throw new ArgumentException("This ticket reached its conversation limit.");
  var minute=DateTimeOffset.UtcNow.AddMinutes(-1);if(await s.Repository<SupportReply>().GetReadOnlyQueryable().CountAsync(x=>x.AuthorUserId==actor.UserId&&x.CreatedAtUtc>=minute,ct)>=30)throw new ArgumentException("Please wait before posting more replies.");
  var user=await s.Repository<ApplicationUser>().GetReadOnlyQueryable().SingleAsync(x=>x.Id==actor.UserId,ct);ticket.Version=Guid.NewGuid();s.Repository<SupportReply>().Add(new SupportReply{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=ticket.CustomerId,TicketId=id,AuthorUserId=actor.UserId,AuthorName=user.DisplayName,AuthorRole=member.Role,Body=text,CreatedAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(ct);
 }
 public async Task AttachAsync(Guid ticketId,Guid? replyId,Guid operationId,string filename,Stream content,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var ticket=await s.Repository<SupportTicket>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new ArgumentException("Ticket not found.");TicketAccess(member,ticket);if(ticket.Stage==SupportStage.Closed)throw new InvalidOperationException("The ticket is closed.");
  if(replyId is {} reply){if(!await s.Repository<SupportReply>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==reply&&x.TicketId==ticketId&&x.CustomerId==ticket.CustomerId&&x.AuthorUserId==actor.UserId,ct))throw new UnauthorizedAccessException();}else if(ticket.ReporterUserId!=actor.UserId)throw new UnauthorizedAccessException();
  var safe=Path.GetFileName(filename??"");if(safe.Length is <1 or >160||safe!=filename||safe.Any(c=>char.IsControl(c)||c is '/' or '\\')||!new[]{".txt",".log",".pdf",".png",".jpg",".jpeg",".zip"}.Contains(Path.GetExtension(safe).ToLowerInvariant()))throw new ArgumentException("Use a text, log, PDF, image or ZIP attachment with a safe filename.");
  if(operationId==Guid.Empty)throw new ArgumentException("An upload operation ID is required.");
  var prior=await s.Repository<SupportAttachment>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==operationId,ct);
  if(prior is not null)
  {
   if(prior.TicketId!=ticketId||prior.ReplyId!=replyId||prior.UploadedByUserId!=actor.UserId||prior.Filename!=safe)throw new InvalidOperationException("The upload operation was already used for another attachment.");
   using var retry=new SizeLimitedReadStream(content,20L*1024*1024);var digest=await System.Security.Cryptography.SHA256.HashDataAsync(retry,ct);
   if(!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(digest,Convert.FromHexString(prior.Sha256)))throw new InvalidOperationException("The upload operation was already used for different file content.");
   return;
  }
  if(await s.Repository<SupportAttachment>().GetReadOnlyQueryable().CountAsync(x=>x.TicketId==ticketId,ct)>=50)throw new ArgumentException("This ticket reached its attachment limit.");
  var id=operationId;using var limited=new SizeLimitedReadStream(content,20L*1024*1024);var saved=await files.SaveAsync(id,limited,ct);var committed=false;
  try
  {
   await using(var scan=files.Open(id)){if(await scanner.ScanAsync(scan,ct)!=ArtifactScanVerdict.Clean)throw new ArgumentException("The attachment could not be confirmed clean and was not attached. Ask your administrator to check the malware scanner.");}
   var allocated=await s.Repository<SupportAttachment>().GetReadOnlyQueryable().Where(x=>x.CustomerId==ticket.CustomerId).SumAsync(x=>(long?)x.Size,ct)??0;allocated+=await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().Where(x=>x.CustomerId==ticket.CustomerId).SumAsync(x=>(long?)x.Size,ct)??0;if(allocated>5L*1024*1024*1024-saved.Size)throw new ArgumentException("Customer support attachment storage is full.");
   var ticketSize=await s.Repository<SupportAttachment>().GetReadOnlyQueryable().Where(x=>x.TicketId==ticketId).SumAsync(x=>(long?)x.Size,ct)??0;if(ticketSize>200L*1024*1024-saved.Size)throw new ArgumentException("This ticket reached its 200 MiB attachment limit.");
   var tenant=await s.Repository<Tenant>().GetAllQueryable().SingleAsync(x=>x.Id==actor.TenantId,ct);tenant.Version=Guid.NewGuid();ticket.Version=Guid.NewGuid();s.Repository<SupportAttachment>().Add(new SupportAttachment{Id=id,TenantId=actor.TenantId,CustomerId=ticket.CustomerId,TicketId=ticketId,ReplyId=replyId,UploadedByUserId=actor.UserId,Filename=safe,Size=saved.Size,Sha256=saved.Sha256,CreatedAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(ct);committed=true;
  }
  finally{if(!committed)files.Delete(id);}
 }
 public async Task<SupportAttachmentDownload> DownloadAttachmentAsync(Guid id,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var file=await s.Repository<SupportAttachment>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Attachment not found.");var ticket=await s.Repository<SupportTicket>().GetReadOnlyQueryable().SingleAsync(x=>x.Id==file.TicketId,ct);TicketAccess(member,ticket);if(!await files.VerifyAsync(id,file.Sha256,ct))throw new InvalidOperationException("The attachment failed integrity verification.");return new(file.Filename,files.Open(id));
 }
}
