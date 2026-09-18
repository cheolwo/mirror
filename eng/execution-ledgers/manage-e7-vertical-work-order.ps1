[CmdletBinding()]
param(
    [string] $InputPath = "eng/execution-ledgers/work-orders/interaction-e1-e7-validation.template.json",
    [string] $ProtocolPath = "eng/execution-ledgers/e7-vertical-implementation-protocol.json",
    [string] $PlayableLoopPath = "eng/execution-ledgers/playable-loops.json",
    [string] $GwaeClassificationPath = "docs/AI/generated/world-interaction-gwae-classifications.json",
    [string] $InteractionGoalPath = "eng/execution-ledgers/subject-interaction-development.json",
    [string] $UnityProjectRoot = ''
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../common/presentation-module-bindings.ps1')

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "E7VerticalWorkOrderInvalid:$Code" }
}

function Require-Text([object] $Value, [string] $Code) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $Value)) $Code
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$workOrder = Get-Content -LiteralPath (Join-Path $repositoryRoot $InputPath) `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$protocol = Get-Content -LiteralPath (Join-Path $repositoryRoot $ProtocolPath) `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$loops = Get-Content -LiteralPath (Join-Path $repositoryRoot $PlayableLoopPath) `
    -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $protocol.schemaVersion -eq
    "simulation-e7-vertical-implementation-protocol.v2") "ProtocolSchemaInvalid"
Require ([string] $protocol.evidenceModelRevision -eq
    "horizontal-dual-cycle-evidence.r3") "EvidenceModelInvalid"
foreach ($principleName in @(
    "presentationE4PreparesApplicableAssetPlacementHandoff",
    "assetResearchAloneNeverPromotesE5",
    "nonSpatialOrNonVisualWorkCanDeclareNotApplicable",
    "playableUnitTrackUsesLowestMemberValidationStage",
    "roleObjectActionClassificationRequiredBeforeE5")) {
    $principle = $protocol.principles.PSObject.Properties[$principleName]
    Require ($null -ne $principle -and [bool] $principle.Value) `
        "ProtocolPrincipleMissing:$principleName"
}
$presentationHandoff = $protocol.presentationE4ToE5Handoff
Require ($null -ne $presentationHandoff) "PresentationE4HandoffMissing"
foreach ($applicabilityCode in @("Required", "NotApplicable")) {
    Require (@($presentationHandoff.allowedApplicabilityCodes) -contains
        $applicabilityCode) "PresentationApplicabilityMissing:$applicabilityCode"
}
foreach ($readinessCode in @("Ready", "Conditional", "Blocked")) {
    Require (@($presentationHandoff.allowedReadinessCodes) -contains
        $readinessCode) "PresentationReadinessMissing:$readinessCode"
}
foreach ($fieldName in @(
    "playerReadableMoment", "requiredHCapabilities", "visualKeys",
    "primaryAssetCandidateRefs", "alternativeAssetCandidateRefs",
    "fallbackPresentationRefs", "placementIntent",
    "interactionAnchorIntent", "candidateRevisionOrFingerprint",
    "e5ReadinessCode", "openGapRefs")) {
    Require (@($presentationHandoff.requiredFieldsWhenApplicable) -contains
        $fieldName) "PresentationHandoffFieldMissing:$fieldName"
}
$roleActionProtocolGate = $protocol.roleObjectActionE5Gate
Require ($null -ne $roleActionProtocolGate) "RoleObjectActionE5GateMissing"
Require ([string] $roleActionProtocolGate.requiredFromEvidenceStageCode -eq "E5") `
    "RoleObjectActionE5GateStageInvalid"
Require ([string] $roleActionProtocolGate.worldObjectScopeCode -eq "AllWorldObjects") `
    "RoleObjectActionScopeInvalid"
Require-Text $roleActionProtocolGate.classificationOutputPath `
    "RoleObjectActionClassificationPathMissing"
Require-Text $roleActionProtocolGate.classificationSourcePath `
    "RoleObjectActionClassificationSourcePathMissing"
Require-Text $roleActionProtocolGate.worldInteractionCatalogPath `
    "RoleObjectActionCatalogPathMissing"
Require-Text $roleActionProtocolGate.outputSchemaVersion `
    "RoleObjectActionOutputSchemaMissing"
