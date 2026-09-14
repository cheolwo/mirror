[CmdletBinding()]
param(
    [ValidateSet('Validate', 'Summary')]
    [string] $Mode = 'Validate',
    [string] $CatalogPath = 'eng/execution-ledgers/station-diorama-evidence-rules.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Resolve-RepositoryPath([string] $path) {
    if ([System.IO.Path]::IsPathRooted($path)) { return $path }
    return Join-Path $repositoryRoot $path
}

function Require($condition, [string] $code) {
    if (-not $condition) { throw "StationDioramaEvidenceRulesInvalid:$code" }
}

function Require-Unique([object[]] $values, [string] $code) {
    $textValues = @($values | ForEach-Object { [string] $_ })
    Require (@($textValues | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) "$code`:Blank"
    Require (@($textValues | Sort-Object -Unique).Count -eq $textValues.Count) "$code`:Duplicate"
}

function Require-RepositoryRefs([object[]] $references, [string] $owner) {
    foreach ($reference in @($references)) {
        $path = [string] $reference
        Require (-not [string]::IsNullOrWhiteSpace($path)) "RepositoryReferenceBlank:$owner"
        Require (-not [System.IO.Path]::IsPathRooted($path)) "RepositoryReferenceMustBeRelative:$owner`:$path"
        Require (Test-Path -LiteralPath (Resolve-RepositoryPath $path)) "RepositoryReferenceMissing:$owner`:$path"
    }
}

function Require-SafeOfficialLinks([object[]] $links, [string] $owner) {
    Require (@($links).Count -gt 0) "OfficialLinkMissing:$owner"
    foreach ($linkValue in @($links)) {
        $link = [string] $linkValue
        $uri = $null
        Require ([Uri]::TryCreate($link, [UriKind]::Absolute, [ref] $uri)) "OfficialLinkInvalid:$owner"
        Require ($uri.Scheme -ceq 'https') "OfficialLinkMustUseHttps:$owner"
        Require (-not [string]::IsNullOrWhiteSpace($uri.Host)) "OfficialLinkHostMissing:$owner"
        Require ($link -notmatch '(?i)(serviceKey|api[_-]?key|access[_-]?token|signature|secret)=') "OfficialLinkContainsSecret:$owner"
    }
}

$catalogFile = Resolve-RepositoryPath $CatalogPath
Require (Test-Path -LiteralPath $catalogFile -PathType Leaf) "CatalogMissing:$CatalogPath"
$catalog = Get-Content -LiteralPath $catalogFile -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $catalog.schemaVersion -ceq 'ssalddel.station-diorama-evidence-rule-catalog.v2') 'SchemaVersion'
Require ([string] $catalog.revision -ceq 'station-diorama-evidence-rules.r3') 'Revision'
Require-RepositoryRefs @($catalog.policyRef, $catalog.planRef, $catalog.evidenceStageCatalogRef) 'Catalog'

$policy = $catalog.reviewPolicy
Require ([bool] $policy.agentTaskClosureReviewRequired) 'TaskClosureReviewRequired'
Require ([bool] $policy.candidateRegistrationWithoutApprovalAllowed) 'CandidateRegistrationBoundary'
Require (-not [bool] $policy.candidateApplicationAuthorized) 'CandidateAutomaticApplicationForbidden'
Require ([bool] $policy.provisionalAndAcceptedRequireExplicitHumanApproval) 'HumanApprovalBoundary'
Require (-not [bool] $policy.evidenceStageAutomaticPromotionAllowed) 'EvidenceAutoPromotionForbidden'
Require ([bool] $policy.noCandidateReportRequiredWhenNoneFound) 'NoCandidateReportBoundary'
Require ([int] $policy.provisionalMinimumDistinctStations -ge 2) 'ProvisionalStationMinimum'
Require ([int] $policy.acceptedMinimumDistinctStations -ge 3) 'AcceptedStationMinimum'
Require ([bool] $policy.acceptedRequiresMissingCoverageOrCounterexampleReview) 'AcceptedCounterexampleReview'
Require ([bool] $policy.spatialFoundationEvidenceRequired) 'SpatialFoundationEvidenceRequired'
Require ([bool] $policy.collectionStateDoesNotAuthorizeUsage) 'CollectionUsageSeparationRequired'

