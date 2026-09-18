# [기획 · Unity 운영 관찰 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 절기 캠페인 관찰 Coordinator r64]

- 기준일: 2026-09-17
- 상태: `Draft / UnityObservationCoordinatorConfirmed / ExistingUnityFlowReuseConfirmed / PlanningOnly / ImplementationNotStarted`
- 이전 판본: [절기 캠페인 Core 우선 계획 r63](seasonal-campaign-core-first-plan.r63.md)
- 참조 계획: [통합 개발계획 r61](development-handoff.r61.md)

## 확정

Unity 끝단에는 `SeasonalCampaignObservationCoordinator`를 두고, 기존 디오라마·업무 상태·카드·NPC 표현의 읽기 결과를 하나의 운영자 화면 모델로 조합한다.

이번 판본은 코드 추가가 아니라 후속 개발의 책임·재사용·금지 경계를 정하는 계획이다.

## 권위 흐름

```text
업무 Workflow Core ─┐
Simulation Runtime ─┼→ 공유 계약·상태 사본
절기 Campaign Core ─┘          ↓
                      Local/Remote Adapter
                               ↓
                 SeasonalCampaignObservationCoordinator
                    ├─ Campaign Interpreter
                    ├─ 행정동 Diorama Interpreter
                    ├─ 운영 Scene Interpreter
                    ├─ Card Workspace
                    └─ NPC Movement Presenter 입력
                               ↓
                  SeasonalCampaignScreenModel
                               ↓
                    Presenter·View Socket
                               ↓
                  기존 SimulationWorldShell
```

Unity는 `Ssalddel.Simulation.Domain`이나 Application 구현을 직접 참조하지 않는다. `Ssalddel.Simulation.Contracts`와 `Ssalddel.WorkflowRules.Contracts`가 공개한 상태 사본만 소비한다.

## 기존 Unity 코드 재사용

| 필요한 책임 | 먼저 재사용할 현행 코드 | 보완 범위 |
| --- | --- | --- |
| 행정동 배경 | `AdministrativeDongDioramaClient`, `AdministrativeDongDioramaInterpreter` | 캠페인 세션의 행정동·자료 revision 결속 |
| 운영 객체 | `OperationalWorldSceneClient`, `OperationalWorldSceneInterpreter` | 캠페인 session·phase와 업무 snapshot 상관관계 |
| OS 분류 | `OperationalOsObservationRouter`, `OperationalOsWorldObservationSession` | 음식배달 관찰 묶음과 진단 전달 |
| 전송 | `IOperationalWorldProjectionTransport`, Unity JSON Decoder 패턴 | Campaign 전용 계약 Decoder 또는 Local Adapter |
| 상태 교체 | `StableIdReconciler`, `WorldProjectionReconciler` | 캠페인 선택·phase·modifier의 stable ID/revision 검사 |
| 마지막 정상값 | `LastSuccessfulLoadRuntime` | 캠페인 원천별 stale·오류 진단과 기존 화면 유지 |
| 대상 선택 | `SelectionStateStore` | 주문·기사·음식점·정책 카드 선택을 한 화면 문맥으로 결속 |
| 카드 | `CardWorkspaceCoordinator` | 절기·아르카나 Family source 추가, 실행 권위는 기존 소유자에게 위임 |
| NPC | `NpcMovementInterpreter`, `NpcMovementPresenter` | 캠페인 결과의 읽기 전용 강조·상태 표시 |
| 동네 관찰 | `동네관찰SessionController`, `동네관찰Presenter` | 캠페인 화면 수명과 새로고침 조율 |
| 음식배달 표현 | `음식배달관찰경로`, `음식배달수명주기표현`, `사가정가상음식점선택Presenter` | 첫 점심 피크 Fixture의 운영자 관찰 모델 |

기존 클래스를 복사해 Campaign 전용 중복 구현을 만들지 않는다. 계약 의미가 다른 경우에만 얇은 Adapter·Interpreter를 추가한다.

## 신규 Unity 뼈대 후보

### `SeasonalCampaignObservationCoordinator`

- 캠페인, 디오라마, 운영 객체, 카드 원천의 조회를 조율한다.
- 원천의 권위 상태를 변경하지 않는다.
- 같은 `SessionStableId`, 지역, 자료 revision 묶음인지 검사한다.
- 화면 갱신 중 낮은 revision·중복 stable ID·서로 다른 지역 결속을 거절한다.
- 지역 전환·세션 종료 때 캠페인 메모리와 선택 상태를 정리한다.

