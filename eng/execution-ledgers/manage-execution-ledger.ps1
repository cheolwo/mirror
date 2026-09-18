[CmdletBinding()]
param(
    [ValidateSet("Write", "Check")]
    [string] $Mode = "Check",
    [string] $InputPath = "eng/execution-ledgers/simulation-unity.json",
    [string] $OutputPath = "docs/AI/generated/simulation-unity-execution-tree.md"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "../common/deterministic-text-output.ps1")

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "ExecutionLedgerInvalid:$Code" }
}

function Get-StageIndex([object[]] $Stages, [string] $Code) {
    for ($index = 0; $index -lt $Stages.Count; $index++) {
        if ([string] $Stages[$index].code -eq $Code) { return $index }
    }
    return -1
}

function Test-DependencyCycle(
    [string] $Id,
    [hashtable] $ItemsById,
    [hashtable] $Visiting,
    [hashtable] $Visited) {
    if ($Visited.ContainsKey($Id)) { return }
    if ($Visiting.ContainsKey($Id)) { throw "ExecutionLedgerInvalid:DependencyCycle:$Id" }
    $Visiting[$Id] = $true
    foreach ($dependency in @($ItemsById[$Id].dependsOn)) {
        Test-DependencyCycle ([string] $dependency) $ItemsById $Visiting $Visited
    }
    $Visiting.Remove($Id)
    $Visited[$Id] = $true
}

