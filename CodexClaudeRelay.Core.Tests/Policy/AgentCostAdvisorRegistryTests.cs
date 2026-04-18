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

    [Theory]
    [InlineData(AgentRole.Codex)]
    [InlineData(AgentRole.Claude)]
    public async Task Default_advisor_hooks_are_noop_and_nonrotating(string role)
    {
        var registry = new AgentCostAdvisorRegistry(new CodexCostAdvisor(), new ClaudeCostAdvisor());
        var advisor = registry.For(role);

        // The default-interface hooks must be side-effect-free and non-rotating
        // so any future per-role override is a purely additive change.
        await advisor.OnUsageObservedAsync(new RelayUsageMetrics(), default);
        var shouldRotate = await advisor.ShouldRotateForCostCeilingAsync(new RelayUsageMetrics(), default);

        Assert.False(shouldRotate);
    }
}
