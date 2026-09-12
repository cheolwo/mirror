# [기획 · 시스템·운영 기능 이관 · PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001 · r4]

- 기획 판본: `unity-os-observation-modules.r4`
- 상태: `ApprovedPlanningBaseline / FoodDeliveryServerVerticalSliceTested / FoodDeliveryUnityMemoryModuleImplemented / ReadOnlyOperationalObservationConfirmed / OperatingSystemCenteredShellConfirmed / WorldBindingDeferred`
- 상위 정본: [운영 서버에서 Mirror Unity로의 이관 r8](README.md)
- 관련 운영 기획: [주문 중심 운영체제 업무망 r6](../../공통/PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK/README.md), [비정상 업무 처리·회복 r1](../../공통/PLAN-OPERATIONS-ABNORMAL-WORK-RECOVERY/README.md)
- 대상 장면: canonical `SimulationWorldShell`
- 권위 경계: 운영 서버 상태가 최종 사실이며 Unity는 읽기 전용 관찰 표현만 수행한다
- 공통 읽기 안내: [Unity 운영 데이터 읽기 전용 관찰 안내](../../../../Architecture/Unity운영데이터읽기전용관찰안내.md)
- 개발 인계: `PlanningApproved / GoalNotActivated`

## 목적

운영 서버의 OS를 Unity 안에 복제해 별도 업무 권위로 실행하지 않는다. 서버가 확정한 OS 고유 식별자, 업무 고유 식별자, 생명주기 단계, 인계, 보류·회복, revision을 읽어 `SimulationWorldShell`의 NPC·시설·차량·화물·상태 패널로 표현하는 작은 관찰 모듈을 상향식으로 조립한다.

Unity 안에서도 OS를 단순 표시 속성이 아니라 **운영 데이터 관리의 첫 번째 모듈 경계**로 사용한다. 지역 상태 사본을 받은 뒤 Entity 종류나 화면 종류부터 나누지 않고 `OperatingSystemId`로 먼저 분배한다. 각 OS 모듈은 자기 업무 인스턴스의 단계·대기·인계·보류·표현 사본을 관리하되 서버 상태를 변경하지 않는다.

```text
운영 서버 OS 원장
  → 비식별 운영 지역 상태 사본
  → Unity GET 전송·해독
  → 메모리 상태 저장소
  → OS 관찰 의미 변환
  → 공간·객체 결속
  → Presenter·GameObject 표현
  → SimulationWorldShell 관찰
```

운영 상태 사본과 가상 `SimulationSession`은 같은 표현 어휘를 사용할 수 있지만 자료원과 권위는 섞지 않는다. 한 화면 모듈은 `OperationalSnapshot` 또는 `SimulationSession` 중 하나를 명시적으로 고르고 자동 대체하지 않는다.

## r4 우선 원칙 — 서버 OS 생명주기를 먼저 닫는다

Unity 모듈이나 Prefab을 먼저 만들지 않는다. 관찰할 OS 하나를 고른 뒤 운영 서버에서 그 OS가 소유한 한 건의 생명주기가 시작부터 정상 완료까지 닫히는지 먼저 증명한다. 그 다음 완료 결과를 비식별 상태 사본으로 발행·재조회하고, Unity가 메모리에서 같은 판본을 읽는 것까지 성립한 후에만 공간·객체·Prefab을 검토한다.

```text
운영 OS 한 건 생성
  → 권한·정책 검증
  → 정상 상태 전이
  → DB·Event·Outbox 저장
  → 완료 재조회·멱등·재시작 검증
  → 비식별 읽기 전용 상태 사본 발행
  → 인증된 HTTP 재조회
  → Unity 전송·해독·revision 반영
  → OperatingSystemId별 메모리 관찰 상태
  → 공간 결속
  → primitive 표현
  → Prefab·애니메이션 고도화
```

이 순서는 **OS별 관문**이다. 모든 OS를 서버에서 다 완성한 뒤 Unity를 시작하는 의미가 아니라, OS 하나씩 이 수직 경로를 통과시킨다. 서버 생명주기 증거가 없는 OS는 Unity 객체 생성 대상이 아니다.

