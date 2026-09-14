# [기획 · 월드·공간·배치 · PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC · r6]

- 기획 ID: `PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC`
- 기획 분야: 월드·공간·배치
- 기획 판본: `sagajeong-lane-signal-traffic.r6`
- 상태: `ApprovedPlanningBaseline / R3SyntheticTrafficPlayModeVerified / OptionAConfirmed / ExpandedLivingCatchmentIntentConfirmed / PedestrianLivingCorridorProposed / HWiAreaSetSkyModularAlignmentRequested / APlusH3PartialAreaSetConfirmed / BuildingHierarchySelectionIntentConfirmed / R6ReadOnlySelectionImplemented / AutomatedTestsPassed / CanonicalPlayModeGameViewVerified / DioramaVisualRegressionPassed / UserMouseUpInjectionDeferred / PendingHumanReview / TraversalAuthorityPending / SceneBindingDeferred`
- 상위 기획: [사가정 가상 배달 이동](../PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/README.md)
- 관련 시스템 기획: [역세권 디오라마 모듈 표준 r15](../../시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/README.md)
- 관련 WI·PlayableLoop: 기존 `interaction-goal:synthetic-delivery-move.v1`·`WI-CITY-SYNTHETIC-MOVE`를 보조한다. 새 권위 WI·Goal을 만들지 않는다.
- Graph Map 영향: `UpdateExistingCandidate`. 기존 도로 중심선과 `synthetic-neighborhood.v1` 후보를 보존하고 차량 방향 그래프, 보행 그래프, 횡단 충돌 구역, 보행자 우선 공유 골목, 기존 WI와 H1~H3 의미 결속을 서로 다른 후보 계층으로 추가한다.
- 배치 맵 영향: `Required`. 신호기 방향, 정지선, 횡단보도, 차량·보행 대기열, 생활 체류점과 일반화된 건물 출입 지점의 상대 배치를 동결해야 한다.
- 인계 상태: r3에서 저밀도 `SyntheticFixture`의 대로→생활도로→골목 후보 종점 연속 표시 계약과 결정적 Engine, Unity v2 해석과 저장하지 않는 실제 Game View를 검증했다. r4에서 A안과 더 긴 생활권 의도를 확정했고, r5에서 기존 H·WI·AreaSet·Sky 체계와 조화시키는 모듈 경계를 제안했다. r6는 A+를 H3 두 개와 부분 AreaSet 구성 후보로 관리하는 방향을 확정하고, 기존 사가정 Mesh·카메라·색·Collider 0개 계약을 보존하는 건물 클릭·읽기 전용 H 카드 절편만 구현 승인한다. 실제 H 등록·Graph Map 갱신·전체 1km 통행 권위·실제 신호 주기·서버 HTTP·저장 Scene 결속은 계속 미승인이다.
- 작업 명세: [E1~E7 수직 작업 명세 r3](implementation.md)
- 승인 연구: [사가정 저밀도 신호·골목 표현 적용 연구 r1](low-density-alley-study.r1.md), [연속 배달 표시 경로 연구 r2](continuous-delivery-route-study.r2.md)

## 목표

`station:kr:kric:s1107:0722` 사가정역을 중심으로 한 명목상 1km × 1km 디오라마에 공식 정적 차선·교차로·신호 시설을 독립 계층으로 추가한다. 현행 동결 envelope는 EPSG:5186에서 약 1,034.423m × 1,022.067m이며 이 판본을 조용히 정확 1km 정사각형으로 바꾸지 않는다. 가상 배달 기사와 합성 주변 차량은 검토된 차로를 따라 이동하고, Simulation이 만든 결정적 신호 상태에 따라 같은 정지선에서 대기했다가 다시 출발한다.

실시간 교통이나 실시간 신호 API는 사용하지 않는다. 공개 자료는 도로와 시설이 어디에 어떻게 놓였는지 설명하는 관찰 근거이고, 신호 주기·차량 생성·대기열은 게임용 합성 상태다. 서버와 Simulation이 판본·seed·시간·결과를 소유하고 Unity는 같은 상태 사본을 읽어 이동과 조명·대기열을 표현한다.

## 상위 목적 정렬

이 기획은 새 주문 권위나 직접 성장 보상을 만드는 독립 폐루프가 아니라, 기존 가상 배달 관찰을 실제 동네처럼 읽히게 하는 보조 층이다.

- 나의 막힘: 현재 중심선 기반 경로만으로는 기사가 어느 쪽 차로를 이용하는지, 교차로에서 왜 멈추는지 알기 어렵다.
- 나의 행동: 사가정역을 선택하고 가상 기사를 따라가며 차로 진입·신호 대기·재출발·픽업·전달·복귀를 관찰한다.
- 직접 결과: 기사와 주변 차량이 같은 차로·신호 규칙을 따르는 이유를 화면에서 읽을 수 있다.
- 대가: 공식 자료 판본 결속과 교차로별 사람 검토가 필요하며, 검토되지 않은 골목은 기존 중심선 관찰보다 더 높은 통행 정확도를 주장할 수 없다.
- 권위 결과: 합성 세계의 신호·차량 상태만 Simulation이 결정한다. 실제 주문·배차·교통 운영 결과는 바뀌지 않는다.
- 환류: 검토 완료된 차로·교차로만 다음 배달 경로 후보에 사용할 수 있다.
- 직접 회복이 아닌 보조 층: 광고·후원·업체 노출, 주문 순위, NPC 평판과 오행 분류에는 영향을 주지 않는다.

## 확정

### 범위와 권위

