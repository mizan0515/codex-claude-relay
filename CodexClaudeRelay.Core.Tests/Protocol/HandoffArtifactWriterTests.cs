using CodexClaudeRelay.Core.Models;
using CodexClaudeRelay.Core.Protocol;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Protocol;

public class HandoffArtifactWriterTests
{
    private static TurnPacket PeerHandoffPacket(string from, int turn = 1) => new()
    {
        From = from,
        Turn = turn,
        SessionId = "2026-04-18-sample",
        Handoff = new TurnHandoff
        {
            CloseoutKind = CloseoutKind.PeerHandoff,
            NextTask = "Verify PacketIO round-trip for both roles",
            Context = "G1 scaffolding landed; fixture packets attached",
            Questions = new[] { "Any YAML field ordering requirement?" },
            ReadyForPeerVerification = true,
        },
    };

    [Fact]
    public void Codex_to_claude_renders_CLAUDE_contract_and_recipient()
    {
        var md = HandoffArtifactWriter.Render(PeerHandoffPacket(AgentRole.Codex));

        Assert.Contains("Read PROJECT-RULES.md first. Then read CLAUDE.md and DIALOGUE-PROTOCOL.md.", md);
        Assert.Contains("From: codex → To: claude-code", md);
        Assert.Contains("Session: Document/dialogue/state.json", md);
        Assert.Contains("Previous turn: Document/dialogue/sessions/2026-04-18-sample/turn-1.yaml", md);
        Assert.Contains(HandoffArtifactWriter.MandatoryTail, md);
    }

    [Fact]
    public void Claude_to_codex_flips_contract_to_AGENTS_and_recipient()
    {
        var md = HandoffArtifactWriter.Render(PeerHandoffPacket(AgentRole.Claude));

        Assert.Contains("Read PROJECT-RULES.md first. Then read AGENTS.md and DIALOGUE-PROTOCOL.md.", md);
        Assert.Contains("From: claude-code → To: codex", md);
        Assert.Contains(HandoffArtifactWriter.MandatoryTail, md);
    }

    [Fact]
    public void Symmetric_roles_produce_structurally_identical_artifacts_aside_from_role_fields()
    {
        var codexMd = HandoffArtifactWriter.Render(PeerHandoffPacket(AgentRole.Codex));
        var claudeMd = HandoffArtifactWriter.Render(PeerHandoffPacket(AgentRole.Claude));

        // Same section headers, same line count — no hidden asymmetry.
        Assert.Equal(codexMd.Count(c => c == '\n'), claudeMd.Count(c => c == '\n'));
        Assert.Contains("## Next task", codexMd);
        Assert.Contains("## Next task", claudeMd);
        Assert.Contains("## Context", codexMd);
        Assert.Contains("## Context", claudeMd);
        Assert.Contains("## Summary", codexMd);
        Assert.Contains("## Summary", claudeMd);
    }

    [Fact]
    public void Final_no_handoff_closeout_is_rejected()
    {
        var p = PeerHandoffPacket(AgentRole.Codex) with
        {
            Handoff = new TurnHandoff { CloseoutKind = CloseoutKind.FinalNoHandoff },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => HandoffArtifactWriter.Render(p));
        Assert.Contains("peer_handoff", ex.Message);
    }

    [Fact]
    public void Recovery_resume_closeout_is_rejected()
    {
        var p = PeerHandoffPacket(AgentRole.Codex) with
        {
            Handoff = new TurnHandoff { CloseoutKind = CloseoutKind.RecoveryResume },
        };
        Assert.Throws<InvalidOperationException>(() => HandoffArtifactWriter.Render(p));
    }

    [Fact]
    public void Missing_next_task_is_rejected()
    {
        var p = PeerHandoffPacket(AgentRole.Codex) with
        {
            Handoff = new TurnHandoff
            {
                CloseoutKind = CloseoutKind.PeerHandoff,
                NextTask = "",
                Context = "c",
                ReadyForPeerVerification = true,
            },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => HandoffArtifactWriter.Render(p));
        Assert.Contains("next_task", ex.Message);
    }

    [Fact]
    public void Missing_ready_flag_is_rejected()
    {
        var p = PeerHandoffPacket(AgentRole.Codex) with
        {
            Handoff = new TurnHandoff
            {
                CloseoutKind = CloseoutKind.PeerHandoff,
                NextTask = "t",
                Context = "c",
                ReadyForPeerVerification = false,
            },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => HandoffArtifactWriter.Render(p));
        Assert.Contains("ready_for_peer_verification", ex.Message);
    }

    [Fact]
    public void Unknown_role_is_rejected()
    {
        var p = PeerHandoffPacket("gpt-5");
        Assert.Throws<InvalidOperationException>(() => HandoffArtifactWriter.Render(p));
    }

    [Fact]
    public void Suggest_done_surfaces_in_summary()
    {
        var p = PeerHandoffPacket(AgentRole.Codex) with
        {
            Handoff = new TurnHandoff
            {
                CloseoutKind = CloseoutKind.PeerHandoff,
                NextTask = "t",
                Context = "c",
                ReadyForPeerVerification = true,
                SuggestDone = true,
                DoneReason = "both peers agree",
            },
        };
        var md = HandoffArtifactWriter.Render(p);
        Assert.Contains("suggest_done: true (both peers agree)", md);
    }
}
