using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Persistence;

public interface IRelaySessionStore
{
    Task SaveAsync(RelaySessionState state, CancellationToken cancellationToken);

    Task<RelaySessionState?> LoadAsync(CancellationToken cancellationToken);
}
