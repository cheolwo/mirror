# 홍달 경영 시뮬레이션 — Unity Windows 기획·구현 현황

기준일: **2026-11-12**. 현재 방향은 [r140](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/unity-windows-development-direction.r140.md), 역할과 반환 양식은 [혼용 운영 기준](Codex-Claude-혼용운영안-2026-11-12.md)이 소유한다. 이 문서는 Claude가 다음 구현을 준비할 때 읽는 기능별 대조표이며 별도 실행·승인 원장이 아니다.

후속 표현 기획은 [점진 UI·장소 표현 r141](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/progressive-focus-ui-and-place-reveal.r141.md)이다. 문서·이미지 시안은 구현과 구분하며 현재 Core/uGUI 통합 계획의 승인이나 실행 소유를 변경하지 않는다.

## 먼저 구분할 작업본

### 후속 진행 보고와 기획 범위 (2026-11-12)

사용자는 **Core 통합·우유 5팩 흐름을 Claude가 구현 중**이라고 전달했다. 아래 소스·시험 표는 기존 대조 시점의 근거이며 이번에 새 구현을 실행·검증한 결과가 아니다. 진행 보고를 승인 대기로 되돌리지 않고 완료·통합 여부는 수행자 반환과 대조한다. 지도별 주 업무·전환 규칙과 창고 내부 모델 보기는 [r144](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/scale-os-management-focus.design.r144.md)의 문서화 범위다. 내부 모델은 시각 검토 대기이며 기존 개방형 마트·창고 소비판을 현재 Windows에 적용한 것으로 세지 않는다.

| 구분 | 실제 위치 | 현재 의미 |
| --- | --- | --- |
| 보존 Windows 게임 | [r137 프로젝트와 결과](../../../artifacts/local/hongdal-unity-operator-expansion-20261111/result.json), [실행판](../../../artifacts/local/hongdal-unity-operator-expansion-20261111/player-release/HongdalSimulation.exe) | 검은 지도·성장 안내 UI와 음식 두 주문의 실제 조작 근거. 별도 엔진 EXE 사용 |
| Claude 2.5D 시제품 | [작업 안내](../../../artifacts/local/hongdal-unity-claude-20261112/README.md), [반환](2026-11-12-Claude-Unity-2.5D-반환.md) | 독립 복사 프로젝트와 overlay. 반환에서 지목한 판본은 `player-20261112-114341`. 정식 프로젝트로 승격·통합하지 않음 |
| 재사용할 Core·uGUI | [공통 Core](../../../Ssalddel.Sagajeong.Core/UnityPackage), [구현·검증 기록](../../../artifacts/local/hongdal-mobile-unity-20261111/completion.json) | `C:/Users/user/ssalddel/Assets/Ssalddel/MobileGame`의 별도 소비 모듈에 Core 내장·uGUI·저장 구현. Windows 비율 검토와 Android 실기기는 별개 |
| WPF와 Claude WPF 작업본 | [기존 WPF](../../../tools/Ssalddel.BatchMonitor.Windows/README.md), [Claude WPF](../../../tools/Ssalddel.BatchMonitor.Windows.Claude/README.md) | 기존 규칙·자료 참고. 현재 주 개발 대상은 Unity Windows이며 이 작업본들을 자동 수정하지 않음 |

Hongdal 기준은 `dev/mirror-integration` / `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86`, 별도 Unity checkout은 `unity/workstream-transition` / `605e87ef46b2ebd3b19f65ebd9d650e0ccf44d77`이다. **둘 다 미커밋 변경이 있으므로 commit만으로 현재 작업본을 재현할 수 없다.** 같은 PC에서는 [이번 대조 기록](../../../artifacts/local/unity-windows-handoff-refactor-20261112/completion.json)과 실제 소스를 다시 확인한다. 다른 PC·클라우드에는 이 로컬 경로가 자동 전달되지 않는다.

## 기능별 대조

