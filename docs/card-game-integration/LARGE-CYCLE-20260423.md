# Large Cycle 2026-04-23

## Scope

- Run one real card-game autopilot to relay cycle through the non-developer `Easy Start` path.
- Use compact signal artifacts only.
- Check whether relay peers actually used Unity MCP.

## Execution

- Card game root: `D:\Unity\card game`
- Task slug: `companion-depth-first-slice`
- Entry path: `scripts/gui-smoke/run-gui-easy-operator.ps1`
- Screenshot proof:
  - `scripts/gui-smoke/out-easy-operator/20260423-112709-after-launch.png`
  - `scripts/gui-smoke/out-easy-operator/20260423-112710-after-config.png`
  - `scripts/gui-smoke/out-easy-operator/20260423-113311-after-easy-start.png`

## Observed result

- Initial stale session was detected and replaced.
- The relay entered `relay_active`.
- Replacement session id became `companion-depth-first-slice-20260423-112716`.
- After six minutes, the relay had not reached a terminal session.
- Compact signal later normalized to:
  - `overall_status=relay_dead`
  - `reason=relay_process_missing`
  - `suggested_desktop_action=prepare_fresh_session`

## Problems found

1. The real bounded cycle did not finish within the GUI harness timeout, so the operator proof stopped while the relay was still active.
2. The system could tell that the relay became stale, but it did not produce a compact MCP-usage summary for the session.
3. The latest relay prompt did not explicitly push the peer toward Unity MCP verification, so actual Unity MCP usage was not observable in this run.

## Improvements added

1. Added `scripts/card-game/Get-CardGameRelayEvidence.ps1` so the operator can check compact evidence instead of tailing the JSONL log.
2. `Write-CardGameAutopilotResult.ps1` now records observed MCP calls and observed Unity MCP calls from the session event log instead of hard-coding `mcp_calls = 0`.
3. `profiles/card-game/prompt-prefix.md` now tells peers to prefer Unity MCP when configured and to say explicitly whether Unity MCP was used.
4. `scripts/gui-smoke/run-gui-easy-operator.ps1` now reports early desktop-process exit and timeout state more clearly.

## Unity MCP conclusion for this cycle

- Unity Editor MCP telemetry was available.
- No Unity MCP call was observed in the latest relay session evidence.
- Current conclusion: Unity MCP was available, but this relay cycle did not provide evidence that the peers actually used it.
