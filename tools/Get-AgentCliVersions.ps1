#!/usr/bin/env pwsh
# helpers/Get-AgentCliVersions.ps1 — probe the host-agent CLI versions and
# compare against npm latest. Returns JSON with {agent, current, latest,
# drift_status} per agent. Meant to be wrapped by downstream doctor scripts
# and captured into METRICS (Tier 2 reserved: claude_cli, codex_cli).
#
# See PITFALLS.md 2026-04-24 — "Agent CLI version drift is silent until a
# contract bug surfaces" — for the motivating lesson.
#
# Emits stable JSON on stdout. Non-zero exit ONLY on catastrophic failure
# (neither CLI found); drift itself returns status='outdated' | 'ahead' and
# exit 0 so downstream doctor decides whether to halt.

[CmdletBinding()]
param(
    [string[]]$Agents = @('claude', 'codex'),
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'

function Get-CommandVersion {
    param(
        [Parameter(Mandatory)][string]$Command
    )
    try {
        $raw = & $Command --version 2>$null | Select-Object -First 1
        if (-not $raw) { return '' }
        $match = [regex]::Match([string]$raw, '\d+\.\d+\.\d+')
        if ($match.Success) { return $match.Value }
        return ([string]$raw).Trim()
    }
    catch {
        return ''
    }
}

function Get-NpmLatestVersion {
    param(
        [Parameter(Mandatory)][string]$PackageName
    )
    try {
        $out = npm view $PackageName version 2>$null | Select-Object -First 1
        return ([string]$out).Trim()
    }
    catch {
        return ''
    }
}

function Compare-SemVer {
    param(
        [string]$Current,
        [string]$Latest
    )
    if ([string]::IsNullOrWhiteSpace($Current) -or [string]::IsNullOrWhiteSpace($Latest)) {
        return 'unknown'
    }
    try {
        $c = [version]$Current
        $l = [version]$Latest
        if ($c -lt $l) { return 'outdated' }
        if ($c -eq $l) { return 'current' }
        return 'ahead'
    }
    catch {
        return 'unknown'
    }
}

# Peer-symmetric registry: both agents must be probed with the same strategy.
# Adding a new agent identity = one new row here; no agent-specific branches.
$agentRegistry = @{
    claude = '@anthropic-ai/claude-code'
    codex  = '@openai/codex'
}

$results = @()
$anyFound = $false
foreach ($agent in $Agents) {
    if (-not $agentRegistry.ContainsKey($agent)) {
        $results += [ordered]@{
            agent = $agent
            current = ''
            latest = ''
            drift_status = 'unknown'
            error = "unregistered agent (add to agentRegistry in Get-AgentCliVersions.ps1)"
        }
        continue
    }
    $current = Get-CommandVersion -Command $agent
    if ($current) { $anyFound = $true }
    $latest = Get-NpmLatestVersion -PackageName $agentRegistry[$agent]
    $results += [ordered]@{
        agent = $agent
        current = $current
        latest = $latest
        drift_status = Compare-SemVer -Current $current -Latest $latest
    }
}

$payload = [ordered]@{
    probed_at = [DateTime]::UtcNow.ToString('o')
    agents = $results
}

if ($AsJson) {
    $payload | ConvertTo-Json -Depth 4 -Compress
}
else {
    $payload | ConvertTo-Json -Depth 4
}

if (-not $anyFound) {
    exit 1
}
exit 0
