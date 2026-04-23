param(
  [string]$CardGameRoot = 'D:\Unity\card game',
  [string]$SessionId = ''
)

$ErrorActionPreference = 'Stop'

$appDataDir = Join-Path $env:LOCALAPPDATA 'CodexClaudeRelayMvp'
$autoLogsDir = Join-Path $appDataDir 'auto-logs'
$liveSignalPath = Join-Path $CardGameRoot '.autopilot\generated\relay-live-signal.json'
$currentStatusPath = Join-Path $autoLogsDir 'current-status.txt'

function Read-JsonFile {
  param([string]$Path)

  if (-not (Test-Path -LiteralPath $Path)) {
    return $null
  }

  try {
    return Get-Content -Raw -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json
  } catch {
    return $null
  }
}

function Get-EventLogPathFromStatus {
  param([string]$Path)

  if (-not (Test-Path -LiteralPath $Path)) {
    return ''
  }

  foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
    if ($line -match '^[A-Z]:\\.+\.jsonl$') {
      return $line.Trim()
    }
  }

  return ''
}

function Count-Matches {
  param(
    [string]$Path,
    [string]$Pattern,
    [switch]$EventOnly
  )

  if (-not (Test-Path -LiteralPath $Path)) {
    return 0
  }

  $count = 0
  foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
    if ($EventOnly -and ($line -match 'aggregated_output' -or $line -match 'command_execution')) {
      continue
    }

    if ($line -match $Pattern) {
      $count++
    }
  }

  return $count
}

function Test-Pattern {
  param(
    [string]$Path,
    [string]$Pattern
  )

  return (Count-Matches -Path $Path -Pattern $Pattern) -gt 0
}

$liveSignal = Read-JsonFile -Path $liveSignalPath
if (-not $SessionId -and $liveSignal -and $liveSignal.session_id) {
  $SessionId = [string]$liveSignal.session_id
}

$eventLogPath = ''
if ($SessionId) {
  $candidate = Join-Path $appDataDir "logs\$SessionId.jsonl"
  if (Test-Path -LiteralPath $candidate) {
    $eventLogPath = $candidate
  }
}

if (-not $eventLogPath) {
  $eventLogPath = Get-EventLogPathFromStatus -Path $currentStatusPath
}

$mcpCallsObserved = Count-Matches -Path $eventLogPath -Pattern 'mcp__' -EventOnly
$unityMcpCallsObserved = Count-Matches -Path $eventLogPath -Pattern 'mcp__unityMCP__' -EventOnly
$unityMcpMentionedInPrompt = Test-Pattern -Path $eventLogPath -Pattern 'Unity MCP|mcp__unityMCP__|MCP-FOR-UNITY'
$compactSignalOnly = $true

$summary = [ordered]@{
  generated_at = (Get-Date).ToString('o')
  card_game_root = $CardGameRoot
  session_id = $SessionId
  relay_signal_status = if ($liveSignal) { [string]$liveSignal.status } else { '' }
  event_log_path = $eventLogPath
  mcp_calls_observed = $mcpCallsObserved
  unity_mcp_calls_observed = $unityMcpCallsObserved
  unity_mcp_mentioned_in_prompt_or_log = $unityMcpMentionedInPrompt
  compact_signal_only = $compactSignalOnly
  evidence_marker = if ($unityMcpCallsObserved -gt 0) {
    "[RELAY_EVIDENCE] session=$SessionId unity_mcp=observed calls=$unityMcpCallsObserved"
  } elseif ($mcpCallsObserved -gt 0) {
    "[RELAY_EVIDENCE] session=$SessionId unity_mcp=not-observed mcp_calls=$mcpCallsObserved"
  } else {
    "[RELAY_EVIDENCE] session=$SessionId unity_mcp=not-observed mcp_calls=0"
  }
}

$summary | ConvertTo-Json -Depth 5
