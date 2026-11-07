using Microsoft.EntityFrameworkCore;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.DataConnection.DbContexts;

public partial class AppDbContext
{
    public DbSet<CollectionForm> CollectionForms => Set<CollectionForm>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
    private void ConfigureForms(ModelBuilder model)
    {
        ConfigureTenant(model.Entity<CollectionForm>());
        ConfigureTenant(model.Entity<FormSubmission>());
        var form = model.Entity<CollectionForm>();
        form.Property(x => x.Title).HasMaxLength(160);
        form.Property(x => x.Description).HasMaxLength(2000);
        form.Property(x => x.FieldsJson).HasMaxLength(24000);
        form.Property(x => x.PublicKey).HasMaxLength(64);
        form.Property(x => x.Status).HasMaxLength(16);
        form.HasIndex(x => x.PublicKey).IsUnique();
        form.HasIndex(x => new { x.TenantId, x.CreatedAtUtc, x.Id });
        form.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
            .HasPrincipalKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var submission = model.Entity<FormSubmission>();
        submission.Property(x => x.AnswersJson).HasMaxLength(64000);
        submission.HasIndex(x => new { x.TenantId, x.FormId, x.OperationId }).IsUnique();
        submission.HasIndex(x => new { x.TenantId, x.FormId, x.SubmittedAtUtc, x.Id });
        submission.HasOne<CollectionForm>().WithMany().HasForeignKey(x => new { x.TenantId, x.FormId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
