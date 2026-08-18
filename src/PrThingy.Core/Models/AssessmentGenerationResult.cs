namespace PrThingy.Core.Models;

public sealed record AssessmentGenerationResult(
    Briefing? Briefing,
    string? ErrorMessage);
