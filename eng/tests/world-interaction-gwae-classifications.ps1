$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$managerPath = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-world-interaction-gwae-classifications.ps1'

& pwsh -NoProfile -File $managerPath -Mode Check
if ($LASTEXITCODE -ne 0) {
    throw "WorldInteractionGwaeClassificationCheckFailed:$LASTEXITCODE"
}

$catalog = Get-Content -Raw -Encoding UTF8 (Join-Path $repositoryRoot 'eng/execution-ledgers/world-interactions.json') | ConvertFrom-Json
$output = Get-Content -Raw -Encoding UTF8 (Join-Path $repositoryRoot 'docs/AI/generated/world-interaction-gwae-classifications.json') | ConvertFrom-Json
$markdown = Get-Content -Raw -Encoding UTF8 (Join-Path $repositoryRoot 'docs/AI/generated/world-interaction-gwae-classifications.md')

if ([string] $output.schemaVersion -ne 'mirror-world-interaction-gwae-classification-output.v3') { throw 'WorldInteractionGwaeOutputSchemaInvalid' }
if ([string] $output.sourceRevision -ne 'mirror-world-interaction-gwae-classifications.r11') { throw 'WorldInteractionGwaeSourceRevisionInvalid' }
$e5Policy = $output.e5RoleObjectActionGatePolicy
if ([string] $e5Policy.requiredFromEvidenceStageCode -ne 'E5' -or [string] $e5Policy.worldObjectScopeCode -ne 'AllWorldObjects') { throw 'E5RoleObjectActionGatePolicyInvalid' }
if (@($e5Policy.allowedRoleObjectKindCodes).Count -ne 9 -or -not (@($e5Policy.allowedRoleObjectKindCodes) -contains 'Environment')) { throw 'E5AllWorldObjectKindsMissing' }

$elementMap = @{}
foreach ($definition in @($output.gwaeDefinitions)) { $elementMap[[string] $definition.code] = [string] $definition.element }
if ($elementMap['TAE'] -ne '금' -or $elementMap['JIN'] -ne '목' -or $elementMap['GAM'] -ne '수' -or $elementMap['RI'] -ne '화' -or $elementMap['GAN'] -ne '토') { throw 'FiveElementGwaeMapInvalid' }

$engineProfile = $output.engineCompositionProfile
if ([string] $engineProfile.engineCode -ne 'FiveElementWorkflow') { throw 'FiveElementWorkflowEngineProfileMissing' }
if ([string] $engineProfile.primaryGwae -ne 'GAN' -or [string] $engineProfile.supportGwae -ne 'GAM') { throw 'FiveElementWorkflowEngineGwaeInvalid' }
if ([string] $engineProfile.meaningRevision -ne 'five-element-workflow-engine-meaning.r1') { throw 'FiveElementWorkflowEngineMeaningRevisionInvalid' }
if ([bool] $engineProfile.isExecutionAuthority) { throw 'FiveElementWorkflowEngineOwnsExecution' }
if (@($engineProfile.sourceStableIds).Count -lt 2) { throw 'FiveElementWorkflowEngineSourcesMissing' }

$expectedWorkflowMeaningGwae = [ordered]@{
    'workflow-meaning:restaurant.r1' = 'RI'
    'workflow-meaning:order.r1' = 'JIN'
    'workflow-meaning:dispatch.r1' = 'GAN'
    'workflow-meaning:delivery.r1' = 'GAM'
    'workflow-meaning:warehouse.r1' = 'TAE'
}
if (@($output.workflowMeaningProfiles).Count -ne $expectedWorkflowMeaningGwae.Count) { throw 'WorkflowMeaningProfileCountInvalid' }
foreach ($entry in $expectedWorkflowMeaningGwae.GetEnumerator()) {
    $profile = @($output.workflowMeaningProfiles | Where-Object profileId -eq $entry.Key)
    if ($profile.Count -ne 1 -or [string] $profile[0].primaryGwae -ne [string] $entry.Value) { throw "WorkflowMeaningProfileInvalid:$($entry.Key)" }
    if ([bool] $profile[0].isExecutionAuthority) { throw "WorkflowMeaningProfileOwnsExecution:$($entry.Key)" }
    if (@($profile[0].codeStepKeys).Count -eq 0 -or [string]::IsNullOrWhiteSpace([string] $profile[0].stablePurpose)) { throw "WorkflowMeaningProfileEvidenceMissing:$($entry.Key)" }
}
$dispatchMeaning = @($output.workflowMeaningProfiles | Where-Object profileId -eq 'workflow-meaning:dispatch.r1')
if ($dispatchMeaning.Count -ne 1 -or [string] $dispatchMeaning[0].meaningRevision -ne 'dispatch-workflow-meaning.r2' -or [string] $dispatchMeaning[0].supersedesMeaningRevision -ne 'dispatch-workflow-meaning.r1') { throw 'DispatchWorkflowMeaningRevisionInvalid' }
if (-not $markdown.Contains('| 프로필 | 의미 판본 | 업무 | 대표 괘 | 코드 계보 | 안정 목적 | 실행 권위 |')) { throw 'WorkflowMeaningMarkdownMissing' }
if (-not $markdown.Contains('| 엔진 | 의미 판본 | 주 괘 | 보조 괘 | 안정 목적 | 실행 권위 |')) { throw 'EngineCompositionMarkdownMissing' }
if (-not $markdown.Contains('개별 전이의 모듈·대표 괘나 실행 권위를 바꾸지 않는다')) { throw 'EngineCompositionAuthorityBoundaryMissing' }
if (-not $markdown.Contains('상생·상극은 명령 라우팅이나 권한 부여에 사용하지 않는다')) { throw 'WorkflowMeaningAuthorityBoundaryMissing' }

