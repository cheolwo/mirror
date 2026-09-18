[기획 · 시스템·업무 생명주기 · PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS · r8]

# OS 생명주기 Core와 환경별 Adapter 분리 제안

- 상태: `Proposed / CoreTransitionAndPureGuardScopeConfirmed / ThreeStateDecisionContractConfirmed / AvailableActionsInlineConfirmed / FoodDeliveryAvailableActionsFirstSliceImplemented / LifecycleFailureRecoveryFirstConfirmed / SharedFailureContractFirstSliceImplemented / OperatorSafeRetryOnlyConfirmed / FullCoreAdapterRefactorNotApproved`
- 제안 근거: 2026-09-14 사용자는 음식배달·화물·창고·마트 등 OS 생명주기를 서버 업무, 모바일·Web 업무 화면, Unity NPC 공간 행동에서 함께 사용하되, 변동이 적은 Core와 환경별 조정 부분을 분리하는 리팩터링 제안서를 요청했다.
- r2 확정 근거: 2026-09-14 사용자는 OS Core가 단계 ID·순서·설명뿐 아니라 허용 전이와 환경 비의존 순수 guard까지 포함하는 추천안을 선택했다.
- r3 확정 근거: 2026-09-14 사용자는 Core 전이 판정을 `AllowedByCore / BlockedByCore / RequiresEnvironmentValidation` 세 상태와 안정 이유 코드로 구분하는 추천안을 선택했다.
- r4 보류 근거: 2026-09-14 사용자는 `AvailableActions` 전달 형태 판단을 보류했다.
- r5 확정 근거: 2026-09-14 사용자는 현재 상태 사본에 누를 수 있는 작은 행동을 포함해 버튼을 통제하고 오류 가능성을 줄이는 방향과 그 첫 구현을 승인했다.
- r6 확정 근거: 2026-09-17 사용자는 실제 휴대폰 연결을 기다리는 동안 기능 추가보다 OS 생명주기에서 오류가 발생했을 때의 조치를 명확히 하고 같은 기준으로 앱 검증을 계속하는 방향을 선택했다.
- r7 확정 근거: 2026-09-17 사용자는 최종 실패에 대한 첫 모바일 관리자 공통 조작을 안전 재시도 예약 하나로 제한하는 추천안을 선택했다.
- r8 구현 근거: 2026-09-17 사용자는 음식배달 OS의 판본 충돌·일시 통신 장애·최종 후속처리 실패 세 사례를 첫 세로 절편으로 구현하는 1안을 선택했다.
- 관련 기획: [다중 OS 생명주기 재생 r4](../PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/multi-os-lifecycle-playback.r4.md), [역세권 디오라마 모듈 표준 r16](../PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/README.md), [운영 서버에서 Unity로의 이관](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)
- 기준 아키텍처: [업무 실행 책임 모델](../../../../Architecture/BusinessWorkflowResponsibilityModel.md), [운영·Simulation·Unity 작업 흐름 분리](../../../../Architecture/OperationsSimulationUnity작업흐름분리.md)

## 문제 정의

현재 `OperatingSystemLifecycleCatalog`는 OS 안정 식별자와 단계 순서·이름·책임을 공통 계약으로 제공한다. 음식배달·화물·창고·마트의 서버 검증 Runner와 Unity 표현도 같은 단계 ID를 사용한다. 그러나 다음 책임이 아직 명시적으로 분리된 하나의 구조로 닫혀 있지는 않다.

- 어떤 전이가 OS 자체의 불변 규칙인지
- 운영 서버와 게임 Simulation이 각자 어떤 권위 상태를 소유하는지
- Web·모바일이 어떤 행동을 요청하고 어떤 상태를 재조회하는지
- Unity NPC가 같은 상태를 어떤 H1·H2·H3 공간 행동으로 표현하는지
- 시간, 권한, 저장, 재시도, 외부 연동과 화면 표현 중 무엇이 환경별 차이인지

