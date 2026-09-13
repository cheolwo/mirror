[CmdletBinding()]
param(
    [ValidateSet('PrivateReview', 'Promote')]
    [string] $Mode = 'PrivateReview',
    [string] $BuildingZipPath = 'artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip',
    [string] $OsmPath = 'artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm',
    [string] $BaseMapPath = '',
    [string] $UnityRoot = $env:SSALDDEL_UNITY_ROOT,
    [string] $OutputPath = '',
    [string] $AuditJsonPath = '',
    [string] $AuditHtmlPath = '',
    [string] $LicenseReceiptPath = '',
    [string] $PythonExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$localArtifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local'))

function Resolve-RepositoryPath([string] $Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $Value))
}

function Resolve-LocalArtifactPath([string] $Value) {
    $resolved = Resolve-RepositoryPath $Value
    $prefix = $localArtifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputMustRemainUnderArtifactsLocal:$resolved"
    }
    return $resolved
}

function Remove-OwnedTemporaryFile([string] $Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path) -and [IO.File]::Exists($Path)) {
        [IO.File]::Delete($Path)
    }
}

function Move-AtomicReplace([string] $Source, [string] $Destination) {
    if (-not [IO.File]::Exists($Source)) { throw "AtomicMoveSourceMissing:$Source" }
    if (-not [String]::Equals(
        [IO.Path]::GetDirectoryName($Source),
        [IO.Path]::GetDirectoryName($Destination),
        [StringComparison]::OrdinalIgnoreCase
    )) {
        throw 'AtomicMoveRequiresSameDirectory'
    }
    [IO.File]::Move($Source, $Destination, $true)
}

if ([string]::IsNullOrWhiteSpace($BaseMapPath)) {
    if ([string]::IsNullOrWhiteSpace($UnityRoot)) {
        throw 'UnityRootMissing:SetSSALDDEL_UNITY_ROOTOrBaseMapPath'
    }
    $BaseMapPath = Join-Path $UnityRoot 'Assets/Ssalddel/Resources/SagajeongReference.json'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = if ($Mode -eq 'Promote') {
        'artifacts/local/sagajeong-spatial-presentation/public.json'
    } else {
        'artifacts/local/sagajeong-spatial-presentation/private-review.json'
    }
}
if ([string]::IsNullOrWhiteSpace($AuditJsonPath)) {
    $AuditJsonPath = if ($Mode -eq 'Promote') {
        'artifacts/local/sagajeong-spatial-presentation/public-coverage-audit.json'
    } else {
        'artifacts/local/sagajeong-spatial-presentation/coverage-audit.json'
    }
}
if ([string]::IsNullOrWhiteSpace($AuditHtmlPath)) {
    $AuditHtmlPath = if ($Mode -eq 'Promote') {
        'artifacts/local/sagajeong-spatial-presentation/public-coverage-audit.html'
    } else {
        'artifacts/local/sagajeong-spatial-presentation/coverage-audit.html'
    }
}
if ($Mode -eq 'Promote' -and [string]::IsNullOrWhiteSpace($LicenseReceiptPath)) {
    throw 'PromotionGate:LicenseReceiptMissing'
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

$gisRuntimeCandidates = @(
    (Join-Path $repositoryRoot 'artifacts/local/public-data/gis-runtime-r1'),
    (Join-Path $repositoryRoot 'artifacts/local/python-packages/geospatial')
)
$gisRuntime = $gisRuntimeCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($gisRuntime)) {
    throw 'SpatialPresentationDependenciesMissing:pyproj+shapely'
}

