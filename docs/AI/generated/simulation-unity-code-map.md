# Simulation·Unity 코드 탐색 트리

> 이 문서는 `SsalddelCodeMetadataAttribute`와 `eng/work-areas/simulation-unity.json`에서 자동 생성된다. 직접 수정하지 않는다.

```text
Simulation·Unity
├─ 운영 주문·음식점·배차·화물·창고의 Simulation 재사용 계보 [food-workflow-lineage]
│  ├─ 010 domain.food-order-adaptation · Domain · Confirm
│  ├─ 020 domain.food-dispatch-shared-rule · Domain · Tick
│  ├─ 020 domain.restaurant-response-adaptation · Domain · Confirm
│  ├─ 025 domain.restaurant-cooking-adaptation · Domain · Tick
│  ├─ 030 unity.food-state-projection · ViewModel · Presentation
│  ├─ 040 domain.freight-dispatch-shared-rule · Domain · Confirm
│  ├─ 045 domain.freight-transport-adaptation · Domain · Tick
│  ├─ 050 domain.warehouse-put-away-adaptation · Domain · Confirm
│  └─ 055 domain.warehouse-outbound-shared-rule · Domain · Tick
├─ Simulation 세션 생명주기 [simulation-session-lifecycle]
│  ├─ 010 contract.base-reflection-learning-material · Contract · Definition
│  ├─ 010 contract.online-world · Contract · Query
│  ├─ 010 contract.session-create · Contract · Definition
│  ├─ 015 application.observer-tick-request · Application · Preview
│  ├─ 020 api.session-lifecycle · Api · Confirm
│  ├─ 020 api.world-gameplay · Api · Confirm
│  ├─ 020 domain.approved-learning-ledger · Domain · Persistence
│  ├─ 021 api.online-world · Api · Confirm
│  ├─ 030 application.online-world · Application · Confirm
│  ├─ 030 application.session-lifecycle · Application · Confirm
│  ├─ 030 application.world-gameplay · Application · Confirm
│  ├─ 030 domain.base-reflection · Domain · Tick
│  ├─ 031 application.online-nature-session-provision · Application · Confirm
│  ├─ 032 application.online-cooperative-logging · Application · Confirm
│  ├─ 040 domain.online-world · Domain · Tick
│  ├─ 040 domain.session-aggregate · Domain · Tick
│  └─ 050 infrastructure.session-store · Infrastructure · Persistence
├─ 병렬 경영-전투 [simulation-parallel-battle]
│  ├─ 010 contract.battle-preview · Contract · Definition
│  ├─ 010 contract.local-combat-control-mode · Contract · Definition
│  ├─ 011 contract.local-combat-action · Contract · Definition
│  ├─ 012 contract.local-combat-observer-intervention · Contract · Definition
│  ├─ 020 api.battle · Api · Confirm
│  ├─ 030 application.battle · Application · Confirm
│  ├─ 040 domain.battle-state · Domain · Tick
│  ├─ 050 infrastructure.battle-store · Infrastructure · Persistence
│  └─ 060 unity.battle-presentation · ViewModel · Presentation
├─ 1인칭 농장 타이밍 전투 [simulation-farm-combat-input]
│  ├─ 010 contract.farm-combat · Contract · Definition
│  ├─ 020 api.farm-combat · Api · Confirm
│  ├─ 030 application.farm-combat · Application · Confirm
│  ├─ 040 domain.farm-combat · Domain · Tick
│  └─ 050 unity.farm-combat-input · ClientAdapter · Presentation
├─ Simulation 저장-재생 [simulation-save-replay]
│  ├─ 010 contract.save-request · Contract · Definition
│  ├─ 020 api.save-replay · Api · Persistence
│  ├─ 030 application.save-replay · Application · Persistence
│  ├─ 040 domain.save-package · Domain · Persistence
│  └─ 050 infrastructure.save-store · Infrastructure · Persistence
├─ 공공데이터-파생 World [simulation-world-derivation]
│  ├─ 009 contract.nature-shelter-purpose · Contract · Definition
│  ├─ 010 contract.nature-sleep-safety-candidate · Contract · Definition
│  ├─ 010 domain.derived-world-ledger · Domain · Definition
│  ├─ 011 contract.nature-risky-sleep-warning · Contract · Definition
│  ├─ 012 contract.nature-difficulty-boundary · Contract · Definition
│  ├─ 013 contract.nature-expert-threat-candidate · Contract · Definition
│  ├─ 014 contract.meditation-focus-access-candidate · Contract · Definition
│  ├─ 014 contract.nature-risky-sleep-outcome-candidate · Contract · Definition
│  ├─ 015 contract.meditation-combat-progression-candidate · Contract · Definition
│  ├─ 015 contract.nature-weather-profile-freeze-candidate · Contract · Definition
│  ├─ 016 contract.deep-observation-progression-candidate · Contract · Definition
│  ├─ 016 contract.landscape-composition-tile · Contract · Definition
│  ├─ 016 contract.nature-sleep-protection-spatial-layer-candidate · Contract · Definition
│  ├─ 016 contract.world-map-composition · Contract · Definition
│  ├─ 017 contract.environment-spawn-decision · Contract · Projection
│  ├─ 017 contract.landscape-graph · Contract · Definition
│  ├─ 017 contract.player-growth-hint-projection · Contract · Definition
│  ├─ 018 contract.interior-layout-plan · Contract · Definition
│  ├─ 018 contract.party-proximity-resonance-candidate · Contract · Definition
│  ├─ 019 contract.marketplace-grounded-interior-item · Contract · Definition
│  ├─ 019 contract.party-resonance-recovery-candidate · Contract · Definition
│  ├─ 019 contract.world-asset-placement · Contract · Projection
│  ├─ 019 domain.interior-layout-generate · Domain · Projection
│  ├─ 019 domain.nature-shelter-purpose-readiness · Domain · Query
│  ├─ 020 application.pyeongchang-derivation · Application · Projection
│  ├─ 020 contract.party-resonance-afterglow-candidate · Contract · Definition
│  ├─ 020 domain.marketplace-grounded-item-effect-derive · Domain · Projection
│  ├─ 020 domain.nature-sleep-safety-candidate-readiness · Domain · Query
│  ├─ 021 contract.party-resonance-stacking-candidate · Contract · Definition
│  ├─ 021 domain.nature-risky-sleep-outcome-candidate · Domain · Query
│  ├─ 021 domain.nature-risky-sleep-warning-policy · Domain · Query
│  ├─ 022 contract.gwangbok-resonance-entry-cap-candidate · Contract · Definition
│  ├─ 022 domain.nature-difficulty-boundary · Domain · Query
│  ├─ 022 domain.nature-weather-profile-freeze-candidate · Domain · Query
│  ├─ 023 contract.gwangbok-self-recovery-action-candidate · Contract · Definition
│  ├─ 023 domain.nature-expert-threat-candidate-readiness · Domain · Query
│  ├─ 023 domain.nature-sleep-protection-spatial-layer-candidate · Domain · Query
│  ├─ 024 contract.first-logging-reflection-seed · Contract · Definition
│  ├─ 024 contract.gwangbok-resonance-maintenance-candidate · Contract · Definition
│  ├─ 024 domain.meditation-focus-access-candidate-readiness · Domain · Query
│  ├─ 025 contract.personal-recovery-decay-candidate · Contract · Definition
│  ├─ 025 domain.meditation-combat-progression-candidate-readiness · Domain · Query
│  ├─ 026 contract.personal-recovery-offline-time-candidate · Contract · Definition
│  ├─ 026 domain.deep-observation-progression-candidate-readiness · Domain · Query
│  ├─ 027 contract.personal-recovery-threat-offset-candidate · Contract · Definition
│  ├─ 027 domain.environment-spawn-decision · Domain · Projection
│  ├─ 027 domain.player-growth-hint-projection · Domain · Query
│  ├─ 028 application.world-map-composition · Application · Projection
│  ├─ 028 contract.dark-age-mindfulness-access-candidate · Contract · Definition
│  ├─ 028 domain.party-proximity-resonance-candidate · Domain · Query
│  ├─ 029 application.world-asset-placement · Application · Projection
│  ├─ 029 contract.dark-age-mindfulness-effect-scope-candidate · Contract · Definition
│  ├─ 029 domain.party-resonance-recovery-candidate · Domain · Query
│  ├─ 030 adapter.lh-separated-cell-content · Application · Projection
│  ├─ 030 application.landscape-placement-binding-guard · Application · Projection
│  ├─ 030 application.nature-world-asset-placement-state · Application · Projection
│  ├─ 030 application.separated-world-asset-placement · Application · Projection
│  ├─ 030 contract.dark-age-mindfulness-effect-strength-candidate · Contract · Definition
│  ├─ 030 domain.party-resonance-afterglow-candidate · Domain · Query
│  ├─ 030 infrastructure.derived-world-store · Infrastructure · Persistence
│  ├─ 031 application.nature-world-cell-assembly · Application · Projection
│  ├─ 031 application.world-asset-plan-partition · Application · Projection
│  ├─ 031 domain.party-resonance-stacking-candidate · Domain · Query
│  ├─ 032 domain.gwangbok-resonance-entry-cap-candidate · Domain · Query
│  ├─ 033 domain.gwangbok-self-recovery-action-candidate · Domain · Query
│  ├─ 034 domain.gwangbok-resonance-maintenance-candidate · Domain · Query
│  ├─ 035 domain.personal-recovery-decay-candidate · Domain · Query
│  ├─ 036 domain.personal-recovery-offline-time-candidate · Domain · Query
│  ├─ 037 domain.personal-recovery-threat-offset-candidate · Domain · Query
│  ├─ 038 domain.dark-age-mindfulness-access-candidate · Domain · Query
│  ├─ 039 domain.dark-age-mindfulness-effect-scope-candidate · Domain · Query
│  ├─ 040 domain.dark-age-mindfulness-effect-strength-candidate · Domain · Query
│  ├─ 041 unity.marketplace-grounded-item-detail · ViewModel · Presentation
│  ├─ 042 domain.landscape-graph-assembler · Domain · Projection
│  ├─ 044 application.landscape-graph-job · Application · Projection
│  ├─ 100 application.neighborhood-import · Application · Projection
│  ├─ 110 application.neighborhood-route · Application · Preview
│  ├─ 120 application.neighborhood-movement-candidate · Application · Preview
│  └─ 130 application.synthetic-food-delivery-journey-projection · Application · Projection
├─ 독립 Synty 경관 처리 [simulation-synty-landscape]
│  ├─ 010 domain.synty-ledger · Domain · Definition
│  ├─ 030 application.synty-job · Application · Projection
│  └─ 040 infrastructure.synty-store · Infrastructure · Persistence
├─ L2 타일 Streaming [simulation-world-streaming]
│  ├─ 010 contract.stream-recipe · Contract · Definition
│  ├─ 011 contract.lh-world-profile · Contract · Definition
│  ├─ 012 contract.lh-asset-plan-lifecycle · Contract · Definition
│  ├─ 020 api.world-stream · Api · Query
│  ├─ 025 api.world-region-summary · Api · Query
│  ├─ 028 contract.world-layout-definition · Contract · Definition
│  ├─ 029 contract.area-set-handover-plan · Contract · Preview
│  ├─ 030 application.area-set-handover-plan · Application · Preview
│  ├─ 030 application.world-stream · Application · Projection
│  ├─ 031 application.lh-world-preview · Application · Preview
│  ├─ 032 api.lh-world-preview · Api · Preview
│  ├─ 033 application.lh-interior-plan-handle · Application · Projection
│  ├─ 034 application.lh-asset-plan-lifecycle · Application · Projection
│  ├─ 035 application.lh-asset-plan-window-reconcile · Application · Projection
│  ├─ 040 unity.interior-plan-presentation · ViewModel · Presentation
│  └─ 041 unity.lh-asset-plan-presentation-reconcile · ClientAdapter · Presentation
├─ Farm 감자 3원천 현실근거 [simulation-farm-reality-evidence]
│  ├─ 010 contract.farm-reality-evidence · Contract · Definition
│  ├─ 020 api.farm-reality-evidence · Api · Persistence
│  ├─ 030 application.farm-reality-sync · Application · Persistence
│  └─ 040 infrastructure.farm-reality-store · Infrastructure · Persistence
└─ Unity 마지막 성공 상태 로딩 [unity-resilient-world-load]
   ├─ 010 client.last-successful-runtime · ClientAdapter · Presentation
   ├─ 020 client.community-load · ClientAdapter · Query
   ├─ 020 client.public-data-load · ClientAdapter · Query
   └─ 020 client.warehouse-load · ClientAdapter · Query
```