## 현재 재사용할 기반

| 층 | 현재 기반 | 판정 |
| --- | --- | --- |
| 서버 조합 | `운영지역장면조회UseCase` | 음식 완료·창고 작업/Actor·화물 인계를 OS ID가 있는 지역 상태 사본으로 조합함 |
| Unity 전송 | `IOperationalWorldProjectionTransport`, `OperationalWorldSceneClient` | 인증 GET 전용이며 운영 Command를 제공하지 않음 |
| 해독 | `UnityJsonOperationalWorldSceneDecoder` | `OperationalWorldSceneResponse`를 해독함 |
| 메모리 정합성 | `OperationalWorldSceneInterpreter` | area·cursor·revision·TTL·tombstone·자료원 실패를 메모리에서 처리함 |
| 일반 표현 정합성 | `WorldProjectionReconciler` | 안정 ID와 revision으로 추가·갱신·제거를 계산함 |
| 객체 원형 | `OperationalUnityTransferObjectCatalog`, `운영역할GameObjectCatalogPolicy` | 역할·시설·차량·업무 객체 후보와 생성 금지 경계를 가짐 |
| 실제 장면 결속 | 없음 | 후보를 실제 `SimulationWorldShell` GameObject와 연결하는 공통 결속기가 아직 없음 |

현재 `OperationalWorldSceneItem`은 `OperatingSystemId`, `ItemKind`, `ActivityCode`, revision과 표현 JSON을 전달하지만, OS 생명주기 단계·업무 주의 상태·인계·비정상 사건을 공통적으로 판독하는 Unity용 의미 모델은 아직 없다. 이 빈 층을 먼저 채운다.

## r4 구조 기준 — `SimulationWorldShell`의 OS 중심 관리

현재 `OperationalWorldSceneInterpreter`는 모든 항목을 `SnapshotStableId` 기준의 평평한 사전에 보관한다. 이 revision·TTL·tombstone 정합성은 유지하되, 적용 결과를 OS별 모듈로 분배하는 관찰 Kernel을 그 위에 둔다.

```text
SimulationWorldShell
└─ OperationalOsWorldRoot
   ├─ OperationalWorldSceneClient
   ├─ OperationalWorldSceneInterpreter
   ├─ OperationalOsSnapshotRouter
   ├─ OperationalOsModuleRegistry
   ├─ OperationalOsRelationProjection
   ├─ OperationalOsWorldBinder
   └─ Modules
      ├─ DomesticCargoTransportObservationModule
      ├─ WarehouseCommerceFulfillmentObservationModule
      ├─ SsalddelMartUrbanLogisticsObservationModule
      ├─ FoodDeliveryObservationModule
      └─ UnsupportedOsObservationModule
```

### 관찰 Kernel의 책임

- 공용 지역 상태 사본을 한 번 갱신한다.
- schema·area·cursor·revision·TTL·tombstone을 검증한다.
- 유효한 항목을 `OperatingSystemId`로 묶어 등록된 OS 모듈에 전달한다.
- 미등록 OS를 버리거나 다른 OS로 추측하지 않고 미지원 진단으로 보존한다.
- OS 간 인계는 어느 한 OS의 내부 상태로 합치지 않고 읽기 전용 관계 사본으로 연결한다.
- 모듈별 결과를 공통 World Binder에 전달해 같은 `SimulationWorldShell`에서 조립한다.

### 각 OS 관찰 모듈의 책임

```text
OS 관찰 모듈
├─ OperatingSystemId
├─ 지원 ItemKind·계약 판본
├─ 업무 인스턴스별 관찰 상태 저장
├─ LifecycleStage/AttentionState 해석 Adapter
├─ Actor·Facility·Vehicle·WorkObject 투영
├─ 다른 OS로 나가는/들어오는 인계 참조
└─ 자료 결손·미지원 진단
```

각 OS 모듈은 `HttpClient`, UnityWebRequest, DB 또는 다른 OS 모듈을 직접 호출하지 않는다. 공용 Client가 받은 상태 사본만 소비하고, 다른 OS와의 관계는 안정 인계 ID를 통해 `OperationalOsRelationProjection`이 연결한다. 이렇게 해야 OS가 중심이면서도 전역 God Manager나 모듈 간 순환 호출이 생기지 않는다.

