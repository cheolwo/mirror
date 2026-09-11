# Unity 실시간 정착지 경제·영지 경영·분쟁 Simulation 재정렬 제안서

> 상태: 부분 구현 — Phase A, `HARVEST-IMPACT-1 + STORAGE-1`, 읽기 전용 `WORLD-SHELL-0 + SETTLEMENT-SCENE-0` 완료; 다음 서버 권위 Gate는 `SETTLEMENT-ECONOMY-1`
>
> 재작성일: 2026-08-10
>
> 직접 기반: [Unity canonical 품목별 Farm→Town·City 생애주기](UnityCanonicalProductFarmToMarketLifecycleProposal.md)
>
> Simulation 서버 경계: [D-027 운영 서버와 게임 Simulation 서버를 물리 분리한다](../AI/DECISIONS.md#d-027-운영-서버와-게임-simulation-서버를-물리-분리한다)
>
> World 권위 경계: [Unity World·원장 투영 아키텍처](UnityWorldLedgerProjectionArchitectureProposal.md)
>
> 이 문서의 책임: 현재 구현된 농장·Lot·판로·Cargo·Hub·마트 구조를 버리지 않고, 하나의 연속 시간 속 정착지 경제와 이후 군량·침공·공성 인과로 확장하는 실제 구현 순서와 완료 Gate를 정의한다.

## 1. 최종 제안 요약

프로젝트를 별개의 전쟁 게임으로 갈아엎지 않는다. 현재 강점인 canonical 상품, HarvestLot, 판로 결정, PackageLot, Cargo, 검수 Lot, 재고, 시장, 공급계약과 source lineage를 **정착지 경제의 사실 원장**으로 확장한다.

목표 경험은 다음 한 문장으로 정의한다.

> 감자 300kg의 판로 선택이 며칠 뒤 정착지의 재정·노동·시장 공급·식량 예비를 바꾸고, 그 결과가 주민 생활과 군량, 침공 대응과 공성 지속 가능 시간까지 이어지는 실시간 정착지 경영 Simulation.

다만 현재 다음 단계는 `EXPORT-1`이나 전투 Scene이 아니다. 서버에는 공통 `세력·영지·정착지·세계시간·결정→작업→효과` 문맥과 판로 영향 후보까지 마련됐지만, 실제 경제 allocation과 Unity의 장기 WorldShell·adapter는 아직 연결되지 않았다. 이 상태에서 분기만 늘리면 농장 전용 fixture와 개별 Scene이 깊어지고 정착지 전체 인과는 생기지 않는다.

따라서 우선순위를 다음과 같이 바꾼다.

```text
현재 완료된 좁은 slice
FARM-3 → HARVEST-CHOICE-1 → COOP-1 / DIRECT-1
                         └→ CARGO-1 → JOURNEY-1 → HUB-1/2

재정렬된 다음 순서
SIM-WORLD-0
  → DECISION-WORK-0
  → SAVE-REPLAY-0
  → SETTLEMENT-CORE-1
  → HARVEST-IMPACT-1 + STORAGE-1
  → SETTLEMENT-ECONOMY-1
  → BRANCH-ADAPTER-1
  → SETTLEMENT-INTERACTION-0
  → LOGISTICS-MOVEMENT-1
  → MARKET-CONSUMPTION-1
  → EXPORT-1
  → FOOD-SECURITY-1
  → ARMY-SUPPLY-1
  → INVASION-1
  → DEFENSE-DECISION-1
  → CONFLICT-VIEW-1
```

여기에 서버 권위를 대체하지 않는 Presentation Foundation Track을 둔다.

```text
WORLD-SHELL-0
  → SETTLEMENT-SCENE-0
  → [SETTLEMENT-ECONOMY-1로 복귀]
  → WORLD-SETTLEMENT-NAV-0
  → BRANCH-ADAPTER-1
  → SETTLEMENT-VISUAL-BASE-0
  → SETTLEMENT-INTERACTION-0
```

Shell과 첫 정착지 blockout은 같은 Simulation snapshot을 여러 관찰 규모에서 유지하기 위한 읽기 전용 선행 작업이다. 세부 범위와 중단 Gate는 [Unity Simulation World Shell·정착지 Scene 기반 재정렬 제안서](UnityWorldShellSettlementSceneFoundationProposal.md)를 따른다.

첫 번째 완성 목표는 전쟁 자체가 아니라 **정착지 경제 폐루프**다.

```text
감자 생산
  → 판로 선택
  → 작업 예약
  → 세계시간 진행
  → 노동·재정·재고·시장 공급 변화
  → 주민·주둔군 소비
  → 식량 예비 일수 변화
  → 동일 결정의 인과 추적
```

이 폐루프가 닫힌 뒤에만 군단과 침공을 붙인다. 그래야 전쟁이 별도 미니게임이 아니라 기존 경제의 결과가 된다.

## 2. 현재 프로젝트 상황에 대한 사실 확인

### 2.1 실제로 구현된 기반

| 영역 | 현재 사실 | 재사용 방식 |
| --- | --- | --- |
| canonical 상품 | 서버 상품 identity 60개, Unity visual mapping 28개 | 정착지 자원·식량 Lot의 상품 identity로 유지 |
| FARM-2·FARM-3 | 6×6 밭갈이, 감자 파종·생육·수확, 300kg HarvestLot | 첫 정착지 농장 생산 원장으로 유지 |
| HARVEST-CHOICE-1 | 조합·온라인 직판·수출대행 준비 3개 판로 결정 | 공통 Decision 구조의 첫 migration 대상 |
| COOP-1 | 조합 인수 Lot과 CARGO-1 포장 검토 후보 | 공동 집하 경로의 adapter로 유지 |
| DIRECT-1 | 5kg×60 소포장 Lot과 비공개 등록 초안 | 직접 소비자 판매 경로의 adapter로 유지 |
| CARGO-1 | 20kg×15 PackageLot, 300kg Cargo, 용량·질량 보존 | 정착지 간 물류와 장차 군량 수송의 기반 |
| JOURNEY-1 | Dispatch와 route Tick, Farm→Hub 도착 | 공통 세계시간·경로 엔진으로 이관할 실행 명세 |
| HUB-1·HUB-2 | 288kg 합격, 12kg 손실, City outbound 후보 | 검수·손실·재분배의 공통 Lot 패턴 |
| Urban Market | 재고·진열·수요·공급계약 Simulation 기반 | 정착지 시장·소비·가격 영향의 하류 시스템 |
| Simulation 서버 | 별도 Contracts·Domain·Server, session·멱등 Tick·revision | 게임 세계의 장기 권위가 될 기반 |
| Unity World | Farm·Town·Hub·City Region, 3/4 Game View와 카드 | 정착지 권역과 세계 지도 Presentation의 출발점 |

이 항목들은 폐기 대상이 아니다. 다만 현재 다수는 `Ssalddel.Unity`의 독립 fixture와 별도 Scene에서 증명된 좁은 slice다. 아직 하나의 세계 snapshot과 command log 안에서 함께 실행되는 것은 아니다.

### 2.2 아직 구현되지 않은 핵심

- 세력, 영지, 정착지, 구역, 시설의 canonical Simulation 계약
- 판로 결정에 고정되는 `FactionStableId`, `TerritoryStableId`, `SettlementStableId`
- 모든 경제·물류 작업이 공유하는 세계시간과 예약 사건
- 공통 Decision→Task→Effect 원장과 상태 전이
- 정착지 재정, 노동 capacity, 시장 공급, 식량 예비, 주둔군 군량
- 보관 판로와 보관 감모·capacity
- 판로별 예상 영향과 실제 적용 결과의 분리
- 정착지 population cohort 소비
- save/replay 가능한 durable Simulation snapshot과 command log
- 군단, 보급, 침공, 방어, 전투 결과

### 2.3 현재 계획을 그대로 계속할 때의 문제

`EXPORT-1 → DIRECT-2 → COOP-2 → JOURNEY-2`만 연속 구현하면 각 분기의 화면과 Lot은 늘어나지만 다음 질문에는 답하지 못한다.

- 이 감자 300kg은 어느 정착지의 재고에서 빠졌는가?
- 포장에 며칠과 몇 단위 노동이 들었는가?
- 같은 Tick에 성벽 보수와 농산물 포장이 노동 capacity를 어떻게 경쟁했는가?
- 직판 준비 뒤 시장 공급과 식량 예비가 실제로 얼마나 달라졌는가?
- Cargo가 떠난 동안 주민과 주둔군은 무엇을 소비했는가?
- 이 선택이 공성 지속 가능 일수에 어떻게 연결됐는가?

그래서 **분기 수보다 공통 세계 문맥을 먼저 구현**한다.

## 3. 제품·Simulation·시대 설정 경계

### 3.1 기존 운영 제품을 전쟁 게임으로 바꾸지 않는다

Ssalddel 운영 원장과 0.0 커뮤니티·공공데이터 제품은 계속 현실 사용자, 동의, 계약, 발주, 재고, 결제와 공개 정보의 권위다. 영지·군단·침공은 같은 `Ssalddel` 호스트 안의 Simulation Core가 소유하는 별도 게임 세계다.

```text
Operational World
  실제 사용자·조직·동의·주문·계약·입고·재고·결제

Simulation World
  가상 세력·영지·정착지·인구·재정·판로·군단·침공
```

두 세계는 DB와 entity를 공유하지 않는다. 현실 공공데이터를 시나리오 초기값 근거로 사용할 수는 있지만, 관측 사실과 게임 파생 결과를 source와 mode로 분리한다.

### 3.2 시스템은 시대 중립, 용어와 외형은 시나리오가 소유한다

첨부 방향은 온라인 마켓·수출대행과 성·공성·영주를 한 세계에 함께 둔다. 이를 코드에 그대로 박으면 설정과 재사용성이 충돌한다.

도메인 core는 다음처럼 시대 중립적인 의미를 사용한다.

| Core 의미 | 현재 농업·상거래 시나리오 | 영지 경영 시나리오 표현 |
| --- | --- | --- |
| `DirectConsumerChannel` | 온라인 직접 판매 | 직거래 장터·주문 게시판 |
| `CollectiveShipmentChannel` | 생산자 조합 출하 | 농민 조합·공동 집하 |
| `ExternalMarketChannel` | 수출대행 | 외부 권역 교역·항구 상단 |
| `ReserveStorageChannel` | 저온·일반 창고 보관 | 영지 식량고 비축 |
| `SettlementAuthority` | 도시 관리자 | 영주·총독·시장 |
| `DefenseForce` | 지역 방위 조직 | 수비대·민병대 |

기존 `DirectOnlineSale`, `CooperativeShipment`, `ExportAgent` stable code는 호환을 위해 즉시 이름을 바꾸지 않는다. 새 공통 분류와 scenario presentation profile을 추가하고 adapter에서 기존 code를 mapping한다.

첫 playable은 **시스템 검증용 국경 정착지**로 표현하되, 특정 역사·국가·실존 집단을 모사하지 않는다. 전투 미술과 lore보다 경제 인과가 우선이다.

## 4. 목표 플레이 구조

### 4.1 세 관찰 규모

```text
World Map
  └─ Territory
      └─ Settlement
          ├─ Inner District
          ├─ Town District
          ├─ Farm District
          ├─ Market
          ├─ Storage
          ├─ Logistics Hub
          ├─ Workshop
          ├─ Barracks
          ├─ Gate / Wall
          └─ Residential District
```

- 세계 지도: 정착지, 도로, Cargo, 군단, 위협과 교역망을 본다.
- 정착지 내부: 농장·시장·창고·병영·주거지의 상태와 작업을 본다.
- 분쟁 현장: 성문·성벽·창고·보급로 같은 목표와 부대 상태를 본다.

세 규모는 별도 게임 상태가 아니다. 같은 Simulation snapshot을 서로 다른 Perspective로 투영한다. Scene 전환이 Tick이나 재고를 초기화하지 않는다.

### 4.2 기본 사용자 루프

```text
세계 상태 관찰
  → Lot·시설·군단·사건 선택
  → 판단 카드 열기
  → 비용·노동·기간·위험·예상 효과 Preview
  → 명시적 Confirm
  → Decision 기록
  → Task 예약
  → 세계시간 진행
  → Task 진행·완료·실패
  → Effect 적용
  → 정착지·시장·군량 상태 변화
  → 결과와 원인 Decision 재조회
```

클릭, NPC 도착, 차량 animation, 전투 이펙트는 결과를 만들지 않는다. Simulation Command와 Tick만 상태를 바꾼다.

## 5. 권위 아키텍처와 점진 이관

### 5.1 최종 권위

```text
Ssalddel.Simulation.Contracts
  공통 World·Decision·Task·Effect·Projection 계약
        ↓
Ssalddel.Simulation.Domain
  deterministic Tick·경제·물류·군량·분쟁 규칙
        ↓
Ssalddel.Simulation.Hosting
  session·revision·command idempotency·snapshot·replay 권위
        ↓ API Snapshot / Delta
Ssalddel.Unity
  mirror model·validator·interpreter·presentation model
        ↓
Unity project
  Scene·VisualRoot·card·animation·FX
```

`Ssalddel.Unity/Runtime/Farm`에 구현된 FARM-3·판로·COOP-1·DIRECT-1·CARGO-1 엔진은 현재 검증된 실행 명세다. 이를 한 번에 삭제하거나 이동하지 않는다.

1. Simulation.Contracts에 공통 세계 계약을 추가한다.
2. Simulation.Domain에 새 authoritative aggregate를 구현한다.
3. 기존 Unity fixture 결과와 동일한지 contract test로 비교한다.
4. Unity presenter를 server/mirror adapter로 전환한다.
5. 새 경로가 Game View까지 검증된 뒤 중복 authority를 제거한다.

새 root-level 경제·군사 engine을 Unity assembly에 계속 추가하지 않는다. 장기 저장과 세계 전체 인과의 권위는 Simulation 서버에 둔다.

### 5.2 저장과 replay

현재 Simulation store는 process-local in-memory다. 첫 경제 playable은 짧은 fixture로 가능하지만, 연속 시간·침공·공성을 구현하기 전 다음이 필요하다.

- session snapshot durable store
- append-only command log 또는 동일한 replay 입력
- `ScenarioDataRevision`, `RuleRevision`, `Seed`, `CurrentTick`, `Revision`
- save schema version과 migration 정책
- 같은 seed·scenario·command log의 결정적 결과
- 실패한 Tick의 원자성
- snapshot 복원 뒤 stable ID와 예약 Task 보존

## 6. World Context와 시간 모델

### 6.1 최소 World identity

```text
SimulationSessionStableId
ScenarioStableId
FactionStableId
TerritoryStableId
SettlementStableId
DistrictStableId
FacilityStableId
WorldTick
WorldRevision
```

첫 scenario는 한 세력, 한 영지, 한 정착지로 시작한다. 다세력·다영지를 처음부터 구현하지 않지만 ID와 참조는 생략하지 않는다.

### 6.2 시간은 세 가지를 계속 분리한다

| 시간 | 책임 |
| --- | --- |
| ReferenceDate | 현실 농업·공공 자료의 기준일 |
| Simulation WorldTick·GameDate | 생산·소비·작업·이동·군량 규칙 |
| PresentationTimeOfDay | 태양·조명·NPC·FX 연출 |

첫 playable에서는 `1 Tick = 1 Game Day`를 권장한다. 시간 배속은 Tick 규칙이 아니라 **Tick을 요청하는 빈도**다.

```text
Pause  : Tick 요청 없음
1x     : 기본 주기로 1 Tick
2x·4x  : 더 짧은 현실 간격으로 같은 1 Tick을 요청
```

중요 사건의 자동 일시정지는 client 편의 기능이다. 이미 Confirm된 Task를 취소하거나 다음 Task를 자동 승인하지 않는다.

## 7. 공통 Decision→Task→Effect 원장

### 7.1 상태 분리

첨부안의 단일 결정 상태열을 다음 세 원장으로 나눈다.

```text
Decision
Draft → Previewed → Confirmed → Cancelled

Task
Scheduled → InProgress → Completed
                    ├→ Blocked
                    ├→ Failed
                    └→ Cancelled

Effect
Pending → Applied
       └→ Rejected
```

`Decision.Completed` 같은 상태로 작업 완료와 효과 적용을 뭉치지 않는다. 결정은 무엇을 승인했는지, Task는 무엇이 진행 중인지, Effect는 실제로 어떤 값이 바뀌었는지를 각각 증명한다.

### 7.2 공통 계약

```text
SimulationDecisionSnapshot
  DecisionStableId / DecisionTypeCode / Revision
  Session / Faction / Territory / Settlement
  ActorStableId / TargetStableIds[]
  CreatedTick / ConfirmedTick?
  ExpectedCosts[] / ExpectedEffects[] / Uncertainties[]
  BlockReasonCodes[] / SourceStableIds[]

SimulationTaskSnapshot
  TaskStableId / TaskTypeCode / StateCode / Revision
  CausedByDecisionStableId
  FacilityStableId / AssignedCapacity
  ScheduledStartTick / ExpectedEndTick / ActualEndTick?
  InputLotStableIds[] / OutputCandidateCodes[]
  BlockReasonCodes[] / SourceStableIds[]

SimulationEffectRecord
  EffectStableId / EffectTypeCode / AppliedTick
  CausedByDecisionStableId / CausedByTaskStableId
  TargetLedgerStableId
  BeforeValue / Delta / AfterValue / UnitCode
  SourceStableIds[]
```

예상 효과는 카드용 Interpretation이며 실제 효과가 아니다. 실제 효과는 Task가 완료되는 Tick에 검증된 Effect record로 적용한다.

### 7.3 첫 migration 대상

- `HarvestDispositionDecisionData` → 공통 Decision envelope adapter
- `CooperativeIntakeCommand` → 조합 인수 Task
- `DirectOnlinePackingCommand` → 생산자 포장 Task
- CARGO-1 Pack·Load command → 포장·상차 Task
- JOURNEY-1 Dispatch·route Tick → 이동 Task

기존 stable ID와 source lineage는 유지한다.

## 8. 정착지 경제 최소 모델

### 8.1 첫 snapshot

```text
SettlementEconomySnapshot
  TreasuryBalance
  LaborCapacityTotal / LaborReserved / LaborAvailable
  MarketSupplyByProduct[]
  ReserveStockLots[]
  StorageCapacity / StorageOccupied
  PopulationFoodDemandPerTick
  GarrisonFoodDemandPerTick
  FoodReserveEquivalent
  FoodSecurityDays
  ActiveTaskStableIds[]
  Revision / RuleRevision / SourceStableIds[]
```

재정·노동·식량을 하나의 종합 점수로 합치지 않는다. 사용자가 trade-off를 직접 판단할 수 있도록 독립 지표와 계산 근거를 보여준다.

### 8.2 첫 자원 원장

- 현금 또는 Simulation currency ledger
- 노동 capacity reservation ledger
- 상품별 StockLot ledger
- storage allocation ledger
- 시장 공급 allocation ledger
- 주민 소비 ledger
- 주둔군 소비 ledger
- 손실·감모 ledger

화면의 상자 수, NPC 수, 건물 크기로 수량을 계산하지 않는다.

### 8.3 FoodSecurityDays

```text
FoodSecurityDays
  = 판매·수출 예약을 제외한 섭취 가능 식량 환산량
    / (PopulationDemandPerTick + GarrisonDemandPerTick)
```

처음부터 모든 식품의 영양과 대체율을 현실적으로 모델링하지 않는다. 첫 scenario는 명시적 `FoodEquivalentRuleRevision`을 가진 Fixture를 사용하고 실제 영양 처방으로 표시하지 않는다.

## 9. 수확 판로를 정착지 경제에 연결하는 방법

### 9.1 현재 네 판로의 상태

| 판로 | 현재 구현 | 공통 세계 연결 시 필요한 다음 단계 |
| --- | --- | --- |
| 조합 출하 | Unity 인수 Lot·CARGO-1 포장 검토 후보와 서버 비용·노동·기간·수입 후보 | 실제 노동·재정·HarvestLot allocation 적용과 정산 예정 |
| 온라인 직판 | Unity 5kg×60·비공개 등록 초안과 서버 시장공급·수입·미판매 위험 후보 | 실제 포장 노동·판매 가능 allocation·가격 초안 |
| 수출대행 | Unity workflow 후보와 서버 전문 포장·검사·인계 비용·기간·탈락 위험 후보 | 실제 외부교역 준비 Task와 HarvestLot allocation 적용 |
| 보관 | 서버 capacity·2% 감모·비축량·FoodSecurityDays 후보 | 실제 ReserveStockLot·창고 점유·재정 반영과 출고 제한 |

### 9.2 보관을 EXPORT보다 먼저 구현하는 이유

첫 scenario의 핵심 trade-off는 높은 잠재 수익과 식량 안전의 비교다. 보관이 없으면 사용자는 세 가지 외부 반출 방식 중 하나만 고르게 되어 영지 경영 판단이 성립하지 않는다.

따라서 `STORAGE-1`을 `EXPORT-1`보다 먼저 구현한다.

```text
HarvestLot 300kg
  → ReserveStorage Preview
  → storage capacity·감모·비용·FoodSecurityDays 영향 확인
  → Confirm
  → StorageTask
  → Tick
  → Applied candidate Effect
  → SETTLEMENT-ECONOMY-1
  → ReserveStockLot
```

보관도 즉시 군량이 아니다. `ReserveStockLot → FoodReserveAllocation → GarrisonSupplyAllocation`을 별도 결정으로 둔다.

`HARVEST-IMPACT-1 + STORAGE-1`에서는 위 흐름 중 candidate Effect까지 구현했다. Preview와 Confirm은 서버가 `harvest-impact:fixture-r1` 정책으로 다시 계산하며, Tick이 끝나도 정착지 재정·노동·시장·창고·비축 원장은 변경하지 않는다. 실제 적용과 같은 300kg의 중복 allocation 차단은 다음 Gate가 소유한다.

### 9.3 판로 Preview 카드

모든 판로는 최소 다음 dimension을 같은 순서로 보여준다.

| Dimension | 예시 |
| --- | --- |
| 수량 | 대상 300kg, 예약 후 잔여량 |
| 비용 | 포장·보관·검사·운송 Simulation 비용 |
| 노동 | 필요한 labor-unit와 다른 작업 충돌 |
| 기간 | 시작 Tick, 예상 완료 Tick |
| 재정 | 예상 현금 유입·유출, 불확실성 |
| 시장 | 정착지 시장 공급 변화 |
| 식량 | FoodSecurityDays 변화 |
| 군량 | 아직 배정되지 않은 잠재 영향 |
| 위험 | 미판매·감모·검사 탈락·운송 지연 |
| 경계 | 후보/Simulation 여부와 미실행 효과 |

하나의 추천 점수나 자동 최적 판로를 만들지 않는다.

## 10. 정착지 구역과 인구 모델

### 10.1 첫 정착지 구성

```text
Settlement: border-settlement-1
  Farm District       농장 2, 생산·수확·보관
  Town District       소규모 시장·직거래
  Market District     시장 공급·가격·소비
  Storage District    창고 capacity·감모
  Logistics District  Hub·도로·Cargo
  Residential District 인구집단·식량 소비
  Garrison District   주둔군·군량 소비
  Gate District       향후 봉쇄·방어
```

기존 Farm·Town·Hub·City Region은 이 구역의 Presentation 기반으로 재사용한다. 처음부터 새로운 거대한 World Scene을 만들지 않는다.

### 10.2 인구는 cohort 중심

일반 주민은 다음 집계 단위로 계산한다.

- 인구집단
- 가구집단
- 직업집단
- 노동 가능 집단
- 대피 집단

화면 NPC는 cohort 상태의 대표 표현이다. NPC가 시장에 도착했다고 소비가 자동 발생하지 않는다.

핵심 인물만 지속 entity로 둔다.

- 정착지 관리자
- 농장 대표
- 조합 대표
- 시장 관리자
- 창고 관리자
- 수비대장
- 군단 지휘관

사회적 label은 capability가 아니다. 행동 권한은 Simulation scenario role assignment와 revision에서 나온다.

## 11. 물류와 시장 연결

### 11.1 기존 Journey의 재사용

JOURNEY-1의 Cargo stable ID, route progress, Dispatch Confirm, 도착 경계는 유지한다. 다음 migration에서 독립 `SimulationDate`를 공통 `WorldTick`에 연결한다.

```text
LogisticsTask
  Origin / Destination
  RouteStableId / RouteRevision
  CargoStableId / Quantity
  DepartureTick / ExpectedArrivalTick
  Progress / DelayReason / RiskState
```

차량 follower는 Task progress를 표현할 뿐 도착을 확정하지 않는다.

### 11.2 시장과 소비

정착지 시장은 다음 상태를 분리한다.

```text
ReserveStock
MarketAllocatedStock
ShelfAvailableStock
ReservedForOrders
Sold
Consumed
ExpiredOrLost
```

직판 listing draft가 공개되었다고 판매가 발생하지 않는다. Simulation 주문, allocation, fulfillment Tick을 거쳐야 한다. 기존 Urban Market 수요·주문·재고 Engine을 일반화하되 실제 운영 주문과 섞지 않는다.

## 12. 군량과 분쟁은 경제 폐루프 뒤에 구현한다

### 12.1 군량

```text
ReserveStockLot
  → FoodReserveAllocationDecision
  → GarrisonSupplyLot 또는 ArmySupplyCargo
  → 보급 Task
  → 주둔군·군단 소비
```

군단은 최소 다음 집계 상태를 가진다.

- Cohort와 병력 수
- 사기·피로
- 현재 군량과 일일 소비
- 수송 capacity
- 지휘관
- 현재 명령
- 보급 경로

수천 병사를 개별 GameObject로 계산하지 않는다. Simulation은 cohort, Unity는 대표 유닛을 사용한다.

### 12.2 첫 침공

침공은 다음 상태를 거친다.

```text
Unknown
  → ActivityHint
  → BorderApproach
  → TerritoryEntered
  → OuterRouteThreatened
  → SettlementApproach
  → SiegePreparation
  → AssaultOrWithdrawal
```

첫 적 AI는 전술 지능보다 deterministic scenario schedule을 사용한다. 같은 seed와 command log에서 같은 접근 경로와 사건이 재현돼야 한다.

### 12.3 첫 방어 결정

- 농산물 긴급 반입
- 비축 방출
- 주민 대피
- 민병대 소집
- 성문 봉쇄
- 주둔군 군량 우선 배정

각 결정은 공통 Decision→Task→Effect를 사용한다. 노동·시장·식량·치안의 대가를 함께 보여준다.

### 12.4 전투 구현 순서

1. 경제·군량 기반 운영형 결과 Simulation
2. 세계 지도 군단 이동과 교전 표현
3. 정착지 공성 목표와 부대 단위 명령
4. 필요성이 검증된 뒤 확장형 전술 전투

처음부터 Total War 규모의 전술 전투를 만들지 않는다.

## 13. 첫 번째 플레이 가능한 시나리오

### 13.1 시나리오: 국경의 수확기

초기값은 모두 versioned Simulation Fixture다.

```text
세력             1
영지             1
정착지           1
농장             2
시장             1
창고             1
물류 거점        1
주거 cohort      2~3
주둔군 cohort    1
감자 HarvestLot  300kg
식량 예비        9일 상당
국경 위험        알려진 징후만 존재
```

### 13.2 첫 playable A — 경제 판단

```text
300kg HarvestLot 선택
  → 조합 / 직판 / 보관 / 외부 교역 준비 Preview
  → 비용·노동·기간·식량 예비 영향 확인
  → Confirm
  → Task 예약
  → 3~7 Tick 진행
  → 재정·노동·시장·비축 결과 확인
  → 원인 Decision 열기
```

완료 Gate는 네 판로의 콘텐츠 양이 아니라 같은 입력에서 서로 다른 정착지 snapshot이 결정적으로 생성되는 것이다.

### 13.3 첫 playable B — 식량 압박

인구와 주둔군이 매 Tick 식량을 소비한다. 시장 반출과 보관 결정에 따라 `FoodSecurityDays`가 달라진다. 아직 적군은 공격하지 않아도 된다.

### 13.4 첫 playable C — 침공 전조

경제 폐루프가 안정된 뒤 5~10 Tick 후 적 접근 사건을 발생시킨다. 사용자는 긴급 비축·반입·대피·민병대 소집 중 제한된 결정을 내린다. 결과는 우선 집계 Simulation과 World Map 표현으로 확인한다.

## 14. 재정렬된 구현 단계와 완료 Gate

### Phase A — 공통 세계 기반

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| 1 | `SIM-WORLD-0` — 완료 | session에 Faction·Territory·Settlement identity, WorldTick·GameDate 추가 | 집중 17/17·Simulation 전체 102/102, 멱등·revision·연도 날짜 경계 통과 |
| 2 | `DECISION-WORK-0` — 완료 | 공통 Decision·Task·Effect 계약, validator와 API | 집중 11/11·Simulation 전체 113/113, Preview 무변경·Confirm 분리 원장·Tick Task/Effect 전이 통과 |
| 3 | `SAVE-REPLAY-0` — 완료 | `simulation-save.v1`, append-only Command log, SHA-256 hash와 restore port | 집중 10/10·Simulation 전체 123/123, 새 session store replay hash 동일·변조/순서/덮어쓰기 거부·process-local adapter 경계 명시 |
| 4 | `SETTLEMENT-CORE-1` — 완료 | 첫 정착지 graph, 재정·노동·창고·시장·비축·식량 snapshot | 집중 15/15·정착지+save 25/25·Simulation 전체 138/138, 수량·capacity·ID 참조·FoodSecurityDays·replay hash 통과 |

### Presentation Foundation — 서버 경제 Gate 전 한정 선행

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| P1 | `WORLD-SHELL-0` — 완료 | 신규 Simulation 전용 shell, WorldMap/Settlement root, 공통 카메라·HUD·stable-ID 선택 | 전환 전후 Tick 12·Revision 12 유지, 전용 상태기계 test 포함 Unity 기본 EditMode 44/44·Play Mode Console 오류 0건 |
| P2 | `SETTLEMENT-SCENE-0` — 완료 | Farm·Town·Market·Storage·Logistics·Residential와 Gate·Garrison placeholder를 가진 첫 정착지 blockout | 8개 District socket·placeholder 2개 validator와 최종 World Map·Settlement Interior Game View 확인 |

두 단계는 하나의 제한된 Presentation milestone이다. 완료 즉시 추가 미술·NPC·전투 확장을 중단하고 `SETTLEMENT-ECONOMY-1`로 돌아간다. 기존 공공데이터 `WorldBootstrapScene`은 Simulation shell로 바꾸지 않으며, 신규 `SimulationWorldShell`이 같은 authoritative snapshot을 관찰하는 별도 Presentation surface가 된다.

### Phase B — 감자 판로를 정착지 경제로 연결

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| 5 | `HARVEST-IMPACT-1` — 완료 | 기존 3개 판로를 공통 context·영향 Preview로 감쌈 | 기존 choice/decision stable ID·revision 보존, 서버 재계산, 예상 효과와 정착지 실제 값 분리 |
| 6 | `STORAGE-1` — 완료 | 네 번째 보관 판로, capacity·감모·비축 후보 | 집중 18/18·Simulation 전체 156/156, capacity 초과 차단, 300kg→294kg·FoodSecurityDays 10→12.94 후보와 replay hash 통과 |
| 7 | `SETTLEMENT-ECONOMY-1` — 완료 | 판로별 labor·cash·stock allocation Task/Effect | 집중 48/48·Simulation 전체 161/161, 같은 300kg 중복 배정 금지와 Tick별 보존식·effect ledger·save/replay 통과 |
| 8 | `WORLD-SETTLEMENT-NAV-0` — 완료 | World Map→Settlement→District→Object→Back 관찰 전환 | 같은 Tick 12·Revision 12 유지, 전용 EditMode 8/8과 Play Mode Back 검증 |
| 9 | `BRANCH-ADAPTER-1` — 완료 | COOP-1·DIRECT-1·수출 준비·보관을 서버 Preview 입력·후보 Task에 연결 | .NET 집중 15/15·Unity package 전체 328/328·Unity EditMode 집중 6/6 및 전체 55/55·서버 판로 영향 23/23, stable ID/revision/lineage 호환과 무변경 경계 통과 |
| 10 | `SETTLEMENT-VISUAL-BASE-0` — 완료 | 환경·도로·건물·수목·낮밤의 1차 정착지 미술 | semantic VisualKey 기반 45개 이상 wrapper, 기본 EditMode 57/57·판로 카드 4/4·Play Mode 오류 0건, Overview·Farm·Market PNG |
| 11 | `SETTLEMENT-INTERACTION-0` — 완료 | HarvestLot 선택, 판로 Preview·Confirm·Task·Tick·Effect와 snapshot 재조회 | 집중 8/8·기본 EditMode 65/65·서버 판로 23/23, 비축 revision 12→13→14와 Tick 12→13, HUD·카드 reconcile PNG |

이 Phase가 첫 번째 필수 playable 절단선이다. 여기까지 닫기 전 군단·침공 코드를 시작하지 않는다.

### Phase C — 이동·시장·외부 교역

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| 12 | `LOGISTICS-MOVEMENT-1` — 완료 | CARGO-1/JOURNEY-1을 공통 WorldTick Task로 이관 | 서버 5/5·전체 166/166, Unity 4/4, 동일 Cargo·300kg 예약·lineage·save/replay와 도착 후보 보존 |
| 13 | `MARKET-CONSUMPTION-1` | 시장 allocation·Simulation 주문·주민 소비 | 공개·가격·주문·판매·소비·잔여 재고 상태 분리 |
| 14 | `EXPORT-1` | 외부권역 교역 준비, 전문 포장·검사·대행지 인계 | 비용·노동·기간·탈락 결과, 실제 계약·통관·운송 미생성 |
| 15 | `DIRECT-2` | 가격 초안·공개 결정·주문 후보 | 가격 관측과 Simulation 판매가 분리, 공개 전 주문 차단 |
| 16 | `COOP-2` | 조합 포장부터 Farm→Hub까지 한 workflow로 조립 | 각 Preview/Confirm/Tick 유지, 자동 정산 금지 |

### Phase D — 식량 안보와 군량

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| 17 | `FOOD-SECURITY-1` | population·garrison 소비와 안전재고 | 판로별 FoodSecurityDays가 동일 rule revision으로 재현 |
| 18 | `ARMY-SUPPLY-1` | garrison/army supply allocation과 Cargo | 시장·주민 재고 중복 소비 금지, 보급로 단절 상태 표현 |
| 19 | `ARMY-MOVEMENT-1` | cohort 군단, 행군·피로·군량 소비 | 대표 유닛 수와 실제 병력 수 분리, 도착 animation 비권위 |

### Phase E — 침공·방어·분쟁 표현

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| 20 | `INVASION-1` | deterministic 적 접근과 사건 | seed·route·Tick으로 재현, 정보 수준과 실제 적 상태 분리 |
| 21 | `DEFENSE-DECISION-1` | 비축·대피·민병대·봉쇄 카드 | 경제·노동·식량 대가가 Effect ledger에 남음 |
| 22 | `CONFLICT-RESOLUTION-1` | 보급·사기·방어 준비 기반 집계 전투 | 같은 snapshot·명령에서 동일 손실·퇴각·결과 |
| 23 | `CONFLICT-VIEW-1` | World Map 교전과 정착지 공성 표현 | View가 결과를 계산하지 않고 대표 유닛·목표 상태만 투영 |
| 24 | `ATTACK-1` | 플레이어 공격 계획 Preview·Confirm | 군량·경로·기간·불확실성·외교 영향과 block reason 표시 |

### Phase F — 확장

| 순서 | 단계 | 구현 범위 | 완료 Gate |
| ---: | --- | --- | --- |
| 25 | `GOVERNOR-AUTOMATION-1` | 플레이어가 설정한 제한적 정책 | 정책 revision·예외 카드·철회, 임의 priority score 없음 |
| 26 | `MULTI-SETTLEMENT-1` | 두 번째 정착지와 정착지 간 교역 | 한 정착지 최적화가 다른 정착지 재고를 침범하지 않음 |
| 27 | `CROP-EXPAND-1` | 양파·토마토·딸기·사과 | 공통 world/decision/effect 계약 재사용, 전용 manager 증가 없음 |
| 28 | `TACTICAL-EXPAND-1` | 필요 시 부대 명령·공성 목표 확장 | 경제·보급 인과를 우회하지 않음 |

## 15. 구현 단위와 repository 배치

### 15.1 `Ssalddel.Simulation.Contracts`

- `SimulationWorldContextContracts`
- `SimulationDecisionTaskEffectContracts`
- `SettlementEconomyContracts`
- `SettlementProjectionContracts`

Contract는 기술 역할은 영어, 업무 의미는 한국어 명명 원칙을 따른다. API와 저장 schema는 version을 가진다.

### 15.2 `Ssalddel.Simulation.Domain`

- `정착지WorldAggregate`
- `SimulationClock`
- `결정작업효과Engine`
- `정착지경제Engine`
- 이후 `물류이동Engine`, `식량안보Engine`, `군량Engine`, `분쟁결과Engine`

Engine은 후보·결과를 계산하고 aggregate가 expected revision과 authority를 검증한 뒤 적용한다.

### 15.3 `Ssalddel.Simulation.Hosting`

- world snapshot query
- decision preview/confirm endpoint
- Tick endpoint 확장
- task/effect query
- snapshot/replay store port

Hosting 모듈은 주 서버의 JWT와 HTTP pipeline을 사용한다. Simulation 상태 권위와 저장 DB는 운영 원장과 분리한다.

### 15.4 `Ssalddel.Unity`

- server API mirror model
- mapper·validator
- shared/perspective interpreter
- card·world presentation model
- 기존 FARM·Cargo fixture 호환 adapter

Unity core에 서버 entity나 persistence를 공유하지 않는다.

### 15.5 Unity product project

- `WorldMap` Scene
- `SettlementInterior` Scene
- 기존 Farm·Town·Hub·City wrapper 재사용
- `DecisionCard`, `Timeline`, `CausalityInspector`
- semantic `VisualKey`와 대표 prefab

성·군단 asset이 없어도 Phase B는 primitive·기존 City/Farm wrapper로 검증한다. asset 확보를 domain 구현의 선행 조건으로 만들지 않는다.

## 16. UI·카드·인과관계 보기

### 16.1 카드 종류

- Concept: 제도와 자원 의미
- Status: 현재 정착지·Lot·Task 상태
- Reason: 영향 계산과 block reason
- Action: Preview·Confirm 가능한 행동

기존 Concept Card 문법을 재사용한다.

### 16.2 최소 HUD

```text
GameDate / Pause / Speed
Treasury
Labor Available / Reserved
Market Food Supply
Reserve Food
FoodSecurityDays
Active Tasks
Known Threats
```

HUD 값은 Presentation이 계산하지 않는다.

### 16.3 Causality Inspector

사용자는 다음 연결을 역방향으로 조회할 수 있어야 한다.

```text
FoodSecurityDays 6.2
  ← ReserveStock 840 food-equivalent
  ← Direct sale allocation -300kg
  ← Task producer-packing...
  ← Decision harvest-disposition...
  ← HarvestLot...
  ← CultivationCycle...
```

모든 작은 Effect를 화려한 그래프로 먼저 만들 필요는 없다. 첫 slice는 stable ID와 before/delta/after 표로 충분하다.

## 17. 검증 전략

### 17.1 Headless contract·domain

- 세력·영지·정착지 참조 무결성
- 같은 Lot의 판로·시장·군량 중복 allocation 차단
- 모든 재고 이동의 질량 보존 또는 명시적 loss
- 예상 효과가 실제 ledger를 변경하지 않음
- Confirm 재처리 멱등성과 expected revision 충돌
- Task 시작·완료·실패 Tick 경계
- labor/cash/storage capacity 초과 차단
- 같은 seed·scenario·command log 결과 hash 일치
- save/restore 전후 예약 Task와 source lineage 일치

### 17.2 Integration

- FARM-3 HarvestLot을 Simulation World에 import하는 adapter
- 기존 COOP-1·DIRECT-1 결과와 공통 engine 결과 호환
- CARGO-1/JOURNEY-1의 world tick migration
- market allocation·소비 뒤 잔여 재고 수렴
- Unity 재접속·Scene 전환 뒤 같은 snapshot revision 재조회

### 17.3 Unity

- World Map과 정착지 내부가 같은 Tick·revision을 표시
- Lot 클릭→판로 card→Preview→Confirm→Task→Tick 전체 Play Mode 검증
- 상자·NPC·차량 수로 원장 수량을 계산하지 않음
- Pause와 배속 중 Confirm 자동 실행 없음
- final Game View PNG와 변경 기록
- Console error와 missing-script를 별도 보고

### 17.4 첫 playable acceptance

1. 한 Simulation session에서 28일을 결정적으로 진행할 수 있다.
2. 300kg 감자에 조합·직판·보관·외부교역 준비 네 선택이 보인다.
3. 판로마다 재정·노동·시장·비축 영향 Preview가 다르다.
4. Confirm 전에는 어떤 ledger도 변하지 않는다.
5. Task 완료 Tick에만 실제 Effect가 적용된다.
6. 300kg이 둘 이상의 판로에 중복 배정되지 않는다.
7. 주민·주둔군 소비로 FoodSecurityDays가 변화한다.
8. Scene 전환 후에도 같은 세계시간과 상태가 유지된다.
9. 결과에서 원인 Decision과 HarvestLot까지 역추적할 수 있다.
10. 모든 화면에 Simulation/Fixture와 limitation이 표시된다.

## 18. 명시적 비범위

첫 경제 playable에서는 다음을 구현하지 않는다.

- 수천 명 개별 주민·병사 Simulation
- 완전한 전술 전투
- 해상전과 복잡한 외교
- 실제 결제·수출계약·통관·운송
- 운영 사용자와 게임 세력의 identity 공유
- 현실 인구·종교·국적·언어를 충성도·전투력 proxy로 사용
- AI가 자의적으로 판로·배급·모병을 확정
- 모든 60개 상품 동시 지원
- 새 World를 꾸미기 위한 대규모 asset 선행 작업

## 19. 중단 조건

다음 중 하나가 생기면 다음 Phase로 넘어가지 않는다.

- Unity fixture와 Simulation 서버가 같은 상태의 이중 authority로 남음
- WorldTick 없이 subsystem별 날짜를 독립 증가시킴
- 판로 Preview가 실제 재고·재정·노동을 변경함
- 같은 300kg이 시장·창고·Cargo에 중복 존재함
- `FoodSecurityDays`에 계산 근거와 rule revision이 없음
- 온라인·수출 용어가 영지 scenario domain code에 하드코딩됨
- NPC·차량·전투 animation이 Task 완료를 확정함
- 전투 구현 때문에 경제·군량 lineage를 우회함
- 정착지 추가마다 전용 Scene manager와 switch가 증가함
- Simulation 결과가 운영 주문·계약·결제로 오인됨

## 20. 최종 우선순위 결론

첨부 방향은 현재 구조와 잘 맞는다. Lot·Cargo·판로·재고·시장·원장이라는 프로젝트의 강점이 영지 경영과 분쟁의 기반 경제가 될 수 있다.

그러나 현재 필요한 질적 도약은 `수출 분기 하나 더`나 `성벽·군단 배치`가 아니다.

```text
첫째, 모든 상태가 속할 하나의 Simulation World를 만든다.
둘째, 결정·작업·효과를 분리해 시간이 인과를 전달하게 한다.
셋째, 기존 감자 판로가 정착지 재정·노동·시장·비축을 실제로 바꾸게 한다.
넷째, 주민과 주둔군의 소비로 식량 안전을 계산한다.
다섯째, 그 경제 위에 군량·침공·방어를 올린다.
```

따라서 Phase A의 `SIM-WORLD-0`, `DECISION-WORK-0`, `SAVE-REPLAY-0`, `SETTLEMENT-CORE-1`과 Phase B의 `HARVEST-IMPACT-1 + STORAGE-1`은 완료됐다. 다음 서버 권위 Gate는 실제 재정·노동·시장·비축 원장 적용과 300kg 중복 allocation을 막는 `SETTLEMENT-ECONOMY-1`이다.

`WORLD-SHELL-0 → SETTLEMENT-SCENE-0 → SETTLEMENT-ECONOMY-1 → WORLD-SETTLEMENT-NAV-0 → BRANCH-ADAPTER-1 → SETTLEMENT-VISUAL-BASE-0 → SETTLEMENT-INTERACTION-0`까지 완료됐다. 같은 session·WorldTick·revision을 공유하는 WorldShell에서 300kg HarvestLot의 네 판로와 Preview·Confirm·Task·Tick·Effect·HUD reconcile이 연결되어 첫 경제 playable 절단선이 닫혔다. 다음은 `LOGISTICS-MOVEMENT-1`에서 기존 CARGO-1/JOURNEY-1을 공통 WorldTick Task와 정착지 재고 예약에 합류시킨다.

이 순서를 따르면 지금까지 만든 Farm·Town·Hub·City와 감자 300kg lineage가 버려지지 않는다. 오히려 단순한 생산·유통 시연을 넘어, 플레이어의 한 선택이 며칠 뒤 정착지와 주민, 군량과 분쟁 결과까지 바꾸는 살아 있는 Simulation World의 첫 원인이 된다.
