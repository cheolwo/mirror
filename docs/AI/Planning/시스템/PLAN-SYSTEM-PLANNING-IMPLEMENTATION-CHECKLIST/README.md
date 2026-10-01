[기획 · 기획·개발·검증 통합 관리 · PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST · r11]

현행 보완 순환: [결손 보완과 조건부 제작 규칙 r11](app-production-repair-cycle.r11.md). 새 기능보다 기존 실패/복구를 보완하고 검증 근거와 한계를 함께 축적한다. 아래 r10 문답과 r9 도구 형식은 유지한다.

현행 절차: [부족한 정보만 문답으로 채우는 r10](app-production-guided-intake.r10.md). AI가 먼저 기존 자료를 채우고 필요한 사용자 선택만 질문 하나씩 보완한다. 자료 형식·검사 도구는 [입력 표준 r9](app-production-intake.r9.md)와 [r8 추적 기반](app-production-tracking.r8.md)을 유지한다. [입력 안내](app-production-input-guide.md), [음식점 메뉴 초안](restaurant-menu-input-review.md), [r9 작업지시서](app-production-intake.r9.work-order.json), [r9 결과](app-production-intake.r9.result.md)를 따른다. [생성 현황표](../../../generated/planning-app-production.md)와 [로컬 상태판](../../../generated/planning-app-production.html)은 읽기 전용이다. 도구는 [사용법](../../../../../eng/planning-inquiries/app-production/README.md)을 참고한다.

아래는 **2026-09-17 r7의 역사적 코드 존재 조사**다. 당시 82개·O/X 수를 현재 목록이나 시험 성공률로 읽지 않는다. r8은 이 의미를 유지하면서 별도 증거 연결을 추가한다.

# 기획-코드 존재 대장

- 기획 ID: `PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST`
- 판본: `r7`
- 상태: `Draft / FullPlanningIndexAuditRecorded / OperatorAppAndOperationalDataFirst / SimulationRulesSecond / UnityReadOnlyThird / DioramaFirstWithinUnity / ThirtyDongDioramaSkeletonSufficient / CompletenessAssessmentExcluded`
- 조사 기준일: `2026-09-17`
- 목적: 기획 문서와 현재 작업 트리의 제품 코드 존재 여부를 대조하고, 전체 제품에서는 운영자 앱·운영 데이터를 먼저, Unity 안에서는 디오라마를 먼저 두는 상대적 개발 순서를 구분한다.

## 1. 판정 범위

| 표시 | 의미 |
| --- | --- |
| `O` | 해당 기획 문서 또는 관련 제품 코드가 존재한다. |
| `X` | 현재 조사 범위에서 관련 제품 코드를 발견하지 못했다. |
| `?` | 이름이나 경계가 불명확해 추가 조사가 필요하다. |

` 코드 O `는 완성, 기획 충족, 시험 통과, 실행 가능, 운영 활성화를 뜻하지 않는다. 부분 구현이나 기능 플래그로 비활성화된 코드도 실제 제품 소스에 존재하면 `O`로 표시한다.

문서, 시험, 생성 산출물, Graph Map JSON만 있고 Controller·UseCase·Domain·Service·앱 화면·Unity Runtime 등의 제품 소스가 없으면 `X`로 표시한다.

`기획 O/X`와 `코드 O/X`는 존재 여부 축이고, 아래의 `Unity 개발선`은 현재 선택 순서 축이다. 코드가 이미 `O`여도 현행 디오라마 목표와 직접 관계가 없으면 후순위일 수 있다. 반대로 제품 코드가 아직 `X`여도 기획 문서만으로 충분한 운영 항목은 개발 결손으로 보지 않는다.