if (@($catalog.items).Count -ne @($output.items).Count) { throw 'WorldInteractionCoverageMismatch' }
if (@($output.items | Where-Object { [string]::IsNullOrWhiteSpace([string] $_.actionGwae) }).Count -ne 0) { throw 'ActionGwaeMissing' }
if (@($output.items | Where-Object { $_.targetMode -eq 'Fixed' -and [string]::IsNullOrWhiteSpace([string] $_.targetGwae) }).Count -ne 0) { throw 'FixedTargetGwaeMissing' }
if (@($output.items | Where-Object classificationStatus -notin @('ReviewedExplicit', 'ReviewedByMeaningRule')).Count -ne 0) { throw 'UnreviewedClassificationRemaining' }
if (@($output.items | Where-Object { $_.e5RoleObjectActionGate.applicabilityCode -ne 'Required' }).Count -ne 0) { throw 'E5RoleObjectActionGateCoverageMissing' }
if (@($output.items | Where-Object { [string]::IsNullOrWhiteSpace([string] $_.e5RoleObjectActionGate.roleObjectKindCode) -or [string]::IsNullOrWhiteSpace([string] $_.e5RoleObjectActionGate.actionCode) }).Count -ne 0) { throw 'E5RoleObjectActionBindingMissing' }
if (@($output.items | Where-Object { @($_.e5RoleObjectActionGate.startStateCodes).Count -eq 0 -or @($_.e5RoleObjectActionGate.completionStateCodes).Count -eq 0 -or @($_.e5RoleObjectActionGate.effectCodes).Count -eq 0 }).Count -ne 0) { throw 'E5RoleObjectActionTransitionMissing' }
if (-not $markdown.Contains('| WI | 제목 | E5 역할 객체 | 권위 행위·전환 |')) { throw 'E5RoleObjectActionMarkdownColumnsMissing' }
if (-not $markdown.Contains('`Required/ResolvedExecutionObject`')) { throw 'E5ResolvedRoleObjectMarkdownMissing' }

$consume = @($output.items | Where-Object wiId -eq 'WI-ACTOR-CONSUME')
if ($consume.Count -ne 1 -or $consume[0].targetMode -ne 'InheritFromTargetObject' -or $consume[0].operationMode -ne 'InheritFromTargetObject') { throw 'ConsumeInheritanceInvalid' }

$woodcutting = @($output.items | Where-Object wiId -eq 'WI-NATURE-06')
if ($woodcutting.Count -ne 1) { throw 'WoodcuttingClassificationMissing' }
if ($woodcutting[0].actionGwae -ne 'JIN') { throw 'WoodcuttingActionGwaeInvalid' }
if ($woodcutting[0].operationGwae -ne 'TAE') { throw 'WoodcuttingOperationGwaeInvalid' }
if ($woodcutting[0].targetGwae -ne 'JIN') { throw 'WoodcuttingTargetGwaeInvalid' }
if ($woodcutting[0].supportGwae -ne 'GAN') { throw 'WoodcuttingSupportGwaeInvalid' }

$farmProduction = @($output.items | Where-Object wiId -in @('WI-FARM-01','WI-FARM-02','WI-FARM-03','WI-FARM-04','WI-FARM-05','WI-FARM-06'))
if ($farmProduction.Count -ne 6) { throw 'FarmProductionGwaeCoverageMissing' }
if (@($farmProduction | Where-Object classificationStatus -ne 'ReviewedExplicit').Count -ne 0) { throw 'FarmProductionGwaeExplicitReviewMissing' }
if (($farmProduction | Where-Object wiId -eq 'WI-FARM-02').operationGwae -ne 'TAE') { throw 'FarmSowingOperationGwaeInvalid' }
if (($farmProduction | Where-Object wiId -eq 'WI-FARM-03').actionGwae -ne 'GAN' -or ($farmProduction | Where-Object wiId -eq 'WI-FARM-03').operationGwae -ne 'GAM') { throw 'FarmCropCareOperationGwaeInvalid' }
if (($farmProduction | Where-Object wiId -eq 'WI-FARM-04').supportGwae -ne 'GAM') { throw 'FarmHarvestSupportGwaeInvalid' }
$farmHarvest = @($farmProduction | Where-Object wiId -eq 'WI-FARM-04')
if ($farmHarvest[0].actionGwae -ne 'TAE' -or $farmHarvest[0].targetGwae -ne 'JIN') { throw 'FarmHarvestPrimaryElementInvalid' }
if (@($farmHarvest[0].elementRelations).Count -ne 1 -or $farmHarvest[0].elementRelations[0].relationCode -ne 'KE' -or $farmHarvest[0].elementRelations[0].displayName -ne '금극목') { throw 'FarmHarvestElementRelationInvalid' }
$farmCare = @($farmProduction | Where-Object wiId -eq 'WI-FARM-03')
if ((@($farmCare[0].elementRelations.displayName) -join ',') -ne '수생목,토극수,금극목,목생화') { throw 'FarmCropCareElementRelationsInvalid' }
if ((@($farmCare[0].elementRelations.applicationMode) -join ',') -ne 'RequiredInput,RequiredConstraint,ConditionalCare,Outcome') { throw 'FarmCropCareElementRelationModesInvalid' }

