[CmdletBinding()]
param(
    [ValidateSet("Validate", "Write")]
    [string] $Mode = "Validate",

    [string] $PolicyPath = "eng/execution-ledgers/subject-interaction-development.json",

    [string] $OutputJsonPath = "",

    [string] $OutputMarkdownPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "SubjectInteractionDevelopmentInvalid:$Code" }
}

function Require-Text([object] $Value, [string] $Code) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $Value)) $Code
}

function Resolve-RepoPath([string] $RelativePath) {
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $RelativePath))
}

function Write-IfChanged([string] $Path, [string] $Content) {
    $fullPath = Resolve-RepoPath $Path
    $parent = Split-Path -Parent $fullPath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $normalized = $Content.Replace("`r`n", "`n")
    if ((Test-Path -LiteralPath $fullPath) -and
        (Get-Content -LiteralPath $fullPath -Raw -Encoding UTF8).Replace("`r`n", "`n") -ceq $normalized) {
        return
    }
    [IO.File]::WriteAllText($fullPath, $normalized, [Text.UTF8Encoding]::new($false))
}

function Goal-Id([string] $LegacyLoopId, [string] $WorldInteractionId) {
    $context = $LegacyLoopId -replace '^playable-loop:', '' -replace '\.v[0-9]+$', ''
    $interaction = $WorldInteractionId.ToLowerInvariant().Replace('_', '-')
    return "interaction-goal:$context.$interaction.v1"
}

function Escape-Cell([object] $Value) {
    return ([string] $Value).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$policy = Get-Content -LiteralPath (Resolve-RepoPath $PolicyPath) -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $policy.schemaVersion -eq "mirror-subject-interaction-development.v1") "PolicySchemaInvalid"
foreach ($principleName in @(
    "subjectFoundationPrecedesInteractionDefinition",
    "interactionGoalOwnsExactlyOneWorldInteraction",
    "directResultPrecedesDerivedEffects",
    "playableLoopIsOptionalValidationBundle",
    "storyBeatProvidesContextNotExecutionAuthority",
    "graphMapRequiredOnlyForRelationshipOrSpatialImpact",
    "legacyLoopGoalsRemainReadableDuringMigration")) {
    $property = $policy.principles.PSObject.Properties[$principleName]
    Require ($null -ne $property -and [bool] $property.Value) "PolicyPrincipleMissing:$principleName"
}

$subjects = Get-Content -LiteralPath (Resolve-RepoPath ([string] $policy.subjectCatalogPath)) -Raw -Encoding UTF8 | ConvertFrom-Json
$worldInteractions = Get-Content -LiteralPath (Resolve-RepoPath ([string] $policy.worldInteractionCatalogPath)) -Raw -Encoding UTF8 | ConvertFrom-Json
$classifications = Get-Content -LiteralPath (Resolve-RepoPath ([string] $policy.roleObjectClassificationPath)) -Raw -Encoding UTF8 | ConvertFrom-Json
$legacyGoals = Get-Content -LiteralPath (Resolve-RepoPath ([string] $policy.legacyGoalLedgerPath)) -Raw -Encoding UTF8 | ConvertFrom-Json
$loops = Get-Content -LiteralPath (Resolve-RepoPath ([string] $policy.optionalPlayableLoopCatalogPath)) -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $subjects.schemaVersion -eq "mirror-gameplay-subject-catalog.v1") "SubjectCatalogSchemaInvalid"
Require ([string] $worldInteractions.schemaVersion -eq "5") "WorldInteractionCatalogSchemaInvalid"
Require (@("mirror-world-interaction-gwae-classification-output.v2", "mirror-world-interaction-gwae-classification-output.v3") -contains [string] $classifications.schemaVersion) "ClassificationSchemaInvalid"
Require ([string] $classifications.worldInteractionCatalogRevision -eq [string] $worldInteractions.revision) "ClassificationWorldInteractionRevisionStale"
Require ([string] $loops.schemaVersion -eq "ssalddel-playable-loop-catalog.v6") "OptionalLoopCatalogSchemaInvalid"

