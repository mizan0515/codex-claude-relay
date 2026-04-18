using System.Reflection;
using System.Text.Json;
using CodexClaudeRelay.Core.Adapters;
using CodexClaudeRelay.Core.Broker;
using CodexClaudeRelay.Core.Models;
using CodexClaudeRelay.Core.Persistence;
using CodexClaudeRelay.Core.Protocol;
using CodexClaudeRelay.Core.Runtime;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Broker;

[Collection("BrokerCwdMutating")]
public class BrokerRotationSmokeE2ETests
{
    private sealed class CapturingAdapter : IRelayAdapter
    {
        public string Role { get; init; } = string.Empty;
        public string HandoffJson { get; set; } = string.Empty;
        public string? LastPrompt { get; private set; }
        public string? LastCarryForward { get; private set; }

        public Task<AdapterStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AdapterStatus(RelayHealthStatus.Healthy, true, "ok"));

        public Task<RelayAdapterResult> RunTurnAsync(RelayTurnContext context, CancellationToken cancellationToken)
        {
            LastPrompt = context.Prompt;
            LastCarryForward = context.CarryForward;
            return Task.FromResult(new RelayAdapterResult(HandoffJson));
        }

        public Task<RelayAdapterResult> RunRepairAsync(RelayRepairContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new RelayAdapterResult(HandoffJson));
    }

    private sealed class InMemorySessionStore : IRelaySessionStore
    {
        public RelaySessionState? Last { get; set; }
        public Task SaveAsync(RelaySessionState state, CancellationToken cancellationToken) { Last = state; return Task.CompletedTask; }
        public Task<RelaySessionState?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Last);
    }

    private sealed class InMemoryEventLog : IEventLogWriter
    {
        public List<RelayLogEvent> Events { get; } = new();
        public Task AppendAsync(string sessionId, RelayLogEvent logEvent, CancellationToken cancellationToken)
        {
            Events.Add(logEvent);
            return Task.CompletedTask;
        }
        public string GetLogPath(string sessionId) => string.Empty;
    }

    private static string BuildHandoffJson(string source, string target, string sessionId, int turn, string prompt, string reason)
    {
        var env = new HandoffEnvelope
        {
            Source = source,
            Target = target,
            SessionId = sessionId,
            Turn = turn,
            Ready = true,
            Prompt = prompt,
            Reason = reason,
            Summary = new[] { "pending item A", "pending item B" },
            Completed = new[] { "completed work 1", "completed work 2" },
            Constraints = new[] { "no force-push" },
            CloseoutKind = CloseoutKind.PeerHandoff,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        return JsonSerializer.Serialize(env, HandoffJson.SerializerOptions);
    }

    [Fact]
    public async Task Rotation_emits_summary_generated_event_and_injects_carry_forward_on_next_turn()
    {
        var sessionId = "sess-g6-rot-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var tmpDir = Path.Combine(Path.GetTempPath(), "g6-rotation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        var originalCwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = tmpDir;

        var expectedSummaryPath = RollingSummaryWriter.ResolvePath(
            RollingSummaryWriter.ResolveBaseDirectory(), sessionId, 1);
        if (File.Exists(expectedSummaryPath)) File.Delete(expectedSummaryPath);

        try
        {
            var codex = new CapturingAdapter { Role = AgentRole.Codex };
            var claude = new CapturingAdapter { Role = AgentRole.Claude };
            var store = new InMemorySessionStore();
            var log = new InMemoryEventLog();
            var broker = new RelayBroker(new IRelayAdapter[] { codex, claude }, store, log);

            await broker.StartSessionAsync(sessionId, AgentRole.Codex, "initial prompt", CancellationToken.None);

            codex.HandoffJson = BuildHandoffJson(
                AgentRole.Codex, AgentRole.Claude, sessionId, 1,
                "claude, continue",
                "finish the G6 rotation smoke");
            await broker.AdvanceAsync(CancellationToken.None);

            Assert.Equal(AgentRole.Claude, broker.State.ActiveAgent);
            Assert.Equal("finish the G6 rotation smoke", broker.State.Goal);
            Assert.NotEmpty(broker.State.Completed);
            Assert.False(broker.State.CarryForwardPending);

            var rotateMethod = typeof(RelayBroker).GetMethod(
                "RotateSessionAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(rotateMethod);
            var priorRotationCount = broker.State.RotationCount;
            var task = (Task)rotateMethod!.Invoke(broker, new object[] { "g6 smoke rotation", CancellationToken.None })!;
            await task;

            Assert.True(File.Exists(expectedSummaryPath),
                $"expected rolling-summary file at {expectedSummaryPath}");
            Assert.True(broker.State.CarryForwardPending);
            Assert.Equal(priorRotationCount + 1, broker.State.RotationCount);

            var summaryEvent = Assert.Single(log.Events, e => e.EventType == "summary.generated");
            Assert.Contains($"segment {priorRotationCount + 1}", summaryEvent.Message);
            Assert.NotNull(summaryEvent.Payload);
            Assert.Contains("\"path\"", summaryEvent.Payload!);
            Assert.Contains("\"bytes\"", summaryEvent.Payload!);
            Assert.Contains($"\"segment\":{priorRotationCount + 1}", summaryEvent.Payload!);
            Assert.Contains(log.Events, e => e.EventType == "rotation.triggered");

            claude.HandoffJson = BuildHandoffJson(
                AgentRole.Claude, AgentRole.Codex, sessionId, broker.State.CurrentTurn,
                "codex, next", "rotation follow-up");
            await broker.AdvanceAsync(CancellationToken.None);

            Assert.NotNull(claude.LastCarryForward);
            Assert.Contains("## Carry-forward", claude.LastCarryForward!);
            Assert.Contains("- goal: finish the G6 rotation smoke", claude.LastCarryForward!);
            Assert.Contains("### Completed", claude.LastCarryForward!);
            Assert.Contains("completed work 1", claude.LastCarryForward!);
            Assert.Contains("### Pending", claude.LastCarryForward!);
            Assert.Contains("pending item A", claude.LastCarryForward!);

            Assert.Contains(log.Events, e =>
                e.EventType == "summary.loaded" && e.Role == AgentRole.Claude);
            Assert.False(broker.State.CarryForwardPending);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            if (File.Exists(expectedSummaryPath))
            {
                try { File.Delete(expectedSummaryPath); } catch { /* best-effort */ }
            }
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
        }
    }
}