그 결과 단계 ID는 공유되지만 서버 상태 전이, 클라이언트 진행 화면, Unity 공간 행동 사이의 호환성을 자동으로 증명할 공통 관문은 부족하다.

## 핵심 제안

OS를 하나의 거대한 실행 class로 만들지 않는다. 다음 네 층으로 분리한다.

```text
OS Lifecycle Definition Core
  ├─ 안정 OS·단계 ID
  ├─ 허용 전이와 정상·실패·회복·귀환 의미
  ├─ 순수 불변 조건과 결과·Event 의미
  └─ 계약 판본과 호환 규칙
       │
       ├─ Operations Runtime Adapter
       │    권한·동의·실제 원장·DB·Event·Outbox·외부 연동
       ├─ Simulation Runtime Adapter
       │    가상 시간·seed·Tick·Save/Replay·모의 외부 효과
       ├─ Web/Mobile Experience Adapter
       │    입력 방식·화면 단계·재시도 UX·서버 Command와 재조회
       └─ Unity Projection Adapter
            상태 사본→NPC 행동·Cue·H 공간 결속·카메라/UI
```

Core는 업무 의미를 고정한다. 환경 Adapter는 Core의 의미를 바꾸지 않고 실행 수단과 표현만 조정한다.

## 환경별 권위 경계

| 환경 | 실제 책임 | 바꿀 수 있는 것 | 바꿀 수 없는 것 |
| --- | --- | --- | --- |
| 운영 서버 | 실제 업무 상태의 최종 권위 | 인증·권한·저장·재시도·외부 API·Event | Core 단계 의미와 허용 전이 |
| Web | 긴 입력·표·증빙·관리 화면 | 화면 순서·입력 형식·로딩·오류 UX | 서버 상태 직접 확정 |
| 모바일 | 현장 알림·스캔·위치 입력·간결한 작업 화면 | 기기 권한·오프라인 대기·푸시·입력 UX | 로컬 화면만으로 업무 성공 확정 |
| Simulation Local | 가상 업무의 로컬 권위 | 가상 시간·seed·NPC 후보·Save/Replay | 실제 운영 원장·계약·결제 |
| Simulation Hosted | Local과 같은 Simulation Core의 원격 실행 | 세션 호스팅·동기화·복구 | Local과 다른 업무 의미 생성 |
| Unity 운영 관찰 | 운영 서버 상태 사본 표현 | NPC·Animation·UI·카메라·H 공간 표현 | 운영 Command 성공 자체 판정 |
| Unity Simulation 표현 | Simulation 상태 사본과 입력 표현 | Preview·입력·NPC 이동·피드백 | GameObject 도착으로 Simulation 결과 확정 |

Web과 모바일은 별도 OS Runtime이 아니다. 같은 운영 서버 OS에 명령을 요청하고 같은 원장을 재조회하는 서로 다른 Experience Adapter다. Unity도 운영 관찰과 게임 Simulation 표현을 구분하며 두 권위 상태를 하나의 객체가 동시에 쓰지 않는다.

## Core 확정 범위

1. `OperatingSystemId`, `LifecycleStageId`, 정의 판본
2. 정상 전이, 취소·실패·회복·재진입·종료 전이 그래프
3. 단계별 필수 역할과 필요한 입력 의미
4. 전이 전후의 업무 불변 조건을 판정하는 순수 Policy
5. 직접 결과와 발생 가능한 도메인 Event의 의미 ID
6. 멱등키·예상 revision·낮은 판본 거절 원칙
7. 이전 계약 판본을 읽는 호환 규칙

Core에는 EF `DbContext`, HTTP, MAUI, Blazor, Unity, `MonoBehaviour`, `GameObject`, 기기 API, 실제 시계, 임의 난수와 외부 서비스 SDK를 넣지 않는다.

## Core guard와 환경 검사의 경계

