using CodexClaudeRelay.Core.Broker;
using CodexClaudeRelay.Core.Models;
using CodexClaudeRelay.Core.Policy;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Policy;

/// <summary>
/// B3 step 1 — scaffolding proof. The symmetry contract is enforced at
/// construction time (you cannot build a registry missing a peer) and the
/// <see cref="AgentCostAdvisorRegistry.For"/> lookup returns the right
/// advisor per role. Follow-up iters fill the advisor bodies; these tests
/// pin the contract so the migration stays symmetric.
/// </summary>
public class AgentCostAdvisorRegistryTests
{
    [Fact]
    public void Registry_returns_advisor_matching_role()
    {
        var registry = new AgentCostAdvisorRegistry(new CodexCostAdvisor(), new ClaudeCostAdvisor());

        Assert.Equal(AgentRole.Codex, registry.For(AgentRole.Codex).Role);
        Assert.Equal(AgentRole.Claude, registry.For(AgentRole.Claude).Role);
    }

    [Fact]
    public void Registry_rejects_swapped_role_advisors()
    {
        // Passing the Claude advisor in the codex slot must throw at construction.
        Assert.Throws<ArgumentException>(() =>
            new AgentCostAdvisorRegistry(new ClaudeCostAdvisor(), new ClaudeCostAdvisor()));

        Assert.Throws<ArgumentException>(() =>
            new AgentCostAdvisorRegistry(new CodexCostAdvisor(), new CodexCostAdvisor()));
    }

    [Fact]
    public void Registry_rejects_unknown_role_lookup()
    {
        var registry = new AgentCostAdvisorRegistry(new CodexCostAdvisor(), new ClaudeCostAdvisor());
        Assert.Throws<ArgumentException>(() => registry.For("martian"));
    }

    [Fact]
    public async Task Claude_advisor_stays_silent_when_usage_is_empty()
    {
        // No tokens, no auth method, no ceiling → nothing to say.
        IAgentCostAdvisor advisor = new ClaudeCostAdvisor();
        var ctx = new RecordingCostContext();
        await advisor.OnUsageObservedAsync(ctx, new RelayUsageMetrics(), default);
        Assert.False(await advisor.ShouldRotateForCostCeilingAsync(ctx, new RelayUsageMetrics(), default));
        Assert.Empty(ctx.Events);
    }

    [Fact]
    public async Task Claude_advisor_fires_cost_absent_once_when_tokens_but_no_cost()
    {
        var advisor = new ClaudeCostAdvisor();
        var ctx = new RecordingCostContext();
        var usage = new RelayUsageMetrics(InputTokens: 100, OutputTokens: 50, CostUsd: null, CliVersion: "1.2.3");

        await advisor.OnUsageObservedAsync(ctx, usage, default);
        await advisor.OnUsageObservedAsync(ctx, usage, default);

        var events = ctx.Events.Where(e => e.EventType == "claude.cost.absent").ToList();
        Assert.Single(events);
        Assert.Contains("1.2.3", events[0].Message);
        Assert.True(ctx.State.ClaudeCostAbsentAdvisoryFired);
    }

    [Fact]
    public async Task Claude_advisor_does_not_fire_cost_absent_when_cost_is_positive()
    {
        var advisor = new ClaudeCostAdvisor();
        var ctx = new RecordingCostContext();
        var usage = new RelayUsageMetrics(InputTokens: 100, OutputTokens: 50, CostUsd: 0.01d);

        await advisor.OnUsageObservedAsync(ctx, usage, default);

        Assert.DoesNotContain(ctx.Events, e => e.EventType == "claude.cost.absent");
    }

    [Fact]
    public async Task Claude_advisor_fires_ceiling_disabled_once_for_non_api_key_auth()
    {
        var advisor = new ClaudeCostAdvisor();
        var ctx = new RecordingCostContext { Options = new RelayBrokerOptions { MaxClaudeCostUsd = 5d } };
        var usage = new RelayUsageMetrics(AuthMethod: "claudeai-oauth");

        await advisor.OnUsageObservedAsync(ctx, usage, default);
        await advisor.OnUsageObservedAsync(ctx, usage, default);

        var events = ctx.Events.Where(e => e.EventType == "cost.ceiling.disabled").ToList();
        Assert.Single(events);
        Assert.True(ctx.State.ClaudeCostCeilingDisabledAdvisoryFired);
    }

