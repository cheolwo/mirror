# [기획 · 개발 인계 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 절기 운영 Campaign 리팩토링 계획 r66]

- 기준일: 2026-09-17
- 상태: `Approved / RefactoringPlanPrepared / ExistingFlowsFirst / IncrementalSlicesRequired / FirstSliceAuthorized`
- 선행 기획: [Core 우선 r63](seasonal-campaign-core-first-plan.r63.md), [Unity Coordinator r64](unity-seasonal-campaign-observation-coordinator.r64.md), [원천별 마지막 정상 상태 r65](unity-source-failure-retention-policy.r65.md)
- 기존 통합 인계: [r61](development-handoff.r61.md)

## 목표

현재의 업무 Workflow Core, Simulation Runtime, 주역 Campaign, 행정동 디오라마와 Unity 읽기 경로를 보존하면서 다음 구조를 단계적으로 추가한다.

## 승인 근거

- 2026-09-17 사용자가 인계용 계획을 이 스레드에서 직접 구현하도록 요청했다.
- 첫 구현 묶음은 이 계획의 추천 상한인 `공유 계약 + Domain 전이 + 집중 시험`으로 시작한다.
- 후속 Runtime·Save/Replay·RemoteHost·Unity 결속은 앞 단계 검증이 닫힌 뒤 같은 계획에서 이어간다.

```text
Simulation Runtime Core
├─ Business Workflow Runtime                 기존 유지
├─ SimulationHexagramCampaign                기존 유지
└─ Simulation절기운영Campaign                 신규
   ├─ phase·decision·observation·outcome
   ├─ Preview·Confirm·revision·idempotency
   └─ Save/Replay·Local/Remote parity
                    ↓ Contracts only
Unity
└─ SeasonalCampaignObservationCoordinator    신규 조합 계층
   ├─ 기존 행정동 Diorama
   ├─ 기존 운영 Scene/OS Observation
   ├─ 기존 CardWorkspace
   ├─ 기존 NPC·음식배달 Presenter
   └─ 원천별 last-known-good·진단
```

## 리팩토링 원칙

1. 범용 만능 Campaign base class를 먼저 만들지 않는다.
2. 기존 `SimulationHexagramCampaign` 계약·저장 형식·route를 이름 변경하거나 이주시킬 필요가 없다.
3. 절기 운영 Campaign은 독립 계약과 독립 상태를 가지되 Session·revision·Save/Replay 기반을 재사용한다.
4. Unity는 `Ssalddel.Simulation.Contracts`와 `Ssalddel.WorkflowRules.Contracts`만 소비한다.
5. Unity가 Domain·Application·Persistence를 참조하거나 phase·업무 완료를 자체 판정하지 않는다.
6. 기존 행정동 디오라마, 운영 상태와 GameObject를 복제하지 않는다.
7. 단계마다 기존 시험을 통과시킨 뒤 다음 경계를 연다. 한 번에 Core·API·Unity·Scene을 모두 변경하지 않는다.

## 0단계 — 기준선 동결과 작업 명세

### 수행

- branch·dirty worktree와 파일 소유를 재확인한다.
- r63~r66의 revision/hash를 결속한 WI 하나의 E1~E7 상호작용 수직 검증 명세를 만든다.
- 첫 WI는 `운영자가 절기 운영 Campaign의 현재 구간과 마감 가능 상태를 읽고, Preview 뒤 Confirm한다`로 제한한다.
- r62 점심 피크 자료는 합성 Fixture이며 실제 주문·기사·사업장·금액이 아님을 동결한다.
- 기존 Hexagram Campaign, 음식배달 Workflow와 Unity 관찰 시험을 기준선으로 실행한다.

### 중단

- 기존 Session Save 계약에 독립 확장 상태를 추가할 수 없고 Hexagram 필드를 재사용해야만 하는 경우.
- 같은 작업 파일을 다른 활성 작업이 수정 중이라 소유 범위를 분리할 수 없는 경우.
- 기획 revision/hash 또는 WI의 직접 결과·실패·회복·귀환이 결속되지 않은 경우.

## 1단계 — 절기 운영 Campaign 공유 계약

### 예상 경로

- `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/Simulation절기운영CampaignContracts.cs`
- 같은 경로의 `.meta`
- `Ssalddel.Simulation.Tests/Simulation절기운영CampaignContractTests.cs`

### 계약 후보

