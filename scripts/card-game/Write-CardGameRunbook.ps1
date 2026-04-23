param(
  [string]$ManifestPath,
  [string]$LoopStatusPath = '',
  [string]$ExecutionRoutePath = '',
  [string]$SkillResolverPath = '',
  [string]$DirectPromptPath = '',
  [string]$OutputJsonPath = '',
  [string]$OutputMarkdownPath = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $LoopStatusPath) {
  $LoopStatusPath = Join-Path $repoRoot 'profiles\card-game\generated-loop-status.json'
}
if (-not $ExecutionRoutePath) {
  $ExecutionRoutePath = Join-Path $repoRoot 'profiles\card-game\generated-execution-route.json'
}
if (-not $SkillResolverPath) {
  $SkillResolverPath = Join-Path $repoRoot 'profiles\card-game\generated-skill-resolver.json'
}
$skillBundlePath = Join-Path $repoRoot 'profiles\card-game\generated-skill-bundle.md'
if (-not $DirectPromptPath) {
  $DirectPromptPath = Join-Path $repoRoot 'profiles\card-game\generated-direct-codex-prompt.md'
}
if (-not $OutputJsonPath) {
  $OutputJsonPath = Join-Path $repoRoot 'profiles\card-game\generated-runbook.json'
}
if (-not $OutputMarkdownPath) {
  $OutputMarkdownPath = Join-Path $repoRoot 'profiles\card-game\generated-runbook.md'
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
  throw "Manifest not found: $ManifestPath"
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath -Encoding UTF8 | ConvertFrom-Json
$loopStatus = if (Test-Path -LiteralPath $LoopStatusPath) {
  Get-Content -Raw -LiteralPath $LoopStatusPath -Encoding UTF8 | ConvertFrom-Json
} else {
  $null
}
$executionRoute = if (Test-Path -LiteralPath $ExecutionRoutePath) {
  Get-Content -Raw -LiteralPath $ExecutionRoutePath -Encoding UTF8 | ConvertFrom-Json
} else {
  $null
}
$skillResolver = if (Test-Path -LiteralPath $SkillResolverPath) {
  Get-Content -Raw -LiteralPath $SkillResolverPath -Encoding UTF8 | ConvertFrom-Json
} else {
  $null
}

$runbook = [ordered]@{
  generated_at = (Get-Date).ToString('o')
  session_id = [string]$manifest.session_id
  task_slug = [string]$manifest.task.slug
  bucket = [string]$manifest.task.bucket
  task_summary = [string]$manifest.task.summary
  next_action = if ($loopStatus) { [string]$loopStatus.next_action } else { '' }
  execution_mode = if ($loopStatus) { [string]$loopStatus.execution_mode } else { '' }
  execution_mode_reason = if ($loopStatus) { [string]$loopStatus.execution_mode_reason } else { '' }
  manifest_path = $ManifestPath
  session_prompt_path = Join-Path $repoRoot 'profiles\card-game\generated-session-prompt.md'
  execution_route_path = if (Test-Path -LiteralPath $ExecutionRoutePath) { $ExecutionRoutePath } else { '' }
  skill_resolver_path = if (Test-Path -LiteralPath $SkillResolverPath) { $SkillResolverPath } else { '' }
  skill_bundle_path = if (Test-Path -LiteralPath $skillBundlePath) { $skillBundlePath } else { '' }
  direct_prompt_path = if (Test-Path -LiteralPath $DirectPromptPath) { $DirectPromptPath } else { '' }
  representative_target = if ($executionRoute) { [string]$executionRoute.representative_target } else { '' }
  asmdef_readiness_suggestion = if ($executionRoute) { [string]$executionRoute.asmdef_readiness_suggestion } else { '' }
  next_commands = if ($executionRoute) { @($executionRoute.next_commands) } else { @() }
  recommended_read_path = @($manifest.guidance.recommended_read_path)
  required_skills = @($manifest.guidance.required_skills)
  required_skill_paths = @($manifest.guidance.required_skill_paths)
  required_evidence = @($manifest.guidance.required_evidence)
  forbidden_tools = @($manifest.guidance.forbidden_tools)
  enforcement_notes = @($manifest.guidance.enforcement_notes)
  skill_resolver_status = if ($skillResolver) { [string]$skillResolver.status } else { '' }
  skill_resolver_marker = if ($skillResolver) { [string]$skillResolver.summary_marker } else { '' }
  tool_policy_status = if ($loopStatus) { [string]$loopStatus.tool_policy_status } else { '' }
  tool_policy_marker = if ($loopStatus) { [string]$loopStatus.tool_policy_marker } else { '' }
  tool_policy_violations = if ($loopStatus) { @($loopStatus.tool_policy_violations) } else { @() }
  verification_expectation = [string]$manifest.guidance.verification_expectation
}

$jsonDir = Split-Path -Parent $OutputJsonPath
$mdDir = Split-Path -Parent $OutputMarkdownPath
if ($jsonDir) { New-Item -ItemType Directory -Force -Path $jsonDir | Out-Null }
if ($mdDir) { New-Item -ItemType Directory -Force -Path $mdDir | Out-Null }

$runbook | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Card Game Runbook')
$lines.Add('')
$lines.Add('Generated at: ' + $runbook.generated_at)
$lines.Add('Session id: ' + $runbook.session_id)
$lines.Add('Task slug: ' + $runbook.task_slug)
$lines.Add('Bucket: ' + $runbook.bucket)
$lines.Add('Next action: ' + $runbook.next_action)
$lines.Add('Execution mode: ' + $runbook.execution_mode)
$lines.Add('Execution mode reason: ' + $runbook.execution_mode_reason)
$lines.Add('Representative target: ' + $runbook.representative_target)
$lines.Add('')
$lines.Add('## Artifacts')
$lines.Add('')
$lines.Add('- Manifest: ' + $runbook.manifest_path)
$lines.Add('- Session prompt: ' + $runbook.session_prompt_path)
if ($runbook.execution_route_path) { $lines.Add('- Execution route: ' + $runbook.execution_route_path) }
if ($runbook.skill_resolver_path) { $lines.Add('- Skill resolver: ' + $runbook.skill_resolver_path) }
if ($runbook.skill_bundle_path) { $lines.Add('- Skill bundle: ' + $runbook.skill_bundle_path) }
if ($runbook.direct_prompt_path) { $lines.Add('- Direct prompt: ' + $runbook.direct_prompt_path) }
$lines.Add('')
$lines.Add('## Structural Readiness')
$lines.Add('')
if ($runbook.asmdef_readiness_suggestion) {
  foreach ($line in ($runbook.asmdef_readiness_suggestion -split "`r?`n")) {
    $lines.Add($line)
  }
} else {
  $lines.Add('- No asmdef readiness suggestion available.')
}
$lines.Add('')
$lines.Add('## Read Path')
$lines.Add('')
foreach ($path in $runbook.recommended_read_path) {
  $lines.Add('- ' + $path)
}
$lines.Add('')
$lines.Add('## Skill Contract')
$lines.Add('')
$lines.Add('- Skill resolver status: ' + $runbook.skill_resolver_status)
$lines.Add('- Skill resolver marker: ' + $runbook.skill_resolver_marker)
$lines.Add('- Tool policy status: ' + $runbook.tool_policy_status)
$lines.Add('- Tool policy marker: ' + $runbook.tool_policy_marker)
foreach ($violation in @($runbook.tool_policy_violations)) {
  $lines.Add('- Tool policy violation: ' + [string]$violation)
}
foreach ($skill in @($runbook.required_skills)) {
  $lines.Add('- Required skill: ' + [string]$skill)
}
foreach ($skillRef in @($runbook.required_skill_paths)) {
  $resolvedPath = if ($skillRef.exists) { [string]$skillRef.path } else { 'missing' }
  $lines.Add('- Required skill path: ' + [string]$skillRef.name + ' => ' + $resolvedPath)
}
foreach ($item in @($runbook.required_evidence)) {
  $lines.Add('- Required evidence: ' + [string]$item)
}
foreach ($tool in @($runbook.forbidden_tools)) {
  $lines.Add('- Forbidden tool: ' + [string]$tool)
}
foreach ($note in @($runbook.enforcement_notes)) {
  $lines.Add('- Enforcement note: ' + [string]$note)
}
$lines.Add('')
$lines.Add('## Next Commands')
$lines.Add('')
foreach ($command in @($runbook.next_commands)) {
  $lines.Add('- `' + [string]$command + '`')
}
$lines.Add('')
$lines.Add('## Verification')
$lines.Add('')
$lines.Add('- ' + $runbook.verification_expectation)

[string]::Join("`r`n", $lines) | Set-Content -LiteralPath $OutputMarkdownPath -Encoding UTF8

Write-Host "Wrote runbook JSON: $OutputJsonPath"
Write-Host "Wrote runbook Markdown: $OutputMarkdownPath"