기능 하나만 보려면 `dotnet run --project eng/Ssalddel.CodeMap -- --feature <기능키>`를 사용한다.

## 운영 주문·음식점·배차·화물·창고의 Simulation 재사용 계보 (`food-workflow-lineage`)

- **010 domain.food-order-adaptation** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식배달.cs) · 운영 주문 등록·수령 확인 의미를 세션 주문 상태로 재구성
  - 계층/단계: `Domain / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: SimulationState만 변경; 실제 주문·결제·운영 원장 생성 없음
  - 재사용 종류: `SemanticAdaptation`
  - 원천 코드: [음식주문등록CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식주문등록CommandHandler.cs)
  - 원천 코드: [주문자음식주문수령확인CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/주문자음식주문수령확인CommandHandler.cs)
  - 공유 규칙: [업무흐름규칙Catalog.cs](../../../Ssalddel.WorkflowRules/UnityPackage/Runtime/업무흐름규칙Catalog.cs)
  - 변형/제외: 업무 의미 대응이며 Handler 직접 호출이나 동일 구현 주장이 아니다. 세션 CommandId/Revision/Tick과 가상 주문을 사용하고 운영 메뉴·개인정보·DB·Event를 소비하지 않는다.
- **020 domain.food-dispatch-shared-rule** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/가상배달대기.cs) · 가상 기사 후보 선정에서 운영과 공유하는 픽업 평가 규칙 사용
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 평가 결과를 세션에서 재검사·배정; 운영 배차·알림·위치 조회 없음
  - 재사용 종류: `SharedRuleCall`
  - 원천 코드: [음식배달배차업무정책.cs](../../../Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs)
  - 공유 규칙: [음식배달픽업평가Policy.cs](../../../Ssalddel.WorkflowRules/UnityPackage/Runtime/음식배달픽업평가Policy.cs)
  - 변형/제외: 음식배달후보선정Policy를 통해 공유 픽업 평가·정렬을 호출한다. 운영은 경로서비스/UTC/기사Store, 가상은 표본 경로/세션 시간/기사 사본을 사용한다. 후보 정책 전체나 배차 확정은 동일 코드가 아니다.
- **020 domain.restaurant-response-adaptation** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식점응답.cs) · 운영 음식점의 주문 수락·거절 의미를 세션 결정과 작업으로 재구성
  - 계층/단계: `Domain / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Simulation 결정/작업만 변경; 운영 음식점 주문·계정·알림 없음
  - 재사용 종류: `SemanticAdaptation`
  - 원천 코드: [음식점주문수락CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식점주문수락CommandHandler.cs)
  - 원천 코드: [음식점주문진행변경CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식점주문진행변경CommandHandler.cs)
  - 원천 코드: [음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs)
  - 변형/제외: 운영의 주문 상태 의미를 사용하지만 사용자·음식점 접근권한, UTC 이력, Store/Event를 호출하지 않는다. 세션 Actor capability와 ExpectedRevision으로 별도 판정한다.
- **025 domain.restaurant-cooking-adaptation** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식점조리.cs) · 음식점 진행의 조리중·픽업대기 의미를 NPC 조리 슬롯과 Tick 작업으로 재구성
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 조리 완료는 픽업 대기만 만들며 기사 배정·전달을 자동 확정하지 않음
  - 재사용 종류: `SemanticAdaptation`
  - 원천 코드: [음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs)
  - 변형/제외: 운영 조리예상분 대신 WorkDurationTicks/CookingSlots/NPC capability를 사용한다. 메뉴·가격·리뷰·운영자 계정은 이관하지 않는다.
- **030 unity.food-state-projection** — [음식배달관찰상태](../../../Ssalddel.Unity/Presentation/음식배달관찰경로.cs) · 음식배달 상태 사본을 읽기 전용 표시 문구로 변환
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 표현값만 생성; 원본 상태 변경·주문/배차/수령 확정 없음
  - 재사용 종류: `ProjectionConsumption`
  - 원천 코드: [Simulation음식배달.cs](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식배달.cs)
  - 변형/제외: Simulation음식배달Snapshot의 주문 ID/Revision/상태를 읽는다. 경로 이동·MonoBehaviour 생성·운영 서버 연결은 이 클래스의 책임이 아니다.
- **040 domain.freight-dispatch-shared-rule** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFreightDispatch.cs) · 운영 화물 배차와 공유하는 추천 점수·기사 대기 계산으로 가상 후보를 판정
  - 계층/단계: `Domain / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Preview는 비변경 후보, Confirm만 SimulationState 변경; 실제 기사 배정 없음
  - 재사용 종류: `SharedRuleCall`
  - 원천 코드: [기사대기Aging점수정책.cs](../../../Ssalddel/Services/Dispatch/Queue/기사대기Aging점수정책.cs)
  - 원천 코드: [배차추천평가Service.Scoring.cs](../../../Ssalddel/Services/Dispatch/Recommendation/배차추천평가Service.Scoring.cs)
  - 공유 규칙: [화물배차후보판정Policies.cs](../../../Ssalddel.WorkflowRules/UnityPackage/Runtime/화물배차후보판정Policies.cs)
  - 변형/제외: 공통 후보 점수·대기 보정을 사용하되 가상 cargo/vehicle/location snapshot을 입력으로 삼는다. 운영 조회·기사 알림·배차 원장은 호출하지 않는다.
- **045 domain.freight-transport-adaptation** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFreightTransport.cs) · 운영 기사 운송의 상차·하차·인수 상태 의미를 가상 이동과 별도 Confirm으로 재구성
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 운영 운송원장·위치·정산을 쓰지 않고 세션 이력/SaveReplay만 변경
  - 재사용 종류: `SemanticAdaptation`
  - 원천 코드: [기사운송상태전이Service.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송상태전이Service.cs)
  - 원천 코드: [기사운송진행Controller.cs](../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)
  - 공유 규칙: [업무흐름규칙Catalog.cs](../../../Ssalddel.WorkflowRules/UnityPackage/Runtime/업무흐름규칙Catalog.cs)
  - 변형/제외: 공통 화물운송 상태/허용 전이를 사용하지만 운영 UTC·사용자 명령·GPS·문제신고 대신 WorldTick·가상 이동·ExpectedRevision을 사용한다.
- **050 domain.warehouse-put-away-adaptation** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWarehousePutAway.cs) · 운영 창고의 검수 후 적치 의미를 세션 재고와 NPC 작업으로 재구성
  - 계층/단계: `Domain / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 검수된 Simulation 재고만 적치; 운영 입고·재고·재위탁 변경 없음
  - 재사용 종류: `SemanticAdaptation`
  - 원천 코드: [창고작업Controller.cs](../../../Ssalddel/Controllers/Common/창고작업Controller.cs)
  - 변형/제외: 운영 창고입고 상태 의미에 대응하지만 현재 구현은 공통 Catalog를 직접 호출하지 않는다. 운영 창고/사용자 CRUD·입고 원장·권한·EF 저장 대신 세션 inventory/task/effect를 사용한다.
- **055 domain.warehouse-outbound-shared-rule** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/가상동네보충.cs) · 가상 보충창고 출고 예약에서 운영과 같은 재고 배분 계산 사용
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 가상 동네 보충 재고만 변경; 운영 창고·화물 생성 없음
  - 재사용 종류: `SharedRuleCall`
  - 원천 코드: [OutboundBatchEngine.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/OutboundBatchEngine.cs)
  - 공유 규칙: [창고출고배분Policy.cs](../../../Ssalddel.WorkflowRules/UnityPackage/Runtime/창고출고배분Policy.cs)
  - 변형/제외: 공통 배분 계산만 공유한다. 가상은 유한 합성 재고/고정 보충량/세션 예약을 사용하며 운영 DB 후보 조회·서비스 권역·출고 원장 저장을 수행하지 않는다.

