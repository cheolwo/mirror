# D387 문서·대장·명세 연결 회귀. 자산 조사/Unity 실행/파일 쓰기는 하지 않는다.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function Read-Json([string] $Path) {
    Get-Content -LiteralPath (Join-Path $root $Path) -Raw -Encoding UTF8 | ConvertFrom-Json
}
$script:cases = 0
function Assert-Survey([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "PresentationSyntySurveyInvalid:$Code" }
    $script:cases++
}
$catalog = Read-Json 'eng/execution-ledgers/playable-loop-presentation-validation-modules.json'
$e2 = @($catalog.modules | Where-Object moduleCode -eq 'presentation-projection-lifecycle')[0]
$e4 = @($catalog.modules | Where-Object moduleCode -eq 'presentation-binding')[0]
$e5 = @($catalog.modules | Where-Object moduleCode -eq 'visual-source-bounds')[0]
$actorActionE6 = @($catalog.modules | Where-Object moduleCode -eq 'actor-action-animation-readability')[0]
Assert-Survey ($catalog.modules.Count -eq 19 -and $catalog.commonModuleCodes.Count -eq 8) 'AnimationRefinementModuleAddedWithoutNewStage'
Assert-Survey (($catalog.allowedEvidenceStageCodes -join ',') -ceq 'E1,E2,E3,E4,E5,E6,E7') 'VerticalStagesUnchanged'
Assert-Survey ($e2.evidenceStageCode -ceq 'E2' -and $e4.evidenceStageCode -ceq 'E4' -and $e5.evidenceStageCode -ceq 'E5') 'ExistingStageOwnership'
Assert-Survey (($e2.reads -join ' ').Contains('가벼운 파일 선행 조회')) 'LightweightLookup'
Assert-Survey (($e2.outputs -join ' ').Contains('실제 적합성 확정이나 전체 팩 실측 선행조건 아님')) 'NoExhaustivePrerequisite'
Assert-Survey ($e2.implementationRefs -contains 'repo:eng/execution-ledgers/playable-loop-synty-expression-modules.json') 'ExistingCatalogReused'
foreach ($decision in @('그대로 재사용','연결·설정 보완','형상·리깅·동작 가공 필요','신규 제작 필요','미검사')) {
    Assert-Survey (($e4.outputs -join ' ').Contains($decision)) "Decision:$decision"
}
Assert-Survey (($e4.outputs -join ' ').Contains('동일 자산/판본/문맥 근거 재사용·변경분만 재검증')) 'ReuseContextBoundEvidence'
Assert-Survey (($e4.outputs -join ' ').Contains('사유 있는 NotApplicable')) 'NonAssetExclusion'
Assert-Survey (($e5.outputs -join ' ').Contains('파일 존재·E4 조사만으로 E5 통과하지 않음')) 'NoSurveyPromotion'
Assert-Survey ($catalog.principles.e5RequiresMinimalReadableWorldManifestationNotFinalAnimation -and
    $catalog.principles.staticPoseProgressOrStateSwapMaySatisfyE5 -and
    $catalog.principles.actorActionAnimationBindingBelongsToE6) 'E5E6AnimationBoundary'
Assert-Survey ($actorActionE6.evidenceStageCode -ceq 'E6' -and
    $actorActionE6.requiredFeatureCodes -contains 'ActorAction') 'ActorActionAnimationOwnedByE6'
$guidance = 'repo:docs/AI/Presentation단계별Synty자산조사-2026-08-31.md'
Assert-Survey ($e2.implementationRefs -contains $guidance -and $e4.implementationRefs -contains $guidance) 'GuidanceBound'
$template = Read-Json 'eng/execution-ledgers/work-orders/interaction-e1-e7-validation.template.json'
$farm = Read-Json 'eng/execution-ledgers/work-orders/farm-crop-cycle.e7-work-order.json'
foreach ($order in @($template,$farm)) {
    Assert-Survey ($null -ne $order.presentationE4Preparation -and -not $order.promotionEligible) 'PreparationNoPromotion'
    $plan2 = @($order.trackPlans.presentation.downwardPlan | Where-Object code -eq 'E2')[0]
    $plan4 = @($order.trackPlans.presentation.downwardPlan | Where-Object code -eq 'E4')[0]
    Assert-Survey ($plan2.summary.Contains('가볍') -or $plan2.summary.Contains('가벼운')) 'SpecificationLookup'
    Assert-Survey ($plan4.summary.Contains('미검사') -and $plan4.summary.Contains('presentationE4Preparation')) 'SpecificationSurvey'
}
Assert-Survey ($farm.activeWorldInteractionId -ceq 'WI-FARM-01' -and
    $farm.trackPlans.logic.currentEvidenceStage -ceq 'E3' -and
    $farm.trackPlans.presentation.currentEvidenceStage -ceq 'E1' -and
    $farm.currentEvidenceStage -ceq 'E1') 'FarmAuthorityAndMaturityUnchanged'
Assert-Survey (@($farm.presentationModuleBindings | Where-Object statusCode -eq 'Passed').Count -eq 0) 'NoModulePassedFromGuidance'
$generated = Get-Content -LiteralPath (Join-Path $root 'docs/AI/generated/playable-loop-presentation-validation.md') -Raw -Encoding UTF8
Assert-Survey ($generated.Contains($guidance) -and $generated.Contains('신규 제작 필요') -and $generated.Contains('가벼운 파일 선행 조회')) 'GeneratedResponsibilities'
Write-Output "PresentationSyntySurveyTestsPassed:Cases=$script:cases;AssetInspection=False;Editor=False"