`보존 구현`은 지정한 기존 소비판에 구현된 상태, `시제품`은 별도 실험 작업본의 구현, `통합 대기`는 다른 곳에 있는 구현을 현재 대상에 연결해야 하는 상태다. `기획/시안`은 실제 기능과 구분한다. 아래 기존 시험·화면은 원 기록의 판본에 한정하며 이번 문서 정리에서 게임을 다시 실행한 결과가 아니다.

| 기능 묶음 | 구현 상태·위치 | 확보된 근거 | Claude가 이어받을 때 남은 부분 |
| --- | --- | --- | --- |
| 점진 UI 소개·관여 매장 1.5D·거래 흔적 유지 | [r141 기획·순차 시안](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/progressive-focus-ui-and-place-reveal.r141.md), 제품 미구현 | 문서·시안 제작 기록. 실제 게임 입력·저장 검증 아님 | 닫기 시 선택 보존·픽업 기반 흔적·표현 기록 저장·단계별 소개는 후속 구현 요구 |
| 기사→매장 지정, 수락·픽업·도로 이동·전달·수령·가상 정산 | r137 보존 구현 | [실제 Windows 두 주문 정산](../../../artifacts/local/hongdal-unity-operator-expansion-20261111/result.json) | 2.5D 소비판의 동일 입력·사건·금액 회귀를 별도로 결속 |
| 휠 확대·끌기·지도 중심 화면·선택과 패널 복귀 | r137 구현, Claude 투영·회전 중심 보정 시제품 | r137 실제 창 기록 + Claude 반환·화면 | 2.5D의 표식/클릭·작은 창·경계에서 실제 조작 대조 |
| 공통 배차·정산 Core | [Runtime](../../../Ssalddel.Sagajeong.Core/UnityPackage/Runtime)에 구현 | [52검사·5시나리오/54명령 동등성](../../../artifacts/local/hongdal-mobile-unity-20261111/completion.json), 당시 소스 지문 | 기존 규칙 재작성보다 소비판 연결을 우선. 새 규칙은 별도 범위 |
| Unity 내부 Core 실행 | 별도 MobileGame 세션에 구현, Claude 2.5D에 통합 대기 | `Integration/MobileGameSession.cs`의 `SagajeongSimulationCore`; Claude 판은 여전히 외부 엔진 실행 | 기존 세션·명령·결과 계약을 재사용하여 외부 EXE 교체 범위 명시 |
| uGUI 지도·HUD·선택 | 별도 MobileGame에 구현, Claude 2.5D에 통합 대기 | `MobileGameController.cs`·`MobileMapGraphic.cs`, [Windows 렌더/uGUI 43항목 기록](../../../artifacts/local/hongdal-mobile-unity-20261111/completion.json) | 기존 입력과 2.5D 투영의 연결 및 Windows 화면 설계 확정 |
| 저장·이전 정상본 복구·백그라운드 정지 | 별도 MobileGame에 구현 | 같은 모바일 검토 기록의 저장·손상 복구 | 2.5D와 임시 무념 규칙까지 포함한 저장·재실행은 미확인 |
| 무념·경 시간·등급·타로 방향·시간당 순소득 | Claude `SagajeongDispatchMind.cs` 임시 계산 | 소스·빌드 로그·화면과 수행자 반환 | Core 정본화·계산 계약·저장·하루 소진·성장 검증. 수치 효과 전체 완료로 보지 않음 |
| 동네 정사영 2.5D·표식·이동 범위 제한 | Claude 시제품 구현 | [수행자 반환](2026-11-12-Claude-Unity-2.5D-반환.md), 빌드 로그·화면 11장 존재 | 정식 경로·통합 범위와 2.5D 전용 입력/화면 검증 |
| 건물 외곽·합성 채움 | 구운 메시 시제품, 외곽 1,058동·합성 3,414동 | [작업 안내](../../../artifacts/local/hongdal-unity-claude-20261112/README.md), `diorama-bake/diorama-summary.json` | 실제 도형·추정 높이·합성을 구분. 자료 배포·굽기·로딩 정책은 미정 |
| 기사·가맹점·생산자·공장 성장 탭 | r137 정보 미리보기 구현 | [r137 실제 조작 결과](../../../artifacts/local/hongdal-unity-operator-expansion-20261111/result.json) | 생산·소비 상한의 임시 표시와 별개로 가맹·생산·공장 경영 규칙은 이 판에서 미구현 |
| 모집·입교·9단계 성장·주말/주차·다섯 시간대 수요 | [r124~r132 기획](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/README.md)과 별도 초기 시제품 보존 | 기획·기존 Core 기능은 개별 대조 필요 | 최신 2.5D에서 전 흐름 구현·밸런스·저장 검증 완료 여부는 미확인. 전부 새 기능으로 재구현하지 않음 |
| 화물·창고·가맹·공장·생산과 소비의 경영 연결 | [r3~r7 및 r133~r136](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/README.md), 기존 별도 업무/Simulation 구현 | 기존 앱·Simulation 증거는 있음. 현재 음식 게임과 소비판이 다름 | Unity Windows 게임의 연속 플레이 통합은 후속. 앱 구현을 게임 완료로 세지 않음 |
| 내 운영 현황 창·유동인구/교통·구매 Synty 외형 | 이미지/정적 렌더 시안과 개별 자료 | [현재 작업의 시안 근거](../CURRENT_WORK.md) | 게임 입력·규칙·실행 적용은 후속. 시안을 완료 기능으로 세지 않음 |
| Android·Steam/Drive 자동 자료 배포·운영/AI 연계 | 별도 기획·부분 기반, 현재 Windows 마감 범위 밖 | 모바일 APK/기기 미완료, 자료 묶음과 운영 계약 구분 | Windows 개발의 선행조건으로 두지 않고 별도 구현·권한·검증 필요 |

