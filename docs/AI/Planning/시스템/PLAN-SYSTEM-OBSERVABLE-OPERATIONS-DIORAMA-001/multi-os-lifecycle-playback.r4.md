# [기획 · 시스템·운영표현 · PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001 · 승인 r4]

상태: `Approved / ImplementationAuthorized / VerificationSamplePipeline / SyntheticRestaurantFixture / MultiOsLifecyclePlayback / OperationalEffectsDisabled / FirstWaveFourSpatialOsConfirmed / WarehouseLifecycleConfirmed`

- 이전 표현 기준: [사가정역 생활 디오라마 r3](diorama.r3.md)
- 음식점 자료 입력: [음식점 아이콘·운영 생명주기 검증 제안 r3](../../공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/restaurant-icons-operational-lifecycle-validation-proposal.r3.md)
- OS별 서버 우선 기준: [Unity OS 관찰 모듈 상향식 계획 r4](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/unity-os-observation-modules.r4.md)

## 이번 승인

사용자는 실제 음식점 관측을 알 수 있는 12개 아이콘에 업종별 샘플 메뉴를 붙이되 `샘플 데이터`, `검토용`, `비배포`, `실제 주문 불가`임을 명확히 표시하고, 음식배달뿐 아니라 화물·창고·마트 등 OS별 생명주기가 사가정 디오라마 안에서 순서대로 표현되는지 검증하는 방향을 승인했다.

첫 구현 묶음은 `FoodDeliveryOS`, `DomesticCargoTransportOS`, `WarehouseCommerceFulfillmentOS`, `SsalddelMartUrbanLogisticsOS` 네 공간형 OS로 확정한다. 각 OS는 정상 한 사례와 회복 한 사례를 가진다. `ShipperTransportManagementOS`를 포함한 나머지 6개 OS는 각자의 지역 결속·수직 검증 범위가 별도로 확정될 때까지 진단 목록에만 남기고 단계·객체를 임의로 만들지 않는다.

샘플 자료는 화면을 살아 있게 만드는 도구이자 자동 검증 입력이다. 단계별 상태 사본의 `SourceKindCode`는 `VerificationSample`이고, 음식점 프로필·메뉴와 위치 결속을 담는 Fixture 묶음만 `SyntheticFixture`다. 실제 업체의 현재 메뉴·가격·제휴·주문 가능 상태를 주장하지 않으며, Unity 표현이 운영 서버의 상태를 진행시키지 않는다.

## 목표 장면

사가정역 1km 디오라마에서 관찰자는 다음을 함께 볼 수 있다.

- 실제 공개자료에 근거한 음식점 후보 12곳의 정적 카테고리 아이콘
- 음식점별 업종에 맞춰 결정적으로 생성한 샘플 메뉴 카드
- 샘플 주문의 접수·조리·배차·픽업·신호대기·전달·수령·기사 귀환
- 샘플 화물의 의뢰·배차·상차·운송·하차·인수와 중단·회복
- 샘플 창고의 입고·검수·적치·피킹·포장과 수량 이상 보류·회복
- 샘플 마트의 입고·재고·주문할당·피킹·포장·라스트마일 인계와 품절·재시도

전체 세계는 계속 진행되고 사용자가 한 업무를 선택하면 카메라와 카드만 그 업무를 따라간다. 선택하지 않은 OS도 멈추지 않는다. 새 공식 Scene은 만들지 않고 canonical `SimulationWorldShell`을 유지한다.

## 가장 중요한 판정

현 r4 구현은 600초 실행 안에 네 OS의 `정상 1 + 회복 1`, 총 8개 사례와 77개 단계를 동결했다. 첫 단계 offset은 5초, 마지막 단계 offset은 565초다. 각 사례는 같은 `WorkStableId`와 `SnapshotStableId`를 유지하고 단계마다 revision을 1씩 올린다.

| OS | 사례 수 | 단계 수 | 게시 offset 범위 |
| --- | ---: | ---: | ---: |
| `FoodDeliveryOS` | 2 | 22 (`9 + 13`) | 5~160초 |
| `DomesticCargoTransportOS` | 2 | 19 (`8 + 11`) | 110~300초 |
| `WarehouseCommerceFulfillmentOS` | 2 | 18 (`8 + 10`) | 250~425초 |
| `SsalddelMartUrbanLogisticsOS` | 2 | 18 (`8 + 10`) | 390~565초 |
| 합계 | 8 | 77 | 5~565초 |