$requiredSpatialEvidenceKinds = @('RoadAddress', 'ParcelIdentifier', 'ParcelGeometry')
$allowedCollectionStates = @('Collected', 'NotCollected', 'NotAssessed')
$allowedCoverageStates = @('Complete', 'Missing', 'Unassessed')
$allowedUsageStates = @('PrivateReviewOnly', 'Blocked', 'Unassessed')
Require ((@($policy.requiredSpatialEvidenceKinds | ForEach-Object { [string] $_ } | Sort-Object) -join ',') -ceq (($requiredSpatialEvidenceKinds | Sort-Object) -join ',')) 'SpatialEvidenceKinds'
Require ((@($policy.allowedCollectionStates | ForEach-Object { [string] $_ } | Sort-Object) -join ',') -ceq (($allowedCollectionStates | Sort-Object) -join ',')) 'CollectionStates'
Require ((@($policy.allowedCoverageStates | ForEach-Object { [string] $_ } | Sort-Object) -join ',') -ceq (($allowedCoverageStates | Sort-Object) -join ',')) 'CoverageStates'
Require ((@($policy.allowedUsageStates | ForEach-Object { [string] $_ } | Sort-Object) -join ',') -ceq (($allowedUsageStates | Sort-Object) -join ',')) 'UsageStates'

$stageCatalog = Get-Content -LiteralPath (Resolve-RepositoryPath ([string] $catalog.evidenceStageCatalogRef)) -Raw -Encoding UTF8 | ConvertFrom-Json
$allowedEvidenceStages = @($stageCatalog.stages.code | ForEach-Object { [string] $_ })
Require (($allowedEvidenceStages -join ',') -ceq 'E0,E1,E2,E3,E4,E5,E6,E7,E8,E9,E10') 'EvidenceStageCatalogBoundary'

$sourceReceipts = @($catalog.sourceReceipts)
$rules = @($catalog.rules)
$stationProfiles = @($catalog.stationEvidenceProfiles)
Require ($sourceReceipts.Count -gt 0) 'SourceReceiptsEmpty'
Require ($rules.Count -gt 0) 'RulesEmpty'
Require ($stationProfiles.Count -gt 0) 'StationProfilesEmpty'

$sourceIds = @($sourceReceipts | ForEach-Object { [string] $_.sourceReceiptStableId })
$ruleIds = @($rules | ForEach-Object { [string] $_.ruleStableId })
$stationIds = @($stationProfiles | ForEach-Object { [string] $_.stationStableId })
Require-Unique $sourceIds 'SourceReceiptStableId'
Require-Unique $ruleIds 'RuleStableId'
Require-Unique $stationIds 'StationStableId'

$allowedRightsStatuses = @('Accepted', 'ReferenceOnly', 'PendingHumanReview', 'MixedPendingHumanReview', 'Blocked', 'Unavailable')
foreach ($receipt in $sourceReceipts) {
    $receiptId = [string] $receipt.sourceReceiptStableId
    Require (@($receipt.subjectStationStableIds).Count -gt 0) "SourceStationMissing:$receiptId"
    Require (-not [string]::IsNullOrWhiteSpace([string] $receipt.provider)) "SourceProviderMissing:$receiptId"
    Require (-not [string]::IsNullOrWhiteSpace([string] $receipt.datasetId)) "SourceDatasetMissing:$receiptId"
    Require ([string] $receipt.rightsStatus -cin $allowedRightsStatuses) "SourceRightsStatus:$receiptId"
    Require-SafeOfficialLinks @($receipt.officialLinks) $receiptId
    Require-RepositoryRefs @($receipt.repositoryEvidenceRefs) $receiptId
    Require (@($receipt.limitations).Count -gt 0) "SourceLimitationMissing:$receiptId"
    foreach ($hashProperty in @('rawSha256', 'receiptSha256')) {
        if ($receipt.PSObject.Properties.Name -contains $hashProperty) {
            $hash = [string] $receipt.$hashProperty
            if (-not [string]::IsNullOrWhiteSpace($hash)) {
                Require ($hash -cmatch '^[0-9A-F]{64}$') "SourceHashFormat:$receiptId`:$hashProperty"
            }
        }
    }
}

