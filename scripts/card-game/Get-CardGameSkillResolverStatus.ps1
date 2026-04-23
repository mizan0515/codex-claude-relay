param(
  [string]$ManifestPath = '',
  [string]$OutputJsonPath = '',
  [string]$OutputMarkdownPath = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $ManifestPath) {
  $ManifestPath = Join-Path $repoRoot 'profiles\card-game\generated-admission.json'
}
if (-not $OutputJsonPath) {
  $OutputJsonPath = Join-Path $repoRoot 'profiles\card-game\generated-skill-resolver.json'
}
if (-not $OutputMarkdownPath) {
  $OutputMarkdownPath = Join-Path $repoRoot 'profiles\card-game\generated-skill-resolver.md'
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
  throw "Manifest not found: $ManifestPath"
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath -Encoding UTF8 | ConvertFrom-Json
$skillRefs = @($manifest.guidance.required_skill_paths)
$missingSkills = New-Object System.Collections.Generic.List[string]
$resolvedSkills = New-Object System.Collections.Generic.List[object]

foreach ($skillRef in $skillRefs) {
  $name = [string]$skillRef.name
  $path = [string]$skillRef.path
  $exists = $false
  if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path -LiteralPath $path)) {
    $exists = $true
  }
  if (-not $exists -and -not [string]::IsNullOrWhiteSpace($name)) {
    $missingSkills.Add($name)
  }

  $resolvedSkills.Add([pscustomobject]@{
    name = $name
    path = $path
    exists = $exists
  })
}

$status = if ($skillRefs.Count -eq 0) {
  'none'
} elseif ($missingSkills.Count -gt 0) {
  'missing'
} else {
  'ok'
}

$requiredSkillsArray = @($manifest.guidance.required_skills | ForEach-Object { [string]$_ })
$resolvedSkillsArray = @($resolvedSkills.ToArray())
$missingSkillsArray = @($missingSkills.ToArray())
$summaryMarker = if ($status -eq 'ok' -or $status -eq 'none') {
  '[SKILL_RESOLVER] ok'
} else {
  '[SKILL_RESOLVER] missing ' + (($missingSkillsArray | Select-Object -Unique) -join ',')
}

$summary = [pscustomobject]@{
  generated_at = (Get-Date).ToString('o')
  manifest_path = $ManifestPath
  session_id = [string]$manifest.session_id
  status = $status
  required_skills = $requiredSkillsArray
  resolved_skills = $resolvedSkillsArray
  missing_skills = $missingSkillsArray
  all_required_skills_present = ($status -eq 'ok' -or $status -eq 'none')
  summary_marker = $summaryMarker
}

$jsonDir = Split-Path -Parent $OutputJsonPath
$mdDir = Split-Path -Parent $OutputMarkdownPath
if ($jsonDir) { New-Item -ItemType Directory -Force -Path $jsonDir | Out-Null }
if ($mdDir) { New-Item -ItemType Directory -Force -Path $mdDir | Out-Null }

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Card Game Skill Resolver')
$lines.Add('')
$lines.Add('Generated at: ' + $summary.generated_at)
$lines.Add('Session id: ' + $summary.session_id)
$lines.Add('Status: ' + $summary.status)
$lines.Add('Marker: ' + $summary.summary_marker)
$lines.Add('')
$lines.Add('## Required Skills')
$lines.Add('')
foreach ($item in @($summary.resolved_skills)) {
  $resolvedPath = if ($item.exists) { [string]$item.path } else { 'missing' }
  $lines.Add('- ' + [string]$item.name + ' => ' + $resolvedPath)
}

[string]::Join("`r`n", $lines) | Set-Content -LiteralPath $OutputMarkdownPath -Encoding UTF8
Write-Output ($summary | ConvertTo-Json -Depth 8)
