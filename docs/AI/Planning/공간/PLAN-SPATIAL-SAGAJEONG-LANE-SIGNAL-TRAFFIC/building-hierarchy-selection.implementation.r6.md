# 사가정 건물 H 계층 선택 구현 명세 r6

- 상위 기획: `PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC` r6, `PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES` r15
- 승인 근거: 2026-09-14 사용자 확정. 기존 사가정 디오라마의 현실 공간 표현을 훼손하지 않고 건물 클릭으로 H 계층 정보와 후속 상호작용 진입점을 관리할 수 있게 한다.
- 성격: 기존 WI를 설명하는 읽기 전용 보조 표현 절편. 새 WI·Goal·ActionRecord·WorldRevision을 만들지 않으며 Evidence 단계를 자동 승격하지 않는다.
- 구현 상태: `Implemented / AutomatedTestsPassed / CanonicalPlayModeGameViewVerified / DioramaVisualRegressionPassed / UserMouseUpInjectionDeferred`

## 플레이어 약속

- 지금: 사가정역 디오라마를 전체 또는 근접 관찰한다.
- 여기: `SagajeongReference.json` r3에 동결된 건물 602개 중 한 건물 외곽이다.
- 나: 공간의 생활 의미를 살펴보는 관찰자다.
- 너: 원본 건물과 명시적으로 결속된 H 후보·부분 AreaSet 정보 카드다.
- 이렇게: 기존 UI가 아닌 화면 영역에서 건물을 왼쪽 클릭하고, 원자료와 후보 계층을 읽고, 닫기·전체 보기로 돌아간다.
- 직접 결과: 디오라마 외형과 Simulation 상태를 바꾸지 않고 선택 건물의 공간 근거와 결속 상태를 판독한다.
- 다음 선택: 카드 닫기 또는 후속 승인 뒤 결속된 주문·기사 흐름 읽기 전용 추적이다.

## 입력과 결속

- 지도 판본은 `sagajeong-reference.r3`, SHA-256은 `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`으로 고정한다.
- 건물 원자료는 기존 `참고건물`의 `id`, `address`, `kind`, 높이와 `points`를 읽는다. MeshBuilder가 검증된 높이·공간 보완으로 해당 r3 건물을 대체하면 클릭도 같은 표현 높이·외곽을 쓴다.
- `osm:way:1256772531`과 `synthetic-place:food-route-a`, `osm:way:1256772606`과 `synthetic-place:food-route-b` 두 후보만 명시적으로 결속한다.
- H 후보는 기존 `h1-stock:*`, `h2-candidate:*`, `h3-candidate:*` 참조를 읽을 뿐 H 대장이나 Graph Map을 수정하지 않는다.
- 그 밖의 건물은 `WaitingForGraphMapBinding`이다. 주소·종류·근접 거리로 실제 H·입주·출입구·업체를 추론하지 않는다.

## 구현 경계

- 기존 `사가정운영디오라마View`의 Mesh·카메라·광원·Volume·색·표현 예산을 바꾸지 않는다.
- 건물별 GameObject·Collider·Renderer·Material을 만들지 않는다. CPU 건물 외곽 색인과 ray/다각형 판정만 추가한다.
- 선택 표시는 기존 Game View의 우측 상세 패널과 화면 투영 표식만 사용한다. 상시 외곽선·건물 착색·반투명 덮개를 추가하지 않는다.
- 기존 도로명·주소·업무 표식·교통·다중 OS Overlay와 패널을 동시에 겹치지 않는다. 다른 상세 대상을 고르면 이전 선택을 해제한다.
- `GameOverlayVisible` 검증 화면에서는 새 카드와 표식을 그리지 않는다.
- IMGUI `hotControl`, 역 방어 버튼 영역, uGUI `EventSystem` 점유 중에는 화면 클릭을 건물 선택으로 소비하지 않는다.
- `SimulationWorldShell` Scene·Prefab·MeshBuilder·서버 계약·H 생성물·Graph Map은 수정하지 않는다.
- Sky·날씨·시간대, NPC·차량·보행 이동, 실제 주문/배차/결제, H 편집은 제외한다.

## 검증

- 자동 시험: 지도 판본/hash 거절, 내부·외부·경계 선택, 중첩 결정성, 두 명시 결속과 미결속 대기, 선택 해제 생명주기를 확인한다.
- 구조 회귀: Collider 0, Renderer·Material·chunk·vertex·triangle과 지도 조립 hash가 기존 기준과 같아야 한다.
- 실제 화면: canonical `SimulationWorldShell` Play Mode에서 전체 보기, 건물 클릭 카드, 근접 보기를 확인하고 기존 카메라 휠·오른쪽 드래그와 다른 Overlay 선택을 회귀한다.
- 시각 비교: 동일 해상도·카메라의 선택 전/후 전체 화면과 선택 건물 근접 화면을 남긴다. 화면 증거는 통행·gameplay·서버 연결·Actual E5를 뜻하지 않는다.
- Scene은 저장하지 않고 실행 전후 SHA-256 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`와 dirty 여부를 비교한다.

### 2026-09-14 검증 결과

- 격리 Unity EditMode에서 선택 8건과 기존 사가정 모형·운영·공간 회귀 65건, 총 73/73이 통과했다.
- canonical `SimulationWorldShell` 그래픽 Play Mode에서 1399×628 선택 전 전체·H 후보 선택·건물 근접 Game View 3장을 새 출력 폴더에 남겼다.
- 선택 전후 Renderer 39, Material 5, MeshFilter 39, Collider 0이었고 Scene SHA-256과 dirty=false를 유지했다. 선택 대상은 `SpatialPresentationOverlay` 외곽과 정렬됐다.
- 검증 Driver는 화면 좌표→ray 호출을 검증했지만 사용자 MouseUp 입력을 주입하지는 않았다. `LocalPrivateReview / publicReleaseEvidence=false`이며 실제 H·Graph Map·원장 상태 비교·서버 HTTP·Scene 저장 증거가 아니다.
- 기존 Scene의 누락 Prefab GUID 56개, Unknown script 10건, replay hash·localhost 연결·JobTempAlloc 경고는 재현됐고 별도 문제로 남겼다.

## 완료 상한

- 코드·EditMode 통과만으로 `Implemented / AutomatedTestsPassed`까지 보고한다.
- 실제 Play Mode·Game View와 Scene 불변을 확인한 경우에만 `CanonicalPlayModeGameViewVerified / DioramaVisualRegressionPassed`를 추가한다.
- 서버 HTTP, 실제 Graph Map/H 등록, Scene 저장, 통행 권위, gameplay-ready, Actual E5는 별도 근거 없이는 계속 false다.