$allowedRuleStatuses = @('Observed', 'Candidate', 'ProvisionalSharedRule', 'AcceptedSharedRule', 'StationException', 'Superseded', 'Rejected')
$allowedDiscoveryKinds = @('ImplementedObservation', 'CrossStationObservation', 'EvidenceBoundary', 'PlanningConstraint', 'SagajeongObservation')
foreach ($rule in $rules) {
    $ruleId = [string] $rule.ruleStableId
    $status = [string] $rule.status
    Require ($status -cin $allowedRuleStatuses) "RuleStatus:$ruleId"
    Require ([string] $rule.discoveryKind -cin $allowedDiscoveryKinds) "RuleDiscoveryKind:$ruleId"
    Require (-not [string]::IsNullOrWhiteSpace([string] $rule.statement)) "RuleStatementMissing:$ruleId"
    Require (@($rule.observedStationStableIds).Count -gt 0) "RuleStationMissing:$ruleId"
    Require-Unique @($rule.observedStationStableIds) "RuleStation:$ruleId"
    foreach ($sourceRef in @($rule.sourceReceiptRefs)) {
        Require ($sourceIds -ccontains [string] $sourceRef) "RuleSourceReferenceMissing:$ruleId`:$sourceRef"
    }
    Require-RepositoryRefs @($rule.repositoryEvidenceRefs) $ruleId
    Require (@($rule.evidenceStageImpacts).Count -gt 0) "RuleEvidenceImpactMissing:$ruleId"
    foreach ($stage in @($rule.evidenceStageImpacts)) {
        Require ($allowedEvidenceStages -ccontains [string] $stage) "RuleEvidenceStageInvalid:$ruleId`:$stage"
    }
    Require (@($rule.invalidationTriggers).Count -gt 0) "RuleInvalidationTriggerMissing:$ruleId"

    if ($status -cin @('Observed', 'Candidate')) {
        Require (-not [bool] $rule.applicationAuthorized) "CandidateApplicationAuthorized:$ruleId"
        Require ($null -eq $rule.humanApproval) "CandidateHumanApprovalUnexpected:$ruleId"
    }
    if ($status -cin @('ProvisionalSharedRule', 'AcceptedSharedRule')) {
        Require ([bool] $rule.applicationAuthorized) "SharedRuleApplicationNotAuthorized:$ruleId"
        Require ($null -ne $rule.humanApproval) "SharedRuleHumanApprovalMissing:$ruleId"
        Require ([bool] $rule.humanApproval.approved) "SharedRuleHumanApprovalInvalid:$ruleId"
        Require (-not [string]::IsNullOrWhiteSpace([string] $rule.humanApproval.approvalRef)) "SharedRuleApprovalRefMissing:$ruleId"
    }
    if ($status -ceq 'ProvisionalSharedRule') {
        Require (@($rule.observedStationStableIds).Count -ge [int] $policy.provisionalMinimumDistinctStations) "ProvisionalStationEvidenceInsufficient:$ruleId"
    }
    if ($status -ceq 'AcceptedSharedRule') {
        Require (@($rule.observedStationStableIds).Count -ge [int] $policy.acceptedMinimumDistinctStations) "AcceptedStationEvidenceInsufficient:$ruleId"
        Require (@($rule.counterexamplesOrExceptions).Count -gt 0) "AcceptedCounterexampleReviewMissing:$ruleId"
    }
}

