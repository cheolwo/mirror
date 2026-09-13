[CmdletBinding()]
param(
    [string] $OverlayPath = 'artifacts/local/sagajeong-spatial-presentation/private-review.json',
    [string] $BuildingZipPath = 'artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip',
    [string] $OsmPath = 'artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm',
    [string] $BaseMapPath = '',
    [string] $UnityRoot = $env:SSALDDEL_UNITY_ROOT,
    [string] $AuditJsonPath = 'artifacts/local/sagajeong-spatial-presentation/coverage-audit.json',
    [string] $AuditHtmlPath = 'artifacts/local/sagajeong-spatial-presentation/coverage-audit.html',
    [string] $PromotionReceiptPath = '',
    [string] $PythonExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ([string]::IsNullOrWhiteSpace($BaseMapPath)) {
    if ([string]::IsNullOrWhiteSpace($UnityRoot)) {
        throw 'UnityRootMissing:SetSSALDDEL_UNITY_ROOTOrBaseMapPath'
    }
    $BaseMapPath = Join-Path $UnityRoot 'Assets/Ssalddel/Resources/SagajeongReference.json'
}
if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $pythonCommand) {
        $pythonCommand.Source
    } else {
        Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    }
}
if (-not (Test-Path -LiteralPath $PythonExecutable -PathType Leaf)) {
    throw "PythonRuntimeMissing:$PythonExecutable"
}
$gisRuntime = @(
    (Join-Path $repositoryRoot 'artifacts/local/public-data/gis-runtime-r1'),
    (Join-Path $repositoryRoot 'artifacts/local/python-packages/geospatial')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($gisRuntime)) {
    throw 'SpatialPresentationDependenciesMissing:pyproj+shapely'
}
$previousPythonPath = $env:PYTHONPATH
$env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
    $gisRuntime
} else {
    "$gisRuntime$([IO.Path]::PathSeparator)$previousPythonPath"
}
try {
    $auditArguments = @(
        (Join-Path $repositoryRoot 'eng/neighborhood/audit-sagajeong-spatial-presentation.py'),
        '--repository-root', $repositoryRoot,
        '--overlay', $OverlayPath,
        '--building-zip', $BuildingZipPath,
        '--osm', $OsmPath,
        '--base-map', $BaseMapPath,
        '--audit-json', $AuditJsonPath,
        '--audit-html', $AuditHtmlPath
    )
    if (-not [string]::IsNullOrWhiteSpace($PromotionReceiptPath)) {
        $auditArguments += @('--promotion-receipt', $PromotionReceiptPath)
    }
    & $PythonExecutable @auditArguments
    if ($LASTEXITCODE -ne 0) { throw "SpatialPresentationAuditFailed:$LASTEXITCODE" }
}
finally {
    $env:PYTHONPATH = $previousPythonPath
}
