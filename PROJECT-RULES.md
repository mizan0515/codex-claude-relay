# Project Rules

This repository is the **DAD-v2 peer-symmetric relay** — a C# .NET bridge
between Codex and Claude Code agents. It is **not** a template source
repository, and it does not ship `en/` or `ko/` runtime variants.

## Source Of Truth

1. The following root files govern this repo's contract:
   - `README.md` (when present — current repo has none; see `.autopilot/PROMPT.md` instead)
   - `PROJECT-RULES.md` (this file)
   - `AGENTS.md`
   - `CLAUDE.md`
   - `DIALOGUE-PROTOCOL.md`
   - `.autopilot/PROMPT.md` (IMMUTABLE blocks define the charter)
   - `.githooks/pre-commit` + `.githooks/commit-msg` (protect charter & trailers)
2. DAD-v2 protocol reference specs live under `Document/DAD/`.
3. The external reference repo `D:\dad-v2-system-template` (separate project)
   hosts `en/` and `ko/` DAD-v2 variants. That repo is a **read-only
   protocol spec reference**, not a target this relay writes to.

## Peer Reality

- Codex and Claude Code are equal peers. Every adapter, cost advisor, and
  policy must be expressible with interchangeable agent identifiers.
- No role-conditional branches that exist on only one side
  (see `Policy/IAgentCostAdvisor.cs`).
- Shared behavior changes (docs, code, tests, prompts) must preserve
  peer symmetry; language-only differences are the only allowed asymmetry
  and must be called out explicitly.

## Required Maintainer Checks

Before closing a meaningful change:

1. `dotnet build CodexClaudeRelay.sln` — green.
2. `dotnet test CodexClaudeRelay.sln` — green.
3. `powershell -ExecutionPolicy Bypass -File tools/Validate-Dad-Packet.ps1`
   against any session artifacts you produced.
4. `.autopilot/project.ps1 doctor` — green (verifies solution layout +
   hooks installation).

Treat a change as incomplete if it touches contract docs, hooks, prompts,
or IMMUTABLE blocks without the corresponding policy trailer
(`MISSION-AMEND:`, `IMMUTABLE-ADD:`, `cleanup-operator-approved:`).

## Guardrails

- Do **not** resurrect `en/` / `ko/` variant scaffolding into this root —
  that's the template repo's job.
- Do **not** introduce asymmetric role-conditional logic without a peer
  equivalent.
- Do **not** bypass hooks with `--no-verify` outside an explicit operator
  directive recorded in `STATE.md`.
- Keep frequently read root docs thin; move detailed protocol references
  into `Document/DAD/`.