### API와 OS의 관계

첫 구현에서는 OS마다 별도 API를 만들지 않는다. 기존 지역 상태 사본 API가 `OperatingSystemId`를 포함하므로 Unity가 한 지역 사본을 한 번 받은 다음 OS Router가 분배할 수 있다.

후속에 특정 OS의 상세 단계가 필요하면 기존 지역 상태 사본의 판본을 확장하거나 권한 있는 상세 조회를 별도로 검토한다. 이 경우에도 OS 모듈이 임의 route를 직접 호출하지 않고 공용 Repository/Client 계약을 거친다.

### OS 중심과 공간 중심의 결합

OS는 `무슨 업무가 어떤 상태인가`를 관리하고 공간은 `그 업무를 어디에서 어떻게 볼 것인가`를 관리한다.

```text
OperatingSystemId + WorkStableId + Revision
  → OS 관찰 상태
  → WorldBindingRegistry
  → AreaSet/H1/H2 + ObjectStableId
  → Presenter
```

같은 OS 업무가 여러 공간에 걸칠 수 있고, 같은 창고·마트·도로에는 여러 OS의 표현이 함께 존재할 수 있다. 그러므로 OS 모듈이 GameObject 위치를 소유하지 않고 `WorldBindingRegistry`가 Graph Map·배치맵의 검증된 공간 결속을 제공한다.

## OS 하나를 닫는 상향식 관문

### 1. 운영 서버 생명주기 증거 묶음

먼저 OS 하나와 대표 업무 한 건을 고른다. 시작 Command/API의 권한·초기 상태, 허용된 상태 전이, 담당 주체·수량·위치·기한·인계, 완료 조건, DB 재시작 후 재조회, Event·Outbox 재처리 멱등성을 먼저 검증한다. 취소·거절·보류·회복은 정상 완료와 다른 결과로 구분한다.

첫 표본은 이미 정상 완료 상태 사본과 시험이 있는 `FoodDeliveryOS`를 추천한다.

```text
주문대기 → 조리중 → 픽업대기 → 기사배정 → 픽업완료 → 전달완료 → 수령확인
```

### 2. 운영 완료 → 상태 사본 발행 관문

서버 생명주기가 닫힌 뒤에만 상태 사본을 만든다. 완료 판본과 상태 이력을 다시 검증하고 안정 ID·OS ID·revision·지역·만료 시각·표현 가능한 결과만 발행한다. 개인식별자·상세 주소·정밀 GPS는 제거한다. 저장된 사본 재조회, 동일 입력 재처리, TTL·tombstone, 자료원 일부 실패, 인증 HTTP `200`과 비인증 `401`을 검증한다.

### 3. Unity 전송·메모리 해석 관문

이 단계에서도 GameObject를 만들지 않는다. 인증 GET, JSON 해독, cursor·revision·TTL·tombstone 반영, `OperatingSystemId`별 Router 분배, OS 모듈의 메모리 관찰 상태까지만 검증한다. 서버 완료 판본과 Unity 관찰 판본이 다르면 표현 관문을 닫는다.

### 4. Unity 안전 OS 관찰 계약과 Kernel 계약

UnityEngine, HTTP, DB에 의존하지 않는 순수 계약을 먼저 둔다.

```text
운영OS관찰상태
├─ OperatingSystemId
├─ WorkStableId
├─ Revision
├─ LifecycleStageId
├─ WorkStateCode
├─ AttentionStateCode
├─ ResponsibleRoleCode
├─ HandoffStableIds
├─ IncidentStableId
├─ OccurredAtUtc
└─ DataStatusCode
```

첫 `AttentionStateCode`는 책임 판정이 아니라 표현 의미만 가진다.

- `Normal`: 정상 진행
- `Waiting`: 다른 작업·당사자 대기
- `AttentionRequired`: 확인 필요
- `ProtectedHold`: 추가 손실 방지를 위한 보류
- `Recovering`: 승인된 회복 작업 진행
- `Completed`: 결과 반환 완료
- `Stale`·`Unavailable`: 자료 품질 문제