- 첫 범위는 사가정역 기준점 주변의 기존 명목상 1km 표현 창 하나다. 현행 EPSG:5186 envelope 약 1,034.423m × 1,022.067m를 r2 범위로 동결하며, 행정동·법정동 경계와 원본 500m 타일을 대체하지 않는다.
- 실시간 신호·실시간 교통량·실제 차량 GPS는 수집하지 않는다.
- 실제 사람·차량·번호판·이동 이력을 만들거나 노출하지 않는다. 주변 차량과 기사는 명시적인 `SyntheticFixture`다.
- 공식 정적 자료는 `ObservationPresentationOnly=true`로 시작한다. 출처·기준일·원본 hash·좌표계·이용조건과 검토 상태를 보존한다.
- 차선 도형이 있다는 사실만으로 통행 가능 차로를 확정하지 않는다. 차로 방향·접근 제한·정지선·교차로 연결이 검토된 항목만 `ReviewedLaneTraversalGraph`에 들어간다.
- 기존 `region-mobility-graph-*` 중심선 후보와 projection hash를 덮어쓰지 않는다. 차로 계층을 끄면 기존 표현으로 되돌아갈 수 있어야 한다.
- 한국의 우측통행 원칙은 경로 검토 기준으로 사용하되, 중심선을 좌우로 기계적으로 밀어 만든 선을 실제 차로라고 주장하지 않는다.
- 서버는 정적 자료의 판본·검토·배포 상태를 제공하고, Simulation Core는 게임 시간·seed·신호 현시·합성 차량과 대기열을 결정한다. Hosted는 서버 안의 Simulation Host가 같은 Core를 실행하며 Unity는 읽기 전용이다.
- 새 공식 Scene을 만들지 않고 canonical `SimulationWorldShell`의 사가정 역세권 모듈 아래에서 계층을 조립·해제한다.

### 지역 계층

```text
RegionExperienceRoot
├─ Geography
├─ Mobility
│  ├─ RoadCenterlineGraph                 기존 중심선 후보
│  ├─ LaneGeometryObservation             공식 차선·정지선 도형
│  ├─ DirectionMarkingObservation         공식 진행·회전 방향 표시
│  ├─ TrafficControlObservation
│  │  ├─ Intersections
│  │  ├─ Crosswalks
│  │  ├─ SignalControllers
│  │  └─ SignalHeads
│  ├─ ReviewedLaneTraversalGraph          사람 검토된 차로·회전 연결만
│  ├─ ActiveRouteVisuals
│  └─ SyntheticTraffic
│     ├─ SignalStateSnapshot
│     ├─ AmbientVehicleActors
│     └─ CourierActor
├─ PlacesAndBusinesses
├─ OperationalSnapshot
└─ Gameplay
```

정적 관찰 자료, 검토된 통행망, 동적 합성 상태는 서로 다른 판본과 hash를 갖는다. 하나의 합성 신호 주기를 검증했다는 사실이 원본 차선·시설을 승인하거나 실제 교통 현시를 재현한다는 뜻이 아니다.

## 공식 정적 자료 수집 결과 r1과 횡단보도 r2

서울시 공식 전체 파일 5종 47,430,933 bytes를 수집해 원본별 SHA-256과 함께 비공개 영수증에 동결했다. 전체 556,346행에서 기존 사가정 창과 수치상 교차하거나 포함되는 1,523행을 거친 후보로 저장·재조회했다. 이 가운데 사가정 지역 ID로 저장한 행은 원천 CRS가 해소된 1,398행뿐이다. 신호등 125행은 CRS 충돌이 남은 숫자 envelope 후보로 별도 `area:kr:seoul:traffic-signal-crs-unresolved-review` 버킷에 둔다. 아래 수량은 시설 확정 수나 통행 가능 수가 아니다.

