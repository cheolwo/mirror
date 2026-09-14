# 사가정 차로·신호·합성 교통 E1~E7 수직 작업 명세 r3

- 승인 기획: [사가정 차로·신호·합성 교통 r3](README.md)
- 기획 판본: `sagajeong-lane-signal-traffic.r3`
- 기획 hash: `27E5E10422476693233B1005F9256FD8AA3FB6FB99ECC4FC1CCA36E3E4E40C5C`
- 명세 상태: `SyntheticFixtureImplementationVerified / ServerLogicTested / UnityInterpreterEditModeVerified / SyntheticFixturePlayModeGameViewObserved / NoAutomaticEvidencePromotion`
- 대표 플레이 약속: 가상 배달 기사와 합성 주변 차량이 사가정역 1km의 검토된 차로를 따르고 같은 합성 신호에서 대기·재출발하는 과정을 관찰한다.
- 관련 WI: `interaction-goal:synthetic-delivery-move.v1`·`WI-CITY-SYNTHETIC-MOVE`를 지원한다.
- 신규 WI·Goal: `NotRequiredForSupportSlice`. 기존 배달 이동의 직접 결과를 바꾸지 않는다.
- 준비된 주체: 상위 기획의 가상 기사·가상 오토바이·가상 주문 정적 참조, `sagajeong-low-density-traffic.r2`의 합성 주변 차량 3대와 합성 신호 계획.
- 수정 경계: 공식 정적 교통자료 수집·검증, station-scoped 읽기 계약, 검토 차로 그래프, Simulation 합성 신호·차량 상태, Unity 읽기·표현, 집중 시험과 Game View 증거.
- 보존 경계: 실제 운영 주문·배차·GPS·교통 제어, 기존 OSM 중심선과 사가정 projection hash, 행정동·법정동 원본, canonical `SimulationWorldShell`, 광고·후원 및 다른 작업 변경.

이 문서는 기존 WI의 지원 slice를 위한 E7 v2 작업 명세다. [저밀도 연구 r1](low-density-alley-study.r1.md), [연속 표시 경로 연구 r2](continuous-delivery-route-study.r2.md)와 기획 hash를 기존 `synthetic-delivery-move.e7-work-order.json`에 다시 결속한다. 구현·자동 시험·Game View는 수행할 수 있지만 실제 통행 승인과 E 단계 승격은 별도 판정한다.

## 권위와 저장 책임

| 자료·상태 | 소유자 | Unity 사용 | 금지 |
| --- | --- | --- | --- |
| 원본 파일·metadata·hash·이용조건·검토 상태 | 서버 비공개 검토 원장 | 출처·품질 요약만 | Unity 원본 포함, 검토 없이 배포 |
| 정적 차선·교차로·신호 시설 관찰 자료 | station-scoped 불변 공간 자료 | 읽기 전용 공간 표현 | 통행 가능 자동 판정 |
| 검토된 차로·회전·정지선 그래프 | 서버가 제공하는 판본화된 Simulation 입력 | 경로·배치 해석 | Unity NavMesh 또는 Transform으로 수정 |
| 합성 신호·주변 차량·기사 진행 상태 | Simulation Core | 상태 사본 보간·표현 | 실제 교통·주문·배차 상태로 승격 |
| 실제 주문·배차·교통 운영 | 기존 운영 서버 | 이번 slice에서 사용하지 않음 | 합성 이동 결과로 변경 |

Solo는 `LocalProcess`, Hosted는 서버 안의 `RemoteHost`에서 같은 Simulation Core와 시나리오 판본을 실행한다. Unity는 신호 색상, 차량 위치와 대기 상태를 변경할 권위를 갖지 않는다.

## E7→E1 하향 영향 검토

