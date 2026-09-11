# 화물운송 OS와 음식배달 OS

- 기획 ID: `PLAN-OPERATIONS-LOGISTICS-OS`
- 기획 분야: 공통 / 운영 업무 구조
- 기획 판본: `operations-logistics-os.r6`
- 상태: `Draft / TwoOperationalOsConfirmed / EndToEndLifecycleOwnershipConfirmed / InternalResponsibilitySplitConfirmed / SharedDispatchCoreRelated / ExplicitCrossOsHandoffConfirmed / PerOsEngineCatalogConfirmed / BackendFoundationImplemented / DisposableMySqlValidated / WarehouseOutboundToCargoHandoffImplemented / CompletionProjectionCoreImplemented / OtherConcreteWorkflowAdoptionPending / FallbackActivationPending / OperationalActivationDeferred`
- 상위 기획: 없음. 운영 물류 업무의 상위 경계
- 하위·관련 기획: `PLAN-OPERATIONS-DISPATCH-CORE`, `PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001`
- 관련 WI·PlayableLoop: 없음. 운영 서버 구조 기획이며 Unity 실행 기획이 아님
- Graph Map 영향: `NoImpact` — OS는 운영 업무 경계이며 Unity 공간 Graph Map이 아님
- 개발 인계 상태: `AcceptedInCurrentThread / BackendFoundationImplemented / FrontendAndUnityExcluded`

## 목적

흩어진 기능과 엔진을 사람이 업무 전체 흐름으로 이해할 수 있도록 `화물운송 OS`와 `음식배달 OS`라는 두 운영 경계로 묶는다. OS는 단일 거대 class나 서버 process가 아니라 하나의 업무가 시작해 종료되고 중단에서 회복되는 전 생명주기를 소유하는 제품·운영 관점의 체계다.

저장·API·Event에서 이미 사용하는 안정 식별자 `DomesticCargoTransportOS`와 `FoodDeliveryOS`를 유지한다. 과거의 모호한 `하위 OS`를 새 기술 역할명으로 되살리지 않고 내부 구현은 실제 책임에 따라 `ProcessManager`, `WorkflowCoordinator`, `Policy`, `Engine`, `UseCase`, `Store`로 나눈다.

## 확정

### 화물운송 OS

화물운송 OS는 다음 생명주기를 처음부터 끝까지 소유한다.

1. 화주의 운송 의뢰와 운송 조건 제시
2. 기사 후보 탐색과 조건 확인
3. 배차 제안·수락·거절과 예약
4. 수락 뒤 핵심 조건 변경과 기사 재동의
5. 상차·운송·경유·하차
6. 운송 약속 그래프와 다음 콜 연속 배차
7. 사고·고장·지연·중단과 재배차
8. 완료 증빙·대기 및 이동 보전·정산 후보·이의 제기

현재 `CargoYongdalDispatchEngine`은 이 OS 아래의 배차 판단 엔진으로 분류한다. 차량·화물 적합성, 일정 삽입 가능성, 운송 약속 위험, 운임·대기료 계산은 각각 좁은 Engine·Evaluator·Calculator로 둘 수 있지만 OS 전체를 대신하지 않는다.

### 음식배달 OS

음식배달 OS는 다음 생명주기를 처음부터 끝까지 소유한다.

1. 주문자의 음식 주문
2. 음식점의 주문 수락·거절과 조리시간 선택
3. 조리 진행과 픽업 준비
4. 기사 후보 탐색과 배차 제안·수락·거절
5. 가게 도착·현장 대기·픽업·전달
6. 묶음 배달과 기사별 수행 안전·균형
7. 조리 지연·사고·중단·재조리·재배차
8. 주문 취소·환불·음식점 보상·사고 손실 대응·운영자 검토

현재 `FoodDeliveryDispatchEngine`은 이 OS 아래의 배차 판단 엔진으로 분류한다. 조리시간 추정, 묶음 배달, 기사 균형, 중단 책임과 보상 계산은 각각 좁은 판단 도구와 조율 책임으로 분리한다.

