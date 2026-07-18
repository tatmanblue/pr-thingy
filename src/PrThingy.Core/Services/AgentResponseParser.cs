using System.Text.Json;
using System.Text.Json.Serialization;
using PrThingy.Core.Models;

namespace PrThingy.Core.Services;

public static class AgentResponseParser
{
    public static ParsedBriefingContent Parse(string rawOutput)
    {
        string candidate = rawOutput.Trim();

        if (TryDeserialize(candidate, out ParsedBriefingContent? parsed))
            return parsed;

        string unfenced = StripMarkdownFence(candidate);
        if (unfenced != candidate && TryDeserialize(unfenced, out parsed))
            return parsed;

        string? extracted = ExtractBracedSubstring(unfenced);
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
            BriefingJsonSchema? schema = JsonSerializer.Deserialize<BriefingJsonSchema>(json, JsonOptions);
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
        string trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
            return text;

        int firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return text;

        string withoutOpeningFence = trimmed[(firstNewline + 1)..];
        int closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpeningFence[..closingFenceIndex].Trim()
            : withoutOpeningFence.Trim();
    }

    private static string? ExtractBracedSubstring(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
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
