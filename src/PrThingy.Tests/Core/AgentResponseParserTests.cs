using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Xunit;

namespace PrThingy.Tests.Core;

public class AgentResponseParserTests
{
    [Fact]
    public void Parse_CleanJson_ReturnsWellFormedContent()
    {
        const string raw = """
            {"why": "fixes a bug", "highImpactFiles": ["a.cs", "b.cs"], "topRisks": [
                {"file": "a.cs", "line": 10, "description": "risk 1"},
                {"file": null, "line": null, "description": "risk 2"}
            ]}
            """;

        ParsedBriefingContent result = AgentResponseParser.Parse(raw);

        Assert.True(result.IsWellFormed);
        Assert.Equal("fixes a bug", result.Why);
        Assert.Equal(["a.cs", "b.cs"], result.HighImpactFiles);
        Assert.Equal(2, result.TopRisks.Count);
        Assert.Equal("a.cs", result.TopRisks[0].FilePath);
        Assert.Equal(10, result.TopRisks[0].Line);
        Assert.Equal("risk 1", result.TopRisks[0].Description);
        Assert.Null(result.TopRisks[1].FilePath);
        Assert.Null(result.TopRisks[1].Line);
        Assert.Equal("risk 2", result.TopRisks[1].Description);
    }

    [Fact]
    public void Parse_LegacyPlainStringTopRisks_StillParses()
    {
        const string raw = """{"why": "fixes a bug", "highImpactFiles": [], "topRisks": ["risk 1", "risk 2"]}""";

        ParsedBriefingContent result = AgentResponseParser.Parse(raw);

        Assert.True(result.IsWellFormed);
        Assert.Equal([new RiskItem { Description = "risk 1" }, new RiskItem { Description = "risk 2" }], result.TopRisks);
    }

    [Fact]
    public void Parse_JsonWrappedInMarkdownFence_StripsFenceAndParses()
    {
        const string raw = """
            ```json
            {"why": "adds a feature", "highImpactFiles": ["c.cs"], "topRisks": []}
            ```
            """;

        ParsedBriefingContent result = AgentResponseParser.Parse(raw);

        Assert.True(result.IsWellFormed);
        Assert.Equal("adds a feature", result.Why);
        Assert.Equal(["c.cs"], result.HighImpactFiles);
        Assert.Empty(result.TopRisks);
    }

    [Fact]
    public void Parse_JsonWithSurroundingCommentary_ExtractsBracedSubstring()
    {
        const string raw = """
            Sure, here's the analysis:
            {"why": "refactor", "highImpactFiles": [], "topRisks": ["risk"]}
            Let me know if you need more detail.
            """;

        ParsedBriefingContent result = AgentResponseParser.Parse(raw);

        Assert.True(result.IsWellFormed);
        Assert.Equal("refactor", result.Why);
    }

    [Fact]
    public void Parse_MalformedText_FallsBackToRawOutput()
    {
        const string raw = "I couldn't analyze this PR because the diff was empty.";

        ParsedBriefingContent result = AgentResponseParser.Parse(raw);

        Assert.False(result.IsWellFormed);
        Assert.Equal(raw, result.Why);
        Assert.Empty(result.HighImpactFiles);
        Assert.Empty(result.TopRisks);
    }

    [Fact]
    public void Parse_EmptyString_FallsBackWithoutThrowing()
    {
        ParsedBriefingContent result = AgentResponseParser.Parse(string.Empty);

        Assert.False(result.IsWellFormed);
        Assert.Equal(string.Empty, result.Why);
    }

    [Fact]
    public void Parse_JsonWithExtraUnknownFields_IgnoresThemAndParsesKnownFields()
    {
        const string raw = """{"why": "cleanup", "highImpactFiles": ["x.cs"], "topRisks": [], "confidence": 0.9}""";

        ParsedBriefingContent result = AgentResponseParser.Parse(raw);

        Assert.True(result.IsWellFormed);
        Assert.Equal("cleanup", result.Why);
    }
}