$referenceStations = @($stationProfiles | Where-Object role -ceq 'ReferenceStation')
Require ($referenceStations.Count -eq 1) 'ReferenceStationCount'
Require ([string] $referenceStations[0].stationStableId -ceq 'station:kr:kric:s1107:0722') 'SagajeongReferenceStation'
foreach ($profile in $stationProfiles) {
    $stationId = [string] $profile.stationStableId
    foreach ($sourceRef in @($profile.sourceReceiptRefs)) {
        Require ($sourceIds -ccontains [string] $sourceRef) "StationSourceReferenceMissing:$stationId`:$sourceRef"
    }
    foreach ($ruleRef in @($profile.observedRuleRefs)) {
        Require ($ruleIds -ccontains [string] $ruleRef) "StationObservedRuleMissing:$stationId`:$ruleRef"
    }
    foreach ($ruleRef in @($profile.acceptedRuleRefs)) {
        $matchedRule = @($rules | Where-Object ruleStableId -ceq [string] $ruleRef)
        Require ($matchedRule.Count -eq 1 -and [string] $matchedRule[0].status -ceq 'AcceptedSharedRule') "StationAcceptedRuleNotAccepted:$stationId`:$ruleRef"
    }
    Require-RepositoryRefs @($profile.repositoryEvidenceRefs) $stationId
    $spatialChecks = @($profile.spatialFoundationEvidenceChecks)
    Require ($spatialChecks.Count -eq $requiredSpatialEvidenceKinds.Count) "SpatialEvidenceCheckCount:$stationId"
    $spatialKinds = @($spatialChecks | ForEach-Object { [string] $_.evidenceKind })
    Require-Unique $spatialKinds "SpatialEvidenceKind:$stationId"
    Require ((@($spatialKinds | Sort-Object) -join ',') -ceq (($requiredSpatialEvidenceKinds | Sort-Object) -join ',')) "SpatialEvidenceKindSet:$stationId"

    foreach ($check in $spatialChecks) {
        $kind = [string] $check.evidenceKind
        $owner = "$stationId`:$kind"
        $collectionState = [string] $check.collectionState
        $coverageState = [string] $check.coverageState
        $usageState = [string] $check.usageState
        Require ($allowedCollectionStates -ccontains $collectionState) "SpatialCollectionState:$owner"
        Require ($allowedCoverageStates -ccontains $coverageState) "SpatialCoverageState:$owner"
        Require ($allowedUsageStates -ccontains $usageState) "SpatialUsageState:$owner"
        Require ($null -ne $check.PSObject.Properties['applicationAuthorized']) "SpatialApplicationAuthorizationMissing:$owner"
        Require (-not [bool] $check.applicationAuthorized) "SpatialApplicationAuthorized:$owner"
        Require (-not [string]::IsNullOrWhiteSpace([string] $check.subjectKind)) "SpatialSubjectKindMissing:$owner"
        Require-RepositoryRefs @($check.repositoryEvidenceRefs) $owner
        Require (@($check.limitations).Count -gt 0) "SpatialLimitationMissing:$owner"

        foreach ($sourceRef in @($check.sourceReceiptRefs)) {
            Require ($sourceIds -ccontains [string] $sourceRef) "SpatialSourceReferenceMissing:$owner`:$sourceRef"
            Require (@($profile.sourceReceiptRefs) -ccontains [string] $sourceRef) "SpatialSourceNotOwnedByStation:$owner`:$sourceRef"
        }

        if ($collectionState -ceq 'NotAssessed') {
            Require ($coverageState -ceq 'Unassessed') "SpatialNotAssessedCoverage:$owner"
            Require ($usageState -ceq 'Unassessed') "SpatialNotAssessedUsage:$owner"
            Require (@($check.sourceReceiptRefs).Count -eq 0) "SpatialNotAssessedSourceUnexpected:$owner"
            foreach ($countProperty in @('subjectCount', 'statusLedgerCount', 'evidenceValueSubjectCount', 'missingEvidenceSubjectCount', 'distinctEvidenceValueCount')) {
                Require ($null -eq $check.$countProperty) "SpatialNotAssessedCountUnexpected:$owner`:$countProperty"
            }
            Require (@($check.statusBreakdown.PSObject.Properties).Count -eq 0) "SpatialNotAssessedBreakdownUnexpected:$owner"
            continue
        }

        Require (@($check.sourceReceiptRefs).Count -gt 0) "SpatialSourceMissing:$owner"
        foreach ($countProperty in @('subjectCount', 'statusLedgerCount', 'evidenceValueSubjectCount', 'missingEvidenceSubjectCount')) {
            Require ($null -ne $check.$countProperty) "SpatialCountMissing:$owner`:$countProperty"
            $countValue = [long] $check.$countProperty
            Require ($countValue -ge 0) "SpatialCountNegative:$owner`:$countProperty"
        }
        $subjectCount = [long] $check.subjectCount
        $statusLedgerCount = [long] $check.statusLedgerCount
        $evidenceValueSubjectCount = [long] $check.evidenceValueSubjectCount
        $missingEvidenceSubjectCount = [long] $check.missingEvidenceSubjectCount
        Require ($subjectCount -gt 0) "SpatialSubjectCountEmpty:$owner"
        Require ($statusLedgerCount -eq $subjectCount) "SpatialStatusLedgerIncomplete:$owner"
        Require (($evidenceValueSubjectCount + $missingEvidenceSubjectCount) -eq $subjectCount) "SpatialEvidencePartitionMismatch:$owner"
        if ($null -ne $check.distinctEvidenceValueCount) {
            $distinctCount = [long] $check.distinctEvidenceValueCount
            Require ($distinctCount -ge 0 -and $distinctCount -le $evidenceValueSubjectCount) "SpatialDistinctEvidenceCount:$owner"
        }
        $breakdownTotal = 0L
        foreach ($property in @($check.statusBreakdown.PSObject.Properties)) {
            $value = [long] $property.Value
            Require ($value -ge 0) "SpatialBreakdownNegative:$owner`:$($property.Name)"
            $breakdownTotal += $value
        }
        Require ($breakdownTotal -eq $statusLedgerCount) "SpatialBreakdownMismatch:$owner"

        if ($collectionState -ceq 'Collected') {
            Require ($coverageState -ceq 'Complete') "SpatialCollectedCoverage:$owner"
            Require ($usageState -ceq 'PrivateReviewOnly') "SpatialCollectedUsageBoundary:$owner"
        }
        if ($collectionState -ceq 'NotCollected') {
            Require ($coverageState -ceq 'Missing') "SpatialNotCollectedCoverage:$owner"
            Require ($usageState -ceq 'Blocked') "SpatialNotCollectedUsage:$owner"
            Require ($evidenceValueSubjectCount -eq 0 -and $missingEvidenceSubjectCount -eq $subjectCount) "SpatialNotCollectedPartition:$owner"
        }
    }
    Require (@($profile.limitations).Count -gt 0) "StationLimitationMissing:$stationId"
}