$allowedSubjectStatuses = @($subjects.allowedStatusCodes | ForEach-Object { [string] $_ })
$allowedSubjectKinds = @($subjects.allowedSubjectKindCodes | ForEach-Object { [string] $_ })
$subjectById = @{}
foreach ($subject in @($subjects.items)) {
    $subjectId = [string] $subject.subjectStableId
    Require ($subjectId -match '^subject:[a-z0-9-]+\.v[0-9]+$') "SubjectStableIdInvalid:$subjectId"
    Require (-not $subjectById.ContainsKey($subjectId)) "SubjectStableIdDuplicate:$subjectId"
    Require ($allowedSubjectKinds -contains [string] $subject.subjectKindCode) "SubjectKindInvalid:$subjectId"
    Require ($allowedSubjectStatuses -contains [string] $subject.statusCode) "SubjectStatusInvalid:$subjectId"
    if ([string] $subject.statusCode -eq "Ready") {
        foreach ($fieldName in @($subjects.requiredReadyFields)) {
            $property = $subject.PSObject.Properties[[string] $fieldName]
            Require ($null -ne $property) "ReadySubjectFieldMissing:${subjectId}:$fieldName"
            if ($property.Value -is [System.Array]) {
                Require (@($property.Value).Count -gt 0) "ReadySubjectFieldEmpty:${subjectId}:$fieldName"
            }
            else {
                Require-Text $property.Value "ReadySubjectFieldEmpty:${subjectId}:$fieldName"
            }
        }
        foreach ($referenceField in @("persistenceBoundaryRefs", "runtimeResolutionRefs", "saveReplayIdentityRefs", "sourceRefs")) {
            foreach ($reference in @($subject.$referenceField)) {
                Require (Test-Path -LiteralPath (Resolve-RepoPath ([string] $reference))) "ReadySubjectReferenceMissing:${subjectId}:${referenceField}:${reference}"
            }
        }
    }
    $subjectById[$subjectId] = $subject
}

$bindingByControlPolicy = @{}
foreach ($binding in @($policy.controlPolicySubjectBindings)) {
    $controlPolicy = [string] $binding.controlPolicyCode
    $subjectId = [string] $binding.subjectStableId
    Require-Text $controlPolicy "ControlPolicyCodeMissing"
    Require (-not $bindingByControlPolicy.ContainsKey($controlPolicy)) "ControlPolicyDuplicate:$controlPolicy"
    Require ($subjectById.ContainsKey($subjectId)) "ControlPolicySubjectUnknown:${controlPolicy}:${subjectId}"
    Require ([string] $subjectById[$subjectId].subjectKindCode -eq [string] $binding.roleObjectKindCode) "ControlPolicySubjectKindMismatch:$controlPolicy"
    $bindingByControlPolicy[$controlPolicy] = $binding
}
$directTargetSubjectId = [string] $policy.directTargetSubjectStableId
Require ($subjectById.ContainsKey($directTargetSubjectId)) "DirectTargetSubjectUnknown:$directTargetSubjectId"
Require ([string] $subjectById[$directTargetSubjectId].statusCode -eq "Ready") "DirectTargetSubjectNotReady:$directTargetSubjectId"
Require (@($subjectById[$directTargetSubjectId].roleCodes) -contains "DirectInteractionTarget") "DirectTargetSubjectRoleMissing:$directTargetSubjectId"

$classificationByWi = @{}
foreach ($classification in @($classifications.items)) {
    $classificationByWi[[string] $classification.wiId] = $classification
}
$worldInteractionById = @{}
$compiledInteractions = [Collections.Generic.List[object]]::new()
foreach ($worldInteraction in @($worldInteractions.items)) {
    $wiId = [string] $worldInteraction.id
    Require (-not $worldInteractionById.ContainsKey($wiId)) "WorldInteractionDuplicate:$wiId"
    $worldInteractionById[$wiId] = $worldInteraction
    $controlPolicy = [string] $worldInteraction.controlPolicyCode
    Require ($bindingByControlPolicy.ContainsKey($controlPolicy)) "WorldInteractionSubjectPolicyMissing:${wiId}:${controlPolicy}"
    $subjectBinding = $bindingByControlPolicy[$controlPolicy]
    $subjectId = [string] $subjectBinding.subjectStableId
    Require ([string] $subjectById[$subjectId].statusCode -eq "Ready") "WorldInteractionSubjectNotReady:${wiId}:${subjectId}"
    Require ($classificationByWi.ContainsKey($wiId)) "WorldInteractionClassificationMissing:$wiId"
    $classificationGate = $classificationByWi[$wiId].e5RoleObjectActionGate
    Require ([string] $classificationGate.applicabilityCode -eq "Required") "StateChangingWorldInteractionClassificationNotRequired:$wiId"
    Require ([string] $classificationGate.roleObjectKindCode -eq [string] $subjectBinding.roleObjectKindCode) "WorldInteractionSubjectKindMismatch:$wiId"
    Require (@($worldInteraction.completionStateCodes).Count -gt 0) "WorldInteractionDirectCompletionMissing:$wiId"
    Require (@($worldInteraction.effectCodes).Count -gt 0) "WorldInteractionDirectEffectMissing:$wiId"
    $compiledInteractions.Add([ordered]@{
        worldInteractionId = $wiId
        title = [string] $worldInteraction.title
        controlPolicyCode = $controlPolicy
        subjectStableId = $subjectId
        subjectKindCode = [string] $subjectBinding.roleObjectKindCode
        subjectBindings = @(
            [ordered]@{ roleCode = "Actor"; subjectStableId = $subjectId },
            [ordered]@{ roleCode = "DirectTarget"; subjectStableId = $directTargetSubjectId }
        )
        directCompletionStateCodes = @($worldInteraction.completionStateCodes)
        directEffectCodes = @($worldInteraction.effectCodes)
        subjectFoundationStatusCode = "Ready"
    })
}