## 2. 1차 조사 대장

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB` | O | O | `Ssalddel.Domain/창고/생활권물류거점.cs`, `Ssalddel/Controllers/Common/생활권물류거점Controller.cs` |
| `PLAN-OPERATIONS-ABNORMAL-WORK-RECOVERY` | O | O | `Ssalddel.Domain/운송/비정상운송사건.cs`, `Ssalddel/Application/Admin/Progress/비정상운송사건운영UseCase.cs` |
| `PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK` | O | O | `Ssalddel.Domain/운송/운송업무담당자배정.cs`, `Ssalddel/Application/Shipper/Request/화주운송업무망조회UseCase.cs` |
| `PLAN-OPERATIONS-LOGISTICS-OS` | O | O | `Ssalddel.Domain/운영/운영체제업무인계.cs`, `Ssalddel/Services/Operations/운영체제업무인계Coordinator.cs` |
| `PLAN-OPERATIONS-SHIPPER-TRANSPORT-MANAGEMENT` | O | O | `Ssalddel.Domain/화주/화주운송의뢰.cs`, `Ssalddel/Services/Operations/화주운송의뢰화물운송인계Service.cs` |
| `PLAN-OPERATIONS-SSALDDEL-MART-SUPPLY-NETWORK` | O | O | `Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs`, `Ssalddel/Services/Operations/살뜰마트라스트마일배차인계Service.cs` |
| `PLAN-OPERATIONS-DISPATCH-CORE` | O | O | `Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs`, `Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs` |
| `PLAN-OPERATIONS-ADMIN-DONG-DELIVERY-TERRITORY` | O | O | `Ssalddel.Domain/배달권/배달운영권역.cs`, `Ssalddel/Services/DeliveryZones/배달운영권역관리Service.cs` |
| `PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation절기운영Campaign.cs`, `Ssalddel.Domain/운영/플랫폼운영경제성Calculator.cs` |
| `PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD` | O | O | `Ssalddel.Domain/음식/음식운영정책.cs`, `SsalddelAdminApp/Components/Pages/Operations.razor` |
| `PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS` | O | O | `Ssalddel.Contracts/Common/Workflow/업무실패복구Dtos.cs`, `Ssalddel/Application/Food/음식배달가능행동Projector.cs` |
| `PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001` | O | O | `Ssalddel/Application/WorldProjection/운영지역장면조회UseCase.cs`, `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldSceneInterpreter.cs` |
| `PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001` | O | O | `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldSceneClient.cs`, `Ssalddel.Unity/Runtime/OperationalTransport/UnityJsonOperationalWorldSceneDecoder.cs` |
| `PLAN-SYSTEM-ADMIN-DONG-DIORAMA` | O | O | `Ssalddel/Services/WorldProjection/AdministrativeDongDiorama/행정동디오라마ProjectionBuilder.cs`, `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaInterpreter.cs` |
| `PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES` | O | O | `Ssalddel/Application/WorldProjection/역세권디오라마조회UseCase.cs`, `Ssalddel/Controllers/Common/역세권디오라마Controller.cs` |
| `PLAN-STORY-HEX04-CAMPAIGN` | O | O | `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationHexagramCampaignContracts.cs`, `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationCollectibleCardRewards.cs` |
| `PLAN-STORY-HUB-DISCOVERY` | O | X | 현재는 Graph Map·인계·수집 도구만 확인했고 해당 발견 기획에 직접 결속된 제품 소스는 발견하지 못했다. |
| `PLAN-GAMEPLAY-NATURE-RESOURCE-CONSTRUCTION` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureRestoration.cs`, `Ssalddel.Simulation.Application/RuntimeCore/SimulationRegionalIncidentService.cs` |
| `PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST` | O | X | 본 대장은 기획 문서만 존재하며 제품 코드를 새로 요구하지 않는다. |

## 3. 후속 조사 순서

1. `PLANNING.md`의 현재 문답 우선순위와 `Approved`·`Active`·`ReadyForHandoff` 기획
2. `CURRENT_WORK.md`의 최근 변경과 연결된 기획
3. 일시 정지·대체·호환 이력 기획
4. 과거 문답 기획

후속 조사에서도 완성도·시험·실행·휴대폰·Unity 표현 증거는 판정하지 않는다.

## 4. 현재 경계