- `Simulation절기운영CampaignDefinitionSnapshot`
- `Simulation절기운영CampaignStateSnapshot`
- `Simulation절기운영CampaignPhaseSnapshot`
- `Simulation절기운영CampaignDecisionPreview`
- `Simulation절기운영CampaignObservationSnapshot`
- `Simulation절기운영CampaignOutcomeSnapshot`
- `Simulation절기운영CampaignEvidenceReference`
- `Simulation절기운영CampaignAdvancePreviewRequest`
- `Simulation절기운영CampaignAdvanceConfirmRequest`
- `ISimulation절기운영CampaignRuntime`

외부 JSON 필드는 기존 계약 관례를 따라 영어 stable field를 사용하고 schemaVersion·ruleRevision·sessionStableId·campaignStableId·revision·availableActions를 필수로 둔다.

### 완료 조건

- phase 코드와 허용 전이 후보가 명시된다.
- Preview와 Confirm 계약이 분리된다.
- 조작별 필요한 원천·최소 revision을 담을 확장 자리가 있다.
- Unity가 참조할 수 있는 netstandard·engine-independent 계약이다.
- 아직 Domain 전이·API·Unity 화면은 없다.

## 2단계 — Domain 상태 전이와 결정성

### 예상 경로

- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation절기운영Campaign.cs`
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/경영SimulationSession.cs` 또는 현행 Aggregate의 최소 partial 확장
- `Ssalddel.Simulation.Tests/Simulation절기운영CampaignTests.cs`

### 수행

- Campaign 시작, 현재 구간 조회, 다음 구간 Preview, Confirm, 회복, 결산의 최소 전이를 구현한다.
- expected revision과 Command ID의 멱등성을 검사한다.
- Preview는 Aggregate를 변경하지 않는다.
- 같은 seed·definition revision·입력 snapshot이면 같은 Preview hash를 만든다.
- 무입력 안전 기본값을 명시하되 정확 주문·기사·금액 수치는 Fixture Profile 밖에서 만들지 않는다.
- 주역 Campaign과 동시에 존재할 수 있지만 서로의 상태를 읽거나 변경하지 않는다.

### 완료 조건

- 정상 전이, 잘못된 phase, 낮은 revision, 중복 Command, Preview 불변 시험이 통과한다.
- Logic 예상 상한은 집중 자동 시험 기준 E3이며 실제 E 승격은 대장 판정에 따른다.

## 3단계 — Application·Local Runtime 결속

### 예상 경로

- `Ssalddel.Simulation.Application/RuntimeCore/Simulation절기운영CampaignService.cs`
- `Ssalddel.Simulation.Application/RuntimeCore/SimulationRuntime.cs`
- `Ssalddel.Simulation.Application/RuntimeCore/LocalSimulationRuntime.cs`
- `Ssalddel.Simulation.Tests/LocalSimulationRuntimeTests.cs`

### 수행

- Domain Aggregate 접근과 Preview/Confirm UseCase를 Service로 감싼다.
- `ISimulationRuntime`과 `LocalSimulationRuntime`에 절기 운영 Campaign 포트를 추가한다.
- 업무 Workflow 상태는 읽기 입력으로만 받고 음식배달 단계를 Campaign에서 직접 변경하지 않는다.
- r62 합성 점심 피크 Fixture를 별도 builder/profile로 주입한다.

### 완료 조건

- ASP.NET·HTTP 없이 LocalProcess에서 같은 Campaign 폐루프를 실행한다.
- 실제 FCM·GPS·PG·운영 DB Adapter 호출이 없다.

## 4단계 — Save/Restore/Replay 확장

### 예상 경로

