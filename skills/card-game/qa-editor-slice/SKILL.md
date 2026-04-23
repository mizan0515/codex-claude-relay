# qa-editor-slice

Use when the admitted task bucket is `qa-editor` or the task explicitly targets
`Tools/QA/*`, editor smoke coverage, or Unity verification flows.

Procedure:
1. Read only the scoped files named in `recommended_read_path`.
2. Prefer Unity MCP before shell inspection for live editor checks.
3. Use one narrow QA path, not broad exploratory menu crawling.
4. Capture compact evidence before handoff.

Preferred deterministic checks:
- `scripts/card-game/Get-CardGameManagerSignal.ps1`
- `scripts/card-game/Get-CardGameRelaySignal.ps1`
- `scripts/card-game/Get-CardGameRelayEvidence.ps1`

Acceptance criteria:
- `unity_mcp_observed` must be true for tasks that require live Unity validation.
- final handoff must name the specific Unity MCP tools used.
- no full JSONL tail unless compact evidence is missing or contradictory.