- 본 조사는 대규모로 변경된 현재 작업 트리를 읽기 전용으로 대조했다.
- r4에서는 본 대장의 분류와 `docs/AI/PLANNING.md`의 현행 Unity 우선순위 안내만 갱신하고 제품 코드는 변경하지 않는다.
- 현재 표의 `O`는 코드 존재 여부만 뜻하며 개발 완료 체크로 사용하지 않는다.
- 기존 City·Town·Hub·Farm 기획과 코드는 삭제·대체하지 않고 후순위 독립 게임 영역으로 보존한다.

## 5. 현행 전수 조사 r4

`docs/AI/PLANNING.md`에 등록된 기획 ID 82개를 대조했다. 모든 항목의 기획 문서는 `O`이며, 관련 제품 코드는 `O` 68개·`X` 14개다.

`docs/AI/Planning` 아래 `PLAN-*` 폴더 51개도 대조했다. 목차에 독립 기획으로 등록되지 않은 12개는 수뢰둔·산수몽 각 효의 호환 포인터다. 별도의 미분류 정본 기획 폴더는 발견하지 않았다.

### 5.1 자료·상위 방향

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-ARCH-OPERATIONS-UNITY-TRANSFER` | O | O | `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldSceneClient.cs` |
| `PLAN-DATA-EIGHT-LIFE-DOMAINS` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPlayerDomainCatalog.cs` |
| `PLAN-DATA-GAMEOBJECT-ASSET` | O | O | `Ssalddel/Services/Content/개체시각대응UseCase.cs` |
| `PLAN-DATA-REALITY-MYSQL` | O | O | `Ssalddel.Infrastructure/Persistence/AgriculturalFisheries/AgriculturalFisheriesDbContext.cs` |
| `PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION` | O | O | `Ssalddel/Application/WorldProjection/역세권디오라마건물증거조회UseCase.cs` |
| `PLAN-DATA-SAGAJEONG-HOUSING-MARKET-OBSERVATION` | O | X | 주거 가격 관찰에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-GAME-COMMON-PURPOSE` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationGwangbokResonanceEntryCapCandidateEvaluator.cs` |

### 5.2 게임플레이

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-GAMEPLAY-COMMUNITY-VISITOR` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationCommunityVisitorStay.cs` |
| `PLAN-GAMEPLAY-DELEGATION` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation농사수확위임.cs` |
| `PLAN-GAMEPLAY-FARM-CROP-LIFE` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation감자생산규칙.cs` |
| `PLAN-GAMEPLAY-FARM-DEFENSE` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFarmDefenseMobilization.cs` |
| `PLAN-GAMEPLAY-FIRST-EXPERIENCE` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWorldExploration.cs` |
| `PLAN-GAMEPLAY-FIRST-PERSON-FOCUS` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFocusTimingPolicy.cs` |
| `PLAN-GAMEPLAY-HERBAL-CRAFTING` | O | X | 약초 식별·가공·달이기 폐루프에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-GAMEPLAY-HUB-DEMAND` | O | X | Hub 수요·분배·부족분 폐루프에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-GAMEPLAY-MEDITATION-ACTION` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureFocusMeditation.cs` |
| `PLAN-GAMEPLAY-MULTI-AREA-CHOICE` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationAreaAccess.cs` |
| `PLAN-GAMEPLAY-NATURE-RESOURCE-CONSTRUCTION` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureRestoration.cs` |
| `PLAN-GAMEPLAY-NATURE-SHELTER` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationNatureShelterPurposeReadinessEvaluator.cs` |
| `PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE` | O | O | `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaInterpreter.cs` |
| `PLAN-GAMEPLAY-PERSPECTIVE-ROLES` | O | O | `Ssalddel.Unity/Runtime/Perspectives/RolePerspectiveModels.cs` |
| `PLAN-GAMEPLAY-PLAYER-STAMINA` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation행동체력자연회복.cs` |
| `PLAN-GAMEPLAY-PROGRESSION-CLUSTERS` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPlayerLearningFocus.cs` |
| `PLAN-GAMEPLAY-REGIONAL-MONSTER` | O | X | 지역 오행 몬스터 기획에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-GAMEPLAY-SURVIVAL-ECONOMY` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationSettlementEconomy.cs` |
| `PLAN-GAMEPLAY-TOWN-ORDER` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationIndividualOrderPickup.cs` |