## Simulation 세션 생명주기 (`simulation-session-lifecycle`)

- **010 contract.base-reflection-learning-material** — [SimulationYouTube학습원문관측Snapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationBaseReflectionContracts.cs) · YouTube 원문 관측에서 사람 승인 학습자료까지의 3계층 계약을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 시청 시간·재생 상태·API key·원문 전체는 Simulation 보상 계약에 포함하지 않는다.
- **010 contract.online-world** — [SimulationOnlineWorldDirectorySnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationOnlineWorldContracts.cs) · 공식 지속 세계와 비공개 협동방의 조회·변경 계약을 정의한다.
  - 계층/단계: `Contract / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 온라인 세계 계약은 Solo 저장이나 운영 상태를 가져오지 않는다.
- **010 contract.session-create** — [경영SimulationSession생성Request](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/경영SimulationSessionContracts.cs) · Simulation 세션 생성 입력과 초기 World 문맥을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 운영 업무 생성 계약이 아니라 결정적 Simulation 세션 입력 계약이다.
- **015 application.observer-tick-request** — [관찰시간누적기](../../../Ssalddel.Simulation.Application/Neighborhood/관찰시간누적기.cs) · 재생 중 경과 시간으로 단일 Tick 요청 시점을 준비한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 요청 준비만 하며 Runtime 호출·권위 Tick 진행·앱 종료 시간 따라잡기를 하지 않는다.
- **020 api.session-lifecycle** — [경영SimulationSessionsController](../../../Ssalddel.Simulation.Hosting/Controllers/경영SimulationSessionsController.cs) · 세션 생성·조회·Tick HTTP 경계를 제공한다.
  - 계층/단계: `Api / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Simulation 실행 모드에서만 조립되며 오류 계약과 기존 route를 보존한다.
- **020 api.world-gameplay** — [경영SimulationWorldGameplayController](../../../Ssalddel.Simulation.Hosting/Controllers/경영SimulationWorldGameplayController.cs) · 플레이어 심리·AreaSet 이동·호스팅·협동 건설의 세계 게임플레이 HTTP 경계를 제공한다.
  - 계층/단계: `Api / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 기존 route를 보존하며 운영 상태나 Unity 표현 상태를 직접 변경하지 않는다.
- **020 domain.approved-learning-ledger** — [Simulation승인학습자료파생원장](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationBaseReflection.cs) · 승인 학습자료를 멱등 동기화하고 Simulation 세션용 불변 사본을 만든다.
  - 계층/단계: `Domain / Persistence`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Provider를 호출하거나 운영 DB를 쓰지 않고 사람 승인 Publication만 Simulation 파생 원장에 보관한다.
- **021 api.online-world** — [SimulationOnlineWorldsController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationOnlineWorldsController.cs) · 인증된 플레이어의 공식 지속 세계와 비공개 협동 방 경계를 제공한다.
  - 계층/단계: `Api / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: JWT 행위자를 서버에서 확정하고 운영 DB·운영 업무 상태를 변경하지 않는다.
- **030 application.online-world** — [SimulationOnlineWorldService](../../../Ssalddel.Simulation.Application/RuntimeCore/SimulationOnlineWorldService.cs) · 온라인 세계 조회·합류·파티·신호와 상태 사본 저장을 조율한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `PersistentRead | PersistentWrite | StateMutation`
  - 경계: 검증된 온라인 Simulation 상태만 변경하며 운영 원장을 호출하지 않는다.
- **030 application.session-lifecycle** — [경영SimulationSession생명주기Service](../../../Ssalddel.Simulation.Application/RuntimeCore/경영SimulationSession생명주기Service.cs) · 세션 생성·조회·Tick·저장·복원을 조율한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 실제 업무 상태를 만들지 않으며 기대 개정과 저장 자료 무결성을 통과한 Simulation 상태만 변경한다.
- **030 application.world-gameplay** — [경영SimulationWorldGameplayService](../../../Ssalddel.Simulation.Application/경영SimulationWorldGameplayService.cs) · 플레이어 심리·AreaSet 이동·호스팅·협동 건설의 세계 게임플레이를 조율한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 운영 상태를 변경하지 않으며 서버 세션의 권한·개정·원장을 통해서만 세계 게임플레이 상태를 변경한다.
- **030 domain.base-reflection** — [Simulation거점성찰Engine](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationBaseReflection.cs) · WI-REFLECT-01의 Preview, Confirm, 다음 활동 적용을 결정적으로 처리한다.
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 보상은 게임 안 성찰 선택에만 귀속하며 영상 재생·시청 시간·외부 Provider 결과를 읽지 않는다.
- **031 application.online-nature-session-provision** — [SimulationOnlineNatureSessionProvisioningService](../../../Ssalddel.Simulation.Application/RuntimeCore/SimulationOnlineNatureSessionProvisioningService.cs) · 온라인 AreaSet에 결속된 Nature RemoteHost 세션을 결정적으로 준비한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 현재 Nature Core는 단일 Actor만 지원한다. 세션 준비를 다중 Actor 협동 완료로 승격하지 않는다.
- **032 application.online-cooperative-logging** — [SimulationOnlineCooperativeLoggingService](../../../Ssalddel.Simulation.Application/RuntimeCore/SimulationOnlineCooperativeLoggingService.cs) · JWT 참가자를 AreaSet Actor로 결속해 협동 벌목·집중·재접속을 조율한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Simulation 행위와 계정 명상만 변경하며 운영 상태와 Unity 표현을 변경하지 않는다.
- **040 domain.online-world** — [SimulationOnlineWorldCoordinator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationOnlineWorldCoordinator.cs) · 온라인 세계·AreaSet·파티·계정 명상 원장의 결정적 상태 전이를 소유한다.
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 온라인 Simulation 상태만 소유하고 Solo 저장·운영 상태·Unity 표현을 변경하지 않는다.
- **040 domain.session-aggregate** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/경영SimulationSession.cs) · 결정적 세션 상태와 개정·Tick 상태 전이를 소유한다.
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Aggregate 상태 전이는 Simulation 전용이며 운영 계약·재고·결제를 변경하지 않는다.
- **050 infrastructure.session-store** — [InMemory경영SimulationSessionStore](../../../Ssalddel.Simulation.Infrastructure/InMemorySimulationStores.cs) · 활성 Simulation 세션을 프로세스 수명 동안 보관한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 프로세스 내부 저장소이며 durable 저장이나 다중 인스턴스 동기화를 보장하지 않는다.

## 병렬 경영-전투 (`simulation-parallel-battle`)

선행 기능: `simulation-session-lifecycle`

- **010 contract.battle-preview** — [SimulationBattleCreatePreviewRequest](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationBattleInstanceContracts.cs) · 병렬 전투 생성 미리보기의 서버 입력을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 클라이언트는 전투 결과나 수치 보정을 확정하지 않고 예상 World 개정과 안정 ID만 보낸다.
- **010 contract.local-combat-control-mode** — [SimulationLocalCombatControlModeConfirmRequest](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationLocalCombatContracts.cs) · 현장 전투의 1인칭 직접 행동과 3인칭 전술 지휘 중 하나를 서버에 확정한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 카메라 자체가 아니라 서버가 확정한 전투 조작 방식만 행동 허용 범위를 바꾼다.
- **011 contract.local-combat-action** — [SimulationLocalCombatActionConfirmRequest](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationLocalCombatContracts.cs) · 현재 H5/LH 공간에서 수행하는 전투 행동의 서버 입력을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 클라이언트는 피해·명중·미터 좌표를 제출하지 않고 대상과 행동 의도만 보낸다.
- **012 contract.local-combat-observer-intervention** — [SimulationLocalCombatObserverInterventionConfirmRequest](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationLocalCombatContracts.cs) · 관찰 운영 전투의 단일 전술 일시정지와 비상 카드 발동을 확정한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 클라이언트는 CardCopyStableId와 예상 전투 개정만 보내며 회복·피해·후퇴 결과를 계산하지 않는다.
- **020 api.battle** — [SimulationBattlesController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationBattlesController.cs) · 병렬 전투 조회·Preview·Confirm·진행 HTTP 경계를 제공한다.
  - 계층/단계: `Api / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 클라이언트가 보낸 안정 ID와 예상 개정만 받아 서버 규칙으로 전투 상태를 확정한다.
- **030 application.battle** — [SimulationBattleInstanceService](../../../Ssalddel.Simulation.Application/RuntimeCore/SimulationBattleInstanceService.cs) · 전투 Preview·Confirm·진행과 경영 World 합류를 조율한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 전투 Tick과 경영 WorldTick을 분리하고 완료 결과만 안전한 WorldTick에 합류시킨다.
- **040 domain.battle-state** — [SimulationBattleInstanceState](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationBattleInstances.cs) · 독립 BattleTick·참가·배치·지원·결과 상태 전이를 소유한다.
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 전투 상태는 SimulationOnly이며 운영 자원이나 실제 인력을 잠그지 않는다.
- **050 infrastructure.battle-store** — [InMemorySimulationBattleInstanceStore](../../../Ssalddel.Simulation.Infrastructure/InMemorySimulationBattleInstanceStore.cs) · 활성 전투와 Simulation 자원 예약을 프로세스 수명 동안 보관한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 실제 창고 재고나 인력을 잠그지 않는 process-local Simulation 저장소다.
- **060 unity.battle-presentation** — [BattlePresentationMapper](../../../Ssalddel.Unity/Runtime/Battles/SimulationBattleInstancePresentationModels.cs) · 서버 전투 Snapshot을 경영·전투 카메라와 정보판 표현 상태로 투영한다.
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 전투 결과·World 상태를 계산하거나 변경하지 않는 순수 Unity 표현 변환이다.

## 1인칭 농장 타이밍 전투 (`simulation-farm-combat-input`)

선행 기능: `simulation-session-lifecycle`

- **010 contract.farm-combat** — [SimulationCombatPerspectiveConfirmRequest](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFarmCombatContracts.cs) · 전투 시점·박자·반응 입력의 서버 계약을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: Unity는 안정 식별자·예상 개정·행동·반응 경과 시간만 제출한다.
- **020 api.farm-combat** — [SimulationFarmSurvivalController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationFarmSurvivalController.cs) · 전투 시점·박자 시작·반응 확정 HTTP 경계를 제공한다.
  - 계층/단계: `Api / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Simulation 전용 경로이며 운영 서버 권한·원장을 변경하지 않는다.
