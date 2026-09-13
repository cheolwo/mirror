[CmdletBinding()]
param(
    [switch] $SkipPrivateSourceRun,
    [string] $PythonExecutable = '',
    [string] $BaseMapPath = '',
    [string] $UnityRoot = $env:SSALDDEL_UNITY_ROOT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$builder = Join-Path $repositoryRoot 'eng/neighborhood/build-sagajeong-spatial-presentation.py'
$auditor = Join-Path $repositoryRoot 'eng/neighborhood/audit-sagajeong-spatial-presentation.py'
$manager = Join-Path $repositoryRoot 'eng/neighborhood/manage-sagajeong-spatial-presentation.ps1'
$buildingZip = Join-Path $repositoryRoot 'artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip'
$osm = Join-Path $repositoryRoot 'artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm'

function Require($condition, $code) {
    if (-not $condition) { throw "SagajeongSpatialPresentationTest:$code" }
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $pythonCommand) {
        $pythonCommand.Source
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

$testFolder = Join-Path $repositoryRoot 'artifacts/local/validation/sagajeong-spatial-presentation-tests'
[IO.Directory]::CreateDirectory($testFolder) | Out-Null
$ownedTemporaryFiles = [Collections.Generic.List[string]]::new()
$previousPythonPath = $env:PYTHONPATH
$env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
    $gisRuntime
} else {
    "$gisRuntime$([IO.Path]::PathSeparator)$previousPythonPath"
}
try {
    $builderSelfTest = & $PythonExecutable $builder --self-test | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $builderSelfTest.passed -and $builderSelfTest.checks -ge 25) 'BuilderSelfTest'
    $auditorSelfTest = & $PythonExecutable $auditor --self-test | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $auditorSelfTest.passed) 'AuditorSelfTest'

    $directPromotionOutput = Join-Path $testFolder "direct-promote-$([Guid]::NewGuid().ToString('N')).json"
    $ownedTemporaryFiles.Add($directPromotionOutput)
    $directPromotionText = & $PythonExecutable $builder `
        --repository-root $repositoryRoot `
        --base-map 'not-used-before-token-gate.json' `
        --mode Promote `
        --output $directPromotionOutput 2>&1
    Require ($LASTEXITCODE -eq 1) 'DirectPromotionAccepted'
    Require (($directPromotionText | Out-String) -like '*PromotionGate:ManagerTokenMissing*') 'DirectPromotionTokenGate'
    Require (-not [IO.File]::Exists($directPromotionOutput)) 'DirectPromotionCreatedApprovedOutput'

    if ($SkipPrivateSourceRun) {
        Write-Output 'PASS self-tests and direct Promote gate; frozen private source run skipped explicitly'
        return
    }
    if ([string]::IsNullOrWhiteSpace($BaseMapPath)) {
        if (-not [string]::IsNullOrWhiteSpace($UnityRoot)) {
            $BaseMapPath = Join-Path $UnityRoot 'Assets/Ssalddel/Resources/SagajeongReference.json'
        }
    }
    if (-not (Test-Path -LiteralPath $buildingZip -PathType Leaf) -or
        -not (Test-Path -LiteralPath $osm -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($BaseMapPath) -or
        -not (Test-Path -LiteralPath $BaseMapPath -PathType Leaf)) {
        Write-Output 'PASS self-tests and direct Promote gate; frozen private source or explicit Unity/base-map path unavailable'
        return
    }

    $invalidReceiptPath = Join-Path $testFolder "invalid-rights-$([Guid]::NewGuid().ToString('N')).json"
    $publicInvariantPath = Join-Path $testFolder 'public-invariant.json'
    $publicAuditInvariantPath = Join-Path $testFolder 'public-invariant-audit.json'
    $publicHtmlInvariantPath = Join-Path $testFolder 'public-invariant-audit.html'
    foreach ($path in @($invalidReceiptPath, $publicInvariantPath, $publicAuditInvariantPath, $publicHtmlInvariantPath)) {
        $ownedTemporaryFiles.Add($path)
    }
    $invalidReceipt = [ordered]@{
        datasetId = 'data-go-kr-15083092'
        rawHash = '674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755'
        status = 'RightsReconciled'
        licenseCode = 'KOGL-Type1'
        sourceIdentityVerified = $true
        acquisitionReceipt = [ordered]@{
            provider = '국토교통부'
            datasetId = 'data-go-kr-15083092'
            datasetDate = '2026-08-09'
            fetchedAtUtc = '2026-09-06T14:42:52.765Z'
            rawHash = '674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755'
            rawLength = 135675376
            originalCrs = 'EPSG:5186'
        }
        reviewer = 'SyntheticFixture:NegativeTest'
        reviewedAtUtc = '2026-09-13T00:00:00Z'
        evidenceHash = ('0' * 64)
    }
    [IO.File]::WriteAllText(
        $invalidReceiptPath,
        (($invalidReceipt | ConvertTo-Json -Depth 8) + "`n"),
        [Text.UTF8Encoding]::new($false)
    )
    [IO.File]::WriteAllText($publicInvariantPath, "existing-final-must-not-change`n", [Text.UTF8Encoding]::new($false))
    $publicInvariantHash = (Get-FileHash -LiteralPath $publicInvariantPath -Algorithm SHA256).Hash
    $managerRejected = $false
    try {
        & $manager -Mode Promote -BaseMapPath $BaseMapPath `
            -LicenseReceiptPath $invalidReceiptPath `
            -OutputPath $publicInvariantPath `
            -AuditJsonPath $publicAuditInvariantPath `
            -AuditHtmlPath $publicHtmlInvariantPath `
            -PythonExecutable $PythonExecutable
    }
    catch {
        $managerRejected = $_.Exception.Message -like '*SpatialPresentationPromotionCandidateBuildFailed*'
    }
    Require $managerRejected 'InvalidPromotionWasNotRejected'
    Require ((Get-FileHash -LiteralPath $publicInvariantPath -Algorithm SHA256).Hash -ceq $publicInvariantHash) 'PromotionFailureChangedExistingFinal'
    Require (-not [IO.File]::Exists($publicAuditInvariantPath)) 'PromotionFailureCreatedAuditJson'
    Require (-not [IO.File]::Exists($publicHtmlInvariantPath)) 'PromotionFailureCreatedAuditHtml'
    Require (
        @(Get-ChildItem -LiteralPath $testFolder -Force | Where-Object {
            $_.Name -like '*.pending-*' -or $_.Name -like '*.approved-*'
        }).Count -eq 0
    ) 'PromotionFailureLeftTemporaryFiles'

    $overlayPath = Join-Path $testFolder 'private-review.json'
    $auditJsonPath = Join-Path $testFolder 'coverage-audit.json'
    $auditHtmlPath = Join-Path $testFolder 'coverage-audit.html'
    $unityCandidate = if ([string]::IsNullOrWhiteSpace($UnityRoot)) {
        ''
    } else {
        Join-Path $UnityRoot 'Assets/Ssalddel/Resources/SagajeongSpatialPresentationOverlay.json'
    }
    $candidateExisted = -not [string]::IsNullOrWhiteSpace($unityCandidate) -and [IO.File]::Exists($unityCandidate)
    $candidateHash = if ($candidateExisted) { (Get-FileHash -LiteralPath $unityCandidate -Algorithm SHA256).Hash } else { '' }

    & $manager -Mode PrivateReview -BaseMapPath $BaseMapPath `
        -OutputPath $overlayPath -AuditJsonPath $auditJsonPath -AuditHtmlPath $auditHtmlPath `
        -PythonExecutable $PythonExecutable
    Require ($LASTEXITCODE -eq 0) 'FirstPrivateBuild'
    $firstOverlayHash = (Get-FileHash -LiteralPath $overlayPath -Algorithm SHA256).Hash
    $firstAuditHash = (Get-FileHash -LiteralPath $auditJsonPath -Algorithm SHA256).Hash
    $firstHtmlHash = (Get-FileHash -LiteralPath $auditHtmlPath -Algorithm SHA256).Hash

    & $manager -Mode PrivateReview -BaseMapPath $BaseMapPath `
        -OutputPath $overlayPath -AuditJsonPath $auditJsonPath -AuditHtmlPath $auditHtmlPath `
        -PythonExecutable $PythonExecutable
    Require ($LASTEXITCODE -eq 0) 'SecondPrivateBuild'
    Require ((Get-FileHash -LiteralPath $overlayPath -Algorithm SHA256).Hash -ceq $firstOverlayHash) 'OverlayNotDeterministic'
    Require ((Get-FileHash -LiteralPath $auditJsonPath -Algorithm SHA256).Hash -ceq $firstAuditHash) 'AuditJsonNotDeterministic'
    Require ((Get-FileHash -LiteralPath $auditHtmlPath -Algorithm SHA256).Hash -ceq $firstHtmlHash) 'AuditHtmlNotDeterministic'

    $overlay = Get-Content -LiteralPath $overlayPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $audit = Get-Content -LiteralPath $auditJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($overlay.schemaVersion -ceq 'ssalddel.spatial-presentation-overlay.v1') 'SchemaVersion'
    Require ($overlay.revision -ceq 'sagajeong-spatial-presentation.private-review.r2') 'Revision'
    Require ($overlay.status -ceq 'LocalPrivateReview' -and $overlay.presentationOnly -and -not $overlay.distributionApproved) 'PrivateBoundary'
    Require ($overlay.baseMapRevision -ceq 'sagajeong-reference.r3') 'BaseMapRevision'
    Require ($overlay.baseMapHash -ceq '4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3') 'BaseMapHash'
    Require ($overlay.coordinateMethod -ceq 'WGS84-ECEF-ENU-at-zero-altitude') 'CoordinateMethod'
    Require ($overlay.buildings.Count -eq 4062) 'FrozenBuildingCount'
    Require (@($overlay.buildings | Where-Object heightKind -eq 'ObservedSourceHeight').Count -eq 1427) 'ObservedHeightCount'
    Require (@($overlay.buildings | Where-Object heightKind -eq 'SymbolicFallback4m').Count -eq 2635) 'SymbolicHeightCount'
    Require ($overlay.surfaces.Count -eq 108) 'FrozenSurfaceCount'
    $legacyAliases = @($overlay.buildings.legacyOsmIds)
    Require ($legacyAliases.Count -eq 544) 'FrozenLegacyAliasCount'
    Require (@($legacyAliases | Sort-Object -Unique).Count -eq $legacyAliases.Count) 'LegacyAliasClaimedMoreThanOnce'
    Require (@($overlay.buildings | Where-Object { $_.legacyMatch.method -eq 'WeakFootprintCandidateExcluded' }).Count -eq 5) 'WeakLegacyCandidateCount'
    Require (@($overlay.buildings | Where-Object { $_.legacyMatch.ambiguous -and $_.legacyOsmIds.Count -gt 0 }).Count -eq 0) 'AmbiguousAliasWasNotExcluded'
    Require (($overlay.sourceReceipts | Where-Object rawHash -eq '674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755').licenseCode -ceq 'RightsConflictUnresolved') 'PrivateLicenseBoundary'

    Require ($audit.schemaVersion -ceq 'ssalddel.spatial-presentation-coverage-audit.v2') 'AuditSchemaVersion'
    Require ($audit.status -ceq 'PassedWithIncompleteSurfaceEvidence' -and $audit.cells.Count -eq 100) 'CoverageAuditStatus'
    Require ($audit.summary.classificationCounts.ConfirmedBuilt -eq 88) 'ConfirmedBuiltCount'
    Require ($audit.summary.classificationCounts.ConfirmedOpen -eq 2) 'ConfirmedOpenCount'
    Require ($audit.summary.classificationCounts.IncompleteSurfaceEvidence -eq 5) 'IncompleteSurfaceEvidenceCount'
    Require ($audit.summary.classificationCounts.MissingCoverage -eq 5) 'MissingCoverageCount'
    Require ($audit.sourceAudit.method -ceq 'IndependentBinaryShpDbfAndOsmReparse') 'IndependentSourceAuditMethod'
    Require ($audit.sourceAudit.comparedBuildingCount -eq 4062 -and $audit.sourceAudit.comparedSurfaceCount -eq 108) 'IndependentSourceCounts'
    Require ($audit.sourceCompleteness.omittedSourceFeatureCount -eq 1) 'OmittedSourceFeatureCount'
    Require ($audit.sourceCompleteness.omittedSourceFeatures[0].id -ceq 'osm:relation:14428122') 'KnownIncompleteRelation'
    Require ($audit.sourceCompleteness.affectedCellCount -eq 7) 'IncompleteAffectedCellCount'
    Require ($audit.summary.boundaryClipped -and $audit.summary.boundaryClippedFeatureCount -eq 169) 'BoundaryClipEvidence'
    foreach ($cell in $audit.cells) {
        $ratioSum = [double]$cell.buildingCoverageRatio + [double]$cell.openCoverageRatio + [double]$cell.unknownCoverageRatio
        Require ([Math]::Abs($ratioSum - 1.0) -le 0.000002) "CoveragePartition:$($cell.id)"
        if ($cell.classification -eq 'ConfirmedBuilt') {
            Require ([double]$cell.buildingCoverageRatio -ge 0.20) "ConfirmedBuiltBelowThreshold:$($cell.id)"
        }
    }

    $tamperedPath = Join-Path $testFolder 'tampered-source-feature.json'
    $tamperedAuditJsonPath = Join-Path $testFolder 'tampered-audit.json'
    $tamperedAuditHtmlPath = Join-Path $testFolder 'tampered-audit.html'
    foreach ($path in @($tamperedPath, $tamperedAuditJsonPath, $tamperedAuditHtmlPath)) {
        $ownedTemporaryFiles.Add($path)
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }
    $mutationScript = @'
import sys
from pathlib import Path
sys.path.insert(0, sys.argv[3])
from sagajeong_spatial_presentation import apply_content_hash, load_json, write_json_deterministic
document = load_json(Path(sys.argv[1]))
document["buildings"][0]["sourceFeatureId"] = "tampered-after-content-hash-verification"
apply_content_hash(document)
write_json_deterministic(Path(sys.argv[2]), document)
'@
    & $PythonExecutable -B -c $mutationScript $overlayPath $tamperedPath (Join-Path $repositoryRoot 'eng/neighborhood')
    Require ($LASTEXITCODE -eq 0) 'TamperFixtureCreation'
    $tamperOutput = & $PythonExecutable $auditor `
        --repository-root $repositoryRoot `
        --overlay $tamperedPath `
        --building-zip $buildingZip `
        --osm $osm `
        --base-map $BaseMapPath `
        --audit-json $tamperedAuditJsonPath `
        --audit-html $tamperedAuditHtmlPath 2>&1
    Require ($LASTEXITCODE -eq 1) 'TamperedOverlayAccepted'
    Require (($tamperOutput | Out-String) -like '*IndependentAudit:BuildingFieldMismatch:sourceFeatureId:*') 'TamperedOverlayWrongFailure'
    Require (-not [IO.File]::Exists($tamperedAuditJsonPath) -and -not [IO.File]::Exists($tamperedAuditHtmlPath)) 'TamperFailurePublishedAudit'

    if ($candidateExisted) {
        Require ((Get-FileHash -LiteralPath $unityCandidate -Algorithm SHA256).Hash -ceq $candidateHash) 'UnityCandidateChanged'
    } elseif (-not [string]::IsNullOrWhiteSpace($unityCandidate)) {
        Require (-not [IO.File]::Exists($unityCandidate)) 'UnityCandidateCreated'
    }
    Write-Output "PASS private overlay + independent source audit + tamper rejection + determinism; buildings=$($overlay.buildings.Count); surfaces=$($overlay.surfaces.Count); aliases=$($legacyAliases.Count); contentHash=$($overlay.contentHash); no Unity copy"
}
finally {
    foreach ($path in $ownedTemporaryFiles) {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }
    $env:PYTHONPATH = $previousPythonPath
}