| E | Logic | Presentation |
| --- | --- | --- |
| E7 | 역 선택·기사 관찰 입력부터 신호 대기·재출발·픽업·전달·복귀와 다음 선택이 같은 session에서 닫혀야 한다. | 실제 Game View에서 전체 보기, 차로 확대, 기사와 주변 차량의 동시 정지·순차 출발, 보행 인계와 복귀가 읽혀야 한다. |
| E6 | 경로 단절, 신호 결속 실패, 대기열 포화, stale revision, 취소·재개·모듈 전환의 회복을 설명해야 한다. | 차선·신호 상태·정지 이유·선택 기사와 주변 차량, 실패 진단이 HUD 의존 없이 공간에서 구분되어야 한다. |
| E5 | 승인된 차로 그래프와 합성 설정을 실제 Simulation session이 소비하고 신호·차량 상태 사본을 만들어야 한다. | 같은 상태 사본을 canonical `SimulationWorldShell`의 실제 사가정 모듈에 결속하고 활성 Renderer·Bounds·차량 pooling을 확인해야 한다. |
| E4 | 기사·차량·신호 그룹·정지선·교차로·차로 연결·scenario time과 H·Graph Map·배치 맵 문맥을 같은 판본으로 동결해야 한다. | 신호기·차선·횡단보도·차량 시각 자산 키, LOD, fallback, 위치·방향·가림·크기·카메라 의도를 준비해야 한다. |
| E3 | 원본·정규화·검토 그래프 결정성, 합성 주기와 대기열 결정성, Save/Replay, Local/Remote 동등성, 변조·직선 fallback 거부를 자동 시험해야 한다. | 엄격 Decoder, 낮은 revision·hash 불일치·잘못된 차로/신호 참조 거부, pooling·전환·정리와 보간을 자동 시험해야 한다. |
| E2 | 공식 자료 Adapter, 좌표 변환, 관계 후보 생성, 검토 그래프 생성, 결정적 신호·차량 Engine과 상태 사본 생성기를 준비해야 한다. | 정적 계층 Client·Decoder·Interpreter와 합성 상태 Repository·Presenter를 기존 역세권 모듈 계약에 추가해야 한다. |
| E1 | 정적 관찰/검토 통행/합성 상태의 schema, 안정 ID, 판본·hash, 좌표계, 오류·권위·공개 경계를 고정해야 한다. | 차선·신호·차량 표현 모델, 거리별 LOD, 상태 색상, 수명주기와 기존 Mobility 계층 fallback 계약을 고정해야 한다. |

## 가장 낮은 착수 지점

합성 지원 slice의 Logic E1~E3 계약·순수 Engine 시험과 Presentation E1~E3 해석·수명주기 시험은 구현됐다. 실제 검토 차로 Graph, Application session, HTTP/JSON Client, 저장 Scene과 플레이어 입력은 닫히지 않았으므로 정식 증거 단계는 자동 승격하지 않는다. 공식 정적 자료를 수집했다는 사실이나 임시 Play Mode 화면을 실제 통행 권위의 근거로 재사용하지 않는다.

## 구현 묶음

### A. 공식 자료 수집·감사

1. 사가정역 1km 창과 원천 좌표계를 동결한다.
2. r1 차선·방향표시·교차로·제어기·신호등과 r2 횡단보도 `OA-23081` 원본·metadata를 별도 영수증으로 수집한다.
3. 파일별 SHA-256·수집시각·원천 기준일·필드 정의·이용조건을 보존한다.
4. 1km 절단 결과의 geometry·식별자·중복·좌표계·결손을 감사한다.
5. 로컬 비공개 저장, 동일 입력 재적용 신규 0건, 별도 연결 재조회를 확인한다.

완료 상한은 `EvidenceAcquired / ObservationOnly`다. 이 단계에서 통행망·Simulation·Unity 적용을 선언하지 않는다.

현재 r1은 공식 전체 파일 5종 47,430,933 bytes와 전체 556,346행을 동결했다. 명목상 1km 창의 거친 후보 1,523행 가운데 원천 CRS가 해소된 1,398행만 사가정 지역 ID에 속한다. 신호등 125행은 포털 EPSG:5186 표기와 내장 SRID 2093이 충돌하는 숫자 envelope 후보라 별도 CRS 미해결 검토 버킷에 저장했다. 로컬 MySQL 비공개 검토 원장 저장, 자체 시험 20건, 원본 5건·후보 1,523행의 독립 재조회, 즉시 재적용 신규 0건과 다른 수집 시각 동등성 검사를 확인했다. 차선 1,039행은 bbox 교차 결과이며 관리번호 1,036개와 이력 중복 3개를 포함한다. r2 횡단보도 원본 1건·정규화 84건도 같은 비공개 원장에 저장해 재적용 신규 0건과 독립 재조회를 확인했다. 모든 후보는 `PendingHumanReview`이고 관계 검토·통행 권위는 포함하지 않는다.

### B. 정적 관찰 계약과 관계 후보

다음 의미를 별도 schema로 보존한다. 실제 코드 이름은 기존 계약 대조 뒤 정하되 안정 의미를 합치지 않는다.

```text
lane-geometry-observation.v1
traffic-control-observation.v1
lane-traversal-review.v1
reviewed-lane-graph-manifest.v1
reviewed-lane-graph-tile.v1
```

