using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Interfaces;
namespace TicketingCRM.Controllers;
[ApiController,Authorize,Route("api/support-conversations"),AutoValidateAntiforgeryToken,EnableRateLimiting("licensing"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SupportConversationsController(ISupportConversationService service):ControllerBase
{
 [HttpGet("products")]public Task<IReadOnlyList<SupportProductChoice>> Products(string topic,string? search=null,int page=1,CancellationToken ct=default)=>service.ChatProductsAsync(topic,search,page,ct);
 [HttpGet("chats")]public Task<IReadOnlyList<ChatSummary>> Chats(int page=1,CancellationToken ct=default)=>service.ChatsAsync(page,ct);
 [HttpPost("chats"),RequestSizeLimit(65536)]public async Task<ActionResult<Guid>> Start(StartChatRequest request,CancellationToken ct)=>Ok(await service.StartChatAsync(request,ct));
 [HttpGet("chats/{id:guid}")]public Task<ChatThread?> Chat(Guid id,Guid? knownVersion,CancellationToken ct)=>service.ChatAsync(id,knownVersion,ct);
 [HttpPost("chats/{id:guid}/messages"),RequestSizeLimit(65536)]public async Task<IActionResult> Send(Guid id,SendConversationMessage request,CancellationToken ct){await service.SendChatAsync(id,request,ct);return NoContent();}
 [HttpPost("chats/{id:guid}/read")]public async Task<IActionResult> Read(Guid id,MarkChatReadRequest request,CancellationToken ct){await service.MarkChatReadAsync(id,request.MessageId,ct);return NoContent();}
 [HttpPost("chats/{id:guid}/typing")]public async Task<IActionResult> Typing(Guid id,[FromBody]bool typing,CancellationToken ct){await service.SetChatTypingAsync(id,typing,ct);return NoContent();}
 [HttpGet("chat-notifications")]public Task<ChatNotificationFeed> Notifications(CancellationToken ct)=>service.ChatNotificationsAsync(ct);
 [HttpPost("chats/{id:guid}/attachments"),RequestSizeLimit(22020096),RequestFormLimits(MultipartBodyLengthLimit=22020096)]public async Task<IActionResult> AttachChat(Guid id,[FromForm]Guid messageId,[FromForm]Guid operationId,IFormFile file,CancellationToken ct){await using var content=file.OpenReadStream();await service.AttachChatAsync(id,messageId,operationId,Path.GetFileName(file.FileName),content,ct);return NoContent();}
 [HttpGet("chat-attachments/{id:guid}")]public async Task<IActionResult> DownloadChat(Guid id,CancellationToken ct){var file=await service.DownloadChatAttachmentAsync(id,ct);return File(file.Content,"application/octet-stream",file.Filename);}
 [HttpPost("chats/{id:guid}/close")]public async Task<IActionResult> Close(Guid id,CancellationToken ct){await service.CloseChatAsync(id,ct);return NoContent();}
 [HttpGet("tickets/{id:guid}")]public Task<TicketThread> Ticket(Guid id,CancellationToken ct)=>service.TicketAsync(id,ct);
 [HttpPost("tickets/{id:guid}/replies"),RequestSizeLimit(65536)]public async Task<IActionResult> Reply(Guid id,SendConversationMessage request,CancellationToken ct){await service.ReplyTicketAsync(id,request,ct);return NoContent();}
 [HttpPost("tickets/{id:guid}/attachments"),RequestSizeLimit(22020096),RequestFormLimits(MultipartBodyLengthLimit=22020096)]public async Task<IActionResult> Attach(Guid id,[FromForm]Guid? replyId,[FromForm]Guid operationId,IFormFile file,CancellationToken ct){await using var content=file.OpenReadStream();await service.AttachAsync(id,replyId,operationId,Path.GetFileName(file.FileName),content,ct);return NoContent();}
 [HttpGet("attachments/{id:guid}")]public async Task<IActionResult> Download(Guid id,CancellationToken ct){var file=await service.DownloadAttachmentAsync(id,ct);return File(file.Content,"application/octet-stream",file.Filename);}
}
