# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r36]

## 확정

- 지구본 시작 화면은 한반도를 중앙에 두고 중국을 왼쪽, 일본을 오른쪽에 배치한다.
- 시작 뒤 5초 동안 이 구도를 유지하고, 사용자 입력이 없을 때만 느린 자동 자전을 시작한다. 드래그·확대·선택 입력은 유지 타이머를 다시 시작한다.
- 제목·출처 등 보조 정보는 기본적으로 접어 둔다. 사용자가 `정보 열기`를 선택하거나 국가·공간 객체를 선택했을 때 필요한 정보만 펼친다.
- 이 동작은 Presentation 기본값이며 국가 선택, 운영 원장, Simulation 상태를 변경하지 않는다.

## 구현

- 별도 Unity 저장소의 `세계지구본View`에 `KoreanPeninsulaHeroPose`, 5초 무입력 유지, 한반도·중국·일본 문맥 표식과 접이식 정보 패널을 연결했다.
- 위경도에서 Unity 좌표로 옮기는 경도 축과 화면 선택 역변환을 함께 정렬해, 지리 선택 정확도를 유지하면서 동서 화면 방향을 바로잡았다.
- 육지 표본 mesh의 삼각형 감기 순서를 경도 축 변환에 맞춰 보정했다.
- 기존 canonical `SimulationWorldShell`을 사용했으며 새 Scene이나 운영·Simulation 권위는 만들지 않았다.

## 검증

- Unity 재컴파일: 오류 없음.
- `Ssalddel.Unity.Tests.EditMode.세계지구본Tests`: 15/15 통과.
- 실제 Play Mode Game View: 한반도 중앙, 중국 왼쪽, 일본 오른쪽, 정보 패널 기본 접힘을 확인했다.
- 실제 화면: `C:/Users/user/ssalddel/Documentation/Changes/2026-09-21-korean-peninsula-hero-pose/game-view-start.png`.
- 현행 공식 Scene의 기존 로컬 서버 세션 부재와 `SimulationReplayHashMismatch` 등 Console 오류 8건은 남아 있다. 이번 표현 변경에서 발생한 새 오류로 판정하지 않았으며 Console 0이나 통합 E5 이상을 주장하지 않는다.

## 제외·남은 일

- 제목·출처 패널의 최종 시각 디자인, 서버 확대 자료 적재, 지형 투영, Windows Player 빌드는 이번 범위에 포함하지 않았다.
- 이미지와 구현은 작업 사본이며 commit·push하지 않았다.
- 새 디오라마 규칙 후보 없음.