$builder = Join-Path $repositoryRoot 'eng/neighborhood/build-sagajeong-spatial-presentation.py'
$auditor = Join-Path $repositoryRoot 'eng/neighborhood/audit-sagajeong-spatial-presentation.py'
$previousPythonPath = $env:PYTHONPATH
$previousPromotionToken = $env:SSALDDEL_SPATIAL_PROMOTION_TOKEN
$hadPromotionToken = Test-Path Env:SSALDDEL_SPATIAL_PROMOTION_TOKEN
$ownedTemporaryPaths = [Collections.Generic.List[string]]::new()
try {
    $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
        $gisRuntime
    } else {
        "$gisRuntime$([IO.Path]::PathSeparator)$previousPythonPath"
    }

    if ($Mode -eq 'PrivateReview') {
        & $PythonExecutable $builder `
            --repository-root $repositoryRoot `
            --building-zip $BuildingZipPath `
            --osm $OsmPath `
            --base-map $BaseMapPath `
            --mode PrivateReview `
            --output $OutputPath
        if ($LASTEXITCODE -ne 0) { throw "SpatialPresentationBuildFailed:$LASTEXITCODE" }

        & $PythonExecutable $auditor `
            --repository-root $repositoryRoot `
            --overlay $OutputPath `
            --building-zip $BuildingZipPath `
            --osm $OsmPath `
            --base-map $BaseMapPath `
            --audit-json $AuditJsonPath `
            --audit-html $AuditHtmlPath
        if ($LASTEXITCODE -ne 0) { throw "SpatialPresentationAuditFailed:$LASTEXITCODE" }
        return
    }

    $finalOverlayPath = Resolve-LocalArtifactPath $OutputPath
    $finalAuditJsonPath = Resolve-LocalArtifactPath $AuditJsonPath
    $finalAuditHtmlPath = Resolve-LocalArtifactPath $AuditHtmlPath
    $promotionOutputs = @($finalOverlayPath, $finalAuditJsonPath, $finalAuditHtmlPath)
    if (@($promotionOutputs | Sort-Object -Unique).Count -ne 3) {
        throw 'PromotionOutputsMustBeDistinct'
    }
    $runId = [Guid]::NewGuid().ToString('N')
    $overlayStem = [IO.Path]::GetFileNameWithoutExtension($finalOverlayPath)
    $pendingOverlayPath = Join-Path ([IO.Path]::GetDirectoryName($finalOverlayPath)) ".$overlayStem.pending-$runId.json"
    $approvedOverlayPath = Join-Path ([IO.Path]::GetDirectoryName($finalOverlayPath)) ".$overlayStem.approved-$runId.tmp"
    $pendingAuditJsonPath = Join-Path ([IO.Path]::GetDirectoryName($finalAuditJsonPath)) ".$([IO.Path]::GetFileName($finalAuditJsonPath)).pending-$runId.tmp"
    $pendingAuditHtmlPath = Join-Path ([IO.Path]::GetDirectoryName($finalAuditHtmlPath)) ".$([IO.Path]::GetFileName($finalAuditHtmlPath)).pending-$runId.tmp"
    foreach ($path in @($pendingOverlayPath, $approvedOverlayPath, $pendingAuditJsonPath, $pendingAuditHtmlPath)) {
        $ownedTemporaryPaths.Add($path)
    }

    $finalExisted = [IO.File]::Exists($finalOverlayPath)
    $finalHash = if ($finalExisted) { (Get-FileHash -LiteralPath $finalOverlayPath -Algorithm SHA256).Hash } else { '' }
    $publicationCommitted = $false
    $promotionToken = [Guid]::NewGuid().ToString('N')
    $env:SSALDDEL_SPATIAL_PROMOTION_TOKEN = $promotionToken
    try {
        & $PythonExecutable $builder `
            --repository-root $repositoryRoot `
            --building-zip $BuildingZipPath `
            --osm $OsmPath `
            --base-map $BaseMapPath `
            --mode Promote `
            --license-receipt $LicenseReceiptPath `
            --promotion-token $promotionToken `
            --output $pendingOverlayPath
        if ($LASTEXITCODE -ne 0) { throw "SpatialPresentationPromotionCandidateBuildFailed:$LASTEXITCODE" }

        & $PythonExecutable $auditor `
            --repository-root $repositoryRoot `
            --overlay $pendingOverlayPath `
            --building-zip $BuildingZipPath `
            --osm $OsmPath `
            --base-map $BaseMapPath `
            --promotion-receipt $LicenseReceiptPath `
            --promotion-token $promotionToken `
            --approved-output $approvedOverlayPath `
            --audit-json $pendingAuditJsonPath `
            --audit-html $pendingAuditHtmlPath
        if ($LASTEXITCODE -ne 0) { throw "SpatialPresentationPromotionAuditFailed:$LASTEXITCODE" }

        $audit = Get-Content -LiteralPath $pendingAuditJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($audit.status -notin @('Passed', 'PassedWithIncompleteSurfaceEvidence')) {
            throw "SpatialPresentationPromotionAuditStatusInvalid:$($audit.status)"
        }
        $approved = Get-Content -LiteralPath $approvedOverlayPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($approved.status -cne 'PublicOpenData' -or -not $approved.distributionApproved) {
            throw 'SpatialPresentationPromotionApprovalBoundaryInvalid'
        }

        Move-AtomicReplace $pendingAuditHtmlPath $finalAuditHtmlPath
        Move-AtomicReplace $pendingAuditJsonPath $finalAuditJsonPath
        Move-AtomicReplace $approvedOverlayPath $finalOverlayPath
        $publicationCommitted = $true
    }
    catch {
        if (-not $publicationCommitted) {
            if ($finalExisted) {
                if (-not [IO.File]::Exists($finalOverlayPath) -or
                    (Get-FileHash -LiteralPath $finalOverlayPath -Algorithm SHA256).Hash -cne $finalHash) {
                    throw "PromotionFailureChangedExistingFinal:$($_.Exception.Message)"
                }
            } elseif ([IO.File]::Exists($finalOverlayPath)) {
                throw "PromotionFailureCreatedFinal:$($_.Exception.Message)"
            }
        }
        throw
    }
}
finally {
    foreach ($path in $ownedTemporaryPaths) { Remove-OwnedTemporaryFile $path }
    $env:PYTHONPATH = $previousPythonPath
    if ($hadPromotionToken) {
        $env:SSALDDEL_SPATIAL_PROMOTION_TOKEN = $previousPromotionToken
    } else {
        Remove-Item Env:SSALDDEL_SPATIAL_PROMOTION_TOKEN -ErrorAction SilentlyContinue
    }
}
