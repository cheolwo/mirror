[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestPath = Join-Path $repositoryRoot 'eng/world-seedbeds/station-landmarks/sagajeong-market.collection.r1.json'
$planPath = Join-Path $repositoryRoot 'docs/AI/Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-market-landmark-collection.r6.md'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "StationLandmarkSourceCollectionTest:$code" }
}

Require (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'ManifestMissing'
Require (Test-Path -LiteralPath $planPath -PathType Leaf) 'PlanMissing'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require ($manifest.schemaVersion -ceq 'ssalddel.station-landmark-source-collection.v1') 'SchemaVersion'
Require ($manifest.revision -ceq 'sagajeong-market-landmark-collection.r1') 'Revision'
Require ($manifest.status -ceq 'CollectionSpecificationApproved') 'Status'
Require ($manifest.planRef -ceq 'docs/AI/Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-market-landmark-collection.r6.md') 'PlanReference'

Require ($manifest.candidate.candidateStableId -ceq 'landmark-candidate:kr:seoul:jungnang:sagajeong-market.r1') 'CandidateStableId'
Require ($null -eq $manifest.candidate.canonicalLandmarkStableId) 'CanonicalIdentityWasPrematurelyAssigned'
Require ($manifest.candidate.identityStatus -ceq 'PendingHumanReview') 'IdentityStatus'
$stationRelation = @($manifest.candidate.relations | Where-Object relationCode -ceq 'CandidateLandmarkFor')
Require ($stationRelation.Count -eq 1 -and $stationRelation[0].targetStableId -ceq 'station:kr:kric:s1107:0722') 'StationRelation'

$evidence = $manifest.existingEvidence
Require ($evidence.marketObservation.recordSha256 -ceq '4a67972edd070cfa886e12d5575ad469d2e42aa2c6ecbff0ec6423390b466a07') 'MarketRecordHash'
Require ($evidence.marketObservation.rawSha256 -ceq '13ffd04a946ec7eec28222c8c3762f2e77aab919cd250888cdaaf7f51cce303b') 'MarketRawHash'
Require ($evidence.referencePoint.geometryStatus -ceq 'ReferencePointNotEntranceOrFootprint') 'ReferenceGeometryBoundary'
Require (-not $evidence.addressBuildingCandidate.provesMarketFootprint -and -not $evidence.addressBuildingCandidate.provesEntrance -and -not $evidence.addressBuildingCandidate.provesCurrentOperation) 'AddressCandidateOverclaim'

$requiredModules = @('MarketEntranceThreshold', 'RepresentativeMarketAlley')
foreach ($module in $requiredModules) {
    Require (@($manifest.collectionScope.modules) -contains $module) "MissingModule:$module"
}
$requiredAngles = @(
    'ApproachRoadToEntranceFront',
    'StationToMarketApproachSequence',
    'EntranceToAlleyAxis',
    'AlleyCrossSection',
    'BranchIntersectionAndTermination',
    'RooflineCanopyAndAdjacentBuildingSilhouette'
)
foreach ($angle in $requiredAngles) {
    Require (@($manifest.collectionScope.requiredCaptureAngles) -contains $angle) "MissingCaptureAngle:$angle"
}

Require ($manifest.rightsPolicy.datasetLicenseDoesNotGrantImageRights) 'DatasetImageRightsSeparated'
Require ($manifest.rightsPolicy.imageAcquisitionStatus -ceq 'NotStarted') 'ImageAcquisitionWasPrematurelyClaimed'
Require ($manifest.handoffGates.blender -ceq 'Blocked' -and -not $manifest.blenderBrief.workAuthorized) 'BlenderGate'
Require ($manifest.handoffGates.unity -ceq 'Blocked') 'UnityGate'

$boundary = $manifest.placementBoundary
Require (-not $boundary.graphMapRelationshipReady -and -not $boundary.placementMapGeometryReady) 'MapReadinessOverclaim'
Require (-not $boundary.referencePointMayBeUsedAsApprovedEntrance -and -not $boundary.addressBuildingMayBeUsedAsApprovedFootprint) 'PlacementAuthorityLeak'
Require ($boundary.observationPresentationOnly -and -not $boundary.traversalReady -and -not $boundary.gameplayReady -and -not $boundary.distributionApproved -and -not $boundary.sceneReady) 'AuthorityBoundary'
Require (-not $manifest.sponsorshipBoundary.mayChangeLandmarkSelection -and -not $manifest.sponsorshipBoundary.mayChangeGeometryOrPlacement -and -not $manifest.sponsorshipBoundary.mayChangePresentationScale) 'SponsorshipAuthorityLeak'
Require (-not $manifest.privacyBoundary.containsPrivateResidentData -and -not $manifest.privacyBoundary.containsPersonalContactData -and -not $manifest.privacyBoundary.containsPreciseActorLocation) 'PrivacyBoundary'

$planText = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8
foreach ($requiredText in @('ReferencePointNotEntranceOrFootprint', 'UniqueAddressCandidateNotVerifiedOccupancy', 'BlenderWorkBlocked', 'UnityPlacementBlocked')) {
    Require ($planText.Contains($requiredText)) "PlanBoundaryMissing:$requiredText"
}

Write-Output 'PASS station landmark source collection specification; candidate identity, evidence lineage, capture packet, rights, Blender, Unity and sponsorship gates verified'
