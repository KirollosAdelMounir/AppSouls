using TicketingCRM.Core.FileProcessing;
using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Support;
public sealed partial class SupportConversationService
{
 public async Task AttachChatAsync(Guid chatId,Guid messageId,Guid operationId,string filename,Stream content,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var conversation=await s.Repository<SupportChat>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==chatId,ct)??throw new ArgumentException("Conversation not found.");ChatAccess(member,conversation);if(conversation.Status=="Closed")throw new InvalidOperationException("The conversation is closed.");
  if(messageId != Guid.Empty){if(!await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==messageId&&x.ChatId==chatId&&x.CustomerId==conversation.CustomerId&&x.AuthorUserId==actor.UserId,ct))throw new UnauthorizedAccessException();}else throw new ArgumentException("Choose one of your messages for the attachment.");
  var safe=Path.GetFileName(filename??"");if(safe.Length is <1 or >160||safe!=filename||safe.Any(c=>char.IsControl(c)||c is '/' or '\\')||!new[]{".txt",".log",".pdf",".png",".jpg",".jpeg",".zip"}.Contains(Path.GetExtension(safe).ToLowerInvariant()))throw new ArgumentException("Use a text, log, PDF, image or ZIP attachment with a safe filename.");
  if(operationId==Guid.Empty)throw new ArgumentException("An upload operation ID is required.");
  var prior=await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==operationId,ct);
  if(prior is not null)
  {
   if(prior.ChatId!=chatId||prior.MessageId!=messageId||prior.UploadedByUserId!=actor.UserId||prior.Filename!=safe)throw new InvalidOperationException("The upload operation was already used for another attachment.");
   using var retry=new SizeLimitedReadStream(content,20L*1024*1024);var digest=await System.Security.Cryptography.SHA256.HashDataAsync(retry,ct);
   if(!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(digest,Convert.FromHexString(prior.Sha256)))throw new InvalidOperationException("The upload operation was already used for different file content.");
   return;
  }
  if(await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().CountAsync(x=>x.ChatId==chatId,ct)>=50)throw new ArgumentException("This conversation reached its attachment limit.");
  var id=operationId;using var limited=new SizeLimitedReadStream(content,20L*1024*1024);var saved=await files.SaveAsync(id,limited,ct);var committed=false;
  try
  {
   await using(var scan=files.Open(id)){if(await scanner.ScanAsync(scan,ct)!=ArtifactScanVerdict.Clean)throw new ArgumentException("The attachment could not be confirmed clean and was not attached. Ask your administrator to check the malware scanner.");}
   var allocated=await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().Where(x=>x.CustomerId==conversation.CustomerId).SumAsync(x=>(long?)x.Size,ct)??0;allocated+=await s.Repository<SupportAttachment>().GetReadOnlyQueryable().Where(x=>x.CustomerId==conversation.CustomerId).SumAsync(x=>(long?)x.Size,ct)??0;if(allocated>5L*1024*1024*1024-saved.Size)throw new ArgumentException("Customer support attachment storage is full.");
   var conversationSize=await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().Where(x=>x.ChatId==chatId).SumAsync(x=>(long?)x.Size,ct)??0;if(conversationSize>200L*1024*1024-saved.Size)throw new ArgumentException("This conversation reached its 200 MiB attachment limit.");
   var tenant=await s.Repository<Tenant>().GetAllQueryable().SingleAsync(x=>x.Id==actor.TenantId,ct);tenant.Version=Guid.NewGuid();conversation.Version=Guid.NewGuid();s.Repository<SupportChatAttachment>().Add(new SupportChatAttachment{Id=id,TenantId=actor.TenantId,CustomerId=conversation.CustomerId,ChatId=chatId,MessageId=messageId,UploadedByUserId=actor.UserId,Filename=safe,Size=saved.Size,Sha256=saved.Sha256,CreatedAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(ct);committed=true;
  }
  finally{if(!committed)files.Delete(id);}
 }
 public async Task<SupportAttachmentDownload> DownloadChatAttachmentAsync(Guid id,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var file=await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Attachment not found.");var conversation=await s.Repository<SupportChat>().GetReadOnlyQueryable().SingleAsync(x=>x.Id==file.ChatId,ct);ChatAccess(member,conversation);if(!await files.VerifyAsync(id,file.Sha256,ct))throw new InvalidOperationException("The attachment failed integrity verification.");return new(file.Filename,files.Open(id));
 }
}
