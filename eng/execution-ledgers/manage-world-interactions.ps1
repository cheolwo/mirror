[CmdletBinding()]
param(
    [ValidateSet("Write", "Check")]
    [string] $Mode = "Check",
    [string] $InputPath = "eng/execution-ledgers/world-interactions.json",
    [string] $OutputPath = "docs/AI/generated/world-interaction-catalog.md",
    [string] $ContractOutputPath = "Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldInteractionNames.generated.cs"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "../common/deterministic-text-output.ps1")

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "WorldInteractionCatalogInvalid:$Code" }
}

function Get-StageIndex([object[]] $Stages, [string] $Code) {
    for ($index = 0; $index -lt $Stages.Count; $index++) {
        if ([string] $Stages[$index].code -eq $Code) { return $index }
    }
    return -1
}

function Require-Text([object] $Value, [string] $Code) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $Value)) $Code
}

function Escape-Markdown([string] $Value) {
    if ($null -eq $Value) { return "" }
    return $Value.Replace("|", "\|").Replace("`r", " ").Replace("`n", " ")
}

function Escape-CSharpString([string] $Value) {
    if ($null -eq $Value) { return "" }
    return $Value.Replace("\", "\\").Replace('"', '\"')
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
. (Join-Path $PSScriptRoot 'world-interaction-registration-functions.ps1')
$null = Read-WorldInteractionRegistration $repositoryRoot 'eng/execution-ledgers/world-interaction-registration-relations.json' $InputPath
$resolvedInput = (Resolve-Path (Join-Path $repositoryRoot $InputPath)).Path
$catalog = Get-Content -LiteralPath $resolvedInput -Raw -Encoding UTF8 | ConvertFrom-Json
$resolvedStageCatalog = (Resolve-Path (Join-Path $repositoryRoot ([string] $catalog.evidenceStageCatalogPath))).Path
$stageCatalog = Get-Content -LiteralPath $resolvedStageCatalog -Raw -Encoding UTF8 | ConvertFrom-Json
$evidenceStages = @($stageCatalog.stages)
$resolvedTriggerCatalog = (Resolve-Path (Join-Path $repositoryRoot ([string] $catalog.triggerSourceCatalogPath))).Path
$triggerCatalog = Get-Content -LiteralPath $resolvedTriggerCatalog -Raw -Encoding UTF8 |
    ConvertFrom-Json
$resolvedResponsibilityPolicy = (Resolve-Path (Join-Path $repositoryRoot (
    [string] $catalog.responsibilityPolicyPath))).Path
$responsibilityPolicy = Get-Content -LiteralPath $resolvedResponsibilityPolicy -Raw -Encoding UTF8 |
    ConvertFrom-Json
$resolvedWorkflowCatalog = (Resolve-Path (Join-Path $repositoryRoot (
    [string] $catalog.workflowCatalogPath))).Path
$workflowCatalog = Get-Content -LiteralPath $resolvedWorkflowCatalog -Raw -Encoding UTF8 |
    ConvertFrom-Json
$resolvedPolarityCatalog = (Resolve-Path (Join-Path $repositoryRoot (
    [string] $catalog.polarityQuadrantCatalogPath))).Path
$polarityCatalog = Get-Content -LiteralPath $resolvedPolarityCatalog -Raw -Encoding UTF8 |
    ConvertFrom-Json
$resolvedPlayableLoopCatalog = (Resolve-Path (Join-Path $repositoryRoot (
    [string] $polarityCatalog.playableLoopCatalogPath))).Path
$playableLoopCatalog = Get-Content -LiteralPath $resolvedPlayableLoopCatalog -Raw -Encoding UTF8 |
    ConvertFrom-Json

Require-Text $catalog.catalogKey "CatalogKeyMissing"
Require-Text $catalog.revision "RevisionMissing"
Require-Text $catalog.evidenceStageCatalogPath "EvidenceStageCatalogPathMissing"
Require-Text $catalog.triggerSourceCatalogPath "TriggerSourceCatalogPathMissing"
Require-Text $catalog.responsibilityPolicyPath "ResponsibilityPolicyPathMissing"
Require-Text $catalog.workflowCatalogPath "WorkflowCatalogPathMissing"
Require-Text $catalog.polarityQuadrantCatalogPath "PolarityQuadrantCatalogPathMissing"
Require-Text $catalog.catalogOrderMeaning "CatalogOrderMeaningMissing"
Require ([string] $catalog.defaultImplementationTargetStage -eq "E3") "DefaultImplementationTargetMustBeE3"
Require ([string] $catalog.defaultIntegrationTargetStage -eq "E7") "DefaultIntegrationTargetMustBeE7"
Require ([string] $stageCatalog.schemaVersion -eq "simulation-evidence-stages.v7") "EvidenceStageCatalogSchemaInvalid"
Require ($evidenceStages.Count -eq 11) "EvidenceStagesMustHaveElevenEntries"
Require ((@($evidenceStages.code) -join ",") -eq
    "E0,E1,E2,E3,E4,E5,E6,E7,E8,E9,E10") "EvidenceStageOrderInvalid"
$worldInteractionCount = @($catalog.items).Count
Require ($worldInteractionCount -gt 0) "WorldInteractionCountInvalid"
Require ([string] $catalog.schemaVersion -eq "5") "WorldInteractionCatalogSchemaMustBe5"
Require ([string] $triggerCatalog.schemaVersion -eq
    "world-interaction-trigger-sources.v1") "TriggerSourceCatalogSchemaInvalid"
Require ([string] $responsibilityPolicy.schemaVersion -eq
    "simulation-world-interaction-responsibilities.v1") "ResponsibilityPolicySchemaInvalid"
Require ([string] $workflowCatalog.schemaVersion -eq
    "simulation-world-interaction-flows.v1") "WorkflowCatalogSchemaInvalid"
Require ([string] $polarityCatalog.schemaVersion -eq
    "world-interaction-polarity-quadrants.v1") "PolarityCatalogSchemaInvalid"
Require ([string] $playableLoopCatalog.schemaVersion -eq
    "ssalddel-playable-loop-catalog.v6") "PlayableLoopCatalogSchemaInvalid"
Require ([bool] $triggerCatalog.principles.observationCollectionIsNotWorldInteraction -and
    [bool] $triggerCatalog.principles.meaningfulAuthorityTransitionCreatesWorldInteraction -and
    [bool] $triggerCatalog.principles.executionInstanceStoresSingleSource -and
    [bool] $triggerCatalog.principles.clientCannotChooseTrustedSource) "TriggerSourcePrinciplesMissing"
$triggerSourceCodes = @($triggerCatalog.triggerSourceCodes.code)
Require (($triggerSourceCodes -join ",") -eq
    "DataDriven,PlayerDriven,NpcDriven,WorldDerived") "TriggerSourceCodesInvalid"
$originCodes = @($catalog.originCodes)
$controlPolicyCodes = @($catalog.controlPolicyCodes)
$groupDisplayNames = $catalog.groupDisplayNames
Require ($null -ne $groupDisplayNames) "GroupDisplayNamesMissing"
Require (($originCodes -join ",") -eq
    "OperationsDerived,SimulationNative,Hybrid") "OriginCodesInvalid"
Require (($controlPolicyCodes -join ",") -eq
    "NpcRoutine,PlayerOrNpc,PlayerDirect,WorldAutomatic") "ControlPolicyCodesInvalid"
$responsibilityPrinciples = $responsibilityPolicy.principles
foreach ($principle in @(
    "oneIntentPerWorldInteraction",
    "onePrimaryAuthorityOutcomePerWorldInteraction",
    "actorBindingDoesNotChangeResponsibility",
    "previewConfirmTaskEffectAreOneResponsibilityLifecycle",
    "atomicConsistencySideEffectsAreAllowed",
    "workflowOrderingIsOwnedOutsideWorldInteraction",
    "delegationAndDelegatedActionAreSeparateResponsibilities",
    "automaticTaskPhaseIsNotWorldInteraction",
    "actorWorkCannotRemainWorldAutomatic",
    "legacyInlineFlowHintsAreNonAuthoritative")) {
    $property = $responsibilityPrinciples.PSObject.Properties[$principle]
    Require ($null -ne $property -and [bool] $property.Value) "ResponsibilityPrincipleMissing:$principle"
}
$workflowPrinciples = $workflowCatalog.principles
foreach ($principle in @(
    "flowComposesWorldInteractionsWithoutOwningThem",
    "worldInteractionCanParticipateInMultipleFlows",
    "flowOrderDoesNotBecomeWorldInteractionIdentity",
    "optionalAreaConnectorDoesNotCreateAreaDependency",
    "loopsAndBranchesAreExplicit")) {
    $property = $workflowPrinciples.PSObject.Properties[$principle]
    Require ($null -ne $property -and [bool] $property.Value) "WorkflowPrincipleMissing:$principle"
}
Require ((@($responsibilityPolicy.assessmentCodes) -join ",") -eq
    "Atomic,AtomicBundle,AlternativeOutcomes,LegacyCompositeMigrationRequired,ProceduralStepMigrationRequired,ActorResponsibilityMigrationRequired") `
    "ResponsibilityAssessmentCodesInvalid"
Require ((@($workflowCatalog.flowKindCodes) -join ",") -eq
    "IndependentAreaLoop,OptionalAreaConnector,BranchingAreaLoop") "WorkflowKindCodesInvalid"
$polarityPrinciples = $polarityCatalog.principles
foreach ($principle in @(
    "firstSignRepresentsActorActionPurpose",
    "secondSignRepresentsActualActorBinding",
    "signsDoNotRepresentGoodEvilRewardOrPenalty",
    "quadrantBelongsToExecutionInstance",
    "triggerSourceDoesNotDetermineActorSign",
    "classificationDoesNotChangeSimulationEffects",
    "pureAutomaticTransitionRemainsOutsideQuadrants",
    "contextualPolarityUsesApprovedPlayableLoopBinding",
    "classificationFreezesForInvocationLifetime",
    "missingContextDoesNotBlockWorldInteraction")) {
    $property = $polarityPrinciples.PSObject.Properties[$principle]
    Require ($null -ne $property -and [bool] $property.Value) `
        "PolarityPrincipleMissing:$principle"
}
Require ((@($polarityCatalog.polarityCodes) -join ",") -eq
    "Yang,Yin,Contextual,NotApplicable,Unclassified") "PolarityCodesInvalid"
Require ((@($polarityCatalog.actorCodes) -join ",") -eq
    "PlayerActor,NpcActor,NotApplicable") "PolarityActorCodesInvalid"
Require ((@($polarityCatalog.classificationCodes) -join ",") -eq
    "Fixed,ExecutionContext,NotApplicable") "PolarityClassificationCodesInvalid"
Require ((@($polarityCatalog.quadrants.code) -join ",") -eq
    "YangPlayer,YangNpc,YinPlayer,YinNpc") "QuadrantCodesInvalid"
Require ((@($polarityCatalog.quadrants.symbol) -join ",") -eq
    "++,+-,-+,--") "QuadrantSymbolsInvalid"
$resolvedTriggerSourcesByWi = @{}

$seedbedRoot = Join-Path $repositoryRoot "eng/world-seedbeds/wi-spatial-seedbeds"
$seedbedCatalog = Get-Content -LiteralPath (Join-Path $seedbedRoot "catalog.json") -Raw -Encoding UTF8 |
    ConvertFrom-Json
$approvedSeedbedIds = @{}
foreach ($definitionRef in @($seedbedCatalog.definitionRefs)) {
    $definition = Get-Content -LiteralPath (Join-Path $seedbedRoot ([string] $definitionRef)) -Raw -Encoding UTF8 |
        ConvertFrom-Json
    Require ([string] $definition.reviewStatusCode -eq "ApprovedForSimulation") "SeedbedNotApproved:$($definition.stableId)"
    $approvedSeedbedIds[[string] $definition.stableId] = $true
}

$allowedKinds = @("Command", "AutomaticTransition", "SharedPolicy")
$allowedImplementationStatuses = @("NotStarted", "InProgress", "Blocked", "Done")
$allowedIntegrationStatuses = @("NotSelected", "Selected", "InProgress", "Done", "Blocked")
$itemsById = @{}

foreach ($item in @($catalog.items)) {
    $id = [string] $item.id
    Require-Text $id "ItemIdMissing"
    Require (-not $itemsById.ContainsKey($id)) "DuplicateItemId:$id"
    Require ($allowedKinds -contains [string] $item.kind) "UnknownKind:$id"
    Require ($originCodes -contains [string] $item.originCode) "OriginCodeInvalid:$id"
    Require ($controlPolicyCodes -contains [string] $item.controlPolicyCode) "ControlPolicyCodeInvalid:$id"
    Require-Text $item.groupCode "GroupCodeMissing:$id"
    Require ([int] $item.sequence -gt 0) "SequenceInvalid:$id"
    Require-Text $item.title "TitleMissing:$id"
    Require ([string] $item.title -match "[가-힣]") "KoreanTitleMissing:$id"
    $groupDisplayName = $groupDisplayNames.PSObject.Properties[[string] $item.groupCode]
    Require ($null -ne $groupDisplayName) "GroupDisplayNameMissing:$($item.groupCode)"
    Require-Text $groupDisplayName.Value "GroupDisplayNameEmpty:$($item.groupCode)"
    Require ([string] $groupDisplayName.Value -match "[가-힣]") "KoreanGroupDisplayNameMissing:$($item.groupCode)"
    Require-Text $item.worldAction "WorldActionMissing:$id"
    Require (@($item.startStateCodes).Count -gt 0) "StartStateMissing:$id"
    Require (@($item.completionStateCodes).Count -gt 0) "CompletionStateMissing:$id"
    Require-Text $item.previewRule "PreviewRuleMissing:$id"
    Require-Text $item.confirmRule "ConfirmRuleMissing:$id"
    Require-Text $item.taskRule "TaskRuleMissing:$id"
    Require (@($item.effectCodes).Count -gt 0) "EffectMissing:$id"
    Require-Text $item.cancellationPolicy "CancellationPolicyMissing:$id"
    Require-Text $item.actionCode "ActionCodeMissing:$id"
    Require-Text $item.ruleRevision "RuleRevisionMissing:$id"
    Require (@($item.saveReplayPayloadCodes).Count -gt 0) "SaveReplayPayloadMissing:$id"
    Require (@($item.httpContracts).Count -gt 0) "HttpContractMissing:$id"
    Require (@($item.sourceReferences).Count -gt 0) "SourceReferenceMissing:$id"

    $override = $triggerCatalog.overrides.PSObject.Properties[$id]
    $allowedTriggerSources = @(if ($null -ne $override) { @($override.Value) }
        else {
            $default = $triggerCatalog.defaultAllowedByInteractionKind.PSObject.Properties[
                [string] $item.kind]
            Require ($null -ne $default) "TriggerSourceDefaultMissing:$id"
            @($default.Value)
        })
    Require ($allowedTriggerSources.Count -gt 0) "AllowedTriggerSourceMissing:$id"
    Require (@($allowedTriggerSources | Select-Object -Unique).Count -eq
        $allowedTriggerSources.Count) "AllowedTriggerSourceDuplicate:$id"
    foreach ($triggerSource in $allowedTriggerSources) {
        Require ($triggerSourceCodes -contains [string] $triggerSource) "AllowedTriggerSourceUnknown:${id}:$triggerSource"
    }
    $resolvedTriggerSourcesByWi[$id] = @($allowedTriggerSources)

    $implementation = $item.implementation
    $integration = $item.integration
    Require ($allowedImplementationStatuses -contains [string] $implementation.status) "ImplementationStatusInvalid:$id"
    Require ($allowedIntegrationStatuses -contains [string] $integration.status) "IntegrationStatusInvalid:$id"
    $implementationCurrent = Get-StageIndex $evidenceStages ([string] $implementation.currentStage)
    $implementationTarget = Get-StageIndex $evidenceStages ([string] $implementation.targetStage)
    $integrationCurrent = Get-StageIndex $evidenceStages ([string] $integration.currentStage)
    $integrationTarget = Get-StageIndex $evidenceStages ([string] $integration.targetStage)
    Require ($implementationCurrent -ge 0 -and $implementationTarget -ge 0) "ImplementationStageInvalid:$id"
    Require ($integrationCurrent -ge 0 -and $integrationTarget -ge 0) "IntegrationStageInvalid:$id"
    Require ($implementationCurrent -le $implementationTarget) "ImplementationStageExceedsTarget:$id"
    Require ($integrationCurrent -le $integrationTarget) "IntegrationStageExceedsTarget:$id"
    Require (@("E3", "E4") -contains [string] $implementation.targetStage) "ImplementationTargetMustBeE3OrE4:$id"
    Require ([string] $integration.targetStage -eq "E7") "IntegrationTargetMustBeE7:$id"
    if ([string] $implementation.status -eq "Done") {
        Require ([string] $implementation.currentStage -eq [string] $implementation.targetStage) "ImplementationDoneBeforeTarget:$id"
        Require (@($implementation.evidence).Count -gt 0) "ImplementationEvidenceMissing:$id"
    }
    if ($integrationCurrent -ge (Get-StageIndex $evidenceStages "E4") -and
        @($item.spatialRequirements).Count -gt 0 -and
        $implementationCurrent -lt (Get-StageIndex $evidenceStages "E4")) {
        Require ($integration.PSObject.Properties.Name -contains "e4SeedbedRefs") "E4SeedbedRefsMissing:$id"
        Require (@($integration.e4SeedbedRefs).Count -gt 0) "E4SeedbedRefsEmpty:$id"
        foreach ($seedbedRef in @($integration.e4SeedbedRefs)) {
            Require ($approvedSeedbedIds.ContainsKey([string] $seedbedRef)) "E4SeedbedRefUnknown:${id}:$seedbedRef"
        }
    }
    if ([string] $item.kind -eq "AutomaticTransition") {
        Require ($null -ne $item.automaticTransition) "AutomaticTransitionContractMissing:$id"
        $worldTickTrigger = $item.automaticTransition.PSObject.Properties['triggerKindCode']
        if ($null -eq $worldTickTrigger -or [string] $worldTickTrigger.Value -ne 'WorldTick') {
            Require-Text $item.automaticTransition.triggerWiId "AutomaticTriggerMissing:$id"
        } else {
            Require ([string] $item.controlPolicyCode -eq 'WorldAutomatic') "WorldTickAuthorityInvalid:$id"
            Require ([string]::IsNullOrEmpty([string] $item.automaticTransition.triggerWiId)) "WorldTickCannotInventTriggerWi:$id"
        }
        Require-Text $item.automaticTransition.triggerState "AutomaticTriggerStateMissing:$id"
        Require-Text $item.automaticTransition.targetState "AutomaticTargetStateMissing:$id"
        Require-Text $item.automaticTransition.causeLineage "AutomaticCauseLineageMissing:$id"
    }
    if ([string] $item.kind -eq "SharedPolicy") {
        Require ($null -ne $item.sharedPolicy) "SharedPolicyContractMissing:$id"
        Require (@($item.sharedPolicy.consumers).Count -gt 0) "SharedPolicyConsumersMissing:$id"
        Require (@($item.sharedPolicy.resultCodes).Count -gt 0) "SharedPolicyResultsMissing:$id"
    }
    foreach ($reference in @($item.sourceReferences)) {
        Require (Test-Path -LiteralPath (Join-Path $repositoryRoot ([string] $reference))) "SourceReferenceNotFound:${id}:$reference"
    }
    $itemsById[$id] = $item
}

Require (@($catalog.items | Where-Object kind -eq "Command").Count -eq 121) "CommandCountMustBe121"
Require (@($catalog.items | Where-Object kind -eq "AutomaticTransition").Count -eq 12) "AutomaticTransitionCountMustBe12"
Require (@($catalog.items | Where-Object kind -eq "SharedPolicy").Count -eq 1) "SharedPolicyCountMustBe1"

$groupCodes = @($catalog.items.groupCode | Select-Object -Unique)
Require (@($groupDisplayNames.PSObject.Properties).Count -eq $groupCodes.Count) "GroupDisplayNameCountMismatch"
foreach ($group in @($catalog.items | Group-Object groupCode)) {
    $expectedSequence = 1..$group.Count
    $actualSequence = @($group.Group | Sort-Object sequence |
        ForEach-Object { [int] $_.sequence })
    Require (($actualSequence -join ",") -eq ($expectedSequence -join ",")) "GroupSequenceNotContinuous:$($group.Name)"
    Require (@($group.Group.title | Select-Object -Unique).Count -eq $group.Count) "GroupTitleDuplicate:$($group.Name)"
}

$primaryOutcomeByWi = @{}
$responsibilityKindByWi = @{}
$responsibilityAssessmentByWi = @{}
$atomicBundlesByWi = @{}
$alternativeOutcomesByWi = @{}
$legacyCompositeByWi = @{}
$proceduralStepsByWi = @{}
$actorResponsibilityMigrationsByWi = @{}

foreach ($entry in @($responsibilityPolicy.atomicBundles)) {
    $id = [string] $entry.worldInteractionId
    Require-Text $id "AtomicBundleIdMissing"
    Require (-not $atomicBundlesByWi.ContainsKey($id)) "AtomicBundleDuplicate:$id"
    Require-Text $entry.reason "AtomicBundleReasonMissing:$id"
    $atomicBundlesByWi[$id] = $entry
}
foreach ($entry in @($responsibilityPolicy.alternativeOutcomeSets)) {
    $id = [string] $entry.worldInteractionId
    Require-Text $id "AlternativeOutcomeIdMissing"
    Require (-not $alternativeOutcomesByWi.ContainsKey($id)) "AlternativeOutcomeDuplicate:$id"
    Require (@($entry.outcomeCodes).Count -gt 1) "AlternativeOutcomeCodesMissing:$id"
    Require-Text $entry.reason "AlternativeOutcomeReasonMissing:$id"
    $alternativeOutcomesByWi[$id] = $entry
}
foreach ($entry in @($responsibilityPolicy.legacyCompositeMigrations)) {
    $id = [string] $entry.worldInteractionId
    Require-Text $id "LegacyCompositeIdMissing"
    Require (-not $legacyCompositeByWi.ContainsKey($id)) "LegacyCompositeDuplicate:$id"
    Require (@($entry.targetResponsibilities).Count -gt 1) "LegacyCompositeTargetsMissing:$id"
    Require-Text $entry.compatibilityBoundary "LegacyCompositeCompatibilityMissing:$id"
    $legacyCompositeByWi[$id] = $entry
}
foreach ($entry in @($responsibilityPolicy.proceduralStepMigrations)) {
    $id = [string] $entry.worldInteractionId
    Require-Text $id "ProceduralStepIdMissing"
    Require (-not $proceduralStepsByWi.ContainsKey($id)) "ProceduralStepDuplicate:$id"
    Require-Text $entry.targetOwnerCode "ProceduralStepTargetOwnerMissing:$id"
    Require-Text $entry.reason "ProceduralStepReasonMissing:$id"
    Require-Text $entry.compatibilityBoundary "ProceduralStepCompatibilityMissing:$id"
    $proceduralStepsByWi[$id] = $entry
}
foreach ($entry in @($responsibilityPolicy.actorResponsibilityMigrations)) {
    $id = [string] $entry.worldInteractionId
    Require-Text $id "ActorResponsibilityMigrationIdMissing"
    Require (-not $actorResponsibilityMigrationsByWi.ContainsKey($id)) `
        "ActorResponsibilityMigrationDuplicate:$id"
    Require ([string] $entry.targetControlPolicyCode -eq "NpcRoutine") `
        "ActorResponsibilityTargetPolicyInvalid:$id"
    Require-Text $entry.reason "ActorResponsibilityMigrationReasonMissing:$id"
    $actorResponsibilityMigrationsByWi[$id] = $entry
}

Require (@($responsibilityPolicy.primaryOutcomeCodes.PSObject.Properties).Count -eq
    @($catalog.items).Count) "PrimaryOutcomeCountMismatch"
foreach ($property in @($responsibilityPolicy.primaryOutcomeCodes.PSObject.Properties)) {
    Require ($itemsById.ContainsKey([string] $property.Name)) "PrimaryOutcomeWorldInteractionUnknown:$($property.Name)"
}

foreach ($item in @($catalog.items)) {
    $id = [string] $item.id
    $primaryOutcomeProperty = $responsibilityPolicy.primaryOutcomeCodes.PSObject.Properties[$id]
    Require ($null -ne $primaryOutcomeProperty) "PrimaryOutcomeMissing:$id"
    $primaryOutcome = [string] $primaryOutcomeProperty.Value
    Require-Text $primaryOutcome "PrimaryOutcomeEmpty:$id"
    Require (@($item.effectCodes) -contains $primaryOutcome) "PrimaryOutcomeNotEffect:${id}:$primaryOutcome"
    $primaryOutcomeByWi[$id] = $primaryOutcome

    $kindProperty = $responsibilityPolicy.responsibilityKindByInteractionKind.PSObject.Properties[
        [string] $item.kind]
    Require ($null -ne $kindProperty) "ResponsibilityKindMissing:$id"
    $responsibilityKindByWi[$id] = [string] $kindProperty.Value

    $categoryCount = 0
    if ($atomicBundlesByWi.ContainsKey($id)) { $categoryCount++ }
    if ($alternativeOutcomesByWi.ContainsKey($id)) { $categoryCount++ }
    if ($legacyCompositeByWi.ContainsKey($id)) { $categoryCount++ }
    if ($proceduralStepsByWi.ContainsKey($id)) { $categoryCount++ }
    if ($actorResponsibilityMigrationsByWi.ContainsKey($id)) { $categoryCount++ }
    Require ($categoryCount -le 1) "ResponsibilityAssessmentOverlap:$id"

    if (@($item.effectCodes).Count -gt 1) {
        Require ($categoryCount -eq 1) "MultipleEffectsUnaudited:$id"
    }
    else {
        Require ($categoryCount -eq 0 -or $proceduralStepsByWi.ContainsKey($id) -or
            $actorResponsibilityMigrationsByWi.ContainsKey($id)) `
            "SingleEffectAssessmentInvalid:$id"
    }

    if ($alternativeOutcomesByWi.ContainsKey($id)) {
        $expectedOutcomes = @($item.effectCodes | Sort-Object)
        $declaredOutcomes = @($alternativeOutcomesByWi[$id].outcomeCodes | Sort-Object)
        Require (($expectedOutcomes -join ",") -eq ($declaredOutcomes -join ",")) `
            "AlternativeOutcomeSetMismatch:$id"
    }

    $responsibilityAssessmentByWi[$id] = if ($legacyCompositeByWi.ContainsKey($id)) {
        "LegacyCompositeMigrationRequired"
    }
    elseif ($proceduralStepsByWi.ContainsKey($id)) {
        "ProceduralStepMigrationRequired"
    }
    elseif ($actorResponsibilityMigrationsByWi.ContainsKey($id)) {
        "ActorResponsibilityMigrationRequired"
    }
    elseif ($alternativeOutcomesByWi.ContainsKey($id)) {
        "AlternativeOutcomes"
    }
    elseif ($atomicBundlesByWi.ContainsKey($id)) {
        "AtomicBundle"
    }
    else {
        "Atomic"
    }

    if ([string] $item.kind -eq "AutomaticTransition") {
        $worldTickTrigger = $item.automaticTransition.PSObject.Properties['triggerKindCode']
        if ($null -eq $worldTickTrigger -or [string] $worldTickTrigger.Value -ne 'WorldTick') {
            Require ($itemsById.ContainsKey([string] $item.automaticTransition.triggerWiId)) "AutomaticTriggerNotFound:$id"
        }
    }
    if ($proceduralStepsByWi.ContainsKey($id) -or
        $actorResponsibilityMigrationsByWi.ContainsKey($id)) {
        Require ([string] $item.kind -eq "AutomaticTransition") `
            "AutomaticMigrationMustTargetTransition:$id"
    }
}

$polarityByWi = @{}
$polarityClassificationByWi = @{}
function Add-PolarityAssignments(
    [object[]] $WorldInteractionIds,
    [string] $PolarityCode,
    [string] $ClassificationCode) {
    foreach ($rawId in $WorldInteractionIds) {
        $id = [string] $rawId
        Require ($itemsById.ContainsKey($id)) "PolarityWorldInteractionUnknown:$id"
        Require (-not $polarityByWi.ContainsKey($id)) "PolarityWorldInteractionDuplicate:$id"
        $polarityByWi[$id] = $PolarityCode
        $polarityClassificationByWi[$id] = $ClassificationCode
    }
}
Add-PolarityAssignments @($polarityCatalog.fixedYangWorldInteractionIds) `
    "Yang" "Fixed"
Add-PolarityAssignments @($polarityCatalog.fixedYinWorldInteractionIds) `
    "Yin" "Fixed"
Add-PolarityAssignments @($polarityCatalog.contextualWorldInteractionIds) `
    "Contextual" "ExecutionContext"
Add-PolarityAssignments @($polarityCatalog.notApplicableWorldInteractionIds) `
    "NotApplicable" "NotApplicable"
Require ($polarityByWi.Count -eq $itemsById.Count) "PolarityCoverageMismatch"
Require (@($polarityCatalog.fixedYangWorldInteractionIds).Count -eq 28) `
    "FixedYangCountMustBe28"
Require (@($polarityCatalog.fixedYinWorldInteractionIds).Count -eq 31) `
    "FixedYinCountMustBe31"
Require (@($polarityCatalog.contextualWorldInteractionIds).Count -eq 63) `
    "ContextualPolarityCountMustBe63"
Require (@($polarityCatalog.notApplicableWorldInteractionIds).Count -eq 12) `
    "NotApplicablePolarityCountMustBe12"

$actorMigrationGatedIds = @($polarityCatalog.actorMigrationGatedWorldInteractionIds |
    Sort-Object)
$responsibilityActorMigrationIds = @($actorResponsibilityMigrationsByWi.Keys |
    Sort-Object)
Require (($actorMigrationGatedIds -join ",") -eq
    ($responsibilityActorMigrationIds -join ",")) `
    "ActorMigrationPolarityGateMismatch"
foreach ($id in $actorMigrationGatedIds) {
    Require ([string] $polarityByWi[$id] -eq "Yang") `
        "ActorMigrationPolarityMustBeYang:$id"
}

$playableLoopIds = @{}
foreach ($loop in @($playableLoopCatalog.items)) {
    $playableLoopIds[[string] $loop.loopStableId] = $true
}
$contextBindingKeys = @{}
foreach ($binding in @($polarityCatalog.contextBindings)) {
    $loopId = [string] $binding.playableLoopStableId
    $wiId = [string] $binding.worldInteractionId
    $polarityCode = [string] $binding.polarityCode
    Require ($playableLoopIds.ContainsKey($loopId)) `
        "PolarityContextLoopUnknown:$loopId"
    Require ($itemsById.ContainsKey($wiId)) `
        "PolarityContextWorldInteractionUnknown:$wiId"
    Require ([string] $polarityByWi[$wiId] -eq "Contextual") `
        "PolarityContextWorldInteractionNotContextual:$wiId"
    Require ($polarityCode -in @("Yang", "Yin")) `
        "PolarityContextValueInvalid:${loopId}:$wiId"
    $bindingKey = "$loopId|$wiId"
    Require (-not $contextBindingKeys.ContainsKey($bindingKey)) `
        "PolarityContextBindingDuplicate:$bindingKey"
    $contextBindingKeys[$bindingKey] = $polarityCode
}

$flowEdges = @()
$flowIds = @{}
$edgeIds = @{}
foreach ($flow in @($workflowCatalog.flows)) {
    $flowId = [string] $flow.flowStableId
    Require-Text $flowId "FlowIdMissing"
    Require (-not $flowIds.ContainsKey($flowId)) "FlowIdDuplicate:$flowId"
    $flowIds[$flowId] = $true
    Require-Text $flow.title "FlowTitleMissing:$flowId"
    Require (@($workflowCatalog.flowKindCodes) -contains [string] $flow.flowKindCode) `
        "FlowKindInvalid:$flowId"
    Require (@($flow.edges).Count -gt 0) "FlowEdgesMissing:$flowId"
    foreach ($edge in @($flow.edges)) {
        $fromId = [string] $edge.fromWorldInteractionId
        $toId = [string] $edge.toWorldInteractionId
        Require ($itemsById.ContainsKey($fromId)) "FlowFromUnknown:${flowId}:$fromId"
        Require ($itemsById.ContainsKey($toId)) "FlowToUnknown:${flowId}:$toId"
        $edgeId = "$fromId>$toId"
        Require (-not $edgeIds.ContainsKey($edgeId)) "FlowEdgeDuplicate:$edgeId"
        $edgeIds[$edgeId] = $true
        $flowEdges += [pscustomobject]@{
            flowStableId = $flowId
            flowTitle = [string] $flow.title
            flowKindCode = [string] $flow.flowKindCode
            fromWorldInteractionId = $fromId
            toWorldInteractionId = $toId
        }
    }
}
Require ($flowEdges.Count -eq 62) "FlowEdgeCountMustBe62"

$stageLabels = @{}
foreach ($stage in $evidenceStages) { $stageLabels[[string] $stage.code] = [string] $stage.label }
$kindLabels = @{ Command = "명시적 명령"; AutomaticTransition = "자동 상태 전이"; SharedPolicy = "공유 정책" }
$statusLabels = @{ NotStarted = "미착수"; InProgress = "진행 중"; Blocked = "차단"; Done = "완료"; NotSelected = "미선정"; Selected = "선정" }
$responsibilityKindLabels = @{ ActorIntent = "행위자 의도"; AuthorityTransition = "권위 상태 전이"; SharedDecisionRule = "공유 판정 규칙" }
$responsibilityAssessmentLabels = @{ Atomic = "단일 책임"; AtomicBundle = "원자적 부수 효과"; AlternativeOutcomes = "배타적 결과 묶음"; LegacyCompositeMigrationRequired = "복합 책임·분리 필요"; ProceduralStepMigrationRequired = "절차 단계·Task/Effect로 이동 필요"; ActorResponsibilityMigrationRequired = "실제 Actor 행동으로 전환 필요" }
$flowKindLabels = @{ IndependentAreaLoop = "독립 영역 흐름"; OptionalAreaConnector = "선택형 영역 연결"; BranchingAreaLoop = "분기·반복 영역 흐름" }
$polarityLabels = @{ Yang = "양(陽)"; Yin = "음(陰)"; Contextual = "실행 문맥 판정"; NotApplicable = "사분면 제외" }

$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine("# 세계 상호작용 단위 대장")
[void] $builder.AppendLine()
[void] $builder.AppendLine("> 이 문서는 ``$InputPath``와 참조된 단일 책임·조립 흐름·음양 사분면 대장에서 자동 생성된다. 직접 수정하지 않는다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- 대장 개정: ``$($catalog.revision)``")
[void] $builder.AppendLine("- 증거 단계 개정: ``$($stageCatalog.revision)``")
[void] $builder.AppendLine("- WI 발생원 개정: ``$($triggerCatalog.revision)``")
[void] $builder.AppendLine("- WI 단일 책임 개정: ``$($responsibilityPolicy.revision)``")
[void] $builder.AppendLine("- WI 조립 흐름 개정: ``$($workflowCatalog.revision)``")
[void] $builder.AppendLine("- WI 음양·수행주체 사분면 개정: ``$($polarityCatalog.revision)``")
[void] $builder.AppendLine("- 마지막 확인일: ``$($catalog.lastVerifiedDate)``")
[void] $builder.AppendLine("- 기본 구현 완료선: ``E3 자동 시험 통과``")
[void] $builder.AppendLine("- 실제 공간·공공데이터·Unity 통합 목표선: ``E7 실제 플레이 폐루프``")
[void] $builder.AppendLine("- 전체 항목: ``$($catalog.items.Count)``")
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 읽는 법")
[void] $builder.AppendLine()
[void] $builder.AppendLine("WI는 한 행위자의 한 의도와 하나의 주요 권위 결과를 관통하는 구현·검증 단위다. 플레이어와 NPC가 같은 의도·결과를 만들면 같은 WI를 사용할 수 있으며 행위자 종류는 실행 문맥에 결속한다. Preview·Confirm·Task·Effect는 별도 절차 WI가 아니라 같은 책임의 실행 생명주기다. 여러 WI의 순서·분기·반복은 별도 조립 흐름 대장이 소유하고 WI의 정체성이나 필수 선행 조건이 되지 않는다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("음양은 행동 목적, 두 번째 부호는 실제 Actor를 뜻한다. ``++``·``+-``·``-+``·``--``는 선악이나 배율이 아니며 실행 인스턴스의 설명 좌표다. 순수 자동 전이는 사분면 밖에 두고 Contextual WI는 승인 PlayableLoop 문맥이 있을 때만 Yang 또는 Yin으로 고정한다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("``sequence``는 대장 안에서 찾기 위한 순번일 뿐 게임 실행 단계가 아니다. 효과가 여러 개여도 하나의 주요 결과를 원자적으로 지키는 부수 효과라면 한 WI로 유지할 수 있다. 서로 독립적으로 실패·취소·재시도할 수 있는 복합 책임은 ``복합 책임·분리 필요``로 차단한다. 독립 의도가 없는 자동 절차는 Task/Effect로 내리고, 실제 피킹·포장처럼 Actor가 수행하는 일은 NPC 행동 책임으로 전환한다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 분류 요약")
[void] $builder.AppendLine()
[void] $builder.AppendLine("| 분류 | 수 |")
[void] $builder.AppendLine("| --- | ---: |")
foreach ($kind in $allowedKinds) {
    [void] $builder.AppendLine("| $($kindLabels[$kind]) | $(@($catalog.items | Where-Object kind -eq $kind).Count) |")
}

foreach ($group in @($catalog.items | Sort-Object groupCode, sequence | Group-Object groupCode)) {
    $groupDisplayName = [string] $groupDisplayNames.PSObject.Properties[$group.Name].Value
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("## $groupDisplayName 작업군 (``$($group.Name)``)")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("| 한국어 기능명 · 고유 식별자 | 대장 순번 | 책임 종류 | 단일 책임 판정 | 음양 정의 | 주요 결과 | 조작 정책 | 허용 발생원 | 시작 → 완료 | 구현 | 통합 |")
    [void] $builder.AppendLine("| --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    foreach ($item in @($group.Group | Sort-Object sequence, id)) {
        $states = "$(Escape-Markdown (@($item.startStateCodes) -join ', ')) → $(Escape-Markdown (@($item.completionStateCodes) -join ', '))"
        $implementation = "$($statusLabels[[string] $item.implementation.status]) · ``$($item.implementation.currentStage)→$($item.implementation.targetStage)``"
        $integration = "$($statusLabels[[string] $item.integration.status]) · ``$($item.integration.currentStage)→$($item.integration.targetStage)``"
        $triggerSources = (@($resolvedTriggerSourcesByWi[[string] $item.id]) -join ", ")
        $responsibilityKind = $responsibilityKindLabels[$responsibilityKindByWi[[string] $item.id]]
        $responsibilityAssessment = $responsibilityAssessmentLabels[
            $responsibilityAssessmentByWi[[string] $item.id]]
        $polarity = $polarityLabels[$polarityByWi[[string] $item.id]]
        $primaryOutcome = $primaryOutcomeByWi[[string] $item.id]
        [void] $builder.AppendLine("| $($item.title) · ``$($item.id)`` | $($item.sequence) | $responsibilityKind | $responsibilityAssessment | $polarity | ``$primaryOutcome`` | $($item.controlPolicyCode) | $triggerSources | $states | $implementation | $integration |")
    }
}

[void] $builder.AppendLine()
[void] $builder.AppendLine("## WI 조립 흐름")
[void] $builder.AppendLine()
[void] $builder.AppendLine("아래 연결은 WI 정의가 소유하는 필수 절차가 아니다. 독립 행동을 특정 플레이 폐루프에서 조립하는 선택 가능한 흐름이며, 같은 WI는 다른 흐름에도 참여할 수 있다.")
foreach ($flow in @($workflowCatalog.flows)) {
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("### $($flow.title) (``$($flow.flowStableId)``)")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("- 흐름 종류: $($flowKindLabels[[string] $flow.flowKindCode])")
    foreach ($edge in @($flow.edges)) {
        $from = $itemsById[[string] $edge.fromWorldInteractionId]
        $to = $itemsById[[string] $edge.toWorldInteractionId]
        [void] $builder.AppendLine("- $($from.title) (``$($from.id)``) → $($to.title) (``$($to.id)``)")
    }
}
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 증거 경계")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- E3는 계약·코드·자동 시험의 구현 완료선이다.")
[void] $builder.AppendLine("- Scenario 공간으로 통과한 E3는 실제 LandscapeGraph 또는 공공 공간자료 증거가 아니다.")
[void] $builder.AppendLine("- E4는 WI의 허용 발생원·주체·대상·자료·자원·시간과 공간 적용 여부가 결속되는 단계다.")
[void] $builder.AppendLine("- E5는 실제 Simulation 세계에서 WI가 발생해 권위 상태·Task·Effect·결과·후속 경로로 발현되는 단계다.")
[void] $builder.AppendLine("- H는 공간 포함 계층이며 공간 WI의 E4·E5 입력 증거다. AreaSet·Graph가 존재해도 WI 발현이 없으면 E5가 아니다.")
[void] $builder.AppendLine("- 실제 서버와 저장 Scene에서 사람이 조작한 Play Mode·Game View·Console 증거가 있어야 E7이다.")
[void] $builder.AppendLine("- Unity 애니메이션이나 GameObject 상태가 Task 완료를 확정하지 않는다.")

$expected = ConvertTo-DeterministicText $builder.ToString()
$resolvedOutput = Join-Path $repositoryRoot $OutputPath

$contractBuilder = [Text.StringBuilder]::new()
[void] $contractBuilder.AppendLine('// <auto-generated />')
[void] $contractBuilder.AppendLine('#nullable enable')
[void] $contractBuilder.AppendLine('using System;')
[void] $contractBuilder.AppendLine('using System.Collections.Generic;')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('namespace Ssalddel.Simulation.Contracts')
[void] $contractBuilder.AppendLine('{')
[void] $contractBuilder.AppendLine('    /// <summary>')
[void] $contractBuilder.AppendLine('    /// 안정 WI 고유 식별자에 한국어 기능명·단일 책임·음양 정의를 연결한다.')
[void] $contractBuilder.AppendLine('    /// 원본은 세계 상호작용·단일 책임·조립 흐름·사분면 대장이며 이 파일은 직접 수정하지 않는다.')
[void] $contractBuilder.AppendLine('    /// </summary>')
[void] $contractBuilder.AppendLine('    public sealed class Simulation세계상호작용이름Definition')
[void] $contractBuilder.AppendLine('    {')
[void] $contractBuilder.AppendLine('        public Simulation세계상호작용이름Definition(')
[void] $contractBuilder.AppendLine('            string worldInteractionId, string groupCode,')
[void] $contractBuilder.AppendLine('            string 한국어작업군명, int 대장순번, string 한국어기능명,')
[void] $contractBuilder.AppendLine('            string 책임종류코드, string 주요결과코드, string 단일책임판정코드,')
[void] $contractBuilder.AppendLine('            string 음양분류Code, string 음양판정방식Code, bool actor전환필요)')
[void] $contractBuilder.AppendLine('        {')
[void] $contractBuilder.AppendLine('            WorldInteractionId = worldInteractionId;')
[void] $contractBuilder.AppendLine('            GroupCode = groupCode;')
[void] $contractBuilder.AppendLine('            this.한국어작업군명 = 한국어작업군명;')
[void] $contractBuilder.AppendLine('            this.대장순번 = 대장순번;')
[void] $contractBuilder.AppendLine('            this.한국어기능명 = 한국어기능명;')
[void] $contractBuilder.AppendLine('            this.책임종류코드 = 책임종류코드;')
[void] $contractBuilder.AppendLine('            this.주요결과코드 = 주요결과코드;')
[void] $contractBuilder.AppendLine('            this.단일책임판정코드 = 단일책임판정코드;')
[void] $contractBuilder.AppendLine('            this.음양분류Code = 음양분류Code;')
[void] $contractBuilder.AppendLine('            this.음양판정방식Code = 음양판정방식Code;')
[void] $contractBuilder.AppendLine('            this.Actor전환필요 = actor전환필요;')
[void] $contractBuilder.AppendLine('        }')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public string WorldInteractionId { get; }')
[void] $contractBuilder.AppendLine('        public string GroupCode { get; }')
[void] $contractBuilder.AppendLine('        public string 한국어작업군명 { get; }')
[void] $contractBuilder.AppendLine('        public int 대장순번 { get; }')
[void] $contractBuilder.AppendLine('        public string 한국어기능명 { get; }')
[void] $contractBuilder.AppendLine('        public string 책임종류코드 { get; }')
[void] $contractBuilder.AppendLine('        public string 주요결과코드 { get; }')
[void] $contractBuilder.AppendLine('        public string 단일책임판정코드 { get; }')
[void] $contractBuilder.AppendLine('        public string 음양분류Code { get; }')
[void] $contractBuilder.AppendLine('        public string 음양판정방식Code { get; }')
[void] $contractBuilder.AppendLine('        public bool Actor전환필요 { get; }')
[void] $contractBuilder.AppendLine('        public string 한국어표시명 => 한국어작업군명')
[void] $contractBuilder.AppendLine('            + " · " + 한국어기능명 + " (" + WorldInteractionId + ")";')
[void] $contractBuilder.AppendLine('    }')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('    public static class Simulation세계상호작용이름Catalog')
[void] $contractBuilder.AppendLine('    {')
[void] $contractBuilder.AppendLine('        private static readonly Simulation세계상호작용이름Definition[] 항목들 =')
[void] $contractBuilder.AppendLine('        {')
foreach ($item in @($catalog.items | Sort-Object groupCode, sequence, id)) {
    $id = Escape-CSharpString ([string] $item.id)
    $groupCode = Escape-CSharpString ([string] $item.groupCode)
    $groupDisplayName = Escape-CSharpString ([string] $groupDisplayNames.PSObject.Properties[[string] $item.groupCode].Value)
    $title = Escape-CSharpString ([string] $item.title)
    $responsibilityKind = Escape-CSharpString $responsibilityKindByWi[[string] $item.id]
    $primaryOutcome = Escape-CSharpString $primaryOutcomeByWi[[string] $item.id]
    $assessment = Escape-CSharpString $responsibilityAssessmentByWi[[string] $item.id]
    $polarityCode = Escape-CSharpString $polarityByWi[[string] $item.id]
    $classificationCode = Escape-CSharpString $polarityClassificationByWi[[string] $item.id]
    $actorMigrationRequired = if ($actorMigrationGatedIds -contains [string] $item.id) {
        "true"
    } else { "false" }
    [void] $contractBuilder.AppendLine(('            new Simulation세계상호작용이름Definition("{0}", "{1}", "{2}", {3}, "{4}", "{5}", "{6}", "{7}", "{8}", "{9}", {10}),' -f
        $id, $groupCode, $groupDisplayName, [int] $item.sequence, $title,
        $responsibilityKind, $primaryOutcome, $assessment, $polarityCode,
        $classificationCode, $actorMigrationRequired))
}
[void] $contractBuilder.AppendLine('        };')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        private static readonly Dictionary<string, string> 문맥음양ByKey =')
[void] $contractBuilder.AppendLine('            new Dictionary<string, string>(StringComparer.Ordinal)')
[void] $contractBuilder.AppendLine('            {')
foreach ($binding in @($polarityCatalog.contextBindings | Sort-Object playableLoopStableId, worldInteractionId)) {
    $bindingKey = Escape-CSharpString (([string] $binding.playableLoopStableId) + "|" +
        ([string] $binding.worldInteractionId))
    $bindingPolarity = Escape-CSharpString ([string] $binding.polarityCode)
    [void] $contractBuilder.AppendLine(('                ["{0}"] = "{1}",' -f
        $bindingKey, $bindingPolarity))
}
[void] $contractBuilder.AppendLine('            };')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static IReadOnlyList<Simulation세계상호작용이름Definition> All => 항목들;')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static Simulation세계상호작용이름Definition? Find(string worldInteractionId)')
[void] $contractBuilder.AppendLine('        {')
[void] $contractBuilder.AppendLine('            if (string.IsNullOrWhiteSpace(worldInteractionId)) return null;')
[void] $contractBuilder.AppendLine('            foreach (var 항목 in 항목들)')
[void] $contractBuilder.AppendLine('                if (string.Equals(항목.WorldInteractionId, worldInteractionId,')
[void] $contractBuilder.AppendLine('                        StringComparison.Ordinal))')
[void] $contractBuilder.AppendLine('                    return 항목;')
[void] $contractBuilder.AppendLine('            return null;')
[void] $contractBuilder.AppendLine('        }')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 한국어기능명(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.한국어기능명')
[void] $contractBuilder.AppendLine('               ?? (worldInteractionId ?? string.Empty);')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 한국어표시명(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.한국어표시명')
[void] $contractBuilder.AppendLine('               ?? (string.IsNullOrWhiteSpace(worldInteractionId)')
[void] $contractBuilder.AppendLine('                   ? "알 수 없는 세계 상호작용"')
[void] $contractBuilder.AppendLine('                   : "알 수 없는 세계 상호작용 (" + worldInteractionId + ")");')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 책임종류코드(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.책임종류코드 ?? string.Empty;')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 주요결과코드(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.주요결과코드 ?? string.Empty;')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 단일책임판정코드(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.단일책임판정코드 ?? string.Empty;')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 음양분류Code(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.음양분류Code ?? "Unclassified";')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 음양판정방식Code(string worldInteractionId)')
[void] $contractBuilder.AppendLine('            => Find(worldInteractionId)?.음양판정방식Code ?? string.Empty;')
[void] $contractBuilder.AppendLine()
[void] $contractBuilder.AppendLine('        public static string 문맥음양Code(string worldInteractionId,')
[void] $contractBuilder.AppendLine('            string playableLoopStableId)')
[void] $contractBuilder.AppendLine('        {')
[void] $contractBuilder.AppendLine('            if (string.IsNullOrWhiteSpace(worldInteractionId)')
[void] $contractBuilder.AppendLine('                || string.IsNullOrWhiteSpace(playableLoopStableId))')
[void] $contractBuilder.AppendLine('                return string.Empty;')
[void] $contractBuilder.AppendLine('            return 문맥음양ByKey.TryGetValue(playableLoopStableId.Trim()')
[void] $contractBuilder.AppendLine('                       + "|" + worldInteractionId.Trim(), out var value)')
[void] $contractBuilder.AppendLine('                ? value : string.Empty;')
[void] $contractBuilder.AppendLine('        }')
[void] $contractBuilder.AppendLine('    }')
[void] $contractBuilder.AppendLine('}')
$expectedContract = ConvertTo-DeterministicText $contractBuilder.ToString()
$resolvedContractOutput = Join-Path $repositoryRoot $ContractOutputPath

if ($Mode -eq "Write") {
    Write-DeterministicTextIfChanged $resolvedOutput $expected | Out-Null
    Write-DeterministicTextIfChanged $resolvedContractOutput $expectedContract | Out-Null
    Write-Output "WorldInteractionCatalogGenerated:$OutputPath;$ContractOutputPath"
}
else {
    Require (Test-Path -LiteralPath $resolvedOutput) "GeneratedDocumentMissing:$OutputPath"
    $actual = ConvertTo-DeterministicText ([IO.File]::ReadAllText($resolvedOutput))
    Require ($actual -eq $expected) "GeneratedDocumentOutOfDate:$OutputPath"
    Require (Test-Path -LiteralPath $resolvedContractOutput) "GeneratedContractMissing:$ContractOutputPath"
    $actualContract = ConvertTo-DeterministicText ([IO.File]::ReadAllText($resolvedContractOutput))
    Require ($actualContract -eq $expectedContract) "GeneratedContractOutOfDate:$ContractOutputPath"
    Write-Output "WorldInteractionCatalogValid:$($catalog.items.Count)"
}
