# [개발 · Unity 운영 관찰 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 관찰 수신·조합 기반 r69]

- 기준일: 2026-09-17
- 선행 구현: [Core·저장·RemoteHost r68](seasonal-campaign-core-remote-implementation.r68.md)
- 승인 기준선: [Coordinator r64](unity-seasonal-campaign-observation-coordinator.r64.md), [원천별 마지막 정상 상태 r65](unity-source-failure-retention-policy.r65.md), [리팩토링 계획 r66](seasonal-campaign-refactoring-plan.r66.md)
- 상태: `UnityObservationFoundationImplemented / LogicE4 / PresentationE4 / IntegratedE4 / WorldBindingPending`

## 구현 결과

Scene이나 GameObject를 추가하지 않고 `Ssalddel.Unity` 데이터·조합 계층에 다음 흐름을 구현했다.

```text
인증 GET
→ Unity JSON Decoder 계약
→ Simulation절기운영CampaignInterpreter
→ 원천별 판본 Adapter
→ SeasonalCampaignObservationCoordinator
→ SeasonalCampaignScreenModel
→ SeasonalCampaignPresenter
→ 후속 기존 SimulationWorldShell View Socket
```

### Campaign 수신부

- `Simulation절기운영CampaignClient`는 Campaign 조회 경로만 호출하며 Command API를 제공하지 않는다.
- Interpreter는 schema·rule·Session·지역·Campaign revision·권위 경계를 검증한다.
- 낮은 revision, 같은 revision의 다른 내용, 다른 Session·지역, 운영 상태로 가장한 사본을 거절한다.
- 전송·Decoder·검증 실패 때 마지막 정상 Campaign 사본을 메모리에 유지하고 갱신 지연을 반환한다.
- `Clear` 뒤에만 다른 Session 문맥을 받을 수 있다.

### 원천별 관찰 조합

- Campaign·디오라마·운영 장면·카드·NPC의 revision을 독립 진단으로 유지한다.
- `CompositionRevisionSet`은 원천별 revision 묶음이며 가짜 전역 revision을 만들지 않는다.
- 한 원천 실패는 그 원천만 `Missing` 또는 `RefreshDelayed`로 표시하고 다른 정상 원천을 지우지 않는다.
- Campaign이 지연되면 마지막 phase는 보이되 Campaign 조작만 비활성화한다. 카드·디오라마처럼 무관한 원천 지연으로 모든 조작을 막지 않는다.
- Session·지역 전환은 이전 진단과 선택 상태를 비운다.
- 기존 `AdministrativeDongDioramaApplyResult`와 `OperationalWorldSceneApplyResult`는 얇은 Adapter로 revision 진단만 제공하며 원 payload를 복제하지 않는다.

### Presenter 경계

- Presenter는 읽기 전용 ScreenModel·원천 진단·revision 묶음을 View Socket에 전달한다.
- 버튼 Command, Campaign 전이, GameObject 생성·삭제, 업무 완료 판정은 포함하지 않는다.

## 검증

- Campaign Client·Interpreter 집중 시험: 5/5 통과.
- Coordinator·원천별 지연·문맥 전환·Presenter 시험: 7/7 통과.
- 기존 행정동 디오라마·운영 장면·마지막 정상 상태 회귀를 포함한 Unity 관련 시험: 37/37 통과.
- Core·저장·RemoteHost 관련 회귀: 73/73 통과.

## 증거 상한

- Logic E4: Core·Save/Replay·Local·TestServer HTTP가 검증됐다.
- Presentation E4: Client·Interpreter·Coordinator·ScreenModel·Presenter의 순수 메모리 조립이 검증됐다.
- 통합 E4: 두 궤적의 낮은 값이다. 실제 World 객체 발현인 E5는 아니다.

`UnityJsonSimulation절기운영CampaignDecoder`는 Unity 전처리 경계 안에 있으므로 .NET 시험 빌드가 실제 Unity 컴파일을 대신하지 않는다. Unity Editor·제품 프로젝트 package import·Scene·Play Mode·Game View는 아직 검증하지 않았다.

## 다음 우선순위와 차단

다음은 제품 Unity 저장소의 기존 `SimulationWorldShell` View Socket을 정확히 확인하고 이 Presenter를 한 번만 결속하는 단계다. 새 공식 Scene이나 Campaign 전용 디오라마를 만들지 않는다.

다음 항목은 결속 전에 더 확인해야 한다.

- 카드·NPC 원천이 제공할 안정 revision Adapter
- 조작별 필요한 원천·최소 revision 계약
- 기존 World Root에서 화면 생명주기와 지역 전환을 소유하는 정확한 객체
- Unity package import와 Decoder 실제 컴파일

commit·push·배포는 수행하지 않았다.