기존 여섯 최종 결과 표본과 그 화면 증거는 호환 이력으로 보존하지만 r4의 단계 재생 증거로 승격하지 않는다. r4 코드와 집중 자동 시험은 단계 원장·Outbox·MongoDB revision 정책을 검증하며, 실제 격리 MySQL·MongoDB·Redis 실행과 Play Mode·Game View는 별도 실행 증거가 있어야 완료로 보고한다.

특히 이 `ObservableOperations` Runner는 격리된 `VerificationSample` 투영 파이프라인이다. 실제 음식·화물·창고·마트의 운영 Controller/UseCase/Command를 호출해 업무를 성공시킨 것이 아니며, 실제 운영 workflow의 권한·전이·저장·회복 성공을 대신하지 않는다.

## 실제 음식점과 샘플 메뉴의 분리

### 원장 구조

```text
실제 음식점 공개 관측 12곳                 격리 검증 DB의 샘플 주문 입력
PublicBusinessObservation                  음식점공개프로필
  └─ BuildingAssignment                    └─ 음식점메뉴
       └─ LocalPrivateReview 아이콘              └─ [샘플] 메뉴·가격·조리시간
                    │                                 │
                    └── 관찰운영검증_Fixture결속 ─────┘
                         LocationPresentationAnchorOnly
                         NoBusinessAffiliation
                         OperationalEffectsAllowed=false
```

새 음식점 master나 실제 메뉴 관측 원장을 만들지 않는다. 실제 주문 생명주기 입력에는 기존 `음식점공개프로필`과 `음식점메뉴`를 재사용하되, `Development + Simulation + 전용 컨테이너`의 격리 검증 DB에만 샘플 행을 생성한다.

운영 프로필·메뉴 모델에는 합성 출처 필드가 없으므로 일반 개발 DB나 운영 DB에 실제 상호와 가상 메뉴를 바로 넣지 않는다. 새로 추가할 것은 샘플임을 증명하고 실제 관측 위치와 느슨하게 연결하는 검증 전용 metadata 원장이다.

### 검증 전용 Fixture 원장

`관찰운영검증_Fixture묶음` 구현 필드:

- `FixturePackStableId`, `FixtureRevision`, `FixtureHashSha256`, `DeterministicSeed`
- `SourceKindCode=SyntheticFixture`
- `EnvironmentCode=DevelopmentSimulation`
- `DistributionApproved=false`, `OperationalEffectsAllowed=false`
- 생성 시각, 만료 시각, 생성기 판본

현행 묶음 계보는 `FixtureRevision=sagajeong-restaurant-fixture.v2`, `GeneratorVersion=observable-operations-fixture.v2`다. 기존 r1 묶음·결속·프로필·메뉴를 덮어쓰지 않고 v2 안정 ID로 새 묶음을 추가한다.

`관찰운영검증_Fixture결속` 구현 필드:

- `FixturePackStableId`, `FixtureObjectStableId`, `ObjectKindCode`
- 기존 `음식점공개프로필Id`, 선택적인 `음식점메뉴Id`
- 선택적인 `PublicBusinessObservationStableId`, `BuildingStableId`, `SemanticPlaceStableId`
- `BindingPurposeCode=LocationPresentationAnchorOnly`
- `AffiliationCode=NoBusinessAffiliation`
- `DisplayDisclosureCode=SampleMenuNotActualOffering`
- `ScenarioOrderAllowed=true`, `ActualOrderAllowed=false`, `DistributionApproved=false`
- revision, 연결 hash, 검토 상태

묶음·결속을 다시 사용할 때 저장된 hash 문자열만 신뢰하지 않는다. 현재 안전 필드 전체를 기대값과 비교하고 결속 hash를 다시 계산하며, 주문·배포·검토 상태가 바뀌었으면 재사용을 거절한다.

이 결속은 실제 음식점이 가상 주문을 받았다는 관계가 아니다. 공개 관측 아이콘의 위치를 로컬 표현 anchor로 빌려 쓰는 관계이며 카드에는 항상 다음 문구를 표시한다.

> 샘플 메뉴 · 실제 판매 메뉴 아님 · 해당 업체와 제휴 관계 없음 · 로컬 검토용/비배포

### 샘플 메뉴 생성 규칙