| 구분 | 예 | 소유자 |
| --- | --- | --- |
| 순서 불변 | 픽업 전 전달 완료 금지, 검수 전 적치 금지 | Core |
| 회복 불변 | 중단 뒤 허용된 회복·재진입 단계만 선택 | Core |
| 멱등 불변 | 같은 업무·명령 키가 두 직접 결과를 만들 수 없음 | Core |
| 업무 입력 의미 | 픽업에는 준비된 주문, 상차에는 인계 가능한 화물 필요 | Core가 요구사항을 선언 |
| 사용자 권한 | 현재 사용자가 음식점·기사·창고 작업자인지 | Operations Adapter |
| 실제 자료 | 주문·화물·재고·계약이 현재 DB에 존재하는지 | Operations Adapter와 Store |
| 환경 상태 | GPS, 네트워크, 모바일 오프라인, 서버 시각 | 해당 환경 Adapter |
| 가상 상태 | seed, Tick, 합성 Actor, 가상 재고 | Simulation Adapter |
| 공간 표현 | NPC가 H1에 도착했는지, Animation이 끝났는지 | Unity Adapter의 표현 상태 |

Core는 환경 자료를 직접 조회하지 않고 필요한 조건을 안정 요구사항 코드로 반환한다. 환경 Adapter는 그 요구사항을 검사한 뒤에도 최종 상태 변경을 해당 권위 UseCase에 요청해야 한다.

Core 판정은 다음 최소 자료를 반환한다.

```text
LifecycleCoreDecision
  Outcome                         AllowedByCore | BlockedByCore | RequiresEnvironmentValidation
  OperatingSystemId
  LifecycleDefinitionRevision
  CurrentStageId
  RequestedTransitionId
  NextStageCandidates[]
  CoreReasonCodes[]
  EnvironmentRequirementCodes[]
```

- `AllowedByCore`는 상태 변경 완료가 아니라 Core 순서상 환경 검사를 시작할 수 있다는 뜻이다.
- `BlockedByCore`는 환경이 달라도 실행할 수 없으며 안정 이유 코드를 반환한다.
- `RequiresEnvironmentValidation`은 권한·자료·시각·기기·외부 연동 등 Core 밖 요구사항을 명시한다.
- 환경 검사가 성공해도 실제 변경은 Operations 또는 Simulation의 권위 UseCase가 revision을 다시 확인한 뒤 수행한다.
- 사람에게 보여 줄 한국어 문구는 이유 코드 자체에 넣지 않고 각 클라이언트의 번역·표현 Adapter에서 만든다.

## 환경 Adapter에 둘 후보

### Operations Runtime

- 사용자·조직 인증과 역할 권한
- 실제 Business Case와 Case Section 조회·저장
- Command/UseCase 실행, Event·Outbox, 감사 기록
- 외부 결제·메시지·지도·운송 연동과 기능 플래그
- 재시도·보류·운영자 검토와 실제 시각

### Simulation Runtime

- 세션 ID, 가상 시간, seed와 결정적 Tick
- 합성 Actor·재고·주문·운송 상태
- Preview·Confirm, Save/Restore/Replay
- 운영 외부 효과를 만들지 않는 Stub과 모의 실패
- LocalProcess와 RemoteHost의 동일 결과 검증

### Web·Mobile Experience

- 같은 `AvailableAction`과 상태 사본을 기기별 화면 흐름으로 변환
- 모바일 스캔·푸시·오프라인 입력과 Web의 표·증빙 입력 차이
- Command 성공 뒤 canonical 원장 재조회
- 낮은 revision·만료 action·권한 변경 시 화면 전체 재동기화

클라이언트는 다음 단계를 자체 계산해 확정하지 않는다. 서버가 현재 상태와 허용 행동을 반환하고, 클라이언트는 사용자가 선택한 행동을 다시 서버에 요청한다.

### Unity Projection

