[CmdletBinding()]
param(
    [ValidateSet("Write", "Check")]
    [string] $Mode = "Check",
    [string] $SourcePath = "eng/world-seedbeds/gameplay-spatial-completion.v1.json",
    [string] $JsonOutputPath = "eng/world-seedbeds/generated/gameplay-spatial-completion.v1.json",
    [string] $MarkdownOutputPath = "docs/AI/generated/gameplay-spatial-completion.md"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "../common/deterministic-text-output.ps1")

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "GameplaySpatialCompletionInvalid:$Code" }
}

function Resolve-RepositoryPath([string] $RepositoryRoot, [string] $RelativePath) {
    return Join-Path $RepositoryRoot ($RelativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
}

function Read-Json([string] $Path) {
    Require (Test-Path -LiteralPath $Path) "SourceMissing:$Path"
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function New-DefinitionMap(
    [string] $CatalogRoot,
    [object[]] $DefinitionRefs) {
    $map = @{}
    foreach ($definitionRef in $DefinitionRefs) {
        $definitionPath = Resolve-RepositoryPath $CatalogRoot ([string] $definitionRef.definitionPath)
        $definition = Read-Json $definitionPath
        $stableId = [string] $definition.stableId
        Require (-not [string]::IsNullOrWhiteSpace($stableId)) "DefinitionStableIdMissing:$definitionPath"
        Require (-not $map.ContainsKey($stableId)) "DuplicateDefinition:$stableId"
        $map[$stableId] = $definition
    }
    return $map
}

function Assert-UniqueValues([string[]] $Values, [string] $Code) {
    $nonEmpty = @($Values | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) })
    Require ($nonEmpty.Count -eq @($nonEmpty | Sort-Object -Unique).Count) $Code
}

function Get-HighestTraceState(
    [string] $Current,
    [string] $Candidate,
    [hashtable] $TraceOrder) {
    if ([string]::IsNullOrWhiteSpace($Current)) { return $Candidate }
    if ([int] $TraceOrder[$Candidate] -gt [int] $TraceOrder[$Current]) { return $Candidate }
    return $Current
}

function Add-HTrace(
    [hashtable] $TraceMap,
    [hashtable] $TraceOrder,
    [string] $KnowledgeRef,
    [string] $HierarchyLevelCode,
    [string] $TraceStateCode,
    [string] $StepId,
    [string[]] $ContributionCodes) {
    if (-not $TraceMap.ContainsKey($KnowledgeRef)) {
        $TraceMap[$KnowledgeRef] = [pscustomobject][ordered]@{
            knowledgeRef = $KnowledgeRef
            hierarchyLevelCode = $HierarchyLevelCode
            gameplayTraceStateCode = $TraceStateCode
            stepIds = @()
            contributionCodes = @()
        }
    }
    $trace = $TraceMap[$KnowledgeRef]
    $trace.gameplayTraceStateCode = Get-HighestTraceState ([string] $trace.gameplayTraceStateCode) $TraceStateCode $TraceOrder
    $trace.stepIds = @($trace.stepIds + $StepId | Sort-Object -Unique)
    $trace.contributionCodes = @($trace.contributionCodes + $ContributionCodes | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) } | Sort-Object -Unique)
}

function Test-ChildCoverage(
    [object] $Parent,
    [string[]] $ChildRefs,
    [string] $RequiredProperty,
    [string] $OptionalProperty) {
    $declaredChildren = @($Parent.$RequiredProperty + $Parent.$OptionalProperty)
    return @($ChildRefs | Where-Object { $_ -in $declaredChildren }).Count -gt 0
}