foreach ($applicabilityCode in @("Required", "NotApplicable")) {
    Require (@($roleActionProtocolGate.allowedApplicabilityCodes) -contains
        $applicabilityCode) "RoleObjectActionApplicabilityMissing:$applicabilityCode"
}
foreach ($fieldName in @(
    "roleObjectKindCode", "roleObjectBindingModeCode", "actionCode",
    "startStateCodes", "completionStateCodes", "effectCodes")) {
    Require (@($roleActionProtocolGate.requiredFieldsWhenApplicable) -contains
        $fieldName) "RoleObjectActionFieldMissing:$fieldName"
}
Require ([string] $workOrder.schemaVersion -eq
    [string] $protocol.workOrderSchemaVersion) "WorkOrderSchemaInvalid"
Require (@($protocol.compatibleWorkOrderProtocolRevisions) -contains
    [string] $workOrder.protocolRevision) "ProtocolRevisionInvalid"
Require ([string] $workOrder.evidenceModelRevision -eq
    [string] $protocol.evidenceModelRevision) "WorkOrderEvidenceModelInvalid"
Require-Text $workOrder.workOrderId "WorkOrderIdMissing"
Require ([string] $workOrder.workOrderId -match '^E7-WO-') "WorkOrderIdInvalid"
Require-Text $workOrder.title "TitleMissing"
$nativeGoalProperty = $workOrder.PSObject.Properties['interactionGoalStableId']
$isNativeGoal = $null -ne $nativeGoalProperty -and
    -not [string]::IsNullOrWhiteSpace([string] $nativeGoalProperty.Value)
if (-not $isNativeGoal) {
    Require-Text $workOrder.playableUnitStableId "PlayableUnitMissing"
}
Require-Text $workOrder.activeWorldInteractionId "WorldInteractionMissing"
Require ([string] $workOrder.targetEvidenceStage -eq "E7") "TargetMustBeE7"
Require ([string] $workOrder.currentEvidenceStage -match '^E[0-7]$') `
    "CurrentStageInvalid"
Require (@($protocol.allowedCurrentPasses) -contains
    [string] $workOrder.iterationState.currentPass) "CurrentPassInvalid"
Require-Text $workOrder.iterationState.nextReopenCondition `
    "NextReopenConditionMissing"
Require ($null -ne $workOrder.trackPlans) "TrackPlansMissing"
Require ($null -ne $workOrder.integratedGate) "IntegratedGateMissing"

$trackPlans = @($workOrder.trackPlans.logic, $workOrder.trackPlans.presentation)
foreach ($track in $trackPlans) {
    $trackCode = [string] $track.trackCode
    Require (@($protocol.allowedTrackCodes) -contains $trackCode) `
        "TrackCodeInvalid:$trackCode"
    Require ([string] $track.currentEvidenceStage -match '^E[0-7]$') `
        "TrackCurrentStageInvalid:$trackCode"
    Require ((@($track.downwardPlan.code) -join ',') -eq
        (@($protocol.downwardReviewOrder) -join ',')) "DownwardOrderInvalid:$trackCode"
    Require ((@($track.upwardValidation.code) -join ',') -eq
        (@($protocol.upwardValidationOrder) -join ',')) "UpwardOrderInvalid:$trackCode"

    foreach ($review in @($track.downwardPlan)) {
        Require (@($protocol.allowedDispositions) -contains [string] $review.disposition) `
            "DispositionInvalid:${trackCode}:$($review.code)"
        Require-Text $review.summary "SummaryMissing:${trackCode}:$($review.code)"
    }
    foreach ($review in @($track.upwardValidation)) {
        Require (@($protocol.allowedStatuses) -contains [string] $review.status) `
            "StatusInvalid:${trackCode}:$($review.code)"
    }
}

Require ([string] $workOrder.trackPlans.logic.trackCode -eq "Logic") `
    "LogicTrackMissing"
Require ([string] $workOrder.trackPlans.presentation.trackCode -eq "Presentation") `
    "PresentationTrackMissing"

$logicStageNumber = [int] ([string] $workOrder.trackPlans.logic.currentEvidenceStage).Substring(1)
$presentationStageNumber = [int] ([string] $workOrder.trackPlans.presentation.currentEvidenceStage).Substring(1)
$integratedStageNumber = [Math]::Min($logicStageNumber, $presentationStageNumber)
$integratedStage = "E$integratedStageNumber"
Require ([string] $workOrder.currentEvidenceStage -eq $integratedStage) `
    "CurrentStageMustEqualLowerTrack"
Require ([string] $workOrder.integratedGate.currentEvidenceStage -eq $integratedStage) `
    "IntegratedGateMustEqualLowerTrack"
Require (@($protocol.allowedStatuses) -contains [string] $workOrder.integratedGate.status) `
    "IntegratedStatusInvalid"
Require ($presentationStageNumber -lt 5 -or $logicStageNumber -ge 5) `
    "PresentationE5RequiresLogicE5"

