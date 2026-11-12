# 2026-11-12 Claude Unity 2.5D 반환 (이어받기 묶음)

[Codex·Claude 혼용 운영안](Codex-Claude-혼용운영안-2026-11-12.md)의 방식 B(Claude 계획·뼈대 → Codex 이어받기) 양식으로 쓴 반환이다. 이 작업은 사용자가 Claude에게 직접 요청한 탐색 작업이다. 승인된 PLAN 판본이나 작업지시서에서 나온 것이 아니므로 `Approved`·`ReadyToDispatch`로 보지 않는다.

## 작업실 역할

- 수행: Claude 데스크톱 원격 세션 (PC 셸·git 없음, 파일 쓰기와 화면 조작으로 빌드·확인)
- 이어받기 대상: Codex, 사용자가 수용을 정한 뒤

## 근거

- 사용자 요청 (2026-11-12): 홍달 경영 시뮬레이션 Unity 판 고도화, 지도를 2.5D로 개편, 계획대로 실행, 빈 블록 보완. 그래픽은 후순위.
- 구조 개편 기획 문서 (Claude Docs, 저장소 밖): https://claude.ai/artifact/FJXEXC6amjvtZuswbfyD6y. 핵심은 아래 "기획 요약"에 옮겼다.
- 참고한 기존 기획: r9 디오라마 운영 지도, r48 층별 품질 누적, r50 사가정 제작 방식, r123 개인용 오프라인 우선, r124~r126 기사 무념·등급, r133 경 시간당 순소득, r134 생산·소비 흐름, 모바일 r1.

## 기획 요약 (구조 개편안)

| 영역 | 지금 | 개편 후 |
| --- | --- | --- |
| 지도 | 도로 선분을 평면 텍스처에 그린 2D | 동네 단계는 건물을 세운 2.5D, 서울·구 단계는 2D 유지 |
| 게임 규칙 | 배차·정산은 Core, 무념·흐름은 Unity 임시 계산 | 모두 Core, 저장과 복원 포함 |
| 화면 | 한 클래스가 좌표를 직접 찍는 IMGUI | 월드와 HUD를 나눈 uGUI |
| 실행 구조 | Unity 옆 엔진 exe와 JSON 통신 | Core를 Unity 안에서 직접 실행 |

코드는 규칙(Core) → 실행(게임 세션) → 투영(화면용 모델) → 표현(2.5D 월드, uGUI HUD) 네 층으로 나눈다. 아래로는 상태만, 위로는 명령만 흐른다.

로드맵(날짜 없음, 관문 통과로 진행):

1. 2.5D 지도 시제품 — 관문: 두 주문 배차·정산이 평면 판과 같다, 렌더러 128개·재질 12개 안
2. 규칙을 Core 한곳으로 — 관문: Core 점검 통과, 다시 켜도 같은 상태, 경 시간이 바닥나는 하루
3. 화면 교체(IMGUI → uGUI) — 관문: 1600×900과 작은 창에서 같은 조작, 본문 12px 이상
4. 지도 층 누적(도로 이름, 매장 강조, Synty 소품, 배경 차량) — 관문: 전체·근접·경계 화면 품질이 고르다
5. 범위와 규칙 확장 — 관문: 넓힌 범위에서도 같은 성능 기준과 검사 통과

표현 방식은 혼합안(절차적 압출 바탕 + Synty는 매장·차량·소품만)을 택했다. 사용자가 "계획대로 진행"으로 확인했다.

## 된 것

작업본: `artifacts/local/hongdal-unity-claude-20261112/` (원본 `hongdal-unity-operator-expansion-20261111`과 엔진·Core는 그대로)

| 항목 | 내용 | 증거 수준 |
| --- | --- | --- |
| 기사 무념·경 시간·등급, 경 시간당 순소득, 생산·소비 흐름 | Unity 쪽 임시 계산 (`SagajeongDispatchMind.cs`) | 빌드, 실행 화면 |
| 오토바이 위치 어긋남 버그 | 창이 1280×720보다 클 때 회전 중심 오류 수정 | 빌드, 실행 화면 |
| 1단계 2.5D 지도 | 미리 구운 메시(도로 위계, 실제 외곽 건물 1,058동, 그림자), 정사영 카메라 55°/30°, 표식 투영, 드래그·확대 보정, 2.5D/평면 전환 | 빌드, 실행 화면 |
| 빈 블록 채움 | 예시 건물 3,414동, 실제 건물과 따로 구움, 화면에서 끄고 켬 | 빌드, 실행 화면 |
| 이동 범위 제한 | 2.5D에서 자료 범위 밖으로 끌리지 않음 | 빌드, 실행 화면 |
| 무념 깨짐 오표시 | 출발 순간 1,440분 계산 오류 수정 | 빌드, 실행 화면 |

관문 확인(실행판 `player-20261112-114341`, 채움 전 비교는 `player-20261112-111908`):

- 2.5D와 평면에서 같은 순서로 두 주문 배차 → 두 번 모두 정산 2건, 경 유지 시간 3분 57초, 주문 사건 시각 동일
- 렌더러 4개(채움 끄면 2개), 재질 2개
- 프레임 수치는 재지 않음

