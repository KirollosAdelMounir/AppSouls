using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.DataConnection;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Commerce.DTOs;
using TicketingCRM.Domain.Delivery.DTOs;
using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Support.DTOs;
using TicketingCRM.Domain.Support.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Commerce;
using TicketingCRM.Infrastructure.Forms;
using TicketingCRM.Infrastructure.Support;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;

public sealed class WorkspaceExpansionTests
{
 private sealed record Scope(Guid UserId,Guid TenantId,Guid? CustomerId,string Role,string? SecurityStamp):ITenantContext,ICurrentActor;
 private sealed class Factory(CustomerDeliveryFixture.Actor actor):IDbContextFactory<AppDbContext>
 {
  public AppDbContext CreateDbContext(){var options=new DbContextOptionsBuilder<AppDbContext>();TicketingCRM.DataConnection.DependencyInjection.ConfigureProvider(options,DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!);return new PostgreSqlAppDbContext(options.Options,new Scope(actor.UserId,actor.TenantId,actor.CustomerId,actor.Role,actor.SecurityStamp));}
 }
 private static WorkspaceSessionFactory Sessions(CustomerDeliveryFixture.Actor actor)=>new(new Factory(actor));
 private static CommerceService Commerce(CustomerDeliveryFixture.Actor actor)=>new(Sessions(actor),actor);
 private static SupportConversationService Conversations(CustomerDeliveryFixture f,CustomerDeliveryFixture.Actor actor,ArtifactScanVerdict verdict=ArtifactScanVerdict.Clean)=>new(Sessions(actor),actor,f.Files,new CustomerDeliveryFixture.Scanner(verdict));
 private static CommercialLine[] Lines(Guid product)=>[new(product,"Annual support",2,99.95m,10,14)];
 private static InvoiceInput Invoice(Guid product)=>new("INV-2026-001","Tetrarchia","Supplier tax reference","Supplier address","Buyer company","Buyer tax reference","Buyer address","EGP",DateTimeOffset.UtcNow,null,"Due in 30 days","Sales-entered PDF invoice",Lines(product));
 private static async Task<CustomerDeliveryFixture.Actor> AddStaff(CustomerDeliveryFixture f,string role)
 {
  var id=Guid.NewGuid();await using var db=CustomerDeliveryFixture.Open(f.Tenant);db.Users.Add(new(){Id=id,UserName=id.ToString(),DisplayName="Private employee name",SecurityStamp=f.Stamp});db.TenantMemberships.Add(new(){TenantId=f.Tenant,UserId=id,Role=role});await db.SaveChangesAsync();return new(id,f.Tenant,null,role,f.Stamp);
 }
 [Fact]
 public void PricingRoundsEachLineAndRejectsInvalidQuantitiesAndPrecision()
 {
  var result=CommercialPricing.Calculate(Lines(Guid.NewGuid()));var line=Assert.Single(result.Lines);Assert.Equal(199.90m,result.Totals.Subtotal);Assert.Equal(19.99m,line.Discount);Assert.Equal(25.19m,line.Tax);Assert.Equal(205.10m,line.Total);Assert.Equal(line.Total,result.Totals.Total);
  var valid=Lines(Guid.NewGuid())[0];foreach(var bad in new[]{valid with{Quantity=0},valid with{Quantity=1.0001m},valid with{UnitPrice=-1},valid with{UnitPrice=1.001m},valid with{TaxPercent=100.01m},valid with{DiscountPercent=-1}})Assert.Throws<ArgumentException>(()=>CommercialPricing.Calculate([bad]));
 }
 [PostgreSqlFact]
 public async Task InvoiceSnapshotsAreOwnedImmutableAndIdempotentAndPdfContainsSavedValues()
 {
  using var f=await CustomerDeliveryFixture.Create();var service=Commerce(f.OwnerActor);var order=new CreateOrderRequest(Guid.NewGuid(),f.Customer,f.Contract,"ORDER-1","EGP",Lines(f.Product),"Order notes");var orderId=await service.CreateOrderAsync(order);Assert.Equal(orderId,await service.CreateOrderAsync(order));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CreateOrderAsync(order with{Notes="Changed"}));
  var request=new CreateInvoiceRequest(Guid.NewGuid(),f.Customer,orderId,f.Contract,Invoice(f.Product));var id=await service.CreateInvoiceAsync(request);Assert.Equal(id,await service.CreateInvoiceAsync(request));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CreateInvoiceAsync(request with{Invoice=request.Invoice with{Notes="Changed"}}));
  var own=await service.InvoiceAsync(id);Assert.Equal(205.10m,own.Totals.Total);Assert.Equal("Supplier address",own.Details.SupplierAddress);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(f.OtherActor).InvoiceAsync(id));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(f.CustomerActor).InvoiceAsync(id));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(f.CustomerActor).DownloadInvoiceAsync(id));
  await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateInvoiceAsync(request with{OperationId=Guid.NewGuid(),CustomerId=f.OtherCustomer}));
  var pdf=await service.DownloadInvoiceAsync(id);await using(pdf.Content){var header=new byte[5];Assert.Equal(5,await pdf.Content.ReadAsync(header));Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(header));}
  await using var db=CustomerDeliveryFixture.Open(f.Tenant);var saved=await db.Set<InvoiceDocument>().SingleAsync();saved.Number="Changed";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
 }
 [PostgreSqlFact]
 public async Task SalesCanRecordOrdersButOnlyManagersIssueInvoicesAndLeadershipApprovesPrices()
 {
  using var f=await CustomerDeliveryFixture.Create();var sales=await AddStaff(f,"Sales");var manager=await AddStaff(f,"Sales Manager");var developer=await AddStaff(f,"Technical Developer");
  var order=await Commerce(sales).CreateOrderAsync(new(Guid.NewGuid(),f.Customer,f.Contract,"ORDER-SALES","USD",Lines(f.Product),""));var request=new CreateInvoiceRequest(Guid.NewGuid(),f.Customer,order,f.Contract,Invoice(f.Product));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(sales).CreateInvoiceAsync(request));Assert.NotEqual(Guid.Empty,await Commerce(manager).CreateInvoiceAsync(request));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(developer).OrdersAsync());await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(f.CustomerActor).CreateOrderAsync(new(Guid.NewGuid(),f.Customer,null,"forged","USD",Lines(f.Product),"")));
  await Commerce(sales).UpdatePricingAsync(new(f.Product,null,"USD",100,"Per server, annual support",false));var draft=Assert.Single(await Commerce(sales).CatalogAsync());Assert.False(draft.Approved);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(manager).UpdatePricingAsync(new(f.Product,draft.Version,"USD",100,"Approved?",true)));
  await Commerce(f.OwnerActor).UpdatePricingAsync(new(f.Product,draft.Version,"USD",100,draft.Description,true));var approved=Assert.Single(await Commerce(sales).CatalogAsync());Assert.True(approved.Approved);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Commerce(sales).UpdatePricingAsync(new(f.Product,draft.Version,"USD",50,"Stale",false)));
  await Commerce(sales).UpdatePricingAsync(new(f.Product,approved.Version,"USD",120,"Changed terms",false));Assert.False(Assert.Single(await Commerce(sales).CatalogAsync()).Approved);
 }
 [PostgreSqlFact]
 public async Task ChatRequiresPurchasedTechnicalSoftwareRoutesTeamsAndHidesPrivateStaffNames()
 {
  using var f=await CustomerDeliveryFixture.Create();var sales=await AddStaff(f,"Sales");var support=await AddStaff(f,"Technical Support");var customer=Conversations(f,f.CustomerActor);var request=new StartChatRequest(Guid.NewGuid(),"Technical",f.Product,"Activation help","**License** question");
  var id=await customer.StartChatAsync(request);Assert.Equal(id,await customer.StartChatAsync(request));await Assert.ThrowsAsync<InvalidOperationException>(()=>customer.StartChatAsync(request with{Message="Changed"}));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,f.OtherActor).StartChatAsync(request with{OperationId=Guid.NewGuid()}));await Assert.ThrowsAsync<ArgumentException>(()=>customer.StartChatAsync(request with{OperationId=Guid.NewGuid(),ProductId=null}));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,support).StartChatAsync(request));
  Assert.Empty(await Conversations(f,sales).ChatsAsync());Assert.Single(await Conversations(f,support).ChatsAsync());await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,sales).ChatAsync(id));await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.OtherActor).ChatAsync(id));
  var message=new SendConversationMessage(Guid.NewGuid(),"Please check your installed version.");await Conversations(f,support).SendChatAsync(id,message);await Conversations(f,support).SendChatAsync(id,message);var thread=await customer.ChatAsync(id);Assert.NotNull(thread);Assert.Equal(2,thread.Messages.Count);Assert.Equal("Technical team",thread.Messages.Last().Author);Assert.DoesNotContain("Private employee",thread.Messages.Last().Author);
  Assert.Null(await customer.ChatAsync(id,thread.Chat.Version));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,f.CustomerActor with{SecurityStamp="revoked"}).ChatAsync(id,thread.Chat.Version));
  await customer.CloseChatAsync(id);Assert.False((await customer.ChatAsync(id))!.CanReply);await Assert.ThrowsAsync<InvalidOperationException>(()=>Conversations(f,support).SendChatAsync(id,message with{OperationId=Guid.NewGuid()}));
  var salesId=await customer.StartChatAsync(new(Guid.NewGuid(),"Sales",null,"Pricing","Need a quote"));Assert.Equal(salesId,Assert.Single(await Conversations(f,sales).ChatsAsync()).Id);
 }
 [PostgreSqlFact]
 public async Task TicketRepliesAndAttachmentsEnforceOwnershipScanningAndStableRetries()
 {
  using var f=await CustomerDeliveryFixture.Create();var report=new SupportReportService(Sessions(f.CustomerActor),f.CustomerActor,f.Config);var submitted=await report.SubmitAsync(new(Guid.NewGuid(),f.Product,SupportReportKind.CriticalError,"Crash","A serious problem","Launch","Works","Crashes","1.0.0","Linux","Extreme"));
  var service=Conversations(f,f.CustomerActor);var ticket=await service.TicketAsync(submitted.TicketId);Assert.Equal("Extreme",ticket.Urgency);Assert.Equal("CriticalError",ticket.Kind);Assert.Empty(ticket.Assignees);
  var reply=new SendConversationMessage(Guid.NewGuid(),"More **details**");await service.ReplyTicketAsync(ticket.Id,reply);await service.ReplyTicketAsync(ticket.Id,reply);Assert.Single((await service.TicketAsync(ticket.Id)).Messages);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,f.TechnicalActor).AttachAsync(ticket.Id,reply.OperationId,Guid.NewGuid(),"staff.txt",new MemoryStream([1])));
  var upload=Guid.NewGuid();await service.AttachAsync(ticket.Id,reply.OperationId,upload,"details.log",new MemoryStream([1,2,3]));await service.AttachAsync(ticket.Id,reply.OperationId,upload,"details.log",new MemoryStream([1,2,3]));Assert.Single((await service.TicketAsync(ticket.Id)).Attachments);await Assert.ThrowsAsync<InvalidOperationException>(()=>service.AttachAsync(ticket.Id,reply.OperationId,upload,"details.log",new MemoryStream([4])));
  await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.CustomerActor,ArtifactScanVerdict.Rejected).AttachAsync(ticket.Id,null,Guid.NewGuid(),"bad.log",new MemoryStream([1])));await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.CustomerActor,ArtifactScanVerdict.Unavailable).AttachAsync(ticket.Id,null,Guid.NewGuid(),"unknown.log",new MemoryStream([1])));
  Assert.Single((await service.TicketAsync(ticket.Id)).Attachments);await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.OtherActor).DownloadAttachmentAsync(upload));var file=await Conversations(f,f.TechnicalActor).DownloadAttachmentAsync(upload);await using(file.Content){Assert.Equal(3,file.Content.Length);}
  await using var db=CustomerDeliveryFixture.Open(f.Tenant);Assert.Single(await db.OutboxMessages.ToListAsync());Assert.Single(await db.SupportTicketHistories.ToListAsync());Assert.Single(Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories));
 }
 [PostgreSqlFact]
 public async Task DraftFormEditingTypedAnswersAndExcelExportPreserveThePublishedQuestionSet()
 {
  using var f=await CustomerDeliveryFixture.Create();var service=new FormService(Sessions(f.OwnerActor),f.OwnerActor);var fields=new FormField[]{new("number","Amount","Number",true,[]),new("rating","Rating","Rating",true,[]),new("answer","Answer","Yes / No",true,[]),new("website","Website","Website",false,[])};var id=await service.CreateAsync(new("Survey","",fields));var draft=await service.GetAsync(id);await service.UpdateAsync(id,new(draft.Version,"Updated survey","Description",fields));await Assert.ThrowsAsync<InvalidOperationException>(()=>service.UpdateAsync(id,new(draft.Version,"Stale","",fields)));draft=await service.GetAsync(id);await service.ChangeStateAsync(id,new(draft.Version,true));
  var published=Assert.Single((await service.ListAsync()).Items);await Assert.ThrowsAsync<InvalidOperationException>(()=>service.UpdateAsync(id,new(published.Version,"Changed","",fields)));var pub=new PublicFormService(new PublicFormSessionFactory(DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!));
  var answers=new Dictionary<string,string>{{"number","12.5"},{"rating","5"},{"answer","Yes"},{"website","https://example.test"}};foreach(var pair in new[]{("number","NaN"),("rating","6"),("answer","Maybe"),("website","javascript:alert(1)")})await Assert.ThrowsAsync<ArgumentException>(()=>pub.SubmitAsync(published.PublicKey,new(Guid.NewGuid(),new(answers){[pair.Item1]=pair.Item2})));
  await pub.SubmitAsync(published.PublicKey,new(Guid.NewGuid(),answers));var export=await service.ExportAsync(id);await using(export.Content){using var archive=new ZipArchive(export.Content,ZipArchiveMode.Read);using var reader=new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());var xml=await reader.ReadToEndAsync();Assert.Contains("12.5",xml);Assert.Contains("Rating",xml);Assert.Contains("<v>5</v>",xml);}
 }
 [PostgreSqlFact]
 public async Task StoreLinksRejectHostSpoofingAndPublishedPagingIgnoresNewerQuarantine()
 {
  using var f=await CustomerDeliveryFixture.Create();foreach(var url in new[]{"https://play.google.com.evil.test/store/apps/details?id=test","http://play.google.com/store/apps/details?id=test","https://user:password@play.google.com/store/apps/details?id=test","https://example.test/app"})await Assert.ThrowsAsync<ArgumentException>(()=>f.Releases().CreateStoreReleaseAsync(new(Guid.NewGuid(),f.Product,"1.0.0","Android",url,"")));
  await f.Releases().CreateStoreReleaseAsync(new(Guid.NewGuid(),f.Product,"1.0.0","Android","https://play.google.com/store/apps/details?id=com.example.app","Store version"));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){for(var i=0;i<101;i++)db.ProductReleases.Add(new(){TenantId=f.Tenant,ProductId=f.Product,ReleaseVersion=$"2.0.{i}",Platform="Linux",Filename="installer.sh",UploadedByUserId=f.Owner,UploadedAtUtc=DateTimeOffset.UtcNow});await db.SaveChangesAsync();}
  var published=Assert.Single((await f.Releases().ListAsync("published")).Releases);Assert.NotNull(published.StoreUrl);Assert.Equal(100,(await f.Releases().ListAsync("unpublished")).Releases.Count);Assert.Single((await f.Releases().ListAsync("unpublished",2)).Releases);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Releases(f.CustomerActor).ListAsync("unpublished"));
 }
 [PostgreSqlFact]
 public async Task NativeRowPoliciesStopFilterBypassForeignWritesAndPooledScopeLeaks()
 {
  using var f=await CustomerDeliveryFixture.Create();using var other=await CustomerDeliveryFixture.Create();
  foreach(var actor in new[]{f.CustomerActor,other.CustomerActor,f.OtherActor,f.OwnerActor})
  {
   await using var db=new Factory(actor).CreateDbContext();await db.Database.OpenConnectionAsync();var connection=db.Database.GetDbConnection();await using var command=connection.CreateCommand();command.CommandText="SELECT current_user";Assert.Equal("appsouls_runtime",await command.ExecuteScalarAsync());var rows=await db.Customers.IgnoreQueryFilters().ToListAsync();Assert.All(rows,x=>Assert.Equal(actor.TenantId,x.TenantId));if(actor.CustomerId is {} customer)Assert.Equal(customer,Assert.Single(rows).Id);else Assert.Equal(2,rows.Count);
   command.CommandText=$"INSERT INTO \"Customers\" (\"Id\",\"TenantId\",\"Version\",\"Name\",\"Email\",\"CreatedAtUtc\") VALUES ('{Guid.NewGuid()}','{Guid.NewGuid()}','{Guid.NewGuid()}','Blocked','blocked@example.test',CURRENT_TIMESTAMP)";await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>command.ExecuteNonQueryAsync());
  }
  await using var admin=CustomerDeliveryFixture.Open(Guid.Empty);await admin.Database.OpenConnectionAsync();await using var inspect=admin.Database.GetDbConnection().CreateCommand();inspect.CommandText="SELECT count(*) FROM pg_roles WHERE rolname='appsouls_runtime' AND NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin";Assert.Equal(1L,await inspect.ExecuteScalarAsync());
 }
 [PostgreSqlFact]
 public async Task PublicFormDatabaseScopeCannotReadPrivateTablesOrAnotherFormAndMissingTenantIsDenied()
 {
  using var f=await CustomerDeliveryFixture.Create();var forms=new FormService(Sessions(f.OwnerActor),f.OwnerActor);var fields=new FormField[]{new("name","Name","Text",true,[])};var id=await forms.CreateAsync(new("Public survey","",fields));var other=await forms.CreateAsync(new("Private survey","",fields));var summary=(await forms.ListAsync()).Items.Single(x=>x.Id==id);await forms.ChangeStateAsync(id,new(summary.Version,true));
  await using var session=await new PublicFormSessionFactory(DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!).OpenAsync(summary.PublicKey);Assert.NotNull(session);Assert.Equal(id,Assert.Single(await session.Repository<CollectionForm>().GetAllQueryable().IgnoreQueryFilters().ToListAsync()).Id);Assert.Empty(await session.Repository<Customer>().GetAllQueryable().IgnoreQueryFilters().ToListAsync());Assert.Empty(await session.Repository<Product>().GetAllQueryable().IgnoreQueryFilters().ToListAsync());
  session.Repository<FormSubmission>().Add(new(){TenantId=f.Tenant,FormId=other,OperationId=Guid.NewGuid(),SubmittedAtUtc=DateTimeOffset.UtcNow});await Assert.ThrowsAsync<DbUpdateException>(()=>session.CommitAsync());
  await using var unscoped=new Factory(f.OwnerActor with{TenantId=Guid.Empty}).CreateDbContext();Assert.Empty(await unscoped.Customers.IgnoreQueryFilters().ToListAsync());
 }

 [PostgreSqlFact]
 public async Task NarrowIdentityDirectoryWorksWhileGenericUnscopedPersistenceStaysDenied()
 {
  using var f=await CustomerDeliveryFixture.Create();var services=new ServiceCollection();services.AddDataConnection(DatabaseProvider.PostgreSql,Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!);using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var directory=scope.ServiceProvider.GetRequiredService<ISignInDirectory>();var account=await directory.FindAsync(f.Owner,f.Stamp);Assert.NotNull(account);Assert.Equal(f.Tenant,Assert.Single(account.Workspaces).TenantId);Assert.Null(await directory.FindAsync(f.Owner,"revoked"));
  await using var unscoped=scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();Assert.Empty(await unscoped.Customers.IgnoreQueryFilters().ToListAsync());
 }

 [PostgreSqlFact]
 public async Task ChatAttachmentsRequireOwnMessageCleanScanStableRetryAndAuthorizedDownload()
 {
  using var f=await CustomerDeliveryFixture.Create();using var other=await CustomerDeliveryFixture.Create();
  var customer=Conversations(f,f.CustomerActor);var id=await customer.StartChatAsync(new(Guid.NewGuid(),"Technical",f.Product,"Files","My logs"));var upload=Guid.NewGuid();
  await customer.AttachChatAsync(id,id,upload,"client.log",new MemoryStream([1,2,3]));
  await customer.AttachChatAsync(id,id,upload,"client.log",new MemoryStream([1,2,3]));
  var file=Assert.Single((await customer.ChatAsync(id))!.Attachments);Assert.Equal(id,file.ReplyId);Assert.Equal(3,file.Size);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>customer.AttachChatAsync(id,id,upload,"client.log",new MemoryStream([4])));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,f.TechnicalActor).AttachChatAsync(id,id,Guid.NewGuid(),"forged.log",new MemoryStream([1])));
  var otherId=await customer.StartChatAsync(new(Guid.NewGuid(),"Technical",f.Product,"Other","Another question"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>customer.AttachChatAsync(otherId,id,Guid.NewGuid(),"wrong.log",new MemoryStream([1])));
  foreach(var verdict in new[]{ArtifactScanVerdict.Rejected,ArtifactScanVerdict.Unavailable})
   await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.CustomerActor,verdict).AttachChatAsync(id,id,Guid.NewGuid(),"bad.log",new MemoryStream([1])));
  await Assert.ThrowsAsync<ArgumentException>(()=>customer.AttachChatAsync(id,id,Guid.NewGuid(),"bad.exe",new MemoryStream([1])));
  await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.OtherActor).DownloadChatAttachmentAsync(upload));
  await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(other,other.OwnerActor).DownloadChatAttachmentAsync(upload));
  var download=await Conversations(f,f.TechnicalActor).DownloadChatAttachmentAsync(upload);await using(download.Content){Assert.Equal(3,download.Content.Length);}
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){Assert.Single(await db.Set<SupportChatAttachment>().ToListAsync());var metadata=await db.Set<SupportChatAttachment>().SingleAsync();metadata.Sha256="tampered";await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());}
  Assert.Single(Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories));
  var path=Directory.GetFiles(f.Root,"*.bin",SearchOption.AllDirectories).Single();await File.WriteAllBytesAsync(path,[9]);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>customer.DownloadChatAttachmentAsync(upload));
  await customer.CloseChatAsync(id);await Assert.ThrowsAsync<InvalidOperationException>(()=>customer.AttachChatAsync(id,id,Guid.NewGuid(),"closed.log",new MemoryStream([1])));
 }
 [PostgreSqlFact]
 public async Task TypingIsScopedExpiresAndClearsAfterSendAndClose()
 {
  using var f=await CustomerDeliveryFixture.Create();var customer=Conversations(f,f.CustomerActor);var staff=Conversations(f,f.TechnicalActor);
  var id=await customer.StartChatAsync(new(Guid.NewGuid(),"Technical",f.Product,"Typing","Hello"));
  await customer.SetChatTypingAsync(id,true);Assert.True((await staff.ChatAsync(id))!.OtherPartyTyping);Assert.False((await customer.ChatAsync(id))!.OtherPartyTyping);
  var sales=await AddStaff(f,"Sales");await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Conversations(f,sales).SetChatTypingAsync(id,true));await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.OtherActor).SetChatTypingAsync(id,true));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){var chat=await db.Set<SupportChat>().SingleAsync(x=>x.Id==id);chat.CustomerTypingUntilUtc=DateTimeOffset.UtcNow.AddSeconds(-1);await db.SaveChangesAsync();}
  Assert.False((await staff.ChatAsync(id))!.OtherPartyTyping);
  await staff.SetChatTypingAsync(id,true);Assert.True((await customer.ChatAsync(id))!.OtherPartyTyping);
  await staff.SendChatAsync(id,new(Guid.NewGuid(),"Reply"));Assert.False((await customer.ChatAsync(id))!.OtherPartyTyping);
  await customer.SetChatTypingAsync(id,true);await customer.SetChatTypingAsync(id,false);Assert.False((await staff.ChatAsync(id))!.OtherPartyTyping);
  await customer.SetChatTypingAsync(id,true);await customer.CloseChatAsync(id);Assert.False((await staff.ChatAsync(id))!.OtherPartyTyping);
 }
 [PostgreSqlFact]
 public async Task ChatAlertsNotifyCustomersAndStaffButExcludeOwnMessagesOtherTeamsAndOtherTenants()
 {
  using var f=await CustomerDeliveryFixture.Create();using var other=await CustomerDeliveryFixture.Create();
  var customer=Conversations(f,f.CustomerActor);var sales=await AddStaff(f,"Sales");
  var id=await customer.StartChatAsync(new(Guid.NewGuid(),"Technical",f.Product,"Alert","Hello"));
  var alert=Assert.Single((await Conversations(f,f.TechnicalActor).ChatNotificationsAsync()).Items);Assert.Equal(id,alert.ChatId);Assert.Equal(id,alert.Id);
  Assert.Empty((await Conversations(f,sales).ChatNotificationsAsync()).Items);Assert.Empty((await Conversations(other,other.OwnerActor).ChatNotificationsAsync()).Items);
  Assert.Empty((await customer.ChatNotificationsAsync()).Items);
  await Conversations(f,f.TechnicalActor).SendChatAsync(id,new(Guid.NewGuid(),"Staff reply"));Assert.Single((await Conversations(f,f.TechnicalActor).ChatNotificationsAsync()).Items);Assert.Equal(2,(await Conversations(f,f.OwnerActor).ChatNotificationsAsync()).UnreadCount);
  Assert.Single((await customer.ChatNotificationsAsync()).Items);
  await customer.MarkChatReadAsync(id,(await customer.ChatAsync(id))!.Messages.Last().Id);Assert.Equal(0,(await customer.ChatNotificationsAsync()).UnreadCount);
  Assert.Single((await Conversations(f,f.TechnicalActor).ChatNotificationsAsync()).Items);
  await Conversations(f,f.TechnicalActor).MarkChatReadAsync(id,id);
  Assert.Equal(0,(await Conversations(f,f.TechnicalActor).ChatNotificationsAsync()).UnreadCount);
  Assert.Equal(2,(await Conversations(f,f.OwnerActor).ChatNotificationsAsync()).UnreadCount);
  await customer.SendChatAsync(id,new(Guid.NewGuid(),"Another incoming message"));
  var unread=await Conversations(f,f.TechnicalActor).ChatNotificationsAsync();Assert.Equal(1,unread.UnreadCount);Assert.Equal(id,Assert.Single(unread.Conversations).ChatId);Assert.Equal(1,Assert.Single(unread.Conversations).Count);
  await customer.MarkChatReadAsync(id,id);Assert.Equal(0,(await customer.ChatNotificationsAsync()).UnreadCount);
  await Assert.ThrowsAsync<ArgumentException>(()=>Conversations(f,f.OtherActor).MarkChatReadAsync(id,id));
  await Assert.ThrowsAsync<ArgumentException>(()=>customer.MarkChatReadAsync(id,Guid.NewGuid()));
 }
 [PostgreSqlFact]
 public async Task CustomerWorkspaceExcludesCommercialRecordsAndDisabledMembershipRevokesChatAccess()
 {
  using var f=await CustomerDeliveryFixture.Create();var customer=Conversations(f,f.CustomerActor);
  var workspace=new TicketingCRM.Infrastructure.Workspace.WorkspaceService(Sessions(f.CustomerActor),f.CustomerActor);
  var snapshot=await workspace.GetAsync();Assert.Empty(snapshot.Customers);Assert.Empty(snapshot.Contracts);Assert.Single(snapshot.Products);Assert.Single(snapshot.Entitlements);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>workspace.ContractAsync(f.Contract));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Lifecycle(f.CustomerActor).GetAsync(f.Customer));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(f.CustomerActor).OrdersAsync());await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Commerce(f.CustomerActor).InvoicesAsync());
  var id=await customer.StartChatAsync(new(Guid.NewGuid(),"Technical",f.Product,"Access","Hello"));
  await using(var db=CustomerDeliveryFixture.Open(f.Tenant)){var member=await db.TenantMemberships.SingleAsync(x=>x.UserId==f.CustomerUser);member.IsActive=false;await db.SaveChangesAsync();}
  var failure=await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>customer.ChatAsync(id));Assert.Contains("contact the company",failure.Message);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>customer.SetChatTypingAsync(id,true));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>customer.SendChatAsync(id,new(Guid.NewGuid(),"Disabled")));
 }

}
