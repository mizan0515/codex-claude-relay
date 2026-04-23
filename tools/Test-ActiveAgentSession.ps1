#!/usr/bin/env pwsh
# helpers/Test-ActiveAgentSession.ps1 — peer-symmetric "am I running
# inside a live agent session?" probe. Returns which agents (if any)
# have tell-tale env vars set in the current process. Intended as the
# safety gate a self-update script consults before running
# `npm install -g @<vendor>/<cli>@latest` — self-updating the CLI
# you're hosted in kills the session.
#
# See PITFALLS.md 2026-04-24 — "Agent self-update mid-session breaks
# packet contracts" / "Peer-tool self-updater must not kill its own
# host" — for the motivating lesson (cardgame-dad-relay #22).
#
# Registry-driven: both agents use the same check (env-var union),
# with agent-specific env-var lists. Adding a new agent = one
# registry row.
#
# Exit codes:
#   0 — no active agent detected (safe to self-update any)
#   5 — at least one active agent detected (caller should refuse
#       self-updating THAT agent; other agents remain safe)
#
# JSON payload includes per-agent active/inactive + which env var
# fired, so callers can decide per-agent.

[CmdletBinding()]
param(
    [string[]]$Agents = @('claude', 'codex'),
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'

# Peer-symmetric registry: env vars that the agent's host shell sets
# when a session is live. Keep agent-agnostic branching — each row is
# just `{agent: [env-var names]}`.
$agentEnvRegistry = @{
    codex = @(
        'CODEX_THREAD_ID',
        'CODEX_SHELL',
        'CODEX_INTERNAL_ORIGINATOR_OVERRIDE'
    )
    claude = @(
        'CLAUDE_CODE_SESSION_ID',
        'CLAUDECODE',
        'CLAUDE_PROJECT_DIR'
    )
}

function Test-AgentActive {
    param([string[]]$EnvVars)
    $hits = @()
    foreach ($name in $EnvVars) {
        $val = [Environment]::GetEnvironmentVariable($name)
        if ($val) { $hits += $name }
    }
    return $hits
}

$results = @()
$anyActive = $false
foreach ($agent in $Agents) {
    if (-not $agentEnvRegistry.ContainsKey($agent)) {
        $results += [ordered]@{
            agent = $agent
            active = $false
            hits = @()
            error = "unregistered agent (add to agentEnvRegistry in Test-ActiveAgentSession.ps1)"
        }
        continue
    }
    $hits = Test-AgentActive -EnvVars $agentEnvRegistry[$agent]
    $active = ($hits.Count -gt 0)
    if ($active) { $anyActive = $true }
    $results += [ordered]@{
        agent = $agent
        active = $active
        hits = $hits
    }
}

$payload = [ordered]@{
    probed_at = [DateTime]::UtcNow.ToString('o')
    agents = $results
    any_active = $anyActive
}

if ($AsJson) { $payload | ConvertTo-Json -Depth 4 -Compress } else { $payload | ConvertTo-Json -Depth 4 }

if ($anyActive) { exit 5 }
exit 0