- 차선·방향표시·교차로·신호기·횡단보도의 원천 ID와 원본 geometry를 보존한다.
- 식별자 결속과 공간 근접 결속을 다른 evidence code로 기록한다.
- 여러 후보, 기준일 충돌, 거리·방향 허용치를 벗어난 관계는 `Unresolved`다.
- 실제 파일 내부 공간참조와 metadata 표기가 다르면 normalize하지 않고 수집 오류로 중단한다.
- station-scoped 사본이 기존 행정동 current projection을 덮어쓰지 못하게 한다.

### C. 사람 검토 차로 그래프

- 첫 검토 구간은 사가정역 교차로와 상위 가상 배달 경로가 실제로 사용하는 최소 접근로로 제한한다. 정확 교차로 ID와 접근로 수는 원본 감사 뒤 확정한다.
- 차로 주행 면, 진행 방향, 교차로 진입·이탈, 허용 회전, 정지선과 신호 그룹을 한 검토 판본에 묶는다.
- 방향표시와 우측통행 원칙은 검토 보조 근거로만 사용한다.
- 승인되지 않은 connector는 생성하지 않으며 기존 중심선과의 후보 matching confidence를 통행 권위로 해석하지 않는다.
- 같은 승인 입력으로 동일 노드·간선·connector·hash를 생성한다.

완료 상한은 `TraversalCandidateReviewed`다. 전체 1km 통행 완결과 다른 역 재사용을 선언하지 않는다.

### D. 합성 신호·주변 차량 Simulation

입력은 최소한 다음을 포함한다.

```text
StationStableId
LaneGraphRevision / LaneGraphHash
SignalPlanRevision
TrafficProfileRevision
ScenarioSeed
SimulationTick
CourierJourneyId / RouteFingerprint
```

결정적 Engine은 신호 phase, 차로별 차량 진행, 선행 차량 간격, 정지선 gate, 대기열 순서, 출발·경로 진행과 골목 단일 점유 차단을 계산한다. exact 값은 [저밀도 연구 r1](low-density-alley-study.r1.md)과 [연속 표시 경로 연구 r2](continuous-delivery-route-study.r2.md)에 다시 결속된 `sagajeong-low-density-traffic.r2` 입력이 제공하며 숨은 코드 기본값으로 대체하지 않는다.

상태 사본은 최소한 신호 그룹별 상태, 다음 전환까지의 합성 시간, 차량 고유 식별자·종류·차로·진행 거리·속도·대기 순서·차단 사유, 기사 여정과 graph/config fingerprint, revision을 포함한다. 실제 차량·사람 식별자를 사용하지 않는다.

### E. Unity 읽기·표현

- 기존 사가정 역세권 모듈 아래에 정적 교통 시설과 동적 합성 교통 Root를 별도로 둔다.
- 차선·정지선·횡단보도는 기존 도로 mesh와 동일 좌표 프레임에서 z-fighting 없이 표현하되 도형 좌표를 Unity에서 보정해 권위를 만들지 않는다.
- 신호기 방향은 검토 배치 맵을 사용하고 신호 상태는 Simulation 상태 사본만 읽는다.
- 기사·오토바이와 주변 차량은 동일 차로 polyline·대기열을 소비한다. Unity NavMesh와 물리 충돌은 경로·신호 권위가 아니다.
- 전체 보기와 확대 보기에서 LOD를 바꾸고 선택 기사 경로·현재 신호 대기를 강조한다.
- 모듈 전환, stale snapshot, decode 실패와 종료에서 차량·신호·경로 GameObject와 메모리를 회수한다.
- 저장 Scene을 바꾸기 전에 임시 Root 검증과 Scene hash 비교를 수행하고, 영속 결속은 별도 승인된 단계에서만 한다.

### F. 통합 검증

1. 같은 source/config/seed/tick에서 동일 정적·동적 hash가 나온다.
2. 반대 차로, 미승인 회전, 잘못된 신호 그룹, 정지선 통과와 직선 fallback을 거부한다.
3. 빨간 신호에서 기사와 주변 차량이 같은 정지선·대기열에 멈추고 초록 신호에서 순서대로 출발한다.
4. Save/Restore/Replay와 Solo/Hosted가 같은 신호·차량 결과를 만든다.
5. 통행망 또는 설정 hash가 바뀌면 기존 상태 사본을 원자적으로 거부하고 부분 적용하지 않는다.
6. 기존 사가정 건물·도로·주소·운영 overlay, 행정동/역세권 API와 가상 배달 v1 계약 회귀를 통과한다.
7. Unity EditMode에서 Decoder·Interpreter·보간·대기열·pooling·전환 정리를 확인한다.
8. Play Mode와 Game View에서 전체 보기, 신호 대기 확대, 골목 오토바이 확대와 자동 Tick 이동을 확인한다. 저장하지 않는 검증 메뉴 호출은 실제 플레이어 입력 폐루프와 구분한다.
9. Console 차단 오류 0건, 저장 Scene과 실행 전후 hash, renderer/material/actor 수와 frame 예산을 별도 기록한다.