- `LifecycleSnapshot`을 NPC 작업, Animation Cue, 상태 카드로 해석
- `OperatingSystemId + LifecycleStageId + SemanticPlaceStableId`를 별도 공간 결속 대장에서 H1·H2·H3에 연결
- 이동·대기·인계·회복·귀환을 표현하되 실제 상태 전이는 서버 또는 Simulation Core 결과를 기다림
- H 공간이 없으면 `WaitingForSpatialBinding`으로 남기고 임의 좌표를 만들지 않음

H 결속은 Web·모바일에 필요하지 않은 공간 표현 정보이므로 OS Core에 넣지 않는다. 동일한 단계라도 의미 위치에 따라 다른 H1을 사용할 수 있어야 한다.

## 제안 계약

이름은 구현 전 기존 공개 계약과 충돌 여부를 다시 확인하는 후보이며, 새 만능 DTO를 만들라는 뜻이 아니다.

```text
OperatingSystemLifecycleDefinition
  OperatingSystemId
  DefinitionRevision
  Stages[]
  Transitions[]
  RecoveryAndReturnRules[]

LifecycleStateSnapshot
  AuthorityKind                 Operational | Simulation
  ExecutionEnvironment         Server | LocalProcess | RemoteHost
  WorkStableId
  OperatingSystemId
  LifecycleDefinitionRevision
  CurrentStageId
  Revision
  AttentionState
  SemanticPlaceStableId
  AvailableActions[]
  RelationStableIds[]

OsLifecycleSpatialBinding
  OperatingSystemId
  LifecycleStageId
  SemanticPlaceStableId
  BuildingRoleStableId?
  H1StableId
  H2StableIds[]
  H3StableId?
  EntryConnectorStableId?
  ExitConnectorStableId?
  ReturnH1StableId?
  EvidenceStatus
```

`LifecycleStateSnapshot`은 권위 상태를 전달하고 `OsLifecycleSpatialBinding`은 그 상태를 공간에 해석한다. 공간 결속의 변경이 OS 단계 정의 판본을 불필요하게 올리지 않도록 두 계약을 분리한다.

## 실행 흐름

### 운영 Web·모바일

```text
사용자 입력
→ 환경별 입력 Adapter
→ 서버 Command API
→ Core 전이·불변 조건 검사
→ Operations UseCase 권한·revision 검사
→ 실제 저장·Event·Outbox
→ canonical 상태 재조회
→ Web 또는 모바일 화면 갱신
```

### Unity 운영 관찰

```text
운영 서버 상태 사본
→ Unity Client·Mapper
→ LifecycleStateSnapshot
→ 공간 결속 대장 조회
→ H1 작업·H2 연결·H3 회랑의 NPC 표현
→ 다음 서버 revision 대기
```

NPC가 목표 지점에 도착하거나 Animation을 끝내도 운영 단계가 자동 완료되지 않는다.

### Unity 게임 Simulation

```text
Unity 입력
→ Simulation Preview
→ 명시적 Confirm
→ 같은 Core 의미 규칙을 사용하는 Simulation Runtime
→ 가상 상태·Tick·Save/Replay 변경
→ Simulation 상태 사본
→ Unity NPC·공간 표현 갱신
```

운영과 Simulation은 단계 ID와 순수 규칙을 공유할 수 있지만 Business Case, 저장소, revision 계열과 Event를 공유하지 않는다.

## 네 OS에 대한 첫 적용 범위 후보

| OS | Core에서 유지할 것 | 첫 환경 차이 | H 결속의 주요 결손 |
| --- | --- | --- | --- |
| 음식배달 | 주문·응답·조리·배차·픽업·전달·회복 | 모바일 기사 위치·푸시, Web 음식점 응답, Unity 오토바이·도보 | 조리·대기·픽업·전달·귀환 H1 세분화 |
| 화물운송 | 의뢰·합의·배차·상차·운송·하차·증빙·회복 | 모바일 운행·인수, Web 조건·증빙, Unity 차량 이동 | 상하차 H1, 안전 정차 H1, H3 운송 회랑 |
| 창고 | 입고·검수·적치·할당·피킹·포장·인계·회복 | 모바일 스캔, Web 작업목록, Unity 작업자·팔레트 | 단계별 H1과 창고 H2 내부 연결 |
| 마트·도심물류 | 계약·발주·입고·재고·할당·피킹·인계·회복 | 모바일 피킹, Web 재고·주문, Unity 진열·포장·인계 | 마트 H1/H2와 음식배달 인계 Connector |

