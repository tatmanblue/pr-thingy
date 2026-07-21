namespace PrThingy.Core.Models;

// Default means omit the CLI's --effort flag entirely (use the CLI's own default).
// The rest map to claude --effort's documented values, lowercased.
public enum AgentEffortLevel
{
    Default,
    Low,
    Medium,
    High,
    XHigh,
    Max
}