### OS 내부 책임

| 책임 | 내부 구성요소 | 경계 |
| --- | --- | --- |
| 전체 업무 상태와 장기 진행 | `ProcessManager` | 재시도·보류·다음 단계 조율. 권한 검증 우회 금지 |
| 여러 실행 호출과 외부 인계 순서 | `WorkflowCoordinator` | UseCase·Adapter 호출 순서와 실패 회복 조율 |
| 행동 가능 여부 | `Policy` / `Gate` | 입력을 판정하되 상태 저장 금지 |
| 후보·점수·예상·분류 | `Engine` / `Planner` / `Evaluator` / `Calculator` | 순수 판단 결과와 근거만 반환 |
| 실제 상태 변경 | `UseCase` / `Command` / `ApplicationService` | 권한·현재 상태·revision 검증 뒤 변경 |
| 원본·투영·감사 | `Repository` / `Store` / `Event` / `Outbox` | 멱등 재처리와 같은 업무 건 재구성 보장 |

OS 자체가 DB를 직접 수정하거나 모든 기능을 가진 하나의 service가 되지 않는다. OS 식별자는 모듈·원장·API metadata·운영 화면에서 업무 소속을 설명하고, 실행은 위 구성요소의 명시적 호출 관계로 이루어진다.

### 공통 코어와의 관계

`PLAN-OPERATIONS-DISPATCH-CORE`는 두 OS 위의 상위 실행자가 아니라 두 OS가 함께 사용하는 좁은 공통 규칙이다. 다음처럼 업무 종류와 무관하게 같은 의미인 것만 공유한다.

- 배차 제안·수락·거절·중단의 공통 상태와 사유 구조
- 유효 제안 판정, 기사 수신 의사와 서버 실효 상태의 분리
- 오늘·어제·그제 지표와 균형 점유의 공통 계산 기반
- 안정 ID, revision, 멱등 키, 감사 사건과 재구성 가능한 투영

화물 적재 적합성·운송 약속 그래프는 화물운송 OS가, 음식 조리시간·재조리·음식점 보상은 음식배달 OS가 소유한다. 공통 코어가 두 OS의 전체 생명주기나 영속 상태를 소유하지 않는다.

### Simulation·Unity 경계

두 운영 OS는 실제 사용자 권한과 운영 원장을 소유한다. Unity와 `Ssalddel.Simulation.BusinessWorkflow`는 운영 OS를 직접 실행하거나 구현하지 않고, 가상 세션 계약 또는 승인된 읽기 전용 운영 상태 사본만 별도 Mapper로 해석한다.

### 두 OS 사이의 명시적 인계

마트·창고·복합 배송처럼 하나의 업무가 두 OS를 통과할 때 한 OS가 상대 OS의 내부 Service·Engine·Store를 직접 호출하지 않는다. 출발 OS는 자기 원장의 최신 revision을 검증한 뒤 다음 내용을 가진 불변 인계 요청을 만든다.

- 인계 요청 안정 ID와 멱등 키
- 출발 OS·도착 OS 식별자
- 출발 업무 건과 revision
- 도착 OS가 판단하는 데 필요한 최소 상태 사본
- 인계 조건·기한·요청 시각과 개인정보 공개 범위

도착 OS는 자기 Policy와 UseCase로 인계를 `수락·거절·보류`한다. 수락 전까지 출발 OS의 책임이 사라지지 않으며, 수락이 기록된 뒤에만 출발 OS는 `인계 완료`로 전이한다. 거절·시간 만료·전달 실패는 원래 업무를 자동 완료하거나 유실시키지 않고 출발 OS의 재시도·대안 선택·사람 검토 상태로 되돌린다.

인계 요청과 응답은 Event·Outbox로 재처리 가능하게 남긴다. 같은 요청이 반복돼도 도착 업무 건은 한 번만 만들어지며, 두 OS가 같은 업무 상태를 동시에 권위 있게 수정하지 않는다. 상대 OS의 내부 위험 점수나 엔진 세부 결과는 인계 계약에 포함하지 않고 필요한 결과와 근거 코드만 전달한다.