첫 수직 표본은 기존 증거가 가장 많은 음식배달 하나로 제한한다. 네 OS 계약을 한 번에 이전하지 않고, 음식배달에서 Core→Operations→Web/모바일→Unity Projection의 소비자 호환을 확인한 뒤 같은 패턴을 확장한다.

## 공통 실패·복구 계약 후보

앱이 HTTP 상태와 예외 문구를 각자 해석해 다음 행동을 추측하지 않게 한다. 서버의 역할별 상태 사본 또는 표준 오류 응답은 최소한 다음 의미를 안정 코드로 제공하고, 앱은 그 결과를 화면에 번역한다.

```text
FailureCode
FailureClassCode
ResponsibilityRoleCode
RequiresStateRefresh
RetryPolicyCode
RetryAfterUtc?
CurrentRevision?
AvailableRecoveryActions[]
TraceId
```

원시 예외, 내부 테이블명, 개인정보는 계약에 싣지 않는다. `TraceId`는 운영자와 개발자가 같은 실패를 찾는 상관관계 키일 뿐 사용자 식별자가 아니다.

| 실패 분류 | 기본 조치 | 앱 표현 | 운영자 개입 |
| --- | --- | --- | --- |
| `TransientTechnical` | 같은 멱등 키로 제한 자동 재시도 | 연결 지연·다시 시도 시각 | 최대 시도 뒤에만 |
| `RevisionConflict` | canonical 상태 전체 재조회 | 최신 상태로 갱신하고 사용자가 다시 선택 | 반복 충돌만 |
| `AuthenticationExpired` | refresh token 1회, 실패 시 재로그인 | 로그인 만료 | 보안 이상만 |
| `PermissionDenied` | 자동 재시도 금지 | 현재 역할에서 수행할 수 없음 | 권한 원장 확인 |
| `BusinessRuleBlocked` | 상태와 `AvailableActions` 재조회 | 안정 이유와 가능한 다음 행동 | 정책 예외만 |
| `ExternalPending` | 지정 시각까지 기다린 뒤 조회 | 외부 처리 대기 | 기한 초과 뒤 |
| `TerminalManualReview` | 자동 처리 중단·사건 보존 | 운영자 확인 필요 | 안전 재시도 또는 OS별 판단 |
| `CompensationRequired` | 원 업무를 강제 완료하지 않고 별도 보상·취소 흐름 | 처리 보류와 책임 주체 | OS별 승인 필요 |

공통 복구 행동은 `RefreshState`, `Reauthenticate`, `RetryIdempotent`, `WaitUntilRetryAt`, `ReturnToSafeStep`, `RequestOperatorReview`까지를 우선 후보로 둔다. `ForceComplete`, 임의 revision 변경, 원장 삭제는 공통 행동으로 열지 않는다. 취소·보상·폐기·반환은 음식배달·화물·창고·마트가 자기 정책과 권한으로 별도 판정한다.

### 앱의 고정 처리 순서

```text
Command 요청
→ 성공이면 canonical 상태 재조회
→ 실패면 표준 실패 계약 해석
→ RequiresStateRefresh이면 전체 재조회
→ 서버가 제공한 AvailableRecoveryActions만 표시
→ 자동 재시도는 멱등성·횟수·시각 조건을 만족할 때만 수행
→ 최종 실패는 운영자 확인 대장으로 인계
```

오프라인·시간 초과·앱 종료 중에는 로컬 성공을 만들지 않는다. 보류 중 입력을 저장하더라도 재연결 뒤 서버가 역할·상태·revision을 다시 승인해야 한다.