function Get-CurrentPlayableSliceState([object] $CompletionGates) {
    $state = "Planned"
    $ordered = @(
        @{ Property = "theorySpatiallyComposed"; State = "TheorySpatiallyComposed" },
        @{ Property = "spatiallyComposed"; State = "SpatiallyComposed" },
        @{ Property = "functionallyClosed"; State = "FunctionallyClosed" },
        @{ Property = "experienceValidated"; State = "ExperienceValidated" },
        @{ Property = "playableSliceComplete"; State = "PlayableSliceComplete" }
    )
    foreach ($candidate in $ordered) {
        $gate = $CompletionGates.($candidate.Property)
        if (@($gate.evidenceRefs).Count -eq 0 -or @($gate.blockReasonCodes).Count -gt 0) { break }
        $state = [string] $candidate.State
    }
    return $state
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$resolvedSourcePath = Resolve-RepositoryPath $repositoryRoot $SourcePath
$source = Read-Json $resolvedSourcePath

Require ([string] $source.schemaVersion -eq "simulation-world-gameplay-spatial-completion.v1") "SourceSchema"
Require (@($source.playableSlices).Count -gt 0) "PlayableSliceMissing"
Require ([bool] $source.gatePolicy.spatialApprovalAndGameplayReadinessAreIndependent) "AxesMustRemainIndependent"
Require ([bool] $source.gatePolicy.automaticHPromotionForbidden) "AutomaticHPromotionMustBeForbidden"
Require ([bool] $source.gatePolicy.scenarioFallbackForMissingE5Forbidden) "ScenarioFallbackMustBeForbidden"
Require ([bool] $source.gatePolicy.theorySpatialProductionIndependentFromGameplayTrace) "TheoryProductionMustRemainIndependentFromGameplayTrace"
Require ([bool] $source.gatePolicy.actualE5RequiredForPlayableCompletion) "ActualE5MustRemainRequired"

$traceStateCodes = @($source.traceStateOrder)
Assert-UniqueValues $traceStateCodes "DuplicateTraceState"
$traceOrder = @{}
for ($index = 0; $index -lt $traceStateCodes.Count; $index++) {
    $traceOrder[[string] $traceStateCodes[$index]] = $index
}
foreach ($requiredTraceState in @("Unlinked", "Supporting", "DirectAction", "SequenceMapped", "LoopMapped", "RegionalCausalityMapped")) {
    Require ($traceOrder.ContainsKey($requiredTraceState)) "TraceStateMissing:$requiredTraceState"
}

$supportingContributionCodes = @($source.supportingContributionCodes)
Assert-UniqueValues $supportingContributionCodes "DuplicateSupportingContributionCode"

$catalogPath = Resolve-RepositoryPath $repositoryRoot ([string] $source.sourceCatalogPath)
$catalog = Read-Json $catalogPath
Require ([string] $catalog.schemaVersion -eq "simulation-world-spatial-design-knowledge-catalog.v3") "CatalogSchema"
$catalogRoot = Split-Path -Parent $catalogPath
$h1Map = New-DefinitionMap $catalogRoot @($catalog.h1InteractionDefinitionRefs + $catalog.h1ExpressionDefinitionRefs)
$h2Map = New-DefinitionMap $catalogRoot @($catalog.h2DefinitionRefs)
$h3Map = New-DefinitionMap $catalogRoot @($catalog.h3DefinitionRefs)
$h4Map = New-DefinitionMap $catalogRoot @($catalog.h4DefinitionRefs)

$areaSetPriorityPath = Resolve-RepositoryPath $repositoryRoot ([string] $source.areaSetPriorityPath)
$areaSetPriority = Read-Json $areaSetPriorityPath
Require ([string] $areaSetPriority.schemaVersion -eq "simulation-world-area-set-composition-priorities.v1") "AreaSetPrioritySchema"
$gamePlanMap = @{}
foreach ($candidate in @($areaSetPriority.areaSetCandidates)) {
    $gamePlanCode = [string] $candidate.gamePlanCode
    Require (-not $gamePlanMap.ContainsKey($gamePlanCode)) "DuplicateGamePlan:$gamePlanCode"
    $gamePlanMap[$gamePlanCode] = $candidate
}

$worldInteractionPath = Resolve-RepositoryPath $repositoryRoot ([string] $source.worldInteractionCatalogPath)
$worldInteractionCatalog = Read-Json $worldInteractionPath
Require ([string] $worldInteractionCatalog.schemaVersion -eq "5") "WorldInteractionSchema"
$worldInteractionMap = @{}
foreach ($interaction in @($worldInteractionCatalog.items)) {
    $wiId = [string] $interaction.id
    Require (-not $worldInteractionMap.ContainsKey($wiId)) "DuplicateWorldInteraction:$wiId"
    $worldInteractionMap[$wiId] = $interaction
}

$evidenceStagePath = Resolve-RepositoryPath $repositoryRoot ([string] $source.evidenceStageCatalogPath)
$evidenceStageCatalog = Read-Json $evidenceStagePath
Require ([string] $evidenceStageCatalog.schemaVersion -eq "simulation-evidence-stages.v8") "EvidenceStageSchema"
$evidenceStageCodes = @($evidenceStageCatalog.stages.code)

$theorySpatialFactoryPath = Resolve-RepositoryPath $repositoryRoot ([string] $source.theorySpatialFactoryOutputPath)
$theorySpatialFactory = Read-Json $theorySpatialFactoryPath
Require ([string] $theorySpatialFactory.schemaVersion -eq "simulation-world-theory-spatial-factory-output.v1") "TheorySpatialFactorySchema"
Require ([string] $theorySpatialFactory.humanReviewModeCode -eq "DeferredBatchReview") "TheoryHumanReviewMustRemainDeferred"
Require ([bool] $theorySpatialFactory.authorityBoundary.humanApprovalNotClaimed) "TheoryHumanApprovalBoundaryInvalid"
Require ([bool] $theorySpatialFactory.authorityBoundary.publicDataNotBound) "TheoryPublicDataBoundaryInvalid"
Require ([bool] $theorySpatialFactory.authorityBoundary.runtimeNotValidated) "TheoryRuntimeBoundaryInvalid"

$theoryH2Map = @{}
foreach ($plan in @($theorySpatialFactory.h2Plans)) {
    Require ([string] $plan.theoryStateCode -eq "TheoryQualified") "TheoryH2StateInvalid:$($plan.h2StableId)"
    Require (-not $theoryH2Map.ContainsKey([string] $plan.h2StableId)) "TheoryH2Duplicate:$($plan.h2StableId)"
    $theoryH2Map[[string] $plan.h2StableId] = $plan
}
$theoryH3Map = @{}
foreach ($plan in @($theorySpatialFactory.h3Plans)) {
    Require ([string] $plan.theoryStateCode -eq "TheoryQualified") "TheoryH3StateInvalid:$($plan.h3StableId)"
    Require (-not $theoryH3Map.ContainsKey([string] $plan.h3StableId)) "TheoryH3Duplicate:$($plan.h3StableId)"
    $theoryH3Map[[string] $plan.h3StableId] = $plan
}
$theoryAreaSetsByGamePlan = @{}
foreach ($instance in @($theorySpatialFactory.e5AreaSetInstances)) {
    Require ([string] $instance.evidenceStageCode -eq "E5") "TheoryAreaSetEvidenceStageInvalid:$($instance.areaSetStableId)"
    Require ([string] $instance.e5QualificationCode -eq "E5TheoryQualified") "TheoryAreaSetStateInvalid:$($instance.areaSetStableId)"
    Require ([string] $instance.evidenceKindCode -eq "TheoryGenerated") "TheoryAreaSetEvidenceKindInvalid:$($instance.areaSetStableId)"
    Require (-not [bool] $instance.humanReviewed -and -not [bool] $instance.publicDataBound -and -not [bool] $instance.runtimeValidated) "TheoryAreaSetAuthorityBoundaryInvalid:$($instance.areaSetStableId)"
    $gamePlanCode = [string] $instance.gamePlanCode
    Require (-not $theoryAreaSetsByGamePlan.ContainsKey($gamePlanCode)) "TheoryAreaSetGamePlanDuplicate:$gamePlanCode"
    $theoryAreaSetsByGamePlan[$gamePlanCode] = $instance
}
$theoryRelationCodes = @($theorySpatialFactory.interAreaSetRelations.relationCode | ForEach-Object { [string] $_ })
Assert-UniqueValues $theoryRelationCodes "TheoryAreaSetRelationDuplicate"

$actualE5SpatialPath = Resolve-RepositoryPath $repositoryRoot ([string] $source.actualE5SpatialOutputPath)
$actualE5Spatial = Read-Json $actualE5SpatialPath
Require ([string] $actualE5Spatial.schemaVersion -eq "simulation-world-actual-e5-spatial-output.v1") "ActualE5SpatialSchema"
Require ([string] $actualE5Spatial.authorityBoundary.evidenceStageCode -eq "ActualE5") "ActualE5EvidenceStageInvalid"
Require (-not [bool] $actualE5Spatial.authorityBoundary.publicDataBound) "ActualE5PublicDataWasInvented"
Require (-not [bool] $actualE5Spatial.authorityBoundary.runtimeValidated) "ActualE5RuntimeWasInvented"
Require ([bool] $actualE5Spatial.presentationOnly -and -not [bool] $actualE5Spatial.isOperationalState) "ActualE5AuthorityBoundaryInvalid"

$actualAreaSetByTheory = @{}
foreach ($actualAreaSet in @($actualE5Spatial.areaSets)) {
    $theoryAreaSetStableId = [string] $actualAreaSet.theoryAreaSetStableId
    Require (-not [string]::IsNullOrWhiteSpace($theoryAreaSetStableId)) "ActualE5TheoryAreaSetRefMissing"
    Require (-not $actualAreaSetByTheory.ContainsKey($theoryAreaSetStableId)) "ActualE5TheoryAreaSetDuplicate:$theoryAreaSetStableId"
    Require ([string] $actualAreaSet.definition.definitionStatusCode -eq "Available") "ActualE5AreaSetUnavailable:$theoryAreaSetStableId"
    Require (@($actualAreaSet.graphs | Where-Object statusCode -ne "Available").Count -eq 0) "ActualE5AreaSetGraphUnavailable:$theoryAreaSetStableId"
    Require (@($actualAreaSet.graphs | ForEach-Object { @($_.unresolved) } | Where-Object { $null -ne $_ }).Count -eq 0) "ActualE5AreaSetGraphUnresolved:$theoryAreaSetStableId"
    $actualAreaSetByTheory[$theoryAreaSetStableId] = $actualAreaSet
}

$actualDirectBindingsByWi = @{}
foreach ($binding in @($actualE5Spatial.interactionSpatialCatalog.bindings)) {
    $wiId = [string] $binding.worldInteractionId
    Require (-not $actualDirectBindingsByWi.ContainsKey($wiId)) "ActualE5DirectBindingDuplicate:$wiId"
    $actualDirectBindingsByWi[$wiId] = $binding
}
$actualContextBindingsByWi = @{}
foreach ($binding in @($actualE5Spatial.interactionSpatialCatalog.contextualBindings)) {
    $wiId = [string] $binding.worldInteractionId
    Require (-not $actualContextBindingsByWi.ContainsKey($wiId)) "ActualE5ContextBindingDuplicate:$wiId"
    $actualContextBindingsByWi[$wiId] = $binding
}
$actualNonSpatialWiIds = @($actualE5Spatial.interactionSpatialCatalog.nonSpatialWiIds | ForEach-Object { [string] $_ })
Assert-UniqueValues $actualNonSpatialWiIds "ActualE5NonSpatialWiDuplicate"

$strictGamePlanCodes = @($source.gatePolicy.strictGamePlanCodes)
$warningOnlyGamePlanCodes = @($source.gatePolicy.warningOnlyGamePlanCodes)
Assert-UniqueValues @($strictGamePlanCodes + $warningOnlyGamePlanCodes) "DuplicateGateGamePlan"
foreach ($gamePlanCode in @($strictGamePlanCodes + $warningOnlyGamePlanCodes)) {
    Require ($gamePlanMap.ContainsKey([string] $gamePlanCode)) "UnknownGateGamePlan:$gamePlanCode"
}

$sliceIds = @($source.playableSlices.playableSliceId)
Assert-UniqueValues $sliceIds "DuplicatePlayableSlice"
$sliceReports = @()
$coveredGamePlanCodes = @()
$allHTraceMap = @{}

foreach ($slice in @($source.playableSlices)) {
    $sliceId = [string] $slice.playableSliceId
    Require (-not [string]::IsNullOrWhiteSpace($sliceId)) "PlayableSliceIdMissing"
    Require ([int] $slice.targetDurationMinutes.minimum -gt 0) "DurationMinimumInvalid:$sliceId"
    Require ([int] $slice.targetDurationMinutes.maximum -ge [int] $slice.targetDurationMinutes.minimum) "DurationRangeInvalid:$sliceId"
    Require (@($slice.gamePlanCodes).Count -gt 0) "SliceGamePlanMissing:$sliceId"
    foreach ($gamePlanCode in @($slice.gamePlanCodes)) {
        Require ($gamePlanMap.ContainsKey([string] $gamePlanCode)) "UnknownSliceGamePlan:${sliceId}:$gamePlanCode"
        $coveredGamePlanCodes += [string] $gamePlanCode
    }

    $stepIds = @($slice.steps.stepId)
    Require ($stepIds.Count -gt 0) "SliceStepMissing:$sliceId"
    Assert-UniqueValues $stepIds "DuplicateSliceStep:$sliceId"
    $stepMap = @{}
    $sliceHTraceMap = @{}
    $sliceWiIds = @()

    foreach ($step in @($slice.steps)) {
        $stepId = [string] $step.stepId
        $stepMap[$stepId] = $step
        Require (-not [string]::IsNullOrWhiteSpace([string] $step.verbCode)) "StepVerbMissing:${sliceId}:$stepId"
        Require (@($step.h2Refs).Count -gt 0) "StepH2Missing:${sliceId}:$stepId"
        Require (@($step.h3Refs).Count -gt 0) "StepH3Missing:${sliceId}:$stepId"
        Require (@($step.h4Refs).Count -gt 0) "StepH4Missing:${sliceId}:$stepId"
        Require (@($step.visibleConsequenceCodes).Count -gt 0) "StepVisibleConsequenceMissing:${sliceId}:$stepId"

        foreach ($wiId in @($step.wiIds)) {
            Require ($worldInteractionMap.ContainsKey([string] $wiId)) "UnknownWorldInteraction:${sliceId}:${stepId}:$wiId"
            $sliceWiIds += [string] $wiId
        }

        foreach ($contributionCode in @($step.supportingContributionCodes)) {
            Require ([string] $contributionCode -in $supportingContributionCodes) "UnknownSupportingContribution:${sliceId}:${stepId}:$contributionCode"
        }
        if (@($step.supportingH1Refs).Count -gt 0) {
            Require (@($step.supportingContributionCodes).Count -gt 0) "SupportingContributionMissing:${sliceId}:$stepId"
        }

        foreach ($h1Ref in @($step.directH1Refs)) {
            Require ($h1Map.ContainsKey([string] $h1Ref)) "UnknownH1:${sliceId}:${stepId}:$h1Ref"
            $h1 = $h1Map[[string] $h1Ref]
            Require (@($h1.wiIds).Count -gt 0 -or @($h1.anticipatedGameplayCodes).Count -gt 0) "DirectH1ContextMissing:${sliceId}:${stepId}:$h1Ref"
            Add-HTrace $sliceHTraceMap $traceOrder ([string] $h1Ref) "H1" "DirectAction" $stepId @()
            Add-HTrace $allHTraceMap $traceOrder ([string] $h1Ref) "H1" "DirectAction" $stepId @()
        }
        foreach ($h1Ref in @($step.supportingH1Refs)) {
            Require ($h1Map.ContainsKey([string] $h1Ref)) "UnknownSupportingH1:${sliceId}:${stepId}:$h1Ref"
            Add-HTrace $sliceHTraceMap $traceOrder ([string] $h1Ref) "H1" "Supporting" $stepId @($step.supportingContributionCodes)
            Add-HTrace $allHTraceMap $traceOrder ([string] $h1Ref) "H1" "Supporting" $stepId @($step.supportingContributionCodes)
        }

        $stepH1Refs = @($step.directH1Refs + $step.supportingH1Refs)
        foreach ($h2Ref in @($step.h2Refs)) {
            Require ($h2Map.ContainsKey([string] $h2Ref)) "UnknownH2:${sliceId}:${stepId}:$h2Ref"
            Require (Test-ChildCoverage $h2Map[[string] $h2Ref] $stepH1Refs "requiredH1Refs" "optionalH1Refs") "H2DoesNotContainStepH1:${sliceId}:${stepId}:$h2Ref"
            Add-HTrace $sliceHTraceMap $traceOrder ([string] $h2Ref) "H2" "SequenceMapped" $stepId @()
            Add-HTrace $allHTraceMap $traceOrder ([string] $h2Ref) "H2" "SequenceMapped" $stepId @()
        }
        foreach ($h3Ref in @($step.h3Refs)) {
            Require ($h3Map.ContainsKey([string] $h3Ref)) "UnknownH3:${sliceId}:${stepId}:$h3Ref"
            Require (Test-ChildCoverage $h3Map[[string] $h3Ref] @($step.h2Refs) "requiredH2Refs" "optionalH2Refs") "H3DoesNotContainStepH2:${sliceId}:${stepId}:$h3Ref"
            Add-HTrace $sliceHTraceMap $traceOrder ([string] $h3Ref) "H3" "LoopMapped" $stepId @()
            Add-HTrace $allHTraceMap $traceOrder ([string] $h3Ref) "H3" "LoopMapped" $stepId @()
        }
        foreach ($h4Ref in @($step.h4Refs)) {
            Require ($h4Map.ContainsKey([string] $h4Ref)) "UnknownH4:${sliceId}:${stepId}:$h4Ref"
            Require (Test-ChildCoverage $h4Map[[string] $h4Ref] @($step.h3Refs) "requiredH3Refs" "optionalH3Refs") "H4DoesNotContainStepH3:${sliceId}:${stepId}:$h4Ref"
            Add-HTrace $sliceHTraceMap $traceOrder ([string] $h4Ref) "H4" "RegionalCausalityMapped" $stepId @()
            Add-HTrace $allHTraceMap $traceOrder ([string] $h4Ref) "H4" "RegionalCausalityMapped" $stepId @()
        }

        $requiredRelationCodes = if ($step.PSObject.Properties.Name -contains "requiredRelationCodes") {
            @($step.requiredRelationCodes)
        }
        else {
            @()
        }
        foreach ($relationCode in $requiredRelationCodes) {
            Require (@($areaSetPriority.interAreaSetRelations.relationCode) -contains [string] $relationCode) "UnknownAreaSetRelation:${sliceId}:${stepId}:$relationCode"
        }
    }

    Require (@($slice.branches).Count -ge 2) "SliceNeedsNormalAndRecoveryBranches:$sliceId"
    $branchCodes = @($slice.branches.branchCode)
    Assert-UniqueValues $branchCodes "DuplicateBranch:$sliceId"
    foreach ($branch in @($slice.branches)) {
        Require (@($branch.stepIds).Count -gt 0) "BranchStepMissing:${sliceId}:$($branch.branchCode)"
        Require ([string] $branch.stepIds[0] -eq "day-start") "BranchMustStartAtDayStart:${sliceId}:$($branch.branchCode)"
        Require ([string] $branch.stepIds[-1] -eq "close-day") "BranchMustCloseDay:${sliceId}:$($branch.branchCode)"
        foreach ($stepId in @($branch.stepIds)) {
            Require ($stepMap.ContainsKey([string] $stepId)) "UnknownBranchStep:${sliceId}:$($branch.branchCode):$stepId"
        }
        Require (-not [string]::IsNullOrWhiteSpace([string] $branch.completionStateCode)) "BranchCompletionStateMissing:${sliceId}:$($branch.branchCode)"
    }

    foreach ($handoff in @($slice.regionalHandoffs)) {
        Require ($h4Map.ContainsKey([string] $handoff.fromH4Ref)) "UnknownHandoffFromH4:${sliceId}:$($handoff.handoffCode)"
        Require ($h4Map.ContainsKey([string] $handoff.toH4Ref)) "UnknownHandoffToH4:${sliceId}:$($handoff.handoffCode)"
        Require (-not [string]::IsNullOrWhiteSpace([string] $handoff.relationCode)) "HandoffRelationMissing:${sliceId}:$($handoff.handoffCode)"
        Require ([string] $handoff.theoryBindingStateCode -in @("E5TheoryQualified", "DesignLineageOnly")) "HandoffTheoryBindingStateInvalid:${sliceId}:$($handoff.handoffCode)"
        Require ([string] $handoff.actualBindingStateCode -in @("WaitingForActualE5Binding", "ActualE5Bound", "DesignLineageOnly")) "HandoffActualBindingStateInvalid:${sliceId}:$($handoff.handoffCode)"
        if ([string] $handoff.theoryBindingStateCode -eq "E5TheoryQualified") {
            $matchingTheoryRelation = @($theorySpatialFactory.interAreaSetRelations | Where-Object {
                [string] $_.relationCode -eq [string] $handoff.relationCode -and
                [string] $_.fromAreaSetCandidateRef -eq [string] $handoff.fromH4Ref -and
                [string] $_.toAreaSetCandidateRef -eq [string] $handoff.toH4Ref
            })
            Require ($matchingTheoryRelation.Count -eq 1) "HandoffTheoryRelationMissing:${sliceId}:$($handoff.handoffCode)"
        }
        if ([string] $handoff.actualBindingStateCode -eq "ActualE5Bound") {
            $matchingActualRelation = @($actualE5Spatial.network.relations | Where-Object {
                @($_.sourceStableIds) -contains [string] $handoff.relationCode
            })
            Require ($matchingActualRelation.Count -eq 1) "HandoffActualE5RelationMissing:${sliceId}:$($handoff.handoffCode)"
        }
    }

    foreach ($conditionSlot in @($slice.conditionSlots)) {
        Require (@($conditionSlot.allowedStateCodes).Count -gt 0) "ConditionStatesMissing:${sliceId}:$($conditionSlot.conditionSlotCode)"
        Require (@($conditionSlot.affectedStepIds).Count -gt 0) "ConditionStepsMissing:${sliceId}:$($conditionSlot.conditionSlotCode)"
        Require (@($conditionSlot.spatialExpressionH1Refs).Count -gt 0) "ConditionSpatialExpressionMissing:${sliceId}:$($conditionSlot.conditionSlotCode)"
        foreach ($stepId in @($conditionSlot.affectedStepIds)) {
            Require ($stepMap.ContainsKey([string] $stepId)) "UnknownConditionStep:${sliceId}:$($conditionSlot.conditionSlotCode):$stepId"
        }
        foreach ($h1Ref in @($conditionSlot.spatialExpressionH1Refs)) {
            Require ($h1Map.ContainsKey([string] $h1Ref)) "UnknownConditionH1:${sliceId}:$($conditionSlot.conditionSlotCode):$h1Ref"
            Require ($sliceHTraceMap.ContainsKey([string] $h1Ref)) "UntracedConditionH1:${sliceId}:$($conditionSlot.conditionSlotCode):$h1Ref"
        }
    }

    $sliceTheoryH2Refs = @($sliceHTraceMap.Values | Where-Object hierarchyLevelCode -eq "H2" | ForEach-Object knowledgeRef | Sort-Object -Unique)
    foreach ($h2Ref in $sliceTheoryH2Refs) {
        Require ($theoryH2Map.ContainsKey([string] $h2Ref)) "SliceH2NotTheoryQualified:${sliceId}:$h2Ref"
    }
    $sliceTheoryH3Refs = @($sliceHTraceMap.Values | Where-Object hierarchyLevelCode -eq "H3" | ForEach-Object knowledgeRef | Sort-Object -Unique)
    foreach ($h3Ref in $sliceTheoryH3Refs) {
        Require ($theoryH3Map.ContainsKey([string] $h3Ref)) "SliceH3NotTheoryQualified:${sliceId}:$h3Ref"
    }
    $sliceTheoryH4Refs = @($sliceHTraceMap.Values | Where-Object hierarchyLevelCode -eq "H4" | ForEach-Object knowledgeRef | Sort-Object -Unique)
    $sliceTheoryAreaSets = @()
    foreach ($gamePlanCode in @($slice.gamePlanCodes)) {
        Require ($theoryAreaSetsByGamePlan.ContainsKey([string] $gamePlanCode)) "SliceTheoryAreaSetMissing:${sliceId}:$gamePlanCode"
        $theoryAreaSet = $theoryAreaSetsByGamePlan[[string] $gamePlanCode]
        Require ([string] $theoryAreaSet.worldIntentRef -in $sliceTheoryH4Refs) "SliceTheoryWorldIntentUntraced:${sliceId}:$gamePlanCode"
        foreach ($graphInstance in @($theoryAreaSet.graphInstances)) {
            Require ([string] $graphInstance.h3Ref -in $sliceTheoryH3Refs) "SliceTheoryGraphUntraced:${sliceId}:$($graphInstance.h3Ref)"
        }
        $sliceTheoryAreaSets += $theoryAreaSet
    }
    Require ([string] $slice.theorySpatialBindingStateCode -eq "E5TheoryQualified") "SliceTheorySpatialStateInvalid:$sliceId"
    Require ([string] $slice.actualSpatialBindingStateCode -eq "ActualE5Bound") "SliceActualSpatialStateInvalid:$sliceId"
    $sliceActualAreaSets = @()
    foreach ($theoryAreaSet in $sliceTheoryAreaSets) {
        $theoryAreaSetStableId = [string] $theoryAreaSet.areaSetStableId
        Require ($actualAreaSetByTheory.ContainsKey($theoryAreaSetStableId)) "SliceActualE5AreaSetMissing:${sliceId}:$theoryAreaSetStableId"
        $sliceActualAreaSets += $actualAreaSetByTheory[$theoryAreaSetStableId]
    }

    $plannedEvidenceRefs = @($slice.completionGates.planned.evidenceRefs)
    Require ($plannedEvidenceRefs.Count -gt 0) "PlannedEvidenceMissing:$sliceId"
    foreach ($evidenceRef in $plannedEvidenceRefs) {
        Require (Test-Path -LiteralPath (Resolve-RepositoryPath $repositoryRoot ([string] $evidenceRef))) "PlannedEvidenceFileMissing:${sliceId}:$evidenceRef"
    }
    $theoryEvidenceRefs = @($slice.completionGates.theorySpatiallyComposed.evidenceRefs)
    Require ($theoryEvidenceRefs.Count -gt 0) "TheorySpatialEvidenceMissing:$sliceId"
    Require (@($theoryEvidenceRefs | Where-Object { (Resolve-RepositoryPath $repositoryRoot ([string] $_)) -eq $theorySpatialFactoryPath }).Count -eq 1) "TheorySpatialFactoryEvidenceRefMissing:$sliceId"
    $actualE5EvidenceRefs = @($slice.completionGates.spatiallyComposed.evidenceRefs)
    Require ($actualE5EvidenceRefs.Count -gt 0) "ActualE5SpatialEvidenceMissing:$sliceId"
    Require (@($actualE5EvidenceRefs | Where-Object { (Resolve-RepositoryPath $repositoryRoot ([string] $_)) -eq $actualE5SpatialPath }).Count -eq 1) "ActualE5SpatialEvidenceRefMissing:$sliceId"
    Require (@($slice.completionGates.spatiallyComposed.blockReasonCodes).Count -eq 0) "ActualE5SpatialGateBlocked:$sliceId"

    $completionBlockers = @(
        @($slice.completionGates.theorySpatiallyComposed.blockReasonCodes) +
        @($slice.completionGates.spatiallyComposed.blockReasonCodes) +
        @($slice.completionGates.functionallyClosed.blockReasonCodes) +
        @($slice.completionGates.experienceValidated.blockReasonCodes) +
        @($slice.completionGates.playableSliceComplete.blockReasonCodes) |
            Sort-Object -Unique
    )
    $currentPlayableSliceStateCode = Get-CurrentPlayableSliceState $slice.completionGates
    Require ([string] $slice.declaredPlayableSliceStateCode -eq $currentPlayableSliceStateCode) "DeclaredPlayableStateMismatch:${sliceId}:$currentPlayableSliceStateCode"

    $wiEvidence = @()
    foreach ($wiId in @($sliceWiIds | Sort-Object -Unique)) {
        $interaction = $worldInteractionMap[$wiId]
        Require ([string] $interaction.implementation.currentStage -in $evidenceStageCodes) "UnknownImplementationStage:${sliceId}:$wiId"
        Require ([string] $interaction.integration.currentStage -in $evidenceStageCodes) "UnknownIntegrationStage:${sliceId}:$wiId"
        [object[]] $e4SeedbedRefs = @()
        [object[]] $e5PlacementRefs = @()
        [object[]] $e5ContextRefs = @()
        [object[]] $e6EvidenceRefs = @()
        [object[]] $e7EvidenceRefs = @()
        if ($interaction.integration.PSObject.Properties.Name -contains "e4SeedbedRefs") { $e4SeedbedRefs = @($interaction.integration.e4SeedbedRefs) }
        if ($interaction.integration.PSObject.Properties.Name -contains "e5PlacementRefs") { $e5PlacementRefs = @($interaction.integration.e5PlacementRefs) }
        if ($interaction.integration.PSObject.Properties.Name -contains "e6EvidenceRefs") { $e6EvidenceRefs = @($interaction.integration.e6EvidenceRefs) }
        if ($interaction.integration.PSObject.Properties.Name -contains "e7EvidenceRefs") { $e7EvidenceRefs = @($interaction.integration.e7EvidenceRefs) }
        $directBinding = if ($actualDirectBindingsByWi.ContainsKey($wiId)) { $actualDirectBindingsByWi[$wiId] } else { $null }
        $contextBinding = if ($actualContextBindingsByWi.ContainsKey($wiId)) { $actualContextBindingsByWi[$wiId] } else { $null }
        $isDeclaredNonSpatial = $actualNonSpatialWiIds -contains $wiId
        Require ($null -ne $directBinding -or $null -ne $contextBinding -or $isDeclaredNonSpatial) "SliceWiActualE5ClassificationMissing:${sliceId}:$wiId"
        if ($null -ne $directBinding) { $e5PlacementRefs = @($e5PlacementRefs + [string] $directBinding.spatialStableId | Sort-Object -Unique) }
        if ($null -ne $contextBinding) { $e5ContextRefs = @([string] $contextBinding.contextStableId) }
        $catalogIntegrationStageCode = [string] $interaction.integration.currentStage
        $effectiveIntegrationStageCode = $catalogIntegrationStageCode
        if (($null -ne $directBinding -or $null -ne $contextBinding) -and
            [Array]::IndexOf([object[]] $evidenceStageCodes, $catalogIntegrationStageCode) -lt [Array]::IndexOf([object[]] $evidenceStageCodes, "E5")) {
            $effectiveIntegrationStageCode = "E5"
        }
        $wiEvidence += [ordered]@{
            wiId = $wiId
            title = [string] $interaction.title
            implementationStageCode = [string] $interaction.implementation.currentStage
            catalogIntegrationStageCode = $catalogIntegrationStageCode
            integrationStageCode = $effectiveIntegrationStageCode
            e4SeedbedRefs = $e4SeedbedRefs
            e5PlacementRefs = $e5PlacementRefs
            e5ContextRefs = $e5ContextRefs
            e6EvidenceRefs = $e6EvidenceRefs
            e7EvidenceRefs = $e7EvidenceRefs
        }
    }

    $sliceReports += [ordered]@{
        playableSliceId = $sliceId
        title = [string] $slice.title
        playerPromise = [string] $slice.playerPromise
        targetDurationMinutes = $slice.targetDurationMinutes
        playModeCode = [string] $slice.playModeCode
        targetPlatformCode = [string] $slice.targetPlatformCode
        canonicalScenePath = [string] $slice.canonicalScenePath
        gamePlanCodes = @($slice.gamePlanCodes)
        currentPlayableSliceStateCode = $currentPlayableSliceStateCode
        targetPlayableSliceStateCode = [string] $slice.targetPlayableSliceStateCode
        theorySpatialBindingStateCode = [string] $slice.theorySpatialBindingStateCode
        theoryAreaSetStableIds = @($sliceTheoryAreaSets.areaSetStableId | Sort-Object -Unique)
        actualSpatialBindingStateCode = [string] $slice.actualSpatialBindingStateCode
        actualAreaSetStableIds = @($sliceActualAreaSets.definition.areaSetStableId | Sort-Object -Unique)
        actualNetworkStableId = [string] $actualE5Spatial.network.networkStableId
        stepCount = @($slice.steps).Count
        branchCount = @($slice.branches).Count
        completionBlockReasonCodes = $completionBlockers
        hTrace = @($sliceHTraceMap.Values | Sort-Object hierarchyLevelCode, knowledgeRef)
        wiEvidence = $wiEvidence
        regionalHandoffs = @($slice.regionalHandoffs)
        conditionSlots = @($slice.conditionSlots)
    }
}

$coveredGamePlanCodes = @($coveredGamePlanCodes | Sort-Object -Unique)
$strictMissingGamePlanCodes = @($strictGamePlanCodes | Where-Object { $_ -notin $coveredGamePlanCodes } | Sort-Object)
Require ($strictMissingGamePlanCodes.Count -eq 0) "StrictGamePlanNotCovered:$($strictMissingGamePlanCodes -join ',')"
$warningOnlyMissingGamePlanCodes = @($warningOnlyGamePlanCodes | Where-Object { $_ -notin $coveredGamePlanCodes } | Sort-Object)

$hTrace = @($allHTraceMap.Values | Sort-Object hierarchyLevelCode, knowledgeRef)
$counts = [ordered]@{
    playableSlices = $sliceReports.Count
    coveredGamePlans = $coveredGamePlanCodes.Count
    strictGamePlans = $strictGamePlanCodes.Count
    warningOnlyGamePlans = $warningOnlyGamePlanCodes.Count
    warningOnlyMissingGamePlans = $warningOnlyMissingGamePlanCodes.Count
    h1Traces = @($hTrace | Where-Object hierarchyLevelCode -eq "H1").Count
    h2Traces = @($hTrace | Where-Object hierarchyLevelCode -eq "H2").Count
    h3Traces = @($hTrace | Where-Object hierarchyLevelCode -eq "H3").Count
    h4Traces = @($hTrace | Where-Object hierarchyLevelCode -eq "H4").Count
    completionBlockers = @($sliceReports.completionBlockReasonCodes | Sort-Object -Unique).Count
}

$result = [ordered]@{
    schemaVersion = "simulation-world-gameplay-spatial-completion-report.v1"
    revision = "simulation-world-gameplay-spatial-completion-report.r3"
    sourceRevision = [string] $source.revision
    hCatalogRevision = [string] $catalog.revision
    worldInteractionRevision = [string] $worldInteractionCatalog.revision
    evidenceStageRevision = [string] $evidenceStageCatalog.revision
    theorySpatialFactoryRevision = [string] $theorySpatialFactory.revision
    theorySpatialFactoryPolicyRevision = [string] $theorySpatialFactory.policyRevision
    actualE5SpatialRevision = [string] $actualE5Spatial.revision
    actualE5SpatialPolicyRevision = [string] $actualE5Spatial.policyRevision
    axisDefinitions = [ordered]@{
        h = "공간 자원의 종류와 H1→H4 조립 깊이"
        gameplayTrace = "공간이 기준 플레이를 직접 또는 간접 지원하는 정도"
        evidence = "E0→E9 구현·통합 증거 깊이"
        playableSlice = "사람이 시작부터 다음 날까지 완주하는 마감 상태"
    }
    gatePolicy = $source.gatePolicy
    counts = $counts
    coveredGamePlanCodes = $coveredGamePlanCodes
    warningOnlyMissingGamePlanCodes = $warningOnlyMissingGamePlanCodes
    hTrace = $hTrace
    playableSlices = $sliceReports
    nextPlayableSliceQueue = @($source.nextPlayableSliceQueue)
    authorityBoundary = [string] $source.authorityBoundary
    presentationOnly = $true
    isOperationalState = $false
}

$json = ($result | ConvertTo-Json -Depth 30) + "`n"
$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine("# 게임플레이·H 공간·E 증거·완성 단위 대장")
[void] $builder.AppendLine()
[void] $builder.AppendLine("> 이 문서는 ``$SourcePath``에서 자동 생성된다. 직접 수정하지 않는다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 네 축")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- **H 구조:** 공간 자원의 종류와 H1→H4 조립 깊이")
[void] $builder.AppendLine("- **게임플레이 추적:** 공간이 기준 플레이를 직접 또는 간접 지원하는 정도")
[void] $builder.AppendLine("- **E 증거:** E0→E9 구현·통합 증거 깊이")
[void] $builder.AppendLine("- **완성 단위:** 사람이 시작부터 다음 날까지 완주하는 마감 상태")
[void] $builder.AppendLine()
[void] $builder.AppendLine("이론 공간은 게임플레이 추적·사람 검토를 기다리지 않고 생산한다. ``E5TheoryQualified``와 실제 E5 결속·E7 완주는 서로 다른 사실이다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 현재 완성 단위")
[void] $builder.AppendLine()
[void] $builder.AppendLine("| 기준 플레이 | 현재 상태 | 이론 공간 | 실제 공간 | 목표 | 단계/분기 | H1/H2/H3/H4 | 차단 사유 |")
[void] $builder.AppendLine("| --- | --- | --- | --- | --- | ---: | --- | --- |")
foreach ($slice in $sliceReports) {
    $h1Count = @($slice.hTrace | Where-Object hierarchyLevelCode -eq "H1").Count
    $h2Count = @($slice.hTrace | Where-Object hierarchyLevelCode -eq "H2").Count
    $h3Count = @($slice.hTrace | Where-Object hierarchyLevelCode -eq "H3").Count
    $h4Count = @($slice.hTrace | Where-Object hierarchyLevelCode -eq "H4").Count
    [void] $builder.AppendLine("| $($slice.title) (``$($slice.playableSliceId)``) | ``$($slice.currentPlayableSliceStateCode)`` | ``$($slice.theorySpatialBindingStateCode)`` ($(@($slice.theoryAreaSetStableIds).Count)) | ``$($slice.actualSpatialBindingStateCode)`` ($(@($slice.actualAreaSetStableIds).Count)) | ``$($slice.targetPlayableSliceStateCode)`` | $($slice.stepCount)/$($slice.branchCount) | $h1Count/$h2Count/$h3Count/$h4Count | $(@($slice.completionBlockReasonCodes) -join ', ') |")
}
[void] $builder.AppendLine()
[void] $builder.AppendLine("## H 게임플레이 추적")
[void] $builder.AppendLine()
[void] $builder.AppendLine("| H 참조 | 계층 | 게임플레이 추적 | 단계 | 기여 |")
[void] $builder.AppendLine("| --- | --- | --- | --- | --- |")
foreach ($trace in $hTrace) {
    [void] $builder.AppendLine("| ``$($trace.knowledgeRef)`` | ``$($trace.hierarchyLevelCode)`` | ``$($trace.gameplayTraceStateCode)`` | $(@($trace.stepIds) -join ', ') | $(@($trace.contributionCodes) -join ', ') |")
}
[void] $builder.AppendLine()
[void] $builder.AppendLine("## WI E 증거")
[void] $builder.AppendLine()
[void] $builder.AppendLine("| 기준 플레이 | WI | 구현 | 통합 | E5 직접 배치 | E5 문맥 | E7 플레이 |")
[void] $builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |")
foreach ($slice in $sliceReports) {
    foreach ($wi in @($slice.wiEvidence)) {
        [void] $builder.AppendLine("| ``$($slice.playableSliceId)`` | $($wi.title) (``$($wi.wiId)``) | ``$($wi.implementationStageCode)`` | ``$($wi.integrationStageCode)`` | $(@($wi.e5PlacementRefs).Count) | $(@($wi.e5ContextRefs).Count) | $(@($wi.e7EvidenceRefs).Count) |")
    }
}
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 경고와 다음 순서")
[void] $builder.AppendLine()
if ($warningOnlyMissingGamePlanCodes.Count -eq 0) {
    [void] $builder.AppendLine("- 경고 전용 게임 기획 누락 없음")
}
else {
    [void] $builder.AppendLine("- 아직 기준 플레이가 없는 경고 전용 기획: $($warningOnlyMissingGamePlanCodes -join ', ')")
}
foreach ($nextSlice in @($source.nextPlayableSliceQueue | Sort-Object priority)) {
    [void] $builder.AppendLine("- $($nextSlice.priority). $($nextSlice.title) (``$($nextSlice.playableSliceId)``) — ``$($nextSlice.blockedByPlayableSliceId)`` 완료 뒤 시작")
}
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 판정 경계")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- 지원 경관은 이동·가독성·완충·분위기·상태 표현 기여가 있으면 유지한다.")
[void] $builder.AppendLine("- 게임플레이 추적 누락은 이론 H2·H3·E5 생산을 막지 않고 게임플레이 우선순위에만 영향을 준다.")
[void] $builder.AppendLine("- H 승인, 촬영 ``Good``, E7과 ``PlayableSliceComplete``는 서로 다른 사실이다.")
[void] $builder.AppendLine("- 카드 조건은 공간 표현 연결점만 기록하며 수치와 효과 권위는 서버에 남긴다.")
[void] $builder.AppendLine("- Town·Hub 누락은 현재 경고이며 Nature·Farm 기준 플레이 누락만 검증을 차단한다.")
$markdown = $builder.ToString()

