# [개발 · 운영 Simulation · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · Core·저장·RemoteHost 구현 r68]

- 기준일: 2026-09-17
- 승인 기준선: [절기 운영 Campaign 리팩토링 계획 r66](seasonal-campaign-refactoring-plan.r66.md)
- 상태: `ImplementedThroughRemoteHost / LogicE4 / PresentationE1 / IntegratedE1 / UnityPending`
- 작업 명세: [절기 운영 Campaign E1~E7 상호작용 수직 검증](../../../../../eng/execution-ledgers/work-orders/seasonal-operations-campaign-advance.interaction-e1-e7-validation.json)

## 이번 구현 결과

`Simulation절기운영Campaign`을 기존 주역 Campaign과 분리된 Session 상태로 추가하고 다음 경로를 닫았다.

```text
공유 계약
→ Session Aggregate의 Begin·Preview·Confirm
→ Application Service
→ LocalSimulationRuntime
→ Save schema v32·Restore·Replay·hash
→ Simulation RemoteHost GET·Preview·Confirm API
```

- Preview는 상태를 변경하지 않고 같은 Session·seed·definition·원천 판본 입력에 같은 hash를 반환한다.
- Confirm은 expected revision과 Command ID를 검증하고 같은 명령 재요청을 멱등 처리한다.
- 절기 Campaign 상태와 명령 서명을 Save command log에 기록해 새 Service와 JSON 저장소를 거친 뒤에도 재요청 결과를 보존한다.
- schema v32는 이전 Save schema를 감싸는 확장 형식이며 기존 Hexagram Campaign payload를 재사용하거나 변경하지 않는다.
- Replay는 저장된 절기 상태를 외부 API로 다시 조회하지 않고 command log에서 재구성한 뒤 저장 사본과 대조한다.
- RemoteHost Controller는 조회·Preview·Confirm만 노출한다. Campaign 시작은 승인된 시나리오 조립 책임으로 남겨 Unity나 일반 원격 소비자가 임의 생성하지 못하게 했다.

## HTTP 계약

```text
GET  api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign
POST api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign/advance-previews
POST api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign/advance-commands
```

Controller는 `Simulation절기운영CampaignService`에만 위임하며 구간 판정, 운영 주문·배차 변경, Unity 표현 상태를 소유하지 않는다.

## 검증

- 절기 Campaign·Save/Replay·영속 저장·기존 Hexagram 회귀: 44/44 통과.
- RemoteHost Local/Remote Preview·Confirm 동등성, E 책임 metadata, route manifest: 28/28 통과.
- 위 범위를 합친 최종 관련 회귀: 73/73 통과.
- 실제 외부 서버 접속, Unity Editor, Play Mode, Game View는 실행하지 않았다.
- 기존 서버 nullable 경고 2건은 이번 변경 밖에 남아 있다.

## 증거 상한과 다음 우선순위

- Logic: E4. 계약·권위 상태·결정성·Local·저장 재생·TestServer HTTP까지 자동 검증했다.
- Presentation: E1. Unity가 읽을 phase·available action·source revision 계약만 준비됐다.
- 통합: 더 낮은 궤적인 E1. Unity 수신부와 실제 World 결속을 아직 검증하지 않았다.

다음 우선순위는 기존 Unity 전송·Decoder 관례를 재사용한 읽기 전용 수신부다. 낮은 revision, 잘못된 schema, 다른 Session·지역 결속을 거절하고 원천별 마지막 정상 상태를 메모리에 유지하는 범위까지만 진행한다. Coordinator·Presenter·Scene 배선은 그 이후다.

## 제외

- 실제 주문·배차·정산·기사 알림
- 외부 공공 API 재호출
- Unity phase 재계산 또는 Campaign Command 권위
- Scene·Prefab·GameObject 변경
- 실제 Play Mode·Game View·Windows 빌드 증거
- commit·push·배포
