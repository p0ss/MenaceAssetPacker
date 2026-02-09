using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Resources;

/// <summary>
/// MCP resource provider for template schemas.
/// Exposes template field definitions at modkit://schema/{templateType}
/// </summary>
[McpServerResourceType]
public class SchemaResource
{
    private readonly ModpackManager _modpackManager;

    public SchemaResource(ModpackManager modpackManager)
    {
        _modpackManager = modpackManager;
    }

    [McpServerResource(UriTemplate = "modkit://schema/{templateType}")]
    [Description("Get the schema (field definitions) for a template type. Shows all available fields and their types.")]
    public string GetSchema(string templateType)
    {
        var vanillaPath = _modpackManager.GetVanillaTemplatePath(templateType);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{templateType}' not found" });
        }

        try
        {
            var json = File.ReadAllText(vanillaPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (data == null || data.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "No instances found to infer schema" });
            }

            // Get the first instance to infer the schema
            var firstInstance = data.Values.First();
            var schema = InferSchema(firstInstance);

            return JsonSerializer.Serialize(new
            {
                templateType,
                fields = schema,
                fieldCount = schema.Count,
                sampleInstance = data.Keys.First()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static Dictionary<string, object> InferSchema(JsonElement element)
    {
        var schema = new Dictionary<string, object>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                schema[property.Name] = InferFieldType(property.Value);
            }
        }

        return schema;
    }

    private static object InferFieldType(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => new { type = "string", example = TruncateString(value.GetString(), 50) },
            JsonValueKind.Number => value.TryGetInt32(out var intVal)
                ? new { type = "integer", example = (object)intVal }
                : new { type = "number", example = (object)value.GetDouble() },
            JsonValueKind.True or JsonValueKind.False => new { type = "boolean", example = (object)value.GetBoolean() },
            JsonValueKind.Array => new { type = "array", elementCount = value.GetArrayLength() },
            JsonValueKind.Object => new { type = "object", fields = InferSchema(value) },
            JsonValueKind.Null => new { type = "null" },
            _ => new { type = "unknown" }
        };
    }

    private static string? TruncateString(string? str, int maxLength)
    {
        if (str == null) return null;
        return str.Length <= maxLength ? str : str[..maxLength] + "...";
    }
}