$loopById = @{}
foreach ($loop in @($loops.items)) { $loopById[[string] $loop.loopStableId] = $loop }
$compiledGoals = [Collections.Generic.List[object]]::new()
$goalIds = @{}

if ([bool] $policy.legacyGoalProjection.enabled) {
    $wiField = [string] $policy.legacyGoalProjection.worldInteractionField
    $loopField = [string] $policy.legacyGoalProjection.optionalValidationBundleField
    foreach ($legacyGoal in @($legacyGoals.items)) {
        $legacyLoopId = [string] $legacyGoal.$loopField
        $wiId = [string] $legacyGoal.$wiField
        Require ($worldInteractionById.ContainsKey($wiId)) "LegacyGoalWorldInteractionUnknown:${legacyLoopId}:${wiId}"
        $goalId = Goal-Id $legacyLoopId $wiId
        Require (-not $goalIds.ContainsKey($goalId)) "InteractionGoalDuplicate:$goalId"
        $goalIds[$goalId] = $true
        Require ($loopById.ContainsKey($legacyLoopId)) "LegacyOptionalLoopUnknown:$legacyLoopId"
        Require (@($loopById[$legacyLoopId].worldInteractionIds) -contains $wiId) "LegacyOptionalLoopDoesNotContainInteraction:${legacyLoopId}:${wiId}"
        $interaction = @($compiledInteractions | Where-Object { [string] $_.worldInteractionId -eq $wiId })[0]
        $compiledGoals.Add([ordered]@{
            goalStableId = $goalId
            goalStateCode = [string] $legacyGoal.goalStateCode
            worldInteractionId = $wiId
            subjectBindingRefs = @([string] $interaction.subjectStableId, $directTargetSubjectId)
            optionalPlayableLoopValidationRef = $legacyLoopId
            compatibilitySourceCode = "LegacyPlayableLoopGoalProjection"
            derivedEffectBinding = [ordered]@{
                applicabilityCode = [string] $policy.legacyGoalProjection.derivedEffectApplicabilityCode
                statusCode = "NotApplicable"
                effectCodes = @()
                hopDepthCode = "None"
                causalEvidenceRefs = @()
                idempotencyRefs = @()
                saveReplayRefs = @()
                notApplicableReason = [string] $policy.legacyGoalProjection.derivedEffectReason
            }
        })
    }
}