### 5.3 Graph Map·배치·기획 운영·표현·공간

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-GRAPH-HUB-LOGISTICS-CIRCULATION` | O | O | `Ssalddel.Unity/Runtime/PotatoJourney/PotatoHubReceivingLifecycleModels.cs` |
| `PLAN-GRAPH-LAYER-FIRST-WORKFLOW` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFarmConstructionPlacement.cs` |
| `PLAN-GRAPH-LONG-ROUTE-ENCOUNTER` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationLogisticsMovement.cs` |
| `PLAN-GRAPH-NORTHERN-LIFE` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationIntegratedWorld.cs` |
| `PLAN-GRAPH-NORTHERN-LIFE-REVIEW` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationIntegratedWorld.cs` |
| `PLAN-GRAPH-PLANNING-INTEGRATION` | O | X | Graph Map 관리 도구·JSON은 있지만 통합 기획 자체에 결속된 제품 소스는 발견하지 못함 |
| `PLAN-PLACEMENT-CROSS-AREA-BUILDING` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationAreaBuildingProgression.cs` |
| `PLAN-PLACEMENT-FOREST-EDGE-FARM` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationHansFarmFenceRestoration.cs` |
| `PLAN-PLANNING-DECISION-READING` | O | X | 문서 읽기 안내로만 관리되며 제품 소스를 요구하지 않음 |
| `PLAN-PLANNING-MIGRATION` | O | X | 기획 현행화 운영 문서로만 관리됨 |
| `PLAN-PLANNING-PLAYER-CONTEXT` | O | X | 기획 문답 맥락 정리에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-PLANNING-WI-GWAE` | O | O | `Ssalddel.Simulation.BusinessWorkflow/UnityPackage/Runtime/SimulationBusinessWorkflowRuntimeContracts.cs` |
| `PLAN-PRESENTATION-E4-POOL` | O | O | `Ssalddel.Unity/Runtime/PresentationContracts/PresentationRuleCatalogModels.cs` |
| `PLAN-PRESENTATION-H1-SYNTY-STATE` | O | O | `Ssalddel.Unity/Runtime/PresentationContracts/표현연결Preflight.cs` |
| `PLAN-PRESENTATION-SYNTY-SURVEY` | O | O | `Ssalddel/Services/Content/개체시각자산Catalog.cs` |
| `PLAN-SPATIAL-FOOD-DELIVERY` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식배달.cs` |
| `PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/사가정저밀도교통.cs` |
| `PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/사가정저밀도교통.cs` |

### 5.4 운영

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-OPERATIONS-ABNORMAL-WORK-RECOVERY` | O | O | `Ssalddel.Domain/운송/비정상운송사건.cs` |
| `PLAN-OPERATIONS-ADMIN-DONG-DELIVERY-TERRITORY` | O | O | `Ssalddel.Domain/배달권/배달운영권역.cs` |
| `PLAN-OPERATIONS-DISPATCH-CORE` | O | O | `Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs` |
| `PLAN-OPERATIONS-LOGISTICS-OS` | O | O | `Ssalddel.Domain/운영/운영체제업무인계.cs` |
| `PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB` | O | O | `Ssalddel.Domain/창고/생활권물류거점.cs` |
| `PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK` | O | O | `Ssalddel.Domain/운송/운송업무담당자배정.cs` |
| `PLAN-OPERATIONS-SHIPPER-TRANSPORT-MANAGEMENT` | O | O | `Ssalddel.Domain/화주/화주운송의뢰.cs` |
| `PLAN-OPERATIONS-SSALDDEL-MART-SUPPLY-NETWORK` | O | O | `Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs` |

