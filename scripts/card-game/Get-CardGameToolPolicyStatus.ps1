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
  $OutputPath = Join-Path $repoRoot 'profiles\card-game\generated-tool-policy-status.json'
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
if (-not $SessionId -and $manifest -and $manifest.session_id) {
  $SessionId = [string]$manifest.session_id
}

$forbiddenTools = if ($manifest -and $manifest.guidance) { @($manifest.guidance.forbidden_tools) } else { @() }
$relayEvidenceScriptPath = Join-Path $PSScriptRoot 'Get-CardGameRelayEvidence.ps1'
$relayEvidence = $null
if (Test-Path -LiteralPath $relayEvidenceScriptPath) {
  try {
    $relayEvidence = & powershell -ExecutionPolicy Bypass -File $relayEvidenceScriptPath -CardGameRoot $CardGameRoot -SessionId $SessionId | ConvertFrom-Json
  } catch {
    $relayEvidence = $null
  }
}

$eventLogPath = if ($relayEvidence -and $relayEvidence.event_log_path) { [string]$relayEvidence.event_log_path } else { '' }
$violations = New-Object System.Collections.Generic.List[string]
$unsupported = New-Object System.Collections.Generic.List[string]
$webCallsObserved = 0

if ($eventLogPath -and (Test-Path -LiteralPath $eventLogPath)) {
  foreach ($line in Get-Content -LiteralPath $eventLogPath -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) {
      continue
    }

    $entry = $null
    try {
      $entry = $line | ConvertFrom-Json
    } catch {
      continue
    }

    $rawLine = [string]$line
    $payloadText = [string]$entry.Payload
    $eventType = [string]$entry.EventType

    if ($payloadText -match '"type":"web_search"' -or $eventType -like 'web.*' -or [string]$entry.Message -match 'web-facing tool') {
      $webCallsObserved++
    }
  }
}

foreach ($tool in $forbiddenTools) {
  $name = [string]$tool
  switch ($name) {
    'web' {
      if ($webCallsObserved -gt 0) {
        $violations.Add($name)
      }
    }
    'full-log-tail' {
      $unsupported.Add($name)
    }
    default {
      $unsupported.Add($name)
    }
  }
}

$status = if ($forbiddenTools.Count -eq 0) {
  'none'
} elseif ($violations.Count -gt 0) {
  'violation'
} elseif ($unsupported.Count -gt 0) {
  'partial'
} else {
  'ok'
}

$summary = [pscustomobject]@{
  generated_at = (Get-Date).ToString('o')
  card_game_root = $CardGameRoot
  manifest_path = $ManifestPath
  session_id = $SessionId
  status = $status
  forbidden_tools = @($forbiddenTools)
  violations = @($violations.ToArray())
  unsupported_forbidden_tools = @($unsupported.ToArray())
  web_calls_observed = $webCallsObserved
  event_log_path = $eventLogPath
  summary_marker = if ($status -eq 'violation') {
    '[TOOL_POLICY] violation ' + ((@($violations.ToArray()) | Select-Object -Unique) -join ',')
  } elseif ($status -eq 'ok' -or $status -eq 'none') {
    '[TOOL_POLICY] ok'
  } else {
    '[TOOL_POLICY] partial unsupported=' + ((@($unsupported.ToArray()) | Select-Object -Unique) -join ',')
  }
}

$json = $summary | ConvertTo-Json -Depth 8
$outputDir = Split-Path -Parent $OutputPath
if ($outputDir) {
  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}
Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8

Write-Output $json
