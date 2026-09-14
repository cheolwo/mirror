[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r17]

# 사가정 참조형 역세권 디오라마 공통 모듈 구현 명세

- 상태: `Approved / SagajeongReferenceOnly / Implemented / AutomatedTestsPassed / GameViewNotRequiredForUnchangedOutput`
- 승인 근거: 2026-09-14 사용자 요청 “사가정역을 중심으로 공통단을 만들어 다른 역 디오라마의 부담을 줄이는 표준화 작업을 진행한다. 면목역·용마산역을 지금 새로 구현하는 범위는 아니다.”
- 상위 기획: [역세권 디오라마 모듈 표준](README.md)

## 목표와 범위

사가정역 1km 디오라마의 기존 공간 모형·카메라·건물 선택·읽기 전용 후속 보기를 훼손하지 않고, 한 역의 여러 표현 기능을 공통 Host가 판본화된 모듈 Profile과 준비도 결과로 조립할 수 있게 한다.

이번 절편이 구현하는 것은 다음 네 가지다.

1. 역세권 공통 모듈 종류·의존성·필수 여부를 Unity Runtime의 엔진 비의존 계약으로 정의한다.
2. 같은 입력 상태에서 같은 준비도·진단·조립 hash를 만드는 공통 Planner를 둔다.
3. canonical `SimulationWorldShell` 안에서 활성 역 하나의 Adapter만 표시하는 `ActiveStationDioramaHost`를 추가한다.
4. 기존 `사가정운영디오라마View`를 첫 호환 Adapter로 연결한다.

면목역·용마산역의 새 자료, 새 View, 새 Scene, 새 GameObject 배치, 새 카메라와 활성 Profile은 만들지 않는다. 이미 존재하는 비공개 검토 자료와 공통 공간 표현 코드는 삭제하거나 이름을 바꾸지 않는다.

## 권위와 상태 흐름

```text
서버·Simulation 상태 사본 또는 동결 공간 자료
  → 역별 Adapter의 읽기 전용 상태 설명
  → StationDioramaModulePlanner
  → 필수 모듈 준비도·의존성·조립 hash
  → ActiveStationDioramaHost
  → 기존 사가정 View 표시/숨김
```

- 서버와 Simulation이 업무·게임 상태의 최종 권위다.
- Host와 Planner는 주문·배차·재고·광고·WI·WorldRevision을 변경하지 않는다.
- `PrivateReview`는 관찰 표현 가능 상태일 뿐 공개·배포·통행·gameplay 준비 완료가 아니다.
- 등록되지 않은 역을 사가정 자료로 대체하지 않는다.

## 공통 모듈 Profile

| 순서 | 모듈 종류 | 필수 | 선행 모듈 | 이번 사가정 Adapter |
| --- | --- | --- | --- | --- |
| 10 | `DataEvidence` | 예 | 없음 | 지도·높이·공간 계보를 `PrivateReview`로 제공 |
| 20 | `BaseSpatialPresentation` | 예 | `DataEvidence` | 기존 4×4 Mesh·카메라·조명·색보정 제공 |
| 30 | `SpatialMeaning` | 아니오 | `DataEvidence` | 현재 건물 H 후보 선택을 `PrivateReview`로 제공 |
| 40 | `Mobility` | 아니오 | `DataEvidence` | 이번 Host 결속에서는 미제공 |
| 50 | `Interaction` | 아니오 | `SpatialMeaning` | 건물 역할 카드·읽기 전용 후속 보기를 `PrivateReview`로 제공 |
| 60 | `LifeSimulation` | 아니오 | `Mobility` | 이번 Host 결속에서는 미제공 |
| 70 | `BusinessOverlay` | 아니오 | `DataEvidence` | 이번 Host 결속에서는 미제공 |
| 80 | `EnvironmentPresentation` | 아니오 | `BaseSpatialPresentation` | 이번 Host 결속에서는 미제공 |

