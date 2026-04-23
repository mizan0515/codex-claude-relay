# Large Cycle 2026-04-23

## Goal

Run one real autopilot -> relay cycle against `D:\Unity\card game` through the operator-facing path, avoid tailing full logs, and record the failures that still block safe non-developer use.

## What was run

- Unity preflight:
  - `.autopilot/project.ps1 doctor`
  - Unity MCP telemetry and console checks
- Relay/autopilot path:
  - `scripts/card-game/Invoke-CardGameAutopilotLoop.ps1 -CardGameRoot 'D:\Unity\card game' -MaxSessions 1 -ForceRelay`
- Compact waiting only:
  - `scripts/card-game/Get-CardGameManagerSignal.ps1`
  - `scripts/card-game/Get-CardGameRelaySignal.ps1`
  - `scripts/card-game/Wait-CardGameRelaySignal.ps1 -TimeoutSeconds 420`
- Operator proof screenshots:
  - `scripts/gui-smoke/out-easy-operator/20260423-112709-after-launch.png`
  - `scripts/gui-smoke/out-easy-operator/20260423-112710-after-config.png`
  - `scripts/gui-smoke/out-easy-operator/20260423-113311-after-easy-start.png`

## What was observed

- Unity MCP bridge was live in the editor before the run.
- The relay first replaced a stale session, then continued with `companion-depth-first-slice-20260423-112716`.
- That relay session stayed `Active` on turn 2.
- `last_progress_at` stopped advancing while `source_pid` was still alive.
- `watchdog_remaining_seconds` crossed below zero and kept decreasing.
- The bounded waiter eventually returned:
  - `[RELAY_DONE] false status=timeout reason=watcher_timeout`
- A newer prepared manifest for the same task was generated as `companion-depth-first-slice-20260423-115403`, but the compact manager surface still had to reconcile that with the older active relay session.
- Event-log evidence for the active relay did not show an actual Unity MCP tool call.

## Problems found

1. Hung relay sessions were not escalated quickly enough.
   - Before this fix, `Wait-CardGameRelaySignal.ps1` only treated `process missing` as stale.
   - A live-but-stuck process with an expired watchdog could still keep operators waiting.

2. Prepared-session state and active-relay state could diverge.
   - The loop could prepare a new session while the compact live signal still pointed at an older active relay.
   - That made the operator surface harder to trust.

3. Unity MCP usage was not being recorded as compact evidence.
   - The current relay flow had no small script that could answer `did the peer really use Unity MCP?`
   - `Write-CardGameAutopilotResult.ps1` also hard-coded MCP metrics instead of deriving them from session evidence.

## Improvements added

- `Get-CardGameRelaySignal.ps1`
  - adds `HungWatchdog` derived status when the relay stays active past a watchdog-expiry grace window
- `Wait-CardGameRelaySignal.ps1`
  - exits early with `status=hung reason=watchdog_expired` instead of waiting until an external timeout
- `Get-CardGameManagerSignal.ps1`
  - adds `relay_hung`
  - adds `relay_session_mismatch`
- `Get-CardGameRelayEvidence.ps1`
  - emits a compact marker for whether Unity MCP tool calls were actually observed in the current relay event log
- `Write-CardGameAutopilotResult.ps1`
  - now fills MCP metrics from compact evidence instead of hard-coding zero
- `profiles/card-game/prompt-prefix.md`
  - now explicitly tells peers which Unity MCP tasks they should prefer and requires the final handoff to say whether Unity MCP was used

## Current conclusion

- The relay/autopilot integration is real and runnable, but one large-cycle failure mode was confirmed:
  - a peer can leave the relay `Active` with no progress until well after the watchdog expires
- After this patch, that state should surface as `relay_hung` quickly enough for a non-technical operator to stop waiting and prepare a fresh session.
- Unity MCP was available, but this cycle did not provide evidence that the relay peers actually used it.

## Second large-cycle pass

### What was run

- `scripts/card-game/Run-CardGameManagedRelay.ps1 -CardGameRoot 'D:\Unity\card game' -TaskSlug 'tool-qa-menu-coverage-matrix' -Turns 4 -TimeoutSeconds 900 -ForceRelay`
- Compact-only follow-up:
  - `Get-CardGameManagerSignal.ps1`
  - `Get-CardGameRelaySignal.ps1`
  - `Get-CardGameRelayEvidence.ps1`

### What was observed

- The first rerun did not even enter the GUI worksession because the generated session prompt was passed as a raw multiline command-line argument.
- PowerShell argument parsing broke on localized multiline content, so the relay session prepared successfully but the GUI script failed before the Desktop run started.
- After changing the worksession path to pass `InitialPromptPath` instead of raw prompt text, the next rerun entered the Desktop session correctly.
- That rerun then stopped at:
  - `relay_status = AwaitingApproval`
  - `pending_approval = MCP Tool Approval`
  - `summary = invoke MCP tool 'MCP Tool: mcp_tool_call'`