### 5.5 스토리

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-STORY-CITY-DISCOVERY` | O | X | City 첫 발견·온디맨드 메인 안내에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-STORY-DUAL-PROTAGONIST` | O | X | 모험가·소가주 이중 주인공 서사에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-STORY-FIRST-FARM-DISCOVERY` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationHansFarmFenceRestoration.cs` |
| `PLAN-STORY-HEX03-CAMPAIGN` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationHexagramCampaign.cs` |
| `PLAN-STORY-HEX04-CAMPAIGN` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationCollectibleCardRewards.cs` |
| `PLAN-STORY-HEXAGRAM-CAMPAIGN-RESET` | O | O | `Ssalddel.Simulation.Application/RuntimeCore/SimulationHexagramCampaignService.cs` |
| `PLAN-STORY-HEXAGRAM-SEQUENCE` | O | O | `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationHexagramCampaignContracts.cs` |
| `PLAN-STORY-HUB-DISCOVERY` | O | X | Graph Map·인계·수집 도구만 확인했고 해당 발견 기획에 직접 결속된 제품 소스는 발견하지 못함 |
| `PLAN-STORY-IDEA-MAP-LEARNING` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationPlayerIdeaMap.cs` |
| `PLAN-STORY-MIRROR-MAIN` | O | X | 거울의 흐름 메인 스토리에 직접 결속된 제품 소스를 발견하지 못함 |
| `PLAN-STORY-TOWN-DISCOVERY` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationArcanaTownLife.cs` |
| `PLAN-STORY-YODONG-DEFENSE` | O | X | 요동성 방어 서사에 직접 결속된 제품 소스를 발견하지 못함 |

### 5.6 시스템·시간·시각·월드

| 기획 ID | 기획 | 코드 | 대표 제품 코드 근거 |
| --- | ---: | ---: | --- |
| `PLAN-SYSTEM-ADMIN-DONG-DIORAMA` | O | O | `Ssalddel/Services/WorldProjection/AdministrativeDongDiorama/행정동디오라마ProjectionBuilder.cs` |
| `PLAN-SYSTEM-MYEONMOK-OBSERVER` | O | O | `Ssalddel.Unity/Runtime/WorldProjection/운영역할GameObjectCatalogPolicy.cs` |
| `PLAN-SYSTEM-NEIGHBORHOOD-SPATIAL-PACKAGES` | O | O | `Ssalddel.WorkflowRules.Contracts/UnityPackage/Runtime/RegionExperiencePackageContracts.cs` |
| `PLAN-SYSTEM-NORTHEAST-SEOUL-STATION-DIORAMA-ROLLOUT` | O | O | `Ssalddel/Application/WorldProjection/역세권디오라마조회UseCase.cs` |
| `PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001` | O | O | `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldSceneInterpreter.cs` |
| `PLAN-SYSTEM-OBSERVER-WORLD` | O | O | `Ssalddel.Simulation.Application/관찰세계진행Policy.cs` |
| `PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS` | O | O | `Ssalddel.Contracts/Common/Workflow/업무실패복구Dtos.cs` |
| `PLAN-SYSTEM-REGION-EXPERIENCE-PACKAGES` | O | O | `Ssalddel/Application/WorldProjection/지역ExperiencePackage조회UseCase.cs` |
| `PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD` | O | O | `SsalddelAdminApp/Components/Pages/Operations.razor` |
| `PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation절기운영Campaign.cs` |
| `PLAN-SYSTEM-SAVE-REENTRY` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationSaveReplay.cs` |
| `PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES` | O | O | `Ssalddel/Application/WorldProjection/역세권디오라마조회UseCase.cs` |
| `PLAN-TIME-SEASONAL` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation절기운영Campaign.cs` |
| `PLAN-TIME-SOLAR-TERM-TAROT-TURN` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation타로카드뽑기.cs` |
| `PLAN-VISUAL-HANS-FARM` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationHansFarmFenceRestoration.cs` |
| `PLAN-VISUAL-SYNTY-REFINEMENT` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationWorldSyntyLandscape.cs` |
| `PLAN-WORLD-FOUR-AREAS` | O | O | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationAreaAccess.cs` |
| `PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST` | O | X | 본 대장은 기획 문서만 존재하며 제품 코드를 새로 요구하지 않음 |

