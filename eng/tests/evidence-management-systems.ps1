$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$managementPath = Join-Path $repositoryRoot "eng/execution-ledgers/evidence-management-systems.json"
$management = Get-Content -LiteralPath $managementPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stagesPath = Join-Path $repositoryRoot ([string] $management.evidenceStageCatalogPath)
$stages = Get-Content -LiteralPath $stagesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$verticalProtocolPath = Join-Path $repositoryRoot (
    [string] $management.targetFirstVerticalImplementationProtocolPath)
$workOrderTemplatePath = Join-Path $repositoryRoot (
    [string] $management.targetFirstWorkOrderTemplatePath)
$responsibilityMapPath = Join-Path $repositoryRoot `
    "docs/AI/generated/evidence-responsibility-code-map.json"

if ([string] $management.schemaVersion -ne "simulation-evidence-management-systems.v3") {
    throw "EvidenceManagementSystemSchemaInvalid"
}
if ([string] $management.officialVerticalSpecificationName -ne
        "E1~E7 상호작용 수직 검증 명세" -or
    [string] $management.canonicalVerticalSpecificationSuffix -ne
        ".interaction-e1-e7-validation.json" -or
    [string] $management.legacyVerticalSpecificationSuffix -ne
        ".e7-work-order.json") {
    throw "EvidenceManagementVerticalSpecificationTerminologyInvalid"
}
if ([string] $management.evidenceModelRevision -ne
    "horizontal-dual-cycle-evidence.r3" -or
    (@($stages.stages.code) -join ",") -ne
    "E0,E1,E2,E3,E4,E5,E6,E7,E8,E9,E10") {
    throw "EvidenceManagementStageOrderInvalid"
}
if ([string] $stages.schemaVersion -ne "simulation-evidence-stages.v8" -or
    [string] $stages.revision -ne "simulation-evidence-stages.r15" -or
    [string] $stages.stageOutputContractRevision -ne
    "evidence-stage-output-contract.r1") {
    throw "EvidenceManagementStageOutputCatalogVersionInvalid"
}
$stageOutputPolicy = $stages.stageOutputPolicy
if (-not [bool] $stageOutputPolicy.everyPromotedStageRequiresRecordedOutputs -or
    -not [bool] $stageOutputPolicy.higherStageConsumesLowerStageOutputs -or
    -not [bool] $stageOutputPolicy.outputExistenceAloneNeverPromotesEvidence -or
    -not [bool] $stageOutputPolicy.dataOrScreenshotAloneNeverBecomesInteractionEvidence -or
    -not [bool] $stageOutputPolicy.logicAndPresentationOutputsRemainSeparateThroughE9 -or
    -not [bool] $stageOutputPolicy.integratedVerdictUsesLowerTrack) {
    throw "EvidenceManagementStageOutputPolicyInvalid"
}
$expectedCommonOutputFields = @(
    "subjectStableId", "trackCode", "candidateRevision",
    "contentHashOrBuildHash", "evidenceRefs", "statusCode", "blockers",
    "invalidationConditions")
if ((@($stageOutputPolicy.requiredCommonFields) -join ",") -ne
    ($expectedCommonOutputFields -join ",")) {
    throw "EvidenceManagementStageOutputCommonFieldsInvalid"
}
$expectedOutputKindsByStage = @{
    E0 = @("EvidenceCandidateRecord")
    E1 = @("EvidenceContract", "TrackImpactAssessment")
    E2 = @("ImplementationBindingRecord", "CompatibilityBoundaryRecord")
    E3 = @("DeterminismAndReplayReport", "ConsumerRegressionReport")
    E4 = @("ExecutionContextBinding", "PresentationPreparationHandoff")
    E5 = @("AuthorityManifestationRecord", "WorldPresentationBindingRecord")
    E6 = @("RefinementReviewRecord", "ActorActionBindingRecord")
    E7 = @("ActualInputClosureRecord", "IntegratedTrackVerdict")
    E8 = @("PlayableUnitStabilityCampaignResult")
    E9 = @("AreaHarmonySetEvaluation")
    E10 = @("LimitedOperationObservationReport")
}
$allOutputKindCodes = @()
foreach ($stage in @($stages.stages)) {
    $stageCode = [string] $stage.code
    $outputs = @($stage.requiredOutputs)
    if ((@($outputs.outputKindCode) -join ",") -ne
        (@($expectedOutputKindsByStage[$stageCode]) -join ",")) {
        throw "EvidenceManagementStageOutputKindsInvalid:$stageCode"
    }
    foreach ($output in $outputs) {
        if ([string]::IsNullOrWhiteSpace([string] $output.label) -or
            @($output.minimumContents).Count -eq 0 -or
            [string]::IsNullOrWhiteSpace([string] $output.notSufficientAlone)) {
            throw "EvidenceManagementStageOutputDefinitionIncomplete:${stageCode}:$($output.outputKindCode)"
        }
        $allOutputKindCodes += [string] $output.outputKindCode
    }
}
if ($allOutputKindCodes.Count -ne @($allOutputKindCodes | Sort-Object -Unique).Count) {
    throw "EvidenceManagementStageOutputKindDuplicate"
}
if ((@($management.systems.code) -join ",") -ne "G1,G2,G3,G4,G5") {
    throw "EvidenceManagementSystemOrderInvalid"
}
if (-not [bool] $management.principles.managementSystemsAreNotEvidenceStages -or
    -not [bool] $management.principles.managementCompletionDoesNotPromoteEvidence -or
    -not [bool] $management.principles.promotionRequiresStageCompletionGateEvidence -or
    -not [bool] $management.principles.targetFirstPlanningDoesNotPromoteEvidence -or
    -not [bool] $management.principles.evidencePackageDoesNotPromoteSubjectAutomatically -or
    -not [bool] $management.principles.playableUnitVerticalMaturityEndsAtE7 -or
    -not [bool] $management.principles.postE7EvidenceUsesStabilityAggregateAndOperationalSubjects -or
    -not [bool] $management.principles.logicAndPresentationCycleThroughE1ToE9 -or
    -not [bool] $management.principles.integratedEvidenceUsesLowerTrack -or
    -not [bool] $management.principles.legacyE9DoesNotPromoteCurrentE9) {
    throw "EvidenceManagementAxisBoundaryInvalid"
}
foreach ($catalogPath in @(
    [string] $management.playableLoopCatalogPath,
    [string] $management.evidencePackageCatalogPath)) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $catalogPath))) {
        throw "EvidenceManagementDevelopmentCatalogMissing:$catalogPath"
    }
}
if (-not (Test-Path -LiteralPath $verticalProtocolPath) -or
    -not (Test-Path -LiteralPath $workOrderTemplatePath)) {
    throw "EvidenceManagementVerticalProtocolMissing"
}
$verticalProtocol = Get-Content -LiteralPath $verticalProtocolPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ((@($verticalProtocol.downwardReviewOrder) -join ",") -ne
    "E7,E6,E5,E4,E3,E2,E1" -or
    (@($verticalProtocol.upwardValidationOrder) -join ",") -ne
    "E1,E2,E3,E4,E5,E6,E7" -or
    -not [bool] $verticalProtocol.principles.playableUnitVerticalMaturityEndsAtE7 -or
    -not [bool] $verticalProtocol.principles.logicAndPresentationEachReviewE7ToE1 -or
    -not [bool] $verticalProtocol.principles.logicAndPresentationEachValidateE1ToE7) {
    throw "EvidenceManagementVerticalProtocolOrderInvalid"
}

$expectedTransitions = @("E1>E6", "E6>E7", "E7>E8", "E8>E9", "E9>E10")
$actualTransitions = @($management.systems | ForEach-Object {
    "$($_.primaryTransition.from)>$($_.primaryTransition.to)"
})
if (($actualTransitions -join ",") -ne ($expectedTransitions -join ",")) {
    throw "EvidenceManagementTransitionInvalid"
}

$legacyStagePath = Join-Path $repositoryRoot `
    ([string] $management.legacyCompatibility.evidenceStageCatalogPath)
