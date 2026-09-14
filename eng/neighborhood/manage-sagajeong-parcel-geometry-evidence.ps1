[CmdletBinding()]
param(
    [ValidateSet('Readiness', 'Build', 'Validate', 'Summary', 'SelfTest')]
    [string] $Mode = 'Readiness',
    [string] $SourceArchive = '',
    [string] $ExpectedSourceSha256 = '',
    [string] $SourceRevision = '',
    [string] $OutputDirectory = 'artifacts/local/public-data/sagajeong-parcel-geometry-20260914-r1/derived',
    [string] $PythonExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local'))
$builder = Join-Path $PSScriptRoot 'build-sagajeong-parcel-geometry-evidence.py'

function Resolve-RepositoryPath([string] $Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $Value))
}

function Require-ArtifactPath([string] $Value, [string] $Code) {
    $resolved = Resolve-RepositoryPath $Value
    $prefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SagajeongParcelGeometryManager:$Code`MustRemainUnderArtifactsLocal:$resolved"
    }
    return $resolved
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $candidate = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    $PythonExecutable = if (Test-Path -LiteralPath $candidate -PathType Leaf) { $candidate } else { (Get-Command python -ErrorAction Stop).Source }
}
if (-not (Test-Path -LiteralPath $PythonExecutable -PathType Leaf)) { throw "SagajeongParcelGeometryManager:PythonMissing" }
if (-not (Test-Path -LiteralPath $builder -PathType Leaf)) { throw "SagajeongParcelGeometryManager:BuilderMissing" }

$output = Require-ArtifactPath $OutputDirectory 'Output'
$arguments = @('--repository-root', $repositoryRoot, '--output-dir', $output)
$pythonMode = switch ($Mode) {
    'SelfTest' { 'self-test' }
    'Build' { 'build' }
    'Validate' { 'validate' }
    default { 'readiness' }
}
$arguments += @('--mode', $pythonMode)

if (-not [string]::IsNullOrWhiteSpace($SourceArchive)) {
    $source = Require-ArtifactPath $SourceArchive 'SourceArchive'
    $arguments += @('--source-archive', $source)
}
if ($Mode -eq 'Build') {
    if ([string]::IsNullOrWhiteSpace($SourceArchive)) { throw 'SagajeongParcelGeometryManager:SourceArchiveRequired' }
    if ($ExpectedSourceSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'SagajeongParcelGeometryManager:ExpectedSourceSha256Required' }
    if ([string]::IsNullOrWhiteSpace($SourceRevision)) { throw 'SagajeongParcelGeometryManager:SourceRevisionRequired' }
    $arguments += @('--expected-source-sha256', $ExpectedSourceSha256, '--source-revision', $SourceRevision)
}

$geospatialPackages = Join-Path $repositoryRoot 'artifacts/local/python-packages/geospatial'
$previousPythonPath = $env:PYTHONPATH
try {
    if (Test-Path -LiteralPath $geospatialPackages -PathType Container) {
        $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) { $geospatialPackages } else { "$geospatialPackages;$previousPythonPath" }
    }
    $lines = @(& $PythonExecutable -B $builder @arguments 2>&1)
    $exitCode = $LASTEXITCODE
}
finally {
    $env:PYTHONPATH = $previousPythonPath
}
$text = ($lines | Out-String).Trim()
if ($exitCode -ne 0) { throw "SagajeongParcelGeometryManager:$pythonMode`Failed:$exitCode`n$text" }
$payload = $text | ConvertFrom-Json

if ($Mode -eq 'Summary') {
    Write-Output "SagajeongParcelGeometrySummary:Status=$($payload.status);Target=$($payload.targetParcelCount);Geometry=$($payload.geometryParcelCount);Collection=$($payload.collectionState);Coverage=$($payload.coverageState);Authorized=$($payload.applicationAuthorized)"
    return
}
$payload | ConvertTo-Json -Depth 30