미실행: 저장소 `eng/validate-changes.ps1`, EditMode 시험, 기존 `...Proof.cs` 검증 촬영. 2.5D는 `--sagajeong-` 검증 인자로 실행하면 꺼지게 해 두었다.

## 파일

`overlay/`에 바뀐 파일만 있다. `build-and-run.cmd`가 원본 프로젝트를 한 번 복사하고 overlay를 덮어 빌드한다.

| 파일 | 상태 |
| --- | --- |
| `overlay/Assets/SagajeongDispatch/SagajeongDispatchDiorama.cs` | 새 파일. 2.5D 전부 |
| `overlay/Assets/SagajeongDispatch/SagajeongDispatchMind.cs` | 새 파일. 무념·흐름 임시 계산 |
| `overlay/Assets/Resources/hongdal-diorama.bytes` | 구운 메시 (약 10MB, 형식 `HDIO` 판본 2) |
| `overlay/Assets/Resources/HongdalDiorama.shader`, `HongdalDioramaShadow.shader` | 새 파일 |
| `overlay/Assets/SagajeongDispatch/` 의 Map, Tiles, CityBoard, Motorcycle, Expansion, Focus, View | 기존 파일 수정 |
| `diorama-bake/bake_diorama.py`, `diorama-summary.json` | 메시 굽기 스크립트(Python 3, numpy, scipy)와 요약 |
| `README.md` | 작업본 설명, 표현값, 한계 |
| `screens/` | 실행 화면 11장 |

## 빈 것 (이어받을 순서)

1. **작업본을 정식 경로로 옮길지 결정** — 사용자 판단. 옮긴다면 Unity 프로젝트 정본 위치와 `writePaths`를 정하고, 구운 메시(10MB)를 저장소에 넣을지 빌드 때 굽게 할지 정한다.
2. **2단계: 규칙을 Core로** — `SagajeongDispatchMind.cs` 계산부를 Core로 옮긴다. WPF 작업본의 `tools/Ssalddel.BatchMonitor.Windows.Claude/RiderMind.cs`·`ProductionFlow.cs`가 출발점이 될 수 있다(빌드 미확인). 저장·복원과 주문량 증가(r129·r130)를 함께 넣는다. 완료 조건: Core 점검 통과, 재실행 뒤 같은 상태, 경 시간이 바닥나는 하루가 실제로 나온다.
3. **Core 내장** — 엔진 exe 대신 Core를 Unity 안에서 직접 실행. 모바일 r1의 54명령 동등 결과를 기준으로 쓴다.
4. **3단계: uGUI 교체** — 오늘 만든 화면 시안(`mockups/`)을 기준으로 한다.

## 건드리지 말 것

- 원본 `hongdal-unity-operator-expansion-20261111`, 엔진 `Simulation`, `Ssalddel.Sagajeong.Core`의 기존 동작 (2단계 전까지)
- 2.5D 표현값(카메라 각, 높이 압축)은 시제품 값이다. 바꾸려면 기획 쪽에서 정한다.

## 기획에 돌려보낼 질문

1. 우선순위 기록: r139(WPF 우선)를 Unity 집중으로 바꾸는 새 판본을 낼지
2. 건물 외곽 보강 자료: 도로명주소 전자지도 건물 도형, 국토교통부 GIS건물통합정보 중 무엇을 쓸지, 이용 조건과 게임 배포 가능 여부
3. 예시 채움 건물을 정식 판에도 둘지
4. Synty 에셋을 빌드에 넣어 배포하는 조건

## 커밋 여부

커밋·push 하지 않았다. 모두 PC 작업본과 이 반환 문서뿐이다.

## 2026-11-12 Codex 대조 후속 — 원 반환과 현재 인계 구분

위 내용은 수행자 Claude의 당시 반환으로 보존한다. 사용자 후속 요청으로 **Unity Windows 개발 집중 / Codex 기획·현황 정리**가 확정되어 질문 1은 [r140](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/unity-windows-development-direction.r140.md)으로 답했다. 이번 후속은 문서 정리이며 “빈 것” 1의 정식 경로 결정·프로젝트 이동·시제품 전체 채택을 수행하지 않았다. 자료 이용 조건·합성 채움·Synty 배포 조건은 미정으로 유지한다.

[현재 구현 현황](Unity-Windows-구현현황.md)을 다음 인계의 시작점으로 삼는다. 특히 “빈 것” 3의 Core 내장과 4의 uGUI는 별도 MobileGame 소비 모듈에 이미 구현·검토된 부분이 있다. 2.5D 시제품은 현재 외부 엔진 EXE를 쓰므로 이 차이는 **기존 구현의 통합 대기**다. 전체 프로젝트가 미구현이라고 보고 같은 기능을 다시 만들지 않는다. 무념·생산소비 임시 계산의 Core 이관과 저장 검증은 별도로 남는다.

Codex는 소스·지문·기존 빌드 로그·캡처 파일을 읽기 대조했으며 위 수동 플레이를 이번에 재실행하지 않았다. 기존 `--sagajeong-` 검증에서 2.5D가 꺼지는 조건도 현황표에 남겼다. 다음 구현 계획에는 평면 회귀와 실제 2.5D 입력·화면 검증을 구분하고, [혼용 운영 기준](Codex-Claude-혼용운영안-2026-11-12.md)의 같은 PC 미커밋 판본·허용 경로를 명시한다.