### `SeasonalCampaignInterpreter`

- schema와 rule revision을 검사한다.
- 현재 phase, 가능한 행동, 선택 Preview, 활성 modifier, 관측과 결산을 메모리 모델로 변환한다.
- Unity에서 phase를 계산하거나 다음 구간을 임의 확정하지 않는다.

### `SeasonalCampaignScreenModel`

- 지역·절기·현재 phase와 남은 Simulation 시간.
- 미배차·지연 위험과 원인 카드.
- 서버/Simulation이 허용한 `AvailableActions`만 포함한 정책 버튼.
- 정책 Preview의 기사 반응·서비스·플랫폼 비용·13주 현금 영향.
- 절기·기상·제철·아르카나 문맥과 출처·품질.
- 마감 가능 여부, 결산과 다음 구간 이월 Preview.

### `SeasonalCampaignPresenter`

- 상단 운영 지표, 오른쪽 정책 카드, 선택 상세와 결산을 표현한다.
- 기존 디오라마 GameObject를 복제하지 않고 stable ID로 강조한다.
- 버튼 입력을 직접 상태 변경으로 처리하지 않고 기존 Preview·Confirm 소유자에게 전달한다.

## 첫 결속 장면

첫 화면은 사가정역·면목제3·8동의 r62 사건을 사용한다.

```text
행정동 디오라마
  + 합성 음식점·기사·주문 상태 사본
  + 예상보다 이른 점심 주문 증가
  + 가용 기사 부족
  + 기사 알림 Preview
  + 플랫폼 부담 할증 후보
  + 회복·결산
```

검증된 공간 경로가 없는 이동 구간은 기존 원칙대로 `이동 중`으로 표시하고 좌표를 추정하지 않는다.

## 개발 순서 보완

```text
Campaign Core 계약·상태 전이·Save/Replay
→ Campaign Local/Remote 읽기 Adapter
→ SeasonalCampaignInterpreter
→ SeasonalCampaignObservationCoordinator
→ SeasonalCampaignScreenModel·Presenter
→ 기존 SimulationWorldShell View Socket 결속
→ 실제 Play Mode·Game View 검증
```

`SimulationWorldShell` 외 새 공식 Scene을 만들지 않는다. 실제 View Socket·Prefab·GameObject 배치와 캡처는 별도 개발 수용과 월드·공간·배치 인계 뒤에 수행한다.

## 검증 계획

1. Unity Data assembly가 Contracts만 참조하고 Domain·Application을 참조하지 않는다.
2. LocalProcess와 RemoteHost 상태 사본이 같은 ScreenModel을 만든다.
3. 낮은 revision·중복 stable ID·지역 불일치·schema 불일치를 거절한다.
4. Preview는 화면과 Core 상태를 변경하지 않는다.
5. Campaign 상태가 없어도 디오라마와 정상 업무 관찰은 유지된다.
6. 카드가 없어도 Neutral 캠페인 화면을 만들 수 있다.
7. 지역 전환과 메모리 Clear 뒤 이전 Actor·카드·정책 선택이 남지 않는다.
8. 한 원천 갱신 실패가 기존 정상 상태를 조용히 삭제하지 않는다.
9. 자동 시험과 실제 Scene·Play Mode·Game View 증거를 분리 보고한다.

## 명시적 제외

- Campaign Core 구현과 공개 API route 확정.
- 정확 주문·기사 수, 시간 배율, 부족 임계값과 할증 금액.
- 실제 Unity Scene·Prefab·GameObject·캡처.
- 실제 FCM·GPS·PG·배차·정산 Command.
- 기존 주역 Campaign과 절기 Campaign의 상태 합병.

## 다음 질문 하나

Campaign 구성 원천 하나의 갱신이 실패했을 때 Unity 화면은 어떻게 유지할까?

1. `원천별 마지막 정상 상태 유지 + 해당 영역만 갱신 지연 표시` — 추천. 디오라마 전체를 지우지 않으면서 자료 간 판본 차이를 명확히 보여 준다.
2. `화면 전체 갱신 거절` — 일관성은 강하지만 카드나 NPC 한 원천의 실패로 전체 관찰이 멈춘다.
3. `실패 원천만 즉시 비움` — 단순하지만 정상 객체가 갑자기 사라져 운영자가 실제 변화로 오해할 수 있다.
