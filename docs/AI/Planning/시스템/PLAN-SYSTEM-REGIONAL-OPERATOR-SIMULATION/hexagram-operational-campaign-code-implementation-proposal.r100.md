# [기획 · 운영·시뮬레이션 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 괘상 운영 캠페인 코드 구현 제안 r100]

- 기준일: 2026-09-17
- 상태: `Draft / CodeImplementationProposalPrepared / ExistingCoreReuseConfirmed / WholeHexagramBindingPreserved / FirstSliceAsked / ImplementationNotAuthorized`
- 의미 기준선: [괘 단위 운영 캠페인 r73](whole-hexagram-operator-campaign.r73.md), [화뢰서합·산화비 검토 r99](shi-he-bi-pair-review.r99.md)
- 구현 기준선: [절기 운영 Campaign 리팩토링 r66](seasonal-campaign-refactoring-plan.r66.md), [Core·RemoteHost r68](seasonal-campaign-core-remote-implementation.r68.md), [Unity 관찰 기반 r69](seasonal-campaign-unity-observation-foundation.r69.md)

## 목적

지금까지 정리한 1~22괘의 의미를 코드에 반영하되, 괘 이름이나 상징이 주문·배차·정산·NPC 행동을 직접 결정하지 않게 한다. 괘상은 승인된 운영 캠페인의 주제·압박·회복 방향을 설명하는 판본화된 참조이고, 실제 상태 전이는 기존 `Simulation절기운영Campaign`의 조건·Preview·Confirm·revision이 소유한다.

이번 판본은 코드 구현 방향만 제시한다. 코드·DB·API·Unity·Scene·Game View·E 단계는 변경하거나 승격하지 않는다.

## 현재 코드에서 재사용할 것

```text
경영SimulationSessionAggregate
├─ SimulationHexagramCampaign
│  └─ 기존 이야기 캠페인과 Save/Replay 호환을 그대로 보존
└─ Simulation절기운영Campaign
   ├─ CampaignStableId·DefinitionRevision·AreaStableId
   ├─ PhaseDefinition·RequiredConditionCodes·RequiredSourceCodes
   ├─ PreviewAdvance → PreviewHash
   ├─ ConfirmAdvance → revision·event
   ├─ Save/Restore/Replay
   └─ LocalProcess / RemoteHost 동등 계약
```

- `SimulationHexagramCampaign`의 `CurrentLineOrdinal`과 `StoryStageCount`는 기존 이야기용 호환 계약이다. 운영 캠페인의 효별 단계로 재사용하지 않는다.
- `Simulation절기운영Campaign`은 이미 행정동 디오라마, 운영 장면, 카드 작업공간, NPC 관찰의 원천 판본을 받을 수 있다.
- Unity의 `SeasonalCampaignObservationCoordinator`와 Interpreter는 상태 사본과 `AvailableActions`를 읽는 기존 조합 계층으로 유지한다.

## 권장 코드 구조

```text
기획 의미 대장
operator-hexagram-campaign-definitions.json
  └─ 1~22괘 의미·관계·승인 상태
          │ 사람 승인 뒤 명시적으로 채택
          ▼
Application 캠페인 Profile
Simulation운영괘상CampaignProfile
  └─ 실제 phase·condition·source·policy 결속
          │ DefinitionSnapshot 생성
          ▼
Simulation Domain 권위
Simulation절기운영Campaign
  └─ Preview·Confirm·revision·Save/Replay
          │ 읽기 전용 StateSnapshot
          ▼
표현 Projection
├─ 운영 앱 카드·허용 조작
└─ Unity 디오라마·NPC·원천 진단
```

### 1. 괘상 의미 대장

새 대장 후보는 `eng/execution-ledgers/operator-hexagram-campaign-definitions.json`이다. 기존 `hexagram-story-production.json`은 스토리 정본이므로 덮어쓰거나 운영 상태를 추가하지 않는다.

각 항목은 다음 정도만 소유한다.

```json
{
  "hexagramStableId": "HEX-21-SHI-HE",
  "ordinal": 21,
  "meaningRevision": "operator-hexagram-meaning.r1",
  "operationalThemeCode": "ObstacleResolution",
  "summary": "증거와 공정한 절차로 운영 장애를 해소한다.",
  "relatedStableIds": ["HEX-22-BI-GRACE"],
  "safetyBoundaryCodes": [
    "EvidenceRequired",
    "ProportionalResponseRequired",
    "AppealPathRequired"
  ],
  "approvalState": "MeaningDraft"
}
```

