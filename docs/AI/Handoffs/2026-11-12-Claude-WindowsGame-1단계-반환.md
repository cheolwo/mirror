# 2026-11-12 Claude Windows 모듈 1단계 반환

[구현계획: 법정동 팩과 Core 통합](2026-11-12-Claude-구현계획-법정동팩과Core통합.md) 7절의 결정("Unity는 Windows용 모듈을 새로 만든다")과 1단계를 실행한 반환이다. 사용자 요청("시작")으로 한 작업이다. PLAN 판본이나 작업지시서에서 나온 것이 아니므로 `Approved`·`ReadyToDispatch`로 보지 않는다.

## 작업실 역할

- 수행: Claude 데스크톱 원격 세션 (PC 셸·git 없음, 파일 쓰기와 화면 조작으로 빌드·확인)
- 이어받기 대상: Codex 또는 Claude, 사용자가 수용을 정한 뒤

## 된 것

| 항목 | 내용 |
| --- | --- |
| 새 모듈 | `ssalddel/Assets/Ssalddel/WindowsGame/` — 이름공간 `Ssalddel.WindowsGame`, asmdef `Ssalddel.WindowsGame`(참조 `Ssalddel.Sagajeong.Core`)와 `Ssalddel.WindowsGame.Editor` |
| 화면 코드 이전 | 2.5D 작업본(`hongdal-unity-claude-20261112`)의 18개 파일. 이름공간만 바꿈 |
| Core 직접 실행 | `SagajeongDispatchEngine`을 Core 어댑터로 교체. 엔진 exe·`Simulation` 폴더 없이 동작. 화면 쪽 `Send(DispatchRequest)` 계약은 그대로 |
| 저장·이어하기 | Core `save`/`restore` 사용. `LocalLow/Ssalddel/홍달 경영 시뮬레이션/windows-game/confirmed-session.v1.json`, 임시 파일 교체, `.previous` 보관. `--sagajeong-*` 검증 인자일 때는 읽지도 쓰지도 않음 |
| 선택 유지 | 상세 창 "닫기"가 고른 기사를 지우지 않음 (r141 점검 3) |
| 이어하기 버튼 | 불러온 캠페인 회차가 정산 상태여도 "운영 시작"으로 이어감. 전에는 "같은 조건 시작"이 떠서 새 회차가 될 뻔했음 |
| 빌드 진입점 | `Ssalddel.WindowsGame.Editor.WindowsGameBuild.Build -hongdalWindowsOutput <빈 폴더>\HongdalSimulation.exe` |
| 빌드 묶음 | `Hongdal/artifacts/local/hongdal-windows-build-20261112/` — 모듈과 Core 패키지 3개만 넣은 별도 빌드 프로젝트, `build-and-run.cmd` |

모바일 모듈(`Assets/Ssalddel/MobileGame`)은 건드리지 않았고 서로 참조하지 않는다.

## 관문 확인 (실행판 `player-20261112-143105`, 수정 후 `player-20261112-143528`)

| 관문 | 결과 |
| --- | --- |
| 두 주문 정산 | 기사 A→A 주문, 기사 B→B 주문, 4배속. 플랫폼 수입 9,600원, 기사 보수 7,200원, 가상 차액 3,600원. 경 유지 3분 44초 |
| 다시 켜도 같은 상태 | 창을 닫고 다시 실행 → 11:56:08, 경 유지 3분 44초, 경 시간당 115,486원, 수령·정산 2건 그대로. "저장한 회차를 불러왔습니다" 표시 |
| 이어서 진행 | 수정판에서 "운영 시작" → 11:56:08부터 시계 진행 |
| 선택 유지 | 지도에서 기사 A 선택 → 상세 닫기 → 배차 데스크에서 기사 A 강조 유지, 바로 배차됨 |
| 2.5D | 건물 1,058동 · 채움 3,414동 · 렌더러 4 · 재질 2, 이전과 같음 |
| 빌드 | 오류 0. 경고 508개는 모두 Core의 CS8632(nullable 주석)로 기존 것 |

주문 사건 시각은 이번에 처음 배차를 11:31:47에 해서 이전 실행(11:30대)과 직접 비교하지 않았다. 정산 금액은 계획 관문값과 같다.

미실행: `eng/validate-changes.ps1`, EditMode 시험, `...Proof.cs` 검증 촬영, ssalddel 프로젝트 안에서의 컴파일(빌드는 별도 프로젝트에서만 확인).

## 파일

| 경로 | 상태 |
| --- | --- |
| `ssalddel/Assets/Ssalddel/WindowsGame/Runtime/*.cs` (18) | 새 파일. `SagajeongDispatchEngine.cs`는 새로 씀, `View`·`Focus`·`Expansion`은 위 수정 포함 |
| `.../WindowsGame/Editor/WindowsGameBuild.cs` | 새 파일. 장면은 모듈 안 `Generated/`에 생성 |
| `.../WindowsGame/Resources/HongdalWindows/` | 배달 표본, 도로 자료, 출처 고지 |
| `.../WindowsGame/Resources/` 그 밖 | 그림·타일·구운 메시(약 10MB). 빌드 스크립트가 처음 한 번 2.5D 작업본에서 복사함 |
| `.../WindowsGame/README.md` | 모듈 설명 |
| `artifacts/local/hongdal-windows-build-20261112/` | 빌드 템플릿, `build-and-run.cmd`, 로그, 실행판 2개 |

## 빈 것 (이어받을 순서)

1. **ssalddel 저장소에 넣을지 결정** — 사용자 판단. 넣는다면 `Resources`의 큰 파일(메시 10MB, 타일)을 그대로 둘지, 빌드 때 복사할지 정한다. ssalddel 프로젝트는 URP·Input System을 쓰므로 그 안에서 실행할 때 화면을 따로 확인해야 한다.
2. **2단계: 무념·흐름을 Core로** — `SagajeongDispatchMind.cs`는 아직 화면 쪽 계산이라 저장되지 않는다. 불러오면 0부터 다시 센다.
3. **저장 크기** — Core 저장은 모든 tick 명령을 기록한다. 긴 플레이에서 크기와 불러오기 시간을 재야 한다.
4. **법정동 팩·횡단보도(계획 2단계), 단계별 정보 공개(r141), 이웃 동** — 구현계획 문서 순서대로.

## 기획에 돌려보낼 질문

1. Windows 실행판의 저장 위치·슬롯 수(지금은 확정 저장 1개 + 직전본)
2. 정식 배포 이름과 exe 이름(지금 `HongdalSimulation.exe`)

## 커밋 여부

커밋·push 하지 않았다.
