using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.Licensing;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Interfaces;
using TicketingCRM.Domain.Licensing.Models;
using TicketingCRM.Domain.Persistence.Models;

namespace TicketingCRM.Infrastructure.Licensing;
public sealed partial class LicensingService(IWorkspaceSessionFactory sessions, ICurrentActor actor, ServerConfiguration configuration) : ILicensingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false, MaxDepth = 16 };
    private Task<TenantMembership> Authorize(IWorkspaceSession s,bool staff,CancellationToken ct)=>TicketingCRM.Infrastructure.Security.WorkspaceAccess.RequireAsync(s,actor,administrator:staff,ct:ct);
    public async Task<LicensingSnapshot> GetAsync(CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);var m=await Authorize(s,false,cancellationToken);
        var installs=s.Query<Installation>();var licenses=s.Query<LicenseRevision>();var schemas=s.Query<LicenseSchemaRevision>().Where(x=>x.IsPublished);
        if(m.CustomerId is {} customer){installs=installs.Where(x=>x.CustomerId==customer);licenses=licenses.Where(x=>x.CustomerId==customer);schemas=schemas.Where(x=>s.Query<Entitlement>().Any(e=>e.ProductId==x.ProductId&&e.CustomerId==customer));}
        var schemaRows=await schemas.OrderByDescending(x=>x.Revision).Take(100).ToListAsync(cancellationToken);
        return new(schemaRows.Select(x=>new SchemaSummary(x.Id,x.ProductId,x.Name,x.Revision,x.Extension,JsonSerializer.Deserialize<LicenseModelDefinitionDto>(x.SchemaJson,JsonOptions)?.Fields)).ToArray(),await installs.OrderBy(x=>x.Name).Take(100).Select(x=>new InstallationSummary(x.Id,x.CustomerId,x.ProductId,x.EntitlementId,x.Name,x.IsRevoked)).ToListAsync(cancellationToken),await licenses.Where(x=>x.Status=="Ready").OrderByDescending(x=>x.IssuedAtUtc).Take(100).Select(x=>new IssuedLicenseSummary(x.Id,x.InstallationId,x.Revision,x.Extension,x.IssuedAtUtc,x.ExpiresAtUtc)).ToListAsync(cancellationToken),m.CustomerId is null && m.Role is "Owner" or "Admin");
    }
    public async Task<Guid> PublishSchemaAsync(PublishLicenseSchemaRequest request,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);await Authorize(s,true,cancellationToken);
        if(!await s.Query<Product>().AnyAsync(x=>x.Id==request.ProductId&&x.IsActive,cancellationToken))throw new ArgumentException("Choose an active product.");
        var name=Text(request.Name,160);var extension=Text(request.Extension,24).ToLowerInvariant();
        if(!Regex.IsMatch(extension,@"\A\.[a-z][a-z0-9]{1,20}\z")||new[]{".exe",".dll",".js",".sh",".bat",".cmd",".ps1",".com",".msi",".html",".svg",".py",".jar"}.Contains(extension))throw new ArgumentException("Choose a safe license extension.");
        ValidateMappings(request.Fields);
        var model=new LicenseModelDefinitionDto(actor.TenantId,request.ProductId,name,request.Fields);new LicenseModelCodeGenerator().Export(model);
        var revision=(await s.Query<LicenseSchemaRevision>().Where(x=>x.ProductId==request.ProductId).MaxAsync(x=>(int?)x.Revision,cancellationToken)??0)+1;
        var schema=new LicenseSchemaRevision{TenantId=actor.TenantId,ProductId=request.ProductId,Name=name,Extension=extension,Revision=revision,IsPublished=true,EncryptionProfile=LicenseArtifactProtector.Profile,SchemaJson=JsonSerializer.Serialize(model,JsonOptions)};
        s.Add(schema);await Commit(s,cancellationToken);return schema.Id;
    }
    public async Task<InstallationEnrollment> EnrollAsync(EnrollInstallationRequest request,CancellationToken cancellationToken=default)
    {
        if(request.OperationId==Guid.Empty)throw new ArgumentException("An enrollment operation ID is required.");
        var name=Text(request.Name,160);var pem=Text(request.PublicKeyPem,8192);
        using(var key=RSA.Create()){key.ImportFromPem(pem);if(key.KeySize is not (3072 or 4096 or 8192))throw new ArgumentException("Use a 3072, 4096 or 8192-bit RSA public key."); if(pem.Contains("PRIVATE KEY",StringComparison.Ordinal))throw new ArgumentException("Only the public key may be submitted.");pem=key.ExportSubjectPublicKeyInfoPem();}
        await using var s=await sessions.OpenAsync(cancellationToken);var member=await Authorize(s,false,cancellationToken);
        var entitlement=await s.Query<Entitlement>().SingleOrDefaultAsync(x=>x.Id==request.EntitlementId,cancellationToken)??throw new ArgumentException("Entitlement not found.");
        if(member.CustomerId is {} customer&&entitlement.CustomerId!=customer)throw new UnauthorizedAccessException();
        if(entitlement.IsRevoked||entitlement.StartsAtUtc>DateTimeOffset.UtcNow||entitlement.ExpiresAtUtc<=DateTimeOffset.UtcNow||!await s.Query<Contract>().AnyAsync(x=>x.Id==entitlement.ContractId&&x.Status=="Active",cancellationToken)||!await s.Query<Product>().AnyAsync(x=>x.Id==entitlement.ProductId&&x.IsActive,cancellationToken))throw new InvalidOperationException("An active entitlement and contract are required.");
        var existing=await s.Query<Installation>().SingleOrDefaultAsync(x=>x.Id==request.OperationId,cancellationToken);
        if(existing is not null){if(existing.EntitlementId!=entitlement.Id||existing.PublicKey!=pem||existing.IsRevoked)throw new ArgumentException("Enrollment operation cannot be reused.");return new(existing.Id,null,true);}
        if(await s.Query<Installation>().CountAsync(x=>x.EntitlementId==entitlement.Id&&!x.IsRevoked,cancellationToken)>=entitlement.InstallationLimit)throw new InvalidOperationException("Installation limit reached.");
        // Force a shared optimistic-concurrency write so two concurrent allocations cannot both succeed.
        entitlement.EnrollmentCounter++;
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var installation=new Installation{Id=request.OperationId,TenantId=actor.TenantId,CustomerId=entitlement.CustomerId,ProductId=entitlement.ProductId,EntitlementId=entitlement.Id,Name=name,PublicKey=pem,CredentialHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),RegisteredAtUtc=DateTimeOffset.UtcNow};
        s.Add(installation);await Commit(s,cancellationToken);return new(installation.Id,token,false);
    }
    public async Task<Guid> IssueAsync(IssueLicenseRequest request,CancellationToken cancellationToken=default)
    {
        if(request.OperationId==Guid.Empty)throw new ArgumentException("An issuance operation ID is required.");
        await using var s=await sessions.OpenAsync(cancellationToken);await Authorize(s,true,cancellationToken);
        var existing=await s.Query<LicenseRevision>().SingleOrDefaultAsync(x=>x.Id==request.OperationId,cancellationToken);
        if(existing is not null){if(existing.InstallationId!=request.InstallationId||existing.SchemaRevisionId!=request.SchemaRevisionId||existing.Status!="Ready")throw new ArgumentException("Issuance operation cannot be reused.");return existing.Id;}
        var installation=await s.Query<Installation>().SingleOrDefaultAsync(x=>x.Id==request.InstallationId&&!x.IsRevoked,cancellationToken)??throw new ArgumentException("Installation not found.");
        var entitlement=await s.Query<Entitlement>().SingleOrDefaultAsync(x=>x.Id==installation.EntitlementId&&!x.IsRevoked,cancellationToken)??throw new InvalidOperationException("Entitlement unavailable.");
        if(!await s.Query<Contract>().AnyAsync(x=>x.Id==entitlement.ContractId&&x.Status=="Active",cancellationToken)||!await s.Query<Product>().AnyAsync(x=>x.Id==installation.ProductId&&x.IsActive,cancellationToken)||entitlement.ExpiresAtUtc<=DateTimeOffset.UtcNow)throw new InvalidOperationException("An approved current or future entitlement term is required.");
        var schema=await s.Query<LicenseSchemaRevision>().SingleOrDefaultAsync(x=>x.Id==request.SchemaRevisionId&&x.ProductId==installation.ProductId&&x.IsPublished,cancellationToken)??throw new ArgumentException("Choose a published schema for this product.");
        if(schema.EncryptionProfile!=LicenseArtifactProtector.Profile)throw new InvalidOperationException("Unsupported artifact profile.");
        var model=JsonSerializer.Deserialize<LicenseModelDefinitionDto>(schema.SchemaJson,JsonOptions)??throw new InvalidOperationException("Invalid schema.");
        if(model.TenantId!=actor.TenantId||model.ProductId!=installation.ProductId)throw new InvalidOperationException("Schema scope is invalid.");
        var customer=await s.Query<Customer>().SingleAsync(x=>x.Id==installation.CustomerId,cancellationToken);
        var product=await s.Query<Product>().SingleAsync(x=>x.Id==installation.ProductId,cancellationToken);
        var contract=await s.Query<Contract>().SingleAsync(x=>x.Id==entitlement.ContractId,cancellationToken);
        using var custom=ValidateValues(model,ResolveValues(model,request.CustomValuesJson,customer,product,contract,entitlement,installation));
        var revision=(await s.Query<LicenseRevision>().Where(x=>x.InstallationId==installation.Id).MaxAsync(x=>(int?)x.Revision,cancellationToken)??0)+1;
        var now=DateTimeOffset.UtcNow;
        using var features=JsonDocument.Parse(entitlement.FeaturesJson);
        var payload=JsonSerializer.SerializeToUtf8Bytes(new {FormatVersion=1,LicenseId=request.OperationId,TenantId=actor.TenantId,CustomerId=installation.CustomerId,ProductId=installation.ProductId,EntitlementId=entitlement.Id,InstallationId=installation.Id,RecipientPublicKeySha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(installation.PublicKey))),SchemaRevisionId=schema.Id,SchemaRevision=schema.Revision,Revision=revision,IssuedAtUtc=now,StartsAtUtc=entitlement.StartsAtUtc,ExpiresAtUtc=entitlement.ExpiresAtUtc,InstallationLimit=entitlement.InstallationLimit,Features=features.RootElement,Custom=custom.RootElement});
        if(string.IsNullOrWhiteSpace(configuration.LicenseSigningPrivateKeyPem))throw new InvalidOperationException("Configure the issuer signing key in the server vault first.");
        using var signer=RSA.Create();signer.ImportFromPem(configuration.LicenseSigningPrivateKeyPem);var signingBytes=signer.ExportPkcs8PrivateKey();
        using var recipient=RSA.Create();recipient.ImportFromPem(installation.PublicKey);
        byte[] artifact;try{artifact=new LicenseArtifactProtector().Protect(payload,signingBytes,recipient.ExportSubjectPublicKeyInfo());}finally{CryptographicOperations.ZeroMemory(signingBytes);CryptographicOperations.ZeroMemory(payload);}
        s.Add(new LicenseRevision{Id=request.OperationId,TenantId=actor.TenantId,InstallationId=installation.Id,CustomerId=installation.CustomerId,ProductId=installation.ProductId,EntitlementId=entitlement.Id,SchemaRevisionId=schema.Id,Revision=revision,Status="Ready",Artifact=artifact,Extension=schema.Extension,IssuedAtUtc=now,ExpiresAtUtc=entitlement.ExpiresAtUtc});
        await Commit(s,cancellationToken);return request.OperationId;
    }
    public async Task<LicenseDownload> DownloadAsync(Guid revisionId,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);var member=await Authorize(s,false,cancellationToken);
        var item=await s.Query<LicenseRevision>().SingleOrDefaultAsync(x=>x.Id==revisionId&&x.Status=="Ready",cancellationToken)??throw new ArgumentException("License not found.");
        if(member.CustomerId is {} customer&&item.CustomerId!=customer)throw new UnauthorizedAccessException();
        if(item.Artifact is null)throw new InvalidOperationException("Artifact unavailable.");return new($"license-{item.Id:N}{item.Extension}",item.Artifact,"application/octet-stream");
    }
    public async Task<LicenseDownload> ExportModelAsync(Guid schemaId,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);await Authorize(s,true,cancellationToken);
        var schema=await s.Query<LicenseSchemaRevision>().SingleOrDefaultAsync(x=>x.Id==schemaId&&x.IsPublished,cancellationToken)??throw new ArgumentException("Schema not found.");
        var model=JsonSerializer.Deserialize<LicenseModelDefinitionDto>(schema.SchemaJson,JsonOptions)??throw new InvalidOperationException("Schema unavailable.");
        using var output=new MemoryStream();using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){foreach(var file in new LicenseModelCodeGenerator().Export(model)){using var writer=new StreamWriter(zip.CreateEntry(file.Key).Open(),new UTF8Encoding(false));await writer.WriteAsync(file.Value);}}
        return new($"license-model-{schema.Id:N}.zip",output.ToArray(),"application/zip");
    }
    private static JsonDocument ValidateValues(LicenseModelDefinitionDto model,string json)
    {
        if(json is null||json.Length>65536)throw new ArgumentException("Custom values exceed the size limit.");
        JsonDocument doc;try{doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16});}catch(JsonException){throw new ArgumentException("Custom values must be a JSON object.");}
        try{
            if(doc.RootElement.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Custom values must be an object.");
            var supplied=new HashSet<string>(StringComparer.Ordinal);var definitions=model.Fields.ToDictionary(x=>x.AttributeId,StringComparer.Ordinal);
            foreach(var p in doc.RootElement.EnumerateObject()){
                if(!supplied.Add(p.Name)||!definitions.TryGetValue(p.Name,out var field))throw new ArgumentException("Duplicate or unknown custom attribute.");
                if(p.Value.ValueKind==JsonValueKind.Null){if(!field.AllowsNull)throw new ArgumentException($"{p.Name} cannot be null.");continue;}
                bool valid=field.Type==LicenseFieldType.Boolean?p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:p.Value.ValueKind==JsonValueKind.String;
                if(valid&&field.Type!=LicenseFieldType.Boolean){var value=p.Value.GetString()!;valid=field.Type switch{LicenseFieldType.Text=>true,LicenseFieldType.Integer=>Regex.IsMatch(value,@"\A-?(0|[1-9][0-9]*)\z"),LicenseFieldType.Decimal=>Regex.IsMatch(value,@"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?\z"),LicenseFieldType.Identifier=>Guid.TryParseExact(value,"D",out _),LicenseFieldType.UtcDateTime=>Regex.IsMatch(value,@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z\z")&&DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out _),_=>false};}
                if(!valid)throw new ArgumentException($"Invalid value for {p.Name}.");
            }
            foreach(var field in model.Fields)if(field.IsRequired&&!supplied.Contains(field.AttributeId))throw new ArgumentException($"{field.AttributeId} is required.");
            return doc;
        }catch{doc.Dispose();throw;}
    }
    private static async Task Commit(IWorkspaceSession s,CancellationToken ct) { try { await s.CommitAsync(ct); } catch(DbUpdateException) { throw new InvalidOperationException("The data changed or the operation already exists. Reload and retry."); } }
    private static string Text(string? value,int max){var text=value?.Trim()??"";if(text.Length==0||text.Length>max)throw new ArgumentException($"Text must contain 1 to {max} characters.");return text;}
}
