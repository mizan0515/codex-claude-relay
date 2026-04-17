using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Adapters;

public sealed record RelayAdapterResult(
    string Output,
    string? SessionHandle = null,
    string? Diagnostics = null,
    RelayUsageMetrics? Usage = null,
    bool UsageIsCumulative = false,
    IReadOnlyList<RelayObservedAction>? ObservedActions = null);
