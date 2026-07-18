using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrThingy.Core.Models;

// Reads both the current {file, line, description} shape and the plain-string shape used by
// briefings persisted before RiskItem existed (and by agents that ignore the schema and just
// return a string) — either would otherwise crash the dashboard on load.
public sealed class RiskItemJsonConverter : JsonConverter<RiskItem>
{
    public override RiskItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new RiskItem { Description = reader.GetString() ?? string.Empty };

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        return new RiskItem
        {
            Description = TryGetString(root, "description") ?? string.Empty,
            FilePath = TryGetString(root, "filePath", "file", "path"),
            Line = TryGetInt(root, "line")
        };
    }

    public override void Write(Utf8JsonWriter writer, RiskItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("FilePath", value.FilePath);
        if (value.Line is { } line)
            writer.WriteNumber("Line", line);
        else
            writer.WriteNull("Line");
        writer.WriteString("Description", value.Description);
        writer.WriteEndObject();
    }

    private static string? TryGetString(JsonElement root, params string[] propertyNames)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (Array.Exists(propertyNames, name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
        }

        return null;
    }

    private static int? TryGetInt(JsonElement root, params string[] propertyNames)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (Array.Exists(propertyNames, name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                && property.Value.ValueKind == JsonValueKind.Number)
                return property.Value.GetInt32();
        }

        return null;
    }
}
