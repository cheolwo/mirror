# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r38]

## 확정

- canonical `SimulationWorldShell`이 열린 편집 모드에서도 지구본을 기본 미리보기로 표시한다.
- 편집 미리보기는 저장되지 않는 임시 표현이며 런타임 표현 상태, 운영 원장, Simulation 상태를 변경하지 않는다.
- 대한민국 선택 뒤 한반도 확대 안정 관문을 통과하면 한반도 교류축 자료층을 표시한다. 상세 자료층이 열릴 때 국가 정보 카드는 접고, 축소하면 다시 복원한다.

## 구현

- 별도 Unity 저장소의 `세계지구본View`를 `ExecuteAlways` 미리보기로 확장했다.
- 동적으로 생성한 지구본·카메라·Canvas·교류축 표현에 `DontSaveInEditor`를 적용해 저장 Scene의 정본 객체로 굳지 않게 했다.
- Play Mode에서만 단일 플레이어 관찰 환경 등록, 다른 화면 Canvas 일시 중지와 런타임 표현 상태 갱신을 수행한다.
- 편집 모드에서는 입력·자동 자전을 실행하지 않고 정적인 한반도 중심 기본 구도만 보여 준다.
- 한반도 교류축을 열 때 중복 국가 카드를 접고, 교류축을 닫을 때 선택 국가 카드를 복원한다.

## 검증

- Unity 재컴파일: 오류 없음.
- `Ssalddel.Unity.Tests.EditMode.세계지구본Tests`: 16/16 통과.
- 편집 모드 Game View에서 Play 버튼이 꺼진 상태로 한반도 중심 지구본이 표시됨을 확인했다.
- Play Mode 확대 결과: 대한민국 선택, 확대 안정 관문 적용, 교류축 표시, 국가 카드 접힘, 거점 8개, 구간 7개, 도라산 선택, 최대 관찰 단계 Z4를 확인했다.
- 화면 증거:
  - `C:/Users/user/ssalddel/Documentation/Changes/2026-09-21-korean-peninsula-hero-pose/game-view-edit-mode-preview.png`
  - `C:/Users/user/ssalddel/Documentation/Changes/2026-09-21-korean-peninsula-hero-pose/game-view-peninsula-zoom.png`
- 공식 Scene의 기존 서버 세션 부재·`SimulationReplayHashMismatch` 등 Console 오류 8건은 남아 있다. 이번 표현 변경으로 Console 0이나 통합 E5 이상을 주장하지 않는다.

## 제외·남은 일

- 현행 확대 자료는 서부 교류축의 연구·가상 시나리오 상태 사본이다. 한반도 지형, 하천, 실제 철도 운행과 서버 확대 자료 투영은 아직 연결되지 않았다.
- 새 Scene·서버 API·운영 상태는 만들지 않았다. commit·push는 수행하지 않았다.
- 새 디오라마 규칙 후보 없음.

