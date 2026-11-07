using TicketingCRM.Domain.Support.DTOs;
namespace TicketingCRM.Domain.Support.Interfaces;
public interface ISupportConversationService
{
 Task<IReadOnlyList<SupportProductChoice>> ChatProductsAsync(string topic,string? search=null,int page=1,CancellationToken ct=default);
 Task<IReadOnlyList<ChatSummary>> ChatsAsync(int page=1,CancellationToken ct=default);
 Task<Guid> StartChatAsync(StartChatRequest request,CancellationToken ct=default);
 Task<ChatThread?> ChatAsync(Guid id,Guid? knownVersion=null,CancellationToken ct=default);
 Task SendChatAsync(Guid id,SendConversationMessage request,CancellationToken ct=default);
 Task SetChatTypingAsync(Guid id,bool typing,CancellationToken ct=default);
 Task<ChatNotificationFeed> ChatNotificationsAsync(CancellationToken ct=default);
 Task MarkChatReadAsync(Guid id,Guid messageId,CancellationToken ct=default);
 Task AttachChatAsync(Guid chatId,Guid messageId,Guid operationId,string filename,Stream content,CancellationToken ct=default);
 Task<SupportAttachmentDownload> DownloadChatAttachmentAsync(Guid id,CancellationToken ct=default);
 Task CloseChatAsync(Guid id,CancellationToken ct=default);
 Task<TicketThread> TicketAsync(Guid id,CancellationToken ct=default);
 Task ReplyTicketAsync(Guid id,SendConversationMessage request,CancellationToken ct=default);
 Task AttachAsync(Guid ticketId,Guid? replyId,Guid operationId,string filename,Stream content,CancellationToken ct=default);
 Task<SupportAttachmentDownload> DownloadAttachmentAsync(Guid id,CancellationToken ct=default);
}
public interface IRichTextRenderer { string Render(string text); }
