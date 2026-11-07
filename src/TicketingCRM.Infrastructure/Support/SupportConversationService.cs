using TicketingCRM.Core.FileProcessing;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Delivery.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Interfaces;
using TicketingCRM.Domain.Support.Models;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Support;
public sealed partial class SupportConversationService(IWorkspaceSessionFactory sessions,ICurrentActor actor,PrivateArtifactStore files,IArtifactScanner scanner):ISupportConversationService
{
 private static string Body(string? value,int limit=8000){var text=(value??"").Trim();if(text.Length is <1||text.Length>limit)throw new ArgumentException($"Enter a message up to {limit} characters.");return text;}
 private static bool Team(string role,string topic)=>role is "Owner" or "Admin" || (topic=="Sales"?role is "Sales" or "Sales Manager":role is "Technical" or "Technical Support" or "Technical Developer");
 private static void ChatAccess(TenantMembership member,SupportChat chat){if(member.CustomerId is {} own?chat.CustomerId!=own:!Team(member.Role,chat.Topic))throw new UnauthorizedAccessException();}
 private static void TicketAccess(TenantMembership member,SupportTicket ticket){if(member.CustomerId is {} own){if(ticket.CustomerId!=own)throw new UnauthorizedAccessException();}else if(member.Role is not("Owner" or "Admin" or "Technical" or "Technical Support" or "Technical Developer" or "Sales" or "Sales Manager"))throw new UnauthorizedAccessException();}
 private static string Author(TenantMembership viewer,string name,string role,bool own)=>viewer.CustomerId is null?name:own?"You":role=="Customer"?"Customer team":role is "Sales" or "Sales Manager"?"Sales team":"Technical team";
 public async Task<IReadOnlyList<SupportProductChoice>> ChatProductsAsync(string topic,string? search=null,int page=1,CancellationToken ct=default)
 {
  if(page is <1 or >10000||(search?.Length??0)>160)throw new ArgumentException("Choose a valid catalog search and page.");
  if(topic is not("Technical" or "Sales"))throw new ArgumentException("Choose Technical or Sales.");await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);if(member.CustomerId is not {} customer)throw new UnauthorizedAccessException();
  var products=s.Repository<Product>().GetReadOnlyQueryable();if(topic=="Technical")products=products.Where(p=>s.Repository<Entitlement>().GetReadOnlyQueryable().Any(e=>e.ProductId==p.Id&&e.CustomerId==customer&&!e.IsRevoked));else products=products.Where(x=>x.IsActive);
  if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim().ToLowerInvariant();products=products.Where(x=>x.Name.ToLower().Contains(term)||x.Code.ToLower().Contains(term));}
  return await products.OrderBy(x=>x.Name).ThenBy(x=>x.Id).Skip((page-1)*100).Take(100).Select(x=>new SupportProductChoice(x.Id,x.Name)).ToListAsync(ct);
 }
 public async Task<IReadOnlyList<ChatSummary>> ChatsAsync(int page=1,CancellationToken ct=default)
 {
  if(page is <1 or >10000)throw new ArgumentException("Choose a valid page.");await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var query=s.Repository<SupportChat>().GetReadOnlyQueryable();
  if(member.CustomerId is {} own)query=query.Where(x=>x.CustomerId==own);else if(member.Role is "Sales" or "Sales Manager")query=query.Where(x=>x.Topic=="Sales");else if(member.Role is "Technical" or "Technical Support" or "Technical Developer")query=query.Where(x=>x.Topic=="Technical");
  return await (from chat in query join customer in s.Repository<Customer>().GetReadOnlyQueryable() on chat.CustomerId equals customer.Id join product in s.Repository<Product>().GetReadOnlyQueryable() on chat.ProductId equals product.Id into products from product in products.DefaultIfEmpty() orderby chat.CreatedAtUtc descending,chat.Id select new ChatSummary(chat.Id,chat.Subject,chat.Topic,chat.Status,customer.Name,product==null?null:product.Name,chat.CreatedAtUtc,chat.Version)).Skip((page-1)*50).Take(50).ToListAsync(ct);
 }
 public async Task<Guid> StartChatAsync(StartChatRequest request,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);if(member.CustomerId is not {} customer)throw new UnauthorizedAccessException();
  if(request.OperationId==Guid.Empty||request.Topic is not("Technical" or "Sales")||request.Topic=="Technical"&&request.ProductId is null)throw new ArgumentException("Choose an inquiry team and purchased software for technical inquiries.");
  if(request.ProductId is {} product){if(!await s.Repository<Product>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==product,ct)||request.Topic=="Technical"&&!await s.Repository<Entitlement>().GetReadOnlyQueryable().AnyAsync(x=>x.CustomerId==customer&&x.ProductId==product&&!x.IsRevoked,ct)||request.Topic=="Sales"&&!await s.Repository<Product>().GetReadOnlyQueryable().AnyAsync(x=>x.Id==product&&x.IsActive,ct))throw new UnauthorizedAccessException();}
  var subject=Body(request.Subject,200);var message=Body(request.Message);
  var prior=await s.Repository<SupportChat>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,ct);if(prior is not null){var first=await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,ct);if(prior.OpenedByUserId!=actor.UserId||prior.CustomerId!=customer||prior.Subject!=subject||prior.Topic!=request.Topic||prior.ProductId!=request.ProductId||first?.Body!=message)throw new InvalidOperationException("This operation ID was already used.");return prior.Id;}
  if(await s.Repository<SupportChat>().GetReadOnlyQueryable().CountAsync(x=>x.CustomerId==customer&&x.Status=="Open",ct)>=10)throw new ArgumentException("Close an existing conversation before opening another.");
  var day=new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date,TimeSpan.Zero); if(await s.Repository<SupportChat>().GetReadOnlyQueryable().CountAsync(x=>x.CustomerId==customer&&x.CreatedAtUtc>=day,ct)>=50)throw new ArgumentException("The daily conversation limit has been reached.");
  var row=await s.Repository<Customer>().GetAllQueryable().SingleAsync(x=>x.Id==customer,ct);row.Version=Guid.NewGuid();
  var now=DateTimeOffset.UtcNow;s.Repository<SupportChat>().Add(new SupportChat{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=customer,OpenedByUserId=actor.UserId,ProductId=request.ProductId,Topic=request.Topic,Subject=subject,CreatedAtUtc=now});
  var user=await s.Repository<ApplicationUser>().GetReadOnlyQueryable().SingleAsync(x=>x.Id==actor.UserId,ct);s.Repository<SupportChatMessage>().Add(new SupportChatMessage{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=customer,ChatId=request.OperationId,AuthorUserId=actor.UserId,AuthorName=user.DisplayName,AuthorRole=member.Role,Body=message,CreatedAtUtc=now});await s.CommitAsync(ct);return request.OperationId;
 }
 public async Task<ChatThread?> ChatAsync(Guid id,Guid? knownVersion=null,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var chat=await s.Repository<SupportChat>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Conversation not found.");ChatAccess(member,chat);var typing=chat.Status=="Open"&&(member.CustomerId is null?chat.CustomerTypingUntilUtc:chat.StaffTypingUntilUtc)>DateTimeOffset.UtcNow;
  if(knownVersion==chat.Version&&!typing&&chat.CustomerTypingUntilUtc is null&&chat.StaffTypingUntilUtc is null)return null;
  var customer=await s.Repository<Customer>().GetReadOnlyQueryable().SingleAsync(x=>x.Id==chat.CustomerId,ct);var product=chat.ProductId is {} productId?await s.Repository<Product>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==productId,ct):null;
  var messages=await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().Where(x=>x.ChatId==id).OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Take(100).ToListAsync(ct);
  var attachments=await s.Repository<SupportChatAttachment>().GetReadOnlyQueryable().Where(x=>x.ChatId==id).OrderBy(x=>x.CreatedAtUtc).Take(50).Select(x=>new SupportAttachmentDto(x.Id,x.MessageId,x.Filename,x.Size,x.CreatedAtUtc)).ToListAsync(ct);
  return new(new(chat.Id,chat.Subject,chat.Topic,chat.Status,customer.Name,product?.Name,chat.CreatedAtUtc,chat.Version),messages.AsEnumerable().Reverse().Select(x=>new ConversationMessage(x.Id,Author(member,x.AuthorName,x.AuthorRole,x.AuthorUserId==actor.UserId),x.Body,x.CreatedAtUtc,x.AuthorUserId==actor.UserId)).ToArray(),chat.Status=="Open",attachments,typing);
 }
 public async Task SendChatAsync(Guid id,SendConversationMessage request,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var chat=await s.Repository<SupportChat>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Conversation not found.");ChatAccess(member,chat);var text=Body(request.Body);if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
  var prior=await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==request.OperationId,ct);if(prior is not null){if(prior.ChatId!=id||prior.AuthorUserId!=actor.UserId||prior.Body!=text)throw new InvalidOperationException("The message operation ID was already used.");return;}
  if(chat.Status!="Open")throw new InvalidOperationException("This conversation is closed.");if(await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().CountAsync(x=>x.ChatId==id,ct)>=1000)throw new ArgumentException("This conversation reached its message limit. Start a new conversation.");
  var minute=DateTimeOffset.UtcNow.AddMinutes(-1);if(await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().CountAsync(x=>x.AuthorUserId==actor.UserId&&x.CreatedAtUtc>=minute,ct)>=30)throw new ArgumentException("Please wait before sending more messages.");
  if(member.CustomerId is null)chat.StaffTypingUntilUtc=null;else chat.CustomerTypingUntilUtc=null;
  var user=await s.Repository<ApplicationUser>().GetReadOnlyQueryable().SingleAsync(x=>x.Id==actor.UserId,ct);chat.Version=Guid.NewGuid();s.Repository<SupportChatMessage>().Add(new SupportChatMessage{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=chat.CustomerId,ChatId=chat.Id,AuthorUserId=actor.UserId,AuthorName=user.DisplayName,AuthorRole=member.Role,Body=text,CreatedAtUtc=DateTimeOffset.UtcNow});await s.CommitAsync(ct);
 }
 public async Task CloseChatAsync(Guid id,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);var chat=await s.Repository<SupportChat>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Conversation not found.");ChatAccess(member,chat);if(chat.Status=="Closed")return;chat.Status="Closed";chat.Version=Guid.NewGuid();chat.CustomerTypingUntilUtc=null;chat.StaffTypingUntilUtc=null;chat.ClosedByUserId=actor.UserId;chat.ClosedAtUtc=DateTimeOffset.UtcNow;await s.CommitAsync(ct);
 }
}