### G. 실제 완료 범위

- 서버 `station-synthetic-traffic-snapshot.v2` 계약과 결정적 Engine을 구현했다. 대로·생활도로·골목의 3개 leg, 15개 원천 edge와 3개 역순 표시, 신호 정지·재출발, 같은 차로 간격, 골목 단일 점유, 후보 종점 정차와 비권위 플래그를 검증했다. 저밀도 집중 시험 26/26와 배달 관련 결합 회귀 65/65가 통과했고 개별 TRX는 `artifacts/local/validation/20260913-sagajeong-continuous-route-evidence`에 있다. 최종 범위 Task는 Simulation solution build와 전체 시험 1,954/1,954를 통과했다(`artifacts/local/validation/20260913-185444`).
- Unity는 v2 상태 사본 Interpreter와 Playback 계층, 전체·생활도로·골목 후보 종점 검증 View를 구현했다. EditMode 15/15가 통과했고 신규 시험은 Tick 0~400의 모든 상태 사본을 순서대로 해석해 대기 상태의 현재 속도가 0인지와 비권위 경계를 함께 확인한다.
- 격리된 Unity `6000.5.6f1`의 canonical `SimulationWorldShell` 실제 Play Mode에서 Tick 0~286의 자동 상태 사본 287개를 적용했다. 적색 정지·녹색 재출발, 대로·생활도로·골목 진입과 후보 종점 정차를 확인했으며 경로 진행은 277.277/277.277m다. 실제 Transform 누적 관찰은 대로 106.590m·생활도로 147.209m·골목 23.335m다.
- 전체 대로 접근 Tick 0, 생활도로 Tick 180, 골목 후보 종점 Tick 286의 PNG 3장을 `artifacts/local/validation/20260913-sagajeong-continuous-unity/gameview/20260913-185020-153`에 확보했다. 건물 지붕에 가린 기사는 실제 Transform의 화면 투영점에 명시적인 투시 표식을 더해 판독한다. 검증 View는 저장하지 않는 임시 Root이며 Engine 상태 사본을 Unity Interpreter에 직접 적용한다.
- Hosted HTTP/live server·JSON Decoder/Client 전송·실제 OSM 길찾기·운영 주문 상태 전이·Save/Replay·실제 입력과 저장 Scene 결속은 수행하지 않았다. 저장 Scene SHA-256은 실행 전후 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`로 같고 Git 변경은 0이다. 이번 경로와 무관한 canonical Scene의 기존 bootstrap·replay·server 연결과 누락 자산 오류가 있어 Console 전체 정상은 미검증이다.

## 표현 E4 준비 초안

| 항목 | 준비 내용 | 상태 |
| --- | --- | --- |
| 플레이어가 읽을 순간 | 기사와 주변 차량이 같은 빨간 신호에서 멈췄다가 앞차부터 다시 출발하고, 같은 기사가 대로→생활도로→건물 사이 골목 후보 종점까지 연속 이동하는 순간 | `ContinuousSyntheticFixturePlayModeObserved` |
| H 역할·능력 | 역세권 1km H 문맥 안의 검토 차로 접근·교차로 통과·CurbStop 인계. 정확 H 참조는 Graph Map 대조 전 `Pending` | `Conditional` |
| 시각 자산 키 | 차선, 정지선, 횡단보도, 신호 지주·차량/보행 신호기, 가상 기사 오토바이, 합성 주변 차량 | `CandidateOnly` |
| 주 자산 후보 | 기존 도로 mesh 위 절차적 선형과 primitive 신호기·차량 placeholder | `TemporaryFixtureVerified` |
| 대체 표현 | 저비용 decal/line mesh, 단순 지주·발광면, 화면 크기 고정 vehicle marker | `Pending` |
| fallback | 정적 시설만 표시하고 `SyntheticTrafficUnavailable`; 위치 근거가 없으면 시설 자체를 숨기고 결손 표시 | `Defined` |
| Graph Map | 기존 사가정 Mobility graph에 차로·교차로·신호 제어 후보를 별도 하위 그래프로 추가 | `ReviewRequired` |
| 배치 맵 | 신호기 방향·정지선·횡단보도·lane connector와 queue slot 상대 배치 | `TemporaryFixtureOnly / FormalMapPending` |
| 배치 제약 | 동일 좌표계, 도로면 지지, 신호 방향, 정지선 앞 대기, 대향 차로 비침범, 횡단보도 가림 금지, LOD와 camera 판독 | `DefinedForReview` |
| InteractionAnchor | 역 선택, 기사 선택·따라가기, 교차로 확대. 현재는 검증 메뉴가 화면을 전환하며 실제 플레이어 입력은 아님 | `TemporaryMenuOnly / ExactBindingPending` |
| 후보 fingerprint | 공식 원본·관계 검토·그래프·시나리오·자산 fingerprint를 별도로 결속 | `StaticR1 / CrosswalkR2 / TrafficProfileR2 / DisplayRouteR2Bound` |
| E5 준비 | 자료·Graph Map·배치 맵·자산 후보·profile 수치가 아직 동결되지 않음 | `Blocked` |

## 증거 상한과 열린 관문

- 이 명세에 결속된 구현·자동 시험·임시 Play Mode 화면 증거가 존재하지만 문서 갱신만으로 Logic·Presentation E를 승격하지 않는다.
- 공식 자료 수집 뒤에도 관계 후보·사람 검토·검토 그래프 전에는 `TraversalReady=false`다.
- 합성 주기와 저밀도 차량 profile은 승인·구현됐지만 기능 플래그 없이 운영·공개 환경에서 기본 활성화하지 않는다.
- 순수 Engine의 시험용 상태 사본과 Unity 직접 적용 화면은 확인했다. Application session·Hosted 전송·저장 Scene 결속 전에는 정식 Presentation E5 증거로 승격하지 않는다.
- Play Mode와 Game View는 확인했지만 저장된 canonical Scene과 실제 입력이 없으므로 E7을 선언하지 않는다.
- 한 교차로 검증은 전체 사가정 1km 차로 완결이나 다른 역의 재사용 승인이 아니다.

## 예상 쓰기 경계

정확 파일은 기존 계약·작업 소유권 대조 뒤 동결한다. 현재 예상 범위는 다음과 같다.

- `eng/neighborhood/`: 공식 정적 교통자료 수집·정규화·감사·그래프 생성
- `eng/Ssalddel.PublicDataPortalImport/`: 로컬 비공개 원본·영수증·재적용·독립 재조회
- `Ssalddel.WorkflowRules.Contracts/UnityPackage/Runtime/`: station traffic 정적·동적 읽기 계약
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/`: 결정적 합성 신호·차량 순수 규칙
- `Ssalddel.Simulation.Application/RuntimeCore/`: Local·Hosted 실행 조율과 상태 사본 생성
- `Ssalddel.Simulation.Tests/`: 결정성·Save/Replay·Local/Remote·차단 시험
- `Ssalddel/Application/WorldProjection/`: 역세권 정적 교통 관찰 자료 조회
- `Ssalddel.Tests/`: 출처·hash·권한·ETag·결손·기존 API 회귀
- `C:/Users/user/ssalddel/Assets/Ssalddel/Runtime/World/`: 엄격 Decoder·Interpreter·상태 저장
- `C:/Users/user/ssalddel/Assets/Ssalddel/Presentation/World/`: 차선·신호·차량·대기열 표현과 pooling
- `C:/Users/user/ssalddel/Assets/Ssalddel/Tests/EditMode/`: Unity 계약·전환·회수 시험

`docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`, 기존 Goal 작업 명세는 동결 hash와 실제 검증 수준에 맞춰 갱신한다. 새 장기 제품 결정을 만들지 않았으므로 `docs/AI/DECISIONS.md`는 수정하지 않는다. Graph Map 원본은 통행 승인 전 수정하지 않는다.

## 재개·재검토 조건

1km 창, 역 기준점, 원천 파일·좌표계, 차선·교차로 관계 의미, 우측통행·회전 판정, Simulation 권위, 신호 phase 의미, 차량 profile, 광고·운영 효과, Scene 저장 범위가 바뀌면 가장 이른 Logic 또는 Presentation E1~E4를 다시 연다. 단순 시각 자산 교체라도 배치 외곽·방향·LOD·성능이 달라지면 Presentation E4부터 재검토한다.