- 1~16괘의 `내부 형성`, 17괘 이후의 `외부 관계`는 검색·설명용 상위 구간으로 기록할 수 있지만 강제 해금 순서가 아니다.
- 괘의 짝과 선후 관계는 추천 관계일 뿐 Runtime 선행 조건으로 자동 변환하지 않는다.
- 기획 대장의 `MeaningApproved`가 실제 실행 Profile을 자동 생성하지 않는다. 개발 수용과 시험을 별도로 거친다.

### 2. 공유 계약의 최소 확장

`Simulation절기운영CampaignDefinitionSnapshot`에 다음 선택 참조를 추가하는 안을 권장한다.

```csharp
public sealed class Simulation운영괘상Reference
{
    public string HexagramStableId { get; set; } = string.Empty;
    public string MeaningRevision { get; set; } = string.Empty;
    public string OperationalThemeCode { get; set; } = string.Empty;
    public string[] SafetyBoundaryCodes { get; set; } = Array.Empty<string>();
}

public sealed class Simulation절기운영CampaignDefinitionSnapshot
{
    // 기존 필드 유지
    public Simulation운영괘상Reference? HexagramMeaning { get; set; }
}
```

JSON 필드명은 기존 공개 계약 관례에 맞춰 영어로 직렬화한다. 구판 요청에는 `HexagramMeaning=null`을 허용하여 현행 Neutral 절기 캠페인과 호환한다.

이 참조에는 주문률 `+10%` 같은 수치를 넣지 않는다. 실제 수치·기간·비용은 별도 판본의 Campaign Profile 또는 Policy 정의가 소유한다.

### 3. Application Profile과 Definition Factory

새 후보 타입은 다음과 같다.

- `Simulation운영괘상CampaignProfile`: 실제 운영 사건의 phase·조건·원천·허용 정책을 정의한다.
- `Simulation운영괘상CampaignCatalog`: 승인된 Profile만 조회한다.
- `Simulation운영괘상CampaignDefinitionFactory`: Profile을 자기 완결적인 `DefinitionSnapshot`으로 동결한다.

1~22괘를 22개의 `switch` 분기로 Domain에 넣지 않는다. Domain은 괘 이름을 해석하지 않고 동결된 phase·condition·source와 안전 경계의 형식만 검증한다.

### 4. Domain 검증과 hash

`Simulation절기운영Campaign`에는 다음 검증만 추가한다.

- `HexagramStableId`, `MeaningRevision`, `OperationalThemeCode`의 공백·길이·중복 검사.
- 안전 경계 코드의 정렬·중복 제거와 clone.
- 시작 Command signature, Preview hash, Save/Replay hash에 괘상 참조 판본 포함.
- 실행 도중 `MeaningRevision`을 바꾸면 새 Campaign 시작 또는 명시적 migration 없이는 거절.
- 괘상 참조가 달라도 주문·배차·정산 OS의 상태를 직접 변경하지 않음.

### 5. API와 허용 조작

조회 후보는 다음처럼 분리한다.

```text
GET  /api/v1/simulation/operator-hexagram-campaigns
GET  /api/v1/simulation/operator-hexagram-campaigns/{hexagramStableId}

기존 session route
GET  /api/v1/simulation/sessions/{sessionStableId}/seasonal-operations-campaign
POST .../preview-advance
POST .../confirm-advance
```

첫 두 API는 승인된 정의·설명 조회이고, 실제 진행은 기존 Session route가 담당한다. 앱과 Unity 버튼은 서버가 반환한 `AvailableActions`에 있는 조작만 활성화한다.

### 6. 앱과 Unity Projection

Unity에는 괘별 상태 기계를 다시 만들지 않는다. 기존 Interpreter가 다음 읽기 전용 화면 모델을 만들도록 확장한다.

```text
운영괘상CampaignScreenModel
├─ 괘 이름·번호·의미 판본
├─ 현재 운영 국면과 경과 시간
├─ 관측된 원인·자료 품질·차단 사유
├─ 허용 조작
├─ 비용·위험·회복 예상
└─ 원천별 revision·마지막 정상 상태
```

