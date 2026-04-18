# Phase F live-1 / live-2 — headless harness plan

**Status:** draft — operator-review PR pending. Companion doc to
`phase-f-live-1-plan.md` (the interactive WPF-driven execution plan).

## Motivation

F-live-1 (rotation with carry-forward) and F-live-2 (interactive repair
lands as `handoff.accepted`) are the two outstanding MVP gates (G5/G8).
Both currently require a WPF UI Automation harness the repo does not
have. Adding `.NET-Core-side` xUnit coverage gives equivalent semantic
evidence for Core/Protocol/Broker behavior without touching the WPF
shell, letting autopilot run the gates on every build instead of gating
on a scarce operator-driven live session.

## Scope of the operator-review PR

One commit, one PR. Touches `CodexClaudeRelay.sln` (protected_paths) so
autopilot cannot self-land — this doc exists so operator review is a
one-pass skim.

Diff footprint:

- `CodexClaudeRelay.sln` — add the new project to the solution
  (2 lines in `Project(...)` table + 2 lines in `GlobalSection` config
  map). Exact inserts listed in review comment when PR lands.
- `CodexClaudeRelay.Core.Tests/CodexClaudeRelay.Core.Tests.csproj` —
  new xUnit test project, targets `net10.0`, `ProjectReference` →
  `CodexClaudeRelay.Core`, packages: `Microsoft.NET.Test.Sdk`, `xunit`,
  `xunit.runner.visualstudio`.
- `CodexClaudeRelay.Core.Tests/Protocol/HandoffParserTests.cs` — round-trip
  tests for `completed` + `constraints` arrays (F-impl-3c evidence).
- `CodexClaudeRelay.Core.Tests/Broker/CarryForwardTests.cs` — three cases
  exercising carry-forward (see inventory below).
- `CodexClaudeRelay.Core.Tests/Protocol/RelayPromptBuilderTests.cs` — repair
  prompt rule coverage (E-spec-2 / F-live-2 evidence).

No changes to production code. No changes to `CodexClaudeRelay.Desktop`.

## xUnit test inventory (first cut)

| Test | Gate | What it proves |
|---|---|---|
| `HandoffParser_RoundTrip_CompletedConstraints` | G7 | `completed` + `constraints` arrays survive parse→serialize→parse. |
| `CompleteHandoffAsync_PopulatesGoalPendingCompletedConstraints` | G7 | Broker wires all four sections from envelope to `RelaySessionState`. |
| `RotateSessionAsync_WritesSummaryFile` | G6 | Rolling-summary file lands under `%LocalAppData%/CodexClaudeRelayMvp/summaries/` with non-zero bytes and `summary.generated` emitted. |
| `BuildTurnPrompt_AfterRotation_IncludesCarryForwardBlock` | G7 / G8 | After `CompleteHandoffAsync` + `RotateSessionAsync`, next turn prompt contains the four-section `## Carry-forward` block; `LastHandoffHash` matches. |
| `BuildInteractiveRepairPrompt_RequiresReadyTrue` | G5 | Repair prompt names `ready=true` and the Do-NOT echo rule (string assertions against the prompt text). |

Tests exercise real types (no mocks beyond the filesystem surface for
`summary.generated`). Filesystem tests use `Path.GetTempPath()` +
per-test subfolder cleanup.

## Non-goals

- No WPF / UI Automation coverage — that stays in `phase-f-live-1-plan.md`.
- No Codex CLI / Claude CLI invocation — those tests require network
  and a real Claude Code session; out of scope for the headless
  harness.
- No CI wiring — operator decides whether to run these on every push
  or on-demand via `dotnet test CodexClaudeRelay.Core.Tests`.

## MVP-GATES evidence mapping

Once this PR lands **and** tests run green once:

- G1 (build) — already trivially green; `dotnet test` adds runtime
  signal on top of compile.
- G5 (interactive repair lands as accepted) — string assertions on the
  repair prompt cover the prompt-engineering contract; a future live
  F-live-2 run confirms runtime adherence.
- G6 (summary write) — `RotateSessionAsync_WritesSummaryFile` is direct
  evidence.
- G7 (carry-forward populated) — parser + broker + prompt-builder tests
  together prove all four sections round-trip.
- G8 (rotation live) — semantic coverage (not a live WPF run); operator
  decides whether this satisfies the gate or whether UI Automation is
  still required.

## Risks / review questions for operator

1. Is semantic xUnit coverage acceptable for G8 or does the gate
   require observed UI-driven behavior?
2. Should the Tests project target `net10.0` (matches Core/Desktop) or
   pin to `net8.0` for faster local-machine test runs?
3. OK to land tests + sln edits in a single operator-review PR, or
   prefer splitting (sln/csproj scaffold first, tests as follow-up)?
