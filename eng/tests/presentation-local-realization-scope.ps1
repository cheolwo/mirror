# D390 기존 필드 연결의 구조 회귀. 실제 Scene/공간 간섭 검사나 E 승격 시험이 아니다.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function Read-Json([string] $Path) {
    Get-Content -LiteralPath (Join-Path $root $Path) -Raw -Encoding UTF8 | ConvertFrom-Json
}
$script:cases = 0
function Assert-Scope([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "PresentationLocalScopeInvalid:$Code" }
    $script:cases++
}
$farm = Read-Json 'eng/execution-ledgers/work-orders/farm-crop-cycle.e7-work-order.json'
$goals = Read-Json 'eng/execution-ledgers/codex-playable-loop-goals.json'
$catalog = Read-Json 'eng/execution-ledgers/playable-loop-presentation-validation-modules.json'
$gaps = $farm.presentationE4Preparation.openGapRefs -join ' '
$e5 = @($catalog.modules | Where-Object moduleCode -eq 'visual-source-bounds')[0]
$farmWork = @($goals.workItems | Where-Object loopStableId -eq 'playable-loop:farm-crop-cycle.v1')
$approval = @($farm.additionalApprovals | Where-Object designRevision -eq 'e5-local-realization-scope.design.r1')[0]
$approvalHash = (Get-FileHash -LiteralPath (Join-Path $root $approval.designDocumentRef)).Hash
Assert-Scope ($approval.statusCode -ceq 'Approved' -and $approvalHash -ceq $approval.designHashSha256 -and
    $gaps.Contains('Logic E5 사본·Session/판본') -and $gaps.Contains('지지/접근/통행') -and
    $gaps.Contains('컴포넌트(null9 과거관측)') -and $e5.severityCode -ceq 'Blocking') 'RequiredDeficitsRemainBlocking'
Assert-Scope ($gaps.Contains('간섭은 가장 이른 원인 E의 차단') -and $gaps.Contains('E9로 이관해 우회하지 않는다') -and
    ($e5.outputs -join ' ').Contains('E8 Core 둘 이상')) 'InterferenceCannotBeDeferredToE9'
Assert-Scope ($gaps.Contains('코드·순수 시험 범위에서만') -and $gaps.Contains('실제 E5로 일반화 금지') -and
    $farmWork.Count -eq 7 -and @($farmWork | Where-Object { @($_.dependsOnWorkItemIds).Count -gt 0 }).Count -eq 0 -and
    @($goals.items | Where-Object loopStableId -eq 'playable-loop:farm-crop-cycle.v1')[0].activationPrerequisiteLoopStableIds.Count -eq 0) 'NoInventedWholeWorldPrerequisite'
Assert-Scope (($farm.presentationModuleScope.worldInteractionIds -join ',') -ceq 'WI-FARM-04' -and
    $farm.activeWorldInteractionId -ceq 'WI-FARM-01' -and $farm.trackPlans.logic.currentEvidenceStage -ceq 'E3' -and
    $farm.trackPlans.presentation.currentEvidenceStage -ceq 'E1' -and $farm.currentEvidenceStage -ceq 'E1' -and
    -not $farm.promotionEligible -and @($farm.presentationModuleBindings | Where-Object statusCode -eq 'Passed').Count -eq 0 -and
    $farm.presentationModuleScope.purpose.Contains('부모 Farm 전체 E5')) 'PartialEvidenceDoesNotPromoteParent'
Assert-Scope ($gaps.Contains('해당 소비자/근거만 재검토') -and $gaps.Contains('영향 미확인은 미검증') -and
    $farm.connectionPreflightImplementation.automaticPromotionAllowed -eq $false -and
    $farm.connectionPreflightImplementation.resultCode -ceq 'Conditional') 'ChangedContextRequiresScopedRecheck'
$template = Read-Json 'eng/execution-ledgers/work-orders/interaction-e1-e7-validation.template.json'
$oldA = (Get-FileHash -LiteralPath (Join-Path $root 'eng/execution-ledgers/work-orders/actor-item-equipment.e7-work-order.json')).Hash
$oldB = (Get-FileHash -LiteralPath (Join-Path $root 'eng/execution-ledgers/work-orders/nature-tactical-self-navigation.e7-work-order.json')).Hash
Assert-Scope ($template.schemaVersion -ceq $farm.schemaVersion -and
    $null -eq $template.PSObject.Properties['connectionPreflightImplementation'] -and
    $oldA -ceq 'E44926D17843859169671A154A0BAF6AF15CD2B244AD6D66C752DDCA90D2B2BE' -and
    $oldB -ceq '43BD2F0BD59A370A9191615D745C38E445C16E1AB3E367ABFFD0EAF21EE64420') 'OptionalGuidanceAndLegacyBaselinePreserved'
Assert-Scope ($goals.policy.concurrencyModeCode -ceq 'DependencyAndOwnership' -and
    $null -eq $goals.policy.goalWorkInProgressLimit -and $null -eq $goals.policy.worldInteractionWorkInProgressLimit -and
    $goals.principles.independentWorkMayProceedInParallel -and $catalog.modules.Count -eq 18 -and
    $goals.principles.presentationEvidenceE5RequiresLogicE5) 'ApprovalAndOwnershipNotGlobalCount'
Write-Output "PresentationLocalScopeTestsPassed:Cases=$script:cases;StructuralOnly=True;Editor=False;Promotion=False"
