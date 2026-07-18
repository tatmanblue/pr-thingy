using System.Text.Json;
using System.Text.Json.Serialization;
using PrThingy.Core.Models;

namespace PrThingy.Core.Services;

public static class AgentResponseParser
{
    public static ParsedBriefingContent Parse(string rawOutput)
    {
        var candidate = rawOutput.Trim();

        if (TryDeserialize(candidate, out var parsed))
            return parsed;

        var unfenced = StripMarkdownFence(candidate);
        if (unfenced != candidate && TryDeserialize(unfenced, out parsed))
            return parsed;

        var extracted = ExtractBracedSubstring(unfenced);
        if (extracted is not null && TryDeserialize(extracted, out parsed))
            return parsed;

        return new ParsedBriefingContent(
            Why: candidate,
            HighImpactFiles: [],
            TopRisks: [],
            IsWellFormed: false);
    }

    private static bool TryDeserialize(string json, out ParsedBriefingContent result)
    {
        try
        {
            var schema = JsonSerializer.Deserialize<BriefingJsonSchema>(json, JsonOptions);
            if (schema is null || string.IsNullOrWhiteSpace(schema.Why))
            {
                result = default!;
                return false;
            }

            result = new ParsedBriefingContent(
                Why: schema.Why,
                HighImpactFiles: schema.HighImpactFiles ?? [],
                TopRisks: schema.TopRisks ?? [],
                IsWellFormed: true);
            return true;
        }
        catch (JsonException)
        {
            result = default!;
            return false;
        }
    }

    private static string StripMarkdownFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
            return text;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return text;

        var withoutOpeningFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpeningFence[..closingFenceIndex].Trim()
            : withoutOpeningFence.Trim();
    }

    private static string? ExtractBracedSubstring(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class BriefingJsonSchema
    {
        [JsonPropertyName("why")]
        public string? Why { get; set; }

        [JsonPropertyName("highImpactFiles")]
        public List<string>? HighImpactFiles { get; set; }

        [JsonPropertyName("topRisks")]
        public List<RiskItem>? TopRisks { get; set; }
    }
}

public sealed record ParsedBriefingContent(
    string Why,
    IReadOnlyList<string> HighImpactFiles,
    IReadOnlyList<RiskItem> TopRisks,
    bool IsWellFormed);
