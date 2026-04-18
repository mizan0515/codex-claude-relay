using CodexClaudeRelay.Core.Broker;
using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Policy;

/// <summary>
/// Narrow seam the broker hands to each advisor so advisors can read the
/// session state, inspect options, persist log events, trip budget signals,
/// and rotate the session without pulling in the whole
/// <see cref="Broker.RelayBroker"/> surface.
///
/// Kept intentionally small — if an advisor needs a new hook, we add
/// exactly that hook, not the whole broker. iter66 (B3 step 3) added
/// <see cref="Options"/>, <see cref="TripBudgetAsync"/>, and
/// <see cref="RotateSessionAsync"/> so the Claude cost-ceiling rotate path
/// can be owned by <see cref="ClaudeCostAdvisor"/>.
/// </summary>
public interface IBrokerCostContext
{
    RelaySessionState State { get; }

    RelayBrokerOptions Options { get; }

    Task PersistAndLogAsync(RelayLogEvent logEvent, CancellationToken cancellationToken);

    Task<string> TripBudgetAsync(string role, string signal, string reason, string? payload, CancellationToken cancellationToken);

    Task RotateSessionAsync(string reason, CancellationToken cancellationToken);
}

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
    ValueTask OnUsageObservedAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Returns true if the role's cost ceiling was reached and the caller
    /// should rotate the session. Default: false (no ceiling enforced).
    /// </summary>
    ValueTask<bool> ShouldRotateForCostCeilingAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken) => ValueTask.FromResult(false);
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
/// Codex-side cost advisories. iter65 (B3 step 2) absorbed the two Codex
/// branches from <see cref="Broker.RelayBroker"/>:
/// pricing-fallback notice (one-shot when the adapter reports a fallback
/// reason) and rate-card-stale notice (one-shot when the stub rate-card
/// age ≥ 180 days). Behavior is byte-identical to the pre-move code.
/// </summary>
public sealed class CodexCostAdvisor : IAgentCostAdvisor
{
    private const string RateCardLabel = "(rate-card removed — DAD-v2 reset)";

    public string Role => AgentRole.Codex;

    public async ValueTask OnUsageObservedAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(usage);

        if (!string.IsNullOrWhiteSpace(usage.PricingFallbackReason) &&
            !ctx.State.CodexPricingFallbackAdvisoryFired)
        {
            ctx.State.CodexPricingFallbackAdvisoryFired = true;
            await ctx.PersistAndLogAsync(
                new RelayLogEvent(
                    DateTimeOffset.Now,
                    "codex.pricing.fallback",
                    AgentRole.Codex,
                    $"{usage.PricingFallbackReason}. Rate card: {RateCardLabel}. codex_model={usage.Model ?? "unknown"}",
                    usage.RawJson),
                cancellationToken).ConfigureAwait(false);
        }

