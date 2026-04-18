using CodexClaudeRelay.Core.Models;
using CodexClaudeRelay.Core.Protocol;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Protocol;

public class HandoffArtifactPersisterTests
{
    private static TurnPacket SamplePacket() => new()
    {
        From = AgentRole.Codex,
        Turn = 3,
        SessionId = "2026-04-18-persist",
        Handoff = new TurnHandoff
        {
            CloseoutKind = CloseoutKind.PeerHandoff,
            NextTask = "Verify persister writes to disk",
            Context = "G2-WIRE",
            ReadyForPeerVerification = true,
        },
    };

    [Fact]
    public async Task WriteAsync_creates_parent_directory_and_writes_rendered_body()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dadv2-persist-{Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(root, "sessions", "s1", "turn-3-handoff.md");
            var packet = SamplePacket();

            var bytes = await HandoffArtifactPersister.WriteAsync(packet, path);

            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            Assert.Equal(HandoffArtifactWriter.Render(packet), content);
            Assert.Equal(new FileInfo(path).Length, bytes);
            Assert.Contains("Next task", content);
            Assert.Contains(HandoffArtifactWriter.MandatoryTail, content);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_overwrites_existing_file_atomically()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dadv2-persist-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "turn-1-handoff.md");
            await File.WriteAllTextAsync(path, "stale content");

            await HandoffArtifactPersister.WriteAsync(SamplePacket(), path);

            var content = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("stale content", content);
            Assert.Contains("Next task", content);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FromHandoffEnvelope_maps_core_fields_with_peer_handoff_closeout()
    {
        var env = new HandoffEnvelope
        {
            Source = AgentRole.Claude,
            Target = AgentRole.Codex,
            SessionId = "sess-42",
            Turn = 7,
            Ready = true,
            Prompt = "do the next thing",
            Reason = "carry-forward context",
        };

        var packet = TurnPacketAdapter.FromHandoffEnvelope(env);

        Assert.Equal(AgentRole.Claude, packet.From);
        Assert.Equal(7, packet.Turn);
        Assert.Equal("sess-42", packet.SessionId);
        Assert.Equal(CloseoutKind.PeerHandoff, packet.Handoff.CloseoutKind);
        Assert.Equal("do the next thing", packet.Handoff.NextTask);
        Assert.Equal("carry-forward context", packet.Handoff.Context);
        Assert.True(packet.Handoff.ReadyForPeerVerification);
    }
}
