namespace TicketingCRM.Domain.Persistence.Models;
public sealed class SupportChat : TenantRecord
{
 public Guid CustomerId {get;set;}
 public Guid OpenedByUserId {get;set;}
 public Guid? ProductId {get;set;}
 public string Topic {get;set;}="Technical";
 public string Subject {get;set;}="";
 public string Status {get;set;}="Open";
 public DateTimeOffset CreatedAtUtc {get;set;}
 public DateTimeOffset? ClosedAtUtc {get;set;}
 public Guid? ClosedByUserId {get;set;}
 public DateTimeOffset? CustomerTypingUntilUtc {get;set;}
 public DateTimeOffset? StaffTypingUntilUtc {get;set;}
}
public sealed class SupportReply : TenantRecord
{
 public Guid CustomerId {get;set;}
 public Guid TicketId {get;set;}
 public Guid AuthorUserId {get;set;}
 public string AuthorName {get;set;}="";
 public string AuthorRole {get;set;}="";
 public string Body {get;set;}="";
 public DateTimeOffset CreatedAtUtc {get;set;}
}
public sealed class SupportChatMessage : TenantRecord
{
 public Guid CustomerId {get;set;}
 public Guid ChatId {get;set;}
 public Guid AuthorUserId {get;set;}
 public string AuthorName {get;set;}="";
 public string AuthorRole {get;set;}="";
 public string Body {get;set;}="";
 public DateTimeOffset CreatedAtUtc {get;set;}
}
public sealed class SupportAttachment : TenantRecord
{
 public Guid CustomerId {get;set;}
 public Guid TicketId {get;set;}
 public Guid? ReplyId {get;set;}
 public Guid UploadedByUserId {get;set;}
 public string Filename {get;set;}="";
 public long Size {get;set;}
 public string Sha256 {get;set;}="";
 public DateTimeOffset CreatedAtUtc {get;set;}
}

public sealed class SupportChatAttachment : TenantRecord
{
 public Guid CustomerId {get;set;}
 public Guid ChatId {get;set;}
 public Guid MessageId {get;set;}
 public Guid UploadedByUserId {get;set;}
 public string Filename {get;set;}="";
 public long Size {get;set;}
 public string Sha256 {get;set;}="";
 public DateTimeOffset CreatedAtUtc {get;set;}
}

public sealed class SupportChatReadState : TenantRecord
{
 public Guid CustomerId {get;set;}
 public Guid ChatId {get;set;}
 public Guid UserId {get;set;}
 public DateTimeOffset ReadThroughUtc {get;set;}
}