$legacyStages = Get-Content -LiteralPath $legacyStagePath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string] $legacyStages.evidenceModelRevision -ne
    "legacy-change-adaptive.r10" -or
    (@($legacyStages.stages.code) -join ",") -ne
    "E0,E1,E2,E3,E4,E5,E6,E7,E8,E9") {
    throw "EvidenceManagementLegacyStageCompatibilityInvalid"
}
$postE7Manager = Join-Path $repositoryRoot `
    "eng/execution-ledgers/manage-post-e7-evidence-campaigns.ps1"
$postE7Result = & $postE7Manager -Mode Check -InputPath `
    ([string] $management.postE7EvidenceCampaignCatalogPath)
if ([string] $postE7Result -notlike "PostE7EvidenceCampaignsValid:*") {
    throw "EvidenceManagementPostE7CampaignInvalid:$postE7Result"
}

$stageCodes = @($stages.stages.code)
foreach ($system in @($management.systems)) {
    if ([string]::IsNullOrWhiteSpace([string] $system.label) -or
        [string]::IsNullOrWhiteSpace([string] $system.question) -or
        @($system.responsibilities).Count -eq 0) {
        throw "EvidenceManagementSystemDefinitionIncomplete:$($system.code)"
    }
    if ([string] $system.primaryTransition.from -notin $stageCodes -or
        [string] $system.primaryTransition.to -notin $stageCodes) {
        throw "EvidenceManagementTransitionStageMissing:$($system.code)"
    }
}