    [Fact]
    public async Task Claude_advisor_rotates_when_api_key_cost_ceiling_reached()
    {
        var advisor = new ClaudeCostAdvisor();
        var ctx = new RecordingCostContext
        {
            Options = new RelayBrokerOptions { MaxClaudeCostUsd = 2d },
        };
        ctx.State.TotalCostClaudeUsd = 2.5d;
        ctx.State.ClaudeCostAtLastRotationUsd = 0d;
        var usage = new RelayUsageMetrics(AuthMethod: "api-key");

        var rotated = await advisor.ShouldRotateForCostCeilingAsync(ctx, usage, default);

        Assert.True(rotated);
        Assert.Contains(ctx.Events, e => e.EventType == "budget.tripped");
        Assert.Equal(1, ctx.RotateCallCount);
    }

    [Fact]
    public async Task Claude_advisor_does_not_rotate_below_ceiling()
    {
        var advisor = new ClaudeCostAdvisor();
        var ctx = new RecordingCostContext
        {
            Options = new RelayBrokerOptions { MaxClaudeCostUsd = 10d },
        };
        ctx.State.TotalCostClaudeUsd = 1d;
        var usage = new RelayUsageMetrics(AuthMethod: "api-key");

        var rotated = await advisor.ShouldRotateForCostCeilingAsync(ctx, usage, default);

        Assert.False(rotated);
        Assert.Equal(0, ctx.RotateCallCount);
    }

    [Fact]
    public async Task Codex_advisor_fires_pricing_fallback_once_when_reason_is_set()
    {
        var advisor = new CodexCostAdvisor();
        var ctx = new RecordingCostContext();
        var usage = new RelayUsageMetrics(PricingFallbackReason: "stub rate card unavailable", Model: "gpt-5");

        await advisor.OnUsageObservedAsync(ctx, usage, default);
        await advisor.OnUsageObservedAsync(ctx, usage, default); // second call must be idempotent

        var pricingEvents = ctx.Events.Where(e => e.EventType == "codex.pricing.fallback").ToList();
        Assert.Single(pricingEvents);
        Assert.Contains("stub rate card unavailable", pricingEvents[0].Message);
        Assert.True(ctx.State.CodexPricingFallbackAdvisoryFired);
    }

    [Fact]
    public async Task Codex_advisor_does_not_fire_pricing_fallback_when_reason_is_empty()
    {
        var advisor = new CodexCostAdvisor();
        var ctx = new RecordingCostContext();

        await advisor.OnUsageObservedAsync(ctx, new RelayUsageMetrics(), default);

        Assert.DoesNotContain(ctx.Events, e => e.EventType == "codex.pricing.fallback");
        Assert.False(ctx.State.CodexPricingFallbackAdvisoryFired);
    }

    [Fact]
    public async Task Codex_advisor_fires_rate_card_stale_once()
    {
        // Rate card is currently stubbed as always ≥180 days old (UnixEpoch anchor),
        // so this advisory fires once per session.
        var advisor = new CodexCostAdvisor();
        var ctx = new RecordingCostContext();

        await advisor.OnUsageObservedAsync(ctx, new RelayUsageMetrics(), default);
        await advisor.OnUsageObservedAsync(ctx, new RelayUsageMetrics(), default);

        var stale = ctx.Events.Where(e => e.EventType == "codex.rate_card.stale").ToList();
        Assert.Single(stale);
        Assert.True(ctx.State.CodexRateCardStaleAdvisoryFired);
    }

    private sealed class RecordingCostContext : IBrokerCostContext
    {
        public RelaySessionState State { get; } = new() { SessionId = "test" };
        public RelayBrokerOptions Options { get; set; } = new();
        public List<RelayLogEvent> Events { get; } = new();
        public int RotateCallCount { get; private set; }

        public Task PersistAndLogAsync(RelayLogEvent logEvent, CancellationToken cancellationToken)
        {
            Events.Add(logEvent);
            return Task.CompletedTask;
        }

        public Task<string> TripBudgetAsync(string role, string signal, string reason, string? payload, CancellationToken cancellationToken)
        {
            Events.Add(new RelayLogEvent(DateTimeOffset.Now, "budget.tripped", role, reason, payload));
            State.LastBudgetSignal = signal;
            return Task.FromResult(reason);
        }

        public Task RotateSessionAsync(string reason, CancellationToken cancellationToken)
        {
            RotateCallCount++;
            return Task.CompletedTask;
        }
    }
}
