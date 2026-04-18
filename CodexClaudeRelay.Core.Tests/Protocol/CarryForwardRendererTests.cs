using CodexClaudeRelay.Core.Protocol;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Protocol;

public class CarryForwardRendererTests
{
    [Fact]
    public void TryBuild_returns_null_when_not_pending()
    {
        var result = CarryForwardRenderer.TryBuild(
            carryForwardPending: false,
            lastHandoffHash: "abc",
            goal: "ship g6",
            completed: new[] { "a" },
            pending: new[] { "b" },
            constraints: new[] { "c" });

        Assert.Null(result);
    }

    [Fact]
    public void TryBuild_returns_null_when_pending_but_all_fields_empty()
    {
        var result = CarryForwardRenderer.TryBuild(
            carryForwardPending: true,
            lastHandoffHash: null,
            goal: null,
            completed: Array.Empty<string>(),
            pending: Array.Empty<string>(),
            constraints: Array.Empty<string>());

        Assert.Null(result);
    }

    [Fact]
    public void TryBuild_composes_full_block_with_all_sections_in_order()
    {
        var result = CarryForwardRenderer.TryBuild(
            carryForwardPending: true,
            lastHandoffHash: "sha256:1a2b",
            goal: "automate DAD-v2",
            completed: new[] { "G3 [x]", "G4 [~]" },
            pending: new[] { "G6 coverage" },
            constraints: new[] { "peer-symmetric only" });

        Assert.NotNull(result);
        Assert.StartsWith("## Carry-forward", result);
        Assert.Contains("- prior_handoff_hash: sha256:1a2b", result);
        Assert.Contains("- goal: automate DAD-v2", result);
        Assert.Contains("### Completed", result);
        Assert.Contains("- G3 [x]", result);
        Assert.Contains("- G4 [~]", result);
        Assert.Contains("### Pending", result);
        Assert.Contains("- G6 coverage", result);
        Assert.Contains("### Constraints", result);
        Assert.Contains("- peer-symmetric only", result);

        var completedIdx = result!.IndexOf("### Completed", StringComparison.Ordinal);
        var pendingIdx = result.IndexOf("### Pending", StringComparison.Ordinal);
        var constraintsIdx = result.IndexOf("### Constraints", StringComparison.Ordinal);
        Assert.True(completedIdx < pendingIdx);
        Assert.True(pendingIdx < constraintsIdx);
    }

    [Fact]
    public void TryBuild_omits_missing_sections_but_keeps_header()
    {
        var result = CarryForwardRenderer.TryBuild(
            carryForwardPending: true,
            lastHandoffHash: null,
            goal: "solo goal",
            completed: Array.Empty<string>(),
            pending: new[] { "one" },
            constraints: Array.Empty<string>());

        Assert.NotNull(result);
        Assert.Contains("## Carry-forward", result);
        Assert.DoesNotContain("prior_handoff_hash", result);
        Assert.Contains("- goal: solo goal", result);
        Assert.DoesNotContain("### Completed", result);
        Assert.Contains("### Pending", result);
        Assert.DoesNotContain("### Constraints", result);
    }
}