function Escape-Markdown([string] $Value) {
    if ($null -eq $Value) { return "" }
    return $Value.Replace("|", "\|").Replace("`r", " ").Replace("`n", " ")
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$resolvedInput = (Resolve-Path (Join-Path $repositoryRoot $InputPath)).Path
$ledger = Get-Content -LiteralPath $resolvedInput -Raw -Encoding UTF8 | ConvertFrom-Json
$resolvedStageCatalog = (Resolve-Path (Join-Path $repositoryRoot ([string] $ledger.evidenceStageCatalogPath))).Path
$stageCatalog = Get-Content -LiteralPath $resolvedStageCatalog -Raw -Encoding UTF8 | ConvertFrom-Json
$evidenceStages = @($stageCatalog.stages)

Require (-not [string]::IsNullOrWhiteSpace($ledger.ledgerKey)) "LedgerKeyMissing"
Require (-not [string]::IsNullOrWhiteSpace($ledger.revision)) "RevisionMissing"
Require (-not [string]::IsNullOrWhiteSpace($ledger.evidenceStageCatalogPath)) "EvidenceStageCatalogPathMissing"
Require ([string] $stageCatalog.schemaVersion -eq "simulation-evidence-stages.v8") "EvidenceStageCatalogSchemaInvalid"
Require ($evidenceStages.Count -eq 11) "EvidenceStagesMustHaveElevenEntries"
Require ((@($evidenceStages.code) -join ",") -eq
    "E0,E1,E2,E3,E4,E5,E6,E7,E8,E9,E10") "EvidenceStageOrderInvalid"
Require (@($ledger.items).Count -gt 0) "ItemsMissing"

$allowedStatuses = @("NotStarted", "InProgress", "Blocked", "Done", "Superseded")
$itemsById = @{}
foreach ($item in @($ledger.items)) {
    $id = [string] $item.id
    Require (-not [string]::IsNullOrWhiteSpace($id)) "ItemIdMissing"
    Require (-not $itemsById.ContainsKey($id)) "DuplicateItemId:$id"
    Require ($allowedStatuses -contains [string] $item.status) "UnknownStatus:$id"
    Require ((Get-StageIndex $evidenceStages ([string] $item.currentEvidenceStage)) -ge 0) "UnknownCurrentStage:$id"
    Require ((Get-StageIndex $evidenceStages ([string] $item.targetEvidenceStage)) -ge 0) "UnknownTargetStage:$id"
    Require (((Get-StageIndex $evidenceStages ([string] $item.currentEvidenceStage)) -le
        (Get-StageIndex $evidenceStages ([string] $item.targetEvidenceStage)))) "StageExceedsTarget:$id"
    Require ([string] $item.targetEvidenceStage -eq "E7") "TargetEvidenceStageMustBeE7:$id"
    Require (@($item.sourceReferences).Count -gt 0) "SourceReferenceMissing:$id"
    Require (-not [string]::IsNullOrWhiteSpace([string] $item.doneWhen)) "DoneWhenMissing:$id"
    if ([string] $item.status -notin @("Done", "Superseded")) {
        Require (-not [string]::IsNullOrWhiteSpace([string] $item.nextAction)) "NextActionMissing:$id"
    }
    if ([string] $item.status -eq "Blocked") {
        Require (@($item.blockers).Count -gt 0) "BlockedReasonMissing:$id"
    }
    if ([string] $item.status -eq "Done") {
        Require ([string] $item.currentEvidenceStage -eq [string] $item.targetEvidenceStage) "DoneWithoutTargetEvidence:$id"
        Require (@($item.evidence).Count -gt 0) "DoneEvidenceMissing:$id"
    }
    foreach ($reference in @($item.sourceReferences)) {
        Require (Test-Path -LiteralPath (Join-Path $repositoryRoot ([string] $reference))) "SourceReferenceNotFound:${id}:$reference"
    }
    $itemsById[$id] = $item
}

foreach ($item in @($ledger.items)) {
    foreach ($dependency in @($item.dependsOn)) {
        Require ($itemsById.ContainsKey([string] $dependency)) "DependencyNotFound:$($item.id):$dependency"
        Require ([string] $dependency -ne [string] $item.id) "SelfDependency:$($item.id)"
    }
}

$visiting = @{}
$visited = @{}
foreach ($id in $itemsById.Keys) {
    Test-DependencyCycle ([string] $id) $itemsById $visiting $visited
}
Require ($itemsById.ContainsKey([string] $ledger.firstExecutionTrackId) -or
    [string] $ledger.firstExecutionTrackId -eq "TRACK-DAEGWALLYEONG-L2-REAL-DATA") "FirstExecutionTrackMissing"

$statusLabels = @{
    NotStarted = "미착수"
    InProgress = "진행 중"
    Blocked = "차단"
    Done = "완료"
    Superseded = "대체됨"
}
$stageLabels = @{}
foreach ($stage in $evidenceStages) { $stageLabels[[string] $stage.code] = [string] $stage.label }

$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine("# Simulation·Unity 미완료 실행 트리")
[void] $builder.AppendLine()
[void] $builder.AppendLine("> 이 문서는 ``$InputPath``에서 자동 생성된다. 직접 수정하지 않는다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- 원장 개정: ``$($ledger.revision)``")
[void] $builder.AppendLine("- 증거 단계 개정: ``$($stageCatalog.revision)``")
[void] $builder.AppendLine("- 마지막 확인일: ``$($ledger.lastVerifiedDate)``")
[void] $builder.AppendLine("- 첫 실행축: ``$($ledger.firstExecutionTrackId)``")
[void] $builder.AppendLine("- 중심 타일: ``$($ledger.firstExecutionTileKey)``")
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 상태 요약")
[void] $builder.AppendLine()
[void] $builder.AppendLine("| 상태 | 수 |")
[void] $builder.AppendLine("| --- | ---: |")
foreach ($status in $allowedStatuses) {
    $count = @($ledger.items | Where-Object status -eq $status).Count
    [void] $builder.AppendLine("| $($statusLabels[$status]) | $count |")
}
[void] $builder.AppendLine()
[void] $builder.AppendLine("## 의존 실행 트리")
[void] $builder.AppendLine()
[void] $builder.AppendLine('```text')
[void] $builder.AppendLine("대관령 L2 E7 실제 플레이 종단 완결")
[void] $builder.AppendLine("├─ 공간 원자료: GEO-LEGAL-01 → GEO-DEM-01 → GEO-LANDCOVER-01")
[void] $builder.AppendLine("├─ 건물 관계: GEO-LEGAL-01 → DATA-BUILDING-01")
[void] $builder.AppendLine("├─ 파생 DB: DB-REGION-SUMMARY-01")
[void] $builder.AppendLine("├─ 공간 산출물: ART-TILE-01")
[void] $builder.AppendLine("├─ 서버 전송: API-STREAM-01")
[void] $builder.AppendLine("└─ Unity 실제 타일: UNITY-REAL-TILE-01")
[void] $builder.AppendLine("   ├─ live Simulation: SIM-LIVE-HTTP-01")
[void] $builder.AppendLine("   ├─ URP 표현: RENDER-URP-01 → PERF-HLOD-01")
[void] $builder.AppendLine("   ├─ 영속화: PERSIST-SIM-01")
[void] $builder.AppendLine("   ├─ 역할 UI: UI-FIGMA-01")
[void] $builder.AppendLine("   └─ 전국 확장: EXPAND-NATIONWIDE-01")
[void] $builder.AppendLine('```')

foreach ($category in @($ledger.items | Sort-Object priority, id | Group-Object category)) {
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("## $($category.Name)")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("| ID | 상태 | 현재→목표 | 다음 실행 |")
    [void] $builder.AppendLine("| --- | --- | --- | --- |")
    foreach ($item in @($category.Group | Sort-Object priority, id)) {
        $stage = "``$($item.currentEvidenceStage) $($stageLabels[[string] $item.currentEvidenceStage])`` → ``$($item.targetEvidenceStage) $($stageLabels[[string] $item.targetEvidenceStage])``"
        [void] $builder.AppendLine("| ``$($item.id)`` $([string] $item.title) | $($statusLabels[[string] $item.status]) | $stage | $(Escape-Markdown ([string] $item.nextAction)) |")
    }
}

[void] $builder.AppendLine()
[void] $builder.AppendLine("## 승격 규칙")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- 계획 문구나 코드 존재만으로 완료 처리하지 않는다.")
[void] $builder.AppendLine("- E4는 WI 공간 모판, E5는 권위 경관 조립, E6는 AreaSet 플레이 전 정제·필요 근거 결속, E7은 실제 플레이 폐루프다. GIS 결속은 E6 안의 독립 선택 축이다.")
[void] $builder.AppendLine("- DEM·도로는 공통 필수 자료가 아니다. 선택한 현실 결속 프로필이 요구할 때만 E6 준비도와 완료 판정에 참여한다.")
[void] $builder.AppendLine("- 실제 DB 적용, HTTP 왕복, Play Mode, Game View, commit과 push는 서로 다른 증거다.")
[void] $builder.AppendLine("- ``Done``은 목표 증거 단계와 검증 자료가 모두 있을 때만 허용한다.")
[void] $builder.AppendLine("- 원자료가 부족하면 Fixture로 숨기지 않고 ``Blocked`` 또는 ``InProgress``와 차단 사유를 유지한다.")

$expected = ConvertTo-DeterministicText $builder.ToString()
$resolvedOutput = Join-Path $repositoryRoot $OutputPath
if ($Mode -eq "Write") {
    Write-DeterministicTextIfChanged $resolvedOutput $expected | Out-Null
    Write-Output "ExecutionLedgerGenerated:$OutputPath"
}
else {
    Require (Test-Path -LiteralPath $resolvedOutput) "GeneratedDocumentMissing:$OutputPath"
    $actual = ConvertTo-DeterministicText ([IO.File]::ReadAllText($resolvedOutput))
    Require ($actual -eq $expected) "GeneratedDocumentOutOfDate:$OutputPath"
    Write-Output "ExecutionLedgerValid:$($ledger.items.Count)"
}
