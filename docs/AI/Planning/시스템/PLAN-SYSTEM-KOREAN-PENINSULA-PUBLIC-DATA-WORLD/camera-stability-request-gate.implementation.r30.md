# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r30]

## 개발 묶음

기획 세 건을 첫 `기획 3회 → 작은 개발 1회` 묶음으로 수용했다.

- `spatial-semantic-zoom-object-card.decision.r25.md` — SHA-256 `7541057F4D0F2655E25C698DA86DF8326DFE74587AC1845E5CE96D58CAC75026`
- `zoom-driven-spatial-streaming.decision.r27.md` — SHA-256 `E46024F6F0405F007A68798A87196EB67D21797A1F8C8DEA3D9636C9C1B87E58`
- `camera-stability-request-gate.decision.r28.md` — SHA-256 `EFA8CC5B875D07EC0CFB70A0C74BF9E5CDD4AE82F8208E3E9CB50CDD0BEC78A5`

이번 개발은 위 계약 전체가 아니라, 후속 자료 적재가 의존하는 0.3초 카메라 안정 요청 관문만 구현했다.

## 구현

별도 Unity 저장소 `C:\Users\user\ssalddel`에서 다음 파일을 변경했다.

- `Assets/Ssalddel/Runtime/WorldMap/공간관찰요청안정관문.cs`와 `.meta`
  - 마지막 입력 뒤 0.3초가 지난 최종 요청만 꺼낸다.
  - 새 입력은 앞선 대기를 대체하고 요청 세대를 증가시킨다.
- `Assets/Ssalddel/Presentation/WorldMap/세계지구본View.cs`
  - 카메라 거리 변화 시 한반도 표시·숨김을 즉시 바꾸지 않고 안정 관문에 예약한다.
  - 대기 중 작은 `자료 불러오는 중` 상태를 표시한다.
  - 명시적인 한반도 관찰 버튼 동작과 기존 진입·이탈 거리 히스테리시스는 유지한다.
- `Assets/Ssalddel/Tests/EditMode/세계지구본Tests.cs`
  - 0.299초에는 적용하지 않고 0.3초에 적용하는 경계와 최종 요청 대체를 검증한다.

Scene, Builder, 서버 API, 공공자료 수집, SRTM·HydroRIVERS 투영은 변경하지 않았다. Unity는 계속 읽기 전용 표현 계층이며 이 관문은 Simulation이나 운영 권위 상태를 바꾸지 않는다.

## 검증

- Unity 재컴파일: 완료, 컴파일 오류 0.
- Unity EditMode 집중 시험 `Ssalddel.Unity.Tests.EditMode.세계지구본Tests`: 13/13 통과.
- 명령줄 Unity 시험은 같은 프로젝트가 Editor에서 열려 있어 실행하지 못했고, 열린 Editor의 Unity Pipeline 시험으로 대체했다.
- canonical `SimulationWorldShell`을 실제 Play Mode로 실행해 Game View의 지구본·국가 경계·관찰 안내 렌더를 확인했고, 화면 증거를 `artifacts/local/unity-captures/2026-09-21-world-globe-playmode.png`에 남겼다.
- 작은 확대 입력 뒤 지구본 카메라가 반응하는 것은 확인했다. 0.3초 대기 표시는 캡처 도구 지연보다 짧아 정지 화면 증거로 분리하지 못했으며, 그 경계는 앞선 EditMode 시험으로만 입증한다.
- 별도 실행 중인 `OrdererApp` 창이 Unity 위를 반복해서 덮어 캡처 전에 최소화해야 했다. 이는 지구본 표현 결과가 아니라 데스크톱 검증 환경의 창 간섭이다.
- Console에는 로컬 서버 세션 부재, 운영 계획 서버 연결 실패, 비활성화 중 부모 변경 등 기존 오류가 재현됐다. 실행 시점에 오류 수가 변동했으므로 Console 0이나 완전한 통합 검증을 주장하지 않는다.

## 남은 범위

- 안정 요청을 실제 확대별 상태 사본 API와 연결
- 늦게 도착한 이전 세대 서버 응답 거절
- 실제 지형 자료의 동결 투영과 확대별 LOD
- Play Mode에서 연속 확대의 최소·최대 거리와 지구본 표시 경계를 별도로 검증하고 기존 Console 오류를 분리 복구

commit·push는 수행하지 않았다. 새 디오라마 규칙 후보 없음.
