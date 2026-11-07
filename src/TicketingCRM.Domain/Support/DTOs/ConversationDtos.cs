namespace TicketingCRM.Domain.Support.DTOs;
public sealed record SupportProductChoice(Guid Id,string Name);
public sealed record SupportAssignee(Guid UserId,string Name);
public sealed record ChatSummary(Guid Id,string Subject,string Topic,string Status,string CustomerName,string? ProductName,DateTimeOffset CreatedAtUtc,Guid Version);
public sealed record ConversationMessage(Guid Id,string Author,string Body,DateTimeOffset CreatedAtUtc,bool Own);
public sealed record ChatThread(ChatSummary Chat,IReadOnlyList<ConversationMessage> Messages,bool CanReply,IReadOnlyList<SupportAttachmentDto> Attachments,bool OtherPartyTyping);
public sealed record StartChatRequest(Guid OperationId,string Topic,Guid? ProductId,string Subject,string Message);
public sealed record SendConversationMessage(Guid OperationId,string Body);
public sealed record SupportAttachmentDto(Guid Id,Guid? ReplyId,string Filename,long Size,DateTimeOffset UploadedAtUtc);
public sealed record TicketThread(Guid Id,string Reference,string Title,string Description,string Urgency,string Kind,string ApplicationVersion,string Environment,string Steps,string Expected,string Actual,IReadOnlyList<ConversationMessage> Messages,IReadOnlyList<SupportAttachmentDto> Attachments,IReadOnlyList<SupportAssignee> Assignees,bool CanReply,bool CanAttachInitial = false);
public sealed record SupportAttachmentDownload(string Filename,Stream Content);

public sealed record ChatNotification(Guid Id,Guid ChatId,DateTimeOffset CreatedAtUtc);
public sealed record ChatUnreadCount(Guid ChatId,int Count);
public sealed record ChatNotificationFeed(int UnreadCount,IReadOnlyList<ChatNotification> Items,IReadOnlyList<ChatUnreadCount> Conversations);
public sealed record MarkChatReadRequest(Guid MessageId);
