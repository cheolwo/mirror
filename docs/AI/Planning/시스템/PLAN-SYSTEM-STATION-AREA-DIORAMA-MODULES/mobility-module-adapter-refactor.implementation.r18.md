[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r18]

# 사가정 Mobility 공통 Adapter 보존형 리팩터링 명세

- 상태: `Implemented / BehaviorPreservingRefactor / MobilityAdapterSeamOnly / AutomatedTestsPassed / SceneBindingDeferred`
- 승인 근거: 2026-09-14 사용자 요청 “기존에 있는 것은 내버려 두고 리팩토링할 만한 것은 리팩토링한다.”
- 선행 판본: [사가정 참조형 공통 모듈 r17](station-module-standardization.implementation.r17.md)
- 재사용 WI: `WI-CITY-SYNTHETIC-MOVE`
- 재사용 Goal: `interaction-goal:synthetic-delivery-move.v1`

## 목표

기존 사가정 디오라마의 Mesh·카메라·건물 선택·차로·신호·차량·기사·음식배달 경로 계산을 다시 만들지 않는다. 이미 검증된 Mobility 표현 가운데 조립부가 명시적으로 고른 하나를 얇은 공통 Adapter 뒤에 두고, `ActiveStationDioramaHost`가 `Mobility` 상태와 표시 수명으로 다룰 이음부를 마련한다.

이 절편은 새 교통 기능이나 실제 World 결속이 아니라 구조 리팩터링이다. 기존 상태 사본 Interpreter와 Playback이 자료 검증·위치 계산을 계속 소유하고, Adapter는 읽기 전용 준비도·안정 판본·진단과 표시 여부만 전달한다.

## 구현 범위

1. 후속 단일 모듈 Adapter의 상태 생성 규칙을 `StationDioramaModuleLayerRuntimeStateFactory`로 모았다. 기존 사가정 View의 contribution 구현은 그대로 뒀다.
2. 한 역의 한 모듈을 Host에 등록·해제하고 기존 `Behaviour.enabled` 값을 보존하는 공통 `StationDioramaSingleLayerAdapter`를 추가했다.
3. `사가정MobilityModuleAdapter`는 기존 `사가정저밀도교통PlaybackLayer` 또는 `음식배달JourneyPlaybackLayer` 하나를 선택적으로 감싼다. 둘을 동시에 받으면 같은 배달 기사·경로가 겹칠 수 있으므로 `StationDioramaModuleSourceAmbiguous`로 거부한다.
4. Host는 등록 전 Adapter를 숨기고 전체 contribution을 사전 검증한다. 중복 layer가 거부되면 등록 목록·활성 plan·hash를 바꾸지 않으며 해제 시 해당 Adapter를 즉시 숨긴다.
5. 비활성 저밀도 Playback에 상태 사본을 적용할 때 새 정적·동적 Root가 먼저 보이지 않도록 `Apply` 끝에서 기존 `isActiveAndEnabled` 상태를 다시 적용했다. 차로·신호·경로·배우 계산은 바꾸지 않았다.
6. 기존 `사가정운영디오라마View`의 네 contribution과 공개 멤버·Mesh·카메라 구현은 수정하지 않았다.

## 상태·판본 규칙

| 입력 상태 | `Mobility` 상태 | 진단 |
| --- | --- | --- |
| 선택한 Playback 참조 없음 | `NotProvided` | `WaitingForMobilityPlayback` |
| Playback은 있으나 적용된 상태 사본·활성 경로가 없음 | `Blocked` | 해당 source의 대기 진단 |
| 준비됐지만 source 판본이 비어 있음 | `Blocked` | `StationDioramaModuleSourceRevisionMissing` |
| 저밀도 교통의 정적 도형과 적용 revision이 준비됨 | `PrivateReview` | `SagajeongLowDensityTrafficReady` |
| 음식배달 경로가 하나 이상 활성화됨 | `PrivateReview` | `SagajeongFoodDeliveryJourneyReady` |
| 두 Playback을 동시에 지정함 | 등록 거부 | `StationDioramaModuleSourceAmbiguous` |

