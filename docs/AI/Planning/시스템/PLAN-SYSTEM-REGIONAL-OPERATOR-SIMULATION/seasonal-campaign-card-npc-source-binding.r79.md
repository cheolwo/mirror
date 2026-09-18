# [개발 · Unity 운영 관찰 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 카드·NPC 원천 결속 r79]

- 기준일: 2026-09-17
- 승인 기준선: [리팩토링 계획 r66](seasonal-campaign-refactoring-plan.r66.md)
- 선행 구현: [World 결속 소스 r74](seasonal-campaign-world-binding-source.r74.md)
- 상태: `CardNpcRevisionBindingImplemented / ActualUnityCompilePassed / SceneBindingPending / IntegratedE4`

## 구현 결과

기존 카드 서랍과 동네 NPC 관찰 화면을 새 원장으로 복제하지 않고, 각자가 이미 가진 원천 revision만 `SeasonalCampaignObservationCoordinator`의 독립 원천 진단으로 전달한다.

- `CardWorkspaceCoordinator`는 Family별 `SourceRevision`을 정렬된 `SourceRevisions`로 보존한다. 이를 하나의 가짜 전역 revision으로 합치지 않는다.
- 카드 Adapter는 `FamilyCode:revision` 묶음을 결정적으로 직렬화하고 ordinal은 갱신 순서 비교용 최댓값으로만 사용한다.
- 카드 판본이 없거나 Presentation 사본이 아니면 최신으로 추정하지 않고 `CardWorkspaceRevisionUnavailable`로 지연 처리한다.
- NPC Adapter는 기존 `동네관찰ScreenModel.Revision`만 전달하며 NPC 이동이나 업무 결과를 새로 계산하지 않는다.
- 제품 Unity의 `카드서랍Presenter`와 `가상배달관찰Controller`는 정상 화면 적용 시 읽기 전용 사건을 내보낸다.
- `절기운영CampaignWorldController`는 늦게 생성되는 두 Presenter를 재발견해 구독하고, 같은 원천 코드의 최신 적용 결과만 교체한다.

## 검증

- Hongdal 카드·Campaign 집중 시험: `13/13` 통과.
- 제품 Unity 실제 import·compile 및 `절기운영CampaignWorldBindingTests`: `3/3` 통과.
- 전체 Task 기준선: Simulation `1,979/1,979`, Unity 순수 코어 `802/802` 통과.
- `git diff --check`: 오류 없음. 기존 줄바꿈 경고만 존재한다.

## 증거 상한과 다음 우선순위

이 구현은 소스와 EditMode의 E4 증거다. canonical `SimulationWorldShell` Scene에는 아직 구성 요소를 저장하지 않았고 실제 RemoteHost·token·Campaign 세션, Play Mode·Game View도 확인하지 않았다. 따라서 E5 이상으로 승격하지 않는다.

다음 우선순위는 월드·공간·배치 소유권 아래에서 canonical Scene에 기존 Builder 결속을 실제 저장하고, 격리 RemoteHost에 연결해 원천별 지연·복구를 Play Mode에서 확인하는 것이다.