| 자료 | 동결 파일·기준일 후보 | 선별 행 | r1 판정 |
| --- | --- | ---: | --- |
| [서울시 차선 관련 정보](https://data.seoul.go.kr/dataList/OA-15537/S/1/datasetView.do) | `A058_L_차선.zip` · 2021-08-09 | 1,039 | `StoredCandidate / PendingHumanReview` |
| [서울시 방향표시 관련 정보](https://data.seoul.go.kr/dataList/OA-15536/S/1/datasetView.do) | `A055_P_방향표시_20260213.zip` · 2026-02-13 | 318 | `StoredCandidate / PendingHumanReview` |
| [서울시 교차로 관련 정보](https://data.seoul.go.kr/dataList/OA-15534/S/1/datasetView.do) | `A008_P_20250814.zip` · 2025-08-14 | 25 | `StoredCandidate / PendingHumanReview` |
| [서울시 교통신호제어기 관련 정보](https://data.seoul.go.kr/dataList/OA-15538/S/1/datasetView.do) | `A061_P_제어기.zip` · 2021-08-09 | 16 | `StoredCandidate / PendingHumanReview` |
| [서울시 신호등 관련 정보](https://data.seoul.go.kr/dataList/OA-15546/S/1/datasetView.do) | `신호등(부착대).csv` · 2023-06-23 | 125 | `CrsConflictNumericEnvelopeCandidate / RegionMembershipUnresolved / PendingHumanReview` |

[서울시 교차로 및 횡단보도 시설·위치정보 OA-23081](https://data.seoul.go.kr/dataList/OA-23081/F/1/datasetView.do)의 `서울시 교차로 및 횡단보도 시설·위치정보_20260824.xlsx` 1,735,662 bytes를 별도 r2 원본으로 동결했다. SHA-256은 `1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE`이고 전체 21,776개 자료 행 중 좌표 공란 1행은 추정 없이 제외했다. EPSG:5186 사가정 창 안에는 중랑구 84행, 보행등 `유` 40행·`무` 44행, 교차로 관리번호 27개가 있다. `CSS_NUM=3205`·`사가정역`에는 횡단보도 7행, 보행등 `유` 4행이 결속된다. 로컬 MySQL에 원본 1건·정규화 84건을 비공개 저장하고 별도 문맥에서 84건을 재조회했으며, 즉시 재적용은 신규 원본·행 0건이었다. 자체 시험 18건과 직접 build를 통과했다. 판정은 `StoredCandidate / PendingHumanReview`; 차선·신호기·정지선 관계와 통행 권위는 승인하지 않았다.

차선 1,039행은 관리번호 1,036개와 이력 중복 3개를 포함하며 실제 polyline 교차가 아닌 bbox 교차 후보다. 표의 기준일은 동결 source version의 후보 기준일이지 원천 관측일이 아니고, 동적 포털 HTML 자체는 r1 원본 snapshot으로 보존하지 않았다.

자료마다 기준일이 다를 수 있으므로 최신 포털 수정일을 곧바로 도형 판본으로 사용하지 않는다. 실제 내려받은 파일의 이름·크기·SHA-256·원천 기준일·수집시각·좌표계·이용조건을 각각 동결한다. 좌표계 표기와 파일 내부 공간참조가 다르거나 같은 식별자가 서로 다른 위치를 가리키면 결합을 중단한다.

## 자료 수집부터 통행망까지의 관문

1. **범위 동결**: 사가정역 공식 기준점, 1km 창, WGS84와 원천 좌표계 변환 규칙을 판본화한다.
2. **원본 수집**: r1 다섯 자료의 metadata와 원본을 한 번 수집하고 파일 hash·기준일·이용조건을 비공개 검토 원장에 저장한다. 횡단보도는 r2에서 별도 영수증으로 수집한다.
3. **독립 재조회**: 같은 원본의 재적용이 중복을 만들지 않는지 확인하고 별도 프로세스에서 같은 hash·행 수·범위를 읽는다.
4. **1km 절단·감사**: r1의 차선·방향표시·교차로·신호제어기·신호등 범위 내 수량, geometry 유효성, 좌표계와 결손을 기록한다. 횡단보도 감사와 관계 결속은 r2에서 수행한다.
5. **관계 후보 생성**: 제공 식별자와 공간 근접을 별도 근거로 보존해 교차로–제어기–신호기–정지선–횡단보도 후보를 만든다. 불일치·다중 후보는 `Unresolved`로 둔다.
6. **차로 연결 검토**: 차선 사이의 주행 면, 진행 방향, 진입·이탈, 회전 연결, 정지선을 사람 검토한다. 공식 방향표시는 보조 근거이지 단독 통행 권위가 아니다.
7. **검토 통행망 생성**: 승인된 항목만 결정적 차로 그래프로 만들고 원본·정규화·관계 검토·그래프 hash를 함께 보존한다.
8. **합성 교통 적용**: 같은 검토 그래프를 읽는 신호 주기·기사·주변 차량 Simulation을 연결한다.

자료가 부족한 골목은 차로를 추정하지 않는다. 해당 구간은 `LaneGeometryMissing`, `DirectionUnresolved`, `IntersectionConnectionUnresolved` 중 정확한 결손으로 남기고 차로 기반 경로 계산에서 제외한다.

## 결정적 합성 신호와 주변 차량

### 신호 상태

- 신호 계획은 `signalPlanRevision`, `laneGraphHash`, `scenarioSeed`, `simulationTick`으로 같은 상태가 재현되어야 한다.
- 첫 검증 시나리오는 총 28초의 `SyntheticSignalCycle`: 남북 녹색 10초, 황색 2초, 전방향 적색 2초, 동서 녹색 10초, 황색 2초, 전방향 적색 2초다. 실제 사가정 신호 주기가 아니며 보행 현시는 정적 시설 관찰만 한다.
- 공식 정적 자료에서 신호기 존재는 확인할 수 있어도 실제 운영 주기를 추정하지 않는다. 합성 주기는 UI와 자료 카드에서 `SyntheticSignalCycle`로 표시한다.
- 주기 설정이 없거나 hash가 맞지 않으면 신호기는 정적 시설로만 보이고 차량 Simulation은 해당 접근 정지선에서 `TrafficControlUnresolved`로 멈춘다.

### 기사와 주변 차량

- 가상 기사와 주변 차량은 같은 차로 연결, 신호 그룹, 정지선, 안전 간격과 대기열 규칙을 사용한다.
- 차량은 차로 중앙 궤적을 따라 이동하고 빨간 신호에서는 정지선 뒤 같은 차로의 대기열에 선다. 초록 신호에서는 앞 차량부터 결정적으로 재출발한다.
- 주변 차량은 세계의 생활감을 만드는 `AmbientVehicle`이며 주문·배차·경제·NPC 수요·평판을 생성하지 않는다.
- `sagajeong-low-density-traffic.r2`는 배달 오토바이 1대와 주변 승용차 3대, 최대 4대를 표시한다. 0.25초 Tick, seed `20260913`, 대로 6m/s·생활도로 4m/s·골목 2.5m/s의 합성 표시 속도, 정지선 1.5m 전 정지, 같은 차로 최소 6m 간격을 판본화한다. 이 속도는 실제 제한속도나 관측속도가 아니다.
- 검토되지 않은 회전 연결, 대향 차로 침범, 차로 사이 순간 이동, 건물 중심 직선 fallback은 허용하지 않는다.
- 충돌 물리로 교통 결과를 확정하지 않는다. 1차는 차로 점유·선행 차량 간격·정지선 gate로 재현 가능한 대기와 재출발을 만든다.

### 골목 오토바이

- OSM 후보망의 `residential` 169개·`service` 145개 way는 관찰 후보이며 통행 승인이 아니다. 오토바이 access 태그와 방향이 대부분 비어 있으므로 자동으로 실행 그래프에 넣지 않는다.
- 첫 화면 후보는 OSM `service` way `1256772587`·이름 `면목로44가길`의 원본 중심선 22.775m다. `SagajeongReference.r3` 건물 윤곽과 중심선 충돌은 0건이지만 방향·access·유효 폭·주차·출입구가 미확정이므로 `ManualVisualReviewCandidate / TraversalReady=false`로 둔다.
- `service=parking_aisle`, `footway`, `cycleway`, `pedestrian`, 접근 금지·의미 미확정 선형에는 오토바이를 배치하지 않는다.
- 합성 주변 승용차는 골목 후보에 진입하지 않는다. 오토바이는 연속 표시 여정의 마지막 구간을 2.5m/s로 지나 후보 종점에 정차하고, 좁은 구간은 `이동자원점유Policy`의 단일 점유 후보로 검증한다. r1의 골목 단독 왕복 표본은 역사적 화면 증거일 뿐 r2 여정의 현재 동작이 아니다.

## 첫 관찰 폐루프

```text
사가정역 모듈 선택
→ 가상 주문과 기사 선택
→ 검토된 우측통행 차로 진입
→ 주변 차량과 같은 접근로를 따라 교차로 접근
→ 빨간 신호에서 정지선 뒤 대기
→ 합성 초록 신호에서 앞 차량부터 출발
→ 음식점 CurbStop·Entrance에서 픽업
→ 배송지까지 같은 규칙으로 이동·전달
→ 대기 위치로 복귀
→ 다음 가상 주문 또는 역 전체 관찰 선택
```

신호 대기나 주변 차량 표현은 도착을 실제 주문 완료로 바꾸지 않는다. 기존 가상 배달 사본의 읽기 전용 경계를 유지하고, 향후 권위 WI가 승인될 때만 Simulation 배송 상태 전이와 결속한다.

현재 구현된 보조 관찰 구간은 `사가정로 접근 → 합성 신호 대기·재출발 → 면목로44길·면목로44가길 생활도로 → OSM way 1256772587 골목 → 후보 종점 정차`다. 약 277.3m의 동결 표시선이며 픽업·전달·복귀나 실제 주소 Entrance를 완료하지 않는다. 이동망 판본·projection/content hash, 15개 원천 edge와 세 역순 edge를 상태 사본과 별도 표시 경로 지문에 포함한다.

## 실패·회복

| 실패 | 화면과 상태 | 회복 |
| --- | --- | --- |
| 원본·좌표계·hash 불일치 | 새 교통 계층 전체를 거부하고 이유를 표시 | 맞는 동결 판본을 다시 읽는다. 기존 중심선 관찰 자료는 별도 상태로 유지한다. |
| 차로 방향·교차로 연결 미확정 | 해당 연결을 경로 후보에서 제외 | 사람 검토 뒤 새 그래프 판본으로 재생성한다. |
| 신호 그룹·정지선 결속 미확정 | 차량이 마지막 확인 정지선에서 멈추고 `TrafficControlUnresolved` | 결속을 검토한 뒤 같은 session을 새 판본에서 다시 시작한다. |
| 합성 주기 설정 없음 | 정적 신호 시설만 표시하고 차량 자동 진행을 끈다 | 승인된 시나리오 설정을 선택한다. |
| 차량 경로 단절 | 마지막 확인 위치에서 `RouteUnresolved` | 다른 승인 경로를 고르거나 해당 주문을 취소하고 대기 위치로 복귀한다. |
| stale revision 또는 모듈 전환 | 오래된 상태 사본을 폐기하고 차량·신호 표현을 회수 | 새 station/session 상태 사본을 원자적으로 적용한다. |

## Unity 표현 원칙

- 차선·정지선·횡단보도는 확대 시 읽히고 전체 보기에서는 도로 위계를 해치지 않도록 거리별 단순화를 사용한다.
- 신호기 위치와 향하는 방향은 검토된 배치 맵을 따르며, 모든 도로 방향에 같은 신호등을 복제하지 않는다.
- 기사·오토바이·주변 차량은 화면 크기와 성능을 위해 pooling과 LOD를 사용하되, 비활성화·전환 시 메모리와 GameObject를 회수한다.
- 선택한 기사 경로와 현재 신호 대기는 주변 차량보다 명확하게 읽히되, 광고·후원 배지나 운영 상태의 색상 의미를 덮지 않는다.
- Game View에서는 전체 1km 대로 접근, 생활도로 접근, 건물 사이 골목 후보 종점의 세 화면을 남긴다. 자동 재생에서는 서로 다른 Tick의 이동량과 빨간 신호 정지·초록 신호 재출발을 별도 기록하고, 건물에 가린 기사의 정확 Transform 투영 위치는 명시적인 화면 투시 표식으로만 보조한다.

## r2 최초 구현·검증 결과

- 서버에는 `sagajeong-low-density-traffic.r1` 계약과 결정적 Engine을 추가했다. 배달 오토바이 1대와 주변 승용차 최대 3대, 0.25초 Tick, seed `20260913`, 28초 합성 신호 주기, 정지선·최소 간격과 골목 전용 왕복을 상태 사본으로 만든다. 유한 수치·판본·hash·기사 여정 ID와 경로 지문·차로/신호 참조가 다르면 원자적으로 거부한다.
- 입력 hash는 차로 `5748C4A8D4C4EAC9FF1E55C1578F18546FEBCC5AC7ADC602D20B1615F1BDB2B0`, 설정 `21388BBB6104F7362E9246C8700C95704EF76F9A5F3A14D56A8AAC2A77941CA9`, 전체 입력 `AE0FA2D2BD12807F0BE8416005B2F7B3BB1FA090BFB327F4BA3602715ABAA218`, 배달 경로 `720664785E1CE71ECB7D492ED5903514FA5D1A6237D91896187C99385146B88A`로 동결했다.
- 서버 집중 시험은 저밀도 교통·음식 배달 여정·다중 기사 공통 규칙 40/40, Unity EditMode는 엄격 해석·낮은 revision·hash 충돌·배우 회수·자동 화면 검증 선행 조건을 포함해 42/42가 통과했다.
- Unity `6000.5.6f1`의 격리 프로젝트에서 canonical `SimulationWorldShell`을 열고 저장하지 않는 임시 View로 실제 Play Mode를 실행했다. 자동 재생 73개 상태 사본·최종 Tick 72에서 `redStop=true`, `greenRestart=true`, `alleyRoundTrip=true`를 확인했다. 대로 이동 34.99m, 골목 정방향 22.49m·역방향 21.95m였고 관찰 종료 시 활성 배우는 3개였다.
- Game View는 전체 1km, 사가정역 합성 신호 대기, `면목로44가길` 골목 오토바이의 3장을 남겼다. 화면은 합성 상태 사본을 Engine에서 Unity Interpreter로 직접 적용한 결과이며 HTTP API·JSON Client 경로를 거치지 않았다. 저장 Scene은 바꾸지 않았고 메인 Unity 편집기의 미저장 작업도 건드리지 않았다.
- 이 결과는 `SyntheticFixture`의 Transform 이동·신호 대기·골목 판독 증거다. OSM way `1256772587`은 계속 `ManualVisualReviewCandidate / TraversalReady=false`이며, 음식점→대로→골목→전달지를 잇는 실제 통합 배달 경로, Hosted 실행, Save/Replay, 플레이어 입력, Scene 영속 결속과 Steam 공개 승인은 후속이다.

## r3 연속 후보 경로 구현·검증 결과

- 서버 계약을 `station-synthetic-traffic-snapshot.v2`로 확장하고 기사 표시를 대로·생활도로·골목의 3개 leg로 연결했다. 기사는 주변 차량과 같은 동서 접근 차로와 신호·6m 대기 간격을 사용한 뒤 생활도로와 단일 점유 골목을 지나 `StoppedAtCandidateEndpoint`에 머문다. `Delivered`나 운영 주문 변경은 생성하지 않는다.
- 상태 사본은 이동망 stable ID·revision·projection/content hash, 4개 lane 도형, 원천 방향 `Unknown`, 검토 `PendingHumanReview`, 실행 권위 false, 15개 source edge의 순서와 `AsStored/ReverseStored`를 제공한다. actor의 `DisplayPolylineOrderCode`는 화면 polyline 순서만 뜻하며 OSM 통행 방향과 분리했다.
- 기존 음식배달 경로 지문은 정적 합성 참조로만 유지한다. `JourneyBindingStateCode=DeclaredSyntheticReferenceOnly`, `OperationalBindingReady=false`, `CanonicalDeliveryMutationAllowed=false`, `TraversalReady=false`, `GameplayReady=false`, `DistributionApproved=false`다.
- 서버 저밀도 집중 시험 26/26와 배달 관련 결합 회귀 65/65를 통과했고 개별 TRX를 `artifacts/local/validation/20260913-sagajeong-continuous-route-evidence`에 남겼다. 최종 범위 Task는 Simulation solution build와 전체 시험 1,954/1,954를 통과했다(`artifacts/local/validation/20260913-185444`). 원천 edge 길이 합 277.276m와 3자리 표시 좌표 재계산 277.277388m를 별도 필드로 확인했다. 정지선까지 일부 이동한 Tick에 대기 상태와 이동 평균 속도가 함께 남던 첫 Play 오류는, 위치·경로 진행을 보존하고 대기 상태의 현재 속도만 0으로 기록하도록 고쳤으며 0~400 Tick 전 구간 회귀로 잠갔다.
- Unity `6000.5.6f1` EditMode에서 v2 상태 사본의 엄격 해석·수명주기와 0~400 Tick 순차 해석을 15/15로 확인했다. 격리된 canonical `SimulationWorldShell` Play Mode는 Tick 0~286의 자동 상태 사본 287개를 적용해 적색 정지, 녹색 재출발, 대로·생활도로·골목 진입과 후보 종점 정차를 모두 관찰했다. 최종 경로 진행은 277.277/277.277m이고 실제 Transform 누적 관찰은 대로 106.590m·생활도로 147.209m·골목 23.335m다.
- 전체 대로 접근 Tick 0, 생활도로 Tick 180, 골목 후보 종점 Tick 286의 Game View PNG 3장을 `artifacts/local/validation/20260913-sagajeong-continuous-unity/gameview/20260913-185020-153`에 남겼다. 지붕 가림 구간은 `기사 위치 · 실제 Transform · 화면 투시 표식`으로 정확 투영점을 함께 표시한다. 화면은 Engine 상태 사본을 Unity Interpreter에 직접 적용한 결과이며 HTTP API·JSON Client를 거치지 않았다.
- 검증 전후 저장 `SimulationWorldShell` SHA-256은 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`로 같고 Scene 파일은 Git 변경 0이다. 메인 Unity 편집기의 미저장 Scene은 그대로 보존했다. canonical Scene의 기존 bootstrap·replay·server 연결과 누락 자산 오류는 이번 경로 성공과 별도로 남으므로 Console 전체 정상은 선언하지 않는다.

## r4 A+ 생활 회랑 보완 문답

### 지금·여기·나·너·이렇게

- **지금**: 전체 1km 디오라마와 277.277m 차량·오토바이 후보 경로는 보이지만, 짧은 단일 이동만으로는 역 주변에서 사람들이 출근하고 장을 보고 산책하고 배달 기사와 골목에서 양보하는 생활 연속성을 읽기 어렵다.
- **여기**: 전체 1km 배경은 그대로 두고 사가정역 교차로, 역세권 보도, 면목로45길·사가정로51길 쪽 생활도로, 면목로44길·44가길 쪽 이면도로와 건물 사이 골목을 한 활성 생활 회랑으로 묶는다.
- **나**: 관찰자는 전체 조망에서 생활 흐름을 보고 한 기사나 보행자를 선택해 교차로·상점 앞·골목·귀환까지 따라간다.
- **너**: 합성 주민·통근자·산책자·장보기 보행자, 배달 오토바이와 저밀도 차량은 같은 Simulation Tick과 충돌 규칙을 공유하되 실제 사람·가구·주문을 뜻하지 않는다.
- **이렇게**: 도로 면, 차량 그래프, 보행 그래프, 횡단 충돌 구역과 공유 골목 면을 분리하고 Unity는 서버·Simulation 상태 사본을 읽어 이동과 대기·양보를 표현한다.

### A+ 활성 생활 회랑 후보

```text
사가정 1km 정적 배경
└─ A+ 활성 생활 회랑 약 390m × 350m
   ├─ 사가정역 교차로·횡단 구역
   ├─ 역세권 생활 순환 약 339.808m
   ├─ 동측 이면도로·골목 순환 약 302.557m
   ├─ 사가정로 연결부 왕복 약 110.694m
   └─ 합계 약 753.060m의 8자형 관찰 동선
```

- 이 후보는 전체 1km를 동시에 동적으로 채우지 않으면서도 대로 신호 대기, 횡단, 상점 앞 체류, 이면도로 통행, 건물 사이 공유 골목 양보와 귀환을 한 화면 체계 안에서 관찰할 수 있는 거리다.
- 역세권 순환 후보는 `218790209` 사가정로, `218798305` 면목로, `371238059` 면목로45길, `371238058` 사가정로51길을 잇는다.
- 이면도로·골목 순환 후보는 `1112326984` 사가정로, `1112326970` 면목로44길, `576294786` 면목로44가길, `1256772587` 골목 후보와 `1256772583`·`238268118` 면목로44길, `238268119` 사가정로53길을 잇는다.
- 두 순환로와 연결부는 현행 동결 이동망에 존재하는 선형으로만 계산한 후보이며, 방향·access·보도 연속성은 `PendingHumanReview`, 모든 통행 권위는 `TraversalReady=false`다.
- 사람 검토 전에는 두 공유 골목 충돌 구역을 `SyntheticDisplayOnly`로만 표시한다. 실제 골목 폭·보도·차량 통행 가능성을 확정하거나 길찾기 권위로 사용하지 않는다.

### 원본 공간과 판독용 공간 분리

- 건물 윤곽, 주소 결속점, 도로 중심선과 공개 자료 좌표는 `SourceGeometry`로 보존하며 생활 연출을 위해 옮기거나 덮어쓰지 않는다.
- 좁은 도로와 골목은 카메라 거리에서 배우가 가려지지 않도록 폭, 가장자리 여백, 배우 lateral offset을 `PresentationGeometry / SyntheticDisplayOnly`로 별도 생성할 수 있다.
- 일반 도로 양쪽의 고정 폭 띠를 곧바로 실제 보도나 보행 가능 영역으로 해석하지 않는다. `footway/path/pedestrian` 후보, 횡단보도와 사람 검토 연결만 `ReviewedPedestrianGraph`로 승격한다.
- 골목은 보도가 있다고 꾸미지 않고 `SharedAlleySurface`로 표현한다. 보행자가 먼저 점유하면 오토바이는 `WaitingForPedestrian`으로 대기하고, 보행자가 벗어나면 같은 요청 순서대로 재출발한다.

### 첫 생활 배우와 상태 후보

- 기존 합성 생활 배우 9명과 별도로 주변 보행자 6명을 둔다. 첫 활성 예산 후보는 화면 내 보행자 최대 12명, 차량·오토바이 최대 4대다.
- 통근자 2명은 일반화된 구역 입구에서 보도로 들어와 신호를 기다리고 횡단한 뒤 역 입구로 나간다.
- 장보기 보행자 2명은 일반화된 주거 출입 지점에서 골목과 상점 앞 체류점을 왕복한다.
- 산책자 1명은 순환로와 5~15초 휴식을 반복하고, 통과 보행자 1명은 화면 가장자리 입구 사이를 지나간다.
- 공통 상태는 `Enter → Walk → Pause / WaitSignal / Cross / YieldInAlley → Exit`이며 화면 중앙의 임의 생성·삭제, 목적 없는 무작위 배회, 건물 관통과 순간 이동을 금지한다.
- 일반화된 출입 지점은 실제 세대·주민·상세 주소가 아니며 음식점·운영 OS 원장과 분리된다. 주변 보행자는 주문·수요·평판·경제 효과를 만들지 않는다.

### 거리별 표현과 검증 후보

- 0~80m는 최대 6명의 저폴리 배우에 보행·대기 동작을 표시하고, 80~220m는 최대 12명을 단순 동작으로 표현하며, 그 밖은 작은 생활 표식으로 낮춘다.
- 배우 풀 후보는 16~20개로 두고 횡단 중이거나 공유 골목을 점유한 배우는 거리 때문에 갑자기 회수하지 않는다.
- 같은 seed·Tick·판본 입력은 같은 이동·대기·양보와 상태 hash를 만들어야 한다.
- 빨간 보행 신호 횡단, 차량–보행 충돌, 건물 관통, 골목 교착은 0건이어야 하며 골목 양보 뒤에는 둘 다 결정적으로 이동을 재개해야 한다.
- 최소 1,800 Tick 연속 Simulation과 Unity EditMode를 통과한 뒤 전체 조망, 교차로 횡단, 상점 앞 체류, 공유 골목 양보의 Game View 4장을 별도 증거로 남긴다.
- 자동 시험, Play Mode 화면, 서버 HTTP 연결, 저장 Scene 결속, 실제 길찾기 승인과 Steam 배포 승인은 서로 대신하지 않는다.

## r5 H·WI·AreaSet·Sky 조화 보완

### H 공간 의미 결속

사가정 1km 공간자료와 A+ 활성 창을 H 단계로 다시 이름 붙이지 않는다. H는 면적이나 좌표 크기가 아니라 Actor의 행위와 공간 조립 깊이를 뜻한다.

| 구분 | 사가정 r5 후보 | 기존 재사용 경계 |
| --- | --- | --- |
| H 밖 공간 근거 | 건물·도로·차선·신호·행정 경계·원본 hash·1km 표현 창 | `StationDioramaDatasetDescriptor`와 기존 사가정 공간 판본을 보존한다. |
| H1 행동·지원 지점 | 역 출입·신호 대기/횡단, 음식점 픽업, 전달·수령, 공유 골목 양보, 근무·휴식 | 실제 WI 직접 결과 또는 명시된 지원 역할이 있는 지점만 결속한다. |
| H2 블록 | 역 교차로, 생활 상권, 저층 주거, 공유 골목 | 기존 `lowrise-residential`, `market-life-commerce`, `town-residential-alley`, `town-order-fulfillment` 후보를 역할 참고로만 사용한다. |
| H3 경관 | 339.808m 역세권 생활 순환과 302.557m 이면도로·골목 순환 | `town-resident-service-loop`, `town-market-fulfillment`의 폐루프 구조를 참고하되 좌표와 지역 의미를 복사하지 않는다. |
| H4·AreaSet | 두 H3와 이후 사가정시장·안전/회복 역할을 묶을 사가정 생활권 | 첫 A+는 `부분 AreaSet 구성 후보`다. 기존 Town 기준안이나 Actual E5가 아니다. |
| H5 | 여러 역 AreaSet과 역간 물리 회랑의 Scenario 배치 | 단일 사가정역 첫 리팩터링에서는 만들지 않는다. |

현행 Town 기준 구성은 저층 생활, 순환시장, 오염 통제·구호, 주민 서비스와 시장 이행 다섯 역할 슬롯을 요구한다. A+가 제공하는 일부 역할만으로 기존 Town AreaSet을 그대로 복사하거나 완성 H4라고 선언하지 않는다. 사가정시장과 복합 생태 이변의 안전·회복 공간은 후속 기획에서 실제 필요가 확인될 때 추가한다.

### WI와 NPC 결속

- 음식점 생활은 `WI-CITY-RESTAURANT-ACCEPT → WI-CITY-RESTAURANT-COOK`을 재사용한다.
- 기사 생활은 `WI-CITY-SYNTHETIC-ASSIGN → MOVE → PICKUP → DELIVER → RECEIVE → RETURN`을 재사용한다.
- 근무와 휴식은 `WI-CITY-SYNTHETIC-LIFE-SHIFT`, `WI-CITY-SYNTHETIC-LIFE-REST`를 재사용한다.
- 신호 대기, 횡단 대기, 골목 양보와 프레임별 보행은 독립 WI가 아니다. `MOVE`의 Task·guard·blocked reason과 공용 이동·점유 정책에서 처리한다.
- 주변 통근자·장보기 보행자·산책자·통과 보행자는 합성 관찰 프로필이다. 주문·수요·평판·재고·성장·ActionRecord와 H 승격을 만들지 않는다.
- `PickedUp`, `Delivered`, `Received`, `Returned`, `Working`, `Resting`처럼 하나의 의도와 직접 권위 결과가 있는 전이에만 기존 WI 기록을 남긴다.

### Sky Engine과 시간·날씨

- Sky Engine은 H나 AreaSet 아래에 넣지 않고 canonical `SimulationWorldShell`의 세계 공통 표현 형제로 유지한다.
- 현행 사가정 View는 전용 layer와 고정 배경·오후 표현광을 사용하므로 기존 Sky 상태가 실제로 보이는 상태가 아니다. 기존 Nature 대기 프로필을 사가정용으로 재명명하지 않는다.
- 첫 구조 후보는 `WorldAtmosphere` 상태 사본을 Nature와 사가정 표현 대상에 함께 전달하는 Shell 수준 공급원과 다중 대상 Adapter다. 사가정 대상은 전용 카메라 배경·안개·layer 광원·구름·강수만 표현한다.
- 첫 단계 날씨는 조명·안개·구름·비·번개·음향만 바꾸고 동일 Tick의 WI 결과·NPC 일정·차량/보행 경로와 속도는 바꾸지 않는다.
- 우산, 처마 대기, 젖은 도로 감속, 날씨별 경로 선택은 별도 게임 효과다. 정확 주체·대가·실패·회복·Save/Replay가 승인되기 전에는 열지 않는다.

### 안전한 리팩터링 순서

1. r3의 상태 hash와 Game View를 회귀 기준으로 동결한다.
2. 기존 사가정 View를 `ActiveStationDioramaHost` 후보 아래 호환 Adapter로 감싸되 화면과 상태 결과를 바꾸지 않는다.
3. 공간 근거, H/부분 AreaSet, Mobility, WI/NPC, 업체·광고 Overlay와 Presentation을 별도 모듈로 분리한다.
4. 기존 WI 정본과 생활 ActionRecord·`MOVE` 작업 명세의 의미 불일치를 먼저 해소한다.
5. A+ 두 H3 후보와 H1/H2 역할을 Graph Map 레벨 1~3에 결속하고, 통행 미승인 도형은 계속 `TraversalReady=false`로 둔다.
6. 기존 Nature Sky 경로를 회귀한 뒤 사가정 환경 표현 대상을 추가하고 이중 조명·layer 누출·카메라 배경을 검사한다.
7. 역 전환 시 배우 pool, WI 구독, 표현 Root, 환경 대상을 모두 회수하고 같은 입력에서 같은 조립 hash를 확인한다.
8. 자동 시험, Play Mode·Game View, Console·청음, Save/Replay, Hosted 상태 사본과 실제 통행 승인을 각각 분리해 검증한다.

현재 AreaSet 구성 패턴 검사는 통과하지만 H 공간 생성 문서와 게임 기획 주도 H 재고는 각각 `GeneratedDocumentOutOfDate`, `DemandH2TargetMismatch`로 차단된다. 이 드리프트를 다른 작업의 생성물을 임의 갱신해 숨기지 않으며, 해당 소유 작업이 해소되기 전에는 사가정 H 등록·hash 봉인·Actual E5 승격을 수행하지 않는다.

## r6 기존 디오라마 보존형 건물 선택 모듈

- 첫 구현은 `SagajeongReference.json` r3의 건물 602개·도로 2,397개와 기존 절차 Mesh를 다시 만들지 않는다. 카메라·조명·색상·Renderer·Material·chunk·Scene hash도 회귀 기준으로 고정한다.
- 602개 건물마다 GameObject나 Collider를 만들지 않는다. 화면 ray와 동결 건물 외곽을 CPU에서 대조하고, 겹치면 가장 가까운 교차와 작은 외곽 면적, 건물 고유 식별자 순으로 하나를 결정한다.
- 높이 보완과 비공개 공간 보완층이 r3 건물을 대체해 그린 경우는 MeshBuilder와 동일한 높이·외곽으로 선택 기둥과 H 표식을 계산한다. 식별자·주소·H 후보는 계속 r3 원본 602개에만 소유시키고, 추가 보완 건물을 자동 H로 승격하지 않는다.
- 모든 건물은 원본 고유 식별자·공개 주소·건물 종류·표현 높이 근거를 조회할 수 있다. 선택은 Unity 표현 로컬 상태이며 서버·Simulation·WorldRevision·WI·ActionRecord를 바꾸지 않는다.
- 첫 H 후보 결속은 기존 합성 의미 위치가 건물 윤곽 안에 확인된 두 곳으로 제한한다.

| 건물 원본 | 합성 의미 위치 | 읽기 전용 후보 결속 |
| --- | --- | --- |
| `osm:way:1256772531` | `synthetic-place:food-route-a` | H1 `h1-stock:town-order-packing` → H2 `h2-candidate:town-order-fulfillment`·보조 `h2-candidate:market-life-commerce` → H3 `h3-candidate:town-market-fulfillment` / `MarketFulfillment` |
| `osm:way:1256772606` | `synthetic-place:food-route-b` | H1 `h1-stock:town-resident-pickup` → H2 `h2-candidate:lowrise-residential`·보조 `h2-candidate:town-residential-alley` → H3 `h3-candidate:town-resident-service-loop` / `ResidentService` |

- 이 결속은 실제 업체 입점·세대·출입구·통행 가능성을 뜻하지 않는다. 다른 건물은 주소·종류·거리로 H를 추정하지 않고 `WaitingForGraphMapBinding`으로 남긴다.
- 부분 AreaSet은 `area-set:theory:lowrise-market-region`과 `area-set-composition:town:lowrise-life-market`을 참고하는 `PartialCandidate`다. 충족 후보 `ResidentService`·`MarketFulfillment`, 미충족 `LowriseMarket`·`CircularMarket`·`ContaminationRelief`를 함께 표시하며 실제 AreaSet 고유 식별자는 만들지 않는다.
- 카드에는 `CandidateOnly=true`, `ActualE5=false`, `TraversalReady=false`, `GameplayReady=false`, `ServerProjectionReady=false`를 표시한다. 전체 보기·화면 닫기·다른 도로 또는 업무 표식 선택에서 건물 선택을 해제한다.
- 상단 역 방어 IMGUI 버튼과 하단 통합 시점 uGUI 버튼이 입력을 소유하면 건물 선택을 시작하지 않는다. 카드는 역 방어 진입 버튼 아래에 배치한다.
- 첫 절편은 선택 표시와 정보 카드까지만 포함한다. 후속 버튼은 선택 건물에 명시적으로 결속되고 이미 준비된 WI의 조회·Preview만 열 수 있으며 H 편집, 주문 수락, 배차, 결제 같은 상태 변경은 별도 승인 전 만들지 않는다.
- 구현·검증 범위는 [건물 H 계층 선택 구현 명세 r6](building-hierarchy-selection.implementation.r6.md)가 소유한다.

## 제외

- 실제 교통신호 주기·실시간 교통량·CCTV·차량 GPS 수집
- 실제 번호판·운전자·기사 위치·개인 주소의 저장·표시
- 검토되지 않은 차로와 회전 연결의 자동 승인
- 물리 기반 전체 교통 시뮬레이션, 교통법규 전 범위, 사고·경찰·긴급차량 체계
- 실제 주문·배차·결제·광고 노출 순위·NPC 수요에 대한 효과
- 사가정역 밖 다른 역으로의 자동 확대와 새 Unity Scene 생성
- 화면 성공을 통행 권위·운영 완료·Steam 배포 승인으로 해석하는 것

## 미정

- 첫 검토 교차로 후보는 교차로·제어기 양쪽에서 `CSS_NUM=3205`, 이름 `사가정역`으로 각 1건, 횡단보도 7건으로 확인됐다. 개별 신호기·정지선·접근로의 사람 검토 관계는 미정이다.
- 첫 A+ 활성 범위를 약 390m × 350m, 753.060m 8자형 생활 회랑과 공유 골목 충돌 구역 2곳으로 동결할지
- 첫 관찰 시간대를 출근 전후, 낮 장보기, 저녁 배달 중 무엇으로 시작할지와 시간대별 배우 밀도
- 저밀도 이후 보통·혼잡 profile의 차량·보행자 수, 생성 간격과 전체 1km 성능 예산
- 오토바이의 실제 자동차 차로 이용 범위와 골목별 access·방향·폭·출입구 통행 승인
- 버스·자전거를 후속 생활 교통에 포함할지
- 저밀도 지원 Simulation을 향후 독립 WI Goal로 분리할지
- 정적 관찰 자료의 Mongo station-scoped 저장 계약과 검토 상태의 정확 schema 이름
- 사가정 합성 시간·대기 프로필과 날씨의 WI·NPC 행동 효과를 여는 가장 이른 단계
- H 생성 문서·수요 재고 드리프트와 생활 ActionRecord·`MOVE` 직접 결과 명칭 불일치의 소유 작업
- 나머지 600개 건물의 H 후보 결속을 어떤 판본화된 Graph Map 입력과 사람 검토 절차로 확장할지

## 다음 질문 하나

건물 선택 카드의 첫 후속 조작은 **그 건물에 명시적으로 결속된 기존 음식배달 주문·기사 흐름을 읽기 전용으로 추적하는 기능**으로 둘까?

추천은 **그렇게 한다**다. 사용자가 클릭한 공간에서 이미 구현된 주문·기사 상태 사본을 따라가게 하면 H 모듈이 실제 생활 흐름을 설명하는 첫 효용이 생긴다. 주문 수락·배차·결제처럼 권위 상태를 바꾸는 조작과 H 구조 편집은 별도 관문으로 유지한다. 390m × 350m·753.060m 공간 동결과 첫 시간대 질문은 다음 수평 문답으로 보존한다.
