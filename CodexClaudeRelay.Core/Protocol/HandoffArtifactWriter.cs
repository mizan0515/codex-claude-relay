using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Protocol;

/// <summary>
/// Generates the 7-part DAD-v2 peer handoff prompt (the artifact saved to
/// <c>handoff.prompt_artifact</c>) from a <see cref="TurnPacket"/>.
///
/// Required parts per <c>Document/DAD/VALIDATION-AND-PROMPTS.md</c>
/// "Peer Prompt Rules":
///   1. Read PROJECT-RULES + agent contract + DIALOGUE-PROTOCOL
///   2. Session: Document/dialogue/state.json
///   3. Previous turn: Document/dialogue/sessions/{sid}/turn-{N}.yaml
///   4. concrete handoff.next_task + handoff.context
///   5. relay-friendly summary
///   6. mandatory tail block
///   7. (this artifact — implicit; recursive)
///
/// This writer is pure: <c>Render(packet)</c> returns the markdown body.
/// Persistence (<c>File.WriteAllText</c>) lives in the broker, not here.
///
/// Peer-symmetric: the recipient is computed via
/// <see cref="AgentRole.Peer(string)"/> from the packet's <c>From</c> field —
/// no Codex/Claude branching.
/// </summary>
public static class HandoffArtifactWriter
{
    public const string MandatoryTail =
        "---\n" +
        "If you find any gap or improvement, fix it directly and report the diff.\n" +
        "If nothing needs to change, state explicitly: \"No change needed, PASS\".\n" +
        "Important: do not evaluate leniently. Never say \"looks good\". Cite concrete evidence and examples.\n";

    /// <summary>
    /// Render the peer handoff artifact for the given packet.
    /// Throws <see cref="InvalidOperationException"/> if the packet's handoff
    /// is not a valid <c>peer_handoff</c> closeout — only that closeout kind
    /// produces an artifact (final_no_handoff and recovery_resume leave
    /// <c>prompt_artifact</c> empty per PACKET-SCHEMA.md).
    /// </summary>
    public static string Render(TurnPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        EnsurePeerHandoff(packet);

        var recipient = AgentRole.Peer(packet.From);
        var contractFile = ContractFileFor(recipient);
        var prevTurnPath = $"Document/dialogue/sessions/{packet.SessionId}/turn-{packet.Turn}.yaml";

        return string.Concat(
            // Part 1: required reading
            $"Read PROJECT-RULES.md first. Then read {contractFile} and DIALOGUE-PROTOCOL.md. ",
            "If that file points to Document/DAD references, read the needed files there too.\n\n",
            // Part 2: session pointer
            "Session: Document/dialogue/state.json\n\n",
            // Part 3: previous-turn pointer
            $"Previous turn: {prevTurnPath}\n\n",
            // Part 4: concrete next_task + context
            "## Next task\n",
            $"{packet.Handoff.NextTask}\n\n",
            "## Context\n",
            $"{packet.Handoff.Context}\n\n",
            // Part 5: relay-friendly summary (questions + done flags surface here)
            RenderSummary(packet),
            // Part 6: mandatory tail
            "\n",
            MandatoryTail);
    }

    private static string RenderSummary(TurnPacket packet)
    {
        var lines = new List<string> { "## Summary" };
        lines.Add($"- From: {packet.From} → To: {AgentRole.Peer(packet.From)}");
        lines.Add($"- Turn: {packet.Turn} (session {packet.SessionId})");
        lines.Add($"- Closeout: {packet.Handoff.CloseoutKind}");
        if (packet.Handoff.Questions.Count > 0)
        {
            lines.Add("- Open questions:");
            foreach (var q in packet.Handoff.Questions)
            {
                lines.Add($"  - {q}");
            }
        }
        if (packet.Handoff.SuggestDone)
        {
            lines.Add($"- suggest_done: true ({packet.Handoff.DoneReason})");
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string ContractFileFor(string recipientRole) => recipientRole switch
    {
        AgentRole.Codex => "AGENTS.md",
        AgentRole.Claude => "CLAUDE.md",
        _ => throw new ArgumentOutOfRangeException(nameof(recipientRole), recipientRole, "no contract file mapping"),
    };

    private static void EnsurePeerHandoff(TurnPacket packet)
    {
        var h = packet.Handoff;
        if (!string.Equals(h.CloseoutKind, CloseoutKind.PeerHandoff, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"HandoffArtifactWriter only renders peer_handoff closeouts; got '{h.CloseoutKind}'.");
        }
        if (!h.ReadyForPeerVerification)
        {
            throw new InvalidOperationException("peer_handoff requires ready_for_peer_verification: true.");
        }
        if (string.IsNullOrWhiteSpace(h.NextTask))
        {
            throw new InvalidOperationException("peer_handoff requires non-empty handoff.next_task.");
        }
        if (string.IsNullOrWhiteSpace(h.Context))
        {
            throw new InvalidOperationException("peer_handoff requires non-empty handoff.context.");
        }
        if (string.IsNullOrWhiteSpace(packet.SessionId))
        {
            throw new InvalidOperationException("peer_handoff requires non-empty session_id.");
        }
        if (!AgentRole.IsValid(packet.From))
        {
            throw new InvalidOperationException($"unknown agent role in packet.from: '{packet.From}'.");
        }
    }
}
