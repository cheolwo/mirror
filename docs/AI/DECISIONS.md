# Ssalddel AI Shared Decisions

## 2026-09-08 관찰 동네의 맵·코드 선행, 씬 반영 후행 — Accepted

사용자 승인에 따라 현재 도형 동네 전체의 관계는 Graph Map, 위치·크기·작업점·명시 경로는 배치 맵으로 먼저 대조한다. 코드 및 자동 검증으로 준비한 변경 묶음만 후속 실제 Scene에 반영한다. 기능마다 Scene을 새로 만들거나 매번 배치·캡처를 필수로 요구하지 않는다. 기존 ID·모드·저장·배치값을 보존하며 알려진 공간 불일치는 검사 차단으로 드러낸다. 정의·생성 코드·자동 시험은 실제 E5 배치·보행·Game View 증거를 대신하지 않는다. 현행 범위와 결과는 [동네 맵 선행 정리 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/neighborhood-maps.r1.md)이 소유한다.

## 2026-09-08 공통 관찰 공간 누적·기존 모드 보존 — Accepted

사용자 승인에 따라 신규 관찰 업무는 기존 동네·시설·주체에 우선 누적한다. 기능마다 별도 공식 Scene이나 중복 시설을 만들지 않으며 기존 모드·저장 슬롯을 삭제하지 않는다. 표현 배치·카메라·선택 화면의 공통 부분을 공유하되, 로컬 Simulation과 운영 서버의 권위 상태는 분리하고 같은 객체를 두 실행 경로가 동시에 갱신하지 않게 한다. 기존 좌표 차이를 강제 이주하거나 모든 모드를 한꺼번에 실행한다는 뜻은 아니다. 단계별 업무와 기존 기능의 조화는 회귀 검증으로 확인하며, 기획·예시·도형만으로 실제 업무 완료를 선언하지 않는다. 첫 적용 범위와 남은 구현은 [마트 창고 업무 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/shared-neighborhood-warehouse.r1.md)이 소유한다.

## 2026-09-07 기존 기획 인지형 역경 스토리 정밀화 — Accepted

사용자 요청에 따라 [역경 64괘·효사 순차 학습형 스토리 기획](../Architecture/역경64괘효사순차학습기획.md) `hexagram-line-study.r2`가 아래 r1의 엄격한 제1괘 재시작 규칙을 대체한다. 기존 기획을 먼저 대조해 이미 다룬 범위를 반복하지 않고 아직 비어 있는 첫 괘부터 정밀화한다. 중천건·중지곤은 서막 씨앗을 보존한 채 이번 회차에서 건너뛰고, 수뢰둔은 승인된 육효 기획을 재사용하며, 산수몽은 기존 육효 문서를 보존하고 세부 미확정 검토를 뒤로 미룬다. 수천수는 캠페인 큰 줄기만 있고 육효가 모두 미배정이므로 현재 정밀화 커서를 수천수 초구로 옮긴다. 이 우선순위 변경은 기존 문서 삭제, 미완성 범위의 완료 소급, Runtime·WI·H·Graph Map·Evidence 변경을 뜻하지 않는다.

## 2026-09-07 역경 64괘·효사 순차 학습형 스토리 저작 — Accepted

사용자 요청에 따라 [역경 64괘·효사 순차 학습형 스토리 기획](../Architecture/역경64괘효사순차학습기획.md) `hexagram-line-study.r1`을 스토리 저작 기준으로 채택했다. 제1괘부터 제64괘까지, 각 괘의 초효부터 상효까지 원문과 의미를 공부한 뒤 게임 사건을 한 효씩 문답하는 엄격한 재시작 규칙은 위 `hexagram-line-study.r2`가 대체한다. 실제 플레이의 384개 직선 진행, 자동 WI/H/Graph Map 생성, 코드·저장 변경이나 E 승격을 뜻하지 않는 경계는 계속 유효하다.

## 2026-09-05 기획 표시명 단순화 — Accepted

사용자 승인에 따라 일반 기획 제목·목차·응답에서 관행적 `-001`을 생략하고 개정은 별도 판본으로 구분한다. 기존 내부 ID·경로·개발 계약·승인 hash는 호환용으로 보존하며 신규 일반 기획은 접미사 없이 만든다. 의미 있는 효·WI·H 순번은 유지한다. 기존 순번형 명명 지침의 신규 일반 기획 적용만 대체하며 세부 정본은 [기획 표시명과 호환 식별자](../Architecture/기획문서독립관리체계.md#기획-표시명과-호환-식별자)다.


## 2026-09-05 스토리 영감과 진행 권위 분리 — SupersededForSequentialStoryStudy

사용자 요청에 따라 [스토리 영감과 플레이 진행 분리](../Architecture/스토리영감과플레이진행분리.md) `story-inspiration.r1`을 공통 기준으로 채택했으나, 2026-09-07부터 신규 역경 스토리 공부·저작 순서는 `hexagram-line-study.r1`이 대체한다. 자유 이야기 씨앗 보존, 실제 플레이 순서와 저작 순서의 분리, 기존 PLAN·HEX·API·저장 식별자와 승인 사건의 호환 경계는 계속 유효하다.

> GPT Chat과 Codex가 공통으로 따라야 하는 장기 결정을 기록한다. 현재 진행 상황은 [CURRENT_WORK.md](CURRENT_WORK.md)에 둔다. 기존 결정을 바꿀 때는 원문을 삭제하지 않고 상태를 `Superseded`로 바꾼 뒤 대체 결정 ID를 연결한다.

## 상태 코드

- `Accepted`: 현재 적용하는 결정
- `Superseded`: 후속 결정으로 대체됨
- `Deprecated`: 더 이상 새 작업에 적용하지 않지만 호환성 때문에 기록을 유지함

## 결정 식별자 읽는 법

기존 `D-001` 형식은 작성 순서와 과거 링크를 보존하는 **전역 이력 번호**다. 분야별로 결정을 찾고 그 분야에서 몇 번째 결정인지 알 수 있도록 새 결정과 정리된 기존 결정에는 아래 별칭을 함께 기록한다.

```text
D-{주분야}-{세부주제}-{분야별 순번}
예: D-GAMEPLAY-BATTLE-PREPARATION-001
    게임플레이 / 전투 준비 / 이 분야의 첫 번째 결정
```

- 주분야는 `PLANNING`, `STORY`, `GAMEPLAY`, `PRESENTATION`, `INTERACTION`, `DATA`, `OPERATIONS`, `UNITY`처럼 결정의 첫 책임을 나타낸다.
- 세부주제는 `YODONG`, `COMBAT-RISK`, `COMBAT-COMMAND`, `BATTLE-PREPARATION`처럼 사람이 검색할 수 있는 안정된 영문 코드로 둔다.
- 분야별 순번은 같은 `주분야+세부주제` 안에서만 증가하며 세 자리로 표기한다. 대체·폐기된 번호를 재사용하거나 중간 삽입을 위해 다시 매기지 않는다.
- 한 결정에 첫 책임 별칭은 하나만 둔다. 다른 분야와의 관계는 본문과 관련 결정 링크로 표현해 같은 결정을 여러 분야 횟수에 중복 집계하지 않는다.
- 제목은 “무엇을 결정했는가”를 자연어로 계속 설명한다. 별칭만으로 의미를 축약하거나 기존 `D-###` 제목·앵커를 바꾸지 않는다.
- 전체 분야·세부주제 수와 모든 결정의 대응은 [결정 분야별 전수 색인](generated/decision-field-index.md)에서 조회한다. 이 색인은 생성기로 검증하며 직접 수정하지 않는다.
- D 번호 사이에서 실제 게임 기획·현재 문답·메인 스토리·Graph Map의 출발점을 찾을 때는 [결정 원장의 기획 시작점 읽기 안내](결정원장-기획시작점-읽기안내.md)를 먼저 본다.
- 현행 게임 기획의 본문·판본·상하위 관계는 [기획 목차](PLANNING.md)가 소유한다. 이 원장은 여러 기획에 공통 적용되는 장기 원칙과 과거 결정 이력을 보존한다.

## 빠른 색인

- [D-297 야외 모닥불 곁의 휴식에도 불의 추가 회복을 적용한다](#d-297-야외-모닥불-곁의-휴식에도-불의-추가-회복을-적용한다)
- [D-296 불은 휴식의 필수가 아니라 추가 회복 요소로 둔다](#d-296-불은-휴식의-필수가-아니라-추가-회복-요소로-둔다)
- [D-295 자연 회복은 걷기·대기 조건이 되면 별도 지연 없이 재개한다](#d-295-자연-회복은-걷기대기-조건이-되면-별도-지연-없이-재개한다)
- [D-294 위협 접근은 오두막 회복 우대만 해제하고 실제 전투·피격은 휴식을 종료한다](#d-294-위협-접근은-오두막-회복-우대만-해제하고-실제-전투피격은-휴식을-종료한다)
- [D-293 전투 중 휴식 시작을 금지하고 전투 종료 후 다시 허용한다](#d-293-전투-중-휴식-시작을-금지하고-전투-종료-후-다시-허용한다)
- [D-292 휴식은 버튼 한 번으로 시작하고 유지 입력을 요구하지 않는다](#d-292-휴식은-버튼-한-번으로-시작하고-유지-입력을-요구하지-않는다)
- [D-291 체력 회복 속도는 세 단계로 구분하고 Farm 반복 시험으로 조정한다](#d-291-체력-회복-속도는-세-단계로-구분하고-farm-반복-시험으로-조정한다)
- [D-290 휴식은 이동·작업·피격 시 즉시 중단하고 이미 회복한 체력은 유지한다](#d-290-휴식은-이동작업피격-시-즉시-중단하고-이미-회복한-체력은-유지한다)
- [D-289 제자리 휴식은 시설 없이 허용하고 안전한 오두막은 회복 효율을 높인다](#d-289-제자리-휴식은-시설-없이-허용하고-안전한-오두막은-회복-효율을-높인다)
- [D-288 자연 체력 회복은 걷기·대기 중 허용하고 노동·질주·전투 중 중단한다](#d-288-자연-체력-회복은-걷기대기-중-허용하고-노동질주전투-중-중단한다)
- [D-287 행동 체력은 자연·휴식·물품으로 회복하고 성장에 따라 최대치를 늘린다](#d-287-행동-체력은-자연휴식물품으로-회복하고-성장에-따라-최대치를-늘린다)
- [D-286 기존 WI를 Session·배치·저장에 연결하는 E5 전달을 우선한다](#d-286-기존-wi를-session배치저장에-연결하는-e5-전달을-우선한다)
- [D-284 Goal·WI 고정 WIP 상한을 없애고 실제 의존성과 변경 소유권으로 병렬 개발한다](#d-284-goalwi-고정-wip-상한을-없애고-실제-의존성과-변경-소유권으로-병렬-개발한다)
- [D-283 WI 후보 등록은 상위 분류·특화·결과 투영과 실행 행동을 구별한다](#d-283-wi-후보-등록은-상위-분류특화결과-투영과-실행-행동을-구별한다)

| 결정 범위 | 중심 주제 |
| --- | --- |
| D-001~D-026 | Unity World 기본 경계, 데이터·관점·표현 분리 |
| D-027~D-050 | 운영 서버·Simulation 서버 분리, 공공데이터와 상품·공간 근거 |
| D-051~D-085 | World Projection, 규칙·UI 대장, Synty·URP 표현 파이프라인 |
| D-086~D-113 | 타일·영역·업무 객체와 서버 파생 DB |
| D-114~D-125 | `SimulationWorldShell`, 1·3인칭, 시야 기반 L2 스트리밍 |
| D-126~D-138 | 생존·카드·팀 관전·전투·전술 분대 |
| D-139 이후 | 구조 안정화와 후속 리팩토링 결정 |

기존 문서가 참조하는 결정 제목 앵커는 유지한다. 상태를 바꾸거나 대체할 때에는 기존 본문을 삭제하지 않고 후속 결정 번호를 연결한다.

## D-001 Unity 개발 순서는 제품 버전에 종속하지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-08

Unity 구현 순서를 0.0, 0.5, 1.0 같은 제품 릴리스 번호의 순서로 정하지 않는다. 제품 버전은 공개·운영 capability의 게이트로 유지하고, Unity는 전체 도메인에 공통인 데이터·projection·interaction 계약과 검증 가능한 vertical slice의 필요 순서로 개발한다.

## D-002 Unity는 전체 도메인을 World 관점에서 통합한다

- 상태: `Accepted`
- 결정일: 2026-08-08

Unity는 특정 WebApp이나 일부 페이지의 3D 이식본이 아니다. 농장, 공공데이터, 커뮤니티, 공동 원장, 시장, 운송과 창고를 `World`, `Data`, `Object`, `Interaction`, `Simulation` 관점에서 통합하는 World Projection Client로 설계한다.

전체 도메인을 한 번에 구현한다는 뜻은 아니다. 공통 wrapper와 좁은 vertical slice를 반복해 확장한다.

## D-003 운영 상태의 최종 권위는 서버다

- 상태: `Accepted`
- 결정일: 2026-08-08

권한, 공개 범위, 실제 상태, 원장, revision과 운영 Command의 성공 여부는 서버가 결정한다. Unity animation, GameObject 상태, NPC 도착이나 local cache만으로 주문·참여·배차·검수·입출고를 확정하지 않는다.

운영 interaction은 `preview → explicit confirmation → server Command → canonical re-query → presentation update` 순서를 따른다.

## D-004 Simulation과 Operational 상태를 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-08

simulation fixture, sample과 FakePG는 실제 운영 데이터가 아니다. source type, 실행 mode, provenance와 UI 표시에서 operational data와 구분하며 운영 실패를 simulation 성공으로 숨기지 않는다.

실행 효과는 저장소 공통 기준인 `SsalddelExecution:Mode`의 `Simulation`과 `Operational` 경계를 따른다.

## D-005 Sensor는 단일 관측 projection을 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-08

Sensor는 stable ID, revision, source, 측정값·단위, 기준 시각, freshness, 판정 상태와 근거 reference를 보존하는 일반 관측 모델이다. Unity에서는 물리 장비의 상태, 표시등과 material로 표현하며 별도의 두 번째 감각 표현 모델이나 이중 projection 계약을 두지 않는다.

View가 raw 값을 임의로 재판정하지 않고 서버 또는 승인된 rule이 만든 상태를 표시한다.

## D-006 Git 저장소 문서를 AI 공용 기억으로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-08

GPT Chat 대화 기록이나 Codex 세션을 프로젝트의 유일한 기억으로 사용하지 않는다.

- `GptProjectContext.md`: 제품과 아키텍처의 공용 시작 컨텍스트
- `DECISIONS.md`: 쉽게 바꾸지 않는 장기 결정
- `CURRENT_WORK.md`: 최근 완료, 검증, 현재 작업, 다음 후보와 미해결 항목의 최신 snapshot
- `AGENTS.md`: Codex가 위 문서와 경로별 기준으로 진입하는 작업 규칙

세부 정책은 Architecture와 Version 기준 문서에 유지하고 공용 문서에는 필요한 요약과 link만 둔다.

## D-007 외부 시각 asset은 View wrapper 뒤에 둔다

- 상태: `Accepted`
- 결정일: 2026-08-08

Synty를 포함한 외부 asset은 Presentation 계층의 교체 가능한 시각 리소스다. 원본 Prefab에 Ssalddel 업무 로직을 직접 넣지 않고 `VisualRoot`를 가진 project View wrapper로 감싼다. primitive placeholder로 socket, scale, interaction과 target platform 성능을 먼저 검증한 뒤 구매·도입 범위를 정한다.

## D-008 DbSet과 Unity Controller를 1:1로 대응하지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-08

EF `DbSet`, MongoDB 원장과 외부 관측은 Unity에 표현할 현실 실체와 상태를 찾는 출발점이다. Unity가 Entity나 document를 직접 소비하지 않고, 서버가 권한과 공개 범위에 맞는 aggregate projection API를 제공한다.

Unity UseCase는 사용자 질문과 행동 단위로 만들고 SceneController는 Entity 종류가 아니라 World Zone의 상태와 과업을 기준으로 여러 UseCase를 조율한다. 관계 table, 이력과 Outbox는 독립 GameObject가 아니라 aggregate의 상태·관계·revision 또는 내부 동기화 근거로 사용한다.

## D-009 첫 Presentation vertical slice는 도심마트다

- 상태: `Accepted`
- 결정일: 2026-08-08

첫 실제 Unity Presentation 코드 단위는 도심마트 Zone으로 한다. 마트 전체 업무를 한 번에 구현하지 않고 진열대 3개, 상품상자, 가격표, 재고 상태, 출처·기준시각과 상품 선택 상세 panel까지만 연결한다.

초기 Controller↔View 계약은 `SimulatedFixture` ScreenModel로 검증하고, 이후 같은 `I도심마트조회UseCase` 경계 뒤에 Mapper·Repository와 실제 서버 snapshot을 연결한다. Controller와 View가 DTO 또는 EF Entity를 직접 해석하지 않는다.

## D-010 차량 중심 차고가 아니라 도심 물류센터를 Zone으로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-08

입고, 분류·검수, 보관, 출고 대기, 상차와 운송 인계를 묶는 상위 공간은 `도심 물류센터` Zone으로 명명한다.

`창고`, `입·출고 Dock`, `분류 Zone`, `상차 Zone`과 `차량 대기 Bay`는 물류센터 내부 또는 연결 object로 구성한다. `차고`는 차량 정비·보관이 독립 과업으로 필요할 때만 추가한다.

## D-011 Unity Presentation composition root는 VContainer를 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-08

VContainer 1.18.0을 채택하고 Zone별 `LifetimeScope`에서 UseCase, validator, View와 SceneController를 조립한다. MonoBehaviour는 `[Inject]` method injection을 사용하고 engine-independent core는 VContainer를 참조하지 않는다.

Controller 내 simulation fallback `new`와 Scene Builder의 수동 `ConfigureView` 배선은 제거한다. Simulation·Operational 구현 선택은 Controller가 아니라 LifetimeScope 등록에서 바꾸며, 향후 공통 API Client·session이 필요할 때 Application Scope → Zone child Scope로 확장한다.

## D-012 World는 공유하고 Role Perspective를 겹친다

- 상태: `Accepted`
- 결정일: 2026-08-08

생산자, 주문자와 운송자마다 별도 Scene이나 Zone을 복제하지 않는다. 농장, 시장, 주거공동체와 도심 물류센터는 같은 stable-ID 기반 World Object를 공유하고, 활성 역할에 따라 강조 정보, 상세 panel과 허용 interaction만 교체한다.

Controller는 두 축으로 분리한다.

- Zone Controller: 장소의 canonical 상태와 object 생명주기를 조율한다.
- Role Experience Controller: 서버가 승인한 역할별 질문, 강조와 행동 가능 범위를 조율한다.

Role Perspective는 클라이언트 UI 테마나 `if role` 기반 권한 필터가 아니다. Unity가 보내는 역할 선택은 조회 요청일 뿐 권한 증명이 아니며, 서버가 인증 session, 실제 역할 할당, 현재 Zone과 업무 배정을 검증한 projection만 반환한다. Unity는 그 snapshot에 포함된 object 강조와 `AllowedInteractions`만 적용하고 누락된 개인정보나 권한을 추론하지 않는다. 운영 Command는 실행 시 서버가 권한과 revision을 다시 검증한다.

## D-013 NPC 이동은 업무 상태의 Presentation이다

- 상태: `Accepted`
- 결정일: 2026-08-08

NPC는 Zone마다 별도 이동 구현을 복제하지 않는다. 공통 `NpcMovementSnapshot`, stable ID, semantic route와 waypoint 계약을 사용하고 각 Zone은 route profile과 Transform 배치만 제공한다. 서버가 일반 업무 DTO에 Unity `Vector3` 좌표를 보내지 않는다.

운영 NPC는 canonical task stable ID와 revision이 있는 서버 projection만 사용하고, simulation NPC는 `SimulatedFixture`로 구분한다. NavMeshAgent 도착과 Animator event는 표현 결과일 뿐 상차, 하차, 피킹, 검수, 배송 또는 주문 상태를 확정하지 않는다. 실제 상태 변경은 사용자 확인과 서버 Command 성공 뒤 canonical snapshot 재조회로만 반영한다.

개인 공간에는 기본적으로 자동 NPC를 두지 않는다. 다른 Zone은 농장 생산자, 마트 주문자·재고 담당, 주거공동체 주문자·분배 담당, 전통시장 상인·운송자, 물류센터 Dock 작업자·운송자, 창고 picker와 공공·협동 공간 안내 역할의 semantic route를 제공한다.

## D-014 농장 운영 aggregate와 공개 작물 기준을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-08

농장 운영의 canonical root는 소유자 경계를 가진 `농장`이며 `농장구획`, `재배작기`, `농업센서`, `농업센서관측`과 `농장작업`을 연결한다. 모든 World object와 task는 저장된 stable ID와 revision을 사용한다.

농사로 등 공개 작물 기준의 stable ID·source key는 `재배작기`가 참조할 수 있지만, 공개 분류가 실제 농장의 재배·생육·생산 상태를 뜻하지 않는다. 센서 판정은 서버가 관측값, 최신성, 판정 규칙 revision, 근거 card와 한계를 함께 저장·투영하며 Unity View는 raw value를 재판정하지 않는다.

운영 생산자 NPC는 `농장작업`의 canonical task stable ID와 semantic waypoint만 표현하고, Unity 도착이나 animation으로 작업 상태를 변경하지 않는다.

## D-015 Unity 심화 개발 단위는 Zone 업무 흐름이다

- 상태: `Accepted`
- 결정일: 2026-08-08

P0~P7의 Controller·primitive Scene·VContainer 배선 이후에는 Controller 수를 늘리는 것을 진척 기준으로 삼지 않는다. 현실 업무 하나를 canonical source, 권한 projection, Unity Snapshot, World object, NPC, interaction과 server 재조회까지 연결하는 Zone vertical slice를 기본 개발 단위로 사용한다.

게임 목표와 보상은 먼저 발명하지 않는다. 실제 업무의 시간, 대기열, 동선, 용량, 선택, 협력과 결과를 정직하게 공간화한 뒤 관찰에서 확인된 제약만 별도 simulation 또는 승인된 운영 보조 경험 후보로 만든다.

첫 심화 대상은 canonical 재고·적재·피킹·출고와 NPC 기반이 가장 준비된 창고 Zone이다. P8 협동조합·공동원장 공간은 앞 Zone 두 곳 이상에서 같은 공동 원장·역할·비용·노동·결정 요구가 확인된 뒤 만든다.

## D-016 Unity 읽기 흐름은 Data·Interpretation·Presentation을 기본으로 한다

- 상태: `Accepted`
- 결정일: 2026-08-08

Unity가 서버와 공공 source에서 받은 값을 World로 표현하는 기본 변환을 `Data → Interpretation → Presentation` 세 단계로 구분한다.

- Data: 허용된 사실, ApiModel·Mapper·Repository·Snapshot, source·단위·기준시각·정밀도와 data revision
- Interpretation: 여러 Snapshot의 관계·공간 의미·상태 분류·derived metric·simulation과 rule lineage
- Presentation: 표현 관점, Presenter·PresentationModel·SceneController·View·GameObject·NPC·UI와 visual revision

서버의 권한 관점인 `Authorized Perspective`와 Unity의 표현 관점인 `Presentation Perspective`를 분리한다. stable ID는 세 단계를 관통하고 Data, Interpretation과 Presentation revision을 서로 덮어쓰지 않는다.

Query Application은 세 단계를 조율하고 Command Application은 `preview → explicit confirmation → server Command → canonical re-query` 폐루프를 담당한다. Command 결과로 client Snapshot이나 View를 운영 성공 상태로 직접 수정하지 않는다.

현재 P0~P7 코드는 일괄 이동하지 않는다. Warehouse W1을 첫 migration pilot으로 삼아 transport mapping, 위치·관계 해석과 View 표현을 분리한 뒤 다음에 변경하는 Zone에 점진 적용한다.

## D-017 WorldState와 identity·runtime 경계를 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-08

D-016의 세 단계는 다음처럼 구체화한다. Data와 Interpretation은 Scene이 아니라 도메인 World State 기준으로 공유하며 Presentation만 Zone·surface·관점·장치에 맞게 분기한다. Interpretation 결과는 `*WorldState`로 명명하고 Unity Scene이나 View 계약과 구분한다.

`SourceStableId`, `WorldStableId`, `PresentationStableId`는 우연히 같은 문자열일 수 있어도 서로 다른 identity다. source→world→presentation lineage를 보존하고, 다대다 관계는 단일 `SourceStableId` 체인이 아니라 typed World relation graph로 표현한다.

조회 취소, initial/refresh 상태와 last-success는 Data나 View가 아니라 Application Runtime이 소유한다. Runtime은 Data 재조회, 기존 Data 재해석, 기존 World State 재투영을 구분하며 authorization scope가 바뀔 때만 기존 authorized cache를 버리고 새로 조회한다. World Presentation과 Runtime Status는 별도 channel로 전달한다.

Presentation은 순수 Projector, surface별 Reconciler, Unity Applicator로 구분한다. Projector는 GameObject를 만들지 않고 Applicator는 Data를 해석하지 않는다. PublicData, Community와 Warehouse의 기존 facade는 호환을 위해 유지하되 DIP5R에서 새 identity·graph·runtime 계약으로 점진 전환한다.

## D-018 비교 가격의 파생값은 단계간 가격차로 표현한다

- 상태: `Accepted`
- 결정일: 2026-08-08

산지·생산자 수취·도매·소매 가격은 품목, 등급, 규격, 지역, 관측기간, 통화와 단위가 비교 가능할 때만 Interpretation에서 결합한다. 원본 가격과 `단계간 가격차`는 별도 필드와 revision으로 보존한다.

운송비, 선별·포장비, 보관비, 수수료, 폐기 위험, 인건비와 실제 수익 자료가 분해되지 않은 가격차를 `유통마진`, `이익` 또는 특정 참여자의 수취액으로 표시하지 않는다. 비용 source가 확보되면 별도의 cost component WorldState와 근거 lineage로 확장한다.

가격 그래프, 가격표, 유통경로 World 표현과 DetailPanel은 같은 유통가격WorldState를 소비하는 Presentation surface다. Chart나 View가 가격차·증감률·수요 상태를 직접 계산하지 않는다.

## D-019 Interpretation은 Shared World와 Perspective 단계로 나눈다

- 상태: `Accepted`
- 결정일: 2026-08-08

역할별 Interpreter가 Data 결합과 공통 계산을 반복하지 않도록 Interpretation 내부를 `Shared World Interpretation`과 `Perspective Interpretation`으로 분리한다. Shared 단계는 상태, typed relation, spatial/route graph, constraint, candidate와 possibility를 역할과 독립적인 `SharedWorldState`로 만든다.

Perspective 단계는 같은 SharedWorldState를 `Role + Intent + Zone + Focus + Operational/Simulation mode` 문맥으로 읽어 `PerspectiveWorldState`를 만든다. Interpretation Perspective는 역할에게 상황이 갖는 의미와 허용된 다음 행동 후보를 다루고, Presentation Perspective는 그 의미를 map, chart, NPC, route와 panel로 표현한다.

Perspective 변경은 서버 authorization을 확대하지 않는다. authorization scope가 같을 때만 기존 SharedWorldState를 재사용하며, scope가 달라지면 이전 authorized cache와 selection을 제거하고 서버에서 새 Data를 조회한다.

## D-020 Data 조회는 Session·World·Authorization scope에 묶는다

- 상태: `Accepted`
- 결정일: 2026-08-08

Data Layer는 Data 종류와 transport만이 아니라 `누구를 위한 어느 World의 어떤 authorization 범위인가`를 나타내는 Data Context와 그 수명을 소유한다. Unity가 임의 UserId·Role로 권한을 만들지 않고 서버가 승인한 불투명 session, World, role·capability와 authorization revision을 입력으로 사용한다.

Data scope는 `Global`, `World`, `AuthorizedUser`, `AuthorizedUserWorld`로 구분한다. Global public cache는 World 전환과 logout에서 재사용할 수 있지만 World·authorized cache, selection과 private WorldState는 해당 context가 바뀌면 폐기한다. 동일 World 안의 표현 관점 변경만 재해석·재투영하고, Session·World·authorization·mode가 바뀌면 contextual Data query부터 다시 실행한다.

동일한 `WorldStableId`가 여러 World에 존재할 수 있으므로 `WorldObjectRef`로 `WorldContextId`와 객체 ID를 함께 참조한다. cache key는 scope·mode·dataset과 필요한 context identity로 만들고 Data revision은 entry에 별도 보존한다.

## D-021 외부·공공 데이터는 서버 수집·정규화 경계를 통과한다

- 상태: `Accepted`
- 결정일: 2026-08-08

Unity는 토양·농업 토지·인구·가격·기후·교통 등 외부 공급자를 직접 호출하지 않는다. 서버가 공급자별 credential과 wire format을 격리하고 원자료를 private storage에 보존한 뒤 source, dataset, 기준시각, 단위, 공간 정밀도, 한계와 revision이 있는 Ssalddel normalized data로 변환한다.

기존 `PublicDataApiMetadataCatalog`, server User Secrets와 private `IObjectStorageService`를 재사용한다. Source 등록과 수집 활성화는 별도 상태이며 수집은 source별 명시 설정 전까지 기본 비활성이다. credential 값은 DB, 계약, 로그와 Unity에 저장하지 않는다.

운영 provider 실패를 fixture나 simulation으로 대체하지 않는다. 동일 raw hash는 기본적으로 다시 정규화하지 않고 수집 Run과 마지막 성공 normalized data를 독립적으로 보존한다. 공급자 고유 지역 code와 DTO는 normalized 경계 밖으로 노출하지 않으며 Unity는 Ssalddel API projection만 조회한다.

## D-022 외부 공급자 단계는 계약 조사와 실제 연결을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-09

외부 데이터 provider 작업은 `P6-A 공급자 계약 조사`와 `P6-B 실제 수집 검증`으로 분리한다. P6-A는 credential 없이 공식 문서·metadata로 dataset, dimension, unit, code, 공간·시간 정밀도, 결측, license와 attribution을 조사하고 provider-independent normalized contract와 fixture를 고정한다.

P6-B에서만 필요한 credential을 server secret으로 설정하고 live call, raw object 저장, parser/normalizer와 DB lineage를 검증한다. fixture test, metadata endpoint 성공이나 Source Catalog 등록을 운영 수집 성공으로 표현하지 않는다. live 응답 field·null·오류·rate-limit은 실제 호출 전까지 확정 상태로 두지 않는다.

SoilGrids 같은 raster source는 전 세계 cell을 일반 DB row로 펼치지 않는다. bounded WCS coverage 또는 object/spatial storage를 사용하고 source mapped unit·conversion factor·depth·quantile·CRS와 model uncertainty를 보존한다.

## D-023 첫 실제 농업 공급자는 World Bank 최신 경지면적 한 건으로 제한한다

- 상태: Accepted
- 결정일: 2026-08-09

> 물리 호스트·주소·컨테이너 분리는 D-557이 대체한다. 운영과 Simulation의 상태 권위·DB·계약 경계를 분리하는 내용은 계속 유효하다.

P6-B 첫 live source는 credential이 없는 World Bank WDI `AG.LND.ARBL.HA`로 고정한다. 대한민국 `KOR`의 전 연도 시계열 대신 `mrv=1` 최신 비결측 관측 한 건만 bounded collection하고, source의 기본 collection 상태는 계속 비활성으로 둔다.

live 검증은 전용 opt-in 명령에서만 수행하며 실제 응답을 private raw object와 관계형 테스트 DB의 `Run → RawSnapshot → NormalizedRecord` 계보까지 통과시킨다. 이 로컬 검증을 운영 DB migration 적용, 운영 object storage 저장, 정기 scheduler 또는 Unity API 완료로 확대 해석하지 않는다.

## D-024 도심마트 첫 운영 업무는 진열 보충으로 3계층 migration한다

- 상태: Accepted
- 결정일: 2026-08-09

도심마트의 다음 확장은 새 UI·MVVM·asset 추가보다 현재 `ScreenModel → View` 흐름을 `Data Snapshot → Shared World Interpretation → 관리자 Perspective Interpretation → Presentation`으로 분리하는 DIP6 migration을 먼저 수행한다. 첫 업무는 위치별 재고·진열대·진행 작업 관계와 필요·후보·차단 사유를 함께 검증할 수 있는 `진열 보충`으로 제한한다.

현재 `api/v1/orderer/mart/products`의 `판매가능수량`은 주문자용 공개 투영이며 내부 보관재고·진열재고·예약재고 또는 물리 상자 수가 아니다. 이 route는 공개 상품 호환 경로로 유지하고, 마트 관리자 operational World는 별도의 authorized server Projection과 canonical 진열대·위치별 재고·작업·capability가 준비된 뒤 연결한다. 그 전의 관리자 흐름은 명시적 Simulation/read-only proof로만 제공한다.

가능 작업 해석은 후보와 차단 사유를 만들 뿐 권한이나 운영 성공을 확정하지 않는다. 실제 진열 보충은 preview·명시적 확인·server Command·revision 재검증·canonical 재조회로만 반영하고 NPC 도착이나 animation 완료를 서버 작업 완료로 취급하지 않는다. 상세 기준은 [Unity 도심마트 운영자 3계층 재정비 설계](../Architecture/UrbanMarketOperatorDataInterpretationPresentationRedesign.md)를 따른다.

## D-025 도심마트 관리자 우선순위보다 재고 할당 무결성을 먼저 보강한다

- 상태: Accepted
- 결정일: 2026-08-09

UM3의 상품별 후방재고 합산과 진열대별 활성 작업 차감은 같은 상품을 여러 진열대가 공유할 때 재고를 중복 추천할 수 있다. 따라서 UM4 관리자 Perspective보다 UM3R을 먼저 수행해 원천 재고별 `OnHand / Allocated / Available`과 모든 비종료 작업의 전역 할당을 계산한다.

작업의 단일 `SourceInventoryStableId`는 호환 facade로 유지하되 기본 계약은 여러 원천 위치의 수량을 나타내는 명시적 allocation과 `SourcePlan`으로 확장한다. 할당 초과, 알 수 없는 원천, 단위 불일치와 불완전 계획은 실행 후보가 아니라 data-attention 상태다. Operational 예약의 최종 권위는 서버이며 Unity simulation 계산을 canonical allocation으로 표현하지 않는다.

관리자 우선순위는 무결성 검증을 통과한 Shared World만 소비하고 priority reason·rule revision·source lineage를 보존한다. 판매속도나 시간창 Data가 없을 때 `곧 품절`을 추론하지 않으며, 입고·발주·유통기한·재고 차이도 각 canonical Projection이 생긴 뒤 별도 업무 slice로 확장한다.

## D-026 UM5 뒤 도심마트 공급 계약 경영 Simulation을 우선한다

- 상태: Accepted
- 결정일: 2026-08-09

UM0~UM4의 진열 보충 운영은 공급 계약 결정이 만든 하류 결과로 유지한다. UM5에서 Runtime·surface reconcile·selection·last-success 기반을 먼저 닫은 뒤, 감자 한 품목·세 공급처·4주 시나리오의 `SC0~SC7` 공급 계약 경영 Simulation을 도심마트의 다음 playable track으로 우선한다.

서버에 이미 존재하는 `플랫폼공급조건계약 → 공급계약이용등록 → 조직개별공급발주`는 operational canonical 경계다. Simulation 공급처·Offer·계약안·수요·납품·현금 원장은 공통 `SsalddelExecution:Mode=Simulation` 안의 별도 schema·stable ID로 만들며 세 번째 실행 mode를 추가하지 않는다. Simulation 확정이 실제 계약·발주·결제·입고를 생성하지 않고, Operational 연결은 SC9에서 기존 서버 권한, 조직 동의, 개별 발주 확인, expected revision, 멱등 Command와 canonical 재조회를 통과할 때만 수행한다.

공급 계약 관리 Perspective는 `ReviewReplenishment`와 별도 Intent·Interpreter를 사용한다. 4주 결과는 충족률·비용·현금·폐기·작업 부담·공급 집중도와 대응력을 독립 지표로 제공하고 하나의 이익 점수로 합치지 않는다. 수요·판매속도 해석은 명시적 Simulation 수요 시나리오 또는 운영 canonical Data가 있을 때만 수행한다. 상세 기준은 [도심마트 공급 계약 경영 Simulation 설계](../Architecture/UrbanMarketSupplyManagementSimulationDesign.md)를 따른다.

## D-027 운영 서버와 게임 Simulation 서버를 물리 분리한다

- 상태: Accepted
- 결정일: 2026-08-09

기존 `Ssalddel` 서버는 실제 사용자·조직·동의·공급계약·발주·입고·재고·결제·원장의 최종 권위를 계속 소유한다. Unity 경영 게임의 scenario, seed, session, 가상 시간, save·replay와 Simulation 전용 계약 결과는 별도 `Ssalddel.Simulation.Server`가 소유한다. 두 서버는 데이터베이스와 영속 entity를 공유하지 않으며, 공통 의미가 필요할 때도 명시적 contract·stable ID·source lineage를 통해서만 연결한다.

Simulation 서버는 공통 `SsalddelExecution:Mode=Simulation`을 사용하고 기본 설정에서는 API가 비활성이다. Simulation Command는 실제 계약·발주·결제·입고를 만들 수 없고, 운영 서버의 Domain·Infrastructure 또는 Unity assembly를 참조하지 않는다. Unity client는 operational repository와 simulation repository를 분리하며, SC9의 명시적 adapter 전에는 Simulation 상태를 운영 Command payload로 변환하지 않는다.

현재 첫 수직 슬라이스는 별도 Contracts·Domain·Server·Tests와 session 생성·조회·멱등 Tick·expected revision·scenario lineage까지만 포함한다. 영속 저장소, 인증·사용자별 session scope, 시나리오 catalog와 실제 공급계약 Engine은 후속 slice에서 추가한다.

## D-028 공급계약 Simulation 전에 지역 수요와 주문 객체를 명시한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-026을 구체화하며 대체하지 않음

도심마트 첫 Playable은 공급계약 조건만 비교하지 않고 `지역 인구·세대 공공 Data → 잠재수요 Interpretation → 명시적 Demand Scenario → synthetic Simulation 주문 → 주문 재고할당·충족 → 공급계약 판단`의 계보를 갖는다. 인구·세대수는 주문이 아니며 인구 변화율을 주문 변화율로 직접 사용하지 않는다. 상품 선택률, Simulation 점유율, 계절·요일·행사, seed와 rule revision을 수요 시나리오에 명시했을 때만 주문을 생성한다.

Simulation 주문은 Stable ID, 생성 Tick, 기한, 요청·할당·충족·미충족 수량과 상태를 가진 독립 객체다. 주문 할당은 전역 판매 가능 재고를 중복 소비하지 않아야 하며 UM3R의 진열 보충 작업 allocation과 별도 원장을 사용한다. Tick은 `주문 생성 → 현재 재고 1차 할당 → 납품·검수·입고 → 진열 보충 → 재할당 → 충족 마감 → 폐기 → 결제` 순서를 사용한다. 예정 입고는 검수·진열 capacity를 통과하기 전 현재 가용재고로 합산하지 않는다.

`DemandAndOrderBriefingSurface`는 오늘 주문, backlog, 현재 가용재고, 예정 입고, 향후 7일 Simulation 수요와 계약 공급량을 함께 표시한다. 즉시 충족, 입고 후 충족 가능, 기한 내 충족 불가를 분리하고 reason·basis·rule lineage를 보존한다. Simulation 주문 ID는 실제 주문 ID로 승격하지 않으며 Operational 주문은 기존 운영 서버의 권한·집계·억제된 canonical Projection만 사용한다. 상세 기준은 [도심마트 지역 수요·주문 Simulation 설계](../Architecture/UrbanMarketDemandOrderSimulationDesign.md)를 따른다.

## D-029 공동주택 주문자 집단은 기존 공동구매 원장과 개별 주문 집계를 재사용한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-026과 D-028을 구체화하며 대체하지 않음

공동주택은 별도 주문 도메인이 아니라 기존 자동집단의 배송권·생활권·공동수령 context다. 주민 의향은 기존 `individual-demand`, 모집과 합의는 `GroupPurchase`, 주민별 확정은 `Order`, 확정 수량 집계는 `GroupOrder` 원장을 authority로 사용한다. 공동주택 전용 주문자 집단 Entity·대표 원장·주문 원장을 만들지 않는다.

`공동구매자동집단상태코드.확정`은 모집 결과의 후속 원장 인계 승인이지 주민별 주문 확정이 아니다. 도심마트 Simulation의 `GroupConfirmedDemand`는 연결된 유효 개별 주문을 합산한 `group-order` Projection에서만 읽으며, 비구속 의향은 `GroupIntentDemand`로 별도 표시하고 hard demand에 합산하지 않는다.

공동주택 대표는 기존 공동구매 원장의 `공동구매 대표` 역할과 배송권 운영주체 context로 표현하지만 다른 주민의 주문·수량·결제·계약 권한을 얻지 않는다. 대표의 마트 문의 capability는 기존 역할 배정과 revision을 서버가 검증하는 일반화된 경계가 생긴 뒤에만 연결한다. 확정 fulfillment 뒤 공동수령은 기존 `ResidentialPickup`의 출고·운송 stable ID를 재사용한다. 상세 조사와 구현 순서는 [도심마트 공동주택 주문자 집단 통합 설계](../Architecture/UrbanMarketResidentialOrdererGroupIntegrationDesign.md)를 따른다.

## D-030 공동주택 대표의 사회적 context·업무 권한·NPC 표현을 분리한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-013과 D-029를 구체화하며 대체하지 않음

공동주택 대표는 Unity World에서 주민자치 대표, 입주자대표회의 대표 또는 관리사무소 조정자 같은 사회적 context를 가질 수 있다. 이 label은 세계와 이야기의 의미이며 주문·결제·계약 권한의 근거가 아니다. 실제 capability는 기존 `GroupPurchase` 원장의 `공동구매 대표` 역할 배정, authorization decision과 revision 검증에서만 나온다.

첫 Simulation playable에는 `ResidentialCommunityRepresentative` context와 `ResidentialGroupRepresentative` NPC actor를 명시적으로 둔다. 이동은 공통 `NpcMovementSnapshot → semantic route → waypoint → NavMeshAgent/Animator`를 재사용하고, 주거공동체 leg와 마트 상담 leg를 상위 representative visit state로 묶는다. 한 route나 snapshot에 서로 다른 Zone의 waypoint를 섞지 않는다.

대표 NPC의 이동·도착·대화·animation은 집단 수요 확인, 마트 문의, 조건 전달과 공동수령 조율 상태의 Presentation이다. NPC가 `market.manager-desk`에 도착하거나 대화를 마쳐도 주민 확인, 문의 제출, 주문, 계약, 발주 또는 수령 완료가 자동 실행되지 않는다. 첫 playable의 마트 관리자는 플레이어이며 자동 시연에서 점장 NPC를 표시해도 동일 Perspective의 표현일 뿐 별도 권한자가 아니다. 상세 계약과 RG1·RG4-NPC 순서는 [도심마트 공동주택 주문자 집단 통합 설계](../Architecture/UrbanMarketResidentialOrdererGroupIntegrationDesign.md)를 따른다.

## D-031 Unity 업무 학습은 공통 Concept Card Presentation 문법으로 제공한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-016~D-020과 D-030을 구체화하며 대체하지 않음

Unity World에서 낯선 업무 개념을 설명하는 Presentation은 `Concept`, `Status`, `Reason`, `Action` 네 종류의 공통 카드 문법을 사용한다. 개념 정의, 현재 상태, 판단 근거와 가능한 행동은 서로 다른 책임이며 하나의 정보 panel이나 단일 점수로 합치지 않는다.

계층은 `Perspective WorldState → Learning Card Projector → ConceptCardDeckPresentationModel → ConceptCardView → VisualSkinAdapter`로 둔다. Synty INTERFACE 등 외부 UI asset은 교체 가능한 visual skin이며 업무 의미, stable ID, source lineage, revision, capability를 소유하지 않는다. View와 prefab은 서버·Simulation 값을 다시 계산하지 않는다.

Action Card는 권한을 만들거나 즉시 Command를 실행하지 않는다. 카드 클릭, NPC 도착, 대화와 animation은 Presentation Event이며 Operational 행동은 `Preview → 명시적 확인 → 기존 server Command → canonical 재조회`를 유지한다. API 실패를 Simulation fixture로 대체하지 않고 역할·권한이 바뀌면 비공개 deck과 기존 선택을 제거한다.

첫 vertical slice는 공동주택 대표 NPC를 anchor로 의향 수요, 확정 수요, 공동수령, 공급 상태, 부족 근거와 공급 검토 행동을 연결한다. 도심마트 이후 농장, 가격, 물류, 공동구매와 공공데이터도 같은 카드 문법을 사용하되 각 도메인의 Interpretation과 권한 경계를 보존한다. 상세 기준은 [Unity 개념 카드 Presentation 패턴](../Architecture/UnityConceptCardPresentationPattern.md)을 따른다.

## D-032 도심마트 관리자 30초 업무 Queue와 우선순위 점수를 제거한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-025의 재고 할당 무결성 우선 결정은 유지하고 관리자 priority·queue 부분만 대체함

현재 Data에는 판매속도, 업무 기한, SLA, 담당자와 지연시간처럼 관리자 업무의 실제 긴급도를 판정할 근거가 충분하지 않다. 따라서 UM4는 `UrgentActions / PendingActions / InProgress / DataAttention` 30초 queue, `PriorityScore`, priority reason과 manager summary surface를 만들지 않는다.

`마트관리자PerspectiveWorldState`는 Shared World의 모든 진열 상태를 Stable ID 결정적 순서로 보존하고 `NeedCode`, 차단 사유, 허용 interaction, SourcePlan, focus 관계, rule revision과 source lineage만 전달한다. Stable ID 순서는 업무 우선순위가 아니다. 실제 우선순위는 authoritative 운영 Data와 명시적 rule이 추가될 때 별도 Interpretation으로 설계한다.

## D-033 Farm·Town·City를 독립 Presentation Region으로 구성하고 이동망으로 연결한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: 기존 WORLD-1~WORLD-5의 공급망 시각 기반을 확장하며 canonical 업무 Zone 결정을 대체하지 않음

Farm·Town·City를 하나의 선형 Transition에 종속시키지 않고 각각 독립적으로 발전할 수 있는 Macro Presentation Region으로 구성한다. 기본 Map은 Farm 북서, City 북동, Town 남쪽 중앙의 삼각형 배치를 사용하고 Farm↔Town 농촌 생활도로, Town↔City 지역 간선도로, Farm↔City 농산물 화물회랑으로 연결한다. 정확한 좌표·거리와 footprint는 prefab·camera·NavMesh 실측 뒤 확정한다.

각 Region은 내부 Composition, route, focus anchor와 바깥쪽 expansion socket을 독립적으로 소유한다. 서로 마주보는 안쪽 면에는 안정된 Gate signature를 두며 Region Scene은 다른 Region 내부 object를 직접 참조하지 않는다. 초기에는 하나의 Integration Scene에서 분리된 root로 검증하고, Gate·Route 계약이 안정된 뒤 World Shell과 Farm·Town·City additive Scene으로 분리할 수 있다. Region은 Presentation 경계이므로 Town 표시만으로 운영 서버에 새 Town Entity·실제 주민 주소·canonical Zone을 만들지 않는다.

사람·차량 이동은 stable ID·revision·source lineage를 가진 Stateful Journey와 업무 의미가 없는 Ambient Traffic으로 분리한다. 기존 Cargo Journey는 Farm↔City 화물회랑에 재사용하고 대표 주민·농부·배송 actor는 명시된 Gate와 semantic route를 따라 이동한다. NPC·차량의 출발·도착·animation은 주문·계약·입고·검수·수령 또는 농작업 완료를 자동 실행하지 않는다. 세부 기준은 [Farm·Town·City 3개 독립 Region Map 구성 설계](../Architecture/UnityFarmTownCityThreeRegionMapLayoutDesign.md)를 따른다.

## D-034 Town과 City 사이에 다중 origin 지역 물류허브를 둔다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-033의 세 독립 Region 원칙은 유지하고 기본 화물 topology의 Farm↔City 직통 회랑 부분을 대체함

Farm·Town·City는 계속 독립적으로 발전하는 세 주 Region으로 유지한다. 화물망은 Town과 City 사이에 `Regional Logistics Hub`를 두는 hub-and-spoke 구조를 기본으로 한다. 여러 Farm의 농산물과 여러 Town의 지역 집배송 화물은 origin별 Gate·Route로 Hub에 들어오고, 입고·검수·보관·분류와 명시적 outbound allocation을 거친 뒤 City 또는 다른 destination으로 재출하된다. Farm→City 직송은 명시적 계약·긴급·대체 route가 있을 때만 허용하는 예외이며 첫 구현의 기본 경로가 아니다.

Hub는 네 번째 생활권이나 실제 행정구역이 아니라 기존 Urban Logistics Center·Warehouse canonical Projection을 표시하는 공유 운영 Area다. cargo 차량의 Hub 도착은 입고 완료가 아니고 Dock·검수 animation은 검사 통과가 아니며, Hub 출발은 City 입고·마트 재고 반영을 의미하지 않는다. 여러 origin lot의 통합은 ProductStableId, 단위, 품질·보관 호환성, accepted available quantity, allocation과 source lineage를 명시적으로 검증해야 한다.

Town↔City 주민·대표·Bus 이동은 Hub freight yard를 통과하지 않는 passenger route로 분리한다. Farm↔Town 직판·생활 이동도 유지한다. 세부 Map, 다중 origin, inbound·outbound와 직송 예외 기준은 [Farm·Town·City 지역 물류허브 Map·Flow 설계](../Architecture/UnityFarmTownCityRegionalLogisticsHubDesign.md)를 따른다.

## D-035 Synty animation은 실제 source를 검증하고 공용 Presentation adapter로 사용한다

- 상태: Accepted
- 결정일: 2026-08-09
- 관계: D-007 외부 시각 asset 경계와 D-013 NPC 이동 Presentation 원칙을 구체화함

현재 import된 Synty Farm·Town·City character는 Humanoid rig를 제공하지만 standalone·embedded animation clip은 확인되지 않았다. Town character prefab 8개의 Animator Controller 참조도 대응 asset을 찾지 못했으므로 사용 가능한 Synty animation으로 간주하지 않는다. 이후 실제 Synty clip이 확인되면 우선 사용하되, 현재는 검증된 Humanoid clip 리타기팅과 차량·설비의 절차형 동작, 명시적 fallback을 사용한다. `SyntyProvided`, `Retargeted`, `Procedural`, `Fallback` source를 catalog에서 구분하고 vendor prefab은 직접 수정하지 않는다.

Animation은 canonical 또는 Simulation state가 만든 `AnimationIntent`를 표현할 뿐이다. NavMesh·route follower가 Journey 위치를 소유하고 root motion은 기본적으로 끄며, NPC·차량 도착·animation event·FX 완료는 Command·Tick·입고·검수·판매·수령을 확정하지 않는다. 구현은 Map·Gate 실측 뒤 공용 source validator와 Idle/Walk adapter부터 시작하고 Zone별 작업 동작은 해당 vertical slice에서 한 종류씩 추가한다. 세부 기준은 [Synty Animation·FX 재사용과 리타기팅 설계](../Architecture/UnitySyntyAnimationReuseAndRetargetDesign.md)를 따른다.

## D-036 공통 상품 stable ID와 출처별 품목코드를 분리한다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-021의 공공데이터 정규화 경계와 감자생산유통 World를 구체화하며 기존 외부 코드를 대체하지 않음

KAMIS 품목코드, 국제 HS·한국 HSK, USDA AMS `Commodity`, 농사로 품목구분Code는 서로 다른 기관·분류 목적의 코드다. 이들을 같은 값으로 변환하거나 한 외부 코드를 Ssalddel 도메인 authority로 사용하지 않고, 서버 내부 `CanonicalProductStableId` 아래에 출처별 code relation을 둔다.

relation은 `Confirmed`, `Candidate`, `Unlinked`를 구분하고 source key, code scheme, code, 상위 code, label, match quality와 근거를 함께 보존한다. `Candidate`는 검색·표시 후보일 뿐 가격 직접 비교, 세관 신고, 계약, 재고 또는 운영 상태 확정에 사용할 수 없다. 공식 코드 근거가 없으면 이름으로 추정하지 않고 `Unlinked`로 남긴다.

첫 항목은 기존 `product:potato`를 재사용한다. KAMIS 식량작물 `100/152`는 확인된 source relation, HS4 `0701`과 USDA AMS `Potatoes`는 후보 relation이며 농사로 품목구분Code는 공식 근거를 확인할 때까지 미연결이다. `공통식품품목Identity`, `공통식품품목Code관계`, `공통식품품목Code관계검토이력`을 별도 영속 원장으로 두고 공개 API는 이 DB projection만 읽는다. 코드 catalog는 초기 seed와 World 상수의 bootstrap 근거로만 남기며 다품목 추가·관계 승격은 revision과 검토 이력을 함께 기록해야 한다.

기존 공공데이터 전수 대조는 KAMIS 분류·품목코드를 정렬 기준으로 사용할 수 있지만 이를 canonical 상품 생성 권한으로 사용하지 않는다. HS·AMS 후보가 있어도 내부 상품이 없으면 `CandidateOnly`, 후보가 없으면 `Unmapped`, 같은 KAMIS code에 이름 충돌이 있으면 `SourceConflict`로 둔다. Preview는 read-only이며 새 상품과 relation의 생성·승격은 별도 검토 Command에서만 수행한다.

## D-037 다품목 승격과 Farm asset 대응을 별도 검토 축으로 유지한다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-036의 다품목 추가 절차와 D-007의 외부 시각 asset 경계를 구체화함

다품목 확대는 현재 관측을 다시 계산한 `PreviewHash`와 명시적 확인 주체가 일치하는 maintenance Command에서만 수행한다. Ssalddel이 발급하는 새 식별자는 `product:food:{KAMIS분류Code}:{KAMIS품목Code}` 형식을 사용하지만, 이 형식은 생성 시점의 충돌 없는 원천 key를 보존하기 위한 내부 식별 규칙일 뿐 KAMIS가 상품 업무의 최종 권위라는 뜻은 아니다. 재실행은 이미 Confirmed인 KAMIS 관계를 기준으로 멱등이어야 한다.

승격 시 KAMIS 분류·품목 관계만 `Confirmed`로 기록한다. HS와 USDA AMS의 복수 후보는 각 관계를 `Candidate`로 보존하고, 농사로는 공식 품목구분Code crosswalk가 없으면 `Unlinked`로 둔다. 한 품목에 여러 HS·AMS 후보가 존재할 수 있으므로 원장은 source별 단일 관계를 강제하지 않고 relation stable ID와 검토 이력으로 각각 식별한다.

Unity의 Farm asset 대응은 이 source-code 검토와 별도다. 모든 canonical 상품을 `Direct`, `Representative`, `Unmapped` 중 하나로 분류하고 `CanonicalProductStableId → semantic VisualKey → prefab reference` 순서로 연결한다. `Representative`는 유사 외형일 뿐 동일 품종·가격·HS 관계를 뜻하지 않으며, `Unmapped`는 Wheat·Potato·Onion 같은 다른 prefab으로 임의 대체하지 않는다. prefab 이름과 `Assets/` 경로는 서버 DTO·Domain·Simulation에 들어가지 않는다.

## D-038 정착지 경영·분쟁 Simulation은 공통 World와 경제 인과를 먼저 닫는다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-027, D-033, D-036~D-037을 유지하며 Unity 구현 우선순위를 재정렬함

현재 FARM-3, HARVEST-CHOICE-1, COOP-1, DIRECT-1, CARGO-1, JOURNEY-1과 Hub Lot 흐름을 폐기하지 않고 정착지 경제·군량·침공으로 확장한다. 다만 판로 분기나 전투 Scene을 계속 늘리기 전에 `SIM-WORLD-0 → DECISION-WORK-0 → SAVE-REPLAY-0 → SETTLEMENT-CORE-1` 순서로 세력·영지·정착지·공통 세계시간과 Decision·Task·Effect 원장을 먼저 만든다.

영지·군단·침공은 기존 운영 제품에 추가되는 운영 상태가 아니라 `Ssalddel.Simulation.Server`가 소유하는 별도 Simulation World다. 현실 사용자·조직·동의·주문·계약·결제와 DB를 공유하지 않으며 게임 Command가 운영 효과를 만들지 않는다. 현재 `Ssalddel.Unity`의 농장·판로·Cargo engine은 검증된 실행 명세로 보존하되, 장기 World authority는 Simulation.Contracts·Domain·Server로 점진 이관한다.

도메인 의미는 시대 중립적으로 유지하고 온라인 마켓·수출대행·영주·성·상단 같은 용어와 외형은 scenario presentation profile에서 표현한다. 첫 필수 playable은 군단이나 공성전이 아니라 감자 300kg 판로가 정착지 재정·노동·시장 공급·비축과 식량 안전 일수를 실제로 바꾸고 원인 Decision까지 역추적되는 경제 폐루프다. 상세 순서와 Gate는 [Unity 실시간 정착지 경제·영지 경영·분쟁 Simulation 재정렬 제안서](../Architecture/UnityRealtimeTerritoryManagementConflictSimulationProposal.md)를 따른다.

## D-039 Simulation save는 versioned package와 Command replay로 검증한다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-038의 `SAVE-REPLAY-0` 저장·복원 경계를 구체화함

Simulation save는 `simulation-save.v1` schema를 가진 package로 저장한다. package는 session 생성 요청, 저장 시점 snapshot, 순서화된 Confirm/Tick Command log, hash algorithm과 replay hash를 함께 보존한다. snapshot은 빠른 조회와 검증 자료이며 복원 권위로 직접 주입하지 않는다. 새 aggregate에 같은 seed·scenario revision·rule revision과 Command를 재실행하고 package 자체 hash와 replay 결과 hash가 모두 일치할 때만 session store에 등록한다.

Command log는 session 내부 적용 순서를 보존하며 멱등 재시도는 새 항목을 추가하지 않는다. schema 불일치, sequence·payload·hash 변조, replay 결과 위치 불일치와 이미 활성인 session 덮어쓰기는 거부한다. 실패한 restore는 부분 session을 등록하지 않는다.

현재 `InMemorySimulationSessionSaveStore`는 restore port의 기본 개발 adapter일 뿐 process restart를 넘는 durable store가 아니다. 실제 연속 플레이 저장은 별도 외부 adapter, schema migration, 동시성·보존·백업 정책을 통과한 뒤 활성화한다. Simulation save는 운영 서버 DB·계약·주문·결제 원장과 공유하지 않는다.

## D-040 정착지 초기 경제는 scenario 입력과 독립 원장 지표로 구성한다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-038의 `SETTLEMENT-CORE-1`과 D-039 save/replay 입력 경계를 구체화함

정착지 초기 경제는 Unity Scene, 상자·NPC 수, 건물 크기나 운영 서버 상태에서 추정하지 않는다. Simulation session 생성 시 scenario가 명시적으로 제공한 District·Facility graph, 재정, 노동 capacity, storage, 상품별 시장 공급, 비축 StockLot, 주민·주둔군 수요와 source stable ID를 사용한다. 기존 session 생성 호환성을 위해 정착지 입력은 선택적이지만, 제공된 입력은 session 생성 멱등 payload와 save/replay hash에 포함한다.

재정·노동·창고·시장 공급·비축·식량 수요는 하나의 종합 점수로 합치지 않는다. `LaborAvailable = Total - Reserved`, `StorageAvailable = Capacity - Occupied`를 보존하고 비축 Lot 합계는 occupied storage를 초과할 수 없다. Facility는 존재하는 District를 참조하고 비축 Lot은 `Storage` Facility만 참조한다. 상품별 시장 공급과 StockLot stable ID는 중복될 수 없다.

`FoodSecurityDays`는 `판매·수출 예약을 제외한 FoodEquivalent / (PopulationDemandPerTick + GarrisonDemandPerTick)`으로 계산한다. FoodEquivalent unit과 rule revision을 반드시 보존하며 첫 값은 현실 영양 처방이 아닌 Simulation Fixture다. `SETTLEMENT-CORE-1`의 값은 초기 snapshot과 active Task projection까지만 제공하고, 실제 Decision Effect에 따른 재정·노동·재고 변경과 Lot 중복 배정 차단은 `SETTLEMENT-ECONOMY-1`에서 수행한다.

## D-041 수확 판로 영향과 비축은 서버 계산 후보로 먼저 연결한다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-038의 `HARVEST-IMPACT-1 + STORAGE-1`을 구현하고 D-040의 실제 경제 적용 경계를 유지함

기존 Unity 수확 판로의 `CooperativeShipment`, `DirectOnlineSale`, `ExportAgent` choice code와 `harvest-disposition:sim.potato.20260407.r1` stable ID를 변경하지 않는다. Simulation 서버는 같은 HarvestLot·판로 결정 stable ID와 revision을 받아 비용, 노동, 기간, 예상 수입, 시장·비축 영향, 위험과 차단 사유를 `harvest-impact:fixture-r1` 정책으로 다시 계산한다. Unity가 계산한 예상값을 Confirm payload의 권위로 받지 않으며, 판로 추천 점수나 자동 선택도 만들지 않는다.

네 번째 선택 `ReserveStorage`는 2% Fixture 감모, 창고 capacity, 같은 상품의 기존 비축 Lot에서 얻은 FoodEquivalent 환산 근거와 rule revision을 사용해 `ReserveStockLotCandidate`와 `FoodSecurityDaysCandidate`를 만든다. 보관은 즉시 군량이 아니며 실제 StockLot, 창고 점유, 재정, 노동, 시장 공급과 식량 안전 일수는 이 단계에서 변경하지 않는다.

Preview는 어떤 session 원장도 바꾸지 않는다. Confirm은 공통 `Decision → Task → Effect` 원장에 후보만 기록하고 Tick 완료는 Effect record를 `Applied`로 전이할 뿐 정착지 경제 값을 적용하지 않는다. 같은 300kg의 실제 allocation과 중복 배정 차단, 비용·수입·재고 반영은 다음 `SETTLEMENT-ECONOMY-1`에서 하나의 transaction 경계로 구현한다. 현재 HarvestLot 관계는 Simulation import 계약과 source-revision lineage로 보존하며 운영 수확 원장이나 실제 거래를 증명하지 않는다.

## D-042 World Map과 정착지 내부는 같은 Simulation snapshot의 관찰 규모다

- 상태: Accepted
- 결정일: 2026-08-10
- 관계: D-027·D-038~D-041의 Simulation 권위를 유지하며 Unity Presentation의 장기 Scene 골격과 구현 순서를 구체화함

World Map, Settlement Interior, District, Object와 향후 Conflict View는 서로 다른 save나 독립 Simulation이 아니다. 모두 같은 `SimulationSession`, `WorldTick`, `WorldRevision`, `SettlementStableId`, Task·Effect·Stock·Treasury·Labor snapshot을 다른 관찰 규모에서 표현한다. Scene 또는 Presentation root를 전환해도 session·시간·재고를 다시 만들지 않으며 GameObject와 prefab은 canonical state를 소유하지 않는다.

다음 서버 권위 Gate는 계속 `SETTLEMENT-ECONOMY-1`이다. 다만 개별 기능 Scene의 추가가 장기 구조를 고착시키기 전에 `WORLD-SHELL-0 → SETTLEMENT-SCENE-0`을 하나의 읽기 전용 Presentation milestone으로 먼저 수행한다. 두 단계는 WorldMap·Settlement root, 공통 카메라·HUD·stable-ID 선택, 첫 정착지 District socket과 blockout만 만들며 경제 계산, Preview·Confirm, Tick 자동 진행, NPC·차량 기반 완료와 분쟁 기능을 포함하지 않는다. 완료 뒤 시각 확장을 중단하고 `SETTLEMENT-ECONOMY-1`로 복귀한다.

## D-043 수확 판로 Confirm은 capacity 예약이고 Task 완료 Tick은 경제 원장 적용이다

- 상태: 확정
- 결정일: 2026-08-10
- 대체 범위: D-041의 "Confirm/Tick은 후보 Effect만 기록하고 정착지 경제를 변경하지 않는다"는 임시 경계

수확 판로 Preview는 계속 무변경 후보 계산이다. Confirm은 하나의 `HarvestLotStableId`에 하나의 `HarvestLotAllocation`만 허용하고, `Labor`, `Treasury`, 비축의 `Storage` capacity를 예약한다. Confirm 자체는 잔액·시장 공급·비축 Stock Lot을 확정 변경하지 않는다.

해당 Task의 완료 Tick에서 aggregate가 예약을 해제하고 비용과 Simulation 수입, 직판 시장 공급 또는 감모 후 비축 Stock Lot·FoodEquivalent를 같은 lock 안에서 반영한다. Allocation과 Effect는 함께 Applied로 전이한다. 같은 HarvestLot은 Applied 뒤에도 다른 판로에 중복 배정할 수 없다. 수확 판로 Confirm은 일반 DecisionConfirm으로 축약하지 않고 전용 Command payload를 save/replay하여 정책 입력과 lineage를 복원한다.

이 원장은 Simulation 전용이며 실제 판매·계약·수출·입고·정산을 만들지 않는다. Unity의 NPC 도착, 차량 animation, 상자 Renderer 수는 완료와 수량의 권위가 아니다.

## D-044 World navigation은 상위 선택을 보존하고 하위 선택만 해제한다

- 상태: 확정
- 결정일: 2026-08-10

`World Map → Settlement → District → Object`는 하나의 Simulation snapshot을 다른 관찰 규모로 보여주는 Presentation navigation이다. 이동 중 session, Tick, revision과 경제 원장을 재생성하거나 변경하지 않는다.

`Back`은 `Object → District → Settlement → World Map` 순서로 한 단계씩 이동한다. Object에서 Back하면 Object 선택만, District에서 Back하면 District 이하 선택만 해제한다. World Map으로 돌아가도 최근 Settlement 선택은 context로 보존하되, 다음 snapshot에서 해당 stable ID가 사라지거나 session이 바뀌면 기존 D-042 규칙에 따라 유효하지 않은 선택을 해제한다.

카메라 World/Zone/Object focus, 선택 강조와 breadcrumb는 Presentation이다. Collider나 클릭 target은 stable ID를 전달할 뿐 Simulation Command를 발행하지 않으며, 차량·NPC 도착과 Renderer 수는 Task 완료나 수량을 확정하지 않는다.

기존 공공데이터 `WorldBootstrapScene`은 공개지도 surface로 유지하고 Simulation World Shell로 암묵적으로 재사용하지 않는다. 신규 `SimulationWorldShell`은 첫 버전에서 하나의 Scene 안의 `WorldMapRoot`와 `SettlementInteriorRoot`를 전환해 동일 snapshot 보존을 검증한다. District는 Presentation socket이며 새로운 서버 Entity나 전용 manager가 아니다. 상세 hierarchy, 선택 규칙, 합류 순서와 완료 Gate는 [Unity Simulation World Shell·정착지 Scene 기반 재정렬 제안서](../Architecture/UnityWorldShellSettlementSceneFoundationProposal.md)를 따른다.

## D-045 Synty 에셋은 자동 원본 목록과 사람의 연구 기록을 분리해 승격한다

- 상태: 확정
- 결정일: 2026-08-10

Farm·Town·City의 Synty Prefab은 곧바로 업무 의미나 `VisualKey`가 되지 않는다. Editor가 GUID·원본 이름·경로·묶음·분류·Prefab 참조를 `에셋원본Index`로 자동 색인하고, 사람이 관찰한 사실·현실 의미·월드 역할 후보·함께 둘 에셋·연결할 자료 후보·승격 후보를 별도 `에셋연구Catalog`에 기록한다. 자동 재색인은 연구 기록을 덮어쓰지 않는다.

연구 상태는 `미검토 → 관찰됨 → 해석됨 → 장소 검증됨 → 체계 검증됨 → 월드 목록 승격`으로 구분한다. `해석됨`은 후보 의미일 뿐 실제 생산량·재고·운영 상태·공공데이터 연결을 증명하지 않는다. 장소와 작은 Simulation 검증을 통과한 항목만 semantic `VisualKey` 후보로 승격하며, 원본 Prefab 이름과 `Assets/` 경로는 Domain·서버 DTO·Simulation stable ID가 되지 않는다.

`신티에셋연구소`는 한 번에 모든 Prefab을 생성하지 않고 묶음·분류별 12개씩 전시한다. 표본 선택은 카메라 초점·선택 강조·연구 카드만 바꾸며 Command, Tick, 운영 저장을 실행하지 않는다. 첫 연구 항목은 Farm의 `온실 01`이고 `farm.facility.greenhouse`는 아직 승격 후보로만 보존한다.

## D-046 Unity 판로 adapter는 서버 Preview 입력과 후보 Task 의미만 구성한다

- 상태: 확정
- 결정일: 2026-08-10

Unity의 수확 판로는 `CooperativeShipment`, `DirectOnlineSale`, `ExportAgent`, `ReserveStorage` 네 choice와 각각의 `CooperativeIntakeCandidate`, `ProducerPackingCandidate`, `ExportReadinessCandidate`, `ReserveStockLotCandidate` workflow code를 서버 계약과 동일하게 사용한다. adapter는 기존 `HarvestDispositionDecision`·`HarvestLot`의 stable ID, revision, 상품, 수량, 단위와 source lineage를 서버 Preview 입력 형태로 보존한다.

adapter가 제시하는 `task:harvest-impact:{DispositionDecisionStableId}`, `{ChoiceCode}Work`, input Lot과 output candidate는 아직 후보 의미다. Unity는 비용, 노동, 기간, 시설, 예상 수입, 감모, FoodSecurityDays, block reason과 Effect를 계산하지 않는다. 서버가 현재 session과 정책 revision으로 다시 Preview하고 사용자가 명시적으로 Confirm한 뒤에만 authoritative Decision·Task 예약이 생기며, 완료 Tick만 D-043의 정착지 경제 원장을 변경한다.

기존 조합 인수·직판 포장·Cargo 준비 구현은 폐기하지 않고 후속 workflow의 Presentation/fixture 명세로 보존한다. adapter 생성만으로 실제 조합 출하, 상품 게시, 수출, 창고 입고, Cargo, 판매 또는 정산이 발생했다고 표시하지 않는다.

## D-047 Unity 연구 Scene 파일명은 한국어 목적 이름을 사용한다

- 상태: 확정
- 결정일: 2026-08-10

`Assets/Ssalddel/Experiments - 연구` 아래 Scene은 사람이 Project 창에서 바로 목적을 알아볼 수 있도록 장소·흐름·검증 목적을 한국어 파일명으로 표현한다. 예를 들어 `PotatoHubReceivingLifecycle`은 `감자물류거점입고검수흐름`, `UrbanMarketCityPackVerticalSlice`는 `도심마트도시팩적용연구`로 표시한다. 기술 단계명, class, namespace와 외부 contract 식별자는 호환성과 코드 탐색을 위해 필요한 경우 영어를 유지할 수 있지만 Scene 파일명에 그대로 노출할 필요는 없다.

Scene 이름 변경은 Unity `AssetDatabase`를 통해 `.unity`와 `.meta`를 함께 이동하여 GUID를 보존한다. Builder, Test, 문서와 Build Settings 등 경로 소비자는 같은 변경에서 새 경로로 갱신하고, 모든 연구 Scene을 실제로 열어 유효성과 이름 일치를 검증한다.

한국어 Scene 파일명은 개발자 탐색을 돕는 Presentation 이름이다. Domain stable ID, 서버 계약, Simulation 권위와 운영 상태를 대신하지 않는다.

## D-048 정착지 1차 미술은 semantic VisualKey와 고정 Presentation 시간으로 구성한다

- 상태: 확정
- 결정일: 2026-08-10

`SimulationWorldShell`의 Farm, Town, Market, Storage, Logistics, Residential는 기존 District socket과 navigation target을 유지한 채 Farm/Urban/Environment catalog의 semantic `VisualKey`로 Synty prefab을 연결한다. Vendor prefab 이름과 경로는 Domain·서버 DTO·Simulation stable ID가 아니며 `WorldVisualInstanceView`와 `VisualRoot` 뒤에 둔다.

첫 시각 기준은 실제 경제량을 Renderer나 상자 수로 재현하는 것이 아니라 한 Overview에서 생산·저장·운반·판매·생활의 공간 관계를 읽게 하는 것이다. Gate와 Garrison은 후속 기능을 위한 Presentation placeholder이며 존재만으로 방어 capability나 주둔 상태를 만들지 않는다.

시간대는 기존 시간 Presentation을 오후 15:00 고정값으로 재사용한다. 자동 순환은 끄고 Simulation `WorldTick`, `WorldRevision`, `GameDate`, Task 완료를 변경하지 않는다. 향후 Day/Night가 Simulation 시간과 연동되더라도 authoritative snapshot을 입력으로 받아 표현할 뿐 Scene presenter가 시간을 진행시키지 않는다.

## D-049 Unity 정착지 상호작용은 Simulation authority 응답만 reconcile한다

- 상태: 확정
- 결정일: 2026-08-10

HarvestLot action card는 `CooperativeShipment`, `DirectOnlineSale`, `ReserveStorage`, `ExportAgent` 네 choice를 보여주지만 비용·노동·기간·수입·감모·FoodSecurityDays와 Effect를 Unity에서 결정하지 않는다. Production client는 공식 Simulation session 조회, harvest disposition impact Preview·Confirm, Tick API를 expected revision과 함께 호출하고 응답 snapshot만 WorldShell HUD와 카드에 reconcile한다.

Preview는 session revision과 WorldTick을 바꾸지 않아야 한다. Confirm은 allocation과 재정·노동·storage capacity 예약까지만 표시하고, 명시적인 완료 Tick 응답에서만 allocation·Task·Effect Applied와 정착지 경제 변경을 표시한다. UI 버튼, 차량·NPC animation, Renderer와 상자 수는 완료 근거가 아니다.

Game View 검증용 `SimulationFixtureAuthority`는 운영 실패 fallback이 아닌 test double이다. Production HTTP client와 같은 경계를 재현하지만 실제 판매·배송·수출·계약·정산을 만들지 않으며 HUD에 fixture임을 표시한다. 실제 실행 서버 live 호출은 별도 검증 사실로 보고한다.

## D-050 Cargo 이동은 공통 WorldTick Task와 원재고 예약을 함께 보존한다

- 상태: 확정
- 결정일: 2026-08-11

기존 CARGO-1/JOURNEY-1의 Cargo stable ID, HarvestLot·PackageLot lineage와 route는 폐기하지 않고 서버 Simulation session의 물류 이동 계약으로 적응한다. Preview는 후보만 반환하고 Confirm은 원천 HarvestLot allocation 수량을 예약한 뒤 공통 Decision·Task·Effect를 생성한다. 출발·진행·도착은 차량 animation이나 Renderer 위치가 아니라 공통 WorldTick 응답으로만 확정한다.

도착은 destination stock candidate를 만들 뿐 Hub 검수·입고를 자동 확정하지 않는다. 같은 원천 allocation을 다른 Cargo에 중복 배정할 수 없으며 이동 중 Cargo 상태와 예약량은 save/replay hash에 포함한다. Unity fixture authority는 Game View test double이고 운영 실패 fallback이 아니다.

## D-051 Unity C# 이름은 한국어 업무 의미와 영어 기술 역할을 조합한다

- 상태: 확정
- 결정일: 2026-08-11

Unity의 C# 파일명, type, method와 test 이름은 사람이 업무 목적을 바로 이해할 수 있도록 한국어 업무 의미를 우선한다. `물류이동Presenter`, `정착지상호작용AuthorityRepository`, `물류이동Tests`처럼 업무·도메인 의미는 한국어로 쓰고 `MonoBehaviour`, `Presenter`, `Repository`, `Authority`, `Snapshot`, `Preview`, `Confirm`, `Task`, `StableId` 같은 기술 역할·공통 계약 용어는 영어를 유지할 수 있다.

이 규칙은 외부 호환 경계를 깨지 않는다. 서버 route, JSON wire field, 직렬화된 stable ID, 기존 오류 code, vendor asset path와 이미 공개된 계약 식별자는 그대로 보존하거나 명시적 변환 경계를 둔다. Unity asset 파일을 바꿀 때는 `AssetDatabase`로 `.meta` GUID를 유지하고 Scene·prefab 참조, 컴파일과 관련 test를 함께 검증한다.

대규모 일괄 rename은 하지 않는다. 현재 구현하거나 수정하는 vertical slice부터 적용하며, 기존 이름은 소비자와 serialization 영향을 확인한 작은 묶음으로 순차 정리한다. 이름 변경만으로 Simulation 권위, 원장 상태나 Presentation 결과를 변경하지 않는다.

## D-052 운영 API의 업무 규칙은 순수 공통 계층을 거쳐 Simulation에 적용한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-038~D-043의 운영/Simulation 권위 분리와 D-050의 공통 WorldTick 인과를 유지하며 `MARKET-CONSUMPTION-1`의 주문 기반을 구체화함

음식배달·화물운송·개별주문·같이주문의 운영 Controller나 POST API를 Simulation이 직접 호출하지 않는다. 운영 코드에서 확인한 상태 code, 허용 전이, 차단 사유와 수량 보존식을 `Ssalddel.WorkflowRules.Contracts`와 `Ssalddel.WorkflowRules`의 외부 효과 없는 순수 규칙으로 고정하고, 운영과 Simulation adapter가 같은 업무 의미를 각자의 권위 안에서 사용한다. 규칙에는 `SourceCapabilityKey`, source contract revision, rule revision과 source stable ID를 남기며 API route 문자열은 업무 identity로 사용하지 않는다.

실제 결제·실제 기사 배차·GPS·주소·개인 알림·운영 주문/재고 쓰기는 Simulation 제외 효과로 명시한다. 개별주문과 같이주문은 현재 운영 원장의 실제 상태까지만 공통 규칙에 포함하고, 운영에 없는 실행 단계를 완성된 API처럼 만들지 않는다. Simulation 전용 주문은 별도 원장으로 시장 재고와 노동을 예약하고, 공통 `Decision → Task → WorldTick → Effect`와 save/replay를 통해서만 수령 준비 또는 취소·예약 반환 상태로 진행한다.

## D-053 Simulation 화물운송은 Cargo 이동과 업무 상태 원장을 분리해 결합한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-050의 Cargo 이동 권위와 D-052의 공통 업무 규칙 적용 경계를 구체화함

Cargo 수량·Lot 계보·원재고 예약·경로 진행은 기존 `LOGISTICS-MOVEMENT-1`이 계속 소유한다. 화물운송 원장은 같은 Cargo를 참조하면서 운송 의뢰, 배차 후보, 가상 운송 주체·차량 용량과 `배차대기 → 매칭중 → 배차확정 → 상차지도착 → 상차완료 → 운송중 → 하차지도착 → 인수완료` 인과 이력을 별도로 보존한다. 두 원장을 새 Cargo나 별도 WorldTick으로 복제하지 않는다.

차량 animation이나 목적지 도착만으로 인수완료를 확정하지 않는다. 물류 이동이 `ArrivedAtDestination`에 도달하면 화물운송은 `하차지도착`에 머물고, 별도 인수 Preview·Confirm으로 예약된 Task가 완료되는 WorldTick에서만 `인수완료`가 된다. 실제 기사 배정, GPS 위치 쓰기, 운임 정산과 운송사 알림은 Simulation 제외 효과이며, 화물 상태와 전이 이력은 save/replay hash에 포함한다.

## D-054 Simulation 같이주문은 명시적 개별 의향을 보존한 모집 결과 원장이다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-052의 운영/Simulation 규칙 공유와 개별 동의 경계를 같이주문 모집 결과에 적용함

Simulation 같이주문은 참여자 수와 총수량만 저장하는 익명 합계가 아니다. 각 참여자의 의향 stable ID, 참여자 stable ID, 희망수량, 단위, 명시적 참여 동의와 source lineage를 보존하고, 같은 참여자의 중복 의향이나 동의 없는 의향을 자동 합산하지 않는다. 실제 개인정보·주소·결제정보는 포함하지 않는다.

Preview는 운영 `공동구매자동집단화계획기`의 예약결제 없는 목표 판정과 같은 의미로 `수요수집중` 또는 `확정대기` 후보를 계산하지만 원장을 변경하지 않는다. 사용자가 모집 결과를 명시적으로 Confirm하면 Task가 예약되고, 완료 WorldTick에서 목표 충족은 `확정`, 목표 미달은 `모집종료목표미달`로 전이한다. 이 결과는 Simulation 수요 원장이며 실제 주문 생성·결제·계약·자동 참여 동의를 뜻하지 않는다.

## D-055 Simulation 음식배달의 전달과 주문자 수령 확인을 분리한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-052의 운영/Simulation 경계와 공통 상태 규칙을 음식배달 생애주기에 적용함

Simulation 음식배달은 운영 음식 주문 API나 저장소를 호출하지 않고 별도 가상 주문 원장을 만든다. 주문 접수 Confirm 뒤 공통 WorldTick이 `주문대기 → 조리중 → 픽업대기 → 기사배정 → 픽업완료 → 전달완료`를 진행한다. 여기서 기사배정은 익명의 Simulation 후보 상태이며 실제 기사 계정, GPS, 개인 주소, 결제, 운영 주문 쓰기나 실시간 알림을 만들지 않는다.

`전달완료`는 기사 관점의 전달 결과이고 `수령확인`은 주문자의 별도 의사다. 차량이나 NPC animation, 목적지 도착, 전달 Task 완료만으로 수령확인하지 않는다. 전달 완료 상태에서 주문자가 별도 Preview·Confirm한 수령 Task가 다음 WorldTick에 완료될 때만 `수령확인`으로 전이한다. 주문 stable ID, 메뉴, 음식점/목적 시설, 비식별 배송권, 수량, 기간, 전이 이력과 source lineage는 save/replay hash에 포함한다.

## D-056 주민 소비는 주문 이행에서 이미 차감된 시장재고를 다시 차감하지 않는다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-052의 Simulation 개별주문 원장과 D-043의 정착지 경제 수량 보존을 주민 소비까지 확장함

개별주문의 재고예약은 포장·수령준비 Task가 완료되는 WorldTick에 시장재고에서 한 번만 차감된다. 이후 주민의 수령·소비 Confirm은 해당 주문, 소진된 재고예약, 상품, 시장 시설, 주문자와 수량 계보를 검증하지만 시장재고를 다시 줄이지 않는다. 예를 들어 감자 300kg에서 20kg 주문이 이행되면 시장 잔여는 280kg이고, 소비 완료 뒤에도 시장 잔여 280kg과 주민 소비 누계 20kg가 같은 정착지 경제 snapshot에 함께 존재한다.

수령준비 전 주문, 다른 주문자, 소진되지 않은 예약, 주문·예약 수량 불일치와 같은 주문의 중복 소비는 차단한다. 소비 Confirm은 Task 예약까지만 수행하고 완료 WorldTick에서 주문과 소비 원장을 `Consumed`로 전이한다. 품목별 주민 소비 누계는 실제 주민 개인정보나 식품영양 환산치가 아니며, 비축량·`FoodSecurityDays`를 근거 없이 변경하지 않는다.

## D-057 수출 준비 검사는 운영 수출이 아니라 실패를 보존하는 Simulation 후보 원장이다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-044의 수확 판로 단일 배분과 D-052의 운영/Simulation 경계를 외부 교역 준비에 적용함

수출 준비는 `ExportAgent` 판로로 적용 완료된 수확 배분만 원천으로 사용한다. Confirm에서 준비 수량을 예약하고 공통 WorldTick이 포장과 모의 검사를 진행한다. 검사 통과는 실제 수출이나 운송사 인계가 아니라 배송대행지 인계 후보를 만들 수 있는 상태일 뿐이며, 수출신고·통관·무역계약·운송·정산을 확정하지 않는다.

검사 실패는 성공처럼 숨기거나 수확물을 소진하지 않는다. 실패 사유와 재작업 가능 상태를 수출 준비 원장에 남기고 원배분의 예약 수량을 해제한다. 이후 재작업·재검사는 별도 Preview·Confirm으로 명시적으로 선택해야 하며, 차량이나 NPC 연출로 검사 또는 인계를 확정하지 않는다.

## D-058 수출 재작업은 실패 원장을 덮어쓰지 않는 새 검사 시도다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-057의 실패 보존과 명시적 재작업 원칙을 다중 검사 시도 계보로 확장함

수출 준비 검사에 실패하면 기존 준비 원장의 상태·사유·검사 시각을 수정해 성공으로 바꾸지 않는다. 사용자가 재작업을 Preview하고 Confirm하면 새 준비 stable ID를 가진 후속 시도를 만들고 루트 준비, 직전 실패 준비와 시도 번호를 함께 기록한다. 직전 실패 시도는 더 이상 재작업 가능하지 않게 닫아 같은 실패 결과에서 여러 재작업이 동시에 수량을 예약하지 못하게 한다.

후속 시도도 WorldTick에서 재작업과 재검사를 거친다. 통과한 최신 시도만 배송대행지 인계 후보가 되고, 실패하면 해당 시도에 새 실패 사유를 기록하고 원배분 수량을 다시 반환한다. 모든 과거 시도는 save/replay 대상이며 삭제하거나 현재 성공 상태로 합쳐 쓰지 않는다.

## D-059 Cargo 준비 완료는 배송대행지 인계나 차량 출발이 아니다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-057·D-058의 검사 통과 계보와 기존 물류 이동 사이에 명시적인 Cargo 준비 경계를 추가함

검사 통과한 최신 수출 준비 시도만 Cargo 준비 원천이 된다. Cargo 준비는 HarvestLot, 포장 Lot 후보, 상품, 수량, Cargo stable ID, 경로와 출발·목적 시설 입력을 하나의 후보 원장으로 조립한다. 수출 준비에서 이미 예약한 수량을 승계하며 같은 수량을 다시 출고 예약하지 않는다.

Cargo 준비 Task 완료는 `ReadyForHandoff`를 의미할 뿐 배송대행지의 실제 인수, 운송사 계약, 배차, 상차, 차량 출발, 수출신고나 통관을 확정하지 않는다. 따라서 Cargo 준비 완료만으로 기존 물류 이동 원장을 만들거나 WorldTick 이동을 시작하지 않는다. 배송대행지 인계와 이후 물류 이동은 각각 별도 Preview·Confirm을 요구한다.

## D-060 배송대행지 Simulation 인계와 물류 이동 시작을 분리한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-059의 Cargo 준비 경계 뒤에 인계 확인을 추가하고 기존 물류 이동과의 분리를 유지함

배송대행지 인계 Preview는 `ReadyForHandoff` Cargo와 원래 Cargo 준비 목적 시설이 일치할 때만 허용한다. Confirm은 인계 Task를 예약하고 완료 WorldTick에서 Cargo 준비와 인계 원장을 `HandedOffInSimulation`으로 전이한다. 이는 Simulation에서 배송대행지가 Cargo를 넘겨받았다는 기록이며 운영 사업자의 실제 인수 증빙이나 계약이 아니다.

인계 완료만으로 배차, 차량 지정, 상차, 출발 또는 기존 물류 이동 원장을 만들지 않는다. 인계된 Cargo의 기존 300kg 출고 예약은 유지하고 이후 물류 이동 결정이 이를 승계해야 한다. 차량과 NPC 연출은 인계 완료나 이동 시작을 확정하지 않는다.

## D-061 수출 Cargo 물류 이동은 기존 출고 예약을 승계한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-060의 별도 인계 완료를 기존 `LOGISTICS-MOVEMENT-1`의 선택적 source로 연결함

배송대행지 Simulation 인계가 완료된 Cargo는 기존 물류 이동 Preview 입력으로 사용할 수 있다. 인계 stable ID, Cargo, HarvestLot, 포장 Lot, 상품, 배분, 수량·단위와 출발 시설이 모두 일치해야 하며 인계 완료 전이나 불일치 계보는 차단한다.

수출 준비에서 이미 확보한 출고 예약은 물류 이동 Confirm에서 다시 더하지 않는다. 물류 이동은 같은 300kg 예약을 승계하고 명시적 Confirm 뒤 WorldTick에서만 출발·진행·도착한다. 화물운송 binding이 없는 경우 실제 운송사, 차량, 배차, GPS와 운임 정산을 만들지 않으며 목적지 도착도 별도 인수 결정 전에는 재고 확정이 아니다.

## D-062 항만 준비시설 도착과 인수 완료를 분리한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-061의 목적지 도착 후보 뒤에 별도 항만 인수 Decision·Task를 추가함

수출 Cargo는 기존 물류 이동이 `ArrivedAtDestination`이고 배송대행지 인계 계보와 목적 시설이 일치할 때만 항만 인수 Preview 대상이 된다. Confirm은 별도 인수 Task를 예약하고 완료 WorldTick에서만 `ReceivedAtPortStaging`으로 전이한다. 기존 출고 예약 300kg과 HarvestLot·포장 Lot·상품·Cargo 계보는 그대로 유지한다.

항만 준비시설 인수는 실제 수출신고, 공인 검사, 검역, 통관 또는 선적이 아니다. 해당 운영 효과는 만들지 않으며 이후 준비 여부도 별도 Decision으로 다룬다. Scene의 하역 animation이나 NPC 도착은 인수 완료 권위를 갖지 않는다.

## D-063 수출 준비성 검토는 자기 진술형 Simulation 후보로 제한한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-062의 항만 인수 완료 뒤 서류·검사 준비 여부를 별도 검토하되 운영 승인과 분리함

`ReceivedAtPortStaging`인 Cargo만 수출 준비성 검토 Preview 대상이 된다. 검토 입력은 서류 묶음 준비 여부와 후속 검사 준비 여부이며, 이 값은 Simulation 자기 진술이다. Confirm과 완료 WorldTick은 `ReadyCandidate` 또는 누락 코드가 있는 `ActionRequired`를 기록할 뿐 정부·검사기관의 확인을 뜻하지 않는다.

보완 필요 결과는 보존하고 새 stable ID, 부모 검토 stable ID와 증가한 시도 번호로 재검토한다. 준비 후보가 완료된 항만 인수에는 중복 검토를 허용하지 않는다. 어느 결과도 실제 수출신고, 공인 검사, 검역 승인, 통관, 선복 예약 또는 선적 권위를 만들지 않으며 출고 예약 300kg과 전체 Lot 계보를 유지한다.

## D-064 수출 선적 계획은 비교 가능한 추정 후보이며 재정을 바꾸지 않는다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-063의 준비 후보 뒤에 목적 시장과 운송 방식의 선택 단계를 추가함

`ReadyCandidate`로 완료된 준비성 검토만 선적 계획 Preview 원천이 된다. 사용자는 목적 국가·시장과 해상/항공 방식별 예상 매출, 국제물류·취급·기타 비용, 예상 운송 기간과 위험 점수를 비교할 수 있다. 위험 수준은 0~33 낮음, 34~66 중간, 67~100 높음으로 결정적으로 계산한다.

Preview는 비교만 제공하고 하나의 명시적 Confirm 뒤 완료 WorldTick에서만 `PlannedCandidate`를 만든다. 같은 준비성 검토에서 계획을 중복 확정하지 않는다. 금액·기간·위험은 출처를 가진 Simulation 추정치이며 견적이나 수익 보장이 아니다. 계획 단계는 정착지 재정과 출고 예약을 변경하지 않고 실제 운송 예약·수출신고·공식 검사·검역 승인·통관·선적을 생성하지 않는다.

## D-065 수출 실행 결과는 seed 기반으로 숨겨 두고 기존 예상 매출과 정산한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-064의 선택된 계획 후보를 Simulation 결과까지 닫되 운영 실행과 분리함

`PlannedCandidate`만 별도 수출 실행 Preview·Confirm 원천이 된다. Preview는 위험 점수에서 계산한 성공 확률과 성공·손실 재정 후보를 제공하지만 실제 결과 roll은 완료 전까지 공개하지 않는다. Confirm은 최악 손실을 감당할 재정 여력만 예약하고, WorldTick이 시작되면 `InTransit`, 계획 기간이 끝나면 세션 seed와 실행 stable ID의 결정적 roll로 성공 또는 손실을 확정한다.

판로 선택에서 이미 반영한 `ProjectedRevenue`는 결과 정산 시 조정한다. 성공 차액은 `계획 순수익 - 기존 예상 매출`, 손실 차액은 `-계획 총비용 - 기존 예상 매출`이다. 이로써 예상 매출을 중복 반영하지 않는다. 성공은 도착 수량, 손실은 손실 수량을 기록하고 모두 출고 예약을 종료한다. 결과 effect는 Simulation 재정 원장에 한 번만 적용하며 실제 운송 예약·신고·검사·검역·통관·선적·운영 정산 권위를 만들지 않는다.

## D-066 수확물 판로 카드는 기존 원장의 읽기 projection만 사용한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-065까지 분리된 판로별 원장을 하나의 생산자 관점으로 읽되 새 권위를 만들지 않음

HarvestLot 판로 결과는 조합 출하·직판·보관·외부 교역 네 선택지를 고정 순서로 반환한다. 실제 선택된 경로만 기존 allocation과 후속 원장에서 현재 단계, 해결·잔여·시장 공급·비축·도착·손실 수량, 누적 Simulation 재정 효과와 위험 결과를 읽는다. 선택되지 않은 경로는 `NotSelected`이며 실제 결과값을 부여하지 않는다.

조합 출하는 물류나 인수 완료 증거가 없으면 해결 수량을 0으로 유지한다. 직판은 allocation이 시장 공급에 반영된 수량, 보관은 감모 후 생성된 비축 Lot 수량, 외부 교역은 최신 준비·Cargo·인계·항만·준비성·계획·실행 계보를 따른다. 이 projection은 조회 전용이며 session revision, 원장, 재정, save/replay hash를 바꾸지 않고 운영 효과도 만들지 않는다.

## D-067 Unity 판로 결과 카드는 서버 읽기 projection을 한국어로만 표현한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-049의 Authority reconcile 원칙과 D-066의 통합 판로 읽기를 Unity 생산자 카드에 적용함

Unity HarvestLot 카드는 공식 `harvest-route-outcomes` 응답의 네 판로, 선택 여부, 현재 단계, 수량, 재정과 위험 결과를 그대로 받아 한국어 Presentation model로 변환한다. Unity는 해결·잔여·비축·도착·손실 수량이나 누적 재정 효과를 다시 계산하지 않고, server code를 사용자 문구로 대응시키는 일만 한다.

판로 결과 조회는 Decision·Task·Effect 명령 흐름과 분리한다. 읽기 조회가 실패해도 이미 Confirm 또는 WorldTick으로 확정된 session snapshot과 상호작용 phase를 되돌리거나 실패로 바꾸지 않는다. Scene과 버튼은 원장 권위가 아니며 후속 진행은 같은 결과 projection을 다시 조회해 표현한다.

## D-068 Unity 판로 재접속은 session과 결과 목록의 동일 revision을 원자적으로 적용한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-067의 읽기 카드가 장시간 실행과 재접속에서도 stale 상태를 표시하지 않도록 보강함

Unity가 판로 카드를 다시 열 때는 최신 Simulation session snapshot을 읽은 뒤 판로 결과 목록을 조회한다. 두 응답의 session stable ID, WorldRevision과 WorldTick이 모두 일치할 때만 WorldShell snapshot, 현재 결과와 카드 phase를 함께 교체한다. 목록이 stale이거나 현재 allocation이 있는데 결과가 비어 있으면 기존 화면 snapshot을 보존하고 읽기 오류만 기록한다.

첫 playable의 한-Lot 범위에서는 목록이 하나일 때 해당 결과를 현재 HarvestLot 카드에 연결한다. 여러 Lot이 생기면 object stable ID와 HarvestLot stable ID의 명시적 mapping을 추가해야 하며 임의의 첫 항목을 선택하지 않는다. 재접속으로 기존 Preview가 없을 때는 Task 기간이나 Tick 수를 추정하지 않고 진행 버튼을 비활성화한다.

## D-069 Unity 판로 작업 재개는 session Task의 남은 Tick만 사용한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-068에서 재접속 시 비활성화한 진행 기능을 권위 있는 Task 원장으로만 다시 열음

Unity는 판로 allocation에 연결된 `TaskStableId`로 같은 Simulation session snapshot의 Task를 찾는다. allocation이 `Reserved`, Task가 활성 상태이고 `Scheduled` 또는 `InProgress`이며 예정 완료 Tick이 현재 WorldTick보다 뒤일 때만 재개할 수 있다. 남은 기간은 `ExpectedEndTick - WorldTick`으로 계산하고 서버가 반환한 일정과 일치하는지 검증한다.

재접속 전에 보았던 Preview나 화면 메모리에서 기간·비용·효과를 복원하지 않는다. 계속 진행은 현재 session revision과 검증한 남은 Tick을 기존 Tick Command에 전달하며, 모순된 Task snapshot은 최신 판로 목록과 함께 화면에 적용하지 않는다. Unity 애니메이션이나 버튼은 Task 완료 권위를 갖지 않는다.

## D-070 플레이 경영 시간은 명시적 턴 마감으로만 진행한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: 기존 `OneTickOneDay`와 D-003의 Preview·Confirm·revision 원칙을 플레이 시간 경계로 구체화함

플레이 중 경영 행동과 조회는 게임 날짜를 자동으로 진행하지 않는다. 사용자는 현재 경영일의 예약 업무와 선택 카드를 검토하고 `턴 마감 Preview → 명시적 Confirm`을 거쳐야 다음 WorldTick과 다음 게임 날짜로 진행한다. 기존 `/ticks` API는 저장 재생과 기술 호환을 위해 유지하지만 플레이 UI의 기본 시간 진행 경로로 사용하지 않는다.

턴 카드는 0장 또는 허용된 수만 선택할 수 있고 서버의 canonical catalog에 있는 stable ID·revision·종류·효과만 적용한다. 선택 효과는 마감한 날을 소급 변경하지 않고 다음 경영일에만 활성화된다. LLM과 Unity View는 카드의 수치·규칙을 만들지 않는다. 초기 `TURN-0`의 바보·전차 카드는 명시적 Fixture이며 승인 publication이나 실제 문화 자료로 오인하지 않는다. 문화 카드는 같은 종류 계약으로 확장하되 출처·달력·효과 규칙을 확정하기 전에는 임의로 게시하지 않는다.

## D-072 문화 턴 카드는 지역·기간·공식 원천·달력·효과 규칙이 완전할 때만 게시한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-070의 문화 카드 확장 조건을 첫 canonical Fixture 계약으로 구체화함

`Culture` 턴 카드는 stable ID와 표시 문구만으로 catalog에 들어오지 않는다. `RegionKey`, Simulation game date 유효 시작·종료, `CalendarRevision`, `EffectRuleRevision`, source stable ID, HTTPS source URL과 근거 확인 시각이 모두 있어야 하며 하나라도 빠지면 서버와 Unity가 fail-closed로 거부한다.

공식 기관 원천은 특정 지역 행사나 생활양식의 대표성을 자동 증명하지 않는다. 첫 `CULTURE-CARD-0`은 서울의 특정 사실을 주장하지 않는 생활문화 질문 Fixture이며, 실제 행사·계절 문화 카드는 지역별 원문 근거와 사람 검수 publication을 별도로 거쳐야 한다. 카드의 수치와 다음 턴 효과는 서버 effect rule revision만 결정하며 Unity와 LLM은 만들거나 보완하지 않는다.

## D-071 Unity 다중 판로 카드는 object-Lot 명시 mapping으로만 선택한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-068의 한-Lot 임시 자동 선택을 여러 Lot에서도 안전한 선택 계약으로 확장함

Unity는 Scene object stable ID와 HarvestLot stable ID를 서로 다른 identity로 유지하고 일대일 mapping을 통해서만 연결한다. object 이름, 표시 순서, prefab 또는 판로 결과 목록의 첫 항목으로 Lot을 추측하지 않는다. object stable ID 중복과 HarvestLot stable ID 중복은 composition 오류로 차단한다.

Simulation session의 모든 HarvestLot allocation은 Lot별 Task authority로 보존한다. 사용자가 object를 선택하면 mapping된 Lot의 allocation·Task·Effect·남은 Tick과 판로 결과만 현재 카드에 함께 투영한다. mapping 결과나 Lot별 Task가 누락되면 기존 화면 선택을 유지하며 다른 Lot의 Task를 대신 보여주거나 진행하지 않는다.

## D-073 Unity 에셋 현실 관측은 연구 해석과 Simulation에서 분리한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: 신티 에셋 연구소를 공공데이터 탐구 입구로 확장하되 운영 사실과 Presentation의 경계를 고정함

Unity 에셋은 source asset GUID로 별도 현실 관측 Catalog의 후보와 연결한다. 에셋 연구 Catalog는 외형에서 관찰한 사실, 현실 의미와 활용 후보를 설명하고, 현실 관측 Catalog는 공공데이터 source·서버 조회 경로·지역·관측 기간·유통 단계·품종·등급·단위·표본 수·revision을 독립적으로 보존한다. 실제 응답이 없거나 필수 근거가 빠지면 `실제 관측 미수집`으로 표시하며 Simulation Fixture나 에셋 수량으로 보완하지 않는다.

공공 관측은 선택한 에셋과 관련된 현실 현상을 이해하게 할 수 있지만 그 prefab의 실제 중량·품질·원산지·소유자·가격이나 특정 농장의 생산·출하 사실을 증명하지 않는다. 기존 Simulation 값은 비교 자료임을 명시하고 실제 관측과 같은 카드에서 혼동되지 않게 구분한다.

source key와 API path는 관측 출처를 찾는 기술 식별자일 뿐 Domain 권위가 아니다. 제품은 canonical product stable ID와 확인된 source 품목 관계를 유지하고, 운영 확정이나 상태 변경은 기존 서버 Command·원장 경계를 통과한다.

## D-074 KAMIS 대응 작물은 모판에서 연구한 뒤 Farm Scene으로 승격한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-073의 현실 관측 분리를 여러 Farm 작물의 반복 가능한 Scene 승격 절차로 구체화함

Farm Pack 작물은 곧바로 생산 Scene에 배치하지 않고 `KAMIS 작물 모판`에서 source prefab, 한국어 이름, canonical product stable ID, KAMIS 분류·품목 관계와 시각 대응 수준을 먼저 확인한다. 첫 모판은 `FarmProductVisualCatalog`에서 `Direct`인 18종만 포함한다. 비슷한 외형을 빌리는 `Representative`와 전용 prefab이 없는 `Unmapped`는 같은 모판에 섞지 않고 별도 검토 구역에 둔다.

모판에서 실제 Scene으로 승격하려면 source·지역·조사일·유통 단계·품종·등급·단위·revision의 관측 경계, 장소 역할과 작은 Simulation, VisualKey와 object-product stable-ID mapping을 각각 확인해야 한다. Scene의 renderer 수·에셋 크기·배치 위치는 생산량·재고·가격·품질 권위가 아니다. 실제 관측이 없을 때는 `실제 관측 미수집`을 유지하고 Simulation Fixture로 대체하지 않는다.

## D-075 공공 관측 출처표와 에셋 연결표는 분리하고 모판 문맥으로 선택한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-073의 현실 관측 분리와 D-074의 Scene 승격 절차를 토양·농업환경 자료로 확장함

공공 관측의 자료 식별자, 제공기관, 공식 주소, 응답 형식, 공간·시간 기준, 관측 항목, 이용조건, 한계와 확인 기준일은 `공공관측SourceCatalog`에서 출처 단위로 보존한다. source asset GUID와 자료의 관계, 실제 관측 수집 여부, 알 수 있는 것과 없는 것, Simulation 비교는 `에셋공공관측Catalog`에서 별도로 보존한다. 같은 에셋이 KAMIS 모판과 토양 모판처럼 여러 연구 문맥에 참여할 수 있으므로 상세 카드는 현재 선택한 모판의 연결을 우선하고 임의의 첫 연결을 사용하지 않는다.

자료 진행 상태는 `자료 후보 → 메타데이터 확인 → 표본 응답 확인 → 실제 관측 연결`로 구분한다. 메타데이터 확인만으로 실제 값, 특정 필지 상태, 감자 재배 권고나 관수 명령을 만들지 않는다. 서로 다른 `환경·식물·소품` 분류의 에셋도 하나의 연구 질문을 구성하면 같은 모판에 전시할 수 있지만, 실제 Scene 승격은 위치 공개 범위, 표본 응답과 코드 대응, 서버 읽기 경계, 작은 Simulation 검증을 각각 통과해야 한다.

## D-076 농사 생육은 일수 대신 환경 Snapshot의 제한 요인과 스트레스로 진행한다

- 상태: 확정
- 날짜: 2026-08-11
- 관계: D-021의 공공데이터 정규화, D-070의 하루 한 턴, D-073~D-075의 관측·연구·Simulation 분리를 감자 생육 규칙으로 구체화함

작물 요구조건, 토양 기준과 수분 상태, 일별 기온·햇빛·강수를 하나의 `재배환경일일Snapshot`으로 고정한다. 하루 생육점수는 가장 부족한 동적 조건을 제한 요인으로 삼고 토양 적합도를 반영하며, 가뭄·과습·저온·고온·저일사·토양부적합 스트레스는 원인별로 별도 누적한다. 강수량은 생육점수에 직접 더하지 않고 유효강수·관수·증발산·유출·배수를 거친 토양수분으로 판정하며, 일사량과 일조시간을 같은 값으로 취급하지 않는다.

농사로와 기상·토양 API는 현실 기준정보와 관측 source이며 Simulation 상태의 권위가 아니다. 서버가 원문·단위·품질·공간·시간·rule revision을 검증하고 Preview·Confirm·Tick을 계산하며 Unity는 결과만 한국어 카드와 에셋 상태로 표현한다. 관측 결측을 Fixture로 조용히 대체하지 않고, 여러 날 진행도 하루씩 계산한다. 첫 감자 구현은 실제 농업 권고가 아닌 별도 versioned Fixture rule로 검증한 뒤 공식 근거를 사람이 검토해 승격한다.

## D-077 Unity 턴 마감은 Confirm 뒤 canonical session을 다시 조회한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-070의 명시적 턴 마감과 D-072의 문화 카드 출처·효과 규칙을 실제 Unity HTTP 경계로 연결함

Unity의 서버 모드 턴 마감은 서버 context 조회, Preview, 명시적 Confirm을 거친다. Confirm 응답만으로 WorldShell을 바꾸지 않고 같은 session을 다시 조회해 stable ID, revision 증가, 완료 Tick과 다음 턴을 검증한 뒤 canonical snapshot을 적용한다. 네트워크 또는 검증 실패를 Fixture 성공으로 대체하지 않으며 기존 화면 상태를 보존하고 오류를 드러낸다.

개발 편의를 위한 결정적 session 자동 생성은 명시적 Development Simulation 조립부에서만 허용한다. 사용자가 이어서 플레이할 session 선택, 인증과 권한, 영속 저장, 서버 재시작 복구는 별도 경계이며 운영 모드의 자동 생성이나 sample fallback 근거가 아니다.

## D-078 턴 카드는 분야별 모판에서 검증한 뒤 서버 덱으로 승격한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-070의 하루 한 턴, D-072의 문화카드 근거, D-073~D-075의 에셋 모판 승격 방식을 턴 카드 전체에 적용함

철학·학당, 지역문화, 경영사건과 공공관측 카드는 서로 다른 모판에서 출처와 검수 방식을 보존한다. 후보 카드는 `카드 씨앗 → 출처 메타데이터 → 내용·사람 검수 → 효과 규칙 → 모판 화면 → 게시 snapshot → 게임 덱 이식` 단계를 거친다. 모판은 연구 projection이며 카드가 모판에 있다는 사실만으로 게시 승인이나 게임 효과 권위를 갖지 않는다.

개발용 Fixture 카드는 효과 규칙과 화면 계약을 검증할 수 있지만 승인 publication을 대신하지 않는다. 실제 승인 catalog가 비어 있으면 빈 덱이 정상이며 Unity나 LLM이 일반 지식, 다른 지역 자료 또는 임의 수치로 보완하지 않는다. 턴 마감 효과는 C5 게시 snapshot과 C6 서버 덱 이식을 통과한 카드만 canonical session에 적용한다.

## D-079 농사로 작업군·콘텐츠·canonical 상품 관계를 분리한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-036의 출처별 품목코드 분리와 D-076의 사람 검토 후 rule 승격 경계를 농사로 감자 Profile에 적용함

농사로 `farmWorkingPlanNew`의 `kidofcomdtySeCode=210005`는 감자 상품코드가 아니라 `밭농사` 작업군 분류다. 감자는 해당 작업군의 일정 목록에서 제목과 `cntntsNo=30699`로 식별되고 상세 응답이 같은 콘텐츠번호·제목·작업군을 반환할 때만 사람 검토용 Profile 후보로 연결한다. 이 콘텐츠 연결은 기존 `NONGSARO_KIND_OF_COMMODITY` 상품 관계를 Confirmed로 승격하지 않으며 그 관계는 별도 공식 품목 crosswalk가 확인될 때까지 `Unlinked`로 유지한다.

상세·시기 원문의 토양, 물, 기온, 햇빛, 생육 단계와 작형 관련 구간은 `LocatedNeedsReview` 근거로만 보존한다. 원문의 수치·시기·재배 설명은 품종·지역·작형·단위와 적용 범위를 사람이 검토하고 새 rule revision을 승인하기 전까지 `FARM-ENV` 임계값이나 운영 농업 처방으로 변환하지 않는다.

## D-080 기상청 ASOS 일관측은 지점·날짜·원문 단위로 보존한다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: D-021의 공공데이터 정규화와 D-076의 기후·토양·작물 일일 Snapshot 경계를 기상청 일관측에 구체화함

기상청 ASOS 일자료는 `관측소 ID + 관측일`을 stable ID의 기준으로 삼고, 관측소·날짜 한 건만 정확히 선택한다. 원문 응답 SHA-256, 조회시각, 출처, 단위, 품질, 공간 정밀도와 한계를 함께 보존하며, 기온·강수량·일사량·일조시간·상대습도를 서로 대체할 수 없는 별도 관측값으로 다룬다.

원천의 빈 값은 0이나 Fixture로 보완하지 않고 `Incomplete`와 결측 필드 목록으로 드러낸다. 필수 항목 중 하나라도 결측이면 FARM-ENV Simulation 입력 자격을 자동 차단한다. ASOS는 농장 면 전체가 아닌 관측소 지점 자료이므로, 농장과 관측소의 거리는 근거 URL이 있는 좌표 context가 제공된 경우에만 계산하고 그렇지 않으면 미계산으로 남긴다. D-1 이전의 과거 관측만 허용하며 예보나 운영 농업 처방으로 표현하지 않는다.

## D-081 통합 모판·전시관의 Scene 이식 단위는 업무 장면이 아니라 개별 Object다

- 상태: 확정
- 결정일: 2026-08-11
- 관계: EXH-0~5의 업무 계보와 증거를 유지하면서 전시관의 모판 역할과 Scene 이식 단위를 명확히 함

통합 모판·전시관은 완성된 업무 Scene을 전시하거나 Story 전체를 하나의 배치 모듈로 제공하지 않는다. 화물·창고, 주문자 집단·마트, 음식배달 같은 업무 계보는 여러 Object의 관계를 설명하는 `Story`로 보존하고, 실제 Scene에 이식하는 단위는 건물, 시설, 가구, Dock, Gate, 차량, 화물, actor visual과 marker 같은 독립 `Seedbed Object`로 둔다.

업무 record stable ID, 모판 Object stable ID와 Scene Placement stable ID는 서로 다른 identity다. Object prefab은 주문·배차·재고·수령 상태를 소유하지 않으며 서버/Simulation snapshot을 Presenter로 받아 표현한다. prefab path·GUID·좌표는 Unity catalog와 Scene placement가 관리하고 shared/server contract는 semantic role, compatible zone, DataBinding, Gate와 evidence만 보존한다.

Object가 독립 Preview에서 검증됐다는 사실만으로 다른 Scene에 자동 배치하지 않는다. 대상 Scene별 placement stable ID, placement profile revision, Runtime test와 Game View evidence를 가진 명시적 O6 승격 기록이 있어야 하며 한 Scene의 승격은 다른 Scene의 승격을 대신하지 않는다.

## D-082 Simulation 생산·소비는 부호가 아니라 자원 변동 유형과 효과 묶음으로 기록한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-038의 공통 Decision·Task·Effect, D-043의 정착지 경제 수량 보존, D-052의 순수 업무 규칙, D-056의 주민 소비 1회 차감을 일반화함

Simulation의 농장 확장, 생산, 소비, 예약, 이동, 변환, 손실과 용량 변화는 하나의 종합 점수나 부호만으로 기록하지 않는다. 같은 양수라도 생산·예약 해제·외부 유입·용량 증가는 서로 다르고, 같은 음수라도 소비·예약·이동 반출·변환 입력·손실은 서로 다르므로 자원 변동 유형을 명시한다.

한 프로젝트는 현금, 노동, 재고, 저장 용량, 생산 용량, 시간과 위험에 대한 여러 효과선을 하나의 효과 묶음으로 만든다. 각 효과선은 대상 원장, 자원 종류, 품목·Lot 참조, 이전 값, 변화량, 이후 값, 단위, 규칙 개정 번호와 출처를 보존한다. 예약은 전체량을 바꾸지 않고 가용량과 예약량 사이에서 이동하며, 위치 이동은 원천 감소와 대상 증가를, 형태 변환은 입력·출력·부산물·손실을 같은 묶음에서 수량 보존식으로 검증한다.

예상 효과는 Preview용 관점별 조회 결과이고 실제 효과가 아니다. 명시적 Confirm 뒤 Task가 완료되는 WorldTick에서만 효과 묶음을 원자적으로 적용하며, 같은 효과 고유 식별자는 한 번만 적용한다. Unity의 `+`·`-` 표시, 작물 Mesh, 상자 수와 애니메이션은 설명용 표현이며 자원 변화의 근거가 아니다. 실제 수치는 Simulation Fixture와 출처·사람 검토를 거친 규칙 개정을 구분하고 운영 원장에 자동 적용하지 않는다.

## D-083 규칙은 업무·해석·표현·상호작용 계층과 세부 영역으로 분리한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-003의 서버 권위, D-004의 Simulation/Operational 분리, D-049의 Unity 상태 재조회와 D-082의 자원 효과 묶음을 규칙 분류에 적용함

프로젝트 규칙을 하나의 목록으로 관리하지 않고 `업무·Simulation`, `해석`, `표현`, `상호작용` 계층으로 먼저 분리한다. 업무·Simulation 규칙은 생산·소비·운송·창고·시장·시설·시간 영역으로 나누며 서버 원장, Task, 상태 전이와 자원 효과를 소유한다. 해석 규칙은 권한·공개 범위에 맞는 관점별 조회 결과와 표현 모델을 만들지만 기준 원장을 변경하지 않는다.

표현 규칙은 그래픽·Material·LOD·FX, 카메라·초점·전환, 애니메이션·이동 표현, 조명·오디오·UI를 담당한다. 표현 규칙은 서버 상태를 읽어 보여줄 수 있지만 생산량·재고·운송·입고·수령 완료를 만들 수 없다. 상호작용 규칙은 선택, 정보 보기와 Preview·Confirm 요청을 구성하며 서버 응답 전에 업무 성공을 확정하지 않는다.

`Simulation자원효과묶음Snapshot.RuleDomainCode`에는 업무 규칙 영역만 허용한다. `Presentation.Camera` 같은 표현 규칙은 별도 Unity 대장과 계약에서 관리하며 자원 효과 묶음으로 변환하지 않는다. 차량 이동, 카메라 도착, 애니메이션 완료, Renderer·GameObject 상태는 업무 규칙의 완료 근거가 아니다.

## D-084 감자 생산의 첫 기준 단위는 명시적 면적을 가진 단일 Tile 재배 단위다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-076의 환경 생육, D-081의 개별 배치 객체, D-082의 자원 효과 묶음과 D-083의 생산 규칙 영역을 첫 감자 생산 세로 단면으로 연결함

감자 재배체 배치 객체는 실제 한 포기나 밭 전체가 아니라 단일 `TileStableId`에 대응하는 재배 군집을 표현한다. 생산 규칙은 Unity Mesh 수나 Grid 칸 수로 면적을 추정하지 않고, 재배 단위 상태에 물리 면적과 유효 재배 면적 비율을 명시적으로 요구한다. Tile의 현실 기준 면적과 유효 비율의 공식 값은 아직 확정하지 않으며 Fixture와 운영 기준을 구분한다.

수확 후보량은 `단위면적 기준 생산성 × 유효 재배 면적 × 환경 계수 × 투입 계수 × 시설 계수 × 손실 계수`로 계산한다. 첫 규칙은 출처와 한계를 가진 Fixture만 허용하며 관측 자료나 Unity 표현을 검토 없이 생산 규칙으로 승격하지 않는다. 재배 단위가 수확 준비 상태이고 Decision이 확정됐으며 Task가 완료된 경우에만 새 수확 Lot을 위한 대기 중 생산 효과 묶음을 만든다. 실제 원장 반영은 해당 묶음을 WorldTick에서 원자 적용했을 때만 성립한다.

기존 300kg Fixture는 호환 테스트에서 100m²와 3kg/m²를 사용해 재현하지만, 두 수치는 실제 농업 생산성·면적 또는 운영 수확량의 근거가 아니다. 단일 Tile은 첫 계약에서 별도 수확 Lot을 만들며 여러 Tile의 Lot 합산은 별도 규칙으로 남긴다.

## D-085 수요·예약·주문 이행·주민 소비는 서로 다른 자원 단계다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-043의 정착지 수량 보존, D-056의 시장 재고 1회 차감, D-082의 자원 효과 묶음과 D-083의 시장·소비 규칙 영역을 공통 효과로 연결함

수요와 주문 의향은 그 자체로 재고를 바꾸지 않는다. 주문 Confirm은 시장 가용량을 주문별 예약량으로 옮기며 전체 보유량은 유지한다. 주문 이행 Task가 완료되면 예약량을 주민 수령량으로 옮기고 이 시점에 시장 보유량에서 한 번만 제외한다. 주민의 실제 소비는 별도 Confirm과 Task 완료 뒤 주민 수령량을 줄이고 소비 누계를 올린다.

주민 소비 단계는 시장 가용량이나 예약량을 다시 차감하지 않는다. 소비 기록에는 주문 이행 후 시장 잔여 수량을 관측값으로 보존하되, 그 값과 소비 시점 값이 다르거나 추가 시장 차감이 적용됐다고 표시되면 효과 묶음을 만들지 않는다. 따라서 주문 이행과 소비는 시간·상태·원장이 분리되면서도 같은 주문·예약·품목·주민 계보를 유지한다.

시장 예약과 이행 효과는 `Market` 규칙 영역, 주민의 실제 소비 효과는 `Consumption` 규칙 영역에 둔다. 음식이 사라지는 Unity 애니메이션이나 UI 숫자 변화는 소비 완료 근거가 아니며 서버 Simulation의 완료된 Task와 최신 상태 사본을 기준으로 한다.

## D-086 운송은 상차·이동 자원 소비·하차·인수 확인을 분리한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-054의 화물운송 Simulation, D-060의 공통 물류 이동, D-082의 이동·손실 보존과 D-083의 운송 규칙 영역을 공통 자원 효과로 연결함

Cargo 운송은 차량이 움직이는 하나의 애니메이션으로 취급하지 않는다. 상차는 원산지 Cargo를 차량 Cargo로 옮기고, 이동은 서버 WorldTick과 경로 진행 상태를 기준으로 연료·노동·명시적 Cargo 손실을 기록한다. 목적지 도착 뒤 하차는 차량 Cargo를 목적지 대기 Cargo로 옮기며, 별도 인수 Confirm과 Task 완료 뒤에만 인수 Cargo로 전환한다.

차량 용량과 Cargo 단위가 맞지 않으면 상차 효과를 만들지 않는다. 운송 손실은 원천 Cargo 감소와 손실 원장 증가를 같은 보존 묶음에 기록하며 원래 Cargo 전체량 이상을 잃을 수 없다. 도착과 인수는 다른 상태이고, 도착만으로 창고 입고나 재고 반영을 확정하지 않는다.

Unity 차량의 Transform, NavMesh 도착, 바퀴 회전, 카메라 추적과 도착 FX는 운송 상태나 인수 완료의 근거가 아니다. 서버의 물류 이동 상태, 화물운송 상태, 완료 Tick과 인수 Task를 다시 조회한 결과만 업무 상태를 바꿀 수 있다.

## D-087 창고는 인수·검수·적치·보관·피킹·출고와 용량을 함께 기록한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-043의 정착지 저장 용량, D-060의 물류 도착·인수 분리, D-082의 이동·예약·손실 보존과 D-083의 창고 규칙 영역을 공통 자원 효과로 연결함

운송 인수가 완료됐다는 사실만으로 창고 재고가 되지 않는다. 인수 Cargo는 창고 입고 대기와 검수를 거치며, 검수 합격량만 적치할 수 있다. 검수 거부량은 조용히 삭제하지 않고 별도 거부·손실 원장과 출처를 남긴다.

적치는 Cargo 수량 이동과 저장 용량 점유를 같은 효과 묶음에서 처리한다. 가용 용량과 점유 용량의 합은 전체 저장 용량과 같아야 한다. 보관 감모와 출고로 창고 재고가 줄면 같은 양의 점유 용량을 해제한다. 피킹은 재고를 즉시 외부로 내보내지 않고 출고 예약으로 옮기며, 별도 출고 Task 완료 뒤에만 출고 인계 Cargo가 된다.

Shelf의 상자 개수, 지게차 애니메이션, Dock 점유 표시와 창고 카메라는 검수·입고·출고 완료의 근거가 아니다. 서버의 완료된 창고 Task와 최신 재고·용량 상태 사본만 창고 업무 결과를 확정한다.

## D-088 Unity 표현 규칙은 영역별 출력 채널과 구현 상태를 대장으로 관리한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-049의 Unity 상태 재조회, D-081의 배치 객체 이식 단위와 D-083의 표현 규칙 영역을 기존 시각 규칙에 적용함

Unity 표현 규칙은 그래픽, 카메라, 애니메이션, 조명, 오디오, UI의 여섯 영역으로 나누고 각 영역이 사용할 수 있는 출력 채널을 제한한다. 그래픽은 Material·Color·Mesh Variant·LOD·FX, 카메라는 초점·거리·구도·전환, 애니메이션은 Animator 상태·이동 재생·속도, 조명은 Light·Ambient·Fog·시간대 시각, 오디오는 Cue·Volume·공간 혼합, UI는 Label·Icon·Panel·Badge·Progress만 다룬다.

기존 `VisualRuleRevision` 문자열과 클래스명은 변경하지 않고 표현 규칙 대장에 연결한다. 이미 코드에 존재하는 규칙은 `ExistingRuleMapped`, 분류 계약만 준비한 규칙은 `ContractPrepared`로 구분한다. 계약 준비 상태는 Unity Component 연결, Scene 실행 또는 Game View 검증 완료를 의미하지 않는다.

모든 표현 규칙은 기준 원장을 변경하지 않고 업무 완료를 확정하지 않는다고 명시해야 한다. 업무 규칙 영역, 서버 Command 효과, 생산·운송·입고·소비 완료를 표현 출력으로 선언하면 검증에서 차단한다. 카메라 도착, 애니메이션 종료, Renderer·GameObject 변화는 계속 업무 완료 근거가 아니다.

## D-089 통합 전시관의 규칙 실험대는 미리보기와 서버 재조회 결과를 비교한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-049의 Unity 상태 재조회, D-081의 개별 배치 객체 이식, D-083의 규칙 계층과 D-088의 표현 규칙 비권위성을 전시관 실험 흐름에 적용함

통합 전시관의 규칙 실험대는 생산·소비·운송·창고 규칙을 Unity에서 다시 계산하거나 기준 원장을 직접 변경하지 않는다. 서버 기준 상태 사본과 예상 효과를 불러와 이전 값·증감값·예상 이후 값을 보여주고, 명시적 Simulation 확정 뒤에는 서버의 최신 상태 사본을 다시 조회해 예상값과 실제값을 대조한다. 확정 요청만으로 Unity의 기준 상태를 바꾸지 않는다.

표현 규칙 실험은 그래픽·카메라·애니메이션·조명·오디오·UI의 차이를 미리보기로만 보여준다. 표현 효과는 기준 원장 자원 효과를 만들거나 Simulation 확정을 요청할 수 없다. 모든 실험 시나리오는 기존 모판 배치 객체의 고유 식별자를 참조하고 실운영 API를 호출하지 않는다.

규칙 실험대의 공통 상태는 기준 상태 준비, 미리보기 표시, 서버 재조회 대기, 예상값과 재조회값 대조 완료, 실패로 구분한다. 실제 Scene 버튼·Prefab·카메라가 연결되거나 Game View가 확인되기 전에는 코드 계약 검증을 전시관 실행 완료로 보고하지 않는다.

## D-090 Unity 감자 생산 실험대는 서버 효과를 재계산하지 않고 변환한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-084의 감자 생산 Fixture 규칙과 D-089의 전시관 미리보기·서버 재조회 대조 경계를 첫 생산 실험대에 적용함

Unity의 감자 생산 실험대 변환기는 `Ssalddel.Simulation` 구현이나 서버 Entity를 직접 참조하지 않고 서버 API 사본만 소비한다. 유효 재배 면적, 기본 수확량, 환경·투입·시설·손실 계수가 반영된 예상 수확량을 Unity에서 다시 계산하지 않는다. 서버가 제공한 생산 효과선의 이전 값, 증감값, 이후 값과 예상 수확량이 서로 일치하는지만 검증해 모판 기준 상태와 미리보기로 변환한다.

첫 감자 생산 실험은 `rule:potato-production.fixture.v1`, `Production`, `Simulation`, 적용 전 `Pending` 효과만 허용한다. 실운영 모드, 서버 권위가 아닌 상태, 기준 개정 번호 불일치, 수확량과 효과선 불일치, 대장과 다른 규칙 고유 식별자는 거부한다.

Simulation 확정 뒤 최신 상태 사본에는 미리보기에서 사용한 효과 묶음의 적용 기록이 있어야 한다. 적용 기록과 더 최신 개정 번호를 확인한 뒤에만 수확 재고 원장의 실제값을 예상값과 대조한다. API 사본 변환기 검증은 실제 HTTP 연결, Unity Scene 배치 또는 Game View 검증을 대신하지 않는다.

## D-091 다품목 Unity 모판은 서버가 보장하는 연결 깊이를 품목별로 구분한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-081의 개별 배치 객체, D-085의 수요·예약·이행·소비 분리, D-089의 서버 재조회와 D-090의 생산 효과 비재계산 원칙을 감자 외 품목과 운영 API에 확장함

Unity 모판은 서버 공개 상품 API가 반환하는 품목을 개수 제한 없이 투영하되, 모든 품목이 생산 규칙까지 연결됐다고 가정하지 않는다. 현재 감자는 생산 Fixture에서 마트 표현까지 연결된 품목이고, 쌀·양파는 공개 상품·가격·판매 가능 수량과 마트 업무만 연결된 품목이다. 공개 상품에 생산 품목 기준 고유 식별자가 없으면 이름이 같다는 이유로 생산 Lot·Cargo·재고 계보를 자동 연결하지 않는다.

마트 주문 요청은 주문 확정이 아니라 비구속 구매 의향이다. Unity는 인증, 개인정보 동의 증적, 수량, 현재 안내 버전과 명시적 확인을 미리보기에서 점검할 수 있지만 서버가 현재 상품 상태와 권한을 다시 검증하고 기록한다. 등록 응답 뒤 같은 주문 의향 상세를 다시 조회해야 Unity 상태를 갱신한다.

비구속 주문 의향은 재고 차감·예약, 결제, 피킹·포장, 배송 또는 계약을 만들지 않는다. Unity API 사본은 기존 한국어 JSON 필드명, API 경로와 상태 코드를 변경하지 않는다. Client 인터페이스와 결정적 테스트는 실제 UnityWebRequest, 인증 세션, 운영 DB 저장, Scene 또는 Game View 검증을 대신하지 않는다.

## D-092 Unity 운영 API Client는 공통 전송 계층과 인증 경계를 재사용한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-073의 서버 재조회 원칙과 D-091의 다품목·비구속 주문 의향 경계를 실제 Unity 프로젝트 Client에 적용함

도심마트 전용 Client는 별도의 HTTP 전송 구현을 만들지 않고 실제 Unity 프로젝트의 공통 `IUnityApiClient`와 `UnityWebRequestApiClient`를 사용한다. 공개 상품 목록은 인증 없이 조회하고, 개인 주문 의향 등록과 상세 재조회는 인증 토큰을 요구한다. API 경로, 기존 한국어 JSON 필드, 상태 코드는 변경하지 않는다.

운영 HTTP 실패를 Simulation 자료로 대체하지 않는다. Simulation 표본과 운영 서버 자료는 조립 시점부터 명시적으로 분리하며, 운영 Client의 오류는 사용자에게 설명 가능한 실패 상태로 전달한다. Client 집중 테스트와 Editor 재컴파일은 실제 서버 기동, 유효 인증 토큰, 운영 DB 기록, Scene·Play Mode 또는 Game View 검증을 대신하지 않는다.

## D-093 게임 세계 Simulation 서버를 실제 운영 전 예행연습 서버로 사용한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-027의 물리 분리와 D-092의 Unity Client 인증 경계를 유지하면서 상시 서버 운영 구성을 두 서버로 단순화함

> 두 상시 서버라는 배포 형태는 D-557이 대체한다. 실제 운영 전 가상 업무 규칙을 반복 검증하되 운영 원장으로 자동 승격하지 않는 목적은 계속 유효하다.

`Ssalddel.Simulation.Server`는 게임 세계의 scenario·seed·session·가상 시간·save·replay 권위를 유지하면서 생산·소비·운송·창고·시장 업무 규칙을 실제 운영 전에 반복 검증하는 예행연습 서버 역할을 함께 맡는다. 별도의 세 번째 상시 예행연습용 운영 서버를 기본 구조로 두지 않는다. `Ssalddel`은 WebApp·MAUI와 Unity 운영 기능이 사용하는 실제 운영 서버다.

Unity는 두 서버 주소와 Client 타입을 분리한다. 공개 상품·공공데이터·실제 사용자 의향처럼 운영 API를 읽거나 쓰는 Client는 운영 서버만 사용하고, WorldTick·가상 자원·Simulation Confirm은 예행연습·게임 세계 서버만 사용한다. Simulation 결과와 가상 식별자는 운영 원장으로 자동 승격하지 않으며, 두 서버가 공통 의미를 가져야 할 때는 명시적 계약·고유 식별자·데이터 계보와 호환성 테스트를 사용한다.

예행연습 서버의 검증 근거를 운영 배포 판단에 사용하려면 영속 save·replay, 기준 scenario, 실패·재시도 시험과 운영 계약 호환성 검증을 단계적으로 갖춰야 한다. 운영 배포 직전의 동일 빌드 기동·migration·설정 검증은 별도 일회성 배포 검증으로 남기며 세 번째 상시 권위 서버로 취급하지 않는다.

## D-094 Simulation 서버는 수집된 공공데이터 DB를 읽기 전용으로 공유한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-027의 두 서버 물리 분리와 D-093의 게임 세계·예행연습 서버 역할에서 공공데이터 저장소만 공유하는 예외를 명시함

운영 서버가 외부 제공처에서 수집한 공공데이터는 Simulation 규칙의 공통 입력 근거이므로 `Ssalddel.Simulation.Server`도 같은 MySQL DB와 농수산 공공데이터 스키마를 조회한다. 자료를 복제한 별도 Simulation 공공데이터 DB를 기본 구조로 두지 않는다.

공유 범위는 공공데이터 전용 `AgriculturalFisheriesDbContext`의 읽기뿐이다. Application은 조회 포트를 정의하고 `Ssalddel.Simulation.Persistence`가 EF 조회를 구현하며 Server는 이 전용 연결 모듈만 조립한다. Server가 운영 `Ssalddel.Infrastructure`나 `Ssalddel.Domain`을 직접 참조하지 않도록 유지한다. 운영 업무용 `SsalddelContext`, 공공데이터 수집기, migration과 초기화 작업은 등록하지 않으며 조회 추적과 `SaveChanges`를 차단한다. 배포 환경에서는 같은 DB를 가리키는 별도 읽기 전용 계정을 사용해 응용 코드 밖에서도 쓰기를 막는다.

Simulation session, WorldTick, 가상 재고, save와 replay는 공공데이터 DB에 저장하지 않는다. 공공데이터 관측은 생산·시장 규칙의 입력 근거가 될 수 있지만 실제 주문·재고·계약 완료 사실이나 Simulation 결과를 뜻하지 않는다. Unity 표현 또한 공공데이터의 출처·기준 시각·단위·결측 여부를 유지해야 하며 화면 변화만으로 운영 사실을 만들지 않는다.

## D-095 운영자 전용 재고 Shelf는 주소 지정 가능한 피킹 위치 단위다

- 상태: `Accepted`
- 결정일: 2026-08-12

Unity의 운영자 전용 재고 Shelf는 창고 전체나 상품 한 종류가 아니라 운영자가 주소로 접근할 수 있는 적재·피킹 위치 한 곳을 표현한다. 같은 위치의 여러 재고 항목은 한 Shelf 상태로 묶을 수 있으며, 한 상품이 여러 위치에 나뉘면 여러 Shelf로 표현한다.

Shelf 수량은 창고 기준 원장의 상태 사본에서 읽고 Unity가 재계산하거나 변경하지 않는다. 위치가 없는 재고는 임의 Shelf로 만들지 않으며 관련 피킹 작업은 `LocationUnmapped`로 남긴다. 실제 피킹 완료는 Unity 이동이나 애니메이션이 아니라 서버 명령 뒤 기준 원장 재조회로 확인한다.

## D-096 SimulationWorldShell의 플레이어 카메라는 Presentation 전용 입력 모듈이다

- 상태: `Accepted`
- 결정일: 2026-08-12

`SimulationWorldShell`의 실제 사용자 탐색은 Scene View 편집 카메라가 아니라 `PlayerCameraRig/CameraPivot/Main Camera` 계층과 Input System 기반 전략 카메라가 담당한다. 이동·회전·Zoom과 배치 객체 초점은 Presentation 상태이며 서버 원장, Simulation 상태, `WorldTick`, 상태 버전이나 업무 완료를 바꾸지 않는다.

자유 탐색과 배치 객체 초점은 명시적으로 구분한다. 배치 객체 선택과 `ESC` 복귀는 별도 단계로 연결하고 UI 위의 포인터 입력은 World 선택으로 전달하지 않는다.

## D-096 일반 타로를 경영 게임의 기본 덱으로 두고 학당 카드는 선택형 확장으로 분리한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-070의 명시적 턴 마감, D-078의 카드 모판 승격과 기존 학당 카드 구현을 일반 타로 기반 지역 경영 게임으로 확장함

`살뜰 아르카나`의 기본 덱은 특정 학당 해석을 전제로 하지 않는 일반 타로 원형과 게임용 경영 해석으로 구성한다. 기존 `learning:hongik.*` 카드는 고유 식별자와 저장 의미를 유지하는 선택형 학당 콘텐츠로 남기며, 같은 타로 원형의 일반 게임 카드는 별도 고유 식별자·해석 개정 번호·효과 규칙 개정 번호를 사용한다.

타로 원형, 게임용 해석, 시나리오 덱, 개별 카드 제안, 경영 대응과 배치 객체 반응 관점별 조회 결과를 분리한다. 카드 선택은 다음 날 적용할 조건과 대응을 열 뿐 생산·운송·입고·재고·판매를 직접 확정하지 않는다. 실제 업무 변화는 기존 시뮬레이션 업무의 미리보기·명시적 확정·기준 상태 사본 재조회로만 적용한다.

첫 뼈대는 여제·전차·정의·절제 네 장, 결정론적 3장 제안, 한 장과 대응 선택, O6 실제 World 배치 검증 완료 객체 7개의 영향 미리보기까지만 닫는다. 지역 신뢰와 환경·시설 상태를 임의의 단일 점수로 만들거나 타로 78장 전체와 최종 밸런스를 먼저 확정하지 않는다.

## D-097 타로 규칙은 기존 업무 규칙에 보정선을 제공하는 상위 시뮬레이션 규칙이다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-082의 자원 효과 묶음, D-083의 규칙 계층과 D-096의 일반 타로 기본 덱을 실제 시뮬레이션 계산 순서로 구체화함

활성 타로 카드의 방향과 경영 대응은 생산·소비·운송·창고·시장·시설·시간 규칙보다 상위에서 적용할 하위 규칙과 플러스·마이너스 보정선을 결정한다. 각 보정선은 대상 연결 지점, 계산 방식, 값, 단위, 허용 범위, 활성 턴, 원천 카드와 상위 규칙 개정 번호를 보존한다. 정방향과 역방향은 같은 값의 부호를 자동 반전하지 않고 각각 독립된 기회·부담 보정 묶음으로 관리한다.

기존 하위 업무 규칙은 허용하는 보정 연결 지점과 범위를 명시하고, 타로 보정을 소비해 기준 후보와 다른 최종 후보를 계산한다. 등록되지 않은 연결 지점, 단위 불일치, 호환되지 않는 규칙 개정 번호와 허용 범위 초과는 실패 상태로 닫는다. 카드가 활성화되지 않았을 때는 기존 규칙 결과와 완전히 같아야 한다.

타로 상위 규칙은 수량 보존, 음수 재고 금지, 차량·창고 용량, 단위 일치, 권한, 현재 상태 전이와 Simulation·Operational 분리를 우회할 수 없다. 이 불변 안전 조건은 보정 적용 뒤에도 다시 검증한다. 미리보기는 보정 전 기준 후보·보정선·보정 후 최종 후보를 함께 반환하고 기준 원장을 바꾸지 않으며, 실제 효과는 기존 Confirm·Task·WorldTick과 기준 상태 사본 재조회 경계에서만 적용한다.

## D-098 일반 타로 뽑기는 seed·턴·덱 개정 번호·선택 이력으로 결정한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-070의 명시적 턴 마감, D-096의 일반 타로 기본 덱과 D-097의 상위 규칙 적용 출처를 결정적 제안 계약으로 구체화함

일반 타로의 첫 시작 덱은 여제·전차·정의·절제 각 3복사본, 총 12칸으로 구성한다. 카드 원형의 고유 식별자, 덱 안의 카드 복사본 고유 식별자와 특정 턴의 제안 고유 식별자를 분리한다. 같은 카드 종류가 한 번의 제안에 중복되더라도 복사본과 제안 식별자는 충돌하지 않는다.

시뮬레이션 서버는 시나리오 seed, 턴 번호, 덱 개정 번호와 이전 타로 제안 선택 이력으로 세 장과 각 정·역방향을 결정한다. Unity 프레임 순서, 클라이언트 시각이나 클라이언트 난수는 결과 권위가 아니다. 턴 마감 요청은 서버가 제안한 고유 식별자·카드 고유 식별자·방향이 모두 일치해야 하며, 선택된 복사본·제안·방향은 턴 기록과 save·replay hash에 보존한다.

기존 학당·문화 카드 목록은 호환 경계로 유지하고 일반 타로 제안 묶음과 구분한다. 카드 선택은 다음 턴의 상위 규칙 조건을 활성화할 뿐 하위 업무 원장을 직접 변경하지 않는다.

## D-099 타로 객체 관계와 현재 강조 상태를 분리한다

- 상태: 확정
- 결정일: 2026-08-12
- 관계: D-078의 모판 승격, D-096의 첫 O6 객체 범위와 D-097의 타로 상위 규칙 경계를 객체 반응 미리보기로 구체화함

타로 카드와 배치 객체의 영향 가능 관계는 개정 번호가 있는 서버 구성 대장으로 관리한다. 첫 구성 대장 `integrated-seedbed:o6.r1`은 O6 실제 World 배치 검증을 마친 감자 수확 상자, 농장 출하 상자, 배송 차량, 공용 화물 Pallet, Hub 입고 Gate, 도심마트와 집단수요 Cart Table 일곱 객체만 포함한다. Unity의 이름 검색, 태그 추측이나 화면 배치 순서는 이 관계의 권위가 아니다.

현재 강조 상태는 정적 관계와 별도로 현재 Simulation 세션의 수확 배정, 화물운송, 물류 이동, 마트 공급과 공동주문 상태에서 계산한다. 카드와 연결되어 있어도 현재 관련 상태가 없으면 영향 가능 객체로는 반환하되 강조하지 않는다. 근거가 된 상태 고유 식별자와 상태 부재 사유를 함께 반환해 같은 서버 상태에서 재현할 수 있게 한다.

객체 반응 미리보기는 카드 제안과 현재 상태를 읽는 후보 조회이며 세션 개정 번호, 기준 원장, 카드 선택 기록이나 운영 원장을 변경하지 않는다. 객체의 색 변화, 차량 이동 또는 Gate 표현은 생산·운송·입고·재고 업무의 확정 근거가 아니며 실제 변화는 기존 Confirm·Task·WorldTick 뒤 기준 상태 사본 재조회로만 확인한다.

## D-100 공간 World는 고정 Tile·Area·AreaSet과 통계 구성 대장으로 반복 생성한다

- 상태: `Accepted`
- 결정일: 2026-08-13

EPSG:5186 고정 격자의 L0 8km, L1 2km, L2 500m Tile을 공간 Layer 처리와 cache 단위로 사용한다. 법정동과 Farm·Town·Hub는 Tile을 참조하는 Area, 이들과 회랑은 AreaSet으로 구성한다. 기존 Synty `Composition` 세트는 Area가 아니라 Area 안의 교체 가능한 경관 조각이다.

DEM·WorldCover·법정동 경계는 배치 가능 위치, 환경부 세분류 면적 통계는 행정구역 전체의 구성 목표로만 사용한다. 세분류 공간 SHP가 없는 논·밭·수종 배치는 `StatisticallyAllocated`이며 실제 위치로 표현하지 않는다. 후보 면적이 목표보다 적으면 새로 만들지 않고 `UnresolvedTargetArea`로 남긴다.

`PhysicalElevation`은 경사·수계·배치 판정, `VisualElevation`은 Renderer 높이 과장에만 사용한다. 면적 배분 결과와 Synty 경관 개체 계획을 별도 산출물로 유지하고, Halo·세계 좌표 seed·수작업 Reference Tile·실제 렌더링 비용을 중간 검증한다. Unity 표현과 LOD 전환은 공간 사실이나 Simulation·운영 상태를 변경하지 않는다.

## D-101 행정구역별 건물은 출처별 DB 원장을 먼저 구축하고 World에 투영한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-100의 Area 경관 입력을 실제 건물 자료로 확장함

행정동·법정동·건물을 Unity Scene이나 Synty Prefab에서 먼저 정의하지 않는다. 행정안전부 행정기관–관할 법정동 관계, 건축HUB 건축물대장 속성, VWorld GIS건물통합정보 도형을 서로 다른 원본과 기준일로 DB화하고 검증된 관점별 조회 결과만 World가 읽는다.

법정동코드와 행정동코드는 같은 식별자가 아니며 기준일별 다대다 관할 관계를 유지한다. 건축물대장 PK, VWorld feature ID와 도로명주소 건물관리번호도 하나의 값으로 합치지 않고 근거·연결 방식·신뢰 수준을 가진 identity link로 연결한다. 행정동 경계가 있으면 건물 대표점의 공간 포함 판정을 우선하고, 관할 관계가 1:N이면 법정동코드만으로 행정동을 확정하지 않는다.

World 기본 projection은 소유자·상세 호수·주택가격·개인 연락처를 포함하지 않는다. 건물 이름이 비어 있어도 주용도·구조·층수·높이·면적·footprint 근거로 집계하며, 원본 도형이 없거나 지역 연결이 모호하면 임의 배치하지 않고 미해결 상태로 남긴다. Synty Prefab과 LOD/HLOD는 건물 원장의 표현 계획일 뿐 건물 사실이나 행정구역 배정을 변경하지 않는다.

## D-102 건축물 공식 주용도와 상위 경관 Category를 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-101의 건물 원장을 행정동별 검색·집계와 D-100의 경관 입력으로 구체화함

건축물대장의 공식 주용도 코드·이름은 관측 원문으로 보존하고, 주거·농업·물류·상업 같은 상위 Category는 별도 규칙 결과로 저장한다. Category는 건물 이름이나 Prefab 이름으로 추정하지 않으며 분류 방법, 근거 수준, 규칙 개정 번호와 분류 시각을 기록한다. 공식 주용도가 없으면 `unresolved`, 공식 주용도는 있으나 현재 규칙이 다루지 않으면 `other`로 남긴다.

행정동별 구성은 건물 원문을 직접 덮어쓰는 열이 아니라 기준시점별 집계 projection으로 생성한다. 집계에는 건물 수, 건축면적, 연면적, 이름·도형 연결·미해결 수와 재현 가능한 hash를 남긴다. Synty `WorldRoleCode`는 표현 후보를 좁히는 값일 뿐 실제 시설의 업무 역할, 물류 Hub 자격이나 Simulation 상태를 확정하지 않는다.

## D-103 건축물 형태의 공식값·단순 계산값·Synty 표현값을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-102의 건물 Category를 층수·밀도 기반 시각 구성으로 확장함

건축물대장의 공식 지상층수·건폐율·용적률은 관측값으로 보존한다. 공식 값이 없을 때 높이와 용도별 층고로 구한 층수는 `추정 지상층수`, 건축면적과 연면적을 대지면적으로 나눈 값은 `단순 건폐 비율`과 `단순 연면적 대지 비율`로 부르며 법정 건폐율·용적률로 노출하지 않는다.

`건축물형태Profile`은 공식값과 파생값·근거 종류·규칙 개정 번호·hash를 가진다. `건축물시각구성계획`은 표현 층수, 중간층 반복 수, 대지 점유·주변 여백·LOD 등급과 의미 기반 시각 Family만 가지며 항상 표현 전용이다. Synty Prefab 경로나 원본 이름은 업무·공간 원장에 저장하지 않고 `VisualKey/Catalog`에서 해석한다.

## D-104 건물 안의 상호는 공개 인허가 사업장과 보수적인 주소 연결로 표현한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-101의 건물 원장과 D-102의 용도 Category에 공개 사업장 구성을 연결함

건물 안의 사람이나 사업자를 추정하지 않고 행정안전부 지방행정인허가 자료에 공개된 사업장을 별도 원장으로 수집한다. 정규화 원장에는 사업장명, 인허가 업종·업태, 영업상태, 공개 주소·좌표, 인허가·폐업·최종수정 시각과 원본 계보를 저장한다. 대표자명·전화번호·사업자등록번호는 건물 World 구성 목적의 기본 projection에 저장하지 않는다.

사업장 주소와 건물 주소가 정확히 일치하고 건물 후보가 하나일 때만 파생 연결한다. 후보가 여러 개이거나 주소·건물 자료가 부족하면 미확정으로 남긴다. 주소 연결은 현재 입주·소유·임대 관계의 공식 증거가 아니며 공개 인허가 자료도 모든 사업체의 전수명부가 아니다. Unity는 검증된 공개 상호명이나 업종 밀도를 표현할 수 있지만, 이를 영업 자격·재고·공급 의사·물류 Hub 역할이나 실제 사람의 활동으로 확정하지 않는다.

## D-105 공유 공공데이터 DB와 Simulation World 파생 관계 DB를 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-100의 World 생성, D-101~D-104의 행정구역·건물·사업장 원장을 Simulation 저장 경계로 구체화함

운영 서버의 공공데이터 수집기가 공식 원본과 정규화 결과를 공유 공공데이터 DB에 저장한다. 운영 서버와 Simulation 서버는 이 DB를 함께 읽되 Simulation 서버는 SELECT 전용 연결만 사용하고 수집·migration·`SaveChanges`를 수행하지 않는다.

Simulation 서버가 공공데이터·공간 규칙·시나리오·Synty 구성 대장을 결합해 만든 node, 관계와 시각 배치 계획은 별도 `SimulationWorldDerived` DB에 불변 파생 실행본으로 저장한다. 각 실행본은 원본 DB·자료 개정·SHA-256, Recipe·규칙·VisualCatalog 개정, seed, 입력 fingerprint와 출력 hash를 보존한다. 같은 입력과 결과는 재사용하고 같은 실행 식별자에 다른 내용은 거부한다.

파생 DB의 `VisualKey`와 배치 계획은 표현 전용이며 Prefab 경로·원본 GUID를 저장하지 않는다. 파생 관계는 운영 계약·재고·배차·사업장 입주나 행정구역 공식 사실을 확정하지 않고 운영 DB와 공유 공공데이터 DB로 역승격되지 않는다.

새 파생 DB의 물리 표·열·외래키·인덱스와 migration 이력 표는 한국어 업무 의미를 먼저 사용한다. 다만 C# 속성명, 설정 키, API·JSON 계약, `SHA256`·`UTC`·`DB`·좌표축 같은 표준 기술 표기는 호환성과 의미 보존을 위해 유지한다. 이미 운영 중인 업무 DB와 공유 공공데이터 DB의 물리 스키마는 이 결정으로 일괄 변경하지 않는다.

영역별 건물 표현은 공유 공공데이터 DB의 건축물·공개 인허가 사업장을 원본 레코드 단위로 참조하고, `영역 포함 건물`, `건물 연결 공개 사업장`, `건물 배치 계획`을 서로 분리한다. 관측 도형·관측 대표점·영역 통계 구성·시나리오 배치 근거를 구분하며, 사업장–건물 주소 연결은 실제 입주나 소유 관계로 승격하지 않는다. 공개 상호·업종·영업상태는 표현 후보에 사용할 수 있지만 대표자·연락처·사업자등록번호는 파생 World 원장에 투영하지 않는다.

그래픽 표현 계획은 건물 배치와 다시 분리한다. 파생 DB에는 실제 자산 파일 경로 대신 질감 세트·재질 변형·색조·배경·조명·시간대의 의미 키와 그림자·LOD·품질 정책을 저장한다. Unity 구성 대장이 키를 실제 자산으로 해석하며 원본 Synty Prefab·Material을 수정하지 않는다. 그래픽 계획은 항상 표현 전용이고 공간·건물·인허가·업무 사실을 바꾸지 않는다.

## D-106 Unity 공간 실행과 Synty 경관 실행을 독립 파이프라인으로 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-105의 단일 결합 실행본 중 Synty·그래픽 결합 부분을 대체하고, 공유 공공데이터 DB와 공간 DB 분리는 유지함

공공데이터에서 Unity 공간 DB로 가는 파이프라인은 원본 계보, Tile·Area·AreaSet, 공간 관계, Unity 좌표 변환, Terrain·Mask와 배치 기준점까지만 생성한다. 새 `SchemaVersion 2` 공간 실행에는 `VisualCatalogRevision`, 그래픽 표현 계획과 `VisualKey` 시각 배치를 저장하지 않으며 공간 fingerprint에도 포함하지 않는다. 기존 `SchemaVersion 1` 결합 실행과 표는 읽기 호환을 위해 보존한다.

Synty 경관 처리는 별도 `Synty경관JobShell`이 담당한다. Job은 공간 실행 고유 식별자와 공간 출력 SHA-256, Tile·Area·AreaSet 범위, 경관 규칙, Synty 구성 대장, URP 표현 대장, seed, 대상 플랫폼과 품질 단계를 입력으로 봉인한다. 공간 출력 hash나 AreaSet이 저장본과 다르면 실행을 거부하며 Synty 실행은 공간 원장을 수정하거나 역갱신할 수 없다.

Synty 실행은 그래픽 표현 계획, 의미 기반 `VisualKey` 배치와 배치 거부 사유를 별도 불변 원장에 저장한다. Terrain·Mask·기준점이 없으면 `(0,0,0)`이나 임의 위치를 만들지 않고 `UnitySpatialArtifactMissing`으로 보류한다. Prefab·Material·Shader Graph·HLOD처럼 Unity AssetDatabase가 필요한 결합은 후속 Unity BatchMode 작업자가 수행하며, 원본 Synty Prefab·Material·`.meta` GUID는 계속 수정하지 않는다.

## D-107 Simulation 상태는 의미 기반 렌더링 의도를 거쳐 URP 표현으로 번역한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-106의 정적 Synty 경관 실행 위에 현재 Simulation 상태의 동적 표현을 합성함

Simulation Domain은 `Bloom`, Shader 값, Material 경로 같은 URP 구현을 결정하지 않는다. 확정된 Simulation 상태 사본을 별도 Projector가 `Simulation렌더링의도`로 번역하고, 표현 합성 규칙과 버전이 있는 URP 표현 대장이 플랫폼 Capability에 맞는 의미 기반 Profile 키를 선택한다. Unity 구성 대장이 이 키를 실제 `MaterialPropertyBlock`, Volume, Renderer Feature, Particle과 Animation에 연결한다.

렌더링 의도는 `Environment / Surface / Lighting / ObjectState / Attention / Fx / Animation` Channel, World·AreaSet·Area·Tile·Route·Facility·Object 범위, 우선순위, 수명, 근거, 원본 상태·Session 개정 번호를 보존한다. 같은 대상과 Channel의 충돌은 높은 우선순위, 동일 우선순위는 고유 식별자 순으로 해결하고 억제 근거를 남긴다. 기간과 개정 수명을 지난 의도는 제거하며 확인한 `OneShot` 의도는 재조회에서 반복하지 않는다.

Runtime 표현 상태 사본은 공간 실행과 Synty 시각 실행의 ID·출력 SHA-256, Simulation·World 개정, 렌더링 규칙·URP 대장·Capability 개정과 합성 결과로 결정적 hash를 만든다. 도로 표면이나 공간 근거가 없으면 흙길 먼지 같은 효과를 꾸며내지 않고 Fallback 사유를 남긴다. 표현 Pipeline은 Simulation 상태를 수정하지 않고 운영 상태를 입력으로 받지 않으며 Animation·Particle·URP 효과 완료는 업무 완료나 Command·WorldTick을 발생시키지 않는다.

## D-108 공간 규칙과 Simulation 규칙은 개정 가능한 객체 표현 결합 원장에서 만난다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-106의 공간·Synty 분리와 D-107의 Runtime 표현 사이에 객체별 해석 계보를 추가함

공간 node를 Synty Prefab이나 URP 수치에 직접 연결하지 않는다. 공간 사실을 판정하는 `공간규칙Metadata`, Simulation 상태를 설명하는 `Simulation규칙Metadata`, 두 규칙을 객체 의미·범위·우선순위·기본 구성 키·동적 표현 의도 묶음 키로 연결하는 `객체표현결합규칙`을 별도 대장으로 관리한다.

규칙 상태는 `Draft / Active / Retired`로 구분한다. 아직 확정되지 않은 Simulation 규칙은 초안으로 축적할 수 있지만 활성 해석에는 참여하지 않는다. Simulation 규칙이 없거나 초안인 동안에는 활성 공간 규칙만 참조하는 결합 규칙이 기본 외형을 제공할 수 있다. 이후 Simulation 규칙을 활성화해도 공간 실행본을 수정하지 않고 새로운 규칙 대장 개정과 해석 실행본을 만든다.

객체 표현 해석 실행은 공간 실행 ID·출력 SHA-256, 선택적 Simulation 세션·개정·WorldTick, 규칙 대장 개정, 입력 fingerprint와 출력 hash를 봉인한다. 객체별 결과에는 적용한 공간·Simulation·결합 규칙, 기본 구성 키, 동적 표현 의도 묶음 키, 미충족 처리와 근거를 저장한다. 결과는 항상 표현 전용이며 같은 대장 개정 또는 해석 실행 식별자에 다른 내용을 덮어쓰지 않는다. 의미 키에는 Prefab·Material 경로를 저장하지 않고 Unity 구성 대장이 마지막에 해석한다.

## D-109 평창군 Unity 공간 표현은 전체 원장을 보존하고 건물 종류별 하나로 축약한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-105의 공유 공공데이터 원장과 D-108의 객체 표현 결합 사이에 제한된 대표 Projection을 추가함

평창군 건축물·공개 사업장 원본은 공유 공공데이터 DB에 전수 보존한다. Unity 공간 실행과 객체 표현 규칙 편집에는 모든 건물을 그대로 복제하지 않고 건물 용도 Category마다 대표 건물 node 하나만 사용한다. 이는 원본 삭제·통계 축소·공식 건물 통합이 아니라 화면과 규칙 실험을 위한 표현 Projection이다.

대표군은 건물 용도 Category로 만든다. 각 종류에서 이름·면적·층수·높이·주소가 상대적으로 충실한 건물을 먼저 선택하고 같은 조건이면 고정 seed hash로 정한다. 같은 원본·규칙·seed에서는 입력 순서와 무관하게 같은 대표를 선택한다. 대표 node는 `대표군코드`, `대표원본건수`, `대표순위=1`을 보존하며 대표원본건수의 합은 전체 후보 수와 일치해야 한다.

공개 인허가 사업장은 선택된 대표 건물과 검증된 주소·도형 관계가 있는 항목만 표현 후보로 가져온다. 대표 건물이나 Synty Prefab 하나를 실제 회사 한 곳, 입주 관계 또는 회사 수와 동일하게 해석하지 않는다. 사용자가 상세 원본을 조회할 때는 대표 node가 아니라 공유 공공데이터 원본 레코드와 계보를 사용한다.

첫 화면 검증을 위해 종류별 대표에는 고정 seed로 `Idle / Operating / Loading / Maintenance` 중 하나의 시험 상태를 배정할 수 있다. 이 규칙은 `ScenarioFixtureBuildingActivity`이며 실제 회사 영업·작업 상태나 공공데이터 관측으로 승격하지 않는다. 건물 종류 공간 규칙과 시험 Simulation 규칙이 결합한 결과만 기본 구성 키와 동적 표현 의도 묶음 키로 전달한다.

## D-110 Simulation 서버는 운영 서버의 컨테이너 관례를 따르되 DB 권한과 migration을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-105의 공유 공공데이터 DB와 Simulation World 파생 DB 경계를 배포 구성으로 구체화함

> 별도 Simulation 컨테이너·포트·실행 모드는 D-557이 대체한다. DB 계정·상태 권위·migration 실행을 분리하는 내용은 계속 유효하다.

Simulation 서버는 운영 `Ssalddel` 서버와 같은 .NET 10 다단계 이미지, 비루트 실행, `Section__Key` 환경 변수 주입, `live`와 `ready` 상태 확인 관례를 사용한다. 실행 모드는 항상 `SsalddelExecution:Mode=Simulation`이어야 하며 기본 포트와 컨테이너 이름은 운영 API와 분리한다.

운영 업무 DB의 일반 연결 문자열을 Simulation 컨테이너에 재사용하지 않는다. 공유 공공데이터 DB에는 `SELECT`만 허용한 전용 계정을 사용하고, 파생 관계·규칙·표현 해석 실행본은 별도 Simulation World DB의 전용 쓰기 계정으로 저장한다. 개발용 fallback 연결은 Container 환경에서 사용하지 않으며 실제 secret은 source·Compose 기본값·로그에 기록하지 않는다.

상태 확인에서 `live`는 프로세스 응답만, `ready`는 설정으로 활성화된 두 DB의 연결 가능성을 검사한다. DB schema migration은 API host 시작 시 자동 실행하지 않는다. 별도 명령이나 배포 단계에서 명시적으로 적용해 읽기 전용 공공데이터 계정으로 migration을 시도하거나 여러 host가 동시에 schema를 변경하지 않도록 한다.

## D-111 Simulation World 파생 DB는 업무 규칙의 관계와 계보를 집결한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-108의 객체 표현 결합 전에 시설 의미와 실행 가능한 업무 규칙의 연결을 명시함

공간 node에 창고·마트·음식점 같은 업무 의미를 곧바로 고정하지 않는다. `시설 의미 → 시설 기능 → 업무 Simulation 규칙 → 객체–규칙 연결 → Scenario 규칙 묶음`을 독립된 개정 대장으로 저장하고, 각 시설 의미가 공간 node와 어떤 근거로 연결되었는지 보존한다. 첫 평창군 Farm·Hub·Mart·Restaurant는 실제 영업 관측이 아니라 `Scenario` 근거다.

파생 DB는 규칙 식별자·개정, 영역, 상태, Engine 키, 입력·출력 계약, 결정적 실행 여부와 Parameter를 저장하지만 규칙 실행 코드를 복제하지 않는다. 현재 주문·재고·화물·WorldTick은 Session 원장이 소유하며, 규칙 대장과 Synty·URP 표현 완료는 업무 완료나 운영 효과를 만들지 않는다.

같은 규칙 대장 개정은 공간 실행 ID·공간 출력 SHA-256·규칙 대장 SHA-256이 같을 때만 재사용한다. 시설·기능·규칙·연결·Scenario 항목은 상위 대장에 외래키로 묶고, 참조하지 않는 기능이나 규칙, 중복 식별자, 운영 규칙 혼입은 저장 전에 거부한다. 객체 표현 결합은 이 대장을 읽어 의미 기반 구성·렌더링 의도를 만들고 실제 Prefab·Material 결합은 독립 Synty·URP 파이프라인이 마지막에 수행한다.

## D-112 Unity UI 구현 전 Figma 근거 UI 기획 원장을 둔다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-111 업무 규칙 대장과 Unity ScreenModel 사이의 기획 경계를 추가함

기존 Figma의 01~09 역할 서비스 계층, 주문자의 발견→비교→참여→준비 판단, 공통 홈의 둘러보기·참여 경계·역할 선택을 UI 정보 구조의 설계 근거로 사용한다. Figma 파일·node 식별자와 확인한 구조를 계보로 저장하되 Figma를 서버 권한이나 업무 상태의 권위로 사용하지 않는다.

UI 기획 대장은 시설·역할·업무 단계별 화면 영역, 정보 항목, 상태 표현, 행동 후보와 업무 규칙 연결을 저장한다. 픽셀 좌표·Prefab·Material·색상 수치는 저장하지 않고 Unity가 World HUD·선택 정보판·업무 상세판으로 다시 구성한다. Confirm 후보는 Preview·명시적 확인·기대 개정 번호·서버 Command 키를 모두 요구하고, Command 성공 뒤 같은 원장을 재조회해야 한다.

## D-113 UI는 규칙 식별자가 아니라 객체–업무 규칙 연결을 통해 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-111의 객체–업무 규칙 연결과 D-112의 UI 기획을 하나의 검증 가능한 계보로 연결함

UI 화면이 규칙 식별자를 별도 대응표로만 참조하면 규칙의 시설이나 시설 기능이 바뀌어도 잘못된 화면 연결이 남을 수 있다. `SchemaVersion 2` UI 기획은 원본 객체–업무 규칙 연결 고유 식별자와 시설 기능 코드를 함께 저장하고, 화면 시설·기능·규칙 개정의 일치를 검증한다. 활성 원본 연결은 UI 기획에서 빠지거나 중복될 수 없다.

지역별 UI 구성 차이는 Application의 UI 기획 조립기가 담당한다. Job Shell은 업무 규칙 대장 읽기, 조립기 호출과 불변 원장 저장만 조율하며 평창군 화면 정의를 직접 소유하지 않는다. Unity는 이 원장을 ScreenModel로 투영하지만 현재 Session 상태·권한·Command 성공 여부를 UI 기획 DB에서 만들지 않는다.

## D-114 Tile과 경관 완결 영역의 책임을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-108 공간–Simulation 규칙 결합과 D-111 업무 규칙 집결 전에 반복 가능한 공간 완결 범위를 고정함

L0 8km·L1 2km·L2 500m Tile은 공간 원본 절단, fingerprint, 캐시와 부분 재생성의 기술 단위다. 미술·Simulation 규칙·UI·Unity Runtime을 사람이 함께 검수하는 단위는 인접 L2 타일 2×2를 묶은 1km×1km `경관 완결 영역`으로 둔다. Area는 법정동·Farm·Hub·Town 의미 범위이고 AreaSet은 완결 영역·Area·회랑을 참조하는 시나리오 묶음이므로 서로 대체하지 않는다.

첫 완결 영역은 대관령 Farm으로 고정한다. 전체 평창군 타일을 먼저 산출하지 않고 해당 L2 네 타일과 필요한 L1·L0 상위 타일만 처리한다. 완결 상태는 원자료, 물리 공간, 공간 의미, Scenario 규칙, 경관 계획, UI 계획, Unity Runtime, 최종 검증 관문을 모두 기록하며 자료 대기나 Editor 증거 대기를 완료로 승격하지 않는다. 같은 원본·규칙·seed·타일 범위는 같은 완결 영역 hash를 생성한다.

## D-115 경관 품질은 Synty 연결 뒤 독립 Rendering Profile로 일괄 적용한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-106의 독립 Synty Pipeline과 D-114의 경관 완결 영역 뒤에 Unity 표현 품질 관문을 추가함

공간 사실과 Simulation 규칙은 URP의 조명·안개·색보정 수치를 직접 소유하지 않는다. 영역 역할, 시간대, 계절과 날씨 같은 의미 코드를 `RenderingProfile` 고유 식별자로 해석한 뒤 Unity의 독립 `경관 품질 후처리` 단계가 태양, 환경광, 그림자, 하늘, 대기 원근, 색보정, Bloom, Vignette와 카메라 모드를 일괄 적용한다.

## D-116 플레이어 경관 탐색은 Simulation 권위와 분리한 표현 전용 입력이다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-096 플레이어 카메라 원칙과 D-115 경관 품질 Profile을 Farm 완결 영역의 직접 보행으로 확장한다.

Unity 플레이어는 의미 기반 플레이어 루트가 `CharacterController`, Synty `VisualRoot`와 1인칭 시선 회전축을 소유하고, 영역의 표현 계층은 독립 RTS 전술 카메라 회전축을 소유한다. F2 1인칭의 WASD·방향키·Shift·마우스 시선과 F3 RTS 전술 화면의 유닛 선택·지형 우클릭 목적지 이동·WASD 화면 이동·휠 줌은 공간을 관찰하는 `PresentationOnly` 입력이다. F1은 기존 전략 화면으로 복귀한다. 어느 조작도 서버 상태, `WorldTick`, 상태 개정 번호나 업무 완료를 변경하지 않는다. Synty 캐릭터 Prefab과 동작은 교체 가능한 표현이고 플레이어 고유 식별자나 업무 권위를 갖지 않는다.

RTS 전술 3인칭은 캐릭터 어깨를 따라가는 근접 추적 카메라가 아니라 높은 사선에서 영역·건물·유닛을 함께 읽는 독립 초점 카메라다. 전술 초점은 WASD·방향키로 이동하고 휠로 확대·축소하며 F로 선택 유닛에 재집중한다. 선택 원과 목적지 표식은 `VisualRoot` 바깥의 표현 계층에 둔다. 우클릭 목적지는 지형 충돌 결과와 완결 영역 이동 경계로 제한하고 현재 단계에서는 로컬 직선 이동만 수행한다. 실제 도로 경로 탐색, 화물 배차, 기사 업무 이동이나 서버 Command로 승격할 때는 별도 권위 계약과 확인 절차를 추가한다.

같은 공간·Synty 배치에 Overview, Region, Task와 1인칭 카메라를 함께 제공할 수 있지만 카메라 이동과 화면 효과는 항상 `PresentationOnly`다. 1인칭 이동은 WorldTick, 상태 개정, 주문·생산·운송 완료를 바꾸지 않는다. Synty 원본 Prefab·Material을 수정하지 않고 Volume Profile, wrapper와 카메라 설정을 교체해 지역별 미감을 조정한다.

첫 Profile은 `rendering-profile:sim:pyeongchang:rural-clear-day.v1`이며 대관령 Farm 1km 완결 영역에서 검증한다. 다른 Farm·Town·Hub는 같은 처리기를 재사용하고 Profile만 교체한다. 실제 날씨나 시간대와 연동할 때도 서버는 의미 코드와 근거만 제공하며 Unity가 구체적인 URP 수치를 해석한다.

## D-117 NPC 직업·권한은 운영 인증이 아니라 Simulation 조직·역량·위임 규칙으로 실행한다

- 상태: `Accepted`
- 결정일: 2026-08-13
- 관계: D-111의 업무 규칙 집결과 D-116의 표현 전용 캐릭터 조작 사이에 NPC 업무 실행 권위를 추가함

현재 NPC 행동은 운영 사용자의 로그인 세션, 실제 HR 조직, API 역할 Annotation이나 운영 시설 권한을 조회하지 않는다. Simulation Session이 시나리오 조직, NPC 행위자, 기술, 역량 부여, 관리자 위임과 업무 정책을 소유하고 같은 seed·입력·WorldTick에서 결정적으로 담당자와 행동 단계를 계산한다.

첫 세로 단위는 진부면 물류 거점의 입고검수다. 도착 화물의 검수 작업은 `배정 → 이동 → 작업 → 완료`를 거쳐야 보관 가능한 재고가 되며, 자동화 중지나 적격자 부재는 작업 삭제가 아니라 `Blocked` 상태로 남긴다. 시나리오 관리자는 같은 조직·시설 범위 안에서만 역량을 위임할 수 있고 자동 위임으로 받은 역량은 다시 위임할 수 없다.

사용자 개입은 NPC를 직접 조종해 업무 완료를 만드는 방식이 아니라 자동 배정, 자동 위임, 우선순위와 선호 담당자 같은 Simulation 정책을 변경하는 방식으로 둔다. Unity는 `Npc업무행동Projection`을 따라 캐릭터 위치와 Idle/Walk를 표현할 뿐 WorldTick, 작업 완료, 재고 상태나 권한을 변경하지 않는다.

## D-118 UI 행동은 실행 가능한 호출 계약과 확정 뒤 재조회를 함께 제공한다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-112 UI 기획과 D-113 객체–업무 규칙 연결을 첫 런타임 수직 단위로 구체화함

UI 기획 DB는 화면에 무엇을 보여줄지 정의하지만 현재 Session 상태나 버튼 활성 여부를 저장하지 않는다. Application의 관점별 조회 서비스가 같은 Session 원장에서 상태·WorldTick·업무 대상·규칙 근거를 읽어 `SimulationWorldUIProjection`을 생성한다. 따라서 UI 상태를 위한 별도 쓰기 원장이나 migration을 만들지 않는다.

실행 가능한 UI 행동은 기능 키와 표시 이름만 전달하지 않는다. HTTP 방식, route template, 요청·응답 계약 키, 대상 고유 식별자와 대상 개정, 기대 Session 개정, 행위자, 확정 뒤 canonical 재조회 route를 함께 전달한다. 클라이언트는 가격·수량·개정이나 성공 상태를 자체 계산하지 않고 Preview 요청, 명시적 Confirm, 같은 정보판 재조회 순서를 따른다.

첫 적용은 진부면 물류 거점 입고 검수다. 도착 가능한 화물과 적격 NPC가 있을 때만 Preview·Confirm을 활성화하고, Confirm 뒤 NPC 작업과 화물 인수가 실제 Simulation `WorldTick`에서 완료되어 보관 가능 재고가 생긴 뒤에만 정보판을 `Completed`로 표시한다. Unity 버튼과 animation은 이 상태를 표현하지만 독립적으로 완료를 만들지 않는다.

## D-119 입고 검수 완료와 적재 완료를 다른 상태·행동으로 관리한다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-117의 NPC 검수 실행과 D-118의 정보판 완료 판정을 적재까지 확장하며, D-118의 검수 후 즉시 `Completed` 판정을 대체함

공통 입고 흐름은 `입고예정 → 검수대기 → 적재대기 → 적재완료`를 사용한다. Simulation의 `StorageEligible`은 검수를 통과했으나 아직 적재하지 않은 `적재대기` 상태로 해석하고, 같은 재고 고유 식별자와 개정을 참조하는 별도 적재 Preview·Confirm·NPC 작업이 완료된 뒤에만 `적재완료` 상태로 전이한다.

운영 MAUI는 기존 운영 API에 수령 기록을 저장하고 정확한 입고 요청·입고상품 ID를 재조회하는 경계를 유지한다. Simulation은 같은 canonical 업무 코드를 사용하되 운영 로그인·HR·운영 재고를 변경하지 않는 독립 Session 원장으로 실행한다. `SimulationWorldUIProjection`은 업무 코드·현재 단계·실행 모드·canonical 행동 코드를 함께 전달하며, Unity는 이 상태를 표현할 뿐 검수나 적재 완료를 독자적으로 만들지 않는다.

## D-120 Figma·MAUI·Unity는 디자인 의미를 공유하고 렌더러 구현은 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-112의 Figma 근거, D-118의 실행 계약, D-119의 검수–적재 흐름을 Unity 실제 정보판으로 구체화함

Figma와 MAUI에서 확인한 역할 색, 상태 배지, 정보 우선순위, Preview·Confirm 문법은 `DesignProfileRevision`, Layout Profile과 역할·상태·정보·행동의 의미 키로 공유한다. 서버는 RGB, 글꼴, UGUI 계층이나 Prefab 경로를 내려보내지 않고 현재 상태와 표현 의도만 제공한다. MAUI와 Unity는 같은 의미를 각 실행 환경에 맞게 렌더링하며 디자인 개정이 맞지 않으면 Unity는 중립 표현과 경고를 사용한다.

Unity 기본 Scene은 Simulation 서버를 권위로 사용한다. 클라이언트는 Projection이 허용한 검수·적재 Preview, 명시적 Confirm과 WorldTick만 호출하고 확정 뒤 같은 정보판을 다시 조회한다. 마지막 성공 상태와 stale 표시는 유지하지만 네트워크 실패를 fixture 성공으로 바꾸지 않는다. 결정적 fixture는 EditMode·PlayMode와 화면 증거 생성에만 명시적으로 사용하며 운영 API·운영 DB나 실제 HR 권한을 호출하지 않는다.

## D-121 Unity 최종 실행은 SimulationWorldShell 하나에 통합한다

- 상태: 확정
- 날짜: 2026-08-14
- 관계: D-115의 농장 플레이어 시점과 D-120의 진부 입고 정보판을 하나의 실행 맥락으로 통합함

Unity의 월드 개요, 대관령 Farm 1인칭, RTS형 농장 전술 시점과 진부 Hub 입고 정보판은 `SimulationWorldShell` 하나에서 전환한다. Build Settings에서 활성화하는 최종 Play Scene도 이 Scene 하나로 제한한다. `WorldBootstrapScene`과 기능별 연구 Scene은 삭제하거나 자동 합성하지 않고 관찰·회귀 검증 자료로 보존하며 독립 게임 진입점으로 사용하지 않는다.

화면 전환 막대는 카메라 교체와 무관하게 유지되는 Overlay Canvas로 구성한다. 모드 전환은 WorldRoot의 가시성, 카메라 초점과 정보판 문맥만 바꾸는 `PresentationOnly` 동작이며 서버 상태, `WorldTick`, 상태 개정 번호나 업무 완료를 변경하지 않는다. 새로운 기능은 연구 Scene에서 근거를 확인한 뒤 같은 `SimulationWorldShell`에 좁게 통합하고 별도의 최종 Play Scene을 추가하지 않는다.

## D-122 1인칭 월드는 고정 L2 타일 창과 자료 상태를 따라 동적으로 준비한다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-114의 기술 타일과 D-116의 1인칭 탐색을 D-121 단일 Play Scene의 런타임 생명주기로 연결함

대관령 Farm의 첫 동적 공간은 EPSG:5186 L2 500m 타일을 원본·캐시 식별 단위로 사용한다. 플레이어가 있는 중심 타일 주변 `3×3`은 활성 창, `5×5`는 미리 준비하는 창으로 유지하며 타일을 벗어나면 더 이상 필요하지 않은 표현 루트는 파괴하지 않고 풀로 돌려 재사용한다. 첫 검증 범위는 중심 `kr5186:l2:700:1145`의 5×5이고 같은 Recipe·원본·규칙 개정은 같은 타일 hash를 제공한다.

시뮬레이션 서버는 Recipe, Tile Manifest, Layer 산출물 상태와 활동 관점별 조회 결과를 읽기 API로 제공한다. Unity는 이 계약을 읽어 표현 생명주기만 바꾸며 `WorldTick`, Session 개정, 주문·생산·운송 완료를 확정하지 않는다. 실제 DEM·토지피복·배치 마스크 산출물이 없으면 URL·hash·높이 Mesh를 꾸며내지 않고 `WaitingForSpatialArtifact`를 표시한다. 개발 Fixture도 같은 결손 상태와 `PresentationOnly` 경계만 표현하며 실제 공간 증거가 아니다.

첫 단계는 Addressables나 전국 스트리밍을 도입하지 않는다. 실제 산출물 저장·다운로드·검증이 연결되기 전까지 동적 경계는 Collider 없는 상태 표시이고 기존 `ScenarioTerrainPreview`를 물리 DEM으로 승격하지 않는다. 장거리 확장 시 원점 이동과 다중 사용자 위치 전송은 이 타일 식별자를 재사용하되 별도 권위·성능 검증 뒤 활성화한다.

## D-123 타일 안전 창과 카메라 시야 기반 표현 우선순위를 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-122의 고정 타일 생명주기에 1인칭 시야·건물 표현 승격과 이동 안전 경계를 추가함

플레이어 주변 `3×3` 활성·`5×5` 준비 창은 이동 안전과 자료 가용성을 위한 방사형 범위로 유지한다. 카메라 절두체, 화면 가장자리 여백과 짧은 이동 예측은 이 창 안에서 무엇을 먼저 표현할지만 결정한다. 시야 밖 타일을 즉시 버리거나 카메라가 보지 않는다는 이유로 안전 지면을 해제하지 않는다.

타일별 건물 조회 결과는 `Declared → ProxyQueued/ProxyActive → DetailQueued/DetailActive → HiddenCached` 상태를 갖는다. 먼 거리·예측 시야에는 충돌 없는 단순 프록시를 먼저 배치하고, 실제 시야와 상세 거리 안에 들어오면 의미 기반 `VisualKey`를 Synty Prefab으로 해석한다. 화면 밖으로 나간 표현은 유예 시간 뒤 비활성 캐시에 두며 다시 보일 때 재사용한다. 이 상태 전이는 모두 `PresentationOnly`이고 공공데이터 관측, 업무 완료, `WorldTick`이나 Session 개정을 만들지 않는다.

실제 DEM·배치 마스크가 준비되지 않은 서버 모드에서는 다음 위치의 타일이 추적 중이어도 안전 기반 Layer와 지면 충돌이 모두 확인될 때만 이동한다. 명시적 개발 Fixture는 기존 `ScenarioTerrainPreview` Collider를 이동 검증용으로 사용할 수 있지만 이를 물리 DEM 완료로 승격하지 않는다. 타일·시야·프록시·상세·캐시·안전 판정은 런타임 진단 트리로 함께 표시한다.

## D-124 월드 API는 행정동·법정동 파생 Projection을 먼저 읽는다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-101·D-102의 행정구역·건물 원장과 D-114·D-122의 타일 Pipeline 사이에 서버 가공 경계를 추가함

Simulation 서버는 월드 요청마다 공유 공공데이터 원문을 직접 조인하지 않는다. 공간 파생 실행이 건축물–법정동·행정동 Assignment, 법정동–행정동 관할 관계와 행정동별 건물 Category 집계를 먼저 불변 node·relation으로 저장하고, 월드 API는 최신 완료 실행의 지역 Projection을 읽는다.

법정동과 행정동은 서로 다른 고유 식별자와 근거를 유지한다. 경계 geometry와 지역–타일 교차 관계가 없으면 타일을 추정하지 않고 `WaitingForRegionGeometry`를 반환한다. 이 Projection은 Unity 공간 표현을 위한 파생 읽기 결과이며 운영 시설 자격, 주문·재고·운송 상태나 Simulation 업무 완료를 확정하지 않는다.

## D-125 L2 Runtime은 상세 3×3·활성 5×5·준비 9×9의 예산형 창을 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-14
- 관계: D-122·D-123의 고정 타일 창을 공식 엔진 조사와 경계 선행 준비로 대체함

500m L2의 첫 PC Profile은 3×3 상세 후보, 5×5 활성 지형·상호작용, 9×9 Manifest·산출물 준비 창을 사용한다. 대관령 Fixture의 11×11 제공 범위는 한 칸 앞당긴 9×9 준비 창을 검증하기 위한 범위이며 동시 다운로드나 활성 범위가 아니다. 한 시점의 동시 타일 로드는 4개로 제한한다.

플레이어가 타일 경계까지 타일 폭의 25% 안으로 접근하고 이동 방향이 그 경계를 향하면 준비 중심을 한 타일 앞당긴다. 경계를 넘을 때 기존 Slot과 검증된 Manifest를 재사용하고 새 가장자리만 요청한다. 최고 상세 Prefab은 상세 창 전체가 아니라 카메라 절두체·거리·프레임 예산을 통과한 객체만 승격한다.

이 숫자는 공식 엔진의 고정 권장값을 복사한 것이 아니라 스트리밍 원점, Loading Range, HLOD, 비동기 참조 관리와 동시 타일 로드 제한 원칙을 현재 L2 크기에 적용한 초기값이다. 실제 지형 산출물이 연결되면 프레임 시간·메모리·요청 p95·경계 대기·캐시 적중률을 근거로 플랫폼·이동수단별 Recipe를 새 개정으로 분리한다.

## D-126 생존 타로는 안전 거점 전원 합의 뒤 다음 Tick에만 적용한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-096의 타로 카드 조건·대응 경계와 D-097의 상위 규칙 보정선을 1인칭 생존 Session에 적용함

공공데이터는 지형·건물·도로·공개 사업체의 공간 사실을 제공하며 생존 위기, 아이템 재고와 타로 효과를 만들지 않는다. 생존 타로는 `SimulationScenario` 초기 규칙과 같은 Session 원장 안에서만 실행하며 운영 재고나 공공데이터 원본을 변경하지 않는다.

기회는 한 Tick을 하루로 보는 기본 3일 주기와 첫 식량 비축 `2 인일 이하` 위기에서 발생하고, 미해결 기회는 한 건만 유지한다. Scenario가 농장 건물 범위를 지정하면 월드 전체 식량과 별도로 농장 건물 컨테이너 및 농장 안 참여자의 소지 식량을 합산한다. 이 농장 자급분이 기본 `2 인일 이하`면 농장 밖 보급·탐색이 필요하다는 `ExternalExpeditionRequired` 기회를 일반 식량 위기보다 먼저 한 번 생성한다. 농장 밖에 충분한 식량이 있어도 이 판정은 유지하며, 같은 부족을 일반 식량 위기로 중복 생성하지 않는다. 농장 범위를 지정하지 않은 기존 Scenario는 종전 전체 식량 위기 규칙을 유지한다. 같은 seed·Tick·deck 개정과 선택 이력은 같은 3장 제안을 만든다. 응답은 Scenario가 지정한 안전 건물에 참여자 전원이 함께 있을 때만 허용하며, 전원이 동일 제안을 선택해야 확정할 수 있다.

클라이언트는 `CommandId`, `ExpectedRevision`, 기회 고유 식별자, 참여자 고유 식별자와 제안 고유 식별자만 보낸다. 카드, 방향, 수치와 기회·부담 보정선은 서버가 결정한다. 확정한 보정선은 확정 Tick의 재고·이동·생산을 즉시 바꾸지 않고 다음 Tick 한 번만 활성화하며, 재고 생성·용량 초과·접근 정책 우회나 운영 효과를 만들 수 없다. 응답·합의·아이템 획득은 모두 Save/Replay 명령 로그와 해시에 포함한다. Unity는 이 상태를 정보판·효과로 표현할 수 있지만 화면이나 animation으로 합의·생존 결과를 독자적으로 확정하지 않는다.

## D-127 세계 사건은 서버 원장에 먼저 확정하고 Unity는 개정 기반 표현 자료만 읽는다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-126의 생존 타로 기회를 첫 세계 사건 adapter로 일반화함

Simulation Session이 사건 발생 조건, 선택지, 참여자 응답과 확정 결과를 먼저 계산한다. 사건은 안정적 고유 식별자, 사건 개정, 마지막으로 변경된 세계 개정, 발생·노출 Tick, 건물·타일·지역 닻과 규칙 개정을 가진다. 클라이언트는 마지막으로 반영한 세계 개정 이후의 변경만 요청하고 서버가 반환한 다음 개정을 커서로 저장한다.

Unity에는 Prefab·Material·Synty 원본 경로를 내려주지 않고 `PresentationKey`와 사건 의미·상태·선택지·공간 닻을 내려준다. Unity는 다운로드한 결과를 정보판·사운드·파티클·환경 효과로 표현하되, 사건 발생·합의·보정선을 다시 계산하지 않는다. `SimulationOnly=true`, `IsOperationalState=false`, `PresentationOnly=true`의 경계를 어긴 응답은 수신 계약에서 거부한다.

생존 타로는 첫 adapter이며 사건 원장 자체를 독립 화면 상태로 중복 저장하지 않는다. 현재 Save/Replay는 세션 생성과 응답·합의 Command를 다시 실행해 같은 사건 고유 식별자·개정·결과를 재생한다. 다른 사건 원천을 추가할 때도 재생 가능한 규칙 입력과 Command 계보를 먼저 정의한다.

## D-128 농장 생존은 플레이어·NPC 노동과 회복 가능한 위협을 같은 Session 원장에 둔다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-122·D-125의 L2 공간 창, D-126의 생존 타로, D-127의 세계 사건 Projection을 첫 주 농장 생존 흐름으로 결합함

공공데이터의 지형·법정동·건물·사업체는 농장 생존 Scenario의 공간 닻과 배치 제약으로만 사용한다. 실제 사람·사업체·건물을 감염자, 약탈자, 전리품이나 공격 대상으로 해석하지 않는다. 좀비는 환경 압력, 약탈자는 가상 세력이며 모두 `SimulationOnly=true`, `IsOperationalState=false`인 오버레이다.

플레이어 직접 노동과 NPC 위임 노동은 같은 농장 상태를 변경하지만 비용 계약은 분리한다. 플레이어 노동은 개인 체력을, NPC 위임은 Settlement 공동 노동력을 예약하며 완료 뒤 반환한다. 클라이언트는 배우·대상·행동·배치 방식과 예상 개정만 보내고 소요 시간·노동력·체력·재료 비용은 서버가 정한다.

위협 결과는 영구 사망 대신 부상, 보급품 손실, 시설 피해와 수리 필요량으로 남긴다. 서버가 seed와 방어 준비도에 따라 결과를 계산하고 Save/Replay Command 계보와 세계 사건 개정으로 재현한다. Unity는 서버의 위협 종류·상태·개체 수·`PresentationKey`를 `VisualKey`로 해석할 뿐 개체 수나 성공 결과를 독자적으로 계산하지 않는다. 유료 Synty 팩은 원본 경로나 구매 여부를 업무 계약에 넣지 않고 현재 fallback과 선호 SourcePack을 가진 구성 대장에서 마지막에 연결한다.

## D-129 같은 Simulation 팀은 별도 요청 없이 서로 관찰하되 조작 권한을 공유하지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-116의 표현 전용 플레이어 카메라와 D-122·D-125의 L2 타일 창을 협동 Session 관찰에 확장함

같은 Simulation 팀에 가입한 행위자는 팀 정책이 허용한 1인칭 시선 또는 따라보기 카메라를 별도 건별 동의 요청 없이 사용할 수 있다. 팀 가입 시 상호 관찰 가능성을 안내하고 관찰 중에는 대상 화면에 관찰자 표시를 유지한다. 다른 팀 행위자에게는 이 자동 허용을 적용하지 않는다.

관찰 권한은 서버의 팀 원장 또는 그 읽기 Projection이 같은 Session·팀·구성원·정책 개정을 확인한 결과로만 부여한다. Unity가 구성원 목록이나 팀 정책을 제출해 권한을 만들 수 없다. 관찰 결과는 대상 이동·시선·상호작용 Command, 재고, `WorldTick`, Session 개정이나 운영 상태를 변경하지 않으며 `CanControlTarget=false`, `MoveObserverActor=false`, `ChangesWorldState=false`를 유지한다.

관찰은 권한 미리보기와 별도의 짧은 수명 관찰 Session으로 관리한다. 시작 뒤에도 각 공개 위치 상태 사본을 조회할 때 현재 팀 구성원·정책 개정을 다시 검사하며, 팀 이탈·정책 개정·명시적 종료·로컬 위험이 발생하면 관찰 표현을 종료한다. 공개 상태 사본에는 대상의 L2 타일, 타일 내부 평면 오프셋, 표고 참고값, 카메라 높이, 시선 회전과 이동 의도만 포함하고 재고·개인 UI·대화 내용은 포함하지 않는다. 대상에게는 활성 관찰자 수를 표시하되 이 목록으로 조작 권한을 만들지 않는다. 위치 입력은 HTTP 공개 쓰기 API가 아니라 이후 Netcode 또는 전용 위치 수집기가 구현할 내부 저장 경계로 둔다.

Unity는 로컬 플레이어 입력과 관찰 카메라를 분리한다. 관찰 중 로컬 입력 Controller를 일시 정지하고 상대의 공개 가능한 카메라 기준점 또는 따라보기 기준점만 읽는다. 관찰자가 위험해지면 로컬 시점으로 즉시 복귀한다. 먼 팀원을 관찰할 때는 서버가 허용한 대상 타일을 별도 표현 초점으로 사용할 수 있지만 로컬 행위자를 순간이동시키거나 두 지역을 모두 최고 상세로 유지하지 않는다. L2 타일 내부 평면 거리는 World의 500m→Unity 타일 압축률로 변환하고 사람의 카메라 높이는 압축하지 않는다. 실제 다중 사용자 위치 전송, 팀 영속 원장과 Netcode 연결은 별도 어댑터로 검증한다.

## D-130 Simulation 역할은 고정 직업이 아니라 팀 공동 카드와 현재 활동에서 파생한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-126의 사건 해결용 생존 타로와 D-129의 협동 팀 원장을 분리해 역할 전환 능력으로 확장함

생존 타로 카드는 사건 발생·응답·합의 결과를 나타내므로 개인 장착 카드로 전용하지 않는다. 별도의 팀 역할 카드함이 탐험·농사·물류 같은 활동 능력 카드 사본을 보유하며, 구성원은 물리적으로 떨어져 있어도 같은 팀 정책 개정 안에서 카드를 다른 구성원의 장착 칸으로 옮길 수 있다. 역할 카드는 실제 물품 재고가 아니므로 공간 근접이나 아이템 순간이동으로 해석하지 않는다.

캐릭터의 역할은 영구 직업이 아니다. 서버가 `현재 장착 카드 + 활성 활동 배정`을 조합해 현재 역할을 투영하며, 활동이 끝나면 고정 직업으로 남지 않는다. 한 카드 사본은 한 시점에 한 장착 칸에만 존재한다. 누군가 탐험·농사 등 해당 카드를 사용하는 활동을 시작하면 카드와 그 배우의 활동을 잠그고, 종료 전에는 다른 구성원에게 옮기거나 같은 배우가 다른 활동을 동시에 시작할 수 없다.

카드 장착과 활동 시작·종료는 요청 식별자, 카드함 예상 개정과 팀 정책 예상 개정을 서버가 검사한다. Unity는 역할을 독자적으로 계산하거나 카드 사본을 복제하지 않고 서버 상태 사본을 표현한다. 카드 상태는 별도 process-local 카드함이 아니라 Simulation Session aggregate의 선택적 상태이며, 장착·활동 Command와 상태는 Save/Replay 로그·해시에 포함한다.

## D-131 역할 카드 규칙 정의와 현재 장착 상태의 DB 책임을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-130의 팀 공동 역할 카드를 Simulation World 파생 DB와 Session 저장 경계에 배치함

카드 장착·활동 시작·활동 종료의 안정적 규칙 식별자, Engine과 입출력 계약은 `Simulation World 업무 규칙 대장`의 새 불변 개정에 저장할 수 있다. 이 대장은 어떤 Scenario가 어떤 규칙 개정을 사용했는지 재현하는 정적 관계·계보이며 플레이 중 상태의 권위가 아니다.

카드 사본의 현재 장착 대상, 잠금, 활성 활동과 파생 역할은 Simulation Session aggregate가 소유한다. 이 상태와 세 Command는 Session 저장 자료와 재생 해시에 포함하고, 공간 파생 DB에는 복제하지 않는다. 따라서 공간·Synty 파이프라인을 다시 생성해도 팀 장착 상태가 바뀌지 않으며, 카드 규칙 대장을 교체해도 이미 저장된 Session은 생성 요청과 Command 계보로 같은 상태를 재현한다.

## D-132 Simulation Session 저장 자료는 별도 DB에 보존하고 Command 재생으로 복원한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-131의 가변 역할 카드 상태와 기존 `simulation-save.v1` 저장·재현 경계를 물리 저장소에 연결함

공유 공공데이터 DB와 Simulation World 파생 관계 DB에는 Simulation Session 저장 자료를 넣지 않는다. 별도 `Simulation Session DB`가 저장 식별자, Session 식별자, schema, WorldTick, World 개정, Command 수, 재생 SHA-256과 전체 저장 자료 JSON을 보존한다. 운영 업무 DB 상태나 실제 재고로 승격하지 않는다.

현재 활성 aggregate는 계속 프로세스 메모리에 두고 모든 Command마다 DB snapshot으로 덮어쓰지 않는다. 명시적 Save가 불변 저장 자료를 만들고, 프로세스 재시작 뒤 Restore가 그 자료의 Metadata와 JSON을 대조한 다음 기존 Command를 재생해 새 활성 aggregate를 만든다. 같은 저장 식별자와 같은 hash는 멱등 재사용하며 다른 hash는 충돌로 거부한다. JSON·Metadata·재생 hash 또는 Command 결과가 일치하지 않으면 손상 자료로 거부한다.

영속 저장은 `SimulationSessionDatabase:Enabled=true`일 때만 활성화하고 기본 개발·시험 호환을 위해 비활성 상태에서는 기존 메모리 저장소를 사용한다. DB migration은 API host 시작 시 자동 적용하지 않고 별도 명시적 명령으로만 적용한다.

## D-133 농사·영역 발견 보상은 개인 수집 카드 원장으로 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-125의 L2 Runtime, D-128의 농장 노동, D-130·D-131의 팀 역할 카드와 D-132의 Session 저장 경계를 결합함

농사·탐험 보상 카드는 팀 활동 능력을 부여하는 역할 카드나 사건 합의를 보정하는 생존 타로가 아니다. 첫 개정은 수치 효과와 기능 해금이 없는 개인 수집·표현 카드로 유지하고, 획득한 카드 사본은 같은 Simulation 팀 구성원에게 거리와 무관하게 양도할 수 있다. 양도는 소유권만 바꾸며 물리 아이템 이동이나 복제를 만들지 않는다.

농사 보상은 플레이어가 직접 수행한 밭갈기가 `WorldTick`에서 실제 완료된 첫 사건에만 판정한다. NPC 위임, 반복 완료, 방어·의료 작업은 제외한다. 탐험은 서버가 기억한 현재 L2에서 8방향 인접 L2로 이동했음을 확인한 뒤 팀 최초 L2와 그 상위 L1 발견을 각각 한 번 기록한다. 시작 L2와 상위 L1은 이미 밝혀진 것으로 보며 시작 보상을 주지 않는다. 현재 HTTP 기반 첫 개정은 권위 있는 실시간 위치 검증을 대신하지 않고 서버가 기억한 이전 타일·인접성·Fixture 제공 범위만 검사한다.

기본 확률은 직접 농사 완료 20%, 새 L2 15%, 새 L1 40%이며 같은 계열의 활성 역할은 10%p를 더한다. 행위자·농사/탐험 계열별로 5회 연속 실패하면 다음 적격 판정을 보장하고, 성공 시 해당 실패 수를 초기화한다. 판정은 Scenario seed, 보상 규칙 개정, 사건 고유 식별자, 행위자, 유발 종류와 시도 순번을 SHA-256에 넣어 결정하며 클라이언트 요청 식별자·원하는 카드·seed·확률을 입력받지 않는다. 자료집 용량 부족으로 건너뛴 사건은 실패 수에 포함하지 않는다.

보상 성공은 정확한 카드를 즉시 정하지 않고 소유자 전용·무기한 미개봉 기회를 만든다. 별도 뽑기 Command가 팀이 아직 보유하지 않은 같은 계열 정의 중 하나를 결정하며 미개봉 기회도 자료집 용량을 예약한다. 발견 원장, 실패 보정, 판정 기록, 미개봉 기회, 카드 사본과 양도 이력은 Session 상태와 Command 재생 해시에 포함하고 Simulation World 공간 파생 DB에는 복제하지 않는다. Unity에는 `PresentationKey`만 전달할 수 있으며 Prefab 경로나 게임 화면이 보상·소유권을 확정하지 않는다.

## D-134 활동별 기본 시점은 편의 정책이며 사용자의 허용된 수동 전환을 막지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-116의 플레이어 카메라, D-122·D-125의 L2 탐험과 D-128의 농장 생존 작업을 입력 방식에 맞게 결합함

농장 경영은 영역·농지·시설·인부와 여러 작업을 한눈에 비교해야 하므로 RTS형 전술 3인칭을 기본 시점으로 사용한다. 탐험은 직접 이동·근접 상호작용·시야 기반 공간 준비가 중심이므로 1인칭을 기본 시점으로 사용한다. 이는 강제 직업이나 서버 권한이 아니라 Unity의 표현·입력 편의 정책이며, 농장에서도 1인칭으로 걷고 탐험에서도 허용된 전술 3인칭으로 전환할 수 있다.

전술 3인칭의 이점은 단순히 카메라를 멀리 두는 것으로 끝내지 않는다. 농지·시설·인부 다중 선택, 영역 상태 겹쳐보기, 작업 초안과 일괄 계획처럼 넓은 시야에 맞는 도구를 제공한다. 첫 수직 단위는 농지 10개와 수확 마당 선택, 밭갈기·파종·관수·수확 종류 선택과 우클릭 위치 기반 작업 초안까지다. 초안은 아직 서버 `Preview`가 아니며 `RequiresExplicitConfirm=true`, `ChangesWorldState=false`, `PresentationOnly=true`를 유지한다.

1인칭은 같은 플레이어와 Session·재고·역할 카드·농장 상태를 공유하고 카메라와 입력만 바꾼다. 시점 전환, 선택 강조, 작업 초안과 카메라 이동은 `WorldTick`, Session 개정, 생산량, 노동력, 카드 보상이나 업무 완료를 변경하지 않는다. 실제 농장 작업은 기존 Simulation Command의 Preview와 명시적 Confirm을 통과한 뒤 서버 상태 사본을 다시 조회해야 확정된다.

## D-135 1인칭과 3인칭은 전환 전용 카메라로 연속 보간한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 관계: D-134의 활동별 기본 시점과 수동 전환을 시각적으로 연결함

1인칭과 전술 3인칭 사이의 전환은 카메라 활성 상태를 한 프레임에 교체하지 않는다. 현재 활성 카메라의 세계 위치·회전·화각을 전환 전용 카메라가 이어받고 목표 카메라까지 짧은 이차 곡선으로 위치를 이동하며, 회전과 화각은 완만한 ease-in-out으로 보간한다. 캐릭터 Renderer는 카메라가 충분히 멀어지거나 가까워진 중간 지점에서 전환해 머리 내부나 몸체가 갑자기 화면을 가리는 문제를 줄인다.

전환 중에는 1인칭 직접 이동, 전술 선택·이동과 농장 경영 입력을 모두 잠그고 완료 뒤 목표 시점의 입력만 활성화한다. 전환 도중 반대 시점을 다시 선택하면 화면을 처음 위치로 순간이동시키지 않고 현재 전환 카메라 위치를 새 시작점으로 사용한다. 이 과정은 Unity 표현 전용이며 `WorldTick`, Session 개정, 업무 상태, 이동 결과나 작업 확정을 변경하지 않는다.

## D-136 전투는 서버 권위 단일 박자로 판정하고 시점별 이점은 일반 허용 구간에만 둔다

- 상태: 채택
- 날짜: 2026-08-15
- 결정: 농장 직접 전투는 한 번에 하나의 `CombatBeat`만 활성화한다. Unity는 서버가 확정한 공격 유형·충돌 시각·허용 구간을 표시하고 `BeatStableId`, 배우, 방어/카운터 행동과 반응 경과 ms만 제출한다. 판정 등급, 피해, 방어 점수, 경직과 사건 결과는 Session aggregate가 확정한다. 1인칭은 일반 방어·카운터 허용 구간을 넓히고 집중 전조를 제공하며, 전술 3인칭은 표준 구간을 유지하는 대신 위협·동료·시설 상황 인식을 제공한다. 완벽 판정 구간은 양 시점에서 동일하다. 활성 박자 동안 시점 변경과 다른 플레이어 입력은 잠그고, 미반응은 다음 WorldTick에서 결정적으로 만료한다.
- 이유: 1인칭 몰입의 조작 이점을 주면서도 완벽 판정의 경쟁 기준은 동일하게 유지하고, 네트워크 지연에 민감한 피해 결과를 Unity 로컬 시간이나 프레임에 맡기지 않기 위해서다.
- 호환: 기존 `farm-survival.spring-preparation.r1`의 자동 좀비 판정은 유지하고 직접 전투는 `r2`에서만 활성화한다. 전투 Command와 상태 사본은 Save/Replay와 해시에 포함하며 실제 사람·사업체·건물에 전투 의미를 부여하지 않는다.
- 관계: D-127의 서버 권위 세계 사건, D-128의 농장 생존 Session, D-134의 활동별 시점 정책과 D-135의 연속 카메라 전환을 전투 입력까지 확장함

## D-137 1인칭 영웅 성과는 한 명령창 동안만 주변 전선의 전술 기회가 된다

- 상태: 채택
- 날짜: 2026-08-15
- 결정: `farm-survival.spring-preparation.r3`에서 Guard 성공은 `Rally`, Counter 성공은 `Breakthrough` 전술 기회를 만든다. OnTime은 품질 1, Perfect는 품질 2이며 기회를 만든 영웅만 해당 농장 방어 전선의 바로 다음 명령창에서 사용할 수 있다. 전진 공격·대형 사수·전술 후퇴 Confirm은 다음 WorldTick에서 전선 위치, 분대 전투력, 회복 가능한 NPC 부상과 시설·물자 피해로 판정한다. 미확정 명령창은 기회를 만료시키고 보너스 없는 대형 사수를 자동 적용한다.
- 이유: 1인칭 숙련이 3인칭에서 무조건적인 전장 보너스가 아니라 선택 가능한 제한 자원이 되게 하고, 카메라·animation이나 클라이언트 계산이 승패를 확정하지 못하게 하기 위해서다.
- 호환: `r1` 자동 판정과 `r2` 반응 즉시 판정은 유지한다. 영구 NPC 사망, 여러 전선 RTS, 카드 공유와 영구 능력 성장은 첫 개정에 포함하지 않는다. Unity는 전술 시점 전환을 강제하지 않고 제안하며 실제 결과는 서버 상태 사본을 다시 받아 표현한다.
- 관계: D-136의 서버 권위 CombatBeat 결과를 D-134·D-135의 전술 3인칭·연속 카메라 전환과 연결함

## D-138 분대 이동은 서버 판정의 교체 가능한 표현이며 기준점과 대형 슬롯을 분리한다

- 상태: 채택
- 날짜: 2026-08-15
- 결정: Unity는 최신 확정 전술 판정의 명령 종류·전선 위치·분대 인원·전투력을 표현 frame으로 바꾸되 피해·승패·이동 결과를 계산하지 않는다. 한 분대는 경로 탐색을 담당하는 기준점 `NavMeshAgent` 하나와 최대 6개의 결정적 로컬 대형 슬롯으로 구성한다. 전진 공격은 쐐기, 대형 사수는 선형, 전술 후퇴는 종대를 사용하며 분대원은 기준점을 따라 슬롯으로 보간한다. Synty 캐릭터와 동작은 `VisualKey`·wrapper·공용 animation adapter를 거쳐 연결하고 root motion은 사용하지 않는다.
- 이유: 여러 개의 독립 agent가 서로 밀어내며 대형을 깨는 문제를 줄이고, 서버 권위 결과와 경로·대형·캐릭터·동작 자산을 서로 교체 가능하게 유지하기 위해서다.
- 호환: 첫 수직 단위는 아군 6명·적군 6명 표시 예산과 평면 농장 전술 바닥만 지원한다. 서버 `MemberCount`가 작으면 표시 인원도 줄이며 6명을 넘는 인원은 집계 상태로 남긴다. 영구 사망, ragdoll, 여러 전선 동시 경로, 외부 animation pack, 네트워크 위치 동기화는 포함하지 않는다. Unity의 이동·경직·도착은 `WorldTick`, 상태 개정이나 업무 완료의 근거가 아니다.
- 관계: D-137의 서버 전술 판정 사본을 D-118의 Synty 역할 표현, D-134의 전술 3인칭과 `SimulationWorldShell` 최종 Scene에 연결함

## D-139 Simulation·Unity 구조 리팩토링은 외부 계약을 보존한 채 검증 경계부터 진행한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 결정: 기능 확장이 누적된 Simulation·Unity는 짧은 기능 동결 기간에 `검증 라우팅 → API·Application 경계 → Save/Replay → Unity 읽기 런타임 → 문서 상태판` 순으로 정리한다. 기존 API 경로·HTTP 방식·JSON 필드·안정 식별자·저장 해시·DB 구조와 Unity 공개 모델을 바꾸지 않으며, 기존 Facade와 상태 코드는 호환 표면으로 유지한다.
- 이유: 잘못된 solution과 test project를 실행하는 검증 상태에서 거대 Controller·Service·저장 재생 파일을 먼저 분해하면 거짓 성공이나 호환 회귀를 발견하기 어렵기 때문이다.
- 범위: `SimulationWorldShell`, Prefab, Synty 원본, `.meta` GUID와 Game View는 구조 리팩토링에서 변경하지 않는다. 35개 부분 클래스로 구성된 Session Aggregate의 심층 분해는 Save/Replay 결합과 호환 특성 시험을 먼저 정리한 뒤 별도 결정으로 다룬다.
- 관계: D-003·D-004의 서버 권위 경계, D-016의 Unity 읽기 흐름, D-027의 서버 물리 분리와 D-132의 Save/Replay 결정을 구조 안정화 절차에 적용함

## D-140 코드 탐색 특성이 원본이고 생성 코드 지도는 검증되는 파생 자료다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 결정: 운영·Simulation·Unity가 함께 쓰는 코드 탐색 계약은 무의존 `Ssalddel.CodeMetadata`에 둔다. 소스의 `SsalddelCodeMetadataAttribute`와 작업영역 manifest를 원본으로 삼고, Codex와 사람이 읽는 JSON·한국어 트리는 결정적으로 생성한다. 기능 안의 `StepKey`, 단계 의존성, 실행 단계, 읽기·쓰기 데이터 권위와 부수효과를 기록한다.
- 검증: 필수 단계 누락, 중복·순환·역전 흐름, Simulation·Unity의 운영 상태 쓰기, 공유 공공데이터 쓰기, Unity의 표현 외 상태 쓰기와 오래된 생성 파일은 실패시킨다. 전체 공개 타입 미표기는 기능 확장을 막지 않고 프로젝트별 경고로 남긴다.
- 호환: 기존 namespace·특성 생성자·기능 키·descriptor 순서를 유지하고 `Ssalddel.Contracts`에는 타입 전달을 둔다. 메타데이터는 권한·트랜잭션·업무 규칙·DB 원장 또는 Unity 표현의 실행 권위가 아니다.
- 관계: D-003·D-004의 권위 분리, D-016의 Unity 데이터 흐름과 D-139의 구조 리팩토링 검증 경계를 코드 탐색에도 적용함

`Ssalddel.CodeMetadata`는 .NET 프로젝트 참조뿐 아니라 엔진 비의존 로컬 UPM 패키지로도 제공한다. Unity 패키지의 `Ssalddel.Unity.Data`는 이 어셈블리를 참조하고, .NET build 산출물은 패키지 바깥 `artifacts/local/dotnet/Ssalddel.CodeMetadata`에 둬 같은 이름의 사전 컴파일 DLL이 중복 수입되지 않게 한다.

## D-141 1인칭 전투 마우스 입력은 전투 진입과 서버 판정 반응으로 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 결정: 활성 전투 박자가 없고 서버 상태에 `AwaitingCombat` 교전이 있을 때 좌클릭은 피해를 만들지 않고 전투 진입을 요청한다. 기존 곡선 카메라 전환이 끝난 뒤 시점 확정과 박자 시작 Command를 보낸다. 활성 박자에서는 좌클릭을 `Counter`, 우클릭을 `Guard`로 해석하며 UI 위 입력은 소비하고 전투 Command로 만들지 않는다.
- 권위: Unity는 배우·교전·박자·행동과 관측 경과 시간만 보낸다. 등급·피해·방어 점수·경직·전술 기회는 기존 Simulation Session 규칙이 확정하며 응답 상태 사본을 다시 받아 표현한다. 일시 오류는 같은 Command ID로 한 번만 재시도하고 개정 충돌에서는 기대 개정을 클라이언트가 바꿔 재전송하지 않는다.
- 표현: 전투 진입과 활성 박자 동안 플레이어 이동을 잠그고 의미 기반 Input System `Attack`·`Defend`를 사용한다. Synty animation과 HUD는 서버 결과의 표현이며 업무나 전투 결과의 권위를 갖지 않는다.
- 관계: D-135의 연속 카메라 전환, D-136의 단일 박자 서버 판정과 D-137의 영웅 전술 기회를 실제 Unity 입력·HTTP 경계에 연결함

## D-142 기본 생존 장은 경관 산책 중심이며 직접 전투는 계절 방어의 선택 경로다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 결정: 새 농장 생존 Session의 기본 규칙은 `farm-survival.scenic-season.r1`이다. 1~23일은 공공데이터 기반 경관 산책·지역 발견·선택 농사를 유지하고, 24일 예고와 25~26일 준비 뒤 27일에 자동 방어 또는 직접 전투를 선택한다. 미응답은 28일 자동 방어로 확정하며 약탈자 사건은 기본 장에서 생성하지 않는다.
- 전투 경계: 자동 방어는 기존 시설 준비도만 사용한다. 직접 전투를 선택한 경우에만 `AwaitingCombat`과 1인칭 방어·카운터 입력을 열며, 성공은 방어를 보완하지만 참가 자체가 필수 성공 조건은 아니다. 결과는 영구 사망이나 농장 소멸 없이 회복 가능한 시설 피해·물자 손실·부상으로 제한한다.
- 호환: 기존 `farm-survival.spring-preparation.r1~r3`의 일정·판정·Save/Replay 해시는 명시적 규칙으로 보존한다. Unity는 새 규칙의 예고·선택 단계에서 적 개체와 전투 HUD를 노출하지 않고 `AwaitingCombat` 뒤에만 전투 표현을 연다.
- 관계: D-126~D-138의 생존·전투 기능을 삭제하지 않고 D-116의 경관 탐색과 D-122~D-125의 L2 스트리밍을 기본 플레이 순환으로 승격함

## D-143 지역 공공데이터는 원본을 보존하고 LOD별 대표 요약과 상세 조회로 나눈다

- 상태: `Accepted`
- 결정일: 2026-08-15
- 결정: 공유 공공데이터와 공간 파생 node는 전량 보존하고 Unity에는 `region-presentation-summary.v1`이 만든 L0 8개·L1 32개·L2 120개 표현 슬롯만 전달한다. 기본 슬롯은 분포 대표 60%, 지역 특색 25%, 게임 맥락 15%이며 한 분류의 최대 점유율은 40%다. 표현하지 않은 원본은 삭제하지 않고 분류별 `화면생략대표원본수`로 보존한다.
- 공개 경계: 타일·지역 요약에는 실제 상호명을 넣지 않는다. 검증된 건물–공개 사업장 관계가 있고 사용자가 가까이에서 명시적으로 상호작용할 때만 별도 공개 상세 조회가 상호명·분류·출처·기준일을 제공하며 대표자·연락처·사업자등록번호는 제공하지 않는다.
- 권위와 호환: 지역 요약은 Simulation World 파생 DB의 표현 입력이며 운영 사실이나 Session 상태를 확정하지 않는다. 기본 요약 hash와 현재 게임 상황 오버레이 개정을 분리하고 Synty·URP는 의미 기반 `VisualKey`만 해석한다. 평창은 일반 Engine에 넘기는 첫 Recipe이며 일반 Selector와 요약 Engine에 지역 코드를 고정하지 않는다.
- 관계: D-016의 서버 상태 투영, D-032의 파생 DB, D-122~D-125의 L2 스트리밍과 D-140의 코드 탐색·권위 경계를 지역 정보 축약에 적용함

## D-144 Synty 팩은 기술 대장·팩별 기준·의미 구성·검토 계획을 거쳐 Scene에 적용한다

- 상태: `Accepted`
- 결정일: 2026-08-16
- 결정: PolygonNature·Farm·Town·City 원본을 Scene에 바로 배치하지 않는다. 보유 Prefab은 먼저 표현 비용과 분류를 담은 기술 대장으로 전수 등록하고, 팩별 Markdown 기준과 사람이 검토한 의미 구성 대장이 `VisualKey`·`CompositionKey`로 승격한다. AreaSet 계획은 구획별 로컬 저작 경계와 세계 Anchor 변환, 토지피복·역할·경사·수계 조건, 기획서와 네 기준 문서의 SHA-256을 함께 고정한다.
- 적용 관문: 기본 JSON과 사람 보정 JSON을 병합해 검증한 뒤 구획별 Staging Prefab만 생성한다. 저장 Scene 적용은 별도 승인 기록이 기획서 bundle·기본·보정·병합 hash와 모두 일치할 때만 허용한다. 기술 대장 등록, Staging 생성, Scene 적용과 Game View 검증은 서로 다른 증거 단계다.
- 권위와 호환: Synty 원본 Prefab과 Material, `.meta` GUID는 수정하지 않는다. 유료 원본 파일명·경로·GUID는 공개 문서나 업무·공간 계약에 저장하지 않고, 팩 교체가 공공 공간자료·Simulation 상태·업무 완료를 변경하지 않게 한다.
- 관계: D-116의 공공데이터 경관, D-118의 Synty 역할 표현, D-122~D-125의 타일 스트리밍, D-143의 지역 요약을 문서 우선 배치 검토 절차로 연결함

## D-145 미완료 작업은 증거 단계 원장으로 관리하고 중앙 L2 실자료부터 종단 완결한다

- 상태: `Accepted`
- 결정일: 2026-08-16
- 결정: 계획, 코드, 자동 시험, 로컬 DB·자료 적용, Runtime 확인과 원자료부터 Unity까지의 종단 확인을 E0~E6으로 분리한다. `eng/execution-ledgers/simulation-unity.json`을 원본으로 삼고 한국어 실행 트리는 결정적으로 생성한다. 첫 종단 완결 대상은 대관령 중앙 L2 `kr5186:l2:700:1145`이며 전국 확장은 이 타일의 원자료·파생 DB·HTTP·Unity 검증 뒤 진행한다.
- 공간 산출물: Copernicus DEM과 ESA WorldCover 원본은 private storage에 두고 Git에는 원본 hash·CRS·해상도·NoData·수직 기준·산출물 hash·형식·표본 크기를 가진 manifest만 둔다. `PhysicalElevation`은 배치 판정 근거이고 Unity 높이 과장은 표현 전용이다. 확인되지 않은 수직 기준이나 세분류 위치는 꾸며내지 않는다.
- 전달 경계: 파생 DB는 산출물 계보와 객체 키를 저장하고 서버는 루트 경로 이탈, 바이트 길이와 SHA-256을 검증해 본문을 제공한다. Unity는 다시 hash를 확인하고 상세 범위에서만 Mesh를 만든다. 이 표현 Mesh는 Collider나 Simulation 권위를 갖지 않는다.
- 완료 판정: 코드나 Fixture 시험만으로 E6 또는 완료로 올리지 않는다. 실제 저장 Scene의 서버 연결·Play Mode·Game View, mask 기반 Synty 배치와 경계 연속성이 남아 있는 동안 관련 항목은 진행 중으로 유지한다.
- 관계: D-122~D-125의 L2 스트리밍, D-139의 구조 검증, D-140의 생성 코드 지도와 D-143~D-144의 지역 요약·Synty 적용 관문을 실자료 실행 순서로 묶음

## D-146 AreaSet은 문서 중심 상위 컨테이너이고 LandscapeGraph는 독립 조립·스트리밍 단위다

- 상태: `Accepted`
- 결정일: 2026-08-16
- 결정: `AreaSet`은 Area·ScenarioRoute·여러 `LandscapeGraph`와 Graph 관계를 묶는 지역 세계 정의서다. `LandscapeGraph`는 하나 이상의 Area·Tile을 참조하는 독립 공간 조립·검증·부분 재생성·스트리밍 단위이고, `Tile`은 공간 Layer 산출물과 캐시 단위다. Area와 Graph를 1:1로 고정하지 않는다.
- 문서 권위: 사람이 작성한 Markdown은 의미·근거·미해결을 설명하고 JSON만 실행 권위를 가진다. compiler는 Markdown 참조와 JSON 참조가 정확히 같은지 검증하고 두 SHA-256을 분리한다. DB 실행 상태 문서는 결정적으로 생성하며 직접 수정하지 않는다.
- 연결 경계: Graph 내부 Node는 다른 Graph의 Node를 직접 참조하지 않는다. Graph 간 연결은 AreaSet의 `GraphRelation`과 양쪽 `ExternalConnectorStub` 쌍으로만 검증한다. 연결 불일치는 꾸며내지 않고 `GraphConnectorUnresolved`로 기록한다.
- Runtime 경계: 서버의 Graph 빌드 상태와 Unity의 플레이어별 `Prepared / Active / Cached` 상태를 분리한다. 기존 Tile API는 한 Recipe 개정 동안 Graph 결과를 Tile별로 투영하는 호환 조회이며 새 공간 권위를 갖지 않는다. 업무·공간 계약에는 Synty Prefab 경로와 `.meta` GUID를 넣지 않는다.
- 관계: D-116의 공공데이터 경관, D-122~D-125의 Tile 스트리밍, D-144의 문서 우선 Synty 적용과 D-145의 증거 단계 원장을 AreaSet 상위 구조로 연결함

## D-147 공간과 Simulation은 세계 상호작용 단위로 종단 연결한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: 공간 기반 시설과 Simulation 기반 시설을 각각 먼저 일반화하지 않고, 실제 세계 행위 하나를 결정–작업–효과와 공간 요구–능력–예약–상태 변화까지 종단 완결하는 `WI` 작업 단위로 구현한다. `WI`는 새 Domain 엔티티나 공개 계약이 아니라 설계·실행·검증 원장의 상위 식별자다. 첫 단위는 진부 Hub 입고 검수와 창고 적재다.
- 책임 경계: Simulation ActionCode가 필요한 공간 능력·용량·기간을 서버에서 정하고, 공간 정의는 가능한 활동과 기본 용량을 제공한다. 가변 점유·예약은 별도 공간 Simulation이나 개정을 만들지 않고 Session 상태와 기존 `WorldRevision`에 포함한다. 클라이언트는 선택적 선호 공간 식별자만 제출할 수 있고 Unity·Synty 표현은 업무 완료 권위를 갖지 않는다.
- 근거 관문: 실제 진부 Hub 경관 노드가 준비되기 전에는 `Scenario` 공간 근거로 규칙을 검증하되 공공데이터 근거처럼 표현하지 않는다. 이후 승인된 공간 능력 연결을 통해 같은 WI를 경관 그래프 노드로 승격하며 Simulation 규칙과 기존 API를 교체하지 않는다.
- 호환: 공간이 없는 기존 Session과 `simulation-save.v1` 재생 해시를 보존한다. 공간 상태·예약·취소 계보가 필요한 Session은 `simulation-save.v2`를 사용한다. 취소는 작업 계보에 연결된 예약·배정·임시 상태만 되돌린다.
- 관계: D-127의 서버 권위 세계 사건, D-132의 저장·재생, D-139의 호환 리팩토링, D-145의 증거 단계와 D-146의 AreaSet·경관 그래프 책임을 실제 업무 상호작용으로 연결함

## D-148 세계 상호작용 단위의 기본 구현 완료는 E3이고 실세계 승격은 별도로 관리한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: 세계 상호작용 단위는 행위 계약·서버 코드·자동 시험·시험 HTTP 호스트·저장 재생을 포함한 `E3`를 기본 구현 완료로 본다. 실제 공간 DB·경관 그래프·저장 Scene·실행 중 서버·Unity Play Mode·Game View는 구현 완료와 분리한 통합 승격 `E4~E6`으로 관리한다. Unity가 없는 E3 완료를 미완료로 낮추지 않고, 반대로 E3를 실제 공간·화면 종단 증거로 과장하지 않는다.
- 실행 원장: `eng/execution-ledgers/world-interactions.json`을 세계 행위 대장의 기계 원본으로 사용하고 한국어 대장을 결정적으로 생성한다. 항목은 사람이 확정하는 명령, 부모 작업 안의 자동 전이, 여러 행위가 공유하는 판정 정책을 구분한다. 공간 능력·예약 같은 공통 계약은 실제 두 개 이상의 세계 행위에서 반복된 뒤에만 추출한다.
- 첫 인과선: 결정적 300kg 감자 수확에서 집하·포장·상차·Farm→Hub 이동·하차·인수·입고 검수·창고 적재까지를 첫 E3 공급선으로 삼는다. 운송 공간은 상차 지점·운송 회랑·하차 지점을 역할별로 분리하며, 출발 시 상차 예약을 반환하고 도착 시 하차 예약을 반환한다. 이 첫 인과선의 공간 근거는 `Scenario`이므로 공공데이터나 실제 경관 그래프 근거로 표현하지 않는다.
- 호환: 클라이언트는 역할별 선호 공간 고유 식별자만 보낼 수 있고 필요한 능력·용량·기간은 서버 규칙이 정한다. 기존 단일 공간 작업, 공개 API 경로·JSON 이름·상태 코드·저장 자료를 유지하며 역할별 공간 정보가 없는 기존 요청의 재생 해시를 바꾸지 않는다.
- 관계: D-132의 저장·재생, D-139의 호환 리팩토링, D-145의 증거 단계와 D-147의 세계 상호작용 구현 단위를 기본 개발 완료선과 실세계 승격선으로 분리함

## D-149 WI E3 승격은 핵심 인과선·공통 규칙·문서·전체 회귀 순으로 나눈다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: 37개 WI를 한 번에 동일 깊이로 검증하지 않는다. `P0`는 농장 생산→Hub→마트→주문·수령·소비 인과선, `P1`은 NPC 배정·취소·시설 수리·지역 발견·역할·활동·턴 마감, `P2`는 원장·문서 일치, `P3`는 전체 회귀·빌드로 구분한다.
- 완료 판정: 상위 우선순위의 집중 시험이 통과하기 전에 하위 단계의 문서 정리나 전체 회귀를 먼저 실행하지 않는다. E3는 자동 시험 증거를 요구하지만 E4~E6 실제 공간·Unity 증거를 포함하지 않는다.
- 관계: D-145의 증거 단계, D-147의 WI 구현 단위와 D-148의 E3 기본 완료선을 지속 가능한 작업 순서로 구체화함

## D-150 E4 승격은 WI 공간 폐루프와 Graph 계보를 기준으로 개별 판정한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: 경관 Graph 전체가 완성되지 않아도 WI가 사용하는 시작 공간, 능력, 용량, 선행·후속 Edge 또는 외부 연결점과 결과 인계가 모두 닫히고 사용 경로가 미해결 구간을 통과하지 않으면 해당 WI만 E4 승격 후보가 된다. 이 `WI 공간 폐루프`는 새 증거 단계나 Domain 엔티티가 아니라 승격 검사 규칙이다.
- 의미와 근거: 공간 역할은 장소가 무엇인지, 공간 능력은 그 장소에서 무엇이 가능한지를 나타낸다. `1 slot`은 물리 면적이 아니라 해당 WI를 동시에 한 건 수행할 수 있는 Simulation 작업 용량이며 `Scenario` 또는 `ReviewedDesign` 근거를 실제 면적·공공데이터 근거처럼 표현하지 않는다.
- 공급자 경계: 경관 Graph 공간을 요청한 WI는 Graph 개정·해시·Node·연결이 없거나 달라지면 차단하고 Scenario 공간으로 자동 대체하지 않는다. 동일 ActionCode·Preview·Confirm·Task·Effect를 유지하고 공간 정의 해결 결과의 계보만 Graph 근거로 승격한다.
- 완료 판정: 계획·대장·자동 검증은 `E4 준비 완료`, Graph·파생 DB 적용은 `E4 진행 중`이며 저장 Scene에 Graph 식별자·개정·해시를 반영해야 공식 E4다. Play Mode·Game View는 E5 이후다.
- 관계: D-145의 증거 단계, D-146의 Graph 부분 조립, D-147의 공간–Simulation 접점과 D-148의 구현·통합 증거 분리를 개별 WI 승격 규칙으로 구체화함

## D-151 E4~E7은 장소·경관·공공데이터·실제 플레이로 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: E3 구현 완료 뒤의 통합 승격을 `E4 실제 의미 공간 귀속 → E5 이동 가능한 경관 조직 → E6 WI 필수 공공데이터 경관 연결 → E7 실제 플레이 폐루프`로 분리한다. 공통 단계 정의는 `eng/execution-ledgers/evidence-stages.json`을 단일 기준으로 사용하고 Simulation–Unity 실행 원장과 37개 WI 원장은 모두 E7을 통합 목표로 삼는다.
- E4·E5: E4는 WI가 실제 `LandscapeGraph` Node의 역할·능력·용량과 필수 공간 전이에 연결된 상태다. E5는 도로·교차로·Gate가 만든 Block에 공간 역할과 `LandscapePattern`을 배정하고 WI 공간 사이의 결정적 이동 경로를 닫은 상태다. 저장 Scene이나 사람 조작 여부만으로 E4·E5를 판정하지 않는다.
- E6: 선정 WI의 공간 판정에 필요한 최소 공공데이터만 E5 경관에 연결한다. 출처·기준일·좌표계·원본과 파생 hash·근거 수준·한계를 보존하며, 관계없는 자료의 부재는 승격을 막지 않는다. `Scenario`, `ReviewedDesign`, 통계 배분과 관측 위치를 서로 바꾸어 표현하지 않는다.
- E7: 사람이 실제 Simulation 서버와 저장된 `SimulationWorldShell`에서 이동·선택·Preview·Confirm·Tick·재조회를 수행하고 Game View와 Console로 확인해야 한다. 자동 WI는 부모 명령 뒤의 자동 전이를 관찰한다. Unity는 입력과 표현만 담당하며 서버가 성공·효과·개정을 확정한다. 패키징 빌드·배포·운영 공개는 E7의 기본 완료 조건이 아니다.
- 이관: E0~E3 증거는 유지한다. 이전 정의의 E4·E5 값은 새 의미로 자동 승계하지 않고 최대 E3으로 보수적으로 재분류한 뒤 새 관문 증거로 다시 승격한다. D-145의 E0~E6 종단선, D-148의 E4~E6 통합선과 D-150의 저장 Scene 중심 E4 완료 판정은 이 결정으로 구체화·대체한다.

## D-152 E4는 WI 공간 모판이고 E5는 실제 지역 경관 조립이다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: `E4`는 하나 이상의 E3 WI를 공간 역할·능력·업무 용량·내부 관계·외부 연결구와 결합한 위치 독립적이고 재사용 가능한 `WI 공간 모판` 완료선이다. E4는 E3를 대체하지 않고 포함하며, 모판 정의로 만든 `Scenario` 공간에서 동일한 Preview·Confirm·Task·Tick·Effect·Save/Replay를 다시 통과해야 한다.
- E4 경계: E4 실행 권위는 버전 관리 JSON이고 사람이 읽는 Markdown은 의미·근거·한계를 설명한다. 모판은 허용된 경관 문법 `compositionKey` 후보와 회전·크기 제약을 가질 수 있지만 `AreaSet`, `LandscapeGraph`, Tile, 절대좌표, 실제 도로, Prefab·GUID·Material·Scene 경로를 가질 수 없다. `1 slot`은 물리 면적이 아니라 해당 작업을 동시에 한 건 예약할 수 있다는 Simulation 업무 용량이다.
- E5 경계: `E5`는 실제 AreaSet·LandscapeGraph에서 도로 Network와 Junction이 만든 Block에 승인된 E4 모판 인스턴스를 배치하고, 추상 연결구를 실제 Node·Edge·GraphRelation에 결속하여 이동 가능한 지역 경관을 닫는 단계다. 기존 대관령 Farm의 공간 능력 연결과 Graph 폐루프 판정은 폐기하지 않고 E5 배치 후보 증거로 이관한다. 실제 Graph 모드에서 미해결 공간을 Scenario로 자동 대체하지 않는다.
- 계약과 책임: WI 공간 모판은 새 운영 Domain 엔티티나 공개 API·저장 원장이 아니다. 기존 Unity 배치 객체 모판과 위치 독립성·결정적 해시·검토 승인 원칙은 공유하지만, WI 공간 모판 계약과 Unity 표현 모판 계약은 서로 분리한다. 새로운 `LandscapePattern` 계약을 만들지 않고 기존 156개 기준 경관 문법을 허용 후보로 재사용한다.
- 후속 단계: E6는 E5 경관에 필요한 공공데이터 원본·파생·출처·해시 계보를 연결하고, E7은 실제 서버와 저장된 `SimulationWorldShell`에서 플레이어가 이동·선택·Preview·Confirm·Tick·재조회를 수행해 서버 상태와 Game View를 함께 확인한다.
- 호환과 이관: 37개 WI의 E3 구현 증거, ActionCode, API, 상태 코드, Save/Replay 형식과 기존 Fixture 해시는 유지한다. D-151의 E6·E7 정의는 유지하고 E4·E5 정의만 이 결정으로 대체한다. D-150의 Graph 공간 폐루프는 E4 완료 조건이 아니라 E5 후보 검사로 재분류한다.

## D-153 E 증거 단계와 H 공간 포함 계층을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: `E0~E7`은 구현·통합 증거의 깊이만 나타내고, 공간 구조의 포함 깊이는 `H1 WI 공간 모판 → H2 LandscapeBlock → H3 LandscapeGraph → H4 AreaSet`으로 별도 분류한다. 기존에 검토한 `S4~S7` 표기는 같은 숫자의 E 단계와 의미가 충돌하므로 사용하지 않는다.
- 증거와 계층의 관계: E4는 H1 모판 안에서 E3 WI를 다시 실행한 증거다. E5는 승인된 H1 인스턴스를 실제 H2에 배치하고 H2→H3→H4의 도로·연결 지점·GraphRelation·이동 경로를 닫은 증거다. E6는 E5 결과의 공공데이터 계보를, E7은 실제 서버와 저장 Scene에서의 플레이어 폐루프를 검증한다.
- 상태 경계: H 코드는 리소스 종류와 포함 관계를 나타낼 뿐 완료·승격 상태가 아니다. 현재 H3 Graph 다섯 개와 H4 AreaSet 하나가 정의되어 있어도 실제 H2 Block이 없으므로 E5가 아니다. H2는 실제 도로와 경계 근거가 준비될 때까지 정의 수 0인 예약 단계로 유지한다.
- 제외 축: 기준 경관 문법 156개는 H1의 허용 후보와 H2·H3 조립에서 사용하는 공간 문법 어휘이며 H 계층이 아니다. Tile L0~L2는 원자료 처리 해상도, Area는 의미 범위, 경관 완결 영역은 사람 검토 범위, ScenarioRoute는 이동 의미 참조이므로 H 계층에 넣지 않는다.
- 호환: H 분류는 별도 기계 대장에서 리소스 종류와 기준 정의를 대조하여 계산한다. 기존 WI 공간 모판·LandscapeGraph·AreaSet 실행 JSON, 정의 SHA-256, 공개 HTTP 계약, 파생 DB schema와 저장 상태에는 H 필드를 중복 저장하지 않는다.

## D-154 모판을 H1~H4 상향 조립 공간 구성 재고로 확장한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: `모판`은 H1만의 기술 자원명이 아니라 `H1 작업공간 모판 → H2 블록 모판 → H3 경관 모판 → H4 지역 모판`으로 상향 조립되는 공간 구성 자원 계열이다. 기술 계약과 stable ID에서는 기존 `WiSpatialSeedbed`, `LandscapeBlock`, `LandscapeGraph`, `AreaSet` 명칭을 유지한다.
- 재고 경계: 위치 독립적인 후보·승인 참조인 `설계 재고`, 버전 관리되는 실제 H 자원인 `정의 재고`, 특정 부모 공간에 배정·배치된 `배치 재고`를 분리한다. 현재 확인할 수 없는 실제 배치 수량을 꾸며내지 않으며 H3·H4 정의가 있어도 실제 H2와 이동 폐루프가 없으면 E5가 아니다.
- 상향 조립: 상위 재고는 하위 재고의 정확한 revision과 결정적 hash를 참조하고 순환 포함을 금지한다. 후보가 정의 권위를 얻으려면 현실 공간 근거와 사람 검토가 필요하며 Synty 자산이나 Unity Scene은 자동 승격 권위를 갖지 않는다.
- 축 분리: H는 공간 자원 종류, `CandidateForReview / ApprovedReference / DefinedPartialAssemblyReference`는 설계 상태, `Unallocated / Allocated / Placed`는 배치 상태, E0~E7은 구현·통합 증거 깊이다. 어느 상태도 H 숫자나 E 단계에서 자동 유도하지 않는다.
- 호환과 제외: 기존 schema version, 공개 계약, 저장 상태와 stable ID는 변경하지 않고 H 코드는 공통 공간 구성 재고 대장에서만 계산한다. Unity 배치 객체 모판과 규칙 실험 모판은 각각 표현·시험 adapter로 유지하며 H 공간 자원으로 자동 편입하지 않는다.
- 관계: D-152의 H1 실행 모판 경계와 D-153의 E/H 축 분리를 유지하면서, D-153에서 H1에 한정했던 사람 중심 `모판` 용어를 H1~H4 전체 공간 자원 계열과 재고 책임으로 확장함

## D-155 Synty 상향식 공간 재고는 공식 H 계층과 분리해 축적한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: 보유한 Nature·Farm·Town·City 팩을 AreaSet 적용 전에도 연구할 수 있도록 `H1 공간 설계 카드 → H2 블록 조립법 → H3 경관 청사진`을 별도 상향식 설계 지식으로 축적한다. 초기 `catalog.v1`과 항목별 `catalog.v2`는 호환 입력으로 보존하며 현재 v3 수량과 문법 유도 관계는 D-156에서 확장한다.
- 승인 경계: `IdeaInventory → ExploratoryInventory → CandidateForReview → ApprovedReference`를 설계 지식 상태로 사용한다. WI가 아직 없으면 예상 게임 행위를 명시할 수 있지만 존재하지 않는 WI 식별자를 만들지 않는다. 기존 `ApprovedForSimulation` H1 다섯 개만 공식 H1 정의 수에 포함하며 사람 승인이나 실제 도로·경계·지형 근거 없이 후보를 공식 H1·H2·H3 또는 E4·E5 증거로 자동 승격하지 않는다.
- 표현 경계: 상향식 재고는 기존 156개 기준 경관 문법의 A/B/C 후보를 참조한다. Synty Prefab·GUID·Material·Scene 경로는 Unity 표현 대장에서만 연결하며 공간 StableId, Simulation 상태 또는 운영 권위를 만들지 않는다.
- 결정성: 기계 원본과 사람이 읽는 Markdown의 지시문 일치, 파일별 SHA-256, 팩 Prefab 수량, E3 WI 존재, 승인 H1 참조, 경관 문법 키, H1→H2→H3 참조와 금지 필드를 검증한다. 조회 도구는 WI·예상 행위·공간 능력·팩·위상으로 조합 후보와 공백을 제안하지만 승인·배치·AreaSet·경관 그래프 권위를 갖지 않는다.
- 관계: D-144의 Synty 표현 후단, D-147의 WI 공간 상호작용, D-152의 H1~H4 포함 계층을 보존하면서 AreaSet 하향식 기획과 별개로 표현 탐색형 설계 지식을 미리 축적함

## D-156 기준 경관 문법은 검토된 조립법으로 H1~H4 설계 후보를 유도한다

- 상태: `Accepted`
- 결정일: 2026-08-17
- 결정: 기준 경관 문법 52개 의미군·A/B/C 156개 변형을 H 계층에 직접 편입하지 않고 검토된 조립법의 입력으로 사용한다. Nature·Farm·Town·City 32개 의미군은 팩 단독 표현 H1 32장, Network 12개는 H2 블록 골격, Transition 8개는 H3 연결·전환 입력으로 사용하며 여러 H3와 세계 주제로 위치 독립 H4 지역 청사진 후보 5개를 유도한다.
- 재고와 호환: `catalog.v3`는 기존 행동 공간 H1 36개와 팩 표현 H1 32개를 `InteractionSpace / PackExpression`으로 분리하고 기존 v2 StableId·H2 18개·H3 10개 참조를 유지한다. 공통 재고는 H1 68/5, H2 19/0, H3 10/5, H4 5/1의 설계/정의 수를 별도로 보고한다.
- 결정성: `grammar-derivation-recipes.v1.json`에 명시된 조립법만 실행하고 문법·하위 지식·조립법 hash로 입력 지문을 만든다. 52개 의미군과 156개 변형의 계보, A/B/C 완전성, Markdown·JSON·파일 hash와 반복 생성 무변경을 자동 검증한다.
- 권위 경계: 팩 표현 H1에서 WI·능력·업무 용량을 자동 추론하지 않는다. H4 청사진은 실제 AreaSet이 아니며 실제 지역 코드·좌표·DataRequirement·LandscapeGraph StableId를 갖지 않는다. 사람의 세계 의도와 현실 근거 없이 후보를 공식 H 정의나 E 증거로 자동 승격하거나 Scenario로 대체하지 않는다.
- 관계: D-153의 문법/H 계층 분리, D-154의 H1~H4 상향 조립, D-155의 Synty 탐색 재고를 유지하면서 문법에서 H 설계 후보로 이어지는 결정적 계보를 추가함

## D-157 오픈 월드는 고정 좌표 경계가 아니라 H4 의도와 H3·H2 Streaming Coverage로 연다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `SimulationWorldShell`은 계속 하나의 영속 실행 Scene으로 유지하고, 플레이어를 하나의 평창 고정 사각형 안에 가두지 않는다. 이동 가능 범위는 현재 준비된 Tile·H3 Graph·H2 Block의 `Streaming Coverage`에서 파생하며, 카메라와 플레이어는 이 동적 범위를 표현 경계로 사용한다.
- 안전 경계: `OPEN-WORLD-0`에서는 기존 `공간TileStreamingController`의 추적 Tile 범위를 이동·전술 카메라의 동적 Coverage로 사용한다. `공간안전이동Gate`는 목적지가 추적 범위 안이고 지면 충돌 근거가 있을 때만 진입을 허용한다. Streaming이 없거나 초기화되지 않은 실험 Scene은 기존 `플레이어경관Profile` 사각형을 fallback으로 유지한다.
- 상향 조립: H4 AreaSet은 어떤 H3 Graph를 연결해 하나의 지역 경험으로 열지 정한다. 플레이어 접근에 따라 H3는 `Declared → Prepared → Active → Cached/Unloaded`로 전이하고, H3 내부에서는 실제 도로·경계 근거의 H2 Block과 승인된 H1 모판 인스턴스만 상호작용 공간을 제공한다. Tile은 지형·표현 자료의 Streaming 단위이며 H 계층을 대체하지 않는다.
- 권위 경계: 이동·카메라·Graph 가시화는 Unity 표현 상태이며 `WorldTick`, 서버 개정, 업무 완료를 바꾸지 않는다. 새 지역 발견, 여행 시작, 물류 이동 같은 게임 효과는 별도 Preview·Confirm과 서버 Command가 확정한다. 미해결 Graph나 지형은 Scenario 공간으로 자동 대체하지 않는다.
- 완료 판정: 고정 사각형 제거만으로 오픈 월드, H2, E5 또는 Farm→Hub 폐루프를 완료했다고 판정하지 않는다. 실제 완료에는 GraphRelation 양쪽 연결점, 지형 Collider, H2 이동 경로, H1 상호작용 귀속, 저장 Scene Play Mode 이동과 필요한 서버 재조회 증거가 함께 필요하다.
- 관계: D-153의 E/H 축 분리, D-154의 H1~H4 공간 구성 재고, `SimulationWorldShell` 단일 실행 Scene 원칙을 유지하면서 오픈 월드의 런타임 경계를 동적 Streaming Coverage로 구체화함

## D-158 LH 엔진은 L 해상도와 H 의미 권위를 직교시키고 승인 H4 안에서 결정적으로 선행 생성한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `LH 엔진`에서 `L0 8km / L1 2km / L2 500m / L3 125m`는 생성·적재 해상도이고 `H4 AreaSet / H3 LandscapeGraph / H2 LandscapeBlock / H1 작업공간`은 공간 의미·포함·권위 계층이다. 기본 대응은 L0→H4, L1→H3, L2→H2, L3→H1이지만 두 축을 같은 완료 상태로 취급하지 않는다. 플레이어 주변 창은 L3 기준 상세 3×3, 이동 준비 5×5, 선행 자료 9×9이며 셀 경계 25% 전에 이동 방향의 다음 창을 요청한다.
- 생성과 권위: 서버는 `WorldSeed + GeneratorVersion + L3 Cell + 생성 Layer`로 기본 배치와 인접 연결 경계를 결정하고, 고정 농장 Anchor와 절차형 주변 경관 후보를 함께 반환한다. 생성 범위는 승인된 H4 경계 안으로 제한하고 H3·H1 승인 참조를 보존한다. 실제 도로·경계 근거가 없는 H2는 계속 `IdeaInventory` 후보이며 절차 생성 성공으로 H2 정의·E5 증거를 만들지 않는다.
- 실행 준비: 셀은 `Requested → DependenciesReady → GeneratedDataReady → VisualPrepared → PlayerTraversalReady → Active/Cached/Released`로 관리하고 지형 시각, 충돌, 연결, H1 상호작용, NPC 경로, 계절 표현 능력을 분리한다. 최신 요청 epoch만 메인 스레드의 제한된 조립 예산 안에서 적용하며, 셀 Root와 로컬 Synty 대장을 재사용한다. 첫 개정은 Addressables나 제3자 절차 생성 패키지를 필수로 하지 않는다.
- 계절과 저장: 계절은 서버 날짜를 기준으로 봄·여름·가을·겨울 각 28일이며 기본 생성 hash와 계절 표현 hash를 분리한다. Save/Replay는 생성된 전체 월드를 저장하지 않고 `WorldSeed`, `GeneratorVersion`, H4 경계 revision/hash와 발견·상태 변경·배치·제거 Delta만 저장한다. Unity는 공유 계절 Material 변형과 전역 shader 값으로 표현하며 원본 Synty Material과 Simulation 자원 원장을 변경하지 않는다.
- 관계: D-132의 Save/Replay, D-153의 E/H 축 분리, D-157의 Streaming Coverage와 `SimulationWorldShell` 단일 실행 Scene 원칙을 125m 플레이 셀 생성·적재 계약으로 구체화함

## D-159 WI별 E/H 성립 상태는 후보 계보와 실행 증거를 분리해 LH 인계 입력으로 생성한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 37개 WI의 E 단계, 공간 참여 여부, 공식 H1 실행 성립, H1~H4 설계 후보 계보, Graph binding과 LH 인계 가능 상태를 `world-interactions`, 공식 H1 대장, 상향식 설계 재고와 공간 능력 대장에서 결정적으로 합성한다. 이 결과는 새 업무 Domain 엔티티나 운영 상태가 아니라 설계·검증·공간 생성 인계 대장이다.
- 판정: 승인 H1에서 다시 실행된 13개만 `EstablishedH1`, 설계 지식만 연결된 WI는 `CandidateLineage`, 필수 공간인데 H1 설계가 없으면 `MissingRequired`, 공간을 요구하지 않는 명령·정책은 `NotApplicable`로 기록한다. 후보 H2·H3·H4, Graph binding 또는 E5 참조가 존재해도 실제 H2 정의와 이동 폐루프가 없으면 E5나 성립 H 단계를 올리지 않는다.
- 우선순위: P1은 수확→집하→포장→상차, P2는 Hub 입고·검수·보관, P3는 Hub 출고·마트, 나머지는 P4로 관리한다. P1 공간 구성은 절대좌표 없이 승인 H1·능력·업무 용량·내부 관계·외부 연결구와 LH 인계 제약을 선언하고, 실제 도로·Block 경계와 양쪽 Graph 연결이 생길 때까지 H2 후보로 유지한다.
- LH 경계: LH 엔진에는 승인 H1만 상호작용 입력으로 넘기고 설계 후보는 Preview 제안에만 사용할 수 있다. 후보 H2는 플레이어 통과 권위를 주지 않으며 Graph 요청 실패를 Scenario로 대체하지 않는다. Synty 표현 H1과 기준 경관 문법은 배치 후보를 제공하지만 WI·능력·용량을 자동 확정하지 않는다.
- 관계: D-147의 WI 종단 단위, D-152의 E4/H1·E5/H2~H4, D-154의 공간 재고, D-156의 문법 유도 계보와 D-158의 LH 선행 생성을 연결하는 기계 인계 경계를 추가함

## D-160 싱글 플레이 LH 지도 생성은 로컬 엔진을 기본 권위로 하고 서버 연결은 선택 동기화로 둔다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `SimulationWorldShell`의 기본 실행은 서버 접속 없이 Unity의 `로컬공간LHWorldEngine`이 플레이어 L3 위치, 로컬 월드 시드, 생성기 버전과 로컬 달력을 입력으로 주변 지도를 결정적으로 계산한다. 플레이어 주변 상세 3×3·이동 준비 5×5·선행 자료 9×9와 경계 25% 선행 요청은 그대로 유지하며, 상태 화면은 로컬 싱글 플레이 생성 여부를 명시한다.
- 생성 범위: 로컬 엔진은 승인 H4 범위 안에서 H4·H3 승인 참조, H2 `IdeaInventory`, 승인 H1 상호작용 참조를 분리하고, 수확 기준작의 네 고정 거점과 셀마다 3~5개 절차 배치, 동서남북 공유 경계 해시를 계산한다. 같은 시드·생성기 버전·셀은 같은 기본 배치를 만들고 계절은 별도 표현 해시에만 반영한다. H4 밖 셀은 생성하지 않고 미지원 범위로 반환한다.
- 서버 선택 경계: D-158의 LH 계약과 서버 구현은 멀티플레이, 원격 검증, 저장 동기화의 선택 경로로 보존하되 싱글 플레이 시작을 막지 않는다. 이 결정은 D-158의 “서버가 기본 생성 권위” 부분과 “서버 날짜가 기본 계절 기준” 부분을 대체한다. 로컬 Simulation 지도 생성은 주문·계약·입출고·결제 등 실운영 원장의 권위를 얻지 않으며, 실운영 효과는 계속 서버의 권한·개정·Command·Event 경계를 거친다.
- 저장과 호환: 저장 전체 지형 대신 로컬 월드 시드, 생성기 버전, 로컬 날짜, H4 경계 개정·해시와 Delta를 보존한다. 나중에 서버 동기화를 켤 때도 같은 입력 계약과 기본 배치 해시를 비교하고, 생성기 버전 불일치나 충돌을 조용히 덮어쓰지 않는다.
- 관계: D-132의 Save/Replay, D-157의 동적 Streaming Coverage, D-158의 LH 직교 계층·선행 생성 계약을 유지하면서 싱글 플레이 기본 실행 권위를 로컬로 전환함

## D-161 음식·화물 배달은 NPC 경로 이동을 기본 수행 방식으로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 싱글 플레이 음식·화물 배달의 기본 수행자는 확정된 배차 NPC다. 플레이어는 후보 비교·우선순위·Confirm을 담당하고 차량 직접 운전이나 운전자 인계는 첫 범위에 포함하지 않는다. 음식과 화물은 같은 경로·Waypoint·체크포인트 계약을 사용하고 운송 종류와 이동 Profile만 분리한다.
- 진행 권위: NPC Transform 도착이나 animation 종료만으로 운송·전달·입고를 완료하지 않는다. 경로 StableId, Cargo 또는 주문 StableId, NPC, 차량, 순번과 예상 개정이 일치하는 체크포인트만 로컬 Simulation 진행을 만들며 도착 뒤 인수·검수·입고는 별도 업무 상태로 유지한다. 기존 서버 `I물류이동AuthorityClient`와 운영 권위 경계는 보존한다.
- LH 인계: 플레이어 위치가 Streaming의 주 관심점이고 활성 NPC의 다음 경로 셀은 보조 관심점이다. 다음 셀의 충돌·연결·`NpcNavigation` 능력이 준비되지 않으면 NPC는 경계에서 정지하고 준비 뒤 같은 구간에서 재개한다. 메모리나 준비 실패를 순간이동으로 숨기지 않는다.
- 공간 증거: 로컬에서 생성한 배달 경로는 `ScenarioProcedural` 표현 자료다. 실제 도로·경계 원본과 승인 H2, H3 Graph 연결이 없으면 NPC 완주 성공을 H2 정의, E5 경관 폐루프 또는 실운영 배달 증거로 승격하지 않는다.
- 관계: D-086의 도착·인수 분리, D-157의 Streaming Coverage, D-158의 셀 능력 분리와 D-160의 로컬 싱글 플레이 기본 경계를 NPC 물류 이동에 적용함

## D-162 Nature 생활권은 주인공의 상시 체류 세계이고 Farm·Town·City/Hub는 전문 경관 인스턴스다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 플레이어는 Nature 팩을 주축으로 한 생활·탐험 공간에 상시 체류하고, Farm·Town·City/Hub 팩을 주축으로 한 세 전문 경관을 필요에 따라 오가며 각 장소의 사건과 경영 업무를 3인칭 시점에서 관리한다. Nature 생활권은 전문 경관 전환 중에도 사라지지 않으며 한 번에 하나의 전문 경관만 활성화한다. 기본 시점은 탐험·전투·경영 모두 `TacticalThirdPerson`이고 1인칭은 허용되는 표현 선택으로 남긴다.
- 공간 구조: Nature는 `안전 생활핵 → 경계 완충대 → 조우 위험대` 순서의 위험 구역을 가지며, Farm·Town·City/Hub는 각각 Nature 생활권의 승인된 연결구로 진입한다. 이 배치는 H1~H4 후보와 WI를 재사용하는 위치 독립 설계 재고이며 실제 AreaSet·Graph·공공데이터·E5 증거를 자동 생성하지 않는다. 인스턴스 전환은 표현 위치와 활성 경관만 바꾸며 `WorldTick`, 개정 번호와 Simulation 업무 상태를 변경하지 않는다.
- 자산 관문: 실제 적 개체 표현은 `POLYGON Apocalypse` 설치와 검토가 확인된 경우에만 위험대의 Simulation 전용 조우 의도를 표시한다. 해당 팩이 없으면 상태를 `WaitingForApocalypseAssetPack`으로 유지하고 Generic Skeleton·다른 Synty Prefab·절차 Primitive로 자동 대체하지 않는다. 안전 생활핵·경계 완충대, 실운영 상태 또는 지원하지 않는 표현 키에서는 적 개체를 표시하지 않는다.
- 실행 경계: 첫 구현은 기계 경관 대장, 결정적 검증, 엔진 비의존 위험·전환 정책과 Unity 표현 전용 Controller까지만 제공한다. 저장 `SimulationWorldShell` 배선, 실제 몬스터 Prefab, Play Mode와 Game View는 자산 준비와 별도 Scene 적용 검토 뒤 수행한다. Unity Transform·Animator·NavMesh는 사건 완료 권위를 갖지 않는다.
- 관계: D-142의 경관 산책과 선택 전투, D-155~D-156의 Synty 상향식 설계 재고, D-157의 단일 World Streaming, D-160의 로컬 싱글 플레이 기본 권위와 `SimulationWorldShell` 단일 실행 Scene을 플레이어 중심 경관 구조로 구체화함

## D-163 전문 경관의 미해결 사건은 경로별 자연권 위협으로 전파한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: Farm·Town·City/Hub에서 확정된 안전하지 않은 선택과 사건 기한 초과만 `NatureToFarm`, `NatureToTown`, `NatureToCityHub` 경로의 자연권 위협 원인이 된다. 미리보기 차단과 일반 작업 취소는 포함하지 않는다. 해당 경로 남은 심각도 두 배에 전체 남은 심각도의 3분의 1 내림값을 더하고 12로 제한하며, 경로 자체 원인이 남아 있고 압력이 4 이상일 때만 최대 5개의 적 조우를 만든다.
- 인과와 회복: 사건 선택은 후속 WI와 분리하지 않는다. 안전 선택은 Farm 집하·포장, Town 검수·후방 적재·진열, Hub 입고 검수·창고 적재를 요구한다. 원인 WI 완료는 해당 사건을 완전히 해결하고, 자연권 전투 승리는 같은 경로의 가장 오래된 미해결 사건 심각도를 한 단계만 줄인다. 수동 방치 없이 시간이 흐른다는 이유로 자동 회복하지 않으며 기존 계절 농장 위협을 다시 입력하지 않는다.
- 권위와 호환: 선택·심각도·압력·적 수는 Session과 `WorldRevision` 안에서 서버가 확정한다. 클라이언트는 사건 선택 고유 식별자와 예상 개정·명령 고유 식별자만 제출한다. 지역 사건 명령과 자연권 승리 계보는 `simulation-save.v4`에 기록하고 v1~v3 읽기 호환을 유지한다.
- 표현 경계: Apocalypse 자산이 없어도 압력 경고는 유지하지만 적 개체는 만들지 않고 Generic 대체를 금지한다. Unity는 서버 상태를 표현할 뿐 사건 해결·압력 감소·전투 승리를 역추론하지 않는다. 상세 규칙은 [지역 사건과 자연권 위협 연결 규칙](../Architecture/지역사건-자연권위협-규칙.md)을 따른다.
- 관계: D-127의 서버 권위 세계 사건, D-147의 WI 공간 상호작용, D-161의 NPC 물류, D-162의 Nature 상시 생활권과 전문 경관 구조를 실패 결과의 세계 인과로 연결함

## D-164 상향식 공간 재고는 Nature 위협·회복 카드부터 작은 묶음으로 확장한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 상향식 재고 확장은 한 번에 전체 H 계층을 늘리지 않고 `Nature 위협·회복 → Farm 사건 대응 → Town → City/Hub → H2·H3 조립` 순서로 진행한다. 한 검토 묶음은 5~10장으로 제한한다.
- 첫 묶음: 위협 관찰, 사건 흔적 조사, 긴급 후퇴, 정화·복구, 안전 회복 야영지의 H1 탐색 카드 5장을 추가한다. 기존 Nature 기준 경관 문법과 연결하지만 WI·공간 능력·업무 용량·실제 좌표·LandscapeGraph·Unity Scene 권위를 자동 획득하지 않는다.
- 승격 경계: 실제 지역 사건 규칙과 연결할 새 WI가 아직 없으면 `anticipatedGameplayCodes`로 가능성만 기록하고, 사람 검토와 실행 계약 없이 공식 H1이나 E4 이상으로 승격하지 않는다.
- 관계: D-155~D-156의 공간 재고·문법 유도 원칙과 D-162~D-163의 Nature 생활권·위협 인과를 상향식 재고 확장 순서로 구체화함

## D-165 사건 대응 H1은 Nature에서 시작해 Farm과 Town으로 이어지는 H2 우선순위로 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 기존 H2에 아직 포함되지 않은 사건 대응 H1은 `P1 Nature 위협 추적·대피와 복원·안전 회복 → P2 Farm 점검·격리와 손실 회복·복원 인계 → P3 Town 오염 통제와 회수 안내·구호` 순서의 여섯 H2 후보로 조립한다. 우선순위는 플레이어의 상시 체류 공간인 Nature의 위협·회복 폐루프를 먼저 만들고 전문 경관 사건의 결과를 그 폐루프에 인계하는 순서다.
- 조립 기준: H2 후보는 필수 H1 사이의 선행·후속 관계, 공유 위상, 입출력 연결구와 다음 H2로 이어지는 폐루프 출구를 가져야 한다. 같은 필수 H1 조합을 기존 H2가 이미 수용하면 중복 후보를 만들지 않는다. Network 기준 경관 문법은 조립 입력으로 연결하지만 H1의 WI·능력·용량을 자동 추론하지 않는다.
- 승격 경계: 여섯 항목은 위치 독립 설계 재고이며 공식 H2 정의가 아니다. 실제 도로·경계로 결정된 Block 면, 승인 H1 배치, 연결구 폐루프, Graph revision·hash와 공간 근거 검토가 없으면 자동 승격하거나 Scenario로 대체하지 않는다.
- 관계: D-155~D-156의 문법 유도와 H 계층, D-162~D-164의 Nature 중심 세계·사건 인과·재고 확장 순서를 H2 조립 실행 순서로 구체화함

## D-166 Nature·Farm·Town·City/Hub는 각각 독립 AreaSet 후보로 상향 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 네 팩 중심 경관을 하나의 AreaSet에 평면적으로 넣지 않는다. Nature 생활·탐험, Farm 생산·생존, Town 생활·시장, City/Hub 물류를 각각 `H1 → H2 → H3 → H4 AreaSet 후보`로 독립 성장시키고, 상위 구성 대장은 후보 사이의 이동·물류 관계만 관리한다. 팩은 주축 표현 정보이며 AreaSet의 공간 권위가 아니다.
- 우선순위: P1 Nature는 상시 생활 세계이므로 위협 추적·대피 H2와 복원·안전 회복 H2를 자연 생활·위협·회복 H3로 묶고 Nature 생활·탐험 H4 후보에 연결한다. 이후 P2 Farm, P3 Town, P4 City/Hub 순서로 같은 상향 조립을 반복한다.
- 관계 분리: Nature↔Farm·Town·City/Hub는 플레이어 이동 관계다. Farm→City/Hub→Town은 Nature 생활권을 지름길로 쓰지 않는 별도 화물 관계다. 후보 연결은 실제 GraphRelation이 아니며 양쪽 AreaSet의 승인 Connector가 준비되기 전에는 이동 폐루프나 E5 증거로 승격하지 않는다.
- 권위 경계: H4 후보는 실제 AreaSet이 아니다. 실제 지역 세계가 되려면 사람의 세계 의도, 지역 범위, DataRequirement, H3 LandscapeGraph와 GraphRelation 승인이 필요하며 누락 근거를 Scenario로 대체하지 않는다.
- 관계: D-147의 AreaSet-first 공간 권위, D-155~D-156의 H1~H4 상향식 재고, D-162의 Nature 상시 생활권과 전문 경관 구조, D-165의 H2 조립 우선순위를 다중 AreaSet 구성으로 구체화함

## D-167 Farm AreaSet 후보는 생산 흐름과 사건 격리·회복 흐름을 함께 포함한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: P2 Farm은 생산·후처리 경관만 가진 AreaSet 후보로 끝내지 않는다. 노출 점검·사건 격리·기상 보호를 묶은 H2와 손실 회복·자연권 복원 물자 인계를 묶은 H2를 Farm 사건 격리·회복 H3로 조립하고, 고지대 생산 H3·생산 후처리 H3와 함께 Farm 생산·생존 H4 후보의 필수 경관으로 둔다.
- 인계: Farm의 `NatureRestorationOutput`은 Nature 복원·안전 회복 H2의 사건 입력 후보와 의미상 대응한다. 그러나 양쪽 실제 AreaSet의 승인된 외부 연결점과 GraphRelation이 없으면 자동 연결하거나 E5 이동 폐루프로 간주하지 않는다.
- 공간 경계: H2 조립 좌표는 `LocalMeters` 상대 설계값이다. 실제 도로·Block 경계·AreaSet 범위·공공데이터 근거가 없으므로 공식 H2나 실제 Farm AreaSet으로 승격하지 않는다.
- 관계: D-163의 Farm 사건→Nature 위협 인과, D-165의 P2 H2 우선순위, D-166의 팩별 독립 AreaSet 조립 원칙을 Farm 생산·생존 경관으로 구체화함

## D-168 Town AreaSet 후보는 시장 생활 흐름과 오염 통제·주민 구호 흐름을 함께 포함한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: P3 Town은 저층 생활·시장과 반품·회수 경관만 가진 AreaSet 후보로 끝내지 않는다. 재고 오염 점검·격리·정화 폐기 인계를 묶은 H2와 주민 회수 안내·근린 서비스·자연권 구호 물자 인계를 묶은 H2를 생활권 오염 통제·구호 H3로 조립하고, 저층 생활·시장 H3와 반품·회수 순환 H3와 함께 Town 생활·시장 H4 후보의 필수 경관으로 둔다.
- 인계: Town의 `NatureReliefOutput`은 Nature 복원·안전 회복 H2의 사건 입력 후보와 의미상 대응한다. 실제 Town·Nature AreaSet 양쪽 외부 연결점과 GraphRelation이 승인되기 전에는 자동 연결하거나 E5 이동 폐루프로 간주하지 않는다.
- 공간 경계: P3 H2 조립 좌표는 `LocalMeters` 상대 설계값이며 실제 도로·건물·Block 경계와 시장 자료 근거가 아니다. 공식 H2·H3나 실제 Town AreaSet으로 자동 승격하지 않고 Scenario로 대체하지 않는다.
- 관계: D-163의 Town 사건→Nature 위협 인과, D-165의 P3 H2 우선순위, D-166의 팩별 독립 AreaSet 조립 원칙을 Town 생활·시장 경관으로 구체화함

## D-169 H1~H4는 위치 독립 공간 설계 계층이며 공공데이터 결속은 E6에서만 수행한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `H1 작업공간 → H2 블록 → H3 경관 → H4 지역 세계 청사진`은 공간 설계의 조립 깊이만 나타낸다. H 정의에는 실제 지역 좌표, `DataRequirement`, 공공데이터 목적·출처·좌표계·원본·파생 해시나 근거 계보를 넣지 않는다.
- 설계 승격: H1은 역할·능력·용량·연결구, H2는 승인 H1·상대 배치·위상·내부 관계·외부 연결구, H3는 승인 H2와 Node·Edge·Connector 역할, H4는 세계 의도·승인 H3·내부 관계·외부 연결 역할을 검토해 승인한다. 실제 도로·건물·Block 경계나 공공데이터 부재는 H 설계 승격 차단 사유가 아니다.
- E단계 경계: 승인 H 설계를 특정 AreaSet·경관 그래프에 배치하고 사람이 설계한 도로·Gate·이동 경로를 닫는 것은 E5다. 선정 WI의 공간 판정에 필요한 공공데이터만 그 E5 인스턴스에 연결하고 출처·기준일·좌표계·해시·한계를 보존하는 것은 E6다. E6 자료가 H 정의로 역류하거나 H 설계 후보가 실제 AreaSet으로 자동 승격되지 않는다.
- 대체 관계: D-153·D-155·D-156의 위치 독립 설계 원칙을 강화한다. D-152·D-165·D-166·D-167·D-168 중 실제 도로·경계·공공데이터를 공식 H 정의의 선행 조건으로 둔 문구는 이 결정으로 대체한다. 실제 AreaSet 인스턴스의 E5 이동 폐루프와 E6 공공데이터 계보 관문은 유지한다.

## D-170 게임 기획 묶음이 H 재고 범위를 통제하고 H에서 WI의 E 부족분을 유도한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: 상향식 공간 설계는 H를 먼저 축적하되 모든 상호작용 H1을 기존 WI 또는 명시된 예상 플레이에 연결하고, H2~H4를 `Nature 생활·위협·회복`, `Farm 생산·생존`, `Town 생활·시장 안전`, `City/Hub 물류 회복력` 가운데 하나의 게임 기획 묶음에 귀속한다. Synty 팩이나 경관 문법만으로 맥락 없는 공식 H를 늘리지 않는다.
- 재고 처리: 상호작용 H1과 연결되지 않은 팩 표현 H1은 삭제하지 않고 `IdeaInventory`로 격리해 표현 연구 자료로만 유지한다. 기존 WI나 예상 플레이, 하위 H 계보 또는 게임 기획 소속이 없는 항목은 공식 승격과 E 단계 입력에서 제외한다. 자동 삭제와 자동 승격은 모두 금지한다.
- WI 관계: H가 플레이 공간과 연결 흐름을 제안하면 WI별 공간 요구와 현재 E 상태를 대조해 부족한 계약·시험·배치·공공데이터 작업을 만든다. 공간 비의존 WI에 H를 강제하지 않는다. `WI-ORDER-04 주문 포장`처럼 필수 공간이 빠진 경우에는 해당 게임 기획 안에서 H1을 먼저 보완한 뒤 E4 이상을 진행한다.
- 우선순위: H 확장은 `H-P0 맥락·필수 공간 누락 정리 → H-P1 Nature → H-P2 Farm → H-P3 Town → H-P4 City/Hub` 순서다. WI 증거는 `E-P1 Farm 수확·출하 E5 → E-P2 Hub 입고·보관 E5 → E-P3 Town 주문·수령 E4 → E-P4 Nature 사건 WI E1 → E-P5 명시적으로 선정한 WI만 E6` 순서로 진행한다.
- 관계: D-155·D-156의 상향식 재고와 문법 유도, D-162의 Nature 중심 세계 구조, D-169의 H/E·공공데이터 분리를 게임 기획 주도 재고 입고 관문과 두 개의 우선순위 대기열로 구체화한다.

## D-171 Nature 위협 대응의 예상 플레이 네 동사를 독립 E1 WI 계약으로 고정한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: Nature 상시 생활권의 `위협 관찰 → 긴급 후퇴 또는 원인 해결 뒤 복원 → 파티 회복`을 `WI-NATURE-01~04`로 등록한다. 관찰은 상태를 읽고, 후퇴는 안전 생활핵으로 이동하며, 복원은 해결된 원인 계보가 있는 경로만 다루고, 회복은 파티 준비 상태만 바꾼다.
- 금지 경계: 관찰·후퇴·파티 회복은 지역 사건 심각도나 자연권 압력을 낮추지 않는다. 복원도 원인이 해결되지 않은 사건을 우회 해결하거나 다른 경로를 자동 회복하지 않는다. Unity 이동·애니메이션·전투 연출은 어느 효과도 확정하지 않는다.
- H 연결: 다섯 Nature 상호작용 H1 후보와 위협 대응·복원 회복 H2, 자연 생활·위협·회복 H3에 실제 WI 식별자를 연결한다. 현재 WI 계약은 E1이고 H는 설계 후보이므로 Preview/Confirm·Task·저장 재생 시험 전에는 E3, 공식 H1 또는 E5로 승격하지 않는다.
- 호환: 기존 지역 사건·자연권 압력 계산, 세계 사건 API와 `simulation-save.v4`를 교체하지 않는다. 후속 E2~E3 구현은 기존 상태를 입력으로 재사용하고 새 명령의 멱등성·예상 개정·원인 계보를 별도로 검증한다.
- 관계: D-163의 지역 사건 압력, D-164~D-170의 Nature 우선 H 재고와 H 중심 E 보완 순서를 실제 WI 계약으로 연결함

## D-172 Nature H 설계는 반복 플레이 폐루프와 계획 용량을 먼저 봉인한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: Nature 상시 생활권의 기준 플레이를 `탐험 → 위협 관찰·흔적 조사 → 긴급 후퇴 또는 원인 해결 뒤 복원 → 안전 회복 → 탐험 복귀`로 고정한다. 다섯 H1 작업공간 후보, 두 H2 블록 후보와 하나의 H3 경관 후보는 이 반복 플레이와 양방향으로 추적될 때 `CandidateForReview`까지 승격할 수 있다.
- 용량 의미: H1의 `slot`, `route`, `party`, `trace`, `cargo-lot` 수량은 실제 면적·도로 폭·공공데이터 실측값이 아니라 동시 수행과 조립 충돌을 검토하기 위한 위치 독립 계획 용량이다. 공식 Simulation 용량은 WI E2~E3 규칙과 시험에서 별도로 확정한다.
- 연결 관문: 후퇴 분기는 위협 대응 H2의 `RecoveryHandoff`에서 복원·회복 H2의 `RetreatRecoveryInput`으로, 복원 분기는 `ThreatBandContinuation`에서 `IncidentRouteInput`으로 이어진다. 회복 뒤에는 안전 생활핵과 탐험 출구로 복귀해야 하며 어느 분기도 미해결 연결구를 통과하지 않는다.
- 증거 경계: 위 설계는 `DesignCandidateOnly / E1`이다. H 후보가 존재한다는 이유로 WI Preview·Confirm·Task·Effect·Save/Replay, 공식 H1, E5 지역 배치나 E6 공공데이터 결속을 성립한 것으로 보지 않는다.
- 관계: D-169의 H/E·공공데이터 분리, D-170의 게임 기획 주도 H 재고, D-171의 Nature E1 WI를 실제 반복 플레이와 공간 조립 검토 단위로 닫음

## D-173 자연권 위협 관찰은 기존 결정·작업·효과 원장을 재사용해 E3로 완결한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `WI-NATURE-01`은 전용 Preview·Confirm HTTP 계약에서 관찰 대상 자연권 경로와 현재 개정을 받고, 서버가 기존 범용 `Decision → Task → Effect` 요청으로 변환한다. Confirm은 관찰 공간 한 자리를 예약하고 한 Tick 뒤 `NatureThreatObserved` 효과와 원인 사건 계보를 기록한다.
- 비변경 경계: 관찰 효과의 압력 변화량은 0이다. 관찰은 지역 사건 상태·남은 심각도·자연권 압력·조우 상태를 변경하지 않으며 후퇴·복원 선택을 자동 확정하지 않는다.
- 공간과 증거: E3 자동 시험은 `Scenario` 근거의 관찰 공간을 사용한다. 이 성공은 H1 후보 승인, 실제 Nature AreaSet 배치, E5 이동 폐루프나 E6 공공데이터 근거를 만들지 않는다. H 계보는 계속 `CandidateForReview`다.
- 호환: 새 명령 종류를 Save schema에 추가하지 않고 기존 결정 Confirm 명령과 공간 예약을 `simulation-save.v4`로 재생한다. 동일 명령 재전송, 낮은 개정, 잘못된 경로, 공간 부재와 HTTP 경로 Manifest를 자동 시험으로 고정한다.
- 관계: D-171의 네 Nature WI 중 첫 동사를 D-172의 H 반복 플레이 설계 위에서 E3까지 구현함

## D-174 자연권 긴급 후퇴는 선행 위협 근거와 경로 예약으로 E3를 닫는다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `WI-NATURE-02`는 같은 자연권 경로에 적용된 `NatureThreatObserved` 효과 또는 활성 조우가 있을 때만 Preview를 통과한다. Confirm은 `EmergencyAccess + PlayerEscapeRoute + SafeCore`를 가진 후퇴 공간의 파티 용량을 예약하고, 한 Tick 뒤 `PartyRetreatedToSafeCore` 효과를 적용하며 예약을 반환한다.
- 비해결 경계: 후퇴는 지역 사건 상태, 남은 심각도, 자연권 압력과 조우 승패를 변경하지 않는다. 다음 `WI-NATURE-04` 회복을 열 수 있는 파티 상태 인계만 기록한다.
- 공간과 증거: E3 시험은 `Scenario` 근거의 후퇴 경로 한 파티 용량을 사용한다. 이 결과는 Nature H1 승인, AreaSet 배치, E5 이동 폐루프나 E6 공공데이터 근거가 아니다.
- 호환: 새 Save schema를 만들지 않고 기존 결정 Confirm·공간 예약·작업 취소·Save/Replay를 재사용한다. 전용 HTTP Preview·Confirm 경로와 멱등성·payload 충돌·낮은 개정·공간 부재를 자동 시험으로 고정한다.
- 관계: D-171의 두 번째 Nature WI를 D-173 관찰 효과의 후속 행위로 E3까지 구현함

## D-175 자연권 복원은 관찰된 원인 전체 해결 후에만 자재를 소비한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `WI-NATURE-03`은 같은 경로의 `NatureThreatObserved` 효과가 가리킨 원인 사건이 하나 이상 있고, 그 전체가 `Resolved / RemainingSeverity=0`이며 현재 경로의 남은 심각도도 0일 때만 Preview를 통과한다.
- 예약과 소비: Confirm은 `WorkerAccessible + CargoAccessible + RestorationWorkArea`를 가진 공간의 작업 자리와 복원 자재 한 롯을 원자적으로 예약한다. 취소 시 둘 다 반환하고, 완료 시 작업 자리는 반환하며 자재는 소비한다.
- 비해결 경계: `NatureRouteRestored`는 원인이 이미 해결된 경로에서 복구 행위가 완료됐음을 기록한다. 이 효과가 지역 사건 심각도, 자연권 압력, 다른 경로를 추가로 감소시키지 않는다.
- 공간과 증거: E3의 복원 공간과 자재 용량은 `Scenario`다. 이 성공은 Nature H1 승인, 실제 AreaSet 배치, E5 이동 폐루프나 E6 공공데이터 계보를 만들지 않는다.
- 호환: 기존 결정 Confirm·공간 예약·Save/Replay를 재사용하고, 공간 용량에 복원 자재라는 소모성 종류만 추가한다. 전용 Preview·Confirm HTTP와 미관찰·원인 미해결·공간 부재·취소·Save/Replay를 자동 시험으로 고정한다.
- 관계: D-171의 세 번째 Nature WI를 D-173 관찰 계보와 D-163 지역 사건 해결 규칙 위에서 E3까지 구현함

## D-176 파티 회복은 후퇴 또는 복원 효과 후 탐색을 재개하는 E3 행위다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 결정: `WI-NATURE-04`는 같은 자연권 경로의 `PartyRetreatedToSafeCore` 또는 `NatureRouteRestored` 적용 효과가 있을 때만 Preview를 통과한다. Confirm은 안전 생활핵의 파티 휴식 용량과 회복 보급 한 롯을 예약한다.
- 예약과 효과: 취소 시 휴식 용량·보급을 모두 반환하고, 완료 시 휴식 용량은 반환하며 보급은 소비한다. `PartyRecovered`는 파티 준비 상태와 다음 `Explore` 행위만 연다.
- 비해결 경계: 회복은 지역 사건, 남은 심각도, 자연권 압력, 조우 상태를 변경하지 않는다. Unity의 휴식 애니메이션이 회복 완료를 확정하지 않는다.
- 공간과 증거: E3의 안전 회복 야영지·휴식 용량·회복 보급은 `Scenario` 근거다. 이 성공은 Nature H1 승인, AreaSet 배치, E5 공간 폐루프나 E6 공공데이터 계보가 아니다.
- 완결 범위: 이 결정으로 현재 원장의 41개 WI 전체가 E3에 도달한다. 다음 단계는 자동 H 승격이 아니라 Nature 반복 플레이의 H1·H2·H3 사람 검토와 E5 후보 설계다.
- 관계: D-171의 네 번째 Nature WI를 D-174 후퇴·D-175 복원 효과의 공통 후속 행위로 E3까지 구현함

## D-177 Nature 팩 중심 상시 체류 세계를 심리 영역으로 정의하고 두 발전소 인과를 둔다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 용어: 플레이어가 상시 체류하는 Nature 팩 중심 세계의 게임 의미는 `심리 영역`으로, Farm·Town·City/Hub의 독립 AreaSet 묶음은 Simulation 문맥의 `업무 영역`으로 부른다. 심리 영역은 외부 세계 전체가 마음에서 비롯된다는 형이상학적 설명이 아니라 업무 결과가 플레이어의 회복력과 위협 상태에 미친 심리적 영향을 명시적으로 공간화한 세계다.
- 발전소: 심리 영역에는 `회복 발전소`와 `위협 발전소`를 각각 하나씩 둔다. 좋은 업무 결과는 회복 발전소를 강화하고 위협 발전소를 약화하며, 잘못된 결과와 기한 초과는 회복 발전소를 약화하고 위협 발전소를 강화한다. 두 상태는 같은 결과 계보에서 함께 변하지만 한 수치로 합치지 않는다.
- 인과 경계: 확정된 업무 결과는 발생 업무 영역의 직접 영향과 심리 영역의 발전소 변화를 함께 남긴다. 미리보기 차단과 일반 취소는 입력이 아니다. 심리 영역의 행동은 확산과 피해를 완충할 수 있지만 업무 직접 영향을 해결하거나 원인 사건을 종료하지 않으며, 원인 해결은 해당 업무 영역의 후속 WI가 담당한다.
- 되먹임: 두 발전소의 상태는 여러 업무 영역으로 다시 영향을 보낼 수 있으나 원인 경로와 업무 종류별 대상을 보존한다. 모든 영역에 같은 전역 수치를 일괄 적용하지 않으며 수치·임계값·업무별 효과표는 후속 Simulation 수직 조각에서 확정한다.
- 호환과 이행: `NatureHome`, `ProfessionalWorld`, Nature·Farm·Town·City/Hub 팩 코드, AreaSet·WI·API·저장 식별자는 바꾸지 않는다. D-162의 공간·시점·자산 관문은 유지하면서 사람이 읽는 세계 의미를 이 결정으로 구체화한다. D-163의 경로별 위협 계보는 유지하되 자연권 전투가 업무 사건 심각도를 직접 줄이는 현재 동작은 심리 완충과 업무 원인 해결을 분리하는 후속 구현에서 대체한다.
- 증거: 이번 결정은 용어와 인과 기획만 확정한다. 회복 발전소 상태, 두 발전소의 동시 변화, 업무 영역 되먹임, 저장 계약과 Unity 배치는 아직 구현·시험하지 않았다.
- 기준 문서: [업무 사건과 심리 영역 발전소 영향 규칙](../Architecture/지역사건-자연권위협-규칙.md)

## D-178 Construction 팩은 공통 조립층이며 두 발전소는 기존 Nature H2·H3를 확장한다

- 상태: `Accepted`
- 결정일: 2026-08-18
- 팩 역할: POLYGON Construction은 독립 AreaSet이나 다섯 번째 업무 영역이 아니다. Nature·Farm·Town·City/Hub의 구조물·공사·복구·격리·전환 상태를 만드는 공통 조립 재료층이다. 한 H1은 주도 팩 하나, Construction 기능층과 보조 팩 1~2개를 기본으로 하며 모든 공간에 다섯 팩을 같은 비율로 넣지 않는다.
- 발전소 H: `회복 발전 동력핵`과 `위협 발전 집속핵`을 예상 플레이가 있는 H1 문서 후보로 둔다. 회복 발전소는 기존 `nature-restoration-recovery`, 위협 발전소는 기존 `nature-threat-response` H2를 확장하고 둘은 기존 `nature-threat-recovery` H3 안에서 완충 지형과 복귀 동선으로 연결한다. 기존 `NatureHome`, H2·H3 Stable ID, AreaSet·WI·API·저장 식별자는 바꾸지 않는다.
- 변형과 권위: A/B/C는 같은 면적·연결구·핵심 소켓을 유지하는 공간 배치 변형이며 발전소의 시간·강도 상태가 아니다. Prefab 이름·경로는 교체 가능한 표현 참조이고 서버가 확정하는 업무 결과·발전소 상태나 Simulation 권위를 만들지 않는다.
- 증거: 이번 범위는 다섯 팩 2,346개와 Construction 584개 Prefab 설치 여부를 확인한 `DesignPlanOnly` 문서다. 새 H1의 기계 대장 등록, Unity 조립 Prefab·Scene, Play Mode·Game View와 Simulation 수치·저장 계약은 구현하지 않았다.
- 기준 문서: [심리·업무 영역 Synty 5팩 공간 조립 계획](../Architecture/심리업무영역Synty공간조립계획.md)
- 관계: D-155~D-156의 Synty 상향식 재고와 H 계층, D-165의 H2 조립 우선순위, D-170의 게임 기획 관문, D-177의 심리 영역·두 발전소 인과를 보유 자산 조립 규칙으로 구체화한다.

## D-179 다섯 Synty 팩은 H 승격 전에 전수 기술 대장과 의미 자산군으로 관리한다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 전수 범위: Nature 227·Farm 498·Town 702·City 335·Construction 584개, 합계 2,346개 Prefab을 기술 대장에 등록한다. Character·Vehicle·Item·Tool·FX도 누락하지 않되 정적 경관 자동 배치 대상과는 분리한다.
- 호환과 분류: 기존 Farm·Town·City 1,535개 `inventoryId` 산식은 유지한다. 팩마다 다른 원본 `Environment(s)` 경로는 새 정규화 분류로 흡수하고, 각 항목에 의미 자산군, 주 활용 트랙, 검토 상태와 계획 적용 영역을 둔다. Construction은 네 영역의 공통 상태층 후보이며 독립 H1로 자동 승격하지 않는다.
- 승격 경계: 전수 등록과 자동 분류는 보유 표현 재료를 찾은 증거다. 사람 검토 대기 항목과 대표 조립물은 별도로 검증하며 기술 대장만으로 H 승인, E4~E7, AreaSet 배치나 Simulation 상태가 성립하지 않는다.
- 구현 증거: `synty-pack-inventory.v2` 대장에 2,346개·1,499개 의미 자산군을 생성했고 Vehicle 51개를 포함한 자동 분류 2,345개와 Nature `Misc` 사람 검토 대기 1개를 분리했다. Editor·EditMode 시험 어셈블리는 오류 0개로 컴파일했고 집중 Unity EditMode 시험 4/4가 통과했다. 조립 Prefab·Scene, Play Mode·Game View는 완료 증거에 포함하지 않는다.
- 기준 문서: [심리·업무 영역 Synty 5팩 공간 조립 계획](../Architecture/심리업무영역Synty공간조립계획.md), [Synty 상향식 공간 재고 계획](../Architecture/Synty상향식공간재고계획.md)
- 관계: D-178의 Construction 공통 조립층과 발전소 설계를 바꾸지 않고, 그 설계 전에 수행할 표현 재료 전수 관리 규칙을 구현 수준으로 고정한다.

## D-180 휴대폰 공간 조립 검토는 주차 후 후보 선별이며 최종 Scene 승인이 아니다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 안전 경계: 모바일 검토는 합법적인 장소에 완전히 주차하고 운전을 종료한 뒤에만 사용한다. 신호 대기·정체 중 사용하지 않으며 화면은 `안전하게 주차했습니다` 확인 전 판단 입력을 잠근다.
- 권위와 상태: Unity 촬영 batch는 조합·계획·Rendering Profile·이미지 hash를 서버 검토 원장에 등록한다. 휴대폰은 Stable ID, 예상 개정, 멱등 키, 판단 코드와 선택적 문제·메모만 보내며 `Good`은 `ReviewedCandidate`다. 어떤 모바일 판단도 `ApprovedForSceneApply`, Simulation 상태나 운영 업무 결과를 만들지 않는다.
- 변경과 오프라인: 같은 조합물의 입력 또는 촬영 snapshot이 바뀌면 기존 판단을 `Stale`로 만들어 재검토한다. 통신 실패 요청은 휴대폰에 임시 보관하지만 `409` 개정 충돌을 자동 덮어쓰지 않는다.
- 첫 범위: 회복·위협 발전소 × A/B/C × 기본·강화 상태의 12개 카드를 첫 batch로 사용한다. 서버 Mongo 원장·관리자 API·모바일 Web 화면·오프라인 요청 대기열과 촬영 전 batch 생성기를 구현하며, Unity 실제 촬영·업로드와 PC 최종 승인은 후속 검증으로 남긴다.
- 기준 문서: [Synty 모바일 조합물 검토 계획](../Architecture/Synty모바일조합물검토계획.md)
- 관계: D-178~D-179의 다섯 팩 조립·전수 기술 대장을 사람이 짧게 비교 검토하는 인계 절차로 확장하며 기존 정적 경관 배치 승인 영수증의 hash 권위를 유지한다.

## D-181 Synty Web 검토 v2는 불변 촬영 영수증과 부모 bundle 계보로 재촬영을 닫는다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 계약과 경합: 새 Unity 촬영은 `synty-composition-review-batch.v2`를 사용하고 `ExpectedRevision`, `CompositionInputHash`, `RenderingProfileHash`, `ParentCaptureBundleHash`, `CaptureBundleHash`를 함께 보낸다. `NeedsRevision` 재촬영은 현재 원장의 부모 bundle·조립 입력·예상 개정이 모두 맞을 때만 `ReadyForReview`로 돌아가며, 원본 변경은 `Stale`, 늦은 재촬영은 `409`로 처리한다. URL 기반 v1은 읽기 호환으로 유지한다.
- 이미지 권위: Unity는 Blob URL을 결정하지 않고 PNG를 서버에 보낸다. 서버는 signature·크기·원본 hash를 검증하고 PNG를 재인코딩한 뒤 `UploadedSourceSha256`과 `StoredImageSha256`을 분리한다. `CaptureUploadId` 영수증이 `StorageProviderCode + ContainerName + ObjectName + hash`를 보존하며 Review Batch v2는 이 영수증만 참조한다.
- 저장 실패: Blob은 불변 object의 `create-if-absent`, Mongo는 촬영 영수증 원장으로 나눈다. Blob 성공 뒤 Mongo 실패는 orphan을 허용하고 같은 영수증 재시도로 원장 저장을 회복한다. 기존 object를 덮어쓰거나 실패 시 URL을 권위값으로 대체하지 않는다.
- 공개와 개인정보: 현재 검토 이미지는 공개 읽기 object다. URL 비밀성은 접근 제어가 아니므로 Capture Camera는 전용 layer만 렌더링하고 사용자·주소·주문·서버·인증·Console·token·로컬 경로를 PNG에 넣지 않는다. Web은 관리자 화면과 공개 이미지 접근 경계가 다름을 명시한다.
- 이행 순서: 12카드·48이미지를 먼저 만들지 않고 회복 발전소 A Normal 1카드에서 Capture Stage → 업로드 영수증 → Web 판단 → `NeedsRevision` 재촬영 폐루프를 먼저 닫는다. 현재 코드와 로컬 네 시점 PNG까지 구현했으며 Azure Blob·Mongo·관리자 HTTP·실제 휴대폰 왕복은 별도 운영 검증이다.
- 기준 문서: [Synty Web 조합물 검토 폐루프 계획](../Architecture/Synty모바일조합물검토계획.md)
- 관계: D-180의 주차 후 후보 선별과 `Good ≠ 승인`을 유지하면서 촬영 저장·재촬영 경합·공개 이미지 개인정보 경계를 구현 계약으로 구체화한다.

## D-182 Unity 산출물 검토 WebApp은 일반 업무 WebApp과 물리 프로젝트를 분리한다

- 상태: Accepted
- 결정: Unity 촬영 이미지·검토 이력·후보 판단은 `Ssalddel.Web.UnityReviewApp`이 소유한다. 일반 `Ssalddel.WebApp`과 01~05 역할별 WebApp은 Unity 검토 route, page, Client와 오프라인 대기열을 포함하지 않는다.
- 인증: 전용 앱은 기존 서버 로그인 API와 계약을 사용하되 별도 `ssalddel.unity-review.auth.v1` 저장 키를 쓴다. 검토 API에는 Bearer token을 명시적으로 전송하고 `서버관리자` 역할이 아니면 전용 앱 토큰을 보존하지 않는다.
- 배포: 전용 앱은 `Ssalddel.UnityReview.slnx`로 독립 빌드한다. 일반 제품 릴리스와 역할 WebApp에 자동 포함하지 않으며, 배포·hostname·공개 Blob 정책·민감 화면 검증을 별도 게이트로 관리한다.
- 공유 경계: 서버 API·`Ssalddel.Contracts`·공통 인증 토큰 구조만 재사용한다. 일반 WebApp 프로젝트·레이아웃·서비스에 대한 참조는 금지한다.
- 관계: D-181의 공개 이미지·관리자 검토 경계를 배포 가능한 클라이언트 경계로 구체화하며 `Good ≠ 승인`, 서버 원장 권위와 Unity 표현 전용 원칙은 유지한다.

## D-183 Synty 검토 폐루프는 저장·화면 상태·전송·촬영 조립 책임을 분리한다

- 상태: Accepted
- 결정일: 2026-08-19
- 서버: 검토·촬영 UseCase는 상태 전이와 PNG 검증을 소유하고 Mongo·메모리 원장 구현, 영수증 record와 저장 구현은 별도 `Stores` 파일이 소유한다. Blob 위치와 Mongo 영수증의 권위, 기존 API route·JSON 계약·Stable ID는 바꾸지 않는다.
- 전용 WebApp: Razor page는 표시만, code-behind는 인증 생명주기만, `Workspace`는 화면 상태와 판단 전이만 맡는다. HTTP Client는 Bearer API 통신만 맡고 브라우저 `localStorage` 대기열은 전용 오프라인 Store가 맡는다. 일반 `Ssalddel.WebApp`으로 책임을 되돌리지 않는다.
- Unity: 공개 메뉴와 `Synty공간조립Web검토CapturePipeline` 진입점은 유지하고 orchestration, 전용 Capture Stage·카메라, 서버 API client, 전송 model을 파일 단위로 나눈다. 원본 Synty Prefab·저장 Scene·촬영 Stable ID와 v2 계약은 유지한다.
- 검증 경계: 구조 분리는 서버·Web 집중 시험, Unity Editor 어셈블리 빌드와 그래픽 장치를 유지한 EditMode 촬영 시험으로 확인한다. 이는 Azure·Mongo·관리자 HTTP·휴대폰 실기기 왕복 증거를 대신하지 않는다.
- 관계: D-181의 서버 권위·촬영 계보와 D-182의 물리 Web 프로젝트 분리를 내부 책임 경계까지 구체화한다.

## D-184 Unity 산출물 검토 앱은 기존 Azure VM에서 별도 경로·배포 묶음으로 운영한다

- 상태: Accepted
- 결정일: 2026-08-19
- 배포 단위: `Ssalddel.Web.UnityReviewApp`은 일반 01~05 WebApp 게시 묶음과 분리한 `unity-review.tar.gz`로 게시하고 `/opt/ssalddel/web/unity-review`만 원자 교체한다. 일반 역할 앱 파일을 Unity 검토 배포에 다시 게시하거나 함께 교체하지 않는다.
- 공개 경로와 API: 추가 유료 host를 만들지 않고 기존 Azure 미리보기 VM의 `/unity-review/`를 사용한다. Production 앱은 같은 host 루트의 관리자 API를 호출하며 Caddy는 `/unity-review/*`만 전용 정적 루트로 보낸다. 물리 프로젝트·인증 저장 키·배포 묶음의 분리는 유지한다.
- 서버와 데이터: 검토 API가 바뀐 경우 서버 이미지는 별도로 갱신하되 기존 MySQL·MongoDB·Data Protection·업로드 볼륨과 VM 비밀값을 보존한다. 촬영 이미지는 기존 Azure Blob 공개 container의 불변 `world-composition-reviews/` prefix를 사용하고 Mongo 영수증 권위를 유지한다.
- 운영 경계: 화면은 H1·H2·H3 고유 식별자와 촬영 묶음을 보여 주지만 `Good`은 후보 판단일 뿐 H 승인·Scene 적용·E5·Simulation 완료가 아니다. Azure VM의 현재 비용 통제 운영창은 한국 시간 19:00~23:00이며 시간 밖 수동 기동은 별도 명시 승인을 요구한다.
- 관계: D-182의 물리 WebApp 분리를 저비용 Azure 미리보기의 실제 경로와 원격 교체 단위까지 구체화한다.

## D-185 H1~H4 Unity 조합물은 선택 Root 촬영 영수증으로 모바일 검토하되 공간 권위를 만들지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 결정: Unity 편집기에서 H1~H4 조합물의 최상위 Root와 H 계보를 명시하고, 저장 장면을 변경하지 않는 임시 장면에서 단계별 표준 시점 PNG를 만든 뒤 서버 업로드 영수증과 `synty-composition-review-batch.v3` 검토 묶음을 등록한다. 사용자는 전용 Web 검토함을 휴대폰으로 열어 후보 판단을 남긴다.
- 촬영 Profile: H1 장소는 4시점, H2 블록은 5시점, H3 경관은 6시점, H4 지역 청사진은 4시점이다. 서버는 주 검토 대상과 H 계보, Profile, 시점 수, 조립 입력·Rendering Profile·부모 묶음·예상 개정 및 개별 업로드 영수증을 함께 검증한다.
- 권위 경계: 촬영 이미지, 자동 계산한 팩 사용 비율과 모바일 `Good`은 표현 검토 증거와 `ReviewedCandidate`만 만든다. 공식 H 승인, WI의 E단계, AreaSet·경관 그래프·공공데이터 근거, Scene 적용이나 Simulation 상태를 만들지 않는다.
- 보안 경계: Unity는 관리자 토큰을 파일·manifest·로그에 저장하지 않고 환경 변수에서 읽는다. 서버가 PNG를 검증·재인코딩하고 불변 저장 위치 영수증을 발급하며 Unity는 Blob 위치를 결정하지 않는다.
- 관계: D-180의 주차 후 모바일 후보 선별, D-181의 불변 업로드·재촬영 계보, D-183의 촬영·전송 책임 분리를 H1~H4 일반 조합물로 확장함

## D-186 Unity 산출물 검토는 역할별 VM과 분리한 무료 대상 VM의 최소 Docker 스택으로 운영한다

- 상태: Accepted
- 결정일: 2026-08-19
- 대체 관계: D-184의 기존 Azure VM `/unity-review/` 공동 운영 결정을 대체한다. 기존 배포 묶음은 비상용 자료로만 남기며 새 검토 앱은 역할별 WebApp VM의 기동 시간·Caddy·MySQL·MongoDB·비밀값을 공유하지 않는다.
- 실행 경계: 별도 `Standard_B2ats_v2` 또는 같은 무료 대상 SKU에 Caddy, `Ssalddel.UnityReview.Api`, MySQL 8.4만 Docker Compose로 실행한다. MongoDB와 통합 `Ssalddel` 서버는 넣지 않으며 Compose 메모리 상한 64MB·320MB·384MB와 host 2GB swap을 적용한다.
- 코드 경계: `Ssalddel.UnityReview.Core`가 검토 상태 전이와 PNG 검증 소스만 재사용하고 전용 API는 통합 서버 프로젝트를 참조하지 않는다. 전용 solution·hostname·관리자 JWT·MySQL 원장·이미지 volume·배포 archive와 SSH key를 역할별 WebApp에서 분리한다.
- 저장 경계: 미리보기 이미지는 hash 기반 불변 Docker volume, 촬영 영수증과 검토 snapshot은 MySQL에 저장한다. 공개 URL은 Caddy 투영값일 뿐 권위가 아니고 `ContainerName + ObjectName + StoredImageSha256`이 위치·무결성 기준이다. 단일 VM volume은 운영 백업이나 H·E 승인 증거가 아니다.
- 비용 경계: 무료 대상 VM 혜택의 기간·월별 시간과 디스크·공인 IP·전송 부대 비용은 별도로 확인한다. 구독이 쓰기 가능한 상태가 아니면 provisioning을 중단하며 무료 크레딧 반복 생성을 전제로 하지 않는다.
- 관계: D-180의 주차 후 후보 선별, D-181의 불변 영수증·부모 bundle, D-182의 물리 WebApp 분리와 D-185의 H1~H4 촬영 Profile을 유지한다.

## D-187 H1은 인지 부품이고 H2는 첫 공간 조합 판단 단위다

- 상태: `Accepted`
- 결정일: 2026-08-19
- H1 역할: H1 설계 재고는 재사용 가능한 공간 부품이 존재하고 그 의미·표현 후보·게임 기획 맥락을 식별하는 단계다. H2 조합 입력이 되기 위해 모든 H1이 먼저 공식 Simulation 공간 능력·업무 용량 승인을 받을 필요는 없다. WI가 실제로 사용하는 H1의 강한 계약은 해당 WI의 E단계에서 별도로 검증한다.
- H2 역할: 사용자가 공간 구성을 처음 판단하는 기본 단위는 H2 블록 모판이다. 필수 H1 두 개 이상, 정확한 하위 revision·hash, 결정적인 상대 배치, 위상, 내부 도달 관계, 외부 연결구, 크기·금지 조합, 촬영 가능한 Unity Root와 표준 5시점 촬영 기록을 갖춘 뒤 H2 조합물 자체를 사람이 검토한다.
- 상태 분리: `H1 인지 부품 → H2 조합 가능 → H2 검토 가능 → H2 승인`을 구분한다. H2 조합 가능은 공식 승인이나 자동 승격이 아니며 Unity 촬영과 모바일 `Good`도 `ReviewedCandidate`만 만든다. 실제 AreaSet 배치·경관 이동 폐루프는 E5, 공공데이터 계보는 E6에서 별도로 검증한다.
- 재고 운영: H1의 `knowledgeStateCode` 자체를 H2 조합 차단기로 사용하지 않는다. 정의가 존재하고 WI 또는 예상 게임 플레이, 공간 역할, Synty 팩 또는 기준 문법 표현 근거가 각각 하나 이상 식별되면 `IdeaInventory`도 인지 부품으로 사용할 수 있다. 이 맥락 신호가 없는 고아 아이디어만 계속 격리한다. H2 후보 24개는 이 인지 조건을 기준으로 조합 가능성을 먼저 감사하고, 세부 조립법과 Unity Root가 준비된 항목부터 우선 촬영한다.
- 관계: D-170의 게임 기획 주도 H 재고, D-172의 계획 용량, D-180의 모바일 후보 선별과 D-185의 H1~H4 촬영 Profile을 유지하면서 H1 과승인 병목을 제거하고 H2를 실제 공간 판단 표면으로 재정의한다.

## D-188 H 조립·게임플레이 추적·E 증거·완주 상태는 독립 축으로 관리한다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 네 축: H1 인지 부품·H2 블록·H3 `LandscapeGraph`급 경관·H4 `AreaSet`급 지역 설계는 공간 조립 깊이다. `GameplayTraceState`는 `Unlinked`, 지원 경관, 직접 행위, H2 순서, H3 폐루프, H4 지역 인과를 별도로 기록한다. E0~E7은 증거 깊이이고 `PlayableSliceState`는 사람이 실제로 완주할 수 있는 마감 상태다. 어느 한 축도 다른 축을 자동 승격하지 않는다.
- 첫 엄격 관문: `NatureHomeThreatRecovery`와 `FarmProductionSurvival`은 `reference-play:nature-farm-day.v1`에서 H 추적 누락을 차단한다. Town 생활·시장 안전과 City/Hub 물류 회복력의 기준 플레이 누락은 경고만 남기며 기존 H 후보·사람 검토를 폐기하거나 막지 않는다.
- 카드 경계: 첫 기준 플레이에는 `Normal / Opportunity / Threat / Recovery` 조건 슬롯과 영향을 받는 단계·H1 표현 연결점만 둔다. 구체 카드 ID·수치·비용·확률·플러스·마이너스 효과는 서버 턴 규칙이 확정하며 H 대장과 Unity가 계산하지 않는다.
- 완성 판정: Nature↔Farm 기준 플레이는 설계·H 추적이 있어도 실제 E5 `LandscapeGraph`·`AreaSet` 결속, 정상·실패·회복 Runtime 완주, 처음 접한 플레이어의 20~40분 관찰, canonical `SimulationWorldShell` 저장 배선·Play Mode·Game View와 시각·음향·성능 마감 증거가 모두 있기 전에는 `Planned`를 유지한다. H 승인, 촬영 `Good`, 개별 E3·E4 성공만으로 `PlayableSliceComplete`를 선언하지 않는다.
- 관계: D-170의 게임 기획 주도 H 범위, D-172의 Nature 반복 폐루프, D-187의 H1/H2 공간 판단 분리를 보존하면서 실제 게임 완성을 반복해서 판정할 수 있는 별도 관문을 추가한다.

## D-189 H2·H3와 이론 E5 공간 생산은 사람 검토를 차단 관문으로 사용하지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 결정: 게임 플레이 맥락, 필수 하위 H 인지, 결정적 상대 배치, 최소 간격, 연결 그래프, 입구·출구, 세계 의도와 GraphRelation 폐루프를 자동 검사해 `TheoryQualified` H2·H3와 `E5TheoryQualified` Theory AreaSet 인스턴스를 반복 생산한다. 사람의 이미지 검토는 `DeferredBatchReview`로 남기며 생산을 멈추지 않는다.
- E5 경계: 이론 공장은 H4 후보를 그대로 AreaSet으로 가장하지 않고 별도 `area-set:theory:*` 고유 식별자, H3 Graph 인스턴스, 관계와 결정성 hash를 만든다. 이 산출물은 E5 공간 조립 계보를 가지지만 `EvidenceKind=TheoryGenerated`, `humanReviewed=false`, `publicDataBound=false`, `runtimeValidated=false`를 반드시 함께 기록한다.
- 사람 검토: 촬영·휴대폰 판단은 나중 개정의 미감·가독성 개선 입력이다. 검토가 없다는 사실을 숨기거나 자동 사람 승인으로 기록하지 않는다. D-180·D-185의 모바일 검토 경로는 선택적 품질 개선 수단으로 유지한다.
- 후속 관문: H2·H3·이론 E5 생산은 E6 공공데이터 계보와 E7 실제 서버·저장 Scene 플레이를 완료했다고 주장하지 않는다. 실제 지역 근거가 필요한 WI만 E6에서 연결하고 실제 플레이 완주는 E7에서 별도로 검증한다.
- 관계: D-187의 “H2가 첫 사람 판단 단위” 가운데 사람 판단의 생산 차단 역할을 대체한다. H1 인지 부품, H/E 독립 축과 자동 사람 승인 금지는 유지한다.

## D-190 이론 공간 공급과 실제 플레이 공간 완성 사이에 독립 완료 상태를 둔다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 상태 사슬: `PlayableSliceState`는 `Planned → TheorySpatiallyComposed → SpatiallyComposed → FunctionallyClosed → ExperienceValidated → PlayableSliceComplete` 순서를 사용한다. `TheorySpatiallyComposed`는 추적된 H2·H3와 H4 세계 의도가 결정적인 전용 `area-set:theory:*`에 결속되어 다음 실제 배치 작업의 입력이 준비됐음을 뜻한다.
- 이론 관문: 이 상태에는 `TheoryQualified` H2·H3, `E5TheoryQualified`, 별도 고유 식별자와 결정성 hash가 필요하다. 사람 검토, 게임플레이 추적, 공공데이터와 Runtime은 이론 공간 생산을 차단하지 않는다. 엄격 게임플레이 추적은 Nature·Farm 우선 작업 선정을 막을 수 있지만 이미 적격인 이론 H를 폐기하거나 강등하지 않는다.
- 실제 관문: `SpatiallyComposed`는 승인된 H가 실제 지역 근거의 `LandscapeGraph`·`AreaSet`과 양쪽 연결구에 결속된 실제 E5 이동 폐루프가 있을 때만 부여한다. 이론 E5는 실제 E5, E6 공공데이터, E7 서버·저장 Scene Runtime, Play Mode·Game View를 대신하지 않는다.
- 현재 판정: `reference-play:nature-farm-day.v1`은 Nature·Farm 이론 AreaSet 두 개까지 닫혀 `TheorySpatiallyComposed`다. `ActualE5BindingMissing`, Runtime 분기 완주, 처음 접한 플레이어 관찰, canonical Scene 실행 증거, 시각·음향·성능 마감이 남으므로 실제 게임 완성은 아니다.
- 관계: D-188의 네 독립 축과 최종 완료 기준을 유지하되, 이론 공간 공장을 도입한 D-189에 맞춰 D-188의 `Planned` 고정 판정을 이 중간 상태 모델로 대체한다.

## D-191 H2·H3는 StableId를 보존하고 팩 주도 패턴 이름을 별도로 가진다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 이름 계약: 기존 `h2-candidate:*`, `h3-candidate:*` StableId는 저장·계보·생성기 참조용으로 보존한다. 사람이 AreaSet 재고를 구분하는 이름은 `{주도 팩}-H{단계}-{패턴 계열}-{일련번호}` 형식의 별도 `patternCode`와 한국어 `displayNameKo`로 관리한다.
- 팩 경계: 단일 팩 `SinglePack`, 주도 팩과 보조 팩의 `LeadPackWithSupport`, 팩 사이 전환인 `CrossPackTransition`을 구분한다. 혼합 회랑은 어느 한 팩의 단독 자원으로 가장하지 않고 `MIX` 계열을 사용하며 Construction은 독립 AreaSet이 아닌 지원 기능층으로만 기록한다.
- 변형 경계: 패턴 일련번호는 서로 다른 H2·H3 공간 조립을 구분한다. 기준 경관 문법의 A/B/C 표현 변형과 같은 값이 아니며 패턴 코드가 Prefab·GUID·Scene·Simulation 권위를 만들지 않는다.
- 생산 연결: 이론 공간 공장은 패턴 이름 대장 전체를 검증하고 H3 Node에 하위 H2 패턴, 이론 AreaSet Graph 인스턴스에 H3 패턴을 투영한다. 대장 누락·중복·형식 오류·StableId 불일치가 있으면 생산을 중단한다.
- 자원 종류: H2는 여러 H1의 상대 배치·내부 동선·입구·출구를 봉인한 `BlockPattern`이고, H3는 여러 H2와 외부 연결 역할을 묶은 `LandscapeAssemblyPattern`이다. H2·H3를 단순 번호 목록이 아니라 팩·계열·공간 역할이 드러나는 패턴 재고로 관리한다.
- 확장 순서: 첫째 Nature·Farm·City·Town 각 팩만으로 구성 가능한 `SinglePack` H2 블록을 고르게 확보한다. 둘째 같은 팩의 H2를 그 팩 내부 H3로 묶는다. 셋째 주도 팩에 Construction이나 다른 팩을 보조층으로 붙이는 `LeadPackWithSupport`를 확장한다. 넷째 팩 경계를 직접 조립하는 혼합 H2와 혼합 H3를 차례로 만든다. 기존 혼합 패턴은 보존하지만 새 단독 팩 재고보다 먼저 증식시키지 않는다. 예약 이름은 실제 H 정의나 E5 증거가 아니다.
- 관계: D-170의 게임 기획 주도 H 재고, D-189의 비차단 이론 생산과 D-190의 이론 공간 완료 상태를 유지하면서 AreaSet에 채울 H2·H3 재고의 식별성과 확장 순서를 고정한다.

## D-192 팩 단독 H2는 팩 내부 H3보다 먼저 게임 기획 AreaSet에 대기 결속한다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 결정: 팩 단독 H2는 H3가 아직 정의되지 않았다는 이유로 고아 재고로 격리하지 않는다. Nature·Farm·City/Hub·Town 게임 기획 AreaSet 후보가 `stagedPackNativeH2Refs`로 직접 소유하고, 다음 팩 내부 H3 생산의 입력으로 사용한다.
- 현재 재고: Nature 생활핵·조우·방어 3개, Farm 관수·집중수확 2개, City/Hub 정비·비상전력 2개, Town 주거골목·주민서비스 2개를 추가해 H2를 24개에서 33개로 확장했다. 아홉 블록은 모두 단일 주도 팩 `SinglePack`, 위치 독립 상대 조립, 최소 두 H1, Network 문법, 입구·출구를 가지며 `TheoryQualified`다.
- E 경계: H2 생성과 이론 적격은 WI 플레이 순서 추적을 기다리지 않는다. Nature·Farm의 엄격 추적 누락과 City·Town의 경고형 추적 누락은 후속 E 보완 대상으로 표시하며 H3·AreaSet·실제 플레이 완성을 자동 주장하지 않는다.
- 관계: D-189의 사람 검토 비차단 이론 생산, D-191의 팩 주도 패턴 이름과 생산 순서를 첫 팩 단독 H2 묶음으로 실행한다.

## D-193 팩 내부 H3가 준비되면 AreaSet의 임시 H2 직접 참조를 H3 계보로 대체한다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 결정: 같은 팩의 H2를 조합한 H3가 `TheoryQualified`가 되면 게임 기획 AreaSet 후보의 `stagedPackNativeH2Refs`를 제거하고 해당 H3를 `requiredH3Refs`에 결속한다. H2는 H3 정의의 하위 계보로 계속 추적되며 고유 식별자와 해시는 유지한다.
- 현재 재고: Nature 생활핵·조우·방어 폐루프, Farm 계절 생산 폐루프, City/Hub 정비·비상 대응 폐루프, Town 주민 서비스 폐루프를 추가해 H3를 13개에서 17개로 확장했다. 팩 주도 H2·H3 패턴은 50개이며 다음 생산 대기열은 Nature–Town 혼합 H2·H3 두 개다.
- 게임플레이 추적: 엄격 기준 플레이가 사용하는 새 Nature·Farm H3는 플레이 단계에서 명시적으로 추적한다. AreaSet이 H3를 요구한다는 사실만으로 플레이 계보를 자동 보완하거나 E단계를 승격하지 않는다.
- E 경계: 새 H3와 이론 AreaSet 재조립은 위치 독립 `TheoryQualified`·`E5TheoryQualified` 증거다. 실제 지역 Graph 결속, 공공데이터, Unity Scene·Runtime 또는 사람 검토 완료를 뜻하지 않는다.
- 관계: D-192의 H2 대기 결속을 완료된 팩 내부 H3에 한해 종료하고, D-188~D-191의 H·게임플레이·E 독립 축과 StableId 보존 원칙을 유지한다.

## D-194 Nature–Town 혼합 경관은 선택 계보로 만들고 실제 E5 배치를 자동 생성하지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 공간 구성: `MIX-H2-NATURE-TOWN-01`은 Town 구호 물자 인계점과 Nature 복원 작업·안전 회복 공간을 연결한다. `MIX-H3-NATURE-TOWN-01`은 Town 회수·구호 블록, 이 전환 블록, Nature 복원·회복 블록을 `Town 구호 → Nature 복원 → 안전 생활핵 귀환` 흐름으로 묶는다.
- 문법 경계: 기준 156 문법에 존재하지 않는 Nature–Town 전환어를 새로 꾸미지 않는다. H2는 타운·농촌 곡선 도로, H3는 건물 전면·수변 전환 문법을 조합하고 혼합 의미는 H 계보와 연결구가 담당한다.
- AreaSet 경계: 혼합 H3는 Nature와 Town H4 청사진 양쪽의 선택 가능한 교차 경관 계보다. 같은 Graph를 두 이론 AreaSet에 자동 복제하지 않으며 실제 소유 Graph, 양쪽 연결점과 GraphRelation은 후속 E5에서 결정한다.
- E 경계: H2 34개·H3 18개의 `TheoryQualified`와 패턴 대기열 0개는 위치 독립 설계 재고 완성을 뜻한다. 새 경관의 WI 추적·실제 지역 E5·공공데이터 E6·Unity Runtime을 완료했다고 주장하지 않는다.
- 관계: D-191의 P4·P5 혼합 생산 순서를 실행하고 D-188~D-190의 H·게임플레이·E 독립 관문을 유지한다.

## D-195 H2는 배치 가능한 물리 블록이고 H3는 배치 가능한 구역 조립안이다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 주 이름: H2·H3 목록과 공간 계획기에서는 이름 대장의 `spatialDisplayNameKo` 물리 공간 이름을 먼저 표시한다. 기존 `탐색·대피형`, `집중 집하형`, `순환시장형` 같은 행동 중심 이름은 삭제하지 않고 `gameplayProfileNameKo` 보조 활용 유형으로 내린다. StableId와 팩·계열 `patternCode`는 바꾸지 않는다.
- H2 계약: `BlockPattern`은 여러 H1의 로컬 상대 배치, 내부 이동 관계, 기준 경계, 허용 회전, 크기 변형과 외부 연결 역할을 봉인해 도시·농장·자연권 계획기에 한 단위로 놓을 수 있어야 한다.
- H3 계약: `LandscapeAssemblyPattern`은 여러 H2의 상대 배치, 블록 사이 이동 관계, 기준 경계, 구역 형태와 외부 연결 역할을 봉인해 지구·캠퍼스·회랑 단위로 놓을 수 있어야 한다.
- 게임플레이 경계: WI와 사건 흐름은 H2·H3의 존재 이유와 검증 계보지만 공간 자원 자체의 주 이름이나 자원 종류를 대신하지 않는다. 같은 물리 블록은 능력·용량 관문을 만족하는 여러 WI가 재사용할 수 있다.
- 근거 경계: `LocalMeters` 기준 경계와 `spatialFormCode`는 위치 독립 이론 배치 계약이다. 실제 지역 좌표·도로·건물·공공데이터 E6, Unity Scene·Runtime E7 또는 사람 승인을 증명하지 않는다.
- 관계: D-187의 H2 첫 공간 판단 단위와 D-191의 팩 주도 식별자를 유지하면서, 행동 중심 표시 때문에 H2가 블록처럼 읽히지 않던 문제를 공간 계획기용 자원 계약으로 보완한다.

## D-196 실제 E5는 네 전용 AreaSet과 하나의 Network로 결속하고 모든 이론 H3의 처리를 명시한다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 공간 소유권: Nature·Farm·City/Hub·Town은 각각 독립 `area-set:sim:*`을 소유하고, AreaSet 내부 H3는 내부 `LandscapeGraph`, 영역 사이 H3는 `AreaSetNetwork` 소유 경로 Graph로 둔다. 기존 `area-set:sim:pyeongchang:farm-hub-town.v1`은 공개 계약 호환 facade로 보존하며 새 실제 공간의 소유권으로 재사용하지 않는다.
- 현재 결속: 내부 Graph 14개와 Network 경로 Graph 3개를 AreaSet 4개·Network 1개·방향 관계 8개로 결속한다. 현재 이론 H3 18개 가운데 17개는 실제 E5 소유자를 갖고 `h3-candidate:nature-town-relief-loop` 1개는 정책에 명시적으로 보류한다. 이후 이론 H3도 승격 또는 보류 중 하나로 분류되지 않으면 생성을 실패시킨다.
- WI 결속: 전체 WI 41개를 직접 공간 결속 30개, AreaSet 문맥 결속 5개, 비공간 6개로 상호 배타적으로 분류한다. 기준 플레이의 Nature·Farm WI는 이 실제 대장을 근거로 유효 통합 단계를 E5로 계산하고 `reference-play:nature-farm-day.v1`을 `SpatiallyComposed`로 올린다.
- 좌표·증거 경계: 이번 실제 E5는 사람이 승인한 게임 기획을 `ScenarioLocalMeters`에 결정적으로 작성한 공간 결속이다. 공공데이터·실제 도로·DEM 계보 E6, 실행 중 서버·Session DB·사람 조작 E7, 운영 상태나 사람 시각 승인을 뜻하지 않는다.
- Unity 적재: canonical `SimulationWorldShell` 하나에서 Nature는 상시 유지하고 Farm·City/Hub·Town은 선택된 업무 영역만 적재한다. Network 경로 Graph는 전환에 필요한 경로만 준비·캐시하며 별도 공식 Scene을 만들지 않는다.
- 관계: D-190의 `ActualE5BindingMissing` 현재 판정을 대체해 `SpatiallyComposed`로 올리되, `FunctionallyClosed` 이후 관문은 그대로 유지한다. D-194의 Nature–Town 혼합 H3 보류와 D-188의 H·게임플레이·E·완주 독립 축을 유지한다.

## D-197 지역 위협·회복과 카드 효과는 서버 권위 v5 인과 원장으로 계산한다

- 상태: `Accepted`
- 결정일: 2026-08-19
- 인과 원장: Simulation Session은 원인 경로별 `Threat`와 `Recovery`를 함께 보존한다. 확정된 안전 업무 결과와 Nature 복원·파티 회복은 회복을 높이고 위협을 낮추며, 불안전 선택과 기한 초과는 반대로 적용한다. 미리보기 차단과 일반 취소는 인과 입력이 아니다.
- 카드 경계: 정방향의 유리한 카드는 회복 쪽, 역방향의 불리한 카드는 위협 쪽 변화로 확정한다. 다음 날 `Normal / Opportunity / Threat / Recovery`, 사건 심각도와 경로 영향은 서버가 계산하며 Unity는 결과 코드와 원장을 표시할 뿐 수치나 플러스·마이너스를 다시 계산하지 않는다.
- 저장 호환: `simulation-save.v5`는 인과 상태와 변화 계보를 저장·재생한다. v1~v4 저장·재생은 당시 의미를 보존하기 위해 새 인과 규칙을 적용하지 않으며 기존 결과 hash 호환을 유지한다.
- 표현 경계: Unity HUD는 서버 상태 사본을 읽어 위협·회복과 다음 날 결과를 보여 준다. 저장 Scene 배선과 EditMode 시험은 E7 사람 조작, Play Mode·Game View 또는 업무 완료의 증거가 아니다.
- 관계: D-177의 심리 영역·업무 영역 되먹임을 실행 규칙으로 구체화하고, D-196의 실제 E5 공간 소유권과 독립적으로 서버 상태 권위를 유지한다.

## D-198 H 공간 공장은 모든 계층에서 명시적 연결점 의미와 방향성 흐름을 재귀 검증한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 재귀 계약: H2는 H1 배치와 H1 관계, H3는 H2 배치와 H2 관계, 이론 AreaSet은 H3 Graph 배치와 H3 관계, Theory World는 AreaSet 배치와 AreaSet 관계로 구성한다. 배열 순서에 따른 암묵 이동선은 사용하지 않고 `h2RelationRecipes`, `h3RelationRecipes`, `areaSetRelationRecipes`, `worldRelationRecipe`를 실행 입력으로 사용한다.
- 연결 의미: 연결점은 원본 하위 공간·연결점 계보, `Input / Output / Bidirectional` 방향과 복수 이동 종류를 가진다. 관계는 `Directed / Bidirectional`, 이동 종류와 호환 규칙을 명시한다. 관계 고유 식별자는 양쪽 공간·연결점·관계 의미에서 계산하고 정규 정렬 뒤 hash를 생성한다.
- 적격 판정: 하위 공간 두 개 이상과 무방향 연결 성분을 구조 관문으로 검사하고, 연결점 존재·방향·이동 종류·호환 규칙·필수 흐름의 방향성 도달을 의미 관문으로 검사한다. H3는 H2 두 개 이상이어야 하며 기존 Farm–Hub·Hub–Town 단일 회랑 H3는 출하/출고 블록·회랑·입고 블록의 세 H2로 보정한다.
- 실패 분리: 알 수 없는 고유 식별자·방향·호환 규칙 같은 잘못된 계약은 생성을 실패시킨다. 필요한 연결점이나 흐름이 아직 없는 정상적인 설계 미완료는 `SemanticRelationUnresolved`로 남긴다. `SpatialInventoryGap`만 새 H 후보의 근거로 사용하고 `EvidenceGap`은 기존 공간의 WI·시험·계보 보완 대상으로 처리한다.
- 세계 흐름: Nature와 Farm·Town·City/Hub 사이는 양방향 플레이어 이동으로, Farm→City/Hub→Town은 단방향 화물 흐름으로 검증한다. `TheoryWorldQualified`는 위치 독립 의미 폐쇄일 뿐 사람 승인, 실제 지역, 공공데이터 E6, 서버·Unity Runtime E7 권위를 주장하지 않는다.
- 관계: D-189의 사람 검토 비차단 생산, D-195의 배치 가능한 H2·H3 정의와 D-196의 실제 E5 소유권을 유지하면서, 단순 배열 연결을 의미 기반 공간 계획으로 대체한다.

## D-199 LH는 스트리밍 범위와 셀 내용을 분리하고 L과 H를 조회 관계로만 연결한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 책임 분리: LH의 스트리밍 범위 계산기는 플레이어·NPC 관심점에서 필요한 L3 셀과 상세·활성·선행 준비 우선순위만 계산한다. 셀 내용 공급자는 그 셀에 H 계보·공간 배치·연결구·능력 근거·현재 상태를 결합한다. 조정 서비스는 두 책임을 순서대로 호출하며 어느 구현도 상대 책임을 다시 계산하지 않는다.
- L/H 관계: L0~L3는 실행 해상도이고 H1~H4는 의미 구조다. L0→H4, L1→H3, L2→H2, L3→H1은 주 조회 기본값이지 등식·소유권·완료 조건이 아니다. 한 셀은 여러 H 단계를 함께 조회할 수 있고 하나의 H 공간도 여러 셀에 걸칠 수 있다.
- 공급자 경계: 기존 절차 생성은 시험용 셀 내용 공급자로 보존한다. 후속 실제 세계 공급자는 E5 실제 공간, E6 현실 공간자료와 서버 시뮬레이션 현재 상태를 결합한다. 실제 세계 조회 실패나 증거 부족을 시나리오 절차생성으로 조용히 대체하지 않는다.
- 호환: 기존 `DefaultHLevelCode`와 Preview route·안정 식별자·hash는 유지하고 `PrimaryHQueryLevelCode`와 `ContentSourceCode`를 추가한다. 문서와 화면은 한국어 역할명을 먼저 쓰고 실제 코드 이름은 첫 언급에만 병기한다.
- 완료 경계: 이 분리는 LH 엔진 구현과 시나리오 시험의 구조 증거다. E5·E6 실제 공급, Unity 셀 조립, 연결구 통과, 서버 확인 지점 상태 변경, 세계 개정 번호에 따른 재계획이 폐루프로 검증되기 전에는 LH E7 실제 실행 검증 완료로 승격하지 않는다.
- 관계: D-158의 L/H 직교성과 창 크기를 유지하면서 “기본 대응”을 주 조회 관계로 엄밀히 하고, D-160의 로컬 시험 경로와 D-196의 실제 E5 공간 소유권을 교체 가능한 공급자 경계로 연결한다.

## D-200 H2·H3 재고는 팩별 수량이 아니라 게임플레이 공간 수요로 증산한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 수요 원장: H2·H3 신규 생산과 기존 재고 개정은 `gameplay-h-inventory-demands.v1`에서 게임 기획, 관련 WI, 재사용 H, 신규 H와 반드시 닫혀야 하는 흐름을 먼저 선언한다. `SpatialInventoryGap`만 새 H 생성 근거가 되며 `EvidenceGap`은 기존 공간의 WI·시험·Runtime 증거 보완으로 보낸다.
- 첫 증산: City/Hub의 보관→피킹→출고준비→상차와 Town의 배송→입고·검수→후방재고→진열→피킹·포장→주민 수령을 우선한다. 기존 H1을 재사용해 H2 3개와 H3 2개를 추가하고 기존 H2 34개·H3 18개의 StableId를 보존한다.
- 자격 경계: 신규 H는 위치 독립 `TheoryQualified`까지 자동 생산할 수 있으나 WI의 E단계, 실제 E5, 공공데이터 E6와 서버·Unity E7을 자동 승격하지 않는다. 이론 AreaSet에는 신규 H3를 포함하되 실제 E5 정책에서는 명시적으로 보류한다.
- 관계: D-170의 게임 기획 주도 재고, D-188의 H·게임플레이·E·완주 독립 축, D-198의 재귀 의미 관계 검증을 유지하면서 균등 재고 목표보다 실제 플레이 인과선의 역할 분리와 의미 폐쇄를 우선한다.

## D-201 E8은 NPC 생활세계의 자율 행동 폐루프를 검증한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 단계 정의: E8은 선정된 E7 세계에서 NPC가 지속되는 정체성·역할·생활 상태로 다음 목표를 결정하고, 실제 H 공간과 경관 그래프 경로를 선택해 공간·자원을 예약하고 WI를 수행한 결과가 다음 행동에 반영되는 연속 폐루프를 검증하는 단계다.
- 최소 관문: NPC 두 명 이상, 업무 WI 한 종류 이상, 비업무 생활 이동 한 종류 이상, 공유 공간·자원 경쟁 또는 인계 한 건 이상, Effect가 후속 선택을 바꾸는 연속 행동 두 회 이상, Save/Replay 동일성과 실제 서버·저장 `SimulationWorldShell`의 Play Mode·Game View 증거를 요구한다.
- 이동 의미: `NpcTraversal`은 출근·대기·휴식·시장 방문·대피·순찰 같은 일반 생활 이동, `WorkTraversal`은 확정된 WI·Task를 위한 업무 이동, `CargoLogistics`는 화물 이동으로 구분한다. 경로 부재를 순간이동·임의 좌표·Scenario fallback으로 숨기지 않는다.
- 권위 경계: Simulation 서버가 NPC 목표·행동·예약·완료를 결정하고 Unity는 그 결과를 표현한다. NavMesh 도착, Animator Event와 NPC 수는 E8 증거가 아니며 실제 계약·결제·배차·발주·정산을 NPC 자율 행동으로 실행하지 않는다.
- 적용 범위: E8은 모든 WI의 공통 목표가 아니라 `AreaSet + H 경로 + WI 묶음 + NPC 역할군`별 선택 증거다. 이번 결정은 정의와 완료 관문만 확정하며 기존 E0~E7 실행 원장·API·Simulation·Unity 구현을 변경하지 않는다.
- 상세 기준: [E8 NPC 생활세계 폐루프 정의](../Architecture/E8-NPC생활세계폐루프정의.md)를 따른다.
- 관계: D-013의 NPC 이동 Presentation 경계, D-117의 Simulation NPC 조직·역량·정책 권위, D-188의 독립 증거 축과 D-196의 실제 E5 공간 소유권을 유지한다.

## D-202 H5는 권위 상대 공간이며 E6는 선택형 현실 결속이다

- 상태: `Accepted`
- 결정일: 2026-08-20
- H4/H5 경계: H4 청사진은 재사용 공간 의미이고 AreaSet은 특정 세계 문맥의 H4 지역 인스턴스다. H5 `WorldLayout`은 H4 인스턴스와 물리 H3 회랑의 상대 배치를 소유하고, 이동 가능성과 경로 선택은 기존 `AreaSetNetwork`가 계속 소유한다.
- 좌표 계약: H5 직접 자식만 H5 루트 로컬 좌표 `ScenarioLocalMeters`를 사용하고 H4 이하 자식은 바로 위 부모 기준 `ParentLocalMeters`를 사용한다. 최종 위치는 부모 회전을 포함해 변환 합성하며 임의 Scale을 허용하지 않는다.
- E6 선택성: E6는 모든 H5의 필수 단계가 아니다. 세계 배치 정의, 현실 결속 적용 상태, 현실 자료 준비도를 분리하고 첫 세계는 `Optional / ScenarioRelative / NotApplied / Partial`로 둔다. E6 부재는 LH를 절차생성으로 후퇴시키는 근거가 아니다.
- 불변성: E6와 Floating Origin은 H5 이하 상대 X/Z와 배치 hash를 바꾸지 않는다. 실제 자료 정합을 위해 지역·회랑·연결 지점을 옮겨야 하면 E6 수정이 아니라 새 H5 revision을 만든다.
- 저장 계보: LH 저장 상태는 H5 고유 식별자·revision·배치 hash, 배치 권위, 현실 결속 상태와 적용된 경우의 현실 근거 hash를 보존한다. E6 준비도는 권위 상태가 아니다.
- 상세 기준: [H5 세계 배치와 선택형 E6 현실 결속](../Architecture/H5세계배치와선택형E6결속.md)을 따른다.
- 관계: D-196의 실제 E5 AreaSet/Network 소유권과 D-199의 LH 공급자 분리를 유지하면서 H5 세계 배치와 선택형 E6 투영 경계를 추가한다.

## D-203 DEM·도로는 공통 필수 자료가 아니라 현실 결속 프로필의 선택 요구다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 결정: H1~H5 공간 설계, WI Simulation, Scenario 상대 공간과 E7 실제 플레이는 DEM·도로·건물·블록 경계의 존재를 공통 선행 조건으로 요구하지 않는다. 해당 자료는 E6 현실 결속을 적용하기로 선택한 프로필이 그 목적에 필요하다고 선언할 때만 요구한다.
- 정책 판정: `NotRequired`와 `Optional / NotApplied`는 자료 부재로 차단하지 않는다. `Required` 또는 `Optional / Applied`는 선택된 프로필 안에서만 자료 요구·준비도·오염 전파를 판정하며, 누락은 해당 현실 결속 목표를 막되 H5 권위 배치나 Scenario 실행을 무효화하지 않는다.
- 계약 경계: 위치 독립 H와 WI 공간 계획에는 `requiredEvidencePurposeCodes`를 두지 않는다. 현실 자료 후보는 별도 `realityGrounding`에 정책·적용 상태·목표 완료 필요 여부·후보 목적·한계를 기록한다. DEM이나 도로를 다른 공급자로 묵시 대체하거나 `Required` 요청을 Scenario로 후퇴시키지 않는다.
- 기존 결정 정리: D-152의 `E6 WI 필수 공공데이터` 표현을 모든 WI에 적용되는 보편 의무로 읽지 않는다. D-202의 선택형 E6를 구체화하며, 특정 지리 파생 프로필 내부의 필수 Layer 판정은 그 프로필에만 유지한다.
- 기준 문서: [H5 세계 배치와 선택형 E6 현실 결속](../Architecture/H5세계배치와선택형E6결속.md)을 따른다.

## D-207 E6는 AreaSet 정밀 몰입 성숙도이며 GIS 결속은 독립 선택 축이다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 단계 정의: E6는 E5로 성립한 AreaSet의 H3·H2·H1·WI·상태 변화를 질문 행렬로 정밀 조사하고, 모든 H3와 교차 H3 인과 폐루프를 근거와 함께 설명할 수 있는 성숙도다. E5 합격 상태를 덮어쓰지 않고 `SpatialMaturity`, `ImmersionMaturity`, `FreshnessState`, `GroundingStatus`를 독립적으로 보존한다.
- 첫 적용: Farm AreaSet의 농가·생산·후처리, 고지대 농장, 계절 생산·출하, 사고 격리·회복 네 H3와 수확→후처리→출하 및 사고→복구→생산 복귀 폐루프를 첫 `RequiredBeforeE7` 대상으로 삼는다.
- 현실 문맥: 농사로 작물·재해 문맥과 KAMIS 시장 관측 계약은 출처·판본·hash·제한을 가진 설명 근거다. USDA AMS와 GIS는 선택 근거다. 어떤 공공자료도 H5 좌표, 생산량·수익성, WI 규칙을 자동 변경하지 않으며 라이브 공급자 호출 없이 계약 참조만으로 판정한 사실을 명시한다.
- 최신성: AreaSet/H5/H3/H2/H1/WI/질문/정책/근거/생성기 입력 hash가 달라지면 결과는 `Stale`이며 E7 시작 관문을 닫는다. `ImmersionQualified + Stale`을 허용해 과거 합격 사실과 현재 재검사 필요를 함께 표현한다.
- E7 경계: E6 합격은 E7 검증을 시작할 수 있다는 뜻일 뿐 실제 서버 HTTP, 저장 Scene, Play Mode, Game View 또는 E7 완료 증거가 아니다. GIS `NotApplied`만으로 E7을 차단하지 않는다.
- 관계: D-202의 H5 권위 상대 공간과 D-203의 선택형 GIS 자료 요구를 유지하면서, 과거의 E6를 GIS 적용 여부 하나로만 해석하지 않고 AreaSet 이해 성숙도로 확장한다.

## D-204 전투 맵은 H5의 확대가 아니라 지역 문맥에서 결정적으로 파생한 독립 공간이다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 공간 경계: H5 생활세계는 전투 장소의 계보·주변 H 공간·연결 지점·경로·접근 방향을 제공한다. 전투 맵은 이 사실을 `BattleLocalMeters`에서 전술적으로 재구성한 별도 Simulation 공간이며 좌표를 H5로 역투영하거나 Floating Origin 보정을 권위값으로 저장하지 않는다.
- 지역 유효성: 전역 `WorldRevision` 불일치는 재검증 계기일 뿐 자동 거부 사유가 아니다. 현재 1km 지역 문맥 해시와 전장 파생 입력 해시가 미리보기와 같으면 확정할 수 있고, 전체 H5 배치 hash는 계보로만 보존하며 전장 Seed에 넣지 않는다.
- 생성 순서: 문맥 추출 뒤 보존 등급·원본 대상·경로·관계·문맥 경계 포털을 난수 없이 먼저 확정하고, 그 제약 집합 hash 뒤에만 Seed를 계산한다. 첫 Profile은 1km 문맥에서 500m `FarmPerimeter500` 또는 `NatureField500` 전장을 만든다.
- 전투 권위: 부대 시작 사본에는 배우 상태·역할·역량과 카드의 플러스·마이너스 수정치, 전투 규칙 개정을 함께 봉인한다. 참여 배우와 충돌 가능한 세계 대상은 전투 동안 예약하고 100ms BattleTick과 고수준 명령은 서버가 처리한다.
- 결과 합류: 전술 피해값을 H5 수치로 복사하지 않고 시설 피해·관문 피해·배우 부상·목표 확보/상실 의미 효과로 집계한다. `전투 + 원본 대상 + 효과 코드 + 효과 고유 식별자` 멱등 키로 다음 안전한 WorldTick에 한 번만 적용한다.
- Unity 경계: canonical `SimulationWorldShell` 안에서 서버 전장 계획을 별도 Root로 조립하되 전장 생성·판정·세계 결과를 Unity가 다시 계산하지 않는다. 코드·EditMode 증거와 실제 Play Mode·Game View 전환 증거는 구분한다.
- 관계: D-197의 카드·Simulation 서버 권위, D-202의 H5 상대 공간 불변성과 D-199의 실행/표현 분리를 유지하면서 경영세계→전투→경영세계 폐루프를 추가한다.

## D-205 H5 통합 생활세계는 정적 장소와 Session 가변 시설을 결합한 WI 폐루프로 구현한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 공통 뼈대: 농사·제조·물류·건설·군사·전투·복구는 별도 게임 모드나 선행 범용 엔진이 아니라 같은 Session의 `Preview → Confirm → 예약 → Task → Effect`를 공유하는 수직 WI 묶음으로 구현한다. 앞선 Effect가 다음 WI의 가능성을 실제로 열거나 닫아야 한다.
- 공간·시설 경계: H5·H1은 장소와 능력 수용 조건을 제공하고 병영·창고·제조소는 그 장소를 점유하는 `RuntimeFacility`다. 기존 시설은 Scenario seed에서 결정적으로 생성하며 플레이어 병영은 건설 확정 때 `Planned`로 생성되어 건설 중과 운영 상태를 같은 Project와 원자적으로 전이한다.
- 상태 권위: 시설 피해 Effect는 불변 기록이고 적용 대기 항목·적용 영수증·현재 능력 제한을 분리한다. 수리는 확정 시 고정한 제한만 해결하며 시설 능력은 정의와 남은 활성 제한에서 계산한다. Actor 모집·훈련·편성·전투는 실제 Actor 점유와 부상 가용성을 공유한다.
- 전투 결속: `BattleRelevantOverlayHash`는 Session 전역값이 아니라 전투 범위별 파생값이다. `BattleWorldContextHash`는 정적 지역 문맥·조우 범위·공격/방어 문맥·관련 Runtime 투영과 규칙 버전으로 계산하고 `SourceWorldRevision`은 추출 메타데이터로만 둔다.
- 결정성과 저장: 제조 Job·건설 Project·Formation은 각 WI 사이를 잇는 지속 객체이며 시작 시 규칙 revision/hash와 계산된 입출력·기간을 동결한다. Lot 소비·출력 생성·자재 소비·피해 적용은 명령 멱등성과 적용 영수증으로 정확히 한 번 수행하고 `simulation-save.v6`에서 재생한다.
- 자원 규칙: 운송 상자는 감자 포장에 소비되는 자원이지 일반 화물 운송의 공통 필수조건이 아니다. Hub 제조→Farm 운송→병영 건설·감자 포장→민병대 편성→전투 피해→부품 운송·수리→출하 복구를 첫 기준 폐루프로 삼는다.
- 증거 경계: 기준 구현 목표는 위치 독립 Scenario E3다. 이 계약만으로 실제 E5 공간 결속, 선택형 E6 현실 자료, Unity E7 Runtime 또는 NPC E8 자율성을 주장하지 않는다.
- 상세 기준: [H5 통합 생활세계 기준 구현 계약](../Architecture/H5통합생활세계기준구현계약.md)을 따른다.

## D-206 소규모 현장 전투와 대규모 파생 전장은 같은 서버 전투 원장을 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 규모 정책: 적 1~3명은 즉석 교전, 적 4~5명·정예·동료 참여는 현장 전투, 그보다 큰 무리·대규모 습격·강제 사건은 H5 전투 문맥 기반 독립 전장으로 판정한다. 판정과 전환은 서버 권위이며 Unity가 적 수만으로 다시 계산하지 않는다.
- 공간·시간 경계: 현장 전투는 현재 H5/LH 공간을 유지하고 전투 참여 대상 주변의 상세·활성 셀을 고정한다. 전투 중 생활세계 `WorldTick`은 멈추지만 전투 원장은 100ms `CombatTick`으로 진행한다. 독립 전장은 D-204의 `BattleLocalMeters` 파생 공간을 그대로 사용한다.
- 단일 원장: 두 공간 방식은 배우 체력·부상·공격·방어·카드 수정치·예약·의미 효과·Save/Replay를 공유한다. 현장 전투가 확대되면 남은 체력·기력과 참여자를 새 부대 시작 사본에 봉인하고, 독립 전장 결과는 기존 멱등 세계 효과 규칙으로 H5에 합류한다.
- 조작 방식: 카메라 이름을 권위값으로 사용하지 않고 서버가 한 번에 하나의 `DirectAction` 또는 `TacticalCommand`를 확정한다. 1인칭은 직접 이동·기본 공격·방어/반격·회피·역할 카드 기술을 사용한다. 전술 3인칭은 대상 선택·접근/퇴각·대형 유지와 부대 기본 공격 지휘만 사용하고 개인 회피·반격·역할 기술을 실행하지 않는다. 시점 전환이 끝난 뒤 해당 조작 방식이 서버에 확정되기 전에는 새 전투 입력을 보내지 않는다.
- 카드 분리: `Direct*` 카드 수정치는 1인칭 개인 방어와 역할 기술에만 적용한다. 정찰 범위·대형 응집·농장 방어 준비·추격·보급·전개 같은 전술 수정치는 3인칭 부대 기본 공격, 대형 방어와 명령 간격에 적용한다. 카드 사본·플러스/마이너스·규칙 개정은 같은 전투 시작 사본과 Save/Replay에 봉인하며 Unity가 수치를 다시 계산하지 않는다.
- 이전 결정과 관계: D-136의 서버 권위 단일 판정 원칙은 유지한다. D-136·D-137의 기존 좌·우클릭 고정 전투 입력은 현장 전투에서 이 결정의 시점별 번역으로 대체하며, 시점 전환 자체가 WorldTick이나 전투 결과를 바꾸지 않는 D-134·D-135 경계는 유지한다.
- 증거 경계: 코드·단위시험·EditMode·PlayMode 자동 시험은 실제 수동 조작과 Game View 증거를 대신하지 않는다.

## D-208 카드 서랍은 의미 투영을 통합하되 원장·권위·실행 책임을 통합하지 않는다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 의미 층: `Meta / Context / Action / Knowledge / Research`는 소유·상속·권한 트리가 아니라 세계를 해석하는 의미 층이다. 타로는 가장 넓은 문맥을 제안하지만 행동 허용이나 Effect를 직접 확정하지 않는다.
- 타로 경계: `FrameSet`은 턴·일·계절·지역·사건 프레임을 향후 함께 담을 수 있게 하되 첫 버전은 턴 프레임 하나만 허용한다. `ContextProposalCode`는 사건 평가 입력이며 `IncidentStableId`나 `EffectStableId`가 아니다. `NoIncident`는 정상 결과다.
- 관련성 경계: `Relevant / Recommended / Warned / Contrasted / AvailabilityExplained / BlockExplained`는 Unity 강조와 서버 설명 관계다. 관계 자체는 가용성을 바꾸지 않으며 실제 허용·차단은 각 도메인 규칙이 확정한다.
- 전투 편성: 카메라 시점이 아닌 `DirectAction`/직접 전투와 `TacticalCommand`/전술 지휘를 권위 조작 방식으로 사용한다. 두 조작 방식의 주력·지원 편성은 독립하고, BattleInstance 생성 시 카드 복사본·소스 개정·적용 조작 방식·효과·규칙 개정을 동결한다. 전투 중 원본 편성 변경은 현재 전투에 영향을 주지 않는다.
- Unity 경계: canonical `SimulationWorldShell`의 `C` 카드 서랍은 계열별 조회 결과와 행동 route만 통합한다. 턴 마감·팀 역할·전투·정보·연구 모판의 기존 소유자가 명령을 계속 처리하며 Unity는 타로 수치, 전투 수정치, 행동 허용을 재계산하지 않는다.
- 호환성: 기존 턴 카드 필드와 연구 Scene은 호환 영역으로 보존하되, 새 실행 코드는 문자열 접두사로 직접/전술 효과를 추론하지 않는다.

## D-209 첫 E7 플레이 폐루프는 네이처 탐험·접근 조우·현장 대응·탐험 복귀다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 첫 범위: 새 공식 Scene이나 별도 전투 시스템을 만들지 않고 canonical `SimulationWorldShell`의 기존 네이처 경관·플레이어 이동·1/3인칭 시점과 D-206 현장 전투를 연결한다. 서버가 확정한 활성 네이처 조우가 플레이어에게 접근하면 기존 전투 Preview·Confirm으로 인계하고, 완료 뒤 같은 네이처 경관의 탐험 상태로 복귀한다.
- 권위 경계: Unity의 접근 거리와 형상 이동은 전투 요청 계기와 표현일 뿐 조우 생성·적 수·피해·승패를 확정하지 않는다. 1인칭은 `DirectAction`, 3인칭은 `TacticalCommand`이며 카메라 전환만으로 서버 조작 방식이나 WorldRevision을 바꾸지 않는다.
- 자산 경계: 현재 설치된 Nature 팩에는 몬스터 Prefab이 없으므로 첫 배선은 교체 가능한 대체 위협 형상을 사용하고 UI와 상태판에 이를 표시한다. Apocalypse 또는 다른 몬스터 자산이 설치·검토되기 전에는 대체 형상을 Synty 몬스터, 시각 완성이나 Game View 증거로 표현하지 않는다.
- 증거 경계: 자동 EditMode·PlayMode는 Scene 배선, 접근 인계, 전투 종료 후 공간 고정 해제와 탐험 복귀를 검증한다. 실제 서버 HTTP, 수동 1/3인칭 대응, 최종 몬스터 표현과 Game View는 별도 E7 실행 증거다.
- 관계: D-162의 Nature 상시 체류 세계, D-206의 단일 서버 전투 원장과 `SimulationWorldShell` 단일 실행 Scene 원칙을 첫 가벼운 플레이 순서로 구체화한다.

## D-210 첫 네이처 실제 공간은 기존 생활핵·조우·방어 H3를 완전한 복귀 폐루프로 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 공간 우선순위: 몬스터·전투 규칙을 더 확장하기 전에 기존 `h3-candidate:nature-home-encounter-defense`를 생활핵, 조우로, 방어환의 세 H2로 조립한다. 새 H 재고를 만들지 않고 기존 Nature H1·H2·H3를 우선 재사용한다.
- 의미 폐루프: 기존 `생활핵 → 조우로 → 방어환` 두 관계에 `방어환 → 생활핵`의 `RecoveryHandoff`를 추가한다. 물리 보행은 양방향일 수 있지만 의미 관계는 탐색 출발, 위협 접근, 대응 후 회복 복귀의 인과를 보존한다.
- 표현 계약: Unity 소형 조립안은 생활핵 `(0,0,0)`, 조우로 `(22,0,24)`, 방어환 `(45,0,0)`을 기준으로 세 Node, 세 Edge와 여섯 Connector를 가지며 보이는 자산은 `PolygonNature` 원본 Prefab만 사용한다. 이 좌표와 Prefab은 위치 독립 표현이며 실제 지역·공공데이터·Simulation 결과의 권위가 아니다.
- Scene 안전성: canonical `SimulationWorldShell`이 정상적으로 열리고 기존 루트를 확인하기 전에는 Scene을 재생성하거나 덮어쓰지 않는다. 같은 계약의 생성 Prefab을 먼저 검증하고 Scene 적용·Play Mode·Game View는 별도 증거로 남긴다.
- 상세 기준: [Nature 생활핵·조우·방어 공간 계획](../Architecture/Nature생활핵조우방어공간계획.md)을 따른다.

## D-211 바보는 항상 활성인 타로 여정 루트이고 현재 메이저 아르카나는 그 아래의 가변 문맥이다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 여정 루트: 기존 `learning:hongik.fool.beginner-mind`를 타로 전체의 항상 활성인 `JourneyRoot`로 사용한다. 전통 아르카나 번호 `0`과 플레이 여정 순서 `1`은 별도 필드로 보존하며, 바보 자체를 뽑기 결과나 능력치 수정 카드로 취급하지 않는다.
- 가변 메타: 턴·일·계절·지역·사건에서 선택된 다른 메이저 아르카나는 `ActiveMajorArcana` 프레임으로 바보 루트를 참조한다. 활성 프레임의 주제·제안·관련성은 바뀔 수 있지만 루트의 고유 식별자와 항상 활성 상태는 바뀌지 않는다.
- 권위 경계: 여정 루트와 상위 의미 층은 사건·행동·Effect를 직접 만들거나 허용하지 않는다. `Proposal != Incident != Effect`와 D-208의 의미 층·실행 권위 분리를 그대로 유지한다.
- 저장·호환: `simulation-save.v7`부터 여정 루트와 활성 프레임의 부모·메타 층을 재생 hash에 포함한다. v1~v6 저장은 당시 정규형을 사용해 기존 재생 hash 호환성을 유지한다.
- Unity 표현: 카드 서랍은 바보를 읽기 전용 `여정 루트`로 먼저 표시하고, 선택된 메이저 아르카나를 `현재 아르카나`로 그 아래에 표시한다. Unity는 두 층의 수치 효과나 사건 발생을 다시 계산하지 않는다.

## D-212 E6 현실 자료는 세션 시작 상태 사본으로 동결하고 Unity에는 게임 현상을 우선 표현한다

- 상태: `Accepted`
- 결정일: 2026-08-20
- 내부 판단: 공공자료·현실자료는 수집·출처·시각·단위·공간 정밀도·품질·이용 조건 검사를 끝낸 승인 대장에서만 읽는다. Provider는 Simulation Tick이나 Unity 요청 중 호출하지 않고 실제 E5 세션 시작 시 프로필 고유 식별자를 서버가 해석해 불변 상태 사본으로 동결한다.
- 인과 경계: 현실 문맥은 `ContextProposal`이며 `Incident` 또는 `Effect`가 아니다. 의미 신호만으로 생산량·수익·업무 성공·사건 발생·H5 상대 배치를 바꾸지 않고 각 도메인의 기존 서버 규칙이 실제 허용과 상태 전이를 확정한다.
- 누락 경계: 누락·낡음·품질 부족·미승인 이용 조건은 `Unavailable / Stale`로 남긴다. ASOS·KAMIS 수집 실패를 Scenario 관측으로 대체하지 않으며 E6 GIS 미적용을 LH 절차 시험 공급자로 후퇴시키지 않는다.
- 저장·재생: 현실 문맥을 사용하는 세션은 `simulation-save.v8`에 프로필·동결 시각·출처 계보·의미 신호·입력 hash를 포함한다. 이미 시작한 세션은 Provider 장애나 새 관측 도착으로 바뀌지 않으며 v1~v7 hash 정규형은 보존한다.
- Unity 표현: 기본 조회는 공공자료 이름과 원수치 대신 젖은 작업 환경·찬 기운·작물 상태 확인·시장 흐름 같은 게임 현상을 제공한다. 사용자가 정보 상세를 열 때만 출처·기준 시각·공간 정밀도·한계를 `Knowledge + ProjectionReadOnly` 카드로 제공하며 원자료 hash·API key·필지 식별자는 노출하지 않는다.
- 상세 기준: [E6 현실 자료 내부 판단과 간접 표현](../Architecture/E6현실자료내부판단과간접표현.md)을 따른다.

## D-213 AreaSet 구성 패턴은 H2·H3 재고를 역할 슬롯으로 조립하는 위치 독립 제작 계약이다

- 상태: `Accepted`
- 결정일: 2026-08-21
- 계층 경계: AreaSet 구성 패턴은 H4.5 같은 새 공간 계층이 아니다. H4 지역 의도를 승인 H3·H2의 역할 슬롯, 상대 배치, 연결 지점과 플레이 폐루프로 번역해 Actual E5를 만드는 위치 독립 조립 설명서다.
- 권위 흐름: 사람이 읽는 팩별 Markdown은 의도와 근거를 설명하고, 버전 JSON은 결정적 조립 계약이며, Actual E5는 기준안의 실제 Graph 포함·관계 계보를 가진다. Unity 공용 검토실은 구조와 Synty 표현을 투영할 뿐 선택·업무·Simulation 상태의 권위가 아니다.
- 재사용 경계: 구성 패턴은 H3/H2 고유 식별자를 직접 나열하는 데 그치지 않고 생활핵·생산·후처리·시장·입출고 같은 역할 슬롯에 배치한다. H2 교체는 소속 H3의 역할과 연결 계약을 만족할 때만 허용한다.
- 기준·변형 경계: Nature·Farm·Town·City/Hub는 각각 기준안과 불변 변형안을 가질 수 있다. 변형안 자체는 Runtime 상태가 아니며 실제 Session에서 어떤 안을 사용할지는 서버 규칙이 결정한다.
- 현실 자료 경계: 패턴은 실제 도로·건물·블록 경계·DEM·공공데이터를 필수 입력으로 요구하지 않는다. 현실 근거는 E6에서 선택 결속하며, 근거가 없을 때 Scenario로 묵시적으로 대체하지 않는다.
- 상세 기준: [AreaSet 구성 패턴](../Architecture/AreaSet구성패턴.md)을 따른다.

## D-214 Farm·Hub·City 독립 우선, 경로 연결 후속

- 상태: `Accepted`
- 결정일: 2026-08-21
- 원칙: Farm, Hub, City의 내부 플레이와 증거를 각각 먼저 완성한다.
- 연결: 영역 간 경로는 기본 수직 slice가 아니라 독립 준비 뒤의 통합 slice다.
- Unity: 세 영역은 `SimulationWorldShell`의 독립 모듈로 두고 Hub와 City를 구분한다.
- 상세 기준: [Farm·Hub·City 독립 영역 우선 개발 정책](../Architecture/FarmHubCity독립영역우선개발정책.md)을 따른다.

## D-215 E는 증거 성숙도이고 G는 다음 E로 올리는 관리 체계다

- 상태: `Accepted`
- 결정일: 2026-08-22
- 축 분리: `E0~E9`는 세계가 실제로 확보한 증거 성숙도, `G1~G4`는 다음 E 증거를 만들기 위한 관리 체계, `H1~H5`는 공간 포함 깊이로 사용한다. G 작업 완료를 E 승격으로 간주하지 않는다.
- 관리 구간: G1은 E1→E6 세계 성립, G2는 E6→E7 플레이어 경험, G3는 E7→E8 NPC 생활세계, G4는 E8→E9 변화·확장 적응을 주로 관리한다. 구간은 책임 routing이며 단계 간 결함 수정 왕복을 금지하지 않는다.
- E9 정의: E9는 기능 수가 아니라 선정된 E8 세계에 실제 변화를 적용하고 `E8→E1` 하향 영향 분석, Revision·Migration·Save/API 호환성·결정성 검증, `E1→E8` 상향 폐루프 재검증을 거쳐 새 안정 revision을 만든 증거다.
- 이관: WI–H 공간 생산 순서에 사용하던 과거 `G1~G4` 사람 표시를 `H관문-1~4`로 바꾼다. 제품·공급자별 단계도 소유 영역을 포함한 이름을 사용한다. 공개 계약·저장 값의 안정 식별자는 소비자 호환성 확인 없이 일괄 변경하지 않는다.
- 현재 경계: 기존 세계 상호작용·실행 원장의 기본 통합 목표 E7은 유지한다. 이 결정과 대장 확장 자체는 E8·E9 증거가 아니며 선정된 NPC 생활세계 또는 실제 변화 단위별 완료 관문이 필요하다.
- 상세 기준: [E 성숙도와 G 관리 체계](../Architecture/E성숙도와G관리체계.md), [E9 변화 적응형 세계 정의](../Architecture/E9-변화적응형세계정의.md)와 `eng/execution-ledgers/evidence-management-systems.json`을 따른다.

## D-216 권위 지도 묶음은 플레이 목적부터 실행 증거까지 잇는 navigation 기준이다

- 상태: `Accepted`
- 결정일: 2026-08-22
- 진입점: 새 아이디어와 Codex 작업은 [게임 기획 최상위 권위 트리](authority-maps/00_GAME_DESIGN_TREE.md)에서 플레이 목적을 찾고 [WI 전체 게임플레이 그래프](authority-maps/04_WI_GAMEPLAY_GRAPH.md)를 거쳐 H 공간, E 증거, G 관리 체계, 실제 Simulation·Unity 소유 위치로 내려간다.
- 비중복 경계: `docs/AI/authority-maps/00~09`는 사람이 읽는 현재 navigation snapshot이다. 기계 JSON 대장, 계약·코드, `CURRENT_WORK.md`, 이 결정 문서와 실제 실행 기록의 권위를 복제하거나 대체하지 않는다. `08_CURRENT_WORK.md`와 `09_DECISIONS.md`는 중앙 문서로 가는 고정 진입점만 제공한다.
- 우선순위: 첫 추적 기준은 Nature↔Farm 왕복이다. Farm·Hub·Town·City의 WI가 한 그래프에 연결돼 있어도 영역 간 사슬을 기본 개발 순서나 완료 의존 관계로 해석하지 않는다.
- 갱신 규칙: WI·H·E/G·권위 소유자 또는 완료 판정이 바뀌는 의미 있는 작업은 원천 대장·코드와 중앙 snapshot을 먼저 갱신하고, 같은 변경에서 영향받는 권위 지도를 함께 갱신한다.

## D-217 배치 통제 계층은 H 공간 의미와 분리하고 Player 실측 크기를 기준으로 한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 계층 경계: H1~H5는 작업공간·블록·경관·AreaSet·세계 배치의 의미 포함 계층으로 유지한다. 배치 통제 계층은 H4.5나 새 H 단계가 아니라 `세계 외곽 → AreaSet 외곽 → H3 경관 → H2 블록 → H1 발자국`의 허용 범위와 소속을 검사하는 별도 제작·검증 계층이다.
- 크기 기준: Unity 배치 객체의 높이와 수평 발자국은 고정 임의 수치만 사용하지 않고 같은 Scene의 Player 시각 형상 실측 높이에 대한 등급별 비율로 제한한다. 건물·울타리·능선·일반 표현은 서로 다른 비율 범위를 가지며 범위를 벗어난 표현은 정규화하거나 배치를 거부한다.
- 자산 계보: Synty 원본 Transform의 부모 구조는 출처와 교체 가능성을 위해 보존한다. 배치 통제 계층은 H1마다 결정된 H3·H2 소속, 적용 위치, 실측 발자국과 Player 상대 비율을 참조 기록으로 남긴다.
- 모듈 조립: 패턴은 `H3 경관 축 → H2 전면·이격·출입구 규칙 → H1 구성 모듈 조립` 순서로 결정한다. H1 Prefab을 가까운 상위 노드 이름에 사후 귀속시키지 않고, 의도한 H3·H2 역할을 먼저 선택한 뒤 도로·울타리·건물·상호작용 소켓을 배치하며 원본 구성 키를 보존한다.
- 증거 경계: 평탄 H3 바닥의 비율·이격·연속성 통과는 실제 DEM 경사, 도로·수계, NavMesh 통행과 H1 행동 연결 증거가 아니다. 실제 E5/E6 지형에서는 같은 계약에 `slope`와 `placement-mask` 표본을 공급해야 한다.

## D-218 게임 개발은 현재 목표에서 증거와 다음 판단까지 같은 업무 순서를 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 업무 순서: 현재 목표와 차단점 확인 → 시작·선택·결과·복귀가 있는 플레이 단위 선택 → WI와 상태 권위 확정 → Simulation 세로 구현 → H 공간 결속 → canonical `SimulationWorldShell` 조립 → 증거 구분 → 현재 상태 갱신과 다음 판단 순서를 사용한다.
- 선택 우선순위: 현재 목표의 직접 차단점, 현재 폐루프의 서버·저장·Unity 증거, 회귀·배선 실패, 완성도가 낮은 독립 영역의 내부 폐루프, 준비된 영역 간 통합, E8 NPC 생활세계, E9 변화 적응 순으로 판단한다. Farm→Hub→Town 같은 영역 간 경로를 자동 다음 단계로 삼지 않는다.
- 작업 단위: 파일·기능·Scene보다 플레이어의 시작 상태, 선택, 권위 있는 결과와 복귀가 드러나는 작은 흐름을 사용한다. 선택 이유, WI·H, 상태 소유자, 목표 E/G, 포함·제외, 검증과 다음 판단을 기록한다.
- 증거 경계: 문서 작성, 코드 존재, 자동 시험, Actual E5, 실제 서버, Play Mode·Game View와 운영 효과는 서로 다른 증거다. 업무 상태나 템플릿 작성 자체는 E 성숙도 또는 구현 완료가 아니다.
- 기준 문서: [게임 개발 업무 순서 기준](../Architecture/게임개발업무순서기준.md)과 [게임 개발 작업 단위 템플릿](../ProjectOverview/templates/게임개발작업단위템플릿.md)을 따른다.

## D-219 Nature는 생존 생활거점을 1차 플레이로 두고 심리 회복을 그 결과에 결합한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 플레이 우선순위: Nature의 첫 플레이는 안전 빈터 등장 → 도끼 획득 → 수확 구역 벌목 → 통나무 휴대 → 단계형 오두막 건설 → 황혼 위협 → 전투 또는 후퇴 → 회복·재출발이다. 심리 회복은 유지하지만 이 흐름의 1차 행동을 대체하지 않는다.
- 시간 권위: `nature-survival.realtime.r1`은 실제 20분을 한 주기로 사용한다. Solo는 메뉴·응용프로그램 비활성 중 멈추고 Hosted는 계속 간다. 종료 중 경과 시간은 따라잡지 않으며 새 프로필만 주기 경계에서 기존 WorldTick을 진행한다.
- 공간 재사용: 새 H 재고나 공식 Scene을 만들지 않고 기존 `h3-candidate:nature-home-encounter-defense`, 생활핵·조우로 H2, trailhead·shelter·exploration-buffer H1을 재사용한다.
- 권위·저장: 서버 행동은 Preview·Confirm과 개정을 검증하고 도끼·통나무는 기존 플레이어 소지품 원장을 사용한다. 새 상태와 명령은 `simulation-save.v13` hash와 재생에 포함한다. Solo Unity는 같은 공유 규칙의 로컬 권위를 사용하고 Hosted는 서버 권위를 사용한다.
- 호환 관계: D-048의 고정 15:00 표현은 새 프로필에서 대체하고 프로필이 없는 기존 Scene·Session에는 유지한다. D-177의 심리 영역 정의와 D-209의 가벼운 조우 흐름은 폐기하지 않고, Nature 1차 플레이를 생존·생활 거점으로 확장한다. Farm 독립 업무와 Nature↔Farm 통합은 별도 slice로 남긴다.
- 대체 형상: 현재 Skeleton은 명시적인 Synty Generic 대체 위협이다. 실제 몬스터 자산·최종 시각 마감·Game View 증거로 표현하지 않는다.
- 상세 기준: [Nature 생존 생활거점 세로 조각](../Architecture/Nature생존생활거점세로조각.md)을 따른다.

## D-220 WorldTick·권위 실시간·표현 실시간·BattleTick을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 시간 경계: Unity 프레임 시간은 이동·카메라·애니메이션 표현, 권위 실시간 시계는 정규화된 경과 초와 짧은 작업, WorldTick은 NPC·Task·생산·물류·사건의 큰 시간 결과, BattleTick은 전투 내부의 빠른 결정적 진행을 담당한다.
- 개정 경계: `WorldRevision`은 WorldTick과 같은 값이 아니다. Confirm, 권위 실시간 시계와 Tick 등 권위 상태가 바뀔 때 증가하며 WorldTick은 시간 의존 세계 규칙을 진행하는 큰 경계다.
- 현재 Profile: 기본 경영 Session은 명시적 Tick과 `OneTickOneDay`를 사용한다. `nature-survival.realtime.r1`은 1,200초 주기 중 작업·위상을 권위 상태로 누적하고 주기 경계에서만 WorldTick을 진행한다. 전투는 100ms BattleTick 결과를 이후 WorldTick에 합류시킨다.
- Unity 경계: Presentation의 `Update()`·`deltaTime`은 서버 Tick·개정·재고·Effect를 직접 확정하지 않는다. 로컬 권위가 필요하면 Nature `SoloLocal`처럼 공유 결정 규칙, 권위 모드, 개정, Save/Replay 경계를 명시한다.
- 상세 기준: [WorldTick과 실시간 실행 경계](../Architecture/WorldTick과실시간실행경계.md)를 따른다.

## D-221 Simulation Core는 게임 세계이고 Server는 Hosted Host다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 실행 경계: 게임 규칙·Session Aggregate·Preview/Confirm·Task/Effect·WorldTick/Revision·Save/Replay는 공통 `Simulation Core`가 소유한다. `Simulation.Server`는 Hosted Multiplayer에서 같은 Core를 실행하는 Host이며 독립 게임 엔진이 아니다.
- Solo 권위: Solo의 기본 권위 위치는 Unity 프로세스 안의 `LocalSimulationRuntime`이다. Unity `GameObject`, `Update()`와 표현 상태는 권위가 아니며 모든 상태 변경은 공통 Application·Aggregate와 상태 사본 경계를 통과한다.
- 축 분리: `AuthorityLocation = LocalProcess | RemoteHost | ReviewFixture`는 게임 Simulation 실행 위치다. 운영 실행 모드, Session 참가 방식과 기존 호환 enum을 한 축으로 합치지 않는다. 운영 계약·결제·입고 원장의 서버 최종 권위는 유지한다.
- 저장·호환: Solo는 로컬 파일 Adapter를 사용하되 기존 Save Package·Command log·Replay hash·WorldTick·WorldRevision 의미를 보존한다. Hosted는 같은 계약을 서버 저장소에 연결한다.
- 이관 순서: Nature를 첫 공통 Local Runtime 검증 대상으로 삼고 Farm·건설·물류 등은 기능별로 이관한다. Nature 단독 성공을 전체 `SimulationWorldShell`의 서버 의존 제거 완료로 간주하지 않는다.
- 상세 기준: [Solo 우선 Simulation Runtime](../Architecture/SoloFirstSimulationRuntime.md)을 따른다.

## D-222 의미 있는 게임 작업은 E9 목표부터 하향 분해하고 E1부터 상향 검증한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 시작 원칙: 새 게임 기능·규칙·공간·저장 변경은 최종 안정 세계와 호환·rollback을 E9 목표 봉투로 먼저 선언하고 `E9 → E1` 순서로 NPC·플레이어·현실 문맥·AreaSet·WI 공간·시험·코드·계약을 모두 분해한다. 자료가 없으면 층을 지우지 않고 `Gap` 또는 `Blocked`로 남긴다.
- 구현 원칙: 하향 작업 명세가 E1까지 닫힌 뒤 가장 낮은 미완료 의존성부터 구현한다. 구현 후에는 `E1 → E9` 순서로 실제 증거를 재검증하며 하위 관문을 통과하지 않은 상위 코드·화면·문서를 완료 증거로 사용하지 않는다.
- 증거 경계: `targetEvidenceStage = E9`는 장기 완결 방향이지 현재 E9 판정이 아니다. 신규 세로 조각은 E8까지 완성해도 자동 E9가 아니며, 변경 전 E8 안정 revision에 실제 후속 변화·Migration·호환·회귀와 새 안정 revision을 증명해야 `promotionEligible = true`가 될 수 있다.
- 실행·공간 경계: Solo `LocalProcess`와 Hosted `RemoteHost`는 같은 `SharedSimulationCore`를 사용한다. H1~H5 의미 계층과 Player 비율·경사·이격·연속성·출입구를 검사하는 배치 통제 계층은 분리하며 공식 Unity 진입점은 `SimulationWorldShell` 하나로 유지한다.
- 적용: 현재 첫 기계 작업 명세는 Nature 생존·Solo 공통 Core·배치 통제 세로 조각이다. 현재 E3까지만 증거가 있고 E4 WI–H1 모판, E5 Actual 배치·통행, E6 질문 행렬, E7 실제 Solo·Hosted 플레이, E8 NPC와 E9 변화 기준선은 명시적 차단으로 남긴다.
- 상세 기준: [E9↔E1 반복 왕복 구현 체계](../Architecture/E9하향식수직구현체계.md)와 `eng/execution-ledgers/e9-vertical-implementation-protocol.json`을 따른다.

## D-223 E6는 E5 세계를 E7 전에 정제하는 관문이다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 명칭: E6의 사람 중심 이름을 `AreaSet 플레이 전 정제·필요 근거 결속`으로 사용한다. E5까지 조립·성장한 세계를 E7 실제 플레이에 올리기 전에 다듬는 역할을 이름에 명시한다.
- 정제 범위: `E6 정제 질문표`로 장소 의미, H3 사이 인과, WI 시작·완료·복귀, 배치·통행, 권위 경계, 자료 최신성과 한계를 검토한다. 결함은 E5나 더 낮은 소유 단계로 되돌려 수정하고 다시 E6를 통과한다.
- 현실자료 경계: 현실자료와 GIS는 계속 선택형이다. `NotApplied`는 허용할 수 있지만 정제 질문 자체를 생략하는 근거가 아니며, 적용한 자료는 출처·판본·hash·한계를 보존한다.
- 호환: D-207의 E6 의미를 폐기하지 않고 명칭과 설명을 구체화한다. 저장·API의 `ImmersionQualified`, `SpatialMaturity`, `ImmersionMaturity`, `FreshnessState`, `GroundingStatus`와 기존 질문 StableId는 호환을 위해 유지한다.
- 상세 기준: [H5 세계 배치와 E6 플레이 전 정제·선택형 GIS 결속](../Architecture/H5세계배치와선택형E6결속.md), [E 성숙도와 G 관리 체계](../Architecture/E성숙도와G관리체계.md)를 따른다.

## D-224 기존 통합 이력은 보존하고 새 작업은 운영·Simulation·Unity 책임으로 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 기준선: `codex/rename-ssalddel`의 기존 운영·Simulation·문서 혼합 커밋은 과거 통합 이력으로 보존한다. 대규모 history rewrite로 재분할하지 않으며 이 브랜치에 새 일반 목적 작업을 계속 누적하지 않는다.
- 책임 흐름: 실제 사용자·권한·동의·업무 원장은 `Operations`, 게임 Session·규칙·시간·Save/Replay는 `Simulation`, 입력·공간·UI와 권위 상태 사본 표현은 `Unity`가 맡는다. `Integration`은 공개 계약·Adapter·직렬화·호환 회귀만 다루며 독립 상태 권위를 갖지 않는다.
- 브랜치: `cheolwo/ssalddel`은 `operations/*`, `simulation/*`, `integration/*`, 별도 `cheolwo/unity` 저장소는 `unity/*`의 짧은 브랜치를 사용한다. Git은 커밋을 전송하므로 서로 다른 책임 변경은 push 전에 커밋으로 분리한다.
- 적용: 제품 기능 작업 영역과 시스템 책임 흐름은 별도 축이다. 각 작업은 제품 기능 영역과 주 책임을 함께 선택하고, 공유 계약 변경은 생산자와 모든 소비자 검증을 별도로 남긴다.
- 상세 기준: [운영·Simulation·Unity 작업 흐름 분리](../Architecture/OperationsSimulationUnity작업흐름분리.md), `eng/work-areas/responsibility-workstreams.json`과 연결된 세부 manifest를 따른다.

## D-225 게임 작업은 플레이어 선택 폐루프를 E·WI·H 전 단계의 공통 관점으로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 작업 출발점: 기능·Scene·자산보다 플레이어가 상황과 욕구를 인지하고 선택의 비용·시간·위험을 비교하며, 재료를 획득·운반·배정·조립하고 결과를 체감해 귀환 또는 다음 선택으로 이어지는 폐루프를 먼저 정의한다.
- E 관계: 플레이어 관점은 G2·E7에서 처음 시작되지 않는다. E1의 플레이어 약속부터 E9의 변화 후 선택 보존까지 각 단계에 검토 질문을 두되, 기존 E 완료 관문이나 증거 수준을 바꾸거나 앞당기지 않는다.
- H 관계: 플레이어는 H1을 직접 배치·복구·연결하고 H2·H3 목표를 유도할 수 있다. H2는 H1 집합·연결구·용량, H3는 H2 사이 이동·인과·반환 폐루프로 Simulation이 성장 판정하며 H4·H5 계약은 현재 단계에서 플레이어가 임의 생성하지 않는다.
- 재료·표현 경계: 세계 자원·Lot, 조립법·비용, 배치 Preview, H1 공간 인스턴스, H2/H3 성장 판정과 Synty 시각 자산을 별도 상태로 유지한다. Prefab 배치는 자원 소비나 H 성립 증거가 아니다.
- 권위 경계: 플레이어 중심은 개발 관점이며 권위 이전이 아니다. Solo `LocalSimulationRuntime`과 Hosted Simulation 서버가 같은 Core에서 선택 조건·비용·결과·Save/Replay를 판정하고 Unity는 입력·공간 표현·피드백을 담당한다.
- 상세 기준: [플레이어 중심 게임 개발 업무 구조](../Architecture/플레이어중심게임개발업무구조.md)와 `eng/execution-ledgers/player-centered-development-flow.json`을 따른다.

## D-226 지역 사건·Nature 위협·전투 결과·지역 발전을 독립 모듈로 잇는다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 책임 분리: 업무 영역 사건은 원인·기한·필요 WI, 지역 인과는 위협·회복 계보, Nature 압력 정책은 순수 계산, Nature 투영은 경고·조우, 전투 결과는 즉시 안전 인계, 지역 발전은 기회·영역 준비도, Farm 프로젝트는 플레이어의 H1 배치·자원·Task를 각각 소유한다.
- 전이 경계: Nature 전투 승리는 업무 사건 원인을 해결하지 않는다. 같은 원인 사건의 발전 기회는 한 번만 발급하며 기회는 건설 비용이 아니라 시작 자격이다. Confirm에서 예약하고 취소 시 반환하며 H1 `Operational`에서 소비한다.
- 공간 경계: 첫 조각은 기존 `h2-candidate:farm-incident-containment`와 세 H1을 사용한다. 세 H1 완료가 H2 독립 준비와 Nature↔Farm 연결 후보를 열 수 있지만 실제 영역 간 시설·운송·다른 영역 발전을 자동 생성하지 않는다.
- 구현 순서: 기존 동작을 보존하는 파일·책임 분리와 회귀 시험을 먼저 끝내고, 발전 계약 빈 상태 → 저장 판본 → 승리 인계 → Farm 프로젝트 → WI 효과 → Local·Remote → Unity 순으로 구현한다. 파일 분리나 문서 작성만으로 E 단계가 승격되지는 않는다.
- 상세 기준: [지역 사건·Nature 위협·지역 발전 모듈 경계](../Architecture/지역사건-Nature위협-지역발전모듈경계.md)와 `eng/execution-ledgers/work-orders/nature-farm-regional-development.e9-work-order.json`을 따른다.

## D-227 최근 개발 철학의 반복 경계를 프로젝트 불변 골격으로 고정한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 불변 범위: Operations·Simulation·Unity 권위, Solo·Hosted 공통 Core, Preview·Confirm·Revision 상태 변경, 시간 축, E·G·H 분리, 플레이어 H1 조립과 H2/H3 성장 판정, 단일 `SimulationWorldShell`, 공개 계약·저장 호환을 리팩토링과 새 기능이 보존할 골격으로 사용한다.
- 변경 경계: 파일·내부 이름·구체 Adapter·표현은 바꿀 수 있다. 불변 골격을 바꾸려면 새 확정 결정, 호환·Migration 계획과 생산자·소비자 회귀 근거가 먼저 필요하며 문서나 관리 작업만으로 E 단계를 승격하지 않는다.
- 호환 적용: 기존 `CityHub`, 지역 사건 코드 묶음, Runtime의 `ReviewFixture`, 넓은 Gameplay·WI 인터페이스는 즉시 삭제·이름 변경하지 않는다. 새 내부 책임과 좁은 인터페이스를 추가하고 기존 표면은 호환 facade로 유지한다.
- 대체 관계: D-221의 권위 위치 목록에 `ReviewFixture`를 함께 적은 표현만 대체한다. 실제 권위 위치는 `LocalProcess | RemoteHost`, 검토 Fixture는 `SimulationRuntimePurpose`의 실행 목적이며 D-221의 공통 Core·저장·이관 원칙은 유지한다.
- 상세 기준: [프로젝트 불변 개발 골격](../Architecture/프로젝트불변개발골격.md)과 `eng/execution-ledgers/project-invariant-baseline.json`을 따른다.

## D-228 게임 개발 기준 문서는 질문별 단일 책임을 갖고 대체 경로는 호환 안내로 남긴다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 단일 책임: 프로젝트 불변선, 플레이어 관점, 업무 순서, E/G 정의, E9 하향·상향 절차, H 형성 계보, Operations·Simulation·Unity routing을 각각 하나의 기준 문서가 소유한다. 다른 문서는 같은 표·절차를 다시 쓰지 않고 해당 기준을 링크한다.
- 첫 통합: `E9리팩토링모듈골격.md`의 모듈 이름·상태·선택 규칙은 `E9하향식수직구현체계.md`로 통합한다. WI·H의 현재 결속은 기계 대장과 `CURRENT_WORK.md`가 소유하며 장기 절차 문서에 상태 사본을 중복하지 않는다.
- 호환 경계: 대체된 문서 경로는 바로 삭제하지 않고 짧은 호환 안내로 남긴다. 변경 기록과 화면 증빙은 현재 기준과 내용이 겹쳐도 과거 사실이므로 통합·삭제 대상에서 제외한다.
- 진입점: [문서 안내](../README.md)의 `게임 개발 기준 문서의 책임` 표를 현재 문서 routing 기준으로 사용한다.

## D-229 E9와 E1 사이는 한 번 통과하는 파이프라인이 아니라 안정될 때까지 반복 왕복한다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 반복 구조: E9→E1은 현재 목표·영향·누락을 보는 하향 검토이고 E1→E9는 가장 낮은 누락부터 실제로 조립·검증하는 상향 통과다. 상향 중 새 영향, 계약 변경, 잘못된 가정이나 증거 누락이 발견되면 같은 작업 명세에서 관련 하향 판정을 다시 연다.
- 상태 사본: 작업 명세는 계획본과 검증본을 주기마다 새로 만들지 않고 현재 왕복 주기의 상태 사본을 유지한다. 이미 통과한 단계도 새 영향이 있으면 `ReusedWithVerification`, `Affected`, `Gap` 또는 `Blocked`로 다시 판정할 수 있다.
- 종료 조건: 하향 1회와 상향 1회를 마쳤다는 이유로 작업을 닫지 않는다. 선택 범위가 실제 증거로 안정됐거나 남은 차단이 명시됐을 때 현재 주기를 닫으며, E9 승격에는 기존 E8 기준선·실제 변화·Migration·호환·회귀 조건이 계속 필요하다.
- 대체 관계: D-222의 E9 목표 우선, 최저 미완료 의존성 구현, 상향 증거 원칙은 유지한다. 다만 이를 `E9→E1→E9` 단일 통과로 읽는 표현은 이 결정이 대체한다.
- 상세 기준: [E9↔E1 반복 왕복 구현 체계](../Architecture/E9하향식수직구현체계.md)와 `simulation-e9-vertical-implementation-protocol.r3`를 따른다.

## D-230 게임 코드에는 E 증거 상태가 아니라 E 검토 책임을 메타데이터로 남긴다

- 상태: `Accepted`
- 결정일: 2026-08-23
- 축 분리: `SsalddelEvidenceResponsibilityAttribute`는 구성 요소가 E1~E9 중 어느 질문에 답할 책임이 있는지 표시한다. 현재 증거 통과·승격·`promotionEligible`을 기록하지 않으며 기능 호출 흐름을 나타내는 `SsalddelCodeMetadataAttribute`와 분리한다.
- 대표 책임: 검사 후보 타입과 선택된 공개 WI 메서드는 대표 `Primary` E를 정확히 하나 가진다. 실제 교차 책임만 `Secondary`로 추가하며 대표 책임에는 완료 오인을 막는 `Boundary`를 필수로 둔다.
- 전수 경계: Simulation, Simulation Server, 공유 Unity adapter, 실제 Unity 제품과 검증 코드를 강제 범위로 삼는다. 일반 Operations는 제외한다. 호환 facade·기술 보조·생성·외부·Sample·Experiment는 사유가 있는 제외 Attribute를 사용한다.
- 자동 관문: 공통 Reader·validator와 .NET·Unity 생성 지도를 둔다. 이관 뒤 새 공개 후보의 무사유 누락, 다중 Primary, 책임과 제외의 동시 사용, 잘못된 WI 고유 식별자는 strict 검사 오류다.
- 증거 경계: 지도 생성과 strict 통과는 코드 분류·컴파일 증거일 뿐 저장 Scene, 실제 서버, Play Mode·Game View, Save/Replay 폐루프 또는 E 승격을 대신하지 않는다.
- 상세 기준: [코드 탐색 메타데이터의 E 성숙도 책임](../Architecture/SsalddelCodeMetadata.md#e-성숙도-책임-메타데이터)을 따른다.

## D-231 플레이어가 확정하는 Nature 생존 행동은 정식 WI로 관리한다

- 상태: `Accepted`
- 결정일: 2026-08-24
- 판정 기준: 플레이어가 대상을 선택하고 Preview 뒤 Confirm하여 권위 세계 상태를 바꾸는 행동은 WI다. 시간 경과, 입력 유지 중 작업 진행, 자동 조우 발생과 완료 결과는 각각 Realtime·Task·자동 상태 전이·Effect로 남기며 독립 WI로 늘리지 않는다.
- Nature 적용: 기존 `WI-NATURE-01~04`를 보존하고 도끼 획득, 벌목 시작, 오두막 위치 배치, 건설 시작, 입장, 퇴장, 황혼 조우 대응을 `WI-NATURE-05~11`로 등록한다. 같은 `ISimulationNatureSurvivalRuntime.PreviewAsync/ConfirmAsync`를 공유해도 안정 WI 식별자와 영향·증거는 각각 추적한다.
- H 결속: 생활핵과 탐사·조우의 승인 H1 모판을 분리하고 기존 H2 생활핵·조우로 및 H3 생활·조우·방어 폐루프에 연결한다. H1 모판 결속은 E4 증거이며 실제 H2·H3 배치나 E7 플레이를 대신하지 않는다.
- 미완료 경계: 벌목·건설의 공간 예약 충돌과 취소·자재 반환, 오두막 점유 용량, Actual E5 배치와 Play Mode·Game View 증거는 남아 있다. 따라서 신규 7개 WI는 Actual E5 대장에서 `E5 배치 대기`이며 Network 전체 상태는 `Partial`이다.
- 대체 관계: Nature 생존 행동을 41개 대장 밖 프로필 행동으로만 둔 D-219의 해당 범위와 [Nature 생존 생활거점 세로 조각](../Architecture/Nature생존생활거점세로조각.md)의 이전 설명을 대체한다. D-219의 규칙 값·권위·시간·저장 경계는 유지한다.

## D-232 E 성숙도의 주어는 WI이며 공간은 조건부 발현 증거다

- 상태: `Accepted`
- 결정일: 2026-08-24
- 중심 축: E1~E9는 WI 또는 서로 이어지는 WI 폐루프가 얼마나 성숙하게 계약·구현·검증·발현·플레이·생활·변화하는지를 추적한다. H1~H5와 AreaSet·Graph는 공간 의미와 포함 깊이 및 공간 조립 증거를 소유하지만 E 단계 자체를 소유하지 않는다.
- E4: `WI 실행 문맥 결속`으로 정의한다. 허용 발생원, 발의·수행 주체, 대상, 자료·자원·예약, 시간 규칙과 공간 적용 여부를 결속한다. 공간 WI만 H 능력·용량·연결구를 요구하며 비공간 WI는 `NotApplicable`을 명시한다.
- E5: `WI 세계 발현`으로 정의한다. 결정적 Simulation 세계에서 허용 발생원 중 하나가 WI를 만들고 권위 상태 전이, Task·Effect, 결과와 후속·복귀 WI까지 이어져야 한다. AreaSet·Graph·통행은 공간 WI의 추가 증거지만 공간 조립만으로 E5를 선언하지 않는다.
- 발생원: 정의에는 `DataDriven | PlayerDriven | NpcDriven | WorldDerived` 허용 목록을 두고 실행 인스턴스에는 하나만 기록한다. 자료 수집 자체는 WI가 아니며 조건 판정으로 의미 있는 권위 상태 전이가 생길 때 WI가 된다. 기존 발의·수행·진행·권위 `subjectBinding`은 발생원과 분리해 유지한다.
- 호환: 기존 `ActualE5` 공간 계약과 저장·API 값은 이관 기간 동안 공간 조립 증거 facade로 보존한다. 신규 코드와 판정은 `SpatialAssembly` 의미를 사용하며 기존 공간 대장만으로 E5 승격을 만들지 않는다.
- 대체 관계: D-152의 E4 공간 모판·E5 지역 경관 조립을 E 단계의 완료 관문으로 둔 부분과 D-231의 H1 모판 결속을 곧바로 E4 증거로 표현한 부분을 대체한다. 기존 H 설계·ActualE5 공간 산출물과 검증 사실은 삭제하거나 강등하지 않고 조건부 공간 증거로 재분류한다.

## D-233 진행 작업 취소는 예약을 반환하는 독립 WI이며 원래 공간 문맥을 이어받는다

- 상태: `Accepted`
- 결정일: 2026-08-24
- 판정 기준: 입력을 놓아 작업 진행을 멈추는 것은 Realtime 일시 정지다. 진행 작업을 닫고 Actor·Tool·ResourceNode·WorkArea·BuildingSite 점유를 해제하거나 예약 Material을 반환하는 명시 선택은 권위 상태 전이이므로 `WI-NATURE-12`로 관리한다.
- Core 계약: 벌목 취소는 나무와 통나무 상태를 바꾸지 않고 진행 작업만 닫는다. 오두막 건설 취소는 예약 통나무를 인벤토리에 전량 반환하고 건설 진행률과 예약량을 0으로 만든다. 완료된 건설은 예약량을 남기지 않는다.
- 공간 결속: 취소 전용 H1을 새로 만들지 않는다. WI-NATURE-12는 취소 대상 벌목 또는 건설이 이미 결속한 H·Graph와 예약 문맥을 이어받는 Contextual 결속이며, 실행 발현에는 원래 WI와 취소 WI를 함께 기록한다.
- 증거 경계: `simulation-save.v15` Replay hash와 canonical `SimulationWorldShell` PlayMode에서 C 취소·E 퇴장·R 후퇴·저장·Scene 재진입을 검증했다. 이는 Solo PlayMode 증거이며 Hosted 동등성, 수동 Game View·Console, 전체 Nature E7 또는 E8을 대신하지 않는다.
- 대체 관계: D-231의 취소·자재 반환과 Actual E5 배치를 미완료로 둔 부분을 대체한다. D-231의 WI 판정 기준과 WI-NATURE-05~11 식별자는 유지한다.

## D-234 PlayableLoop와 EvidencePackage를 WI·H·E 사이의 공식 연결 객체로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-24
- 판정 단위: 개별 WI와 여러 WI가 성공·실패·회복·귀환까지 닫히는 PlayableLoop의 E를 분리한다. Farm·Hub·Town·City는 독립 PlayableLoop를 먼저 가지며 영역 간 통합은 별도 폐루프로 등록한다.
- 증거 단위: Contract·자동 시험·Save/Replay·HTTP·Unity EditMode·PlayMode·Game View·Hosted·Operations를 EvidencePackage로 분리하고 대상 revision, 환경, 범위, 제외, 산출물 hash·보존 방식과 무효화 조건을 기록한다. 가장 높은 단일 산출물은 폐루프 E를 자동 승격하지 않는다.
- 협업 경계: E9↔E1 작업 명세를 v2로 올리고 대상 PlayableLoop·EvidencePackage, 주·참여 작업 흐름, 담당·검토, 수정 허용 경로, 의존 작업, 입출력 인계, 브랜치와 커밋 범위를 필수화한다.
- 상태 사본: `07_CURRENT_COMPLETION_LEDGER.md`는 `playable-loops.json`과 `evidence-packages.json`에서 생성하며 현재 폐루프 E와 차단을 사람이 중복 소유하지 않는다. `CURRENT_WORK.md`는 상세 작업 snapshot과 검증 로그를 설명한다.
- 대체 관계: D-229의 반복 왕복 원칙은 유지한다. 작업 명세 형식과 protocol revision 참조는 `simulation-e9-vertical-work-order.v2`와 `simulation-e9-vertical-implementation-protocol.r4`가 대체한다.
- 상세 기준: [플레이 폐루프와 증거 묶음 개발 체계](../Architecture/플레이폐루프와증거묶음개발체계.md)를 따른다.

## D-235 메이저 아르카나 방향은 카드가 아니라 활성화 인스턴스에 귀속한다

- 상태: `Accepted`
- 결정일: 2026-08-24
- 방향 가변성: 카드 고유 식별자는 정체성을 소유하고 정·역방향은 `MajorArcanaActivationStableId`별 권위 판정 사본이 소유한다. 동일 카드는 서로 다른 활성화에서 다른 방향일 수 있지만 한 활성화 수명 안에서는 재판정하지 않는다.
- 활성 수명: 교체·해제·향후 재방향 판정은 기존 값을 제자리에서 바꾸지 않고 활성화를 종료한 뒤 새 활성화와 상속 계보를 만든다. 과거 방향·근거 revision/hash·Effect 계보는 보존한다.
- 상속과 효과: 상위 방향은 하위 최종 카드에 한 번만 펼치며 `Numeric | Interpretive | Ordering | DirectionOnly` 정책과 허용 Effect 연결점을 구분한다. 카드가 WI를 직접 실행하거나 세계 사실·심리 기간·운영 상태를 바꾸지 않는다.
- 첫 세로 조각: `RecoveryShare51` 정책과 Town 주민 생활복구 Fixture를 선택형 프로필로 추가한다. 기존 seed 기반 방향 정책과 `simulation-save.v1~v15` 정규형은 유지하고 새 활성화·Town 상태는 `simulation-save.v16`에서만 hash에 포함한다.
- 증거 경계: Domain Fixture·집중 시험·저장 재생은 E8이 아니다. 실제 Town H 이동, canonical `SimulationWorldShell`, Play Mode·Game View, Hosted 동등성과 장기 생활 연속성은 별도 증거가 필요하다.
- 상세 기준: [Ssalddel 게임 기획 통합 기준](../Architecture/게임기획통합기준.md)과 `eng/execution-ledgers/work-orders/town-arcana-npc-life-recovery.e9-work-order.json`을 따른다.

## D-236 Marketplace 상품 관측은 Item 효과 근거이고 Synty는 범주형 외형이다

- 상태: `Accepted`
- 결정일: 2026-08-24
- 표현 경계: Amazon·Alibaba 개별 상품 외형을 복제하지 않는다. Unity는 승인된 `VisualKey`를 Synty 범주형 Prefab으로 표현하며 같은 외형이 서로 다른 승인 특성과 효과 정의를 가질 수 있다.
- 근거와 규칙: 공개 `ApprovedInteriorReference`와 별도 승인 `상품근거특성Profile`을 구분한다. 상품명 문자열이나 실시간 페이지에서 Runtime 효과를 임의 생성하지 않고 `상품특성효과RuleSet` revision으로 결정적으로 변환해 Catalog·Profile·Rule hash를 함께 고정한다.
- 권위 경계: 최초 `상품근거ItemDefinition`은 `DefinitionOnly`이고 Unity 상세는 특성·효과·출처를 읽기 전용으로 표시한다. 실제 Item 사용·설치·장착 효과는 별도 WI Preview·Confirm과 Simulation Core의 Task·Effect·WorldTick을 통과하기 전에는 적용하지 않는다.
- 제외: 가격·재고·브랜드·구매 이력·주민 소유 정보와 현실 상품 성능 보증은 게임 효과 근거로 사용하지 않는다. 기존 세계는 새 Marketplace 관측이나 Rule revision 때문에 자동 변경하지 않는다.
- 상세 기준: [상품 관측 기반 실내공간조립엔진](../Architecture/상품관측기반실내공간조립엔진.md)과 `E9-WO-TOWN-HOUSE-INTERIOR-LAYOUT`을 따른다.

## D-237 현장 전투 참여 방식은 관찰 운영과 직접 개입을 같은 권위 원장에서 잠근다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 단일 원장: `ObserverOperation`과 `DirectAction`은 별도 전투 시스템이나 카메라 상태가 아니다. BattleInstance 생성 뒤 플레이어가 방식과 동결 카드 hash를 한 번 확정하고 해당 전투 수명 동안 바꾸지 않으며 두 방식 모두 같은 100ms BattleTick·체력·승패·Nature 결과 인계를 사용한다.
- 관찰 운영: Simulation Core가 카드 정책과 Stable ID 정렬로 자동 행동을 결정한다. 전투당 한 번의 전술 일시정지에서만 비상 카드 하나를 활성화하거나 건너뛸 수 있고, Unity는 피해 배율·대상·행동 결과를 계산하지 않는다.
- 직접 개입: 기존 직접 이동·공격·방어·회피를 유지한다. 피격 횟수·회피 성공·완료 BattleTick으로 S/A/B 성과를 확정하고 기본 Nature 보상은 동일하게 유지한 채 직접 성과만 최대 재건 부품 2개를 추가한다. 공개 Nature API가 추가 보상을 주입하는 것은 금지하고 전투 Application의 내부 결과 인계만 허용한다.
- 실행과 저장: Solo `LocalProcess`와 Hosted `RemoteHost`는 같은 전투 Application·Domain을 사용한다. 새 Nature r2 저장은 `simulation-save.v18`에 권위 추가 보상 명령을 포함하며 v17 이하 hash 의미는 유지한다. 카드 사본·방식·관찰 개입·직접 성과는 Battle save/replay에 봉인한다.
- 증거 경계: Domain·LocalProcess·HTTP·Unity EditMode 자동시험은 실제 두 방식의 Play Mode 완주와 Game View 체감 증거를 대신하지 않는다. E7과 `promotionEligible`은 canonical `SimulationWorldShell` 실제 입력 및 Hosted 동등성까지 확인한 뒤 별도로 판정한다.
- 대체 관계: D-206의 `TacticalCommand` 수동 전술 입력은 호환을 위해 남긴다. Nature 첫날의 기본 3인칭 참여는 이 결정의 `ObserverOperation`으로 구체화하며 D-206의 단일 전투 원장·서버 권위·공간/시간 경계는 유지한다.
- 상세 기준: [Nature 생존 생활거점 세로 조각](../Architecture/Nature생존생활거점세로조각.md)과 `E9-WO-NATURE-DUAL-COMBAT-PARTICIPATION`을 따른다.

## D-238 영역 건물 발전은 다섯 독립 누적 트리로 관리하고 Nature부터 실제 구현한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 독립성과 누적성: Nature·Farm·Town·Hub·City는 각각 Foundation→Operations→Specialization→Resilience→Landmark 카탈로그를 가지되 서로를 선행 조건으로 삼지 않는다. 한 영역 안에서도 첫 우선 갈래를 골랐다는 이유로 다른 갈래를 영구 잠그지 않으며 선행 건물·재료·배치 조건으로 누적한다.
- 공간과 권위: 플레이어는 H1 건설을 `Preview → Confirm`으로 선택하고 Simulation Core가 비용·선행·발자국·완료를 판정한다. H1 운영 상태는 H2·H3 성장의 근거일 뿐 상위 공간을 자동 승격하지 않는다. Solo `LocalProcess`와 Hosted `RemoteHost`는 같은 Core를 사용한다.
- 첫 실제 조각: Nature r3만 작업대·보관대·방책·자연 배움터·회복 시설의 실제 건설·취소·완료 상태를 가진다. Farm·Town·Hub·City는 현재 결정적 정의 카탈로그이며 실제 자원·작업·효과는 각 독립 세로 조각에서 추가한다.
- 배움터 경계: 자연 배움터는 선택 시설이고 종교 점수·교리 우열·NPC 자격을 만들지 않는다. 운영자 승인 자료만 읽기 결과에 포함하고 현재는 중립 Fixture만 사용한다. 외부 영상 Provider는 세션·시계·Unity 조회에서 호출하지 않는다.
- 저장과 증거: 새 상태는 `simulation-save.v19`에 카탈로그 revision/hash, 건물 노드·작업·학습 방문을 포함하고 v18 이하 hash 의미를 유지한다. 코드·자동시험 E5 Fixture를 실제 `SimulationWorldShell` Play Mode, Game View, Hosted 동등성 또는 E8 완료로 확대 해석하지 않는다.
- 상세 기준: [영역별 건물 발전 테크트리 계획](../Architecture/영역별건물발전테크트리계획.md)과 `E9-WO-NATURE-AREA-BUILDING-PROGRESSION`을 따른다.

## D-239 플레이 폐루프는 Core·Extension 자식과 영역·세계 집계로 완결 판정한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 계층: 직접 성공·실패·회복·귀환을 증명하는 최소 단위는 `PlayableUnit`, 한 영역의 필수 단위를 모으는 파생 판정은 `AreaAggregate`, 독립 영역을 하루 또는 선택적 이동으로 조립하는 판정은 `WorldAggregate`로 관리한다. PlayableUnit은 AreaAggregate 하나를 부모로 가지며 WorldAggregate는 독립 영역을 소유하지 않고 준비 의존으로 참조한다.
- 완결 등급: 생존·생산·생활·창고·서비스의 최소 약속은 `Core`, 건물 발전·타로·사용자 배치는 `Extension`, 자식 상태에서 계산되는 항목은 `Aggregate`다. 선택형 Extension은 영역 Core 완결을 막지 않으며 구현됐다는 이유로 영역 전체를 완료시키지도 않는다.
- 상태 판정: `CoreClosed`와 `ExtendedClosed`는 각각 E5 결정적 발현, `PlayClosed`는 E7 실제 입력·화면·귀환을 뜻한다. AreaAggregate의 CoreClosed는 모든 필수 Core 자식이 닫힌 경우에만 파생하고 단일 고단계 산출물이나 수동 상태 변경으로 승격하지 않는다.
- 작업 순서: 현재 준비도에 따라 Nature→Town→Farm→Hub→City의 내부 Core E5를 먼저 닫고 E6·E7로 올린다. 이 순서는 영역 간 업무 의존이 아니며 Farm→Hub·Hub→City·Nature↔Farm은 독립 영역 준비 뒤 별도 통합 폐루프로 다룬다.
- 경계: Hub Core는 `WI-HUB-05 OutboundReady`에서 닫고 `WI-HUB-06` 차량 적재는 외부 연결로 남긴다. Town 메이저 아르카나는 `WI-CARD-01`로 관리하며 기존 `WI-WORLD-06` 역할 카드 장착과 구분한다. Farm 내부 보관에 지역 발전 예약 번호 `WI-FARM-07~09`를 재사용하지 않는다.
- 대체 관계: D-234의 PlayableLoop·EvidencePackage 원칙을 구체화하며 기존 Stable ID와 증거 묶음을 유지한다. 단일 큰 영역 폐루프를 직접 완료 판정하던 형식은 `ssalddel-playable-loop-catalog.v2`의 자식·집계 파생 판정으로 대체한다.
- 상세 기준: [플레이 폐루프 완결 로드맵](../Architecture/플레이폐루프완결로드맵.md)과 [플레이 폐루프와 증거 묶음 개발 체계](../Architecture/플레이폐루프와증거묶음개발체계.md)를 따른다.

## D-240 플레이어 활동은 역할이 아니라 현장 원정·영역 운영·영역 제조의 선택 가능한 세 갈래로 분류한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 플레이어 중심: 플레이어는 고정 직업이나 플레이 모드를 먼저 선택하지 않는다. 같은 플레이어가 `FieldExpedition`, `AreaOperation`, `AreaManufacturing`을 상황에 따라 오가며 각 행동과 기회 조회에 현재 갈래만 표시한다.
- 상호의존: 현장 원정은 외부 재료와 위협 결과를 가져오고, 영역 운영은 안전·보관·작업공간을 열며, 영역 제조는 확보한 재료를 다음 원정이나 영역 운영에 쓸 수 있는 물품으로 바꾼다. 어느 한 갈래도 기본 플레이 진입을 독점하지 않는다.
- 첫 실제 반환: Nature r4에서 운영 작업대와 오두막 내부 문맥을 요구하는 `WI-NATURE-16`이 통나무 2·재건 부품 1을 예약해 4초 뒤 현장 보급 꾸러미를 만든다. 취소하면 예약을 반환하고, 다음 원정에서 선택적으로 소비하면 패배 시 소지 재료 한 묶음을 한 번 보호한다. 보급이 없어도 기본 원정은 허용한다.
- 권위와 저장: Preview는 상태를 바꾸지 않고 Confirm·권위 시계·전투 결과 인계만 자원과 준비 상태를 바꾼다. Solo `LocalProcess`와 Hosted `RemoteHost`는 같은 Simulation Core를 사용하며 r4 상태는 `simulation-save.v20`에만 추가하고 v19 이하 정규형을 유지한다.
- 증거 경계: 활동 분류는 UI 역할 전환, 새 공식 Scene, 자동 수익·승리·생산량 보정이 아니다. 첫 보호 단위는 실제 Lot이 아니라 현재 인벤토리의 결정적으로 정렬한 재료 한 묶음이며, 실제 `SimulationWorldShell` 입력·Game View와 Hosted hash 동등성 전에는 E7이나 E9로 승격하지 않는다.
- 상세 기준: [플레이어 중심 게임 개발 업무 구조](../Architecture/플레이어중심게임개발업무구조.md), [플레이 폐루프와 증거 묶음 개발 체계](../Architecture/플레이폐루프와증거묶음개발체계.md), [Nature 생존 생활거점 세로 조각](../Architecture/Nature생존생활거점세로조각.md)을 따른다.

## D-241 운영 유래 반복 WI는 NPC 루틴이고 플레이어는 정책과 예외를 통제한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 분류: WI의 업무 의미 유래 `OperationsDerived | SimulationNative | Hybrid`, 게임 통제 정책 `NpcRoutine | PlayerOrNpc | PlayerDirect | WorldAutomatic`, 실행 인스턴스 발생원 `DataDriven | PlayerDriven | NpcDriven | WorldDerived`을 서로 다른 축으로 기록한다. 운영 유래라는 이유만으로 실행 발생원을 추정하지 않는다.
- 플레이어 경계: `NpcRoutine`의 정상 업무는 NPC와 WorldTick이 수행한다. 플레이어는 자동화·우선순위·선호 담당자·완료 전 취소·시설 차단 해소를 선택할 수 있지만 업무 완료 상태나 Effect를 직접 주입하지 않는다. Farm의 직접 작업은 `PlayerOrNpc`로 유지한다.
- 첫 세로 조각: `npc-routine-control.r1`에서 Hub 독립 Fixture의 `WI-HUB-03`은 전용 NPC가 시작하고 `WI-HUB-04~05`는 세계 파생 전이로 이어진다. `WI-HUB-05 OutboundReady`에서 반환하며 `WI-HUB-06` 차량 상차와 외부 운송은 포함하지 않는다. 새 프로필의 직접 Confirm 차단은 현재 이 조각에 구현됐고 나머지 `NpcRoutine` WI는 분류만 완료됐다.
- 실행과 저장: Solo `LocalProcess`와 Hosted `RemoteHost`는 같은 Simulation Core와 읽기 모델을 사용한다. 새 프로필·실행 계보는 `simulation-save.v21`에만 포함하고 v20 이하의 직접 경로와 canonical hash 의미를 보존한다. 운영 Provider·운영 DB·외부 효과는 실행 경로에 포함하지 않는다.
- 증거 경계: 독립 Fixture·결정성·차단·취소·Local/Hosted Adapter·Save/Replay 자동 시험은 E3 증거다. Hub H 공간 결속, 실제 WI 세계 발현, `SimulationWorldShell` Play Mode·Game View와 지속 NPC 생활세계가 없으므로 E5·E7·E8을 주장하지 않는다.
- 상세 기준: [NPC 루틴 WI 통제 정책](../Architecture/NPC루틴WI통제정책.md)과 `E9-WO-HUB-NPC-ROUTINE-OUTBOUND`을 따른다.

## D-242 배치 통제의 주 성립 축은 H1에서 H4 준비도로 올라간다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 이중 방향: 공간의 주 성립 판단은 `H1 운영 증거 → H2 성립 → H3 성립 → H4 AreaSet 준비도`다. 기존 `WorldEnvelope → AreaEnvelope → H3/H2/H1 → 실내` 방향은 폐기하지 않고 위치·크기·충돌을 제한하는 하향식 제약 검사 축으로 재분류한다.
- 권위: H1의 능력·용량·연결·운영 상태·배치 계획 hash를 Simulation Core가 규칙 catalog로 평가한다. Unity Transform, Scene 계층, 시각 배치 통과는 H2 이상의 성립이나 능력 권위를 만들지 않는다.
- 첫 조립: `h1-stock:hub-receiving-storage`와 `h1-stock:hub-outbound-staging`이 같은 승인 Hub 실내 계획을 제공하면 `h2-candidate:hub-internal-warehouse`가 Qualified 뒤 WorldTick에서 Formed가 된다. 하위 증거가 사라지면 Degraded가 된다.
- 상위 경계: H3 진부 Hub는 내부 창고 H2와 차량 H2를 함께 요구한다. 차량 H2가 없는 현재 범위에서는 H3는 Blocked이고 H4는 PartiallyReady이며 H4 인스턴스를 자동 생성하지 않는다.
- 호환과 저장: `placement-control-hierarchy.v1/v2`와 기존 실내 계획을 읽고, 상향식 상태는 선택형 `placement-control-hierarchy.v3`와 `simulation-save.v22`에만 추가한다. 프로필 없는 v21 이하 세션의 hash 의미를 유지한다.
- 증거 경계: 계약·결정성·저장 재생·Local/Hosted Adapter 자동 시험은 E3이며 저장 Scene v3 배선, 실제 이동·NPC 동선·Game View는 E7 별도 증거다.
- 상세 기준: [상향식 배치 성립 통제 계층](../Architecture/상향식배치성립통제계층.md)과 `E9-WO-HUB-INTERNAL-WAREHOUSE-PLACEMENT`를 따른다.

## D-243 발산과 수렴은 수치 균형이 아니라 양방향 플레이 인계로 조화를 판정한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 분류: `발산 흐름(陽)`은 탐사·채집·전투·구조처럼 세계에 직접 개입하는 행동 목적이고, `수렴 흐름(陰)`은 재고·위험을 읽어 정책·우선순위·NPC 위임·회복을 정하는 행동 목적이다. 장소·직업·성별·선악·플러스/마이너스 분류가 아니며 제조·건설·정비는 두 흐름의 순환 연결부다.
- 조화 판정: 50:50 수치, 강제 교대, 단일 흐름 불이익과 자동 보정을 만들지 않는다. 발산 결과가 수렴 선택으로 인계되고 수렴 결과가 다음 발산 선택을 실제로 바꾸는 PlayableLoop가 닫혔을 때 조화를 증명한다.
- 첫 세로 조각: 기존 Nature 현장 보급 왕복에서 `WI-NATURE-16` 직접 제작을 보존하고 `WI-NATURE-17` NPC 위임 제작을 대안으로 추가한다. NPC 부재·정책 중지·역량 부족에도 직접 제작과 기본 원정은 계속 가능하다.
- 경계: 플레이 흐름은 WI 발생원·전투 참여 방식·E 증거·H 공간·타로 방향과 별도 축이다. 카드는 선택과 인계를 설명할 수 있지만 발산·수렴 상태가 메이저 아르카나 정·역방향이나 배율을 바꾸지 않는다.
- 상세 기준: [플레이어 중심 게임 개발 업무 구조](../Architecture/플레이어중심게임개발업무구조.md)와 `player-centered-development-flow.r4`를 따른다.

## D-244 WI는 한국어 기능명을 먼저 표시하고 안정 고유 식별자는 보조 표기로 유지한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 표시 기준: 사람이 읽는 WI 이름은 `한국어 작업군 + 진행 순서 + 한국어 기능명`으로 구성한다. 예시는 `자연 탐사·생활 거점 7단계 · 오두막을 지을 터 선정 (WI-NATURE-07)`이다.
- 호환 경계: `WI-NATURE-07`, `WI-001` 같은 고유 식별자는 저장·재생·API·Event·metadata·공간 결속의 안정 값이므로 이름 정비를 이유로 변경하지 않는다. 한국어 이름은 설명·조회·Unity 표시용 파생값이며 상태 권위를 만들지 않는다.
- 단일 원본: 60개 WI의 한국어 기능명·작업군·진행 순서는 `eng/execution-ledgers/world-interactions.json`이 소유한다. C# 이름 카탈로그와 생성 문서는 관리 스크립트가 결정적으로 생성하며 수동 복제하지 않는다.
- 표시 실패: 알려진 WI를 번호나 ID만으로 표시하지 않는다. 미등록 ID는 임의로 번역하지 않고 `알 수 없는 세계 상호작용 (ID)`로 표시해 대장 누락을 드러낸다.
- 상세 기준: [세계 상호작용 단위 중심 공간–Simulation 통합](../Architecture/세계상호작용단위중심공간Simulation통합.md)의 WI 한국어 명칭 규칙을 따른다.

## D-245 WI는 한 의도와 하나의 주요 권위 결과만 소유하고 절차는 별도 흐름이 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 단일 책임: WI 하나는 한 행위자의 한 의도와 하나의 `primaryOutcomeCode`를 소유한다. Preview·Confirm·Task·Effect는 별도 절차 WI가 아니라 같은 책임의 실행 생명주기다. 하나의 결과를 원자적으로 지키는 부수 효과와 배타적 결과 묶음은 허용하지만, 독립적으로 선택·취소·실패·재시도할 수 있는 행동은 별도 WI로 분리한다.
- Actor 경계: Player·NPC·World는 WI 정체성이 아니라 실행 인스턴스의 발생원과 Actor 결속이다. 같은 의도·대상·결과를 가진 플레이어 작업과 NPC 작업은 같은 atomic WI를 사용하고, NPC 위임 정책과 NPC의 실제 작업은 서로 다른 책임으로 둔다.
- 흐름 경계: 순서·분기·반복·귀환은 `world-interaction-flows.json`이 소유한다. 기존 `predecessorWiIds`·`successorWiIds`는 읽기 호환 힌트일 뿐 새 생성·공간 전이의 권위가 아니다. 선택형 영역 연결은 각 독립 영역의 성립 조건이 되지 않는다.
- 표시 대체: D-244의 한국어 기능명 우선과 안정 ID 보존은 유지하지만 `진행 순서`를 사람이 읽는 이름에 넣는 규칙은 대체한다. `sequence`는 대장 탐색 순번이며 화면에는 `자연 탐사·생활 거점 · 오두막을 지을 터 선정 (WI-NATURE-07)`처럼 표시한다.
- migration: 기존 ID·API·Event·save·replay를 제자리에서 다른 의미로 바꾸지 않는다. 복합 책임 `WI-001`, `WI-NATURE-11`, `14`, `16`, `17`, `WI-WORLD-07`은 책임별로 분리한다. 독립 의도가 없는 `WI-LOG-02~05`, `WI-ORDER-02`, `05`는 부모 Task/Effect로 내리고, 실제 Actor 작업인 `WI-HUB-04~05`, `WI-ORDER-03~04`는 `WorldAutomatic`에서 NPC 행동 책임으로 전환한다. 모두 새 실행·저장 판본에서만 적용한다.
- 증거 경계: 60개 주요 결과·다중 효과 감사와 55개 흐름 분리는 E3 구조 증거다. 복합 WI Runtime migration, Play Mode·Game View, 지속 NPC 생활세계 전에는 E5·E7·E8·E9 완료를 주장하지 않는다.
- 상세 기준: [WI 단일 책임 원칙](../Architecture/WI단일책임원칙.md)과 `E9-WO-WI-SINGLE-RESPONSIBILITY`를 따른다.

## D-246 WI 음양 사분면은 행동 목적과 실제 수행 주체를 직교 결합한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 분류 축: 첫 부호는 실행 행동의 목적이다. 탐사·전투·채집·이동·운반처럼 세계에 직접 개입하면 Yang, 관찰·정책·배정·보관·회복·계획처럼 상태를 수렴하면 Yin이다. 둘째 부호는 실제 `ActorBinding`이며 Player는 `+`, NPC는 `-`다. 따라서 `++`, `+-`, `-+`, `--`는 각각 양·Player, 양·NPC, 음·Player, 음·NPC다.
- 발생원 경계: `TriggerSource`는 무엇 때문에 실행이 시작됐는지만 설명하며 수행 주체 부호를 정하지 않는다. Player가 정책을 선택한 뒤 NPC가 수행하면 `PlayerDriven`이어도 NPC 부호다. 실제 Player/NPC Actor가 없는 DataDriven·WorldDerived 자동 전이는 사분면 밖에 둔다.
- 문맥과 불변성: 제조·건설·복원처럼 연결부인 WI는 승인된 PlayableLoop 문맥으로 실행별 Yang/Yin을 정하고 문맥이 없으면 `Unclassified`로 남긴다. 판정 사본은 Preview→Confirm→Task→Effect 수명 동안 고정하고 같은 WI의 다음 실행은 실제 Actor와 문맥에 따라 다른 사분면일 수 있다.
- 권위와 효과: Simulation Core만 판정하며 Unity와 클라이언트는 상태 사본을 읽기만 한다. 이 사분면은 선악·성별·성공·실패가 아니고 생산량·피해·보상·재고·타로 방향·카드 배율에 영향을 주지 않는다.
- 저장 호환: 새 분류 실행은 `WorldInteractionInvocation.v2`와 NPC 실행 상태 사본에 `world-interaction-polarity-quadrants.r1`을 동결하고 `simulation-save.v23` hash에 포함한다. v22 이하 정규 hash 의미와 복원 판본은 유지한다.
- 대체 관계: D-243의 Player 중심 발산·수렴 설명을 실제 Player/NPC Actor 전체에 적용 가능한 `ActorActionPurpose`로 확장한다. D-232의 네 발생원과 D-245의 단일 책임·Actor 결속은 대체하지 않고 서로 다른 축으로 유지한다.
- 상세 기준: [WI 음양·수행주체 사분면 체계](../Architecture/WI음양수행주체사분면체계.md)와 `E9-WO-WI-YIN-YANG-ACTOR-QUADRANTS`를 따른다.

## D-247 배치 결과는 플레이어 감각 표현축에서 교차 검증한다

- 상태: `Accepted`
- 결정일: 2026-08-25
- 세 축: D-242의 `H1→H4` 상향식 성립축과 `World→소품` 하향식 제약축을 유지하고, Graphics·Camera·Animation·Lighting·Audio·UI를 묶는 `플레이어 감각 표현축`을 교차 검증 축으로 추가한다. 표현 영역은 새 H 단계나 E 단계가 아니다.
- 책임 분리: `placement-presentation-bindings.v1`은 Camera Focus·Actor Work·Tool Socket·Audio Emitter·FX·Cutaway 기준점만 소유한다. `wi-presentation-plan.v1`은 권위 상태 사본별 Animation·Audio·FX·UI Intent와 필수·선택 영역을 소유한다.
- 음향 범위: 객체 접촉음은 배치 객체의 3D Emitter에, 공간 Ambient는 H1~H3 환경에, BGM은 AreaSet·플레이 세션 문맥에 결속한다. BGM은 개별 WI 완료 조건이 아니며 승인 자산이 없으면 미결 상태를 드러낸다.
- 권위와 저장: Animation Event·Audio·FX·카메라 전환은 Command·WorldTick·Task 완료를 호출하지 않는다. 표현 plan/hash는 EvidencePackage 무효화 근거지만 Simulation canonical save/replay hash를 바꾸지 않는다.
- 첫 적용: `WI-NATURE-05~06`은 도끼 획득·벌목 진행·취소·나무 낙하를 상태 사본에서 표현한다. 벌목 Animation과 접촉음은 필수 표현 영역이고 Nature Ambient·BGM은 현재 선택 채널이다.
- 증거 경계: 계약·EditMode·자동 PlayMode는 실제 Game View 판독과 청음을 대신하지 않는다. E7은 canonical Scene의 실제 입력·Animation 중단·복귀·Audio·Console 증거를 별도로 요구한다.
- 상세 기준: [플레이어 감각 표현축](../Architecture/플레이어감각표현축.md)을 따른다.

## D-248 Codex 장기 Goal은 PlayableUnit 하나를 소유하고 WI WIP 1로 진행한다

- 상태: `Superseded`
- 후속 결정: Goal별 PlayableUnit 소유는 유지한다. WIP 1 강제는 D-284, E7 수직 종료와 E8·E9 해석은 D-266을 따른다. 아래 본문은 당시 결정의 역사 기록이다.
- 결정일: 2026-08-25
- Goal 경계: 장기 Goal은 플레이어 약속을 가진 `PlayableUnit` 하나와 목표 `E7 PlayClosed` 또는 `E8 WorldClosed`를 소유한다. `AreaAggregate`·`WorldAggregate`, H 전체, E/G 체계 전체를 Goal 하나에 넣지 않고 필수 자식의 완료에서 파생한다.
- 실행 경계: 동시에 활성 Goal 하나와 WI 하나만 허용한다. 같은 플레이어 약속 안에서 새 영향이 발견되면 Goal을 폐기하지 않고 작업 명세의 E9→E1 하향 검토를 다시 열며, 플레이어 약속 변경 또는 독립 폐루프 교체 때만 Goal을 바꾼다.
- 증거 경계: E7은 실제 입력·Play Mode·Game View·성공/실패/회복/귀환·결정적 Save/Restore/Replay를 요구한다. 필수 `NpcRoutine`이 있는 폐루프는 NPC 판단→행동→결과→다음 판단까지 닫는 E8 `WorldClosed`를 목표로 한다. E9는 안정된 E8 기준선의 변경 적응 증거이지 새 기능의 즉시 Goal이 아니다.
- 우선순위: 플레이어 연속성 기준 `Nature→Farm→Hub→Town→City`를 사용하되 각 영역은 독립 실행한다. Core를 같은 영역의 Extension보다 먼저 닫고 영역 간 화물·운송을 선행 의존으로 만들지 않는다.
- 대체 관계: D-239의 당시 준비도 기준 `Nature→Town→Farm→Hub→City` 실행 순서는 이 플레이어 연속성 우선순위로 대체한다. D-239의 PlayableUnit·Core/Extension·파생 Aggregate 원칙과 기존 StableId는 유지한다.
- 첫 Goal: `playable-loop:nature-shelter-foundation.v1 → E7 PlayClosed`, WI WIP는 `WI-NATURE-05`다. 자동 실제 입력과 Hosted 동등성이 있어도 사람의 전체 수동 완주·실제 청음·남은 Scene 경고 분리 전에는 E7을 `Partial`로 유지한다.
- 상세 기준: [Codex PlayableLoop Goal 운영 체계](../Architecture/CodexPlayableLoopGoal운영체계.md)와 `codex-playable-loop-goals.v1`을 따른다.

## D-249 거점 성찰은 승인 자료를 읽는 플레이어 선택이며 시청 보상이 아니다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 보편 용어: 새 계약·기획·상태 사본에서는 `거점 성찰`을 사용한다. 기존 Unity의 `저녁학당*`과 `hongik-unity-learning-card-publication.v1`은 저장·표현 호환 Adapter로 보존하며 새 권위 이름으로 확장하지 않는다.
- 자료 경계: 영상·문서 원문은 `출처 관측 → 제공자 중립 해석 후보 → 사람 승인 학습 자료`의 세 층을 통과한다. Simulation은 명시적으로 동기화된 승인 자료의 revision과 hash만 읽고 Provider API, API key, 원문 전체, 댓글·시청 기록을 세션이나 Tick 중 조회하지 않는다.
- 보상 경계: 외부 영상 열기나 시청 시간에는 능력치·재화·전투 보상을 부여하지 않는다. 보상은 승인 자료를 바탕으로 플레이어가 게임 안에서 `오늘을 돌아보기`를 명시적으로 확정하고 다음 활동을 시작할 때 한 번만 적용한다.
- 첫 허용 효과: 첫 세로 조각은 `Awareness +1 / BeginnerMind` 또는 `Resolve +1 / IntegratedProgress`만 허용한다. 효과는 정보 해석과 다음 선택을 넓히며 피해·생산량·시장가격·타로 방향을 직접 바꾸지 않는다. 게임 날짜당 1회, 캐릭터와 자료 revision 조합당 1회로 제한한다.
- 폐루프와 증거: `playable-loop:nature-base-reflection.v1`은 `playable-loop:nature-field-supply-return.v1`의 선택 Extension이며 플레이어 선택으로 닫는 E7 대상이다. NPC의 시설 방문과 학습 생활세계는 별도 `building-learning-routine` E8 대상이고 서로 완료 증거를 대신하지 않는다.
- 공간 경계: E4 이후 canonical `SimulationWorldShell`의 기존 Nature H4·H3·H2 안에 H1 `ReflectionInteractionAnchor`를 결속한다. 새 공식 Scene이나 H5는 이 폐루프의 필수 의존성이 아니다.
- 저장 경계: E1~E3 뼈대에서는 동결 상태 사본과 결정적 hash만 제공한다. 주 세션 `simulation-save.v24` 통합은 E4 작업으로 남기며 기존 v23 hash를 소급 변경하지 않는다.
- 상세 기준: [Nature 거점 성찰 폐루프](../Architecture/PlayableLoops/nature-base-reflection.v1.md)와 `E9-WO-NATURE-BASE-REFLECTION`을 따른다.

## D-250 플레이 폐루프의 논리와 표현 성숙도를 분리하고 낮은 단계로 통합 판정한다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 두 궤적: `PlayableLoop`는 `Logic`과 `Presentation` 성숙도를 가진다. 논리는 권위 상태·결정성·저장·재생·Hosted 동등성을, 표현은 실제 입력·배치·카메라·Synty·HUD·Game View 식별 가능성을 판정한다.
- 통합 판정: 통합 E는 두 궤적 중 낮은 단계다. 논리 E7만으로 `PlayClosed`를 선언하지 않으며 두 궤적이 E7 이상이고 열린 피드백이 없을 때만 닫는다.
- 재개 규칙: 표현 검증에서 권위 상태나 H 계약 누락이 발견되면 같은 Goal과 PlayableLoop를 유지한 채 가장 이른 논리 E 단계를 다시 연다. 시각 배치 문제이면 표현 E4~E6만 다시 연다.
- 비대체: 이중 성숙도는 E·G·H·WI와 WI 음양 사분면을 대체하지 않는다. `evidenceTrackCode`로 증거의 책임만 분리한다.
- 상세 기준: [플레이 폐루프 논리·시각 이중 순환 체계](../Architecture/플레이폐루프논리시각이중순환체계.md)를 따른다.

## D-251 표현 E4~E7 승격은 공통 검증 모듈과 기능별 조건 모듈을 통과한다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 공통 관문: 모든 `PlayableUnit`은 E4 상태·H 결속, E5 시각 자료·Bounds, E6 Player 대비 크기·간격과 상태 차이, E7 실제 카메라·입력·결과·귀환 모듈을 적용한다.
- 조건 관문: 지면·건물·울타리·Actor·실내·작업 구역·카메라 가림은 폐루프가 선언한 기능 프로필에 따라서만 추가한다. 사용하지 않는 기능을 억지로 통과시키거나 필요한 기능을 담당자 판단으로 생략하지 않는다.
- 승격과 재개: `Blocking` 실패는 해당 표현 E 승격을 막고 가장 이른 책임 단계를 다시 연다. 상태·H 계약 누락이면 논리 E를, 위치·크기·Bounds·카메라 문제이면 표현 E4~E6을 연다. `Warning`은 위험을 기록하지만 증거를 승격하지 않는다.
- 증거 경계: 자동 Bounds·간격 검증은 실제 Play Mode·Game View를 대신하지 않고 실제 화면 증거도 자동 사전 검증을 대신하지 않는다. 검증 모듈은 Simulation 권위 상태를 변경하지 않는다.
- 관리 기준: 단일 원본은 `eng/execution-ledgers/playable-loop-presentation-validation-modules.json`, 선택 결과는 `docs/AI/generated/playable-loop-presentation-validation.md`다.
- 상세 기준: [플레이 폐루프 논리·표현·결과 순환 개발 방법론](../Architecture/플레이폐루프논리시각이중순환체계.md)의 표현 검증 모듈 관문을 따른다.

## D-252 LH의 3×3·5×5·9×9는 고정 계층이 아니라 기본 동적 맵 창 프로필이다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 일반 계산: 스트리밍 범위 계산은 중심 정수 좌표, `상세 ≤ 활성 ≤ 선행` 반경과 이동 방향 우선순위만 사용한다. L 실행 해상도, H 의미 계층, 셀 키 형식과 셀 내용 공급은 계산기 밖의 Adapter 책임이다.
- 기본 호환: 기존 `FocusL3CellKey`, 125m Scene 배선과 반경 `1·2·4`, 즉 `3×3·5×5·9×9`는 저장·API·Scene 회귀를 위한 첫 보행 PC 프로필로 유지한다. 이를 모든 AreaSet·이동 방식·장치의 보편적 고정값으로 해석하지 않는다.
- 검증 경계: 반경 음수와 포함 순서 역전은 거부하고, 프로필 값은 결정적 창과 우선순위를 만든다. 원형 거리창, 속도 예측 회랑, 복수 스트리밍 원천과 메모리 예산형 선택은 측정 Fixture와 새 계약 전에는 완료로 주장하지 않는다.
- 자료 경계: DEM과 특정 표본 해상도는 셀 내용 공급자의 선택 입력이며 동적 맵 창의 성립 조건이 아니다. 실제 공간 근거가 요구된 요청에서만 승인 자료 누락을 명시적 사용 불가로 처리한다.
- 상세 기준: [LH 공간 실행계층](../Architecture/LH공간실행계층.md)을 따른다.

## D-253 동적 셀 활성화 전에 객체의 표면·간격·가시 하단을 별도 관문으로 검증한다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 동적 맵 창 계산과 객체 배치를 분리한다. 창은 어떤 셀을 준비할지만 정하고, 객체 배치 계획기는 월드 seed·셀 좌표·배치 정책 revision·표면 공급자로 결정적 배치점을 만든다.
- 첫 구현은 평면 표면 공급자를 사용한다. 배치 계획기 자체는 H/L 의미와 지형 알고리즘을 모르며 월드 좌표 표본만 읽는다.
- 셀 내부 최소 객체 간격과 인접 셀 경계 여유를 강제한다. 표면 누락, 배치 금지, 최대 경사 초과, 간격 부족과 후보 고갈을 명시적으로 구분한다.
- Unity 표현은 규칙 적용 배치의 Prefab을 생성한 뒤 Renderer 가시 하단을 실제 셀 표면에 맞추고 공통 `surface-clearance` 검증을 통과해야 `VisualPrepared` 이후로 진행한다.
- 다음 우선순위는 seed 고정 노이즈 표면 공급자와 불규칙 지형 시험이다. Perlin·Simplex·DEM 중 하나를 상위 계약에 고정하지 않으며, 알고리즘 비교는 동일 seed 재현성·셀 경계 연속성·경사·표면 여유·통행 가능성 증거로 판정한다.
- 이번 기준선은 Scene 또는 지형 Mesh를 변경하지 않으며 Play Mode·Game View 완성 증거가 아니다.
- 상세 기준: [LH 공간 실행계층](../Architecture/LH공간실행계층.md)을 따른다.

## D-254 Sky Engine은 세계 공통 대기 권위와 카메라 전역 표현을 잇는다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 축 분리: `LH Engine`은 지면 셀과 H 공간의 조립·적재를 맡고, `Sky Engine`은 세계 공통 시간대·구름·강수·바람·번개·음향을 카메라 전역으로 표현한다. Sky Engine은 H 단계나 배치 통제 단계가 아니며 Unity Transform은 대기 상태 권위를 만들지 않는다.
- 첫 권위: `world-atmosphere.r1`은 Nature r5 권위 시계와 Scenario seed에서 `Clear → Cloudy → Rain → Thunderstorm → Rain → Dawn Clear`를 결정적으로 투영한다. 첫 판본은 가시·청각 표현만 제공하고 이동·수확·전투 같은 게임 효과와 현실 기상 Provider는 포함하지 않는다.
- Unity 호환: 기존 `월드시간대Presenter`의 단독 경로를 유지하고 Sky Engine이 연결된 canonical `SimulationWorldShell`에서만 날씨를 합성한다. Polygon Farm 구름·비 Prefab은 Wrapper 아래 사용하며 오두막 내부 또는 `SkyExposureVolume`에서는 강수와 빗소리만 차폐한다.
- 저장과 증거: 대기 프로필 세션은 `simulation-save.v25`에 상태와 Replay hash를 포함하고 프로필 없는 v24 이하 hash 의미를 유지한다. 코드·자동 시험·저장 Scene 배선은 실제 Play Mode·Game View·Console·청음 E7을 대신하지 않는다.
- 관계: D-048의 프로필 없는 고정 시간대 호환, D-158·D-252·D-253의 LH 공간 실행 경계와 D-250·D-251의 표현 순환·검증 모듈을 유지하면서 세계 대기 표현을 별도 기능으로 추가한다.
- 상세 기준: [Sky Engine 세계 대기 표현 계층](../Architecture/SkyEngine세계대기표현계층.md)과 `E9-WO-NATURE-SKY-ENGINE`을 따른다.

## D-255 LH 불규칙 지형은 교체 가능한 표면 상태 사본으로 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-26
- 표면 경계: LH 셀과 객체 배치는 `I공간동적표면Provider`가 주는 월드 좌표 높이·경사·배치 허용 결과만 읽는다. 첫 불규칙 구현은 seed 고정 다중 옥타브 값 노이즈지만 Perlin·Simplex·DEM을 상위 계약이나 저장 형식에 고정하지 않는다.
- 상태 사본: 비평면 셀은 `17×17` 높이 배열과 표면 mode·규칙 revision·설정 hash·grid hash를 셀 계획에 포함한다. 인접 셀은 공통 월드 좌표를 표본해 경계 높이를 공유하며 Unity는 이 상태 사본으로 통행 Mesh와 Collider를 만든다.
- 호환과 선택: 기존 평면 Cube 경로가 기본이다. 노이즈는 구성 값 또는 `SSALDDEL_LH_NOISE_SURFACE=1`로 명시적으로 선택하며 Scene 기본값, H 의미 계층과 E5·E6 근거를 바꾸지 않는다. DEM은 승인 자료가 필요한 세계에서 사용할 수 있는 별도 선택형 공급자다.
- 객체 관문: 절차 배치와 고정 H 기준점 모두 같은 표면 높이·경사를 사용하고 최대 경사, 간격, 경계 여유와 가시 하단 정렬을 통과해야 한다. 표면 검증 실패를 평면이나 임의 높이로 대체하지 않는다.
- 증거 경계: 결정성 순수 시험과 세 Unity 어셈블리 빌드는 통과했다. 임시 Game View에서 불규칙 지면과 나무 접지를 확인했지만 조명·셀 경계 표현이 미완성이고 기존 Job lock·Sky Presenter Console 오류가 남아 있으므로 Play Mode E7 완료로 보지 않는다.
- 관계: D-252의 가변 창, D-253의 표면·간격·가시 하단 관문과 D-199의 범위/내용 분리를 유지하면서 첫 교체형 불규칙 표면 구현을 추가함
- 상세 기준: [LH 공간 실행계층](../Architecture/LH공간실행계층.md)을 따른다.

## D-256 LH는 지면·셀을 준비하고 Sky 뒤 실외·실내 배치엔진이 표현을 조립한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 실행 순서: canonical `SimulationWorldShell`의 표현 조립은 `LH 지면·셀 준비 → Sky 상태 적용 → 실외자산배치 → 실내공간조립 → 셀 표현 완료` 순서다. Simulation 대기 계산 자체를 LH에 의존시키지는 않는다.
- LH 경계: LH는 스트리밍 창, 지면 상태 사본, Mesh·Collider, H 결속, Connector와 셀 준비 상태만 소유한다. r1 `Placements` 필드는 호환 인계 자료로 유지하지만 Prefab 생성·캐시·해제는 LH 책임이 아니다.
- 배치 경계: 실외배치엔진은 지면과 세계 변화에서 Stable ID·위치를 만들고, 실내공간조립엔진은 건물·H 의미·플레이어 구조 변화에서 고정 실내 계획을 만든다. 두 Runtime은 각자 생성한 표현 객체의 생명주기를 소유한다.
- Sky·지면 영향: 첫 판본은 건조·젖음·적설·바람 같은 표현 Variant만 바꾼다. 날씨는 실외 Stable ID·위치·Spawn·이동·채집 규칙과 실내 가구 계획 hash를 변경하지 않는다.
- 호환과 증거: 기존 `world-asset-placement-plan.v1`, `lh-world.v1`, `simulation-save.v26` 통합 계획과 canonical hash는 호환 Facade로 보존한다. 코드·자동 시험과 조립 순서 기록은 Play Mode·Game View E7을 대신하지 않는다.
- 관계: D-253의 표면 검증, D-254의 Sky 권위·표현 분리와 D-255의 교체형 지면 상태 사본을 유지하면서 LH의 자산 생성 책임만 별도 엔진으로 이관한다.
- 상세 기준: [지도구성과 세계자산배치 분리](../Architecture/지도구성과세계자산배치분리.md), [LH 공간 실행계층](../Architecture/LH공간실행계층.md), [Sky Engine 세계 대기 표현 계층](../Architecture/SkyEngine세계대기표현계층.md)을 따른다.

## D-257 엔진 상호작용은 Logic·Presentation을 같은 WI 명령으로 묶는 통합 관문이다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 성숙도 경계: 엔진 상호작용 검증은 `Logic`과 `Presentation`에 이은 세 번째 E 궤적이 아니다. 같은 `PlayableLoopStableId`, WI, Command와 AuthorityRevision으로 두 궤적의 실제 인계와 귀환을 확인하는 `Integrated` 관문이다.
- 실행 경계: 첫 기준 순서는 `WI Preview → Confirm → Simulation Authority → LH → Sky → 실외배치 → 실내배치 → 화면 결과·귀환`이다. 폐루프 프로필이 필요한 단계만 고르며 이미 준비된 결과는 `Reused`, 적용하지 않는 단계는 `NotApplicable`, 진행할 수 없으면 `Blocked`로 구분한다.
- 권위 경계: `Simulation.AuthorityCore`만 AuthorityRevision을 변경할 수 있다. Unity 표현 단계는 같은 Revision을 읽고 전후 Revision을 보존해야 하며 Trace는 Save/Replay canonical hash, 운영 상태와 공개 진단 API에 포함하지 않는다.
- 첫 적용: `playable-loop:nature-night-day2.v1`의 `WI-NATURE-13~15`에 적용한다. 코드·자동 Trace와 실제 Play Mode·Game View 증거는 서로 대신하지 않는다.
- 관리 기준: 단일 원본은 `eng/execution-ledgers/playable-loop-engine-interaction-validation.json`, 선택 결과는 `docs/AI/generated/playable-loop-engine-interaction-validation.md`다.
- 상세 기준: [플레이 폐루프 엔진 상호작용 검증 체계](../Architecture/플레이폐루프엔진상호작용검증체계.md)와 `E9-WO-PLAYABLE-LOOP-ENGINE-INTERACTION`을 따른다.

## D-258 Synty 표현은 A/B/C 완전성이 아니라 PlayableUnit의 플레이 순간으로 모듈화한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 기준 단위: Synty 표현의 신규 기준 단위는 52개 의미군 × A/B/C가 아니라 `PlayableUnit`이다. 각 폐루프 모듈은 포함 WI·필요 H 능력과 진입·선택·진행·성공·실패 회복·귀환 순간을 실외 기반·기능 객체·상태 덧입힘·실내 설비/소품·Actor/FX 역할에 연결한다.
- 자산 선택: 모듈은 Prefab 경로 대신 기술 대장의 `assetFamilyId` 후보를 선언한다. 후보 수를 세 개로 강제하지 않으며, 적합한 후보 하나는 그대로 사용하고 후보가 없으면 억지 대체하지 않고 차단 또는 명시적 보류로 남긴다.
- 기존 문법: 156개 기준 경관 문법과 A/B/C Builder는 `LegacyGenerated` 읽기 호환 입력이다. 신규 생성·세 변형 완전성 검사를 중단하고 연결구·반복·Bounds·경사·통행처럼 검증 가치가 있는 규칙만 새 배치 검증으로 이관한다. 활성 폐루프·공식 Scene·WI 모판 참조가 0이 되기 전에는 생성 Prefab을 삭제하지 않는다.
- 결정성과 권위: `WorldSeed + PlacementStableId + ModuleRevision + SlotStableId + AuthorityStateCode`로 자산 계열과 계열 내부 Prefab을 결정한다. Unity 표현은 Simulation의 WI 결과·H 의미·`WorldRevision`·Save/Replay hash를 변경하지 않는다.
- 첫 적용: Nature 생활거점의 기초, 황혼 귀환, 밤→Day2, 작업대 네 PlayableUnit과 WI 15개를 첫 모듈로 관리한다. Construction은 독립 공간이 아닌 공유 상태 계층, Generic은 전수 재고 편입 대기 공통 골격, Starter는 제품 기본 후보에서 제외한 시험용 대체 자산이다.
- 관리 기준: 단일 원본은 `eng/execution-ledgers/playable-loop-synty-expression-modules.json`, 생성 상태는 `docs/AI/generated/playable-loop-synty-expression-modules.md`다.
- 상세 기준: [플레이 폐루프 Synty 표현 모듈 체계](../Architecture/플레이폐루프Synty표현모듈체계.md)를 따른다.

## D-259 Synty 팩 출처와 게임 기능 모듈을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 전수 대장: Nature·Farm·Town·City·Construction·Generic·Starter 7팩의 현재 Prefab `2,899`개를 팩 이름이 아니라 지면·식생·실외 구조·실외 기능 소품·통행망·전이·건설 상태·실내 구조·실내 설비·실내 소품·Actor/차량/도구·피드백 FX의 12개 기능 모듈로 분류한다. 모든 Prefab은 하나 이상의 기능 모듈 또는 명시적 보류 이유를 가져야 한다.
- 수명주기: 자동 분류와 제품 배치 승인을 구분하기 위해 `production-ready`, `needs-review`, `shared-base`, `prototype-fallback`, `reserved-for-future-loop`, `excluded`를 사용한다. Construction은 독립 AreaSet이 아닌 공유 건설·복구 상태 계층, Generic은 공유 기반, Starter는 prototype fallback이다.
- 폐루프 연결: 표현 슬롯은 배치 역할에서 기능 모듈을 먼저 요구하고 그 안에서 `assetFamilyId`와 Prefab을 결정한다. 결정 입력에는 자산 모듈 대장 revision을 포함하며 표현은 AuthorityRevision을 변경하지 않는다.
- 호환: 기존 156개 A/B/C와 `CompositionKey`는 `LegacyCompositionAdapter`가 읽는 호환 입력이다. 신규 표현 모듈은 이를 직접 읽지 않으며 공식 참조 0·규칙 이관·저장/Scene 호환·폐루프 Runtime 검증 전에는 물리 삭제하지 않는다.
- 관리 기준: `eng/execution-ledgers/synty-asset-functional-modules.json`과 [플레이 폐루프 Synty 표현 모듈 체계](../Architecture/플레이폐루프Synty표현모듈체계.md)를 따른다.

## D-260 PlayableUnit 수직 성숙도는 E7에서 끝나고 E8~E10은 수평 증거로 판정한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 수직 경계: 하나의 `PlayableUnit`과 장기 Codex Goal은 E1 계약부터 E7 실제 플레이 폐루프까지만 수직으로 올라간다. 새 작업은 E7→E1로 영향을 검토하고 가장 낮은 미완료 의존성을 구현한 뒤 E1→E7로 검증한다.
- 수평 판정: E8은 같은 업무·심리 영역의 E7 둘 이상을 묶은 `AreaHarmonySet` 조화, E9는 사람이 대표 순서로 평가하는 재미·몰입·완성도 개선, E10은 E9 승인 불변 build의 제한 운영 관찰이다. E8~E10은 한 WI나 PlayableUnit의 다음 숫자 승격이 아니다.
- NPC 경계: 과거 E8 NPC 생활세계 책임은 폐기하지 않고 관련 E8 조화 묶음의 필수 `npc-life-continuity` 모듈로 이관한다. NPC가 관련되지 않은 묶음만 사유가 있는 `NotApplicable`을 허용한다.
- 운영 경계: 첫 E10은 `WindowsX64 + LocalProcess`이며 판본화된 관찰 일수·완주 세션·시험자 수·rollback과 사람의 계속 운영 승인을 요구한다. Provider 호출·운영 DB·결제·계약·일반 공개는 별도 권위 승인 없이는 포함하지 않는다.
- 호환: 과거 `legacy-change-adaptive.r10`의 E8 NPC·E9 변화 적응 문서, 계약과 `.e9-work-order.json`은 판본과 함께 읽기 호환한다. 과거 증거를 현재 E8·E9로 변환하거나 합산하지 않으며 변경 영향·Migration·호환·회귀는 단계 번호와 분리된 교차 책임으로 유지한다.
- 현재 판정: Nature 현재 Goal `playable-loop:nature-night-day2.v1`은 통합 E6, 활성 WI `WI-NATURE-14`는 E5다. 첫 `area-harmony:nature-core.v1`은 구성원 미완료로 `WaitingForMembers`이며 E8·E9·E10 증거를 새로 주장하지 않는다.
- 상세 기준: [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md)를 따른다.

## D-261 물품 획득과 장착을 보편 WI로 분리하고 능력은 장착 상태에서 파생한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 보편 계약: `WI-ACTOR-01`은 세계의 고유 물품 인스턴스를 인벤토리로 이전하고 `WI-ACTOR-02`는 소유 물품의 `Equip`·`Unequip`·`Swap`을 확정한다. 도끼·삽·곡괭이 같은 구체 도구 WI는 이 계약을 특화한다.
- 슬롯·능력: 첫 슬롯은 `MainHand`, `OffHand`, `Head`, `Body`, `Legs`, `Feet`, `Back`, `Accessory`다. 획득만으로 작업 능력을 주지 않으며 장착된 ItemDefinition에서 `capability:woodcutting`, `capability:terrain-grading`, `capability:mining` 등을 파생한다.
- WI·H 경계: 보편 획득·장착 WI의 공간은 `NotApplicable`이다. 도끼 발견 지점·접근·작업 장소 같은 H 결속은 `WI-NATURE-05`와 후속 구체 WI가 소유한다. 상태창 열기처럼 읽기 전용 입력은 WI가 아니다.
- 원장 경계: 역할 카드 `WI-WORLD-06`과 전투 편성은 물리 물품 장착과 같은 Preview·Confirm 리듬을 재사용할 수 있지만 공개 ID·저장 원장·효과 의미는 합치지 않는다.
- 저장·호환: 새 명시적 장착 상태는 `simulation-save.v27`에 포함한다. ActorEquipment 입력이 없는 v1~v26 Nature 저장과 자동 장착 의미·hash는 호환 경계로 보존한다.
- 적용·차단: Nature 도끼는 획득 뒤 인벤토리에 들어가고 MainHand 장착 뒤에만 벌목할 수 있다. 코드·자동 시험과 Unity 소비 어셈블리 빌드는 실제 Play Mode·Game View E7을 대신하지 않으며 기존 E7 증거는 새 입력 흐름으로 재검증한다. 지형 평탄화 `WI-WORLD-09`·`simulation-save.v28`은 이 재검증 뒤 여는 후속 작업이다.
- 상세 기준: [플레이어 중심 게임 개발 업무 구조](../Architecture/플레이어중심게임개발업무구조.md)와 현재 `E7-WO-ACTOR-ITEM-EQUIPMENT`을 따른다. 과거 `E9-WO-ACTOR-ITEM-EQUIPMENT`은 D-264에 따라 읽기 호환 자료다.

## D-262 자연 방향광은 URP Lit와 표현 검증 기록으로 강화한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 계산 경계: Nature의 실제 표면 명암은 URP Lit가 월드 법선과 표면→광원 방향의 `saturate(dot(N, L))`를 계산하게 한다. C# 방향광 모델은 동일 방향의 결정성·설명 가능성, 조명 프로필과 검증 입력을 제공하며 별도 색칠 Shader를 중복 구현하지 않는다.
- 프로필: 첫 프로필은 `lighting.pyeongchang.shared-day.v2`, 규칙은 `directional-lighting.natural.r1`이다. 낮·황혼·밤의 환경광 보정은 그늘 면 판독을 위한 표현 값이며 게임 규칙·시야·은신 효과가 아니다.
- 대상 경계: 첫 검증 대상은 LH 지면과 Nature 나무·통나무·오두막·작업대다. 상세 셀은 바닥만으로 통과할 수 없고 핵심물의 Mesh 법선, Lit Shader와 그림자 투사·수신 정책이 확인되어야 `WorldPresentation.Ready`가 된다.
- 자산 경계: Synty 원본 Prefab·Material·Shader Graph는 수정하지 않는다. Runtime Wrapper와 기존 `자연경관ShadowPolicyView`를 우선하며 개별 Renderer 기본 색을 시간대마다 일괄 덮어쓰지 않는다.
- 권위·증거 경계: 방향광 검증은 Presentation E6만 판정한다. 실패해도 LH 통행 준비, Simulation `WorldTick`·`AuthorityRevision`, Save/Replay hash를 변경하지 않으며 실제 그늘 면 가독성은 Play Mode·Game View에서 별도로 확인한다.
- 관계: D-250·D-251의 논리·표현 순환과 공통 표현 검증, D-254의 Sky 권위·표현 분리, D-255·D-256의 LH 지면·배치엔진 분리를 유지하면서 방향광 표면 판독 관문을 추가한다.
- 상세 기준: [Sky Engine 세계 대기 표현 계층](../Architecture/SkyEngine세계대기표현계층.md)과 `E9-WO-NATURE-SKY-ENGINE`을 따른다.

## D-263 Synty 자산 설계 분류는 한국어를 먼저 쓰고 Stable Code를 보존한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 사람 분류: 설계·문서·생성 대장에서는 `실외 표현 / 실내 표현 / 공통 표현`을 최상위로 두고 `범위 → 기능군 → 세부 기능군 → 자산 종류 → 자산 계열 → 실제 Prefab` 순서로 읽는다.
- 저장 호환: `Outdoor`, `Interior`, `Shared`, `world-surface`, `interior-fixture`, `assetFamilyId`와 Prefab GUID 같은 기존 Stable Code·직렬화 값은 바꾸지 않는다. 새 내부 분류 JSON의 필드명은 `범위Code`, `기능군Code`, `세부기능군Code`, `자산종류Code`, `자산계열StableId`처럼 한국어 업무명을 우선한다.
- 권위 경계: 이 분류는 Synty 표현 자산을 찾고 검토하는 기술 대장이다. Prefab 이름·경로·분류 결과가 Simulation 상태, H 공간 의미, WI 결과나 운영 권위를 결정하지 않는다.
- 호환 관계: D-258의 PlayableUnit 표현 선택과 D-259의 12개 영문 기능 Module Stable Code를 유지하면서 사람이 보는 이름과 하위 분류만 한국어 단일 원본으로 확장한다.
- 관리 기준: 한국어 트리의 단일 원본은 `eng/execution-ledgers/synty-asset-human-taxonomy.json`, 기능군·팩 호환 대장은 `eng/execution-ledgers/synty-asset-functional-modules.json`이다. 상세 기준은 [플레이 폐루프 Synty 표현 모듈 체계](../Architecture/플레이폐루프Synty표현모듈체계.md)를 따른다.

## D-264 E1~E9는 판정 주체를 바꾸되 Logic·Presentation 왕복을 유지한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 증거 모델: 현재 판본은 `horizontal-dual-cycle-evidence.r2`다. D-260의 `PlayableUnit E1~E7`, `AreaHarmonySet E8`, `HumanPlaytestCampaign E9`, `LimitedOperationWindow E10` 주체 분리는 유지한다.
- PlayableUnit 왕복: 모든 PlayableUnit은 Logic·Presentation 각각에서 E7→E1 영향을 검토하고 E1→E7을 조립·검증한다. 통합 단계는 두 궤적의 낮은 값이며 열린 피드백이 있으면 닫지 않는다. 표현 E1~E4 계획은 논리 E5 전에 가능하지만 표현 E5 이상 증거는 논리 E5 상태 사본을 요구한다.
- E8 왕복: AreaHarmonySet은 같은 frozen revision과 build에서 논리 인계·실패·NPC 연속성과 공간 전환·카메라·피드백·NPC 관찰 가능성을 별도 궤적으로 판정한다. 발견 사항은 E8 모듈 또는 관련 PlayableUnit의 가장 이른 E1~E7로 되돌린다.
- E9 왕복: 사람 세션은 논리의 인과 일관성·선택 결과·진행 연속성과 표현의 판독·피드백 명료성·전환 자연스러움을 따로 평가한다. 두 궤적과 재미·몰입·완성도, 열린 중대 발견, 사람 승인을 같은 후보 revision에서 통합한다.
- E10 경계: E10은 세 번째 논리·표현 확장이 아니라 E9에서 승인한 불변 후보의 제한 운영 통합 관문이다. E9 또는 후보 revision이 다시 열리면 관찰창을 무효화한다.
- 이관: 기존 자동시험은 Logic 증거로 보존한다. 표현 증거가 분류되지 않은 PlayableUnit은 추정 승격하지 않고 Presentation E1로 이관한다. 과거 `.e9-work-order.json`은 읽기 호환 자료이며 활성 작업은 `simulation-e7-vertical-work-order.v2`를 사용한다.
- 상세 기준: [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md), [플레이 폐루프 논리·표현·결과 순환](../Architecture/플레이폐루프논리시각이중순환체계.md)을 따른다.

## D-265 E8 조화 묶음은 소유 AreaAggregate의 Core 일부를 선택할 수 있다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 소유 경계: 하나의 `PlayableUnit`은 기존처럼 정확히 하나의 `AreaAggregate`에만 속한다. 짧은 E8 검증을 위해 같은 자식을 가진 두 번째 Aggregate를 만들지 않는다.
- 선택 경계: `AreaHarmonySet`은 소유 Aggregate의 필수 Core 자식 중 서로 이어지는 둘 이상을 선택할 수 있다. 선택한 모든 구성원이 해당 Aggregate의 Core여야 하며 외부 영역·선택 확장 자식을 조용히 섞을 수 없다.
- 판정 경계: 부분 HarmonySet 통과는 선택한 실행 순서의 조화 증거일 뿐 전체 Aggregate 폐루프의 E8 완료를 뜻하지 않는다. 전체 Core 조화 캠페인은 별도 상태로 유지한다.
- 첫 적용: `area-harmony:nature-first-evening.v1`은 Nature 생활거점 Aggregate 안의 `nature-shelter-foundation`과 `nature-twilight-return`만 묶어 관찰 승리·안전 귀환과 후퇴·안전 귀환 두 순서를 검증한다. 현재 Shelter Presentation E7이 막혀 있어 `WaitingForMembers`다.

## D-266 E8은 개별 폐루프 안정, E9는 영역 조화와 사람 승인으로 판정한다

- 상태: `Accepted`
- 부분 대체: 아래 Goal 완전성의 `Goal WIP와 WI WIP는 각각 1` 조건은 D-284로 대체한다. E8·E9 증거 모델과 소유 경계는 유지한다.
- 결정일: 2026-08-27
- 증거 모델: 현재 판본은 `horizontal-dual-cycle-evidence.r3`다. PlayableUnit과 Codex Goal의 수직 성숙도는 E7에서 끝나며 Logic·Presentation 통합 단계는 낮은 값을 사용한다.
- E8 주체: E7을 통과한 PlayableUnit 하나마다 정확히 하나의 `PlayableUnitStabilityCampaign`을 둔다. 같은 후보 revision과 build에서 Logic 결정적 실행 최소 3회, Save·Restore·Replay, LocalProcess·RemoteHost 동등성, Presentation 실제 입력 최소 2회, Save 재진입과 차단 Console 오류 0건을 요구한다.
- E9 주체: 같은 AreaAggregate의 E8 Core 둘 이상을 `AreaHarmonySet`으로 묶는다. 권위·자원·시간·실패 회복·NPC 연속성과 공간 이동·카메라·입력·피드백 조화를 검증하고, 사람이 논리·표현 지표와 재미·몰입·완성도 4/5 이상, 열린 Blocking·Major 0건, 후보 revision·build hash를 명시적으로 승인해야 한다.
- 영역 경계: Nature·Farm·Hub는 Core가 둘 이상이므로 E9 후보를 둘 수 있다. Town·City는 현재 Core가 하나뿐이며 E9 형식에 맞추려고 기존 Core를 억지로 분할하지 않는다. 새 독립 Core 플레이 약속이 정의될 때까지 E9 후보를 보류한다.
- Goal 완전성: 16개 PlayableUnit을 Codex Goal 대장에 정확히 한 번 등록한다. 11개 Core를 Nature→Farm→Hub→Town→City 순서로 먼저 닫은 뒤 Nature·Farm·Town의 5개 Extension을 진행한다. Goal WIP와 WI WIP는 각각 1이다.
- 증거 무효화: 후보 revision이 바뀌면 영향받은 E8 반복 안정성과 E9 조화·사람 승인을 다시 연다. E8 실패는 같은 PlayableUnit의 가장 이른 E1~E7, E9 실패는 E9 모듈 또는 관련 PlayableUnit의 가장 이른 E1~E8로 귀속한다.
- 대체 관계: D-260의 E7 수직 종료와 E10 제한 운영 경계는 유지한다. D-260·D-264·D-265의 `E8=AreaHarmonySet`, `E9=별도 HumanPlaytestCampaign` 해석과 부분 조화의 E8 단계 표기는 이 결정이 대체한다. 과거 E8 NPC·E9 변화 적응과 `.e9-work-order.json`은 계속 판본화된 읽기 호환 자료다.
- 상세 기준: [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md), `eng/execution-ledgers/post-e7-evidence-campaigns.json`, `eng/execution-ledgers/codex-playable-loop-goals.json`을 따른다.

## D-267 권위 행위 기록은 엔진과 분리하고 분야 성장은 효과 계보에서 파생한다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 기록 경계: 권위 WI 결과는 Effect 적용과 같은 결과 계보에서 중립 `행위 발현 기록`으로 멱등 추가한다. 기록에는 엔진 이름 대신 변화 의미·영향 공간·WI·Command·Task·Effect·revision·hash를 둔다.
- 엔진 경계: LH·실외 배치·실내 배치·Sky 엔진은 파이프라인의 필수 단계나 라우터 대상이 아니다. 각 엔진이 자신의 생명주기에서 기록을 cursor로 읽고 자신의 파생·표현 상태만 변경하며 Simulation `WorldRevision`은 변경하지 않는다.
- 성장 경계: 플레이어 분야는 이해도·현장 숙련도·운영 숙련도를 분리한다. 성공은 `+2`, 의미 있는 실패·후퇴/복구는 `+1`, 취소·차단은 `0`이며 서로 다른 유효 반복에 감쇠나 일일 상한을 두지 않는다. 동일 기록은 중복 지급하지 않는다.
- NPC 경계: NPC 완료만으로 플레이어 숙련도를 지급하지 않는다. 명시적 플레이어 위임, 그 위임을 참조한 NPC 완료, 두 기록을 참조한 플레이어 `WI-REVIEW-01` 검토가 모두 있어야 운영 숙련 기여가 성립한다.
- 개인화 경계: 이해도는 권한 있는 사실을 숨기거나 주변 몬스터 기본 생성량을 바꾸지 않는다. 강조·설명·선택형 기회 후보만 조정한다.
- 저장·호환: `simulation-save.v28`은 체크포인트+tail 원장과 분야 Profile을 함께 hash로 봉인한다. 둘 중 하나만 있는 저장은 거부하고 v1~v27 읽기 호환을 유지한다.
- 대체 관계: D-261에서 지형 평탄화용으로 미리 적어 둔 `simulation-save.v28` 번호 예약만 이 결정이 대체한다. `WI-WORLD-09`의 의미와 구현 보류 상태는 유지하며 실제 저장 연결 시 다음 가용 wrapper 판본을 사용한다.
- 증거 경계: 현재 구현은 계약·도메인·fixture·저장 재생의 E1~E3 기반이다. 실제 WI Effect 자동 append, Session Adapter, 엔진 cursor 연결, Unity 상태창·Game View는 별도 상향 조립과 증거가 필요하다.
- 상세 기준: [플레이어 행위 기록과 분야 성장 통합 체계](../Architecture/플레이어행위기록과분야성장통합체계.md)와 `eng/execution-ledgers/player-domain-proficiencies.json`을 따른다.

## D-268 행위 파이프라인은 E Logic·Presentation과 E8~E10의 공통 통합 관문이다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 성숙도 경계: 행위 파이프라인은 Logic·Presentation을 대신하는 세 번째 E 축이 아니다. 실패는 같은 PlayableUnit Goal에서 책임이 시작되는 가장 이른 Logic 또는 Presentation E를 다시 연다.
- E5 권위 경계: 성공한 WI Confirm은 권위 결과와 같은 원자 경계에서 행위 원장을 반드시 추가한다. 플레이어 분야 성장은 별도 필수 결과가 아니며 직접 수행·결속 정책이 맞을 때만 적용하고, 그 외에는 사유가 있는 `NotApplicable`를 기록한다.
- 전수 적용: 16개 PlayableUnit과 55개 Loop-WI 조합을 결정적으로 파생해 검증한다. 현재 Adapter가 없는 조합은 완료로 간주하지 않고 차단 프로필로 유지한다.
- 표현 경계: Presentation E5 이상은 표현 엔진이 행위 원장을 cursor로 읽고 같은 `CommandId + AuthorityRevision`을 보존하며 Simulation `WorldRevision`을 바꾸지 않았다는 증거를 요구한다.
- 수평 증거: E8은 행위 원장·조건부 성장·Trace hash 동등성, E9는 같은 Profile bundle과 cursor 연속성, E10은 중복 기록·hash 불일치·cursor 재구축 실패가 없는 제한 운영을 요구한다. 새 관문 이전의 E8·E9 증거는 삭제하지 않지만 자동 승격 근거로 재사용하지 않는다.
- 상세 기준: [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md), [Codex PlayableLoop Goal 운영 체계](../Architecture/CodexPlayableLoopGoal운영체계.md), `eng/execution-ledgers/playable-loop-engine-interaction-validation.json`을 따른다.

## D-269 주제 기획 승인은 새 PlayableLoop Goal보다 앞선다

- 상태: `Accepted`
- 결정일: 2026-08-27
- 1:1 경계: 하나의 주제는 하나의 `PlayableUnit`에만 대응하며 하나의 장기 Goal과 한 번에 하나의 활성 WI로 구현한다. `AreaAggregate`·`WorldAggregate`에는 주제 기획 관문을 두지 않는다.
- 책임 분리: 주제 기획서는 플레이어 약속·재미·선택·대가·실패 회복을 소유한다. PlayableLoop·Goal 원장과 생성 상태판은 현재 Logic·Presentation·통합 E, 시험, 차단과 활성 WI를 소유한다.
- 활성화 관문: 새 Goal은 기획서 경로, 필수 절, source 참조, revision, SHA-256, 명시적 승인 근거가 모두 맞는 `Approved` 상태에서만 활성화한다. 승인 뒤 파일 hash가 달라지면 승인을 무효화한다.
- 이전 예외: 현재 활성 `playable-loop:nature-night-day2.v1`만 `LegacyActiveMigration`을 한시적으로 허용한다. 예외는 다른 루프로 이전할 수 없고 이 Goal을 완료하기 전에 `Approved`로 전환한다. 다음 대기 Goal부터 예외는 없다.
- 변경 규칙: 플레이어 약속이 바뀌면 새 주제·PlayableLoop·Goal을 만들고, 같은 약속의 정제는 기획 판본을 올려 다시 승인한 뒤 가장 이른 영향 E를 연다.
- 상세 기준: [주제 기획 기반 PlayableLoop 개발 체계](../Architecture/주제기획기반PlayableLoop개발체계.md), `eng/execution-ledgers/manage-playable-loop-topic-planning.ps1`을 따른다.

## D-270 집중 판정은 WI Task에 종속하고 명상은 횡단 성장축으로 둔다

- 상태: `Superseded by D-271`
- 결정일: 2026-08-28
- 행위 경계: `FocusTiming`은 새 WI가 아니라 기존 WI Task의 선택적 수행 품질 판정이다. 기본 행위 성공·실패와 분야 숙련을 재정의하지 않으며 Task가 권위 완료된 경우에만 발현한다.
- 성장 경계: 명상 숙련은 17번째 분야가 아니라 모든 분야를 가로지르는 성장축이다. 분야·세부 숙련·Challenge·완료 행위 기록 계보를 함께 보존한다.
- 판정 경계: 권위 Core는 백만분율 정수 왕복 위치를 계산한다. `Standard`, `Assisted`, `NeutralSkip`을 구분하고 건너뛰기는 실패 연속·명상·심리 수치를 변경하지 않는다.
- 심리 경계: 정확·양호는 플레이어 개인 NatureMind 회복, 같은 세부 숙련의 실패 두 번은 개인 위협에만 기여한다. 지역 전체 위협, 몬스터 생성, 자원량과 운영 상태는 직접 변경하지 않는다.
- 저장·증거: 명상 기여가 있는 저장은 `simulation-save.v29`로 봉인하고 v1~v28 읽기 호환을 유지한다. 서버 Logic E5 자동 시험은 Unity 실제 입력·상태창·Game View Presentation E7을 대신하지 않는다.
- 상세 기준: [벌목 집중과 명상 숙련 체계](../Architecture/벌목집중과명상숙련체계.md)를 따른다.

## D-271 모든 플레이어 WI를 집중 Profile로 분류하고 명상 성장은 행위 원장 계보에 결속한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 적용 경계: 플레이어 분야 카탈로그에 결속된 모든 WI는 `Applied`, `PendingProfile`, `NpcOnly`, `Automatic`, `Excluded` 중 하나로 분류한다. Profile이 승인되지 않은 WI에는 보상을 추정하지 않고 판본화된 `NotApplicable`을 남긴다.
- 실행 경계: 공통 실행 파이프라인은 `FocusEvidenceCollect`를 권위 확정 전에, `MeditationProgressionApply`를 행위 기록과 분야 성장 뒤에 기록한다. 분야·명상 기여와 허용된 개인 심리 효과는 같은 완료 행위·Command·Revision 계보를 사용한다.
- 보상 경계: 첫 판본은 Perfect `250 Milli`, Good `100 Milli`의 작은 명상 경험과 같은 양의 개인 회복만 지급한다. 집중 실패만으로 위협을 올리지 않으며 기본 WI 결과와 물질 보상을 재정의하지 않는다.
- 소비 경계: LH·Sky·실외·실내·World Presentation은 행위 원장 또는 같은 Revision 상태 사본을 자신의 생명주기에서 독립 조회한다. 엔진은 파이프라인에 종속되어 즉시 호출될 필요가 없고 권위 Revision을 변경할 수 없다.
- 저장·증거: 권위 원본은 `명상경험Milli`이며 `simulation-save.v29`가 Challenge·명상 기여·행위 원장 계보를 봉인한다. 자동 Logic E5는 Unity 실제 입력·상태창·Game View Presentation E7을 대신하지 않는다.
- 상세 기준: [전 행위 몰입과 명상 숙련·행위 원장 통합 체계](../Architecture/전행위몰입과명상숙련행위원장통합체계.md)와 [벌목 집중과 명상 숙련 체계](../Architecture/벌목집중과명상숙련체계.md)를 따른다.

## D-272 Solo를 유지하면서 공식 지속 세계와 서버 권위 비공개 협동방을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 실행 방식: Solo는 `LocalProcess`를 유지한다. 비공개 협동방과 공식 지속 세계는 모두 개발사 `RemoteHost`의 같은 Simulation Core를 권위로 사용하며 플레이어 PC를 권위 P2P Host로 만들지 않는다.
- 세계 수명: 비공개방은 2~4명 초대형이고 전원 이탈 시 상태 사본을 정지한다. 공식 세계는 접속자가 없어도 활성 상태 사본을 유지하며 첫 기준은 Nature AreaSet 4개, AreaSet당 32명, 전체 128명이다. 이 숫자는 첫 용량 계약이며 운영 부하 검증 완료를 뜻하지 않는다.
- 세션 결속: 각 온라인 AreaSet은 세계·AreaSet·규칙 revision에서 계산한 서로 다른 `RemoteHost` Simulation 세션 식별자를 상태 사본에 봉인한다. 연결 참가자의 명시적 Provision 요청은 해당 식별자로 프로세스 내부 Nature Runtime을 결정적으로 준비하고 참가자별 Actor·인벤토리·도끼·Nature Mind를 등록한다. 같은 AreaSet 참가자는 공식 지속 세계와 비공개 협동방 모두 하나의 세션을 공유하며, 다른 AreaSet 세션의 행위 기록은 현재 기여로 받아들이지 않는다.
- 공유 경계: 서로 다른 온라인 세계 사이에는 서버가 권위 행위 기록으로 검증한 명상 경험만 계정 원장에 공유한다. 아이템·재료·장비·분야 숙련·건축물·NPC·위협·세계 시간·공동 목표는 세계별로 격리하고 Solo 저장을 온라인에 import하지 않는다.
- 협동 경계: 첫 소통은 자유 텍스트·음성 없이 고정 신호만 허용한다. 2~4명 파티는 같은 AreaSet에서 성립하며 AreaSet 전환은 전원 수용 가능할 때만 같은 revision에서 원자적으로 수행한다.
- 권위·보안: HTTP와 SignalR은 JWT 행위자를 서버에서 확정하고 예상 World·AreaSet·Session revision과 명령 payload hash를 검증한다. 공개 벌목 route는 대상·Challenge·입력 시점만 받고 Actor·완료 시간·보상량은 서버가 정한다. 완료 행위 기록의 계정 명상은 서버가 자동 인계하며 클라이언트가 임의 명상·목표 수치를 제출하는 공개 route는 두지 않는다.
- 저장·재접속 경계: 기존 개인 `simulation-save.v29`와 별도로 `simulation-online-world-checkpoint.v1`을 Simulation Session DB에 보관한다. 협동 Actor·플레이어별 분야/명상 Profile·행위 기록 hash는 v29 Save/Replay로 복원하며 재접속은 마지막 cursor 이후 권위 기록만 같은 hash chain 순서로 반환한다. 현재 프로세스 재접속과 저장·재생 결정성은 검증했지만 온라인 Runtime의 운영 재기동 자동 적재, Unity 다중 입력·Play Mode·Game View와 운영 배포는 별도 증거다.
- 성숙도 경계: 첫 협동 WI는 `WI-NATURE-06 벌목`으로 제한한다. 단일 권위 `ActiveWork` 충돌, 두 Actor의 순차 완료, 각 Actor 및 계정 명상 계보를 자동시험으로 검증했지만 이 기반은 현재 PlayableLoop Goal이나 E 증거를 자동 승격하지 않는다.
- 상세 기준: [공식 지속 세계와 비공개 협동방 체계](../Architecture/공식지속세계와비공개협동방체계.md)를 따른다.

## D-273 전술 시점은 자기 캐릭터 선택·이동과 카메라 탐색 입력을 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 플레이어 약속: F3 전술 시점에서 자기 캐릭터를 좌클릭 또는 빈 지면 박스 드래그로 선택하고, 선택된 캐릭터는 지면 우클릭 목적지나 방향키로 이동한다.
- 입력 경계: 방향키는 선택된 자기 캐릭터 이동, WASD는 RTS 카메라 이동을 담당한다. Backspace를 450ms 안에 두 번 누르면 카메라를 자기 캐릭터에 재집중하며 기존 F 재집중은 호환 입력으로 유지한다.
- 선택 경계: 첫 판본은 자기 캐릭터 한 명만 선택한다. 온라인 참가자의 위치·선택·다중 유닛 명령은 포함하지 않고 Farm 농지 선택·작업 초안 입력을 기존 우선순위 안에서 보존한다.
- 권위 경계: 선택·CharacterController 이동·카메라·선택 원·목적지는 `PresentationOnly`이며 `WorldTick`과 `WorldRevision`을 변경하지 않는다. 세계 변화 증거는 이동 뒤 실행하는 `WI-NATURE-05 벌목 도끼 획득` Preview·Confirm·행위 기록에서 성립한다.
- Goal 경계: 새 승인 주제 `topic:nature-tactical-self-navigation.v1`과 `playable-loop:nature-tactical-self-navigation.v1`을 활성 Goal로 둔다. 기존 `nature-night-day2` 구현·증거는 삭제하지 않고 `Deferred`로 보존한다.
- 대장 경계: 전술 Core 추가 뒤 전수 관문은 17개 PlayableUnit과 56개 Loop-WI 조합이다.
- 대체 관계: D-116의 전술 카메라·PresentationOnly 원칙은 유지하고, D-116의 WASD·방향키 카메라 이동과 단일 클릭 선택 입력 배정만 이 결정이 대체한다. D-266의 16개 Goal 수와 D-268의 16개 PlayableUnit·55개 Loop-WI 고정 수는 이 결정이 대체한다.
- 상세 기준: [Nature 전술 자기 캐릭터 이동](../Architecture/PlayableLoops/Nature전술자기캐릭터이동.md)과 [Unity Landscape Rendering Pipeline](../Architecture/UnityLandscapeRenderingPipeline.md)을 따른다.

## D-274 다섯 영역 위치를 H5 상대좌표로 고정하고 City는 예약 상태로 분리한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 위치 권위: Nature·Farm·Hub·Town·City는 `ScenarioLocalMeters`의 `WorldAreaAnchor`로 고정한다. E6·DEM·도로·토지피복은 앵커를 이동시키지 않으며 위치 변경은 새 WorldLayout revision으로만 수행한다.
- 영역 분리: 기존 네 Actual E5 AreaSet 좌표를 보존하고 `CityHub`는 Hub의 읽기 호환 별칭으로만 유지한다. 신규 H5 결과의 정규 역할은 `Hub`이며 City는 `area-set:sim:pyeongchang:city-service.v1`로 분리한다.
- 예약 경계: City는 고정 위치와 특징 Profile만 가진 `Reserved` 앵커다. 메타데이터 선행 적재는 허용하지만 Actual E5 Graph와 승인 회랑 전에는 통행·활성화·WI 실행을 거부한다. Town–City는 같은 이유로 `ReservedCorridor`다.
- 영역 특징: Nature는 숲·회복·위협, Farm은 십자 농로·네 밭·완만한 작업 지형, Hub는 평탄 물류 동선, Town은 저층 시장 생활권, City는 고밀도 서비스권 규칙을 앵커 Profile로 소유한다.
- Goal 경계: 폐루프는 고정 앵커 안에서 H1~H3와 Logic·Presentation을 성숙시키며 공간 고정만으로 E를 승격하지 않는다. 현재 전술 Goal을 먼저 닫고 Day2→작업대→Nature 현장 왕복→Farm→Hub→Town→City Core 순서를 유지한다.
- 상세 기준: [H5 세계 배치와 E6 플레이 전 정제·선택형 GIS 결속](../Architecture/H5세계배치와선택형E6결속.md)을 따른다.

## D-275 플레이 폐루프 기획과 개발은 승인 기획서를 저장소 인계면으로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 스레드 책임: 기획 스레드는 플레이어 약속·재미·선택·대가·성공·실패·회복·귀환과 WI·Logic·Presentation·H·저장·권위·제외 범위를 구체화한다. 별도 요청이 없으면 제품 코드를 구현하거나 활성 Goal을 교체하지 않는다.
- 개발 입력: 개발 스레드는 대화 기억이나 대기열 제목을 구현 명세로 사용하지 않는다. 생성 Goal 상태판, `Approved` 기획서와 일치하는 revision·SHA-256·승인 근거, E7 작업 명세, 파이프라인 프로필과 활성 WI를 순서대로 읽고 승인 범위만 구현한다.
- 차단 규칙: 기획서가 없거나 hash가 다르거나 핵심 선택·대가·실패·회복·귀환이 미정이면 Goal 활성화와 구현을 차단한다. 개발 중 기획 충돌을 발견하면 `openFeedbackItems`와 가장 이른 재개 E를 기록하고 기획 revision 재승인으로 돌려보낸다.
- 개발 재량: 플레이어 약속과 권위·저장 경계를 바꾸지 않는 내부 클래스 분리, 시험 보조 코드, 성능 최적화는 개발 스레드가 결정할 수 있다. 플레이어 경험 의미나 WI 책임이 달라지면 기획 재승인이 필요하다.
- 증거 경계: 기획서는 현재 E나 시험·Runtime·Game View 완료를 소유하지 않는다. 구현·증거 상태는 PlayableLoop·Goal·EvidencePackage 원장과 생성 상태판만 소유한다.
- 대체 관계: D-269의 기획 승인 우선 원칙을 유지하고, 당시 특정 활성 Goal에만 붙였던 이전 예외 설명은 현재 원장과 생성 상태판을 읽는 일반 규칙으로 대체한다.
- 상세 기준: [주제 기획 기반 PlayableLoop 개발 체계](../Architecture/주제기획기반PlayableLoop개발체계.md), [Codex PlayableLoop Goal 운영 체계](../Architecture/CodexPlayableLoopGoal운영체계.md), `AGENTS.md`를 따른다.

## D-276 구체 설계는 전문 심화 연구로 분기한 뒤 PlayableLoop에 재결속한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 체계 경계: 건물·공간·배치·애니메이션의 구체 설계를 새 E·G·H·Goal로 만들지 않는다. PlayableLoop 기획에서 필요한 연구만 분기하고 검토된 기준선을 같은 기획서와 E7 작업 명세에 다시 결속한다.
- 필요성 판정: 각 분야는 `Required` 또는 사유 있는 `NotRequired`로 판정한다. 플레이어 선택·대가·회복, H Capability, 권위·저장, 실제 입력·카메라·접촉·판독 또는 자산 fallback에 영향을 주면 `Required`다.
- 상태 관문: 연구 상태는 `Required → Draft → Reviewed → Accepted → Obsolete`를 사용한다. 모든 `Required` 연구가 `Accepted`되고 기획서에 반영되기 전에는 해당 신규·의미 변경 기획을 `Approved`로 올리지 않는다.
- 증거 경계: 연구 `Accepted`는 설계 기준선 승인일 뿐 E 승격이나 코드·Scene·Play Mode·Game View 완료가 아니다. 실행 증거는 Goal·E7 작업 명세·EvidencePackage만 소유한다.
- 피드백 경계: 구현이 기준선과 다르면 구현 결함으로 고치고, 기준선 자체가 틀리면 영향받은 연구와 가장 이른 Logic 또는 Presentation E를 다시 연다. 개발 스레드는 충돌한 연구를 임의로 선택하지 않는다.
- 적용 경계: 다음 신규 기획서와 의미 변경 revision부터 필수다. 현재 활성 Goal의 이미 승인된 범위를 소급 무효화하지 않으며 첫 적용 후보는 Nature 보관·수면·다음 날 계획 반환이다.
- 상세 기준: [PlayableLoop 전문 심화 연구 분기·재결속 체계](../Architecture/PlayableLoop전문심화연구분기재결속체계.md), [PlayableLoop 주제 기획서 템플릿](../ProjectOverview/templates/PlayableLoop주제기획서템플릿.md), [전문 심화 연구 템플릿](../ProjectOverview/templates/PlayableLoop전문심화연구템플릿.md)을 따른다.

## D-277 짧은 정차·대기 문답을 PlayableLoop 기획의 공식 Draft 절차로 사용한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 진행 단위: 기획 스레드는 한 회차에 핵심 질문 하나를 제시한다. 답변 뒤 사용자의 의도, 확정 후보, 미정·충돌, 영향 대상과 다음 질문을 정리하고 의미를 확인받는다.
- 안전 경계: 이동 중 즉시 답변을 요구하지 않는다. 사용자가 안전하게 정차했거나 대기 중일 때만 이어가며 긴 비교·화면 확인은 별도 회차로 보낸다. 게임 기획에 불필요한 실제 배달 위치·고객·연락처·운행 정보는 기록하지 않는다.
- 저장 경계: 대화 원문이나 AI 기억은 개발 인계 자료가 아니다. 확인된 결정은 판본화된 문답 정밀화 기록에 요약하고 주제 기획서의 `sourcePlanningDocumentRefs`로 결속한다.
- 승인 경계: 문답의 `Confirmed`·`Closed`는 E 증거나 기획 승인 상태가 아니다. 플레이어 약속과 선택·대가·회복·귀환, WI, Logic·Presentation, H, 저장·권위와 전문 연구 판정을 기획서로 합성하고 별도 승인해야 한다.
- 재개 경계: 기록은 현재 질문 ID, 마지막 확인 질문 ID, 미정과 다음 질문을 보존한다. 대화가 중단되면 같은 위치에서 재개하고 이미 확인한 질문을 처음부터 반복하지 않는다.
- 상세 기준: [PlayableLoop 문답 정밀화 체계](../Architecture/PlayableLoop문답정밀화체계.md)와 [문답 정밀화 기록 템플릿](../ProjectOverview/templates/PlayableLoop문답정밀화기록템플릿.md)을 따른다.

## D-278 명상은 실행 WI가 아닌 비실행 상위 WI군으로 구체 플레이어 행위를 묶는다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 분류 경계: 모든 구체적 플레이어 WI는 `wi-family:meditation` 상위 분류 후보로 전수 판정한다. `PlayerDirect`, `PlayerOrOperation`, `LearningOnly`는 `Bound`, `OperationOnly`와 의미 있는 플레이어 행위가 아닌 WI는 사유 있는 `NotApplicable`로 기록한다.
- 실행 경계: 명상 WI군은 `MetadataOnly`이며 자체 `Preview`, `Confirm`, `Task`, `Effect`, 비용·예약 또는 `WorldRevision`을 만들지 않는다. 구체 WI가 권위 완료된 뒤 같은 `ActionRecord`를 기존 `MeditationProgressionApply`가 소비한다.
- 책임 경계: 명상 WI군 결속은 구체 WI의 결과·물질 보상·분야 숙련을 재정의하지 않고 개별 집중 Profile 승인도 대신하지 않는다. 실제 Actor가 NPC이거나 자동 실행이면 Player 결속 가능 WI라도 개인 명상 기여를 지급하지 않는다.
- 호환 경계: 첫 리팩토링은 `meditation-wi-family-catalog.r1`과 명상 적용 관문만 추가한다. 저장 payload와 `simulation-save.v29`, 현재 Perfect·Good 수치는 유지한다. `Q-122`에서 확인한 의미 있는 완료의 기본 회복은 별도 Profile revision·상한·감쇠·Save 증거를 갖춘 후속 변경으로 남긴다.
- 상세 기준: [전 행위 몰입과 명상 숙련·행위 원장 통합 체계](../Architecture/전행위몰입과명상숙련행위원장통합체계.md)와 [플레이어 내면·명상 문답](../Architecture/PlayableLoops/PlanningSessions/플레이어내면명상/player-mind-meditation.inquiry.r1.md)을 따른다.

## D-279 Farm 작업 참여는 초기 Solo 가능성과 후속 협력 성장을 공통 비실행 정책으로 연결한다

- 상태: `Accepted`
- 결정일: 2026-08-28
- 시작 경계: 첫 농지의 기본 적합도 조사와 소규모 개간은 플레이어 혼자 완결할 수 있다. NPC·다른 플레이어·대형 장비가 없다는 이유로 초반 Farm 진행을 차단하지 않는다.
- 성장 경계: 면적과 급경사·암석·배수·먼 수원 같은 현장 난도가 증가할수록 시간·피로·도구 내구도·부상 부담이 커지고 협력자·전문가·장비의 효율이 높아진다. 현재 도구로 물리적으로 수행할 수 없는 경우만 별도 차단한다.
- 권한 경계: 잡초 제거·정리·소량 운반·이미 Confirm된 작업 보조는 기본 자동 허용하되 플레이어가 끌 수 있다. 자원 소비·수확/처분·지형 변경·건설/철거·새 작업 확정은 명시적 Confirm 또는 사전 위임을 요구한다.
- 호혜 경계: 가벼운 도움은 즉시 결제 대신 호혜 기여로 기록하고 식사·선물·맞도움 등으로 보답할 수 있다. 전문 기술·장시간·위험·장비 노동은 시작 전에 보수·보급·시간·위험 부담을 합의한다.
- 구현 경계: 첫 판본 `work-participation-policy.r1`은 기존 Farm 작업·NPC 배정·협동 건설 기여가 소비할 비실행 판정 계약이다. 자체 WI·Preview·Confirm·Task·Effect·WorldRevision·Save 상태를 만들지 않으며 활성 Goal이나 Farm E를 승격하지 않는다.
- 상세 기준: [Q-001~Q-130 개발 뼈대 인계](../Architecture/Q001-Q130개발뼈대인계.md)와 [건물·공간·배치 문답](../Architecture/PlayableLoops/PlanningSessions/건물공간배치/building-spatial-placement.inquiry.r1.md)을 따른다.

## D-280 Presentation E4는 적용 가능한 자산·배치 후보를 E5 준비 인계로 남긴다

- 상태: `Accepted`
- 결정일: 2026-08-29
- 적용 경계: 공간·물체·소품·Actor·UI 시각 자료가 플레이어 판독에 필요한 WI의 Presentation E4에 적용한다. 비공간·비시각 WI는 사유 있는 `NotApplicable`을 사용할 수 있다.
- 인계 내용: 플레이어 판독 순간, 필요한 H 역할·능력, 의미 기반 `VisualKey`, 주·대체·fallback 자산 후보, 배치·`InteractionAnchor` 의도, 후보 revision·fingerprint, `Ready/Conditional/Blocked` 준비 상태와 열린 결함을 `presentationE4Preparation`으로 남긴다.
- E5 경계: 자산 후보 조사와 문서 결속은 E5 증거가 아니다. E5는 E4 인계를 소비해 실제 Prefab 또는 대체 표현, World 배치, 활성 `Renderer`·`Collider`·`Bounds`, 권위 상태와 WI 발현을 검증한다.
- 재개 경계: E5에서 새 자산이나 H 제약을 발견하면 임시로 굳히지 않고 Presentation E4를 다시 열어 후보 판본과 배치 의도를 개정한다.
- 첫 적용 후보: `WI-ACTOR-03 지식 습득`은 Town 열린 책을 주 후보, City·Generic 종이와 Construction 클립보드를 대체 후보로 조사했지만 실제 Scene·Collider·Game View는 미검증이므로 `Conditional`이다.
- 상세 기준: [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md)와 [플레이 폐루프 논리·표현 이중 순환 체계](../Architecture/플레이폐루프논리시각이중순환체계.md)를 따른다.

## D-281 문답 질문은 주제·깊이·전체 번호를 함께 표시하고 깊이별 Evidence 준비 전망을 제공한다

- 상태: `Accepted`
- 결정일: 2026-08-29
- 질문 식별: 새 질문은 `Q-{주제}-D{1~5}-{주제내연번} · 전체 Q-{전체연번}`으로 표시한다. 기존 `Q-001` 참조는 전체 호환 번호로 보존하며 재번호화하지 않는다.
- 묶음 답변: 기본은 핵심 질문 하나지만 사용자가 요청하면 같은 주제·깊이의 독립 질문 3~5개를 묶고 `1 3 2 2 1` 형식의 순서형 답변을 허용한다.
- 깊이·Evidence 관계: D1은 E1, D2는 E2, D3은 Logic E3~E4·Presentation E3, D4는 Presentation E4와 양 궤적 E5 착수, D5는 E6~E7 및 후속 E8~E9 캠페인 준비 전망을 제공한다.
- 증거 경계: D는 예상 구현 준비 범위이며 실제 E가 아니다. 실제 Logic·Presentation·통합 E는 PlayableLoop 대장, E7 작업 명세와 EvidencePackage에서만 읽고 질문 답변만으로 승격하지 않는다.
- 표시 계약: 각 주제 문답은 현재 D, 예상 E 준비 범위, 실제 검증 E, 가장 이른 다음 차단을 함께 보여 준다.
- 기계 단일 원본: `eng/execution-ledgers/playable-loop-inquiry-depths.json`과 `manage-playable-loop-inquiry-depths.ps1`을 따른다.
- 첫 적용: [Farm 병영·방위 문답](../Architecture/PlayableLoops/PlanningSessions/Farm병영방위/farm-barracks-defense.inquiry.r1.md)은 D1·D2 확인, D3 진행, 실제 E 미등록 상태를 분리해 기록한다.

## D-282 신규 Synty 환경·애니메이션 팩은 WI·H 표현 원천으로 분리 결속한다

- 상태: `Accepted`
- 결정일: 2026-08-29
- 재고 경계: 13팩 Prefab `4,211`개는 기존 12개 기능 모듈 대장에서 관리하고, Base Locomotion·Emotes and Taunts·Sword Combat Clip은 별도 애니메이션 원천 프로필로 관리한다. 애니메이션 팩의 예제 Prefab 수를 동작 가용량으로 사용하지 않는다.
- 영역 경계: Alpine Mountain·Dungeon Realms·Dwarven Dungeon Map은 새 AreaSet이나 Simulation 상태를 만들지 않는다. 기존 PlayableLoop의 WI 순간과 H Capability가 요구할 때만 표현 후보로 선택한다.
- 애니메이션 결속: Actor 동작은 `AnimationRole → ActionCue → Clip 후보 → Rig/Avatar/root motion/Controller 호환 → Adapter 결속 → 실제 전이·중단·귀환` 순서로 검증한다. Clip·Animation Event는 피해·수확·건설·회복·재고 같은 권위 결과를 만들지 않는다.
- 승인 경계: 새 팩은 모두 `needs-review / PresentationOnly`에서 시작한다. 원천 조사와 후보 등록은 Presentation E4 준비이며, 실제 Prefab·Clip·대상 Rig·H 배치·입력·Game View와 권위 불변이 확인되기 전에는 E5 이상을 선언하지 않는다.
- fallback 경계: 공급자 Prefab이나 Controller를 직접 고쳐 게임 규칙을 붙이지 않는다. 프로젝트 Wrapper·명시적 Adapter·procedural fallback을 사용하며 fallback 사용과 미확인 호환을 숨기지 않는다.
- 첫 적용 순서: Base Locomotion의 현재 플레이어 이동, Sword Combat의 Nature 황혼 전투, Emotes and Taunts의 명상·협력 피드백 순으로 좁은 수직 조각을 검증한다.
- 상세 기준: [플레이 폐루프 Synty 표현 모듈 체계](../Architecture/플레이폐루프Synty표현모듈체계.md)와 `eng/execution-ledgers/synty-asset-functional-modules.json`을 따른다.

## D-283 WI 후보 등록은 상위 분류·특화·결과 투영과 실행 행동을 구별한다

- 상태: `Accepted`
- 결정일: 2026-08-30
- 근거: 신규 후보 41개를 상하위 관계로 분류하고 중복을 제외해 등록하라는 사용자 요청.
- 비실행 상위 분류는 Preview·Confirm·Task·Effect를 소유하지 않는다. 공통 행동의 특화 관계 역시 부모와 자식을 중복 실행하는 호출 관계가 아니다. 작업 순서는 기존 흐름 대장이 별도로 소유한다.
- 같은 표현이나 영역 이름만으로 WI를 합치지 않는다. 의도·변경 원장·주요 결과가 같은 경우에만 특화 프로필로 연결하고, 원문 후보 ID와 질문 출처는 보존한다. 결과 조회와 복합 절차는 실행 WI로 위장하지 않는다.
- 첫 감사 결과: 신규 행동 33개, 중복 특화 프로필 2개, 상위 분류 5개, 결과 투영 1개. 기획이 미확정인 하위 행동·비용·보상은 이번 등록으로 승인하지 않는다.
- 신규 행동은 E0 미착수로 등록하며 등록과 실제 구현·명상 성장 결속·기획 승인·Evidence 승격을 분리한다. 기존 활성 Goal/WI, Save, API와 Unity 장면은 변경하지 않는다.
- 단일 근거: `eng/execution-ledgers/world-interaction-registration-relations.json`; 사람이 읽는 [등록 결과](generated/world-interaction-registration.md)는 생성기로 관리한다.

## D-284 Goal·WI 고정 WIP 상한을 없애고 실제 의존성과 변경 소유권으로 병렬 개발한다

- 상태: `Accepted`
- 결정일: 2026-08-30
- 근거: WIP 1 원칙 때문에 독립적인 구현까지 중단되는 문제를 해소하고 분야별 병렬 개발 결과를 `개발` 스레드에서 통합하라는 사용자 승인.
- 소유 경계: Goal 하나는 계속 PlayableUnit 하나를 소유한다. 전역·스레드별 Goal/WI 고정 상한은 없애며 여러 Goal, 같은 Goal의 여러 WI, 같은 WI의 비중첩 하위 작업을 허용한다. 작업 개수는 표시 정보이지 실패 조건이 아니다.
- 실행 경계: 작업마다 담당, 기준 revision/hash, 작업 명세, 수정 경로, 공유 계약, 선행 의존성, 승인 범위와 E 상한, 통합 상태를 기록한다. 기획 미승인·hash 불일치·실제 선행 미완료·조율되지 않은 수정 충돌만 해당 작업을 막으며 다른 WI가 활성이라는 이유만으로 차단하지 않는다.
- 통합 경계: 같은 파일·공유 계약의 쓰기는 변경 소유자를 정해 조율한다. 분야별 스레드는 독립 구현·시험 결과를 인계하고 `개발` 스레드가 최종 통합을 책임진다. 호환 결과의 묶음 통합과 독립 검증의 병렬 실행은 허용한다. 별도 worktree만으로 공유 계약 충돌이 해결된 것으로 보지 않는다.
- 증거 경계: 모든 작업의 기획 승인·Required 연구·E 상한·행위 기록·Logic/Presentation 관문은 유지한다. 같은 WI의 하위 결과는 같은 WI·판본에 결속하며 병렬 등록·작업 완료로 Evidence를 자동 승격하지 않는다. 공간 후보와 실제 Scene·Game View 증거도 계속 구별한다.
- 이전 경계: 기존 진행 작업·증거·대기 상태를 보존하고 대기 Goal/WI를 자동 활성화하지 않는다. 단일 `activeGoal`은 대표 표시의 호환 조회로만 유지하고 실행 판단은 v4 원장의 전체 작업 목록을 읽는다. 게임 Save/API 의미는 변경하지 않는다.
- 대체 관계: D-248과 D-266의 Goal/WI WIP 1 조건 및 이를 재서술한 현행 개발 지침을 대체한다. 역사 문서의 당시 WIP 표기는 과거 근거로 보존한다. 기획 승인·단일 PlayableUnit 소유·E7 수직 종료·E8/E9 검증은 대체하지 않는다.
- 상세 기준: [Codex PlayableLoop Goal 운영 체계](../Architecture/CodexPlayableLoopGoal운영체계.md), [주제 기획 기반 PlayableLoop 개발 체계](../Architecture/주제기획기반PlayableLoop개발체계.md), `eng/execution-ledgers/codex-playable-loop-goals.json`.

## D-285 전문 작업 결과는 개발에서 통합·검증한 뒤 기획으로 반환한다

- 상태: `Accepted`
- 결정일: 2026-08-30
- 근거: 배치·애니메이션 등 분야별 스레드 결과를 개발이 통합 정리하고 기획에 반환하도록 하자는 사용자 요청과 계획 구현 승인. 개발의 규칙 구현·통합 겸임을 선택했다.
- 역할: 기획은 플레이어 약속·문답·변경 승인을, 개발은 Simulation·WI 및 통합을, 월드·공간·배치와 애니메이션 담당은 독립 전문 산출물을 소유한다. 기존 스레드를 재사용하며 ‘하위’는 작업상 인계 관계다.
- 반환: 전문 담당은 개발에 원문 인계·판본/hash·변경 범위·시험·미검증·차단을 전달한다. 개발은 실제 통합 결과, 미통합 준비물, 증거, 필요한 기획 판단, 다음 작업으로 정리해 기획에 반환한다. 모든 전문 작업 완료를 기다리지 않고 영향받은 작업만 차단한다.
- 권한: 기존 `workItems`·통합 승인 기록·E 상한을 사용한다. 후보 조사·개별 시험·문서 반영은 실행 통합이나 E 승격이 아니다. 이 결정으로 미승인 WI·새 스레드·반복 실행·commit/push를 자동 허용하지 않는다.
- 소유권: 공통 실행 원장·생성물·통합 상태판은 개발이 유지한다. 기획 승인 문서와 전문 산출물은 각 담당이 유지하며 공유 문서·Scene·Editor의 실제 쓰기는 조율한다.
- 보완 관계: D-284의 무상한 병렬·실제 의존성 원칙을 유지하고 전문 결과의 집계·기획 반환 책임을 구체화한다. API·Save·Goal 스키마는 변경하지 않는다.
- 상세 기준: [Goal 운영 체계](../Architecture/CodexPlayableLoopGoal운영체계.md#전문-담당과-기획-환류), [개발 통합 상태판](개발통합상태판.md).

## D-286 기존 WI를 Session·배치·저장에 연결하는 E5 전달을 우선한다

- 상태: `Accepted`
- 결정일: 2026-08-30
- 근거: 사용자가 준비된 WI와 Farm 병행, 실제 씬 배치·실행 확인, 핵심 상태 저장 포함을 선택하고 E5 개발 계획 구현을 승인했다.
- 우선 범위: WI-ACTOR-03 지식 습득과 WI-COMMUNITY-VISITOR-STAY 응대를 독립적으로 E5까지 연결하고 Farm WI-FARM-01~04와 강변 H2 배치 기반을 병행한다. 원천 기획은 지식 r5/방문자 r3/Farm r1과 Required 연구에 결속한다.
- 증거: 기존 E7 최종 목표는 유지하고 이번 전달 상한만 E5다. Logic/Presentation은 따로 검증하고 통합 E는 낮은 값이다. Scene·자산·캡처만으로 E5나 E7을 선언하지 않으며 지식 한 WI의 완료를 전체 약초 폐루프 완료로 확대하지 않는다.
- 구현: 독립 메모리 원장을 Session의 상태·행위·명령 기록·Save/Replay와 연결한다. 이전 Save 읽기·hash, Local/Remote 같은 Core, 현재 입력·canonical SimulationWorldShell을 보존한다. 핵심 저장 복원과 실제 씬 실행·대표 Game View를 이번 완료 조건에 포함한다.
- 공간: Farm net10 후보는 현행 호환 소스/변환으로 이식한다. 후보 원본과 정규형 계획 hash를 구분하고 실제 자산·좌표·H·Bounds·통로를 검증한다. 후보 수신/기술 변환 승인을 실제 패턴 승인으로 계산하지 않는다.
- 병렬: D-284/D-285를 유지한다. 개발이 공통 원장·코드·Scene을 통합하고 전문 담당은 비중첩 범위를 진행한다. 신규 E3 WI보다 준비된 기존 WI의 실행 연결을 우선하되 미정/보류 WI의 자동 활성화는 허용하지 않는다.
- 제외: 새 약효·체류 기간·회원 정책·대규모 자유 건설·공공 API 실호출·운영 DB·멀티플레이 화면·E6/E7 자동 승격·commit/push.
- 상세 기준: [기존 WI 세계 발현 E5 개발 계획](../Architecture/기존WI세계발현E5개발계획.md).

## D-287 행동 체력은 자연·휴식·물품으로 회복하고 성장에 따라 최대치를 늘린다

- 상태: `Accepted` (기획 방향; 실행 수치·조건은 아직 Draft)
- 결정일: 2026-08-30
- 근거: Farm 새 밭 연속 두 주기에105체력이 필요하나 상한100·회복 없음이라는 반환에 대해 사용자가 지속 자연 회복, 휴식 회복, 포션 회복, 레벨 상승에 따른 상한 증가를 요청했다.
- 의미: 이 결정의 체력은 행동 체력 Stamina다. Health 생명력·질병·내면 회복/위협 수치를 하나로 합치지 않는다. MedicalRest의 기존 부상 치료 의미도 조용히 바꾸지 않는다.
- 확정: 자연 회복·명시적 휴식·회복 물품 소비의 세 경로와 성장에 따른 최대치 확장을 둔다. 현재값과 최대치는 구별한다.
- 미정: 자연 회복 허용 활동·속도, 휴식 계약·속도, 포션 효과/소비/획득 조건, 레벨/성장의 입력·상한 증가량·레벨 상승 시 현재값 처리. 이 수치를 개발이 임의 확정하거나 기존 Save 의미를 바꾸지 않는다.
- 연결: 공통 Simulation 시간·명령/효과·행위 기록·Save/Replay·같은 Local/Remote Core를 사용한다. 기존 E5 승인7문서와 hash는 이번 방향 결정만으로 바꾸지 않으며 실제 회복 구현은 후속 승인 명세에 결속한다. 무관한 작업은 계속한다.
- 상세: [플레이어 행동 체력 회복과 성장](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-288 자연 체력 회복은 걷기·대기 중 허용하고 노동·질주·전투 중 중단한다

- 상태: `Accepted` (자연 회복의 활동 조건)
- 결정일: 2026-08-30
- 근거: 걷기·대기 중 회복, 노동·질주·전투 중 중단 제안에 사용자가 자연 회복은 그렇게 하겠다고 승인했다.
- 확정: 행동 체력 Stamina의 자연 회복은 걷기·대기 중 진행하고 노동·질주·전투 중에는 멈춘다. 전투 중 걷기·대기를 회복 허용으로 해석하지 않는다.
- 보완: D-287의 미정 중 자연 회복 허용 활동을 확정한다. 회복 속도·재개 지연·휴식 조건·포션·성장 수치는 아직 미정이며 기존 MedicalRest·Health·내면 회복 의미는 유지한다.
- 구현 경계: 이 조건을 기획 r2에 반영한다. 기존 E5 승인 기획/hash·작업 명세·Save·코드·E 판정은 자동 변경하지 않는다. 개발의 비의존 작업은 계속한다.
- 상세: [플레이어 행동 체력 회복과 성장 r2](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-289 제자리 휴식은 시설 없이 허용하고 안전한 오두막은 회복 효율을 높인다

- 상태: `Accepted` (휴식 장소·상대적인 회복 효율)
- 결정일: 2026-08-30
- 근거: 시설 없이 제자리에서도 쉴 수 있고 안전한 오두막에서는 더 효율적으로 회복하는 제안에 사용자가 “그렇게 진행”으로 승인했다.
- 확정: 행동 체력의 기본 휴식은 시설을 요구하지 않는다. 안전한 오두막에서의 휴식은 일반 제자리 휴식보다 회복 효율이 높다.
- 보완: D-287의 휴식 조건 중 장소와 상대 효율을 구체화한다. D-288 자연 회복 조건은 유지한다. 오두막의 존재만으로 안전이나 공격 면역을 부여하지 않는다.
- 미정: 회복 속도·우대량, 휴식 시작/중단, 기존 주변 위협과 안전 판정의 연결. 수면·체온·질병·Health·내면 회복 효과를 이 결정으로 확대하지 않는다.
- 구현 경계: 기획 r3에 반영하며 기존 E5 승인 기획/hash·명세·Save·코드·E 판정은 자동 변경하지 않는다. 비의존 개발은 계속한다.
- 상세: [플레이어 행동 체력 회복과 성장 r3](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-290 휴식은 이동·작업·피격 시 즉시 중단하고 이미 회복한 체력은 유지한다

- 상태: `Accepted` (휴식 중단·누적 회복 보존)
- 결정일: 2026-08-30
- 근거: 이동·작업을 시작하거나 공격받으면 휴식을 즉시 끝내고 그동안 회복한 체력은 유지하는 제안에 사용자가 “진행”으로 승인했다.
- 확정: 휴식 완료나 애니메이션 종료를 기다리지 않고 이동·작업·피격으로 중단한다. 중단을 이유로 이미 적용된 Stamina 회복량을 취소하지 않는다.
- 보완: D-287/D-289의 휴식 중단 조건을 확정한다. 이후 작업 비용·피격 피해는 정상 적용하며 중단 자체의 추가 회복이나 면역은 없다. 중단 후 자연 회복은 D-288의 실제 활동 조건을 따른다.
- 미정·검증: 회복 속도·휴식 시작·안전 판정은 남아 있다. 같은 권위 시간의 회복/중단 순서와 재시작 중복 회복 방지는 공통 Core에서 검증한다.
- 구현 경계: 기획 r4에 반영하며 기존 E5 승인 기획/hash·명세·Save·코드·E 판정은 자동 변경하지 않는다. 비의존 개발은 계속한다.
- 상세: [플레이어 행동 체력 회복과 성장 r4](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-291 체력 회복 속도는 세 단계로 구분하고 Farm 반복 시험으로 조정한다

- 상태: `Accepted` (상대 속도·수치 조정 방법)
- 결정일: 2026-08-30
- 근거: 자연 회복보다 제자리 휴식을 빠르게, 안전한 오두막 휴식을 그보다 빠르게 두고 실제 농사 반복 시험으로 수치를 조정하자는 제안에 사용자가 “그 방식으로” 승인했다.
- 확정: 동일 권위 게임 시간에서 `자연 회복 < 제자리 휴식 < 안전한 오두막 휴식`을 유지한다. 짧은 휴식 후 다음 작업을 이어갈 수 있도록 실제 Farm 비용·시간 흐름에 맞춰 개발이 초기 조정값을 선정하고 비교한다.
- 보완: D-287~D-290의 구체 회복 속도는 숫자별 재승인 대기 대신 시험 조정 대상으로 다룬다. 시간 단위·작업1회분 회복 시간·두 주기 결과를 남기며 자동 시험과 실제 입력 체감 검증을 구별한다.
- 경계: 기존 비용15/상한100을 우회 변경하지 않는다. 휴식 시작·안전 판정·재개 지연 등 남은 조건, 포션·성장·저장 호환은 별도 검토한다. 기존 E5 동결 기획/hash·실행 명세·E 판정을 자동 변경하지 않는다.
- 상세: [플레이어 행동 체력 회복과 성장 r5](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-292 휴식은 버튼 한 번으로 시작하고 유지 입력을 요구하지 않는다

- 상태: `Accepted` (휴식 시작 조작)
- 결정일: 2026-08-30
- 근거: 버튼을 한 번 눌러 휴식을 시작하고 계속 누를 필요 없이 이동·작업·피격 시 종료하는 제안에 사용자가 “진행”으로 승인했다.
- 확정: 휴식 시작은 단일 버튼 조작이다. 입력을 놓아도 유지하고 D-290의 중단 조건과 기회복량 보존을 따른다. 반복 입력으로 회복 보상을 만들지 않는다.
- 경계: 기존 명령 검증·멱등성을 유지한다. 전투 중 시작 허용·재클릭 수동 취소·UI 위치·단축키는 이번 승인에 포함하지 않는다. 회복 속도는 D-291의 시험 조정 대상으로 유지한다.
- 구현 경계: 기획 r7에 반영하며 기존 E5 동결 기획/hash·실행 명세·Save·E 판정을 자동 변경하지 않는다.
- 상세: [플레이어 행동 체력 회복과 성장 r7](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-293 전투 중 휴식 시작을 금지하고 전투 종료 후 다시 허용한다

- 상태: `Accepted` (전투 중 휴식 시작 조건)
- 결정일: 2026-08-30
- 근거: 전투 중에는 휴식을 시작할 수 없고 전투가 끝나면 다시 사용하도록 하자는 제안에 사용자가 “어어”로 승인했다.
- 확정: 해당 Actor의 실제 전투 상태를 기준으로 휴식 시작을 거부한다. 전투 종료 후에는 버튼1회로 다시 시작할 수 있다. 자동 재개·즉시 회복을 부여하지 않는다.
- 보완: D-292의 미정 중 전투 중 시작 허용 여부를 확정한다. 기존 D-290 중단·기회복량 보존, D-291 수치 시험 조정은 유지한다.
- 경계: UI 비활성화뿐 아니라 Core에서 검증한다. 오두막 내부 또는 카메라 전환을 전투 제한 우회 근거로 사용하지 않는다. 안전 판정·재개 지연·시간 연결은 별도 검토하고 기존 E5 동결 기획/hash·명세·Save·E를 자동 변경하지 않는다.
- 상세: [플레이어 행동 체력 회복과 성장 r8](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-294 위협 접근은 오두막 회복 우대만 해제하고 실제 전투·피격은 휴식을 종료한다

- 상태: `Accepted` (위협 접근·전투 전이의 휴식 반응)
- 결정일: 2026-08-30
- 근거: 주변 위협 접근 시 오두막의 빠른 회복 혜택만 해제하고 일반 속도로 휴식을 유지하며 실제 전투 진입·피격 때 종료하는 제안에 사용자가 “어어”로 승인했다.
- 확정: 접근만으로 휴식을 끊지 않는다. 우대를 해제한 이후에는 일반 제자리 휴식 속도를 적용하며, 실제 전투 진입·피격은 즉시 종료한다. 기존 이동·작업 중단과 기회복량 보존도 유지한다.
- 경계: 위협 접근의 거리·대상·방호 및 실제 안전 판정은 별도 계약 검토 대상이다. 기존 정상 회복량을 소급 취소하거나 같은 시간의 회복을 중복 적용하지 않는다.
- 구현 경계: 기획 r9에 반영하며 기존 E5 동결 기획/hash·명세·코드·Save·E를 자동 변경하지 않는다.
- 상세: [플레이어 행동 체력 회복과 성장 r9](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-295 자연 회복은 걷기·대기 조건이 되면 별도 지연 없이 재개한다

- 상태: `Accepted` (자연 회복 재개 지연0)
- 결정일: 2026-08-30
- 근거: 노동·질주·전투가 끝나 걷거나 대기하는 상태가 되면 별도 대기시간 없이 자연 회복을 시작하자는 제안에 사용자가 “진행”으로 승인했다.
- 확정: 실제 걷기·대기 조건을 충족한 시점부터 별도 지연 없이 권위 경과 시간에 따라 자연 회복한다. 다른 회복 금지 활동이 남아 있으면 재개하지 않는다.
- 경계: 상태 전환 자체의 즉시 보상·금지 구간 소급 회복·일반 휴식 자동 재시작은 없다. 경과 시간0의 전환 반복으로 회복량을 얻지 못해야 한다. D-288의 금지 활동과 기존 비용/피해 규칙은 유지한다.
- 구현 경계: 기획 r10에 반영한다. 기존 E5 동결 기획/hash·명세·코드·Save·E를 자동 변경하지 않으며 실제 시간·활동 연결과 안전 판정은 별도 검증한다.
- 상세: [플레이어 행동 체력 회복과 성장 r10](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-296 불은 휴식의 필수가 아니라 추가 회복 요소로 둔다

- 상태: `Accepted` (불에 의한 휴식 회복 향상 방향)
- 결정일: 2026-08-30
- 근거: 오두막 안전 조건 문답에서 사용자가 “불이 있으면 더 빠른 회복”을 요청했다.
- 확정: 기본 휴식은 불 없이 가능하고, 같은 휴식·안전 조건에서 유효하게 타는 불이 있으면 회복이 더 빠르다. 불이 있다는 사실을 안전 판정이나 전투 중 휴식 허용으로 대체하지 않는다.
- 경계: 직전 최소 안전 정책 전체의 승인으로 확대하지 않는다. 야외 모닥불 적용·유효 범위·잔불·중첩·위협 접근 시 불 효과는 아직 미정이다. 기존 열원 상태 계약을 재사용 검토하며 장식/불꽃만으로 권위 효과를 발생시키지 않는다.
- 구현: 기획 r11에 반영한다. 기존 열원 E3를 실제 연소·공간·Save 완료로 승격하지 않고 기존 E5 동결 기획/hash·명세·코드·Save·E는 변경하지 않는다.
- 상세: [플레이어 행동 체력 회복과 성장 r11](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-297 야외 모닥불 곁의 휴식에도 불의 추가 회복을 적용한다

- 상태: `Accepted` (불 효과의 야외 휴식 적용)
- 결정일: 2026-08-30
- 근거: 오두막 밖 모닥불 곁에서 쉬어도 추가 회복을 받게 하자는 제안에 사용자가 “ㅇㅇ”로 승인했다.
- 확정: 야외 모닥불 휴식에도 D-296의 추가 행동 체력 회복 효과를 적용한다. 오두막 등 건물을 필수로 요구하지 않는다.
- 경계: 불의 회복 효과를 건물의 보호·수면·안전 효과와 구분한다. 실제 열원 유효 범위·가림·잔불·중첩·위협 접근 중 효과는 별도 검토하며 기존 전투/피격 중단을 유지한다. 야외 불과 안전한 오두막의 상대 속도 전체를 고정하지 않는다.
- 구현: 기획 r12에 반영한다. 기존 E5 동결 기획/hash·명세·코드·Save·E 및 실제 자산/애니메이션은 이번에 변경하지 않는다.
- 상세: [플레이어 행동 체력 회복과 성장 r12](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-298 View 캡처는 월드·공간·배치 담당이 실행하고 개발이 통합한다

- 상태: `Accepted` (캡처 담당 분담)
- 결정일: 2026-08-30
- 근거: 사용자가 “View캡처는 월드 공간 배치 쓰레드가 담당하도록 설정”을 요청했다.
- 확정: Game View 및 보조 Scene View 캡처의 실행 담당은 월드·공간·배치다. 개발은 대상 WI·Scene·판본·재현 입력과 확인 조건을 인계하고, 반환된 화면·Console·재현 근거를 검토해 통합한 뒤 기획에 반환한다.
- 경계: D-285 전문 담당→개발→기획의 인계 구조를 유지한다. 담당 지정 자체는 즉시 캡처·Editor 점유·공식 Scene 저장·미저장 변경 폐기·새 기획 승인·E 승격이 아니다. 실제 캡처 여부와 증거 수준을 별도로 기록한다.
- 상세: [View 캡처 분담](../Architecture/CodexPlayableLoopGoal운영체계.md#view-캡처-분담).

## D-299 여러 불의 회복 효과는 합산하지 않고 가장 효과적인 하나만 적용한다

- 상태: `Accepted` (열원 간 추가 회복 비중첩)
- 결정일: 2026-08-30
- 근거: 가장 효과적인 유효 열원 하나만 적용하고 선택된 불이 꺼지면 다른 유효 열원으로 이어지는 제안에 사용자가 “추천대로”로 승인했다.
- 확정: 불의 개수로 추가 회복량을 합산하지 않는다. 현재 유효한 열원 중 가장 효과적인 하나를 적용하고 유효 열원이 없으면 불의 추가 효과만 해제한다. 전환 자체의 즉시 보상이나 같은 시간의 중복 회복은 없다.
- 경계: 거리·가림·잔불·위협 접근 중 불 효과와 자연 회복/휴식/오두막 우대 간 합성은 이번 승인에 포함하지 않는다. 동률 선택·시간 정산은 결정적인 기술 검증 대상으로 둔다. 기존 E5 승인 기획·명세·코드·Save·E는 자동 변경하지 않는다.
- 상세: [행동 체력 기획 r13](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-300 막힌 벽 너머의 열원은 휴식 추가 회복에 적용하지 않는다

- 상태: `Accepted` (열원 회복의 벽 차단)
- 결정일: 2026-08-30
- 근거: 가까워도 막힌 벽 너머의 불은 회복 효과를 주지 않는 제안에 사용자가 “벽도 고려”로 승인했다.
- 확정: 막힌 벽에 차단된 열원은 추가 회복 후보에서 제외한 뒤 D-299의 유효 열원 하나를 선택한다. 남은 열원이 없으면 불의 추가 효과만 해제하고 일반 휴식과 기회복량은 보존한다.
- 경계: 열린 문/창문/틈, 거리 수치, 잔불, 실내 온도 계산은 이번에 확정하지 않는다. 카메라 가시성·장식 불꽃을 권위 판정으로 전용하지 않는다. 기획 반영이며 기존 E5 명세·코드·Save·E 승격은 없다.
- 상세: [행동 체력 기획 r14](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-301 휴식 중 불 효과와 벽 차단 이유를 작은 아이콘·짧은 문구로 안내한다

- 상태: `Accepted` (휴식 피드백)
- 결정일: 2026-08-30
- 근거: 작은 불 아이콘·“불 곁에서 회복 중”, 벽 차단 시 “벽에 가려 열기가 닿지 않음”으로 안내하는 제안에 사용자가 “어어”로 승인했다.
- 확정: 복잡한 수치 없이 현재 효과와 차단 이유를 짧게 표시한다. 실제 적용 중인 유효 열원과 Core 판정에 맞춰 표현하고 아이콘 자체로 효과를 발생시키지 않는다.
- 경계: UI 자산·정확한 배치·추가 소리·새 경고창은 선정/구현하지 않았다. 기존 E5 승인·명세·코드·Save·E는 변경하지 않는다. View 캡처는 D-298 분담을 따른다.
- 상세: [행동 체력 기획 r15](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-302 행동 체력이 가득 차면 휴식을 끝내고 다음 행동은 플레이어가 선택한다

- 상태: `Accepted` (휴식 완료와 작업 복귀)
- 결정일: 2026-08-30
- 근거: 최대치 도달 시 휴식 자동 종료·“충분히 쉬었습니다” 안내·제자리 대기를 제안했고 사용자가 “어어”로 승인했다.
- 확정: 현재 유효한 행동 체력 최대치에 도달하면 명시적 휴식을 자동 종료한다. 다음 농사·이동은 자동 재개하지 않고 플레이어 선택으로 둔다. 종료 보상이나 상한 초과 회복은 없다.
- 경계: 일반 휴식에 대한 결정이다. 수면·NPC 위임·성장 시 현재값 보충·가득 찬 상태의 휴식 시작 여부를 함께 확정하지 않는다. 실제 구현·검증 완료나 기존 E5 승인·명세·Save·E 변경은 아니다.
- 상세: [행동 체력 기획 r16](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-303 포션 한 개를 소비하면 행동 체력을 즉시 일정량 회복한다

- 상태: `Accepted` (포션 즉시 회복)
- 결정일: 2026-08-30
- 근거: 포션 한 개 소비로 행동 체력을 즉시 일정량 회복하고 양은 개발 시험으로 조정하는 제안에 사용자가 “즉시”로 답했다.
- 확정: 유효한 소비 확정에 즉시 회복 효과를 연결한다. 지속 회복으로 나누지 않으며 일반 휴식과 별개의 물품 소비 선택으로 둔다. Preview 무변경·Confirm 멱등·소비 거부 시 효과 없음의 기존 경계를 유지한다.
- 경계: 전투 사용·재사용 대기·획득처·최대치 상태의 소비 정책은 미정이다. HP·체온·질병·내면 회복으로 확대하지 않는다. 기존 E5 승인·명세·코드·Save·E는 자동 변경하지 않는다.
- 상세: [행동 체력 기획 r17](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-304 행동 체력 포션은 전투 중 사용을 허용하고 재사용 대기를 둔다

- 상태: `Accepted` (포션 전투 사용과 재사용 대기)
- 결정일: 2026-08-30
- 근거: 전투 중 포션 사용을 허용하되 사용 후 잠깐 재사용을 기다리고 구체 시간은 전투 시험으로 조정하는 제안에 사용자가 “그렇게 허용”으로 승인했다.
- 확정: 전투 중 유효한 소비에 D-303의 즉시 회복을 적용하고 사용 후 재사용 대기를 둔다. 대기 중 사용 거부는 물품·체력을 변경하지 않는다. 구체 대기시간은 개발 전투 시험 조정 대상이다.
- 경계: 자연 회복·일반 휴식의 전투 제한은 유지한다. 다른 포션 종류의 대기 공유·획득처·최대치 정책을 함께 확정하지 않는다. 실제 전투/Save 연결이나 기존 E5 승인·명세·코드·E 변경은 아니다.
- 상세: [행동 체력 기획 r18](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-305 체력이 가득 차면 포션 소비를 막고 부족할 때만 최대치까지 회복한다

- 상태: `Accepted` (포션 최대치 소비 보호)
- 결정일: 2026-08-30
- 근거: 최대치에서 사용을 막고 물품·대기를 보존하며 조금이라도 부족하면 최대치까지만 회복하는 제안에 사용자가 “그렇게 진행”으로 승인했다.
- 확정: 최대치에서 포션 사용은 거부하고 소비·회복·재사용 대기 변경은 없다. 부족할 때 다른 사용 조건을 충족하면 한 개를 소비하고 최대치까지만 즉시 회복하며 정상 재사용 대기를 적용한다. 초과분을 저장하지 않는다.
- 경계: Preview와 Confirm 사이의 상태 변화를 권위 실행 시 재검사한다. 획득처·다른 포션의 대기 공유는 이번에 확정하지 않는다. 기존 E5 승인·명세·코드·Save·E는 자동 변경하지 않는다.
- 상세: [행동 체력 기획 r19](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-306 포션 획득은 탐험·제작·거래의 선택 경로이며 고정 순서를 강제하지 않는다

- 상태: `Accepted` (포션 획득 경로의 자유)
- 결정일: 2026-08-30
- 근거: 폐야영지에서 소량 발견 후 제작·거래로 마련하는 제안에 사용자가 “그렇게 진행”한 뒤 “꼭 정해진 루트대로 가야되는건 아니었으면”으로 보완했다. 후자의 자유 선택 조건을 함께 적용한다.
- 확정: 폐야영지 발견은 예시이며 필수 시작점이 아니다. 가능한 탐험·제작·거래 중 플레이어가 선택하고 경로를 바꿀 수 있다. 야영지 방문이나 고정된 해금 순서를 요구하지 않는다. 첫 사용에 제작법 지식을 요구하지 않는다.
- 경계: 각 방식의 재료·시설·대가·권한 등 정당한 실행 조건은 유지한다. 발견 수량·재생·조합·가격·상인·자산 배치는 별도 설계하며 기존 약초차를 Stamina 포션으로 바꾸지 않는다. 기존 승인된 다른 폐루프를 자동 개정하거나 코드·명세·Save·E를 변경하는 승인은 아니다.
- 상세: [행동 체력 기획 r20](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-307 선택한 실제 활동으로 행동 체력이 성장하며 활동 다양성을 의무화하지 않는다

- 상태: `Accepted` (행동 체력 성장 경로)
- 결정일: 2026-08-30
- 근거: 전투뿐 아니라 농사·채집·건설도 성장에 기여하고 여러 활동을 반드시 골고루 할 필요 없이 선택한 생활로 성장하는 제안에 사용자가 “어어”로 승인했다.
- 확정: 실제 수행한 활동의 결과를 행동 체력 최대치 성장 근거로 삼는다. 전투를 필수로 요구하거나 활동 다양성을 필수 성장 조건으로 두지 않는다. 동일 행위 재처리·거부·Preview를 새 성장으로 계산하지 않는다.
- 경계: 활동별 성장량 동일, 매 행위마다 증가, 새로운 공통 레벨 구현을 의미하지 않는다. 구체 기여량·단계·상한·성장 시 현재값 보충은 별도 결정이다. 기존 E5 승인·명세·코드·Save·E는 자동 변경하지 않는다.
- 상세: [행동 체력 기획 r21](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-308 성장으로 최대 체력이 늘어나면 현재 체력도 증가분만큼 보충한다

- 상태: `Accepted` (성장 시 현재 행동 체력 보충)
- 결정일: 2026-08-30
- 근거: 최대치100→105일 때 현재값30→35처럼 증가분만 보충하는 제안에 사용자가 “보충”으로 승인했다.
- 확정: 성장에 따른 실제 최대치 증가분을 현재값에도 더하되 새 최대치를 넘지 않는다. 완전 회복이나 비율 유지가 아니며 같은 성장의 중복 처리로 보충을 반복하지 않는다. 예시 수치5는 성장량 확정이 아니다.
- 경계: 일시 버프·장비 교체·최대치 감소 정책은 별도다. 공통 레벨/분야 숙련 매핑·성장량·상한은 미정으로 유지하고 기존 E5 승인·명세·코드·Save·E를 자동 변경하지 않는다.
- 상세: [행동 체력 기획 r22](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-309 활동 경험에 따른 최대치 자동 성장에 체력과 마나를 포함한다

- 상태: `Accepted` (체력·마나 최대치 자동 성장 방향)
- 결정일: 2026-08-30
- 근거: 활동 경험 축적 후 별도 포인트 배분 없이 최대치가 자동 증가하고 짧게 알리는 제안에 사용자가 “체력뿐만 아니라 마나도 그렇게 증가”하도록 요청했다.
- 확정: 체력과 마나는 활동 경험이 성장 기준에 도달하면 각 최대치가 자동으로 성장하는 방향이다. 활동 다양성 의무나 별도 포인트 배분을 필수로 두지 않는다.
- 경계: 모든 활동이 두 자원을 같은 양으로 성장시키는 의미가 아니다. 마나별 기여 활동·현재값 보충·회복/소비/포션·명상/정신력 관계는 별도 확인한다. 기존 체력 r22 증가분 보충은 유지하며 새 공통 Mana 계약 구현·기존 E5 승인·명세·Save·E 변경은 없다.
- 상세: [행동 체력·마나 성장 방향 r23](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-310 마나 최대치는 명상과 집중한 일상 행동으로도 성장한다

- 상태: `Accepted` (마나 성장 기여 경로)
- 결정일: 2026-08-30
- 근거: 마법 사용 외 명상(정신 차림)·집중에 성공한 제작/채집도 성장에 기여하는 제안에 사용자가 “진행”으로 승인했다.
- 확정: 마법 전투를 필수로 요구하지 않고 선택한 생활에서 마나 성장 경험을 쌓을 수 있다. 실제 집중 성공 행위 결과를 근거로 하며 Preview·거부·중복 처리로 성장시키지 않는다.
- 경계: 모든 일상 행동의 집중 성공·동일 기여량을 보장하지 않는다. 명상 숙련도와 마나는 별도 값이며 현재 마나 보충·회복·소비 정책, 세부 판정·수치는 별도다. 코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r24](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-311 마나 성장 시 최대치 증가분만 현재 마나에 보충한다

- 상태: `Accepted` (마나 성장 보충)
- 결정일: 2026-08-30
- 근거: 최대치100→105일 때 현재값30→35처럼 증가분만 보충하는 제안에 사용자 “진행” 승인.
- 확정: 실제 마나 최대치 증가분을 현재값에 더하되 새 최대치를 넘지 않는다. 완전 회복·비율 유지가 아니며 중복 성장/저장 복원으로 반복 보충하지 않는다. 수치5는 예시다.
- 대체 관계: D-309·D-310에서 미정으로 남긴 성장 시 현재 마나 보충만 확정한다. 회복·소비·포션·임시 최대치 변화는 여전히 별도다. 코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r25](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-312 마나는 자연 회복되고 명상 중에는 더 빠르게 회복된다

- 상태: `Accepted` (마나 회복 기본 방향)
- 결정일: 2026-08-30
- 근거: 평소 느린 회복과 명상(정신 차림) 중 더 빠른 회복을 제안했고 사용자가 “인정”으로 승인했다.
- 확정: 마나는 자연 회복되므로 명상을 필수 회복 수단으로 두지 않는다. 명상은 현재 마나를 더 빠르게 회복하는 선택지다. 최대치 성장과 회복은 별도다.
- 대체 관계: D-309~D-311의 미정 회복 경로 중 기본 방향만 확정한다. 전투/마법 사용/활동별 허용·명상 중단·수치·포션·소비는 별도이며 체력 회복 조건을 자동 적용하지 않는다. 코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r26](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-313 전투 중 마나 자연 회복은 유지하고 명상 회복은 제한한다

- 상태: `Accepted` (전투 중 마나 회복 경계)
- 결정일: 2026-08-30
- 근거: 전투 중 느린 자연 회복 유지·빠른 명상 회복은 전투 종료 후 허용하는 제안에 사용자 “인정” 승인.
- 확정: 전투 상태만으로 마나 자연 회복을 중단하지 않는다. 명상 우대는 비전투에만 적용하므로 전투 중 시작을 거부하고 진입 시 우대를 중단한다. 이미 정상 회복한 값은 유지한다.
- 대체 관계: D-312의 전투 중 허용 여부를 확정한다. 전환 순간 고정 보상·전투 종료 후 자동 명상 재시작은 승인하지 않는다. 시전 세부 처리·수치·비전투 조작 및 코드·Save·E·기존 E5 승인은 별도다.
- 상세: [행동 체력·마나 기획 r27](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-314 명상은 버튼 한 번으로 시작하고 행동·피격으로 종료한다

- 상태: `Accepted` (마나 회복 명상 조작)
- 결정일: 2026-08-30
- 근거: 버튼1회 시작·이동/작업/피격 시 종료·기회복 마나 보존·플레이어 선택 재시작 제안에 사용자 “인정” 승인.
- 확정: 입력 유지 없이 명상을 시작한다. 이동·작업 시작·피격 시 즉시 종료하고 기회복량은 유지한다. 원인 해소나 전투 종료 후 자동 재시작하지 않는다. 기존 전투 중 시작 금지 유지.
- 경계: 명시적 마나 회복 명상에 한정하며 일상 WI 집중 성공을 금지하지 않는다. 키 배정·재클릭·마나 최대치 도달 시 종료·애니메이션은 별도다. 코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r28](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-315 마나가 가득 차도 명상은 유지하고 짧게 알린다

- 상태: `Accepted` (명상 충만 처리)
- 결정일: 2026-08-30
- 근거: 마나 충만 시 짧은 안내 후 명상 유지 제안에 사용자 “그렇게 진행” 승인.
- 확정: 충만만으로 명상을 자동 종료하지 않는다. 플레이어 종료 선택 또는 기존 이동·작업·피격·전투 진입으로 종료하며 체력 휴식 자동 종료와 구분한다. 충만 안내를 같은 상태에서 계속 반복하지 않는다.
- 대체 관계: D-314의 마나 최대치 도달 시 처리만 확정한다. 상한 초과 회복·충만 유지 추가 성장/내면 보상은 승인하지 않는다. UI 세부·음원·코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r29](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-316 명상 중 같은 버튼으로 명상을 끝낸다

- 상태: `Accepted` (명상 종료 버튼)
- 결정일: 2026-08-30
- 근거: 명상 중 ‘명상 끝내기’로 버튼을 바꾸고 한 번 더 눌러 종료하는 제안에 사용자 “ㅇㅇ” 승인.
- 확정: 이동 없이 제자리에서 명상을 종료할 수 있다. 정상 회복량은 유지하고 종료 보상·자동 행동 재개는 없다. 중복 종료 요청이 재시작으로 반전되지 않도록 한다.
- 대체 관계: D-314의 명상 재클릭 종료 미정을 확정한다. 체력 휴식 재클릭·키/위치·애니메이션·코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r30](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-317 회복 상태는 작게 표시하고 속도와 효과는 상세에서 보여준다

- 상태: `Accepted` (회복 정보 계층)
- 결정일: 2026-08-30
- 근거: 체력·마나 옆 현재 상태 표시와 상세 펼침의 회복 속도·효과 표시 제안에 사용자 “인정” 승인.
- 확정: 기본 화면은 ‘자연 회복 중’·‘휴식 중’·‘명상 중’ 같은 작은 상태 표시를 사용하고 상세에서 속도·적용 효과를 읽는다. 두 자원과 명상 상태/실제 회복 여부를 구분한다.
- 경계: 같은 권위 상태 사본의 읽기 전용 표현이다. 위치·색상·아이콘·수치·자산·코드·Scene·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r31](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-318 행동 자원이 부족하면 부족량과 회복 방법을 안내한다

- 상태: `Accepted` (행동 불가 안내·회복 선택권)
- 결정일: 2026-08-30
- 근거: 행동 버튼/단축키의 의미와 수확 체력 부족 예시를 설명한 뒤 사용자가 “이러는 게 좋겠다라는 게 내 생각이야”로 승인했다.
- 확정: 행동 시도 시 체력·마나 부족량과 가능한 회복 방법을 짧게 안내한다. 자동 휴식·명상·포션 소비 없이 플레이어가 회복 또는 다른 행동을 선택한다.
- 경계: 권위 거부 결과와 실제 이용 가능한 회복 방법을 표시한다. 다른 실패 원인을 덮거나 안내로 자원을 소비하지 않는다. 회복 후 행동 자동 재시도·UI 세부·코드·Save·E·기존 E5 승인 변경은 없다.
- 상세: [행동 체력·마나 기획 r32](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md).

## D-319 행동 체력 자연 회복을 독립된 첫 실행 범위로 승인한다

- 상태: `Accepted` (자연 회복 실행 승인)
- 결정일: 2026-08-30
- 근거: 개발의 기본 회복 실행 범위 요청을 제안한 뒤 사용자가 “그렇게 확정. 자연회복에대해”로 승인했다. 명시된 자연 회복으로 범위를 좁힌다.
- 확정: 걷기/대기 회복·노동/질주/전투 중단·허용 상태 복귀 후 지연0·실제 권위 시간만 누적하는 기존 규칙을 Farm 반복에 연결한다. 순수 시험부터 Session/Host/Save 검증까지 개발하되 실제 근거로만 E를 판정한다.
- 경계: 제자리 휴식·불/오두막·마나·포션·성장·새 UI는 이번 실행 승인에서 제외한다. 종합 기획 r32와 기존 승인 원문7개를 바꾸지 않고 별도 동결 기획/명세 결속으로 추가한다. 기존 병렬 개발은 유지하고 commit/push는 승인하지 않는다.
- 상세: [행동 체력 자연 회복 기획 r1](../Architecture/PlayableLoops/플레이어행동체력자연회복.md).

## D-320 Farm 건물 실측 크기를 수용하도록 부지를 넓힌다

- 상태: `Accepted` (Farm 공간 연구 보완·구현 방향)
- 결정일: 2026-08-30
- 근거: Barn 실측 외곽이 기존12×10m 후보보다 커서 건물 축소 대신 부지·작업마당·통로를 조정하는 제안에 사용자가 “진행해줘 그렇게. 아까 그 부지 넓히는거”로 승인했다.
- 확정: native 건물/Player 비율을 유지하고 실측 외곽과 실제 접근 여유에 맞춰 새 후보 부지를 산출한다. 세부 치수는 공간 담당이 실제 Player·문·지지/충돌 조건으로 검증한다.
- 경계: 생산100㎡·수확량·주 하천 보존·H 소유 경계·실내 제외 유지. 구 후보/연구 r1/승인7원문은 보존하고 연구 r2·새 후보 revision으로 결속한다. 승인만으로 성공 배치나 E 승격은 없으며 기존 병렬 개발은 계속한다.
- 상세: [Farm 공간·배치 연구 r2](../Architecture/PlayableLoops/Farm경작세계발현E5-공간배치연구.r2.md).

## D-321 건설 취소 재료 반환을 난이도별로 구분한다

- 상태: `Accepted` (입문자·숙련자 반환 방향, 세부 실행 미정)
- 결정일: 2026-08-30
- 근거: 사용자가 취소 시 초보자100%·숙련자 약70% 반환을 제안했다. 기존 문답 명칭에 맞춰 초보자는 입문자로 기록한다.
- 확정: 미완성 건설의 명시 취소 시 입문자 전량·숙련자 일부 반환을 적용하는 방향이다. 단순 중단의 재료/진척 보존(Q-059)은 유지한다.
- 대체 관계: D-233의 오두막 취소 전량 반환은 새 난이도별 정책에서 부분 대체할 대상이다. 과거 RuleRevision/Save의 결과와 hash는 보존하고 신규 실행 기획·호환 검증 없이 소급 변경하지 않는다.
- 미정: 노말 반환율·숙련자 최종 비율/단위 반올림·예약/투입/소비 재료 계산 범위·난이도 변경·완성 건물 철거. 기존 D319/D320 실행 승인을 확장하지 않는다.
- 상세: [Nature 자원·건설 후속 문답](../Architecture/PlayableLoops/PlanningSessions/Nature자원건설/nature-resource-construction.inquiry.r1.md).

## D-322 노말 건설 취소는 약85% 재료 반환 방향으로 둔다

- 상태: `Accepted` (노말 반환율 방향)
- 결정일: 2026-08-30
- 근거: 노말을 입문자/숙련자 중간인 약85% 반환으로 두는 제안에 사용자가 동의했다.
- 확정: D-321의 노말 미정을 보완하여 입문자100%·노말 약85%·숙련자 약70% 반환 방향을 사용한다.
- 경계: 재료 적용 범위·최종 수치 Profile/반올림·난이도 변경·완성 철거는 미정이다. 일시중단 보존·과거 Rule/Save와 D319/D320 실행 범위는 변경하지 않는다.
- 상세: [Nature 자원·건설 내용 r3](../Architecture/PlayableLoops/PlanningSessions/Nature자원건설/nature-resource-construction.inquiry.r1.md).

## D-323 미사용 재료와 실제 시공 사용분의 취소 반환을 구분한다

- 상태: `Accepted` (건설 취소 재료 적용 범위)
- 결정일: 2026-08-30
- 근거: 미사용 예약은 전량 반환하고 실제 시공 사용분만 난이도별 반환율을 적용하는 제안에 사용자가 정밀하게 구분하는 것이 좋다고 동의했다.
- 확정: 예약/현장 적치 등 미사용 재료는 전량 보존·반환 대상이다. 실제 사용분에만 입문자100%·노말 약85%·숙련자 약70%를 적용한다. 현장 반입을 소비와 혼동하거나 같은 재료를 두 번 반환하지 않는다.
- 대체 관계: D-321~322의 재료 적용 범위 미정을 보완한다. 시공 소비 시점·단위/반올림·반환 위치/운반·난이도 변경·완성 철거는 별도다. 기존 Rule/Save·코드·E·D319/D320 실행 승인은 변경하지 않는다.
- 상세: [Nature 자원·건설 내용 r4](../Architecture/PlayableLoops/PlanningSessions/Nature자원건설/nature-resource-construction.inquiry.r1.md).

## D-324 건설 취소 확정 전에 보존·회수·손실 재료를 나눠 안내한다

- 상태: `Accepted` (취소 미리보기 기획)
- 결정일: 2026-08-30
- 근거: 사용자가 취소 확정 전 세 항목으로 구분해 보여주는 방식을 명시적으로 승인했다.
- 확정: D-323의 재료 상태 구분을 미리보기에 연결하고 재료별 보존량·회수량·손실량 및 적용 난이도/반환율을 보여준 뒤 명시적 확정을 받는다. 미리보기만으로 세계 상태를 변경하지 않는다.
- 경계: 상세 계산·운반 미정과 기존 Rule/Save 호환을 유지한다. 이번은 기획 반영이며 코드/UI 구현 또는 기존 실행 범위 확장·E 승격은 아니다.
- 상세: [Nature 자원·건설 내용 r5](../Architecture/PlayableLoops/PlanningSessions/Nature자원건설/nature-resource-construction.inquiry.r1.md).

## D-325 문답 원문을 보존하고 파일 기반 검색 색인으로 재개한다

- 상태: `Accepted` (기획 문답 탐색 구조)
- 결정일: 2026-08-30
- 근거: 사용자가 기존 질문·답변을 파일적으로 데이터베이스화한 뒤 부족한 부분에서 문답을 이어가도록 요청했다.
- 확정: 주제 Markdown·동결 아카이브·기존 구현 원장은 각 책임을 유지한다. 생성 JSON의 원문 hash·절·질문 관계로 Q001~347 및 번호 없는 후속 문답을 함께 조회하며 새 질문 전 기존 답변/후속 결정을 확인한다.
- 경계: 색인은 검색용 사본이며 별도 승인·구현·E 원장이 아니다. 소실 원문·미답변·미분류 깊이는 추정하지 않는다. 기존 번호·승인 hash·게임 코드·Scene·Save는 변경하지 않는다.
- 상세: [문답 파일 데이터베이스](../../eng/planning-inquiries/README.md).

## D-326 첫 약초차 반복은 기존 냄비로 물을 운반하고 컵으로 마신다

- 상태: `Accepted` (Q-347 용기 역할 기획)
- 결정일: 2026-08-30
- 근거: 별도 물통 없이 발견한 냄비로 물을 떠 와 달이고 컵으로 마시는 제안에 사용자가 “그렇게 해줘”로 동의했다.
- 확정: 첫 반복에 신규 물통 획득을 필수로 요구하지 않는다. Q-346의 첫 물 1회 제공·이후 주변 수원 직접 확보를 유지한다.
- 경계: 물의 양·수질/정화·채수 시간·운반 조작 제약은 미정이며 기존 실행 승인·Save·E를 자동 변경하지 않는다.
- 상세: [약초 제작 문답 Q-347](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-347-물-운반-용기).

## D-327 물 확보량은 고정 제작 1회분이 아니라 용기별 용량을 따른다

- 상태: `Accepted` (Q-348 용량 기준)
- 결정일: 2026-08-30
- 근거: 사용자가 냄비·컵마다 용량이 있고 그 용량만큼 물이 떠진다고 명시했다.
- 확정: 고정 ‘달이기 1회분’ 제안은 미채택으로 남기고 용기별 최대 용량과 현재량을 구분한다. 제작 소비량은 용기 용량과 동일시하지 않는다.
- 경계: 실제 용량 수치·표시 단위·처방 소비량·시간/운반 제약은 별도다. 기존 냄비/컵 역할과 첫 물 제공을 유지하고, 실행 승인·Save·E를 자동 변경하지 않는다.
- 상세: [약초 제작 문답 Q-348](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-348-첫-물-수량-단위).

## D-328 물 보관 가능 여부와 용량을 구분한다

- 상태: `Accepted` (Q-348 후속 보완)
- 결정일: 2026-08-30
- 근거: 사용자가 물을 보관할 수 있는 것과 없는 것을 구분하도록 요청했다.
- 확정: 물품의 물 보관 가능 여부를 먼저 확인하고 가능한 용기에만 물 용량·현재량을 적용한다. 일반 수납 용량이나 Prefab 외형을 물 보관 능력으로 대체하지 않는다.
- 경계: 개별 물품 분류·누수/손상·다른 액체·가열 가능 여부는 별도다. 기획 보완이며 기존 코드·Save·E·실행 승인은 변경하지 않는다.
- 상세: [약초 제작 문답 Q-348](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-348-첫-물-수량-단위).

## D-329 달이기 중 다른 행동을 허용하고 약초 포션의 수요 공급을 후속 확장한다

- 상태: `Accepted` (Q-349 달이기 규칙·후속 확장 방향)
- 결정일: 2026-08-30
- 근거: 사용자가 달이는 동안 다른 행동을 허용하고 약초로 포션을 제작해 City·Town·Hub 등의 수요에 공급하는 방향을 요청했다.
- 확정: 달이기 시작 후 계속 앞에 서 있을 필요가 없다. 약초 포션 제작·공급은 자기 회복 이후 선택 가능한 후속 폐루프 후보로 연결한다.
- 경계: 열원 상실·공간 해제·오프라인 진행은 별도다. 포션 효과·거래 조건·운송을 임의 확정하지 않고 약초차와 체력 포션을 동일시하지 않는다. 영역 순서를 강제하지 않으며 기존 실행 승인·코드·Save·E는 자동 변경하지 않는다.
- 상세: [약초 제작 문답 Q-349](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-349-달이는-동안의-플레이어-행동).

## D-330 자기 돌봄이 타인의 이로움으로 확장되는 자리이타를 기획 방향으로 둔다

- 상태: `Accepted` (게임 기획 방향)
- 결정일: 2026-08-30
- 근거: 사용자가 자신을 위해 약초를 달이는 활동이 다른 사람에게까지 도움이 되도록 자연스럽게 뻗어가는 자리이타 방향을 요청했다.
- 확정: 독립적인 자기 생존·회복 경험에서 출발하여 기술·물품과 상대의 필요가 만나는 자발적 공급·교류·협력으로 확장한다. 약초 포션 공급은 첫 적용 예시다.
- 경계: 무상 기부·자동 납품·고정 지역 순서를 강제하지 않는다. 도움에 따른 회복·관계·경제 수치, 새 코드·WI·Save·E 승격은 별도 실행 명세와 증거가 필요하다.
- 단일 기준: [플레이어 중심 게임 개발 업무 구조](../Architecture/플레이어중심게임개발업무구조.md#자리이타--나를-위한-활동이-타인의-이로움으로-이어지는-기획).

## D-331 처방별 연속 가열·돌봄 차이를 후반 전문 제작으로 확장한다

- 상태: `AcceptedDirection / FutureExtension` (Q-350)
- 결정일: 2026-08-30
- 근거: 사용자는 처방에 따라 가열 중단이 효과에 영향을 줄 수 있고 곁에서 돌봄이 필요할 수도 있으며, 이러한 복잡성은 후반 콘텐츠로 두기를 요청했다.
- 확정 방향: 연속 가열 조건과 현장 돌봄 조건을 분리하고 초기 Q-349의 다른 행동 허용은 유지한다. 전문 제작을 즐기는 사람이 포션 수요에 공급하며 다른 활동을 즐기는 사람과 협력하는 자리이타 확장으로 연결한다.
- 경계: 초보자의 의무 노동·멀티플레이 선행·모든 처방의 무손실 재개를 승인하지 않는다. 첫 약초차 중단 처리와 개별 처방의 손실/효과/돌봄/해금/거래 수치는 별도다. 현실 약효를 단정하지 않으며 실행 승인·코드·Save·E는 자동 변경하지 않는다.
- 상세: [약초 제작 문답 Q-350](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-350-달이는-중-불이-꺼졌을-때).

## D-332 첫 약초차의 따뜻함 감정·날숨 표현과 자유 행동을 유지한다

- 상태: `Accepted` (Q-351 표현·초기 자유 행동), 후반 집중은 `FutureExtension`
- 결정일: 2026-08-30
- 근거: 사용자가 음용 뒤 따뜻함을 느끼는 감정과 “하…” 효과음을 요청했고, 첫 약초차는 기다리도록 묶지 않되 후반에는 몰입을 위한 머무름이 필요할 수 있다고 말했다.
- 확정: 첫 약초차 음용 뒤 따뜻함을 표현하며 회복 완료까지 제자리 대기를 강제하지 않는다. 후반 집중의 적용 단계·처방·시간·중단은 별도로 정한다.
- 경계: 실제 회복의 즉시/지속 분배는 아직 미정이다. 표현을 권위 회복 결과로 대체하지 않으며 음원 확보·실제 청취·코드·Save·E 승격을 주장하지 않는다.
- 기준: [약초 문답 Q-351](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-351-약초차를-마신-뒤-회복의-시간-흐름), [오디오 요구사항 대장](../Architecture/PlayableLoops/오디오요구사항대장.md)의 `audio:nature:brew:warmth-exhale.r1`.

## D-333 약초차·탕에 마법 술식을 추가하는 물약 제작을 구분한다

- 상태: `AcceptedDirection / FutureExtension` (Q-352)
- 결정일: 2026-08-30
- 근거: 사용자가 약재를 달인 차·탕에 마법 술식을 적용하는 추가 과정을 거쳐 물약이 되도록 요청했다.
- 확정 방향: 달임 산출물과 술식 가공 산출물을 구분한다. 첫 약초차 폐루프에는 마법을 요구하지 않고 물약 제작은 후속 전문 경로로 둔다. Q-349/D-329의 포션 공급 방향을 제작 단계 관점에서 보완한다.
- 경계: 실제 약재/처방 정보와 게임의 마법 효과를 분리한다. 술식 비용·효과·실패·자산은 미정이며 기존 포션 소비 계약·Save·코드·E를 소급 변경하지 않는다.
- 상세: [약초 문답 Q-352](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-352-약초-달임과-마법-술식-물약-제작의-단계-구분).

## D-334 관심·명상에서 얻는 이데아의 편린을 NPC 인연 가능성과 연결한다

- 상태: `AcceptedDirection / FutureExtension` (Q-353)
- 결정일: 2026-08-30
- 근거: 사용자가 관심을 기울일 분야를 설정하고 명상·숙련 성장 과정에서 이데아의 편린을 얻으며 관련 NPC와 상호작용하는 길이 열리는 방향을 요청했다.
- 확정 방향: 관심·명상·편린·만남·학습을 구분한다. 물약 술식 진입은 그 적용 사례이며 초기 약초차의 선행 조건은 아니다.
- 경계: 편린은 완전한 지식/시전 자격이 아니며 관련 NPC의 즉시 등장·고용을 보장하지 않는다. 구체 획득·만남 확률·관심 설정/변경·학습 조건은 미정이다. 기존 Recipe 경로와 실행 승인·코드·Save·E는 자동 변경하지 않는다.
- 상세: [약초 문답 Q-353](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-353-첫-물약-술식을-만나는-경로). 플레이어 내면·명상 문답은 이 원문을 교차 참조한다.

## D-335 상태창형 UI에서 관심 분야 카드를 선택한다

- 상태: `AcceptedDirection` (Q-353 UI 보완)
- 결정일: 2026-08-30
- 근거: 사용자가 상태창처럼 열 수 있는 UI에서 카드로 관심을 설정하고 이데아의 편린 획득 경로로 이어지도록 요청했다.
- 확정: 관심 분야 카드 선택을 D-334의 명상·편린 경로 입력으로 표현한다. 선택 카드와 실제 획득 편린·학습 지식은 구분한다.
- 경계: 패널/카드의 구체 배치·일러스트·선택 개수·변경 비용/제약은 미정이다. 선택 즉시 편린/NPC를 지급하지 않으며 이번에 UI·코드·Save·E를 변경하지 않는다.
- 상세: [약초 문답 Q-353](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-353-첫-물약-술식을-만나는-경로). 명상 문답은 동일 원문의 UI 결정을 교차 참조한다.

## D-336 관심·행동 기록을 근거로 방문 NPC 후보를 결정한다

- 상태: `AcceptedDirection / FutureExtension` (Q-354)
- 결정일: 2026-08-30
- 근거: 사용자가 방문자를 고정하지 않고 관심 주제와 플레이 행동의 기록에 따라 방문 NPC가 달라지도록 요청했다.
- 확정 방향: 명시적 관심·확정된 게임 행동·인연 기록을 방문 후보의 근거로 삼고, 연결 길 안전·접근 가능성과 실제 도착은 별도로 판정한다. 기존 납품/길 안전 인연 경로를 폐기하지 않는다.
- 경계: 외부 개인정보 수집·상시 서버 DB 선행을 요구하지 않는다. 비중·확률·빈도·반복·NPC 공급/일정은 미정이다. 기존 방문자 실행 승인·저장·E는 자동 변경하지 않는다.
- 상세: [공동체 방문 문답 Q-354](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-354-관심행동-기록에-따른-방문-npc-후보).

## D-337 호출당 외부 비용 없는 로컬 LLM/RAG 대화 지원 기반 설치를 승인한다

- 상태: `Accepted` (지원 기반 설치·격리 코드·시험 범위)
- 결정일: 2026-08-30
- 근거: 사용자가 로컬 LLM/RAG를 설치하여 프로젝트 구조에 반영하도록 요청했다.
- 범위: 고정 LLMUnity 패키지, 기본 비활성인 로컬 NPC 대화/검색 연결부, 근거 제한·기본 대사·취소/오류 경계와 시험. 구현 담당은 개발이며 Unity 점유·기존 변경을 조율한다.
- 금지: 유료/클라우드 추론 대체, LLM의 WI·방문 선정·보상 확정, 전체 개인정보/원장 노출, 기존 Scene/Save/E 임의 변경. 모델 다운로드·실제 추론은 이번 패키지 설치와 구분한다.
- 단일 승인 기준: [로컬 NPC 대화·검색 지원 기반 r1](../Architecture/PlayableLoops/로컬Npc대화지원기반.md).

## D-338 방문 NPC는 분야 화두를 꺼내고 관련 거래 기회를 제공한다

- 상태: `AcceptedDirection / FutureExtension` (Q-355)
- 결정일: 2026-08-30
- 근거: 사용자가 편린 설명 대신 관련 분야의 화두와 거래를 원하며, 전투의 무기·방어구와 물약 제조의 재료·레시피 공급을 예로 들었다. 시작을 돕는 주제 카드는 허용했다.
- 확정: 관심·실제 활동에 맞는 방문 NPC의 제안을 선택 가능한 거래로 연결한다. 편린 공개는 필수 단계가 아니며 기존 편린 획득/인연 경로를 폐기하지 않는다.
- 경계: 자유 입력·가격·재고·빈도는 미정이다. 대사 생성이 거래·지식·방문 결과를 확정하지 않으며 D-337 실행 승인·기존 WI/Save/E는 변경하지 않는다.
- 상세: [공동체 방문 문답 Q-355](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-355-npc의-분야-화두와-거래-제안).

## D-339 기획 답변을 기록한 뒤 다음 핵심 질문을 이어서 제시한다

- 상태: `Accepted` (문답 진행 방식)
- 결정일: 2026-08-30
- 근거: 사용자가 문답 하나가 끝나면 별도 요청 없이 계속 이어지도록 설정해 달라고 요청했다.
- 확정: 답변 기록·중복 확인 뒤 같은 응답에서 후속 질문 하나를 제시한다. 분야별 공백을 균형 있게 고르고 새 추천은 `Asked`로 유지한다.
- 경계: 사용자 중단/다른 요청 우선, 무응답 시 독촉·예약 자동화 없음, 미래 질문 자동 동의나 실행 승인 아님.
- 기준: [문답 정밀화 체계](../Architecture/PlayableLoop문답정밀화체계.md#답변-뒤-다음-질문을-이어가는-기본-진행).

## D-340 NPC 물품 외상을 도시 은행·계좌 상환 경로와 연결한다

- 상태: `AcceptedDirection / FutureExtension` (Q-356)
- 결정일: 2026-08-30
- 근거: 사용자가 NPC 외상으로 플레이어 채무/NPC 채권이 생기고 도시 은행 또는 계좌 입금으로 상환되는 게임 장치를 요청했다.
- 확정 방향: 물품을 먼저 사용하고 해당 NPC 채무를 상환하는 경로가 City 접근·교류의 동기가 된다. 은행은 상환 경로이며 NPC 채권자 의미를 유지한다.
- 경계: 현금 대출·이자·한도·기한·연체·분할 상환은 미정, 물물교환 추천 전체 승인 아님. 선택하지 않은 플레이어의 강제 채무/City 선행·실제 금융 연결 없음. 단순 예치와 특정 채무 상환 구분 필요. 기존 실행 승인·Save·E는 변경하지 않는다.
- 상세: [방문 거래 Q-356](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-356-첫-방문-거래의-대가-마련). 다음 Q-357은 미승인 질문이다.

## D-341 연체 영향을 유예·계좌 정지·신용·상단 인지로 단계화한다

- 상태: `AcceptedDirection / FutureExtension` (Q-357; D-340의 연체 미정을 방향 확정으로 보완)
- 결정일: 2026-08-30
- 근거: 사용자가 상환 유예 후 계좌 거래 정지·신용 저하, 장기 지속 시 City/Town 상단의 인지와 타 NPC 거래 불이익을 요청했다.
- 확정: 단계적 영향과 장기 지속 시 정보 인지를 구분한다. 즉시 모든 NPC에게 전파되는 전역 불이익으로 간주하지 않는다.
- 경계: 계좌 제한 범위·기간·신용/거래 수치·완납 후 회복은 미정. 게임 거래 신용은 개인 내면/호혜/실제 금융 신용과 분리하며 LLM은 판정을 소유하지 않는다. 기존 실행 승인·Save·E 변경 없음.
- 상세: [방문 거래 Q-357](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-357-상환-지연-시-플레이-영향). Q-358의 상환 통로 보존은 미승인 추천이다.

## D-342 계좌 거래 정지 중에도 입금·채무 상환을 허용한다

- 상태: `AcceptedDirection / FutureExtension` (Q-358 Confirmed; D-341의 상환 통로 미정을 보완)
- 결정일: 2026-08-30
- 근거: 사용자가 계좌 정지 중 입금과 상환을 허용하는 제안에 동의했다.
- 확정: 계좌 정지가 입금·해당 채무 상환 통로를 막지 않는다. 단순 입금과 특정 채무 상환을 구분한다.
- 경계: 일반 출금/송금/구매·자동 상환·완납 후 해제/신용 회복은 미정이다. 실행 승인·코드·Save·E 변경 없음.
- 상세: [방문 거래 Q-358](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-358-계좌-정지-중-상환-통로). 다음 Q-359는 미승인 질문이다.

## D-343 연체 해소 후 계좌 제한을 풀고 신용은 점진 회복한다

- 상태: `AcceptedDirection / FutureExtension` (Q-359 Confirmed; D-342의 완납 후 처리 미정을 보완)
- 결정일: 2026-08-30
- 근거: 사용자가 완납 후 해당 계좌 제한 해제와 정상 거래/약속 이행에 따른 점진 신용 회복 제안에 동의했다.
- 확정: 정지 원인 연체를 해소하면 해당 제한을 해제한다. 신용은 즉시 초기화하지 않고 이후 정상 거래·약속 이행으로 회복한다.
- 경계: 다른 미해결 정지 사유의 자동 해제 아님. 회복 속도/상한·상단 정보 갱신 시점은 미정이며 코드·Save·E/실행 승인 변경 없음.
- 상세: [방문 거래 Q-359](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-359-완납-뒤-계좌신용-회복).

## D-344 첫 약초차는 불 꺼짐 중 재료·진척을 보존한다

- 상태: `AcceptedDirection` (Q-360 Confirmed)
- 결정일: 2026-08-30
- 확정: 불이 꺼지면 첫 차의 진행만 멈추고 재료·기존 진척을 유지한다. 연료 보충/재점화 후 이어 달인다.
- 경계: 자동 점화/음용·모든 후반 처방 무손실·오프라인 진행 승인 아님. 기존 실행 명세·Save·E 변경 없음.
- 상세: [약초 Q-360](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-360-첫-약초차의-불-꺼짐과-재개).

## D-345 후반 개척 지역의 선택형 멀티플레이에 거래 신용을 연계한다

- 상태: `AcceptedDirection / FutureExtension` (Q-359 후속 사용자 보완)
- 결정일: 2026-08-30
- 방향: 초기 NPC 파산은 제외하고 후반 미상환의 NPC 경제/파산 가능성을 고려한다. 후반 개척 지역의 멀티플레이 교류를 지향하되 로컬 Solo 선택을 유지한다. NPC 거래 신용 문제는 다른 플레이어와의 거래 제한에도 영향을 주는 방향이다.
- 경계: 자동 온라인 전환·공개·로컬 신용 무검증 반입 없음. 파산/개척 문턱·제한/정보 공개 범위·연결/회복 방식은 미정. 현재 실행 승인·코드·Save·E 변경 없음.
- 상세: [방문 거래 후반 확장](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md#q-359-후속-보완-후반-개척멀티플레이-거래-신용). 사용자 요청에 따라 다음 문답은 약초 제작을 우선한다.

## D-346 첫 약초차의 체온 회복과 질병 관련 효과를 시간적으로 분리한다

- 상태: `AcceptedDirection` (Q-361 Confirmed)
- 결정일: 2026-08-30
- 근거: 불명확한 음성을 재확인한 뒤 사용자가 “그렇게 진행”으로 동의했다.
- 확정: 체온은 바로 조금 회복하고, 질병 위험 완화·초기 질병 회복은 일정 시간에 걸쳐 적용한다. 첫 차는 회복 완료 대기를 강제하지 않으며 심한 질병을 치료하지 않는다.
- 경계: 효과량·시간·중첩·환경/전투 영향은 미정. 게임 규칙이며 실제 의학적 효능 주장이 아니다. 기존 실행 명세·Save·E 변경 없음.
- 상세: [약초 Q-361](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-361-첫-약초차-효과의-시간-분배). Q-362는 미승인 후속 질문이다.

## D-347 같은 첫 약초차의 지속 효과는 중첩하지 않고 시간을 갱신한다

- 상태: `AcceptedDirection` (Q-362 Confirmed)
- 결정일: 2026-08-30
- 근거: 사용자가 같은 차의 지속 효과 갱신 제안에 “그렇게 처리”로 동의했다.
- 확정: 질병 관련 회복 속도는 중첩하지 않는다. 유효한 재음용 시 남은 시간을 기본 지속 시간으로 갱신하고 누적 가산하지 않는다.
- 경계: 체온 즉시 회복의 재음용 상한·음용량·다른 처방/포션 병용·구체 시간은 미정. 기존 실행 명세·Save·E 변경 없음.
- 상세: [약초 Q-362](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-362-같은-약초차의-지속-효과-중첩). Q-363은 미승인 후속 질문이다.

## D-348 식은 첫 약초차의 질병 효과를 유지하고 재가열을 허용한다

- 상태: `AcceptedDirection` (Q-363 Confirmed)
- 결정일: 2026-08-30
- 근거: 사용자가 식은 차와 재가열 추천에 동의했다.
- 확정: 식음만으로 질병 관련 효과를 잃지 않는다. 따뜻할 때의 체온 회복 혜택은 줄어들고 재가열할 수 있다. 다시 데울 때 새 약초를 필수 투입하지 않는다.
- 경계: 부패/오염/보관 기한·휴대·재가열 비용/용기 안전·온도 수치는 미정이다. 현실 효능/식품 안전 주장이 아닌 첫 차의 게임 규칙이다. 기존 실행 명세·Save·E 변경 없음.
- 상세: [약초 Q-363](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-363-식은-약초차와-재가열). Q-364는 미승인 후속 질문이다.

## D-349 마개 달린 휴대 용기에 약초차를 담아 탐험에 가져간다

- 상태: `AcceptedDirection` (Q-364 Confirmed)
- 결정일: 2026-08-30
- 근거: 보유 자산 외형 조사 결과 제시 뒤 사용자가 휴대 방식에 동의했다.
- 확정: 액체 휴대에 적합한 마개 달린 용기에 차를 담아 탐험에 가져간다. 현장 컵 음용은 유지하며 휴대 병은 첫 자기회복의 필수 조건이 아니다.
- 경계: 특정 Prefab 단일 확정·개폐 연출·용량/가열/보온/누수 승인 아님. 자산 조사와 실제 휴대 기능 구현을 구분한다. 기존 실행 명세·Save·E 변경 없음.
- 상세: [약초 Q-364](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-364-탐험용-약초차-휴대-용기). Q-365는 미승인 후속 질문이다.

## D-350 약초차 음용 후 빈 휴대 용기를 유지해 재사용한다

- 상태: `AcceptedDirection` (Q-365 Confirmed)
- 결정일: 2026-08-30
- 근거: 사용자가 내용물만 소비하고 빈 병을 유지하는 제안에 동의했다.
- 확정: 차를 다 마셔도 휴대 용기는 남는다. 새로 만든 차를 다시 채워 탐험에 재사용할 수 있다.
- 경계: 세척·파손·액체 혼합/교체·보관 공간·구체 음용량은 미정. 기존 실행 명세·코드·Save·E 변경 없음.
- 상세: [약초 Q-365](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-365-음용-후-빈-용기-재사용). Q-366은 미승인 후속 질문이다.

## D-351 다른 종류의 차로 교체하기 전에 기존 내용물을 비운다

- 상태: `AcceptedDirection` (Q-366 ConfirmedDirection)
- 결정일: 2026-08-30
- 근거: 사용자가 다른 차를 담기 전 처리에 “비우도록”이라고 답했다.
- 확정: 다른 종류의 차를 담기 전에 기존 내용물을 비운다. 종류·양 안내 후 명시적 선택으로 처리하며 자동 폐기하지 않는 맥락을 유지한다.
- 경계: 같은 차 보충·온도/품질/제조분 합산·희석·세척은 미정. 기존 실행 명세·코드·Save·E 변경 없음.
- 상세: [약초 Q-366](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-366-잔여-액체가-있는-용기-채우기). Q-367은 미승인 후속 질문이다.

## D-352 한 번 음용할 때 1회분만 소비하고 나머지를 보존한다

- 상태: `AcceptedDirection` (Q-367 Confirmed)
- 결정일: 2026-08-30
- 근거: 사용자가 한 번 마실 때 병 전량이 아닌 정해진 음용량을 소비하는 제안에 동의했다.
- 확정: 게임용 1회 음용량만 소비하고 잔량은 남긴다. 용기별 최대 용량과 1회 행동 소비량을 구분한다.
- 경계: 구체 양·단위·1회분 미만 잔량·컵/병 입력·음용 중단은 미정. 기존 실행 명세·코드·Save·E 변경 없음.
- 상세: [약초 Q-367](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-367-한-번-마실-때-소비하는-양). Q-368은 미승인 후속 질문이다.

## D-353 질문 약 10개와 추천 답안을 묶어 검토·승인한 뒤 개발에 인계한다

- 상태: `Accepted` (기획 진행 절차만).
- 결정일: 2026-08-30
- 근거: 사용자가 약 10개 질문을 기획 담당이 고르고 적절한 답안까지 제안한 뒤 사용자 검토·승인을 받아 개발 작업실에 인계하는 방식으로 변경을 요청했다.
- 확정: 기존 질문 검색→중복 없는 구현 공백 약 10개→추천 답안과 대가 제시→전체/부분 승인·수정→기획 revision/hash·실행 명세 결속→개발 통합 담당의 전문 배분·검증·결과 반환을 기본으로 한다. 사용자 요청에 따라 묶음 크기를 바꿀 수 있다.
- 대체: D-339의 매 답변 뒤 단일 후속 질문 기본 방식. 중복 방지·분야 균형·정차 중 검토·미승인 경계는 유지한다.
- 경계: 추천 작성은 사용자 답변을 대행한 승인이 아니다. 현재 검토 묶음 HB-01/r1의 Q-368~377은 모두 Asked이며 코드·실행 원장·Save·E를 승인하지 않았다. 전체 승인도 해당 판본에 적힌 범위에만 적용한다.
- 기준: [문답 정밀화 체계](../Architecture/PlayableLoop문답정밀화체계.md), [첫 검토 묶음](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#hb-01-첫-약초차-반복-플레이-검토-묶음).

## D-354 보편 데우기 WI 아래 물·식은 차 데우기를 적용 사례로 둔다

- 상태: `AcceptedDirection` (Q-371 ConfirmedDirection).
- 결정일: 2026-08-30
- 근거: 사용자가 물도 데울 수 있으므로 보편 WI를 상위에 두고 물 데우기·식은 차 데우기를 하위로 정리하도록 제안했다.
- 확정 방향: 내용물을 데우는 공통 행위와 대상별 적용 규칙을 분리한다. 열원 자체의 점화/보충/소화 및 처방을 완성하는 달이기와 책임을 구분한다. 공통 처리를 조합하되 중복 비용·효과·행위 기록을 만들지 않는다.
- 경계: 신규 WI ID·상속 구조·게임 코드·실행 명세 변경은 아직 없다. Q-371의 냄비 한정/시간/비용 등 세부안 및 HB-01의 다른 9개 답안은 미승인이다. 물 가열을 정수·약효 발생으로 확대하지 않는다.
- 기준: [약초 Q-371](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md#q-371-재가열-용기). HB-01/r2, 약초 문답 내용 r29로 보완한다.

## D-355 기존 문답과 WI를 보편 행위·적용 사례·조합 관계로 정리한다

- 상태: `AcceptedDirection` (정리 작업 요청. 새 분류의 실행 등록 승인이 아님).
- 결정일: 2026-08-30
- 근거: 사용자가 지금까지 답변한 문답에서 보편 WI를 찾아 기존 WI와 함께 계층적으로 정리하도록 요청했다.
- 결과: [문답 기반 보편 WI 계층 정리](../Architecture/PlayableLoops/문답기반보편WI계층정리.md) r1. 기존 105개 WI를 누락/중복 없이 탐색 배치하고 기존 18군·5특화와 신규 공통 행위 후보를 구분했다.
- 기준: 비실행 군, 보편 실행 WI의 대상별 특화, 여러 WI의 절차 조합, 명상/성장의 횡단 적용을 분리한다. 사례마다 새 명령을 만들거나 부모·자식 비용·행위 기록을 중복 적용하지 않는다.
- 경계: 20개 탐색 묶음은 읽기 보조 제안이지 20개 새 WI가 아니다. 기존 관계 대장의 미분류 49개는 구현 부재가 아니며 HB-01 미승인 9개·소실 Q272~274·보류 원문을 확정으로 확대하지 않는다. 원장/코드/Save/E 변경 없음.

## D-356 HB-01의 Q368~377 추천안을 전체 승인한다

- 상태: `Accepted` (2026-08-30).
- 근거: 사용자가 직전 10개 추천 검토 표에 “어 전체 추천대로 해줘”라고 답했다.
- 범위: 잔량 음용·시험 용량·동종 보충·보편 데우기와 냄비/연료·초기 부패 보류·완료 표시·음용 중단·추위 병행·거리/LH 독립 진행·저장 재개를 확인한다. 문답 r30/HB-01 r3에 각 Q를 Confirmed로 기록한다.
- 개발 인계: [HB-01 개발 승인 보완](../Architecture/PlayableLoops/Nature약초차-HB01개발승인보완.md) `nature-herbal-tea.hb01-addendum.r1 / Approved`. 기존 r5/hash를 보존하고 추가 명세 결속 후 집중 Core 시험부터 기존 E5 상한 내 진행한다. 질문마다 새 WI를 만들지 않는다.
- 경계: 숫자는 첫 시험값, 실제 구현/검증 완료가 아니다. 후반 포션·경제·부패·새 미래 질문은 제외한다. 현재 E와 실행 원장을 이 기획 기록으로 자동 승격하지 않는다.

## D-357 FB-01 농사 생활·위임의 수정 답변과 나머지 추천안을 승인한다

- 상태: `Accepted` (2026-08-30). 대상은 Q378~386 및 재검토 Q268이다.
- 사용자는 수확 지연 손실, 사전 예산 안 NPC 구매/외상 기본 꺼짐, 숙련자 모드의 Player·NPC·Monster 밟힘 피해, 훈련 농사 NPC의 후퇴→무기고→무장 대응으로 수정하고 나머지 추천안을 승인했다. 과거 FB-01/r1의 무손실 지연·자동 구매 전면 금지·모든 모드 걷기 피해 없음 추천은 대체한다.
- 단일 답변 원문은 [FB-01/r2](../Architecture/PlayableLoops/PlanningSessions/검토묶음/FB-01-농사생활위임.md)가 연결하는 세 주제 문답 내용 r3다. [추가 개발 승인](../Architecture/PlayableLoops/Farm생활위임-FB01개발승인보완.md) `farm-life-delegation.fb01-addendum.r1 / Approved`에 초기 구현과 후속 경계를 둔다.
- 첫 위임은 지정 밭 수확/지정 보관처 운반이다. Town·City 트럭 판매 위탁은 후속 확장 방향이며 첫 농사 선행 조건이 아니다. 씨앗 자동 생산·구체 지연 손실 곡선·밟힘 피해량은 이번에 확정하지 않는다.
- Q268은 기획 Confirmed로 바꾸되 구현 Deferred/작업 결속/E는 별개다. 원장 변경과 전문 담당 배분은 개발이 수행한다. D356 등 기존 승인 개발을 중단하거나 E/Save/Scene 완료를 선언하지 않는다.

## D-358 Blender 제작은 애니메이션 전문 담당, 플레이 기획은 기획 스레드에 둔다

- 상태: `Accepted` (2026-08-30). 사용자는 Blender 관련 실제 전문 작업을 기존 애니메이션 작업실에서 진행하고 이 스레드에서는 기획을 계속하도록 요청했다.
- 애니메이션 담당은 기존 설치 여부 확인→공식 Blender 설치/환경 준비→모델·리깅·애니메이션 수정용 원본 관리→Unity 전달용 FBX 검증을 맡는다. 기존 Synty 원본은 보존하고 수정 산출물을 분리한다. 기존 설치를 임의 제거/교체하거나 유료 도구를 구매하지 않는다.
- 기획 담당은 원하는 행위·동작 느낌·대가·중단/복귀·접촉 시점과 수용 기준을 정한다. 첫 캐릭터/도끼 스윙 한 동작은 앞선 제안이며, 구체 대상과 기존 승인 범위를 개발이 대조해 결속한다. 역할 배정을 미정 동작이나 모든 자산 재제작의 승인으로 해석하지 않는다.
- 전문 결과는 애니메이션→개발 통합·검증→기획으로 반환한다. 개발은 WI/상태/AnimationCue 연결과 공유 파일 소유를 관리한다. 실제 Scene·Game View 캡처는 기존 월드·공간·배치 담당과 조율하며 Editor를 중복 점유하지 않는다.
- Synty 구매 시점 라이선스와 재가공/재배포/생성형 AI 제한을 확인한다. 외부 AI 모델 생성 서비스에 자산을 업로드하지 않는다. 도구 설치와 편집 준비는 실제 동작·게임 E 검증 완료가 아니다.
- 이번 기록은 역할·제작 경로 확정이다. 기획 스레드는 Blender 설치·Unity 패키지/Scene 변경을 직접 실행하지 않았으며 CURRENT_WORK와 통합 상태판은 개발 담당이 동기화한다.

## D-359 구매한 Synty 캐릭터의 도끼 벌목 동작 하나를 제작한다

- 상태: `Accepted` (2026-08-30). 사용자가 기존 캐릭터의 도끼 벌목 동작 제작을 승인했다. D358의 대상 미승인 상태를 이번 한 동작에 한해 대체한다.
- [제작 승인 및 수용 기준](../Architecture/PlayableLoops/Nature벌목동작-Blender제작승인.md) `nature-woodcutting-animation.design.r1 / Approved`를 따른다. 기존 WI-NATURE-06·Task와 구매 자산을 재사용하고 손/도구 접촉·발 미끄러짐·타격·반복·중단/복귀를 검증한다.
- 애니 담당이 원본/Rig 기준선·Blender 제작, 개발이 기존 권위 상태·Adapter 통합, 공간 담당이 실제 입력과 Game View 캡처를 맡는다. 기술 후보 선정과 조정은 승인 범위 내 진행하고 플레이 의미 변경만 기획으로 반환한다.
- 구매 확인은 추가 결제 요구가 아니며 공급사 편집/AI 제한을 면제하지 않는다. 공급사 원본·기존 Scene 변경을 보존하고 E·새 WI·Save 규칙을 자동 변경하지 않는다.

## D-360 Nature 기초 폐루프의 기획 문서 연결을 복원한다

- 상태: `Accepted` (2026-08-30). 개발 보고의 기존 E7/E7·Completed와 planningGate NotStarted/공란 불일치를 확인했다.
- [Nature 도끼·벌목·오두막 기초](../Architecture/PlayableLoops/Nature도끼벌목오두막기초.md) `nature-shelter-foundation.design.r1`을 기존 주제/PlayableUnit에 1:1 결속한다. 기존 약속·증거와 D359 원문/hash를 보존한다. 검사 완화나 원문 조용한 수정은 하지 않는다.
- 새 범위는 D359 한 동작이다. 주제 등록 복원은 Required 애니 연구의 Accepted/hash를 대신하지 않으며 준비와 실제 제작 관문을 구분한다. 기존 E7은 새 제작 성공 근거가 아니다.

## D-361 기존 WI와 보유 애니메이션을 대조해 부족한 표현부터 제작한다

- 상태: `Accepted` (2026-08-30). 사용자는 기존 WI·보유 애니메이션을 확인해 없는 동작을 제작하는 방향을 요청했다.
- [WI 애니메이션 보완 제작 운영](../Architecture/PlayableLoops/WI애니메이션보완제작운영.md) `wi-animation-gap-production.r1`을 따른다. 직접 재사용·보정·미결속·자산 없음·미검증/불필요를 구분하고 실제 활성 폐루프와 전환 품질 중심으로 순차 배분한다.
- 애니 조사/제작→개발 결속·통합→공간 실제 화면→기획 반환을 유지한다. 새 구매/게임 의미 변경/미승인 AI 가공을 자동 승인하지 않는다. D359 첫 벌목 제작과 다른 승인 개발은 독립 계속한다.

## D-362 LS-01 경관 구성 Q387~396 추천안을 전체 채택한다

- 상태: `Accepted` (2026-08-30). 사용자 답변 “전체를 그냥 추천대로 그렇게 채택을 해 줘봐봐.”
- 원문: [LS-01 경관 구성](../Architecture/PlayableLoops/PlanningSessions/건물공간배치/landscape-composition.inquiry.r1.md), 내용 `landscape-composition.inquiry.r2 / LS-01/r2`, 질문10개 Confirmed.
- 강변 접근 시 Barn 노출·강둑의 군집과 여백·숲 높이 전환·차분한 자연색·길 가장자리·표면 구분·수관 가림 완화·국소 야간 조명·근거리 날씨 판독·실제 상태 기반 생활 흔적을 채택한다.
- 기존 부지 확대/크기·하천·통행·자유 이동·권위 상태를 유지한다. 구체 자산/후보는 공간 연구·명세 추가 결속 및 실제 화면 검증이 필요하며 기획 승인을 Scene/E 완료로 해석하지 않는다. 개발 통합 담당이 공간 작업을 배분하고 비교 Game View와 결과를 반환한다.

## D-363 배치 엔진 고도화를 우선하고 LH는 공간 실행을 지원한다

- 상태: `AcceptedDirection` (2026-08-30). 사용자 요청: LH보다 배치 엔진 고도화를 먼저 하고 LH는 결정된 배치가 잘 나타나도록 지원한다.
- 기준: [지도구성과 세계자산배치 분리](../Architecture/지도구성과세계자산배치분리.md)의 D-363 절. 배치 엔진은 기능·점유·통행·경관 계획, LH는 셀 준비·활성·캐시·해제·재진입, Unity는 실제 객체 조립·렌더링을 맡는다. 우선순위 변경은 실행 파이프라인의 순서 역전이나 LH의 지도/배치 의미 소유를 뜻하지 않는다.
- 첫 적용은 기존 Farm/LS-01 승인 후보와 실측을 활용한 배치 품질 검증이다. LH 신규 확장은 후순위로 두되, 배치 연결을 막는 오류와 준비 능력·재진입 검증은 계속한다. 기존 애니·업무 구현도 중지하지 않는다.
- 기존 승인 원문/hash·연구/후보 관문·자유 이동·하천·생산 면적·native 크기를 유지한다. 신규 실내 제작·원경 거리·게임 규칙·Save·Scene·E 승격을 자동 승인하지 않는다. CURRENT_WORK와 실제 작업 배분은 개발 통합 담당이 동기화한다.

## D-364 Simulation·배치·LH를 하나의 공간 실행 파이프라인에서 조율한다

- 상태: `AcceptedDirection` (2026-08-30). 사용자가 배치·LH·Simulation을 파이프라인으로 통합 관리하고 실행·해제를 함께 조율하는 방향을 선택했다.
- 기준: [지도구성과 세계자산배치 분리](../Architecture/지도구성과세계자산배치분리.md)의 D-364 절. 외부 진입·진행 상태·요청 수명은 통합하고 Simulation 권위·배치 계획·LH 셀 수명·Unity 조립 책임은 분리한다.
- 지역 접근·상태 변경·이탈·취소/실패·재방문을 연결한다. 표현 해제는 작물·재고·NPC 상태 삭제나 세션 종료가 아니며, 실패한 표현이 성공한 권위 명령을 취소하지 않는다. 기존 시간/저장 규칙을 유지한다.
- D363 배치 고도화와 진행 중 Farm/LS01 소비 검증을 우선 유지한다. 기존 Coordinator·LH·정합 처리 호출 관계를 대조해 중복 조율만 정리하며 새 거대 관리자나 Simulation 권위의 Unity 이동을 요구하지 않는다.
- 실행 명세 결속과 전문 배분/상태판은 개발 담당이 관리한다. 기존 원문/hash·후보/연구 관문을 보존하며 신규 Save 의미·Scene 적용·시각 승인·E 승격은 자동 변경하지 않는다.

## D-365 첫 농사 목표 시간과 선택형 집중 타이밍

- 상태: `AcceptedDirection` (2026-08-30). [Q397](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md)은 첫 직접 농사 체험 10~15분 목표를 확정한다. 강제 제한시간·경로 또는 모든 작물의 성장 시간 통일이 아니다.
- [Q398](../Architecture/PlayableLoops/PlanningSessions/플레이어내면명상/player-mind-meditation.inquiry.r1.md#q-398-선택형-집중-타이밍과-명상-성장)은 왕복 표시·목표 영역·Space 적중 피드백과 성공 시 공통 명상 역량 성장 방향을 확정한다. 불참해도 기본 작업은 가능하다. 현실 심리 상태 측정이 아닌 게임 수행 판정이다.
- 기존 집중 타이밍 Policy·Profile·행위 기록을 재사용 검토한다. 첫 Nature 적용 코드를 농사까지 완료된 것으로 해석하지 않으며 기존 회복 지급을 모든 WI로 확대하지 않는다. 적용 WI/빈도/실패 처리/수치/접근성 연결은 원문의 미정·후속 추천을 구분한다.
- 개발은 기존 승인 작업을 계속하며 이 결정의 기술 영향과 기존 Profile 차이를 검토한다. 명세 결속·현재 E·Save·Scene·실제 입력 성공은 자동 승인하지 않는다. 상태판은 개발 통합 담당이 동기화한다.

## D-366 선택형 집중 실패 무손실과 자료 조사 전문 담당

- 상태: `AcceptedDirection` (2026-08-30). Q398 후속 답변으로 미스·불참은 기본 작업/작물 손실 없이, 성공 시에만 추가 명상 성장을 받는다. 일반 작업 비용·기존 기본 성장은 유지한다. 명상 문답 내용 r5가 원문이며 제시 빈도·적용 WI·수치는 여전히 미정이다.
- 사용자가 자료 조사 작업 스레드 신설을 요청했다. [게임 자료 조사 전문 운영](../Architecture/게임자료조사전문운영.md)에 따라 공공 데이터와 게임 자료의 출처·표본·사용 조건을 관리한다.
- D285의 전문→개발 검토·통합→기획 반환을 유지한다. 첫 작업은 기존 자료 체계 재고와 Farm 작물 근거의 작은 조사 묶음이다. 신규 게임 효과·실행 WI·E·Save 자동 변경이나 유료/대량 수집 승인이 아니다.

## D-367 기존 가격 자료의 해석을 통한 게임 교역 기회

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 Q399](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md#q-399-현실-가격-자료를-해석한-게임-교역-기회) 내용 r2를 따른다.
- 기존 KAMIS·AMS 자료를 중간 해석 계층에서 소비하고 기본 화면은 게임 거래 기회, 상세 화면은 원문과 해석 근거를 구분한다. 국가/시장 차이를 알아보며 게임 자금을 마련하는 선택 경로이며 강제 무역/현실 거래가 아니다.
- 서로 다른 시장 단계·품목·통화·단위를 직접 차익으로 취급하지 않는다. 기존 수집/표준 코드/자료 동결을 재사용 검토하며 운영 원문·게임 권위·표현을 분리한다. 세부 가격/이익/갱신/국가 대응은 미정, Q398 빈도는 별도 미정이다.
- 자료 조사의 비교 가능성과 출처 결과를 개발이 검토해 기획에 반환한다. 새 실행 WI·Save·E/Scene 변경 승인은 포함하지 않는다.

## D-368 교역 위험을 상단 보험과 직접 경로 개입으로 다룬다

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 Q400](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md#q-400-교역-위험과-상단-적재물-보험) 내용 r3를 따른다.
- 게임 교역 위험 안내, 거대 상단과 적재물 보험 계약을 통한 보상, 플레이어의 직접 경로 위협 제거를 선택 경로로 둔다. 위험 저감과 손실 보전을 구분하고 강제 가입/전투로 만들지 않는다.
- 비용·보장 범위·비율·한도·지급 시점·병행 조건은 미정이다. 몬스터 습격 손실부터 시작한다는 안은 추천이지 확정이 아니다. Q399 예상 이익 표시/계산 전체를 승인한 것으로 해석하지 않는다.
- 기존 운송/회랑 안전/계약 기록을 재사용 검토하며 현실 보험 계약이나 운영 지급이 아닌 게임 Simulation이다. 기존 개발·자료 조사를 막지 않고 새 WI/코드/Save/E/Scene 자동 변경은 하지 않는다.

## D-369 첫 보험 보장 범위와 현실에 닿는 운영 경험

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 문답](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md) 내용 r4의 Q400 후속/Q401을 따른다.
- D368의 첫 보장 범위 추천을 확정으로 대체한다. 몬스터 습격에 따른 적재물 손실을 보장하고 시세 하락·판매 실패는 제외한다. 보험료·보상 비율/한도·시점은 미정이다.
- 구매·물류 대행·사냥 공급·제조·상단 운영에서 현실과 연결되는 선택·비용·결과의 의미를 경험하게 한다. 공통 업무 구조에 원칙을 연결하되 반복 노동·강제 학습·현실 사업 성과 보장으로 해석하지 않는다.
- 짧은 선택/결과/개선점 요약은 아직 추천이다. 기존 승인 개발을 유지하며 신규 실행 명세·WI·E·Save·Scene 변경을 자동 승인하지 않는다.

## D-370 작업 결과는 짧게 요약하고 상세는 선택해서 펼친다

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 문답 Q401](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md#q-401-현실에-닿는-운영-경험과-게임의-재미) 내용 r5가 D369의 미승인 요약 제안을 대체한다.
- 작업 완료 뒤 선택·결과/확인된 원인·다음에 바꿔볼 점을 짧게 제시하고, 원가·계산·출처 상세는 사용자가 펼쳐 본다. 강제 평가표나 별도 학습 시험으로 만들지 않는다.
- 기존 결과 기록/상태 사본·Q386 요약/알림을 재사용 검토한다. 추정 원인을 사실로 만들거나 요약 열람으로 보상·세계 상태를 변경하지 않는다. 첫 적용 활동·정확한 UI와 빈도는 미정이며 실제 구현·E 증거와 구분한다.

## D-371 현실 자료에 연결된 게임 경험을 시장 통찰로 확장한다

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 Q402](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md#q-402-게임-경험에서-현실-시장-통찰로-이어지는-참고-서비스) 내용 r6가 기준이다. 현실 상품·시장 차이를 이해하는 유익한 참고 서비스 방향이며 구체 구현은 후속 확장이다.
- 원천 관측·상품 대응·게임 해석·현실 참고 비교를 구분한다. 게임 성공을 현실 수익 근거로 쓰지 않으며 유사 상품을 동일 상품으로 단정하지 않는다. 조건/비용/출처/미확인 정보를 보존한다.
- 별도 선택형 현실 참고 화면은 추천이며 UI·수집 방식·상품 범위·수익모델 미정이다. 직전 농산물 판매 첫 적용 추천은 아직 미승인이다. 실제 구매/판매/계약/유료 API·대량 수집·새 WI/E/Save/Scene 변경 승인으로 확대하지 않는다.

## D-372 게임의 재미를 우선하고 현실 자료는 선택해서 열람한다

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 Q402](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md#q-402-게임-경험에서-현실-시장-통찰로-이어지는-참고-서비스) 내용 r7이 D371의 선택형 현실 참고 화면 추천을 확정으로 대체한다.
- 게임에서는 게임 경험에 집중하고 관심이 생긴 사용자가 '현실 자료 살펴보기'를 선택해 현실 근거·조건·불확실성을 확인한다. 열람을 게임 진행의 필수 조건으로 만들지 않으며 구매·계약으로 자동 전환하지 않는다.
- 구체 UI 위치/형태·외부 링크·상품 범위·자료 접근/갱신·수익모델과 첫 농산물 요약 대상은 별도 미정이다. 기존 조사·개발을 유지하며 실제 수집/화면 구현·새 WI·Save·E 승격으로 보고하지 않는다.

## D-373 현실 자료 열람은 상품·거래 결과 상세에서 진입한다

- 상태: `AcceptedDirection` (2026-08-30). [생존경제 Q402](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md#q-402-게임-경험에서-현실-시장-통찰로-이어지는-참고-서비스) 내용 r8을 따른다.
- 상품 상세와 거래 결과 상세 화면에 '현실 자료 살펴보기' 항목을 둔다. D372의 선택형 열람을 유지하며 상시 팝업·강제 열람으로 바꾸지 않는다.
- 세부 패널·외부 링크·자료 없는 경우·수집/갱신 등은 미정이다. 기획 위치 확정과 실제 UI/자료 연결 검증은 별개이며 기존 코드·자료 조사·실행 승인 경계를 유지한다.

## D-374 게임 상품을 먼저 만들고 현실 자료의 제공은 운영자가 검토한다

- 상태: `AcceptedDirection` (2026-08-30). 생존경제 Q403/r9 및 [게임·현실 상품 자료 운영 기반](../Architecture/게임현실상품자료운영기반.md) r1을 따른다.
- 게임 상품→향후 현실 상품 후보/비교→운영자 검토/제공자료 선정 순서다. Apify는 향후 후보이며 지금 수집하지 않는다.
- 사용자 요청에 따라 운영 서버의 기본 비활성 계약·검토 상태·가상 Fixture 시험 골격을 개발에 인계한다. 기존 공통 Gateway/상품/검수 재사용을 우선한다. 실제 자료 수집·게시/통지·운영 DB 쓰기·구매·Game Save/Scene/E 변경은 제외한다.

## D-375 승인된 개발을 야간 반복 검증으로 이어간다

- 상태: `AcceptedDirection` (2026-08-30). 사용자의 수면 중 지속 개발 요청에 따라 [야간 개발 순환](야간개발순환-2026-08-30.md) r1을 적용한다. 우선 내일09:00 KST까지로 운영 시간을 가정한다.
- 개발 통합 아래 공간/동작의 실제 반복 검증·좁은 패치·패턴 재사용 검증을 진행하고 필요하면 애니메이션/Blender 전문으로 피드백한다. 실제 화면 검증 목표와 자동시험을 구분한다.
- 기획 매시간 점검은 중복 작업을 만들지 않고 기존 승인 범위를 이어준다. 새 규칙/권리/파괴적 복구 판단은 해당 작업만 보류하며 독립 개발은 계속한다. 원본 보존·Editor 단독 점유·기존 E 상한/연구 경계·외부 호출/commit/push 금지를 유지한다.

## D-376 야간 보완에 월드 경계와 배경 연속성을 포함한다

- 상태: `AcceptedDirection` (2026-08-30). 사용자가 맵과 나무가 중간에서 끊겨 보이는 현상의 야간 보완을 요청했다. [야간 월드 경계 보완](야간월드경계보완-2026-08-30.md) r1을 D375의 추가 승인으로 결속한다.
- 기존 LH·배치·표현 연결의 실제 원인을 조사하고 좁은 기술 수정·경계 왕복·재조립·UI 포함 Game View 전후·성능 검증을 진행한다. 기존 벌목/배치 작업은 유지하고 Editor 단독 점유를 조율한다.
- 표시 거리 무제한 확대·안개로 누락 은폐·새 지형/자원/H 의미 변경·고정 연구 조용한 수정은 하지 않는다. 신규 원경 구성은 연구 결속과 검증을 분리하며, 09:00 마감·E 상한·원본 보존·외부 효과/commit/push 금지를 유지한다.

## D-377 전체 문답을 E5 세계 통합 Queue로 정리해 가능한 묶음을 완성한다

- 상태: `AcceptedDirection` (2026-08-30). [전체 문답 E5 세계 통합 계획](전체문답-E5세계통합계획-2026-08-30.md) r1을 따른다. Q001~403 및 후속 질문을 기존 WI·폐루프·실제 구현/근거에 대조하고 준비된 승인 묶음부터 가능한 만큼 E5까지 연결한다.
- 하나의 canonical SimulationWorldShell 안에서 독립 영역과 실제 상태 기반 배치·동작을 통합한다. 시각 조화 목표는 E9 승인과 구별하며, 질문 수·배치 수·시험 수로 E5를 대신하지 않는다.
- 기존 실행 묶음은 계속하고 전수 대조는 병행한다. 개발 통합을 통해 공간·애니·자료 담당에 배분한다. 새 미정/후속 기능 일괄 승인·하위 상한 자동 상향은 하지 않으며 개별 명세·Accepted 연구·소유 경로를 결속한다.
- 12시간은 전체 완료 보장이 아니다. 기존 09:00 안전 마감 시 실제 달성/부분 구현/미완료·화면·다음 Queue를 반환한다.

## D-378 화면의 부유 흰색 객체와 네모난 면 겹침을 정리한다

- 상태: `AcceptedDirection` (2026-08-30). [야간 부유 객체·겹침 정리](야간부유객체-겹침정리-2026-08-30.md) r1을 따른다. 사용자 지적 대상을 공간 담당이 실제 화면/객체로 식별한 후 불필요한 표현은 제거하고 겹침은 조정한다.
- 색상만으로 일괄 삭제하지 않으며 지지면/충돌/상호작용·공급사 원본을 보존한다. 생성 원인과 재조립 재발까지 검증하고 정확한 변경 대상·복구 방법·Game View 전후를 반환한다. 대상 미확정과 실제 수정 완료는 분리한다.

## D-379 도끼 접촉 타격음과 완료 후 나무 넘어짐을 보완한다

- 상태: `AcceptedDirection` (2026-08-30). [Nature 벌목 타격음과 나무 넘어짐](../Architecture/PlayableLoops/Nature벌목타격음과나무넘어짐.md) r1을 따른다. 실제 목재 타격처럼 들리는 접촉음과 나무가 쓰러지는 동작을 요구하며 애니메이션 담당의 Blender 제작 경로를 검토한다.
- 기존 오디오 impact/fall 식별자와 나무 낙하 재고를 재사용 조사하고 Required 연구·실제 대상·명세·소유 경로를 결속한다. 완료 행위 없는 낙하/중복 재생·보상 생성은 금지하며 나무 낙하 피해·자원량·벌목 시간은 변경하지 않는다.
- 원본 불변·기존 미확인 가공 권리 경계·E 상한을 유지한다. 진행 중 입력 개선과 독립 진행하고 실제 연속 화면 및 청취 검증을 코드/파일 생성과 분리한다.

## D-380 보유 자산과 확정 기획을 대조해 애니메이션·리깅 정밀화 계획을 먼저 만든다

- 상태: `AcceptedPlanningDirection` (2026-08-30). 사용자 요청에 따라 [WI 애니메이션·리깅 정밀화 계획](../Architecture/PlayableLoops/WI애니메이션-리깅정밀화계획.md) r1을 작성한다. 계획의 신규 제작 순위·수용 기준은 `ReadyForReview`이며 일괄 제작 승인이 아니다.
- 기존 D361 재고 대조를 재사용하여 보유 동작 연결 부족, 대상별 접촉/리타깃 보완, 실제 리그·형상 보완, 신규 동작 필요, 미검사를 분리한다. FBX/Clip 이름 검색만으로 부재를 확정하거나 모든 소품을 재리깅하지 않는다.
- 개발 통합을 통해 전문 재고·연구 검토를 이어가되 새 자산 제작·코드/Scene 변경은 이 요청으로 시작하지 않는다. 기존 D359/D379 및 승인 야간 작업은 유지하고 신규 제작은 해당 기획·연구·명세·소유 경로 결속 뒤 별도 진행한다.

## D-381 벌목 개발이 장기 정체되면 농장·약초의 준비된 플레이 구간으로 전환한다

- 상태: `AcceptedDirection` (2026-08-31). [야간 플레이 구간 전환](야간개발-플레이구간전환-2026-08-31.md) r1을 D375의 추가 기준으로 적용한다. 벌목만 고집하지 않고 실제로 해볼 수 있는 구간을 늘리는 것이 목적이다.
- 개발이 현재 검증을 안전하게 마감하고, 같은 차단에 새 근거·실제 진전이 없으면 승인된 농장 관리 또는 약초 채집·달이기·섭취 중 준비된 좁은 범위를 선택한다. 기존 명세/연구/상태·입력 의존성과 공유 차단을 대조하며 모든 후보를 새로 승인한 것으로 해석하지 않는다.
- 벌목의 성공·차단·재개점을 보존하며 게임 내 벌목 시간/비용/횟수는 바꾸지 않는다. 기존 09:00 마감·원본/소유권·E 상한·외부 효과 경계를 유지한다.

## D-382 야간 목표를 여러 분야의 시각 진척 비교로 넓힌다

- 상태: `AcceptedDirection` (2026-08-31). [분야별 시각 진척 계획](야간개발-분야별시각진척-2026-08-31.md) r1에 따라 문답의 농장·약초·방위/전투·외상/상환·NPC·경관을 대조하고 준비된 분야부터 대표 화면을 확보한다. 하나의 완주보다 폭넓은 시각 확인을 이번 야간 우선 목표로 한다.
- 실제 플레이, 격리 자산/Fixture 검토, 비실행 UI 시안을 분리 표시한다. 시각 후보 준비를 허용하되 새 게임 규칙·권위 결과·미확인 가공·공식 Scene 추가·E 승격으로 확대하지 않는다. 미정/화면 없는 분야도 정직하게 기록한다.
- 전문별 독립 준비→공간 단독 캡처→개발 검토→기획의 분야별 시각 목차 반환으로 진행한다. 기존 D375/D381의 안전 경계·09:00 마감은 유지하고 같은 벌목 차단을 모든 분야의 선행으로 두지 않는다.

## D-383 애니메이션 담당을 동작 품질과 시각 성과 중심으로 운영한다

- 상태: `AcceptedDirection` (2026-08-31). 사용자 개선 요청에 따라 [동작 성과 중심 운영](애니메이션-동작성과중심운영-2026-08-31.md) r1을 적용한다. D361의 추가 운영 기준이며 기존 승인 원문/hash를 보존한다.
- 진행 중 검증은 안전 마감하고 일반 입력/UI/카메라/농장 표시 회귀의 기본 소유를 개발로 돌린다. 애니메이션은 대상 Rig/Clip·접촉·전환·중단 복귀와 전용 시험, 공간은 실제 캡처를 담당한다.
- 기존 승인 안의 준비된 동작부터 동일 조건 비교·연속 동작 근거·남은 결함을 반환한다. 격리 재생과 실제 입력 연결을 구별하며 D380 신규 제작 일괄 승인·가공 권리 해제·E 승격으로 확대하지 않는다. 개발이 기존 작업 목록/명세/상태판에 결속한다.

## D-384 프로젝트 표시명과 GitHub 저장소를 Mirror거울로 변경한다

- 상태: `AcceptedDirection` (2026-08-31). 사용자 요청에 따라 영어 표시명은 `Mirror`, 한국어 이름은 `거울`로 한다. Ssalddel/살뜰은 이전 이름으로 안내한다.
- GitHub `cheolwo/ssalddel`을 `cheolwo/mirror`로 변경했다. 저장소 ID `1280462258`, 공개 여부, 기본 브랜치 `main`을 보존했다. 변경 전후 원격 main `60a82b692644af8210255f3240ee9d4f26d65fa3` 및 개발 브랜치 `712c4a08349bcda0b7c4b0489bfb8ad2a1e7087a` 일치를 확인했다. 로컬 origin은 `https://github.com/cheolwo/mirror.git`다.
- README와 공용 컨텍스트에서 새 표시명과 호환 경계를 안내한다. 실행 중 병렬 작업을 보존하기 위해 `Ssalddel.*` 코드/assembly/package, `Assets/Ssalddel`, 로컬 Hongdal/ssalddel 경로, 기존 브랜치명, GUID·Save·승인 기획/hash는 이번 일괄 변경 대상에서 제외한다. 배포된 앱/Unity ProductName/운영 서비스 주소도 아직 미변경이며 내부 이름 이관 완료로 표현하지 않는다.
- 별도 `cheolwo/unity` 저장소 이름은 유지한다. 저장소 이름 변경은 commit/push·배포가 아니며 이번 로컬 문서 변경은 미커밋이다. 후속 내부 식별자 이관은 참조·직렬화·저장 호환과 실제 앱 영향 검증을 별도로 계획한다.
- GitHub Pages 비활성 및 현재 로컬 `.github`의 옛 저장소 직접 참조 없음까지 확인했다. 외부 연동 전체를 검증한 것은 아니다. 기존 주소의 일반 Git/웹 리다이렉트와 Actions·Pages 예외는 [GitHub 공식 이름 변경 안내](https://docs.github.com/en/repositories/creating-and-managing-repositories/renaming-a-repository)를 따른다. 이전 이름을 새 저장소로 재사용하지 않는다.

## D-385 좁은 목의 병 대신 조리 냄비로 읽히는 보유 Prefab을 적용한다

- 상태: `AcceptedDirection` (2026-08-31). [냄비 표현 교체](약초차-냄비표현교체-2026-08-31.md) r1을 따른다. 기존 CampPot01은 약초차 냄비 역할에서 제외하되 원본/과거 근거는 보존한다.
- 개발→공간 조사로 보유 자산의 실제 입구·내부·크기·배치 적합성을 확인하고, 적합한 대체 후보를 기존 연구 후속 판본/명세에 결속해 교체한다. 파일명만으로 선정하거나 사용자에게 같은 교체 의향을 재질문하지 않는다.
- 게임 용량·가열·달이기 규칙과 Mug01은 변경하지 않는다. 기존 Scene/가공 권리/E 경계를 유지하며 후보 외형과 실제 기능 적용 결과를 분리한다.

## D-386 Presentation E단계에 최소 공통 모듈과 실제 구현·증거를 연결한다

- 상태: `Approved` (2026-08-31). 사용자가 [Presentation 단계별 최소 모듈](Presentation단계별최소모듈-2026-08-31.md) 계획의 구현을 승인했다. 기존 표현 모듈 대장/명세/생성 상태판을 확장하며 별도 성숙도나 중복 대장을 만들지 않는다.
- E1 계약, E2 코드, E3 자동시험, E4 후보/필요 가공 결속, E5 실제 상태/장면, E6 품질 정제, E7 실제 플레이의 책임과 구현·시험 참조를 구분한다. 기존 E 정의·현재 성숙도·승인 상한은 변경하지 않는다.
- 첫 소비자는 한 밭 수확의 상태 표현 준비다. 기존 공통 비교기/권위 Snapshot을 재사용하고 Logic·생산량·비용·시간·Save·공개 API를 변경하지 않는다. 상태가 없으면 미연결로 반환하며 임의 주입하지 않는다.
- 과거 명세는 미연결로 읽기 호환하고 소스 존재·문서 생성·격리시험만으로 Passed/E5/E7을 선언하지 않는다. Passed 연결은 기존 EvidencePackage의 대상·단계·판본·파일 hash가 필요하다.
- 이번 구현만 재개한다. 사용자 중지 이후 야간 자동화/r129/다른 전문 작업은 계속 중지하며 Scene·Packages·Blender 가공·실제 입력/캡처·commit/push는 자동 재개하지 않는다.

## D-387 보유 Synty 자산 조사를 Presentation E4의 명시적 준비 과정으로 둔다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [단계별 Synty 자산 조사](Presentation단계별Synty자산조사-2026-08-31.md) r1을 D386의 추가 기획으로 기록한다. 기존 원문과 E 정의는 보존한다.
- E2에서 기존 목록을 선행 조회하고 E4에서 대상별 후보·적합성·재사용/보완/신규 제작/미검사를 명시한다. E5 실제 연결과 E6~E7 품질·플레이 검증은 별개이며 E8~E10을 새 제작 단계로 재정의하지 않는다.
- 기존 조사 근거를 재사용하고 변경분만 확인한다. 모든 WI의 전수 조사나 임의 자산 가공·구매·Scene 적용을 승인한 것은 아니다.
- 기획·문답은 기획 스레드, 개발·테스트·기술 통합은 개발 전문 스레드가 소유한다. 공간/애니메이션 전문 조사 배분과 결과 인수도 개발이 조율한다. D386의 이전 기획 담당 기술 쓰기 배정은 이 최신 역할 분담으로 대체한다.

## D-388 논리가 선행한 기존 기능에도 자산 조사와 표현 준비를 적용한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [Logic 선행 기능의 Presentation 균형 계획](Logic선행기능-Presentation균형계획-2026-08-31.md) r1에 따라 기존 Logic E3·E4 기능의 실제 자산 후보·사용 준비 공백을 조사한다. D387을 신규 기능에만 적용하지 않는다.
- 개발은 기존 WI/폐루프·승인·증거와 보유 자산을 대조하고, 먼저 Farm 한 밭 상태와 기존 조사된 약초·방문자의 누락을 기존 E4 준비/명세/목록에 연결한다. 새 Logic 확장보다 준비된 기능의 표현 격차를 우선 해소하되 전역 WIP 차단은 만들지 않는다.
- 자산 조사 없는 기능과 조사됐으나 제품 연결이 없는 기능을 구분한다. 표현 E1~E4 준비는 병행하되 E5의 논리 E5 선행 조건과 기존 승인 경계를 유지하며 숫자를 맞추기 위해 자동 승격하지 않는다.
- 이번 허용은 파일 기반 대조·보유 후보 조사·명세 준비와 담당 배분이다. 실제 Scene/가공/새 규칙은 개별 승인 범위를 따르며 r129/야간 전체 재개가 아니다. 코드·테스트·기술 통합과 상태판 갱신은 개발이 맡는다.

## D-389 Presentation E4→E5의 연결 사전검사와 인계를 공통화한다

- 상태: `Approved` (2026-08-31). 사용자가 사전검사·인계 공통화를 선택하고 [E4→E5 연결 사전검사 계획](Presentation-E4-E5연결사전검사-2026-08-31.md)의 구현을 승인했다. 범용 조립 실행기나 새 E축은 만들지 않는다.
- 후보 일치·상태 공급·대상 건전성·배치/입력·소유/해제를 읽기 전용으로 검사한다. Ready/Conditional/Blocked와 대상/판본·근거·누락·담당·재개 E를 남기고 실제 적용/해제는 기존 분야별 코드가 담당한다.
- Farm 한 밭 준비 소비자에 먼저 적용하고 방문자 준비 계약은 두 번째 시험 입력으로 재사용한다. 현재 결손을 숨기거나 관측 입력을 실제 Scene 성공으로 바꾸지 않는다.
- 개발 전문 스레드가 코드·명세 결속·시험·생성물·상태판을 통합한다. Logic/Save/공개 API·현재 E와 기존 승인 상한, r129/야간 중지·Scene/가공/실제 캡처 경계는 유지한다.

## D-390 전체 배치 기준과 개별 E5 성립·후속 조화의 책임을 분리한다

- 상태: `Approved` (2026-08-31). 사용자가 [E5 개별 성립과 전체 배치 책임 분리](E5개별성립과전체배치책임분리-2026-08-31.md) 리팩토링을 승인했다. [기준 방법론](../Architecture/플레이폐루프논리시각이중순환체계.md#전체-배치-기준과-개별-e5-성립의-책임-분리-d-390)에 반영한다.
- 전체 공간의 승인 제약은 함께 관리하되 개별 구현·검증은 기존 Goal/명세 안의 작은 실제 성립 단위로 수행한다. 필수 상태·판본·접근·컴포넌트·입력/결과·소유 결손은 차단하고, 영향 없는 다른 기능·경관 미완성을 허위 선행 조건으로 삼지 않는다.
- 준비·조립·연결의 부분 증거를 보존하지만 새 E 하위 단계나 완료율·중복 대장을 만들지 않는다. 한 대상 성공으로 전체 폐루프 E5를 승격하지 않는다. E9 영역 조화는 기존 E8 Core 둘 이상 관문을 유지하며 현재 기능을 깨는 간섭을 E9로 미뤄 통과시키지 않는다.
- 방법론에 남은 단일 WI WIP 문구는 현행 복수 작업 목록·실제 의존성·경로 소유 정책으로 정합화한다. 기획은 기준/승인, 개발은 D389 안전 통합 뒤 기존 명세·관리 도구의 필요한 보완/시험·생성물·상태판을 담당한다.
- D386~D389 원문/hash·현재 E·Logic/Save/공개 API와 중지된 r129/야간/Editor·Scene·가공·캡처·commit/push 경계를 유지한다. 이번 승인으로 실제 Farm 배치나 Scene 결손 수리를 자동 재개하지 않는다.

## D-391 시간·공간·플레이어·대상 관점으로 기존 기획과 WI를 정리한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 기존 문답·기획을 시간, 공간, 나(플레이어), 대상(상대·사물·환경)의 네 관점으로 계속 문서화하고 그 사이 WI를 연결하도록 재정리를 요청했다. [공통 기준](../Architecture/플레이어중심게임개발업무구조.md#시간공간플레이어대상으로-wi를-설명한다-d391)과 [적용 계획](시간공간플레이어대상-WI기획정리-2026-08-31.md)을 따른다.
- 네 관점은 행동의 조건을 설명하고 WI·권위 결과·표현·회복/귀환·다음 선택을 잇는다. 새 E/G/H 축이나 고정 진행 순서, 질문별 H/WI 자동 생성으로 해석하지 않는다. 공간 조건과 환경 대상, 플레이어 의도와 실제 NPC 수행자를 구분한다.
- 기존 승인 답변·연구·명세와 ID/hash는 우선 보존하고 원문 대응표로 재정리한다. 미정·제안·해당 없음·근거 미확인을 확정과 분리하며 시간 단위·규칙·수치·대상 권한을 새로 추정하지 않는다. 전체 문답이 이미 이관됐다는 의미는 아니다.
- 기획은 공통 관점·문답/의미 정리를, 개발은 기존 문답·공간 색인의 최소 보완·원문/관계 검증·회귀·상태판을 맡는다. 별도 운영 DB·중복 대장은 만들지 않고 기존 자료와 조회를 재사용한다.
- 이 정리는 새 게임 기능/Scene/가공/Save/E 승격·commit/push 또는 r129/야간 재개 승인이 아니다. 기존 별도 승인된 H1 시각조사는 그 좁은 범위로 유지한다.

## D-392 네 관점과 WI 순환으로 전체 기획 이관을 우선한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 대표 사례를 넘어 기존 전체 기획의 이관부터 먼저 진행하고 이후 문답도 같은 구조를 따르도록 요청했다. [전체 기획 이관 계획](전체기획-네관점순환이관-2026-08-31.md)을 따른다.
- D391 공통 관점과 원문 보존을 유지하고, 시간·공간·플레이어·대상의 현재 조건→선택/WI→결과→달라진 상태와 다음 선택의 분기로 정리한다. 네 관점을 순서대로 수행할 단계나 고정 지역 진행으로 바꾸지 않는다.
- 전체 항목의 목록 확보와 원문 의미 대조·관계 검토를 구분한다. 개발은 기존 색인 밖 관련 기획도 조사하고 주제별로 부분 반환하되 전체를 검토하기 전 완료를 선언하지 않는다. 미정·소실·충돌은 별도 상태로 남기며 추정 답변으로 메우지 않는다.
- 기획은 의미와 후속 질문, 개발은 구조적 이관·기존 파일 조회 도구/문서의 최소 보완·검증·상태판을 담당한다. 기존 문답/기획/연구/명세 ID와 승인hash를 보존하며 새 운영 DB·중복 대장을 만들지 않는다.
- 이번 우선순위는 문서 연결 정리다. 새 게임 규칙·H/WI 등록·Scene/Save/E·commit/push·r129/야간 재개는 승인하지 않는다. H1 시각조사는 별도 좁은 승인으로 유지하고 문서 이관 전체를 캡처 완료에 종속시키지 않는다.

## D-393 네 관점에 Sky·LH/배치·플레이어 상태·대상 시스템을 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 시간성에 Sky, 공간성에 LH와 배치 엔진, 나에 플레이어 상태창·인벤토리·장착, 대상에 NPC·몬스터·약초·사물·환경을 두고 그 상황에서의 선택이 WI로 나타나도록 정리하기를 원한다. [네 관점 시스템 대응](네관점-엔진과대상-WI연결-2026-08-31.md)을 D391/D392에 추가한다.
- 이는 기획/조회 관점의 대응이다. 실제 시간·대기·Task 판정은 Simulation, Sky는 해당 상태의 표현 경계를 유지한다. LH 셀 준비/유지/해제와 기존 배치 계획·조립 소비를 공간 파이프라인 안에서 구분하고 코드 상하관계·엔진 병합을 자동 변경하지 않는다.
- 플레이어의 권위 상태와 UI, 대상의 상태/행동 주체성과 외형을 구분한다. 네 관점은 여러 모듈을 참조할 수 있으며 같은 상태를 중복 소유하지 않는다. 자기 대상 행동이나 비공간 읽기에 가짜 대상/H1/WI를 만들지 않는다.
- 개발은 기존 전체 이관의 일곱 칸과 파일 조회를 유지하면서 관련 시스템·상태 원천·표현 소비·미연결을 대조한다. 기획 원문 보존·의미/기술 책임과 기존 중지/승인 경계는 유지한다. 새 엔진 구현·시간 규칙·게임 기능·Scene/Save/E·commit/push 승인은 아니다.

## D-394 기획의 공통 안내 말을 지금·여기·나·너·이렇게로 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 시간성=지금, 공간성=여기, 플레이어=나, NPC·몬스터·약초·사물·환경 등 대상=너, WI=이렇게라는 표현을 기획 문서에 반영하도록 요청했다. [공통 안내 말](../Architecture/플레이어중심게임개발업무구조.md#기획의-쉬운-말--지금여기나너이렇게-d394)을 따른다.
- 기존 네 조건과 선택/WI·결과·다음 선택의 일곱 칸은 유지한다. ‘이렇게’는 새 조건 축이 아니라 행동·선택 방식이며 ‘지금 여기서 나는 너에게 이렇게 한다’는 고정 진행 순서가 아니다.
- 사람용 제목/조회 안내를 연결하되 기존 코드·ID·스키마·승인 답변과 규칙은 바꾸지 않는다. 시간 경과·원격 공간·자기 대상·NPC 주체성과 읽기 UI/WI 구분도 유지한다.
- 기획 공통 문서만 보완하며 색인/생성 안내와 문서 검증은 개발에 인계한다. 이 용어 보완은 게임/Scene/Save/E·야간 작업 재개나 전체 이관 완료가 아니다.

## D-395 같은 기획 패턴을 문서·코드·자산·Unity 검증과 결과 기록으로 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 지금·여기·나·너·이렇게 패턴으로 문서와 코드를 정립하고 보유 Synty/Prefab을 조사하며 Unity에서 검증한 결과를 다시 문서화하는 절차를 원한다. [공통 왕복 절차](../Architecture/플레이어중심게임개발업무구조.md#같은-기획을-문서코드자산unity결과로-왕복한다-d395)에 반영한다.
- 같은 WI/기획 판본의 조건→코드/시험→후보/공간→실제 표현/입력→결과/다음 선택을 기존 명세·색인·보고에서 추적한다. 자산 확인을 코드 완료 뒤에만 수행하거나 모든 작업을 직렬화하지 않으며 새 E 단계·대장을 만들지 않는다.
- 실패는 기획·코드/상태·자산/배치·동작 등 실제 책임에 돌려보내고 관측과 다음 변경점을 남긴다. 코드 시험·격리 미리보기·실제 Game View의 증거를 서로 대체하지 않는다.
- D392 전체 기획 이관 우선순위를 유지한다. 개별 구현·Unity 검증은 기존 승인 주제/연구·명세/대상/경로가 준비된 범위에서 개발이 조율하며 이번 절차 합의로 중지된 모든 기능·r129/야간/Scene 수리/가공을 일괄 재개하지 않는다.
- 기획은 의미·기준, 개발은 구현·검증·통합과 기록, 공간/애니 전문은 해당 자료를 맡는다. 기존 권위·원본 보존·E 상한·외부 효과·commit/push 경계는 유지한다.

## D-396 WI 전체를 중심으로 E4까지 문서·코드·Synty 준비를 진행한다

- 상태: `ApprovedWorkDirection` (2026-08-31). 사용자는 공통 기획을 바탕으로 WI 전체의 지금·여기·나·너·이렇게/결과·다음 선택을 문서화하고 E4까지 문서·코드·시험·Synty/Prefab 준비를 진행하도록 요청했다. [WI 전체 E4 준비 계획](WI전체-E4문서코드자산준비-2026-08-31.md)을 따른다.
- D392 문답 전체 이관을 WI 중심 읽기의 근거/역조회로 연결한다. 공식 WI 전목록과 미등록 후보를 구분하고 기존 ID·승인 원문·명세/근거를 재사용한다. 새 수기 대장·필드 복제나 질문별 WI 자동 등록은 하지 않는다.
- 기존 승인 규칙과 선행 조건이 준비된 WI부터 실제 E1~E4 코드·시험·표현 후보 부족분을 보완한다. 미정·권리·충돌은 해당 항목만 반환하고 독립 작업은 진행한다. 개발이 기존 Goal·명세·경로 소유와 추가 범위를 결속한다.
- E4 Prefab 준비는 적합한 후보·사용 조건·배치/Anchor 의도와 근거를 갖추는 것이며 실제 제품 Scene·상태/입력 연결 E5를 뜻하지 않는다. 높은 기존 E는 보존하며 문서 작성이나 파일 존재로 일괄 승격하지 않는다.
- 이번 WI 전체 E4 준비를 진행하되 r129/야간·무관한 중지 작업 재개, 미확인 가공/구매·E5 일괄 배치·원본/미소유 변경·운영 효과·commit/push는 포함하지 않는다. 기획은 의미/선택, 개발과 전문 담당은 구현·조사·검증·통합 및 부분 결과 반환을 맡는다.

## D-397 실제 문답도 지금·여기·나·너·이렇게의 상황과 선택으로 이어간다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 문서 정리뿐 아니라 기획 대화 자체도 지금·여기·나·너·이렇게 틀로 이어가며 생각을 깊게 발전시키기를 원한다. [문답의 실제 진행 방식](../Architecture/PlayableLoop문답정밀화체계.md#실제-문답도-지금여기나너이렇게로-진행한다-d397)을 따른다.
- 기존 답변을 대조한 공통 상황→선택/WI→대가·결과→다음 선택을 제시한다. 시간·공간·플레이어·대상의 조건 차이를 비교하며 실제 미정만 묻고 이미 확정된 규칙이나 기술 연결 문제를 새 사용자 선택으로 되돌리지 않는다.
- D353의 약 10개 질문·추천 답안 검토 묶음과 전체/부분 승인·보류를 유지한다. 관심 부분은 작은 후속 문답으로 깊게 다루되 예시·추천·미답변을 자동 승인하지 않는다. 일곱 칸은 대화의 안내이지 고정 플레이 순서나 새 WI/H/E 체계가 아니다.
- 기존 공통 기준·D396 승인 원문은 보존한다. 기획 문답과 기존 승인 E4 준비는 병행하며 문서 검증·조회 반영·현재 상태 동기화는 개발에 인계한다. 새 게임 규칙·코드/Scene/Save·야간 재개·commit/push 승인은 아니다.

## D-398 한 회차에 질문 하나로 상황과 선택을 깊게 탐구한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 상황이 달라졌으므로 10개 묶음보다 지금·여기·나·너·이렇게 틀의 질문 하나를 깊게 이어가도록 요청했다. [문답 정밀화 체계](../Architecture/PlayableLoop문답정밀화체계.md)의 현재 기본을 단일 질문으로 변경한다.
- D353 및 D397의 10개 묶음 기본만 대체한다. 기존 확정 답변과 미답변 묶음은 보존하고, 사용자가 다시 묶음을 요청하지 않는 한 질문 하나와 추천·대가를 제시한다. 한 질문 안에 여러 결정을 숨기거나 일곱 관점을 일곱 질문으로 만들지 않는다.
- 답변으로 달라진 상황·결과·다음 선택에서 후속 질문 하나를 이어간다. 이미 답한 사실은 재사용하고 미정·설명용 가정·제안을 구분한다. 원문·Q/의미 ID·WI 연결 및 개발 인계 경계는 유지한다.
- [Q378 후속 수확 여유 질문](../Architecture/PlayableLoops/PlanningSessions/건물공간배치/harvest-ready-grace-window.inquiry.r1.md)은 이번 방식의 첫 `Asked` 기록이며, 그 추천은 아직 승인되지 않았다. 이 운영 방식 변경만으로 지연 손실·시간 수치·보상·E·실행 범위를 바꾸지 않는다. 기존 D396 승인 준비는 계속 진행한다.

## D-399 시간성을 절기 중심으로 읽고 농사·계절 상품·물류에 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 ‘지금’을 절기·계절로 파악하고 파종·수확 시기, City의 계절 상품, Hub의 입고·출고를 함께 기획하기를 원한다. [공통 시간 문맥](../Architecture/플레이어중심게임개발업무구조.md#절기를-공통-시간-문맥으로-사용한다-d399)과 [절기 문답](../Architecture/PlayableLoops/PlanningSessions/시간절기/seasonal-time-context.inquiry.r1.md)을 연결한다.
- 기존 계절별 수요·가격/전망·달력 기획을 재사용한다. 절기라는 공통 문맥을 영역별 규칙이 해석하며 특정 작물·상품의 일정/수치·성공·가격·입출고를 절기명만으로 자동 확정하지 않는다. 영역 간 이동·거래는 선택 가능한 연결이다.
- 절기 중심 기획은 하루 중 때·날씨·작업 경과·기한을 없애거나 모든 시간 단위를 절기로 치환하는 뜻이 아니다. 기존 Simulation 시간 권위와 Sky 표현·UI 판독을 유지한다. 목록·길이·현실 달력 대응·지역차·시기 외 행동 조건은 미정이다.
- D398 단일 심화 질문은 유지한다. 수확 여유 후속은 미답변 그대로이며 이번 방향으로 승인하지 않는다. 다음 파종 적기 밖 행동 질문도 `Asked`다. 기존 승인 원문·D396 범위/실행·Save·Scene·E·중지 경계를 보존하고 문서 검증/조회 연결은 개발에 인계한다.

## D-400 계절의 야생 변화와 재배 시설 대응을 기획에 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 Nature 야생에서 계절별 몬스터 종류·속성 변화, 적기를 조금 놓친 위험감수 파종, 재배 중 비닐하우스 같은 시설을 통한 수확 기회 보완을 원한다. [시간·절기 문답 내용 r2](../Architecture/PlayableLoops/PlanningSessions/시간절기/seasonal-time-context.inquiry.r1.md)와 공통 시간 문맥에 반영한다.
- `seasonal-sowing-outside-window`는 Asked→Confirmed이며 수치·실행 완성을 뜻하지 않는다. `seasonal-cultivation-facility-mitigation`과 `seasonal-nature-monster-variation`은 ConfirmedDirection으로 구분한다. 기존 Q161/163/164 계절 속성·관찰 기준과 온실의 생산 조건 역할을 재사용하며 새 WI를 자동 등록하지 않는다.
- 시설은 조건을 보완할 기회이지 설치 즉시 수확 성공·지난 손실 전량 복구·모든 계절 무위험 재배가 아니다. 출현 종류와 기존 개체 속성 전환 시점·난방/연료·시설 능력·비용·작물 대응은 미정이다. Nature 기존 ID/의미와 Simulation 권위·Sky 표현 분리를 유지한다.
- 다음 `seasonal-greenhouse-protection-limit`은 Asked이며 기본 시설의 보호 한계/추가 준비 추천은 미승인이다. 수확 여유 Asked도 유지한다. 기존 승인 원문/FB01/D396 및 시간·Save·Scene·E·중지 경계를 변경하지 않으며 개발에는 문서 검증·색인 상태 동기화를 인계한다.

## D-401 재배 추위 대응을 난방과 마법진까지 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 기본 비닐하우스의 보호에 더해 난방을 활용하고, 더 심한 추위에는 기존 울타리 바깥 마법진 구상과 연결해 마법진도 활용하기를 원한다. [시간·절기 문답 내용 r3](../Architecture/PlayableLoops/PlanningSessions/시간절기/seasonal-time-context.inquiry.r1.md)에 기록한다.
- `seasonal-greenhouse-protection-limit`은 Asked→Confirmed, `seasonal-cultivation-magic-protection`은 ConfirmedDirection이다. 기존 Q080/Q084 및 Q025/Q030의 보호·토양 회복·물리/마법 방어 역할은 재사용하되 해당 효과를 보온으로 자동 전용하지 않는다.
- 비닐하우스·난방·마법진은 준비 수단이지 강제 구매 순서가 아니다. 성장/학습 자격을 생략하지 않으며 범위·성능·마력/재료·중첩·중단/손실 회복은 미정이다. 시설 설치/마법진 발동만으로 수확 성공이나 영구 무비용 보호를 보장하지 않는다.
- 다음 `seasonal-magic-protection-unattended`(미리 마력을 채워 두고 자리를 떠날 수 있는 유지 방식)는 Asked로 남긴다. 기존 수확 여유 Asked 및 D396 승인 준비는 유지하며 코드·마나/시간/Save·Scene·E·가공/야간 재개·commit/push 승인은 추가하지 않는다. 문서 검증·색인 및 현재 상태 동기화는 개발에 인계한다.

## D-402 발전 수단을 기능 군집과 테크트리로 관리하고 정보를 점진 공개한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 문답에서 확장되는 발전 스펙트럼을 관련 군집으로 묶어 관리하고 향후 테크트리 UI에서 이해·계획·접근하기 쉽게 하기를 원한다. 이어서 큰 방향은 미리 보여주고 구체 내용은 발견·학습하며 드러내는 추천에 동의했다. [발전 군집과 테크트리 계획](문답기반-발전군집과테크트리-2026-08-31.md)이 승인 의미의 원문이다.
- `inquiry-progression-clusters`는 ConfirmedDirection, `tech-tree-progressive-disclosure`는 Confirmed로 기록한다. 기능 군집과 실제 필수 선행·선택 대안·병행 보완을 구분하고, 모든 스펙트럼을 직선 해금 순서로 바꾸지 않는다. 기존 영역 독립성과 선택형 전문화를 유지한다.
- 기존 건물 발전·WI·지식·공간 원문을 참조하는 별도 조회 관점이며 중복 DB/실행 원장을 만들라는 뜻이 아니다. 노드와 WI/H/E를 일대일 대응시키지 않으며 공개·학습·해금·사용을 구분한다. 미발견 세부 기술·레시피·획득처의 전면 공개나 UI 열람 보상은 승인하지 않는다.
- D401 검증창 동안 대화·개발 인계에 보관했던 내용을 완료/쓰기 보류 해제 뒤 문서화했다. 과거 검증 hash는 당시 이력으로 보존한다. 기존 수확 여유·마법진 부재 보호 Asked는 미변경이며, 실제 새 테크트리 코드/UI·규칙·Scene/Save·E·r129/야간·commit/push 재개는 아니다. 개발에는 문서 검증·색인 영향/재사용 조사와 최신 상태 동기화만 인계한다.

## D-403 한국 24절기를 기준으로 농수산물 제철 자료를 조사·연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 한국에서 쓰는 24절기를 기획에 활용하고 농산물·수산물의 제철 자료를 조회·정리하며 전산화해 관리하는 방향을 원한다. [시간·절기 문답 r4](../Architecture/PlayableLoops/PlanningSessions/시간절기/seasonal-time-context.inquiry.r1.md)와 [제철 자료 조사 계획](24절기-제철자료-조사와기획연결-2026-08-31.md)에 기록한다.
- `korean-24-solar-terms-planning`은 Confirmed(명칭·순서 기준), `solar-term-seasonal-food-research`는 ConfirmedDirection이다. D399의 목록 미정을 대체하되 게임 절기 길이/현실 동기화/지역별 기후·작물 수치·가격은 미정이다.
- 공식 원천의 월별 추천·제철·파종/수확·어획/출하와 지역·품종/어종·방식을 구별하여 원천 사실→검토한 절기 대응→게임 해석을 연결한다. 현실 자료는 게임 결과의 직접 권위나 일괄 성공·이익 보장이 아니다.
- 기존 자료조사 담당에게 개발 경유로 24절기 기준과 농산물3/수산물3 이하의 첫 소수 표본을 배분한다. 기존 GameData/품목/출처 관리를 재사용한 파일형 자료 우선이며 운영 DB/Provider·대량수집·유료 접근·재배포 권리 자동승인·게임/Scene/Save 변경은 포함하지 않는다.
- 기획 원문 외 색인·검증·공유 조사 통합·현재 상태는 개발 소유다. D396 승인 준비 및 기존 두 Asked, r129/야간/Editor 중지·E·commit/push 경계는 유지한다. 이번 초동 출처 확인과 향후 전문 표본 확보는 구분해 보고한다.

## D-404 자료조사에서 로그인 등 접근 조치가 필요하면 먼저 보고한다

- 상태: `AcceptedOperatingDirection` (2026-08-31). 사용자는 자료조사 담당이 공공데이터 로그인 등이 필요한 경우 사용자에게 보고하도록 요청했다. [자료조사 운영의 접근 필요 보고](../Architecture/게임자료조사전문운영.md#로그인활용신청이-필요할-때-먼저-보고한다-d404)를 공통 기준으로 보완한다.
- 전문 → 개발 → 기획 → 사용자 경로로 사이트/공식주소·대상자료·확인된 차단/필요이유·사용자 조치·비용/권한 미정·공개 대안/독립 진행 범위를 부분 반환한다. 단순 연결/추출 오류를 로그인 필요로 단정하지 않는다.
- 비밀번호·인증번호·키 원문을 보고/대화로 요구하지 않으며 로그인/가입/키발급/약관동의/결제의 자동 실행 승인은 아니다. 해당 자료만 대기하고 독립적인 공개 자료 조사는 계속한다.
- 기존 D403 원문·소수 표본 범위/자료 권리 경계와 D396·두 Asked·게임/Scene/Save·중지 상태는 불변이다. 개발에 담당 전달과 문서/조회 영향 검증·현재 상태 동기화를 인계한다.

## D-405 절기별 산·숲 경관을 월드맵·배치·LH 협력으로 표현한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 보유 Synty 자산으로 산·나무·숲을 구성하고 절기에 따라 색감이 달라지기를 원하며 월드맵·배치·LH 세 엔진의 협력을 제안했다. [시간절기 문답 r5](../Architecture/PlayableLoops/PlanningSessions/시간절기/seasonal-time-context.inquiry.r1.md)의 `seasonal-landscape-appearance`/`seasonal-spatial-engine-coordination`을 ConfirmedDirection으로 기록한다.
- [기존 공간 파이프라인의 D405 책임](../Architecture/지도구성과세계자산배치분리.md#절기별-산숲-표현과-세-공간-엔진의-협력--d405)을 따른다. 지도구성은 지역 구조/기준점, 배치는 자산/군집·계절 표현 계획, LH는 셀 수명/재진입을 맡고 실제 재질·LOD 적용은 Unity 표현에서 수행한다. Simulation 시간/상태 권위와 Sky 빛·기상은 별도 책임으로 협력한다.
- D363/D364 실행 순서를 유지하며 LH가 객체 배치/해제를 다시 소유하거나 새 최상위 엔진을 추가하지 않는다. 색 변화는 배치 재생성·채집량·충돌/통행·수확 완료가 아니며 원본/공유 재질을 변경하지 않는다.
- 정확 팔레트/수종/지역·전환 속도·첫 후보·적설/낙엽 효과는 미정이다. 24벌 자산 제작을 강제하지 않으며 기존 계절표현 hash·Synty/LS01 근거를 먼저 파일 조사해 준비안을 반환한다. 기존 연구 수용을 새 가공 승인으로 전용하지 않는다.
- 기획4문서 외 개발은 문서/색인 검증·기존 코드 조사·소유 배분, 공간은 기존 자산 파일 조사만 우선한다. 새 Editor/촬영/Scene·Save·E·가공·야간/r129 재개·commit/push 승인은 아니다. D403/D404 조사와 D396 독립 준비 및 기존 두 Asked는 유지한다.

## D-406 초기 산재 표시를 정리하고 월드맵 기반·실제 보행을 우선한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 Play 전 Game View에 흩어진 사람·집·창고 등을 정리하고, 먼저 큼직한 월드맵을 마련해 실제 플레이어 이동을 확인한 뒤 농장·Town·City 등을 배치하기를 요청했다. [월드맵 우선 실행 계획](월드맵우선-초기표시정리와보행검증-2026-08-31.md)이 범위 원문이다.
- 이번 순서는 초기 표시 식별/정리→기존 지도/지형 연결과 큰 구도→실제 보행/재진입→영역별 배치다. D363 당시 배치 우선순위를 이번 범위에서 조정하고 D364/D405 역할·조립 순서, D390 개별 성립과 독립 영역 원칙은 유지한다.
- 불필요한 임시/중복/비결속 표시만 정확히 확인하여 가역 분리·비활성 또는 생성 복사본 제거한다. 기능 대상/Player/원본/Save·H/세계 상태를 삭제하거나 전체 Root·미소유 dirty를 일괄 정리하지 않는다.
- 개발이 기존 승인/연구·명세·정확 경로를 결속하고 공간 담당이 단독 Editor/Play/입력/Game View를 수행한다. 본인 변경만 분리·백업·차이 확인 가능한 해당 Scene 저장은 허용하되 새 공식 Scene·전체 Builder·강제 복구·새 게임 규칙/Save 주입·패키지 변경·commit/push는 제외한다.
- 초기 화면 정리·월드맵 구도·실제 보행은 각각 증거로 반환하며 이미지나 문서만으로 실제 이동/전체 E5를 선언하지 않는다. 지형 크기/새 지리·도시 위치는 기존 근거를 우선하고 새 선택이 필요한 부분만 반환한다. D403/D404 및 D396 비의존 작업은 병행, r129/야간 자동화·무관한 작업 중지는 그대로다.

## D-407 네 업무영역을 지형지물로 구분하는 월드맵 제안을 작성한다

- 상태: `AcceptedPlanningDirection` / 상세 제안 `ReadyForReview` (2026-08-31). 사용자는 City·Town·Farm·Hub를 오픈월드 안에서 자연스럽게 구분하고, 보유 Prefab을 조사하여 각 영역의 특성을 살리는 제안서를 요청했다. [네 업무영역 자연 경계와 자산 선정 제안](월드맵-4업무영역-자연경계와자산선정제안-2026-08-31.md)에 기존 자료와 추천안을 연결한다.
- 하나의 연속된 지형에서 능선·하천·숲 가장자리·여백·건물 밀도/윤곽으로 장소를 읽는 방향이다. 구체 지형·최종 Prefab·좌표·경로·팔레트는 제안이며 일괄 채택이 아니다. 색 변화만으로 영역을 판정하거나 강제 Farm→Town→Hub→City 순서를 만들지 않는다.
- 기존 H5 기준점과 AreaSet/H/WI를 재사용한다. Farm 생산·Town 저층 생활·Hub 물류·City 고밀 서비스의 독립성을 유지하고, 기존 `CityHub`는 Hub 호환값으로만 읽는다. 현재 City `Reserved`를 실제 통행·업무 활성화로 올리지 않는다.
- 현재 13팩 기능 대장과 기존 실측/연구를 우선해 역할별 소수 후보를 조사하는 방법을 제안한다. 실제 파일·크기·재질/LOD·접지/Collider·출입/작업 접근을 별도로 확인하며 Prefab 존재·격리 이미지·H 조립·Game View를 구별한다. 새 구매·가공·원본 수정·새 H/WI/Scene·E 승격 승인은 아니다.
- 기획 쓰기는 제안서와 이 결정 두 파일이다. 개발에 문서 검증·기존 목록/기준 참조 대조·전문 파일 조사상의 결손 반환 및 현재 상태 동기화를 인계한다. 실제 네 영역 배치/추가 Editor 실행은 상세 검토와 명세 결속 뒤 진행한다. D406의 기존 허용 작업과 현재 Editor 부재 차단을 별도로 유지하고, r129/야간·무관한 중지 작업·commit/push를 재개하지 않는다.

## D-408 발견형 구도를 첫 인과 기록으로 남기고 기존 문답을 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 발견하며 알아가는 구도를 채택하고 이를 첫 번째 인과 기록으로 남기되 게임에서 고정된 첫 순서로 확정하지 않았다. 기존 Q1~400번대 답변을 활용하여 플레이를 지금·여기·나·너·이렇게→결과→다음 선택으로 점차 문서화한다. [첫 플레이 체감 문답 r3](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md)가 승인 의미를 소유한다.
- `discovery-led-play-causality`와 `inquiry-causal-flow-reuse`는 `ConfirmedDirection`이다. 인과 기록 01은 단서→관심/판단→선택한 접근/관찰→달라진 판독 정보→다음 선택을 설명한다. 기록 순서와 플레이 필수 순서, 관찰과 권위 지식/지도 해금·보상을 구분한다. 새 전체 Q 번호·WI/H는 만들지 않는다.
- 춘분·낮·숲 가장자리·들판·건물/연기는 직전 설명용 예시로 남긴다. 시작 위치·첫 단서·카메라 모드·정확 절기/날씨·D407의 상세 배치/자산 전체를 승인한 것이 아니다. 기존 자유 이동·전술 시야·재집중을 없애거나 강제 경로/안개 지도·발견 보상을 도입하지 않는다.
- [공통 관점](../Architecture/플레이어중심게임개발업무구조.md#첫-인과-기록에서-기존-문답의-플레이-흐름을-이어간다-d408)에 연결하고 D391/392/394/398의 원문 보존·기존 파일색인·단일 질문 방식을 재사용한다. Q079·Q085/098·Q090·Q109와 기존 전술 이동의 근거를 첫 대응으로 삼되 해당 원문 의미·Q39710~15분과 다른 Asked는 보존한다. 기존 답변 재정리를 신규 규칙/실행 전체 승인으로 해석하지 않는다.
- 기획 쓰기는 첫 플레이 문답·공통 관점·이 결정 세 파일이다. 개발에 두 의미 ID/순환 조회 연결, 원문·상태·참조·신선도 검증 및 관련 작은 묶음의 기존 답변 대조/현재 상태 갱신을 인계한다. 전체 의미 이관 완료·E4/E5·Scene/Save 적용과 분리한다. D406의 현재 화면 후속은 계속 가능하며 r129/야간·무관한 중지 작업·commit/push를 재개하지 않는다.

## D-409 날씨별 발견 난도를 확정하고 전투 활용은 후속으로 분리한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 비·안개에 따라 발견 난도가 달라지는 추천을 채택하고, 향후 전투의 유리/불리한 조건으로도 날씨를 활용하되 지금은 발견 난도에 집중하도록 요청했다. [첫 플레이 체감 문답 r5](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md#인과-기록-01-후속--비와-안개-속-발견-단서)가 승인 의미를 소유한다.
- `discovery-cue-weather-readability`는 `Asked`→`Confirmed`다. 먼 단서는 날씨에 따라 약해지지만 접근 가능한 가까운 구간의 기존 흔적을 판독할 수 있게 하며, 날씨 호전을 기다리는 것을 발견의 필수 조건으로 만들지 않는다. Q052/053의 연기·천/솥·난이도별 표식 및 잔불 상태를 유지한다. 자동 발견·안전 통행·무료 열원 복구를 보장하지 않는다.
- 전투 활용은 `FutureExtension`이다. 기존 Q163/164와 연결하되 전투 수치·명중/피해·은신/탐지 AI·몬스터 속성 변화의 추가 승인이나 이번 구현 배분이 아니다. 정확 가시거리·안개/강수 임계값·단서 배치·LOD·첫 절기/시간은 여전히 미정이다.
- 기획 쓰기는 첫 플레이 문답과 이 결정 두 파일이다. 개발에 기존 색인/순환 조회 상태 갱신·원문 검증·현재 상태 동기화와 발견 단서 표현의 기존 근거/모듈 재사용·결손 조사를 인계한다. 새 전투 작업·가공·Scene 일괄 배치·E/Save·r129/야간·commit/push로 확대하지 않는다. D408 인과 기록/Q397·기존 다른 Asked·D406 실제 화면 후속은 보존한다.

## D-410 잔여 문답을 현재 관점으로 전량 대조하고 리팩토링한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 미검토 389항목도 지금의 관점으로 검토·리팩토링하도록 요청했다. [기존 전체 이관 계획 r2의 잔여 검토](전체기획-네관점순환이관-2026-08-31.md#7-잔여-389항목의-원문-대조와-관점별-리팩토링-d410)를 실행 기준으로 삼고 D392의 원문 보존·단일 관계 입력을 유지한다.
- 시작 454항목 중 대조65/미검토389를 기준으로 정확 잔여 ID·원문 판본 목록을 개발이 확정한다. 기존 소실3은 별도 근거 문제로 유지하며 새 유입/원문 변경 재검토를 잔여 처리량과 분리한다. 전체 목록화나 미검토의 일괄 미정 치환을 의미 대조 완료로 세지 않는다.
- 원문 답변을 지금·여기·나·너·이렇게→결과→다음 선택으로 압축·연결하고 확정/미정/제안/해당 없음/근거 소실을 구분한다. 기존 Q·의미 ID·WI/H·원문hash·승인 의미를 보존하며 원문에 없는 시간·절기·수치·실패/회복·선행 관계를 만들지 않는다.
- 개발 통합 담당이 분야별 비중첩 원문 조사와 기존 관계/생성뷰 통합·검증·현재 상태 보고를 조율한다. 기획은 해석 충돌과 새 선택을 받아 단일 질문으로 이어간다. 주제별 부분 반환 뒤 다음 독립 묶음을 이어가며 대표 사례만으로 전량 완료를 선언하지 않는다.
- 기획 쓰기는 이 결정과 기존 이관 계획 두 파일이다. 코드·자산/이미지 참조의 조사와 실제 구현/Unity 증거를 분리한다. 새 게임 규칙·WI/H·E·Scene/Save·가공·운영 DB·외부 수집·commit/push 및 r129/야간 재개 승인은 아니다. D406 독립 승인 후속을 전체 문답 검토 완료까지 대기시키지 않는다.

## D-411 숲 가장자리와 농장 외곽부터 플레이 경험을 구체화한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 Farm·Town·Hub·City의 월드맵 영역 구분을 유지하면서, 여기서는 숲 가장자리↔농장 외곽의 플레이 경험부터 구체화하기로 했다. [첫 플레이 체감 r6의 선정 기록](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md#먼저-구체화할-구간--숲-가장자리와-농장-외곽-d411)의 `forest-farm-edge-planning-focus`를 `Confirmed`로 기록한다.
- D407 대표 구간의 기획 대상 선정이며 전체 네 영역 배치/자산/좌표·경로 승인이나 게임 필수 시작 장소·스폰·튜토리얼 결정이 아니다. 기존 AreaSet/H와 City 예약, 영역별 독립성은 유지한다.
- 지금·여기·나·너·이렇게→결과→다음 선택으로 발견·접근·관찰·귀환을 구체화하고 D408 인과 기록/D409 날씨별 발견 난도를 재사용한다. 폐야영지 단서나 모든 WI를 이 구간에 자동 배치하지 않는다. 실제 지식·소유·수확·통행 권위는 보존한다.
- 후속 `forest-farm-edge-reference-solar-term`의 춘분 기준안은 `Asked`다. 이번 답변은 특정 절기·시작 날씨·작물/수종/색감·시간 규칙을 확정하지 않는다.
- 기획 쓰기는 첫 플레이 문답과 이 결정 두 파일이다. 개발에 기존 조회·문서 검증과 현재 상태 동기화를 인계하고 D410 시작389와 새유입·변경분을 구분한다. D406 독립 정리/보행과 기존 승인 준비는 유지하되 새 게임/Scene·Save·E·r129/야간·commit/push를 재개하지 않는다.

## D-412 춘분을 기획 기준으로 삼고 보유 경관 자산을 먼저 조사한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 숲 가장자리↔농장 외곽의 기준을 춘분 무렵으로 채택하고, 폭포·계곡·절벽·협곡 등을 표현할 보유 자산을 구체 구현 전에 조사하도록 요청했다. [첫 플레이 체감 r7](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md#기준-절기--춘분-무렵-d412)의 `forest-farm-edge-reference-solar-term`은 `Asked`→`Confirmed`다. 게임 시작일·시간/날씨·실제 수종/작기까지 정한 것은 아니다.
- `forest-farm-edge-landscape-asset-survey`는 `Confirmed`이며 [파일 조사 범위와 결과 양식](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md#구현-전-경관-자산-조사--폭포계곡절벽협곡-d412)을 따른다. 사용자 표현 CGS/CTS는 실제 보유 팩명 확인 대상으로 남기고 기존 Synty 13팩·Nature/Alpine Mountain과 관련 물/효과·지형 후보를 우선한다.
- 완성된 단일 Prefab, 모듈·물/효과 조합, 추가 지형이 필요한 경우와 조사 범위 내 미발견을 구분한다. 파일 경로·GUID/hash·근거 종류·확인/미검사·역할과 후속 검증을 연결한다. 네 경관 모두 배치, 경관으로 인한 새 위험/통행·전투·채수 기능이나 원본 수정은 승인하지 않는다.
- 개발→공간의 기존 담당 경로로 파일 조사하고, 개발이 문서·조회/원문 보존 검증 및 CURRENT/통합판을 맡는다. 기획은 첫 플레이 문답과 이 결정 두 파일만 수정한다. 조사 자료는 E2/E4 준비에 쓰되 E 자동 승격·실제 E5/Game View로 세지 않는다.
- D410 시작389와 새유입/원문변경분을 구분하며 검토를 계속한다. D406 독립승인 범위는 유지한다. 이번 새 Editor·렌더·Scene/Prefab/재질·Blender·게임코드/Save·H/WI·외부구매·r129/야간·commit/push는 허용하지 않는다.

## D-413 춘분 무렵의 약초와 농작물 자료를 경관 기획과 병행 조사한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 춘분 무렵 자라는 약초·농산물도 먼저 조사하여 이후 경관과 종합하기를 요청했다. [첫 플레이 체감 r8](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md#춘분의-약초식물농작물도-함께-조사한다-d413)의 `spring-equinox-herb-crop-research`는 `Confirmed`다.
- [기존 조사 계획 r2의 춘분 범위](24절기-제철자료-조사와기획연결-2026-08-31.md#6-춘분-무렵의-약초식물농작물-사전-조사-d413)를 따른다. 기존 D403/농사로 자료부터 재사용하고 야생 식물·약초와 농작물을 소수 공식 근거로 조사한다. 첫 묶음은 각4종 이내/총8종 이내이며 수를 채우기 위한 추정은 하지 않는다.
- 생육·새순/꽃·파종/정식·수확/채집·출하/제철을 구분하고 지역·고도/기후·노지/시설·종/품종과 근거 정밀도를 남긴다. 봄·3월을 정확 춘분으로, 시장 공급을 주변 생육으로 전용하지 않는다. 식용 나물과 약초 분류·현실 자료와 게임 약효/수치도 분리한다.
- 개발→기존 자료조사 담당→개발 검토·통합→기획으로 반환하고 D412 경관 조사와 서식 문맥/표현 후보를 나중에 대조한다. 자료조사 담당은 전용 D413 조사3파일, 개발은 통합·검증/상태판, 기획은 이 결정·첫 플레이 문답·기존 조사 계획3파일을 맡는다. 새 DB·공통 상품/자산 원장을 만들지 않는다.
- D404 접근 필요 선반환과 권리 경계를 유지한다. 실제 채집/복용 안전·치료 효능·게임 배치/성장·가격·보상·WI/H/E·Scene/Save·운영 DB·유료/대량수집·r129/야간·commit/push는 승인하지 않는다. D410/412 및 독립 D406은 병행한다.

## D-414 북부 춘분의 굶주린 농장 발견과 상호 도움·눈과 침엽수 배경을 기획한다

- 상태: `ApprovedScopedWork` (2026-08-31). [북부 춘분 보완본 r2](북부춘분-굶주린농장발견-기획보완-2026-08-31.md)가 답변을 소유하고 [첫 플레이 r9](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md#현재-사례-보완--북부-춘분과-함께-먹거리-마련-d414)에 연결한다. D413 동결 종료 뒤 기존 분리 보완본을 결속하며 새 전체 Q 번호는 만들지 않는다.
- `northern-spring-hungry-farm-encounter`는 `ConfirmedDirection`이다. 춘분에도 춥고 잔설이 있는 북부 숲 가장자리/농장에서 굶주린 NPC를 발견하고 당장 먹거리가 없는 플레이어가 사냥 또는 채집으로 나누어 먹을 방법을 찾는다. 직전 봄 밭일 관찰 추천을 이 사례에서 대체한다. 필수 사냥·시작 장소·구조 타이머·NPC 사망/보상·현실 지역/수종/식용 판정을 추가하지 않는다.
- `hungry-farm-npc-local-knowledge-help`는 r1의 `Asked`에서 `Confirmed`다. NPC도 알고 있는 지역 단서를 나누며 함께 해결한다. 구체 동행·도구 지급·단서 정밀도·왕래 해금/고용은 미정이다. Q114의 납품 인연과 조우/나눔을 구별하고 기존 소유·안전 연결·권한을 보존한다. 후속 거점/탐색 역할 분담은 `Asked`이며 이번 동의에 포함하지 않는다.
- `northern-spring-snow-conifer-survey`는 `Confirmed`다. 사용자는 보유 Synty의 눈 자산을 조사하고 소나무 같은 침엽수 중심으로 배경을 배치하기를 원한다. D412의 Snow/Glacier 파일과 기존 Pine 근거를 재사용하여 잔설 지면·적설 수목/바위·침엽수·강설 FX를 분리 조사한다. 배치 의도를 정확 Prefab/좌표/밀도/장면 완료로 올리지 않는다.
- 기획은 보완본·첫 플레이·이 결정 세 파일, 개발은 조회/문서 검증·기존 의미/자산 근거 대조·CURRENT/통합판, 공간은 지정 보고/임시 근거의 소수 파일 조사를 소유한다. 조사 결과의 적합성·공백을 받은 뒤 기존 명세/연구에 후속 배치를 결속한다. 첫 묶음은 새 Editor/렌더/Scene·원본/공유 재질·가공·설치/구매·게임코드/Save·E/새 WI/H 변경 없이 진행한다.
- D413 식물 자료는 북부·추위·잔설/정확 지역·고도 미정과의 적합성을 따로 표시한다. 기존 중부/남부 자료 폐기·반복 수집이나 이번 현실 생물 안전/효능 조언은 없다. D410~413 당시 전량 검토와 추가 유입을 분리하고 D406 독립 승인 범위는 유지하며 r129/야간·commit/push를 재개하지 않는다.

## D-415 눈 없는 춘분의 봄으로 전환하고 부담 없는 식사 협력과 가방 필요를 반영한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [현재 보완본 r3](북부춘분-굶주린농장발견-기획보완-2026-08-31.md)와 [첫 플레이 r10](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md)에 D414 이후 답변을 결속한다. 기존 북부 경로/ID는 연결 호환용이며 기후 전제를 강제하지 않는다.
- `farm-encounter-spring-without-snow`는 `Confirmed`다. 사용자는 눈 덮인 겨울 같은 조건이 이야기/퀘스트 기획을 어렵게 한다며 그냥 봄·춘분으로 진행하기로 했다. 이번 장면의 눈/잔설·강추위 및 그로 인한 농사 곤란을 필수 전제로 삼지 않는다. 세계 전체의 계절/기상 폐지나 현실 지역 재지정·Scene 변경이 아니다.
- D414의 눈 관련 추가 조사·배치 준비는 중단한다. 확보한6후보/근거·당시 검증은 보존하며 실패/삭제로 바꾸지 않는다. 침엽수 배경 선호와 숲 가장자리↔농장 외곽은 유지한다. 북부 잔설 조건에 자료를 억지로 맞추지 않되 봄/3월 자료의 실제 지역·품종·시기 대응은 여전히 필요하다.
- `hungry-farm-first-meal-role-split`은 `Asked`→`Confirmed`다. NPC가 가능한 식사 준비/현지 단서를 맡고 플레이어가 먹거리를 찾는다. 자원 생성·부재중 자동 완료·동행/고용은 자동 승인하지 않는다.
- `farm-meal-low-pressure-help`, `farm-meal-small-contribution-return`, `farm-meal-player-inventory-connection`은 `ConfirmedDirection`이다. 퀘스트 압박을 낮추고 준비 가능한 작은 식사를 함께한 뒤 나중에 또 돕는 관계, 소지품을 확인/선택해 준비와 나눔에 연결할 필요를 반영한다. 기한·사망/평판 불이익·UI·최소 재료/소비/보상·가방 크기/무게·장착 효과는 미정이다.
- 먹거리 부족 NPC/상호 도움은 유지하지만 그 원인을 강추위·지난겨울 식량 소진·가뭄 등으로 자동 확정하지 않는다. Q114 정식 인연/왕래 해금과 조우/나눔은 분리한다. 범용 가방/식사 연결은 [현황 조사](../Reports/북부춘분-인벤토리현황조사-2026-08-31.md)의 기존 수량·장착 기반과 미연결을 출발점으로 삼으며 새 구현 승인이 아니다.
- 기획은 보완본/첫 플레이/결정3파일만 작성하고 개발에 상태·대체 관계/기존 원문·조회/문서 검증·CURRENT/통합판을 인계한다. D414 당시462/871/483과154회귀는 이력으로 보존한다. 신규 코드/Editor·Scene·Save·자료 수집 확대·E/새 WI/H·가공·r129/야간·commit/push는 없으며 D406 독립 범위는 유지한다.

## D-416 여러 영역의 선택형 플레이를 함께 설계하고 승인된 개발을 병행한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 숲/농장의 한 루트만 깊게 파는 데 머무르지 않고 Town의 와인·맥주 제작, City의 도시 업무/기존 마트 등 여러 영역을 종합 설계하며 전방위로 개발하기를 원했다. [다영역 계획](다영역-선택형플레이와병행개발-2026-08-31.md)의 `multi-area-choice-parallel-development`를 `ConfirmedDirection`으로 기록하고 [공통 관점](../Architecture/플레이어중심게임개발업무구조.md#여러-영역의-선택형-플레이와-개발을-함께-진행한다-d416)에 연결한다.
- Nature 출발/농장 식사 협력은 가능한 경로이며 모든 플레이어의 필수 시작/진행 순서가 아니다. D415 봄 사례를 폐기하지 않되 다른 영역의 설계/개발을 그 완료까지 대기시키지 않는다. Farm·Town·Hub·City의 독립 의미와 City 예약 등 실제 접근 조건을 유지한다.
- 개발 첫 묶음은 기존 영역별 문답/Loop·WI·기획/연구/명세·코드·자산/실제 화면의 준비 대조다. 준비된 승인 범위의 비중첩 기술 후속은 정확 소유/명세를 결속하여 병행하고, 기존 문답으로 채울 기획 관문과 새 선택은 분리 반환한다. 미승인 양조 Recipe·시간/비용·품질·운영 권한을 임의 확정하지 않는다. `NotStarted`를 이 방향만으로 `Approved`로 바꾸지 않는다.
- 기존 공통 인벤토리·물품/제조·대화·주문/재고·표현 모듈은 실제 의미가 같을 때 재사용한다. 양조와 약초 달이기의 규칙/완료 결과를 같다고 취급하지 않는다. 다음 `town-brewing-first-participation`은 `Asked`이며 기존 NPC 공방에서 배우며 소량 제작하는 구체 추천은 아직 미승인이다.
- City/마트는 기존 알뜰살뜰 마트·3.5 주문/도심 재고·피킹/포장/수령 흐름을 읽기 대조한다. 게임의 도시 경험과 실제 운영 업무는 별도 권한·동의·상태/실행 경계를 유지하며 게임 행동으로 실제 주문·발주·배차·결제·재고 변경을 실행하지 않는다.
- 기획 소유는 새 계획·공통 관점의 연결 절·이 결정3파일, 개발 소유는 지정 준비대조 보고·기존 조회/상태판/등록 가능한 명세/원장 통합이다. 전문은 개발이 정확 경로로 분배한다. 첫 조사/검증과 기존 승인 범위 밖의 신규 기능/Scene·가공·운영 DB·외부 호출·야간/r129·commit/push 재개를 혼동하지 않는다. 화면 시안·자동시험·실제 게임 및 영역 간 조화를 각각 구분한다.

## D-417 직접 탐험의 1인칭과 넓은 운영 시점·현실 업무 보조를 연결한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [시점 역할 기획 r2](탐험과운영-시점역할과현실업무연결-2026-08-31.md)가 승인 의미와 기존 구현 대조의 원문이며 [공통 관점](../Architecture/플레이어중심게임개발업무구조.md#시점의-스케일로-직접-탐험과-운영을-연결한다-d417d418)에 연결한다. D416 검증 종료/쓰기 보류 해제 뒤 결속한다.
- `first-person-exploration-entry`는 `Confirmed`: 최초 플레이의 권장 기본을 1인칭 직접 탐험·풍경·채집으로 한다. 기존 D134의 탐험 1인칭/경영 전술 편의 정책을 재사용하고 Q183의 현장 3인칭 구상에 초기 탐험 기본을 보완한다. 특정 시작 지역·모든 Save 복원·전투 진입을 1인칭으로 강제하지 않는다.
- 후속 대조 정정: D134 뒤의 D162가 탐험·전투·경영 기본을 전술3P로 승인했으므로 현재 Exploration3P는 해당 결정과 일치한다. [현재 원문 r3](탐험과운영-시점역할과현실업무연결-2026-08-31.md)에 그 이력을 추가했다. D417 최초1P는 후속 진입 명세의 변경 방향이며 기존 구현 결함으로 단정하지 않는다. D417/D418 여섯 의미ID와 상태는 불변이다.
- `multi-area-observation-operation-perspective`는 `ConfirmedDirection`: 운영이 늘면 전술 3인칭과 광역 관찰을 활용해 여러 영역·작업을 비교/지시한다. 자동 전환 임계값·해금·원격 권한은 미정이며 D134의 허용된 수동 전환, D273 자기 Actor/카메라 분리, 현행 D237 전투 참여 방식 잠금을 유지한다.
- `optional-auto-hunting-operations` 및 `game-global-real-work-assistance`는 `ConfirmedDirection`/`FutureExtension`이다. 기존 관찰 전투를 오픈월드 자동사냥 전 과정 완료로 취급하지 않는다. 글로벌 실제 업무 보조도 장기 방향에 포함하되 게임·선택형 현실 참고·실제 업무의 목적을 구별하며 기존 Simulation/Operational 실행 경계·인증·역할·동의·서버 확인 절차를 유지한다.
- 기획은 원문·공통·결정3파일, 개발은 지정 기존구현대조 보고·기존 조회/상태판·문서 검증을 소유한다. Exploration 기본/명시 override/Builder 시작값 등 확인한 차이는 새 Scene 실행 성공이 아니다. 최초 후속은 읽기 대조/문서 통합이며 코드 필요는 정확 명세/범위를 별도로 결속한다. Town 양조 Asked와 D415/D416은 유지, 실제 자동사냥·운영 API/DB·새 코드/Scene·E·r129/야간·commit/push 재개는 없다.

## D-418 WI를 시점의 스케일별 공통·특화·전용으로 구분하고 독립 발전을 관리한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [D417 원문의 D418 절](탐험과운영-시점역할과현실업무연결-2026-08-31.md#7-시점을-스케일로-보고-wi와-대응한다-d418)을 단일 기준으로 사용한다. `perspective-scale-wi-classification`과 `perspective-independent-development`는 `ConfirmedDirection`이다.
- 시점은 가까운 대상↔현장↔여러 영역의 판독/조작 스펙트럼이다. H 공간 포함·권한·E 성숙도·카메라 확대 수치와 동일시하지 않는다. 시각 범위, 조작 단위, 실제 명령 가능 조건은 구별한다.
- 기존 WI ID를 유지하고 시점 공통·시점 특화·특정 시점 전용 및 미검토/해당 없음을 기록한다. 공통/특화는 겹칠 수 있어 허용 시점과 편의 기능을 분리하며 활동별 AllowedView/Advantage를 WI 권한표로 취급하지 않는다. 전용에는 승인된 근거·제한 사유·다른 시점 안내/복귀를 요구하고 신규 배타 규칙은 임의 확정하지 않는다.
- 각 시점의 입력·판독·상태/자산 연결·결과/복귀 근거를 기존 WI/명세의 표현 준비에 연결한다. 한 시점 성공을 다른 시점 완료로 복사하거나 미완료 시점 때문에 기존 근거를 폐기하지 않는다. 통합 E는 현재 승인 범위와 기존 판정 기준을 유지한다.
- D402 발전 군집의 필수 선행·선택 대안·병행 보완을 재사용하며, 플레이어 해금/능력 발전과 개발 준비 진척은 별도다. 1인칭→전술→광역의 고정 해금, 시점 경험치·신규 E축/DB/중복 WI/원장은 만들지 않는다. 전투 수명 잠금과 실제 업무 실행 경계는 D417과 같이 유지한다.
- 첫 인계는 D417 지정 보고의 후속 절에서 기존 WI/활동 정책과 소수 대응 사례를 읽기 대조하고 기존 조회에 원문 의미를 결속하는 것이다. 전체105 분류 완료·시점별 전용 확정·신규 schema/API/게임코드·Editor·Scene·E/등록 변경이나 중지 작업 재개로 확대하지 않는다. 구현 필요는 개발이 정확 경로·명세·검증 범위로 반환한다.

## D-419 1인칭 직접 타이밍 수행을 선택형 추가 효과에 특화한다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [선택형 집중 원문](1인칭-선택형집중타이밍-기획보완-2026-08-31.md)의 `first-person-timing-bonus-specialization`은 `ConfirmedDirection`이다. 가까운 대상의 동작을 보며 버튼/마우스를 타이밍에 맞춰 직접 수행하고 추가 효과를 얻는 방향이다. D417/D418 동결 검증 중 분리 작성한 r1부터의 답변을 검증 종료 뒤 연결한다.
- Q398/D366의 성공 추가 명상 성장과 미스/불참/건너뛰기 시 기본 작업 추가 손실 없음은 유지한다. 원래 작업 비용·위험이나 기존 전투 실패를 면제하지 않으며 회복 후보·품질/수확량/전투피해를 전 WI에 자동 확대하지 않는다.
- 1인칭 특화는 모든 WI의 1인칭 전용화·카메라 자체 보너스가 아니다. [공통 기준](../Architecture/플레이어중심게임개발업무구조.md#원하는-작업에서-반복-집중으로-효율을-높인다-d419d421)에 연결하며 실제 입력/대상·기존 Profile·성장/행위 기록·표현 준비를 개발이 대조한다. 자동/NPC 수행은 플레이어의 타이밍 성공이 아니다.

## D-420 집중 타이밍은 플레이어가 선택한 작업에서만 제시한다

- 상태: `AcceptedPlanningDecision` (2026-08-31). [제시 조건](1인칭-선택형집중타이밍-기획보완-2026-08-31.md#5-제시-조건-확정--선택한-작업에서만-d420)의 `first-person-focus-presentation-trigger`는 r1 `Asked`→r2 `Confirmed`다. 사용자는 원하는 작업에서 ‘집중해서 하기’를 선택했을 때만 제시하기로 답했다.
- 1인칭 진입·대상을 바라보기·일반 작업 시작만으로 타이밍 도전을 자동 표시하지 않는다. 집중을 선택하지 않은 작업의 기본 진행은 유지한다. 버튼의 정확 위치/표시·선택 가능 시점·설정 기억/자동 재적용은 미정이며 전체 작업 일괄 활성화를 승인하지 않는다.
- 집중을 선택했다는 사실만으로 성공·보상·작업 완료가 되지 않는다. 도전 반복과 효율 방향은 후속 D421을 따르고 다른 미답변이나 Town 양조 참여는 승인하지 않는다.

## D-421 반복 동작마다 집중에 재도전하고 성공으로 작업 효율을 높인다

- 상태: `AcceptedPlanningDirection` (2026-08-31). [반복 기회와 효율 답변](1인칭-선택형집중타이밍-기획보완-2026-08-31.md#반복-동작에서-계속-도전한다-d421)을 따른다. `first-person-focus-attempt-count`는 `Asked`→`ConfirmedDirection`, 새 `first-person-focus-efficiency-bonus`는 `ConfirmedDirection`이다. 제안했던 ‘작업당 한 번·미스 뒤 재시도 없음’은 미채택이며 과거 확정 규칙으로 취급하지 않는다.
- 집중을 선택한 작업에서 도끼질 같은 유효한 반복 동작에 맞춰 계속 도전할 수 있고, 한 번 미스해도 이후 동작의 기회를 닫지 않는다. 가능하면 매 동작을 선호한다. ‘세 번 중 한 번’은 예시이지 고정 간격이 아니며 모든 WI의 간격/횟수·같은 동작 내 재입력은 미정이다.
- 유효한 성공은 실제 작업 진척/더 빠른 완료에 기여한다. 미스/불참은 그 기회의 추가 효율 없이 기본 작업을 이어 가며 추가 손실을 만들지 않는다. 정확 효율 계산·상한·시간/도구/체력/재료 소모와 명상 성장 배분/총량은 미정이다. 애니메이션만 빨라진 것을 작업 효율 구현으로 세지 않는다.
- 실제 다음 동작의 새 도전과 같은 판정 재전송을 구분한다. 기존 단일 Challenge의 제출 거부를 없애거나 같은 성공을 중복 지급하는 방법으로 구현하지 않는다. 현재 Nature 구현은 재사용 출발점이며 새 반복 기획을 작업당1회로 되돌리는 권위가 아니다. 작업/동작·도전·완료 기록·판본/저장 영향은 정확 후속 명세에서 검토한다.
- 기획은 집중 원문·공통·결정 및 시점 원문의 D162 이력 보완 네 파일을 소유한다. 개발은 기존 D417 보고 후속절·조회/문서 검증·CURRENT/통합판을 소유하여 네 의미ID를 함께 인수한다. 기존474항목/175회귀는 앞선 판본 이력으로 보존한다. 이번 새 게임코드·UI·입력/성장 수치·Scene/Save·Editor·E/WI·실제 운영·r129/야간·commit/push 변경은 없다.

## D-426 개체 종류와 개별 기록의 시각 자산 대응을 여러 분야에 구현한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 종류별 기본 표현과 개별 기록별 표현을 함께 지원하고, 개별 표현이 없으면 승인 기본형을 사용하도록 선택했다. 감자뿐 아니라 창고·마트 등으로 확장한 계획을 `Implement the proposed plan.`으로 승인했다. [D426 구현 인계 원문](개체레코드-시각자산대응-확장과개발인계-2026-08-31.md)이 범위를 소유한다.
- D425의 품목 중심 범위/첫 적용 순서를 확장하되 원문·품목 코드/외형 적합성·배치 검증의 분리를 보존한다. 공통 저장·조회·검토·선택을 사용하고 유효한 개별 승인 → 같은 문맥의 승인 기본형 → 미연결로 결정한다. 대체 여부/사유를 반환하며 권한/원천 오류·충돌을 fallback으로 숨기지 않는다.
- 창고·마트·농장·농수산물부터 다른 분야로 독립적인 작은 적용을 이어간다. DbSet은 조사 출발점이며 모든 행의 객체화를 승인하지 않는다. 가상창고·마트 상품·관측/이력과 실제 시설을 구분하고 기존 권한별 조회·자산 대장·배치 검증을 재사용한다.
- 개발이 계약·저장/관리자 API·선택·Adapter·시험·기존 로컬 Docker DB 적용을 소유하고 자료/공간 담당을 조율한다. 현재 스키마와 D424의 전용 테이블/마이그레이션 이력 공백을 확인한 제한 추가만 허용한다. 운영 DB·원천/원본 변경·자동 수집/제작·Scene 일괄 배치·E 승격·r129/야간/무관한 중지 작업·commit/push는 제외한다.
- 기획 문서 작성 시 개발 메시지 도구가 승인 필요로 전송을 거부했다. 실행 승인과 담당 수신/코드·DB 착수는 구별하며 차단을 우회하지 않고 인계 대기를 기록한다.
- 후속 사용자 지시 `여기서 진행`에 따라 D426에 한해 이 작업이 코드 구현·시험을 직접 맡는다. 기존 개발 작업으로의 메시지 차단은 해제된 것으로 기록하지 않는다. 승인 원문 r1/hash는 보존하고 실제 구현 경로와 검증 결과는 별도 기술 보고/CURRENT_WORK로 갱신한다. 다른 작업 재개나 Scene·운영 효과 승인을 확대하지 않는다.

## D-427 농장 건물과 밭은 이격·정렬하고 상점 진열대는 실내에 둔다

- 상태: `AcceptedPlanningDirection` (2026-08-31). 사용자는 제시된 과거 농장 화면의 헛간·건물·밭 사이를 이격하여 질서 있게 정렬하고, 상점 화면의 야외 진열대를 실내에 두도록 요청했다. [시각 배치 보완 원문](농장정돈-상점실내진열-시각배치보완-2026-08-31.md)의 두 의미 ID를 `ConfirmedDirection`으로 기록한다.
- 과거 Showcase의 외형 참고와 현재 canonical World 적용을 구분한다. 농장 건물/마당·통로/밭의 관계를 정돈하고 상점은 실제 내부·출입구·선반과 통로를 확인한다. 정확 간격·축·실내 후보는 실제 자산/지지/접근을 조사하여 계획에 남기며 임의 축소·벽 속 배치·생산/재고 변경으로 맞추지 않는다.
- D422/D423의 구조·제약→계획→검사→조립→실물/해제·재적용을 재사용한다. Farm과 상점은 독립 적용이며 전체 야외시장 폐지·새 거래 기능·원본/실험 Scene 덮어쓰기·E 승격 승인이 아니다. 필요한 연구/기존 명세와 정확 소유 경로는 개발이 결속한다.
- 기획은 원문·결정·CURRENT의 인계 지점을 기록하고 실제 구현/검증·공간 배분은 개발에 인계한다. `HANDOFF-CHECK-20260831-B`는 개발 실제 수신과 기획의 ACK 수신까지 확인되어 D426 당시 메시지 차단과 구별한다. 이 통신 확인만으로 D426 실물/DB 인수나 이번 배치 실행이 완료된 것은 아니다. 기존 동결 원문과 r129/야간·무관한 중지 작업·commit/push 경계는 유지한다.

## D-428 NPC 양조 공방에서 배우고 국내 현실 양조장 자료를 선택형으로 연결한다

- 상태: `ApprovedScopedWork` — 기획 선택과 제한 자료 조사 (2026-08-31). [다영역 기획 r2의 Town 답변](다영역-선택형플레이와병행개발-2026-08-31.md#7-다음-단일-문답--town-제작에-어떻게-참여할까)이 승인 의미를 소유한다. `town-brewing-first-participation`을 Asked→Confirmed로 바꾸고 NPC 공방 학습·소량 제작부터 경험한다. 시설 소유를 필수로 만들지 않되 주종·재료·비용·시간·실패/회복은 여전히 미정이다.
- `town-brewing-public-data-research` Confirmed / `town-brewing-optional-reality-reference` ConfirmedDirection. 국내 양조장 공공자료를 조사하고, 관심이 생기면 현실 상세보기를 열어 실제 활동을 알아보도록 연결한다. 게임 공방/실제 시설/상품/체험 프로그램을 구분하고 D371~D374의 선택형 참고·대응 검토를 재사용한다. 열람·현실 예약/구매·제조/운영은 별도이며 자동 전환이나 게임 보상 조건은 없다.
- 기획의 공식 페이지 확인에서 aT 찾아가는양조장정보·전통주 체험 프로그램·전통주정보를 발견했다. 원문에 URL·자료 시점/한계를 남기고 자료 담당이 기존 재고와 최대3출처/시설3개 소표본을 대조한다. 메타데이터 확인을 실제 수신·최신 운영/예약 가능·자료 재표시·DB 저장 완료로 올리지 않는다. 로그인/활용신청은 필요가 확인된 자료만 D404 절차로 먼저 반환한다.
- 첫 현실 상세보기의 방문/체험 중심 추천은 `town-brewing-reality-detail-first-purpose`/Asked로 남긴다. 사용자의 현실 연계 방향을 창업·설비구매·주류판매·자동예약 승인으로 확대하지 않는다. 새 Recipe·게임UI/Scene·공급원API/DB쓰기·E/권위 변경은 이번 배분이 아니다.
- 기획은 원문·결정 두 파일, 개발은 기존 조회/명세 필요 범위·문서 검증·현재 상태와 자료 담당 조율을 맡는다. 기존 D427/승인된 독립 개발을 중단시키지 않고 r129/야간·무관한 중지 작업·commit/push는 재개하지 않는다.

## D-429 공공자료 조사는 기존 서버 수집과 Docker MySQL 축적까지를 기본으로 한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 공공자료 조사 요청을 공식 자료 확인에서 끝내지 않고 기존 ASP.NET Core와 Docker MySQL 수집·축적까지 하나의 파이프라인으로 구성하고 공통 지침에 정립하도록 요청했다. [자료조사 운영 r7의 기본 파이프라인](../Architecture/게임자료조사전문운영.md#공공자료-조사의-기본-완료-범위--수집저장재조회-d429)이 단일 기준이며 `public-data-research-default-server-mysql-pipeline`은 `Confirmed`다. 루트 AGENTS에는 요약과 이 기준 링크만 둔다.
- 기본 완료는 요청/기존자료 대조 → 공식 출처·이용조건/접근 확인 → 기존 서버 수집/가져오기 → 확인된 기존 로컬 Docker MySQL 저장 → 독립 재조회·동일 입력 중복 방지 → 개발 검토·기획 보고다. 자료 담당이 수집·축적을 수행하고 개발이 정확 DB/계약·공급원 코드/보안·검증을 조율한다. 필요한 작은 기술 연결은 진행하되 가짜 자료·메타데이터·파일만 저장한 결과로 DB 완료를 대신하지 않는다.
- D424의 로컬 DB/보존·최소 추가·비밀값 기준을 유지하며 D428의 API/DB쓰기 제외를 해당3출처/3시설의 허용된 작은 실제 수집·저장까지 추가 승인으로 대체한다. D428 원문r2/hash·목적/표본/권리·현실 상세 Asked는 보존하고 이미 확보한 자료를 재사용한다. 현재 자료판본 차이는 수집시점/원문시점을 분리해 기록한다.
- 사용자가 조사/문서만 또는 저장 없이 요청하면 그 지시가 우선한다. 접근/키/보관권한·정확DB가 불명확하면 그 단계만 보류해 필요 조치를 먼저 보고한다. 자료보관/공개/게임활용은 분리하며 기본 비공개·검토보류다. 원격 운영DB·정기/대량/유료수집·계정/약관동의·게시·예약/구매·게임권위·파괴변경은 자동 승인하지 않는다.
- 기획 소유는 AGENTS·운영 원문·DECISIONS 세 파일이다. 개발은 기존 검색/인계·CURRENT/통합판에 대체관계를 연결하고 자료 담당의 D428 범위를 조정하여 실제 수집/저장·재조회 결과를 반환한다. 기획 문서 반영이나 배분을 실제 MySQL 성공으로 보고하지 않는다. 다른 중지 작업·r129/야간·Editor/Scene·commit/push를 재개하지 않는다.

## D-430 기존 DbSet 개체·레코드와 KAMIS 코드의 실제 시각 대응을 먼저 재검증한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 통신 점검이나 오크 조사보다 기존 서버 개체/테이블과 KAMIS 농수산물 코드가 Synty 자산에 적절히 대응하는지 먼저 검증하고 전산화로 이어가기를 요청했다. [재검증 원문 r1](기존개체-KAMIS-시각대응-재검증과축적-2026-08-31.md)의 `existing-entity-kamis-visual-audit-first`는 `Confirmed`다.
- D425/D426 기존 구조와 동결 원문을 보존하고 코드 지원·실테이블/레코드·원천 코드 대응·개별/기본 표현 대응·현재 자산/외형 적합성을 따로 감사한다. 기존 여섯 원천 조회와 농산물/수산물 소수 표본부터 진행하며 미지원 DbSet/미검토 품목도 목록에 남긴다. 가격 관측을 상품/공간 실체로, 재고를 건물로 전용하지 않는다.
- 개발이 실제 DB/조회/선택/시험과 자료·공간 담당을 조율한다. 기획은 원문/결정 두 파일, 개발은 지정 감사 보고/CURRENT/통합판을 소유한다. 읽기 감사 뒤 확인된 범위만 기존 D426/D429 로컬 저장·재조회/멱등 경로로 축적하며 근거 없는 적합성 승인·자동 활성화로 연결 수를 채우지 않는다.
- 코드/합성 시험/자산 이름 일치와 실제 저장·외형·Unity 배치 검증은 별도다. 새 수집·원격 운영 쓰기·원천 코드/가격/재고 변경·전체 마이그레이션·원본/Scene·E·r129/야간·commit/push를 이번 요청으로 확대하지 않는다. 앞선 오크 조사는 미마감 상태로 두고 이 감사를 우선한다.

## D-431 Synty 자산 목록과 개체 대응 관계를 MySQL에 먼저 구축한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 반복 Unity 확인보다 MySQL에 Synty 목록을 두고 기존 개체와 관계로 어떤 자산을 쓰는지 먼저 전산화하기를 요청했다. [구현 원문 r1](Synty자산-MySQL목록과개체관계-구현승인-2026-08-31.md)의 `synty-mysql-catalog-and-entity-relations-first`는 `Confirmed`다.
- D430 감사는 입력으로 유지한다. 기존 자산 대장/키/Prefab 메타데이터를 DB 목록으로 가져오고 D426 대응·감사/권한별 Reader를 재사용하여 종류 기본/개별 기록·공통 품목과 명시 관계를 연결한다. 자산 바이너리·텍스처·GameObject 업로드가 아니라 식별자·판본·역할·근거·검토 상태의 영속화다. 없는 목록 테이블/관계만 최소 추가한다.
- 파일/참조 정합성이 확인된 미검토 후보를 먼저 저장·조회할 수 있게 한다. 실제 외형 적합성/제품 선택 승인은 분리하고 가짜 근거·자동 승인·제품 기본 활성화는 하지 않는다. 기존 8키와 실재 개체/품목 소수 표본부터 서버 가져오기→정확 로컬 Docker MySQL 저장→관계 독립 조회/멱등 검증까지 구현한다. Unity 실행/Scene 배치는 이번 완료 조건이 아니다.
- 기획은 원문/결정, 개발은 정확 코드·제한 DDL·API/가져오기·회귀/실DB·보고/상태판을 소유한다. 새로운 자료 수집·원천/개인정보 변경·원격 운영 쓰기·전체 마이그레이션·원본/Scene·E·r129/야간·commit/push 경계는 유지한다. 문서/후보 등록/관계 저장/승인 선택/Unity 사용의 실제 도달점을 따로 반환한다.

## D-432 개체별 자산 할당을 실제 이미지로 비교·검토할 수 있게 한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 구체적 개체/자산 할당 결정에 이미지 확인이 필요하므로 그 조치를 요청했다. [이미지 비교·할당 검토 원문 r1](개체자산-이미지비교와할당검토-2026-08-31.md)의 `entity-asset-image-comparison-before-assignment`는 `Confirmed`다.
- D431 DB 목록/후보 관계 구축은 이미지가 없어도 계속한다. 구체적 할당은 개체 정보·상태/목적·기존 코드 관계와 후보 이미지·판본·근거를 함께 보고 판단한다. 이미지 열람이나 이번 방향 승인이 실제 후보/전체 개체의 자동 승인은 아니다. D426 검토·권한/판본을 재사용한다.
- 기존 이미지부터 현재 자산과 대조하며 없는 기존8키 후보만 공간 담당의 정확 보호/단독 슬롯 아래 격리 촬영한다. 혼합 Showcase/개별 미리보기/실제 게임 화면을 구분하고 원본·canonical Scene 변경이나 새 공식 Scene·불필요 Play를 하지 않는다. 이미지와 데이터 비교 조회는 개발이 기존 관리/미디어 경로를 재사용해 제공한다.
- DB에는 이미지 참조/hash·증거 종류·자산 판본을 연결한다. 실제 이미지/Prefab 원본을 공개 업로드하지 않고 권한 없는 파일 읽기를 허용하지 않는다. 사용자에게 대표 이미지를 반환하되 파일/DB 조회·시각 검토/할당 승인·Unity 적용을 분리한다. 기획은 원문/결정, 개발은 구현·검증/상태판과 공간 배분을 맡으며 D431의 독립 진행·r129/야간/commit/push 중지를 유지한다.

## D-433 프리팹 이미지 전담 조사를 만들고 Azure 비공개 보관·웹 열람으로 연결한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 보유 Synty 프리팹의 외형 이미지를 조사·저장하고 동봉 이미지를 재사용하며, 별도 프리팹 조사 작업과 Azure Blob/웹 열람을 마련하기를 요청했다. [전담 조사·보관 원문 r1](프리팹이미지-전담조사와Azure보관-2026-08-31.md)의 `prefab-image-dedicated-research-and-azure-storage`는 `Confirmed`다.
- 전담은 개별 이미지/팩·자산판본·근거와 권리 분류를 소유한다. 기존 공간 담당은 필요한 소수 촬영, 개발은 DB/Azure/인증·웹과 통합을 소유한다. 같은 자료를 여러 전문이 중복 소유하지 않으며 공유 원본/미소유 dirty는 보존한다.
- D432의 외부 업로드 제외를 확인된 기존 Azure 대상의 제한 비공개 이미지 업로드·권한 있는 웹 열람에 한해 대체한다. Prefab/텍스처 원본·공개 컨테이너/재배포·새 계정/구독·권리 미확인 자료 업로드는 승인하지 않는다. 동봉 썸네일·텍스처/아이콘·격리 미리보기·혼합 Showcase를 구분한다.
- D431 기존8키부터 준비된 이미지를 파일→Blob→MySQL 관계→웹으로 연결하고 실제 업로드·재읽기/중복 방지·인증 열람을 각각 검증한다. DB 기반은 독립 진행한다. 작업 생성·이미지 발견·URL 반환을 실제 저장/웹 확인/할당 승인 완료로 표현하지 않는다. 기획은 원문/결정, 개발은 실제 기술/상태판과 새 담당 배분을 맡으며 원본/Scene·E·r129/야간·commit/push 중지를 유지한다.
- 전담 `프리팹 조사`의 독립 worktree 생성 요청을 접수했다(`client-new-thread:2b78f571-5019-42ac-88b2-bd3a91b97420`). 현재 준비 중이며 정식 작업 ID/실제 조사 착수는 별도 확인한다. 이 임시 식별자를 메시지 전송용 작업 ID로 사용하거나 중복 생성하지 않는다.

## D-434 게임 객체를 역할별 여러 자산으로 구성하여 MySQL 관계로 관리한다

- 상태: `ApprovedScopedWork` (2026-08-31). [다중 자산 구성 원문 r1](게임객체-다중자산구성과공방-개발인계-2026-08-31.md)의 `game-object-role-based-multi-asset-composition`은 `Confirmed`다. Town NPC 공방을 첫 설계 대상으로 객체 정의와 여러 Synty 자산의 역할별 관계를 관리한다. 각 연결 행은 객체/구성과 한 자산을 잇지만 전체 객체↔자산은 다대다이며 일대일 전체 제한을 뜻하지 않는다.
- 기존 D426/D431 단일 대응·판본/검토/권한·이력을 보존하고 구성 계약을 먼저 재사용한다. 후보 대안과 함께 쓰는 부품, 정의와 실제 World 개체, NPC Actor와 시설·현실 양조장을 구분한다. 편집용 공방 정의 초안·역할별 구성의 제한 서버/로컬 MySQL 저장·재조회까지 허용하며 원천/실제 Session/Scene 생성은 아니다.
- 기획은 원문/결정, 개발은 정확 기술 경로·최소 DDL·회귀/실DB·공통 상태판과 전문 배분을 맡는다. D432/D433 이미지·웹 연결과 기존 작업은 독립 진행한다. 자산 자동 승인/권위 기능·Save·배치/E 승격·r129/야간·commit/push는 포함하지 않는다.
- D428 `town-brewing-reality-detail-first-purpose`는 이번 답변으로 `Asked`→`Confirmed`: 첫 현실 상세는 소개·방문/체험·배움 정보 중심. 과거 r2는 보존하고 위 원문의 추가 답변을 연결한다. 다음 `town-workshop-component-interaction`은 설비 직접 선택/장식 비조작 추천의 `Asked`이며 이번 DB 승인과 분리한다.

## D-435 WI의 플레이 관점에서 게임 객체를 추출하여 기존 MySQL 정의와 연결한다

- 상태: `ApprovedScopedWork` (2026-08-31). [추출·저장 원문 r1](WI문서-게임객체추출과MySQL연결-2026-08-31.md)의 `wi-document-derived-game-object-registration`은 `Confirmed`다. 기존 WI/문답의 지금·여기·나·너·이렇게와 결과에서 객체를 식별하여 D434 편집용 게임 객체와 WI 참여 관계를 저장한다.
- 같은 객체를 여러 WI가 재사용하며 ‘나/너’는 관계의 역할이다. 문서의 명사·상태·능력·시간·권한을 전부 별도 실물로 만들지 않는다. 직접 근거/해석 후보·정의/실제 개체·정보형 대상·객체↔WI와 객체↔자산 관계를 구분한다. 기존 원문·WI/고유 식별자·H/명세·판본/hash를 추적하고 새 WI/실제 Session을 생성하지 않는다.
- 공식 목록105개(r43 확인)의 조회·분류에서 시작하여 벌목/수확/지식 습득 세 WI를 첫 저장·재조회로 검증하고 분야별로 확장한다. 전량 의미 완료/동일성/자산 적합성은 추정하지 않는다. Town 공방에 WI가 없으면 미연결로 남기며 배합물 달이기를 발효 양조로 전용하지 않는다.
- 개발이 D434 소유/기존 Context·검토·권한/멱등·이력을 재사용해 최소 추출/관계·제한 로컬 MySQL 저장·독립 조회와 회귀를 맡는다. 기획은 원문/결정만 소유한다. D433/434 독립 진행·원천 보존/기본 비활성·Scene/Save/E·r129/야간·commit/push 중지를 유지한다. 설비 직접 선택 질문은 계속 `Asked`다.

## D-436 새 문답 확대보다 기존 요청을 사용자 확인 가능한 결과로 우선 마무리한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 기존 요청의 완결을 위한 우선순위 제안을 승인했다. [마무리 순서 원문 r1](기존요청-우선순위와완료기준-2026-08-31.md)의 `approved-work-closeout-priority`는 `Confirmed`다.
- 현재 D434 안전 마감 → D435 WI/객체 DB 연결 → D432/D433 실제 이미지 비교 → D426/D434 후보 선택 저장·재조회 → D422/D427 Farm 한 구역 실제 배치 순으로 결과를 완결한다. 전체105 WI·자산 전수 완료를 첫 결과의 선행으로 만들지 않으며 독립 준비는 병행한다. 기존 자료 수집도 맡긴 제한 범위의 실제 저장·재조회로 닫되 새 조사 주제는 확대하지 않는다.
- 개발은 기존 CURRENT/통합판에 요청·담당/소유·대기/진행/차단/완료·선행 조건·근거·다음을 최신화하고 완료한 작은 묶음 다음의 승인 작업을 이어간다. 별도 큐/관리 시스템을 만들거나 운영 메타데이터 작업을 새 게임 Goal/WI로 등록하지 않는다. 전문 제출/개발 통합/사용자 확인을 구분한다.
- 기획은 승인/결과 검토, 개발은 구현·시험/DB/웹·전문 배분을 맡는다. 새 선택·권리/접근·파괴적 조치가 필요한 해당 단계만 보류하고 기술 결손은 승인 범위에서 처리한다. 원문/자산 자동 승인·실제 World 생성·E/Save/운영 원천·공개 게시·commit/push를 확대하지 않는다. 실제 Farm 작업은 기존 연구/명세·정확 소유/보호 결속 후 진행하며 r129/야간/무관한 중지 작업·자동화는 재개하지 않는다.

## D-437 보유 Synty 팩 전체의 자산 목록을 조사하여 MySQL에 축적한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 City·Town·Farm 등 많은 보유 팩의 자산 목록을 전수 조사·DB화하고 기존 Synty 조사 담당이 맡도록 요청했다. [전수 목록·DB 원문 r1](Synty전수자산목록-조사와MySQL축적-2026-08-31.md)의 `owned-synty-full-inventory-mysql`은 `Confirmed`다.
- D431 첫8키 목록 제한을 현재 로컬에서 확인할 수 있는 보유 팩 전수 메타데이터로 확장한다. Prefab 전수와 보조 자산 종류를 구별하고 포함/제외 경로·실제 판본·식별/중복·미확인을 기록한다. 과거 팩/재고 수를 실측으로 대체하지 않고 구매함/미임포트 자산까지 확인했다고 하지 않는다.
- 기존 프리팹 조사 작업은 전수 입력/판본·누락/검증 기록을 주관하고 개발은 기존 ASP.NET Core 목록/가져오기·관리 경계와 확인한 로컬 Docker MySQL 저장·독립 재조회를 통합한다. 전담 전용 파일 조사는 D435와 병행하고 공용 계약/DB 쓰기는 개발이 조율한다. 파일 목록 제출만으로 DB 완료를 선언하지 않는다.
- 목록 등록은 이미지/외형 적합성·자산 선택·실제 게임 배치와 분리한다. D433 촬영/업로드를 전수로 확대하지 않고 기존 관계/승인·판본·원천 데이터를 보존한다. 팩별 묶음/재처리·전체 식별자 대조를 통해 등록·미등록/충돌을 모두 설명하며 새 키/자동 승인·구매/다운로드·외부 수집·운영DB·공개게시·Scene/Save/E·r129/야간·commit/push는 확대하지 않는다.

## D-438 WI 전수 객체 조사를 쉬고 있는 자료 담당에 분담하고 개발이 DB 등록을 통합한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 WI에서 추출할 게임 객체를 전수 조사·정리하여 추가 등록하고 쉬고 있는 담당에 맡기도록 요청했다. [전수 객체 조사 원문 r1](WI전수객체조사-유휴담당분담과DB인계-2026-08-31.md)의 `wi-full-object-survey-idle-specialist`는 `Confirmed`다.
- 현재 공식 WI r43/105개와 연결 원문을 대상으로 기존 D435 첫3 WI와 별개 담당의 전수 내용 대조를 병행한다. WI 자체를 객체로 복제하지 않고 행위자/대상·도구/투입물/결과·장소 관계에서 정의를 추출한다. 재사용/신규 후보·정보형/비객체·미정·근거 누락을 모두 설명하며 전수 조회를 의미 검토/DB 완료로 표시하지 않는다.
- idle 확인한 기존 자료 조사 작업이 전용 worktree의 D438 조사3파일/artifacts를 소유하고 개발은 기존 D435 계약/Context·정의/이력과 제한 로컬 MySQL 등록·독립 재조회를 소유한다. 새 작업 생성/공유 파일 중복 쓰기/외부 자료 수집을 하지 않는다. 프리팹 조사 D437은 독립 유지한다.
- 분야별 근거 확정분부터 인수·추가 등록하고 전량 완료를 첫 저장의 선행으로 만들지 않는다. 새 의미 선택이 필요한 항목만 기획으로 돌리며 기존 승인 상태·원문/데이터·자산 미선정·실제 World 구분을 보존한다. 기존 D436 우선순위와 r129/야간·Scene/Save/E·운영원천·공개게시·commit/push 경계는 유지한다.

## D-439 표준 기획 문서의 판단을 로컬 저장 기능으로 MySQL 관계에 연결한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 기획에서 Codex가 WI 재사용/확장·객체·자산 관계를 정리한 판본을 관계형 DB에 보존하는 파이프라인을 요청하고 이 기획 작업에서 구현하도록 지시했다. 이어 같은 로컬 환경이므로 API 전달은 필수가 아니며 문서를 바탕으로 직접 MySQL에 정리하도록 정정했다. [기준·구현 범위](../Architecture/기획판본-서버반입파이프라인.md)가 이 요청의 원문 결속 기준이다.
- 문서의 일곱 칸과 관계 블록이 의미 기준이고 로컬 도구는 현재 WI·원문/hash·참조·판본을 검증한다. 서버에서 자연어 의미나 신규 WI를 다시 자동 결정하지 않는다. 기존 ASP.NET Core UseCase를 로컬에서 호출하되 권한/멱등·트랜잭션·재조회 경계는 유지하며 새 API/큐/데몬을 만들지 않는다.
- 이번 첫 저장은 이미 있는 WI와 객체 정의의 참여 관계만 기존 D435 표에 연결한다. 기존 벌목→나무 대상 관계 한 문서의 실제 로컬 반입·중복 방지·독립 조회를 포함하고 새 정의/자산 선정/DDL은 하지 않는다. 새 WI/Markdown 전용 근거·자산 구성 변경 등 미지원 입력은 명시 미준비로 남기며 일부를 몰래 누락해 저장하지 않는다.
- 기획 도구·시험·표준 문서는 이 작업이 소유하고 공통 서버 Context/DI·D437/D438 DB 쓰기는 개발 소유를 보존해 실행 슬롯을 조율한다. 실제 저장/게임 적용·E/Scene·운영 원천/공개 게시를 분리하며 기존 r129/야간·commit/push 중지는 유지한다.

## D-440 근거가 충분한 자산을 Codex가 역할별 자동 할당하고 개발 작업이 구현한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자가 개별 이미지 선택의 지연을 줄이는 자동 할당과 ‘확실한 것부터 자동 할당’을 선택하고, 개발 작업 구현 이관 계획을 승인했다. [자동 할당 원문 r1](개체자산-자동할당-D440-개발인계-2026-08-31.md)의 `game-object-evidence-based-auto-assignment`는 `Confirmed`다.
- Codex는 현재 개체/WI·역할과 정확 자산 판본·용도/형상·필요한 기술 근거를 대조해 구성 초안을 자동 선택·저장한다. 이름 유사성/임의 신뢰도만으로 선정하지 않고 미정·불일치/부족근거는 추가 조사 후 보류하며 독립 항목은 진행한다. 사용자 전수 이미지 승인은 선행조건이 아니지만 실제 근거 검토·권리·판본 검사는 유지한다.
- 기존 선택은 보호하고 미선정 슬롯부터 처리한다. D434 다중 구성과 D439 로컬 반입을 재사용·최소 확장하며 현재 등록 정의를 조회한 뒤 첫 실제 묶음 최대10역할의 자동 선정·로컬 MySQL 저장·독립 재조회·멱등/경합을 검증한다. D431 단일 승인 대응과 기존 정의/관계·원천은 보존한다. 새 전체 자산 자동 승인이나 실제 World 생성이 아니다.
- 기획은 승인 문서·결과 검토를 맡고 개발 작업 `01a02198-8b2a-7491-ac93-366b30ff474c`이 코드·시험·DB·전문 배분을 맡는다. D439 기획 도구 소유는 자산 구성 후속 범위에 한해 개발로 이관하며 구형 참여 반입을 회귀한다. 개발이 정확 경로·최소 DDL을 선언해 소유를 결속하고 결과를 기획에 반환한다.
- 기존 로컬 검토 웹 제안은 매번 수동 선택 대신 자동 결과·근거·보류와 교체를 확인하는 방향으로 바꾼다. 첫 저장 결과의 선행으로 전체 웹/전수 촬영을 요구하지 않는다. 과거 D432~D439 승인/미선정 이력은 보존하며 이번 추가 자동초안 범위만 대체한다. 추가 VM·Azure업로드·유료 API·공개 게시·Scene/Save/E·r129/야간·commit/push는 포함하지 않는다.

## D-441 기존 Synty 기능 분류를 재사용하여 DB 검색과 자동 할당을 보완한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 Synty/Prefab 자산을 종류별로 관리하여 Codex의 추론과 할당을 쉽게 하도록 요청했다. [분류·검색 원문 r1](Synty자산-분류검색과자동할당-D441-개발인계-2026-08-31.md)의 `synty-taxonomy-assisted-assignment`는 `Confirmed`다.
- 기존 `synty-asset-human-taxonomy.json`의 실외/실내/공통·기능군/세부종류·자산계열과 기능 모듈/Unity 대장을 재사용한다. 기술 종류와 기능·형태·사용 환경·근거 상태를 구분하고 전체 분류 경로로 중복 말단을 식별한다. 팩은 출처이며 사용 영역이나 게임 능력이 아니다.
- D437의 불변 조사 사본을 덮어쓰지 않고 정확 자산 판본에 분류 관계·보조 특성·근거/판본을 결속해 기존 MySQL 목록 검색·D440 후보 조회에 사용한다. 이름/폴더 추정·대장 대응·실제 파일/이미지 확인을 분리하며 미분류는 부적합/제외가 아니다. 분류 일치만으로 자동 선정·사용 승인/E 승격하지 않는다.
- 개발이 기존 코드/계약/대장과 최소 추가 관계·검색 구현·로컬 저장/재조회·회귀를 소유한다. D440의 최대10역할에 필요한 분류를 먼저 보완하고 기존 전체 대장 대응/미대응을 계수하며, 전수 세부 분류·새 촬영 완료를 첫 결과의 선행으로 만들지 않는다. 원 D440/기존 사용자 선택·원천/상태·권리 경계를 보존하고 새 작업/큐·Scene/Save/E·VM/유료호출/외부게시·r129/야간·commit/push는 확대하지 않는다.

## D-442 실제 월드 배치 전에 패턴 조합과 배치 규칙을 정밀화하고 격리 미리보기로 검토한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 여러 패턴을 준비하고 배치 엔진의 규칙으로 패턴 간 어색함을 줄인 뒤 월드를 구성하기를 요청했으며, 수정 계획의 구현을 승인했다. [승인 원문 r1](패턴조합-배치규칙검증-D442-개발인계-2026-08-31.md)의 패턴 사전 조합·격리 미리보기·예외만 사용자 검토는 `Confirmed`다.
- 이번 범위는 기존 자산/패턴 → 조합 후보 → 규칙 검사 → 격리 미리보기 → 국소 보완이다. 앞선 실제 Farm/월드 적용까지의 제안은 대체하며 D406/D422 과거 승인으로 실제 World 배치·보행을 자동 재개하지 않는다. D440/D441의 독립 승인·기존 자산 선택은 보존한다.
- H/AreaSet·배치 구조와 계획/검증/조립 책임을 재사용한다. 선언된 경계/경사 등의 계산 전달 누락과 식별자 순서 반복 검사의 공간상 이웃 공백을 보완하며, 패턴 내부·접점·전체 조합을 검사한다. 필수 제약과 시각 비교 기준, 농장 정렬과 숲 군집, 물리 점유와 허용 수관 겹침을 구분한다. 근거 누락은 미검사이며 새 수치는 검토 판본으로 격리한다.
- 첫 패턴은 기존 헛간 작업마당·혼합 작물밭·숲 가장자리 A/B/C 재고다. Codex가 기존 기준 안의 기본형/변형을 선택하고 결과를 보고하며 새 게임 선택·기준 변경만 질문한다. 기존 판본/해시/실행을 보존하고 새 검사를 명시적인 검토 경로로 연결한다. 외형 조합마다 새 H/WI를 만들거나 검사 통과를 E 성립으로 바꾸지 않는다.
- 개발이 정확 경로/명세·연구·소유를 결속해 코드·시험·통합하고 공간 담당이 단독 Editor의 임시 검토 공간에서 정상/문제 조합을 비교한다. 공식 Scene·실제 Session/World·Save는 변경하지 않으며 자기 복사본과 자원만 관리한다. 새 렌더 실패는 즉시 분리하고 전수 촬영/같은 실패 반복으로 확대하지 않는다.
- 완료는 패턴 목록·규칙 판본·문제 위치/사유·격리 비교 이미지다. 실제 월드 배치/플레이·게임 권위·외부 비용/게시·원본 가공·r129/야간·commit/push는 제외하고, 미관 판단과 자동 검사 증거를 구분한다.

## D-443 기존 노드·엣지 구조를 재사용해 패턴 관계와 연결 의도를 검사한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 농장과 농장 같은 패턴/객체를 노드·엣지로 관리하고 길 연결·부자연스러움 규칙을 점진 추가하기를 요청했으며, 길이 연결되지 않은 경우도 허용할 수 있음을 설명했다. [추가 승인 원문](패턴그래프-노드엣지와연결정책-D443-개발인계-2026-08-31.md)의 `pattern-graph-rule-management`와 `pattern-connectivity-intent-distinction`은 `Confirmed`다.
- 현재 경관 골격·조회 계약에 노드/엣지와 연결점 검사가 있음을 파일로 확인했다. 새 그래프 엔진/DB를 만들지 않고 D442의 기존 검토 코드에 포함·이웃·경계·통행 관계 구분과 관계별 연결 의도/관측을 결속한다. 같은 패턴의 두 사용은 서로 다른 인스턴스이며 외형 묶음을 새 H/WI로 자동 등록하지 않는다.
- 연결 필수·선택·의도된 분리·미정을 구별한다. 필수 연결의 알려진 막힘은 실패, 정보 부족은 미검사이며 엣지 부재를 자동 허용/실패로 판정하지 않는다. 선택형 미연결을 허용하되 선언된 경로의 정상성은 검사하고, 모든 노드를 강제로 하나의 통행 성분으로 연결하지 않는다. 기존 폐루프가 요구한 접근·결과·복귀 조건은 유지한다.
- 그래프 도달성과 실제 지지/형상·문/통로 조건, 군집·반복·여백의 시각 검토를 함께 사용한다. 이웃/포함 엣지를 통행으로 오인하지 않고 문제 노드/엣지·위치·규칙 판본·근거를 반환한다. 비어 있는 연결점·폭/높이를 추정하거나 새 길을 자동 개설하지 않는다.
- 개발이 D442 기존 소유/명세·시험에 추가 결속하고 공간 담당의 격리 검토를 조율한다. 기획 원 D442/hash와 구형 코드 결과를 보존하며 실제 World/Scene/Session·Save·새 권위/H/WI/E·전수 DB·외부 비용/게시·r129/야간·commit/push 범위를 확대하지 않는다.

## D-444 그래프 맵을 기획서의 지금·여기·나·너·이렇게와 연결하여 검토한다

- 상태: `ConfirmedDirection` (2026-08-31). 사용자는 실제 월드 배치 전 공간 관계 데이터를 ‘그래프 맵’이라고 부르고 배치 엔진에서 활용하는 방향에 동의했으며, 기획서의 지금·여기·나·너·이렇게와 노드/엣지를 연결하는 제안서 작성을 요청했다. [기획 연계 제안 r1](그래프맵-기획서연결-제안-2026-08-31.md)은 `ReadyForReview`이며 상세 연결 항목·조회 화면·첫 적용/구현은 미승인이다.
- 기획의 장소·조건·WI·결과·다음 선택과 그래프의 공간 관계를 함께 이해하는 방향을 기록한다. 공간 통행과 행동 인과관계, 객체 정의와 개별 배치, 기획 의도와 실제 상태/검증을 구분한다. 기존 Q/WI/H·공통 문서·공간 색인/그래프를 재사용하는 제안이며 새 H/E 축·그래프 엔진/DB를 추가 승인하지 않는다.
- 기획은 제안서/이 결정만 작성한다. 기존 D442/D443 구현은 독립 유지하고 상세 제안은 승인 전 작업 지시로 사용하지 않는다. CURRENT/통합판은 개발이 문서 검토 대기 사실만 인수하며 코드/검색/명세·DB·Unity/실제 World·Save·E·r129/야간·commit/push 범위는 확대하지 않는다.

## D-445 그래프 맵의 플레이 관계와 세부 배치 규칙을 두 상세 수준으로 문서화한다

- 상태: `ConfirmedDirection` (2026-08-31). 사용자는 그래프 맵에 배치 규칙도 포함하고, 레벨 1은 지금·여기·나·너·이렇게의 기획 관계, 레벨 2는 세부 공간·배치 규칙을 드러내도록 나누어 관리하기를 요청했다. [기획 연계 제안 r2](그래프맵-기획서연결-제안-2026-08-31.md)의 3절에 반영한다. D444 r1/해시와 인수 이력은 보존하고 상세 제안은 `ReadyForReview`로 유지한다.
- 두 레벨은 같은 그래프의 상세 수준이며 H1/H2 또는 E 단계에 대응하지 않는다. 규칙은 적용 노드·엣지·경계·구간/조합과 기획 근거를 참조하고, 레벨 2의 필수 실패·입력 부족은 레벨 1에서도 요약해 읽도록 제안한다. 독립 그래프 두 개·새 H/E 축·새 규칙 엔진을 만들지 않는다.
- 출입구 바깥 길 가장자리의 일정 구간에 울타리를 둔다는 예시는 기록하되, 발화 거리 `2~30m`·편측/양측·여유 폭·간격·마감/교차로 처리 등은 확정 규칙으로 사용하지 않는다. 길을 따른 거리 측정 등 구체 서식은 제안이다. 실제 출입구·경로·형상·통행 근거를 발명하지 않는다.
- 이번 반영은 기획 문서화다. 기획 소유는 기존 제안서/DECISIONS, 개발 소유는 CURRENT/통합판의 인수 상태다. 기존 D442/D443 실행을 중복 배분하지 않고 새 코드/스키마·검색/DB·Unity/World·Save·E·r129/야간·commit/push로 확대하지 않는다.

## D-446 그래프 맵 기반 정밀화를 E5 준비·입증의 공식 주력 절차로 채택한다

- 상태: `Confirmed` (2026-08-31). 사용자는 지금 만든 체계를 E5 전에 주력으로 정밀하게 활용하고 E5 입증까지의 공식 절차로 확립하되 E6·E7까지는 진행하지 않기를 요청했다. [공통 방법론의 E5 공식 절차](../Architecture/플레이폐루프논리시각이중순환체계.md#e5-focused-workflow)를 단일 기준으로 정하고 [플레이어 중심 기획](../Architecture/플레이어중심게임개발업무구조.md)에 연결한다.
- 지금·여기·나·너·이렇게와 결과·다음 선택에서 작은 승인 WI/객체를 골라 레벨 1의 플레이·공간 관계, 레벨 2의 자산/패턴·배치 제약, 코드/시험·E4 인계, 승인된 E5 실제 결속과 결과 문서화를 왕복한다. 두 레벨은 H/E와 다른 상세 수준이다. 비공간·비시각 항목은 사유 있게 제외하고 전체 월드맵·전수 자산/문답·새 그래프 UI 완성을 선행시키지 않는다.
- E 정의·논리/표현 분리·통합 최솟값·D390의 개별 성립 범위를 보존한다. 문서·자산 할당·그래프/규칙 검사 통과는 실제 E5가 아니며, 표현 E5는 논리 E5 상태 사본과 실제 대상/판본의 표현 결속을 요구한다. 필수 통행·입력/결과·소유·상태 결손은 뒤 단계로 미루지 않고 해당 연결의 차단으로 남긴다. 부분 근거를 전체 폐루프 E5로 올리지 않는다.
- D444/D445의 [기획 연계 제안 r3](그래프맵-기획서연결-제안-2026-08-31.md)는 절차 채택과 상세 설계 검토를 분리한다. 원 r1/r2 SHA와 인수 이력은 보존하며 상세 schema/조회 UI·울타리 예시 수치·새 플레이 규칙은 자동 승인하지 않는다. 기획 공통 원문의 새 판본은 별도로 인수하고 과거 승인/증거 hash를 일괄 바꾸지 않는다.
- 이번 반영은 기획 문서 4개와 개발의 문서 인수 범위다. 개발이 코드·시험·전문 배분/통합을 계속 소유하며 추가 기술 구현은 해당 승인·명세에 결속한 범위만 수행한다. E6·E7 자동 속행, 새 코드/DB·검색 재생성·Unity/실제 World·Scene/Save·E 승격·r129/야간 재개·commit/push를 승인하지 않는다. 기존 D442/D443의 독립 승인 범위는 유지한다.

## D-447 공식 준비·입증 절차를 E6 정제와 필요한 현실 근거 결속까지 확장한다

- 상태: `Confirmed` (2026-08-31). 사용자는 E6의 정제와 공공데이터 근거 추가를 고려하여 기존 E5 절차를 E6 준비·입증의 공식 절차로 수정하기를 요청했다. [공통 방법론](../Architecture/플레이폐루프논리시각이중순환체계.md#e6-focused-workflow)의 적용 범위를 확장하고 공통 기획·[그래프 맵 설명 r4](그래프맵-기획서연결-제안-2026-08-31.md)를 동기화한다. D446의 E5까지만이라는 절차 범위는 이 결정으로 대체하되 당시 원문·인수·hash와 실제 E는 보존한다.
- 기존 E1~E4 준비와 E5 실제 성립을 생략하지 않고, 현재 E5 기준선에서 의미·인과·배치·Actor/물건·상태 변화·피드백·회복/귀환·권위/최신성을 정제한다. 그래프 레벨 1/2에서 관련 WI·노드/엣지와 질문·근거를 함께 읽으며 새 E/H 축·레벨 3·중복 원장을 만들지 않는다. AreaSet의 필수 H3 질문·교차 H3 관문과 논리/표현 분리·통합 최솟값은 유지한다.
- 공공·현실 자료는 필요한 정제 질문에만 출처·대상/지역/기간 대응·단위·시각·판본/hash·이용/해석 한계와 함께 결속한다. 기존 자료를 우선 쓰고 별도 조사 요청은 기존 ASP.NET Core·Docker MySQL 수집/저장/재조회 절차를 따른다. 수집/DB 존재·자료 매칭을 E6 합격으로 세지 않으며 미확보·낡음·미승인 대응을 가짜 값으로 메우지 않는다. GIS/공공자료를 모든 WI의 필수 선행으로 만들지 않는다.
- 실제 게임 소비는 승인 자료 사본의 세션 시작 동결·의미 신호·기존 Simulation 규칙·간접 표현 경계를 따른다. 자료나 그래프가 생산량·가격·성공·사건·H5 배치를 자동 확정하지 않는다. 원문 상세는 선택형 참고로 유지하며 실제 수집·저장·게임 소비·화면 검증은 각각 보고한다.
- 종료는 E5 근거/E6 정제/현실 근거·한계/남은 결함/E7 인계를 분리한다. E7 전체 실제 플레이·`PlayClosed`는 이번 절차 완료가 아니며 자동 속행하지 않는다. 이번은 기획 4문서와 개발 인수 범위로서 개별 Goal/명세 상한 일괄 변경·새 코드/검색 Write/DB/Provider·Unity/Scene/Save·E 승격·r129/야간·commit/push를 승인하지 않는다. 기존 독립 승인 작업은 유지한다.

## D-448 누적 변경을 정합화하고 소유가 확인된 맥락별로 커밋한다

- 상태: `ApprovedScopedWork` (2026-08-31). 사용자는 변경된 상황을 반영해 전반적으로 리팩토링한 뒤 맥락별로 나누어 커밋하기를 요청했다. [범위·결과 보고](../Reports/전반리팩토링과맥락별커밋-2026-08-31.md)에 저장소별 기준선·소유·검증·제외를 기록한다. 커밋 금지의 기존 일반 경계는 이 요청 범위에서만 대체하며 푸시는 하지 않는다.
- 기획의 최신 기준 안내, 구현 책임·중복, 원문/조회·생성물 불일치를 기존 동작과 승인 의미를 보존하는 범위에서 정리한다. 코드·시험은 개발이 담당하고 기획은 문서와 최종 Git 분류·커밋을 조율한다. 실제 의존성·소유를 확인하지 않은 전역 재설계나 무관한 변경의 일괄 stage는 하지 않는다.
- 검색/생성물의 최신화는 현재 원문·의미·판본과 소비 관계를 대조한 뒤 해당 입력·파생물에 한정한다. 과거 승인·실행 증거 hash를 현재 값으로 덮어쓰거나 검사 기준을 완화하지 않는다. 완료 코드·시험과 미완료 연결/근거를 별도로 기록한다.
- 미소유 변경·Scene/원본 자산·개인/비밀/운영 원자료·다운로드 바이너리·임시 결과를 보존하며 확인되지 않은 항목은 커밋에서 제외한다. 새 게임 규칙·E 승격·실제 운영/수집·중지된 World/야간 재개·강제 복구·Scene 일괄 저장은 승인하지 않는다.

## D-449 허브를 발견하는 3인칭 도입과 광역 노드 관찰을 기획한다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 양조장이 아닌 허브를 도입의 중심으로 두고, 3인칭으로 수풀 주변을 이동하다 허브에 도달하여 업무를 알아차릴 시간을 갖기를 요청했다. [허브 발견 기획 r1](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 현장/광역 관찰·허브 노드 선택·입구/접점/출구 내역·NPC의 자유로운 퀘스트 참여를 기록한다.
- 이번 허브 도입의 최초 시점은 D417의 권장 1인칭 대신 3인칭으로 정리한다. D417/r3 원문·과거 증거, 다른 경로·저장 복원/전투 잠금·접근성 시점·1인칭 집중 기능은 보존한다. 허브 도입을 기존 농장/양조 경로의 삭제나 전 영역 개발 선후행 의존으로 바꾸지 않는다.
- 세 내역의 구분은 채택하되 구체 의미·정보 범위는 미정이다. ‘접점’을 인수/보관/분류/NPC 의뢰 등 활동 지점으로 해석하는 추천은 `hub-contact-detail-meaning / Asked`다. 입구/출구를 입출고 원장과 자동 동일시하지 않으며 노드 클릭은 운영 명령·이동·자동 수락·보상이나 숨은 정보 공개가 아니다.
- 절기 이름에는 날짜 범위를 괄호로 병기하며 기준 연도/대략을 구별한다. 이번 춘분 날짜는 설명 참고이지 게임 연도·배속 확정이 아니다. 현장과 광역은 같은 상태를 읽고 실제 참여의 자격·약속·실패/회복은 후속 문답으로 정한다.
- D447 공식 절차로 기존 WI/객체·그래프 두 상세 수준·자산/규칙·E4 준비와 E5/E6 결손을 연결한다. 이번은 기획 문서와 읽기 인계뿐이며 새 코드/스키마·DB/검색 Write·UI/카메라/World·Scene/Editor·E 승격·r129/야간·commit/push는 승인하지 않는다.

## D-450 허브를 데이터 통합과 공통 개발 검증의 중심 사례로 활용한다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 여러 데이터가 모이는 허브를 중심으로 사고하면 개발을 효율화할 수 있다고 제안했다. [허브 발견 기획 r3](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)의 5절에 원천 상태 사본→허브 요약/내부 상태→현장·광역 표현→선택/결과·재조회 흐름과 기존 계약 읽기 결과를 추가한다.
- 허브는 Nature·Farm·Town·City와 운영 서버의 모든 상태를 소유하는 전역 원장이 아니다. 각 영역 또는 독립 Fixture의 판본화된 사본을 읽고 허브 내부 보관·예약·분류·피킹·출고 준비와 참여 결과만 해당 권위에서 확정한다. 영역별 독립 완결과 선택형 연결을 유지한다.
- 상태 사본·최신성·공개/권한·Preview/Confirm·표현 모듈을 같은 작은 사례에서 검증해 공통 경계를 정밀화한다. 데이터가 많다는 이유로 모든 필드를 한 화면에 노출하거나 허브 전용 용어를 새 전역 프레임워크로 만들지 않는다. 기본 요약→선택 노드 상세→출처/판본 상세로 펼치는 방향은 기획이며 정확 UI는 미정이다.
- 이 방향만으로 신규 WI/Graph/H/자산 대응·기존 DB/코드·Goal/명세를 자동 등록하거나 E를 올리지 않는다. 실제 구현은 후속 질문과 승인 기획·Required 연구·정확 작업 명세를 결속한 뒤 개발에 인계한다. 현재 새 코드/DB/검색/Editor/Scene·r129/야간·commit/push 승인은 없다.
- 개발 읽기 검토에서는 기존 Hub Fixture·개정/계보·Preview/Confirm·조회 경계를 재사용할 수 있으나, 현행은 내부 NPC 루틴 Logic E3 / Presentation·통합 E1 범위이며 다영역 카드·플레이어 퀘스트·광역 UI가 아님을 확인했다. 발견/관찰자 공개 범위가 없으므로 원 조회를 UI에 노출하지 않는다. 다음 현재 질문은 `hub-node-first-visible-scope / Asked`이고 기존 `hub-contact-detail-meaning`은 다음 순서로 보존한다.

## D-451 허브를 화물차·기사 NPC가 오가는 물류센터로 표현하고 창고 상세를 두 시점에서 함께 읽는다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 허브를 화물차가 많이 오가고 기사 NPC와 대화할 수 있는 물류센터 성격으로 표현하기를 원했다. [허브 발견 기획 r4](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 생활 표현과 기사 대화·퀘스트 방향을 기록한다. 차량 수·동선/밀도·기사 역할·대화/퀘스트 내용·보상/실패는 아직 확정하지 않는다.
- 3인칭 현장의 창고 배치 객체와 광역 화면의 창고 노드는 같은 창고·같은 상태 사본으로 입고·출고·적재 내용을 보여준다. 시점별 별도 재고를 만들지 않고 선택/열람을 입출고 실행·예약·문 열림·순간이동으로 해석하지 않는다.
- 세 내용 묶음은 확정했지만 정확 필드·수량/예정/완료·시간 범위·공개/권한과 `적재`의 범위는 후속 설계다. 현행 내부 조회를 UI에 그대로 노출하지 않고 관찰 가능한 요약과 역할/권한 상세를 분리한다.
- 다음 현재 질문은 `hub-driver-first-dialogue-purpose / Asked`다. 운송 상황·도착/지연 사유·공개 도움 요청을 같은 상태 사본에서 대화 단서로 제공하고 선택형 퀘스트로 이어갈지 묻는다. 새 코드/자산/차량·NPC/창고 연결·DB/검색/Editor/Scene·E·r129/야간·commit/push를 승인하지 않는다.

## D-452 현실 물류 자료를 게임용 허브 물류 사본으로 변환하고 선택형 상세에서 대응 근거를 보여준다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 온라인 쇼핑·물류의 물동량 자료를 기존 ASP.NET Core 서버와 MySQL에 축적하고, 이를 기반으로 게임의 입고·출고·적재 데이터를 만들며 사용자가 상세보기에서 현실 자료와의 대응을 확인하는 방향을 요청했다. [허브 발견 기획 r5](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 수집→승인 사본→게임 의미 신호→선택형 현실 상세 흐름을 기록한다.
- 현실 자료 원문을 Unity나 창고 UI가 직접 읽지 않는다. 기존 외부자료 등록·수집/정규화·출처/해시·멱등 저장과 `SimulationRealityContext`의 승인 사본 동결 경계를 우선 재사용하며, 게임 입출고·재고·차량 상태의 최종 권위는 Simulation에 둔다.
- 국가·산업 단위의 현실 물동량을 게임 창고의 상자 수나 수익으로 그대로 복사하지 않는다. 지역·기간·단위·품목 대응·허브 규모·게임 시간·변환 판본과 한계를 기록하고, 자료 없음/낡음/대응 미검토를 가짜 값으로 메우지 않는다.
- 현실 자료 상세는 관심이 생긴 사용자가 선택해 보는 참고다. 출처·기준 기간/지역·원 단위·변환 요약·제약을 보여주되 게임 진행·퀘스트·성장·보상·구매/계약의 필수 조건으로 만들지 않는다.
- 첫 질문은 `hub-first-reality-derived-logistics-signal / Asked`다. 입고·출고 추세, 불균형, 품목군 비중을 상대 단계로 변환해 창고 분주함·적재 압력·기사 대화 단서에 함께 쓸지 묻는다. 정확 원천/API·이용조건·수집 주기·변환식·공개 필드는 미정이며 이번 결정은 실제 수집·DB 쓰기·새 코드/Provider/UI·Unity/Scene·E·commit/push를 승인하지 않는다.

## D-453 첫 현실 연계 물류 신호는 입출고 추세·불균형·품목군 비중의 상대 단계로 둔다

- 상태: `Confirmed` (2026-09-01). 사용자는 D452의 추천을 채택했다. 현실 자료의 정확 건수를 게임 창고 수량으로 복사하지 않고 `입고량·출고량 추세`, `입출고 불균형`, `품목군 비중`을 허브 규모에 맞춘 상대 단계로 변환한다.
- 창고의 분주함·적재 압력과 기사 NPC의 공개 대화 단서는 같은 승인·동결 사본과 변환 판본을 읽는다. 화면이나 NPC마다 별도 사실을 만들지 않으며 이 신호만으로 실제 차량 도착·재고 변경·입출고 완료를 확정하지 않는다.
- 정확 단계 수·임계값·시간창·품목군 분류·자료 공급처는 후보 자료의 지역/기간/단위·분포·품질·이용조건을 확인한 뒤 정한다. 선택형 상세에는 원 단위, 변환 기준과 현실 건수≠게임 수량 경계를 표시한다.
- 다음 질문은 `hub-driver-first-dialogue-purpose / Asked`로 돌아간다. 이번 확정은 실제 자료 수집/DB 쓰기·변환 코드·UI/NPC/Unity 연결·E 승격·commit/push를 승인하지 않는다.

## D-454 플레이어가 접촉한 게임 데이터의 현실 대응 보고서를 선택적으로 생성·통지할 수 있게 한다

- 상태: `ConfirmedDirection / FutureExtension` (2026-09-01). 사용자는 나중에 플레이어가 원하면 접촉했던 게임 데이터를 현실 자료와 대응시킨 보고서를 이메일 등 연락 수단으로 받을 수 있는 방향을 제안했다. [허브 발견 기획 r7](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 선택형 보고·전달 경계를 기록한다.
- 보고서는 게임 접촉 기록을 현실 사실로 단순 치환하지 않는다. 검토된 대응만 `게임에서 본 것/현실 자료/대응 이유/출처·지역·기간·단위·최신성/한계`로 나란히 제시하며 미검토·낡음·재노출 불가 자료는 이유를 표시하고 제외한다.
- 생성·발송은 기본 꺼짐이다. 대상 기간·항목·전달 수단 선택, 보고서 미리보기와 별도 발송 확인을 요구하며 연락처 동의·검증·철회·실패/발송 기록을 게임 공개 범위와 분리한다. 게임 상호작용만으로 구독하거나 자동 발송하지 않는다.
- 선택형 현실 참고의 기존 경계를 유지한다. 현실 수익·구매·계약·투자 추천, 실제 업무 실행, 광고나 제3자 자동 연락으로 확대하지 않는다. 과거 보고서는 원 자료 갱신 시 조용히 덮어쓰지 않고 입력·자료·변환 판본을 보존한다.
- 정확 형식·주기·보존기간·채널·수신자·연락처 처리·해지·비용은 미정이다. 현재 질문은 허브 플레이의 `hub-driver-first-dialogue-purpose / Asked`를 유지하며 이번 결정은 이메일/메시징 Provider·DB·스케줄러·실제 발송·코드/UI·E·commit/push를 승인하지 않는다.

## D-455 허브 설명과 도움 요청은 물음표·느낌표 표시를 사용자가 선택할 때만 연다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 NPC나 창고에 설명이 있음을 `?`·`!` 표시로만 알리고, 설명을 들을지는 사용자가 선택하게 하기를 원했다. [허브 발견 기획 r8](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 자동 설명/강제 팝업 금지와 선택형 열람을 기록한다.
- 현장 3인칭 대상과 광역 노드는 같은 대상 고유 식별자·공개 범위·상태 판본에서 표시와 내용을 파생한다. 근접·발견만으로 대화를 시작하거나 화면을 멈추지 않으며, 표시 선택·설명 열기·퀘스트 미리보기·수락/실행을 분리한다.
- `?=읽기 전용 상황·시설·현실 자료 설명`, `!=공개 도움 요청·퀘스트 후보`를 추천하되 정확 의미는 `hub-question-exclamation-exact-meaning / Asked`로 확인한다. 아이콘만으로 정보를 전달하지 않고 접근성 텍스트/형태를 함께 준비한다.
- 정확 UI·거리·크기·겹침 우선순위·입력은 미정이다. 이번 결정은 새 UI/아이콘 자산·대화/퀘스트 코드·Unity/Scene·E·commit/push를 승인하지 않는다.

## D-456 물음표는 설명, 느낌표는 도움 요청으로 구분하고 함께 있으면 통합 표시를 사용한다

- 상태: `Confirmed` (2026-09-01). 사용자는 D455의 추천을 채택했다. `?`는 상황·시설·현실 자료의 읽기 전용 설명, `!`는 공개된 도움 요청·퀘스트 후보를 뜻한다.
- 한 대상에 둘 다 있으면 두 아이콘을 겹치지 않고 통합 표시 하나를 둔다. 사용자가 선택한 뒤 내부에서 설명과 도움 요청을 구분하며, 통합 표시 선택도 퀘스트 수락·업무 실행·보고서 생성이 아니다.
- 현장과 광역은 같은 대상·공개 범위·상태 판본에서 표시 상태를 읽는다. 정확 통합 도형·내부 탭/목록·크기/거리/우선순위·접근성 표현은 후속 자산/UI 검토로 남긴다.
- 현재 질문은 `hub-driver-first-dialogue-purpose / Asked`로 돌아간다. 이번 확정은 새 UI/아이콘·대화/퀘스트 코드·Unity/Scene·E·commit/push를 승인하지 않는다.

## D-457 색과 보조표식이 다른 물음표로 정보·퀘스트 획득을 구분하고 느낌표는 완료 확인에 사용한다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 정보 또는 퀘스트를 얻는 대상은 색이 다른 `?`로 구분하고, `!`는 퀘스트를 클리어하는 표시로 두기를 제안했다. 이 결정은 D456의 `?=설명, !=퀘스트 후보` 의미를 대체하며 [허브 발견 기획 r10](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 새 체계를 기록한다.
- 추천 기본은 파란 `?+정보 표식`=읽기 전용 설명, 금색 `?+의뢰 표식`=새 퀘스트 후보, 초록 `!+완료 표식`=권위 목표 충족 후 완료 확인/보고 가능이다. 색만으로 구분하지 않고 내부 모양·상태 텍스트·화면 읽기 이름을 함께 둔다. 정확 색상은 UI 팔레트 검토 전 미정이다.
- `!`는 같은 퀘스트·목표 판본의 권위 결과가 완료 가능을 증명할 때만 표시한다. 표시 선택은 완료 내역/보상/다음 선택 미리보기이며 별도 Confirm이 있으면 자동 정산하지 않는다. 한 대상에 여러 상태가 있으면 통합 표시 뒤 정보/새 의뢰/완료 보고를 구분한다.
- 다음 질문은 `hub-quest-clear-marker-after-confirm / Asked`다. 완료 확인·정산 뒤 World `!`를 없애고 기록에 완료 체크를 남기며 후속 퀘스트가 생길 때 새 `?`를 표시할지 묻는다. 정확 자산/UI·퀘스트 상태 계약·Unity 연결은 미정이며 이번 결정은 코드/Scene/E·commit/push를 승인하지 않는다.

## D-458 퀘스트 정산 뒤 World 완료 표시는 제거하고 기록과 실제 후속 의뢰 표시를 분리한다

- 상태: `Confirmed` (2026-09-01). 사용자는 완료 확인·정산 뒤 World의 `!`를 제거하고 퀘스트 기록에 완료 체크를 남기며, 후속 의뢰가 생기면 새 금색 `?`를 표시하는 추천을 채택했다.
- `!` 제거는 완료 이력 삭제가 아니다. 퀘스트 ID·목표/결과 판본·완료 시각·결과/보상 상태는 기록에 남기고, 일반 대화나 과거 설명 재열람은 완료 아이콘과 분리한다.
- 후속 금색 `?`는 실제 후속 퀘스트 후보가 생성되고 공개 조건을 충족할 때만 나타난다. 단순 UI 교체로 후속 퀘스트를 생성하거나 자동 수락하지 않는다.
- 다음 질문은 `hub-driver-first-dialogue-purpose / Asked`로 돌아간다. 정확 퀘스트 원장/UI·자산·Unity 연결은 미정이며 이번 확정은 코드/Scene/E·commit/push를 승인하지 않는다.

## D-459 의미 있는 금색 의뢰 완료는 개인 회복 기여를 만들고 검증된 공동체 기여로 집계한다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 금색 퀘스트를 클리어하면 개인 회복 수치가 오르고, 개인의 회복 상승이 공동체 회복을 높이는 방향을 제안했다. [허브 발견 기획 r12](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 개인→공동체 기여 계보와 악용 방지 경계를 기록한다.
- 의미 있는 완료는 기존 행위 기록과 Q122의 개인 NatureMind 회복 기여를 재사용한다. 체력/상처 치료와 구별하고 Preview·취소·실패·자동/NPC 대행·멱등 재요청·무의미 반복은 회복 입력이 아니다.
- 공동체 회복은 개인 수치의 원시 합계가 아니다. 실제 공동체 문제 완화 결과·참여자·대상/범위·판본·반복 여부를 원인으로 정규화해 Q196~198의 별도 공동체 관점에 반영한다. 개인/공동체 광복기는 독립 판정을 유지한다.
- 현재 질문은 `hub-community-recovery-eligible-quest-scope / Asked`다. 개인 회복은 모든 의미 있는 금색 의뢰 완료에 조금 주되 공동체 회복은 물류 적체·식량 부족·시설 고장·안전 위험 같은 공동 문제를 실제 완화한 의뢰에만 반영할지 묻는다.
- 정확 수치·상한·감쇠·Profile·Save/Replay·UI/음향과 적용 WI는 미정이며 이번 방향은 코드/DB/Unity/Scene·E·commit/push를 승인하지 않는다.

## D-460 개인 회복은 공동체 회복의 기본층에 반영하고 공공 문제 해결은 더 강한 기여층으로 둔다

- 상태: `Confirmed` (2026-09-01). 사용자는 개인 회복이 합산되어 공동체 회복으로 드러나는 기본 관계를 유지하면서, 공적인 문제를 해결하면 공동체 회복이 더 크게 개선되도록 구분하기를 원했다. D459의 ‘공동 문제 의뢰만 공동체에 반영’이라는 좁은 질문은 이 결정으로 대체한다.
- 공동체 기본층은 구성원 개인 회복/위협의 분포·최근성·참여 상태를 정규화해 읽는다. 모든 개인 회복은 작게 영향을 주지만 인구 수나 원시 합계를 그대로 사용하지 않고 소수 반복으로 급상승하지 않게 상한·감쇠·구성원 범위를 판본화한다.
- 공공 기여층은 물류 적체·식량 부족·시설 고장·안전 위험 같은 공동 문제의 실제 완화 정도·영향 범위·참여 분산·지속성을 읽어 기본층보다 큰 영향을 줄 수 있다. 같은 행위에서 개인 회복 경로와 공공 결과 경로는 원인 코드를 분리해 이중 계산하지 않는다.
- 개인/공동체 회복·위협과 광복기 판정은 기존 Q196~198처럼 별도 상태로 유지한다. 공동체 상태가 개인 상태를 덮어쓰거나 한 개인의 회복이 공동체 광복기를 자동 확정하지 않는다.
- 다음 질문은 `hub-community-recovery-member-scope / Asked`다. 플레이어 외에 허브에 실제 소속·체류하며 관측되는 NPC의 개인 회복도 기본층에 포함하고 방문자·배경 Actor는 제외할지 묻는다. 정확 Profile·수치·소속/공개 계약·Save/UI는 미정이며 코드/DB/Unity/Scene·E·commit/push를 승인하지 않는다.

## D-461 공동체 회복 기본층은 실제 소속·체류·관측 구성원만 집계한다

- 상태: `Confirmed` (2026-09-01). 사용자는 허브에 실제 소속되어 생활하는 플레이어와 NPC의 개인 회복을 공동체 기본층에 포함하고, 잠시 지나가는 방문자나 상태 없는 배경 인물은 제외하는 추천을 채택했다.
- 집계 대상은 소속 관계뿐 아니라 현재 또는 최근 체류와 권위 개인 상태가 있어야 한다. 통과 기사·일시 방문자·배경 Actor·관계 종료/장기 미체류 명목 구성원은 제외한다. 정확 최근성 시간창과 휴가·원정·입원·오프라인 처리 규칙은 후속 Profile로 남긴다.
- 공개 공동체 패널은 개인별 내면 상태를 노출하지 않고 정규화된 집계와 공개 가능한 주요 원인만 보여준다. 역할 권한이 있어도 민감한 개인 원인을 자동 공개하지 않는다.
- 다음 질문은 `hub-driver-first-dialogue-purpose / Asked`로 돌아간다. 실제 구성원 계약·집계/Save/UI는 미정이며 이번 확정은 코드/DB/Unity/Scene·E·commit/push를 승인하지 않는다.

## D-462 파란 물음표는 일반 퀘스트, 금색 물음표는 메인 퀘스트로 두고 일반 경험으로 메인을 준비한다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 퀘스트를 일반/메인으로 구분하고 파란 `?`는 일반, 금색 `?`는 메인 퀘스트로 표시하며 일반 퀘스트를 적절히 완료해야 메인이 열리도록 하기를 원했다. 이 결정은 D457의 파란 `?=정보`, 금색 `?=퀘스트` 의미를 대체하고 D458의 `!=완료 확인`은 유지한다.
- 메인 해금은 일반 퀘스트의 원시 완료 횟수가 아니라 필요한 사람·시설·문제 원인과 준비 결과를 갖추었는지 판정한다. 무관한 반복 퀘스트로 해금하지 않고 같은 준비 목적에는 전투·운반·대화·조사 등 여러 대체 경로를 허용한다.
- 추천 첫 범주는 `물류 흐름 이해`, `구성원 접촉/신뢰`, `공공 문제 기여`다. 잠긴 메인은 공개 가능한 부족 범주만 보여주며 해금과 수락을 분리한다. 일반/메인 등급만으로 개인·공동체 회복 보상을 정하지 않고 실제 완료 품질과 공공 문제 완화 효과를 사용한다.
- 순수 정보 설명은 퀘스트 색 `?`와 충돌하지 않는 중립 정보 표식/기본 대화로 분리하는 방향이며 정확 도형은 미정이다.
- 다음 질문은 `hub-main-quest-general-readiness-categories / Asked`다. 세 범주에서 각각 최소 한 경험을 요구하고 범주별 대체 일반 퀘스트를 둘지 묻는다. 정확 퀘스트 그래프·해금 계약·UI/자산·Unity 연결은 미정이며 코드/Scene/E·commit/push를 승인하지 않는다.

## D-463 퀘스트에서 게임 문제 해결과 대응 현실 자료 이해를 함께 제공한다

- 상태: `ConfirmedDirection` (2026-09-01). 사용자는 퀘스트가 게임 데이터를 다루는 동시에 현실 문제와 자연스럽게 연결되어 게임과 현실에 대한 이해를 함께 높이기를 원했다. [허브 발견 기획 r16](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md)에 게임 해결층·현실 이해층·관계 설명층을 분리해 기록한다.
- 게임 상태와 행동만으로 기본 퀘스트 폐루프를 이해·완료할 수 있어야 하며 Simulation이 결과를 판정한다. 현실 자료는 출처·지역/기간·단위·최신성·변환 판본·한계를 가진 선택형 참고 후보이고 현실 수치 맞히기나 외부 자료 열람을 완료 조건으로 자동 사용하지 않는다.
- 현실 자료를 보면 원인 설명·대체 해결책·완료 보고가 풍부해질 수 있지만 실제 수익·구매/계약·정답을 보장하지 않는다. 열람 자체에는 개인/공동체 회복을 지급하지 않고 실제 게임 행위 완료와 공공 문제 완화를 회복 원인으로 사용한다.
- 자료 없음/지연/대응 미검토이면 그 상태와 Fixture/시나리오 출처를 표시한다. 완료 결과에는 게임 결과·참고 현실 자료·차이/한계·다음 선택을 분리해 남기며 현실 전문 자격 인증으로 표현하지 않는다.
- 다음 질문은 `hub-reality-layer-required-or-optional / Asked`다. 기본 완료는 게임 데이터만으로 가능하게 하고 현실 자료는 이해·선택 폭을 넓히는 심화 경로로 둘지 묻는다. 실제 수집/DB·Quest/UI·Unity 연결과 자료 권리 검증은 미정이며 코드/Scene/E·commit/push를 승인하지 않는다.

## D-464 동적 퀘스트는 그래프 호환 뼈대를 남기고 현재 퀘스트 문답을 마감해 개발에 인계한다

- 상태: `ApprovedScopedDevelopmentHandoff / FutureExtensionSkeleton` (2026-09-01). 사용자는 장기적으로 퀘스트가 그래프의 단계적 절차에 따라 동적으로 형성되기를 원하지만 이번에는 뼈대만 남기고 퀘스트 질문을 마감한 뒤 지금까지 확정한 범위를 개발 스레드에 넘겨 작업하도록 요청했다.
- 그래프 뼈대는 일반/메인 후보·목표·상태 요구·대상·결과·회복/공공 기여·현실 참고 노드와 선행/해금/대안/실패·회복/후속/배타·병행/현실 대응 엣지를 표현한다. 같은 권위 상태·공개 범위·ActionRecord·승인 Template/Profile/현실 의미 신호에서 판본화된 후보와 차단 이유만 만들며 자동 수락·보상·완료를 만들지 않는다.
- 같은 입력/판본의 결정성·Save/Replay·Local/Remote 재현을 요구하고 기존 WI/Graph/Goal과 별도 권위 원장을 만들지 않는다. 정확 schema·생성 규칙·첫 그래프·UI는 후속 승인이다.
- 남은 Asked 항목은 문답 일시중지로 보존하며 승인으로 간주하지 않는다. D454 보고/발송과 D464 전체 동적 생성은 미래 범위다.
- 개발은 r17과 D449~D464를 읽고 기존 계약 재사용/충돌을 확인한 뒤 Confirmed 범위의 가장 작은 vertical slice를 승인 hash·E7 작업 명세·경로/검증 상한에 결속해 구현한다. 이 인계는 자료 수집/DB 쓰기·실제 World/Scene·E 자동 승격·commit/push를 자동 승인하지 않는다.

## D-465 발견 사실은 자동 기록하고 계획 채택은 플레이어가 직접 선택한다

- 상태: `Confirmed` (2026-09-01). 사용자는 Nature Day2 다음 선택에서 발견 사실을 자동 기록하되 실제 계획 채택·수정·보류는 플레이어가 직접 결정하는 추천을 승인했다.
- 자원 변화·NPC 흔적·새 경로 같은 발견은 출처·대상·관측 판본을 가진 발견 기록으로 남긴다. 기록 생성은 활성 계획, 퀘스트 수락, 이동, 보상 또는 회복을 자동 발생시키지 않는다.
- 플레이어는 발견 기록을 계획 후보로 수용·수정·보류하거나 무시할 수 있다. 기존 Q-040의 언제든 접근 가능한 개인 계획과 Q-043의 내면의 울림 수용 선택을 재사용한다.
- 같은 발견의 반복 관측은 새 계획을 중복 생성하지 않고 기존 기록의 최신 관측을 갱신한다. 정확 관측 조건·추천 우선순위·UI·Save/Replay·Unity 표현은 후속 기획·연구 대상이며 이번 결정으로 코드·Scene·E를 자동 승인하지 않는다.
- 상세: [Nature Night Day2 Q-040 보완](../Architecture/PlayableLoops/PlanningSessions/nature-night-day2.inquiry.r1.md#d-465-보완--발견-기록과-계획-채택을-분리한다).

## D-466 현재 실행 가능한 발견만 계획 후보로 추천한다

- 상태: `Confirmed` (2026-09-01). 사용자는 발견 기록은 모두 보존하되 현재 실행 가능한 계획 후보만 추천으로 보여주는 방식을 승인했다.
- 추천 가능 여부는 같은 권위 판본의 플레이어·대상·공간 접근·도구·자원·선행 완료 조건으로 판정한다. 조건이 남았거나 불명확한 발견은 기록 상세에만 남기고 추천 목록에는 올리지 않는다.
- 조건이 나중에 충족되면 같은 발견 기록을 다시 평가하며 중복 후보를 만들지 않는다. 추천은 성공 보장이나 최적 행동 판정이 아니며 채택·수정·보류·무시는 플레이어가 결정한다.
- 이 결정은 자동 퀘스트·이동·보상·회복 또는 코드·Scene·E 승격을 승인하지 않는다. 권위 입력·차단 사유·재평가·Save/Replay·Local/Hosted 결정성은 후속 Logic·Presentation에서 기존 개인 계획·WI 조건 계약을 우선 재사용한다.
- 상세: [Nature Night Day2 D-466](../Architecture/PlayableLoops/PlanningSessions/nature-night-day2.inquiry.r1.md#d-466-보완--현재-실행-가능한-발견만-계획-후보로-추천한다).

## D-467 활성 계획과 가까운 실행 가능 후보를 먼저 보여준다

- 상태: `Confirmed` (2026-09-01). 사용자는 D-466을 통과한 실행 가능 후보 중 활성 계획과 가까운 후보를 먼저 보여주고 나머지도 후순위에서 모두 확인하는 방식을 승인했다.
- 목표·대상·공간·필요 WI 연결을 우선순위 근거로 사용하고, 동률 또는 활성 계획 부재 시 발견 판본과 StableId로 결정적 정렬한다. 같은 입력은 Save/Replay·Local/Hosted에서 같은 순서를 만든다.
- 후순위 후보는 숨기거나 비활성화하지 않는다. 표시 순서는 권위 상태·성공률·보상·위험·실행 가능 판정을 바꾸지 않으며 자동 채택을 만들지 않는다.
- 구체 즉시 노출 개수·펼침 UI·Unity 표현은 후속 기획·검증 대상이고 이번 결정으로 코드·Scene·E를 자동 승인하지 않는다.
- 상세: [Nature Night Day2 D-467](../Architecture/PlayableLoops/PlanningSessions/nature-night-day2.inquiry.r1.md#d-467-보완--활성-계획과-가까운-실행-가능-후보를-먼저-보여준다).

## D-468 관심 분야는 관련 이데아와 실행 기회를 더 쉽게 알아차리게 한다

- 상태: `ConfirmedDirection / E4Scoped` (2026-09-01). 사용자는 설정한 관심 주제와 관련된 이데아의 편린 및 작업을 더 쉽게 알아차리는 방향과 Nature 이번 구현 상한 E4를 요청했다.
- D-334/D-335의 관심 분야 카드·편린·학습 지식 분리를 유지한다. 관심은 같은 권위 판본에서 이미 관측 가능하고 실행 가능한 WI·대상·흔적의 주의 신호를 강화할 뿐, 편린·완성 지식·NPC·퀘스트·계획·보상·회복을 자동 생성하지 않는다.
- 편린 획득은 관심 강조와 별도 권위 결과이며 관련 행동·명상·숙련 조건을 실제 충족해야 한다. UI 강조나 카메라 노출은 편린 획득 증거가 아니다.
- 이번 Nature 범위는 Logic·Presentation E4까지다. 결정적 준비 사본·Save/Replay·Local/Hosted 준비 계약과 플레이어 판독 순간·VisualKey·fallback·자산/Adapter 후보를 검증하되, 실제 Prefab·World 배치·입력·Game View·Console 및 E5 이상은 후속으로 남긴다.
- 상세: [Nature Night Day2 D-468](../Architecture/PlayableLoops/PlanningSessions/nature-night-day2.inquiry.r1.md#d-468-보완--관심-분야는-관련-이데아와-실행-기회를-더-쉽게-알아차리게-한다).

## D-469 E5는 대상 WI에 적용되는 E1~E4 전체 맥락을 소비해 실제 결속한다

- 상태: `ConfirmedProcess` (2026-09-01). 사용자는 E4에서 E5로 넘어갈 때 정보 부족으로 맥락과 맞지 않는 자산 배치를 막기 위해, E5가 E1~E4의 논리·표현 근거를 모두 참조하는 전략을 명시하도록 요청했다.
- E5는 단순 Prefab 배치가 아니다. 같은 Goal·WI·Session·객체·권위 Revision에서 E1 계약, E2 규칙·투영·수명 준비, E3 결정성·조립/해제 회귀, E4의 Actor·대상·시간·H/그래프 맵·VisualKey·후보 fingerprint·fallback·InteractionAnchor를 함께 소비한다.
- 적용 가능한 근거의 참조·판본·hash와 실제 소비 여부를 작업 명세·EvidencePackage에 남긴다. 누락·만료·충돌은 임의 기본값·보기 좋은 대체 자산·좌표 보정으로 덮지 않고, 원인이 있는 가장 이른 E1~E4를 다시 연다.
- 전체 월드 완성은 개별 E5의 선행 조건이 아니다. 대상 WI와 실제로 영향을 주고받는 노드·엣지·접근/복귀·배치 규칙·카메라/판독 제약만 완전하게 소비한다. 부분 WI 성공을 전체 폐루프 E5로 확대하지 않는다.
- 이 결정은 현재 Nature의 E4 상한을 바꾸지 않으며 실제 Prefab/Scene·입력·Game View·Console·E5 실행을 자동 승인하지 않는다. 상세 기준은 [논리·표현 이중순환의 E5 맥락 소비 관문](../Architecture/플레이폐루프논리시각이중순환체계.md#e5는-e1e4-맥락을-소비하는-실제-결속-관문이다)과 [Nature D-469](../Architecture/PlayableLoops/PlanningSessions/nature-night-day2.inquiry.r1.md#d-469-보완--후속-e5는-e1e4-전체-맥락을-소비해-배치한다)을 따른다.

## D-470 메인 스토리는 지금·여기·나·너·이렇게 하위 기획과 WI를 의미로 결속한다

- 상태: `AcceptedPlanningDirection / ReadyForReview` (2026-09-01). 사용자는 게임의 메인 스토리를 문서화하고 스토리에서 플레이 기능에 의미를 부여하며, 지금·여기·나·너·이렇게 문서를 하위 참조로 함께 볼 수 있게 하기를 요청했다.
- 메인 스토리는 `장 → 장면 → 사건 마디 → 하위 기획/PlayableLoop/WI/표현 근거`로 연결한다. 스토리 문장·표식·대사만으로 권위 상태, 퀘스트 수락·완료, 회복이나 E 성숙도를 변경하지 않는다.
- 첫 기준선은 개인의 알아차림과 회복이 타인·공동체의 흐름으로 이어지고, 관심이 생긴 현실 자료를 선택적으로 거울처럼 살펴보는 의미를 사용한다. 정확 장 수·순서·첫 메인 퀘스트·결말은 `ReadyForReview`이며 승인 완료가 아니다.
- 상세 기준은 [메인 스토리와 플레이 기능 결속 체계](../Architecture/메인스토리와플레이기능결속체계.md), 첫 내용 기준선은 [메인 스토리 — Mirror](메인스토리-거울의흐름-기획-2026-09-01.md)를 따른다. 이번 결정은 코드·DB·Scene·Play·Game View·E·commit/push를 승인하지 않는다.

## D-471 흑막상인 원작을 메인 스토리 기준으로 삼고 원문 확인 전 각색을 확정하지 않는다

- 상태: `ConfirmedDirection / SourcePending` (2026-09-01). 사용자는 ChatGPT 개인 프로젝트 `흑막상인`의 스토리를 이 게임의 메인 스토리 기준으로 참조해 개발하기를 원했다.
- 현재 저장소에는 원작 본문이 없고 확인한 브라우저 세션도 로그인되지 않아 원문을 읽지 못했다. 따라서 현재 `Mirror` 메인 스토리 기준선은 원작 대체물이 아니라 PlayableLoop·WI와 연결하기 위한 임시 골격으로 유지한다. 현행 Markdown의 작품 표시명은 `Mirror`로 통일한다.
- 원작을 읽은 뒤 인물·세계 규칙·갈등·사건·결말을 추출하고 `유지/각색/보류/제외`로 판정한 다음, 채택한 사건만 `지금·여기·나·너·이렇게 → 결과 → 다음 선택`과 PlayableLoop/WI에 결속한다.
- 원작 확인 전에는 플레이어가 흑막상인인지, 기존 흑막상인과 어떤 관계인지, 정확 장 순서·적대자·결말을 확정하지 않는다. 현재 추천인 `기존 흑막상인의 흔적을 추적하며 협력자·경쟁자·감시자 관계를 선택`하는 구조도 질문 상태로 유지한다.
- Nature의 자원 보관→수면→Day2→자원 재생→다음 선택은 원작을 침범하지 않는 독립 서장 후보로 문서화할 수 있지만, 원작의 정사 첫 사건이나 E 성숙도 증거로 자동 승격하지 않는다.
- 이번 결정은 원작 프로젝트 수정·메시지 전송, 새 WI/퀘스트 등록, 코드·DB·Scene·Play·Game View·E·commit/push를 승인하지 않는다.

## D-472 기획 스레드는 개발 목표를 소유하거나 중간 완료를 기다리지 않는다

- 상태: `AcceptedOperatingModel` (2026-09-01). 사용자는 기획 스레드에 개발 관련 Goal을 두지 않고, 개발 목표는 개발 작업 스레드가 소유하며 여기서는 기획을 중심으로 계속 진행하기를 요청했다.
- 기획 스레드는 `지금·여기·나·너·이렇게 → 결과 → 다음 선택`, 이야기 의미, 플레이어 선택·대가·실패·회복·귀환, 기존 WI 재사용/신규 후보 판단, 전문 연구 필요성, 승인 판본과 개발 인계를 소유한다.
- 개발 스레드는 승인된 기획·작업 명세를 읽고 코드·시험·Save/Replay·Local/Hosted·Unity·실제 입력·Game View·Console·결과 재조회와 통합 검증을 소유한다. 개발 중간 상태를 기획이 반복 조회하거나 기다리지 않는다.
- 개발은 완결된 검증 묶음 또는 기획 선택 없이는 진행할 수 없는 실제 차단이 있을 때만 기획에 반환한다. 단순 착수, 부분 시험, 대기 상태는 기획 문답을 멈추는 반환 조건이 아니다.
- 기획 문답은 개발 완료와 독립적으로 이어가되, 개발 결과가 돌아오면 완료·미완료·검증·실제 Unity·commit·push를 분리해 기존 기획 가정과 대조한다. 기획 문서가 개발 성공이나 E 승격을 대신하지 않고, 개발 결과도 미승인 플레이 선택을 조용히 확정하지 않는다.

## D-473 상위 E는 같은 후보의 모든 하위 E를 누적 소비해 성립한다

- 상태: `ConfirmedEvidencePrinciple` (2026-09-01). 사용자는 E2가 E1을, E3가 E1~E2를, 이후 상위 E도 모든 하위 E를 총동원해 성립하도록 요청했다.
- E2 이상은 현재 단계의 새 산출물만으로 승격하지 않는다. 같은 Goal·WI·궤적·후보 revision/hash에서 모든 선행 E의 계약·구현·검증·문맥·실제 결속을 참조하고, 현재 경로가 이를 실제로 소비하며 서로 충돌하지 않는지 확인한다.
- 유효하고 동결된 선행 증거는 재사용할 수 있으므로 매번 모든 시험을 처음부터 반복하지 않는다. 하지만 계약·코드·자산·배치·입력·실행 환경 변화로 선행 근거가 무효화되면 가장 이른 영향 E부터 목표 단계까지 다시 닫는다.
- E8은 E1~E7이 포함된 한 PlayableUnit의 동결 묶음, E9는 각 구성원의 E1~E8 묶음, E10은 승인된 E1~E9 묶음을 소비한다. 상위 화면·사람 평가·운영 관찰은 하위 결손을 평균이나 다수결로 상쇄하지 않는다.
- 단일 기준은 [E 증거 누적 소비 원칙](../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md#evidence-cumulative-consumption)이다. 이 결정은 기존 E를 소급 승격하거나 개발 실행·Scene·commit/push를 승인하지 않는다.

## D-474 요동성 방어를 메인 스토리의 첫 장기 목표로 둔다

- 분야별 ID: `D-STORY-YODONG-001`
- 상태: `ConfirmedStoryDirection / ReadyForReview` (2026-09-01). 사용자는 Nature·City·Town·Construction 등 게임의 여러 요소를 이야기상 `요동성` 하나를 방어하는 첫 목표로 묶어 플레이하기를 원했다.
- 요동성 방어는 모든 Area를 순서대로 완료하는 선형 관문이 아니다. Nature·Farm·Town·Hub·City는 독립 폐루프를 유지하고 정찰·자원·식량·공방·인력·물류·협상 중 플레이어가 명시적으로 선택한 기여만 같은 방어 계획에 연결한다.
- `Construction`은 독립 AreaSet이 아니라 성벽·성문·망루·창고·병영의 계획·건설·운영·손상·수리 상태를 표현·조립하는 공유 계층으로 유지한다.
- 장기 목표는 초반부터 알려도 즉시 실패하는 상시 countdown으로 압박하지 않는다. 징후 발견→취약점 이해→자유로운 준비→방어 계획 확정→첫 방어→피해 복구·다음 장의 순서를 사용한다.
- 이는 D-038의 첫 필수 구현·검증 playable을 공성전으로 만들지 않는 원칙을 대체하지 않는다. 요동성은 이야기의 첫 장기 목표이고, 경제·생활·생존의 작은 독립 폐루프는 구현과 실제 플레이에서 먼저 성립할 수 있다.
- 첫 위협의 정체, 성의 통치자·규모·위치, 흑막상인의 역할은 미정이다. 원작 확인 전에는 흑막상인을 공격자나 후원자로 확정하지 않는다.
- 상세는 [요동성 방어 기획 r1](요동성방어-메인스토리기획-2026-09-01.md)을 따른다. 이번 결정은 새 WI·AreaSet·코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-475 요동성 첫 장의 위협·인과·결과·복구 기준선을 닫는다

- 분야별 ID: `D-STORY-YODONG-002`
- 상태: `ApprovedPlanningBaseline / SourceCanonPending` (2026-09-01). 사용자는 D-474의 요동성 방향을 어느 정도 마무리해 두기를 요청했다. [요동성 방어 기획 r2](요동성방어-메인스토리기획-2026-09-01.md)가 D-474의 `ReadyForReview` 범위를 대체한다.
- 첫 표면 위협은 Nature에서 성 방향으로 밀려오는 몬스터·야생 생물의 비정상 이동이다. 생태 변화·서식지 교란·누군가의 의도 중 최종 원인이 무엇인지는 첫 장에서 확정하지 않고 첫 방어 뒤 추적한다.
- 첫 장은 성과 경계 발견→굶주림·교역 지연·순찰 약화 같은 생활 흔들림→비정상 이동 관측→취약점과 대안 이해→같은 판본의 방어 계획 Preview·Confirm→첫 방어와 복구로 닫는다. 일반 활동은 관련 관계·대안·실행 가능 조건을 만들 때 메인 목표를 열며 단순 완료 개수 grind로 사용하지 않는다.
- 방어 계획은 위협 관측, 선택 기여, 시설 상태, 보급·정찰·피난·후퇴 경로, 대가, Preview·Confirm, 방어 결과와 행위 기록·복구 후보를 같은 revision에서 연결한다. 준비 후보를 자동 징발·자동 투입하지 않는다.
- 결과는 완전 성공·부분 성공·전술 후퇴·Confirm 전 준비 중단을 허용하고 모두 보존된 상태와 다음 회복 선택을 남긴다. 영구 성 소멸·주민 전멸·장시간 진행 삭제를 기본 실패로 사용하지 않는다.
- 성의 정확 위치·규모·건축 양식·인물 이름과 흑막상인의 역할은 원작·월드맵 결속 뒤 정한다. 이는 첫 장기 목표의 후속 기획을 막는 미정이 아니며, 새 WI·코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-476 요동성 방어의 기본은 내정·전투 혼합 대응이다

- 분야별 ID: `D-GAMEPLAY-YODONG-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 사용자는 내정 중심과 전투 중심 해결을 모두 허용하되 혼합 대응을 기본으로 두고, 높은 난이도에서는 두 축을 모두 잘해야 방어에 성공하도록 요청했다.
- 세계의 실제 방어는 내정과 전투를 항상 함께 판정한다. 플레이어는 내정 주도·전투 주도·혼합 주도 중 참여 방식을 고를 수 있으며, 직접 맡지 않은 축은 준비된 NPC·시설·비축·위임 정책이 수행한다. 위임은 무료 자동 보정이 아니고 실제 상태·대가·피해를 남긴다.
- 완화 난이도에서는 한 축 직접 주도로도 성공할 수 있고, 표준 난이도에서는 혼합 대응을 기본으로 다른 축의 최소 실행 준비가 필요하다. 고난도에서는 내정과 전투가 각각 독립 최소 조건을 충족하고 두 축의 인계까지 이어져야 완전 성공한다.
- 강한 한 축의 점수로 존재하지 않는 다른 축을 단순 상쇄하지 않는다. 성 유지·주민 안전·보급 지속·위협 억제·복구 부담을 구분하고, 부족한 축은 부분 성공·전술 후퇴·더 큰 복구 부담으로 표현한다.
- 난이도는 적 체력만이 아니라 정보 불확실성·준비 시간·자원 여유·NPC 위임 역량·경로 차단·부상/시설 피해·복구 부담을 함께 조절한다. 정확 수치·명칭·UI와 WI 결속은 후속 밸런스·기획 대상이며, 이번 결정은 코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-477 플레이 유형을 먼저 고르지 않고 실제 활동 기록이 요동성 방비를 바꾼다

- 분야별 ID: `D-GAMEPLAY-YODONG-002`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 사용자는 내정형·전투형 같은 시스템 선택 대신 물약 제조나 전투처럼 원하는 플레이를 자연스럽게 이어가고, 실제로 한 일의 기록이 요동성의 방비를 유동적으로 바꾸기를 원했다. D-476의 세계 차원 혼합 대응·난이도 원칙은 유지하되, 플레이어가 세 참여 유형 중 하나를 먼저 고른다는 표현은 이 결정으로 대체한다.
- 방어 관련 영향은 권위 있게 완료된 행위 기록에서 파생한다. 행위 종류·주체·대상·Area/경로·수량/품질·시각·원인 명령·revision을 보존하고, 화면·애니메이션·메뉴 방문·후보 상태만으로 기여를 만들지 않는다.
- 물약 제조·시장 공급은 지역 의약품 가용성과 조달 조건을 개선한다. 요동성 물약 비축은 실제 구매·계약·기부·운송이 이어졌을 때 증가하며 시장 판매만으로 플레이어 재고를 자동 징발하지 않는다. 농사·수리·물류·돌봄도 같은 방식으로 실제 공급·시설·인력 결과를 통해 내정 방비에 영향을 준다.
- 정찰·사냥·전투는 같은 시기·경로·위협 종류와 관련된 압력·전조·안전 시간에 영향을 준다. 아무 지역의 몬스터 격퇴를 요동성 공격 병력의 전역 차감으로 사용하지 않는다.
- 방어 직전에는 플레이어 유형을 묻지 않고 어떤 기록이 의약 공급·보급·시설·인력·접근로 위협을 어떻게 바꿨는지 근거와 함께 보여 준다. 플레이어는 그 누적 상태에서 추가 준비 또는 직접 전투·현장 지원·지휘·후방 지원을 선택한다.
- 반복 행위의 유효 기간·수요·저장 용량·시장 흡수·위협 재유입과 정확 수치는 후속 규칙 판본에서 정한다. 이번 결정은 새 직업 잠금·자동 징발·코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-478 첫 방어의 군내 역할은 평소 키운 역량에서 주로 정한다

- 분야별 ID: `D-GAMEPLAY-YODONG-003`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 사용자는 비전투 현장 참여도 직접 참여로 인정하되, 첫 방어에서 맡는 군내 역할은 평소 키운 역량에 따라 주로 정해지기를 원했다.
- 물약·약초·치료는 전장 의무, 농사·조리·보관은 배급, 제작·수리·건설은 공병, 운송·창고·거래는 병참, 정찰·사냥은 척후, 전투는 전선, NPC 관계·훈련·계획은 연락·현장 조율의 주 역할 후보로 연결한다. 여러 역량이 있으면 주·보조 역할을 함께 제시한다.
- 역할은 새 직업 선택이나 영구 병과가 아니다. 같은 판본의 완료 기록·숙련·최근 수행·장비·관계·자격과 현재 방어 필요를 읽고, 추천 근거·위험·대체 역할을 Preview에서 보여 준다. 플레이어는 추천을 수용하거나 실행 가능한 대체 역할로 조정할 수 있다.
- 현재 방어의 긴급 필요가 강해도 플레이어를 무조건 재배치하지 않는다. 추가 준비·보조 역할로 제안하고, 전투 중 역할 전환은 실제 장비·접근·인계 조건이 성립할 때만 허용한다.
- 전투 숙련이 낮다는 이유로 의무·공병·병참·대피·연락 역할을 보조적 가짜 참여로 취급하지 않는다. 정확 점수·동률·UI·WI·보상은 후속 기획 대상이며 이번 결정은 코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-479 추천 역할과 다른 행동의 한계는 실제 능력 데이터와 결과로 드러낸다

- 분야별 ID: `D-GAMEPLAY-YODONG-004`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 사용자는 추천 역할과 다른 참여를 임의로 금지하기보다 능력치에 따른 빗나감·무피해 같은 실제 결과를 경험해 자연스럽게 한계를 판단하기를 원했다.
- 행동 판정은 플레이어 능력·숙련·훈련·피로/부상/집중, 장비·도구·품질/내구도, 대상 이동·방어·저항·약점, 거리·각도·시야·지형/날씨, 동료 지원·보급과 행동별 정확도·위력·소모·중단 조건을 같은 판본에서 소비한다.
- 물약 제조 역량이 높은 플레이어도 전선 공격을 시도할 수 있지만 전투 숙련·장비가 부족하면 빗나감, 명중 후 관통 실패와 무피해, 빠른 기력 소모·피격 위험이 발생할 수 있다. 반대로 전투 숙련자도 지식·도구·약품 없이 전문 치료를 자동 성공하지 않는다.
- 피드백은 빗나감, 명중 후 무피해, 행동 중단, 낮은 효과를 구분하고 핵심 원인과 거리 조정·훈련·장비/약점·회복·지원 같은 개선 후보를 보여 준다. 설명 없는 `0`이나 숨은 역할 부적합으로 처리하지 않는다.
- 무기 미장착·사거리 밖·필요 도구 없음·접근 불가처럼 물리적 실행 조건이 없으면 차단할 수 있지만 추천 역할과 다르다는 이유만으로 막지 않는다. 정확 수치·UI·WI·보상과 밸런스는 후속 기획 대상이며 이번 결정은 코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-480 몬스터 이름 색으로 현재 조건의 상대 위험을 보여 준다

- 분야별 ID: `D-PRESENTATION-COMBAT-RISK-001`
- 상태: `ConfirmedPresentationDirection / PlanningBaseline` (2026-09-01). 사용자는 상세 확률표보다 몬스터 이름의 흰색·노란색·빨간색·짙은 빨간색·검은색 스펙트럼으로 위험을 알아서 판단할 수 있기를 원했다.
- 흰색은 적정, 노란색은 경계, 빨간색은 위험, 짙은 빨간색은 치명적 위험, 검은색은 현재 조건에서 의미 있는 피해·안전한 교전을 기대하기 어려운 압도적 위험을 뜻한다. 색은 교전 금지나 승패 보장이 아니다.
- 위험은 고정 레벨·희귀도·보스·퀘스트 중요도가 아니라 현재 플레이어 능력·장비·상태·지원과 관측된 몬스터 방어·저항·이동·환경의 비교 결과다. 조건이 바뀌면 재평가하고, 정보가 부족하면 임의 색 대신 `위험 미확인`을 표시한다.
- 색만으로 의미를 전달하지 않는다. `적정/경계/위험/치명적/압도적`의 짧은 표식 또는 단계형 외곽 표시를 병행하고 검은 이름은 밝은 외곽선으로 가독성을 유지한다. 보스·희귀·퀘스트 표식은 별도 아이콘으로 둔다.
- 기본 화면은 이름 색과 짧은 표식만, 상세 화면은 명중 불리·관통 부족·피격 위험·정보 부족 같은 주요 원인만 보여 준다. 정확 산식·전술/광역 기준 주체·UI 자산과 실제 Game View는 후속 기획·검증 대상이며 이번 결정은 코드·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-481 개인과 분대의 상대 위험을 구분하고 실제 보급 기여를 분대에 반영한다

- 분야별 ID: `D-PRESENTATION-COMBAT-RISK-002`
- 상태: `ConfirmedPresentationDirection / PlanningBaseline` (2026-09-01). 사용자는 개인에게 검은색인 몬스터도 보급·제작으로 강해진 분대에는 주황색일 수 있게 하고 추천한 개인/분대 기준 구분을 승인했다. D-480의 스펙트럼에 노란색과 빨간색 사이 `주황색=높은 경계`를 추가한다.
- 현장 1인칭·3인칭의 이름 주 색은 현재 플레이어, 광역·전술 시점의 주 색은 선택한 분대를 기준으로 한다. 다른 기준은 작은 `개인/분대` 단계 표식으로 병행하고 상세에서 두 색과 차이 원인을 함께 보여 준다.
- 분대 위험은 단순 평균 공격력이 아니라 실제 편성, 전선·치료·공병·병참·정찰 역할, 장비·탄약·물약·식량, 사기·부상·피로, 지휘·대형, 경로·지형과 후퇴/회복 능력을 소비한다. 필수 역할 결손은 다른 높은 수치로 자동 상쇄하지 않는다.
- 플레이어의 보급·제작·치료·운송 기록은 장비와 물자가 해당 분대에 실제 도착·배정됐을 때 위험을 낮춘다. 후보·시장 재고·미도착 화물은 분대 전력으로 세지 않으며, 상세에는 위험 변화에 소비된 플레이어 행위 기록을 근거로 표시할 수 있다.
- 정확 산식·색 경계·전투 중 갱신 시점·UI 자산과 Game View는 후속 기획·검증 대상이며 이번 결정은 코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-482 전투 중 실제 상태 변화에 따라 개인·분대 위험 색을 갱신한다

- 분야별 ID: `D-PRESENTATION-COMBAT-RISK-003`
- 상태: `ConfirmedPresentationDirection / PlanningBaseline` (2026-09-01). 사용자는 전투 중 분대와 몬스터의 실제 상태에 따라 위험 색이 변하는 추천을 승인했다.
- 부상·기절·이탈/복귀, 역할 공백/복구, 탄약·물약·식량·도구·내구도 소모/보급, 대형·지휘·사기·피로, 지원 효과, 퇴로·보급로·시설, 몬스터 부상·격노·약점·증원과 날씨/지형이 위험 단계 경계를 넘으면 색을 다시 판정한다.
- 작은 수치 변화마다 깜빡이지 않는다. 일반 변화는 권위 상태가 단계 경계를 넘고 안정됐을 때 갱신하며, 핵심 역할 상실·퇴로 차단·압도적 증원처럼 생존 판단이 급한 변화는 즉시 반영한다.
- 색 변화 때 기본 화면에는 가장 큰 원인 하나를 짧게 표시하고 상세에는 함께 소비된 상태와 이전/현재 revision을 보여 준다. 위험 판정은 Unity 화면 추정이 아니라 권위 상태 사본에서 파생하며 같은 Replay는 같은 단계 변화 순서를 재현한다.
- 정확 경계·안정 시간·우선 원인 선정·UI 자산과 실제 Game View는 후속 기획·검증 대상이며 이번 결정은 코드·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-483 위험 색은 대응을 추천하지 않고 실행 가능한 전술 선택만 연다

- 분야별 ID: `D-INTERACTION-COMBAT-COMMAND-001`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 사용자는 후퇴·증원·보급이 전투 상황에 따라 모두 불가능할 수 있으므로 시스템 추천보다 실제 가능한 선택을 열고 플레이어가 결정하기를 원했다.
- 위험 색·주요 원인은 판단 정보다. 빨강·짙은 빨강·검정이라도 특정 대응을 추천하거나 자동 일시정지·대상 전환·후퇴·증원·보급을 실행하지 않는다.
- 후퇴는 퇴로·도착 지점·분대 이동/이탈 상태·명령 전달·적과 분리할 시간, 증원은 실제 예비 인력·출동 권한·경로·도착 시간, 보급은 실제 재고·할당·운반 주체·보급로·인계/도착 시간이 성립할 때만 Confirm할 수 있다.
- 선택 가능해도 성공을 보장하지 않는다. 추격·도착 지연·보급 손실은 실제 결과로 남기고, 조건이 없으면 위험 완화용 가짜 선택지를 만들지 않는다. 플레이어가 선택한 뒤에만 해당 Command를 발행한다.
- NPC 자율 정책은 전투 전에 별도로 승인된 경우에만 작동하며 위험 색 자체가 정책을 만들지 않는다. 빠른 메뉴에서 불가 선택을 숨길지 차단 사유와 함께 표시할지는 후속 문답 대상이고 이번 결정은 코드·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-484 빠른 전술 메뉴에는 현재 실행 가능한 명령만 표시한다

- 분야별 ID: `D-INTERACTION-COMBAT-COMMAND-002`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 사용자는 빠른 명령에는 실행 가능한 선택만 두고 불가능한 선택과 이유는 별도 상세에서 확인하는 추천을 수용했다.
- 빠른 메뉴는 현재 권위 판본에서 즉시 Preview 가능한 명령만 표시한다. 실행 불가능한 후퇴·증원·보급을 회색 버튼으로 나열하지 않는다.
- 상황 상세는 차단된 선택, 현재 차단 이유와 `퇴로 복구`, `예비대 확보`, `운반 경로/주체 확보` 같은 재개 사실 조건을 보여 준다. 재개 조건은 추천·자동 계획이 아니며 충족 뒤 다음 상태 사본에서 다시 판정한다.
- 빠른 메뉴 생성 뒤 상태가 바뀔 수 있으므로 Confirm 시 최신 revision으로 실행 가능성을 재검사한다. 조건이 사라졌으면 낡은 명령을 실행하지 않고 상태를 보존한 채 새 차단 사유와 현재 가능한 메뉴를 반환한다.
- 모든 새 명령이 불가능할 때 기존 명령과 분대의 최소 행동을 어떻게 유지할지는 후속 문답 대상이며 이번 결정은 코드·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-485 전투 전에 후퇴·증원·보급 선택의 실제 조건을 마련한다

- 분야별 ID: `D-GAMEPLAY-BATTLE-PREPARATION-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 사용자는 전투 중 선택 불가 원인을 확인한 뒤, 본격적인 전투 전에 보급로·후퇴 가능성·군대 지원 조건을 실제로 성립시키는 전략 플레이를 원했다.
- 후퇴는 퇴로·출구·집결지·이동 수단을, 증원은 예비대·출동 권한·연락·접근로·도착 시간을, 보급은 실제 재고·할당·운반 주체·경로·인계 지점을 전투 전에 조사·확보·배정할 수 있다. 전투 중 필요할 때 편의상 생성하지 않는다.
- 방어 계획 Preview는 성립 조건·결손·유지 취약성·시간/인력/물자 대가와 다른 준비의 기회비용을 보여 준다. 플레이어는 모든 항목을 채우는 대신 남겨 둘 전술 선택과 감수할 결손을 정해 같은 판본으로 Confirm한다.
- Confirm은 시작 사본을 동결할 뿐 성공이나 조건 유지를 보장하지 않는다. 전투 중 실제 경로·인원·재고 변화에 따라 선택 가능성을 다시 판정한다.
- 위협이 Confirm까지 기다리는지 준비 시간 경과로 불리한 전투가 시작될 수 있는지는 후속 문답 대상이며, 이번 결정은 새 WI·코드·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-486 전역 이력 번호와 분야별 결정 ID를 함께 사용한다

- 분야별 ID: `D-PLANNING-DECISION-NAMING-001`
- 상태: `AcceptedPlanningConvention` (2026-09-01). 사용자는 전역 `D-###` 번호만으로는 어떤 분야의 몇 번째 결정인지 파악하기 어려우므로 더 세밀한 이름을 원했다.
- 기존 `D-###`는 작성 순서·앵커·과거 참조를 보존한다. 새 분야별 ID는 `D-{주분야}-{세부주제}-{분야별 순번}`으로 병기하고 제목은 실제 결정 내용을 계속 자연어로 설명한다.
- 분야별 순번은 같은 주분야와 세부주제 안에서만 증가하고 재사용·재번호화를 금지한다. 한 결정은 첫 책임 분야 하나에만 집계하며 관련 분야는 본문 링크로 표현한다.
- 기획 스레드가 새 결정을 사용자에게 보고할 때는 `D-전역번호 | 주제 > 세부분야 | 세부분야 결정 N번째 | 분야별 ID | 자연어 제목` 순서를 기본으로 사용한다. 전역 번호나 분야별 ID 하나만 단독으로 말하지 않으며, 분야별 결정 수는 구현 수나 E 성숙도 수가 아님을 구분한다.
- 첫 적용 뒤 전체 결정 제목 483개를 [결정 분야별 전수 색인](generated/decision-field-index.md)에 분류했다. 11개 주분야·60개 세부주제로 모두 대응하고, `D-096` 중복 두 항목은 서로 다른 분야별 ID로 구분하며 비어 있는 `D-422~425`는 새 결정으로 소급 채우지 않는다.
- 전수 목록 중 D-474~D-486 13개는 본문까지 재검토한 `BodyReviewed`, 그 이전 470개는 제목과 주변 결정군을 대조한 `TitleReviewed`다. 따라서 전수 분류와 전수 본문 의미 감사는 구분하고, 관련 분야를 다시 기획할 때 본문 검토 깊이를 올린다. 이번 결정은 코드·DB·Scene·E 승격·commit/push를 자동 승인하지 않는다.

## D-487 결정 분야 분류와 공식 WI 대장을 양방향 관계 색인으로 연결한다

- 분야별 ID: `D-PLANNING-DECISION-WI-RELATION-001`
- 상태: `AcceptedPlanningConvention` (2026-09-01). 사용자는 기존 WI 정리와 `DECISIONS.md`의 분야별 결정 분류를 항목 단위로 연결해 구조적으로 확인하기를 원했다.
- 공식 WI 원본은 `eng/execution-ledgers/world-interactions.json` 하나를 유지한다. 결정 색인이 별도 WI 목록이나 실행 권위를 만들지 않고, 현재 원본의 모든 결정과 모든 WI를 양방향으로 조회한다.
- 자동 연결은 결정 본문에 공식 WI 고유 식별자가 직접 적혀 있거나 같은 접두사의 숫자 범위가 명시된 경우에만 `ExplicitMention`으로 만든다. 제목 유사도·분야 일치·순번 추측으로 관계를 생성하지 않는다.
- 공식 대장에 없는 `WI-*` 표기는 삭제·자동 치환하지 않고 `NonCanonicalToken`으로 분리한다. 명시 관계가 없는 결정과 WI도 전수 목록에 남겨 `NoExplicitCanonicalLink`로 확인할 수 있게 한다.
- 이 관계는 기획 탐색용 파생 색인이다. 관계 존재가 WI 승인·구현·E 성숙도·Scene 배치·실행을 뜻하지 않으며, 향후 의미 검토 관계를 추가하려면 출처와 검토 상태를 별도 판본으로 명시한다.

## D-488 승인 기획을 Graph Map 작업에 판본 인계하고 최종 결과만 기획에 반환한다

- 분야별 ID: `D-PLANNING-GRAPH-MAP-HANDOFF-001`
- 상태: `AcceptedPlanningConvention` (2026-09-01). 사용자는 기획 스레드는 기획에 집중하고, 승인된 기획을 Graph Map 작업에 넘겨 레벨 1·2·3으로 계속 구현·검증하는 순환을 원했다.
- [Graph Map 기획 인계 순환 체계](../Architecture/GraphMap기획인계순환체계.md)를 공식 절차로 삼는다. 기획 스레드는 승인 판본·SHA-256, 결정/WI, 지금·여기·나·너·이렇게→결과→다음 선택, Graph Map 영향과 제외 범위를 인계하고 Graph Map 원본·생성물·검사 도구를 소유하지 않는다.
- Graph Map 담당은 기존 안정 ID와 현재 판본을 먼저 대조한 뒤 레벨 1 플레이 관계, 레벨 2 배치 제약, 레벨 3 코드·컴포넌트 결속과 필요한 federation·overlay만 갱신한다. 누락된 플레이 선택·대가·실패·회복·귀환을 임의로 결정하지 않는다.
- 기획 스레드는 중간 진행을 기다리지 않고 다른 기획을 이어간다. Graph Map 담당은 `Integrated`, `Blocked`, `NoImpact` 가운데 하나의 최종 결과와 반영·미반영·차단·검증·증거 상한·결과 판본을 반환한다. 플레이어 선택이 필요한 차단만 기획의 다음 단일 질문으로 돌린다.
- 파일 기반 인계 원장은 협업·신선도·반환 상태만 소유한다. Graph Map 반영은 Unity Scene·Prefab 적용·실제 입력·Game View·E 승격을 뜻하지 않으며, 별도 승인 없이 API·DB·새 상태 권위·자동 실행 대기열을 만들지 않는다.

## D-489 Graph Map의 작은 구현 후보를 기존 Goal·WI·작업 명세에 결속해 개발로 인계한다

- 분야별 ID: `D-PLANNING-GRAPH-MAP-DEVELOPMENT-HANDOFF-001`
- 상태: `AcceptedPlanningConvention` (2026-09-01). 사용자는 Graph Map과 기획 노드를 개발 담당이 함께 읽고 실제 구현을 맡도록, Graph Map→개발의 인계 과정도 공식화하기를 원했다.
- [Graph Map 개발 인계 체계](../Architecture/GraphMap개발인계체계.md)를 공식 절차로 삼는다. Graph Map 담당은 전체 맵이 아니라 한 PlayableLoop·대표 WI·정확한 노드/엣지/제약/레벨 3 코드 결속의 작은 slice를 고르고, 기존 Goal·`Approved` 기획 관문·E7 작업 명세·work item 재사용 후보와 판본을 결속해 `ReadyForDevelopment`로 넘긴다.
- `ReadyForDevelopment`는 개발 수용·작업 활성화·코드 작성이 아니다. 개발은 현재 Goal·작업 명세·코드·파일 소유·공유 계약·검증 상한을 다시 확인하고 기존 work item 재사용 또는 비중첩 하위 작업 필요를 결정한 뒤 `AcceptedByDevelopment`, `Blocked` 또는 `Deferred`를 반환한다.
- 개발 결과는 소비한 Graph Map 요소, 정확한 변경 경로, 코드·시험·Runtime·Game View·Save/Replay·서버 연결, 남은 차단과 기획 질문을 분리한다. 최종 결과는 Graph Map 상태와 다음 기획 질문에 환류하되, 기술 결함을 새 플레이 선택으로 바꾸거나 기획 결손을 임시 코드로 채우지 않는다.
- 첫 사례는 북부 생활권 Graph Map r3의 Farm 생산→작업마당 slice다. 기존 `WI-FARM-01`과 `work:farm-crop-cycle:landscape-binding-guard`를 재사용 후보로만 연결하며, 이 결정만으로 Scene 저장·Unity 실행·E 승격·commit/push를 승인하지 않는다.

## D-490 플레이어는 병약한 소가주의 몸에 빙의한 SSS급 연금술사다

- 분야별 ID: `D-STORY-PROTAGONIST-001`
- 상태: `ApprovedPlanningBaseline / SourceCanonPending` (2026-09-01). 플레이어는 이전 세계에서 SSS급 연금술 지식과 재능을 지닌 채 병약한 요동성 소가주의 몸에 빙의한다. 현 가주는 초반에 살아 있어 플레이어는 성주가 아니며 제한된 영지·가신·자원만 운영한다.
- 소가주 지위는 자유로운 탐험·연구·외교의 명분과 제한된 명령권을 함께 준다. 직함만으로 모든 NPC·재고·시설·Hub의 권한을 자동 획득하지 않는다.
- SSS급은 연금술 지식·배합·정제 재능이며 자원·설비·육체·숙련 제한을 삭제하지 않는다. 병약한 육체는 점진적 회복과 위임의 필요를 만든다.
- 세부 기준은 [소가주·연금술·가주 승계 기획 r1](소가주-연금술-가주승계-메인스토리기획-2026-09-01.md)을 따른다. 흑막상인 원작과의 정확 인물·시대·지리 결속은 미정이며 이 결정은 새 WI·코드·Scene·E 승격을 자동 승인하지 않는다.

## D-491 플레이어는 핵심 연금술을 직접 하고 일상 운영은 NPC에게 위임한다

- 분야별 ID: `D-GAMEPLAY-ALCHEMY-ECONOMY-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 소가주는 공방의 납품 의뢰를 받는 일꾼으로 시작하지 않는다. 연구 방향·핵심 배합·원형 물약·고난도 정제는 플레이어가 직접 하고, 검증된 제조·채집·재배·운송·창고·판매는 적합한 NPC에게 위임한다.
- 소가주가 고급 물약 생산 체계를 세우고 성 소속 상단이 실제 판매·운송·정산을 맡는다. 판매·비축·민간 구호·다른 성 지원의 배분은 자금·의약 재고·민심·외교에 다른 결과를 남긴다.
- 위임은 NPC의 역량·인원·도구·시간·경로·재고·권한을 소비하며 무료 자동 완료가 아니다. 시장 재고나 판매 화면만으로 요동성 비축을 증가시키지 않는다.

## D-492 현 가주의 전사는 원래 역사지만 플레이어의 축적 준비로 바꿀 수 있다

- 분야별 ID: `D-STORY-YODONG-SUCCESSION-001`
- 상태: `ApprovedPlanningBaseline / SourceCanonPending` (2026-09-01). 현 가주는 초반에 살아 있고, 북방 전선에서 죽는 것이 원래 역사의 흐름이다. 그러나 플레이어가 평소에 축적한 연금술·상단·보급·후송·정찰·외교·전투 조건과 마지막 지원 판단에 따라 완전 생환·부상 생환·전사로 결과가 바뀐다.
- 생환하면 현 가주의 통치 아래 후계자 성장이 이어지고, 부상 생환하면 소가주가 실질 통치를 맡을 수 있으며, 전사하면 조기 승계·계승 분쟁·요동성 방어 위기가 함께 열린다.
- 지원할 수 있었지만 플레이어가 보류·비개입한 결과도 허용하며, 그 이유와 기록이 가문·가족·가신·다른 성의 관계에 남는다. 생환과 전사 중 어느 것도 선·악 정답이나 즉시 게임 오버가 아니다.
- 가주의 정확한 전선 사건·적 세력·사망 원인은 흑막상인 원작 확인 뒤 결속한다. 이 결정만으로 승계 WI·판정식·컷신·Scene을 자동 생성하지 않는다.

## D-493 요동성은 정확한 countdown 대신 징후와 주기적 위기로 최전방의 긴장을 만든다

- 분야별 ID: `D-GAMEPLAY-YODONG-CRISIS-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 가주의 위기나 다음 대형 전투의 정확한 날짜·남은 시간을 상시 표시하지 않는다. 부상병·피난민, 운송 지연·가격 변화, 정찰 두절, 보급 요청, 시설·경계·몬스터·날씨·봉화·NPC 대사 변화로 위험이 다가옴을 알린다.
- 요동성은 최전방이므로 `전조 → 압박 → 대응 → 결전 → 복구`의 작은 위기를 주기적으로 겪는다. NPC가 실제 역량과 자원으로 통상 범위를 수습하고, 플레이어에게는 중요 판단·직접 개입·자원 배분·예외 위임이 필요한 부분을 보고한다.
- 작은 위기를 외면했다고 즉시 게임 오버로 처리하지 않는다. NPC가 수습하지 못한 잔여 문제는 후속 사건·퀘스트·비축 감소·신뢰 변화로 남고, 가주의 운명적 위기가 그 축적을 종합한다.

## D-494 겨울 전쟁의 난방·방한·의료·보급 준비가 전투 인력을 보호한다

- 분야별 ID: `D-GAMEPLAY-WINTER-LOGISTICS-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 겨울 전쟁 전에 준비한 연료·막사 난방·방한복·회복/해열/동상 물약·온실·약초 보관·창고·보급로·상단 사전 계약·보조 난방진은 병사의 휴식·수면·경계·이동·후송·회복에 실제로 영향한다.
- 준비된 물자는 `생산/구매 → 비축 → 배정 → 운반 → 현장 인계 → 휴식/치료`의 같은 판본 계보를 거쳤을 때만 동상·질병·피로·비전투 손실을 줄인다. 창고 수치·막사 외형·물약 애니메이션만으로 효과를 확정하지 않는다.
- 물자가 부족할 때 전선 병사·성내 주민·상단 운송대 사이의 배분 우선순위와 NPC가 지키는 최소 생존선은 `Asked`로 남긴다. 이 결정은 정확 수치·배분 정책·게임 코드·Scene·E 승격을 자동 승인하지 않는다.

## D-495 새 게임에서 모험가와 소가주 중 빙의 대상을 고르고 다른 인물은 주요 NPC로 남긴다

- 분야별 ID: `D-STORY-DUAL-PROTAGONIST-001`
- 상태: `ApprovedPlanningBaseline / SourceCanonPending` (2026-09-01). 플레이어는 하나의 인격체가 현장/운영 자세를 전환하는 것이 아니라, 새 게임에서 모험가와 병약한 요동성 소가주 중 한 인격체를 빙의 대상으로 선택한다.
- SSS급 연금술 지식은 빙의한 플레이어에게 따라가지만 선택한 몸의 건강·신분·자원·권한·정치적 대가는 다르다. 모험가는 현장 자유·탐험·전투를, 소가주는 NPC 위임·상단·외교·영지 운영을 초기 강점으로 가진다.
- 선택하지 않은 인물은 삭제·빈 자리·플레이어 분신이 아니라 원래 인격·지식·역량·욕구·관계를 가진 주요 NPC로 존속한다. 빙의된 플레이어의 SSS급 지식을 자동 공유하지 않는다.
- 모험가 경로에서 소가주 NPC는 가문·승계·전선 사건의 중심이고, 소가주 경로에서 모험가 NPC는 정찰·탐험·위험 지역의 협력자·경쟁자 후보다. 같은 가주 위기·요동성 방어를 공유하되 정보·권한·대가·해결 방법이 다른다.
- 두 인물의 정확한 원래 성격·이름·첫 만남·기본 관계는 후속 단일 문답과 흑막상인 원작 결속 대상이다. 이 결정은 두 제어 가능 캐릭터의 동시 조작·중간 교체·새 Save schema·코드·Scene·E 승격을 자동 승인하지 않는다.

## D-496 모험가와 소가주는 불신 속 제한 협력으로 시작하고 실제 기록에 따라 관계가 달라진다

- 분야별 ID: `D-STORY-DUAL-PROTAGONIST-002`
- 상태: `ApprovedPlanningBaseline / SourceCanonPending` (2026-09-01). 빙의 대상으로 선택되지 않은 인물은 처음부터 충성스러운 동료가 아니다. 두 인물은 서로의 정체·지식·의도를 충분히 믿지 못한 채 공동 사건에 필요한 정보·경로·자원만 제한적으로 인계한다.
- 관계는 단일 호감도나 대화 선택 한 번으로 결정하지 않는다. 약속 준수, 정보 공개·은폐, 구조·비개입, 자원 배분, 정치적 책임의 실제 기록에 따라 동료·경쟁자·정치적 적수로 발전한다.
- 초기 불신은 협력을 막는 영구 잠금이 아니다. 공동 위협에서 필요한 최소 협력은 열어 두되, 상대가 알지 못하는 SSS급 지식·가문 정보·탐험 정보를 자동 공유하지 않는다.
- 정확한 첫 만남·갈등 원인·원작 인물 관계는 흑막상인 원문 결속 뒤 확정한다. 이 결정은 관계 수치·동료 AI·대사·퀘스트·Scene을 자동 승인하지 않는다.

## D-497 영지 인구에서 자원 수요를 파생하고 부족분은 생산·교역·Hub 조달로 충족한다

- 분야별 ID: `D-GAMEPLAY-SETTLEMENT-SUPPLY-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 영지의 식량·식수·연료·주거·의약·의류·도구·위생·교통·방위 수요는 총인구만이 아니라 가구·연령·역할·건강·계절·날씨·위기·재고·시설 용량·이동 인구·피난민·병력에서 파생한다.
- 현재 재고·확정/예정 생산·운송 중 물량과 예상 수요·최소 안전 재고·예비 목표를 대조하고, 부족분을 영지 내 생산·절약·대체재·다른 성과의 교역·Hub 구매 후보로 나눈다. 실제 조달은 권한·가격·운송·저장·납기·위험을 포함한 Preview와 Confirm 뒤 입고·배정·소비 결과를 다시 조회한다.
- `Hub`는 H3·H4와 같은 공간 포함 단계가 아니라 입고·검수·보관·배분·출고·운송 연결의 기능 역할이다. 독립 Hub Area도 가능하고, 성 H4 내부의 성문·검수소·창고·적재장·상단 지점·도로가 정확히 연결되어 성내 Hub 역할을 할 수도 있다.
- 승인 범위 안의 일상 재고 보충은 NPC가 맡을 수 있지만 새 교역처·대규모 지출·위험 경로·전시 배급·외교 대가는 플레이어 판단을 요구한다. 수요 예측은 자동 구매·자동 계약·자동 징발의 승인이 아니며 정확 비축일수·조달 상한·첫 성내 Hub 구성은 후속 기획 대상이다.

## D-498 자원별 평시 비축일수에 계절·위기 보정치를 적용한다

- 분야별 ID: `D-GAMEPLAY-SETTLEMENT-SUPPLY-002`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 영지의 목표 재고는 하나의 고정 수량이 아니라 자원 분류별 평시 기준 비축일수에 현재 절기·지역 기후·생산 주기·운송 계절성의 계절 보정과, 관찰된 위기 단계·피난민·병력·경로 차단·납기 변동의 위기 보정을 적용해 계산한다.
- 보정 뒤에는 저장 용량·유통기한·예산·조달 상한을 적용하고 현재 재고와 운송 중 물량을 뺀다. 수요 증가와 안전 재고 증가를 별도로 계산해 같은 위험을 중복 반영하지 않는다.
- 플레이어는 기준 수요·계절 영향·위기 영향·상한·부족 원인을 확인할 수 있다. 위기 보정은 공개된 징후와 권위 상태 사본에 근거하며 숨은 미래 사건을 미리 아는 기능이 아니다.
- NPC 자동 보충은 기존 거래처·승인 예산·허용 경로·품목별 상한 안에서만 가능하다. 정확 자원별 기준일수·보정 폭·자동 조달 한도는 후속 기획이며, 이 결정은 자동 징발·무제한 구매·새 계약·DB schema·코드·E 승격을 승인하지 않는다.

## D-499 부족 물자는 모든 집단의 최소 생존선을 보호한 뒤 긴급도에 따라 배분한다

- 분야별 ID: `D-GAMEPLAY-SETTLEMENT-SUPPLY-003`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 영지의 실제 가용 재고와 도착 예정 물량이 전체 수요보다 적으면 주민·전선 병사·환자·운송대 등 각 집단의 식수·기초 열량·필수 난방·응급 의약 최소 생존선을 먼저 계산하고 가능한 범위에서 보호한다.
- 최소 생존선을 확보한 뒤 남은 물자는 생명 위험, 손실까지 남은 시간, 대체재·대체 경로의 존재, 나중에 회복 가능한 손실인지에 따른 현재 긴급도로 배분한다. 신분·소속·전투/민간 구분만으로 고정 우선순위를 만들지 않는다.
- 모든 최소 생존선을 충족할 수 없는 극단 상황은 부족과 예상 손실을 숨기지 않고 플레이어의 예외 배분·대피·추가 교역·전술 후퇴 판단으로 올린다. 실제 효과는 예약·운반·현장 인계가 같은 판본 계보로 확인되어야 한다.
- 정확 집단별 하한·긴급도 점수·NPC 예외 권한은 후속 기획이다. 이 결정은 재고 숫자만으로 현장 공급을 확정하거나 자동 징발·숨은 자동 희생·무제한 조달·코드·E 승격을 승인하지 않는다.

## D-500 모든 최소 생존선을 지킬 수 없으면 보호 우선순위와 예상 손실을 공개해 예외 배분한다

- 분야별 ID: `D-GAMEPLAY-SETTLEMENT-SUPPLY-004`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 모든 집단의 최소 생존선을 보호하는 것을 기본 목표로 유지하지만 실제 물자·시간·수송·대피 조건으로 불가능한 극단 상황에서는 보호 우선순위와 예상 손실을 가진 예외 배분 계획을 허용한다.
- 각 후보는 보호 인원, 예상 사망·부상·이탈, 공동체 기능 손실, 회복 가능성, 필요한 물자·시간·경로와 정보 불확실성을 함께 보여 준다. 신분·재산·국적·호감도만으로 희생 대상을 자동 결정하지 않는다.
- NPC는 사전 승인된 응급 정책 안에서 즉시 사망을 줄이는 임시 배분만 수행할 수 있다. 특정 집단의 지속적 포기·대규모 희생·강제 징발은 플레이어의 명시 판단과 실제 실행 기록을 요구한다.
- 선택·비선택·지연의 이유와 결과는 생존자 관계·민심·생산·방어·승계 정치에 남고, 선택 뒤에도 대피·추가 조달·교역·후퇴·구조로 손실을 줄일 수 있다. 정확 첫 우선 원칙과 무응답 시 NPC 응급 범위는 후속 기획이며 코드·DB·Scene·E 승격은 자동 승인하지 않는다.

## D-501 전원 보호를 우선하고 불가능할 때 생명 수 최대화 뒤 필수 기능을 보존한다

- 분야별 ID: `D-GAMEPLAY-SETTLEMENT-SUPPLY-005`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 플레이어의 기본 정책은 모든 집단의 최소 생존선 보호다. 물자 부족이 발생해도 추가 조달·대체재·대피·운송 변경·생산 전환으로 전원 보호가 가능한지 먼저 다시 확인한다.
- 전원 보호가 실제로 불가능하면 현재 물자와 시간으로 즉시 살릴 수 있는 생명 수를 첫 기준으로 최대화한다. 결과가 비슷하거나 연쇄 사망 위험이 있으면 의료·의류·난방·급수·방어 같은 필수 기능의 유지 여부를 두 번째 기준으로 삼는다.
- 이후 발견·연구·협상으로 얻은 배분 규칙이나 알고리즘은 출처·가정·적용 범위·예상 결과를 확인하고 플레이어가 명시적으로 채택한 경우에만 보조 판정으로 사용한다. 새 규칙이 기본 보호 원칙을 조용히 대체하지 않는다.
- 채택 전후에는 보호 집단·예상 손실·필수 기능·불확실성 차이를 보여 주고 채택·중단·판본 기록을 남긴다. 알고리즘은 설명 가능한 후보이지 자동 명령이 아니며, 정확 NPC 무응답 범위·규칙 획득/UI·수치는 후속 기획이다.

## D-502 새 배분 규칙은 기존 정책과 결과를 비교하고 시험한 뒤 채택하거나 폐기한다

- 분야별 ID: `D-GAMEPLAY-SETTLEMENT-SUPPLY-006`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 플레이어가 연구·NPC·행정 기록·사건을 통해 새 배분 규칙이나 알고리즘을 발견하면 기존 정책과 입력 가정, 적용 대상·기간, 예상 생존자·사망·부상·이탈, 필수 기능 변화, 자원 소모와 불확실성을 비교할 수 있다.
- 새 규칙은 `발견 → 비교 Preview → 제한된 시험 적용 → 정식 채택 또는 폐기`를 거친다. 시험 범위와 되돌릴 수 없는 결과를 먼저 표시하고, 시험 결과가 없는 규칙을 검증 완료로 표현하지 않는다.
- 규칙은 자동 명령이 아니며 플레이어의 명시 채택 전에는 현재 정책을 바꾸지 않는다. 채택·중단·판본 변경과 실제 결과를 보존하고, 기본 전원 보호 원칙을 조용히 대체하지 않는다.
- 사용자는 영지 관리 문답을 이 결정까지 진행하고 마감하기로 했다. 정확 기준일수·자동 조달 상한·성내 Hub 시설·NPC 무응답 범위·규칙 획득처/UI는 `Deferred`이며 재개 요청 전 새 질문·WI·코드·DB·Scene·E 승격으로 확대하지 않는다.

## D-503 스토리 선택은 카드가 아니라 3인칭 현장 행동과 순서로 기록한다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-001`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 생존자 구조·화물 회수·습격 원인 추적 같은 스토리 선택을 모달 카드에서 하나 고르는 방식으로 제시하지 않는다. 플레이어는 3인칭 시점에서 실제 장소로 이동하고 대상과 대화·조사·구조·회수·추적 행동을 수행한다.
- 선택 결과는 카드 선택값이 아니라 접근한 대상, 시작·완료·중단한 행동, 소비한 시간·물자·체력, NPC에게 내린 지시, 현장을 떠난 시점과 후속 행동 기록에서 파생한다.
- 세 목표는 처음부터 영구 배타 분기로 잠그지 않는다. 시간·경로·역량이 허용하면 일부를 차례로 수행할 수 있고, 지연되면 생존자 상태·화물 손실·추적 단서가 실제로 달라진다.
- UI는 관찰 가능한 목표·현재 상태·시간 민감 징후·실행 가능한 상호작용을 보여 줄 수 있지만 선택의 도덕적 정답이나 미래 결과를 카드 문구로 미리 확정하지 않는다. 정확 첫 사건·입력·카메라·시간 압박·WI·Scene은 후속 기획과 개발 결속 대상이다.

## D-504 행동형 스토리 선택에는 countdown 대신 현장 징후의 부드러운 시간 압박을 사용한다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-002`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 운송대 습격처럼 시간에 민감한 현장은 플레이어를 무한히 기다리지 않지만 초 단위 countdown이나 숨은 정확 마감 시각을 암기하게 하지 않는다.
- 같은 권위 시간이 흐르면서 생존자의 목소리·움직임·상처, 연기·불길, 날씨·빛, 발자국·혈흔·냄새·흩어진 물품, NPC의 이동·보고가 단계적으로 변해 구조·회수·추적의 긴급도를 알린다.
- 징후는 실제 상태 변화와 같은 판본을 읽고, 연출만 악화되거나 상태만 변하고 표현이 고정되지 않는다. 플레이어가 메뉴·대화·상세 정보를 읽는 동안 시간이 어떻게 흐를지는 후속 기획에서 명시한다.
- 핵심 시간 정보는 색·소리 하나에만 의존하지 않고 시각·음향·텍스트/아이콘·NPC 반응 중 둘 이상의 경로로 전달한다. 정확 시간 단계·접근성 설정·입력·WI·Scene은 후속이며 이 결정은 구현·E 승격을 자동 승인하지 않는다.
- 기획상 필요한 오디오는 중앙 대장의 `audio:story:urgent:survivor-distress.r1`, `audio:story:urgent:hazard-escalation.r1`, `audio:story:urgent:npc-status-call.r1`로 분리 등록한다. 현재 상태는 `Specified`이며 실제 파일·결속·청취·승인이 아니다.

## D-505 기획 중 발견한 소리는 중앙 오디오 요구사항 대장에 별도 상태로 기록한다

- 분야별 ID: `D-PLANNING-AUDIO-REQUIREMENT-001`
- 상태: `AcceptedPlanningConvention` (2026-09-01). 기획 문답에서 효과음·환경음·BGM·음성이 필요해지면 주제 문서에 의미와 중앙 [오디오 요구사항 대장](../Architecture/PlayableLoops/오디오요구사항대장.md)의 `AudioRequirementStableId`를 연결한다. 별도 기능별 음원 목록이나 임시 Cue ID를 만들지 않는다.
- 중앙 대장은 `Discovered → Specified → CandidateSourced → PrototypeBound → RuntimeAudible → Accepted`와 `Deferred / Rejected`를 구분한다. 기획 확정, 음원 후보 확보, 이용조건 검토, Unity 결속, 실제 Play Mode 청취와 사람 승인을 서로 대신하지 않는다.
- 각 요구는 발생 순간·권위 상태·WI/H 문맥·2D/3D/Area/MusicBus·중복/복원 규칙·음량/끄기·자막/아이콘 같은 대체 판독과 출처·라이선스·hash를 단계에 맞게 기록한다. API key·로그인·결제 정보는 기록하지 않는다.
- 기획 스레드는 필요성과 의미를 기록하고, 실제 제작·구매·녹음·Unity 연결·청취 검증은 승인된 개발·표현 작업으로 인계한다. 요구 등록만으로 Presentation E나 기능 완료를 선언하지 않는다.

## D-506 시간 기반 압박은 신속한 대응으로 현장 목표를 모두 해결할 수 있게 설계한다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-003`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 운송대 습격 뒤 생존자 구조·마차 안정화·화물 보호·도주 몬스터 추적에는 같은 권위 시간이 흐르며, 너무 늦으면 부상 악화·화물 유실·흔적 소실처럼 각 목표의 조건이 나빠진다.
- 시간 압박은 고정 6시간이나 초 단위 countdown으로 정하지 않는다. 사건 규모·날씨·지형·몬스터 속도·플레이어와 동행 NPC의 역량에 맞는 단계형 시간창을 사용하고, 남은 가능성은 흔적의 선명도·NPC 보고·현장 변화·시간대 같은 관찰 가능한 정보로 알린다.
- 플레이어가 늦지 않게 우선순위와 동선을 정하고 직접 행동과 NPC 지시를 적절히 병행하면 구조·마차·화물·추적을 모두 원만하게 해결할 수 있는 경로를 기본 목표로 둔다. 처음부터 반드시 하나를 포기시키는 강제 삼자택일로 만들지 않는다.
- 늦어서 직접 추적 시간창을 놓치면 도주 몬스터의 즉시 소탕·사냥 기회는 닫힐 수 있지만 이야기를 영구 실패로 끝내지 않는다. 남은 흔적 조사·목격자·정찰·후속 수색 같은 비용이 더 드는 회복 경로를 둔다. 정확 시간 단계·난이도별 여유·NPC 병행 범위·WI·수치는 후속 기획 대상이다.

## D-507 모험가 시작은 개인의 신뢰에서 동료·거점·연결망으로 세력이 점진적으로 확장된다

- 분야별 ID: `D-STORY-ADVENTURER-POWER-GROWTH-001`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 모험가로 시작한 플레이어는 가문·영지·상단·병력을 처음부터 소유하지 않는다. 현장 자유와 SSS급 연금술 지식을 바탕으로 탐험·구조·거래·조사·전투의 실제 결과를 남기며 신뢰와 영향력을 쌓는다.
- 세력 성장은 `개인 생존과 평판 → 반복 협력자 → 소규모 동료·채집/운송망 → 임시 거점과 공방 → 지역 조직·상단·요동성 협력망`처럼 점진적으로 열린다. 단계는 단순 명성 수치가 아니라 사람·장소·설비·물자·경로·약속의 실제 관계가 성립했는지로 판정한다.
- 동료와 거점은 플레이어의 분신이나 무료 자동 생산기가 아니다. 각자의 역량·욕구·신뢰·보수·안전 조건이 있고, 위임 결과와 실패·이탈·재협상 기록이 남는다. 모험가가 세력을 키워도 소가주의 가문 권한을 그대로 복제하거나 자동 지휘하지 않는다.
- 후반에는 모험가의 독립 세력이 요동성의 정찰·조달·연금술·구조·전투에 기여하고 소가주 세력과 협력·경쟁·협상할 수 있다. 정확 첫 동료·첫 거점·조직 명칭·성장 관문·WI·수치는 후속 단일 문답에서 정한다.

## D-508 현장 NPC에게 생존자 응급처치와 마차·화물 보호를 맡길 수 있다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-004`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 운송대 습격 현장에서 플레이어는 직접 구조·안정화·보호·추적을 모두 수행할 필요가 없으며, 함께 있는 NPC에게 생존자 응급처치 또는 마차·화물 보호를 명시적으로 맡길 수 있다.
- 위임은 자동 성공이 아니다. NPC의 응급처치·경계·운송 역량, 신뢰와 의지, 필요한 약품·도구·인원, 마차 손상·추가 위협·지형 조건이 충족돼야 하며, 부족한 조건과 예상 가능한 위험을 지시 전에 확인할 수 있게 한다.
- 플레이어는 NPC에게 현장을 맡기고 도주 몬스터를 직접 추적할 수 있다. NPC는 상태가 악화되거나 임무를 완료·중단하면 중앙 오디오 요구 `audio:story:urgent:npc-status-call.r1`과 시각·텍스트 대체 경로로 보고하며, 보고음만으로 결과를 확정하지 않는다.
- 결과에는 담당 NPC·지시 시각·대상·사용 물자·완료/부분 완료/중단·원인·현장 상태 판본을 남긴다. 무응답 시 행동 범위, 명령 변경·복귀, 정확 UI·WI·수치는 후속 기획 대상이다.

## D-509 모험가는 Nature와 Town을 오가다 경비대장에게 발탁되어 병영학교에서 소가주를 만난다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MEETING-001`
- 상태: `SupersededBy D-514` (2026-09-01). 모험가 시작의 초반 동선은 Nature와 Town 사이를 오가며 탐색·채집·구조·의뢰·운송 보조를 수행하는 생활에서 시작한다. 반복된 실제 행동과 결과가 Town 경비대장의 관찰 근거가 된다는 부분은 D-514가 계승한다.
- 경비대장은 한 번의 대화 선택이나 숨은 레벨 수치만으로 모험가를 선발하지 않는다. 위험 대응, 약속 이행, 주민·운송대 보호, Nature 정보 제공과 같은 기록을 보고 병영학교 진입 기회를 연다.
- 모험가는 병영학교에서 훈련·경비·북방 정보가 모이는 조직을 처음 경험하고, 그곳에서 병약한 소가주를 처음 직접 본다. 두 인물은 서로의 정체와 SSS급 연금술 지식을 즉시 공유하지 않으며, 기존 불신 속 제한 협력 기준을 유지한다.
- 병영학교는 모험가의 자유를 즉시 끝내는 영구 군 복무 잠금이 아니다. 정식 생도·단기 연수·특별 협력자 중 정확 신분, 경비대장의 인물상, 첫 공동 과제와 첫 대화는 후속 단일 문답에서 정한다.
- 병영학교 훈련장 판독음은 중앙 오디오 요구 `audio:story:barracks-school:training-yard.r1`로 분리한다. 현재는 `Specified` 기획 요구이며 실제 음원·라이선스·Unity 결속·청취가 아니다.
- 대체 관계: 병영학교 진입과 그곳에서의 소가주 첫 만남은 현재 초반 경로에서 제외하고 다시 미정으로 돌린다. 경비대장이 여는 첫 기회는 D-514의 호송대 호위 의뢰다.

## D-510 모험가의 첫 세력은 관계를 유지하며 함께 이동하는 소규모 NPC 동행대다

- 분야별 ID: `D-STORY-ADVENTURER-POWER-GROWTH-002`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 모험가는 첫 고정 조직이나 영지를 얻기 전에 신뢰를 쌓은 소수 NPC와 소규모 동행대를 구성한다. 동행대는 Nature·Town·Hub·병영학교와 위험 현장을 함께 오가며 탐색·구조·운송·경계·전투를 분담한다.
- 파티 관계는 영입 순간 고정되는 호감도나 전투 보너스가 아니다. 약속 이행, 위험과 보상의 공정한 분담, 부상자 구조, 정보 공개, 휴식·보급, NPC의 욕구와 거부 존중 같은 실제 기록이 신뢰·불만·이탈·재합류에 영향을 준다.
- 플레이어가 NPC 관계를 잘 유지하면 역할 연계·자발적 보고·위기 지원·위임 신뢰 범위가 넓어진다. 관계를 소홀히 해도 즉시 영구 이탈만 강요하지 않고 대화·휴식·보상 정산·약속 이행으로 회복할 기회를 둔다.
- NPC는 플레이어의 분신이나 무조건 복종하는 자동 전투 유닛이 아니다. 각자의 역량·성격·목표·두려움·안전 기준을 가지며, 불가능하거나 부당한 지시는 거부하거나 대안을 제시할 수 있다. 정확 첫 동료 수·역할 조합·동행 상한·관계 UI·수치는 후속 기획 대상이다.
- 파티 협력 판독음은 중앙 오디오 요구 `audio:party:companion:coordination-call.r1`로 분리한다. 현재 `Specified`이며 실제 음성·배우·라이선스·Actor 결속·청취가 아니다.

## D-511 현장 NPC는 별도 지시가 없어도 안전한 범위의 기본 응급처치를 수행한다

- 분야별 ID: `D-INTERACTION-NPC-FIELD-AUTONOMY-001`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 생존자가 쓰러져 있고 즉각적인 위험이 없거나 감당 가능한 경우, 응급처치 역량이 있는 현장 NPC는 플레이어의 세부 명령을 기다리지 않고 기본 응급처치를 시작할 수 있다.
- 기본 응급처치는 출혈 억제·기도 확보·안정 자세·보온·상태 관찰처럼 악화를 늦추는 안정화다. 중상 완치, 희귀 약품·고급 물약 사용, 위험 지역 장거리 후송, 화물 포기·이동, 몬스터 추적까지 자동 확대하지 않는다.
- NPC는 자기 안전, 환자 접근 가능성, 응급처치 역량, 공용 기본 소모품과 추가 위협을 확인한다. 조건이 부족하면 무리한 연출상 처치를 하지 않고 플레이어에게 필요한 지원·도구·위험을 보고한다.
- 자율 행동의 시작·사용한 기본 물자·환자 상태 변화·중단/완료·원인을 현장 판본에 남긴다. 플레이어는 이후 담당 변경·고급 치료 승인·후송·철수를 지시할 수 있다. 정확 기본 물자 목록·중상 단계·AI 우선순위·WI·수치는 후속 기획 대상이다.

## D-512 NPC별 적성과 역할 조합에 따라 소규모 동행대의 장점과 공백이 달라진다

- 분야별 ID: `D-GAMEPLAY-COMPANION-PARTY-COMPOSITION-001`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 동행 NPC는 동일한 범용 전투원이 아니다. 마법·검술·궁술·정찰·응급처치·채집·제작·운송·교섭 같은 적성, 실제 숙련, 장비, 체력과 성격에 따라 잘하는 일과 취약한 일이 다르다.
- 파티 구성은 현재 목적과 위험에 따라 장단점이 생긴다. 전열 중심은 직접 교전에 강하지만 조사·치료·마법 대응이 약할 수 있고, 마법·지원 중심은 제어·회복·탐지에 강하지만 근접 보호와 장기 이동 부담이 커질 수 있다. 정찰·운송 중심도 이동·정보·화물에는 강하지만 정면전 공백을 가진다.
- 하나의 보편적 최강 조합이나 숨은 정답 파티를 두지 않는다. 임무 전에 알려진 위협·경로·날씨·필요 물자와 현재 역할 범위·중복·공백을 보여 주되, 다른 조합도 소모품·전술·우회·NPC 위임으로 보완할 수 있게 한다.
- 역할은 타고난 적성만으로 영구 고정하지 않는다. 경험·훈련·장비·관계로 보조 역할을 배울 수 있지만, 짧은 시간에 모든 역할을 완전히 대체하지는 않는다. 역할 수행 결과와 성장·부상·피로를 실제 NPC 기록에 남긴다.
- 파티 역할 보고는 기존 `audio:party:companion:coordination-call.r1`의 역할별 변형으로 관리하고 새 안정 ID를 자동 생성하지 않는다. 정확 역할 수·첫 동행대 조합·전환 비용·파티 상한·UI·수치는 후속 기획 대상이다.

## D-513 모험가는 이데아의 편린을 통해 검술의 본질을 꿰뚫어보는 시각을 가진다

- 분야별 ID: `D-STORY-ADVENTURER-IDEA-SIGHT-001`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 모험가 빙의체의 고유한 캐릭터성은 검술 동작의 겉모습만 모방하는 것이 아니라 자세·호흡·힘의 전달·거리·박자·의도·빈틈에 담긴 원리를 알아차리고 검술의 이데아에 가까운 편린 후보를 보는 시각이다.
- 이 시각은 D-334/D-335/D-468의 관심 분야·명상·주의 강화·편린·학습 지식 구분을 유지한다. 관찰 강조나 ‘이해할 것 같다’는 감각은 편린 획득·검술 학습·시전/사용 자격·숙련 완료가 아니다. 실제 편린은 관련 관찰·실전·훈련·성찰 조건을 충족한 권위 결과로만 남긴다.
- 편린을 얻어도 몸의 근력·유연성·반응·체력, 무기 적응, 반복 훈련과 스승의 교정이 부족하면 기술을 온전히 재현하지 못한다. 통찰은 잘못된 연습을 줄이고 핵심 훈련·상대의 의도·자기 결손을 찾는 이점이며 즉시 최강 검사가 되는 치트가 아니다.
- 다른 NPC의 검술을 빼앗거나 숨은 정답을 전부 공개하지 않는다. 관찰 대상의 실력·거리·시야·관계와 플레이어의 관심·현재 숙련에 따라 보이는 범위가 달라지며, 오해한 가설은 훈련·실전·대화로 검증하거나 폐기할 수 있다.
- 검술 이데아 공명음은 중앙 오디오 요구 `audio:idea:sword:resonance.r1`로 분리한다. 소리는 통찰 후보를 보조할 뿐 편린 획득의 단독 증거가 아니며 현재 `Specified` 기획 요구다. 정확 발현 시점·편린 종류·훈련 보정·UI·WI·수치는 후속 기획 대상이다.

## D-514 경비대장은 모험가에게 병영학교 입교 대신 첫 호송대 호위 임무를 부탁한다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-001`
- 상태: `ConfirmedStoryDirection / Replaces D-509 EarlyRoute` (2026-09-01). 모험가는 Nature와 Town을 오가며 남긴 위험 대응·약속 이행·주민과 운송대 보호 기록으로 경비대장의 신뢰를 얻는다.
- 경비대장이 여는 첫 조직 연결은 병영학교 입교가 아니라 Town과 외부 거점을 오가는 호송대의 호위 부탁이다. 플레이어는 의뢰의 경로·화물 성격·위험 보고·보수·동행 인원을 확인한 뒤 수락하고 현장에 참여한다.
- 호송 임무는 D-503~D-508의 행동형 사건으로 이어진다. 습격이 발생하면 생존자 구조·마차 안정화·화물 보호·도주 몬스터 추적을 직접 행동과 NPC 위임으로 해결하며, 신속하고 적절하게 역할을 나누면 모두 원만하게 해결할 수 있다.
- 임무 결과는 경비대장·호송대·Town 주민·상인과의 신뢰, 후속 의뢰, 동행대 후보와 모험가 세력 성장의 첫 근거가 된다. 병영학교와 소가주의 첫 만남 시점·장소·원인은 다시 미정이며 이 임무 성공만으로 군 소속이나 가문 권한을 얻지 않는다.
- 정확 출발지·목적지·화물·습격 몬스터·호위 NPC·보수·WI·Graph Map 경로는 후속 기획 대상이다. 현재 긴급 현장·파티 보고 오디오 요구를 재사용하며 새 음원 ID를 자동 추가하지 않는다.

## D-515 희귀 약품은 기본 승인제로 두되 현장을 떠나기 전에 NPC 자율 사용 정책을 설정한다

- 분야별 ID: `D-INTERACTION-NPC-FIELD-AUTONOMY-002`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 희귀 약품·고급 물약은 기본적으로 플레이어 승인을 받아 사용한다. 플레이어가 추적이나 다른 현장 목표를 위해 자리를 떠나기 전에는 담당 NPC와 약품 범위를 지정해 자율 사용 정책을 설정할 수 있다.
- 정책은 `승인 필요`, `생명 위험 시 자율 사용`, `자동 사용 금지`의 세 상태로 둔다. 기본값은 `승인 필요`이며, 플레이어의 침묵이나 이탈을 사용 허가로 해석하지 않는다.
- `생명 위험 시 자율 사용`은 응급처치 역량이 있는 NPC가 즉각적인 사망 위험, 대상 적합성, 기대 효과, 금기·중복 투여, 남은 수량과 더 안전한 대안의 부재를 확인한 경우에만 허용한다. 판단 근거가 부족하면 기본 안정화와 지원 요청에 머문다.
- 이탈 시점의 정책은 임무·담당 NPC·약품 범위·판본에 동결한다. 실제 사용·보류·실패, 대상, 수량, 사유와 남은 재고를 기록하고 플레이어가 복귀한 뒤 검토할 수 있게 한다.
- 정확 약품 등급, 생명 위험 판정 기준, 통신 가능 거리, 설정 UI, 대응 WI와 수치는 후속 기획 대상이다. 이 결정은 희귀 약품을 무상·무한 소비시키거나 NPC의 의료 역량을 자동으로 높이지 않는다.

## D-516 모험가 동행대의 기본 전투 조합은 플레이어를 포함한 근거리 1명과 원거리 1명이다

- 분야별 ID: `D-GAMEPLAY-COMPANION-PARTY-COMPOSITION-002`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 모험가 플레이어는 전사·궁수 등 서로 다른 전투 역할로 성장할 수 있으므로 기본 전투 조합은 고정 NPC 직업 목록이 아니라 **플레이어를 포함한 근거리 담당 1명과 원거리 담당 1명**의 역할 충족으로 정의한다.
- 플레이어가 근거리 역할이면 기본 동행 후보는 원거리 역할을 우선 제시하고, 플레이어가 원거리 역할이면 근거리 역할을 우선 제시한다. 플레이어가 어느 역할도 충분히 수행하지 못하거나 지원 역할을 선택하면 두 역할을 NPC가 나눠 맡을 수 있다.
- 이 구성은 이해하기 쉬운 기본안이자 추천일 뿐 출전 강제 조건이 아니다. 임무·관계·부상·가용 인원에 따라 단독, 동일 역할 중복, 마법·치료·정찰 중심 조합도 허용하되 비어 있는 전열 보호·원거리 견제와 예상 위험을 출발 전에 보여 준다.
- 근거리/원거리 판정은 장비 이름만이 아니라 현재 무기·실제 숙련·행동 가능 거리·탄약 또는 자원·부상 상태를 함께 본다. 정확 역할 전환 기준, 파티 인원 상한, 추천 UI, 전투 WI와 수치는 후속 기획 대상이다.

## D-517 NPC 관계의 중심은 선물과 대화가 아니라 실제 공동 행동과 약속 이행 기록이다

- 분야별 ID: `D-STORY-ADVENTURER-POWER-GROWTH-003`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). NPC 신뢰와 동료 관계의 주된 근거는 함께 이동·전투·구조·채집·운송·휴식한 기록, 맡은 역할의 수행, 약속 준수, 위험·보상·물자의 분담과 거부 의사의 존중이다.
- 선물과 대화는 관계의 의미를 표현하고 오해를 풀거나 갈등을 회복하는 보조 수단이다. 비싼 선물 반복이나 정답 대사 선택만으로 공동 행동에서 생긴 배신·방치·불공정 분배를 지우거나 높은 신뢰를 구매할 수 없다.
- 관계 변화는 행동의 결과뿐 아니라 맥락을 함께 본다. 수행 가능성, 플레이어가 알고 있던 위험, 약속한 범위, 실패 뒤 보고·구조·보상·재시도와 NPC 자신의 판단을 기록해 의도하지 않은 실패와 고의적 방치를 구분한다.
- UI는 단일 호감도 숫자보다 최근 공동 행동, 지킨 약속과 미해결 약속, 신뢰 가능한 역할, 갈등·회복 가능 행동을 요약한다. 정확 기억 기간·관계 단계·망각·용서 규칙·UI·WI·수치는 후속 기획 대상이다.

## D-518 병영·마법학교·마탑 교육의 공통 목표는 이데아를 직관해 현실에 드러내는 것이다

- 분야별 ID: `D-STORY-ADVENTURER-IDEA-SIGHT-002`
- 상태: `ConfirmedWorldviewDirection / PlanningBaseline` (2026-09-01). 병영에서 무예를 배우든 마법학교·마탑에서 마법을 배우든 교육의 공통 본질은 현상의 겉모양을 암기하는 데 있지 않다. 관련 이데아의 원리와 관계를 직관하고, 그것을 몸·무기·마력·술식·도구를 통해 현실에 재현하는 능력을 기르는 데 있다.
- 병영은 자세·호흡·거리·박자·힘·대형과 실전을 통해, 마법학교는 이론·언어·기호·기초 술식과 안전한 반복을 통해, 마탑은 전문 연구·가설·실험·고난도 현상 제어를 통해 같은 목표에 서로 다른 경로로 접근한다.
- 학습은 `현상 관찰 → 이데아 편린 후보 직관 → 설명·가설 → 반복 훈련·실험 → 현실 발현 → 결과 관찰·교정`으로 순환한다. 교육기관은 교사·자료·시설·안전·교정과 검증 기회를 제공하지만 입학·수료·직위만으로 이데아의 편린이나 실제 숙련을 자동 지급하지 않는다.
- 현실 발현은 단순 시각 효과가 아니라 같은 원리를 의도한 조건에서 반복해 결과를 만들고, 실패·대가·한계를 설명하며 회복할 수 있어야 한다. 직관만 있고 재현하지 못한 상태, 재현했지만 원리를 이해하지 못한 모방, 안정적으로 이해·재현한 숙련을 구분한다.
- 정확 교육 과정, 학파·마탑별 해석 차이, 평가 방식, 학위·자격, 관련 WI·UI·수치는 후속 기획 대상이다. 이 결정은 병영학교를 초반 필수 경로로 되돌리거나 마탑 입학을 자동 승인하지 않는다.

## D-519 경비대장의 소개로 만난 훈련 교관의 시범 동작에서 첫 검술 이데아 후보를 직관한다

- 분야별 ID: `D-STORY-ADVENTURER-IDEA-SIGHT-003`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 경비대장은 검술 기본기를 직접 시연하는 인물이 아니다. 모험가가 호송과 위험 대응에서 신뢰를 쌓으면 적합한 훈련 교관에게 소개하거나 시범 훈련을 참관할 기회를 마련한다.
- 첫 직관 장면은 훈련 교관이 기본 자세·발놀림·호흡·힘의 전달·거리와 베기 동작을 시범 보이는 순간이다. 주인공은 화려한 고급 기술이 아니라 정제된 기본기 안에서 동작들을 하나로 묶는 검술 이데아의 원리와 편린 후보를 알아차린다.
- 관찰 순간에는 시야·주의·기존 경험에 따라 자세·힘의 흐름·빈틈 같은 단서가 강조되지만, 이것만으로 실제 편린 획득이나 기술 습득을 확정하지 않는다. 주인공이 자신의 말로 원리를 설명하고 직접 따라 해 본 뒤 교관의 교정과 반복 훈련·실전에서 재현해야 한다.
- 교관은 주인공의 질문·설명·재현을 보고 통찰의 깊이와 오해를 판단한다. 경비대장의 추천은 만남의 기회일 뿐 교관의 인정·수료·병영 소속을 보장하지 않는다.
- 이 장면의 직관 신호는 기존 `audio:idea:sword:resonance.r1`을 재사용한다. D-509의 병영학교 입교·소가주 첫 만남과 `audio:story:barracks-school:training-yard.r1`은 계속 대체·Deferred 상태이며, 정확 훈련 장소·교관 인물·첫 기본기·편린 내용·WI·UI·Scene은 후속 기획 대상이다.

## D-520 첫 호송 화물은 국방 경비 거점으로 보내는 의약품과 보존식량이다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-002`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 모험가가 처음 호위하는 화물은 Town 또는 조달 거점에서 북방의 국방 경비 거점으로 보내는 의약품과 보존식량이다. 화물은 단순 판매품이 아니라 경계 인력의 치료·근무·순찰 지속에 필요한 예정 보급이다.
- 의약품 화물과 보존식량 화물은 수량·포장·손상·유실·인수 상태를 분리한다. 의약품 손실은 부상자 치료와 응급 대비를, 식량 손실은 경계 인력의 체력·근무 교대·장기 주둔을 악화시키며 한쪽 보존이 다른 쪽 손실을 숨기지 않는다.
- 습격 현장에서는 생존자 구조, 마차 안정화, 두 화물군 보호와 도주 몬스터 추적이 함께 열린다. 플레이어와 NPC가 신속히 역할을 나누면 전부 해결할 수 있지만, 지연·파손·임시 사용이 발생하면 실제 남은 수량과 사유가 목적지 인수 기록에 이어진다.
- 화물은 목적지 경비 거점의 적격 담당자가 실제로 인수하고 재고에 반영한 뒤에만 방비에 기여한다. 출발·화면 표시·도착 연출만으로 의약품·식량 비축을 증가시키지 않는다.
- 현장 부상자에게 호송 의약품을 전용할 수 있는 조건, 일반·희귀 약품 구분, 보존식량 종류, 출발지·목적 거점, 수량·보상·경로·습격 원인·WI·Graph Map은 후속 기획 대상이다. 현실 군수 자료나 실제 시설 정보를 자동 반입하지 않는다.

## D-521 모험가의 핵심 재능은 이데아를 직관하고 원리를 자기 방식으로 흡수·재구성하는 능력이다

- 분야별 ID: `D-STORY-ADVENTURER-IDEA-SIGHT-004`
- 상태: `ConfirmedCharacterDirection / PlanningBaseline` (2026-09-01). 모험가 빙의체는 검술에만 한정되지 않고 무예·마법·연금술·채집·제작·생존 기술 등 다양한 현상에서 이데아의 원리와 편린 후보를 남보다 잘 알아차리는 탁월한 직관력을 가진다.
- 이 재능의 핵심은 본 것을 그대로 복사하는 것이 아니라 `관찰 → 핵심 원리 추출 → 자기 몸·감각·마력·장비·환경과 대조 → 자기 방식의 가설·동작·술식으로 재구성 → 반복 검증`을 거쳐 자기 것으로 소화하는 데 있다.
- 서로 다른 분야에서 얻은 원리를 연결해 새로운 해결법을 발견할 수 있지만, 피상적 유사성만으로 기술을 합치거나 대상의 고유 능력·혈통·기억을 빼앗지 않는다. 잘못 이해한 직관은 실패·부작용·교관/NPC의 반론과 실제 결과를 통해 수정하거나 폐기한다.
- 뛰어난 흡수력은 학습 방향과 시행착오를 줄여 줄 뿐 신체 능력·마력량·재료·도구·안전·반복 훈련·시간을 생략하지 않는다. 직관한 원리, 설명 가능한 지식, 한 번의 재현, 안정적 숙련과 독자적 응용을 서로 다른 상태로 기록한다.
- 정확 분야별 흡수 속도, 교차 분야 조합 조건, 오해·부작용, 성장 상한, UI·WI·수치는 후속 기획 대상이다. 이 결정은 모든 기술 즉시 습득이나 무제한 만능화를 승인하지 않는다.

## D-522 메인 스토리 기획서의 표시 이름은 Mirror로 한다

- 분야별 ID: `D-PLANNING-PROJECT-IDENTITY-002`
- 상태: `AcceptedNaming / Replaces StoryDisplayName` (2026-09-01). 메인 스토리 기준선과 사용자에게 보이는 기획서 이름은 프로젝트 표시명과 같은 `Mirror`를 사용한다.
- 이전 초안의 한국어 표시명은 현행 Markdown 제목·본문·진행 보고에서 재사용하지 않는다. 한국어 설명이 필요할 때는 기존 프로젝트 표기 기준에 따라 `Mirror(거울)`을 사용할 수 있다.
- 기존 파일 경로 `메인스토리-거울의흐름-기획-2026-09-01.md`, 내부 판본 키 `main-story-mirror-flow`, 링크와 과거 hash는 호환을 위해 유지한다. 이 결정은 namespace·assembly·Save·GUID·저장소·Unity 내부 식별자를 다시 이름 변경하는 승인이 아니다.

## D-523 자주 쓰는 동행대 구성을 프리셋으로 저장하고 출발 전에 현재 조건을 다시 확인한다

- 분야별 ID: `D-GAMEPLAY-COMPANION-PARTY-COMPOSITION-003`
- 상태: `ConfirmedGameplayDirection / PlanningBaseline` (2026-09-01). 플레이어는 자주 사용하는 동행대 구성에 이름을 붙여 프리셋으로 저장하고 이후 임무 준비에서 다시 불러올 수 있다.
- 프리셋은 플레이어 역할, NPC 고유 식별자와 주·보조 역할, 근거리·원거리·지원 배치, 기본 장비/소모품 구성 참조와 D-515의 NPC별·약품 범위별 자율 사용 정책을 저장한다. 실제 장비·약품 수량이나 NPC 현재 상태를 복제하지 않는다.
- 프리셋을 불러오면 현재 관계·참여 의사·부상·피로·위치·임무 자격·장비·탄약/마력·재고를 다시 대조한다. 참여할 수 없는 NPC나 부족한 물자는 자동 생성·강제 배치하지 않고 결손 역할·대체 후보·예상 위험을 보여 준다.
- 출발 전에는 현재 판본으로 해석된 구성과 자율 정책을 플레이어가 확인한다. 저장된 프리셋 변경과 이번 임무 한정 수정은 구분하며, 임무 중 일어난 부상·관계 변화·소비가 원본 프리셋을 조용히 덮어쓰지 않는다.
- 정확 프리셋 수, 공유·정렬·추천, 장비 세부 범위, 자동 보충, UI·Save·WI·수치는 후속 기획 대상이다. 프리셋은 파티 출전 성공이나 NPC 동의를 보장하는 권위 명령이 아니다.

## D-524 경비대 호송 의뢰 전에 한스 농장에서 공동 행동으로 친분을 쌓는다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-003`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 모험가 초반 경로를 `Nature 탐색 → 춘분 무렵 농장 발견 → 한스 아저씨와 반복 협력 → Town·경비대의 호송 의뢰`로 연결한다. `한스 아저씨`는 D-414·D-415에서 이어지는 농장 경로의 첫 이름 있는 지속 NPC이며, 굶주린 농장 주민 전체를 대신하는 유일한 NPC는 아니다.
- 친분은 한 번의 선물·정답 대화·숨은 호감도 충전으로 성립하지 않는다. 먹거리 마련, 농장 주변 위험 확인, 간단한 수리·운반·밭일 보조, 약속한 시간에 돌아오기, 결과 공유와 공정한 정산처럼 플레이어가 실제로 함께한 작은 행동 기록으로 쌓인다. 정확 첫 WI와 필요한 횟수는 후속 기획에서 정한다.
- 한스는 Farm의 계절·작물·통행·주변 위험과 Town 사람들에 관한 생활 정보를 알려 주고, 플레이어의 신뢰 가능한 행동을 경비대나 운송 관계자에게 증언하거나 첫 연결을 마련할 수 있다. 그러나 한스의 추천 한 번이 군 조직 가입·호송 임무·보상을 자동 승인하지 않으며, 경비대장은 실제 위험 대응·약속 이행·주민과 운송대 보호 기록과 현재 임무 조건을 별도로 확인한다.
- 플레이어가 Farm 활동을 원하지 않는 시작 경로를 영구 차단하지 않는다. 한스 관계는 모험가 초반의 권장 인과이자 자연스러운 튜토리얼 경로이며, 다른 검증 가능한 공동 행동 기록으로 경비대 신뢰를 얻는 대체 경로는 후속 기획에서 열어 둘 수 있다.
- 정확 한스 외형·나이·가족·농장 소유 구조·대사·첫 문제·관계 단계·보상·WI·Graph Map 노드·Prefab·Scene은 미정이다. 이번 결정은 새 코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-525 한스와의 첫 공동 행동은 농장 침입 흔적 조사와 무너진 울타리 임시 보수다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-004`
- 상태: `ConfirmedStoryDirection / PlanningBaseline` (2026-09-01). 모험가는 한스와 함께 농장 가장자리의 발자국·훼손·사라진 물품·가축 반응을 조사하고, 위험이 다시 들어오지 않도록 무너진 울타리를 임시 보수한다.
- 조사와 보수는 서로 다른 결과를 남긴다. 조사는 Nature에서 Farm·Town 방향으로 이어지는 위협 단서와 안전한 통행 정보를 만들고, 보수는 농장 주민·작물·가축의 즉시 위험을 낮춘다. 하나만 완료해도 그 결과는 남지만 둘을 함께 마치면 한스의 신뢰와 경비대에 전달할 근거가 더 명확해진다.
- 한스는 위치·농장 상태·도구 사용법과 자신이 본 변화를 알려 주고 플레이어와 역할을 나눈다. 플레이어가 모든 일을 대신 처리하거나 전투를 강제로 겪는 튜토리얼로 만들지 않으며, 발견한 흔적만으로 몬스터 종류·습격 원인·요동성 위협을 확정하지 않는다.
- 정확 흔적 종류·울타리 길이·재료·도구·시간·실패/복구·첫 WI·Graph Map L1/L2/L3·자산과 배치는 후속 기획 대상이다. 이번 결정은 실제 Farm Scene 수정이나 E 승격을 승인하지 않는다.

## D-526 호송대 중상자는 현장 책임자의 기록 아래 사전 허용된 의약품을 긴급 사용한다

- 분야별 ID: `D-INTERACTION-NPC-FIELD-AUTONOMY-003`
- 상태: `ConfirmedInteractionDirection / PlanningBaseline` (2026-09-01). 호송대 습격으로 중상자가 발생하고 플레이어의 즉시 승인을 기다리면 생명 위험이 커지는 경우, 사전에 `생명 위험 시 자율 사용` 권한을 받은 적격 현장 책임자는 허용된 의약품 범위 안에서 긴급 처치를 결정할 수 있다.
- 현장 책임자는 환자 고유 식별자, 부상 상태와 판본, 사용 의약품·Lot·수량, 사용 시각, 판단자, 적용한 자율 정책 판본, 생명 위험·금기·중복·대안·재고 확인 근거, 처치 직후 결과와 후속 인계, 잔여 화물·재고를 기록한다. 통신이 가능하고 지연 위험이 낮으면 기존 승인 요청을 우선한다.
- 의약품 사용 기록은 치료 완료를 자동 선언하지 않는다. 환자 상태를 다시 관찰하고 후송·추가 치료 필요를 남기며, 소비한 호송 화물은 목적지 인수 수량에서 정확히 차감한다. 같은 사용을 환자 치료와 거점 납품 양쪽에 중복 계상하지 않는다.
- 권한이 없거나 정책이 `승인 필요`·`자동 사용 금지`인 약품은 책임자라는 이유만으로 사용할 수 없다. 담당자의 의료 역량·접근 안전·약품 적합성·실제 재고가 부족하면 사용을 차단하고 가능한 기본 안정화와 지원 요청을 기록한다.
- 정확 중상 판정·현장 책임자 선정·의약품 등급·UI·통신·WI·Save·Replay·수치는 후속 기획 대상이다. 이번 결정은 무한 의약품, 자동 완치, 현실 의료 판단이나 Unity 실행을 승인하지 않는다.

## D-527 중요한 현장 결정은 실행 가능한 선택 카드로 안내하고 실제 행동으로 성립시킨다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-005`
- 상태: `ConfirmedInteractionDirection / PartiallyReplaces D-503 Presentation` (2026-09-01). 플레이어가 자유로운 현장에서 무엇을 해야 할지 이해하기 어려울 수 있으므로, 이야기상 중요한 결정 순간에는 현재 실행 가능한 의도와 우선순위를 카드로 보여 주고 플레이어의 선택을 받는다.
- 카드는 `무엇을 우선할지`, 직접 수행할지 적격 NPC에게 맡길지, 현재 관찰된 대상·필요 물자·알려진 위험·시간 민감 징후와 불확실성을 짧게 설명한다. 예를 들어 호송 습격에서는 `중상자 지원`, `마차·화물 보호`, `도주 흔적 추적`, `동행대에 역할 위임`처럼 현재 조건에서 시작 가능한 카드만 기본 목록에 둔다. 차단된 선택과 해제 조건은 별도 상세에서 확인한다.
- 카드 선택은 임무 결과·도덕적 정답·보상·성공을 즉시 확정하지 않는다. 선택한 카드는 활성 의도·우선 목표·위임 초안을 만들고, 실제 접근·Confirm·행동 시작·완료·중단·소비·NPC 수행과 현장 이탈 기록이 결과를 결정한다. 실행 전에 카드를 닫거나 다른 의도로 바꿀 수 있다.
- D-503의 현장 행동 증거 원칙은 유지한다. 다만 `모달 카드를 제시하지 않는다`는 표시 기준은 이번 결정이 대체한다. 카드는 자유 탐색을 영구 잠그지 않고 필요할 때 다시 열 수 있으며, 시간 민감 현장에서 카드 화면을 열었다는 이유만으로 권위 시간이 자동 정지하지 않는다.
- 정확 카드 수·화면 위치·일러스트·키보드/패드 입력·일시정지 정책·추천 강조·튜토리얼 노출 횟수·WI·Save·Replay는 후속 기획 대상이다. 이번 결정은 새 UI 코드·Scene·E 승격·commit/push를 승인하지 않는다.

## D-528 교관의 기본기 시범을 직관하면 미숙련 스킬이 상태창에 조용히 등록된다

- 분야별 ID: `D-STORY-ADVENTURER-IDEA-SIGHT-005`
- 상태: `ConfirmedStoryDirection / PartiallyReplaces D-519 AcquisitionBoundary` (2026-09-01). 모험가가 훈련 교관의 기본기 시범을 필요한 범위까지 관찰하고 이데아의 원리와 편린 후보를 직관하면, 해당 기본기 스킬 항목을 캐릭터의 스킬 상태에 `관찰 등록 / 미숙련`으로 추가한다.
- 주인공은 장면에서 크게 놀라거나 획득 사실을 선언하지 않는다. 대형 팝업·강제 상태창·과장된 획득 연출 없이 진행하며, 플레이어가 이후 스킬 상태창을 열면 새 항목과 현재 미숙련 상태·관찰 출처·다음 훈련 조건을 확인할 수 있다. 필요한 경우 작은 새 항목 표시만 사용할 수 있다.
- 등록은 연습 가능한 지식·동작 후보가 생겼다는 뜻이다. 즉시 능력치 상승, 안정적 실전 사용, 완성된 편린, 교관의 인정·수료나 다른 검술의 자동 습득을 부여하지 않는다. 설명·모방·교정·반복 훈련과 실제 재현으로 숙련 단계를 올린다.
- 권위 기록은 관찰자·교관·시범 스킬 고유 식별자, 관찰 완료와 직관 근거, 원인 행위 기록·revision을 보존해야 한다. `audio:idea:sword:resonance.r1`이나 UI 표식만으로 등록하지 않으며, 공명음은 사용하더라도 눈에 띄는 획득 알림이 아니라 직관을 보조하는 절제된 신호다.
- D-519의 교관·기본기·이데아 직관 장면은 유지한다. 다만 `관찰만으로 기술 항목도 등록하지 않는다`는 해석은 이번 결정이 대체하고, 실제 숙련·안정적 발현을 자동 획득하지 않는 경계는 유지한다.
- 정확 첫 스킬 이름·설명·초기 단계·훈련 횟수·숙련 수치·상태창 배치·새 항목 표시·WI·Save·Replay·Scene은 후속 기획 대상이다. 이번 결정은 새 코드·Unity UI·E 승격·commit/push를 승인하지 않는다.

## D-529 한스와의 첫 신뢰는 플레이어의 자발적인 벌목과 무보수 울타리 수리에서 시작한다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-005`
- 상태: `ConfirmedStoryDirection / Replaces D-525 OpeningSequence` (2026-09-01). 모험가는 춘분 농장의 망가진 울타리를 발견한 뒤 한스의 의뢰나 보상을 기다리지 않고, 농장 경계 밖에서 벌목이 허용된 가까운 나무 한 그루를 직접 베어 목재를 마련하고 울타리를 먼저 고친다.
- 한스는 결과만 전해 듣는 것이 아니라 플레이어가 나무를 베고 목재를 옮겨 울타리를 고치는 과정을 직접 목격한다. 한스가 대가를 먼저 요구하지 않고 생활의 위험을 줄인 행동을 좋게 보는 순간이 첫 관계의 시작이며, 별도 의뢰 보수·즉시 호감 수치·선물 보상은 지급하지 않는다.
- `대가 없이`는 행동 비용이 없다는 뜻이 아니다. 벌목과 수리에는 시간·체력·도구 내구도와 나무·목재 소비가 실제로 남고, 플레이어가 소유·채취 권한이 없는 나무를 임의로 베거나 보수 결과만 연출해서는 성립하지 않는다. 벌목 대상과 울타리, 생산된 목재, 소비 수량, 수리 결과와 한스의 관찰 기록은 같은 원인 계보로 이어져야 한다.
- D-525의 침입 흔적 조사는 폐기하지 않고 첫 신뢰가 열린 뒤 한스와 함께 수행하는 후속 공동 행동으로 이동한다. 조사는 위협·통행 단서를, 선행 수리는 주민·작물·가축의 즉시 위험 감소를 각각 남기며, 한스의 증언은 경비대의 독립 확인을 대신하지 않는다.
- 정확 나무 종류·거리·벌목 횟수·울타리 구간·수리 도구·대사·관계 단계·WI 재사용/복합 WI·Graph Map L1/L2/L3·Prefab·Scene·오디오·보상 표시는 후속 기획 대상이다. 이번 결정은 새 코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-530 선택 카드 열람은 안전한 상황에서 정지하고 시간 민감 사건에서는 감속한다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-006`
- 상태: `ConfirmedInteractionDirection / Completes D-527 TimePolicy` (2026-09-01). 자유 탐색·거점·전투 전 준비처럼 현재 판본에서 안전 판정이 성립한 상황에서는 선택 카드를 열어도 불이익 없이 읽을 수 있도록 세계 시간을 완전히 정지한다.
- 전투·추적·화재·붕괴·생존자 상태 악화처럼 시간 민감 사건이 이미 시작된 뒤에는 카드 열람 중에도 권위 시간이 멈추지 않고 느리게 흐른다. 카드는 현재 `정지` 또는 `감속 진행` 상태와 계속 변하는 시간 민감 징후를 명시하며, 닫은 뒤 실제 세계 상태를 다시 조회한다.
- 카드를 반복해서 열고 닫아 사건 진행을 고정·초기화·되감기할 수 없다. 카드 선택은 D-527과 같이 활성 의도·우선 목표·위임 초안만 만들며 실제 행동·소비·NPC 수행·결과를 대신하지 않는다. 카드가 열린 동안에도 사전 승인된 NPC 자율 행동은 해당 정책 범위에서 계속될 수 있다.
- 정확 감속 배율·진입/복귀 보간 시간·입력 차단 범위·오디오 처리·멀티플레이/Hosted 동기화·접근성 옵션·UI 표시는 후속 기획과 실제 체감 검증 대상이다. 감속이 현장 판단을 불가능하게 하거나 사실상 무제한 정지로 작동하면 가장 이른 표현·시간 책임 단계로 되돌린다.

## D-531 첫 울타리 수리는 현장 손도끼 발견과 선택 카드 뒤 직접 행동으로 완결한다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-006`
- 상태: `ConfirmedStoryDirection / Refines D-529 OpeningExecution` (2026-09-01). 모험가는 시작부터 손도끼를 소유하지 않는다. 춘분 농장의 망가진 울타리 부근에서 바닥에 떨어진 낡은 손도끼를 발견하고, 이를 조사한 뒤 `손도끼를 들어 가까운 허용 나무를 베고 울타리를 수리한다`는 하나의 선택 카드를 확인한다.
- 카드는 행동의 목적·예상 순서와 대가를 알려 주고 활성 의도를 정할 뿐 도끼 획득·벌목·수리를 자동 완료하지 않는다. 플레이어가 카드를 선택한 뒤 실제로 손도끼까지 이동해 줍고, 허용 나무까지 이동해 직접 벌목하며, 생긴 목재를 울타리까지 가져와 손상 구간을 직접 수리해야 한다.
- 각 단계는 `도끼 발견 → 카드 선택 → 도끼 줍기 → 허용 나무 접근 → 벌목 시작·완료 → 목재 획득·운반 → 울타리 수리 시작·완료 → 한스의 직접 목격 → 첫 신뢰`의 원인 계보를 보존한다. 중간에 취소·이탈·도구 파손·재료 부족이 발생하면 완료된 마지막 단계부터 회복할 수 있으며 카드 재열람으로 소비나 진행을 초기화하지 않는다.
- 선택 카드에는 `울타리를 그냥 살펴본다` 또는 `지금은 지나간다`처럼 비실행 선택을 함께 둘 수 있다. 수리를 선택하지 않았다고 관계가 즉시 악화되지는 않으며, 나중에 현장 상태가 유효하면 다시 선택할 수 있다. 소유·채취 권한이 없는 나무, 목적과 무관한 벌목, 바닥 손도끼를 영구 무상 소유물로 확정하는 해석은 허용하지 않는다.
- 한스의 첫 신뢰는 D-529와 같이 완성 연출이나 카드 선택이 아니라 실제 수리 과정과 결과를 직접 목격했을 때 열린다. 정확 손도끼 소유자·반납/양도 처리, 나무 종류·거리, 벌목 횟수, 목재 수량, 울타리 구간, 카드 문구·UI, WI 분해/재사용, Graph Map L1/L2/L3, Prefab·Scene·오디오는 후속 기획 대상이다. 이번 결정은 코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.
- 이번 결정은 새 시간 권위, 카드 UI 코드, Scene·Save·E 승격·commit/push를 승인하지 않는다.

## D-532 현장 손도끼 사용과 한스 집 옆 추가 목재 적재는 선택 카드로 분리한다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-007`
- 상태: `ConfirmedStoryDirection / Refines D-531 OptionalActions` (2026-09-01). 바닥의 손도끼는 플레이어가 반드시 주워야 하는 강제 튜토리얼 도구가 아니다. 발견한 상태로 그대로 둘 수도 있고, 선택 카드에서 사용을 고른 뒤 주워서 가지고 다니며 벌목에 이용할 수도 있다. 사용하지 않거나 현장을 떠났다는 이유만으로 실패·관계 하락·퀘스트 포기를 확정하지 않는다.
- 첫 카드 묶음은 `손도끼를 사용해 울타리를 수리한다`, `손도끼만 살펴본다`, `지금은 그대로 둔다`를 현재 조건에 따라 보여 준다. 손도끼를 주운 뒤에도 벌목·수리 대상을 실제로 찾아 행동해야 하며, 카드는 도끼를 자동 장착하거나 나무·목재·수리 결과를 지급하지 않는다.
- 울타리 수리 뒤에는 별도의 선택으로 `허용된 나무를 더 베어 한스 집 옆 지정 적재 지점에 목재를 둔다`를 열 수 있다. 플레이어는 손도끼를 계속 가지고 직접 추가 벌목·운반·적재할 수 있으며, 실제 적재된 목재의 종류·수량·상태만 한스가 이후 사용할 수 있는 도움으로 기록한다.
- 추가 목재는 울타리 수리와 다른 선택 결과다. 수행하지 않아도 첫 수리와 이미 성립한 신뢰를 취소하지 않고, 수행했다고 첫 신뢰 단계를 반복 지급하거나 무제한 호감·무한 목재를 만들지 않는다. 허용 벌목 구역·재생·도구 내구도·운반 한계와 한스 집 주변 통행·화재·출입구를 침범하지 않는 적재 지점을 지켜야 한다.
- 손도끼는 사용 후 즉시 반납하도록 강제하지 않는다. 플레이어가 소지하거나 다시 내려놓는 상태를 허용하되, 정확 장기 소유권·분실·파손·수리·대체 도구·인벤토리 슬롯·지정 적재 위치·카드 UI·WI·Graph Map·Prefab·Scene은 후속 기획 대상이다. 이번 결정은 코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-533 한스는 플레이어가 떠난 뒤 집 옆의 추가 목재를 발견한다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-008`
- 상태: `ConfirmedStoryDirection / Refines D-532 DiscoveryTiming` (2026-09-01). 플레이어가 울타리 수리 뒤 선택적으로 마련한 여분 목재는 한스에게 직접 건네거나 대가를 요구하지 않는다. 플레이어는 실제 목재를 한스 집 옆의 지정 적재 지점에 조용히 쌓아 두고 현장을 떠날 수 있다.
- 첫 울타리 수리는 한스가 과정을 직접 목격해 첫 신뢰를 여는 행동이고, 추가 목재는 그 뒤 한스가 적재 상태를 직접 발견해 플레이어의 지속적인 배려를 알아차리는 후속 행동이다. 두 관찰 시점과 관계 근거를 합치지 않는다.
- 한스의 사후 발견은 플레이어가 현장을 벗어났고, 적재된 목재가 실제로 남아 있으며, 한스가 생활 동선에서 지정 지점에 접근한 뒤에 성립한다. 화면 밖 자동 호감 지급이나 플레이어가 목재를 내려놓는 순간의 원격 인지로 처리하지 않는다.
- 발견 뒤에는 한스의 짧은 반응·다음 만남의 대사·생활 재료 사용 가능 상태를 열 수 있지만, 즉시 보상 팝업·강제 대화·반복 신뢰 지급은 하지 않는다. 플레이어가 돌아오기 전에 목재가 소비·이동·분실되면 실제 상태와 원인을 보존한다.
- 정확 한스 발견 시점·생활 일정·목재 사용처·첫 반응·다음 만남 조건·관계 변화량·오디오·WI·Graph Map·Scene은 후속 기획 대상이다. 이번 결정은 코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-534 플레이어는 이동 중 변화된 농장을 먼저 보고 한스의 후속 표식을 선택한다

- 분야별 ID: `D-INTERACTION-EMBODIED-STORY-CHOICE-007`
- 상태: `ConfirmedInteractionDirection / Reuses D-457~D-458 MarkerSemantics` (2026-09-01). 한스가 추가 목재를 발견한 뒤에는 현재 필요한 작은 농장 시설 하나를 실제로 보수하거나 정돈해 농장의 표현 상태를 바꾼다. 플레이어가 다시 농장 주변을 이동할 때 강제 대화·결과 팝업보다 변화된 축사·농기구 보관대·목재 적재 상태를 먼저 관찰할 수 있게 한다.
- 변화는 추가 목재가 실제로 남아 있고 한스가 발견·사용한 결과여야 한다. 단순 장식 교체만으로 목재 소비·수리 완료·관계 변화를 만들지 않으며, 어떤 시설이 바뀌었는지와 사용 목재 수량·남은 적재량을 같은 원인 계보로 보존한다.
- 변화가 생긴 뒤 한스 머리 위에는 선택 가능한 후속 상호작용 표식을 표시한다. D-457~D-458의 현행 의미를 재사용해, 지난 도움의 확인·마감 대화가 가능하면 `!`, 새로운 일반 의뢰가 생기면 파란 `?`, 메인 의뢰가 실제로 열리면 금색 `?`를 사용한다. 여러 상태가 동시에 있으면 통합 표시 안에서 구분한다.
- `!`를 선택하면 한스의 발견·목재 사용·농장 변화와 감사 또는 다음 관계 단서를 확인할 수 있지만 자동 대화·자동 보상·자동 정산은 하지 않는다. 플레이어는 표식을 무시하고 계속 이동할 수 있으며, 변화된 농장 관찰 자체와 한스 대화 열람을 서로 다른 기록으로 둔다.
- 정확 보수 시설·외형 판본·관찰 거리·표식 등장 시점·대사·보상·후속 의뢰·UI 자산·오디오·WI·Graph Map·Prefab·Scene은 후속 기획 대상이다. 이번 결정은 코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-535 여분 목재로 수리되는 첫 농장 변화는 한스의 집이다

- 분야별 ID: `D-PRESENTATION-FARM-HANS-HOUSE-001`
- 상태: `ConfirmedPresentationDirection / RequiresAssetSurveyBeforeE5` (2026-09-01). 한스가 목재 부족 때문에 고치지 못하고 있던 집을 첫 시각 변화 대상으로 삼는다. 플레이어가 두고 간 여분 목재를 한스가 실제로 발견·사용한 뒤, 재방문한 플레이어가 이동 중 이전보다 잘 수리된 한스 집을 먼저 관찰한다.
- 권위 상태는 최소 `수리 필요 → 목재 확보 → 수리 중 → 수리됨`을 구분한다. 목재 적재만으로 집이 즉시 완성되지 않고, 한스의 발견·재료 사용·작업 경과 또는 완료 판정이 이어져야 한다. 수리된 외형은 집의 내구·거주 가능 상태를 표현하지만 Unity 표현이 권위 상태를 직접 변경하지 않는다.
- 표현은 기존 한스 집 후보를 보존하면서 보유 Synty Construction 계열의 비계·사다리·목재 보강재·공사 소품을 `수리 중` 단계의 후보로 조사한다. `수리됨` 단계는 같은 집의 손상 요소 제거·보강된 외형 또는 호환되는 수리 완료 변형을 우선하며, Construction Prefab 존재만으로 정확 조합·치수·지지면·통행·스타일 호환을 확정하지 않는다.
- 플레이어는 수리 전후를 강제 비교 화면으로 보지 않는다. 같은 이동 동선에서 지붕·벽·문 주변의 보강과 비계 감소, 정돈된 목재 같은 큰 차이를 자연스럽게 읽고, 이후 한스 머리 위 `!`를 선택해 지난 도움의 확인 대화를 열 수 있다.
- 정확 손상 부위·필요 목재량·수리 시간·한스 작업 애니메이션·집 Prefab·Construction 후보·단계별 구성·Collider/Bounds·오디오·WI·Graph Map L2/L3·Scene은 전문 자산 조사와 Presentation E4 동결 뒤 정한다. 이번 결정은 실제 Prefab 선정·Scene 배치·Play/Game View·코드·DB·E 승격·commit/push를 승인하지 않는다.

## D-536 한스의 손상된 집은 원본을 보존한 Blender 전용 복사본 후보로 만든다

- 분야별 ID: `D-PRESENTATION-FARM-HANS-HOUSE-002`
- 상태: `ConfirmedPresentationDirection / TechnicallyFeasibleButToolBlocked` (2026-09-01). 로컬 Unity 자산에서 `SM_Bld_Farmhouse_01.fbx`, `SM_Bld_Farmhouse_02.fbx`와 별도 Collision FBX, 대응 Prefab을 확인했다. 따라서 기존 집 메시의 프로젝트 전용 복사본을 Blender로 편집해 지붕 일부가 뚫리고 처마가 손상된 별도 변형을 만드는 것은 기술적으로 가능하다.
- Synty 원본 FBX·Prefab·Material·`.meta`는 수정하지 않는다. 선택된 집 FBX를 프로젝트 소유 가공 경로에 복사해 지붕 면을 제거·재배치하고, 노출 단면·깨진 가장자리·필요한 간단한 내부 차폐를 보완한 뒤 별도 FBX와 새 Prefab으로 조립한다. 원본 재질 슬롯·축·단위·피벗·법선·UV·저폴리 스타일을 최대한 보존한다.
- 손상 상태는 전용 가공본, 수리 완료 상태는 가능한 경우 원래 Farmhouse Prefab을 재사용한다. 수리 중 상태는 손상본 위에 Synty Construction 비계·사다리·판재·방수포 후보를 결합한다. 이렇게 하면 완성 집까지 새로 모델링하는 범위를 피하면서 전후 차이를 크게 유지할 수 있다.
- 지붕이 본체 메시 일부이므로 단순 Renderer 토글만으로 자연스러운 구멍을 만들 수 있다고 가정하지 않는다. 실제 가공 뒤에는 내부가 비어 보이는 문제, 뒤집힌 면·빛샘·그림자, 재질 슬롯, 별도 MeshCollider/간소 충돌, 문·유리 정렬, Bounds·LOD·성능과 Farm 미감을 격리 Prefab에서 검증한다.
- 현재 호스트의 PATH, `Program Files`, 사용자 경로에서 `blender.exe`를 확인하지 못했다. Blender 설치·다운로드를 자동 수행하지 않고, 기존 사용 가능한 Blender 환경이나 별도 승인된 설치 경로가 확보되기 전에는 `실제가공 미착수`로 둔다. 정확 Farmhouse 선택·손상 부위·가공 복사본 경로·라이선스/재배포 경계·Prefab 조립·Game View 승인은 전문 조사와 개발 인계를 거친다.

## D-537 수리된 한스의 집은 경비대 임무를 지원하는 첫 생활 거점이 된다

- 분야별 ID: `D-STORY-ADVENTURER-FIRST-MISSION-009`
- 상태: `ConfirmedStoryDirection / Refines D-534 FollowUp` (2026-09-01). 한스가 집을 수리하고 플레이어와의 신뢰가 성립하면, 한스의 집은 모험가가 Farm·Nature 인근에서 경비대 임무를 준비하고 돌아올 수 있는 첫 작은 생활 거점이 된다.
- 거점에서는 안전 판정이 성립할 때 휴식·기본 회복, 현재 임무와 주변 위험 정보 확인, 허용된 소량 물자 정리, 동행대와 다음 이동 준비를 할 수 있다. 경비대가 맡긴 순찰·호송·침입 흔적 조사·주민 지원 임무에서 출발점이나 중간 복귀점으로 활용할 수 있으나, 임무 승인·완료·보급 지급은 각각 경비대와 실제 행동 기록이 판정한다.
- 한스의 집은 경비대 지휘소·무제한 창고·무료 치료소·플레이어 소유 주택이 아니다. 한스의 생활공간·재고·동선과 플레이어에게 허용된 휴식·적재 범위를 나누고, 위험이 집 가까이 이어졌거나 한스가 부재·부상·거부 상태이면 이용 범위가 달라질 수 있다.
- 이 거점은 한스에게 베푼 도움의 실질적인 관계 결과이자 Nature와 Town 사이의 짧은 임무 순환을 잇는 장소다. 단순 해금 팝업이 아니라 수리된 집 관찰, 한스의 `!` 확인 대화, 이용 허락이 순서대로 성립해야 한다.
- 정확 휴식 효율·회복 범위·보관 슬롯·동행대 대기·임무 게시 방식·한스 생활구역·위험 시 폐쇄 조건·WI·Graph Map L1/L2/L3·Prefab·Scene·Save는 후속 기획 대상이다. 이번 결정은 Blender·코드·DB·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-538 한스는 첫 경계 순찰에서 정체를 숨긴 은둔고수의 면모를 드러낸다

- 분야별 ID: `D-STORY-HANS-HIDDEN-MASTER-001`
- 상태: `ConfirmedStoryDirection / FirstRevealOnly` (2026-09-01). 수리된 집을 거점으로 시작하는 첫 농장 외곽·Nature 경계 순찰에는 한스가 주변 지리를 알려 주는 안내자로 잠시 동행한다. 평소에는 노련한 농부처럼 행동하며 먼저 전투를 지배하거나 자신의 과거를 설명하지 않는다.
- 순찰 중 예상보다 강한 몬스터 무리가 나타나 플레이어 또는 주민의 생명이 실제로 위험해지는 순간, 한스는 짧고 정제된 한 번의 동작으로 여러 몬스터를 밀어내거나 쓰러뜨려 전황을 급격히 바꾼다. 이 장면은 무조건적인 처형 연출이 아니라 위협의 강도·거리·대상과 한스의 실제 행동 결과가 이어져야 한다.
- 한스의 개입은 플레이어의 전투를 반복 대행하는 자동 구제 장치가 아니다. 플레이어가 감당 가능한 상황에서는 지형·흔적·퇴로를 알려 주고 스스로 대응하게 하며, 한스의 결정적 개입은 첫 정체 단서와 정말 위험한 예외 상황에 제한한다. 이후에도 관계·약속·상황에 따라 도움 범위가 달라진다.
- 플레이어는 기술 이름이나 과거를 즉시 알지 못한다. 지나치게 정확한 자세, 농기구나 평범한 도구로도 가능한 힘의 전달, 전투 뒤 담담한 태도와 설명 회피를 통해 `한스는 평범한 농부가 아니다`라는 의문만 얻고, 후속 조사·공동 행동·신뢰로 정체를 알아간다.
- 표현 후보에는 한 번의 준비 동작, 매우 짧은 타격 또는 휘두름, 몬스터들의 방향성 있는 반응, 충격음 뒤 짧은 정적이 필요하다. 정확 무기·기술·과거 신분·몬스터 수와 등급·발동 조건·피해/넉백 규칙·오디오·애니메이션·WI·Graph Map·Scene은 후속 기획 대상이며 이번 결정은 코드·자산 제작·Unity·E 승격·commit/push를 승인하지 않는다.

## D-539 한스가 집 수리를 미룬 이유는 무능이나 체력 부족이 아니라 능글맞은 무심함이다

- 분야별 ID: `D-STORY-HANS-HIDDEN-MASTER-002`
- 상태: `ConfirmedStoryDirection / Replaces D-535 RepairMotivationOnly` (2026-09-01). 한스는 집을 수리할 힘·기술·기본 자원이 전혀 없어서 방치한 것이 아니다. `이 정도면 아직 살 만하다`고 넘기며 우선순위를 낮게 두고 귀찮은 일을 능글맞게 미뤄 온 사람이다.
- 한스가 플레이어를 속여 목재를 얻거나 일부러 불쌍한 모습을 연출한 것은 아니다. 플레이어가 자발적으로 울타리를 고치고, 별도 보상도 요구하지 않은 채 여분 목재까지 조용히 두고 간 성의를 발견하자 이를 무시할 수 없어 자기 손으로 집을 제대로 수리한다.
- 따라서 수리된 집은 플레이어가 한스 대신 모든 노동을 완료한 보상이 아니라, 플레이어의 선행이 한스의 자발적 행동과 상호성을 이끌어 낸 결과다. 소비된 목재와 작업 시간은 실제로 남되, 한스의 체력 고갈·빈곤·무능을 필수 원인으로 만들지 않는다.
- 이 성격은 D-538의 은둔고수 면모와 연결한다. 평소에는 힘을 과시하지 않고 귀찮은 일에 느긋하지만, 사람의 성의나 실제 생명 위험에는 즉시 반응한다. 집 수리와 몬스터 개입 모두 `할 수 없어서 안 함`이 아니라 `평소에는 굳이 나서지 않지만 나설 이유가 생기면 정확히 행동함`으로 읽혀야 한다.
- 정확 말투·수리 착수 시점·혼자 수리하는 범위·도움을 받는 NPC·과거 사연·능력 공개 단계·WI·Graph Map·애니메이션·Scene은 후속 기획 대상이다. 이번 결정은 수리 자원·시간을 없애거나 자동 수리·코드·Unity·E 승격·commit/push를 승인하지 않는다.

## D-540 한스는 감사를 능글맞게 돌려 말하고 식사·잠자리로 답한다

- 분야별 ID: `D-STORY-HANS-HIDDEN-MASTER-003`
- 상태: `ConfirmedStoryDirection / DialogueAndReciprocity` (2026-09-01). 플레이어가 수리된 집을 보고 한스의 `!` 대화를 열면, 한스는 엄숙한 감사 연설보다 `나도 마침 고칠 참이었는데 자네가 재촉한 셈이군`처럼 가볍게 웃으며 성의를 인정한다. 정확 문장은 성우·말투 기획 전 후보로 두되 능글맞은 정서는 유지한다.
- 농담이 플레이어의 노동과 비용을 깎아내리지 않도록, 한스는 실제 행동으로 답한다. 식사 한 끼, 안전한 잠자리, 집의 허용 구역과 주변 정보 이용을 제안해 D-537의 생활 거점을 연다. 이것은 즉시 소유권·무제한 회복·무상 창고를 부여하는 보상이 아니다.
- 플레이어는 대화를 미루거나 제안을 거절할 수 있으며, 감사 대화를 열람하지 않았다고 이미 수리된 집이나 성립한 신뢰가 취소되지는 않는다. 다만 집 이용 허락과 첫 안내는 실제 대화 또는 동등한 확인 절차 뒤에 열린다.
- 정확 대사·식사 효과·잠자리 시간·회복량·허용 구역·동행대 수용·표정·몸짓·오디오·WI·Graph Map·Scene은 후속 기획 대상이다. 이번 결정은 UI·코드·Unity·E 승격·commit/push를 승인하지 않는다.

## D-541 한스 집에는 오래됐지만 잘 관리된 무기 하나가 선택형 정체 단서로 남는다

- 분야별 ID: `D-STORY-HANS-HIDDEN-MASTER-004`
- 상태: `ConfirmedStoryDirection / OptionalEnvironmentalClue` (2026-09-01). 생활 거점으로 열린 한스 집의 허용 구역에는 오래됐지만 지나치게 잘 관리된 무기 하나를 눈에 잘 띄지 않는 위치에 둔다. 강제 카메라·퀘스트 표식·자동 설명 없이 주변을 관심 있게 살펴본 플레이어만 발견한다.
- 무기의 마모와 관리 상태는 오랫동안 실전 기술을 익힌 사람이 계속 손질해 왔다는 인상을 주지만, 발견만으로 한스의 직업·소속·전력·기술 이름을 확정하지 않는다. 플레이어 상태에는 `한스의 과거와 관련 있어 보이는 관리된 무기`라는 관찰 단서만 남긴다.
- 무기는 플레이어가 즉시 획득·장착·판매할 수 있는 전리품이 아니다. 소유자는 한스이며 허락 없는 반출은 허용하지 않는다. 한스에게 물으면 처음에는 능글맞게 화제를 돌리거나 오래된 물건일 뿐이라고 답하고, 이후 신뢰와 사건 기록에 따라 더 깊은 대화를 열 수 있다.
- 이 단서는 D-538의 실제 전투 개입을 대신하지 않는다. 무기를 먼저 봐도 한스의 능력이 증명되지는 않고, 전투를 먼저 봤다면 무기가 과거를 추측하게 하는 추가 근거가 된다. 두 관찰 순서 모두 허용한다.
- 정확 무기 종류·배치 위치·외형·상호작용 거리·관찰 문구·후속 대화 조건·Prefab·WI·Graph Map L1/L2/L3·Scene은 후속 기획 대상이다. 이번 결정은 자산 선정·아이템 지급·코드·Unity·E 승격·commit/push를 승인하지 않는다.

## D-542 한스의 관리된 무기는 검으로 정하고 Synty Prefab 후보를 E4에서 조사한다

- 분야별 ID: `D-PRESENTATION-HANS-WEAPON-001`
- 상태: `ConfirmedPresentationDirection / AssetSurveyRequiredBeforeSelection` (2026-09-01). D-541의 오래됐지만 잘 관리된 무기 종류는 `한스의 검`으로 좁힌다. 정확 외형은 새로 임의 제작하거나 이름만으로 확정하지 않고, 현재 프로젝트가 보유한 Synty 자산의 검·검집·무기 소품 Prefab을 조사해 후보를 만든다.
- 후보 조사는 원본 Prefab·모델·재질·라이선스 경계를 보존하며, 한스의 평범한 농부 외형과 대비되되 지나치게 화려하게 정체를 즉시 폭로하지 않는 검을 우선한다. 오래된 사용감, 지속적인 관리 상태, 집 안 비전투 배치 가능성, 손 장착점·검집·크기·스타일 호환을 구분해 기록한다.
- Presentation E4에는 최소 주 후보·대체 후보·fallback, GUID 또는 안정 자산 식별자, 원본 경로·fingerprint, 집 안 배치 의도, 관찰 거리와 판독 순간, 한스 소유권, 실제 전투 사용 여부의 미정 상태를 남긴다. 후보 목록만으로 Prefab 선택·한스 장착·Scene 배치·E5를 선언하지 않는다.
- 집 안 환경 단서는 검이 실제로 존재하고 관리 상태를 판독할 수 있어야 하지만, 플레이어가 집에서 바로 획득·장착·판매하지 못한다. 첫 경계 순찰에서 한스가 이 검을 사용하는지는 별도 기획 결정으로 남기며 환경 단서와 전투 표현을 자동 결합하지 않는다.
- 정확 Synty 팩·Prefab·검집 조합·손잡이/날 색상·배치 받침·AnimationRole·ActionCue·오디오·WI·Graph Map L2/L3·Scene은 후속 자산 조사 대상이다. 이번 결정은 실제 자산 조사 착수·Prefab 선정·Blender 가공·Unity·E 승격·commit/push를 승인하지 않는다.

## D-543 첫 경계 순찰의 마수 무리는 깊은 숲의 더 큰 위협에 밀려 내려왔다

- 분야별 ID: `D-STORY-FARM-BOUNDARY-THREAT-001`
- 상태: `ConfirmedStoryDirection / CauseUnidentified` (2026-09-01). 첫 농장 외곽·Nature 경계 순찰에서 만나는 마수 무리는 평소부터 농장을 목표로 삼은 고정 습격대가 아니라, 더 깊은 숲에서 아직 확인되지 않은 무언가에 영역을 빼앗기거나 쫓겨 내려온 굶주린 무리다.
- 마수는 밀려난 피해자이기도 하지만 현재 주민·가축·플레이어에게 실제 위해를 가하는 위험이다. 플레이어는 회피·유도·퇴치·전투 가운데 가능한 대응을 선택할 수 있고, 생명 위기가 생기면 한스가 예외적으로 개입한다. 원인을 알았다는 이유로 현재 공격 피해를 무효화하지 않는다.
- 전투 뒤에는 이동 방향이 평소와 다르거나, 등 뒤의 상처·낯선 냄새·부서진 영역 표식·서로 다른 마수가 같은 방향에서 도망친 흔적처럼 `더 깊은 원인이 있다`는 관찰 가능한 단서를 남긴다. 한스는 원인을 단정하지 않고 추적 여부를 다음 선택으로 돌린다.
- 이 무리를 쓰러뜨리는 것은 첫 순찰의 즉시 위험을 줄일 뿐 깊은 숲의 원인을 해결하지 않는다. 후속 Nature 탐사와 경비대 보고, 주민 통행 안전 확인으로 이어지며, 정확 원인은 별도 문답에서 결정한다.
- 정확 마수 종·수·등급·행동·밀려난 원인·흔적·추적 시간창·전투/회피 결과·WI·Graph Map·Prefab·오디오·Scene은 후속 기획 대상이다. 이번 결정은 몬스터 자산 선정·전투 코드·Unity·E 승격·commit/push를 승인하지 않는다.

## D-544 한스의 검은 첫 순찰에서 사용하지 않고 이후의 진짜 위기에 남겨 둔다

- 분야별 ID: `D-PRESENTATION-HANS-WEAPON-002`
- 상태: `ConfirmedPresentationDirection / DeferredCombatReveal` (2026-09-01). 첫 경계 순찰에서 한스는 D-542의 검을 꺼내지 않는다. 평범한 농기구나 현장 도구를 짧고 정확하게 사용해 D-538의 숨은 실력만 드러내고, 검을 든 본래 전투 방식은 훨씬 뒤의 중대한 위기까지 보류한다.
- 집에서 검을 먼저 발견한 플레이어에게는 `왜 저 검을 쓰지 않았는가`라는 의문이 남고, 발견하지 못한 플레이어도 첫 순찰의 비범한 움직임만 기억한다. 두 관찰 순서는 모두 유효하며 검 발견을 첫 순찰 진행 조건으로 만들지 않는다.
- 검의 첫 실전 사용은 한스가 평범한 도구만으로는 보호할 수 없는 위협, 자신의 과거와 책임을 외면할 수 없는 사건, 또는 신뢰하는 이들을 지키기 위해 정체 노출을 감수하는 순간에만 연다. 단순 피해 수치 상승이나 일반 몬스터 반복 사냥에는 사용하지 않는다.
- 첫 순찰의 정확 농기구와 후속 검 사용 사건은 아직 미정이다. 검이 집에 남아 있다면 실제 회수·소지·장착 계보가 필요하고 화면 밖 자동 장착이나 순간 생성으로 처리하지 않는다.
- 정확 첫 순찰 도구·검 첫 사용 장면·발동 조건·전투 기술·AnimationRole·ActionCue·오디오·WI·Graph Map·Prefab·Scene은 후속 기획 대상이다. 이번 결정은 자산 선정·애니메이션 제작·Unity·E 승격·commit/push를 승인하지 않는다.

## D-545 영역을 넓히는 거대 마수는 요동성 적대 조직이 이용하는 전위 위협이다

- 분야별 ID: `D-STORY-FARM-BOUNDARY-THREAT-002`
- 상태: `ConfirmedStoryDirection / HiddenOrganizationUnrevealed` (2026-09-01). D-543의 마수 무리를 밀어낸 직접 원인은 자신의 영역을 넓히는 거대 마수다. 그러나 이 거대 마수는 최종 배후가 아니라, 요동성을 위협하는 조직이 길들이거나 유도해 전위대·하수 전력처럼 이용하는 존재로 둔다.
- 거대 마수에게는 영역 확장·먹이·번식 같은 생태적 동기가 남아 있어 단순한 원격 조종 인형으로 표현하지 않는다. 적대 조직은 그 습성을 이용해 이동 방향과 충돌 시점을 요동성 쪽으로 유도하고, 밀려난 하위 마수와 주민 피해를 간접적으로 발생시킨다.
- 첫 경계 사건에서는 조직의 이름·목표·지휘자를 밝히지 않는다. 비정상적으로 정돈된 유도 흔적, 자연 발생으로 보기 어려운 표식·사슬·먹이 흔적, 서로 다른 마수의 동일 방향 이동처럼 `누군가 생태적 위협을 이용한다`는 단서만 누적한다.
- 플레이어는 처음에는 밀려난 마수 대응과 거대 마수의 영역 경계를 조사하고, 이후 보고·추적·관찰 기록을 통해 거대 마수 뒤의 조직과 요동성 위협을 연결한다. 거대 마수 격퇴와 조직의 계획 저지는 서로 다른 결과이며 하나가 다른 하나를 자동 완료하지 않는다.
- 정확 조직명·세력 목적·거대 마수 종과 등급·지배 방식·첫 공개 시점·요동성 공격 계획·WI·Graph Map L1~L3·자산·전투·Scene은 후속 기획 대상이다. 이번 결정은 몬스터·조직 자산 선정, 전투 구현, Unity, E 승격, commit/push를 승인하지 않는다.

## D-546 첫 Town 공방 경험은 견습의 생산 실패 원인을 직관해 복구를 돕는다

- 분야별 ID: `D-STORY-ADVENTURER-IDEA-SIGHT-006`
- 상태: `ConfirmedStoryDirection / HypothesisBeforeRecovery` (2026-09-01). 플레이어의 첫 Town 양조·연금 공방 경험은 자신이 고급 완제품을 만들어 능력을 과시하는 사건보다, 생산에 실패한 견습 NPC의 작업을 관찰하고 실패 원인을 알아차려 다시 생산할 수 있도록 돕는 사건으로 둔다.
- 직관은 거창한 해설이나 획득 선언보다 작업대·재료 상태·가열·투입 순서를 잠깐 살핀 뒤 `이렇게 하는 것 아닌가?`라고 무심히 짚는 모습으로 드러낸다. 견습은 처음에는 반신반의하지만, 원인 후보를 대조하고 작은 시험 배치를 거쳐 실제 결과로 확인한다.
- 이데아 직관은 정답 후보와 핵심 관찰점을 제공할 뿐 실패한 생산을 즉시 복구하거나 완제품·Recipe·숙련·편린을 자동 지급하지 않는다. 재료·도구·시간·안전 조건을 갖추고 견습이 직접 재현하거나 플레이어와 함께 검증해야 생산 복구가 성립한다.
- 플레이어는 견습의 일을 빼앗거나 공방 운영권을 얻지 않는다. 실패 원인을 함께 확인하고 다음 시도를 가능하게 만드는 것이 첫 관계 기록이며, 이후 직접 제조·NPC 성장·공방 협력·현실 양조 자료의 선택형 상세보기로 확장할 수 있다.
- 정확 주종·물약·실패 원인·대사·시험 배치 수량·재료 손실·직관 신호·편린/스킬 기록·WI·Graph Map·Prefab·오디오·Scene은 후속 기획 대상이다. 이번 결정은 Recipe 확정·생산 코드·공방 소유·Unity·E 승격·commit/push를 승인하지 않는다.

## D-547 허브의 미도착 화물은 플레이 중 모은 단서를 직접 추론해 해결한다

- 분야별 ID: `D-GAMEPLAY-HUB-INFERENCE-QUEST-001`
- 상태: `ConfirmedGameplayDirection / PlayerInferenceRequired` (2026-09-01). 플레이어는 허브에서 예정 화물이 도착하지 않았다는 이상을 먼저 인지해 미해결 관심사로 남기고, 이후 자유롭게 플레이하면서 운송 기록·기사 증언·도로 흔적·날씨·몬스터 활동 등 서로 다른 곳의 단서를 획득한다.
- 단서 기록은 관찰된 사실·출처·시각·대상과 신뢰도 또는 충돌 여부를 보여 주되 `정답 원인`을 자동 조합하거나 추천 화살표로 확정하지 않는다. 플레이어가 단서들을 비교해 원인 가설과 이를 지지하는 근거를 직접 선택한다.
- 모든 단서를 강제로 모으지 않는다. 충분한 근거가 있으면 일찍 추론할 수 있고 더 조사해 불확실성을 줄일 수도 있다. 잘못된 추론은 영구 실패보다 추가 확인·반증·가설 수정으로 회복할 수 있게 하되, 시간 경과로 흔적·화물 상태·도움 가능 범위가 달라질 수 있다.
- 퀘스트 완료는 답안 하나를 고르는 순간이 아니라, 선택한 가설에 맞는 현장 확인·대상 발견·화물 회수 또는 관계자 보고처럼 실제 결과가 확인된 뒤 성립한다. 우연히 정답 문구를 골랐거나 공략을 읽은 것만으로 완료하지 않는다.
- 정확 화물·지연 원인·필수/선택 단서·가설 수·오답 대가·시간창·기록 UI·WI·Graph Map·현실 자료 대응·자산·Scene은 후속 기획 대상이다. 이번 결정은 동적 퀘스트 생성 전체·자동 추론·운송 권위 변경·코드·Unity·E 승격·commit/push를 승인하지 않는다.

## D-548 미도착 화물 문제 예방은 플레이어와 NPC의 회복을 높이고 플레이어의 명상 준비에 가중한다

- 분야별 ID: `D-GAMEPLAY-HUB-RECOVERY-MEDITATION-001`
- 상태: `ConfirmedGameplayDirection / RecoveryCauseSeparated` (2026-09-01). D-547의 미도착 화물을 실제로 찾아 관계자에게 알리고 후속 누락·손상·생명 위험 같은 문제를 예방한 결과가 확인되면, 도움받은 NPC는 안도와 안전 회복으로 개인 회복이 오르고 플레이어도 유의미한 도움을 완수한 효능감으로 개인 회복이 오른다.
- 회복은 단서 화면 열람, 추측 선택, 현실 자료 읽기, 중복 보고만으로 지급하지 않는다. 같은 원인·대상·행위 기록·판본에서 실제 화물 확인과 문제 예방 결과가 성립해야 하며, NPC가 전부 대신 해결했거나 이미 완료된 결과를 다시 제출한 경우 플레이어 회복으로 중복 계산하지 않는다.
- D-459~D-461을 재사용한다. 직접 도움받고 상태가 관측되는 NPC와 실제 참여 플레이어의 개인 회복만 바꾸며 모든 허브 배경 Actor에게 일괄 지급하지 않는다. 공공 문제 완화는 공동체 회복의 별도 기여로 남기고 개인 회복 합산과 같은 원인을 두 번 원시 합계하지 않는다.
- 플레이어의 개인 회복은 이후 명상에 들어가기 좋은 안정 상태를 나타내는 `명상 준비 가중치`의 유리한 입력이 된다. 회복이 높을수록 명상 시작 또는 집중 상태 진입이 조금 수월해질 수 있지만, 명상 숙련 경험을 직접 지급하거나 명상을 자동 시작하고 성공·이데아의 편린 획득을 보장하지 않는다. 관심 분야·현재 집중력·명상 숙련·행동 조건은 계속 독립적으로 판정한다.
- NPC 개인 회복이 플레이어 명상 확률을 직접 곱하지 않는다. NPC 회복은 실제 소속·체류·관측 범위에서 공동체 기본층에 기여하며, 공동체가 개인 회복을 돕는 효과는 Q-197~198의 유지 시간·공동 행동 조건과 상한을 거쳐야 한다. 즉시 `플레이어 회복 → 공동체 회복 → 플레이어 회복`으로 되먹임하지 않는다.
- 정확 회복량·상한·감쇠·효과 지속 시간·명상 준비 곡선·UI/Audio·Save/Replay·WI·Graph Map·코드·Unity는 후속 설계다. 이번 결정은 회복 수치 구현·명상 자동 발동·E 승격·commit/push를 승인하지 않는다.

## D-549 회복이 높은 때의 명상은 실제 획득한 영감과 이데아의 편린 산출량을 증폭한다

- 분야별 ID: `D-GAMEPLAY-MEDITATION-INSPIRATION-001`
- 상태: `ConfirmedGameplayDirection / ExactMultiplierPending` (2026-09-01). D-548로 개인 회복이 높아진 상태에서 플레이어가 명상을 선택하면, 평상시보다 더 많은 영감을 얻을 수 있고 편린 획득 판정이 실제로 성립한 경우에는 획득 편린 수량에도 배수 보정을 적용한다.
- `편린 ×3·×4·×5`는 회복 구간에 따른 단계형 증폭의 방향 예시다. 정확 단계 수·회복 문턱·배수·상한·반올림·분야별 차이는 아직 확정하지 않는다. 낮은 회복에서의 기본 명상 가치도 0으로 만들지 않는다.
- 배수는 기본 편린 획득 판정 뒤 산출량에만 적용한다. 명상 시작·화면 열람·관심 카드 선택만으로 편린을 만들지 않고, 획득 실패를 성공으로 뒤집거나 미완성 관찰 후보를 완전한 지식·스킬·Recipe로 바꾸지 않는다. 여러 개의 편린도 서로 다른 통찰을 자동 발견했다는 뜻이 아니라 같은 분야 성장에 쓰이는 판본화된 산출 단위다.
- 같은 명상 Session·원인 행위 기록·회복 사본·결과 판본에서 한 번만 계산한다. 저장 불러오기·재조회·중복 Confirm·화면 재생으로 배수를 다시 적용하지 않으며, NPC의 회복이나 공동체 회복을 플레이어 배수에 직접 합산하지 않는다.
- 정확 영감/편린 상태 계약, 명상 시간·중단·실패, 일일/Session 상한, 관심 분야 대응, UI/Audio·Save/Replay·WI·Graph Map·코드·Unity는 후속 설계다. 회복 소모 여부는 D-550에서 `소모하지 않고 적절한 명상 완료 뒤 추가 회복`으로 확정했다. 이번 결정은 실제 수치 구현·편린 지급·E 승격·commit/push를 승인하지 않는다.

## D-550 증폭 명상은 회복을 소모하지 않고 적절한 완료 뒤 회복을 더 높인다

- 분야별 ID: `D-GAMEPLAY-MEDITATION-RECOVERY-LOOP-001`
- 상태: `ConfirmedGameplayDirection / ExactGrowthPending` (2026-09-01). D-549의 증폭 명상은 개인 회복을 지불하는 소비 행위가 아니다. 회복이 높은 상태에서 명상을 수행하고 적절한 완료 결과가 성립하면 영감·편린 산출 증폭과 별도로 플레이어 개인 회복도 다시 오른다.
- 사용자에게 보여 줄 증폭 상태 문구는 `증폭(무분별)`로 기록한다. 이 문구의 정확 의미, 노출 위치, 접근성 대체 문구와 `무분별`이 단계명·상태명·경고 중 무엇인지는 후속 UI 문맥에서 확정하며 현재 수치 규칙으로 해석하지 않는다.
- 명상 시작·화면 열기·즉시 취소·실패·저장 불러오기·중복 Confirm은 추가 회복을 만들지 않는다. 같은 명상 Session·원인·결과 판본에 한 번만 지급하며, 의미 없는 짧은 반복으로 `회복 상승 → 편린 배수 → 회복 상승`을 무한 증식시키지 않도록 반복 감쇠·시간창·상한을 별도로 둔다.
- 추가 회복은 명상 숙련 경험, 집중력, 공동체 회복과 별개다. 플레이어 회복 증가가 NPC 회복을 직접 올리거나 공동체 광복기를 자동 확정하지 않고, 명상 완료만으로 편린·완전 지식·Recipe·스킬 획득을 보장하지 않는다.
- 정확 회복량·적절한 완료 기준·반복 감쇠·일일/Session 상한·표시 지속 시간·UI/Audio·Save/Replay·WI·Graph Map·코드·Unity는 후속 설계다. 이번 결정은 실제 수치 구현·E 승격·commit/push를 승인하지 않는다.

## D-551 강적 조우의 정신적 위협에는 전투 중 집중 타이밍으로 회복을 지키고 되올릴 수 있다

- 분야별 ID: `D-GAMEPLAY-COMBAT-MIND-FOCUS-001`
- 상태: `ConfirmedGameplayDirection / ExactCombatProfilePending` (2026-09-01). 플레이어 개인 또는 현재 분대보다 강한 적과 실제로 대치하면 상대 위험 단계·피해·압박·퇴로와 지원 조건에 따라 개인 위협이 오르고 회복이 지속적으로 낮아질 수 있다. 적 이름 색만 보거나 멀리서 발견한 사실만으로 같은 감소를 확정하지 않는다.
- 플레이어는 전투 중 선택형 집중 대응에 참여할 수 있다. 이동하는 목표 구간에 맞춰 클릭·버튼을 누르는 타이밍 입력이 성공하면 진행 중인 회복 하락을 완화하고, 더 좋은 판정은 이미 낮아진 회복 일부를 되올릴 수 있다. 한 번의 성공으로 적 전력·피해·위협 원인을 제거하거나 회복을 즉시 최대치로 만들지 않는다.
- 위험 판정은 D-480~D-482의 개인/분대 상대 위험과 실제 전투 상태 갱신을 소비한다. 플레이어 개인에게 검정이어도 분대에게 주황일 수 있으며, 어떤 기준으로 직접 압박받는지에 따라 정신 효과를 산정한다. 강한 적을 반복 호출·시야 진입/이탈해 회복을 버는 구조는 허용하지 않는다.
- 집중 미니게임은 기존 Q-398과 D-271의 `Perfect/Good/Miss/NoInput/NeutralSkip`·행위 기록 계보를 재사용할 후보지만 전투 전용 Profile은 별도 승인·판본화한다. 실패·무입력·접근성 건너뛰기에 추가 위협 벌점을 얹지 않고 전투 자체에서 발생한 위협·회복 변화만 계속 적용한다.
- 전투 권위 시간과 공격·방어·이동·후퇴 입력을 몰래 정지하거나 대신하지 않는다. 접근성 대체 입력, 표시 빈도, 동시 전투 조작 충돌, 집중 중 피격, 개인/분대 기여, 중단·후퇴·전투 종료 귀환을 실제 Play에서 검증해야 한다.
- 정확 위협/회복 변화량·시간 곡선·적 위험 단계별 Profile·성공 판정 폭·발동 빈도·상한·UI/Audio·Save/Replay·WI·Graph Map·코드·Unity는 후속 설계다. 이번 결정은 전투 수치 구현·자동 승리·E 승격·commit/push를 승인하지 않는다.

## D-552 전투 집중 기회는 위협이 크게 변하는 핵심 순간에만 짧게 연다

- 분야별 ID: `D-INTERACTION-COMBAT-FOCUS-TRIGGER-001`
- 상태: `ConfirmedInteractionDirection / ExactCuePending` (2026-09-01). D-551의 집중 타이밍은 전투 내내 상시 반복 표시하지 않고 강한 공격 예고, 동료의 중상·이탈, 퇴로 차단, 적 증원·격노, 지휘 단절처럼 개인 위협과 회복 흐름이 크게 변하는 순간에만 짧게 연다.
- 기회는 현재 관측 가능한 전투 사건과 같은 판본에 결속한다. 작은 수치 변동·일반 공격마다 띄우거나 일정 시간마다 원인 없이 생성하지 않으며, 화면 밖 숨은 정보와 아직 확정되지 않은 피해를 미리 알려 주지 않는다.
- 집중 입력은 공격·방어·회피·이동과 충돌하지 않는 짧은 별도 입력창을 사용하고, 놓치거나 건너뛰어도 추가 위협 벌점을 주지 않는다. 실제 사건으로 인한 기본 위협·회복 변화는 계속 적용한다.
- 정확 사건 종류·동시 발생 우선순위·예고 길이·입력창·일시정지/감속 여부·접근성 대체·UI/Audio·WI·코드·Unity는 후속 설계다. 이번 결정은 전투 조작 변경·E 승격·commit/push를 승인하지 않는다.

## D-553 강대한 적의 종합 역량은 구성 노드와 지원 연결을 실제로 분리해 낮출 수 있다

- 분야별 ID: `D-GAMEPLAY-COMBAT-DIVIDE-CONQUER-001`
- 상태: `ConfirmedGameplayDirection / GraphCompositionRequired` (2026-09-01). 강대한 적 집단이나 복합 위협은 하나의 고정 전투력 덩어리가 아니라 지휘 개체·호위·원거리/지원 역할·증원·보급/회복 원천·소환/둥지·엄폐·접근 경로 같은 구성 노드와 지휘·지원·이동·증원 연결을 가진 전투 그래프로 표현할 수 있다.
- 플레이어가 정찰·유인·매복·경로 차단·지휘 개체 고립·지원 역할 분리·증원 지연·보급/소환 원천 무력화 같은 실제 행동으로 노드나 연결을 끊으면, 남은 적의 종합 역량·사기·대형·회복·증원 가능성이 다시 계산되어 위험 단계가 낮아질 수 있다. 분리 결과가 실제 상태와 행위 기록에 남기 전에는 화면상 거리나 선택 그룹만으로 약화시키지 않는다.
- 모든 적을 임의로 잘라 약화시키지는 않는다. 단일 거대 마수도 부위·행동 단계·보호 개체·환경 지원·후퇴/회복 원천처럼 실제 독립 가능한 구성만 분할 대상으로 삼고, 분리할 근거가 없으면 하나의 강적 상태를 유지한다.
- 분할은 자동 승리가 아니다. 나뉜 집단이 재집결하거나 다른 경로로 증원할 수 있고, 플레이어가 한 부분을 상대하는 동안 다른 부분이 민간인·보급로·거점을 공격할 수 있다. 시간·위치·정찰 정보·동행대 역할·소모품과 실패·후퇴 대가를 보존한다.
- Graph Map L1은 전투 전의 목적·선택·결과 인과를, L2는 적 구성 노드·지원 엣지와 분리/재결합 규칙을, L3는 기존 전투·분대·경로·행위 기록 계약 참조를 관리한다. Graph Map이 전투 권위나 피해 계산을 대신하지 않는다.
- 정확 노드/엣지 종류·종합 역량 계산·재집결 조건·AI·전투 공간·UI/Audio·WI·Save/Replay·코드·Unity는 후속 설계다. 이번 결정은 새 전투 schema·몬스터 구성·Scene·E 승격·commit/push를 승인하지 않는다.

## D-554 최근 확정 기획의 Synty 표현 요구를 선별해 개발·전문 조사로 인계한다

- 분야별 ID: `D-PRESENTATION-SYNTY-SURVEY-HANDOFF-001`
- 상태: `ApprovedResearchHandoff / PresentationE4Only` (2026-09-01). [최근 기획 Synty Prefab 조사 인계](최근기획-SyntyPrefab조사-개발인계-2026-09-01.md) r1에 한스 집 상태·검/현장 도구·첫 경계 마수, Town 견습 공방, Hub 미도착 화물·안도/명상, 거대 마수·분할 정복 역할·집중 신호의 표현 요구를 P0~P2로 분류했다.
- D-437의 MySQL 보유 자산 14,461개/Prefab 4,221개와 기존 Farm·약초 용기·방문자 Actor·봄 경관 조사 근거를 먼저 재사용한다. 같은 역할·판본을 다시 전수 스캔하거나 모든 Prefab을 새로 촬영하지 않는다.
- 개발은 기존 자산 목록·기능 대장과 작업 명세 결속을 소유하고, 공간 담당은 환경·건물·소품·몬스터 외형/Bounds/Collider, 애니메이션 담당은 Actor·Rig/Avatar·Clip·Controller·접촉/귀환을 조사해 개발에 반환한다.
- 반환은 정확 경로·GUID·팩·fingerprint·이미지 근거와 `그대로 재사용 / 연결·설정 보완 / 격리 가공 필요 / 신규 제작·구매 필요 / 미검사·미확보`를 구분한다. 파일 확인·격리 Preview·실제 Scene/Game View는 서로 다른 증거다.
- 이번 인계는 보유 자산 조사·격리 미리보기·Presentation E4 준비 기록까지만 승인한다. 새 구매/다운로드·원본 수정·Blender 가공·Scene 저장·실제 배치/입력·E5·commit/push는 자동 승인하지 않는다.

## D-555 첫 경계 마수는 동물형·야수형 의미를 유지하고 현재 사람형 후보는 미확보 보류한다

- 분야별 ID: `D-PRESENTATION-FARM-BOUNDARY-BEAST-001`
- 상태: `ConfirmedPresentationDirection / SuitableAssetUnavailableDeferred` (2026-09-01). D-543의 더 깊은 숲에서 밀려 내려온 굶주린 마수 무리는 동물형·야수형 실루엣과 움직임을 유지한다. D-554 P0에서 확인한 `Demon`·`Undead` 사람형 후보를 이름과 전투 가능성만으로 대체 채택하지 않는다.
- 현재 보유 목록의 좁은 조사에서 적합한 동물형 Prefab과 Claw/Bite/Unarmed 동작 결속을 확보하지 못했으므로 이 역할은 `적합 자산 미확보 / 보류`로 남긴다. 이는 전체 보유 자산·향후 구매 시장에 후보가 없다는 전수 부재 증명이 아니다.
- 후속 조사는 보유 팩의 다른 이름·Mesh/Prefab·애니메이션 원천과 구매 가능한 동물형/야수형 팩 후보를 비교할 수 있지만, 구매·다운로드·Import·실제 채택은 별도 승인 전까지 하지 않는다. 임시 사람형 fallback을 실제 정사 화면이나 E4 주 후보로 올리지 않는다.
- 정확 종·크기·무리 구성·Rig/Avatar·이동/공격/피격 Clip·Collider·오디오·WI·Graph Map·Scene은 후속 기획·조사 대상이다. 이번 결정은 자산 구매·제작·Unity 배치·E 승격·commit/push를 승인하지 않는다.

## D-556 게임의 상위 목적은 나와 상대의 오행 순환을 회복시켜 광복기 가능성을 넓히는 것이다

- 분야별 ID: `D-GAMEPLAY-FIVE-ELEMENT-RECOVERY-PURPOSE-001`
- 상태: `CompatibilityPointerOnly / MigratedToPlanningDocument` (2026-09-02). 이 항목은 개별 게임 기획 답변을 결정 원장에 기록했던 호환 이력이다. 삭제·재번호화하지 않지만 현행 기획 기준으로 사용하거나 후속 답변을 여기에 누적하지 않는다. 현재 권위 본문은 `PLAN-GAME-COMMON-PURPOSE-001`의 [게임 상위 목적 — 오행 순환·회복·광복기 r2](게임상위목적-오행순환과광복기-기획-2026-09-02.md)가 소유한다. 아래 내용은 이관 전 당시 기록이며 최신 판본은 기획 문서를 따른다.
- 여기서 상대 또는 `너`는 NPC·동행자·주민·조직·적대자 같은 사람·집단뿐 아니라 손도끼·나무·약초·밭·집·창고·도로 같은 사물·자원·시설과 숲·물길·기상·서식지 같은 환경·공간·생태를 포함한다. 사람의 주체성·동의·거절은 보존하고 사물·환경에 의도·감정을 억지로 부여하지 않는다.
- 오행은 개체마다 다섯 수치를 저장하는 새 상태축이 아니라 기존 괘성의 행위·작용·대상·보조 관계로 필요한 역할과 결손을 읽는 메타데이터다. 적용되지 않는 괘를 억지로 채우지 않고 괘성만으로 회복량·효율·도덕성·광복기를 계산하지 않는다.
- `회복`은 실제 상태 개선과 회복 기여를 뜻한다. 사람은 개인·공동체 회복, 사물·시설은 내구·기능·운영 복구, 환경은 생태·경로·안전·생산 복원처럼 기존 상태를 통해 구체화한다. `회복역량`은 손상 뒤 회복할 수 있는 능력·여지로 별도 구분한다. 플레이어의 명상 역량 성장은 개인 회복역량을 높이는 대표 경로지만 현재 회복을 즉시 채우지 않으며, 정확 변환식·상한·해금 항목과 중복 방지는 별도 Profile이 소유한다. 그 밖에도 숙련·시설·관계·지식·비축 같은 근거와 별도 규칙 없이 회복 결과에 따라 자동 증가하지 않는다. 둘 중 어느 것도 모든 대상에 공통인 새 단일 권위 수치로 만들지 않는다. 적대 상황의 순환 회복도 상대를 무조건 강화하는 것이 아니라 피해 중단·격리·분리·퇴로 확보·생태 관계 복구처럼 맥락에 맞는 결과를 뜻할 수 있다.
- 한 결과는 관점 플레이어, 실제 실행 Actor, 직접 대상, 간접 영향 범위와 회복 판정 대상을 분리한다. 회복 기여는 같은 WI·ActionRecord·Actor·대상·Session·Revision의 권위 결과, 전후 상태 차이 또는 확인된 피해 예방과 판본화된 기여 Profile이 있을 때만 후보가 된다. 위임한 플레이어와 수행 NPC, 직접 대상과 공동체 수혜 범위를 같은 주체로 합치지 않는다.
- 벌목·전투·제작처럼 한 대상의 소모·손실과 다른 대상·범위의 개선이 함께 일어날 수 있다. 대상별 `개선 / 유지 / 손실 / 미확인`을 보존하며 개별 나무 소모를 숲 회복으로, 적대 개체 제압을 그 개체의 회복으로, 재료 소모를 수혜자 회복으로 자동 치환하지 않는다.
- 화면 열람·Preview·취소·중복 Confirm·Save 재조회는 기여 원인이 아니며, 한 사람의 회복이나 한 사물의 수리를 모든 개인·공동체에 중복 합산하지 않는다.
- 개인 광복기와 공동체 광복기는 기존처럼 독립 판정한다. 사물·시설·환경 자체를 심리적 광복기에 넣지 않으며, 이들의 회복 결과가 실제 공동체 문제 완화로 이어진 경우에만 참여자·대상·범위가 있는 공동체 회복 기여 후보가 된다.
- 현재 구현은 플레이어별 `NatureMind`와 개인 기간 전이, 일부 사건의 전체 구성 플레이어 효과 배분까지다. 이것을 사람·사물·환경 전반의 공통 회복 기여나 공동체 집계 구현으로 간주하지 않는다. 정확 대상별 기여 Profile·수치·상한·감쇠·반복 판정·UI/Audio·Graph Map·WI·Save/Replay·코드·Unity는 후속 설계다. 이번 결정은 새 공통 회복 수치·자동 광복기·E 승격·개발 착수·commit/push를 승인하지 않는다.

## D-557 Ssalddel 하나가 운영 API와 Hosted Simulation API를 함께 호스팅한다

- 상태: `Accepted`
- 결정일: 2026-09-11
- 관계: D-027·D-093·D-110의 물리 호스트 분리를 대체하고, D-094·D-105·D-221의 상태 권위·DB·공공데이터 읽기 경계는 유지한다.

장기 실행 서버는 `Ssalddel` 하나만 둔다. Web·모바일·Hosted Unity는 한 기본 주소와 `Ssalddel` 로그인 JWT를 사용한다. 기존 `/api/simulation/v1/*`와 Simulation SignalR Hub 경로는 유지하되, 별도 실행 가능한 `Ssalddel.Simulation.Server`, 전용 포트와 전용 API 컨테이너는 제거한다. Simulation HTTP 조립은 비실행 `Ssalddel.Simulation.Hosting` 모듈이 담당하며 migration·공간 파생 명령은 `eng/Ssalddel.Simulation.Tools` 단발성 CLI로 분리한다.

물리 호스트 통합은 상태 권위 통합이 아니다. 실제 사용자·조직·주문·계약·결제·운영 재고는 기존 운영 원장이 최종 권위를 유지하고, scenario·seed·Simulation Session·가상 시간·WorldTick·save/replay는 Simulation Core와 전용 저장 원장이 소유한다. `SimulationSession`과 `SimulationWorldDerived` DB, 공유 공공데이터 읽기 전용 계정, 명시적 migration과 데이터 계보를 계속 분리한다. 한 프로세스 안에서도 Simulation Command가 운영 효과를 만들거나 양쪽 entity를 같은 원장으로 저장하지 않는다.

모든 Hosted Simulation API는 기본적으로 주 서버 인증을 요구한다. 개인 세션은 로그인 주체와 세션 고유 식별자를 별도 접근 원장에 결속하고 다른 사용자의 세션은 존재 여부를 노출하지 않는다. 온라인 세계의 공유 방은 방 소유자·멤버십 규칙이 별도로 접근을 판정한다. 원격 Simulation 논리 클라이언트는 운영 API와 같은 주소·토큰을 사용하되 이름 있는 별도 Client와 계약을 유지하며, 운영 실패·Simulation 실패·Local Runtime 사이 자동 fallback을 허용하지 않는다.
