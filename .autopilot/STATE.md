# .autopilot/STATE.md — live state, keep ≤60 lines. Loaded every iteration.

root: .
base: main
iteration: 35
status: halted
active_task: null
# Last completed: Iter 35 auto-halt — triggered by `3 consecutive upkeep → auto-halt` rule in PROMPT.md Idle-upkeep mode block (iters 33/34/35 all upkeep with empty autopilot-reachable queue). No HALT file written (the IMMUTABLE halt block lists 5 auto-halt conditions that write HALT; 3-consecutive-upkeep is NOT one of them — it's a mutable mode-local rule, so we emit `status: halted` + LAST_HALT_NOTE without creating HALT). Operator resume path: (a) merge `[mvp-gates-scorecard-flip]` PR (flips G1/G4/G6/G7→[x], G5/G8→[~] per mvp-gates-evidence.md); (b) drop `OPERATOR: focus on <task>` sticky into STATE.md; (c) open access to UIA harness / real remote to unblock G5/G8 live halves; (d) re-fire /loop — next boot iter 36 will re-enter mode dispatch cleanly. Smoke last green on main: 34/34 PASS, Release 0/0.
# active_task schema:
#   slug: <kebab-case>
#   plan: [bullet, bullet]
#   started_iter: N
#   branch: dev/autopilot-<slug>-<YYYYMMDD>
#   gate: G<n>  (reference to .autopilot/MVP-GATES.md)

plan_docs:
  - DEV-PROGRESS.md
  - IMPROVEMENT-PLAN.md
  - README.md

spec_docs:
  - PHASE-A-SPEC.md
  - INTERACTIVE-REBUILD-PLAN.md
  - TESTING-CHECKLIST.md
  - CLAUDE-APPROVAL-DECISION.md
  - capability-matrix.md

reference_docs:
  - KNOWN-PITFALLS.md
  - phase-f-survey.md
  - git-workflow.md

# Auto-merge refuses if the PR diff touches any of these:
protected_paths:
  - CodexClaudeRelay.sln
  - .autopilot/PROMPT.md
  - .autopilot/MVP-GATES.md
  - .autopilot/CLEANUP-LOG.md
  - .autopilot/CLEANUP-CANDIDATES.md
  - .autopilot/project.ps1
  - .autopilot/project.sh
  - .githooks/
  # Dormant defensive guards (origin-template carryover — files do not exist here
  # today, but if they ever reappear the guard activates automatically. Do NOT
  # prune these just because the paths are missing from this repo).
  - en/
  - ko/
  - tools/
  - PROJECT-RULES.md
  - CLAUDE.md
  - AGENTS.md
  - DIALOGUE-PROTOCOL.md

open_questions:
  - "Does the rotation-with-carry-forward exercise (F-live-1) meaningfully preserve Goal/Completed/Pending across the split, or do the fields end up empty in practice?"
  - "Can the approval UI communicate destructive-tier risk without operator reading the full command, or is the risk summary still too abstract?"
  - "Is Claude's audit-only stance still the right call given the handoff-parser maturity curve, or should we revisit per CLAUDE-APPROVAL-DECISION.md?"

# MVP gates: canonical checklist at .autopilot/MVP-GATES.md. STATE tracks only tally.
mvp_gates: 0/8
mvp_last_advanced_iter: 0

# OPERATOR overrides — any line starting with `OPERATOR:` wins over PROMPT.md.
#   OPERATOR: halt
#   OPERATOR: halt evolution
#   OPERATOR: focus on <task>
#   OPERATOR: allow evolution <rationale>
#   OPERATOR: allow push to main for <task>    (single use, delete after use)
#   OPERATOR: require human review             (disables auto-merge globally)
#   OPERATOR: run cleanup                      (promotes Cleanup mode this iter; one-shot)
#   OPERATOR: mvp-rescope <rationale>          (allow gate count to decrease; one-shot)
#   OPERATOR: post-mvp <direction>             (unblocks after mvp-complete halt; sticky)
#   OPERATOR: approve cleanup <candidate-date> (authorizes Phase B bulk-delete; one-shot)
#
# One-shot overrides are CONSUMED by the loop at the end of the iteration that
# acts on them — the exit step deletes the line from this file. Sticky
# overrides persist until the operator removes them manually.