$recovery = @($output.items | Where-Object wiId -eq 'WI-NATURE-04')
if ($recovery.Count -ne 1) { throw 'RecoveryClassificationMissing' }
if ($recovery[0].actionGwae -ne 'GAN' -or $recovery[0].targetGwae -ne 'GAN') { throw 'RecoveryGwaeInvalid' }

$plan = @($output.items | Where-Object wiId -eq 'WI-ACTOR-PLAN-SET')
if ($plan.Count -ne 1 -or $plan[0].actionGwae -ne 'GAN' -or $plan[0].targetGwae -ne 'GAN') { throw 'PersonalPlanGwaeInvalid' }

$blueprint = @($output.items | Where-Object wiId -eq 'WI-CON-BLUEPRINT-PLACE')
if ($blueprint.Count -ne 1 -or $blueprint[0].actionGwae -ne 'GAN' -or $blueprint[0].operationGwae -ne 'TAE') { throw 'BlueprintGwaeInvalid' }

$heatSource = @($output.items | Where-Object wiId -eq 'WI-HEAT-SOURCE-STATE-CHANGE')
if ($heatSource.Count -ne 1 -or $heatSource[0].operationMode -ne 'ByActionCode') { throw 'HeatSourceOperationModeInvalid' }

$regeneration = @($output.items | Where-Object wiId -eq 'WI-WORLD-RESOURCE-REGENERATE')
if ($regeneration.Count -ne 1 -or $regeneration[0].subjectKind -ne 'World') { throw 'WorldRegenerationSubjectInvalid' }

$hubLoading = @($output.items | Where-Object wiId -eq 'WI-HUB-06')
if ($hubLoading.Count -ne 1 -or $hubLoading[0].actionGwae -ne 'RI' -or $hubLoading[0].operationGwae -ne 'TAE' -or $hubLoading[0].elementRelations[0].displayName -ne '화생토') { throw 'HubLoadingFiveElementTransitionInvalid' }
$hubTransport = @($output.items | Where-Object wiId -eq 'WI-MARKET-01')
if ($hubTransport.Count -ne 1 -or $hubTransport[0].actionGwae -ne 'GAM' -or $hubTransport[0].elementRelations[0].displayName -ne '금생수') { throw 'HubTransportFiveElementTransitionInvalid' }
$marketReceipt = @($output.items | Where-Object wiId -eq 'WI-MARKET-02')
if ($marketReceipt.Count -ne 1 -or $marketReceipt[0].actionGwae -ne 'JIN' -or $marketReceipt[0].elementRelations[0].displayName -ne '수생목') { throw 'MarketReceiptFiveElementTransitionInvalid' }

$farmProfile = @($output.roleObjectActionProfiles | Where-Object profileId -eq 'role-action-profile:farm-crop-cycle.v1')
if ($farmProfile.Count -ne 1 -or [string] $farmProfile[0].e5ReadinessCode -ne 'Conditional' -or @($farmProfile[0].steps).Count -ne 4) { throw 'FarmRoleObjectActionProfileInvalid' }
$deliveryProfile = @($output.roleObjectActionProfiles | Where-Object profileId -eq 'role-action-profile:urban-motorcycle-parcel.v1')
if ($deliveryProfile.Count -ne 1 -or [string] $deliveryProfile[0].e5ReadinessCode -ne 'Blocked' -or @($deliveryProfile[0].steps).Count -ne 6) { throw 'DeliveryRoleObjectActionProfileInvalid' }
if ((@($deliveryProfile[0].steps | ForEach-Object gwaeCode) -join ',') -ne 'JIN,RI,GAN,TAE,GAM,JIN') { throw 'DeliveryFiveElementSequenceInvalid' }
if (@($deliveryProfile[0].steps | Where-Object bindingKindCode -eq 'Gap').Count -ne 2 -or @($deliveryProfile[0].openGapRefs).Count -ne 2) { throw 'DeliveryE5GapsWereHidden' }

Write-Output "WorldInteractionGwaeClassificationTests:Passed:$(@($output.items).Count)"
