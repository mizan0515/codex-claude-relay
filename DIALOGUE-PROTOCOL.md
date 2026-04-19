# Dialogue Protocol (Relay Root)

This repository is the **DAD-v2 peer-symmetric relay** runtime contract.
It is **not** a template source repo, and it does not host `en/` / `ko/`
variants.

## Purpose

- automate peer-symmetric turn brokering between Codex and Claude Code
- maintain Desktop-level context across CLI turns via the 8 primitives
  in `.autopilot/PROMPT.md` IMMUTABLE:mission (rolling summary,
  carry-forward, SHA dedup, rotation, recovery_resume, YAML packets,
  handoff artifacts, JSONL audit log)
- enable tool use and MCP on either peer without role asymmetry

## Turn Flow

1. Read `PROJECT-RULES.md`.
2. Read `AGENTS.md` or `CLAUDE.md` depending on which peer you are.
3. Read `.autopilot/STATE.md` and any named `Document/DAD/*.md` specs
   your task touches.
4. Inspect the affected C# code under `CodexClaudeRelay.Core/` and
   `CodexClaudeRelay.Desktop/` (+ tests in `CodexClaudeRelay.Core.Tests/`).
5. Apply peer-symmetric changes; language-only differences must be
   explicit.
6. Run `dotnet build`, `dotnet test`, and relevant validators
   (`tools/Validate-Dad-Packet.ps1` for session artifacts).

## Protocol Reference

Detailed DAD-v2 runtime rules live in:

- `Document/DAD/PACKET-SCHEMA.md`
- `Document/DAD/STATE-AND-LIFECYCLE.md`
- `Document/DAD/BACKLOG-AND-ADMISSION.md`
- `Document/DAD/VALIDATION-AND-PROMPTS.md`
- `Document/DAD/VALIDATOR-FIRST-DISCOVERY-DEFERRED.md`

The external repo `D:\dad-v2-system-template` contains `en/` and `ko/`
language variants of the DAD-v2 spec. Those are a **read-only reference**
for protocol wording; this relay does not read from or write to that repo
at runtime.

## What Stays Out Of This Repo

- no `en/` / `ko/` runtime variant scaffolding (that belongs in the
  template repo)
- no role-asymmetric broker logic (Codex-only or Claude-only branches)
- no IMMUTABLE-block modifications without the required trailer
