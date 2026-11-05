namespace TicketingCRM.Domain.Persistence.Models;
public sealed class ProductRelease:TenantRecord
{
 public Guid ProductId{get;set;}
 public Guid UploadedByUserId{get;set;}
 public string ReleaseVersion{get;set;}="";
 public string Platform{get;set;}="";
 public string Filename{get;set;}="";
 public string Notes{get;set;}="";
 public string Sha256{get;set;}="";
 public long Size{get;set;}
 public string Status{get;set;}="Quarantined";
 public string ManifestJson{get;set;}="";
 public string ManifestSignature{get;set;}="";
 public DateTimeOffset UploadedAtUtc{get;set;}
 public DateTimeOffset? PublishedAtUtc{get;set;}
 public Guid? PublishedByUserId{get;set;}
 public Guid? RevokedByUserId{get;set;}
 public DateTimeOffset? RevokedAtUtc{get;set;}
}