$responsibilityMap = Get-Content -LiteralPath $responsibilityMapPath -Raw `
    -Encoding UTF8 | ConvertFrom-Json
if ([string] $responsibilityMap.schemaVersion -ne
    "ssalddel-evidence-responsibility-map.v2") {
    throw "EvidenceManagementResponsibilityMapSchemaInvalid"
}
$submodules = @($responsibilityMap.submodules)
if ($submodules.Count -ne 16 -or
    @($submodules | Where-Object evidenceStage -eq "E1").Count -ne 5 -or
    @($submodules | Where-Object evidenceStage -eq "E2").Count -ne 6 -or
    @($submodules | Where-Object evidenceStage -eq "E3").Count -ne 5) {
    throw "EvidenceManagementSubmoduleCatalogInvalid"
}
if (@($submodules | Where-Object {
        ([int] $_.primaryCount + [int] $_.secondaryCount) -eq 0
    }).Count -gt 0) {
    throw "EvidenceManagementSubmoduleBindingMissing"
}
$e5Components = @($responsibilityMap.components | Where-Object {
    $_.PSObject.Properties.Name -contains "primaryEvidenceStage" -and
    [string] $_.primaryEvidenceStage -eq "E5"
})
if ($e5Components.Count -eq 0) {
    throw "EvidenceManagementE5ComponentMissing"
}
$invalidE5Components = @($e5Components | Where-Object {
    [string] $_.assemblyName -like "Ssalddel.Simulation.Persistence*" -or
    [string] $_.assemblyName -like "Ssalddel.Unity*"
})
if ($invalidE5Components.Count -gt 0) {
    throw "EvidenceManagementE5BoundaryInvalid:$($invalidE5Components[0].componentName)"
}
if (-not ($e5Components | Where-Object {
        [string] $_.assemblyName -eq "Ssalddel.Simulation.Application"
    }) -or -not ($e5Components | Where-Object {
        [string] $_.assemblyName -eq "Ssalddel.Simulation.Domain"
    })) {
    throw "EvidenceManagementE5ExecutionPipelineMissing"
}

Write-Output "EvidenceManagementSystemsTestsPassed:G1=E1-E6;G2=E6-E7;G3=E7-E8;G4=E8-E9;G5=E9-E10;Vertical=E1-E7;E1E3Submodules=$($submodules.Count);E5Components=$($e5Components.Count)"
