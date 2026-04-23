#!/usr/bin/env pwsh
# helpers/Test-RepoIdentityDrift.ps1 — verify a repo's declared identity
# (IMMUTABLE:repo-identity block) matches live git state. Catches the
# class of bug where a copied template still declares the upstream
# repo's origin/branch and emits packets under the wrong fingerprint.
#
# See PITFALLS.md 2026-04-24 — "Repo identity belongs on every
# machine-readable output surface" — and TEMPLATE-INTERACTION.md
# (repo_identity schema bullet).
#
# Inputs:
#   -Root       repo root (default: current directory)
#   -Expected   hashtable override {repo_name; remote_origin; branch}
#               typically parsed by caller from the IMMUTABLE block
#   -Strict     exit non-zero on drift (default: warn only, exit 0)
#
# Default caller is a doctor script: soft tripwire unless -Strict.

[CmdletBinding()]
param(
    [string]$Root = (Get-Location).Path,
    [hashtable]$Expected,
    [switch]$Strict,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'

function Get-GitOutput {
    param([string[]]$GitArgs)
    try {
        $out = & git -C $Root @GitArgs 2>$null
        if ($LASTEXITCODE -ne 0) { return '' }
        return ([string]$out).Trim()
    }
    catch {
        return ''
    }
}

function Parse-ImmutableIdentity {
    param([string]$RootPath)
    $candidates = @(
        (Join-Path $RootPath '.autopilot/PROMPT.md'),
        (Join-Path $RootPath '.autopilot/STATE.md'),
        (Join-Path $RootPath 'AGENTS.md'),
        (Join-Path $RootPath 'CLAUDE.md')
    )
    foreach ($file in $candidates) {
        if (-not (Test-Path $file)) { continue }
        $text = Get-Content -LiteralPath $file -Raw -ErrorAction SilentlyContinue
        if (-not $text) { continue }
        $match = [regex]::Match($text, '(?ms)IMMUTABLE:repo-identity.*?(?=IMMUTABLE:|\z)')
        if (-not $match.Success) { continue }
        $block = $match.Value
        $repo = [regex]::Match($block, '(?mi)^\s*repo_name:\s*(?<v>\S+)').Groups['v'].Value
        $origin = [regex]::Match($block, '(?mi)^\s*remote_origin:\s*(?<v>\S+)').Groups['v'].Value
        $branch = [regex]::Match($block, '(?mi)^\s*branch:\s*(?<v>\S+)').Groups['v'].Value
        if ($repo -or $origin -or $branch) {
            return @{
                repo_name = $repo
                remote_origin = $origin
                branch = $branch
                source_file = $file
            }
        }
    }
    return $null
}

if (-not $Expected) {
    $Expected = Parse-ImmutableIdentity -RootPath $Root
}

$liveOrigin = Get-GitOutput -GitArgs @('remote', 'get-url', 'origin')
$liveBranch = Get-GitOutput -GitArgs @('rev-parse', '--abbrev-ref', 'HEAD')
$liveRepoName = if ($liveOrigin) { [System.IO.Path]::GetFileNameWithoutExtension($liveOrigin) } else { '' }

$drift = @()
if ($Expected) {
    if ($Expected.remote_origin -and $liveOrigin -and ($Expected.remote_origin -ne $liveOrigin)) {
        $drift += "remote_origin declared '$($Expected.remote_origin)' but live is '$liveOrigin'"
    }
    if ($Expected.branch -and $liveBranch -and ($Expected.branch -ne $liveBranch)) {
        $drift += "branch declared '$($Expected.branch)' but live is '$liveBranch' (non-fatal if feature branch — caller decides)"
    }
    if ($Expected.repo_name -and $liveRepoName -and ($Expected.repo_name -ne $liveRepoName)) {
        $drift += "repo_name declared '$($Expected.repo_name)' but live origin basename is '$liveRepoName'"
    }
}

$payload = [ordered]@{
    probed_at = [DateTime]::UtcNow.ToString('o')
    root = $Root
    expected = $Expected
    live = [ordered]@{
        remote_origin = $liveOrigin
        branch = $liveBranch
        repo_name = $liveRepoName
    }
    drift = $drift
    drift_status = if ($drift.Count -eq 0) { 'clean' } else { 'drifted' }
}

if ($AsJson) {
    $payload | ConvertTo-Json -Depth 4 -Compress
}
else {
    $payload | ConvertTo-Json -Depth 4
}

if ($drift.Count -gt 0 -and $Strict) {
    exit 1
}
exit 0
