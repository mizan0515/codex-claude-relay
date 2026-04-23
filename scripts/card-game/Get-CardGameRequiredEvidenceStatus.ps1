param(
  [string]$CardGameRoot = 'D:\Unity\card game',
  [string]$ManifestPath = '',
  [string]$SessionId = '',
  [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $ManifestPath) {
  $ManifestPath = Join-Path $repoRoot 'profiles\card-game\generated-admission.json'
}
if (-not $OutputPath) {
  $OutputPath = Join-Path $repoRoot 'profiles\card-game\generated-required-evidence-status.json'
}

function Read-JsonFile {
  param([string]$Path)

  if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
    return $null
  }

  try {
    return Get-Content -Raw -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json
  } catch {
    return $null
  }
}

$manifest = Read-JsonFile -Path $ManifestPath
$requiredEvidence = if ($manifest -and $manifest.guidance) { @($manifest.guidance.required_evidence) } else { @() }
if (-not $SessionId -and $manifest -and $manifest.session_id) {
  $SessionId = [string]$manifest.session_id
}

$relayEvidence = $null
$relayEvidenceScriptPath = Join-Path $PSScriptRoot 'Get-CardGameRelayEvidence.ps1'
if (Test-Path -LiteralPath $relayEvidenceScriptPath) {
  try {
    $relayEvidence = & powershell -ExecutionPolicy Bypass -File $relayEvidenceScriptPath -CardGameRoot $CardGameRoot -SessionId $SessionId | ConvertFrom-Json
  } catch {
    $relayEvidence = $null
  }
}

$generatedRoot = Join-Path $CardGameRoot '.autopilot\generated'
$managerSignalJsonPath = Join-Path $generatedRoot 'relay-manager-signal.json'
$managerSignalTextPath = Join-Path $generatedRoot 'relay-manager-signal.txt'
$relaySignalJsonPath = Join-Path $generatedRoot 'relay-live-signal.json'
$relaySignalTextPath = Join-Path $generatedRoot 'relay-live-signal.txt'

$missingEvidence = New-Object System.Collections.Generic.List[string]
$unsupportedEvidence = New-Object System.Collections.Generic.List[string]

foreach ($item in $requiredEvidence) {
  $name = [string]$item
  if ([string]::IsNullOrWhiteSpace($name)) {
    continue
  }

  switch ($name) {
    'compact_manager_signal' {
      if (-not ((Test-Path -LiteralPath $managerSignalJsonPath) -or (Test-Path -LiteralPath $managerSignalTextPath))) {
        $missingEvidence.Add($name)
      }
    }
    'compact_relay_signal' {
      if (-not ((Test-Path -LiteralPath $relaySignalJsonPath) -or (Test-Path -LiteralPath $relaySignalTextPath))) {
        $missingEvidence.Add($name)
      }
    }
    'unity_mcp_observed' {
      if (-not ($relayEvidence -and $relayEvidence.unity_mcp_observed)) {
        $missingEvidence.Add($name)
      }
    }
    default {
      $unsupportedEvidence.Add($name)
    }
  }
}

$status = if ($requiredEvidence.Count -eq 0) {
  'none'
} elseif ($missingEvidence.Count -gt 0) {
  'missing'
} elseif ($unsupportedEvidence.Count -gt 0) {
  'unsupported'
} else {
  'ok'
}

$summary = [ordered]@{
  generated_at = (Get-Date).ToString('o')
  card_game_root = $CardGameRoot
  manifest_path = $ManifestPath
  session_id = $SessionId
  status = $status
  required_evidence = @($requiredEvidence)
  missing_evidence = @($missingEvidence)
  unsupported_evidence = @($unsupportedEvidence)
  all_required_evidence_present = ($status -eq 'ok' -or $status -eq 'none')
  relay_evidence_marker = if ($relayEvidence -and $relayEvidence.summary_marker) { [string]$relayEvidence.summary_marker } else { '' }
  summary_marker = if ($status -eq 'ok' -or $status -eq 'none') {
    '[REQUIRED_EVIDENCE] ok'
  } elseif ($status -eq 'missing') {
    '[REQUIRED_EVIDENCE] missing ' + (($missingEvidence | Select-Object -Unique) -join ',')
  } else {
    '[REQUIRED_EVIDENCE] unsupported ' + (($unsupportedEvidence | Select-Object -Unique) -join ',')
  }
}

$json = $summary | ConvertTo-Json -Depth 6
$outputDir = Split-Path -Parent $OutputPath
if ($outputDir) {
  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}
Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8

Write-Output $json