$resolvedJsonOutputPath = Resolve-RepositoryPath $repositoryRoot $JsonOutputPath
$resolvedMarkdownOutputPath = Resolve-RepositoryPath $repositoryRoot $MarkdownOutputPath
if ($Mode -eq "Write") {
    Write-DeterministicTextIfChanged $resolvedJsonOutputPath $json | Out-Null
    Write-DeterministicTextIfChanged $resolvedMarkdownOutputPath $markdown | Out-Null
    Write-Output "GameplaySpatialCompletionGenerated:Slices=$($counts.playableSlices);Strict=$($counts.strictGamePlans);Warnings=$($counts.warningOnlyMissingGamePlans);H=$($counts.h1Traces)/$($counts.h2Traces)/$($counts.h3Traces)/$($counts.h4Traces);Blockers=$($counts.completionBlockers)"
}
else {
    Require (Test-Path -LiteralPath $resolvedJsonOutputPath) "JsonOutputMissing"
    Require (Test-Path -LiteralPath $resolvedMarkdownOutputPath) "MarkdownOutputMissing"
    Require ((ConvertTo-DeterministicText (Get-Content -LiteralPath $resolvedJsonOutputPath -Raw -Encoding UTF8)) -ceq (ConvertTo-DeterministicText $json)) "JsonOutputStale"
    Require ((ConvertTo-DeterministicText (Get-Content -LiteralPath $resolvedMarkdownOutputPath -Raw -Encoding UTF8)) -ceq (ConvertTo-DeterministicText $markdown)) "MarkdownOutputStale"
    Write-Output "GameplaySpatialCompletionValid:Slices=$($counts.playableSlices);Strict=$($counts.strictGamePlans);Warnings=$($counts.warningOnlyMissingGamePlans);H=$($counts.h1Traces)/$($counts.h2Traces)/$($counts.h3Traces)/$($counts.h4Traces);Blockers=$($counts.completionBlockers)"
}