- `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationSaveReplayContracts.cs`
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationSaveReplay.cs`
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationSaveReplayCloner.cs`
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationReplayHasher.cs`
- 관련 Simulation 시험

### 수행

- 기존 Hexagram 필드와 별도의 `SeasonalOperationsCampaign` payload를 추가한다.
- schema 호환을 위해 구판 Save에는 nullable/neutral 기본을 적용한다.
- 현재 phase·decision·modifier snapshot·관측 참조·outcome·이월 상태를 저장한다.
- Replay는 외부 공공 API와 운영 API를 재호출하지 않는다.

### 완료 조건

- 구판 Save 복원, 신규 Save round-trip, 같은 replay hash와 변조 거절이 통과한다.
- 기존 Hexagram Campaign Save/Replay 시험이 그대로 통과한다.

## 5단계 — RemoteHost API와 Local/Remote 동등성

### 예상 경로

- `Ssalddel.Simulation.Hosting/Controllers/Simulation절기운영CampaignController.cs`
- Hosting 등록 지점과 관련 API 시험

### route 후보

```text
GET  api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign
POST api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign/advance-previews
POST api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign/advance-commands
```

### 수행

- Controller는 Application Service에 위임하고 자체 규칙을 갖지 않는다.
- expected revision·Command ID·인증/Session 접근 실패를 기존 오류 계약으로 전달한다.
- TestServer와 Local Runtime이 같은 입력에서 같은 상태·Preview hash를 반환하는지 검증한다.

### 중단

- route를 위해 운영 서버의 실제 주문·배차 API를 호출해야 하는 경우.
- Simulation과 Operational 실행 모드 경계가 불명확한 경우.

## 6단계 — Unity Campaign 읽기 수신부

### 예상 경로

- `Ssalddel.Unity/Runtime/Campaigns/Simulation절기운영CampaignModels.cs`
- `Ssalddel.Unity/Runtime/Campaigns/Simulation절기운영CampaignInterpreter.cs`
- `Ssalddel.Unity/Runtime/Campaigns/Simulation절기운영CampaignClient.cs`
- `Ssalddel.Unity/Runtime/OperationalTransport/UnityJsonSimulation절기운영CampaignDecoder.cs`
- `Ssalddel.Unity.Tests/Simulation절기운영CampaignInterpreterTests.cs`

### 재사용

- `OperationalWorldSceneClient`의 취소·전송·Decoder 경계.
- `AdministrativeDongDioramaInterpreter`의 schema·hash·revision 거절 방식.
- `StableIdReconciler`와 `LastSuccessfulLoadRuntime`.

### 완료 조건

- 낮은 revision, 잘못된 schema, 다른 Session·지역 조합을 거절한다.
- 마지막 정상 Campaign 상태를 메모리에 유지한다.
- Unity는 phase와 available action을 다시 계산하지 않는다.
- 아직 Scene·GameObject·실제 입력은 없다.

## 7단계 — `SeasonalCampaignObservationCoordinator`

### 예상 경로

- `Ssalddel.Unity/Runtime/Campaigns/SeasonalCampaignObservationCoordinator.cs`
- `Ssalddel.Unity/Runtime/Campaigns/SeasonalCampaignScreenModels.cs`
- `Ssalddel.Unity.Tests/SeasonalCampaignObservationCoordinatorTests.cs`

### 입력

- `AdministrativeDongDioramaInterpreter`
- `OperationalOsWorldObservationSession`
- Campaign Interpreter
- `CardWorkspaceCoordinator`
- NPC·음식배달 관찰 모델

### 수행

- 원천별 상태를 `CompositionRevisionSet`으로 조합한다.
- 원천 하나가 실패하면 해당 원천의 마지막 정상 상태와 진단을 유지한다.
- 실패 원천을 객체 삭제·업무 종료·phase 전환으로 해석하지 않는다.
- 지역 전환·세션 종료 때 이전 문맥과 선택 상태를 정리한다.
- 조작별 원천 의존성을 읽어 필요한 조작만 비활성화한다. 이 세부 계약은 사용자 승인 전 후보로 둔다.

### 완료 조건

- 행정동·운영 상태·카드·NPC 원천별 실패/복구 조합 시험.
- 전체 화면 공백, 중복 stable ID와 이전 지역 상태 누출 방지.
- Presentation 예상 상한은 메모리 화면 모델 기준 E4이며 실제 World 배치 E5를 선언하지 않는다.

## 8단계 — Presenter와 기존 World 결속

### 예상 경로

- `Ssalddel.Unity/Runtime/Campaigns/SeasonalCampaignPresenter.cs`
- 기존 음식배달·동네 관찰 Presenter의 최소 확장
- 제품 Unity 저장소의 기존 `SimulationWorldShell` View Socket. 정확 경로는 구현 수용 시 재확인한다.

### 수행

- 상단 운영 지표, 오른쪽 원인·정책 카드, 선택 상세, 마감 Preview를 ScreenModel로 표현한다.
- `CardWorkspaceCoordinator`에 절기·아르카나 Family source를 얇게 연결한다.
- Actor·건물은 기존 stable ID 객체를 강조할 뿐 복제하지 않는다.
- 실제 버튼은 Core가 제공한 `AvailableActions`만 노출한다.

### 완료 조건

- 자동 Unity 소비자 컴파일·EditMode 시험과 실제 Play Mode·Game View를 분리한다.
- 실제 View 증거에는 전체 조망, 갱신 지연, 정책 Preview, 회복·결산 화면과 Console 기록이 필요하다.
- Scene 저장·Prefab 배선·캡처는 월드·공간·배치 담당의 별도 수용 전 수행하지 않는다.

## 9단계 — 첫 수직 절편 통합

첫 통합 절편은 하나만 사용한다.

```text
사가정역·면목제3·8동
→ 합성 정상 음식배달 상태
→ 예상보다 이른 점심 주문 증가
→ 가용 기사 부족
→ 기사 가용 의향 알림 Preview
→ 무입력 기본값 또는 Confirm
→ 회복
→ 결산
```

- 검증된 이동 graph가 없는 구간은 `이동 중`으로 표현한다.
- 실제 상호명·개인·GPS·결제·배차·정산을 사용하지 않는다.
- 정확 시간·수량·금액은 승인된 Fixture Profile에서만 사용한다.

## 단계별 검증 묶음

| 단계 | 필수 검증 | 회귀 |
| --- | --- | --- |
| 1~2 | 계약 validator, Domain 전이·멱등·결정성 | Hexagram Campaign, 음식배달 Domain |
| 3 | Local Runtime 실행 | 기존 Session·Workflow Runtime |
| 4 | Save/Restore/Replay·hash | 기존 모든 Save schema·Hexagram |
| 5 | TestServer·Local/Remote parity | Hosting route·인증·오류 계약 |
| 6 | Decoder·Interpreter·last-known-good | 행정동·운영 Scene Unity 시험 |
| 7 | 원천 조합·부분 실패·지역 전환 | CardWorkspace·NPC·선택 저장 |
| 8~9 | 소비자 컴파일·EditMode·실제 Play Mode/Game View | `SimulationWorldShell`과 음식배달 관찰 |

각 작업 뒤 `eng/validate-changes.ps1 -Level Fast -Paths <이번 파일>`을 실행하고, 절편 완료 전 `-Level Task`를 실행한다. 자동 시험 성공은 실제 Scene·Game View 증거를 대신하지 않는다.

## 맥락별 커밋 후보

1. `feat(simulation): 절기 운영 캠페인 계약과 도메인 뼈대`
2. `feat(simulation): 절기 캠페인 로컬 런타임과 저장 재생`
3. `feat(simulation-host): 절기 캠페인 원격 어댑터`
4. `feat(unity-data): 절기 캠페인 읽기 해석기와 클라이언트`
5. `feat(unity): 절기 캠페인 관찰 조정자와 화면 모델`
6. `feat(unity-presentation): 사가정 피크 관찰 화면 결속`
7. `docs(planning): 절기 캠페인 구현 증거와 현재 상태`

커밋은 각 단계가 검증된 뒤 사용자가 요청할 때만 수행하고, push는 별도 요청 전 수행하지 않는다.

## 전체 중단 조건

- 실제 운영 Command나 실제 기사 알림을 Simulation Fixture로 가장해야 하는 경우.
- Unity가 Campaign phase·업무 완료·재무 결과의 최종 권위를 가져야 하는 구조.
- 기존 공개 계약·Save·stable ID를 호환 없이 변경해야 하는 경우.
- 행정동·운영 상태·Campaign revision 불일치를 숨기기 위해 임의 병합이 필요한 경우.
- 실제 Game View가 없는데 Presentation E5 이상을 요구하는 경우.

## 다음 질문 하나

첫 구현 묶음은 어디까지 닫을까?

1. `공유 계약 + Domain 전이 + 집중 시험` — 추천. 가장 작은 권위 단위를 먼저 안정화하고 Runtime·API·Unity를 후속 커밋으로 분리한다.
2. `Local Runtime·Save/Replay까지 한 번에` — 실행 가능성은 빨리 보이지만 변경 범위와 회귀 위험이 커진다.
3. `Unity ScreenModel까지 한 번에` — 화면 연결은 빠르지만 Core 계약이 바뀌면 Unity 재작업이 커진다.