$highestTrackStageNumber = [Math]::Max($logicStageNumber, $presentationStageNumber)
if ($highestTrackStageNumber -ge 5) {
    $classificationOutputFullPath = Join-Path $repositoryRoot $GwaeClassificationPath
    Require (Test-Path -LiteralPath $classificationOutputFullPath) `
        "RoleObjectActionClassificationOutputMissing"
    $classificationOutput = Get-Content -LiteralPath $classificationOutputFullPath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ([string] $classificationOutput.schemaVersion -eq
        [string] $roleActionProtocolGate.outputSchemaVersion) `
        "RoleObjectActionClassificationSchemaInvalid"
    Require-Text $classificationOutput.sourceRevision `
        "RoleObjectActionClassificationRevisionMissing"

    $classificationSourceFullPath = Join-Path $repositoryRoot `
        ([string] $roleActionProtocolGate.classificationSourcePath)
    Require (Test-Path -LiteralPath $classificationSourceFullPath) `
        "RoleObjectActionClassificationSourceMissing"
    $classificationSource = Get-Content -LiteralPath $classificationSourceFullPath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ([string] $classificationOutput.sourceRevision -eq
        [string] $classificationSource.revision) `
        "RoleObjectActionClassificationSourceStale"

    $classificationPolicyProperty = $classificationOutput.PSObject.Properties[
        "e5RoleObjectActionGatePolicy"]
    Require ($null -ne $classificationPolicyProperty) `
        "RoleObjectActionClassificationPolicyMissing"
    $classificationPolicy = $classificationPolicyProperty.Value
    Require ([string] $classificationPolicy.requiredFromEvidenceStageCode -eq "E5") `
        "RoleObjectActionClassificationStageInvalid"
    Require ([string] $classificationPolicy.worldObjectScopeCode -eq "AllWorldObjects") `
        "RoleObjectActionClassificationScopeInvalid"

    $worldInteractionCatalogFullPath = Join-Path $repositoryRoot `
        ([string] $roleActionProtocolGate.worldInteractionCatalogPath)
    Require (Test-Path -LiteralPath $worldInteractionCatalogFullPath) `
        "RoleObjectActionWorldInteractionCatalogMissing"
    $worldInteractionCatalog = Get-Content -LiteralPath $worldInteractionCatalogFullPath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ([string] $classificationOutput.worldInteractionCatalogRevision -eq
        [string] $worldInteractionCatalog.revision) `
        "RoleObjectActionClassificationStale"

    $activeWorldInteractionId = [string] $workOrder.activeWorldInteractionId
    $worldInteraction = @($worldInteractionCatalog.items | Where-Object {
        [string] $_.id -eq $activeWorldInteractionId
    })
    Require ($worldInteraction.Count -eq 1) `
        "RoleObjectActionWorldInteractionUnknown:$activeWorldInteractionId"
    $classification = @($classificationOutput.items | Where-Object {
        [string] $_.wiId -eq $activeWorldInteractionId
    })
    Require ($classification.Count -eq 1) `
        "RoleObjectActionClassificationMissing:$activeWorldInteractionId"
    $gateProperty = $classification[0].PSObject.Properties["e5RoleObjectActionGate"]
    Require ($null -ne $gateProperty) `
        "RoleObjectActionClassificationGateMissing:$activeWorldInteractionId"
    $classificationGate = $gateProperty.Value
    $applicabilityCode = [string] $classificationGate.applicabilityCode
    Require (@($roleActionProtocolGate.allowedApplicabilityCodes) -contains
        $applicabilityCode) `
        "RoleObjectActionApplicabilityInvalid:$activeWorldInteractionId"

    if ($applicabilityCode -eq "Required") {
        foreach ($fieldName in @(
            "roleObjectKindCode", "roleObjectBindingModeCode", "actionCode",
            "startStateCodes", "completionStateCodes", "effectCodes")) {
            Require ($null -ne $classificationGate.PSObject.Properties[$fieldName]) `
                "RoleObjectActionBindingFieldMissing:${activeWorldInteractionId}:$fieldName"
        }
        Require (@($classificationPolicy.allowedRoleObjectKindCodes) -contains
            [string] $classificationGate.roleObjectKindCode) `
            "RoleObjectActionKindInvalid:$activeWorldInteractionId"
        Require-Text $classificationGate.roleObjectBindingModeCode `
            "RoleObjectActionBindingModeMissing:$activeWorldInteractionId"
        Require ([string] $classificationGate.actionCode -eq
            [string] $worldInteraction[0].actionCode) `
            "RoleObjectActionCodeMismatch:$activeWorldInteractionId"
        foreach ($transitionFieldName in @(
            "startStateCodes", "completionStateCodes", "effectCodes")) {
            $classifiedValues = @($classificationGate.$transitionFieldName |
                ForEach-Object { [string] $_ })
            $catalogValues = @($worldInteraction[0].$transitionFieldName |
                ForEach-Object { [string] $_ })
            Require ($classifiedValues.Count -gt 0) `
                "RoleObjectActionTransitionMissing:${activeWorldInteractionId}:$transitionFieldName"
            Require (($classifiedValues -join ',') -eq ($catalogValues -join ',')) `
                "RoleObjectActionTransitionMismatch:${activeWorldInteractionId}:$transitionFieldName"
        }
    }
    else {
        $reasonProperty = $classificationGate.PSObject.Properties["notApplicableReason"]
        Require ($null -ne $reasonProperty -and
            -not [string]::IsNullOrWhiteSpace([string] $reasonProperty.Value)) `
            "RoleObjectActionNotApplicableReasonMissing:$activeWorldInteractionId"
    }
}

$isTemplate = [string] $workOrder.workOrderId -eq "E7-WO-TEMPLATE"
if ($null -ne $workOrder.PSObject.Properties['presentationModuleBindings']) {
    $presentationModules = Get-Content -LiteralPath (Join-Path $repositoryRoot 'eng/execution-ledgers/playable-loop-presentation-validation-modules.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Test-PresentationModuleBindings $workOrder $presentationModules $repositoryRoot $UnityProjectRoot | Out-Null
}
$preparationProperty = $workOrder.PSObject.Properties["presentationE4Preparation"]
if ($isTemplate -or $null -ne $preparationProperty) {
    $preparation = $workOrder.presentationE4Preparation
    Require ($null -ne $preparation) "PresentationE4PreparationMissing"
    Require (@($presentationHandoff.allowedApplicabilityCodes) -contains
        [string] $preparation.applicabilityCode) `
        "PresentationApplicabilityInvalid"
    Require (@($presentationHandoff.allowedReadinessCodes) -contains
        [string] $preparation.e5ReadinessCode) `
        "PresentationReadinessInvalid"
    foreach ($fieldName in @($presentationHandoff.requiredFieldsWhenApplicable)) {
        Require ($null -ne $preparation.PSObject.Properties[$fieldName]) `
            "PresentationFieldMissing:$fieldName"
    }
    Require ($null -ne $preparation.PSObject.Properties["notApplicableReason"]) `
        "PresentationNotApplicableReasonMissing"
}
if (-not $isTemplate -and $isNativeGoal) {
    $goalCatalog = Get-Content -LiteralPath (Join-Path $repositoryRoot $InteractionGoalPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    $goals = @($goalCatalog.nativeInteractionGoals | Where-Object goalStableId -eq $nativeGoalProperty.Value)
    Require ($goals.Count -eq 1) "InteractionGoalUnknown"
    $goal = $goals[0]
    Require ([string] $goal.worldInteractionId -eq [string] $workOrder.activeWorldInteractionId) "InteractionGoalWiMismatch"
    $wiCatalog = Get-Content -LiteralPath (Join-Path $repositoryRoot $goalCatalog.worldInteractionCatalogPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    Require (@($wiCatalog.items | Where-Object id -eq $workOrder.activeWorldInteractionId).Count -eq 1) "InteractionGoalWiUnknown"
    $subjects = Get-Content -LiteralPath (Join-Path $repositoryRoot $goalCatalog.subjectCatalogPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    Require (@($workOrder.subjectBindingRefs).Count -gt 0) "InteractionSubjectsMissing"
    Require ((@($workOrder.subjectBindingRefs | Sort-Object) -join '|') -eq
        (@($goal.subjectBindingRefs | Sort-Object) -join '|')) "InteractionSubjectsMismatch"
    foreach ($subjectId in @($workOrder.subjectBindingRefs)) {
        $subject = @($subjects.items | Where-Object subjectStableId -eq $subjectId)
        Require ($subject.Count -eq 1 -and [string] $subject[0].statusCode -eq 'Ready') "InteractionSubjectNotReady:$subjectId"
    }
    $planning = $workOrder.planningGate
    Require ([string] $planning.statusCode -eq 'Approved' -and [string] $goal.planningGate.statusCode -eq 'Approved') "InteractionPlanningNotApproved"
    foreach ($field in @('designDocumentRef', 'designRevision', 'designHashSha256', 'approvalEvidenceRef')) {
        Require-Text $planning.$field "InteractionPlanningFieldMissing:$field"
        Require ([string] $planning.$field -eq [string] $goal.planningGate.$field) "InteractionPlanningMismatch:$field"
    }
    $designPath = Join-Path $repositoryRoot $planning.designDocumentRef
    Require (Test-Path -LiteralPath $designPath) "InteractionDesignMissing"
    Require ((Get-FileHash -LiteralPath $designPath -Algorithm SHA256).Hash -eq [string] $planning.designHashSha256) "InteractionDesignHashMismatch"
    Require (Test-Path -LiteralPath (Join-Path $repositoryRoot $planning.approvalEvidenceRef)) "InteractionApprovalMissing"
    $registeredOrderPath = Join-Path $repositoryRoot $goal.workOrderRef
    Require (Test-Path -LiteralPath $registeredOrderPath) "InteractionWorkOrderMissing"
    $registeredOrder = Get-Content -LiteralPath $registeredOrderPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ([string] $registeredOrder.workOrderId -eq [string] $workOrder.workOrderId) "InteractionWorkOrderMismatch"
    Require ($highestTrackStageNumber -le [int] ([string] $goal.deliveryCap).Substring(1)) "InteractionDeliveryCapExceeded"
    $optionalLoop = [string] $goal.optionalPlayableLoopValidationRef
    $orderLoop = $workOrder.PSObject.Properties['playableUnitStableId']
    Require (($null -eq $orderLoop -and [string]::IsNullOrEmpty($optionalLoop)) -or
        ($null -ne $orderLoop -and [string] $orderLoop.Value -eq $optionalLoop)) "InteractionOptionalLoopMismatch"
    if (-not [string]::IsNullOrEmpty($optionalLoop)) {
        $bundle = @($loops.items | Where-Object loopStableId -eq $optionalLoop)
        Require ($bundle.Count -eq 1 -and @($bundle[0].worldInteractionIds) -contains $workOrder.activeWorldInteractionId) "InteractionOptionalLoopInvalid"
    }
}
if (-not $isTemplate -and -not $isNativeGoal) {
    $loop = @($loops.items | Where-Object {
        [string] $_.loopStableId -eq [string] $workOrder.playableUnitStableId
    })
    Require ($loop.Count -eq 1) "PlayableUnitUnknown"
    Require ([string] $loop[0].loopLevelCode -eq "PlayableUnit") `
        "SubjectMustBePlayableUnit"
    Require ([string] $loop[0].finalEvidenceStage -eq "E7") `
        "PlayableUnitFinalStageMustBeE7"
    Require (@($loop[0].worldInteractionIds) -contains
        [string] $workOrder.activeWorldInteractionId) "WorldInteractionOutsideUnit"
    Require ($null -ne $loop[0].maturityTracks) "PlayableUnitTracksMissing"
    $loopLogicStageNumber = [int] ([string] $loop[0].maturityTracks.logic.currentStage).Substring(1)
    $loopPresentationStageNumber = [int] ([string] $loop[0].maturityTracks.presentation.currentStage).Substring(1)
    Require ($logicStageNumber -ge $loopLogicStageNumber) `
        "LogicStageBelowPlayableUnit"
    Require ($presentationStageNumber -ge $loopPresentationStageNumber) `
        "PresentationStageBelowPlayableUnit"
}

if ([bool] $workOrder.promotionEligible) {
    Require (-not $isTemplate) "TemplateCannotPromote"
    Require ([string] $workOrder.currentEvidenceStage -eq "E7") `
        "PromotionRequiresE7"
    Require ($logicStageNumber -eq 7 -and $presentationStageNumber -eq 7) `
        "PromotionRequiresBothTracksE7"
    foreach ($track in $trackPlans) {
        Require (@($track.upwardValidation | Where-Object status -ne "Passed").Count `
            -eq 0) "PromotionRequiresAllPassed:$($track.trackCode)"
    }
    Require ([string] $workOrder.integratedGate.status -eq "Passed") `
        "PromotionRequiresIntegratedPass"
    Require-Text $workOrder.integratedGate.candidateRevision `
        "PromotionRequiresCandidateRevision"
    Require (@($workOrder.integratedGate.openFeedbackRefs).Count -eq 0) `
        "PromotionRequiresNoOpenFeedback"
}

Write-Output "E7VerticalWorkOrderValid:$($workOrder.workOrderId);Logic=$($workOrder.trackPlans.logic.currentEvidenceStage);Presentation=$($workOrder.trackPlans.presentation.currentEvidenceStage);Integrated=$($workOrder.currentEvidenceStage);Pass=$($workOrder.iterationState.currentPass);PromotionEligible=$([bool] $workOrder.promotionEligible)"
