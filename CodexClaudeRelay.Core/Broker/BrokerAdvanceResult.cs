using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Broker;

public sealed record BrokerAdvanceResult(
    bool Succeeded,
    bool AwaitingHuman,
    bool Repaired,
    string Message,
    RelaySessionState State);
