# 사가정 건물 역할 카드·읽기 전용 후속 보기 구현 명세 r16

- 상위 기획: `PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES` r16
- 선행 구현: `PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC` 건물 H 선택 r6
- 승인 근거: 2026-09-14 사용자 확정. 음식점·창고·주거·일반 역할을 건물과 분리하고 한 건물에 여러 역할을 결속할 수 있게 하며, 역할별 카드와 명시 결속된 읽기 전용 후속 흐름을 연다.
- 상태: `Approved / Implemented / AutomatedTestsPassed / GameViewNotReverified`

## 플레이어 약속

- 지금: 현실 공간 윤곽을 보존한 사가정 디오라마에서 건물을 선택한다.
- 여기: 동결 r3 건물 하나와 그 건물에 명시적으로 결속된 합성 의미 위치다.
- 나: 지역 생활의 공간·업무 관계를 살펴보는 관찰자다.
- 너: 건물 자체가 아니라 그 안에서 수행 가능한 음식점·창고·주거 전달 역할이다.
- 이렇게: 건물을 선택하고 역할 탭을 고른 뒤 역할별 H1과 생명주기 요약을 읽고, 준비된 상태 사본이 있으면 읽기 전용 경로를 선택한다.
- 직접 결과: 건물 외형이나 서버 상태를 바꾸지 않고 건물의 복수 역할과 생활 흐름을 구분해 이해한다.
- 다음 선택: 카드 닫기, 건물 확대, 음식배달 경로 추적 또는 다른 역할 보기다.

## 역할과 H 계층의 분리

- `건물 → 역할 Profile 여러 개 → 역할별 H1 여러 개 → H2 → H3 → H4/AreaSet` 순서로 관리한다.
- 음식점·창고·주거는 건물 종류나 H 계층 단계가 아니다. 역할 Profile은 같은 건물에 복수로 결속할 수 있고 H1은 주문 포장, 수령, 입고·보관, 출고 준비 같은 최소 행동 공간을 유지한다.
- 첫 역할 코드는 `Restaurant`, `Warehouse`, `ResidentialDelivery`, `GeneralUnbound` 네 개다.
- 실제 상호·입점·거주·업무 사실을 추정하지 않는다. `SyntheticFixture`, `SourceGeometryOnly`, 공개 사업장 사실을 분리하고 카드에 실제 업체 사실이 아님을 표시한다.

## 첫 명시 결속

| 건물 | 의미 위치 | 역할 | 역할별 H1 | 후속 보기 |
| --- | --- | --- | --- | --- |
| `osm:way:1256772531` | `synthetic-place:food-route-a` | 음식점 | `h1-stock:town-order-packing` | 준비된 `route:synthetic:sagajeong-food-delivery:a` 선택, 없으면 상태 사본 대기와 단계 요약 |
| `osm:way:1256772606` | `synthetic-place:food-route-b` | 주거·전달 | `h1-stock:town-resident-pickup` | 같은 음식배달 경로의 도착·수령 관점, 없으면 상태 사본 대기 |
| `osm:way:470492219` | `synthetic-place:warehouse-a` | 창고 | `h1-stock:hub-receiving-storage`, `h1-stock:hub-outbound-staging` | 합성 창고 생명주기 미리보기만 제공 |

세 의미 위치는 각각 r3 건물 윤곽 하나에만 포함되는지 확인한다. 이 포함은 실제 입점·출입구·통행 가능성의 증거가 아니다. 나머지 599개 건물은 `GeneralUnbound / WaitingForGraphMapBinding`으로 유지한다.

## 구현 경계

- 기존 Mesh·건물 높이·색·카메라·조명·도로·Collider 0개·Scene hash를 바꾸지 않는다.
- 역할 탭과 카드 텍스트만 추가하고 건물별 GameObject·Renderer·Material을 만들지 않는다.
- 후속 조작은 `음식배달JourneyPlaybackLayer.SetSelectedRoute`의 읽기 전용 경로 선택 또는 합성 단계 요약만 허용한다. 주문 수락·배차·결제·창고 확정·H 편집·ActionRecord·WorldRevision 변경은 금지한다.
- 실제 경로 상태 사본이 없으면 합성 fallback을 실행하지 않고 `WaitingForReadOnlyRouteSnapshot`을 표시한다.
- 창고는 기존 일반화 생명주기 순서를 보여 주되 실제 창고 경로나 재고 상태로 해석하지 않는다.
- `SimulationWorldShell` Scene·Prefab·서버 계약·Graph Map·H 생성 대장은 수정하지 않는다.

## 검증 계획

- 세 의미 위치가 예상 건물 하나에만 포함되고 역할 코드·의미 위치·H1 배열이 결정적인지 시험한다.
- 일반 건물이 역할이나 H1을 주소·건물 종류로 추정하지 않는지 시험한다.
- 음식배달 경로가 있을 때만 선택하고 없을 때 대기 상태를 유지하며, 모든 후속 결과가 `ReadOnly=true / ChangesAuthorityState=false`인지 시험한다.
- 역할 선택·후속 보기·전체 보기·닫기 생명주기에서 기존 Renderer·Material·Collider 수가 변하지 않는지 회귀한다.
- 기존 건물 선택·사가정 모형·운영·공간 표현 시험을 함께 실행한다.
- 실제 Game View가 필요하면 격리 Unity에서 역할 탭·후속 상태와 기존 디오라마 외형을 확인하되 Scene은 저장하지 않는다.

## 완료 상한

- 코드와 자동 시험이 통과하면 `Implemented / AutomatedTestsPassed`까지만 인정한다.
- 실제 Game View를 다시 확인하지 않으면 r6의 기존 캡처를 r16 역할 카드 증거로 재사용하지 않는다.
- 서버 HTTP·실제 업체·실제 창고·운영 주문·Graph Map/H 등록·Scene 영속 결속·Actual E5·GameplayReady는 계속 미완료다.
