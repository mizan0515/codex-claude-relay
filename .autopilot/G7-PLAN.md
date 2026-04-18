# G7 PLAN — Consensus convergence closeout

Gate definition (MVP-GATES.md):
> When both peers mark `handoff.ready_for_peer_verification: true` AND
> `handoff.suggest_done: true` in consecutive turns with matching
> `checkpoint_results`, the broker seals the session as `converged` in
> state.json and links it in `Document/dialogue/backlog.json`.
>
> Evidence: state.json showing `session_status: converged` + backlog entry
> with `closed_by_session_id` filled.

## What already exists (verified 2026-04-18 iter 38)

- `TurnPacket.Handoff.ReadyForPeerVerification` + `SuggestDone` fields
  parsed + YAML-serialized (see `TurnPacketYamlPersister` line 42-43).
- `HandoffArtifactWriter` rejects peer_handoff without
  `ready_for_peer_verification: true` (line 106-108).
- `HandoffArtifactWriter` emits "suggest_done: true (both peers agree)"
  markdown line when flag set (line 84-87).
- `RelayBroker.CompleteHandoffAsync` pipeline writes turn packet YAML +
  handoff artifact + state.json on every accepted handoff (G4 iter27-29).
- `TurnPacket.Handoff.CheckpointResults` array (G3 iter24-25).

## What is missing for G7 `[~]` → `[x]`

1. **`RelaySessionStatus.Converged` enum value** — currently
   `{ Idle, Active, AwaitingApproval, Paused, Stopped, Failed }`. Add
   `Converged`. Desktop XAML status-visual map needs a new entry
   (green-seal hue).
2. **`ConvergenceDetector` pure function** — takes previous turn's
   `Handoff` + current turn's `Handoff`, returns `ConvergenceDecision`
   `{ IsConverged, Reason, MatchingCheckpoints }`. Rules:
   - both turns have `ready_for_peer_verification: true`,
   - both turns have `suggest_done: true`,
   - the two `from` fields are opposite peers (codex vs claude),
   - `checkpoint_results` lists match by `{checkpoint_id, status}`
     (order-insensitive, same set of ids with same statuses).
3. **Broker wiring** — on accepted handoff, broker consults
   `ConvergenceDetector` against the prior turn's stored handoff; if
   converged, set `State.Status = Converged`, emit
   `session.converged` event (JSONL), skip the usual
   `ActiveAgent` swap (terminal turn), persist state.json.
4. **Backlog link** — `BacklogClosureWriter` locates or creates
   `Document/dialogue/backlog.json`, finds the entry whose
   `session_id` matches (or appends one if missing), and fills
   `closed_by_session_id` + `closed_at` + `converged_turn`. Pure
   render fn + atomic WriteAsync, same pattern as
   `TurnPacketYamlPersister`.
5. **xunit evidence facts** — ConvergenceDetectorTests (4-5 facts),
   BacklogClosureWriterTests (3 facts), plus 1 broker-integration
   fact asserting `session.converged` event + `State.Status =
   Converged` on canned converging pair.

## iter execution order (target 3-4 iters)

- **iter 38** (this iter): G7-PLAN.md authored, no code change.
- **iter 39**: `ConvergenceDetector` pure fn + xunit 4-5 facts.
  `RelaySessionStatus.Converged` enum addition (Desktop XAML stays on
  Active hue until later — additive only). ≤120 LOC. Auto-merge path.
- **iter 40**: `BacklogClosureWriter` pure render + WriteAsync + 3
  facts. ≤140 LOC. Auto-merge path.
- **iter 41**: Broker wiring — `CompleteHandoffAsync` invokes
  detector, on converge sets status + emits event + calls backlog
  writer. xunit 2-3 integration facts (canned prior handoff in
  State + synthesize current handoff matching). G7 `[ ]` → `[~]`
  flip in same PR once wiring proven. ≤180 LOC. Auto-merge path.
- **iter 42**: end-to-end smoke with fake `IRelayAdapter` pair that
  returns convergent handoffs across two turns → session flips to
  Converged, backlog.json has closure entry. `[~]` → `[x]` flip.
  ≤150 LOC. Auto-merge path.

Alternative: bundle iter 41-42 into one larger PR if fake-adapter
harness is already built for G4/G5/G6 `[x]` follow-up.

## Peer-symmetry check

Convergence detector MUST be order-agnostic: a
(codex→claude, claude→codex) convergent pair must produce the
same `IsConverged=true` as (claude→codex, codex→claude). The rule is
"opposite peers on two consecutive turns, both flags set, matching
checkpoints" — not "codex goes first" (IMMUTABLE mission rule 3).

## Risk flags

- **Terminal turn has no next agent** — `ActiveAgent` swap must be
  skipped when converged. Missing this will produce a "phantom"
  turn 3 request. Guard via explicit `if (decision.IsConverged)
  return;` after state persist, before agent-swap.
- **Backlog.json may not yet exist** — writer must handle missing
  file (create with `[]` scaffold) and missing session entry
  (append rather than fail).
- **Checkpoint match semantics** — what if one side reports a
  checkpoint the other omitted? Current plan: require exact set
  equality by id. Could later relax to "intersection non-empty and
  all matching entries PASS", but stricter is safer for first pass.
- **False-positive convergence under recovery_resume** —
  recovery_resume turns MAY carry `suggest_done: true` artifactually.
  Convergence detector MUST check `closeout_kind == peer_handoff` on
  both turns, not just the flags. Reject convergence for
  recovery_resume pairs.

## Follow-up once `[x]`

- **Approval-UI surface** — Desktop XAML status visual for Converged
  state (green seal, "세션 합의 종료" label).
- **Operator notification** — emit `operator.session_closed` event
  with summary link for Korean dashboard surfacing.
- **Multi-session chain** — if backlog.json has a next-session
  pointer, broker could auto-queue the successor. Deferred as
  post-MVP.