### 검증 행렬

각 OS는 정상 경로 외에 최소 다음 사례를 같은 판본에서 검증한다.

1. 통신 시간 초과 뒤 멱등 재시도
2. 낮은 revision 충돌 뒤 전체 재조회
3. 인증 만료 뒤 갱신 또는 재로그인
4. 권한 없는 역할의 행동 거절
5. 업무 규칙 거절 뒤 올바른 `AvailableActions`
6. 외부 처리 대기와 만료
7. 최대 재시도 초과 뒤 운영자 확인 인계
8. 운영자 조치 뒤 원 업무·후속 원장의 독립 재조회

이 행렬은 서버 단위 시험, 역할 Client headless, 관리자 앱 ViewModel, 실제 장치 UI 증거를 서로 분리한다. 앞 단계 성공을 뒤 단계 증거로 대신하지 않는다.

## 단계적 리팩터링 제안

1. 현행 lifecycle 단계 ID, 실제 상태 전이, 화면별 중복 판정과 변경 주체를 전수 대조한다.
2. 기존 `OperatingSystemLifecycleCatalog`를 호환 기준선으로 동결하고 단계 삭제·이름 변경을 금지한다.
3. 허용 전이·회복·귀환과 순수 불변 조건을 Core 후보로 추가하되 실제 저장 코드를 이동하지 않는다.
4. 음식배달 Operations UseCase가 Core 판정 결과를 사용하고 기존 API·Event·DB 결과가 같은지 특성 시험으로 비교한다.
5. Web·모바일의 자체 다음 단계 추론을 제거하고 서버의 상태·허용 행동을 소비하게 한다.
6. Simulation Local·Hosted가 같은 Core 의미를 사용하되 독립 가상 Aggregate와 Save/Replay를 유지한다.
7. Unity에는 Core 전이 실행기가 아니라 상태 사본 Interpreter와 `OsLifecycleSpatialBinding` Adapter를 둔다.
8. 음식배달 한 사례의 정상·거절·취소·중단·회복·귀환을 서버, Web/모바일 계약, Simulation, Unity 공간 표현에서 같은 정의 판본으로 교차 검증한다.
9. 화물, 창고, 마트 순으로 확장하고 OS 간 인계는 서로 다른 `WorkStableId`와 명시적 관계 ID를 유지한다.

## 검증 관문

- 동일 Core 입력은 환경과 무관하게 같은 허용·거부 코드와 다음 단계 후보를 만든다.
- Operations만 실제 업무 원장을 변경하며 권한·revision·Event·Outbox를 검증한다.
- Web·모바일은 서버 실패를 로컬 성공이나 sample fallback으로 숨기지 않는다.
- Simulation Local·Hosted는 같은 명령열에서 최종 revision과 Save/Replay hash가 같다.
- Unity NPC 도착·Animation·카드 조작이 운영 또는 Simulation 권위 revision을 독자적으로 변경하지 않는다.
- H 결속 누락은 다른 좌표로 자동 대체하지 않고 `WaitingForSpatialBinding`으로 남는다.
- 단계 정의 판본과 환경 Adapter 지원 판본이 다르면 실행·표현 전에 명시적으로 거절한다.
- 기존 API, Event, 저장 ID와 단계 ID는 migration·호환 판본 없이 변경하지 않는다.

## 제외 범위

- 이번 제안에서 코드·DB migration·API·클라이언트·Unity Scene을 수정하지 않는다.
- 네 OS 전체를 하나의 공통 상태 기계나 하나의 Process Manager로 합치지 않는다.
- 운영과 Simulation의 상태 저장소를 통합하지 않는다.
- H1~H3를 OS Core 단계로 만들지 않는다.
- 모바일 오프라인 상태에서 실제 업무 완료를 확정하지 않는다.
- 현재 4개 OS 이외의 6개 OS에 임의 생명주기를 만들지 않는다.

## 확정

