using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Policy;

/// <summary>
/// Per-role cost advisory contract for G1 peer symmetry (B3 step 1).
///
/// Today <see cref="Broker.RelayBroker"/> carries six role-conditional
/// methods (LogCodexPricingFallbackAsync, LogCodexRateCardStaleAsync,
/// LogClaudeCostCeilingDisabledAsync, LogCacheInflationSignalAsync,
/// LogCostAvailabilitySignalsAsync, TryRotateForClaudeCostCeilingAsync),
/// each starting with <c>if (role != AgentRole.X) return;</c>. That's
/// structurally asymmetric — the Codex side has two hooks, the Claude side
/// has four, and the broker knows both.
///
/// This interface is the landing pad for the symmetric refactor (operator
/// decision iter61 · option "e — generalize"). The first follow-up iter
/// migrates one method per PR into role-specific implementations behind
/// this seam, keeping behavior byte-identical. Until then, the broker's
/// existing methods remain the source of truth; the registry below is only
/// wired in tests to prove the type-level contract.
///
/// Symmetry rule: both <see cref="AgentRole.Codex"/> and
/// <see cref="AgentRole.Claude"/> MUST have an advisor registered. An
/// advisor that has nothing to say for a given hook returns
/// <see cref="ValueTask.CompletedTask"/> / false — not a missing
/// registration.
/// </summary>
public interface IAgentCostAdvisor
{
    /// <summary>The agent this advisor speaks for (<see cref="AgentRole.Codex"/> or <see cref="AgentRole.Claude"/>).</summary>
    string Role { get; }

    /// <summary>
    /// Fires zero or more informational advisories observed from the most
    /// recent usage reading. Default implementation is a no-op so concrete
    /// advisors only override the hooks they need.
    /// </summary>
    ValueTask OnUsageObservedAsync(RelayUsageMetrics usage, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Returns true if the role's cost ceiling was reached and the caller
    /// should rotate the session. Default: false (no ceiling enforced).
    /// </summary>
    ValueTask<bool> ShouldRotateForCostCeilingAsync(RelayUsageMetrics usage, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}

/// <summary>
/// Role → advisor lookup. Intentionally a pair (not a Dictionary) so the
/// symmetry is enforced at construction time: you cannot build a registry
/// missing one peer.
/// </summary>
public sealed class AgentCostAdvisorRegistry
{
    private readonly IAgentCostAdvisor _codex;
    private readonly IAgentCostAdvisor _claude;

    public AgentCostAdvisorRegistry(IAgentCostAdvisor codex, IAgentCostAdvisor claude)
    {
        ArgumentNullException.ThrowIfNull(codex);
        ArgumentNullException.ThrowIfNull(claude);

        if (codex.Role != AgentRole.Codex)
            throw new ArgumentException($"Expected advisor for '{AgentRole.Codex}', got '{codex.Role}'.", nameof(codex));
        if (claude.Role != AgentRole.Claude)
            throw new ArgumentException($"Expected advisor for '{AgentRole.Claude}', got '{claude.Role}'.", nameof(claude));

        _codex = codex;
        _claude = claude;
    }

    public IAgentCostAdvisor For(string role) => role switch
    {
        AgentRole.Codex => _codex,
        AgentRole.Claude => _claude,
        _ => throw new ArgumentException($"Unknown role '{role}'. Expected '{AgentRole.Codex}' or '{AgentRole.Claude}'.", nameof(role)),
    };
}

/// <summary>
/// Concrete advisor stubs for iter64 scaffolding. Both sides default to
/// no-op behavior — follow-up iters move the existing broker logic here
/// method-by-method without changing observable behavior.
/// </summary>
public sealed class CodexCostAdvisor : IAgentCostAdvisor
{
    public string Role => AgentRole.Codex;
}

/// <inheritdoc cref="CodexCostAdvisor" />
public sealed class ClaudeCostAdvisor : IAgentCostAdvisor
{
    public string Role => AgentRole.Claude;
}
