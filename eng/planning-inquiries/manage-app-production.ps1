[CmdletBinding()]
param(
    [ValidateSet('Write', 'Validate', 'Export', 'Serve', 'IntakeCheck')] [string] $Mode = 'Validate',
    [string] $BindingsPath = 'eng/planning-inquiries/app-production/role-app-links.json',
    [string] $OutputPath = 'docs/AI/generated/planning-app-production',
    [string[]] $EvidencePaths = @(),
    [string] $PlanId,
    [string] $WorkItemId,
    [string] $Destination,
    [ValidateRange(1024,65535)] [int] $Port = 5388
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$node = Get-Command node -ErrorAction Stop
$action = if ($Mode -eq 'Validate') { 'check' } elseif ($Mode -eq 'IntakeCheck') { 'intake-check' } else { $Mode.ToLowerInvariant() }
$arguments = @((Join-Path $PSScriptRoot 'app-production/cli.mjs'), $action, '--root', $root, '--bindings', $BindingsPath, '--output', $OutputPath)
foreach ($evidence in $EvidencePaths) { $arguments += @('--evidence', $evidence) }
if ($PlanId) { $arguments += @('--plan', $PlanId) }
if ($WorkItemId) { $arguments += @('--work-item', $WorkItemId) }
if ($Destination) { $arguments += @('--destination', $Destination) }
if ($Mode -eq 'Serve') { $arguments += @('--port', [string]$Port) }
& $node.Source @arguments
if ($LASTEXITCODE -ne 0) { throw "PlanningAppProductionFailed:$LASTEXITCODE" }
