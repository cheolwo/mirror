# [개발 · Unity 운영 관찰 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · World 결속 소스 r74]

- 기준일: 2026-09-17
- 승인 기준선: [리팩토링 계획 r66](seasonal-campaign-refactoring-plan.r66.md)
- 선행 구현: [Core·RemoteHost r68](seasonal-campaign-core-remote-implementation.r68.md), [Unity 관찰 기반 r69](seasonal-campaign-unity-observation-foundation.r69.md)
- 상태: `WorldBindingSourceImplemented / ActualUnityCompilePassed / SceneBindingPending / IntegratedE4`

## 구현 결과

현재 국면의 `RequiredSourceCodes`를 Unity 관찰 원천에 명시적으로 대응시켰다. 필수 원천이 없거나 지연되면 마지막 정상 Campaign 국면은 계속 보이지만 `AvailableActions`만 비워진다. 디오라마·카드·NPC 등 필수가 아닌 원천의 지연은 전체 관찰을 막지 않는다. 알 수 없는 원천 코드는 최신이라고 추정하지 않고 조작을 차단한다.

제품 Unity 저장소에는 다음 읽기 전용 결속 소스를 추가했다.

- 환경 변수에서만 token을 읽는 `EnvironmentUnityAccessTokenProvider`.
- Simulation 예행연습 Client의 인증 GET을 Campaign 전송 계약으로 바꾸는 `절기운영CampaignApiAdapter`.
- 화면 모델과 원천 지연을 보관하는 `절기운영CampaignWorldView` View Socket.
- 기존 Shell 세션과 운영 장면 revision을 조합하는 `절기운영CampaignWorldController`.
- 기존 `OperationalOsWorldRoot` 조립기에 같은 View·Controller를 한 번만 추가하는 Builder 결속.

`운영관찰WorldController`는 정상 적용된 읽기 결과만 `SnapshotApplied`로 내보낸다. 새 Controller는 이 판본을 Campaign Coordinator에 전달하지만 운영 장면 원장이나 GameObject를 변경하지 않는다. Campaign 조회는 GET만 허용하고 token·응답을 Asset, PlayerPrefs, Save/Replay 또는 로그에 저장하지 않는다.

## 검증

- Hongdal 순수 코어 집중 시험: Campaign 수신·조합 14/14 통과.
- Hongdal Task 검증 기준선: Simulation 1,979/1,979, Unity 순수 코어 798/798 통과.
- 실제 Unity package import와 스크립트 컴파일 통과. 이 과정에서 `Ssalddel.Unity.OperationalTransport`에 누락된 `Ssalddel.Simulation.Contracts` assembly 참조를 보완했다.
- 제품 Unity 새 결속 EditMode: 2/2 통과.
- 기존 `SimulationWorldShellTests`: 12/12 통과.
- 기존 `운영관찰WorldTests`: 5/5 통과.
- Unity 검증 자료: `C:/Users/user/ssalddel/artifacts/local/validation/20260917-seasonal-campaign-world-binding/`.

## 증거 상한과 다음 우선순위

제품 Unity 소스와 Builder 결속 및 실제 Unity 컴파일까지 확인했지만 canonical `SimulationWorldShell.unity`는 저장하지 않았다. 실제 RemoteHost·token·Campaign 세션 연결, Play Mode, Game View도 수행하지 않았으므로 통합 증거는 E4를 유지한다.

다음 우선순위는 다음 순서다.

1. 카드·NPC 원천의 안정 revision Adapter를 기존 소유 모델에 맞춰 추가한다.
2. canonical Scene에 Builder 결과를 저장하고 Missing Script·중복 Component를 검사한다.
3. 인증된 RemoteHost 상태 사본으로 Play Mode를 검증한다.
4. 실제 Game View에서 정상·필수 원천 지연·복구를 각각 캡처한다.

Scene 저장·Play Mode·Game View·commit·push·배포는 수행하지 않았다.
