using System.Text.Json;
using System.Text.RegularExpressions;
using TicketingCRM.Domain.Licensing.DTOs;
using TicketingCRM.Domain.Licensing.Models;

namespace TicketingCRM.Core.Licensing;

/// <summary>Exports safe dictionary models with a common JSON contract; does not export cryptographic keys or SDKs.</summary>
public sealed partial class LicenseModelCodeGenerator
{
    public IReadOnlyDictionary<string, string> Export(LicenseModelDefinitionDto model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.TenantId == Guid.Empty || model.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(model.Name) || model.Fields is null || model.Fields.Count > 128)
            throw new ArgumentException("A scoped model with at most 128 fields is required.", nameof(model));
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in model.Fields)
        {
            if (field is null || !SafeName().IsMatch(field.AttributeId ?? "") || !names.Add(field.AttributeId!))
                throw new ArgumentException("Field IDs must be unique ASCII identifiers of at most 64 characters.", nameof(model));
            if (field.Type is LicenseFieldType.Object or LicenseFieldType.List or LicenseFieldType.Enumeration || !Enum.IsDefined(field.Type))
                throw new NotSupportedException("Nested, list and enumeration exports require richer schema metadata and are not supported yet.");
        }
        // Every user-controlled value is serialized as a string literal, never interpolated as source syntax.
        var schema = JsonSerializer.Serialize(model.Fields.Select(f => new { name = f.AttributeId, type = f.Type.ToString(), required = f.IsRequired, nullable = f.AllowsNull }));
        var schemaLiteral = JsonSerializer.Serialize(schema);
        var js = """
// App Souls model export v1. Integer and Decimal are canonical decimal strings.
const fields = JSON.parse(SCHEMA);
export function validateLicenseModel(value) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new TypeError('Expected object');
  for (const key of Object.keys(value)) if (!fields.some(f => f.name === key)) throw new TypeError('Unknown field: ' + key);
  for (const f of fields) {
    if (!Object.hasOwn(value, f.name)) { if (f.required) throw new TypeError('Missing field: ' + f.name); continue; }
    const v = value[f.name];
    if (v === null) { if (!f.nullable) throw new TypeError('Null field: ' + f.name); continue; }
    const valid = f.type === 'Boolean' ? typeof v === 'boolean' : typeof v === 'string' &&
      (f.type === 'Text' || f.type === 'Integer' && /^-?(0|[1-9][0-9]*)$(?![\s\S])/.test(v) ||
      f.type === 'Decimal' && /^-?(0|[1-9][0-9]*)(\.[0-9]+)?$(?![\s\S])/.test(v) ||
      f.type === 'Identifier' && /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$(?![\s\S])/.test(v) ||
      f.type === 'UtcDateTime' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$(?![\s\S])/.test(v) && !v.startsWith('0000') && !Number.isNaN(Date.parse(v)) && new Date(v).toISOString().slice(0, 19) === v.slice(0, 19));
    if (!valid) throw new TypeError('Invalid field: ' + f.name);
  }
  return value;
}
export function serializeLicenseModel(value) { return JSON.stringify(validateLicenseModel(value)); }
export function parseLicenseModel(json) { return validateLicenseModel(JSON.parse(json)); }
""".Replace("SCHEMA", schemaLiteral, StringComparison.Ordinal);
        var py = """
# App Souls model export v1. Integer and Decimal remain strings to preserve precision.
import json
import re
from datetime import datetime
_FIELDS = json.loads(SCHEMA)
def validate_license_model(value):
    if not isinstance(value, dict): raise ValueError('Expected object')
    if any(k not in {f['name'] for f in _FIELDS} for k in value): raise ValueError('Unknown field')
    for f in _FIELDS:
        if f['name'] not in value:
            if f['required']: raise ValueError('Missing field: ' + f['name'])
            continue
        v = value[f['name']]
        if v is None:
            if not f['nullable']: raise ValueError('Null field: ' + f['name'])
            continue
        t = f['type']
        valid = isinstance(v, bool) if t == 'Boolean' else isinstance(v, str)
        patterns = {'Integer': r'-?(0|[1-9][0-9]*)', 'Decimal': r'-?(0|[1-9][0-9]*)(\.[0-9]+)?', 'Identifier': r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', 'UtcDateTime': r'[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z'}
        if valid and t in patterns: valid = re.fullmatch(patterns[t], v) is not None
        if valid and t == 'UtcDateTime':
            try: datetime.fromisoformat(v.replace('Z', '+00:00'))
            except ValueError: valid = False
        if not valid: raise ValueError('Invalid field: ' + f['name'])
    return value

def serialize_license_model(value): return json.dumps(validate_license_model(value), ensure_ascii=False)
def parse_license_model(value): return validate_license_model(json.loads(value))
""".Replace("SCHEMA", schemaLiteral, StringComparison.Ordinal);
        var csFields = string.Join(",\n", model.Fields.Select(f => $"        new({JsonSerializer.Serialize(f.AttributeId)}, {JsonSerializer.Serialize(f.Type.ToString())}, {f.IsRequired.ToString().ToLowerInvariant()}, {f.AllowsNull.ToString().ToLowerInvariant()})"));
        var cs = """
// App Souls model export v1. No private keys. All large/exact numeric values use strings.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
public static class LicenseModel
{
    private sealed record Field(string Name, string Type, bool Required, bool Nullable);
    private static readonly Field[] Fields = [FIELDS];
    public static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object) throw new FormatException("Expected object");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Fields) names.Add(f.Name);
        foreach (var p in value.EnumerateObject()) if (!names.Contains(p.Name)) throw new FormatException("Unknown field");
        foreach (var f in Fields)
        {
            if (!value.TryGetProperty(f.Name, out var v)) { if (f.Required) throw new FormatException("Missing field: " + f.Name); continue; }
            if (v.ValueKind == JsonValueKind.Null) { if (!f.Nullable) throw new FormatException("Null field: " + f.Name); continue; }
            bool valid = f.Type == "Boolean" ? v.ValueKind is JsonValueKind.True or JsonValueKind.False : v.ValueKind == JsonValueKind.String;
            if (valid && f.Type != "Boolean")
            {
                var text = v.GetString()!;
                valid = f.Type switch {
                    "Integer" => Regex.IsMatch(text, @"\A-?(0|[1-9][0-9]*)\z"),
                    "Decimal" => Regex.IsMatch(text, @"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?\z"),
                    "Identifier" => Guid.TryParseExact(text, "D", out _),
                    "UtcDateTime" => Regex.IsMatch(text, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z\z") && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _),
                    _ => true };
            }
            if (!valid) throw new FormatException("Invalid field: " + f.Name);
        }
        return value.Clone();
    }
    public static string Serialize(JsonElement model) => Parse(model.GetRawText()).GetRawText();
}
""".Replace("FIELDS", csFields, StringComparison.Ordinal);
        var javaFields = string.Join(",\n", model.Fields.Select(f => $"        new Field({JsonSerializer.Serialize(f.AttributeId)}, {JsonSerializer.Serialize(f.Type.ToString())}, {f.IsRequired.ToString().ToLowerInvariant()}, {f.AllowsNull.ToString().ToLowerInvariant()})"));
        var java = """
// App Souls model export v1. Java 17+. JSON adapters must preserve strings and booleans.
import java.util.Map;
import java.util.Set;
import java.util.HashSet;
import java.time.Instant;
public final class LicenseModel {
    private record Field(String name, String type, boolean required, boolean nullable) {}
    private static final Field[] FIELDS = {DEFINITIONS};
    private LicenseModel() {}
    public static Map<String, Object> validate(Map<String, Object> value) {
        if (value == null) throw new IllegalArgumentException("Expected object");
        Set<String> names = new HashSet<>();
        for (Field f : FIELDS) names.add(f.name());
        for (String k : value.keySet()) if (!names.contains(k)) throw new IllegalArgumentException("Unknown field");
        for (Field f : FIELDS) {
            if (!value.containsKey(f.name())) { if (f.required()) throw new IllegalArgumentException("Missing field: " + f.name()); continue; }
            Object v = value.get(f.name());
            if (v == null) { if (!f.nullable()) throw new IllegalArgumentException("Null field: " + f.name()); continue; }
            boolean valid = f.type().equals("Boolean") ? v instanceof Boolean : v instanceof String;
            if (valid && v instanceof String s) {
                valid = switch (f.type()) {
                    case "Integer" -> s.matches("-?(0|[1-9][0-9]*)");
                    case "Decimal" -> s.matches("-?(0|[1-9][0-9]*)(\\.[0-9]+)?");
                    case "Identifier" -> s.matches("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                    case "UtcDateTime" -> validInstant(s);
                    default -> true;
                };
            }
            if (!valid) throw new IllegalArgumentException("Invalid field: " + f.name());
        }
        return value;
    }
    private static boolean validInstant(String value) {
        if (!value.matches("[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z")) return false;
        try { java.time.LocalDateTime.parse(value.substring(0, value.length() - 1)); return !value.startsWith("0000"); } catch (java.time.DateTimeException error) { return false; }
    }
}
""".Replace("DEFINITIONS", javaFields, StringComparison.Ordinal);
        var manifest = JsonSerializer.Serialize(new { generatorVersion = 1, wireVersion = 1, model.TenantId, model.ProductId, model.Name, fields = model.Fields, limitations = "Primitive fields only. Java uses caller JSON adapter. Decimal and Integer are strings. Verify original signed bytes before parsing. Duplicate JSON properties must be rejected by the integration parser." }, new JsonSerializerOptions { WriteIndented = true });
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LicenseModel.cs"] = cs, ["license-model.js"] = js, ["LicenseModel.java"] = java,
            ["license_model.py"] = py, ["manifest.json"] = manifest
        };
    }
    [GeneratedRegex("\\A[A-Za-z][A-Za-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();
}
