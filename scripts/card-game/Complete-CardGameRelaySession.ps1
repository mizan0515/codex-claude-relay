param(
  [string]$CardGameRoot = 'D:\Unity\card game',
  [string]$ManifestPath = '',
  [string]$SessionId = '',
  [switch]$SkipHeuristics,
  [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot '..\..')
$dialogueRoot = Join-Path $CardGameRoot 'Document\dialogue'
$learningPath = Join-Path $dialogueRoot 'learning-memory\session-outcomes.jsonl'
$heuristicsJsonPath = Join-Path $repoRoot 'docs\card-game-integration\learning-memory\heuristics.json'
$heuristicsMarkdownPath = Join-Path $repoRoot 'docs\card-game-integration\learning-memory\heuristics.md'
$completionMarkerPath = Join-Path $repoRoot 'profiles\card-game\generated-last-completed-session.json'
$profileRoot = Join-Path $repoRoot 'profiles\card-game'
$archivedManifestDir = Join-Path $profileRoot 'archive'
$defaultManifestPath = Join-Path $profileRoot 'generated-admission.json'
$defaultPromptPath = Join-Path $profileRoot 'generated-session-prompt.md'
$defaultPlanPath = Join-Path $profileRoot 'generated-session-plan.md'
$requiredEvidenceScriptPath = Join-Path $scriptRoot 'Get-CardGameRequiredEvidenceStatus.ps1'
$toolPolicyScriptPath = Join-Path $scriptRoot 'Get-CardGameToolPolicyStatus.ps1'

if (-not $ManifestPath) {
  $ManifestPath = Join-Path $repoRoot 'profiles\card-game\generated-admission.json'
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
  throw "Manifest not found: $ManifestPath"
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath -Encoding UTF8 | ConvertFrom-Json
if (-not $SessionId) {
  $SessionId = [string]$manifest.session_id
}

if (-not $SessionId) {
  throw 'Session id is empty.'
}

$sessionStatePath = Join-Path $dialogueRoot "sessions\$SessionId\state.json"
if (-not (Test-Path -LiteralPath $sessionStatePath)) {
  throw "Session state not found: $sessionStatePath"
}

$sessionState = Get-Content -Raw -LiteralPath $sessionStatePath -Encoding UTF8 | ConvertFrom-Json
$terminalStatuses = @('converged', 'abandoned', 'stopped', 'failed')
if ($terminalStatuses -notcontains ([string]$sessionState.session_status)) {
  throw "Session $SessionId is not terminal yet: $($sessionState.session_status)"
}

if (Test-Path -LiteralPath $requiredEvidenceScriptPath) {
  $requiredEvidenceStatus = & powershell -ExecutionPolicy Bypass -File $requiredEvidenceScriptPath `
    -CardGameRoot $CardGameRoot `
    -ManifestPath $ManifestPath `
    -SessionId $SessionId | ConvertFrom-Json

  if (-not $requiredEvidenceStatus.all_required_evidence_present) {
    $missing = @($requiredEvidenceStatus.missing_evidence) -join ', '
    $unsupported = @($requiredEvidenceStatus.unsupported_evidence) -join ', '
    $detail = if ($missing) {
      "missing evidence: $missing"
    } elseif ($unsupported) {
      "unsupported evidence: $unsupported"
    } else {
      'required evidence check failed'
    }

    throw "Session $SessionId cannot be completed because the skill contract is not satisfied ($detail)."
  }
}

if (Test-Path -LiteralPath $toolPolicyScriptPath) {
  $toolPolicyStatus = & powershell -ExecutionPolicy Bypass -File $toolPolicyScriptPath `
    -CardGameRoot $CardGameRoot `
    -ManifestPath $ManifestPath `
    -SessionId $SessionId | ConvertFrom-Json

  if ([string]$toolPolicyStatus.status -eq 'violation') {
    $violations = @($toolPolicyStatus.violations) -join ', '
    throw "Session $SessionId cannot be completed because forbidden tool policy was violated ($violations)."
  }
}

if (-not $SkipHeuristics -and (Test-Path -LiteralPath $learningPath)) {
  powershell -ExecutionPolicy Bypass -File (Join-Path $scriptRoot 'Update-CardGameHeuristics.ps1') `
    -LearningPath $learningPath `
    -OutputJsonPath $heuristicsJsonPath `
    -OutputMarkdownPath $heuristicsMarkdownPath
}

$writeBackArgs = @(
  '-ExecutionPolicy', 'Bypass',
  '-File', (Join-Path $scriptRoot 'Write-CardGameAutopilotResult.ps1'),
  '-CardGameRoot', $CardGameRoot,
  '-ManifestPath', $ManifestPath,
  '-SessionStatePath', $sessionStatePath
)

if (Test-Path -LiteralPath $learningPath) {
  $writeBackArgs += @('-LearningRecordPath', $learningPath)
}

if ($WhatIf) {
  $writeBackArgs += '-WhatIf'
}

& powershell @writeBackArgs

$completionMarkerDir = Split-Path -Parent $completionMarkerPath
if ($completionMarkerDir) {
  New-Item -ItemType Directory -Force -Path $completionMarkerDir | Out-Null
}

$completionMarker = [ordered]@{
  completed_at = (Get-Date).ToString('o')
  session_id = $SessionId
  session_status = [string]$sessionState.session_status
  manifest_path = $ManifestPath
}
$completionMarker | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $completionMarkerPath -Encoding UTF8

if (Test-Path -LiteralPath $ManifestPath) {
  New-Item -ItemType Directory -Force -Path $archivedManifestDir | Out-Null
  $archivedManifestPath = Join-Path $archivedManifestDir ("manifest-{0}.json" -f $SessionId)
  Copy-Item -LiteralPath $ManifestPath -Destination $archivedManifestPath -Force

  if ([System.StringComparer]::OrdinalIgnoreCase.Equals((Resolve-Path $ManifestPath).Path, (Resolve-Path $defaultManifestPath).Path)) {
    Remove-Item -LiteralPath $ManifestPath -Force
    if (Test-Path -LiteralPath $defaultPromptPath) {
      Remove-Item -LiteralPath $defaultPromptPath -Force
    }
    if (Test-Path -LiteralPath $defaultPlanPath) {
      Remove-Item -LiteralPath $defaultPlanPath -Force
    }
  }
}

Write-Host "Completed relay session integration: $SessionId"