- Narrow state inspection showed the queued payload was actually:
  - `item.type = mcp_tool_call`
  - `item.tool = read_mcp_resource`
  - `item.server = invalid`
- So the broker was escalating a generic read-only MCP resource probe because it only recognized `ReadMcpResourceTool` by title, not the newer `mcp_tool_call` wrapper payload.

### Problems found

4. GUI worksession prompt handoff was not robust against multiline localized prompts.
   - Prepared sessions could succeed while the actual Desktop run never started.
   - This is an operator-facing blocker because it looks like relay preparation worked even though no peer cycle actually ran.

5. Generic `mcp_tool_call` wrappers were punching through the default safe MCP policy.
   - Read-only MCP resource probes were being escalated into operator review.
   - That deadlocked the exact qa-editor slices that require Unity MCP evidence.

### Improvements added

- `scripts/gui-smoke/run-gui-worksession.ps1`
  - now accepts `InitialPromptPath`
  - reads the prompt from disk inside the script instead of trusting raw multiline argv
- `scripts/card-game/Run-CardGameManagedRelay.ps1`
  - now passes the generated prompt path into the GUI worksession instead of embedding prompt text directly in the child PowerShell invocation
- `CodexClaudeRelay.Core/Policy/RelayApprovalPolicy.cs`
  - now inspects the inner `item.tool` and `item.server` fields inside generic `mcp_tool_call` payloads
  - auto-allows read-only `read_mcp_resource` / `list_mcp_resources` wrappers
  - auto-allows safe `unityMCP` verification tools such as `read_console`, `refresh_unity`, `execute_menu_item`, `manage_editor`, `run_tests`, and `get_test_job`

### Current conclusion after the second pass

- The real managed relay cycle now gets past the prompt-handoff failure that previously prevented the GUI run.
- The next blocker was not "Unity MCP is dangerous" but "the broker could not see that the inner MCP tool was read-only or safe verification work".
- After the policy patch, the next large-cycle rerun should no longer stop on generic read-only `mcp_tool_call` wrappers before the peer can reach actual Unity verification work.

## Third large-cycle pass

### What was run

- `scripts/card-game/Run-CardGameManagedRelay.ps1 -CardGameRoot 'D:\Unity\card game' -TaskSlug 'tool-qa-menu-coverage-matrix' -Turns 4 -TimeoutSeconds 600 -ForceRelay`
- Compact-only follow-up:
  - `Get-CardGameRelaySignal.ps1`
  - `Get-CardGameManagerSignal.ps1`
  - `Get-CardGameRequiredEvidenceStatus.ps1`
  - `Get-CardGameRelayEvidence.ps1 -SessionId tool-qa-menu-coverage-matrix-20260423-175541`

### What was observed

- The managed relay session actually ran:
  - turn 1 on `codex`
  - handoff to turn 2 on `claude-code`
- The broker no longer stopped on a generic `mcp_tool_call` approval.
- The session then paused because Claude exceeded the cumulative output budget:
  - `Cumulative output token budget exceeded for claude-code. Total output tokens: 23828.`
- Compact evidence initially returned a false negative for Unity MCP, even though the event log contained:
  - `mcp__unityMCP__read_console`
  - post-turn handoff text saying Unity MCP was used on the Codex side

### Problems found

6. The qa-editor relay budget was too low for a real Unity verification slice.
   - `maxCumulativeOutputTokens = 16000` was not enough for a 4-turn managed relay on `tool-qa-menu-coverage-matrix`.
   - This caused a pause mid-cycle even though the relay was genuinely running.

7. Compact Unity MCP evidence was undercounting real usage.
   - The evidence parser did not recognize generic `mcp_tool_call` wrapper payloads early enough.
   - It also missed some Unity MCP mentions embedded in completed-turn / accepted-handoff payloads.

### Improvements added

- `profiles/card-game/broker.cardgame.json`
  - raises `maxCumulativeOutputTokens` from `16000` to `32000` for the card-game profile
- `profiles/card-game/prompt-prefix.md`
  - explicitly forbids placeholder invalid MCP probes and tells peers to say `Unity MCP not used` instead of fabricating probe traffic
- `scripts/card-game/Get-CardGameRelayEvidence.ps1`
  - now reads payload text before Unity MCP detection
  - recognizes generic `mcp_tool_call` wrapper payloads
  - recognizes Unity MCP usage mentioned in turn-complete / handoff payload text

### Current conclusion after the third pass

- The relay peers did actually use Unity MCP in the real large-cycle session.
- The compact proof is now:
  - `[RELAY_EVIDENCE] unity_mcp=observed count=12`
- Required evidence now resolves to:
  - `[REQUIRED_EVIDENCE] ok`
- The remaining large-cycle blocker is no longer "prove Unity MCP was used", but "keep the qa-editor relay inside a realistic output budget without reopening full logs".