- **030 application.farm-combat** — [SimulationFarmSurvivalService](../../../Ssalddel.Simulation.Application/RuntimeCore/SimulationFarmSurvivalService.cs) · 전투 입력을 현재 Simulation Session aggregate에 전달한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: 운영 업무 상태가 아니라 Simulation Session 상태만 변경한다.
- **040 domain.farm-combat** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFarmCombat.cs) · 전투 박자·타이밍 등급·피해·전술 기회를 결정적으로 판정한다.
  - 계층/단계: `Domain / Tick`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `StateMutation`
  - 경계: Unity가 제출한 판정 결과를 신뢰하지 않고 Session aggregate가 최종 결과를 확정한다.
- **050 unity.farm-combat-input** — [FarmCombatInputCommandFactory](../../../Ssalddel.Unity/Runtime/Survival/SimulationFarmCombatPresentationModels.cs) · 서버 전투 상태를 공격 진입·방어·반격 명령 초안으로 변환한다.
  - 계층/단계: `ClientAdapter / Presentation`
  - 읽기/쓰기: `SimulationState → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: 피해·판정 등급·전술 효과를 계산하지 않고 안정 식별자와 입력 시각만 전달한다.

## Simulation 저장-재생 (`simulation-save-replay`)

선행 기능: `simulation-session-lifecycle`, `simulation-parallel-battle`

- **010 contract.save-request** — [SimulationSessionSaveRequest](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationSaveReplayContracts.cs) · 세션 저장 식별자와 기대 개정을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 저장 자료는 Simulation 상태만 포함하며 운영 원장과 공공데이터 원본을 복제하지 않는다.
- **020 api.save-replay** — [경영SimulationSessionsController](../../../Ssalddel.Simulation.Hosting/Controllers/경영SimulationSessionsController.cs) · 세션 저장·복원 HTTP 경계를 제공한다.
  - 계층/단계: `Api / Persistence`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 저장 식별자와 기대 개정을 서버가 검증하며 운영 서버 저장 API로 전달하지 않는다.
- **030 application.save-replay** — [경영SimulationSession생명주기Service](../../../Ssalddel.Simulation.Application/RuntimeCore/경영SimulationSession생명주기Service.cs) · 세션 저장·복원과 전투 저장 자료 결합을 조율한다.
  - 계층/단계: `Application / Persistence`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 검증된 simulation-save.v1·v2 자료만 저장·복원하며 활성 세션을 임의로 덮어쓰지 않는다.
- **040 domain.save-package** — [경영SimulationSessionAggregate](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationSaveReplay.cs) · 세션 Snapshot과 Command log를 봉인한 저장 자료로 만든다.
  - 계층/단계: `Domain / Persistence`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 재생 hash와 schema가 일치하는 Simulation 자료만 만들며 운영 원장을 직렬화하지 않는다.
- **050 infrastructure.save-store** — [SimulationSessionSaveStore](../../../Ssalddel.Simulation.Persistence/SimulationSessionSavePersistence.cs) · 검증된 세션 저장 자료 JSON과 재생 hash를 Simulation 전용 DB에 보관한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `SimulationState → SimulationState`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 공유 공공데이터 DB가 아니라 별도 SimulationSession DB만 읽고 쓴다.

## 공공데이터-파생 World (`simulation-world-derivation`)

- **009 contract.nature-shelter-purpose** — [SimulationNatureShelterPurposeReadinessSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · Nature 오두막의 1차 목적과 핵심·보조 효용의 구현 준비 상태를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 안전한 수면을 1차 목적으로 고정하며 보관 용량을 체온·피로·질병 규칙의 대체 증거로 사용하지 않는다.
- **010 contract.nature-sleep-safety-candidate** — [SimulationNatureSleepSafetyCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · Nature 수면 안전 단계의 기획 후보와 구현 공백을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: Q002의 합성 후보를 확정 규칙과 구분하며 수면 허용·연료·질병 결과를 결정하지 않는다.
- **010 domain.derived-world-ledger** — [SimulationWorld파생원장](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWorldDerivation.cs) · 공간 원본 계보·파생 node·관계·배치 계획을 불변 실행 단위로 정의한다.
  - 계층/단계: `Domain / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 관측·파생·통계배분·시나리오·장식 근거를 분리하며 추정 위치를 실제 사실로 승격하지 않는다.