미제공 선택 모듈은 조용히 성공 처리하지 않고 `WaitingForModuleAdapter`로 남긴다. 필수 모듈이 빠지거나 차단되면 Host는 해당 역을 표시하지 않는다.

## 출력 보존 조건

- `SagajeongReference` r3와 공간 Overlay revision/hash를 바꾸지 않는다.
- `사가정모형MeshBuilder`의 vertex·triangle·chunk·renderer·material 생성 경로를 바꾸지 않는다.
- 기존 카메라 pitch/yaw, 전체 보기 framing, URP 색보정과 관찰 layer 번호를 바꾸지 않는다.
- 기존 건물 602개 선택, 역할 카드와 음식배달 읽기 전용 경로 선택 동작을 바꾸지 않는다.
- Host는 Adapter의 표시 수명만 조율하고 개별 자료를 직접 읽거나 Mesh를 생성하지 않는다.

## 검증 계획

- 공통 Profile이 중복 모듈, 존재하지 않는 의존성, 역순 의존성과 필수 기반 누락을 거부하는지 확인한다.
- 입력 순서가 달라도 같은 조립 hash와 같은 준비도 결과가 생성되는지 확인한다.
- 필수 모듈이 준비된 사가정 Adapter만 활성화되고 선택 모듈 결손은 진단으로 남는지 확인한다.
- 필수 모듈 결손, 등록되지 않은 역, 교차 역 Adapter 대체를 거부하는지 확인한다.
- 기존 사가정 Mesh·공간·운영·H 선택 회귀 시험을 함께 실행한다.
- 이번 절편은 Scene·시각 결과를 변경하지 않으므로 Play Mode·Game View 캡처는 완료 조건에 넣지 않는다. 실제 역 전환 UI나 새 역 Adapter가 생길 때 별도 검증한다.

## 완료 상한

`공통 계약·Planner·Host + 사가정 호환 Adapter + EditMode 회귀`까지다. 서버 API schema 변경, Hosted 연결, Save/Replay, Scene 저장, 면목역·용마산역 활성화, Steam 배포, E 승격은 포함하지 않는다.

## 구현·검증 결과

- Unity Runtime에 엔진 비의존 모듈 Profile·Planner·준비도·결정적 조립 hash를 추가했다.
- Unity Presentation에 `ActiveStationDioramaHost`와 Adapter 계약을 추가하고 기존 `사가정운영디오라마View`를 첫 Adapter로 연결했다.
- 활성 모듈 Catalog에는 사가정만 등록했다. 기존 면목·용마산 자료 Profile은 보존했지만 새 Host의 활성 Profile로 올리지 않았다.
- 메인 Unity 작업 파일과 SHA-256이 같은 격리 사본에서 공통 모듈 시험 9/9, 기존 사가정 공간 Overlay 54/54, H 선택 10/10, 모형 4/4, 운영 View 7/7, 합계 84/84를 통과했다. 위치 독립 Profile factory가 새 역 자료·View·Catalog 등록 없이 같은 8모듈 구조를 만드는 시험을 포함한다.
- 넓은 `사가정*` 필터에서는 위 75건이 통과하고 기존 Synty 대장 관련 5건이 실패했다. 관련 소스·카탈로그 asset은 메인과 일치하지만 격리 사본에는 외부 공급사 `Assets/Synty` 팩이 없어서 카탈로그의 Prefab GUID 4개를 해소하지 못하고 `prefab == null` 검증에서 `LegalDongScenicCatalogInvalid`가 재현됐다. 메인 자산 트리에서는 네 GUID를 모두 확인했으며, 이 격리 환경 결손은 이번 표준화 쓰기 경로와 분리했다.
- Scene·Prefab·MeshBuilder·카메라 수치·자료 asset은 수정하지 않았다. Play Mode·Game View는 새 시각 출력이 없는 이번 절편에서 다시 실행하지 않았다.