`기사과실`, `화주과실`, `음식점과실` 같은 귀책 표시는 이 공통 상태에 넣지 않는다.

같은 단계에서 `IOperationalOsObservationModule`, `OperationalOsModuleDescriptor`, `OperationalOsObservationBatch`에 해당하는 순수 계약을 정의한다. 계약 이름은 구현 시 기존 명명·assembly 경계를 다시 확인하되 의미는 OS별 입력·출력·지원 판본을 명시하는 데 한정한다.

### 5. OS 모듈 대장과 상태 사본 Router

지원 OS ID, 지원 계약 판본, Adapter와 표현 능력을 등록하는 읽기 전용 대장을 둔다. Router는 기존 Interpreter의 적용 결과를 `OperatingSystemId`별 batch로 나눠 모듈에 전달한다. 대장은 OS 생명주기 상태를 전진시키지 않는다.

### 6. 운영 상태 사본 Adapter

기존 `OperationalWorldSceneItem`을 위 관찰 계약으로 바꾸는 순수 Adapter를 만든다. OS별 원문 상태 코드를 View가 직접 해석하지 않게 한다. 알 수 없는 OS·단계는 임의 추론하지 않고 `Unsupported` 또는 `AttentionRequired`가 아닌 별도 미지원 진단으로 남긴다.

### 7. OS 관찰 메모리 저장소

기존 `OperationalWorldSceneInterpreter`의 area·cursor·revision·TTL·tombstone 처리를 재사용한다. 그 결과만 관찰 상태 Adapter에 전달한다. 새 Save나 `PlayerPrefs`를 만들지 않고 앱 종료 뒤 운영 상태를 재생하지 않는다.

### 8. OS 간 관계 투영

주문→마트→음식배달이나 화주→화물운송→화주 인수처럼 OS를 넘는 인계를 안정 인계 ID와 revision으로 읽는다. 출발·도착 모듈이 서로의 내부 저장소를 직접 읽지 않고 관계 투영이 두 업무 참조를 연결한다. 아직 구현되지 않은 인계는 선으로 추측해 만들지 않는다.

### 9. 공간·객체 결속 대장

OS 하나를 건물 하나와 동일시하지 않는다. 관찰 상태를 기존 객체 원형과 H1/H2에 결속한다.

```text
OS 업무 인스턴스
  ├─ Actor: 작업을 수행하거나 기다리는 NPC
  ├─ Facility: 음식점·마트·창고·인수 지점
  ├─ Vehicle: 오토바이·화물차
  ├─ WorkObject: 주문 묶음·상자·팔레트·화물
  └─ Panel: 단계·대기 이유·근거·다음 예정 표시
```

공간 결속이 없는 상태는 임의 좌표에 생성하지 않고 운영지도/목록 패널의 미배치 항목으로 남긴다.

### 10. 비귀책 시각 어휘

저채도 기본 표현 위에 작은 상태 신호만 더한다.

- 정상 진행: 이동·작업 중 cue
- 대기: 정지 자세와 대기 표식
- 보호 보류: 해당 화물/작업 구간만 격리 띠와 점검 표식
- 회복 중: 원 업무와 구별되는 점검·재포장·재배차 cue
- 완료: 다음 OS 또는 주문 루트로 결과가 돌아갔음을 짧게 표시

빨간색만으로 사람의 잘못을 암시하지 않는다. 색·아이콘·문구는 `문제 있음`, `확인 중`, `안전 보류`를 구분한다.

### 11. GameObject 결속기와 Presenter

기존 `WorldProjectionReconciler`의 Added·Updated·Removed를 받아 다음만 수행한다.

- 기존 안정 ID 객체 갱신
- 준비된 객체 원형의 `VisualRoot` 선택
- 위치·상태 cue·간단한 패널 적용
- tombstone·만료 객체 제거
- 미지원 상태 fallback 표현

애니메이션 종료나 NPC 도착으로 서버 상태를 변경하지 않는다.

### 12. `SimulationWorldShell` 조립 모듈

새 Scene이나 전역 `OSManager`를 만들지 않는다. 공식 장면에 다음과 같은 작은 Composition Root 하나를 연결한다.

