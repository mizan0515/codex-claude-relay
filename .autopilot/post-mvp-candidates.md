# .autopilot/post-mvp-candidates.md — directions beyond MVP

This doc exists so the autopilot's `mvp-complete` auto-halt does not
strand the operator without options. When all 8 gates flip to `[x]`
and the loop writes `status: mvp-complete, awaiting operator
direction`, the operator unblocks with `OPERATOR: post-mvp <direction>`
in STATE.md. The candidates below are the menu.

**Context at authoring (iter 32, 2026-04-18):** MVP gates 0/8 on the
scorecard; G1/G4/G6/G7 substantively satisfied in code per
`mvp-gates-evidence.md` (PR #20) and awaiting the operator-only
`[mvp-gates-scorecard-flip]` PR. G5/G8 semantic halves covered by
smoke (PRs #17/#22); live halves still blocked on UIA harness.

This is **preparation, not commitment**. Each candidate is a
starting sketch — scope will firm up when operator picks one.

---

## Candidate A — Claude tier elevation

**Status today:** `CLAUDE-APPROVAL-DECISION.md` keeps the Claude
adapter audit-only. Dialogue/approval requests from Claude are
captured but never promoted to equal-tier dispatch.

**Why revisit post-MVP:** the MVP's approval-first guarantees were
validated against the Codex path; the audit-only stance for Claude
was a risk-reduction move, not a terminal design choice. Once the
approval loop has lived under real load, the trust graph is the
next place value compounds.

**Scope sketch:**
- Author a tier-elevation spec: trust graph, consent UI copy,
  rollback path if elevated-Claude produces a policy gap.
- Add a second approval lane in the broker (today's pipeline
  assumes Codex as the actor).
- Mirror the destructive-tier QA (`drive-destructive-qa.ps1`)
  against the Claude adapter.

**Risk:** blast radius. Elevation without parity on the dialogue
protocol (if `DIALOGUE-PROTOCOL.md` ever lands) can produce audit
gaps that are hard to recover from.

**Unblock:** operator directive + explicit blast-radius carve-out
in PROMPT.md (outside IMMUTABLE).

---

## Candidate B — Multi-session brokering

**Status today:** `RelayBroker` owns a single `RelaySessionState`
instance. Rotation swaps the segment; it does not fan out across
parallel sessions.

**Why revisit post-MVP:** approval-first is the MVP's point; once
solid, scaling to multiple concurrent sessions (one per project, or
one per risk tier) is the natural next step — especially for
operators who drive Codex across more than one repo.

**Scope sketch:**
- Introduce a `RelaySessionRegistry` that maps `SessionId → state`.
- Session-scoped summary files under
  `%LocalAppData%\CodexClaudeRelayMvp\summaries\<sessionId>\`.
- Per-session approval panes in the WPF shell (or CLI equivalent).
- Cross-session audit export (ties to Candidate D).

**Risk:** contention and fairness — the single-session design today
sidesteps a lot of WPF dispatcher work that would surface with
multiple live sessions.

**Unblock:** operator directive + evidence demand (e.g. a concrete
workflow where single-session costs them).

---

## Candidate C — Policy DSL / declarative risk-tier config

**Status today:** risk tiers and approval decisions are hardcoded
in Core. Operator tunes behavior via source edits.

**Why revisit post-MVP:** the value proposition of an approval-
first relay is weakened if the risk policy is opaque. A declarative
policy file (JSON / YAML / Rego-like) lets the operator audit and
tune without rebuilding.

**Scope sketch:**
- Define a minimal risk-policy schema (tier → action → allow/deny
  / approval-required).
- `RelayBroker` loads policy at startup with a guarded reload path.
- Ship a default policy matching today's hardcoded behavior so the
  change is a no-op by default.
- Policy-diff export for audit.

**Risk:** expressivity creep. A DSL that accretes features without
a governance process becomes the new hardcoded mess.

**Unblock:** operator directive + an incident or evidence demand
where today's hardcoding caused misalignment.

---

## Candidate D — Audit-trail export + replay

**Status today:** `logs/*.jsonl` + `auto-logs/` capture everything
needed to reconstruct a session, but there is no curated export
format and no replay tool.

**Why revisit post-MVP:** incident response is the quiet axis of
value. Being able to hand a redacted bundle to a reviewer — or
replay a bad session locally to reproduce a bug — is a property
large-org adoption will demand.

**Scope sketch:**
- Define `session-export.v1` — redacted JSONL bundle with schema.
- CLI / menu action that packages a session into the bundle.
- Replay harness that rehydrates `RelaySessionState` from the
  bundle and steps through turns deterministically.
- Hooks for future test generation (replay → regression smoke).

**Risk:** redaction correctness. Getting this wrong leaks
secrets; getting it too aggressive makes the bundle useless.

**Unblock:** operator directive + a concrete first consumer
(e.g. internal incident review).

---

## Candidate E — Cross-platform / CLI mode

**Status today:** `CodexClaudeRelay.Desktop` is WPF-bound; Windows
only. Core is .NET 10 and would move.

**Why revisit post-MVP:** the relay's value is in the broker, not
the chrome. A headless CLI mode + an Avalonia (or similar) shell
broadens where this lives.

**Scope sketch:**
- Extract the broker service layer from anything Desktop-coupled
  (approvals are already events; make the host pluggable).
- Thin CLI host with an approval prompt on stdin/stdout.
- Evaluate Avalonia/Uno for a same-shape UI on macOS/Linux.

**Risk:** divergence. Two shells become three surfaces to keep in
sync; Windows-only UX cues (UI Automation, dialogue protocol) do
not transfer 1:1.

**Unblock:** operator directive + a concrete non-Windows user.

---

## How to select

One post-mvp direction per `OPERATOR: post-mvp <direction>` line.
The autopilot will treat it as a sticky override and promote it to
the Active priority. Example:

```
OPERATOR: post-mvp policy-dsl — declarative risk-tier config, start with schema + default policy parity
```

Re-scoping a direction mid-flight: replace the sticky line. The
loop picks up the new text on next boot.