## 6. 전체 제품과 Unity 개발선 분류

### 6.1 전체 제품 우선순위

| 전체 순서 | 현재 개발선 | Unity와의 관계 |
| --- | --- | --- |
| `1순위` | 운영 서버 데이터·OS 생명주기와 모바일 운영자 앱 | 업무 사실과 사람의 허용된 판단·조치가 먼저다. |
| `2순위` | 운영 계약을 재사용한 격리 Simulation 규칙·검증 | 운영 상태·전이·실패·회복 의미를 외부 효과 없이 검증한다. |
| `3순위` | Unity 읽기 전용 관찰·3D 디오라마 | 운영·Simulation·공공자료를 표현하며 권위를 갖지 않는다. |

기존 30개 행정동 디오라마 뼈대는 현재 후속 표현 기반으로 충분하다. 운영자 앱에서 새로 필요한 상태·관계·공간 정보가 발견되기 전에는 디오라마 확대를 전체 프로젝트의 다음 작업으로 자동 선택하지 않는다.

### 6.2 Unity 내부 판정 규칙

| 개발선 | 의미 | 현행 선택 원칙 |
| --- | --- | --- |
| `P0 디오라마 공간 골격` | 행정동·역세권·건물·도로·필지·랜드마크·배치 자료와 `SimulationWorldShell` 관찰 공간 | Unity의 다음 공간 개발은 여기서 고른다. |
| `P1 디오라마 관찰 오버레이` | 서버 권위의 운영 상태를 읽기 전용 객체·경로·상태 사본으로 표현 | P0 공간 근거가 있는 작은 절편만 연결한다. |
| `P2 디오라마 시각 정제` | Blender·Synty·Prefab·LOD·표현 자산 교체 | 출처·배치·관찰 의미가 동결된 객체부터 적용한다. |
| `Hold 독립 게임 영역` | City·Town·Hub·Farm·Nature의 독립 폐루프, 발견 이야기, 전투·성장·제작 | 환경 유형과 구분해 보존하되 디오라마 기반선 뒤로 미룬다. |
| `Meta 기획 운영` | 기획 문답·판본·대장·Graph Map 인계 운영 | 제품 개발 우선순위와 분리한다. |

디오라마 관련 `O`는 공간 자료·배치·건물·도로·시설·랜드마크·관찰 객체 또는 운영 상태의 공간 투영을 직접 바꾸는 범위다. 기존 디오라마를 단순 배경으로 사용하는 이야기·대화·전투·성장·카드·학습 기획은 디오라마 관련 `X`로 분류한다.

### 6.3 City·Town·Hub·Farm의 현행 역할

`PLAN-WORLD-FOUR-AREAS`의 City·Town·Hub·Farm 명칭은 P0 디오라마가 현실 공간의 성격을 분류할 때 재사용한다. 이는 네 게임 영역의 개발 재개를 뜻하지 않는다.

| 분류 | 현행 디오라마 용도 | 게임플레이 상태 |
| --- | --- | --- |
| `City` | 서울 행정동·역세권 같은 고밀 도시 생활 디오라마 | City 발견·공공 문제 폐루프는 `Hold` |
| `Town` | 읍내·소도시·저중밀 생활권 디오라마 | Town 주문·생활 폐루프는 `Hold` |
| `Hub` | 물류센터·물류단지·환적 거점 디오라마 | Hub 수요·분배·출고 폐루프는 `Hold` |
| `Farm` | 농장·농업 생산 구역 디오라마 | Farm 재배·방어·발견 폐루프는 `Hold` |

실제 지역 안정 ID가 먼저이고 디오라마 유형은 분류 속성이다. 유형은 새 AreaSet·Scene·업무 권위·이동 경로를 자동 생성하지 않는다. 혼합 지역은 `주 유형 1개 + 보조 유형 여러 개`로 기록한다.

### 6.4 행정동 디오라마와 배달권 관계

행정동·역세권 디오라마가 현행 Unity 최우선이다. 서버의 배달운영권역은 디오라마가 임의로 만든 공간이 아니라 공식 행정동 안정 ID 여러 개를 묶은 운영 원장이다.

