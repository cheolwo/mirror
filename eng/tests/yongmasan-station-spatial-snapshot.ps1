[CmdletBinding()]
param(
    [string] $PythonExecutable = '',
    [switch] $SkipFrozenSourceRun,
    [switch] $RetainVerifiedArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$builder = Join-Path $repositoryRoot 'eng/neighborhood/build-yongmasan-station-spatial-snapshot.py'
$auditor = Join-Path $repositoryRoot 'eng/neighborhood/audit-yongmasan-station-spatial-snapshot.py'
$acquisition = Join-Path $repositoryRoot 'eng/neighborhood/acquire-yongmasan-station-osm.ps1'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "YongmasanStationSpatialSnapshotTest:$code" }
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $python = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $python) { $python.Source } else {
        Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    }
}
Require (Test-Path -LiteralPath $PythonExecutable -PathType Leaf) 'PythonRuntimeMissing'
$gisRuntime = @(
    (Join-Path $repositoryRoot 'artifacts/local/public-data/gis-runtime-r1'),
    (Join-Path $repositoryRoot 'artifacts/local/python-packages/geospatial')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
Require (-not [string]::IsNullOrWhiteSpace($gisRuntime)) 'GisRuntimeMissing'

$testDirectory = Join-Path $repositoryRoot 'artifacts/local/validation/yongmasan-station-spatial-snapshot-tests'
[IO.Directory]::CreateDirectory($testDirectory) | Out-Null
$ownedFiles = [Collections.Generic.List[string]]::new()
$previousPythonPath = $env:PYTHONPATH
$previousNoBytecode = $env:PYTHONDONTWRITEBYTECODE
$env:PYTHONPATH = "$gisRuntime$([IO.Path]::PathSeparator)$previousPythonPath"
$env:PYTHONDONTWRITEBYTECODE = '1'
try {
    $source = & $acquisition -VerifyOnly | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and -not $source.networkRequested) 'FrozenOsmReuseFailed'
    Require ($source.byteLength -eq 1389565) 'FrozenOsmLength'
    Require ($source.sha256 -ceq '5E690398DD779E91EDF467A09424FBBBB4448E5B3DC7D6FE3A15865E543227F1') 'FrozenOsmHash'
    $selfTest = & $PythonExecutable -B $builder --self-test | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $selfTest.passed -and $selfTest.checks -ge 9) 'BuilderSelfTest'
    $auditSelfTest = & $PythonExecutable -B $auditor --self-test | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $auditSelfTest.passed) 'AuditorSelfTest'

    if ($SkipFrozenSourceRun) {
        Write-Output 'PASS self-tests and frozen OSM reuse; full frozen-source run skipped explicitly'
        return
    }

    $snapshot = Join-Path $testDirectory 'private-review.json'
    $audit = Join-Path $testDirectory 'coverage-audit.json'
    $tampered = Join-Path $testDirectory 'tampered.json'
    $tamperedAudit = Join-Path $testDirectory 'tampered-audit.json'
    foreach ($path in @($snapshot, $audit, $tampered, $tamperedAudit)) {
        $ownedFiles.Add($path)
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }

    $first = & $PythonExecutable -B $builder --output $snapshot | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0) 'FirstBuildFailed'
    $firstFileHash = (Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash
    $second = & $PythonExecutable -B $builder --output $snapshot | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0) 'SecondBuildFailed'
    Require ($second.contentHash -ceq $first.contentHash) 'ContentHashNotDeterministic'
    Require ((Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash -ceq $firstFileHash) 'SnapshotBytesNotDeterministic'

    $auditResult = & $PythonExecutable -B $auditor --snapshot $snapshot --audit-output $audit | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $auditResult.status -ceq 'PassedWithMissingCoverage') 'AuditFailed'
    $value = Get-Content -LiteralPath $snapshot -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($value.revision -ceq 'yongmasan-station-spatial-snapshot.private-review.r1') 'Revision'
    Require ($value.contentHash -ceq '43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D') 'ContentHash'
    Require ($firstFileHash -ceq '6AC2E8595B913B6AE80D6426476F334225491E89555CEB9A5C1A17BD568120B6') 'FileHash'
    Require ($value.transitStationStableId -ceq 'station:kr:kric:s1107:0723' -and $value.stationName -ceq '용마산역') 'StationIdentity'
    Require ($value.coordinateFrame.originLatitude -eq 37.573752 -and $value.coordinateFrame.originLongitude -eq 127.086802) 'StationAnchor'
    Require ($value.window.widthMeters -eq 1000 -and $value.window.depthMeters -eq 1000) 'Window'
    Require ($value.presentationOnly -and -not $value.distributionApproved -and -not $value.gameplayReady -and -not $value.traversalReady) 'AuthorityBoundary'
    Require ($value.buildings.Count -eq 2859 -and $value.roads.Count -eq 116 -and $value.surfaces.Count -eq 41) 'GeometryCounts'
    Require ($value.administrativeAreas.Count -eq 4 -and $value.coverage.cells.Count -eq 100 -and $value.tiles.Count -eq 4) 'PartitionCounts'
    Require ($value.coverage.summary.classificationCounts.ConfirmedBuilt -eq 72) 'ConfirmedBuiltCount'
    Require ($value.coverage.summary.classificationCounts.ConfirmedOpen -eq 6) 'ConfirmedOpenCount'
    Require ($value.coverage.summary.classificationCounts.IncompleteSurfaceEvidence -eq 12) 'IncompleteSurfaceCount'
    Require ($value.coverage.summary.classificationCounts.MissingCoverage -eq 10) 'MissingCoverageCount'
    $stationArea = @($value.administrativeAreas | Where-Object containsStation)
    Require ($stationArea.Count -eq 1 -and $stationArea[0].administrativeAreaStableId -ceq 'region:kr:hjd:1126054000') 'StationAdministrativeArea'
    $expectedAreas = @('region:kr:hjd:1121576000','region:kr:hjd:1121577000','region:kr:hjd:1126054000','region:kr:hjd:1126057000')
    Require ((@($value.administrativeAreas.administrativeAreaStableId | Sort-Object) -join ',') -ceq ($expectedAreas -join ',')) 'AdministrativeAreaSet'
    $missingCodes = @($value.missingCoverage.code | Sort-Object -Unique)
    foreach ($code in @('BuildingRightsConflictUnresolved','LegalDongBoundaryCoverageUnverified','OsmRelationMembersMissing','RoadWidthUnavailable','SurfaceCoverageIncomplete','TraversalAuthorityUnavailable')) {
        Require ($missingCodes -contains $code) "MissingCoverageCode:$code"
    }

    $value.stationName = 'tampered'
    [IO.File]::WriteAllText($tampered, (($value | ConvertTo-Json -Depth 100) + "`n"), [Text.UTF8Encoding]::new($false))
    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $tamperText = & $PythonExecutable -B $auditor --snapshot $tampered --audit-output $tamperedAudit 2>&1
        $tamperExitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $saved }
    Require ($tamperExitCode -eq 1 -and (($tamperText | Out-String) -like '*contentHashMismatch*')) 'TamperRejection'
    Require (-not [IO.File]::Exists($tamperedAudit)) 'TamperedAuditCreated'

    if ($RetainVerifiedArtifacts) {
        $finalDirectory = Join-Path $repositoryRoot 'artifacts/local/yongmasan-station-spatial-snapshot'
        [IO.Directory]::CreateDirectory($finalDirectory) | Out-Null
        [IO.File]::Copy($snapshot, (Join-Path $finalDirectory 'private-review.json'), $true)
        [IO.File]::Copy($audit, (Join-Path $finalDirectory 'coverage-audit.json'), $true)
    }
    Write-Output "PASS deterministic frozen-source build + audit + tamper rejection; buildings=$($value.buildings.Count); roads=$($value.roads.Count); surfaces=$($value.surfaces.Count); contentHash=$($first.contentHash)"
}
finally {
    foreach ($path in $ownedFiles) { if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) } }
    $env:PYTHONPATH = $previousPythonPath
    $env:PYTHONDONTWRITEBYTECODE = $previousNoBytecode
}
