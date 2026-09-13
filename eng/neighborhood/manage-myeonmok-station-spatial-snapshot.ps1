[CmdletBinding()]
param(
    [string] $PythonExecutable = '',
    [string] $OutputPath = 'artifacts/local/myeonmok-station-spatial-snapshot/private-review.json',
    [string] $AuditOutputPath = 'artifacts/local/myeonmok-station-spatial-snapshot/coverage-audit.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Require($condition, [string] $code) {
    if (-not $condition) { throw "MyeonmokStationSpatialSnapshotManager:$code" }
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $python = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $python) {
        $python.Source
    } else {
        Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    }
}
Require (Test-Path -LiteralPath $PythonExecutable -PathType Leaf) 'PythonRuntimeMissing'

$gisRuntime = @(
    (Join-Path $repositoryRoot 'artifacts/local/public-data/gis-runtime-r1'),
    (Join-Path $repositoryRoot 'artifacts/local/python-packages/geospatial')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
Require (-not [string]::IsNullOrWhiteSpace($gisRuntime)) 'GisRuntimeMissing'

$builder = Join-Path $PSScriptRoot 'build-myeonmok-station-spatial-snapshot.py'
$auditor = Join-Path $PSScriptRoot 'audit-myeonmok-station-spatial-snapshot.py'
$acquisition = Join-Path $PSScriptRoot 'acquire-myeonmok-station-osm.ps1'
$previousPythonPath = $env:PYTHONPATH
$previousNoBytecode = $env:PYTHONDONTWRITEBYTECODE
$env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
    $gisRuntime
} else {
    "$gisRuntime$([IO.Path]::PathSeparator)$previousPythonPath"
}
$env:PYTHONDONTWRITEBYTECODE = '1'
try {
    $acquisitionResult = & $acquisition -VerifyOnly | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and -not $acquisitionResult.networkRequested) 'FrozenOsmVerificationFailed'

    $buildResult = & $PythonExecutable -B $builder `
        --repository-root $repositoryRoot `
        --output $OutputPath | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0) 'BuildFailed'

    $auditResult = & $PythonExecutable -B $auditor `
        --repository-root $repositoryRoot `
        --snapshot $OutputPath `
        --audit-output $AuditOutputPath | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0) 'AuditFailed'
    [ordered]@{
        status = 'GeneratedAndAudited'
        sourceReusedWithoutNetwork = -not [bool]$acquisitionResult.networkRequested
        output = $buildResult.output
        revision = $buildResult.revision
        contentHash = $buildResult.contentHash
        auditOutput = $auditResult.auditOutput
        auditHash = $auditResult.auditHash
        buildingCount = $buildResult.buildingCount
        roadCount = $buildResult.roadCount
        surfaceCount = $buildResult.surfaceCount
        administrativeAreaCount = $buildResult.administrativeAreaCount
        tileCount = $buildResult.tileCount
        coverage = $buildResult.coverage
        missingCoverageCodes = $auditResult.missingCoverageCodes
        distributionApproved = $false
        gameplayReady = $false
        traversalReady = $false
    } | ConvertTo-Json -Depth 10
}
finally {
    $env:PYTHONPATH = $previousPythonPath
    $env:PYTHONDONTWRITEBYTECODE = $previousNoBytecode
}