- 실제 상호 문자열에서 대표 메뉴를 추측하지 않고 공개 업종 코드만 사용한다.
- 검증 Fixture factory가 한식·분식·카페/제과·치킨·구이·국/탕·기타 업종 템플릿을 소유하며 주점·유흥 후보는 제외한다.
- 음식점당 2~3개 메뉴를 배정하고 입력 hash·directory 안정 ID·생성기 판본·고정 seed로 프로필/메뉴 ID, 표시 순서, 합성 가격과 조리시간을 결정한다.
- 메뉴명에는 `샘플` 의미를 넣고 가격·조리시간에도 `SyntheticValue=true`를 보존한다.
- 실제 메뉴 이미지·로고·리뷰·별점은 사용하지 않고 공통 카테고리 아이콘만 쓴다.
- 같은 seed와 입력으로 같은 메뉴 집합과 hash가 생성되어야 한다.
- 12곳의 비공개 샘플 프로필·메뉴는 Fixture 결속을 검증하기 위해 저장한다. r4 timeline은 이 프로필로 실제 주문을 발생시키지 않는 독립 `VerificationSample`이며, 음식점 위치와는 안정 ID만 결속한다.

## OS 현행 준비도

공통 OS ID는 10개이며 Warehouse 9단계가 추가되어 공통 생명주기 단계가 정의된 OS는 현재 5개다. r4 첫 묶음은 그중 음식·화물·창고·마트 네 OS만 사용하며, 모든 OS를 임의 단계로 채워 한꺼번에 움직이지 않는다.

| OS | 공통 단계 정의 | 현재 지역 투영 | r4 첫 조치 |
| --- | --- | --- | --- |
| `FoodDeliveryOS` | 8단계, 실제 주문 전이와 회복 있음 | 수령확인 완료만 | 정상·중단 회복 전체 단계 첫 수직 검증 |
| `DomesticCargoTransportOS` | 8단계 | 최근 인수완료·창고 인계 중심 | 정상·비정상 회복 단계 두 번째 검증 |
| `SsalddelMartUrbanLogisticsOS` | 8단계 | 거점 상태와 합성 회복 완료 중심 | 피킹·포장·Food OS 인계 단계 검증 |
| `ShipperTransportManagementOS` | 8단계 | 사가정 장면 결속 없음 | 화물 OS 인계 관계가 안정된 뒤 2차 |
| `WarehouseCommerceFulfillmentOS` | 공통 9단계; 정상 8단계·수량 불일치 회복 10단계 | task/actor 일반 표식 | 확정 경로의 단계별 revision 검증 |
| `GroupPurchaseDemandOS` | 공통 lifecycle 없음 | 없음 | 후속 업무망 기획 |
| `GroupPurchaseImportOS` | 공통 lifecycle 없음 | 없음 | 후속 업무망 기획 |
| `CommunityTrustOS` | 공통 lifecycle 없음 | 없음 | 공간 이동보다 패널형 표현 검토 |
| `PlatformOperationsOS` | 공통 lifecycle 없음 | 없음 | 관리자 진단이며 일반 NPC화 금지 |
| `EducationFieldExperienceOS` | 공통 lifecycle 없음 | 없음 | 별도 교육 경험 기획 뒤 검토 |

창고 공통 lifecycle은 `warehouse.inbound-plan → warehouse.receiving → warehouse.inspection → warehouse.put-away-inventory → warehouse.outbound-allocation → warehouse.picking → warehouse.packing → warehouse.outbound-handoff`의 정상 8단계와 `warehouse.exception-recovery`를 포함한 9개 ID로 확정됐다. 수량 불일치 회복 사례는 검수에서 이상을 발견한 뒤 `exception-recovery → inspection`으로 재계수하고 적치부터 출고 인계까지 재개하는 총 10단계다.

## r4 단계 자료 구조

기존 `관찰운영검증_실행`, `관찰운영검증_사례`, `관찰운영검증_Outbox`를 보존한다. 사례 한 행에 최종 단계를 넣는 구조를 사례 header와 단계 원장으로 분리한다.

```text
관찰운영검증_실행
└─ 관찰운영검증_사례                  # OS·정상/회복·시작/종료 요약
   └─ 관찰운영검증_단계               # 같은 WorkStableId의 revision timeline
      └─ 관찰운영검증_Outbox
         └─ MongoDB 지역 상태 사본
            └─ GET scene-snapshots v2
               └─ Unity Interpreter → OS Router → Presenter
```

`관찰운영검증_단계`의 최소 필드는 다음과 같다.