- **011 contract.nature-risky-sleep-warning** — [SimulationNatureRiskySleepWarningSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · 위험 수면 허용과 경고 표시의 난이도·사용자 설정 경계를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 경고 가시성은 정보 표현이며 수면 안전 판정이나 실제 위험도를 변경하지 않는다.
- **012 contract.nature-difficulty-boundary** — [SimulationNatureDifficultyBoundarySnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · Nature 난이도의 공통 수면 판정식과 별도 위협 출몰 Profile 경계를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 난이도는 같은 주변 상태의 수면 안전 공식을 바꾸지 않고 출몰 입력과 경고 정보량만 선택한다.
- **013 contract.nature-expert-threat-candidate** — [SimulationNatureExpertThreatCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · 숙련자 위협의 빈도·무리 규모·개별 능력 강화와 기존 집중 체계 결속 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 강화 차원과 기존 집중 Profile 결속을 기술하며 수치·보상·집중 부족 결과를 확정하지 않는다.
- **014 contract.meditation-focus-access-candidate** — [Simulation명상집중접근CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFocusMeditationContracts.cs) · 명상 숙련에 따른 일상 행동·기본 공격의 집중 접근 확대 후보와 기존 집중 판정 경계를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 숙련 접근 후보를 정의하지만 기본 공격 피해·크리티컬·순간 집중 역할을 임의로 확정하지 않는다.
- **014 contract.nature-risky-sleep-outcome-candidate** — [SimulationNatureRiskySleepOutcomeCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · 위험 수면 중 동물·몬스터 접근은 강제 각성과 전투·후퇴 선택으로, 추위·강수·질병 위험은 기상 결과로 분리해 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 중단·누적 결과 종류만 정의하며 실제 전투 생성·피로·체온·질병 변경이나 기상청 자료 동결을 확정하지 않는다.
- **015 contract.meditation-combat-progression-candidate** — [Simulation명상전투성장CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFocusMeditationContracts.cs) · 명상 숙련의 크리티컬 확률·기본 피해 안정화·심층 관찰 인계 성장 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 성장 순서를 정의하지만 승인 전 기본 공격 피해·크리티컬 결과·관찰 정보를 변경하지 않는다.
- **015 contract.nature-weather-profile-freeze-candidate** — [SimulationNatureWeatherProfileFreezeCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · 품질 승인 관측을 새 세계·하루 시작 경계에서 일반화된 날씨 Profile과 출처 hash·규칙 판본으로 봉인하는 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SharedPublicData → None`
  - 부수효과: `None`
  - 경계: 외부 API 응답을 플레이 중 직접 반영하지 않으며 실제 수집·품질 승인·Save 판본·Sky 표현을 수행하지 않는다.
- **016 contract.deep-observation-progression-candidate** — [Simulation심층관찰CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFocusMeditationContracts.cs) · 명상 성장에 따른 환경·전투·사회 성장 낌새의 단계적 관찰 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 관찰 계층을 정의하지만 권한 없는 원본 행동 기록·인벤토리·정확한 성장 수치를 노출하지 않는다.
- **016 contract.landscape-composition-tile** — [SimulationWorldLandscapeCompositionTileResponse](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldLandscapeCompositionContracts.cs) · 공간 근거로 조립된 경관 Graph와 의미 기반 Composition 배치를 Unity에 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: Prefab 경로·GUID·상품명은 노출하지 않으며, 응답은 표현 계획이지 운영 사실이나 실제 시설 존재의 확정이 아니다.
- **016 contract.nature-sleep-protection-spatial-layer-candidate** — [SimulationNatureSleepProtectionSpatialLayerCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureSurvivalContracts.cs) · 침상·오두막 실내·열원 영향권·울타리 물리 경계·마법진 상위 위협 경계를 중첩 가능한 수면 보호 공간층으로 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 공간층 역할과 배치·형상·Graph 근거 요구만 정의하며 실제 범위·좌표·충돌·보호 결과를 확정하지 않는다.
- **016 contract.world-map-composition** — [ISimulation지도구성Engine](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldAssetPlacementContracts.cs) · 지면·셀·H·연결구를 자산 선택 없는 지도구성 계획으로 만든다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 건물·환경·실내 VisualKey와 Spawn 확률을 선택하지 않는다.
- **017 contract.environment-spawn-decision** — [ISimulation환경발생DecisionEngine](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldAssetPlacementContracts.cs) · 결정적 환경 발생 후보와 선택 결과를 계산한다.
  - 계층/단계: `Contract / Projection`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: SimulationEntity 결과는 WorldTick Confirm 없이 권위 상태가 되지 않는다.
- **017 contract.landscape-graph** — [SimulationWorldLandscapeGraphResponse](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldLandscapeCompositionContracts.cs) · 여러 타일과 Area를 참조하는 하나의 경관 Graph를 Unity에 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: Graph는 표현용 공간 구조이며 Unity의 로드 상태나 운영 업무 상태를 확정하지 않는다.
- **017 contract.player-growth-hint-projection** — [Simulation성장낌새ProjectionSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationPlayerDomainProficiencyContracts.cs) · 다른 플레이어의 승인된 성장 분야를 정확한 수치가 아닌 정성적 낌새로 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 승인된 분야와 정성 단계만 전달하며 기여 기록·정확한 수치·해금·인벤토리를 노출하지 않는다.
- **018 contract.interior-layout-plan** — [I실내공간조립Engine](../../../Ssalddel.Interior.Contracts/UnityPackage/Runtime/InteriorLayoutContracts.cs) · H 의미와 건물 문맥을 결정적 InteriorPlacementPlan으로 만드는 엔진 계약이다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 새 H 단계, Simulation 상태, Prefab 경로, 재고·가격·소유권을 만들지 않는다.
- **018 contract.party-proximity-resonance-candidate** — [Simulation파티근접공명CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFocusMeditationContracts.cs) · 같은 파티의 승인된 명상 숙련자가 가까이 있을 때 역할 배정 없는 수동 공명 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 공명 발생 조건만 정의하며 역할을 제안·수락·배정하거나 회복·위협·행위 능력치를 변경하지 않는다.
- **019 contract.marketplace-grounded-interior-item** — [I상품특성효과DerivationEngine](../../../Ssalddel.Interior.Contracts/UnityPackage/Runtime/상품근거ItemContracts.cs) · 승인 상품 Reference를 게임용 특성·효과 정의와 범주형 VisualKey로 결속하는 계약이다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 효과 정의만 만들며 WI Confirm·WorldTick·재고·소유권·운영 상품 성능을 확정하지 않는다.
- **019 contract.party-resonance-recovery-candidate** — [Simulation파티공명회복CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 파티 근접 공명의 첫 결과를 분야별 직접 버프가 아닌 대상 플레이어의 개인 회복 축 후보로 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 회복 축 선택만 정의하며 크기·지속·중첩·기간 진입이나 전투·제작·채집 능력치를 변경하지 않는다.
- **019 contract.world-asset-placement** — [ISimulation세계자산배치Engine](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldAssetPlacementContracts.cs) · 지도와 권위 변화에서 환경·건물·실내 자산 배치 계획을 만든다.
  - 계층/단계: `Contract / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: LH 상세도와 Unity Prefab은 결정하지 않으며 권위 Spawn을 직접 확정하지 않는다.
- **019 domain.interior-layout-generate** — [DeterministicInteriorLayoutEngine](../../../Ssalddel.Interior.Domain/UnityPackage/Runtime/DeterministicInteriorLayoutEngine.cs) · Structure·Zone·Fixture·Surface·Slot과 승인 Reference를 결정적 실내 배치 계획으로 조립한다.
  - 계층/단계: `Domain / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 같은 입력은 같은 hash를 만들며 WorldTick·재고·가격·소유권을 변경하지 않는다.
- **019 domain.nature-shelter-purpose-readiness** — [SimulationNatureShelterPurposeReadinessEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureShelterPurposeReadinessEvaluator.cs) · 현재 Nature 오두막 상태가 안전한 수면의 핵심 효용을 실제로 구현했는지 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 기존 RecoveryAvailable을 체온·피로·질병의 개별 구현으로 간주하지 않고 확인된 효용만 Ready로 판정한다.
- **020 application.pyeongchang-derivation** — [평창군공간파생Pipeline](../../../Ssalddel.Simulation.Persistence/PyeongchangWorldDerivationPipeline.cs) · 평창군 공공데이터를 읽어 대표 건물·공간 관계·Unity 타일 계획을 결정적으로 조립한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `SharedPublicData → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 공유 공공데이터는 읽기 전용이며 건물 도형이나 DEM이 없으면 임의 좌표를 생성하지 않는다.
- **020 contract.party-resonance-afterglow-candidate** — [Simulation파티공명잔향CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 근접 파티 공명이 끝난 뒤 회복 효과가 즉시 사라지지 않고 권위 시간 기반 잔향으로 감쇠하는 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 잔향의 시간 책임만 정의하며 지속시간·감쇠 계수·중첩·Save 상태나 Unity deltaTime 계산을 확정하지 않는다.
- **020 domain.marketplace-grounded-item-effect-derive** — [상품특성효과DerivationEngine](../../../Ssalddel.Interior.Domain/UnityPackage/Runtime/상품특성효과DerivationEngine.cs) · 승인된 상품 특성을 revision 고정 규칙으로 게임용 효과 정의에 결정적으로 변환한다.
  - 계층/단계: `Domain / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 상품명 문자열이나 실시간 Marketplace 상태로 효과를 임의 생성하지 않고 Simulation 상태를 변경하지 않는다.
- **020 domain.nature-sleep-safety-candidate-readiness** — [SimulationNatureSleepSafetyCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureSleepSafetyCandidateEvaluator.cs) · 주변 상황별 수면 안전 기획 후보의 보호 수단과 질병 증분 범위 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 후보 준비도만 읽으며 위험 수면 허용·차단, 불 연료 소비, 질병 발병·회복을 실행하지 않는다.
- **021 contract.party-resonance-stacking-candidate** — [Simulation파티공명중첩CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 여러 파티 공명을 강도와 고유 식별자로 결정적 정렬하고 최강 기여는 온전히, 후속 기여는 순위 감쇠 대상으로 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 중첩 순서와 정책 종류만 정의하며 감쇠 계수·최대 인원·광복기 진입 상한이나 NatureMind 상태를 확정하지 않는다.
- **021 domain.nature-risky-sleep-outcome-candidate** — [SimulationNatureRiskySleepOutcomeCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureRiskySleepOutcomeCandidateEvaluator.cs) · 수면 안전 후보와 위협·날씨 노출을 읽어 강제 각성 인계와 기상 누적 결과를 결정적 후보로 분리한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 결과 후보만 판정하며 수면 Task·전투·피로·체온·질병·WorldRevision을 변경하지 않는다.
- **021 domain.nature-risky-sleep-warning-policy** — [SimulationNatureRiskySleepWarningPolicyResolver](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureRiskySleepWarningPolicyResolver.cs) · 위험 수면의 모드 기본값과 사용자 설정으로 경고 가시성을 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 경고 표시만 판정하고 수면 선택을 차단하거나 Simulation 위험·회복 수치를 바꾸지 않는다.
- **022 contract.gwangbok-resonance-entry-cap-candidate** — [Simulation광복기공명상한CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 파티 공명은 광복기 진입 직전까지만 돕고 대상 플레이어 자신의 회복 기여가 마지막 문턱을 넘게 하는 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 공명 단독 진입 금지와 자기 행위 필요만 정의하며 인정 WI·정확한 여유 폭·기간 전이를 확정하지 않는다.
- **022 domain.nature-difficulty-boundary** — [SimulationNatureDifficultyBoundaryResolver](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureDifficultyBoundaryResolver.cs) · Nature 난이도에서 공통 수면 판정식과 별도 위협 출몰 Profile을 선택한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: Profile revision을 선택할 뿐 출몰을 생성하거나 수면 안전 결과·WorldRevision을 변경하지 않는다.
- **022 domain.nature-weather-profile-freeze-candidate** — [SimulationNatureWeatherProfileFreezeCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureWeatherProfileFreezeCandidateEvaluator.cs) · 위험 수면 날씨 입력과 승인 관측 계보를 읽어 하루 날씨 Profile 동결 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SharedPublicData → None`
  - 부수효과: `None`
  - 경계: 동결 후보 준비도만 판정하며 외부 호출·날씨 상태·Save·Sky 표현을 변경하지 않는다.
- **023 contract.gwangbok-self-recovery-action-candidate** — [Simulation광복기자기회복행위CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFocusMeditationContracts.cs) · 명상(정신 차림) 또는 집중 성공으로 실제 개인 회복 기여를 남긴 WI만 광복기 마지막 자기 행위 후보로 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: ActionRecord·회복 변화·집중 성공 자격만 정의하며 완전한 수면은 제외하고 실제 기간 전이를 적용하지 않는다.
- **023 domain.nature-expert-threat-candidate-readiness** — [SimulationNatureExpertThreatCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureExpertThreatCandidateEvaluator.cs) · 숙련자 위협 강화 세 축과 기존 집중 Profile 결속의 구현 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 준비도만 판정하고 Spawn·전투·보상·집중 자원·WorldRevision을 변경하지 않는다.
- **023 domain.nature-sleep-protection-spatial-layer-candidate** — [SimulationNatureSleepProtectionSpatialLayerCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureSleepProtectionSpatialLayerCandidateEvaluator.cs) · 수면 보호 공간층의 역할·배치·형상·경계 Graph 근거를 읽어 중첩 공간 후보 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 공간층 근거 준비도만 판정하며 H 정의·배치·Collider·Graph·보호 상태를 변경하지 않는다.
- **024 contract.first-logging-reflection-seed** — [Simulation첫벌목성찰SeedSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFirstLoggingReflectionContracts.cs) · 실제 첫 벌목 행위 기록과 한스 집 안전 휴식 근거에서 관찰·원인·개선 성찰 씨앗을 준비한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: ActionRecord와 안전 휴식 근거를 읽을 뿐 벌목·휴식·보상·편린을 새로 만들지 않는다.
- **024 contract.gwangbok-resonance-maintenance-candidate** — [Simulation광복기공명유지CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 자기 행위로 진입한 광복기에서 공명·잔향은 감쇠를 늦추지만 영구 유지하지 못하고 주기적 자기 회복 행위를 요구한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 유지 책임만 정의하며 실제 감쇠율·자기 행동 주기·기간 이탈·WorldTick 상태를 확정하지 않는다.
- **024 domain.meditation-focus-access-candidate-readiness** — [SimulationMeditationFocusAccessCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationMeditationFocusAccessCandidateEvaluator.cs) · 명상 숙련의 집중 접근 확대 후보가 기존 집중 판정과 결속될 준비가 되었는지 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: revision 준비도만 판정하고 숙련·집중·공격 피해·크리티컬·WorldRevision을 변경하지 않는다.
- **025 contract.personal-recovery-decay-candidate** — [Simulation개인회복감쇠CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 개인 회복 감쇠를 권위 게임 시간 기본 감쇠와 위협·피로·집중 실패 추가 감쇠로 분리해 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 감쇠 원인과 순서만 정의하며 계수·오프라인 시간·Recovery 상태·기간 이탈을 확정하지 않는다.
- **025 domain.meditation-combat-progression-candidate-readiness** — [SimulationMeditationCombatProgressionCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationMeditationCombatProgressionCandidateEvaluator.cs) · 명상 숙련의 전투 보상 성장 후보가 승인된 전투 Effect에 인계될 준비가 되었는지 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 성장 revision 준비도만 판정하고 현재 전투 피해·크리티컬·관찰·WorldRevision을 변경하지 않는다.
- **026 contract.personal-recovery-offline-time-candidate** — [Simulation개인회복오프라인시간CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 게임 종료 중 현실 시간에는 개인 회복 감쇠를 멈추고 Save 복원 뒤 권위 게임 시간이 재개될 때만 이어가는 후보를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 벽시계 경과시간을 입력으로 사용하지 않으며 실제 Save 판본·복원·감쇠 적용이나 위협 상쇄를 확정하지 않는다.
- **026 domain.deep-observation-progression-candidate-readiness** — [SimulationDeepObservationCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationDeepObservationCandidateEvaluator.cs) · 환경·전투·사회 성장 낌새의 단계적 관찰 Projection 후보 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: Projection revision 준비도만 판정하고 권한을 만들거나 비공개 원본·정확한 성장 수치를 노출하지 않는다.
- **027 contract.personal-recovery-threat-offset-candidate** — [Simulation개인회복위협상쇄CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 개인 회복이 같은 플레이어의 위협을 낮추고 명상·정신 차림 성공과 숙련도가 상쇄·기간 문턱 후보에 기여하는 경계를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 상쇄와 숙련도 문턱 완화 후보만 정의하며 비율·곡선·기간 충돌·실제 Mind Effect를 확정하지 않는다.
- **027 domain.environment-spawn-decision** — [Simulation결정적환경발생DecisionEngine](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWorldAssetPlacement.cs) · 변화 Context가 적용된 환경 발생 가중치를 결정적으로 판정한다.
  - 계층/단계: `Domain / Projection`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 결정은 후보이며 SimulationEntity는 WorldTick Effect가 별도로 확정해야 한다.
- **027 domain.player-growth-hint-projection** — [SimulationPlayerGrowthHintProjection](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPlayerGrowthHintProjection.cs) · 승인된 플레이어 분야 진척을 정확한 수치 없이 정성적 성장 낌새로 투영한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 기존 분야 Profile을 읽지만 기여 기록·정확한 수치·해금·인벤토리를 결과에 복사하지 않는다.
- **028 application.world-map-composition** — [SimulationScenario지도구성Engine](../../../Ssalddel.Simulation.Application/SimulationWorldAssetPlacementPlanning.cs) · 기존 LH 시나리오 지식에서 객체 선택 없는 지도구성 계획을 만든다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 환경·건물·실내 자산과 Prefab을 선택하지 않는다.
- **028 contract.dark-age-mindfulness-access-candidate** — [Simulation암흑기정신차림접근CandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 극한 위협에서는 암흑기를 지배 기간으로 유지하면서 극한 명상 숙련자의 제한적 광복기 계열 효과 접근 후보를 별도 상태로 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 단일 PeriodStateCode를 보존하며 허용 효과 범위·강도·유지 비용이나 실제 기간 전이를 확정하지 않는다.
- **028 domain.party-proximity-resonance-candidate** — [SimulationPartyProximityResonanceCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPartyProximityResonanceCandidateEvaluator.cs) · 같은 파티·근접·승인된 명상 자격을 읽어 역할 배정 없는 수동 공명 후보를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 입력된 승인 판정과 파티 문맥만 읽으며 명상 Profile·NatureMind·역할·WorldRevision을 변경하지 않는다.
- **029 application.world-asset-placement** — [Simulation결정적세계자산배치Engine](../../../Ssalddel.Simulation.Application/SimulationWorldAssetPlacementPlanning.cs) · 지도·공간 변화·결정적 Spawn에서 환경·건물·실내 계획을 조립한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `SimulationState | DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 권위 Spawn과 건물 상태를 만들지 않고 LH 상세도와 Prefab을 결정하지 않는다.
- **029 contract.dark-age-mindfulness-effect-scope-candidate** — [Simulation암흑기정신차림EffectScopeCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 암흑기 안의 제한 접근을 개인 전투 집중·심층 관찰·정밀 제작 효과로 한정하고 세계·공동체 효과를 차단하는 Profile을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 효과 범주와 소비자 허용 여부만 정의하며 강도·지속·실제 전투·관찰·제작 수치를 적용하지 않는다.
- **029 domain.party-resonance-recovery-candidate** — [SimulationPartyResonanceRecoveryCandidateResolver](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPartyResonanceRecoveryCandidateResolver.cs) · Q010의 근접 공명 후보를 개인 Recovery 축 후보로만 해석하고 분야별 직접 버프를 만들지 않는다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 효과 축 후보만 반환하며 NatureMind Effect를 추가하거나 기간·행위 능력치·WorldRevision을 변경하지 않는다.
- **030 adapter.lh-separated-cell-content** — [SimulationSeparatedLhCellContentSource](../../../Ssalddel.Simulation.Application/SimulationSeparatedLhCellContentSource.cs) · 분리된 지도구성·세계자산배치 결과를 기존 LH 셀 계약으로 변환한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: LH는 배치 규칙을 소유하지 않고 이미 계산된 계획의 준비·활성·해제만 담당한다.
- **030 application.landscape-placement-binding-guard** — [Simulation경관배치소비검증Service](../../../Ssalddel.Simulation.Application/Simulation경관배치소비검증Service.cs) · 기준 배치를 보존하며 경관 장식의 공통 소비 입력을 검사한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 입력·권위 상태·WorldRevision·배치 계획을 변경하지 않는 순수 사전검사다.
- **030 application.nature-world-asset-placement-state** — [SimulationNature세계자산배치Service](../../../Ssalddel.Simulation.Application/SimulationWorldAssetPlacementPlanning.cs) · Nature 권위 상태를 지도·공간 변화 뒤 실외·실내 계획으로 분리하고 호환 상태 사본으로 조립한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 플레이어 변화 정보를 읽어 표현 계획을 만들 뿐 Simulation 권위 상태를 변경하지 않는다.
- **030 application.separated-world-asset-placement** — [Simulation분리세계자산배치Coordinator](../../../Ssalddel.Simulation.Application/SimulationSeparatedWorldAssetPlacement.cs) · 통합 세계자산 계획을 실외 배치와 실내 배치 계획으로 분리한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 기존 v26 계획과 hash를 변경하지 않고 파생 실행 계획만 만든다.
- **030 contract.dark-age-mindfulness-effect-strength-candidate** — [Simulation암흑기정신차림EffectStrengthCandidateSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationNatureMindContracts.cs) · 암흑기 정신 차림 효과의 접근은 현재 회복 비중으로, 접근 후 강도는 장기 명상 숙련도로 판정하도록 입력 책임을 분리한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 접근·강도 입력 소유자만 정의하며 정확한 숙련 곡선·효과량·지속·비용이나 실제 Effect를 확정하지 않는다.
- **030 domain.party-resonance-afterglow-candidate** — [SimulationPartyResonanceAfterglowCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPartyResonanceAfterglowCandidateEvaluator.cs) · Q011 회복 공명 후보와 판본화된 시간 정책을 읽어 권위 Tick 기반 잔향 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 잔향 준비도만 판정하며 WorldTick·Save·NatureMind·Unity 프레임 상태를 변경하지 않는다.
- **030 infrastructure.derived-world-store** — [SimulationWorld파생원장Store](../../../Ssalddel.Simulation.Persistence/SimulationWorldDerivationPersistence.cs) · 파생 World 원장과 입력·출력 hash를 별도 DB에 멱등 저장한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `DerivedWorld → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: SimulationWorldDerived DB만 변경하며 입력 fingerprint가 다른 같은 식별자는 충돌로 거부한다.
- **031 application.nature-world-cell-assembly** — [SimulationNatureWorldCellAssemblyEngine](../../../Ssalddel.Simulation.Application/RuntimeCore/SimulationNatureWorldCellAssemblyEngine.cs) · LH 셀과 Nature 상태에서 독립 실외·실내 배치 인계 자료를 조립한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `SimulationState | DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 지면·셀은 LH, Prefab 생명주기는 Unity 배치 Runtime이 소유하며 이 엔진은 권위 상태를 변경하지 않는다.
- **031 application.world-asset-plan-partition** — [Simulation결정적세계자산배치Plan분리Service](../../../Ssalddel.Simulation.Application/SimulationWorldAssetPlacementPlanPartitioning.cs) · 봉인된 통합 세계자산 배치 계획을 실외·실내 실행 계획으로 결정적으로 분리한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 통합 계획과 WorldRevision을 다시 계산하지 않고 동일한 계획 identity의 실행 입력만 만든다.
- **031 domain.party-resonance-stacking-candidate** — [SimulationPartyResonanceStackingCandidatePlanner](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPartyResonanceStackingCandidatePlanner.cs) · 여러 공명 제공자를 강도 내림차순·고유 식별자 오름차순으로 정렬해 최강 전체·후속 감쇠 후보를 계획한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 결정적 순위와 감쇠 필요 여부만 반환하며 최종 합산량·상한·NatureMind·WorldRevision을 변경하지 않는다.
- **032 domain.gwangbok-resonance-entry-cap-candidate** — [SimulationGwangbokResonanceEntryCapCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationGwangbokResonanceEntryCapCandidateEvaluator.cs) · 공명 중첩 후보와 자기 회복 기여 여부를 읽어 광복기 마지막 문턱의 주도권 후보를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 진입 자격 후보만 판정하며 Recovery 수치·기간 상태·ActionRecord·WorldRevision을 변경하지 않는다.
- **033 domain.gwangbok-self-recovery-action-candidate** — [SimulationGwangbokSelfRecoveryActionCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationGwangbokSelfRecoveryActionCandidateEvaluator.cs) · 판본화된 WI Profile과 ActionRecord·회복 변화·집중 결과를 읽어 광복기 자기 회복 행위 후보를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 기존 기록을 읽어 자격만 판정하며 회복 Effect·기간·ActionRecord·WorldRevision을 변경하지 않는다.
- **034 domain.gwangbok-resonance-maintenance-candidate** — [SimulationGwangbokResonanceMaintenanceCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationGwangbokResonanceMaintenanceCandidateEvaluator.cs) · 광복기·자기 진입 행위·공명·잔향을 읽어 유지 보조와 자기 회복 갱신 필요를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 유지 준비도만 판정하며 Recovery·기간·WorldTick·Save 상태를 변경하지 않는다.
- **035 domain.personal-recovery-decay-candidate** — [SimulationPersonalRecoveryDecayCandidatePlanner](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPersonalRecoveryDecayCandidatePlanner.cs) · 권위 시간과 위협·피로·집중 실패 상태를 읽어 개인 회복 감쇠 원인의 결정적 계산 순서를 계획한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 원인 순서만 계획하며 감쇠 계수·Recovery·기간·WorldTick 상태를 변경하지 않는다.
- **036 domain.personal-recovery-offline-time-candidate** — [SimulationPersonalRecoveryOfflineTimeCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPersonalRecoveryOfflineTimeCandidateEvaluator.cs) · 개인 회복 감쇠 후보와 Save 기준 Tick을 읽어 오프라인 현실 시간 정지·권위 게임 시간 재개 정책의 준비도를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 오프라인 정책 준비도만 판정하며 벽시계·Recovery·Save·WorldTick 상태를 읽거나 변경하지 않는다.
- **037 domain.personal-recovery-threat-offset-candidate** — [SimulationPersonalRecoveryThreatOffsetCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPersonalRecoveryThreatOffsetCandidateEvaluator.cs) · 개인 회복·위협·명상 자기 행위·숙련도를 읽어 위협 상쇄와 광복기 문턱 완화 후보를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 후보 자격만 판정하며 상쇄량·기간 문턱·Recovery·Threat·WorldTick 상태를 변경하지 않는다.
- **038 domain.dark-age-mindfulness-access-candidate** — [SimulationDarkAgeMindfulnessAccessCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationDarkAgeMindfulnessAccessCandidateEvaluator.cs) · 개인 기간·회복 비중·위협 비중·명상 숙련도를 읽어 암흑기 우세와 제한적 정신 차림 효과 접근 후보를 판정한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 접근 후보만 판정하며 PeriodStateCode·Effect 권한·Recovery·Threat 상태를 변경하지 않는다.
- **039 domain.dark-age-mindfulness-effect-scope-candidate** — [SimulationDarkAgeMindfulnessEffectScopeCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationDarkAgeMindfulnessEffectScopeCandidateEvaluator.cs) · 암흑기 정신 차림 접근 후보와 판본화된 Effect Profile을 읽어 개인 효과만 허용하고 세계 효과를 거부한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: Profile 허용 여부만 판정하며 Effect·전투·관찰·제작·세계 상태를 변경하지 않는다.
- **040 domain.dark-age-mindfulness-effect-strength-candidate** — [SimulationDarkAgeMindfulnessEffectStrengthCandidateEvaluator](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationDarkAgeMindfulnessEffectStrengthCandidateEvaluator.cs) · 허용된 개인 정신 차림 효과와 현재 회복·장기 명상 숙련도를 읽어 접근과 강도 입력 책임을 분리한다.
  - 계층/단계: `Domain / Query`
  - 읽기/쓰기: `SimulationState → None`
  - 부수효과: `None`
  - 경계: 입력 책임과 준비도만 판정하며 효과 강도·Recovery·숙련도·세계 상태를 변경하지 않는다.
- **041 unity.marketplace-grounded-item-detail** — [상품근거ItemDetailProjection](../../../Ssalddel.Unity/Runtime/Interiors/상품근거ItemDetailProjection.cs) · 상품 근거 Item 정의를 범주형 Synty 표현과 읽기 전용 특성·효과 상세로 투영한다.
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `DerivedWorld → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: Unity는 특성·효과를 재계산하거나 World 상태에 적용하지 않고 승인 근거와 정의를 표시만 한다.
- **042 domain.landscape-graph-assembler** — [SimulationWorldLandscapeGraphAssembler](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWorldLandscapeAssembly.cs) · Macro·Meso 공간 골격을 156개 의미 모판의 연결·반복 문법으로 결정적으로 조립한다.
  - 계층/단계: `Domain / Projection`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 실제 도로가 없는 연결은 Scenario 근거를 유지하며, Micro 장식 좌표와 Prefab은 Unity wrapper가 결정한다.
- **044 application.landscape-graph-job** — [SimulationWorldLandscapeCompositionJobShell](../../../Ssalddel.Simulation.Application/SimulationWorldLandscapeCompositionService.cs) · 공간 Layer 준비 상태를 확인하고 네 L2 타일의 경관 Graph를 조립·저장한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 자료가 없는 타일은 꾸며내지 않고 대기 상태로 저장하며 Unity Prefab을 해석하지 않는다.
- **100 application.neighborhood-import** — [동네공간ImportService](../../../Ssalddel.Simulation.Application/Neighborhood/동네공간ImportService.cs) · 전처리된 동네 도형을 검증하고 불변 공간 후보로 읽는다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `SharedPublicData → None`
  - 부수효과: `None`
  - 경계: 주어진 바이트만 읽는다. 실제 자료 확보·좌표 재투영·DB/World 적용은 별도다.
- **110 application.neighborhood-route** — [동네이동경로Engine](../../../Ssalddel.Simulation.Application/Neighborhood/동네이동경로Engine.cs) · 정규화 동네 공간의 방향·통행 조건에 맞는 경로 후보를 반환한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 불변 공간 후보를 읽는 순수 계산이며 실제 배차·Actor 이동·주문 상태는 변경하지 않는다.
- **120 application.neighborhood-movement-candidate** — [동네이동진행Engine](../../../Ssalddel.Simulation.Application/Neighborhood/동네이동진행Engine.cs) · 경로 후보의 거리 진행·차단·복원 후보를 계산한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 기존 Actor/주문/Session 상태를 변경하지 않는 후보 계산이다.
- **130 application.synthetic-food-delivery-journey-projection** — [음식배달여정SnapshotFactory](../../../Ssalddel.Simulation.Application/Neighborhood/음식배달여정Projection.cs) · 합성 음식 배달 상태와 판본화된 이동망을 Unity 읽기 전용 여정으로 투영한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: SyntheticFixture 전용 읽기 사본이며 도착·보간으로 주문·픽업·전달 상태를 변경하지 않는다.

## 독립 Synty 경관 처리 (`simulation-synty-landscape`)

선행 기능: `simulation-world-derivation`

- **010 domain.synty-ledger** — [SimulationWorldSynty경관실행원장](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWorldSyntyLandscape.cs) · 공간 출력과 Synty·URP 대장 개정을 결합한 경관 실행 결과를 정의한다.
  - 계층/단계: `Domain / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 경관 실행은 표현 계획이며 공간 원본·법정동·Simulation 상태를 변경하지 않는다.
- **030 application.synty-job** — [SimulationWorldSynty경관JobShell](../../../Ssalddel.Simulation.Application/SimulationWorldSyntyLandscapeJobShell.cs) · 공간 실행을 읽어 Synty·URP 경관 계획을 만들고 별도 실행 원장으로 저장한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: Synty 원본 경로나 Prefab 이름을 업무 권위로 사용하지 않고 공간 출력 hash를 입력으로 삼는다.
- **040 infrastructure.synty-store** — [SimulationWorldSynty경관Store](../../../Ssalddel.Simulation.Persistence/SimulationWorldSyntyLandscapePersistence.cs) · Synty 경관 실행과 그래픽·배치·거부 결과를 별도 파생 DB에 저장한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `DerivedWorld → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 공간 실행과 별도 fingerprint를 사용하며 Synty 대장 변경이 공간 원장을 다시 쓰게 하지 않는다.

## L2 타일 Streaming (`simulation-world-streaming`)

선행 기능: `simulation-world-derivation`

- **010 contract.stream-recipe** — [SimulationWorldStreamRecipeResponse](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldStreamingContracts.cs) · L2 타일 활성·준비 범위와 사전 적재 규칙을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: Recipe는 제공 범위와 로드 정책이며 Unity가 전체 타일을 동시에 생성하라는 명령이 아니다.
- **011 contract.lh-world-profile** — [SimulationLhWorldProfileResponse](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationLhWorldContracts.cs) · L 해상도와 H 공간 계보를 결합하는 LH World 생성·스트리밍 Profile을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: Profile과 Cell Preview는 Simulation 공간 후보이며 H 권위나 운영 상태를 새로 확정하지 않는다.
- **012 contract.lh-asset-plan-lifecycle** — [SimulationLhAssetPlanLifecycleSnapshot](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationLhAssetPlanLifecycleContracts.cs) · 동결된 실외·실내 배치 계획의 LH 표현 수명주기 상태를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 수명주기 Revision은 표현 준비 상태이며 Simulation WorldRevision이나 배치 내용을 변경하지 않는다.
- **020 api.world-stream** — [SimulationWorldStreamingController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationWorldStreamingController.cs) · 타일 Recipe·Manifest·Layer·객체 Projection 조회 경계를 제공한다.
  - 계층/단계: `Api / Query`
  - 읽기/쓰기: `SimulationState | DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 조회와 eligibility Preview는 타일이나 업무 상태를 생성·확정하지 않는다.
- **025 api.world-region-summary** — [SimulationWorldRegionSummaryController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationWorldRegionSummaryController.cs) · 지역·타일별 대표 정보와 가까운 공개 객체의 제한된 상세정보를 제공한다.
  - 계층/단계: `Api / Query`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 요약 응답에는 상호명을 넣지 않고 명시적인 공개 상세 조회에서만 공개 공공데이터 상호명을 반환한다.
- **028 contract.world-layout-definition** — [SimulationWorldLayoutDefinitionResponse](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationWorldLayoutContracts.cs) · H4 AreaSet과 H3 회랑의 H5 상대 공간 배치를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: ScenarioRelative H5는 E6 없이도 권위 세계이며 AreaSetNetwork나 Simulation 상태를 변경하지 않는다.
- **029 contract.area-set-handover-plan** — [SimulationAreaSetHandoverPlanResponse](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationAreaSetHandoverContracts.cs) · 현재 AreaSet에서 물리 회랑으로 이어지는 다음 AreaSet의 단계별 준비 후보를 전달한다.
  - 계층/단계: `Contract / Preview`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 인계 계획은 자료·메모리 준비 후보이며 현재 AreaSet, 이동 권한, WorldTick 또는 운영 상태를 변경하지 않는다.
- **030 application.area-set-handover-plan** — [SimulationAreaSetHandoverPlanner](../../../Ssalddel.Simulation.Application/SimulationAreaSetHandoverPlanner.cs) · H5 물리 회랑과 이동 방향을 이용해 다음 AreaSet의 준비 깊이를 결정한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `SimulationState | DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 준비 우선순위만 계산하며 자료 상주 상태, 현재 AreaSet, 접근 권한과 WorldTick을 확정하지 않는다.
- **030 application.world-stream** — [SimulationWorldStreamingService](../../../Ssalddel.Simulation.Application/SimulationWorldStreamingService.cs) · 카메라·플레이어 경계 접근에 필요한 타일 Recipe와 Manifest Projection을 제공한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 자료가 없는 DEM·배치 좌표·URL을 꾸며내지 않고 명시된 제공 범위만 투영한다.
- **031 application.lh-world-preview** — [SimulationLhWorldService](../../../Ssalddel.Simulation.Application/SimulationLhWorldService.cs) · 플레이어 L3 위치를 기준으로 스트리밍 범위와 셀 내용을 분리해 주변 공간 후보를 계산한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `SimulationState | DerivedWorld → None`
  - 부수효과: `None`
  - 경계: Preview는 공간 권위·운영 원장·자원 원장을 변경하지 않는다. 시나리오 셀 내용 공급자는 실제 E5·E6 근거로 자동 승격되지 않는다.
- **032 api.lh-world-preview** — [SimulationLhWorldController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationLhWorldController.cs) · 플레이어 L3 위치에 필요한 LH Cell 후보를 서버 Simulation 시각으로 Preview한다.
  - 계층/단계: `Api / Preview`
  - 읽기/쓰기: `SimulationState | DerivedWorld → None`
  - 부수효과: `None`
  - 경계: 정확한 Transform을 받지 않고 양자화한 L3 Cell만 사용하며 Preview는 어떤 원장도 변경하지 않는다.
- **033 application.lh-interior-plan-handle** — [SimulationLhInteriorPlanHandleService](../../../Ssalddel.Simulation.Application/SimulationLhInteriorPlanHandleService.cs) · LH 셀 상세도와 이미 고정된 실내 계획 handle을 연결한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → None`
  - 부수효과: `None`
  - 경계: LH는 실내를 생성하거나 재배치하지 않고 준비 상세도만 선택한다.
- **034 application.lh-asset-plan-lifecycle** — [SimulationLhAssetPlanLifecycleService](../../../Ssalddel.Simulation.Application/SimulationLhAssetPlanLifecycleService.cs) · 동결 배치 계획의 LH 준비·활성·캐시·해제 상태만 전이한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: 표현 수명주기만 변경하며 배치 계획과 Simulation WorldRevision은 고정한다.
- **035 application.lh-asset-plan-window-reconcile** — [SimulationLhAssetPlanWindowReconciler](../../../Ssalddel.Simulation.Application/SimulationLhAssetPlanWindowReconciler.cs) · LH Window 역할과 캐시 용량을 동결 배치 계획의 표현 수명주기로 조정한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `DerivedWorld → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: 셀 활성 우선순위와 수명주기만 결정하며 계획 생성·재배치·Simulation 상태 변경은 수행하지 않는다.
- **040 unity.interior-plan-presentation** — [InteriorPresentationProjection](../../../Ssalddel.Unity/Runtime/Interiors/InteriorPresentationProjection.cs) · 고정된 실내 계획을 LH Focus 상세도에 맞는 Unity VisualKey와 Reference 카드로 투영한다.
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `DerivedWorld → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: Pinned hash 불일치는 닫고 Unity는 Plan·상품 승인·Simulation 상태를 변경하지 않는다.
- **041 unity.lh-asset-plan-presentation-reconcile** — [SimulationLhAssetPlanPresentationReconciler](../../../Ssalddel.Unity/Runtime/WorldProjection/SimulationLhAssetPlanPresentationReconciler.cs) · LH 배치 계획 수명주기 상태를 Unity Prepare·Activate·Cache·Release 표현 명령으로 변환한다.
  - 계층/단계: `ClientAdapter / Presentation`
  - 읽기/쓰기: `DerivedWorld → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: Prefab 선택이나 GameObject 생성 없이 표현 명령만 만들며 Simulation 권위 상태를 변경하지 않는다.

## Farm 감자 3원천 현실근거 (`simulation-farm-reality-evidence`)

선행 기능: `simulation-world-derivation`, `simulation-session-lifecycle`

- **010 contract.farm-reality-evidence** — [SimulationFarmRealityEvidenceBundle](../../../Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFarmRealityEvidenceContracts.cs) · 감자 Farm Area의 승인된 농사로·KAMIS·USDA AMS 현실 근거 묶음을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 원 단위와 관계 상태를 보존하며 가격 차이·수익·사건·공간 배치를 계산하지 않는다.
- **020 api.farm-reality-evidence** — [SimulationFarmRealityEvidenceController](../../../Ssalddel.Simulation.Hosting/Controllers/SimulationFarmRealityEvidenceController.cs) · 감자 Farm 현실근거의 명시적 동기화와 읽기 전용 조회 경계를 제공한다.
  - 계층/단계: `Api / Persistence`
  - 읽기/쓰기: `SharedPublicData | DerivedWorld → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: 동기화는 명시적 요청에서만 수행하며 Tick·Unity 조회 중 Provider를 호출하지 않는다.
- **030 application.farm-reality-sync** — [SimulationFarmRealityEvidenceService](../../../Ssalddel.Simulation.Application/SimulationFarmRealityEvidenceService.cs) · 운영 승인 묶음을 검증·해시해 Simulation 파생 원장에 명시적으로 동기화한다.
  - 계층/단계: `Application / Persistence`
  - 읽기/쓰기: `SharedPublicData → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: Provider를 호출하지 않고 승인된 운영 자료만 읽으며 ContextProposal 외 Simulation 효과를 만들지 않는다.
- **040 infrastructure.farm-reality-store** — [SimulationFarmRealityEvidenceStore](../../../Ssalddel.Simulation.Persistence/SimulationFarmRealityEvidencePersistence.cs) · 승인 현실근거 묶음을 입력 hash 기준으로 Simulation World 파생 DB에 멱등 저장한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `DerivedWorld → DerivedWorld`
  - 부수효과: `PersistentRead | PersistentWrite`
  - 경계: Simulation World 파생 DB만 변경하며 같은 revision의 다른 hash를 거부한다.

## Unity 마지막 성공 상태 로딩 (`unity-resilient-world-load`)

선행 기능: `simulation-world-streaming`

- **010 client.last-successful-runtime** — [LastSuccessfulLoadRuntime`2](../../../Ssalddel.Unity/Runtime/Application/LastSuccessfulLoadRuntime.cs) · 최초 조회와 새로고침을 구분하고 마지막 성공 Snapshot을 유지한다.
  - 계층/단계: `ClientAdapter / Presentation`
  - 읽기/쓰기: `None → ClientPresentation`
  - 부수효과: `UiStateMutation`
  - 경계: 서버 상태를 만들거나 변경하지 않고 실패 시 마지막 성공 표현만 보존한다.
- **020 client.community-load** — [CommunityMarketSquareLoadCoordinator](../../../Ssalddel.Unity/Runtime/Community/CommunityMarketSquareModels.cs) · 커뮤니티 광장 Snapshot 조회와 마지막 성공 상태 조정을 연결한다.
  - 계층/단계: `ClientAdapter / Query`
  - 읽기/쓰기: `OperationalState → ClientPresentation`
  - 부수효과: `NetworkCall | UiStateMutation`
  - 경계: 공개 Projection만 읽고 커뮤니티 원장이나 서버 개정을 변경하지 않는다.
- **020 client.public-data-load** — [PublicDataHallLoadCoordinator](../../../Ssalddel.Unity/Runtime/PublicData/PublicWorldMapModels.cs) · 공공데이터 World Map Snapshot 조회와 마지막 성공 상태 조정을 연결한다.
  - 계층/단계: `ClientAdapter / Query`
  - 읽기/쓰기: `SharedPublicData → ClientPresentation`
  - 부수효과: `NetworkCall | UiStateMutation`
  - 경계: 출처와 자료 상태가 있는 조회 결과만 표현하며 공공데이터 원본을 수정하지 않는다.
- **020 client.warehouse-load** — [WarehouseWorldLoadCoordinator](../../../Ssalddel.Unity/Runtime/Warehouse/WarehouseWorldModels.cs) · 창고 World Snapshot 조회와 마지막 성공 상태 조정을 연결한다.
  - 계층/단계: `ClientAdapter / Query`
  - 읽기/쓰기: `OperationalState → ClientPresentation`
  - 부수효과: `NetworkCall | UiStateMutation`
  - 경계: 권한 적용된 창고 Projection만 표현하며 Unity가 입출고 완료를 확정하지 않는다.

## 진단 요약

- 오류: 0
- 경고: 8
- 일반 공개 타입의 미표기는 경고이며, 필수 단계·권위 위반·오래된 생성 파일만 검증을 차단한다.
