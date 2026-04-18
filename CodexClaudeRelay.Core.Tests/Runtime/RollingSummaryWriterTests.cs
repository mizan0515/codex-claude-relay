using CodexClaudeRelay.Core.Models;
using CodexClaudeRelay.Core.Runtime;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Runtime;

public class RollingSummaryWriterTests
{
    private static RollingSummaryFields SampleFields(HandoffEnvelope? handoff = null, string? pendingPrompt = null) =>
        new(
            SessionId: "2026-04-18-g6",
            SegmentNumber: 2,
            RotationReason: "turn count threshold",
            SessionStartedAt: new DateTimeOffset(2026, 4, 18, 13, 0, 0, TimeSpan.Zero),
            TurnsSinceLastRotation: 7,
            ActiveAgentAtRotation: AgentRole.Codex,
            TotalInputTokens: 12345,
            TotalOutputTokens: 6789,
            TotalCacheReadInputTokens: 4000,
            TotalCacheCreationInputTokens: 2000,
            TotalCostClaudeUsd: 0.1234,
            TotalCostCodexUsd: 0.0567,
            LastHandoff: handoff,
            PendingPrompt: pendingPrompt);

    [Fact]
    public void BuildMarkdown_includes_all_required_sections_with_handoff_and_prompt()
    {
        var handoff = new HandoffEnvelope
        {
            Source = AgentRole.Codex,
            Target = AgentRole.Claude,
            Turn = 3,
            Ready = true,
            Reason = "peer review complete",
        };
        var md = RollingSummaryWriter.BuildMarkdown(SampleFields(handoff, "next: run G6 tests"));

        Assert.Contains("# Session 2026-04-18-g6 — segment 2", md);
        Assert.Contains("- Rotation reason: turn count threshold", md);
        Assert.Contains("- Turns in this segment: 7", md);
        Assert.Contains($"- Active agent at rotation: {AgentRole.Codex}", md);
        Assert.Contains("## Cumulative totals", md);
        Assert.Contains("- input_tokens: 12345", md);
        Assert.Contains("- output_tokens: 6789", md);
        Assert.Contains("- cost_claude_usd: 0.1234", md);
        Assert.Contains("- cost_codex_usd: 0.0567", md);
        Assert.Contains("## Last handoff", md);
        Assert.Contains($"- source: {AgentRole.Codex}", md);
        Assert.Contains($"- target: {AgentRole.Claude}", md);
        Assert.Contains("- turn: 3", md);
        Assert.Contains("- ready: True", md);
        Assert.Contains("- reason: peer review complete", md);
        Assert.Contains("## Pending prompt at rotation boundary", md);
        Assert.Contains("next: run G6 tests", md);
    }

    [Fact]
    public void BuildMarkdown_handles_missing_handoff_and_empty_prompt()
    {
        var md = RollingSummaryWriter.BuildMarkdown(SampleFields(handoff: null, pendingPrompt: null));

        Assert.Contains("- (no handoff captured this segment)", md);
        Assert.Contains("## Pending prompt at rotation boundary", md);
        Assert.Contains("(none)", md);
    }

    [Fact]
    public async Task WriteAsync_lands_file_with_expected_bytes_and_content()
    {
        var fields = SampleFields();
        var result = await RollingSummaryWriter.WriteAsync(fields, CancellationToken.None);

        try
        {
            Assert.True(File.Exists(result.Path));
            var disk = await File.ReadAllTextAsync(result.Path, CancellationToken.None);
            Assert.Equal(result.Markdown, disk);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(disk), result.Bytes);
            Assert.Contains(fields.SessionId, result.Path);
            Assert.Contains($"segment-{fields.SegmentNumber}", result.Path);
        }
        finally
        {
            if (File.Exists(result.Path))
            {
                File.Delete(result.Path);
            }
        }
    }

    [Fact]
    public void BuildGeneratedEventPayload_produces_valid_json_shape_with_escaping()
    {
        var payload = RollingSummaryWriter.BuildGeneratedEventPayload(
            path: @"C:\tmp\a""b\segment.md",
            bytes: 2048,
            segmentNumber: 2,
            sessionId: "sid-7",
            turns: 4,
            costClaudeUsd: 0.01,
            costCodexUsd: 0.02);

        Assert.StartsWith("{", payload);
        Assert.EndsWith("}", payload);
        Assert.Contains("\"bytes\":2048", payload);
        Assert.Contains("\"segment\":2", payload);
        Assert.Contains("\"session_id\":\"sid-7\"", payload);
        Assert.Contains("\"turns\":4", payload);
        Assert.Contains("\"cost_claude_usd\":0.0100", payload);
        Assert.Contains("\"cost_codex_usd\":0.0200", payload);
        Assert.Contains(@"\\", payload);
        Assert.Contains("\\\"", payload);
    }
}