- 복합 키: `RunStableId + CaseCode + StepSequence`
- `OperatingSystemId`, `WorkStableId`, `LifecycleStageCode`, `AttentionStateCode`
- `ScheduledOffsetMilliseconds`, `OccurredAtUtc`, `Revision`
- `ObjectKindCode`, `SemanticPlaceStableId`, `RelationStableIdsJson`
- `ExpectedStateCode`, `FailureOrRecoveryCode`, `ReturnStateCode`
- `SourceKindCode=VerificationSample`, `FixtureRevision`, `StepHashSha256`

단계가 바뀔 때 새 GameObject를 생성하지 않고 같은 `SnapshotStableId`의 높은 revision을 게시한다. 낮은 revision과 같은 revision의 다른 내용은 거절한다. 완료·취소·철회 뒤에는 tombstone 또는 TTL 만료로 화면에서 제거한다.

검증 schema v2는 기존 전용 volume의 `관찰운영검증_실행`·`관찰운영검증_사례`·`관찰운영검증_Outbox`를 보존하고 단계·Fixture 묶음·Fixture 결속 표와 schema 판본 행을 멱등하게 추가한다. 코드상 additive upgrade와 독립 재조회 시험은 마련됐으며, 실제 기존 MySQL volume 재기동 결과는 별도 실행 증거로 남긴다.

## OS별 표본 묶음

첫 공간형 OS 묶음은 각 OS마다 `정상 1 + 회복 1`을 가진 8개 사례로 확장한다.

### 음식배달

- 정상: 주문 → 음식점 수락 → 조리 → 픽업대기 → 기사배정 → 픽업 → 전달 → 수령확인 → 기사 귀환
- 회복: 픽업 뒤 기사 중단 → 재조리 → 재배차 → 재픽업 → 전달 → 수령확인 → 기사 귀환
- 표현: 음식점 조리 pulse, 픽업 대기, 오토바이/도보, 신호대기, 고객 전달, 대기점 귀환

### 화물운송

- 정상: 의뢰 → 조건 합의 → 배차 → 상차 → 운송 → 하차 → 인수 → 증빙 반환
- 회복: 시간 충돌 또는 운행 중단 → 보호 보류 → 후속 예약 해제 → 재계획/재배차 → 인수
- 표현: 상차장·화물차·화물 묶음·경로·하차장과 보류/회복 표식

### 창고

- 정상 8단계: 입고 계획 → 입고 → 검수 → 적치·재고 → 출고 할당 → 피킹 → 포장 → 출고 인계
- 수량 불일치 회복 10단계: 입고 계획 → 입고 → 불일치 발견 → 단위 보호 보류 → 재검수·재계수 → 적치·재고 → 출고 할당 → 피킹 → 포장 → 출고 인계
- 표현: 입고구역·검수대·보호 보류 위치·적재 위치·피킹 Actor·팔레트/상자
- 공통 단계 ID와 두 경로는 확정됐으며 더 이상 r4 구현 차단 항목이 아니다.

### 마트·도심물류

- 정상: 공급계약 이용 → 입고 발주 → 입고·검수 → 적재·재고 → 주문할당 → 피킹·포장 → Food OS 인계 → 완료
- 회복: 품절 또는 후속 투영 실패 → 대체/보류 → 재시도 → 승인된 자식 업무 인계 → 완료
- 표현: 마트·진열/포장 구역·주문 묶음·라스트마일 인계선

OS 간 인계는 같은 업무로 합치지 않는다. 예를 들어 마트가 피킹을 끝낸 결과와 Food OS의 배달 자식 업무는 서로 다른 `WorkStableId`이고 안정 인계 ID로만 연결한다.

## 두 종류의 검증을 분리한다

샘플 단계가 화면에서 순서대로 바뀌는 것만으로 서버 OS가 실제로 동작했다고 선언하지 않는다.

### A. 서버 OS 수직 검증

각 OS의 실제 Controller/UseCase/Command를 격리 DB에서 호출해 권한, 허용 전이, 원장 revision, Event/Outbox, 재시작 재조회, 멱등, 실패·회복을 검증한다. 이 결과에서 비식별 지역 상태 사본을 생성한다.