### OS별 Engine Catalog

화물운송 OS와 음식배달 OS는 서로 독립된 Engine Catalog를 소유한다. 공통 배차 코어의 `EngineFamilyId`는 두 OS가 공유할 수 있는 능력 종류만 설명하며, 실제 구현 선택·판본·활성화·중단은 각 OS Catalog가 책임진다. 한 OS의 엔진 배포나 정책 변경이 다른 OS의 활성 엔진을 자동 변경하지 않는다.

각 Catalog 항목은 최소한 다음을 가진다.

- OS 안정 ID, Engine family ID, 구현 Engine 안정 ID
- 입력·결과 계약 판본과 정책 판본
- `Primary`, `ApprovedSafeFallback`, `Shadow` 역할
- `Disabled`, `Shadow`, `Active` 활성 상태와 적용 조건
- 활성화 시각·승인 주체·변경 사유·직전 판본
- 실패·중단 시 사용할 승인된 대체 항목 또는 명시적 보류 결과

`Primary`는 해당 OS의 권위 있는 판단 후보를 만든다. `Shadow`는 같은 불변 입력 사본으로 계산하지만 결과를 배차·알림·정산·상태 전이에 사용하지 않고 비교·감사 자료만 남긴다. `ApprovedSafeFallback`은 같은 결과 계약과 더 보수적인 안전 조건을 만족하는 사전 승인 구현만 등록할 수 있으며, 다른 OS의 엔진을 임시 대체품으로 사용하지 않는다.

어떤 엔진을 호출할지는 OS의 Coordinator가 Catalog와 현재 운영 설정을 확인해 결정한다. Engine은 자기 활성 상태를 바꾸거나 다음 Engine을 직접 호출하지 않으며 DB·Event·Outbox를 수정하지 않는다. 실제 효과는 UseCase가 선택된 결과의 계약 판본·입력 revision·만료 여부를 다시 검증한 뒤 적용한다.

## 현재 코드와의 대응

- 안정 OS 식별자: `OperatingSystemIds.DomesticCargoTransport`, `OperatingSystemIds.FoodDelivery`
- OS별 전 생명주기 단계 대장: `OperatingSystemLifecycleCatalog`
- 공통 배차 엔진 계열: `EngineFamilyIds.TransportRequestDispatch`
- 화물 구현 엔진: `EngineImplementationIds.CargoYongdalDispatch`
- 음식 구현 엔진: `EngineImplementationIds.FoodDeliveryDispatch`
- OS별 구현 선택 대장: `OperatingSystemEngineCatalog`
- 실행 시 OS별 Primary 해석: `운영체제배차EngineCatalog`
- 명시적 인계 계약·원장·조율: `운영체제업무인계Contracts`, `운영체제업무인계`, `운영체제업무인계Coordinator`
- 인계 재처리 기반: `운영체제업무인계_Outbox`
- 공통 배차 정책 정본: `PLAN-OPERATIONS-DISPATCH-CORE`

기존 식별자와 구현 엔진은 삭제하거나 이름을 한꺼번에 바꾸지 않았다. 배차 후보 선정은 업무 유형을 먼저 소유 OS로 해석하고 그 OS의 활성 `Primary`만 고른다. 화물 OS가 음식배달 구현을, 음식배달 OS가 화물 구현을 자동 대체품으로 사용하는 과거의 family 전체 노출은 제거했다. 화물 배차 엔진이 직접 하던 운송 의뢰 DB 조회도 후보 선정 Application Service로 옮겨, 엔진에는 필요한 운송 방식만 불변 입력 맥락으로 전달한다.

