param(
  [string]$ManifestPath = '',
  [string]$OutputJsonPath = '',
  [string]$OutputMarkdownPath = '',
  [int]$MaxCharsPerSkill = 1600
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $ManifestPath) {
  $ManifestPath = Join-Path $repoRoot 'profiles\card-game\generated-admission.json'
}
if (-not $OutputJsonPath) {
  $OutputJsonPath = Join-Path $repoRoot 'profiles\card-game\generated-skill-bundle.json'
}
if (-not $OutputMarkdownPath) {
  $OutputMarkdownPath = Join-Path $repoRoot 'profiles\card-game\generated-skill-bundle.md'
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
  throw "Manifest not found: $ManifestPath"
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath -Encoding UTF8 | ConvertFrom-Json
$skillRefs = @($manifest.guidance.required_skill_paths)
$bundleItems = New-Object System.Collections.Generic.List[object]

foreach ($skillRef in $skillRefs) {
  $name = [string]$skillRef.name
  $path = [string]$skillRef.path
  if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) {
    $bundleItems.Add([pscustomobject]@{
      name = $name
      path = $path
      exists = $false
      excerpt = ''
      truncated = $false
    })
    continue
  }

  $raw = (Get-Content -Raw -LiteralPath $path -Encoding UTF8).Trim()
  $excerpt = $raw
  $truncated = $false
  if ($excerpt.Length -gt $MaxCharsPerSkill) {
    $excerpt = $excerpt.Substring(0, $MaxCharsPerSkill).TrimEnd() + "`n[truncated]"
    $truncated = $true
  }

  $bundleItems.Add([pscustomobject]@{
    name = $name
    path = $path
    exists = $true
    excerpt = $excerpt
    truncated = $truncated
  })
}

$bundleArray = @($bundleItems.ToArray())
$summary = [pscustomobject]@{
  generated_at = (Get-Date).ToString('o')
  manifest_path = $ManifestPath
  session_id = [string]$manifest.session_id
  skill_count = $bundleArray.Count
  skills = $bundleArray
  summary_marker = if ($bundleArray.Count -gt 0) {
    '[SKILL_BUNDLE] ready count=' + $bundleArray.Count
  } else {
    '[SKILL_BUNDLE] empty'
  }
}

$jsonDir = Split-Path -Parent $OutputJsonPath
$mdDir = Split-Path -Parent $OutputMarkdownPath
if ($jsonDir) { New-Item -ItemType Directory -Force -Path $jsonDir | Out-Null }
if ($mdDir) { New-Item -ItemType Directory -Force -Path $mdDir | Out-Null }

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Card Game Skill Bundle')
$lines.Add('')
$lines.Add('Generated at: ' + $summary.generated_at)
$lines.Add('Session id: ' + $summary.session_id)
$lines.Add('Marker: ' + $summary.summary_marker)
$lines.Add('')
foreach ($item in $bundleArray) {
  $lines.Add('## ' + [string]$item.name)
  $lines.Add('')
  $resolvedPath = if ($item.exists) { [string]$item.path } else { 'missing' }
  $lines.Add('- Path: ' + $resolvedPath)
  if ($item.exists) {
    $lines.Add('')
    foreach ($line in ([string]$item.excerpt -split "`r?`n")) {
      $lines.Add($line)
    }
  }
  $lines.Add('')
}

[string]::Join("`r`n", $lines) | Set-Content -LiteralPath $OutputMarkdownPath -Encoding UTF8
Write-Output ($summary | ConvertTo-Json -Depth 8)
