# [기획 · 배달 플랫폼 운영 Campaign · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 절기 캠페인 Core 우선 계획 r63]

- 기준일: 2026-09-17
- 상태: `Draft / SimulationCoreFirstConfirmed / PlanningOnly / ImplementationNotStarted`
- 이전 판본: [첫 피크 중심 사건 r62](urgent-lunch-demand-driver-shortage.r62.md)
- 참조 계획: [통합 개발계획 r61](development-handoff.r61.md)

## 확정

절기·운영 캠페인의 첫 개발 방향은 **Simulation Core 우선**으로 한다.

이번 판본은 개발 순서와 경계를 정하는 계획이다. 코드·DB·API·Unity·Scene·Prefab·Goal·WI·E 단계는 만들거나 활성화하지 않는다.

## 목적

사가정역·면목제3·8동의 첫 점심 피크를 하나의 고정 시연으로만 만들지 않고, 이후 절기·지역·운영 사건을 같은 구조로 추가할 수 있는 결정적 캠페인 실행 뼈대를 준비한다.

기존 `SimulationHexagramCampaign`은 역경 이야기 전용 상태와 진행 의미를 보존한다. 배달 운영의 절기 캠페인을 그 타입에 억지로 넣거나 기존 공개 계약을 일반화하지 않는다. 재사용하는 것은 Session·revision·Preview/Confirm·Save/Replay·Local/Remote 동등성의 검증 방식이다.

## 첫 개발 단위

안정 식별자 후보는 `DEV-REGIONAL-OPERATOR-CAMPAIGN-CORE-01`로 둔다. 실제 Goal·WI 등록과 활성화는 별도 개발 수용 때 수행한다.

### 1. 공통 계약

- `CampaignDefinition`: 캠페인 종류, 규칙 판본, 허용 구간과 선택 종류.
- `CampaignSession`: session·seed·현재 구간·revision·시작/종료 상태.
- `CampaignPhase`: 잔잔한 시작, 초반, 분기 1, 중반, 분기 2, 후반 피크, 회복, 결산.
- `CampaignDecision`: Preview, Confirm, 무입력 기본값, 적용·만료·원복.
- `CampaignObservation`: 주문·기사·조리·서비스·재무 관측값과 자료 판본.
- `CampaignModifier`: 절기·제철·기상·아르카나가 제공하는 허용된 보정 후보.
- `CampaignOutcome`: 직접 결과, 비용, 회복, 다음 캠페인 이월.
- `CampaignEvidence`: 원천 사실, 검토한 대응, 게임 해석과 품질·출처.

### 2. 권위와 상태 전이

- Solo는 `LocalProcess`, Hosted는 `RemoteHost`에서 같은 Simulation Core 규칙을 실행한다.
- 모든 변경은 expected revision과 멱등 Command ID를 요구한다.
- Preview는 상태를 변경하지 않고, Confirm만 판본화된 선택을 기록한다.
- 입력이 없으면 승인된 안전 기본값으로 진행할 수 있다.
- Unity 시간·Animation·GameObject는 구간 진입이나 마감을 확정하지 않는다.
- 절기 공공자료와 운영 서버 자료는 읽기 전용 입력이며 Simulation 결과와 분리한다.

### 3. 결정성과 저장

- 같은 seed·CampaignDefinition revision·자료 revision·정책 revision이면 같은 결과를 만든다.
- Save에는 현재 구간, 선택, modifier snapshot, 관측 입력의 참조, 결과와 이월 상태를 넣는다.
- Replay는 외부 API를 다시 호출하지 않고 동결된 입력과 사건 기록을 사용한다.
- 낮은 revision, 중복 Command, 허용되지 않은 구간 전이는 명시적 오류로 거절한다.

### 4. 첫 Fixture

첫 Fixture는 r62의 `예상보다 빠른 점심 주문 증가 + 가용 기사 부족`을 사용한다.

```text
정상 준비
→ 주문 증가
→ 기사 부족과 미배차·지연 위험
→ 알림 Preview/Confirm 또는 안전 기본값
→ 필요 시 플랫폼 부담 할증 후보
→ 공급 반응과 피크 처리
→ 회복
→ 결산과 다음 구간 이월
```

정확 주문 수·기사 수·시간 배율·부족 임계값·할증 금액은 별도 Fixture Profile 승인 전 개발 기본값으로 확정하지 않는다.

## 개발 순서에서의 위치

통합 개발계획 r61의 번호는 호환을 위해 바꾸지 않는다. 다음 순서로 보완한다.

```text
DEV-REGIONAL-OPERATOR-09 지역 자료 세션·검증 캐시
→ DEV-REGIONAL-OPERATOR-10 합성 정상 배달 상태 사본
→ DEV-REGIONAL-OPERATOR-CAMPAIGN-CORE-01 공통 캠페인 Core
→ DEV-REGIONAL-OPERATOR-11 Unity 운영자 첫 화면·프롤로그
→ DEV-REGIONAL-OPERATOR-12 기사 공급 알림·피크 할증
→ DEV-REGIONAL-OPERATOR-13 피크·일·절기·13주 시간층 결속
→ DEV-REGIONAL-OPERATOR-14 아르카나·제철 운영 문맥
```

Core는 후속 콘텐츠가 의존할 상태·시간·결정 계약만 소유한다. 기사 공급 계산, 배차, 회계, 카드 효과, Unity 표현을 직접 구현하지 않는다.

## 검증 계획

1. 같은 입력의 결과 hash 결정성.
2. Preview 전후 상태 불변.
3. Confirm 멱등성과 revision 충돌 거절.
4. 허용된 phase 순서와 무입력 안전 기본값.
5. Save/Restore/Replay 결과 동일성.
6. LocalProcess와 RemoteHost 결과 계약 동등성.
7. 실제 FCM·PG·GPS·운영 DB·외부 공공 API 미호출.
8. 절기·카드가 없는 Neutral 캠페인의 완주 가능성.
9. 공공자료 원천 사실과 게임 보정 결과의 계보 분리.

## 명시적 제외

- 실제 절기 날짜 Profile과 현실 시간 배율 확정.
- 첫 아르카나 덱과 카드별 수치.
- 제철 품목이 주문률에 미치는 실제 보정값.
- Unity 화면·Scene·Game View 및 NPC 이동.
- 실제 주문·배차·알림·결제·정산 Adapter.
- 기존 `SimulationHexagramCampaign` 공개 계약 변경.

## 다음 질문 하나

공통 Campaign Core의 구간 진행 권위는 무엇으로 둘까?

1. `조건 충족 + 운영자 마감 Confirm` — 추천. 시간·목표 조건을 충족하면 마감 가능 상태가 되고 운영자가 Preview 후 다음 구간을 확정한다.
2. `시간 경과 자동 진행` — 간단하지만 운영자가 결산과 정책 영향을 읽기 전에 넘어갈 수 있다.
3. `운영자 수동 진행만` — 통제는 명확하지만 실제 시간처럼 흐르는 운영 Simulation의 자동성이 약해진다.
