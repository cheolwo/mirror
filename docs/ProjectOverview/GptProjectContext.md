# Mirror (거울) AI 공용 프로젝트 컨텍스트

> 이 문서는 GPT Chat과 Codex가 공통으로 사용하는 Ssalddel 프로젝트 컨텍스트다. 장기 정책의 원문을 복제하지 않고 현재 방향, 권위 경계, 구현 상태와 문서 탐색 순서를 요약한다. 세부 내용이 충돌하면 실제 route·contract·test·실행 설정과 아래의 기준 문서를 다시 확인한다.

현재 프로젝트 표시명은 **Mirror(거울)**이며 Ssalddel/살뜰은 이전 이름이다. GitHub는 `cheolwo/mirror`로 변경했다. 이 문서 이하와 코드의 `Ssalddel`은 같은 프로젝트의 기존 내부 식별자를 가리키며, namespace·assembly·package·Save·로컬 폴더·별도 Unity 저장소는 이번에 이름을 바꾸지 않았다. 기준은 [D384](../AI/DECISIONS.md#d-384-프로젝트-표시명과-github-저장소를-mirror거울로-변경한다)다.

## 1. AI가 먼저 기억할 한 문장

Ssalddel은 출처가 있는 정보와 커뮤니티 대화가 명시적 동의를 거쳐 공동 원장과 역할 협업으로 이어지는 서버 중심 플랫폼이다. Unity는 운영 서버 상태를 탑다운 공간·센서·업무 오브젝트로 체험하게 하는 연구 근거 기반 **World Projection Client**이며, 게임 Simulation은 Solo에서 Unity 내부 공통 Local Runtime, Hosted에서 단일 `Ssalddel` 호스트의 Simulation Hosting 모듈이 같은 Core를 실행한다.

Unity는 독립 농장 게임도 아니고 기존 Web 페이지를 3D로 복제한 클라이언트도 아니다.

## 2. 제품 릴리스와 Unity 개발 순서

Ssalddel에는 서로 다른 두 계획 축이 있다.

- **제품 릴리스 축:** 서버와 Web의 기본 공개·운영 우선순위는 현재 0.0 커뮤니티·공공데이터 기반이다. 후속 capability는 각 릴리스 게이트와 운영 요건을 충족할 때만 공개한다.
- **Unity 구축 축:** 특정 제품 버전을 순서대로 3D로 이식하지 않는다. 전체 Ssalddel 도메인을 `World`, `Data`, `Object`, `Interaction`, `Simulation` 관점에서 통합하고, 공통 계약과 검증 가능한 vertical slice의 필요 순서로 구현한다.

따라서 0.0은 Unity 기능 목록의 제한선이 아니다. Unity는 농장·시장·공동 원장·운송·창고 같은 전체 도메인의 공간 구조와 공통 object 계약을 설계할 수 있다. 다만 결제, 계약, 자동 배차, 보관, 정산과 외부 운영 효과는 해당 서버 capability와 권한·운영 게이트가 준비되기 전까지 simulation 또는 비활성 표현으로 유지한다.

Unity의 첫 목표는 전체 로드맵을 한 번에 구현하는 것이 아니라, 실제 데이터와 대표 Web 업무를 재사용 가능한 World Projection 구조로 연결하는 좁은 vertical slice를 완성하는 것이다.

## 3. 시스템별 책임

| 영역 | 책임 | 포함하지 않는 것 |
| --- | --- | --- |
| 운영 서버 | 실제 사용자·조직의 권한, 공개 범위, 검증, 계약·발주·입고·재고·결제 원장, revision, Command, Event·Outbox | 게임 session, 가상 시간, save·replay, Prefab·Scene |
| Simulation Core | 가상 scenario·seed·session·Tick·save·replay와 게임 전용 결과. Solo는 Local Runtime, Hosted는 단일 `Ssalddel` 호스트 내부 모듈에서 실행 | 실제 계약·발주·결제·입고 효과, 운영 DB·entity 공유 |
| Web | 긴 입력, 표, 검색, 관리자 기능, 주소·계좌·결제·증빙 등 민감하거나 정밀한 업무 | 공간 체험을 위한 3D 표현 |
| Unity | 공간 탐색, 실제 상태의 시각화, 관계와 흐름 표현, preview와 확인, 서버 확정 상태 재표시 | 독자적인 운영 원장과 GameObject만으로 확정한 업무 성공 |

운영 상태 변경은 반드시 다음 순서를 따른다.

```text
Unity interaction
  → preview
  → explicit confirmation
  → server UseCase or Command
  → authorization and revision validation
  → persistence and event
  → canonical state re-query
  → Unity presentation update
```

Animation 완료, NPC 도착 또는 버튼 효과만으로 주문·참여·배차·검수·입출고가 성공했다고 처리하지 않는다.

## 4. Unity 데이터 흐름

```text
Server API
  → API Client
  → ApiModel or DTO
  → explicit Mapper
  → Repository and state store
  → Domain or Projection Model
  → UseCase
  → Presenter and ScreenModel
  → SceneController
  → View socket
  → Prefab and Inspector wiring
  → Placeholder or external visual asset
```

서버 상태의 출발점은 EF `DbSet`, MongoDB 원장과 외부 관측이지만, 이를 Unity Controller와 1:1로 대응하지 않는다. 서버가 여러 영속 객체를 권한·공개 범위에 맞는 aggregate projection으로 조합하고, Unity SceneController는 Entity 종류가 아니라 사용자가 한 Zone에서 보는 상태와 수행하는 과업을 기준으로 UseCase를 묶는다. 상세 기준은 [서버 상태에서 Unity World Projection으로의 설계](../Architecture/UnityServerStateToWorldProjectionDesign.md)를 따른다.

핵심 원칙:

1. DTO는 서버 transport 계약이며 View에 직접 전달하지 않는다.
2. Mapper가 schema, 단위, source, freshness와 호환성을 명시적으로 판정한다.
3. Repository는 API Client, Mapper, cache와 마지막 성공 snapshot을 조율한다.
4. 도메인·projection model은 `GameObject`, `MonoBehaviour`, `Transform`, `Animator`, `Vector3`에 의존하지 않는다.
5. UseCase는 조회, 조합, 권한·검증 결과와 사용자 행위를 표현한다.
6. SceneController는 Unity 생명주기와 화면 상태를 조율하되 서버 요청을 `Update`에서 반복하지 않는다.
7. View는 이미 결정된 상태를 Renderer, UI, Animator, NavMeshAgent와 효과로 표현한다.
8. 외부 3D asset은 View의 `VisualRoot` 아래에 연결하며 업무 계약에 asset 이름이나 경로를 넣지 않는다.

## 5. 로딩과 snapshot 정책

Unity 조회 화면은 최소한 다음 상태를 구분한다.

```text
Idle
Loading
Success
InitialLoadError
Refreshing
RefreshError
```

- 최초 조회 실패: 성공 snapshot이 없으므로 빈 상태와 재시도를 제공한다.
- 성공 후 갱신 실패: 기존 object와 마지막 성공 데이터를 유지하고 갱신 오류를 별도로 표시한다.
- 새 ID: 생성한다.
- 기존 ID와 높은 revision: 갱신한다.
- 사라진 ID: 제거한다.
- 동일 ID와 동일 상태: 유지한다.
- 중복 stable ID 또는 낮은 revision: snapshot 전체를 잘못된 입력으로 거부한다.

## 6. Page-to-World 변환 규칙

150개 이상의 Web route를 각각 Unity Scene으로 만들지 않는다. route를 6~8개 World Zone, 공통 Prefab과 재사용 가능한 2D panel로 압축한다.

| 유형 | 의미 | 예시 |
| --- | --- | --- |
| 공간화 | 페이지군을 장소로 표현 | 광장, 농장, 시장, 도심 물류센터, 창고 |
| 오브젝트화 | 기능을 상호작용 물체로 표현 | 게시판, 정보대, 트럭, 팔레트, 지도 테이블 |
| 패널화 | 상세 조회와 비교를 2D UI로 유지 | 가격표, 주문서, 원장 이력, 근거 카드 요약 |
| 행동화 | 확정된 상태나 진행을 행동으로 표현 | 이동, 상차, 하차, 검수, 수확 |
| Web 유지 | Unity 이관의 실익이 적거나 민감한 기능 | 계좌, 결제, 복잡한 주소, 관리자 설정 |

한 route가 여러 유형으로 나뉠 수 있다. 예를 들어 운송 상세의 트럭과 경로는 공간화하고, 진행 상태는 행동화하며, 주소·증빙·정산은 panel 또는 Web handoff로 유지한다.

대표 World Zone:

```text
community-market-square
public-data-hall
farm
cooperative-hall
market-order
urban-logistics-center
warehouse
personal-space
```

첫 presentation scene은 `community-market-square`이며 primitive placeholder로 구조를 먼저 검증한다.

## 7. Placeholder와 외부 asset 경계

외부 asset 구매 전에 다음 공통 wrapper를 primitive로 구현한다.

```text
WorldPortal
InformationKiosk
CommunityBoard
LedgerBoard
InteractionDesk
StatusMeter
FarmTile
ProductCrate
TransportTruck
WarehousePallet
LoadingZone
DetailPanel
```

권장 구조:

```text
SsalddelSensorView
  ├─ stable ID binding
  ├─ revision guard
  ├─ click and evidence-card binding
  └─ VisualRoot
       ├─ 현재: primitive placeholder
       └─ 이후: Synty model 또는 다른 시각 asset
```

Synty 원본 Prefab에 Ssalddel 업무 로직을 직접 넣지 않는다. Synty는 Presentation 계층의 교체 가능한 시각 리소스다. 구매 범위는 장식 선호가 아니라 placeholder로 검증한 object·animation·성능 요구에서 산출한다.

## 8. 센서와 연구 근거

센서 상태는 다음 정보를 보존한다.

```text
SensorId
Revision
SourceType
MeasurementType
Value and Unit
ObservedAt
DataStatus
ConditionCode
RuleVersion
EvidenceCardIds
Confidence and Limitation
```

센서 projection은 물리 장비의 상태, 표시등과 material 표현만 담당한다. raw 측정값을 View에서 다시 판정하지 않고, 서버 또는 승인된 rule이 만든 `Normal`, `Dry`, `Critical`, `Waterlogged`, `Stale`, `Offline` 상태를 표현한다.

근거 카드는 다음을 섞지 않는다.

1. 연구자료가 직접 뒷받침하는 주장
2. Ssalddel이 적용한 판정 규칙
3. Unity의 시각적 번역

각 카드에는 source, 연구 범위, 한계, 적용 rule version과 effective date가 필요하다. 공공데이터에는 출처, 기준 시각, 단위, 통화, 지역, 갱신 주기와 제한을 함께 표시한다.

## 9. 구현 상태를 말하는 법

GPT Chat과 Codex는 상태를 다음 세 범주로 구분해 답한다.

### A. 현재 저장소에서 확인된 구현

- `Ssalddel.Unity` engine-independent local package
- ApiModels, Mapping, DataManager와 stable ID
- 결정적 농업 simulation과 headless tests
- 대표 route 18개의 `PageWorldProjectionCatalog`
- stable-ID/revision 기반 `WorldProjectionReconciler`
- 연구 근거 model·validator
- 물리 센서 projection model
- interaction preview·확인·canonical 재조회 계약
- 도심마트 ScreenModel·simulated 조회 UseCase·validator
- importable Urban Market sample의 SceneController·하위 View socket·primitive scene builder

### B. 사용자 보고를 통해 알려졌지만 현재 체크아웃에서 소스가 확인되지 않은 실행 상태

- 실제 API를 사용한 `UnityWebRequest → Repository → UseCase → Scene Controller → Presenter → WorldBootstrapScene` 흐름
- 실제 관측 데이터 123건의 Unity marker 표시
- 초기 로드와 refresh 오류 정책의 PlayMode 동작

이 항목은 현재 저장소에서 다시 확인하기 전까지 “현재 체크아웃에서 검증됨”이라고 표현하지 않는다. 소스 위치나 실행 증거가 제공되면 A 범주로 승격한다.

### C. 설계 또는 다음 구현 대상

- 제품 Unity project의 presentation assembly와 실제 Scene·Prefab·Inspector 배선
- `CommunityMarketSquare` primitive scene과 top-down 입력·카메라
- 대표 route catalog와 실제 ViewModel·API 연결
- 센서 VisualRoot와 근거 카드 panel
- Unity EditMode·PlayMode, Windows·Android built-player 성능 검증
- Synty asset 구매·import·license·URP·모바일 성능 검증
- Editor Script와 Unity CLI 기반 Prefab 생성·배선 자동화

Urban Market sample source와 Editor builder는 구현됐지만 실제 제품 Unity project에 import·연결된 상태는 아니다. sample 구현과 제품 Scene 통합을 구분한다.

문서에 적혀 있다는 이유만으로 C를 구현 완료로 보고하지 않는다. build/test, Unity runtime, browser, commit, push와 배포는 각각 별도 증거다.

## 10. 데이터 모듈 이관 우선순위

| 우선순위 | 작업 | 완료 기준 |
| --- | --- | --- |
| P0 | stable ID, revision, provenance, freshness, privacy와 Simulation/Operational 계약 고정 | server와 Unity가 같은 의미를 사용하고 fixture가 실제 데이터로 오인되지 않음 |
| P1 | ApiModel·Mapper와 오류·호환성 정책 | 누락 field, 단위, 오래된 데이터와 schema 불일치를 명시적으로 처리 |
| P2 | Repository·cache·마지막 성공 snapshot | 최초 실패와 refresh 실패가 다른 상태로 동작 |
| P3 | 조회 UseCase·Presenter·ScreenModel | Scene과 무관한 테스트로 상태 전이를 검증 |
| P4 | WorldProjection adapter와 stable-ID reconcile | object 전체 재생성 없이 추가·갱신·제거·유지 계산 |
| P5 | SceneController·placeholder View socket | 실제 또는 명시적 simulation 데이터가 primitive world에 표시됨 |
| P6 | 운영 Command 확인·재조회 흐름 | 서버 성공 snapshot을 받은 뒤에만 표현을 확정 |
| P7 | 외부 asset·animation·Editor 자동화 | wrapper 계약과 target platform 성능을 유지하며 외형 교체 가능 |

## 11. AI 작업 규칙

이 프로젝트를 다룰 때 GPT Chat과 Codex는 다음 규칙을 따른다.

1. 먼저 실제 route, contract, test, DI와 실행 설정을 검색한다.
2. 구현, 테스트 검증, runtime 검증, 제안과 사용자 보고를 분리한다.
3. 서버 권위와 Unity 표현 책임을 뒤집지 않는다.
4. 공개 정보, 개인·조직 운영 정보와 민감 정보를 같은 projection에 섞지 않는다.
5. sample, fixture, FakePG와 simulation을 실제 운영 연동으로 표현하지 않는다.
6. 운영 실패를 sample 성공으로 숨기지 않는다.
7. 기존 contract와 wrapper를 먼저 재사용하고 새 abstraction은 책임 혼합을 줄일 때만 만든다.
8. 외부 API 호출, credential 추가, 결제, 배포, asset 구매, commit과 push는 명시적 승인 없이 수행하지 않는다.
9. 관련 없는 작업 트리 변경을 보존한다.
10. 답변 마지막에는 완료한 변경, 검증, 미검증 범위와 남은 위험을 구분한다.

## 12. 기준 문서 읽기 순서

1. [확정 결정](../AI/DECISIONS.md)
2. [현재 작업](../AI/CURRENT_WORK.md)
3. [Unity 운영 데이터 읽기 전용 관찰 안내](../Architecture/Unity운영데이터읽기전용관찰안내.md)
4. [Unity package 구조와 현재 상태](../../Ssalddel.Unity/PROJECT_STRUCTURE.md)
5. [서버 상태에서 Unity World Projection으로의 설계](../Architecture/UnityServerStateToWorldProjectionDesign.md)
6. [Unity 클라이언트 계층 구조 설계](../Architecture/UnityClientLayeredArchitecture.md)
7. [Unity 농업·유통 simulation 제안](../Architecture/UnityAgricultureDistributionSimulationProposal.md)
8. [Unity 원장 World Projection 제안](../Architecture/UnityWorldLedgerProjectionArchitectureProposal.md)
9. [0.0 집중 로드맵](../Versions/v0.0/focus-roadmap.md)
10. [커뮤니티 0.0 기반 제품 원칙](../Architecture/CommunityFoundationV0Policy.md)
11. [업무 실행 책임 모델](../Architecture/BusinessWorkflowResponsibilityModel.md)

## 13. 새 AI 작업 시작용 문구

GPT Chat에서는 이 문서와 `CURRENT_WORK.md`를 첨부하고 다음 문구로 시작한다.

```text
첨부한 `Ssalddel AI 공용 프로젝트 컨텍스트`와 `CURRENT_WORK`를 이번 대화의 기준으로 사용해줘.
기준 문서와 실제 코드가 다르면 실제 route, contract, test와 실행 설정을 먼저 확인하고 차이를 알려줘.
구현됨, 테스트로 검증됨, runtime에서 검증됨, 사용자 보고, 설계·미구현을 구분해줘.
서버는 운영 상태의 최종 권위이고 Unity는 World Projection Client라는 경계를 유지해줘. 게임 Simulation은 Solo의 Unity 내부 Local Runtime과 Hosted의 단일 `Ssalddel` 호스트 내부 Simulation 모듈이 같은 Core를 실행한다는 별도 축으로 판단해줘. 물리 호스트·주소·JWT를 하나로 써도 운영 원장과 Simulation Session·가상 시간·save/replay의 상태 권위와 DB는 합치지 않는다.
현재 요청: [여기에 작업을 작성]
```

Codex는 루트 `AGENTS.md`에서 이 문서, `DECISIONS.md`, `CURRENT_WORK.md` 순서로 진입한다. 작업 후에는 실제 변경과 검증 결과를 `CURRENT_WORK.md`에 반영한다.

## 14. 문서 갱신 조건

다음 변화가 생기면 이 문서를 함께 갱신한다.

- 기본 집중 버전 또는 공개 범위 변경
- Unity project의 실제 위치 확정
- P2 runtime 소스와 현재 core의 결합 완료
- 새로운 canonical contract, state 또는 World Zone 추가
- Unity PlayMode·built player 검증 완료
- 외부 asset 도입 또는 기술 stack ADR 확정

세부 정책은 각 기준 문서에 기록하고, 이 문서에는 GPT Chat과 Codex가 작업 시작 전에 반드시 알아야 할 요약과 링크만 유지한다.