```text
행정동 공간 자료·디오라마 준비
    → 같은 행정동 안정 ID들의 배달운영권역 membership
    → 별도 운영 승인 뒤 주문·배차·정산에 연결
```

- 행정동 디오라마 준비와 Draft 배달운영권역 구성은 서로 조회할 수 있지만 각각의 권위를 유지한다.
- 역세권 1km 디오라마는 여러 행정동을 가로지를 수 있는 관찰용 화면이며 배달권 경계가 아니다.
- Unity 화면상 인접성, City·Town·Hub·Farm 유형, 랜드마크 배치만으로 배달권 membership을 생성하지 않는다.
- 현재 분류 작업은 여기서 닫고 실제 권역 활성화와 배차 적용은 `PLAN-OPERATIONS-ADMIN-DONG-DELIVERY-TERRITORY`의 후속 관문으로 남긴다.

### 6.5 Unity 내부 우선 개발선

| 개발선 | 기획 ID | 현재 적용 범위 |
| --- | --- | --- |
| `P0` | `PLAN-SYSTEM-ADMIN-DONG-DIORAMA` | 행정동별 자료 계층, 경계·건물·도로·배치 근거와 검증 대장 |
| `P0` | `PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES` | 사가정역부터 출구·랜드마크·모듈 조립 기준을 축적 |
| `P0` | `PLAN-SYSTEM-NORTHEAST-SEOUL-STATION-DIORAMA-ROLLOUT` | 사가정 기준선 이후 역세권을 같은 증거 규칙으로 확장 |
| `P0` | `PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION` | 건물·주소·필지 증거 결손을 배치 전에 닫는 자료 기반 |
| `P0` | `PLAN-SYSTEM-MYEONMOK-OBSERVER` | 면목동 실제 지도 기반 관찰 범위와 합성 오버레이 분리 |
| `P0` | `PLAN-SYSTEM-NEIGHBORHOOD-SPATIAL-PACKAGES` | 법정동·행정동 자료를 재사용 가능한 공간 패키지로 준비 |
| `P0` | `PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY`, `PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC` | 사가정 도로·이동·신호 자료를 디오라마 참고 표현으로 사용 |
| `P1` | `PLAN-ARCH-OPERATIONS-UNITY-TRANSFER`, `PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001` | 운영 서버 상태를 Unity가 재판정하지 않고 읽기 전용으로 관찰 |
| `P1` | `PLAN-SPATIAL-FOOD-DELIVERY`, `PLAN-SYSTEM-OBSERVER-WORLD` | 검증된 기준점 안에서 배달 객체와 NPC 생활을 관찰 오버레이로 표현 |
| `P1` | `PLAN-SYSTEM-REGION-EXPERIENCE-PACKAGES` | 지역 자료·이동·관찰 계층을 한 지역 패키지로 묶되 실행 권위는 부여하지 않음 |
| `P2` | `PLAN-DATA-GAMEOBJECT-ASSET`, `PLAN-PRESENTATION-E4-POOL`, `PLAN-PRESENTATION-SYNTY-SURVEY`, `PLAN-VISUAL-SYNTY-REFINEMENT` | P0·P1에서 선택된 객체의 시각 후보·대체 표현만 지원 |

`PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE`의 지도·공간 밀도 표현 자료는 P0 참고로 재사용할 수 있지만, 방어 플레이 폐루프 자체는 `Hold`다. 같은 원칙으로 배달 경로 표현을 우선한다고 해서 플레이어 직접 조작이나 전투까지 함께 활성화하지 않는다.

### 6.6 후순위 독립 게임 영역