        if (!ctx.State.CodexRateCardStaleAdvisoryFired)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var ageDays = today.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;
            if (ageDays >= 180)
            {
                ctx.State.CodexRateCardStaleAdvisoryFired = true;
                await ctx.PersistAndLogAsync(
                    new RelayLogEvent(
                        DateTimeOffset.Now,
                        "codex.rate_card.stale",
                        AgentRole.Codex,
                        $"Codex rate card {RateCardLabel} is {ageDays} days old. Local Codex cost estimates for this session may be inaccurate until the rate card is refreshed."),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Claude-side cost advisories. iter66 (B3 step 3) absorbed the four Claude
/// branches from <see cref="Broker.RelayBroker"/>:
/// cost-availability / cache-inflation / cost-ceiling-disabled log fires
/// (all one-shot, gated on existing State flags) are emitted from
/// <see cref="OnUsageObservedAsync"/>; the api-key cost-ceiling rotate
/// flow is owned by <see cref="ShouldRotateForCostCeilingAsync"/>.
/// Behavior is byte-identical to the pre-move code (same event types,
/// same messages, same State flag names, same trip/rotate sequence).
/// </summary>
public sealed class ClaudeCostAdvisor : IAgentCostAdvisor
{
    private const long SuspiciousClaudeCacheCreationTokens = 15_000;

    public string Role => AgentRole.Claude;

    public async ValueTask OnUsageObservedAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(usage);

        await LogCostAvailabilityAsync(ctx, usage, cancellationToken).ConfigureAwait(false);
        await LogCacheInflationAsync(ctx, usage, cancellationToken).ConfigureAwait(false);
        await LogCostCeilingDisabledAsync(ctx, usage, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> ShouldRotateForCostCeilingAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(usage);

        if (!ctx.Options.MaxClaudeCostUsd.HasValue)
        {
            return false;
        }

        if (!ClaudeAuthMethod.IsApiKey(usage.AuthMethod))
        {
            return false;
        }

        var segmentCost = Math.Max(0d, ctx.State.TotalCostClaudeUsd - ctx.State.ClaudeCostAtLastRotationUsd);
        if (segmentCost < ctx.Options.MaxClaudeCostUsd.Value)
        {
            return false;
        }

        var reason = await ctx.TripBudgetAsync(
            AgentRole.Claude,
            "claude_cost_ceiling",
            $"Claude api-key estimated cost ceiling reached for this session segment. segment_cost_usd={segmentCost:F4}, ceiling_usd={ctx.Options.MaxClaudeCostUsd.Value:F4}. Rotating session.",
            usage.RawJson,
            cancellationToken).ConfigureAwait(false);
        await ctx.RotateSessionAsync(reason, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task LogCostAvailabilityAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken)
    {
        if (ctx.State.ClaudeCostAbsentAdvisoryFired)
        {
            return;
        }

        var turnHadRealTokens =
            (usage.InputTokens ?? 0) > 0 ||
            (usage.OutputTokens ?? 0) > 0 ||
            (usage.CacheCreationInputTokens ?? 0) > 0 ||
            (usage.CacheReadInputTokens ?? 0) > 0;

        if (!turnHadRealTokens || (usage.CostUsd is not null && usage.CostUsd > 0d))
        {
            return;
        }

        ctx.State.ClaudeCostAbsentAdvisoryFired = true;
        await ctx.PersistAndLogAsync(
            new RelayLogEvent(
                DateTimeOffset.Now,
                "claude.cost.absent",
                AgentRole.Claude,
                $"Claude reported token usage but no positive CLI-estimated cost. This can happen on subscription plans, provider-routed deployments, or CLI versions without cost reporting. claude_cli_version={usage.CliVersion ?? "unknown"}",
                usage.RawJson),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task LogCacheInflationAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken)
    {
        if (ctx.State.TurnsSinceLastRotation < 1 || ctx.State.CacheInflationAdvisoryFired)
        {
            return;
        }

        var cacheCreationTokens = usage.CacheCreationInputTokens ?? 0;
        var nonCachedInputTokens = usage.InputTokens ?? 0;
        if (cacheCreationTokens < SuspiciousClaudeCacheCreationTokens)
        {
            return;
        }

        if (nonCachedInputTokens > 0 && cacheCreationTokens <= nonCachedInputTokens * 2)
        {
            return;
        }

        ctx.State.CacheInflationAdvisoryFired = true;
        await ctx.PersistAndLogAsync(
            new RelayLogEvent(
                DateTimeOffset.Now,
                "cache.inflation.suspected",
                AgentRole.Claude,
                $"cache_creation_input_tokens={cacheCreationTokens}, input_tokens={nonCachedInputTokens}, claude_cli_version={usage.CliVersion ?? "unknown"}. Possible Claude CLI cache-creation inflation (see Anthropic issue #46917). Further inflation this session will remain visible only in adapter.usage totals.",
                usage.RawJson),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task LogCostCeilingDisabledAsync(IBrokerCostContext ctx, RelayUsageMetrics usage, CancellationToken cancellationToken)
    {
        if (!ctx.Options.MaxClaudeCostUsd.HasValue ||
            ctx.State.ClaudeCostCeilingDisabledAdvisoryFired ||
            string.IsNullOrWhiteSpace(usage.AuthMethod) ||
            ClaudeAuthMethod.IsApiKey(usage.AuthMethod))
        {
            return;
        }

        ctx.State.ClaudeCostCeilingDisabledAdvisoryFired = true;
        await ctx.PersistAndLogAsync(
            new RelayLogEvent(
                DateTimeOffset.Now,
                "cost.ceiling.disabled",
                AgentRole.Claude,
                $"Configured Claude cost ceiling is inactive for this session segment because auth is not api-key. ceiling_usd={ctx.Options.MaxClaudeCostUsd.Value:F4}, observed_auth_method={usage.AuthMethod}.",
                usage.RawJson),
            cancellationToken).ConfigureAwait(false);
    }
}
