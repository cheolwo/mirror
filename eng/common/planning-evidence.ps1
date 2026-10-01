# Optional planning evidence bridge. Call Start before commands and Complete from finally.
# Only explicit source references, sanitized execution scope and artifact hashes are persisted.
function Start-PlanningEvidenceContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $RepoRoot,
        [Parameter(Mandatory)][string] $WorkItemId,
        [string] $LinksPath = "eng/planning-inquiries/app-production/role-app-links.json",
        [Parameter(Mandatory)][string] $StartPath,
        [Parameter(Mandatory)][string] $Kind,
        [hashtable] $Scope = @{},
        [string[]] $InputPaths = @(),
        [string[]] $InputRoots = @(),
        [string[]] $ToolPaths = @(),
        [string] $TargetFramework
    )
    $modulePath = Join-Path $RepoRoot "eng/planning-inquiries/app-production/evidence.mjs"
    $nodeCommand = Get-Command node -ErrorAction Stop
    $request = @{
        root = $RepoRoot; workItemId = $WorkItemId; linksPath = $LinksPath
        startPath = $StartPath; kind = $Kind; scope = $Scope
        inputPaths = @($InputPaths); inputRoots = @($InputRoots); toolPaths = @($ToolPaths)
        environment = @{ powershell = $PSVersionTable.PSVersion.ToString(); targetFramework = $TargetFramework }
    }
    $response = $request | ConvertTo-Json -Depth 12 -Compress | & $nodeCommand.Source $modulePath start
    if ($LASTEXITCODE -ne 0) { throw "PlanningEvidenceStartFailed" }
    $parsed = $response | ConvertFrom-Json
    return [pscustomobject]@{
        RepoRoot = $RepoRoot; StartPath = $parsed.startPath; ManifestPath = $parsed.manifestPath
        NodePath = $nodeCommand.Source; ModulePath = $modulePath
    }
}

function Complete-PlanningEvidenceContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)][ValidateSet("Passed", "Failed", "NotRun", "NotApplicable", "Unknown")][string] $Result,
        [object[]] $Checks = @(),
        [string[]] $ArtifactPaths = @()
    )
    $request = @{
        root = $Context.RepoRoot; startPath = $Context.StartPath
        result = $Result; checks = @($Checks); artifactPaths = @($ArtifactPaths)
    }
    $response = $request | ConvertTo-Json -Depth 12 -Compress | & $Context.NodePath $Context.ModulePath finish
    if ($LASTEXITCODE -ne 0) { throw "PlanningEvidenceFinishFailed" }
    $parsed = $response | ConvertFrom-Json
    Write-Host "Planning evidence: $($parsed.manifestPath) [$($parsed.result)/$($parsed.freshness)]"
}