## 검증에서 빠뜨리지 않을 차이

- 별도 모바일 소비판의 `source-fingerprints.json` 43파일은 이번 읽기 대조에서 현재 소스와 일치했다. 당시 통과 근거를 보존하지만, Claude 시제품으로 옮긴 뒤의 시험 통과를 뜻하지 않는다.
- Claude 반환은 빌드·실제 화면 관찰을 기록했다. Codex는 이번에 해당 소스·빌드 로그·캡처 파일의 존재를 대조했으며 그 화면을 다시 조작하지 않았다. 저장소 검증·EditMode 시험은 수행자도 미실행으로 보고했다.
- Claude `SagajeongDispatchDiorama.cs`는 `--sagajeong-` 검증 인자에서 2.5D를 끈다. 따라서 기존 자동 촬영만 통과해서는 2.5D 화면 완료가 아니다. 평면 회귀와 2.5D 실제 화면 근거를 각각 남겨야 한다.
- `SagajeongDispatchMind.cs`의 임시 지표, 정산 2건과 경 유지 시간 3분 57초의 수행자 관찰, Core·운영의 확정 상태는 서로 다른 사실이다. 프레임 성능·하루 소진·정확한 소득 규칙은 미확인이다.

## 진행 중 작업과 반환 방법

r140 정리 당시에는 2.5D Windows 소비판에 기존 Core·uGUI를 연결하는 구현 계획이 다음 개발 준비였다. 이후 사용자 전달 기준 Core 통합·우유 5팩 구현이 진행 중이므로 그 범위를 다시 착수 대기로 처리하지 않는다. [Claude 반환의 빈 것](2026-11-12-Claude-Unity-2.5D-반환.md) 중 Core 내장·uGUI는 기존 모듈의 통합 차이로 대조하고, 무념 계산의 Core 이관은 별도 변경 범위·보호 시험과 함께 반환받는다. 현재 Codex 문서 검토는 r144 지도별 경영 관점과 창고 내부 표현이며 r141/r143의 기존 표현 기준도 함께 유지한다.

Claude는 대상 프로젝트·기준 지문·허용 경로·재사용 파일·이번에 비워 둘 부분·같은 두 주문에서 보존할 사건과 금액·검증 방법을 [인계 양식](Codex-Claude-혼용운영안-2026-11-12.md)에 채운다. 구현 반환에는 이 표의 해당 행만 새 소스·결과 링크로 갱신하고 남은 한 가지를 적는다. 이번 정리는 제품 이동·새 규칙 구현·시제품 채택·실행권 부여를 수행하지 않았다.