- 음식배달의 실제 역할 API 통과 증거는 별도 격리 수직 검증에서 확인하며, 현 `ObservableOperations` Runner 결과로 대체하지 않는다.
- 화물은 운영 전이와 Simulation Save/Replay 증거를 구분하고, 운영 서버 수직 시험이 닫힌 단계만 투영한다.
- 창고는 확정된 공통 9단계를 실제 창고 Controller/UseCase 전이와 별도로 결속·검증한다.
- 마트는 비구속 주문 의향, 피킹 작업, Food OS 인계를 서로 다른 권위로 보존한다.

### B. 디오라마 표현 검증

동결된 `VerificationSample` timeline을 Unity가 읽어 같은 안정 ID의 상태·위치·주의·회복 cue를 바꾸는지 확인한다. 음식점 Fixture 묶음만 `SyntheticFixture`다. 이 검증은 GameObject, 카메라, 표시 성능과 정리를 증명하지만 실제 운영 Controller/UseCase workflow의 성공을 대신하지 않는다.

`OperationalProjection`과 `VerificationSample`은 같은 화면 어휘를 사용할 수 있어도 자동 fallback하지 않는다. 어느 자료원을 본 것인지 화면과 증거 manifest에 기록한다.

## Unity 조립

현재 `운영관찰WorldView`의 OS별 Root, 안정 ID 객체 재사용, TTL 제거, 낮은 revision 동결, 복구와 사가정 전용 카메라를 재사용한다. generic 완료 표식에 단계 의미를 억지로 넣지 않고 runtime-only 검증 계층을 추가한다.

```text
SimulationWorldShell
└─ OperationalOsWorldRoot
   ├─ GeographyRoot
   ├─ PlacesAndBusinessesRoot
   │  ├─ RestaurantIcons
   │  └─ SampleMenuCards
   ├─ OSLifecycleValidationRuntime
   │  ├─ SharedPlaceAnchors
   │  ├─ FoodDelivery
   │  ├─ DomesticCargo
   │  ├─ WarehouseFulfillment
   │  ├─ MartUrbanLogistics
   │  └─ Diagnostics
   ├─ MobilityRoot
   └─ OperationalSnapshotRoot
```

검증 Root는 `DontSaveInEditor | DontSaveInBuild`로 생성하고 Play Mode 종료 때 모두 제거한다. 음식배달 이동·저밀도 신호 교통의 경로/Actor pool은 소유권을 확인한 뒤 재사용하되, 기존 검증 카메라들이 서로를 비활성화하지 않도록 공용 capture driver 하나가 순서를 조율한다.

단계 표현은 OS별 Adapter가 소유한다.

- `Active`: 작업 cue와 현재 위치
- `Waiting`: 정지 자세와 대기 표식
- `RecoveryPending`: 보호 보류 띠와 원인 범주
- `Recovered`: 회복 행동과 재개 경로
- `Completed`: 직접 결과와 다음 인계
- `Cancelled/Expired`: 짧은 종료 표시 뒤 제거

색만으로 상태를 구분하지 않고 아이콘·문구·동작을 함께 사용한다. 빨간색을 사람의 과실 의미로 쓰지 않는다.

## 검증 순서

1. 현재 6개 최종 표본과 지도·Scene hash를 기준선으로 동결한다.
2. Fixture 묶음·결속, 12개 샘플 프로필과 메뉴를 격리 DB에 저장하고 독립 재조회·재적용 중복 0을 확인한다.
3. 사례 단계 schema와 동일 ID revision timeline, Outbox 재처리, MongoDB 재조회, Redis 일시정지·재개를 검증한다.
4. 음식배달 정상·회복을 실제 서버 수직 시험과 표현 timeline 양쪽에서 닫는다.
5. 화물 정상·회복을 같은 방식으로 닫는다.
6. 확정된 창고 9단계 계약으로 정상 8단계·수량 이상 회복 10단계의 저장·투영을 검증한다.
7. 마트 정상·회복과 Food OS 자식 업무 인계를 닫는다.
8. Unity Decoder·Interpreter·Router·OS Adapter의 schema, 낮은 revision, 동일 revision 충돌, TTL, tombstone, 부분 실패, 지역 전환·메모리 삭제를 검증한다.
9. OS 하나씩 단독 Play Mode에서 모든 단계·실패·회복·귀환을 확인한다.
10. 마지막으로 600초 통합 실행에서 저밀도로 겹쳐 진행하고 Game View와 상태 manifest를 함께 보존한다.

자동 시험은 주입 시계로 빠르게 실행할 수 있지만 최종 화면 증거는 기존 안전 조건을 유지한 실제 600초 검증 실행 한 번을 사용한다. `ReplayAllowed=false`인 상태 사본을 파일로 재생하지 않고 같은 fixture revision/seed로 새 실행을 만든다.

