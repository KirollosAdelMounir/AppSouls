using Microsoft.EntityFrameworkCore;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.DataConnection.DbContexts;
public partial class AppDbContext
{
 private void ConfigureConversations(ModelBuilder model)
 {
  ConfigureTenant(model.Entity<SupportChat>());ConfigureTenant(model.Entity<SupportReply>());ConfigureTenant(model.Entity<SupportChatMessage>());ConfigureTenant(model.Entity<SupportAttachment>());
  model.Entity<SupportTicket>().HasAlternateKey(x=>new{x.TenantId,x.CustomerId,x.Id});
  var chat=model.Entity<SupportChat>();chat.Property(x=>x.Subject).HasMaxLength(200);chat.Property(x=>x.Topic).HasMaxLength(20);chat.Property(x=>x.Status).HasMaxLength(20);chat.HasAlternateKey(x=>new{x.TenantId,x.CustomerId,x.Id});chat.HasIndex(x=>new{x.TenantId,x.Topic,x.Status,x.CreatedAtUtc});chat.HasIndex(x=>new{x.TenantId,x.CustomerId,x.CreatedAtUtc});
  chat.HasOne<Customer>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  chat.HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProductId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  var reply=model.Entity<SupportReply>();reply.Property(x=>x.Body).HasMaxLength(8000);reply.Property(x=>x.AuthorName).HasMaxLength(200);reply.Property(x=>x.AuthorRole).HasMaxLength(40);reply.HasAlternateKey(x=>new{x.TenantId,x.CustomerId,x.TicketId,x.Id});reply.HasIndex(x=>new{x.TenantId,x.TicketId,x.CreatedAtUtc});reply.HasIndex(x=>new{x.TenantId,x.AuthorUserId,x.CreatedAtUtc});
  var message=model.Entity<SupportChatMessage>();message.Property(x=>x.Body).HasMaxLength(8000);message.Property(x=>x.AuthorName).HasMaxLength(200);message.Property(x=>x.AuthorRole).HasMaxLength(40);message.HasIndex(x=>new{x.TenantId,x.ChatId,x.CreatedAtUtc});message.HasIndex(x=>new{x.TenantId,x.AuthorUserId,x.CreatedAtUtc});
  message.HasOne<SupportChat>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.ChatId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  reply.HasOne<SupportTicket>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.TicketId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  var file=model.Entity<SupportAttachment>();file.Property(x=>x.Filename).HasMaxLength(160);file.Property(x=>x.Sha256).HasMaxLength(64);file.HasIndex(x=>new{x.TenantId,x.TicketId});file.HasIndex(x=>new{x.TenantId,x.CustomerId});
  file.HasOne<SupportTicket>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.TicketId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  // Reply ownership and parent matching are also checked before file consumption.
  file.HasOne<SupportReply>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.TicketId,x.ReplyId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.TicketId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  ConfigureTenant(model.Entity<SupportChatAttachment>());
  message.HasAlternateKey(x=>new{x.TenantId,x.CustomerId,x.ChatId,x.Id});
  var chatFile=model.Entity<SupportChatAttachment>();chatFile.Property(x=>x.Filename).HasMaxLength(160);chatFile.Property(x=>x.Sha256).HasMaxLength(64);
  chatFile.HasIndex(x=>new{x.TenantId,x.ChatId});chatFile.HasIndex(x=>new{x.TenantId,x.CustomerId});
  chatFile.HasOne<SupportChatMessage>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.ChatId,x.MessageId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.ChatId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  ConfigureTenant(model.Entity<SupportChatReadState>());
  var read=model.Entity<SupportChatReadState>();read.HasIndex(x=>new{x.TenantId,x.UserId,x.ChatId}).IsUnique();
  read.HasOne<SupportChat>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CustomerId,x.ChatId}).HasPrincipalKey(x=>new{x.TenantId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  model.Entity<SupportTicket>().Property(x=>x.Urgency).HasMaxLength(20).HasDefaultValue("Medium");
  model.Entity<SupportTicket>().HasIndex(x=>new{x.TenantId,x.CustomerId,x.CreatedAtUtc});
 }
}