$candidateCount = @($rules | Where-Object status -ceq 'Candidate').Count
$provisionalCount = @($rules | Where-Object status -ceq 'ProvisionalSharedRule').Count
$acceptedCount = @($rules | Where-Object status -ceq 'AcceptedSharedRule').Count
$spatialCheckCount = @($stationProfiles | ForEach-Object { @($_.spatialFoundationEvidenceChecks) }).Count
$spatialCollectedCount = @($stationProfiles.spatialFoundationEvidenceChecks | Where-Object collectionState -ceq 'Collected').Count
$spatialMissingCount = @($stationProfiles.spatialFoundationEvidenceChecks | Where-Object coverageState -ceq 'Missing').Count
$spatialUnassessedCount = @($stationProfiles.spatialFoundationEvidenceChecks | Where-Object coverageState -ceq 'Unassessed').Count

if ($Mode -ceq 'Summary') {
    Write-Output "StationDioramaEvidenceRulesSummary:Revision=$($catalog.revision);Sources=$($sourceReceipts.Count);Rules=$($rules.Count);Candidates=$candidateCount;Provisional=$provisionalCount;Accepted=$acceptedCount;Stations=$($stationProfiles.Count);SpatialChecks=$spatialCheckCount;Collected=$spatialCollectedCount;Missing=$spatialMissingCount;Unassessed=$spatialUnassessedCount"
    foreach ($rule in @($rules | Where-Object status -in @('Observed', 'Candidate'))) {
        Write-Output "ReviewPending:$($rule.ruleStableId);Status=$($rule.status);Stations=$(@($rule.observedStationStableIds).Count)"
    }
    return
}

Write-Output "StationDioramaEvidenceRulesValid:Revision=$($catalog.revision);Sources=$($sourceReceipts.Count);Rules=$($rules.Count);Candidates=$candidateCount;Provisional=$provisionalCount;Accepted=$acceptedCount;Stations=$($stationProfiles.Count);SpatialChecks=$spatialCheckCount;Collected=$spatialCollectedCount;Missing=$spatialMissingCount;Unassessed=$spatialUnassessedCount"