- OS는 단일 실행 class가 아니라 안정 식별자 아래 여러 업무 책임을 조립한 전 생명주기 경계다.
- Core 의미 규칙과 환경별 권위·입력·저장·표현을 분리한다.
- Web·모바일은 같은 운영 서버를 사용하는 Experience Adapter다.
- Unity는 운영 관찰과 Simulation 표현을 구분한다.
- H 공간 결속은 별도 Integration 계약이다.
- Core는 단계 ID·순서·설명, 허용 전이, 실패·회복·귀환과 환경 비의존 순수 guard를 소유한다.
- 인증·실제 DB 존재 여부·외부 API·기기 상태·실제/가상 시계·Unity 공간 도착은 Core guard가 아니다.
- Core 전이 판정은 `AllowedByCore / BlockedByCore / RequiresEnvironmentValidation` 세 상태와 안정 이유·환경 요구사항 코드를 반환한다.
- 최종 실패에서 관리자 앱이 공통으로 제공하는 변경 행동은 `RetryIdempotent`에 따른 안전 재시도 예약 하나다. 취소·보상·폐기·반환·강제 완료는 공통 복구 행동이 아니며 각 OS의 별도 정책·권한·승인·증거가 준비돼야 한다.

## 미정

- 첫 음식배달 표본의 정확한 `FailureCode` 목록과 기존 API 오류의 호환 mapping
- 모바일 오프라인 입력의 보관·만료·재승인 규칙
- `OsLifecycleSpatialBinding`의 서버 저장 위치와 Graph Map 관계
- 기존 환경별 중복 전이 판정의 실제 목록과 migration 순서
- 안전 재시도 예약 뒤 다시 실패한 사건의 알림 우선순위와 담당자 배정 방식

## 확정된 AvailableActions 경계

현재 상태를 조회할 때 호출자별 작은 `AvailableActions`를 같은 역할별 상태 사본에 포함한다. 항목은 `ActionId`, `RevisionKindCode`, `ExpectedRevision?`, `ExpiresAtUtc?`, `EnvironmentRequirementCodes`로 제한하고 큰 입력 schema는 별도 Command 계약에 둔다. 앱은 이 목록으로 버튼을 통제하지만 실제 Command는 역할·상태·revision을 다시 검증한다. Unity 읽기 전용 상태 사본에는 조작 행동을 포함하지 않는다.

## r8 구현: 음식배달 실패·복구 첫 세로 절편

- 공통 계약에 실패 분류, 재시도 정책, 책임 역할, 상태 전체 재조회 필요 여부와 허용 복구 행동을 추가했다. 원시 예외·내부 자료·개인정보는 싣지 않는다.
- 음식점 진행 변경의 판본 충돌은 `RevisionConflict`, `RefreshState`, 서버의 현재 주문 revision을 응답한다. 음식점 앱은 해당 주문 정본을 즉시 다시 읽어 로컬 상태를 교체하되 실패 자체는 숨기지 않는다.
- 전송 단계의 일시 통신 장애는 기존 `클라이언트요청Id`와 같은 요청 DTO를 그대로 사용해 한 번만 재시도한다. 서버가 복구 행동을 허용하지 않은 일반 HTTP 실패는 자동 재시도하지 않는다.
- 후속 처리 최대 실패는 `TerminalManualReview`와 `ScheduleOperatorSafeRetry`만 노출한다. 관리자 UI는 이 허용 행동이 있을 때만 안전 재시도 예약 버튼을 표시하며 원 업무 상태는 변경하지 않는다.
- 집중 시험 41건, 음식점 데스크 Windows 빌드, 관리자 앱 Windows 빌드가 통과했다. 실제 서버 실행, 네이티브 앱 클릭, Android 장치, Unity·Game View 검증은 수행하지 않았다.

## 다음 질문 하나

같은 실패·복구 계약의 다음 확장 대상을 화물운송 OS로 할지, 창고·상거래 이행 OS로 할지는 아직 확정하지 않았다.