- 앱은 업무 카드와 상세 근거를 표시한다.
- Unity는 같은 revision을 건물·NPC·권역 오버레이·카드에 결속한다.
- 색상·괘상·아르카나·연출은 Presentation hint이며 Campaign 결과나 업무 권위를 변경하지 않는다.
- 원천 지연 때는 마지막 정상 화면을 유지하되 조작을 비활성화하고 지연 원천을 표시한다.

## 제21괘·제22괘 코드 투영 예시

### 화뢰서합 Profile 후보

```text
campaign:operator:shi-he-obstacle-resolution.v1
장애 감지
→ 증거·영향 범위 확인
→ 해결안 Preview
→ 운영자 Confirm
→ 회복 상태 관찰
→ 종료 또는 재개방
```

필수 조건 후보는 `ObstacleClassified`, `MinimumEvidenceSatisfied`, `AffectedCasesIsolated`, `RecoveryPathPrepared`다. 증거가 부족하면 `CanConfirm=false`와 명시적 `BlockReasonCodes`를 반환한다. 이는 자동 처벌이나 책임 확정이 아니다.

### 산화비 Profile 후보

```text
campaign:operator:bi-readable-presentation.v1
표현할 실질 revision 동결
→ 앱·Unity Projection 준비
→ 누락·오해 가능성 검사
→ 읽기 전용 표현 활성
→ 이해 가능성·원천 일치 확인
```

산화비의 성공은 화면이 화려해지는 것이 아니라, 같은 권위 revision의 원인·상태·다음 행동을 사용자가 구분할 수 있는 것이다. 이 Campaign이 완료돼도 주문·배차·정산 상태는 변하지 않는다.

두 Profile은 의미상 이어질 수 있지만 Runtime에서 서로를 강제 선행 조건으로 두지 않는다. 실제 운영 사건이 독립적이면 각각 시작할 수 있다.

## 구현 순서

1. **계약·검사기**: 선택형 괘상 참조, clone·정렬·hash·호환 시험.
2. **첫 Profile 결속**: 이미 운영 사건과 성공 기준이 가장 구체적인 `HEX-03-ZHUN` 사가정 점심 피크에 의미 참조를 연결한다.
3. **저장·동등성**: Save/Restore/Replay와 LocalProcess/RemoteHost 동일 결과를 검증한다.
4. **API·Unity 표시**: 괘 이름·의미 판본·차단 사유·허용 조작을 읽기 전용으로 표시한다.
5. **21괘 Profile**: 장애 분류·증거·격리·회복 경로가 확정된 뒤 화뢰서합을 구현한다.
6. **22괘 Profile**: 같은 revision의 앱·Unity 판독 기준을 확정한 뒤 산화비를 구현한다.
7. **1~22 확장**: 승인된 운영 사건이 생길 때만 Profile을 하나씩 추가한다. 의미 대장만 있는 괘는 실행 불가 상태를 유지한다.

## 시험 묶음

- 기존 `SimulationHexagramCampaign`의 계약·Save/Replay hash가 변하지 않는다.
- 효 개수나 `CurrentLineOrdinal` 없이 괘 전체 참조로 운영 Campaign이 진행된다.
- 같은 definition·meaning·source·policy revision은 같은 Preview hash를 만든다.
- 의미 판본 하나라도 다르면 오래된 Preview Confirm을 거절한다.
- `AvailableActions`에 없는 버튼은 앱과 Unity에서 실행할 수 없다.
- 21괘는 증거·격리·회복 조건이 없으면 Confirm이 차단된다.
- 22괘 Presentation 설정 변경이 OS 원장·Campaign 결과 hash를 임의로 바꾸지 않는다.
- Save/Restore/Replay와 LocalProcess/RemoteHost가 같은 상태·행위 목록을 반환한다.
- 원천 지연·미확정 행정동 위치·낮은 자료 판본을 정상 값으로 추정하지 않는다.

## 제외

- 1~22괘에 대한 22개 실행 Profile 일괄 구현.
- 괘 번호 순서의 강제 플레이·자동 해금.
- 효사별 API·상태 전이·WI 자동 생성.
- 괘상만으로 주문률·배달 완료율·수익을 보정하는 공식.
- 실제 운영 주문·배차·결제·정산 변경.
- Unity Scene 저장·Play Mode·Game View·배포.

## 다음 질문 하나

첫 구현 slice를 `선택형 괘상 참조 계약 + 의미 대장 검사기 + 기존 수뢰둔 점심 피크 Profile 결속 + Save/Replay·Local/Remote 회귀`까지로 닫을까?
