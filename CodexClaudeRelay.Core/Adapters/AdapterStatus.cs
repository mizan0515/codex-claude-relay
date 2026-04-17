using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Adapters;

public sealed record AdapterStatus(
    RelayHealthStatus Health,
    bool IsAuthenticated,
    string Message);