foreach ($goal in @($policy.nativeInteractionGoals)) {
    $goalId = [string] $goal.goalStableId
    $wiId = [string] $goal.worldInteractionId
    Require ($goalId -match '^interaction-goal:[a-z0-9.-]+\.v[0-9]+$') "NativeGoalStableIdInvalid:$goalId"
    Require (-not $goalIds.ContainsKey($goalId)) "InteractionGoalDuplicate:$goalId"
    $goalIds[$goalId] = $true
    Require ($worldInteractionById.ContainsKey($wiId)) "NativeGoalWorldInteractionUnknown:${goalId}:${wiId}"
    Require (@($policy.allowedGoalStateCodes) -contains [string] $goal.goalStateCode) "NativeGoalStateInvalid:$goalId"
    $planningGateProperty = $goal.PSObject.Properties["planningGate"]
    Require ($null -ne $planningGateProperty) "NativeGoalPlanningGateMissing:$goalId"
    $planningGate = $planningGateProperty.Value
    Require (@($policy.allowedPlanningStatusCodes) -contains [string] $planningGate.statusCode) "NativeGoalPlanningNotApproved:$goalId"
    foreach ($planningField in @("topicStableId", "designDocumentRef", "designRevision", "designHashSha256", "approvalEvidenceRef")) {
        $planningProperty = $planningGate.PSObject.Properties[$planningField]
        Require ($null -ne $planningProperty) "NativeGoalPlanningFieldMissing:${goalId}:${planningField}"
        Require-Text $planningProperty.Value "NativeGoalPlanningFieldEmpty:${goalId}:${planningField}"
    }
    $designDocumentPath = Resolve-RepoPath ([string] $planningGate.designDocumentRef)
    Require (Test-Path -LiteralPath $designDocumentPath) "NativeGoalDesignDocumentMissing:$goalId"
    $actualDesignHash = (Get-FileHash -LiteralPath $designDocumentPath -Algorithm SHA256).Hash
    Require ($actualDesignHash -eq [string] $planningGate.designHashSha256) "NativeGoalDesignHashDrift:$goalId"
    Require (Test-Path -LiteralPath (Resolve-RepoPath ([string] $planningGate.approvalEvidenceRef))) "NativeGoalApprovalEvidenceMissing:$goalId"
    $subjectRefs = @($goal.subjectBindingRefs | ForEach-Object { [string] $_ })
    Require ($subjectRefs.Count -gt 0) "NativeGoalSubjectBindingsMissing:$goalId"
    foreach ($subjectId in $subjectRefs) {
        Require ($subjectById.ContainsKey($subjectId)) "NativeGoalSubjectUnknown:${goalId}:${subjectId}"
        Require ([string] $subjectById[$subjectId].statusCode -eq "Ready") "NativeGoalSubjectNotReady:${goalId}:${subjectId}"
    }
    $requiredSubjectId = [string] $bindingByControlPolicy[[string] $worldInteractionById[$wiId].controlPolicyCode].subjectStableId
    Require ($subjectRefs -contains $requiredSubjectId) "NativeGoalExecutionSubjectMissing:${goalId}:${requiredSubjectId}"
    Require ($subjectRefs -contains $directTargetSubjectId) "NativeGoalDirectTargetSubjectMissing:${goalId}:${directTargetSubjectId}"
    $loopRef = [string] $goal.optionalPlayableLoopValidationRef
    if (-not [string]::IsNullOrWhiteSpace($loopRef)) {
        Require ($loopById.ContainsKey($loopRef)) "NativeOptionalLoopUnknown:${goalId}:${loopRef}"
        Require (@($loopById[$loopRef].worldInteractionIds) -contains $wiId) "NativeOptionalLoopDoesNotContainInteraction:${goalId}:${loopRef}"
    }
    $derived = $goal.derivedEffectBinding
    Require ($null -ne $derived) "NativeDerivedEffectBindingMissing:$goalId"
    $applicability = [string] $derived.applicabilityCode
    $status = [string] $derived.statusCode
    Require (@($policy.allowedDerivedEffectApplicabilityCodes) -contains $applicability) "NativeDerivedEffectApplicabilityInvalid:$goalId"
    Require (@($policy.allowedDerivedEffectStatusCodes) -contains $status) "NativeDerivedEffectStatusInvalid:$goalId"
    if ($applicability -eq "NotApplicable") {
        Require ($status -eq "NotApplicable") "NativeDerivedEffectStatusMustBeNotApplicable:$goalId"
        Require-Text $derived.notApplicableReason "NativeDerivedEffectReasonMissing:$goalId"
    }
    else {
        Require ($status -ne "NotApplicable") "NativeDerivedEffectRequiredStatusInvalid:$goalId"
        Require ([string] $derived.hopDepthCode -in @("OneHop", "TwoHop")) "NativeDerivedEffectHopDepthInvalid:$goalId"
        Require (@($derived.effectCodes).Count -gt 0) "NativeDerivedEffectCodesMissing:$goalId"
        Require (@($derived.idempotencyRefs).Count -gt 0) "NativeDerivedEffectIdempotencyMissing:$goalId"
        Require (@($derived.saveReplayRefs).Count -gt 0) "NativeDerivedEffectSaveReplayMissing:$goalId"
        if ([string] $derived.hopDepthCode -eq "TwoHop") {
            Require (@($derived.causalEvidenceRefs).Count -gt 0) "NativeDerivedEffectTwoHopCausalityMissing:$goalId"
        }
    }
    $compiledGoals.Add([ordered]@{
        goalStableId = $goalId
        goalStateCode = [string] $goal.goalStateCode
        worldInteractionId = $wiId
        subjectBindingRefs = $subjectRefs
            optionalPlayableLoopValidationRef = $loopRef
            planningGate = $planningGate
            compatibilitySourceCode = "NativeInteractionGoal"
        derivedEffectBinding = $derived
    })
}

