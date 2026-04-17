using CodexClaudeRelay.Core.Broker;

namespace CodexClaudeRelay.CodexProtocol;

public sealed class CodexProtocolConnectionOptions
{
    public required string WorkingDirectory { get; init; }

    public Action<CodexProtocolMessage>? MessageObserver { get; init; }

    public Func<CodexProtocolServerRequest, CancellationToken, Task<CodexProtocolServerRequestResponse>>? ServerRequestHandler { get; init; }

    public RelayJobObjectOptions? JobObjectOptions { get; init; }
}
