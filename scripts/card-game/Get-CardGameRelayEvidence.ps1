param(
  [string]$CardGameRoot = 'D:\Unity\card game',
  [string]$SessionId = '',
  [string]$EventLogPath = '',
  [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'

function Read-JsonFile {
  param([string]$Path)

  if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
    return $null
  }

  return Get-Content -Raw -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json
}

if (-not $EventLogPath) {
  $signalPath = Join-Path $CardGameRoot '.autopilot\generated\relay-live-signal.json'
  $signal = Read-JsonFile -Path $signalPath
  if ($signal -and $signal.event_log_path) {
    $EventLogPath = [string]$signal.event_log_path
  }
}

$resolvedSessionId = $SessionId
$toolEventsObserved = 0
$unityMcpCallsObserved = 0
$unityMcpObserved = $false
$status = 'missing'

if ($EventLogPath -and (Test-Path -LiteralPath $EventLogPath)) {
  $status = 'ok'
  foreach ($line in Get-Content -LiteralPath $EventLogPath -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) {
      continue
    }

    $entry = $null
    try {
      $entry = $line | ConvertFrom-Json
    } catch {
      continue
    }

    if (-not $resolvedSessionId -and $entry.PSObject.Properties.Name -contains 'Payload' -and $entry.Payload) {
      if ([string]$entry.Payload -match 'session_id[:=]"?([^"\s,}]+)') {
        $resolvedSessionId = $Matches[1]
      }
    }

    $eventType = [string]$entry.EventType
    if ($eventType -notin @('tool.invoked', 'tool.completed')) {
      continue
    }

    $payloadText = [string]$entry.Payload
    if ($payloadText -match '"type":"command_execution"') {
      continue
    }

    $toolEventsObserved++
    if ($payloadText -match 'mcp__unityMCP__\.[A-Za-z_]+' -or [string]$entry.Message -match 'mcp__unityMCP__\.[A-Za-z_]+') {
      $unityMcpCallsObserved++
      $unityMcpObserved = $true
    }
  }
} elseif ($EventLogPath) {
  $status = 'event_log_missing'
}

$summary = [ordered]@{
  status = $status
  session_id = $resolvedSessionId
  event_log_path = $EventLogPath
  tool_events_observed = $toolEventsObserved
  unity_mcp_calls_observed = $unityMcpCallsObserved
  unity_mcp_observed = $unityMcpObserved
  summary_marker = if ($unityMcpObserved) {
    "[RELAY_EVIDENCE] unity_mcp=observed count=$unityMcpCallsObserved"
  } else {
    "[RELAY_EVIDENCE] unity_mcp=not-observed count=0"
  }
}

$json = $summary | ConvertTo-Json -Depth 4
if ($OutputPath) {
  $outputDir = Split-Path -Parent $OutputPath
  if ($outputDir) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
  }
  Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8
}

Write-Output $json
