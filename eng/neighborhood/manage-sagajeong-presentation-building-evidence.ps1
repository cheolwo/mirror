[CmdletBinding()]
param(
    [ValidateSet('Build', 'Validate', 'Summary')]
    [string] $Mode = 'Validate',
    [string] $OutputDirectory = 'artifacts/local/public-data/sagajeong-presentation-building-evidence-20260914-r1',
    [string] $PythonExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$localArtifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local'))
$builder = Join-Path $PSScriptRoot 'build-sagajeong-presentation-building-evidence.py'

function Resolve-RepositoryPath([string] $Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $Value))
}

function Resolve-LocalArtifactDirectory([string] $Value) {
    $resolved = Resolve-RepositoryPath $Value
    $prefix = $localArtifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SagajeongPresentationBuildingEvidence:OutputMustRemainUnderArtifactsLocal:$resolved"
    }
    return $resolved
}

function Invoke-Generator([string[]] $Arguments, [string] $Phase) {
    $savedErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $lines = @(& $PythonExecutable -B $builder @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorPreference
    }
    $text = ($lines | Out-String).Trim()
    if ($exitCode -ne 0) {
        throw "SagajeongPresentationBuildingEvidenceManager:$Phase`Failed:$exitCode`n$text"
    }
    try {
        return $text | ConvertFrom-Json
    }
    catch {
        throw "SagajeongPresentationBuildingEvidenceManager:$Phase`InvalidJson:$($_.Exception.Message)"
    }
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $pythonCommand) {
        $pythonCommand.Source
    }
    else {
        Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    }
}
if (-not (Test-Path -LiteralPath $PythonExecutable -PathType Leaf)) {
    throw "SagajeongPresentationBuildingEvidenceManager:PythonRuntimeMissing:$PythonExecutable"
}
if (-not (Test-Path -LiteralPath $builder -PathType Leaf)) {
    throw "SagajeongPresentationBuildingEvidenceManager:BuilderMissing:$builder"
}

$output = Resolve-LocalArtifactDirectory $OutputDirectory
$arguments = @('--repository-root', $repositoryRoot, '--output-dir', $output)
if ($Mode -eq 'Build') {
    $null = Invoke-Generator -Arguments $arguments -Phase 'Build'
}
$validation = Invoke-Generator -Arguments ($arguments + '--validate-only') -Phase 'Validate'

$reportedStatus = switch ($Mode) {
    'Build' { 'BuiltAndValidated' }
    'Summary' { 'Summary' }
    default { 'Validated' }
}
[ordered]@{
    status = $reportedStatus
    networkRequested = [bool] $validation.networkRequested
    stationStableId = [string] $validation.stationStableId
    bindingRevision = [string] $validation.bindingRevision
    bindingContentSha256 = [string] $validation.bindingContentSha256
    bindingFileSha256 = [string] $validation.bindingFileSha256
    addressRevision = [string] $validation.addressRevision
    addressContentSha256 = [string] $validation.addressContentSha256
    addressFileSha256 = [string] $validation.addressFileSha256
    presentationBuildingCount = [int] $validation.presentationBuildingCount
    bindingCount = [int] $validation.bindingCount
    parcelIdentifierCount = [int] $validation.parcelIdentifierCount
    uniqueParcelIdentifierCount = [int] $validation.uniqueParcelIdentifierCount
    rawParcelAddressCandidates = [ordered]@{
        single = [int] $validation.rawParcelAddressCandidates.single
        multiple = [int] $validation.rawParcelAddressCandidates.multiple
        none = [int] $validation.rawParcelAddressCandidates.none
    }
    officialPromotedCount = [int] $validation.officialPromotedCount
    consumerUseAuthorizedCount = [int] $validation.consumerUseAuthorizedCount
    parcelGeometryCollected = [bool] $validation.parcelGeometryCollected
} | ConvertTo-Json -Depth 5 -Compress
