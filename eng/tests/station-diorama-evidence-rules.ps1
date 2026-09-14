[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$managerPath = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-station-diorama-evidence-rules.ps1'
$catalogPath = Join-Path $repositoryRoot 'eng/execution-ledgers/station-diorama-evidence-rules.json'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "StationDioramaEvidenceRulesTest:$code" }
}

function Assert-CatalogTamperRejected([string] $name, [scriptblock] $mutate, [string] $expectedCode) {
    $temporaryPath = [System.IO.Path]::Combine(
        [System.IO.Path]::GetTempPath(),
        "station-diorama-evidence-$([Guid]::NewGuid().ToString('N')).json")
    try {
        $tampered = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
        & $mutate $tampered
        $tampered | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $temporaryPath -Encoding utf8

        $rejected = $false
        try {
            & $managerPath -Mode Validate -CatalogPath $temporaryPath | Out-Null
        }
        catch {
            $rejected = $_.Exception.Message.IndexOf("StationDioramaEvidenceRulesInvalid:$expectedCode", [StringComparison]::Ordinal) -ge 0
        }
        Require $rejected "TamperNotRejected:$name`:$expectedCode"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

Require (Test-Path -LiteralPath $managerPath -PathType Leaf) 'ManagerMissing'
Require (Test-Path -LiteralPath $catalogPath -PathType Leaf) 'CatalogMissing'

$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$architecturePath = Join-Path $repositoryRoot ([string] $catalog.policyRef)
$planPath = Join-Path $repositoryRoot ([string] $catalog.planRef)
Require (Test-Path -LiteralPath $architecturePath -PathType Leaf) 'ArchitectureMissing'
Require (Test-Path -LiteralPath $planPath -PathType Leaf) 'PlanMissing'

$validation = & $managerPath -Mode Validate
Require ([string] $validation -ceq 'StationDioramaEvidenceRulesValid:Revision=station-diorama-evidence-rules.r3;Sources=10;Rules=12;Candidates=12;Provisional=0;Accepted=0;Stations=3;SpatialChecks=9;Collected=2;Missing=1;Unassessed=6') 'ValidationSummary'

Require (@($catalog.rules).Count -eq 12) 'CandidateCount'
Require (@($catalog.rules | Where-Object status -ne 'Candidate').Count -eq 0) 'PrematureRulePromotion'
Require (@($catalog.rules | Where-Object applicationAuthorized).Count -eq 0) 'PrematureRuleApplication'
Require (@($catalog.stationEvidenceProfiles | Where-Object { @($_.acceptedRuleRefs).Count -gt 0 }).Count -eq 0) 'PrematureStationApplication'

$requiredRuleIds = @(
    'diorama-rule:station-window-is-view-not-authority.v1',
    'diorama-rule:missing-coverage-remains-explicit.v1',
    'diorama-rule:source-simulation-presentation-separated.v1',
    'diorama-rule:station-profile-owns-specific-values.v1',
    'diorama-rule:cross-station-fallback-forbidden.v1',
    'diorama-rule:presentation-toggle-does-not-mutate-authority.v1',
    'diorama-rule:public-data-does-not-identify-real-actors.v1',
    'diorama-rule:game-view-is-presentation-evidence-only.v1',
    'diorama-rule:preserve-procedural-mass-until-visual-evidence-ready.v1',
    'diorama-rule:historical-landmark-identity-is-versioned-candidate.v1',
    'diorama-rule:numbered-station-exits-are-profile-owned-orientation-anchors.v1',
    'diorama-rule:mixed-spatial-layers-preserve-source-time-crs-and-authority.v1'
)
Require ((@($catalog.rules.ruleStableId | Sort-Object) -join ',') -ceq (@($requiredRuleIds | Sort-Object) -join ',')) 'CurrentRuleSet'

$requiredSpatialKinds = @('RoadAddress', 'ParcelIdentifier', 'ParcelGeometry')
Require ((@($catalog.reviewPolicy.requiredSpatialEvidenceKinds | Sort-Object) -join ',') -ceq (@($requiredSpatialKinds | Sort-Object) -join ',')) 'SpatialEvidenceKinds'
Require ([bool] $catalog.reviewPolicy.collectionStateDoesNotAuthorizeUsage) 'CollectionUsageSeparation'
Require (@($catalog.stationEvidenceProfiles.spatialFoundationEvidenceChecks | Where-Object applicationAuthorized).Count -eq 0) 'SpatialEvidenceApplicationAuthorized'

$requiredSourceIds = @(
    'source-receipt:station-diorama:sagajeong:mois-road-address-building-db.202608',
    'source-receipt:station-diorama:sagajeong:vworld-gis-building-al-d010.20260809',
    'source-receipt:station-diorama:sagajeong:continuous-parcel-metadata.r1'
)
foreach ($sourceId in $requiredSourceIds) {
    Require (@($catalog.sourceReceipts | Where-Object sourceReceiptStableId -ceq $sourceId).Count -eq 1) "SpatialSourceReceipt:$sourceId"
}

$sagajeong = @($catalog.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0722')[0]
$sagajeongRoadAddress = @($sagajeong.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'RoadAddress')[0]
Require ([string] $sagajeongRoadAddress.collectionState -ceq 'Collected') 'SagajeongRoadAddressCollection'
Require ([string] $sagajeongRoadAddress.coverageState -ceq 'Complete') 'SagajeongRoadAddressCoverage'
Require ([string] $sagajeongRoadAddress.usageState -ceq 'PrivateReviewOnly') 'SagajeongRoadAddressUsage'
Require ([long] $sagajeongRoadAddress.subjectCount -eq 4062 -and [long] $sagajeongRoadAddress.statusLedgerCount -eq 4062) 'SagajeongRoadAddressPopulation'
Require ([long] $sagajeongRoadAddress.statusBreakdown.SingleRawParcelAddressCandidate -eq 3796) 'SagajeongRoadAddressUniqueCandidates'
Require ([long] $sagajeongRoadAddress.statusBreakdown.MultipleRawParcelAddressCandidates -eq 60) 'SagajeongRoadAddressMultipleCandidates'
Require ([long] $sagajeongRoadAddress.statusBreakdown.NoRawParcelAddressCandidate -eq 206) 'SagajeongRoadAddressMissingCandidates'

$sagajeongParcelIdentifier = @($sagajeong.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'ParcelIdentifier')[0]
Require ([string] $sagajeongParcelIdentifier.collectionState -ceq 'Collected') 'SagajeongParcelIdentifierCollection'
Require ([string] $sagajeongParcelIdentifier.coverageState -ceq 'Complete') 'SagajeongParcelIdentifierCoverage'
Require ([long] $sagajeongParcelIdentifier.subjectCount -eq 4062 -and [long] $sagajeongParcelIdentifier.evidenceValueSubjectCount -eq 4062) 'SagajeongParcelIdentifierPopulation'
Require ([long] $sagajeongParcelIdentifier.distinctEvidenceValueCount -eq 3774) 'SagajeongUniqueParcelIdentifiers'

$sagajeongParcelGeometry = @($sagajeong.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'ParcelGeometry')[0]
Require ([string] $sagajeongParcelGeometry.collectionState -ceq 'NotCollected') 'SagajeongParcelGeometryCollection'
Require ([string] $sagajeongParcelGeometry.coverageState -ceq 'Missing') 'SagajeongParcelGeometryCoverage'
Require ([string] $sagajeongParcelGeometry.usageState -ceq 'Blocked') 'SagajeongParcelGeometryUsage'
Require ([long] $sagajeongParcelGeometry.subjectCount -eq 3774 -and [long] $sagajeongParcelGeometry.missingEvidenceSubjectCount -eq 3774) 'SagajeongParcelGeometryMissing'
Require (@($sagajeongParcelGeometry.limitations | Where-Object { ([string] $_).Contains('BlockedExternalAccess') }).Count -eq 1) 'SagajeongParcelGeometryAccessBlocker'

foreach ($stationId in @('station:kr:kric:s1107:0721', 'station:kr:kric:s1107:0723')) {
    $station = @($catalog.stationEvidenceProfiles | Where-Object stationStableId -ceq $stationId)[0]
    Require (@($station.spatialFoundationEvidenceChecks).Count -eq 3) "UnassessedKindCount:$stationId"
    Require (@($station.spatialFoundationEvidenceChecks | Where-Object collectionState -cne 'NotAssessed').Count -eq 0) "UnassessedCollection:$stationId"
    Require (@($station.spatialFoundationEvidenceChecks | Where-Object coverageState -cne 'Unassessed').Count -eq 0) "UnassessedCoverage:$stationId"
    Require (@($station.spatialFoundationEvidenceChecks | Where-Object usageState -cne 'Unassessed').Count -eq 0) "UnassessedUsage:$stationId"
}

Assert-CatalogTamperRejected 'missing-kind' {
    param($copy)
    $profile = @($copy.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0722')[0]
    $profile.spatialFoundationEvidenceChecks = @($profile.spatialFoundationEvidenceChecks | Where-Object evidenceKind -cne 'ParcelGeometry')
} 'SpatialEvidenceCheckCount:station:kr:kric:s1107:0722'

Assert-CatalogTamperRejected 'collected-does-not-authorize-usage' {
    param($copy)
    $profile = @($copy.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0722')[0]
    @($profile.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'RoadAddress')[0].usageState = 'Blocked'
} 'SpatialCollectedUsageBoundary:station:kr:kric:s1107:0722:RoadAddress'

Assert-CatalogTamperRejected 'spatial-check-does-not-authorize-application' {
    param($copy)
    $profile = @($copy.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0722')[0]
    @($profile.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'RoadAddress')[0].applicationAuthorized = $true
} 'SpatialApplicationAuthorized:station:kr:kric:s1107:0722:RoadAddress'

Assert-CatalogTamperRejected 'parcel-count-partition' {
    param($copy)
    $profile = @($copy.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0722')[0]
    @($profile.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'ParcelIdentifier')[0].missingEvidenceSubjectCount = 1
} 'SpatialEvidencePartitionMismatch:station:kr:kric:s1107:0722:ParcelIdentifier'

Assert-CatalogTamperRejected 'missing-geometry-promoted' {
    param($copy)
    $profile = @($copy.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0722')[0]
    @($profile.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'ParcelGeometry')[0].collectionState = 'Collected'
} 'SpatialCollectedCoverage:station:kr:kric:s1107:0722:ParcelGeometry'

Assert-CatalogTamperRejected 'unassessed-count-invented' {
    param($copy)
    $profile = @($copy.stationEvidenceProfiles | Where-Object stationStableId -ceq 'station:kr:kric:s1107:0721')[0]
    @($profile.spatialFoundationEvidenceChecks | Where-Object evidenceKind -ceq 'RoadAddress')[0].subjectCount = 1
} 'SpatialNotAssessedCountUnexpected:station:kr:kric:s1107:0721:RoadAddress:subjectCount'

$architectureText = Get-Content -LiteralPath $architecturePath -Raw -Encoding UTF8
foreach ($requiredText in @('Candidate', 'ProvisionalSharedRule', 'AcceptedSharedRule', 'E1', 'E10', 'CrossStationConformance', 'RoadAddress', 'ParcelIdentifier', 'ParcelGeometry', 'NotAssessed')) {
    Require ($architectureText.Contains($requiredText)) "ArchitectureBoundaryMissing:$requiredText"
}

$planText = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8
foreach ($requiredText in @('applicationAuthorized=false', 'commit', 'push')) {
    Require ($planText.Contains($requiredText)) "PlanBoundaryMissing:$requiredText"
}

Write-Output 'PASS station diorama evidence evolution; source links, address and parcel collection/usage separation, missing parcel geometry, tamper rejection, human promotion gate and E-stage non-promotion verified'