$result = [ordered]@{
    schemaVersion = "mirror-subject-interaction-development-output.v1"
    sourceRevision = [string] $policy.revision
    subjectCatalogRevision = [string] $subjects.revision
    worldInteractionCatalogRevision = [string] $worldInteractions.revision
    legacyGoalLedgerRevision = [string] $legacyGoals.revision
    optionalPlayableLoopCatalogRevision = [string] $loops.revision
    developmentOrder = @("SubjectFoundation", "InteractionCore", "DirectResult", "DerivedEffects", "OptionalPlayableLoopValidation")
    counts = [ordered]@{
        readySubjects = @($subjects.items | Where-Object statusCode -eq "Ready").Count
        worldInteractions = $compiledInteractions.Count
        interactionGoals = $compiledGoals.Count
        nativeLooplessGoals = @($compiledGoals | Where-Object { $_.compatibilitySourceCode -eq "NativeInteractionGoal" -and [string]::IsNullOrWhiteSpace([string] $_.optionalPlayableLoopValidationRef) }).Count
        legacyProjectedGoals = @($compiledGoals | Where-Object compatibilitySourceCode -eq "LegacyPlayableLoopGoalProjection").Count
        optionalLoopBindings = @($compiledGoals | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_.optionalPlayableLoopValidationRef) }).Count
    }
    subjects = @($subjects.items)
    worldInteractions = @($compiledInteractions)
    interactionGoals = @($compiledGoals)
    boundaries = @(
        "주체 Ready는 계약 준비이며 실제 Runtime 인스턴스·Unity 배치·E5 증거가 아니다.",
        "WI Goal은 Loop 없이 존재할 수 있고 PlayableLoop는 선택적 폐쇄성 검증 묶음이다.",
        "기존 Loop Goal 투영은 읽기 호환이며 새 파생 효과나 E 승격을 만들지 않는다."
    )
}

if ($Mode -eq "Write") {
    $jsonPath = if ([string]::IsNullOrWhiteSpace($OutputJsonPath)) { [string] $policy.outputJsonPath } else { $OutputJsonPath }
    $markdownPath = if ([string]::IsNullOrWhiteSpace($OutputMarkdownPath)) { [string] $policy.outputMarkdownPath } else { $OutputMarkdownPath }
    Write-IfChanged $jsonPath (($result | ConvertTo-Json -Depth 30) + "`n")

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("# 주체·상호작용 중심 개발 상태")
    $lines.Add("")
    $lines.Add("- 기준 판본: ``$($policy.revision)``")
    $lines.Add("- 준비된 주체: ``$($result.counts.readySubjects)``")
    $lines.Add("- 주체가 결속된 WI: ``$($result.counts.worldInteractions)``")
    $lines.Add("- 상호작용 Goal: ``$($result.counts.interactionGoals)``")
    $lines.Add("- 기존 Goal 호환 투영: ``$($result.counts.legacyProjectedGoals)``")
    $lines.Add("- Loop 없는 신규 Goal: ``$($result.counts.nativeLooplessGoals)``")
    $lines.Add("")
    $lines.Add("개발 순서: ``SubjectFoundation → InteractionCore → DirectResult → DerivedEffects → OptionalPlayableLoopValidation``")
    $lines.Add("")
    $lines.Add("| Goal | WI | 준비된 주체 | 선택적 검증 묶음 | 파생 작용 | 상태 |")
    $lines.Add("| --- | --- | --- | --- | --- | --- |")
    foreach ($goal in @($compiledGoals)) {
        $loopText = if ([string]::IsNullOrWhiteSpace([string] $goal.optionalPlayableLoopValidationRef)) { "없음" } else { "``$($goal.optionalPlayableLoopValidationRef)``" }
        $lines.Add("| ``$(Escape-Cell $goal.goalStableId)`` | ``$(Escape-Cell $goal.worldInteractionId)`` | $(Escape-Cell ($goal.subjectBindingRefs -join '<br>')) | $loopText | $($goal.derivedEffectBinding.applicabilityCode) | $($goal.goalStateCode) |")
    }
    $lines.Add("")
    $lines.Add("> 이 표는 개발 관문과 호환 투영이다. 주체 Ready나 기존 E를 새 Runtime·Game View·E 승격 증거로 해석하지 않는다.")
    Write-IfChanged $markdownPath (($lines -join "`n") + "`n")
}

Write-Output "SubjectInteractionDevelopmentValid:Subjects=$($result.counts.readySubjects);WorldInteractions=$($result.counts.worldInteractions);Goals=$($result.counts.interactionGoals);Loopless=$($result.counts.nativeLooplessGoals);Legacy=$($result.counts.legacyProjectedGoals)"