```text
SimulationWorldShell
└─ OperationalOsWorldRoot
   ├─ SourceSelection
   ├─ RefreshScheduler
   ├─ OperationalOsSnapshotRouter
   ├─ OperationalOsModuleRegistry
   ├─ OperationalOsRelationProjection
   ├─ WorldBindingRegistry
   ├─ Presenter
   └─ DiagnosticsPanel
```

갱신 실패 시 기존 마지막 성공 표현을 유지하고 `RefreshError`만 표시한다. NPC `Tick`, 로컬 Simulation 시간과 운영 서버 cursor를 서로 대신하지 않는다.

### 13. 첫 OS 수직 표본

첫 표본은 서버에 정상 완료 이력·완료 사본·Unity 공유 계약이 이미 있는 음식 배달로 제안한다.

```text
FoodDeliveryOS
  주문대기→조리중→픽업대기→기사배정→픽업완료→전달완료→수령확인
  → 완료 상태 사본 발행
  → 지역 상태 사본 API 재조회
  → Unity 메모리의 FoodDeliveryOS 관찰 상태
  → 후속 공간·primitive 표현
```

정상 경로를 먼저 닫은 뒤 거절·취소·재배차·재조리는 같은 OS의 별도 회복 표본으로 추가한다. 화물운송 비정상 사건은 두 번째 OS 수직 표본으로 내린다.

### 14. OS별 확장 순서

1. 음식 배달 정상 완료·완료 사본·Unity 메모리 관찰
2. 음식 배달 거절·취소·재배차·재조리 회복
3. 화물운송 정상 완료·화주 인수 반환
4. 화물운송 비정상 사건·보호 보류·회복
5. 창고 입고·검수·적치와 이상 수량 보류
6. 마트 피킹·포장·라스트마일 인계와 결과 반환
7. 공동주문 대표·담당자·비용 귀속의 비식별 업무망

각 표본은 독립적으로 `조회 → 해석 → 객체 결속 → 갱신 → 만료/제거`까지 닫은 뒤 다음 OS로 확장한다.

## 첫 구현·검증 묶음 제안

첫 Goal은 Scene 그래픽이 아니라 `FoodDeliveryOS` 정상 완료의 서버 증거와 Unity 메모리 인계까지만 묶는다.

1. `FoodDeliveryOS` 시작→수령확인 정상 전이 집중 시험
2. 완료 조건·완료 재조회·DB 재시작·Outbox 재처리 시험
3. 완료 상태 사본의 비식별·지역·revision·TTL 시험
4. 인증된 지역 상태 사본 HTTP 재조회
5. `운영OS관찰상태`, OS 모듈 대장과 OS별 Router
6. `FoodDeliveryOS` Adapter·메모리 관찰 모듈
7. 서버 완료 revision과 Unity 관찰 revision 일치, TTL·tombstone·부분 실패 회귀 시험

그 다음 Goal에서만 공간 결속 대장과 primitive 주문·음식점·기사·배달 완료 표식을 `SimulationWorldShell`에 연결한다. Prefab·애니메이션은 primitive 표현의 안정 ID·revision·제거 회귀가 통과한 뒤의 별도 표현 Goal로 둔다.

## 첫 서버 수직 관문 구현 결과

`음식배달정상수직생명주기Tests`가 배차 엔진의 추천 결과인 추천 운송 원장을 입력 경계로 받아 다음 실제 서버 경로를 한 시험에서 관통한다.

```text
음식주문등록CommandHandler
  → 음식점주문수락CommandHandler
  → 음식점주문진행변경CommandHandler(픽업준비)
  → 음식배달기사업무Service(수락·픽업완료·전달완료)
  → 주문자음식주문수령확인CommandHandler
  → 음식배달완료WorldProjectionService
  → 음식배달완료WorldSnapshot조회UseCase
```

