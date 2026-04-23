# Skillify Apply 2026-04-23

## Why this was added

- `agent-skills` emphasizes lifecycle-aligned skills, explicit entry points, and repeatable quality gates.
- Garry Tan's `skillify` framing emphasizes converting each recurring agent failure into a durable asset:
  - skill instructions
  - deterministic scripts
  - tests/evals
  - resolver/discoverability
  - end-to-end smoke
- the main failure in this relay was not "missing prompts". it was:
  - missing discoverability for local skills
  - missing hard completion gate for required evidence
  - too much reliance on operator memory

## What changed in this repo

1. skill contracts are now bucket-driven in `profiles/card-game/skill-contracts.json`.
2. local repo skills now live under `skills/card-game/`.
3. admission manifest now carries:
   - `required_skills`
   - `required_skill_paths`
   - `required_evidence`
   - `forbidden_tools`
   - `forbidden_tool_policy`
   - `enforcement_notes`
4. generated prompt, session plan, and runbook surface those contracts.
5. loop status and completion write-back now hard-stop when required evidence is missing.
6. loop status and completion write-back now hard-stop on detectable forbidden-tool violations such as `web` for Unity-local slices.

## Current contract posture

- `required_evidence`:
  - enforced by `Get-CardGameLoopStatus.ps1`
  - enforced again by `Complete-CardGameRelaySession.ps1`
- `required_skills`:
  - now discoverable through explicit `required_skill_paths`
  - activated into the generated session prompt through the compact skill bundle
- `forbidden_tools`:
  - documented and surfaced in artifacts
  - `web` is now post-hoc enforced from relay event logs
  - `full-log-tail` is still policy-only because it is not yet directly observable from relay event logs
- `governance`:
  - resolver, required evidence, and tool policy are aggregated into one compact governance artifact
  - Desktop now shows the plain-language governance reason in `Managed Autopilot Status` and `Easy Status`
  - governance now points to one blocker artifact path so the operator can open a compact proof/policy file instead of guessing
  - manager signal now refreshes governance before it renders, so stale governance state cannot be mistaken for the current relay mismatch state
  - governance now carries the exact missing key, so the operator can see `unity_mcp_observed` or another failed field without opening raw JSON first
  - governance now carries one recommended next action sentence so the operator is not forced to translate raw field names into remediation steps
  - governance now carries a structured action id and button label so Desktop can swap from a generic blocker button to the current remediation surface
  - some action ids now execute a lightweight runtime fix path, such as refreshing compact signals, instead of only opening artifacts
  - the `unity_mcp_observed` blocker now upgrades to a bounded `Retry Unity Verification` action instead of a passive artifact-open step
  - Desktop now records the last remediation result so operators can tell whether the retry path improved the blocker state
  - the remediation result is now mirrored to a compact artifact so it survives Desktop restarts
  - governance now re-reads the remediation artifact, so repeated retries can be downgraded into an evidence-reading path instead of looping blindly
  - remediation retry count is now part of governance, so repeated Unity verification failures can escalate to a human-attention action
  - retry budget is now exposed in the operator surface so the remaining automatic attempts are visible before a human escalation happens
  - retry-budget exhaustion now changes the Desktop operator surface itself, not just the text, so the escalation is visible at a glance
  - remediation artifacts now mirror the same retry-budget summary as manager signal, so the compact surfaces stay consistent across restarts
  - ops dashboard now mirrors the same remediation and retry-budget sentinels, so compact observability is aligned across every operator surface
  - the no-remediation state is now explicit (`[REMEDIATION] no remediation yet`) so operators can distinguish missing activity from a broken dashboard
  - Desktop now refreshes both manager signal and ops dashboard whenever it writes a remediation artifact, so the compact surfaces stay in lockstep
  - Desktop now also re-reads manager signal immediately after writing remediation artifacts, so the UI text does not lag one timer tick behind the compact files

## Remaining gaps

1. turn the remaining `forbidden_tools` checks into deterministic policies where possible.
2. add eval fixtures that assert:
   - `qa-editor` cannot complete without `unity_mcp_observed`
   - bounded-cycle prompts surface the expected skill paths
3. add stronger Desktop affordances after opening the blocker artifact, such as copying the remediation sentence or key directly from the UI.

## Research notes

- skills are useful only when they are discoverable and evaluated, not just stored.
- deterministic workflows beat free-form agent loops for repeated operational steps.
- compact artifacts are a better evidence boundary than full transcript inspection in Unity-heavy projects.

## Sources

- [addyosmani/agent-skills](https://github.com/addyosmani/agent-skills)
- [GeekNews summary of Garry Tan's skillify thread](https://news.hada.io/topic?id=28777)
- [Anthropic: Building effective agents](https://www.anthropic.com/engineering/building-effective-agents)
- [Anthropic: Effective context engineering for AI agents](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents)
- [Anthropic: Demystifying evals for AI agents](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents)