인계 기반은 출발·도착 OS, 출발 업무 revision, 최소 JSON 상태 사본, 계약 판본, 만료시각과 공개 범위를 MySQL 원장에 보존한다. 도착 OS만 `수락·거절·보류`할 수 있고, 수락 때만 현재 책임 OS가 도착 OS로 바뀐다. 생성과 결정 요청은 클라이언트 요청 ID·revision·고유 인덱스·동시성 토큰으로 멱등 처리하며 각 변화는 같은 저장 단위의 Outbox에 남긴다. `20260911062517_AddOperatingSystemHandoffLedger`와 `20260911093054_AddFoodDeliveryCompletedWorldProjection`은 일회용 로컬 MySQL DB에서 처음부터 적용해 검증한 뒤 DB를 삭제했다. 공유 개발 DB와 운영 DB에는 적용하지 않았다.

버전 업무 조회는 화물·음식 OS의 순서 있는 생명주기와 각 OS에 실제로 등록된 Engine Catalog 항목을 제공한다. 아직 구현이 없는 논리 엔진은 `Declared`로 유지하고 다른 OS 구현을 끌어와 `Active`로 보이지 않는다.

이번 판본은 서버·계약·영속 기반에 더해 기존 창고 출고 완료 흐름이 이미 생성된 화물 운송 의뢰를 화물운송 OS에 인계하고 명시적으로 수락하는 첫 수직 연결까지 포함한다. 업무별 완료는 공통 `운영업무완료증명`으로 순서·필수 단계·결과를 확인하고, 음식 배달 완료·창고 작업·화물 인수 완료를 개인정보가 제거된 지역 장면 조회로 조합한다. 새 운송 의뢰나 배차를 생성하지 않으며, 마트·음식 주문 등 다른 ProcessManager 연결과 Outbox 소비자가 상대 OS UseCase를 호출하는 비동기 수직 연결, 자동 fallback, 모바일·Web 화면과 운영 활성화는 포함하지 않는다.

## 구현 검증

- 이번 최종 범위의 지역 장면·기능 관문·창고 인계·음식 완료 집중 회귀: `14/14` 통과
- `Ssalddel/Ssalddel.csproj` build: 오류 `0`, 기존 nullable 경고 `2`
- EF migration/model 대조: `No changes have been made to the model since the last migration.`
- 일회용 로컬 MySQL: 전체 주 DB migration 적용, 음식 완료 투영의 재시작 경계·멱등·만료 정리 시험 통과, 검증 뒤 DB 삭제
- 실제 인증 HTTP: 전용 관찰 기능을 켠 `Ssalddel`에서 미인증 지역 장면 `401`, 인증된 지역 장면·음식 완료 조회 `200`, 자료원 실패 `0`
- 범위 Fast·Task: 공백 검사와 Simulation·Unity 코드 지도는 통과했다. 이후 이번 경로 밖의 기존 `EVIDENCE001` 8건에서 중단되어 전체 성공은 아니다.
- 미실행: 공유 개발·운영 MySQL migration 적용, 실제 Outbox worker 연속 운전, Redis 연동, 모바일·Web·Unity 제품 실행과 운영 활성화

## 미정

- 마트·창고·복합 배송별로 어느 OS가 첫 책임과 고객 최종 결과 통지를 맡을지
- OS별 ProcessManager와 WorkflowCoordinator의 정확한 경계 및 기존 UseCase 재사용표
- 주 엔진 오류·시간 초과·근거 부족에서 승인된 안전 fallback을 자동 실행할 정확한 조건
- Engine Catalog의 승인 역할, 단계적 활성화 비율과 이전 판본 복귀 기준
- 운영자 화면에서 OS 전체 상태와 각 엔진 판단을 보여 줄 범위
- OS 단위 장애 격리, 재처리, 관측 지표와 운영 활성화 관문

## 다음 질문 하나

주 엔진이 오류나 시간 초과로 결과를 만들지 못했을 때 사전에 승인된 안전 fallback이 있으면 자동으로 한 번 실행하고, 없거나 fallback도 실패하면 업무를 보류·수동 검토로 보낼까? 추천은 이 제한적 자동 fallback이다. 기사·주문·화물 흐름을 불필요하게 끊지 않으면서도 승인되지 않은 로직으로 자동 전환하지 않는다. 대가는 fallback 결과임을 감사 원장과 운영 화면에서 구분하고 중복 제안·중복 상태 변경을 막아야 한다는 점이다.
