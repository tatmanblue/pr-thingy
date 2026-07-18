using System.Text.Json.Serialization;

namespace PrThingy.Core.Models;

[JsonConverter(typeof(RiskItemJsonConverter))]
public sealed record RiskItem
{
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public required string Description { get; init; }

    public bool HasLocation => !string.IsNullOrWhiteSpace(FilePath);

    public string LocationDisplay => Line is { } line ? $"{FilePath}:{line}" : FilePath ?? string.Empty;
}
