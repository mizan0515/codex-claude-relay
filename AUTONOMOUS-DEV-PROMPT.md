# Autonomous Development Prompt — SUPERSEDED

**Status: deprecated as of 2026-04-18.**

The autopilot for this prototype now lives under `.autopilot/`. To advance one iteration,
paste `.autopilot/RUN.txt` into Claude Code instead of the old copy-paste prompt.

- Canonical prompt: `.autopilot/PROMPT.md`
- One-shot paste: `.autopilot/RUN.txt`
- State: `.autopilot/STATE.md`
- MVP scorecard: `.autopilot/MVP-GATES.md`
- Cleanup worklist: `.autopilot/CLEANUP-CANDIDATES.md`
- Install git hooks: `powershell -File .autopilot/project.ps1 install-hooks`

The previous 1,400-line `AUTONOMOUS-DEV-PROMPT-COPY.txt` has been retained in git history
(see the commit before the autopilot install) for archaeology, but is no longer the
active operating prompt. The `.autopilot/` layout mechanically enforces IMMUTABLE
sections of the charter via pre-commit + commit-msg hooks, which the old single-file
prompt did not. See `.autopilot/PROMPT.md` IMMUTABLE blocks for the enforced contract.

Rationale: the loop needs stateful sentinel files (MVP-GATES, CLEANUP-LOG, PITFALLS,
METRICS) that are hard to maintain inside a single paste-blob. Moving to `.autopilot/`
also lets the hooks reject accidental charter modifications that the loop itself might
otherwise make during self-evolution.
