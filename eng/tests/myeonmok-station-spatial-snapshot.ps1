[CmdletBinding()]
param(
    [string] $PythonExecutable = '',
    [switch] $SkipFrozenSourceRun,
    [switch] $RetainVerifiedArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$builder = Join-Path $repositoryRoot 'eng/neighborhood/build-myeonmok-station-spatial-snapshot.py'
$auditor = Join-Path $repositoryRoot 'eng/neighborhood/audit-myeonmok-station-spatial-snapshot.py'
$acquisition = Join-Path $repositoryRoot 'eng/neighborhood/acquire-myeonmok-station-osm.ps1'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "MyeonmokStationSpatialSnapshotTest:$code" }
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

$testDirectory = Join-Path $repositoryRoot 'artifacts/local/validation/myeonmok-station-spatial-snapshot-tests'
[IO.Directory]::CreateDirectory($testDirectory) | Out-Null
$ownedFiles = [Collections.Generic.List[string]]::new()
$previousPythonPath = $env:PYTHONPATH
$previousNoBytecode = $env:PYTHONDONTWRITEBYTECODE
$env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
    $gisRuntime
} else {
    "$gisRuntime$([IO.Path]::PathSeparator)$previousPythonPath"
}
$env:PYTHONDONTWRITEBYTECODE = '1'
try {
    $builderSelfTest = & $PythonExecutable -B $builder --self-test | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $builderSelfTest.passed -and $builderSelfTest.checks -ge 10) 'BuilderSelfTest'
    $auditorSelfTest = & $PythonExecutable -B $auditor --self-test | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $auditorSelfTest.passed -and $auditorSelfTest.checks -ge 10) 'AuditorSelfTest'

    $frozenFirst = & $acquisition -VerifyOnly | ConvertFrom-Json
    $frozenSecond = & $acquisition -VerifyOnly | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and -not $frozenFirst.networkRequested -and -not $frozenSecond.networkRequested) 'OsmSourceWasReRequested'
    Require ($frozenFirst.sha256 -ceq 'BD218CB2D9CC49F996DB7BCF2384F6950CCEAC772D5322987879AA21570D0A66') 'OsmHashChanged'
    Require ([int64]$frozenFirst.byteLength -eq 1503543) 'OsmLengthChanged'

    $savedErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $outsideText = & $PythonExecutable -B $builder --output 'eng/neighborhood/forbidden.json' 2>&1
        $outsideExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $savedErrorPreference
    }
    Require ($outsideExitCode -eq 1) 'OutputOutsideArtifactsLocalAccepted'
    Require (($outsideText | Out-String) -like '*OutputMustRemainUnderArtifactsLocal*') 'OutputOutsideArtifactsLocalWrongFailure'
    Require (-not [IO.File]::Exists((Join-Path $repositoryRoot 'eng/neighborhood/forbidden.json'))) 'OutputOutsideArtifactsLocalCreated'

    $invalidOutput = Join-Path $testDirectory 'invalid-source.json'
    $ownedFiles.Add($invalidOutput)
    $ErrorActionPreference = 'Continue'
    try {
        $invalidText = & $PythonExecutable -B $builder `
            --building-zip 'artifacts/local/neighborhood-source-acquisition/myeonmok-station-0721-r1/map.osm' `
            --output $invalidOutput 2>&1
        $invalidExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $savedErrorPreference
    }
    Require ($invalidExitCode -eq 1) 'WrongBuildingSourceAccepted'
    Require (($invalidText | Out-String) -like '*BuildingZipLengthMismatch*') 'WrongBuildingSourceWrongFailure'
    Require (-not [IO.File]::Exists($invalidOutput)) 'WrongBuildingSourceCreatedOutput'

    if ($SkipFrozenSourceRun) {
        Write-Output 'PASS self-tests, frozen OSM reuse, output boundary and wrong-source rejection; full frozen-source run skipped explicitly'
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
    $firstContentHash = $first.contentHash
    $second = & $PythonExecutable -B $builder --output $snapshot | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0) 'SecondBuildFailed'
    Require ($second.contentHash -ceq $firstContentHash) 'ContentHashNotDeterministic'
    Require ((Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash -ceq $firstFileHash) 'SnapshotBytesNotDeterministic'

    $auditResult = & $PythonExecutable -B $auditor --snapshot $snapshot --audit-output $audit | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $auditResult.status -ceq 'PassedWithMissingCoverage') 'AuditFailed'
    $snapshotValue = Get-Content -LiteralPath $snapshot -Raw -Encoding UTF8 | ConvertFrom-Json
    $auditValue = Get-Content -LiteralPath $audit -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($snapshotValue.schemaVersion -ceq 'ssalddel.station-spatial-snapshot.v1') 'SchemaVersion'
    Require ($snapshotValue.revision -ceq 'myeonmok-station-spatial-snapshot.private-review.r1') 'Revision'
    Require ($snapshotValue.status -ceq 'LocalPrivateReview') 'Status'
    Require ($snapshotValue.transitStationStableId -ceq 'station:kr:kric:s1107:0721') 'StationStableId'
    Require ($snapshotValue.coordinateFrame.originLatitude -eq 37.588671 -and $snapshotValue.coordinateFrame.originLongitude -eq 127.087503) 'StationAnchor'
    Require ($snapshotValue.coordinateFrame.halfExtentMeters -eq 500 -and $snapshotValue.window.widthMeters -eq 1000 -and $snapshotValue.window.depthMeters -eq 1000) 'Window'
    Require ($snapshotValue.presentationOnly -and -not $snapshotValue.distributionApproved -and -not $snapshotValue.gameplayReady -and -not $snapshotValue.traversalReady) 'AuthorityBoundary'
    Require ($snapshotValue.buildings.Count -eq 4165) 'FrozenBuildingCount'
    Require (@($snapshotValue.buildings | Where-Object legalDongCode -eq '1126010100').Count -eq 3982) 'MyeonmokLegalBuildingCount'
    Require (@($snapshotValue.buildings | Where-Object legalDongCode -eq '1126010200').Count -eq 183) 'SangbongLegalBuildingCount'
    Require (@($snapshotValue.buildings | Where-Object heightKind -eq 'ObservedSourceHeight').Count -eq 1616) 'ObservedHeightCount'
    Require (@($snapshotValue.buildings | Where-Object heightKind -eq 'SymbolicFallback4m').Count -eq 2549) 'SymbolicHeightCount'
    Require ($snapshotValue.roads.Count -eq 124) 'FrozenRoadCount'
    Require (@($snapshotValue.roads | Where-Object traversalReady).Count -eq 0) 'RoadTraversalAuthorityLeak'
    Require ($snapshotValue.surfaces.Count -eq 48) 'FrozenSurfaceCount'
    Require ($snapshotValue.tiles.Count -eq 4) 'FrozenTileCount'
    Require ($snapshotValue.coverage.cells.Count -eq 100) 'CoverageCellCount'
    Require ($snapshotValue.coverage.summary.classificationCounts.ConfirmedBuilt -eq 98) 'ConfirmedBuiltCount'
    Require ($snapshotValue.coverage.summary.classificationCounts.MissingCoverage -eq 2) 'MissingCoverageCount'
    foreach ($cell in $snapshotValue.coverage.cells) {
        $sum = [double]$cell.buildingCoverageRatio + [double]$cell.openCoverageRatio + [double]$cell.unknownCoverageRatio
        Require ([Math]::Abs($sum - 1.0) -le 0.000002) "CoveragePartition:$($cell.id)"
    }
    $expectedAdministrativeAreas = @(
        'region:kr:hjd:1126052000',
        'region:kr:hjd:1126055000',
        'region:kr:hjd:1126056500',
        'region:kr:hjd:1126057500',
        'region:kr:hjd:1126059000',
        'region:kr:hjd:1126066000'
    )
    Require (
        (@($snapshotValue.administrativeAreas.administrativeAreaStableId | Sort-Object) -join ',') -ceq
        ($expectedAdministrativeAreas -join ',')
    ) 'AdministrativeAreaSet'
    $stationArea = @($snapshotValue.administrativeAreas | Where-Object containsStation)
    Require ($stationArea.Count -eq 1 -and $stationArea[0].administrativeAreaStableId -ceq 'region:kr:hjd:1126056500') 'StationAdministrativeArea'
    $missingCodes = @($snapshotValue.missingCoverage.code | Sort-Object -Unique)
    foreach ($code in @('BuildingRightsConflictUnresolved','LegalDongBoundaryCoverageUnverified','RoadWidthUnavailable','SurfaceCoverageIncomplete','TraversalAuthorityUnavailable')) {
        Require ($missingCodes -contains $code) "MissingCoverageCode:$code"
    }
    Require (-not ($missingCodes -contains 'OsmRelationMembersMissing')) 'FalseIncompleteRelationClaim'
    Require ($auditValue.snapshotContentHash -ceq $snapshotValue.contentHash) 'AuditSnapshotHash'

    $snapshotValue.stationName = 'tampered'
    [IO.File]::WriteAllText($tampered, (($snapshotValue | ConvertTo-Json -Depth 100) + "`n"), [Text.UTF8Encoding]::new($false))
    $ErrorActionPreference = 'Continue'
    try {
        $tamperText = & $PythonExecutable -B $auditor --snapshot $tampered --audit-output $tamperedAudit 2>&1
        $tamperExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $savedErrorPreference
    }
    Require ($tamperExitCode -eq 1) 'TamperedSnapshotAccepted'
    Require (($tamperText | Out-String) -like '*contentHashMismatch*') 'TamperedSnapshotWrongFailure'
    Require (-not [IO.File]::Exists($tamperedAudit)) 'TamperedSnapshotCreatedAudit'

    if ($RetainVerifiedArtifacts) {
        $finalDirectory = Join-Path $repositoryRoot 'artifacts/local/myeonmok-station-spatial-snapshot'
        [IO.Directory]::CreateDirectory($finalDirectory) | Out-Null
        [IO.File]::Copy($snapshot, (Join-Path $finalDirectory 'private-review.json'), $true)
        [IO.File]::Copy($audit, (Join-Path $finalDirectory 'coverage-audit.json'), $true)
    }

    Write-Output "PASS actual frozen-source build + deterministic bytes + full rebuild audit + invalid/tamper rejection; buildings=$($snapshotValue.buildings.Count); roads=$($snapshotValue.roads.Count); surfaces=$($snapshotValue.surfaces.Count); contentHash=$firstContentHash"
}
finally {
    foreach ($path in $ownedFiles) {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }
    $env:PYTHONPATH = $previousPythonPath
    $env:PYTHONDONTWRITEBYTECODE = $previousNoBytecode
}
