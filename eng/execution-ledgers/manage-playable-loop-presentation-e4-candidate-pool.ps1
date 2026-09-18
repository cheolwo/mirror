[CmdletBinding()]
param(
    [ValidateSet('Write', 'Check', 'Query')]
    [string] $Mode = 'Check',
    [string] $CatalogPath = 'eng/execution-ledgers/playable-loop-presentation-e4-candidate-pool.json',
    [string] $OutputPath = 'docs/AI/generated/playable-loop-presentation-e4-candidate-pool.md',
    [string] $MachineOutputPath = 'docs/AI/generated/playable-loop-presentation-e4-candidate-pool.json',
    [ValidateSet('All', 'Plan', 'Area', 'WI', 'Readiness', 'Transition')]
    [string] $QueryKind = 'All',
    [string] $QueryValue = '',
    [string] $UnityProjectRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../common/deterministic-text-output.ps1')

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "PresentationE4PoolInvalid:$Code" }
}

function Require-Text([object] $Value, [string] $Code) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $Value)) $Code
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Escape-Cell([object] $Value) {
    return ([string] $Value).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

function Resolve-RepoPath([string] $RepositoryRoot, [string] $RelativePath) {
    Require-Text $RelativePath 'PathMissing'
    Require (-not [IO.Path]::IsPathRooted($RelativePath)) "PathRooted:$RelativePath"
    $candidate = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
    $prefix = $RepositoryRoot.TrimEnd('\') + '\'
    Require ($candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) "PathTraversal:$RelativePath"
    Require (Test-Path -LiteralPath $candidate -PathType Leaf) "PathMissing:$RelativePath"
    return $candidate
}

function Get-PlanRows([string] $PlanningPath, [string] $RepositoryRoot) {
    $parent = Split-Path -Parent $PlanningPath
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -LiteralPath $PlanningPath -Encoding UTF8) {
        if ($line -notmatch '^\| `(?<id>PLAN-[^`]+)` \| (?<document>.+?) \| `(?<status>[^`]+)` \|') { continue }
        $planId = [string] $Matches.id
        $planStatus = [string] $Matches.status
        $documentCell = [string] $Matches.document
        Require ($documentCell -match '\[[^\]]+\]\((?<path>[^)]+)\)') "PlanDocumentLinkMissing:$planId"
        $rawLink = [string] $Matches.path
        $link = $rawLink.Split('#')[0]
        $resolved = [IO.Path]::GetFullPath((Join-Path $parent $link))
        $prefix = $RepositoryRoot.TrimEnd('\') + '\'
        Require ($resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) "PlanDocumentTraversal:$planId"
        Require (Test-Path -LiteralPath $resolved -PathType Leaf) "PlanDocumentMissing:$planId"
        $relative = $resolved.Substring($RepositoryRoot.Length + 1).Replace('\', '/')
        $text = Get-Content -LiteralPath $resolved -Raw -Encoding UTF8
        $revision = 'Unspecified'
        if ($text -match '(?m)^\s*(?:-\s*)?(?:기획\s*)?(?:판본|revision)\s*[:：]\s*`?(?<revision>[^`\r\n]+)') {
            $revision = $Matches.revision.Trim()
        }
        $rows.Add([pscustomobject]@{
            planId = $planId
            status = $planStatus
            primaryStatus = $planStatus.Split('/')[0].Trim()
            documentRef = $relative
            documentSha256 = Get-Sha256 $resolved
            revision = $revision
        })
    }
    return @($rows)
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$resolvedCatalog = Resolve-RepoPath $repositoryRoot $CatalogPath
$catalog = Get-Content -LiteralPath $resolvedCatalog -Raw -Encoding UTF8 | ConvertFrom-Json
Require ([string] $catalog.schemaVersion -eq 'playable-loop-presentation-e4-candidate-pool.v2') 'SchemaInvalid'

$baselinePath = Resolve-RepoPath $repositoryRoot ([string] $catalog.planningBaseline.path)
$planningPath = Resolve-RepoPath $repositoryRoot ([string] $catalog.planningIndex.path)
Require ((Get-Sha256 $baselinePath) -ceq [string] $catalog.planningBaseline.sha256) 'PlanningBaselineStale'
Require ((Get-Sha256 $planningPath) -ceq [string] $catalog.planningIndex.sha256) 'PlanningIndexStale'

$plans = @(Get-PlanRows $planningPath $repositoryRoot)
Require ($plans.Count -eq [int] $catalog.planningIndex.expectedPlanCount) "PlanCount:$($plans.Count)"
Require ($plans.Count -eq @($plans.planId | Sort-Object -Unique).Count) 'PlanDuplicate'
$planById = @{}
$notApplicable = @($catalog.classificationPolicy.notApplicablePlanIds | ForEach-Object { [string] $_ })
Require ($notApplicable.Count -eq @($notApplicable | Sort-Object -Unique).Count) 'NotApplicablePlanDuplicate'
foreach ($id in $notApplicable) { Require (@($plans.planId) -contains $id) "NotApplicablePlanUnknown:$id" }
$prefixes = @($catalog.classificationPolicy.frozenPrimaryStatusPrefixes | ForEach-Object { [string] $_ })
$classifiedPlans = foreach ($plan in $plans) {
    $classification = if ($notApplicable -contains $plan.planId) { 'NotApplicable' }
    elseif (@($prefixes | Where-Object { $plan.primaryStatus.StartsWith($_, [StringComparison]::Ordinal) }).Count -gt 0) { 'FrozenCandidate' }
    else { 'ProvisionalRequirement' }
    $row = [pscustomobject]@{
        planId = $plan.planId; status = $plan.status; primaryStatus = $plan.primaryStatus
        revision = $plan.revision; documentRef = $plan.documentRef
        documentSha256 = $plan.documentSha256; classificationCode = $classification
        reason = if ($classification -eq 'NotApplicable') { [string] $catalog.classificationPolicy.notApplicableReason } elseif ($classification -eq 'FrozenCandidate') { '승인·확정된 현재 주 상태를 후보 동결 입력으로 사용할 수 있다.' } else { '현재 주 상태가 승인·확정 전이므로 요구만 보존하고 자산·VisualKey를 동결하지 않는다.' }
    }
    $planById[$plan.planId] = $row
    $row
}
Require ($classifiedPlans.Count -eq $plans.Count) 'PlanClassificationIncomplete'

$loopsPath = Resolve-RepoPath $repositoryRoot ([string] $catalog.sourceCatalogs.playableLoops)
$wisPath = Resolve-RepoPath $repositoryRoot ([string] $catalog.sourceCatalogs.worldInteractions)
$modulePath = Resolve-RepoPath $repositoryRoot ([string] $catalog.sourceCatalogs.presentationModules)
$syntyPath = Resolve-RepoPath $repositoryRoot ([string] $catalog.sourceCatalogs.syntyExpressionModules)
$hCatalogPath = Resolve-RepoPath $repositoryRoot ([string] $catalog.sourceCatalogs.hCatalog)
$graphMapPath = Resolve-RepoPath $repositoryRoot ([string] $catalog.graphMapRef)
$loops = (Get-Content $loopsPath -Raw -Encoding UTF8 | ConvertFrom-Json).items
$wis = (Get-Content $wisPath -Raw -Encoding UTF8 | ConvertFrom-Json).items
$modules = Get-Content $modulePath -Raw -Encoding UTF8 | ConvertFrom-Json
$synty = Get-Content $syntyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$hCatalog = Get-Content $hCatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$graphMap = Get-Content $graphMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require-Text $graphMap.graphMapStableId 'GraphMapStableIdMissing'
Require-Text $graphMap.revision 'GraphMapRevisionMissing'
$hCatalogRoot = Split-Path -Parent $hCatalogPath
$hCatalogDefinitions = @{}
foreach ($propertyName in @('h1DefinitionRefs','h2DefinitionRefs','h3DefinitionRefs','h4DefinitionRefs')) {
    foreach ($entry in @($hCatalog.$propertyName)) {
        $definitionPath = [IO.Path]::GetFullPath((Join-Path $hCatalogRoot ([string] $entry.definitionPath)))
        $relative = $definitionPath.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        $hCatalogDefinitions[$relative] = [string] $entry.stableId
    }
}
$loopById = @{}; foreach ($loop in $loops) { $loopById[[string] $loop.loopStableId] = $loop }
$wiById = @{}; foreach ($wi in $wis) { $wiById[[string] $wi.id] = $wi }

$transitionPolicy = $catalog.stateTransitionVisualPolicy
Require ($null -ne $transitionPolicy) 'TransitionPolicyMissing'
Require ([string] $transitionPolicy.fiveElementMetadataPolicy -eq 'OptionalDiagnosticMetadataOnly') 'FiveElementPolicyInvalid'
Require-Text $transitionPolicy.fiveElementNotice 'FiveElementNoticeMissing'
$strategyCodes = @($transitionPolicy.representationStrategyCodes | ForEach-Object { [string] $_ })
$coverageStatusCodes = @($transitionPolicy.coverageStatusCodes | ForEach-Object { [string] $_ })
$blenderStatusCodes = @($transitionPolicy.blenderStatusCodes | ForEach-Object { [string] $_ })
Require ($strategyCodes.Count -eq @($strategyCodes | Sort-Object -Unique).Count) 'TransitionStrategyDuplicate'
Require ($coverageStatusCodes.Count -eq @($coverageStatusCodes | Sort-Object -Unique).Count) 'TransitionCoverageStatusDuplicate'
Require ($blenderStatusCodes.Count -eq @($blenderStatusCodes | Sort-Object -Unique).Count) 'TransitionBlenderStatusDuplicate'

$transitionIds = @{}
$transitionRecords = [Collections.Generic.List[object]]::new()
foreach ($transition in @($catalog.stateTransitionVisualPreparations)) {
    $transitionId = [string] $transition.transitionStableId
    Require-Text $transitionId 'TransitionIdMissing'
    Require (-not $transitionIds.ContainsKey($transitionId)) "TransitionDuplicate:$transitionId"
    $transitionIds[$transitionId] = $true
    Require ($planById.ContainsKey([string] $transition.planId)) "TransitionPlanUnknown:$transitionId"
    Require ($wiById.ContainsKey([string] $transition.worldInteractionId)) "TransitionWiUnknown:$transitionId"
    Require-Text $transition.subjectStableId "TransitionSubjectMissing:$transitionId"
    Require (@($transition.startStateCodes).Count -gt 0) "TransitionStartStateMissing:$transitionId"
    Require (@($transition.resultStateCodes).Count -gt 0) "TransitionResultStateMissing:$transitionId"
    Require-Text $transition.playerReadableMoment "TransitionReadableMomentMissing:$transitionId"
    Require (@($transition.visualKeys).Count -gt 0) "TransitionVisualKeyMissing:$transitionId"
    Require-Text $transition.interactionAnchorIntent "TransitionAnchorIntentMissing:$transitionId"
    Require (@($transition.representationCandidates).Count -gt 0) "TransitionRepresentationMissing:$transitionId"
    Require (@($transition.continuityRequirements).Count -gt 0) "TransitionContinuityMissing:$transitionId"
    Require (@('Ready','Conditional','Blocked','NotApplicable') -contains [string] $transition.readinessCode) "TransitionReadinessInvalid:$transitionId"

    foreach ($candidate in @($transition.representationCandidates)) {
        Require ($strategyCodes -contains [string] $candidate.strategyCode) "TransitionStrategyInvalid:${transitionId}:$($candidate.strategyCode)"
        Require ($coverageStatusCodes -contains [string] $candidate.coverageStatusCode) "TransitionCoverageStatusInvalid:${transitionId}:$($candidate.coverageStatusCode)"
        Require ($blenderStatusCodes -contains [string] $candidate.blenderStatusCode) "TransitionBlenderStatusInvalid:${transitionId}:$($candidate.blenderStatusCode)"
        Require-Text $candidate.candidateRef "TransitionCandidateRefMissing:$transitionId"
        Require-Text $candidate.fingerprint "TransitionFingerprintMissing:$transitionId"
    }

    if ($null -ne $transition.PSObject.Properties['fiveElementMetadata']) {
        $element = $transition.fiveElementMetadata
        Require (@('Applicable','NotRequired') -contains [string] $element.applicabilityCode) "TransitionFiveElementApplicabilityInvalid:$transitionId"
        Require-Text $element.purpose "TransitionFiveElementPurposeMissing:$transitionId"
        if ([string] $element.applicabilityCode -eq 'Applicable') {
            Require (@('Wood','Fire','Earth','Metal','Water') -contains [string] $element.primaryElementCode) "TransitionFiveElementCodeInvalid:$transitionId"
        }
    }

    if ([string] $transition.readinessCode -eq 'Ready') {
        Require (@($transition.representationCandidates | Where-Object coverageStatusCode -eq 'Missing').Count -eq 0) "ReadyTransitionCoverageMissing:$transitionId"
        Require (@($transition.representationCandidates | Where-Object {
            [string] $_.strategyCode -eq 'BlenderOwnedVariant' -and
            [string] $_.blenderStatusCode -notin @('BlenderValidatedCopy','UnityImportCandidate')
        }).Count -eq 0) "ReadyTransitionBlenderUnvalidated:$transitionId"
        Require ($null -ne $transition.placementMapRef -and -not [string]::IsNullOrWhiteSpace([string] $transition.placementMapRef)) "ReadyTransitionPlacementMissing:$transitionId"
        Resolve-RepoPath $repositoryRoot ([string] $transition.placementMapRef) | Out-Null
    }

    $transitionRecords.Add([pscustomobject]@{
        transitionStableId = $transitionId
        planId = [string] $transition.planId
        planningRevision = [string] $planById[[string] $transition.planId].revision
        planningSha256 = [string] $planById[[string] $transition.planId].documentSha256
        worldInteractionId = [string] $transition.worldInteractionId
        worldInteractionTitle = [string] $wiById[[string] $transition.worldInteractionId].title
        subjectStableId = [string] $transition.subjectStableId
        subjectKindCode = [string] $transition.subjectKindCode
        startStateCodes = @($transition.startStateCodes)
        activeStateCodes = @($transition.activeStateCodes)
        resultStateCodes = @($transition.resultStateCodes)
        recoveryStateCodes = @($transition.recoveryStateCodes)
        playerReadableMoment = [string] $transition.playerReadableMoment
        visualKeys = @($transition.visualKeys)
        interactionAnchorIntent = [string] $transition.interactionAnchorIntent
        placementMapRef = $transition.placementMapRef
        representationCandidates = @($transition.representationCandidates)
        continuityRequirements = @($transition.continuityRequirements)
        fiveElementMetadata = $transition.fiveElementMetadata
        readinessCode = [string] $transition.readinessCode
        earliestReopenStage = [string] $transition.earliestReopenStage
        missing = @($transition.missing)
        nextOwner = [string] $transition.nextOwner
    })
}
Require ($transitionRecords.Count -eq [int] $catalog.normalization.expectedTransitionPreparationCount) "TransitionPreparationCount:$($transitionRecords.Count)"

$workOrderRoot = (Resolve-Path (Join-Path $repositoryRoot ([string] $catalog.normalization.workOrderDirectory))).Path
$workOrders = @(Get-ChildItem -LiteralPath $workOrderRoot -Filter '*.json' -File | Where-Object Name -ne 'interaction-e1-e7-validation.template.json')
$existingPreparations = @($workOrders | Where-Object {
    $json = Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $null -ne $json.PSObject.Properties['presentationE4Preparation']
})
Require ($existingPreparations.Count -eq [int] $catalog.normalization.expectedExistingPresentationE4PreparationCount) "ExistingPreparationCount:$($existingPreparations.Count)"
Require (@($synty.loopModules).Count -eq [int] $catalog.normalization.expectedExistingSyntyLoopModuleCount) "SyntyLoopModuleCount:$(@($synty.loopModules).Count)"

$existingVisualKeys = [Collections.Generic.List[string]]::new()
foreach ($file in $existingPreparations) {
    $workOrder = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string] $workOrder.presentationE4Preparation.applicabilityCode -ne 'Required') { continue }
    foreach ($key in @($workOrder.presentationE4Preparation.visualKeys)) {
        Require-Text $key "ExistingVisualKeyMissing:$($file.Name)"
        $existingVisualKeys.Add([string] $key)
    }
}
Require ($existingVisualKeys.Count -eq @($existingVisualKeys | Sort-Object -Unique).Count) 'ExistingVisualKeyDuplicate'

$candidateIds = @{}
$poolVisualKeys = @{}
$records = [Collections.Generic.List[object]]::new()
foreach ($area in @($catalog.areaPreparations)) {
    $areaCode = [string] $area.areaCode
    Require (@('Ready','Conditional','Blocked','NotApplicable') -contains [string] $area.readinessCode) "ReadinessInvalid:$areaCode"
    Require ($planById.ContainsKey([string] $area.planId)) "AreaPlanUnknown:$areaCode"
    $plan = $planById[[string] $area.planId]
    Require ($loopById.ContainsKey([string] $area.loopStableId)) "AreaLoopUnknown:$areaCode"
    $loop = $loopById[[string] $area.loopStableId]
    $workOrder = $null
    if ($null -ne $area.workOrderRef -and -not [string]::IsNullOrWhiteSpace([string] $area.workOrderRef)) {
        $workOrderPath = Resolve-RepoPath $repositoryRoot ([string] $area.workOrderRef)
        $workOrder = Get-Content $workOrderPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Require ([string] $workOrder.playableUnitStableId -eq [string] $area.loopStableId) "WorkOrderLoopMismatch:$areaCode"
        Require ($null -ne $workOrder.PSObject.Properties['presentationE4Preparation']) "WorkOrderPreparationMissing:$areaCode"
    }
    else {
        Require ([string] $area.readinessCode -eq 'Blocked') "MissingWorkOrderMustBlock:$areaCode"
    }
    $isFrozen = [string] $plan.classificationCode -eq 'FrozenCandidate'
    $planningGateApproved = [string] $loop.planningGate.statusCode -eq 'Approved'
    if (-not $isFrozen -or -not $planningGateApproved) {
        Require ([string] $area.readinessCode -eq 'Blocked') "UnapprovedAreaMustBlock:$areaCode"
        Require (@($area.assetCandidates).Count -eq 0) "UnapprovedAreaAssetFrozen:$areaCode"
        Require (@($area.worldInteractions | ForEach-Object { @($_.visualKeys).Count } | Measure-Object -Sum).Sum -eq 0) "UnapprovedAreaVisualKeyFrozen:$areaCode"
    }
    Resolve-RepoPath $repositoryRoot ([string] $catalog.graphMapRef) | Out-Null
    if ($null -ne $area.placementMapRef -and -not [string]::IsNullOrWhiteSpace([string] $area.placementMapRef)) {
        Resolve-RepoPath $repositoryRoot ([string] $area.placementMapRef) | Out-Null
    }
    foreach ($hRef in @($area.hDefinitionRefs)) {
        $resolvedH = Resolve-RepoPath $repositoryRoot ([string] $hRef)
        $relativeH = $resolvedH.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        Require ($hCatalogDefinitions.ContainsKey($relativeH)) "HDefinitionNotInCatalog:$relativeH"
        $hDefinition = Get-Content $resolvedH -Raw -Encoding UTF8 | ConvertFrom-Json
        Require-Text $hDefinition.stableId "HStableIdMissing:$relativeH"
        Require ([string] $hDefinition.stableId -ceq [string] $hCatalogDefinitions[$relativeH]) "HStableIdMismatch:$relativeH"
    }
    $assetById = @{}
    foreach ($asset in @($area.assetCandidates)) {
        $assetId = [string] $asset.id
        Require-Text $assetId "AssetIdMissing:$areaCode"
        Require (-not $candidateIds.ContainsKey($assetId)) "AssetDuplicate:$assetId"
        $candidateIds[$assetId] = $true; $assetById[$assetId] = $asset
        Require ([string] $asset.guid -match '^[0-9a-f]{32}$') "AssetGuidInvalid:$assetId"
        Require ([string] $asset.fileSha256 -match '^[0-9A-F]{64}$') "AssetHashInvalid:$assetId"
        Require ([string] $asset.metaSha256 -match '^[0-9A-F]{64}$') "AssetMetaHashInvalid:$assetId"
        if (-not [string]::IsNullOrWhiteSpace($UnityProjectRoot)) {
            $assetPath = [IO.Path]::GetFullPath((Join-Path $UnityProjectRoot ([string] $asset.assetPath)))
            $unityPrefix = [IO.Path]::GetFullPath($UnityProjectRoot).TrimEnd('\') + '\'
            Require ($assetPath.StartsWith($unityPrefix, [StringComparison]::OrdinalIgnoreCase)) "AssetPathTraversal:$assetId"
            Require (Test-Path -LiteralPath $assetPath -PathType Leaf) "AssetPathMissing:$assetId"
            Require ((Get-Sha256 $assetPath) -ceq [string] $asset.fileSha256) "AssetFileStale:$assetId"
            Require ((Get-Sha256 ($assetPath + '.meta')) -ceq [string] $asset.metaSha256) "AssetMetaStale:$assetId"
            $guidLine = Select-String -LiteralPath ($assetPath + '.meta') -Pattern '^guid:\s*(\S+)' | Select-Object -First 1
            Require ($null -ne $guidLine -and [string] $guidLine.Matches.Groups[1].Value -ceq [string] $asset.guid) "AssetGuidMismatch:$assetId"
        }
    }
    $expectedWis = @($loop.worldInteractionIds | ForEach-Object { [string] $_ })
    $actualWis = @($area.worldInteractions | ForEach-Object { [string] $_.id })
    $expectedWiKey = (($expectedWis | Sort-Object) -join '|')
    $actualWiKey = (($actualWis | Sort-Object) -join '|')
    Require ($expectedWiKey -ceq $actualWiKey) "AreaWiSetMismatch:$areaCode"
    foreach ($wiPreparation in @($area.worldInteractions)) {
        $wiId = [string] $wiPreparation.id
        Require ($wiById.ContainsKey($wiId)) "WiUnknown:$wiId"
        foreach ($assetId in @($wiPreparation.assetCandidateIds)) { Require ($assetById.ContainsKey([string] $assetId)) "WiAssetUnknown:${wiId}:$assetId" }
        foreach ($key in @($wiPreparation.visualKeys)) {
            Require-Text $key "VisualKeyMissing:$wiId"
            Require (-not $poolVisualKeys.ContainsKey([string] $key)) "VisualKeyDuplicate:$key"
            $poolVisualKeys[[string] $key] = $wiId
        }
        if ([string] $area.readinessCode -eq 'Ready') {
            Require ($null -ne $area.placementMapRef -and -not [string]::IsNullOrWhiteSpace([string] $area.placementMapRef)) "ReadyPlacementMapMissing:$wiId"
            Require (@($area.constraintCodes).Count -gt 0) "ReadyConstraintsMissing:$wiId"
            Require (@($wiPreparation.visualKeys).Count -gt 0) "ReadyVisualKeyMissing:$wiId"
            Require-Text $wiPreparation.interactionAnchor "ReadyAnchorMissing:$wiId"
            Require (@($wiPreparation.assetCandidateIds).Count -gt 0) "ReadyAssetMissing:$wiId"
            $resolvedPlacement = Resolve-RepoPath $repositoryRoot ([string] $area.placementMapRef)
            $placement = Get-Content $resolvedPlacement -Raw -Encoding UTF8 | ConvertFrom-Json
            Require ($null -ne $placement.PSObject.Properties['placementMapRef'] -and -not [string]::IsNullOrWhiteSpace([string] $placement.placementMapRef)) "ReadyFrozenPlacementMissing:$wiId"
            Require ([string] $placement.validationResultCode -in @('Passed','Ready')) "ReadyPlacementValidationMissing:$wiId"
            if ($null -ne $wiPreparation.PSObject.Properties['subjectKindCode'] -and [string] $wiPreparation.subjectKindCode -eq 'Actor') {
                Require ($null -ne $wiPreparation.PSObject.Properties['actorPreparation']) "ReadyActorPreparationMissing:$wiId"
                foreach ($field in @('rigRef','avatarRef','clipRefs','interruptionPolicy','returnPolicy')) {
                    Require ($null -ne $wiPreparation.actorPreparation.PSObject.Properties[$field]) "ReadyActorFieldMissing:${wiId}:$field"
                    if ($field -eq 'clipRefs') { Require (@($wiPreparation.actorPreparation.clipRefs).Count -gt 0) "ReadyActorFieldMissing:${wiId}:$field" }
                    else { Require-Text $wiPreparation.actorPreparation.$field "ReadyActorFieldMissing:${wiId}:$field" }
                }
            }
        }
        $records.Add([pscustomobject]@{
            areaCode = $areaCode; planId = [string] $area.planId; planStatus = $plan.status
            planningRevision = $plan.revision; planningSha256 = $plan.documentSha256
            loopStableId = [string] $area.loopStableId; worldInteractionId = $wiId
            worldInteractionTitle = [string] $wiById[$wiId].title
            playerReadableMoment = [string] $wiById[$wiId].worldAction
            logicContext = ('Logic {0}; Integration {1}' -f $wiById[$wiId].implementation.currentStage, $wiById[$wiId].integration.currentStage)
            hDefinitionRefs = @($area.hDefinitionRefs); graphMapRef = [string] $catalog.graphMapRef
            graphMapStableId = [string] $graphMap.graphMapStableId; graphMapRevision = [string] $graphMap.revision
            graphMapSha256 = Get-Sha256 $graphMapPath; placementMapRef = $area.placementMapRef
            visualKeys = @($wiPreparation.visualKeys); interactionAnchor = [string] $wiPreparation.interactionAnchor
            assetCandidates = @($wiPreparation.assetCandidateIds | ForEach-Object { $assetById[[string] $_] })
            workOrderRef = $area.workOrderRef; readinessCode = [string] $area.readinessCode
            missing = @($area.missing); nextOwner = [string] $area.nextOwner
            earliestReopenStage = [string] $area.earliestReopenStage
        })
    }
}

$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine('# Presentation E4 후보 풀 상태판')
[void] $builder.AppendLine()
[void] $builder.AppendLine("이 문서는 ``$CatalogPath``에서 생성된다. 후보 등록은 Unity 배치·E5·Evidence 승격이 아니다.")
[void] $builder.AppendLine()
$classGroups = $classifiedPlans | Group-Object classificationCode
$readinessGroups = @($records) | Group-Object readinessCode
foreach ($code in @('FrozenCandidate','ProvisionalRequirement','NotApplicable')) { [void] $builder.AppendLine(('- 기획 {0}: `{1}`' -f $code, (@($classGroups | Where-Object Name -eq $code).Count | ForEach-Object { if ($_ -eq 0) { 0 } else { ($classGroups | Where-Object Name -eq $code).Group.Count } }))) }
foreach ($code in @('Ready','Conditional','Blocked','NotApplicable')) { $g=$readinessGroups|Where-Object Name -eq $code; [void] $builder.AppendLine(('- WI {0}: `{1}`' -f $code, $(if($null -eq $g){0}else{$g.Group.Count}))) }
[void] $builder.AppendLine(('- 기존 presentationE4Preparation 재사용: `{0}`개, Required VisualKey `{1}`개' -f $existingPreparations.Count, $existingVisualKeys.Count))
[void] $builder.AppendLine(('- 기존 Synty 폐루프 모듈 재사용: `{0}`개' -f @($synty.loopModules).Count))
[void] $builder.AppendLine(('- 상태 변화 표현 준비: `{0}`개' -f $transitionRecords.Count))
[void] $builder.AppendLine()
[void] $builder.AppendLine('## 기획 46개 분류')
[void] $builder.AppendLine()
[void] $builder.AppendLine('| 기획 | 상태 | 분류 | 판본 / SHA-256 | 문서 |')
[void] $builder.AppendLine('| --- | --- | --- | --- | --- |')
foreach ($plan in $classifiedPlans) { [void] $builder.AppendLine(('| `{0}` | {1} | {2} | `{3}` / `{4}` | `{5}` |' -f (Escape-Cell $plan.planId),(Escape-Cell $plan.status),$plan.classificationCode,(Escape-Cell $plan.revision),$plan.documentSha256,(Escape-Cell $plan.documentRef))) }
[void] $builder.AppendLine()
[void] $builder.AppendLine('## 첫 수평 묶음')
[void] $builder.AppendLine()
[void] $builder.AppendLine('| 영역 / WI | 판독 순간 | VisualKey / Anchor | H·배치 | 후보 | 준비 | 가장 이른 재개 / 담당 |')
[void] $builder.AppendLine('| --- | --- | --- | --- | --- | --- | --- |')
foreach ($record in @($records | Sort-Object areaCode,worldInteractionId)) {
    $candidateText = if (@($record.assetCandidates).Count -eq 0) { '미동결' } else { @($record.assetCandidates | ForEach-Object { "``$($_.id)`` ($($_.reuseDecision))" }) -join '<br>' }
    $spatial = (@($record.hDefinitionRefs) + @($record.placementMapRef | Where-Object { $_ })) -join '<br>'
    [void] $builder.AppendLine(('| {0} / `{1}` | {2} | {3} / {4} | {5} | {6} | **{7}**<br>{8} | {9} / {10} |' -f $record.areaCode,$record.worldInteractionId,(Escape-Cell $record.playerReadableMoment),(Escape-Cell (@($record.visualKeys) -join ', ')),(Escape-Cell $record.interactionAnchor),(Escape-Cell $spatial),(Escape-Cell $candidateText),$record.readinessCode,(Escape-Cell (@($record.missing) -join ', ')),$record.earliestReopenStage,(Escape-Cell $record.nextOwner)))
}
[void] $builder.AppendLine()
[void] $builder.AppendLine('## 상태 변화 표현 준비')
[void] $builder.AppendLine()
[void] $builder.AppendLine('오행은 선택적인 진단 메타데이터이며, E4의 판정 대상은 실제 상태 변화의 판독 가능성과 자연스러운 연속성이다.')
[void] $builder.AppendLine()
[void] $builder.AppendLine('| 변화 / WI | 시작 → 진행 → 결과 / 회복 | 표현 후보 | 연속성 | 준비 / 결손·담당 |')
[void] $builder.AppendLine('| --- | --- | --- | --- | --- |')
foreach ($transition in @($transitionRecords | Sort-Object transitionStableId)) {
    $states = ('{0} → {1} → {2} / {3}' -f (@($transition.startStateCodes) -join ', '),(@($transition.activeStateCodes) -join ', '),(@($transition.resultStateCodes) -join ', '),(@($transition.recoveryStateCodes) -join ', '))
    $representations = @($transition.representationCandidates | ForEach-Object { "``$($_.strategyCode)``/$($_.coverageStatusCode)/$($_.blenderStatusCode)" }) -join '<br>'
    [void] $builder.AppendLine(('| `{0}` / `{1}` | {2} | {3} | {4} | **{5}**<br>{6}<br>{7} |' -f $transition.transitionStableId,$transition.worldInteractionId,(Escape-Cell $states),(Escape-Cell $representations),(Escape-Cell (@($transition.continuityRequirements) -join ', ')),$transition.readinessCode,(Escape-Cell (@($transition.missing) -join ', ')),(Escape-Cell $transition.nextOwner)))
}
[void] $builder.AppendLine()
[void] $builder.AppendLine('## 정규화 경계')
[void] $builder.AppendLine()
[void] $builder.AppendLine('- 기존 Required 준비는 `workOrderRef + loop + WI + VisualKey`로 재사용하며 같은 후보를 복제하지 않는다.')
[void] $builder.AppendLine('- NotApplicable은 해당 작업 명세 범위와 사유를 보존하며 같은 WI의 다른 Required 준비를 덮어쓰지 않는다.')
[void] $builder.AppendLine('- 공간 후보는 Graph Map·배치 맵·통행·가시성 제약 없이는 Ready가 될 수 없다.')
[void] $builder.AppendLine('- 미승인 기획은 요구와 결손만 남기며 VisualKey·GUID·fingerprint를 동결하지 않는다.')
[void] $builder.AppendLine('- 상태 변화 표현은 오행별 모델 수가 아니라 시작·진행·결과·중단/회복의 판독과 동일 객체 연속성을 검사한다.')
[void] $builder.AppendLine('- Blender는 기존 Prefab·Unity 조립·재질·애니메이션으로 필요한 변화를 읽을 수 없을 때만 프로젝트 소유 파생형으로 연다.')
$generated = ConvertTo-DeterministicText $builder.ToString()
$machineOutput = ConvertTo-DeterministicText (([ordered]@{
    schemaVersion = 'playable-loop-presentation-e4-candidate-pool.generated.v1'
    revision = [string] $catalog.revision
    planningCount = $classifiedPlans.Count
    plans = @($classifiedPlans)
    worldInteractionCount = $records.Count
    items = @($records)
    stateTransitionVisualCount = $transitionRecords.Count
    stateTransitionVisualItems = @($transitionRecords)
} | ConvertTo-Json -Depth 40) + "`n")

if ($Mode -eq 'Query') {
    if ($QueryKind -eq 'Transition') {
        $selectedTransitions = @($transitionRecords | Where-Object transitionStableId -eq $QueryValue)
        [pscustomobject]@{ revision=$catalog.revision; queryKind=$QueryKind; queryValue=$QueryValue; count=$selectedTransitions.Count; items=$selectedTransitions } | ConvertTo-Json -Depth 30
        exit 0
    }
    $selected = @($records)
    switch ($QueryKind) {
        'Plan' { $selected = @($selected | Where-Object planId -eq $QueryValue) }
        'Area' { $selected = @($selected | Where-Object areaCode -eq $QueryValue) }
        'WI' { $selected = @($selected | Where-Object worldInteractionId -eq $QueryValue) }
        'Readiness' { $selected = @($selected | Where-Object readinessCode -eq $QueryValue) }
    }
    [pscustomobject]@{ revision=$catalog.revision; queryKind=$QueryKind; queryValue=$QueryValue; count=$selected.Count; items=$selected } | ConvertTo-Json -Depth 30
    exit 0
}

$resolvedOutput = Join-Path $repositoryRoot $OutputPath
$resolvedMachineOutput = Join-Path $repositoryRoot $MachineOutputPath
if ($Mode -eq 'Write') {
    $directory = Split-Path -Parent $resolvedOutput
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    Write-DeterministicTextIfChanged $resolvedOutput $generated | Out-Null
    Write-DeterministicTextIfChanged $resolvedMachineOutput $machineOutput | Out-Null
}
else {
    Require (Test-Path -LiteralPath $resolvedOutput -PathType Leaf) 'GeneratedOutputMissing'
    $existing = Get-Content -LiteralPath $resolvedOutput -Raw -Encoding UTF8
    Require ((ConvertTo-DeterministicText $existing) -ceq $generated) 'GeneratedOutputOutOfDate'
    Require (Test-Path -LiteralPath $resolvedMachineOutput -PathType Leaf) 'GeneratedMachineOutputMissing'
    $existingMachine = Get-Content -LiteralPath $resolvedMachineOutput -Raw -Encoding UTF8
    Require ((ConvertTo-DeterministicText $existingMachine) -ceq $machineOutput) 'GeneratedMachineOutputOutOfDate'
}

$counts = @{}
foreach ($code in @('FrozenCandidate','ProvisionalRequirement','NotApplicable')) { $counts[$code]=@($classifiedPlans|Where-Object classificationCode -eq $code).Count }
Write-Output ("PresentationE4CandidatePoolValid:Plans={0};Frozen={1};Provisional={2};NotApplicable={3};WI={4};Transitions={5};ExistingPreparations={6};SyntyLoopModules={7}" -f $classifiedPlans.Count,$counts.FrozenCandidate,$counts.ProvisionalRequirement,$counts.NotApplicable,$records.Count,$transitionRecords.Count,$existingPreparations.Count,@($synty.loopModules).Count)
