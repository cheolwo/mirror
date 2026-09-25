# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r44]

## 목표

r42의 `지구본 → 권역 지형판` 전환이 같은 프레임에서 두 표현을 교체해 생뚱맞게 보이던 문제를 줄인다. 확대·축소가 같은 공간을 더 가까이 보거나 멀리 보는 경험으로 이어지게 한다.

## 구현

- 의미 확대 요청이 300ms 안정된 뒤 0.75초짜리 양방향 전환을 시작한다.
- 전반부에는 작은 권역판이 지구본 한반도 표면 앞에서 약 8%에서 42% 크기로 자란다.
- 중간 관문에서 지구본을 물리고, 후반부에는 권역판이 카메라 안쪽 깊이에서 원점으로 이동하며 100% 크기와 기존 7도 관찰 각도로 펴진다.
- 확대를 되돌리거나 전환 중 방향을 바꾸면 현재 진행값부터 역방향으로 이어 간다.
- 전환 중에도 세계 표현 상태는 지구본 구간과 권역 지형 구간을 중간 관문 기준으로 구분한다.
- 배경판 앞면과 지형 이미지가 같은 깊이에 놓였던 문제를 분리해 최종 판의 줄무늬 현상을 제거했다.
- 전환은 Presentation 전용이며 운영·Simulation 상태, 카메라 선택 대상, Scene 저장 구조를 변경하지 않는다.

## 검증

- Unity 재컴파일 오류 없음.
- `Ssalddel.Unity.Tests.EditMode.세계지구본Tests` 21/21 통과.
- 실제 Play Mode 자동 진입 완료: `Layer=RegionalTerrain; Stage=Coastline; Transition=1.00; Active=False`.
- 실제 Play Mode 자동 복귀 완료: `Layer=Globe; Stage=Hidden; Transition=0.00; Active=False`.
- 전환 중간 Game View: `Assets/Documentation/Changes/2026-09-21-korean-peninsula-terrain-preview/game-view-globe-to-terrain-transition-r44.png`.
- 최종 Game View: `Assets/Documentation/Changes/2026-09-21-korean-peninsula-terrain-preview/game-view-regional-terrain-transition-complete-r44.png`.
- 기존 서버 세션 부재·Replay hash 불일치·비활성화 중 부모 변경 오류 9건은 그대로 남아 있다. 이번 전환 전용 오류는 확인되지 않았으나 Console 0과 통합 E5 이상은 주장하지 않는다.

## 남은 판단

현재 방식은 지구본 표면에서 직사각형 지형판이 펼쳐지는 첫 연결안이다. 다음 시각 조정 후보는 가장자리 마스크·곡면에서 평면으로 펴지는 mesh 변형·선택 지점 정밀 정렬이지만, 현재 결과를 먼저 관찰한 뒤 구체화한다.

commit·push·Scene 저장은 수행하지 않았다. 새 디오라마 규칙 후보 없음.
