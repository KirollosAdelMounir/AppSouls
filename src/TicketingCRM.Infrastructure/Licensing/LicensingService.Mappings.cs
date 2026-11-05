using System.Globalization;
using System.Text.Json;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Models;
using TicketingCRM.Domain.Persistence.Models;
namespace TicketingCRM.Infrastructure.Licensing;
public sealed partial class LicensingService
{
    private static LicenseFieldType SourceType(LicenseValueSource source)=>source switch
    {
        LicenseValueSource.CustomerId or LicenseValueSource.ProductId or LicenseValueSource.InstallationId=>LicenseFieldType.Identifier,
        LicenseValueSource.InstallationLimit=>LicenseFieldType.Integer,
        LicenseValueSource.StartsAtUtc or LicenseValueSource.ExpiresAtUtc=>LicenseFieldType.UtcDateTime,
        _=>LicenseFieldType.Text
    };
    private static void ValidateMappings(IReadOnlyList<LicenseFieldDefinition> fields)
    {
        if(fields is null||fields.Count>128)throw new ArgumentException("Choose at most 128 attributes.");
        foreach(var field in fields)
        {
            if(field is null||string.IsNullOrWhiteSpace(field.Label)||field.Label.Length>160)throw new ArgumentException("Enter an attribute label of up to 160 characters.");
            if(!Enum.IsDefined(field.Source))throw new ArgumentException("Choose an approved attribute source.");
            if(field.Source!=LicenseValueSource.Manual&&field.Type!=SourceType(field.Source))throw new ArgumentException("The attribute type must match its selected source.");
            if(field.Source==LicenseValueSource.ExpiresAtUtc&&!field.AllowsNull)throw new ArgumentException("Expiry mappings must allow null for perpetual subscriptions.");
        }
    }
    private static string ResolveValues(LicenseModelDefinitionDto model,string input,Customer customer,Product product,Contract contract,Entitlement entitlement,Installation installation)
    {
        ValidateMappings(model.Fields);
        if(input is null||input.Length>65536)throw new ArgumentException("Custom values exceed the size limit.");
        JsonDocument document;try{document=JsonDocument.Parse(input,new JsonDocumentOptions{MaxDepth=16});}catch(JsonException){throw new ArgumentException("Custom values must be a JSON object.");}
        using(document)
        {
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Custom values must be an object.");
            var values=new Dictionary<string,object?>(StringComparer.Ordinal);
            foreach(var item in document.RootElement.EnumerateObject())
            {
                var field=model.Fields.SingleOrDefault(x=>x.AttributeId==item.Name);
                if(field is null||field.Source!=LicenseValueSource.Manual||!values.TryAdd(item.Name,item.Value.Clone()))throw new ArgumentException("Mapped, duplicate or unknown attributes cannot be supplied manually.");
            }
            foreach(var field in model.Fields.Where(x=>x.Source!=LicenseValueSource.Manual))values[field.AttributeId]=field.Source switch
            {
                LicenseValueSource.CustomerId=>customer.Id.ToString("D"),LicenseValueSource.CustomerName=>customer.Name,
                LicenseValueSource.CustomerEmail=>customer.Email,LicenseValueSource.CustomerCompanyNumber=>customer.CompanyNumber,
                LicenseValueSource.ProductId=>product.Id.ToString("D"),LicenseValueSource.ProductName=>product.Name,LicenseValueSource.ProductCode=>product.Code,
                LicenseValueSource.ContractReference=>contract.Reference,LicenseValueSource.InstallationId=>installation.Id.ToString("D"),LicenseValueSource.InstallationName=>installation.Name,
                LicenseValueSource.InstallationLimit=>entitlement.InstallationLimit.ToString(CultureInfo.InvariantCulture),
                LicenseValueSource.StartsAtUtc=>entitlement.StartsAtUtc.UtcDateTime.ToString("O",CultureInfo.InvariantCulture),
                LicenseValueSource.ExpiresAtUtc=>entitlement.ExpiresAtUtc?.UtcDateTime.ToString("O",CultureInfo.InvariantCulture),
                _=>throw new ArgumentException("Unsupported field source.")
            };
            return JsonSerializer.Serialize(values);
        }
    }
}