- Adapter revision은 `station-diorama-module:sagajeong-mobility-adapter.r1`로 고정한다.
- 저밀도 source revision은 매 Tick의 `AppliedRevision`이 아니라 기존 이동 그래프·차로·신호·교통 프로필·배달 표시 경로 판본의 안정 조합을 사용한다. 같은 구성에서 Tick만 바뀌면 공통 조립 hash가 흔들리지 않는다.
- 음식배달 source revision `food-delivery-journey-playback.v1`은 Playback 호환 판본이다. 실제 Graph revision/hash를 대신하지 않으며 그 검증은 기존 Interpreter가 계속 담당한다.
- 기존 Playback에 새 상태 사본을 적용한 실제 조립부는 적용 완료 직후 `RefreshModuleState()`를 호출해야 한다. 이 호출이 Host 준비도와 표시 여부를 다시 계산한다.
- `PrivateReview`는 관찰 가능 상태일 뿐 통행 승인이나 gameplay 준비가 아니다. `TraversalReady=false`, `GameplayReady=false`, `DistributionApproved=false`를 유지한다.

## 기존 결과 보존

- `사가정저밀도교통PlaybackLayer`, `음식배달JourneyPlaybackLayer`의 공개 입력 계약·보간·pool·정지선·신호·경로 계산을 바꾸지 않았다.
- Adapter 숨김은 기존 Playback을 삭제하거나 `ClearAll`하지 않는다. 숨기기 직전의 `enabled` 값을 기억하고 다시 보일 때 그대로 복원한다.
- Adapter 등록 전부터 꺼져 있던 Playback은 Host 활성화만으로 켜지지 않는다.
- 기존 사가정 View의 MeshBuilder, Renderer·Material·Collider 수, 카메라 상수와 선택 카드는 바꾸지 않았다.
- 기존 면목·용마산 자료와 View를 등록하거나 대체하지 않았다.
- r17 조립 hash v1에는 호환성을 위해 새 필드를 추가하지 않았다. `LayerKindCode`를 hash에 넣는 변경은 schema v2 후보로 남긴다.

## 검증 결과

메인 쓰기 파일과 SHA-256이 같은 `C:/Users/user/ssalddel-building-h-validation` 격리 사본에서 Unity 6000.5.6f1 EditMode 시험을 실행했다.

| 시험 묶음 | 결과 |
| --- | ---: |
| `역세권디오라마MobilityAdapterTests` | 10/10 |
| `역세권디오라마Module표준Tests` | 11/11 |
| `사가정저밀도교통Tests` | 16/16 |
| `음식배달JourneyMobilityTests` | 27/27 |
| `사가정공간표현OverlayTests` | 54/54 |
| `사가정H공간선택Tests` | 10/10 |
| `사가정모형표현Tests` | 4/4 |
| `사가정운영디오라마Tests` | 7/7 |
| 합계 | **139/139** |

집중 시험은 빈 source, 준비 전 상태, 판본 누락 격리, 단일 contribution, 반복 설정 hash, 다중 source 거부, Host 중복 등록 rollback, 등록·해제·복원, 실제 저밀도 `Blocked → Apply → Refresh → PrivateReview`, Tick 변경 시 조립 hash 안정성을 포함한다. 저밀도 회귀는 비활성 상태에서 `Apply`해도 정적 차로·신호와 동적 배우 Root가 새어 나오지 않는 경우를 포함한다.

결과 XML과 로그는 `C:/Users/user/ssalddel-building-h-validation/artifacts/local/validation/station-diorama-module-r18/`에 있다.

## 제외와 증거 상한

- 차로·신호·보행·차량 알고리즘 변경과 새 교통 자료 수집
- 실제 통행 승인, `TraversalReady`·gameplay·E 승격
- `LifeSimulation`, `BusinessOverlay`, `EnvironmentPresentation` Adapter 구현
- 새 역 활성화, 새 Scene·Prefab·GameObject 배치
- 서버·DB·API schema, 주문·배차·재고·광고·WI·WorldRevision 변경
- Hosted·Save/Replay·Steam 배포·commit·push

새 Adapter는 production bootstrap이나 canonical `SimulationWorldShell` 저장 Scene에 결속하지 않았다. Scene·Prefab·Mesh·카메라·화면 출력이 바뀌지 않았으므로 이번 절편은 EditMode 구조·수명 검증까지만 수행했고 Play Mode·Game View는 재실행하지 않았다. 따라서 결과는 “공통 이음부 준비”이지 “실제 World에서 Mobility 모듈 작동 완료”가 아니다.

## 다음 재개점

저장 Scene을 바꾸지 않는 검증 전용 조립부에서 저밀도 교통 하나만 Adapter에 결속하고, 상태 사본 적용 순서·중복 Renderer 부재·해제 복원과 기존 전체/부분 Game View 출력 불변을 확인한다. 이 검증 뒤에만 canonical Scene 결속 여부를 별도 승인받는다.