| 후순위 묶음 | 포함 기획 | 보존 방식 |
| --- | --- | --- |
| 네 영역 월드 | `PLAN-WORLD-FOUR-AREAS` | 디오라마 환경 유형 분류는 P0에서 재사용하되, 네 영역의 연속 게임 월드·영역별 폐루프·강제 연결 순서는 `Hold` |
| Farm | `PLAN-GAMEPLAY-FARM-CROP-LIFE`, `PLAN-GAMEPLAY-FARM-DEFENSE`, `PLAN-STORY-FIRST-FARM-DISCOVERY`, `PLAN-VISUAL-HANS-FARM` | 기존 코드·문서·증거를 보존하고 새 Unity 폐루프·시각 고도화는 후순위 |
| Town | `PLAN-GAMEPLAY-TOWN-ORDER`, `PLAN-STORY-TOWN-DISCOVERY` | 주문·발견 이야기와 Town 독립 폐루프를 후순위로 유지 |
| Hub | `PLAN-GAMEPLAY-HUB-DEMAND`, `PLAN-STORY-HUB-DISCOVERY`, `PLAN-GRAPH-HUB-LOGISTICS-CIRCULATION` | 수요·분배·발견·Hub 내부 물류 폐루프를 후순위로 유지 |
| City | `PLAN-STORY-CITY-DISCOVERY` | City 발견·공공 문제·도시 플레이를 후순위로 유지 |
| Nature·일반 게임플레이 | 자원 건설·쉼터·약초·몬스터·체력·성장·전투·카드·메인 스토리 기획 | 디오라마 공간이나 관찰 객체를 직접 바꾸는 승인 절편이 생기기 전에는 현행 Unity 우선선에 넣지 않음 |

후순위는 폐기나 미구현 판정이 아니다. 기존 `SimulationWorldShell`과 코드의 호환성을 유지하되, 새 Unity 작업을 자동으로 선택하지 않는 일정 분류다. 향후 다시 활성화할 때에도 Farm→Hub→City 같은 연쇄 경로가 아니라 각 영역의 독립 폐루프부터 재검토한다.

### 6.7 전체 선택 순서

1. 운영 서버의 원장·상태 전이·실패·회복·허용 행동을 안정화한다.
2. 기존 `SsalddelAdminApp` 모바일 셸에서 조회→미리보기→확정→정본 재조회 폐루프를 닫는다.
3. 같은 운영 의미를 격리 Simulation 규칙과 Fixture에서 검증한다.
4. Unity는 정제된 읽기 전용 사본을 기존 30개 행정동·역세권 디오라마 뼈대에 표현한다.
5. Unity 고도화가 다시 필요할 때에는 사가정·면목 근거 보강→관찰 오버레이→시각 정제 순서를 사용한다.
6. City·Town·Hub·Farm 독립 게임 영역은 별도 재개 결정 전까지 후순위로 유지한다.

## 7. 전수 조사에서 코드 `X`로 남은 기획

1. `PLAN-DATA-SAGAJEONG-HOUSING-MARKET-OBSERVATION`
2. `PLAN-GAMEPLAY-HERBAL-CRAFTING`
3. `PLAN-GAMEPLAY-HUB-DEMAND`
4. `PLAN-GAMEPLAY-REGIONAL-MONSTER`
5. `PLAN-GRAPH-PLANNING-INTEGRATION`
6. `PLAN-PLANNING-DECISION-READING`
7. `PLAN-PLANNING-MIGRATION`
8. `PLAN-PLANNING-PLAYER-CONTEXT`
9. `PLAN-STORY-CITY-DISCOVERY`
10. `PLAN-STORY-DUAL-PROTAGONIST`
11. `PLAN-STORY-HUB-DISCOVERY`
12. `PLAN-STORY-MIRROR-MAIN`
13. `PLAN-STORY-YODONG-DEFENSE`
14. `PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST`

이 목록은 개발 우선순위가 아니다. 문서만으로 충분한 기획 운영 항목도 포함된다.

## 8. 현재 분류 결론

전체 제품에서는 운영자 모바일 앱·운영 데이터가 최우선이고, Simulation 검증이 그다음, Unity 읽기 전용 표현이 후순위다. Unity 작업을 다시 시작할 때에는 행정동·역세권 디오라마를 Unity 내부 첫 순서로 둔다. 다음 기획 문답은 운영자 모바일 첫 화면과 첫 실제 사용 폐루프가 소유한다.
