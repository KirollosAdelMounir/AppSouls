using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class ConversationsViewModel(ISupportConversationService service,ISupportReportService reports,ICurrentActor actor):OperationViewModel
{
 public IReadOnlyList<ChatSummary>? Chats{get;private set;}
 public IReadOnlyList<SupportProductChoice> Products{get;private set;}=[];
 public ChatThread? Chat{get;private set;}
 public bool LiveUpdatesStopped{get;private set;}
 public TicketThread? Ticket{get;private set;}
 public SupportReportViewModel? Progress{get;private set;}
 public bool IsCustomer=>actor.CustomerId is not null;
 public bool CanStart=>IsCustomer;
 public string ProductSearch {get;set;}="";
 public int ProductPage {get;private set;}=1;
 public string Topic{get;set;}="Technical";
 public string Product{get;set;}="";
 public string Subject{get;set;}="";
 public string Text{get;set;}="";
 public Guid? SavedChatId{get;private set;}
 public Guid? AttachmentMessageId{get;set;}
 private DateTimeOffset LastTypingHeartbeat,LastChatInput;
 private bool TypingAnnounced;
 public Guid? LastReplyId{get;private set;}
 public string AssigneeLabel{get;set;}="";
 public IReadOnlyList<string> ProductOptions=>new[]{"No specific software"}.Concat(Products.Select((x,i)=>$"{i+1}. {x.Name}")).ToArray();
 public IReadOnlyList<string> AssigneeOptions=>Ticket?.Assignees.Select((x,i)=>$"{i+1}. {x.Name}").ToArray()??[];
 private Guid Operation=Guid.NewGuid();
 private readonly SemaphoreSlim ChatGate=new(1,1);
 private async Task ChatOperation(Func<Task> action){await ChatGate.WaitAsync();try{await action();}finally{ChatGate.Release();}}
 public Task LoadChatsAsync(int page=1)=>RunAsync(async()=>Chats=await service.ChatsAsync(page));
 public Task LoadProductsAsync(int page=1)=>RunAsync(async()=>{ProductPage=Math.Max(1,page);Products=await service.ChatProductsAsync(Topic,ProductSearch,ProductPage);Product="";});
 public Task ChangeTopicAsync(string topic)=>RunAsync(async()=>{Topic=topic;ProductSearch="";ProductPage=1;Products=await service.ChatProductsAsync(topic);Product="";});
 public Task StartAsync()=>RunAsync(async()=>{var index=ProductOptions.ToList().IndexOf(Product)-1;SavedChatId=await service.StartChatAsync(new(Operation,Topic,index<0?null:Products[index].Id,Subject,Text));Message="Conversation created.";});
 public Task LoadChatAsync(Guid id)=>RunAsync(()=>ChatOperation(async()=>Chat=await service.ChatAsync(id)));
 public async Task<bool> PollChatAsync(Guid id,CancellationToken ct)
 {
  var entered=false;
  try { await ChatGate.WaitAsync(ct);entered=true;
   var now=DateTimeOffset.UtcNow;var typing=Text.Length>0&&now-LastChatInput<TimeSpan.FromSeconds(4);
   if(typing&&now-LastTypingHeartbeat>=TimeSpan.FromSeconds(4)||!typing&&TypingAnnounced)
   {await service.SetChatTypingAsync(id,typing,ct);LastTypingHeartbeat=now;TypingAnnounced=typing;}
   var changed=await service.ChatAsync(id,Chat?.Chat.Version,ct);if(changed is null)return false;Chat=changed;return true; }
  catch(OperationCanceledException)when(ct.IsCancellationRequested){return false;}
  catch(UnauthorizedAccessException){Chat=null;LiveUpdatesStopped=true;HasError=true;Message="Your account is disabled or no longer has access. Please contact the company.";return true;}
  catch(Exception){LiveUpdatesStopped=true;HasError=true;Message="Live updates paused. Refresh to check the latest conversation.";return true;}
  finally{if(entered)ChatGate.Release();}
 }
 public Task SendChatAsync(Guid id)=>RunAsync(()=>ChatOperation(async()=>{await service.SendChatAsync(id,new(Operation,Text));AttachmentMessageId=Operation;Operation=Guid.NewGuid();Text="";Chat=await service.ChatAsync(id);Message="Message recorded.";}));
 public void ChangeChatText(string text){Text=text;LastChatInput=DateTimeOffset.UtcNow;}
 public async Task AttachChatAsync(Guid id,BrowserUpload upload)
 {
  if(AttachmentMessageId is not {} message)throw new ArgumentException("Choose one of your messages first.");
  await RunAsync(()=>ChatOperation(async()=>{await service.AttachChatAsync(id,message,upload.OperationId,upload.Filename,upload.Content,upload.CancellationToken);Chat=await service.ChatAsync(id,ct:upload.CancellationToken);Message="Attachment scanned and saved.";}));
  if(HasError)throw new InvalidOperationException(Message);
 }
 public Task CloseChatAsync(Guid id)=>RunAsync(()=>ChatOperation(async()=>{await service.CloseChatAsync(id);Chat=await service.ChatAsync(id);Message="Conversation closed.";}));
 public Task LoadTicketAsync(Guid id)=>RunAsync(async()=>{Ticket=await service.TicketAsync(id);Progress=new(reports,actor,id);await Progress.LoadAsync();var index=Ticket.Assignees.ToList().FindIndex(x=>x.UserId==actor.UserId);AssigneeLabel=index>=0?AssigneeOptions[index]:"";});
 public Task ReplyAsync(Guid id)=>RunAsync(async()=>{await service.ReplyTicketAsync(id,new(Operation,Text));LastReplyId=Operation;Operation=Guid.NewGuid();Text="";Ticket=await service.TicketAsync(id);Message="Reply recorded.";});
 public Task AdvanceAsync(Guid id)=>RunAsync(async()=>{if(Progress is null)return;var index=AssigneeOptions.ToList().IndexOf(AssigneeLabel);Progress.AssignedTechnicalMemberId=index<0?null:Ticket!.Assignees[index].UserId;await Progress.AdvanceAsync();if(Progress.HasError)throw new ArgumentException(Progress.Message);Ticket=await service.TicketAsync(id);Message=Progress.Message;});
 public Task AttachAsync(Guid ticketId,Guid? replyId,BrowserUpload upload)=>RunAsync(async()=>{await service.AttachAsync(ticketId,replyId,upload.OperationId,upload.Filename,upload.Content,upload.CancellationToken);Ticket=await service.TicketAsync(ticketId,upload.CancellationToken);Message="Attachment scanned and saved.";});
}
