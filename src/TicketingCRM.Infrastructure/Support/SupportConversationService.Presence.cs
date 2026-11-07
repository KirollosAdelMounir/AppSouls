using TicketingCRM.DataConnection.Repository.Queries;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Infrastructure.Security;
namespace TicketingCRM.Infrastructure.Support;
public sealed partial class SupportConversationService
{
 public async Task SetChatTypingAsync(Guid id,bool typing,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);
  var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);
  var chat=await s.Repository<SupportChat>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Conversation not found.");
  ChatAccess(member,chat);
  if(chat.Status!="Open")return;
  var now=DateTimeOffset.UtcNow;
  var previous=member.CustomerId is null?chat.StaffTypingUntilUtc:chat.CustomerTypingUntilUtc;
  // Bound writes even if a client sends a heartbeat for every keystroke.
  if(typing&&previous>now.AddSeconds(4)||!typing&&previous is null)return;
  var until=typing?now.AddSeconds(8):(DateTimeOffset?)null;
  if(member.CustomerId is null)chat.StaffTypingUntilUtc=until;else chat.CustomerTypingUntilUtc=until;
  chat.Version=Guid.NewGuid();
  await s.CommitAsync(ct);
 }
 public async Task<ChatNotificationFeed> ChatNotificationsAsync(CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);
  var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);
  var chats=s.Repository<SupportChat>().GetReadOnlyQueryable();
  if(member.CustomerId is {} own)chats=chats.Where(x=>x.CustomerId==own);
  else if(member.Role is "Sales" or "Sales Manager")chats=chats.Where(x=>x.Topic=="Sales");
  else if(member.Role is "Technical" or "Technical Support" or "Technical Developer")chats=chats.Where(x=>x.Topic=="Technical");
  var unread=from message in s.Repository<SupportChatMessage>().GetReadOnlyQueryable()
   join chat in chats on message.ChatId equals chat.Id
   where message.AuthorUserId!=actor.UserId&&!s.Repository<SupportChatReadState>().GetReadOnlyQueryable().Any(r=>r.UserId==actor.UserId&&r.ChatId==chat.Id&&r.ReadThroughUtc>=message.CreatedAtUtc)
   select message;
  var count=await unread.CountAsync(ct);
  var items=await unread.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Take(100).Select(x=>new ChatNotification(x.Id,x.ChatId,x.CreatedAtUtc)).ToListAsync(ct);
  var conversations=await unread.GroupBy(x=>x.ChatId).Select(x=>new {ChatId=x.Key,Count=x.Count()}).OrderByDescending(x=>x.Count).ThenBy(x=>x.ChatId).Take(100).ToListAsync(ct);
  return new(count,items,conversations.Select(x=>new ChatUnreadCount(x.ChatId,x.Count)).ToArray());
 }
 public async Task MarkChatReadAsync(Guid id,Guid messageId,CancellationToken ct=default)
 {
  await using var s=await sessions.OpenAsync(ct);var member=await WorkspaceAccess.RequireAsync(s,actor,ct:ct);
  var chat=await s.Repository<SupportChat>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ArgumentException("Conversation not found.");ChatAccess(member,chat);
  var message=await s.Repository<SupportChatMessage>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==messageId&&x.ChatId==id,ct)??throw new ArgumentException("Message not found.");
  var state=await s.Repository<SupportChatReadState>().GetAllQueryable().SingleOrDefaultAsync(x=>x.UserId==actor.UserId&&x.ChatId==id,ct);
  if(state is null)s.Repository<SupportChatReadState>().Add(new(){TenantId=actor.TenantId,CustomerId=chat.CustomerId,ChatId=id,UserId=actor.UserId,ReadThroughUtc=message.CreatedAtUtc});
  else if(state.ReadThroughUtc<message.CreatedAtUtc){state.ReadThroughUtc=message.CreatedAtUtc;state.Version=Guid.NewGuid();}
  else return;
  await s.CommitAsync(ct);
 }
}