검증 범위는 일곱 단계 순서, 같은 수령 확인 재요청의 revision·Outbox 멱등성, 완료 투영의 단일 처리, 면목동 지역 분류, 서버 완료 revision과 조회 사본 revision 일치, 상세 주소·주문자·기사·주문 식별자의 비공개다. 후보 탐색·점수 계산은 기존 배차 엔진 독립 시험의 책임으로 남기며 이 시험이 대신하지 않는다. 실제 MySQL 재시작, 인증 HTTP, Unity 메모리 OS Router와 `SimulationWorldShell` 표현은 이 시험의 증거가 아니다.

## Unity 메모리 관문 구현 결과

공용 `OperationalWorldOperatingSystemIds`에 지역 상태 사본이 사용하는 안정 OS ID를 명시하고, 기존 `OperationalWorldSceneInterpreter`의 현재 상태를 `OperationalOsObservationRouter`가 OS별 모듈에 교체 적용하도록 구현했다. 첫 모듈은 `FoodDeliveryOsObservationAdapter`이며 `CompletedLifecycle + ReceiptConfirmed + OnlineEphemeral + 로컬 저장·재생 금지` 조건을 모두 만족한 사본만 완료 관찰 상태로 받는다.

`OperationalOsWorldObservationSession`이 기존 인증 GET Client→Interpreter→OS Router를 한 번의 읽기 전용 갱신으로 잇는다. TTL 만료와 Clear는 OS 메모리에도 반영되고, 미지원 OS·미지원 항목·안전하지 않은 데이터 정책은 임의 추론하지 않고 진단으로 남긴다. Interpreter가 schema·area 문제로 갱신을 거부하면 기존 OS 메모리는 유지한다. 집중 시험 `5/5`가 이 경계를 검증하지만 실제 Unity package import, `SimulationWorldShell`, GameObject, Play Mode와 Game View 증거는 아니다.

## 확정

- 기존 운영 지역 장면 전송·해독·메모리 정합성 기반을 재사용한다.
- 새 공식 Scene, 새 운영 원장, Unity 전용 OS 권위를 만들지 않는다.
- OS별 서버 상태 코드를 GameObject가 직접 해석하지 않게 한다.
- 운영 상태와 가상 Simulation 상태는 명시적으로 자료원을 선택하며 자동 대체하지 않는다.
- 공간 결속이 없는 상태는 임의 배치하지 않는다.
- 각 OS는 서버 생명주기·상태 사본·Unity 메모리 인계를 자기 수직 관문으로 먼저 통과한다.
- 서버 생명주기가 완료되지 않은 OS는 Unity 공간 결속·GameObject·Prefab 대상으로 승격하지 않는다.
- 첫 수직 표본은 `FoodDeliveryOS` 정상 완료로 둔다.
- 초기 Unity OS 모듈은 운영 서버 상태를 읽어 표현만 하는 완전한 읽기 전용 관찰로 고정한다.
- 문제 해결 입력과 서버 Command는 초기 OS 관찰 모듈에 넣지 않고 Web·모바일 업무 화면 또는 후속 별도 WI에서 다룬다.
- `SimulationWorldShell`의 운영 데이터는 `OperatingSystemId`를 첫 모듈 분류축으로 사용한다.
- 공용 상태 사본을 OS별 Router가 분배하고, 각 OS 모듈이 자기 업무 인스턴스의 관찰 상태와 표현 투영을 관리한다.
- OS 간 관계는 안정 인계 ID의 읽기 전용 관계 투영으로 연결하고 모듈끼리 직접 호출하지 않는다.
- OS 모듈은 공간 좌표를 소유하지 않고 검증된 Graph Map·배치맵 결속을 `WorldBindingRegistry`에서 받는다.

## 미정

- OS 관찰 계약을 기존 `OperationalWorldSceneItem`의 판본 상승으로 확장할지 별도 내부 해석 모델로만 둘지
- 첫 음식 배달 표본의 정확한 H1·배치 객체·정보 패널 위치
- 비정상 업무 문답에서 아직 확정되지 않은 일부 인수·전체 격리·회복 결과 상태

## 다음 질문 하나

없음. `FoodDeliveryOS` 정상 완료의 서버 수직 관문과 Unity 메모리 관문까지 구현했다. 다음 구현 관문은 검증된 공간 결속 대장과 primitive 표현이며, Prefab·애니메이션과 운영 Command는 계속 제외한다.
