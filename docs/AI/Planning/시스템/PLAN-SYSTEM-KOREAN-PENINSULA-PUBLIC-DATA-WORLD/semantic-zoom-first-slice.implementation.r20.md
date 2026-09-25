# [개발 인계 · 월드·공간·관찰 · 한반도 의미 확대 첫 수직 슬라이스 · r20]

## 승인 기준

- 승인 기획: `korean-peninsula-semantic-zoom-information-layers.decision.r19.md`
- 승인 SHA-256: `95662B68FF620152B1B2477AEDA6B4623D000AC3EE384296CB112766F447315D`
- 사용자 승인: 2026-09-21, 작은 수직 슬라이스부터 개발 진행
- 상태: `Approved / ReadyToDispatch`

## 목표

기존 `SimulationWorldShell/WorldMapRoot/GlobeRoot`의 지구본에서 대한민국을 선택하고 가까이 확대하면 한반도 서부 교류축을 읽기 전용으로 열어 도라산 정보 카드를 선택할 수 있게 한다. 다시 축소하거나 지구본으로 돌아갈 수 있어야 한다.

## 허용 쓰기 경로

- `Hongdal/docs/AI/Planning/시스템/PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/**`
- `Hongdal/docs/AI/PLANNING.md`
- `Hongdal/docs/AI/CURRENT_WORK.md`
- `ssalddel/Assets/Ssalddel/Runtime/WorldMap/한반도교류축CatalogModels.cs*`
- `ssalddel/Assets/Ssalddel/Presentation/WorldMap/한반도교류축View.cs*`
- `ssalddel/Assets/Ssalddel/Presentation/WorldMap/세계지구본View.cs`
- `ssalddel/Assets/Ssalddel/Resources/KoreanPeninsulaCorridorCatalog.json*`
- `ssalddel/Assets/Ssalddel/Tests/EditMode/세계지구본Tests.cs`

기존 Scene, Builder, 국가 카탈로그와 사가정 디오라마는 수정하지 않는다.

## 첫 수직 흐름

```text
대한민국 선택
  → 카메라 거리 39 이하
    → 한반도 서부 교류축 표시
      → 도라산 선택
        → 현재 국내 관찰 상태·최대 Z4·출처·제한 카드
          → 거리 44 이상으로 축소
            → 교류축 닫기·지구본 유지
```

단계 진입 39와 이탈 44를 분리해 경계 떨림을 막는다. 이 수치는 첫 코드 검증용이며 실제 Game View 판독 뒤 후속 판본에서 조정할 수 있다.

## 자료 경계

- 이번 Unity 상태 사본은 r18 공식 출처 대장을 요약한 동결된 표현 자료다.
- 정밀 좌표·건물·출입구·현재 통행·실제 운행 애니메이션을 포함하지 않는다.
- 판문과 봉동은 별도 조사 대상으로 유지한다.
- 부산–서울과 평양–신의주 세부 구간은 `ResearchPending` 또는 `InternationalCorridorDescription`으로만 표시한다.
- Unity 선택과 확대는 운영·Simulation 상태를 변경하지 않는다.

## 검증 상한

- 카탈로그 schema·출처·거점·구간·상태·관계 검토 검증
- 대한민국 선택과 확대 진입·축소 이탈의 EditMode 시험
- 도라산 카드 선택과 상태·출처·최대 관찰 단계 검증
- 기존 국가 선택·사가정 상세 전환·복귀 시험 회귀
- 저장 Scene을 수정하지 않으므로 이번 절편의 상한은 코드·EditMode 조립 검증이다.
- Play Mode·Game View와 실제 DB/API 결속은 별도 후속 검증이다.

## 제외

- MongoDB/RDB 실제 적재와 서버 읽기 API
- Scene·Prefab·카메라 수치 저장 변경
- 북측 상세 지형·역세권 디오라마
- 부산–신의주 실제 운행 가능 노선 확정
- E 단계 자동 승격, commit, push

## 구현 결과

- `세계지구본View`의 대한민국 선택 상태에 의미 확대 진입 거리 `39`, 이탈 거리 `44`를 결속했다. 경계 안팎을 오갈 때 한반도 교류축 화면이 반복 점멸하지 않도록 이력 구간을 둔다.
- `한반도교류축CatalogModels`와 동결 자원 `KoreanPeninsulaCorridorCatalog.json`을 추가했다. 출처 5건, 거점 8개, 구간 7개를 검증하며 관찰 전용·운영 권위 없음·정밀 형상 없음 경계를 강제한다.
- `한반도교류축View`를 읽기 전용 화면으로 추가했다. 부산·서울·도라산·판문·봉동·개성·평양·신의주를 상대적 순서로 선택하고, 자료 상태·연결 상태·최대 관찰 단계·제한·출처 수를 확인할 수 있다.
- 도라산은 `현재 국내 운행 / 최대 Z4`로, 판문–봉동 관계는 `AwaitingIdentityReview`로 표시한다. 현재 통행·정밀 노선·실제 업무 명령은 만들지 않는다.
- 기존 `SimulationWorldShell` Scene과 Builder는 수정하지 않았다. 기존 Scene의 `세계지구본View`가 런타임에 화면을 조립하는 구조를 재사용했다.

## 검증 결과

- Unity 6000.5.6f1 연결 Editor에서 재컴파일을 수행했다.
- `Ssalddel.Unity.Tests.EditMode.세계지구본Tests`: `13/13` 통과했다.
- 신규 검증은 카탈로그 출처·거점·구간 계약과 `대한민국 선택 → 거리 39 이하 → 교류축 → 도라산 카드 → 거리 44 이상 → 지구본 복귀`를 포함한다.
- 기존 국가 판독, 구면 좌표 왕복, 국가 상세 복귀, 공식 Scene의 지구본 단일성도 함께 회귀 통과했다.
- 실제 Play Mode·Game View, DB/API 결속, 서버 상태 사본 수신은 수행하지 않았다. 따라서 이번 결과는 코드·EditMode 조립 검증 상한이며 E 단계는 승격하지 않는다.
- commit·push는 수행하지 않았다. 새 디오라마 규칙 후보 없음.