## Game View 증거 묶음

최소 사용자 확인 화면은 다음 세 장이다.

1. `01-overview-all-os.png`: 전체 1km, 12개 음식점 아이콘, 네 OS의 현재 진행 상태
2. `02-food-pickup-and-signal.png`: 샘플 메뉴 카드, 조리/픽업, 기사와 차량의 신호대기
3. `03-warehouse-cargo-mart-handoff.png`: 창고·화물·마트의 작업과 OS 간 인계 근접 화면

회복 판독이 세 장에서 충분하지 않으면 `04-recovery-and-return.png`를 추가한다. 각 PNG에는 별도 manifest로 run/fixture revision, OS/work/stage/revision, 지도·배치·경로 hash, 캡처 시각과 활성 GameObject 수를 연결한다. 정지 화면은 움직임을 증명하지 않으므로 Transform 변화 로그와 단계 전이 기록을 같이 보존한다.

## 완료 조건

- 12개 음식점마다 2~3개 샘플 메뉴가 결정적으로 생성·저장·독립 재조회되고 모든 카드에 실제 메뉴가 아님이 표시된다.
- 실제 관측 상호·위치와 샘플 프로필·메뉴의 원장은 분리되고 `NoBusinessAffiliation` 결속만 존재한다.
- 실제/운영 DB, 외부 HTTP, 결제·메시지·정산 효과가 발생하지 않는다.
- 첫 대상 OS마다 정상·회복 사례가 같은 `WorkStableId`의 증가 revision으로 진행된다.
- 서버 수직 검증과 Unity 표현 검증의 결과·실패·증거가 분리된다.
- 음식·화물·창고·마트의 단계, 대기, 회복, 완료, 인계와 귀환을 화면에서 구별할 수 있다.
- 낮은 revision·동일 revision 충돌·PII·미지원 OS·미결속 위치는 거부되며 다른 OS 갱신은 계속된다.
- TTL·tombstone·지역 전환·Play Mode 종료 뒤 runtime 객체와 메모리가 제거된다.
- 전체 1장·부분 2장과 필요한 회복 화면, 상태/revision/hash/Transform 기록이 같은 실행에 결속된다.
- 코드·시험, 실제 격리 DB, 실제 HTTP, Play Mode·Game View, 저장 Scene, commit·push를 각각 별도 사실로 보고한다.

## 제외

- 실제 업체의 메뉴라고 주장하는 가상 메뉴, 실제 주문 참여·제휴 표시
- 네이버·배민 화면 수집, 외부 리뷰·평점·이미지 사용
- 실제 사용자·기사·상세 주소·GPS·결제·정산·메시지
- 모든 10개 OS에 임의 lifecycle을 만들어 한꺼번에 배치
- Unity에서 운영 상태 변경, Scene별 OS 복제, 샘플 실패 시 운영 자료 fallback
- Steam 공개, 새 공식 Scene, Evidence 자동 승격

## 확정

- 음식점 12곳에는 업종 기반 샘플 메뉴를 넣되 로컬 격리 검증 자료로만 사용한다.
- 기존 음식점 공개 프로필·메뉴 테이블을 격리 DB의 주문 입력으로 재사용하고 Fixture metadata/결속만 추가한다.
- OS 생명주기는 사례별 최종 점이 아니라 같은 업무의 단계별 revision timeline으로 표현한다.
- 서버 수직 검증과 디오라마 표현 검증을 분리하고 둘 다 통과한 OS만 통합 화면에 넣는다.
- canonical `SimulationWorldShell` 한 장면에서 OS별 독립 모듈을 조립한다.
- 첫 구현은 음식·화물·창고·마트 네 OS의 정상·회복 8개 사례로 닫고, 나머지 6개 OS는 후속 생명주기 확정 뒤 추가한다.

## 미정

- 실제 음식점 후보 12곳의 최종 선정과 건물 anchor 사람 검토
- 실제 Game View에서 확인할 네 OS의 화면 동시 활성 밀도와 후속 속도 조정

## 다음 질문 하나

첫 구현과 검증을 마친 뒤, 네 OS의 화면 밀도와 진행 속도 중 어느 쪽을 먼저 조정할지 실제 Game View 증거를 보고 정한다. 이 후속 질문은 현재 구현을 차단하지 않는다.
