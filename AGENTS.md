# Ssalddel 공통 작업 지침

이 파일은 모든 AI 도구와 스레드가 항상 알아야 하는 공통 경계와 작업 routing만 정의한다. 폴더별 세부 규칙은 가까운 `AGENTS.md`와 기준 문서에서 필요한 부분만 읽는다.

## AI 공용 컨텍스트

GPT Chat과 Codex는 대화 기록이 아니라 저장소 문서를 공용 기억으로 사용한다. 작업을 시작할 때 다음 순서로 현재 상태를 확인한다.

1. [공용 프로젝트 컨텍스트](docs/ProjectOverview/GptProjectContext.md)
2. 게임 기획이면 [기획 목차](docs/AI/PLANNING.md)에서 현재 기획 ID·판본·상태·상하위 관계를 확인
3. [확정 결정](docs/AI/DECISIONS.md)
4. [현재 작업](docs/AI/CURRENT_WORK.md)
5. 게임 구현이면 [Codex PlayableLoop Goal 상태](docs/AI/generated/codex-playable-loop-goals.md)에서 활성 Goal·WI·기획서·작업 명세를 확인
6. 활성 PlayableLoop의 `planningGate.designDocumentRef` 기획서, 그 기획서가 `Required`로 연결한 `Accepted` 전문 심화 연구, E7 작업 명세
7. 작업 경로에 가까운 `AGENTS.md`와 관련 Architecture 문서

설계실·제작실 사이에서 작업을 인계할 때는 [역할별 인계 진입](docs/Architecture/CodexPlayableLoopGoal운영체계.md#설계실제작실-인계)을 따른다. 서로 다른 계정·데스크톱이 GitHub를 사이에 두고 교대하면 [GitHub 기반 기획·개발 작업실 교대 운영 체계](docs/Architecture/GitHub기반기획개발교대운영체계.md)도 함께 따른다. 설계실은 기존 승인 기획·명세를 준비하고, 제작실은 push된 정확한 commit과 현재 기준선을 점검한 뒤 명시적으로 수용한 범위만 진행한다. 인계 점검 통과는 실행 승인이나 Goal 자동 활성화가 아니다.

현재 계정·모델·장치 이름만으로 작업 역할을 추정하지 않는다. 현재 요청이 기획 작업실이면 제품 코드를 바꾸지 않고 canonical PLAN·`PLANNING.md`·`CURRENT_WORK.md`와 필요한 승인 인계만 갱신한다. 개발 작업실이면 `Approved` 기획과 `ReadyToDispatch` 인계, revision/hash, 작업지시서, 허용 `writePaths`가 일치할 때만 구현한다. 두 작업실의 공용 기억은 push된 저장소 문서와 정확한 Git commit이며 대화 요약이나 로컬 `artifacts/`만으로 개발을 승인하지 않는다.

Unity 개발 순서는 제품 릴리스 버전 순서와 별개다. Unity는 전체 Ssalddel 도메인을 `World`, `Data`, `Object`, `Interaction`, `Simulation` 관점에서 다루되, 실제 구현은 검증 가능한 좁은 vertical slice로 진행한다. 영역 개발에서는 Farm·Hub·City 각각의 독립 완결 slice를 먼저 만들고, 영역 간 운송 경로를 기본 slice로 삼지 않는다. 서버는 운영 상태의 최종 권위이며 Unity의 simulation과 operational data를 명확히 구분한다.

의미 있는 구현이나 설계 작업을 마치면 `docs/AI/CURRENT_WORK.md`를 누적 일지가 아닌 최신 snapshot으로 갱신한다. 변경 파일, 검증한 범위, runtime 검증 여부와 남은 문제를 사실대로 기록한다. 장기간 유지할 결정이 바뀌면 `docs/AI/DECISIONS.md`에 새 결정이나 대체 관계를 기록하고 과거 결정을 조용히 덮어쓰지 않는다.

게임 기획의 본문·질문·분기·미정·판본은 [기획 문서 독립 관리 체계](docs/Architecture/기획문서독립관리체계.md)에 따라 개별 기획 문서와 `docs/AI/PLANNING.md`가 소유한다. 기획 답변 하나마다 새 D를 만들지 않으며, 여러 기획이 공통으로 따라야 하는 장기 원칙·권위·안전·호환 경계만 `DECISIONS.md`에 남긴다. 기존 D 항목은 링크 호환과 이력 보존을 위해 삭제하거나 재번호화하지 않는다.

기획 스레드의 응답은 `[기획 · 분야 · PLAN-* · 판본]`을 먼저 표시하고 `확정 / 미정 / 다음 질문 하나`를 구분한다. 답변 내용은 해당 개별 기획 문서의 판본으로 기록하고 `PLANNING.md`에는 현재 판본·상태·관계만 갱신한다. 일반 게임 기획 선택을 새 `D-###`로 만들거나 그렇게 기록했다고 보고하지 않는다. 이미 D로 잘못 들어간 기획 답변은 삭제하지 않고 호환 이력 포인터로 남기며 현행 기획 문서를 가리킨다.

## 우선순위와 적용 범위

1. 시스템/개발자 지침과 현재 스레드의 최신 사용자 요청을 먼저 따른다.
2. 작업 대상 폴더에 더 가까운 `AGENTS.md`가 있으면 함께 읽고 그 범위에서는 가까운 지침을 우선한다.
3. 별도 지시가 없으면 [0.0 커뮤니티·공공데이터 집중 로드맵](docs/Versions/v0.0/focus-roadmap.md)을 기본 우선순위로 삼는다.
4. 문서와 코드가 다르면 실제 route, contract, test, 실행 설정을 확인하고 차이를 알린다. 추측으로 한쪽을 덮지 않는다.
5. 과거 대화나 오래된 선호보다 현재 저장소 문서와 최신 사용자 요청을 우선한다.

## 경로별 routing

| 작업 경로 또는 종류 | 먼저 읽을 지침 | 핵심 기준 |
| --- | --- | --- |
| `Ssalddel/` server·API·Event | `Ssalddel/AGENTS.md` | 업무 실행 책임, Command/Event, metadata |
| `Ssalddel/Controllers/Common/`, `Ssalddel.Community/`, `Ssalddel.Contracts/Common/Community/` | `Ssalddel/AGENTS.md`, `Ssalddel.Community/AGENTS.md` | Common에 포함되는 커뮤니티 API·contract·규칙 경계 |
| `Ssalddel/Controllers/Platform/` | `Ssalddel/Controllers/Platform/AGENTS.md` | 업무 공통과 구분되는 플랫폼 기술 API |
| `Ssalddel.Ui.Common/` 공통 UI | `Ssalddel.Ui.Common/AGENTS.md` | 3단계 navigation, MVVM, render 검증 |
| `Ssalddel.Tests/` test | `Ssalddel.Tests/AGENTS.md` | filter 우선, 영향 project build |
| `docs/` 문서·변경 기록 | `docs/AGENTS.md` | 기준 문서 단일화, link·diff 검증 |
| 여러 project를 통과하는 기능 | `SsalddelCodeMetadataAttribute`, `SsalddelCodeFeatureKeys` 검색 | `StepKey`, `FlowOrder`, `Layer`, `ExecutionStage`, `ReadsFrom/WritesTo`, `Effects`, `Boundary` |
| 운영·Simulation·Unity 책임 분류 | `docs/Architecture/OperationsSimulationUnity작업흐름분리.md`, `eng/work-areas/responsibility-workstreams.json` | 주 상태 소유자를 먼저 고르고 `operations/*`, `simulation/*`, `unity/*`로 짧게 진행하며 계약·Adapter만 `integration/*`로 분리 |
| 서로 다른 계정·데스크톱의 기획→개발 Git 교대 | `docs/Architecture/GitHub기반기획개발교대운영체계.md` | 역할을 작업별로 선언하고, 기획 우선순위와 개발 준비 상태를 분리하며, push된 정확한 branch·commit·PLAN revision/hash·작업지시서로만 교대 |
| Simulation·Unity 탐색 | `eng/work-areas/simulation-unity.json`, `docs/AI/generated/simulation-unity-code-map.md` | 생성 트리에서 기능 키와 핵심 단계를 고른 뒤 소스로 이동하고, 생성 문서를 직접 수정하지 않음 |
| 역세권 디오라마 자료·조립·표현 | `docs/Architecture/역세권디오라마증거진화체계.md`, `eng/execution-ledgers/station-diorama-evidence-rules.json` | 출처와 반례를 연결하고 작업 종료 시 규칙 후보를 판정한다. `Candidate`까지 기록할 수 있지만 공통 적용·승격은 사람 승인을 요구하며 E 단계는 자동 승격하지 않음 |
| 커뮤니티 0.0 | `[SsalddelCommunityV0Module]` 검색 | module catalog와 `0.0-A~E` |
| 기능 slice 작업 | `docs/Architecture/FunctionalWorkAreaPartitioning.md`, `eng/work-areas/<slice>.json` | manifest의 `readFirst`, `sourceRoots`, `excludedRoots`만 먼저 읽고 범위를 넓힐 때 이유를 남김 |
| 지역문화·공공데이터 | `eng/work-areas/regional-culture-public-data.json` | 문화 이미지·지역 key·공식 근거·가격 관측 |
| 댓글·프로필·공개범위 | `eng/work-areas/community-foundation.json` | 0.0 참여·개인정보·신고 보호 |
| 개별 의향·비용 검토 | `eng/work-areas/individual-intent.json` | 0.5 비구속 의향, 결제·발주 금지 |
| 공동구매 | `eng/work-areas/group-purchase.json` | 1.0 별도 동의와 공동 원장 |
| 같이 수입 준비 | `eng/work-areas/trade-readiness.json` | 1.5 전문가 확인과 실행 금지 |

## 시작과 인계

- 시작할 때 `git status --short --branch`를 확인하되, 대규모 결과는 항목 수와 관련 경로만 요약한다.
- 기존 변경은 다른 스레드나 사용자의 작업일 수 있다. 관련 없는 변경을 되돌리거나 정리하거나 함께 stage하지 않는다.
- 같은 파일에 기존 변경이 있으면 patch 직전에 다시 읽고 최소 범위로 합친다. 안전하게 병합할 수 없을 때만 묻는다.
- 검색은 `rg`를 우선하고 `bin`, `obj`, `.vs`, `vendor`, `artifacts`, 생성 산출물과 중첩 worktree를 제외한다.
- 한국어 파일은 UTF-8로 읽고 쓴다.
- 종료할 때 완료한 일, 변경 파일, 실행한 검증, 남은 위험을 짧게 남긴다. 하지 않은 commit·push·배포를 했다고 표현하지 않는다.
- 역세권 디오라마 자료·조립·표현 작업은 종료할 때 출처 기록과 기존 규칙의 지지·반례·무효화 여부, 새 `Observed`·`Candidate`를 `station-diorama-evidence-rules.json` 형식으로 점검한다. 후보가 없으면 `새 디오라마 규칙 후보 없음`을 보고한다. 후보 기록은 구현 범위를 넓히거나 `ProvisionalSharedRule`·`AcceptedSharedRule`·E 증거를 자동 승격하지 않는다.

## 제품과 운영 경계

- Ssalddel의 중심은 **정보 공개형 커뮤니티**다. 대화와 모집이 공동 원장·다이어그램으로 이어지고 업무 도구는 그 위에 붙는다.
- 현재 기본 공개 기준은 `0.0` 커뮤니티·공공데이터 기반이다. 개발은 `3.5` 전체 제품 범위의 페이지·원장·상태 전이를 세로로 계속 완성할 수 있으며, 현재 요청에 대상 버전이 없을 때의 기본 우선순위만 `0.0`으로 둔다. `0.5` 이후 기능은 준비된 capability부터 릴리즈 게이트에 따라 단계적으로 공개하고, 전용 검증 profile과 운영 요건 없이 기본 노출·외부 효과를 켜지 않는다.
- 최종 배포 목표는 `3.5` 마트·도심 물류다. 현재 완성 단계 `0.0`과 최종 목표를 혼동하지 않으며, 새 페이지는 [전체 로드맵 조화형 페이지 원칙](docs/Architecture/WholeRoadmapPagePrinciple.md)에 따라 `0.0`부터 `3.5`까지의 stable ID, 원장과 역할 인계를 고려한다.
- 유상 화물 추천, 자동 배차, 계약 중개, 운임 수취, 보관, 정산은 허가·제휴·법률·운영 준비 전 기본 비활성이다.
- 실행 효과는 `SsalddelExecution:Mode`의 `Simulation`과 `Operational` 경계로만 통제한다. 별도 실행 모드를 화면이나 API마다 만들지 않는다.
- 개발 검증은 sample data, FakePG, 모의 흐름을 사용한다. 운영 저장이나 API 실패를 sample fallback으로 숨기지 않는다.

제품 경계는 [커뮤니티 0.0 기반 제품 원칙](docs/Architecture/CommunityFoundationV0Policy.md), 개발 철학은 [이웃에서 시작하는 공동행동 개발 철학](docs/Architecture/NeighborCenteredDevelopmentPhilosophy.md)을 따른다.

## 공동행동과 개인정보

- 종교, 국적, 언어, 가족 형태, 경제력은 가입·노출·신뢰 점수·검색 순위·역할 자격의 대리 지표로 쓰지 않는다.
- 관심, 참여, 연락처 공개, 가원장, 실원장, 실행은 명시적 동의와 철회 가능한 상태로 분리한다.
- 절감액과 편익뿐 아니라 비용, 노동, 위험, 담당자와 계산 근거를 함께 드러낸다.
- 지리적 가까움은 수요 집계와 물류 효율 후보로만 사용하고 자동 가입·알림·상대 선택·배차·계약 확정 근거로 쓰지 않는다.
- 효율이 낮은 참여자를 숨기거나 배제하지 않고 모집권·시간창·집결 방식·자격 사업자 참여 같은 대안을 표현한다.
- 업무 로그, 공동 원장, 커뮤니티 이야기, 친구 후보 기록, 친구 요청·수락과 연락처 공개는 서로 분리된 상태로 관리한다. 세부 기준은 [업무 경험에서 친구 요청으로 이어지는 커뮤니티 설계 기준](docs/Architecture/FriendRequestCommunityDesignStandard.md)을 따른다.
- API key와 secret은 source, tracked config, 로그, capture에 넣지 않는다.

## 공통 아키텍처

- 기본 방향은 `화면 -> Controller API -> UseCase/Command -> Domain/Infrastructure -> DB/Event/Outbox`다.
- 영속 상태는 API/UseCase/Command가 권한과 현재 상태를 검증한 뒤 변경한다. engine은 후보·점수·분류를 반환할 뿐 확정하지 않는다.
- MongoDB 원장은 유연한 업무 원본, RDB는 권한·조회·정산·보고·안정 투영을 맡는다.
- Event/Outbox 동기화는 재처리 가능하고 멱등해야 하며 순환 발행을 막는다.
- 여러 앱이 같은 업무 의미로 쓰는 contract는 `Ssalddel.Contracts`, 공통 업무 UI와 workflow는 `Ssalddel.Ui.Common`에 둔다.
- `Common`은 기술 재사용 집합이 아니라 01~05가 함께 수행하는 업무 경계다. 커뮤니티 탐색, 참여, 공동 원장, 친구 요청·수락, 상품과 신뢰 환류처럼 여러 역할이 같은 업무 의미로 사용하는 API·contract·UI를 포함한다.
- version·Feature bootstrap, push installation, file transport, 외부 callback처럼 업무 의미 없이 실행 환경을 지원하는 API는 `Controllers/Platform`에 둔다. 여러 앱이 호출한다는 사실만으로 `Common`에 넣지 않는다.
- 커뮤니티 공개 API는 `Ssalddel/Controllers/Common`, 공유 contract는 `Ssalddel.Contracts/Common/Community`, 저장소와 무관한 커뮤니티 규칙은 `Ssalddel.Community`에 둔다. 물리 project는 의존성 경계를 위해 유지한다.
- 새 Controller, DTO, Entity, abstraction보다 기존 route, UseCase, metadata, contract, value object, shared component를 먼저 재사용한다.
- 개인정보 암복호화는 domain property가 아니라 persistence/infrastructure 경계에서 처리한다.

세부 층위는 [업무 실행 책임 모델](docs/Architecture/BusinessWorkflowResponsibilityModel.md), Event 경계는 [Command/Event 리팩토링 원칙](docs/Architecture/CommandEvent리팩토링원칙.md)을 따른다.

## 코드 명명 언어

- 모든 코드에서 기술 역할은 영어, 업무·도메인 의미는 한국어로 쓴다. `국내공동구매협의Controller`, `생산자후보검색`, `_공공데이터조회UseCase`처럼 `한국어 업무명 + 영어 기술 역할`로 조합한다.
- 상세 용어표, 적용 대상, 외부 표준 예외와 기존 contract 호환 규칙의 단일 기준은 [코드 탐색 메타데이터의 코드 명명 언어](docs/Architecture/SsalddelCodeMetadata.md#코드-명명-언어)다.
- 새 코드와 수정하는 코드에 기준을 적용하되, 기존 이름의 광범위한 변경은 기능 단위로 나누고 공개 API·직렬화·Event·metadata·저장 데이터의 호환성을 검증한다.

## 사람이 읽는 Unity 용어와 출력 언어

- Unity·서버 통합 작업의 문서, README, 주석, 진행·완료 보고와 검증 설명은 [Unity 프로젝트 한국어 중심 용어·출력 지침](docs/AI/UnityKoreanTerminologyGuide.md)을 따른다.
- 프로젝트 개념은 `구성 대장`, `업무 흐름`, `배치 객체`, `고유 식별자`, `데이터 연결`, `상태 사본`, `관점별 조회 결과`, `데이터 계보`, `인계`, `배치 검증 기록`처럼 한국어를 먼저 쓴다.
- Unity·C#·표준 고유 기술명과 기존 클래스명·고유 식별자·API 계약·JSON 필드·저장 값은 유지한다. 처음 한 번 의미 설명이 필요할 때만 영어를 병기한다.
- O0~O6은 코드만 단독 보고하지 않고 후보, 준비, 모판 실행 검증, 실제 World 배치 검증의 의미를 함께 적는다.
- 서버의 최종 사실, 역할별 해석 결과, Unity 표현을 분리해 설명한다. Unity 화면이나 애니메이션을 실제 업무 완료 근거로 표현하지 않는다.

## 구현 단위

- 전문 작업의 기본 인계는 `기획 → 개발 → 분야별 담당 → 개발 통합·검증 → 기획 반환`이다. `개발`은 Simulation·WI 구현과 통합을 겸임하고, `월드·공간·배치`·`애니메이션` 담당은 독립 산출물·시험·차단을 개발에 보낸다. [Goal 운영 체계](docs/Architecture/CodexPlayableLoopGoal운영체계.md)의 역할·소유권·반환 양식을 따르고 [개발 통합 상태판](docs/AI/개발통합상태판.md)에서 실제 통합·미통합·기획 결정을 구별한다. 모든 담당 완료를 기다리지 않으며 개별 시험 성공만으로 통합이나 E 승격을 선언하지 않는다. 이것은 작업상 인계 관계이지 앱 스레드 이동·새 스레드 생성·자동 실행 권한이 아니다.
- PlayableLoop 기획 스레드는 [PlayableLoop 문답 정밀화 체계](docs/Architecture/PlayableLoop문답정밀화체계.md)의 D-398 단일 질문 심화를 기본으로 사용한다. 기존 결정의 중복을 확인한 뒤 지금·여기·나·너·이렇게의 상황에서 핵심 질문 하나와 추천·대가를 제시하고 답변에 따라 다음 질문을 이어간다. D-353/D-397의 10개 기본만 대체하며 과거 묶음 승인 해석은 보존하고 사용자가 다시 요청할 때만 묶음을 사용한다. 추천안은 승인 전 `Asked`이며 다른 미답변까지 승인한 것으로 해석하지 않는다. 승인 뒤 기획 revision·hash·작업 명세·담당/경로·검증 상한을 결속해 기존 개발 통합 스레드로 인계한다. 기존 Q/의미 ID를 보존하고 새 전체 Q 번호를 임의 배정하지 않으며 예상 E 준비와 실제 Logic·Presentation·통합 E를 구분한다. 정차·대기 중 검토 원칙을 유지한다.
- 승인된 기획이 공간·관계·표현·코드 결속에 영향을 주면 [Graph Map 기획 인계 순환 체계](docs/Architecture/GraphMap기획인계순환체계.md)를 따른다. 기획 스레드는 승인 판본·hash·지금·여기·나·너·이렇게·결과·다음 선택과 Graph Map 영향만 인계하고 Graph Map 원본·생성물·검사 도구를 직접 수정하거나 중간 진행을 기다리지 않는다. Graph Map 담당은 기존 안정 ID를 먼저 대조해 레벨 1 플레이 관계, 레벨 2 배치 제약, 레벨 3 코드·컴포넌트 결속을 필요한 범위만 반영하고 `Integrated`·`Blocked`·`NoImpact` 최종 결과를 기획에 반환한다. 누락된 플레이 의미를 임의로 만들거나 계획 반영을 Unity Scene·실제 입력·E 승격으로 확대하지 않는다.
- Graph Map 결과를 구현으로 넘길 때는 [Graph Map 개발 인계 체계](docs/Architecture/GraphMap개발인계체계.md)에 따라 한 PlayableLoop·대표 WI와 검증 가능한 노드·엣지·제약의 작은 slice를 고른다. Graph Map 담당은 기존 Goal·`Approved` 기획 관문·E7 작업 명세·work item 재사용 후보와 판본을 결속해 `ReadyForDevelopment`까지만 만들고 작업을 자동 활성화하지 않는다. 개발은 현재 파이프라인·파일 소유·정확 쓰기 경로·검증 상한을 다시 확인한 뒤 수용하며, 코드·시험·Runtime·Game View·Save/Replay·서버 연결을 분리해 최종 결과를 Graph Map과 기획에 반환한다.
- 게임 개발 스레드는 대화 기억이나 대기열 제목만으로 다음 기능을 구현하지 않는다. [주체·상호작용 중심 개발 체계](docs/Architecture/주체상호작용중심개발체계.md)에 따라 주체 기반을 먼저 확립하고, `Approved` 기획서·기획 revision/hash·승인 근거·WI 하나의 E7 작업 명세와 현재 파이프라인 차단을 읽은 뒤 그 범위만 구현한다. 기획서가 없거나 hash가 다르거나 주체 역할·직접 결과·핵심 선택·대가·실패·회복·귀환이 `미정`이면 구현을 시작하지 않고 기획 관문으로 돌려보낸다. 건물·공간·배치·애니메이션처럼 구체 결정이 플레이 경험을 좌우하면 [PlayableLoop 전문 심화 연구 분기·재결속 체계](docs/Architecture/PlayableLoop전문심화연구분기재결속체계.md)에 따라 `Required` 연구를 분기하고, `Accepted` 기준선이 기획서와 작업 명세에 다시 결속되기 전에는 해당 범위를 구현하지 않는다. 구현 중 플레이어 약속이나 연구 기준선을 바꿀 필요가 생겨도 개발 스레드가 조용히 결정하지 않으며, 현재 Goal에 발견 사항과 가장 이른 재개 E를 남기고 연구·기획 revision 재승인을 기다린다.
- 게임 작업은 [플레이어 중심 게임 개발 업무 구조](docs/Architecture/플레이어중심게임개발업무구조.md)에 따라 플레이어가 보는 상황·욕구·선택·대가·재료 획득·조립·결과·다음 선택을 먼저 적는다. Goal 하나는 준비된 주체 사이의 WI 하나를 소유하고 직접 결과를 파생 작용보다 먼저 닫는다. 여러 WI가 성공·실패·회복·귀환을 함께 검증해야 할 때만 [플레이 폐루프와 증거 묶음 개발 체계](docs/Architecture/플레이폐루프와증거묶음개발체계.md)의 PlayableLoop를 선택적 검증 묶음으로 연결한다. 시험·Runtime·화면·Hosted·운영 증거는 범위·제외·revision·무효화 조건을 가진 EvidencePackage로 분리한다. 이 관점과 연결 객체는 E/G/H/WI를 대체하지 않으며 Unity 또는 플레이어에게 상태 권위를 주지 않는다. 플레이어는 H1 조립을 선택하고 H2·H3 성장을 유도할 수 있지만 상위 공간 성립은 Simulation 규칙이 판정한다.
- 각 WI Goal은 [플레이 폐루프 논리·표현·결과 순환 개발 방법론](docs/Architecture/플레이폐루프논리시각이중순환체계.md)에 따라 `Logic`과 `Presentation`을 따로 판정하고 통합 E를 낮은 값으로 계산한다. 선택적 PlayableLoop를 연결한 Goal은 묶음 폐쇄성도 함께 판정한다. 결과는 별도 성숙도 축이 아니라 권위 결과·표현 결과·회복·귀환이 같은 revision에서 이어지는 통합 관문이다. 표현 검증에서 상태나 H 계약 누락이 드러나면 같은 Goal을 유지한 채 가장 이른 논리 E를 다시 연다.
- 표현 궤적 E4~E7을 승격하기 전에 `eng/execution-ledgers/playable-loop-presentation-validation-modules.json`의 공통 모듈과 해당 폐루프 기능 프로필의 조건 모듈을 결정하고 `manage-playable-loop-presentation-validation.ps1 -Mode Validate`를 통과시킨다. 자동 사전 검증과 실제 Play Mode·Game View 증거는 서로 대신하지 않으며 차단 실패는 `openFeedbackItems`에 가장 이른 재개 E로 남긴다.
- 공간·물체·소품·Actor·UI 시각 자료가 필요한 WI의 Presentation E4는 플레이어 판독 순간, H 적용 여부, `VisualKey`, 주·대체·fallback 자산 후보, 배치·`InteractionAnchor` 의도, 후보 revision·fingerprint와 E5 준비 상태를 작업 명세의 `presentationE4Preparation`으로 남긴다. 비공간·비시각 WI는 사유 있는 `NotApplicable`을 사용한다. 이 사전 조사만으로 E5를 선언하지 않으며 E5는 동결된 후보를 실제 Prefab 또는 대체 표현·World 배치·활성 Renderer/Collider/Bounds와 WI 발현에 결속한다.
- Synty 자산을 쓰는 WI는 [플레이 폐루프 Synty 표현 모듈 체계](docs/Architecture/플레이폐루프Synty표현모듈체계.md)를 따른다. 신규 6팩을 Area나 권위 상태로 해석하지 않고 13팩 기능 대장과 `needs-review` 활용 프로필에서 후보를 고른다. Actor 동작은 Prefab 준비와 별개로 `AnimationRole`·`ActionCue`·Clip 후보·Rig/Avatar·root motion·Controller·중단/귀환·fallback을 Presentation E4에 기록한다. E5는 실제 World 객체와 권위 상태에 결속된 최소 판독 표현까지를 요구하며 정적 자세·진행 표시·상태 교체 fallback을 허용한다. 실제 대상 Rig·AnimationClip·입력·전이·중단·귀환과 권위 불변의 동작 검증은 E6에서 수행하고, E7은 실제 입력·Game View 완주를 별도로 검증한다.
- WI가 여러 엔진을 거쳐 표현·귀환되는 폐루프는 `eng/execution-ledgers/playable-loop-engine-interaction-validation.json`의 순서 프로필과 `manage-playable-loop-engine-interaction-validation.ps1 -Mode Validate`를 사용한다. 이 검사는 `Logic`·`Presentation`을 대체하는 세 번째 성숙도 축이 아니라 같은 WI·Command·Revision 인계를 확인하는 통합 관문이며, 표현 엔진은 권위 Revision을 변경할 수 없다. 권위 변화 WI는 `ActionRecordAppend`를 반드시 남기고 플레이어 분야 성장은 `PlayerProgressionApply` 또는 사유 있는 `NotApplicable`로 판정하며, 표현 E5 이상은 해당 행위 기록 또는 같은 Revision 상태 사본의 read 증거를 요구한다.
- 장기 Codex Goal은 [Codex PlayableLoop Goal 운영 체계](docs/Architecture/CodexPlayableLoopGoal운영체계.md)에 따라 각각 WI 하나만 소유한다. 전역·스레드별 Goal/WI WIP 고정 상한은 두지 않는다. 신규 Goal은 `eng/execution-ledgers/subject-interaction-development.json`, 기존 Goal과 작업은 `eng/execution-ledgers/codex-playable-loop-goals.json`의 호환 투영 및 생성 상태판에서 담당·기준 revision/hash·수정 경로·공유 계약·실제 선행 의존성·승인·E 상한을 확인한다. 서로 다른 WI Goal과 같은 WI의 비중첩 하위 작업은 병렬 진행할 수 있지만 같은 파일·공유 계약의 쓰기는 소유자를 정해 조율한다. `개발` 스레드가 최종 통합을 담당하며 호환되는 결과의 묶음 통합·병렬 검증을 허용한다. 다른 작업의 활성 여부나 개수만으로 차단하지 않는다. 새 Goal은 승인된 기획 관문과 `Ready` 주체 결속을 먼저 통과해야 하며 선택적 PlayableLoop가 없어도 등록할 수 있다. 대기 WI 자동 활성화나 Evidence 자동 승격은 하지 않는다. `AreaAggregate`·`WorldAggregate`는 필수 자식 상태에서 파생하며 Goal로 직접 활성화하지 않는다.
- 의미 있는 새 게임 기능·규칙·공간·저장 변경은 구현 전에 [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](docs/Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md)의 E7 수직 작업 명세 v2를 만든다. 작업 명세는 준비된 주체·활성 WI와 선택적인 PlayableLoop, `Logic`·`Presentation` 각각의 E7→E1 영향 검토, E1→E7 조립·검증, 통합 관문을 기록한다. 표현 E1~E4 계획은 논리 E5 전에 진행할 수 있지만 표현 E5 이상 증거는 논리 E5 상태 사본을 요구한다. 새 영향이나 잘못된 가정이 발견되면 같은 Goal과 작업 명세에서 대상 궤적의 가장 이른 E를 다시 열며 안정 상태 또는 명시적 차단까지 왕복한다.
- WI Goal의 수직 성숙도는 E7에서 끝난다. 기존 PlayableLoop 결속 Goal의 E8은 같은 frozen revision의 반복 결정성·Save/Replay·Local/Remote·실제 입력 재진입을 검증한다. Loop 없는 Goal이 E8을 필요로 하면 별도 WI 안정성 캠페인을 명시적으로 등록하고, E7만으로 자동 생성하지 않는다. E9는 E8을 통과한 같은 영역의 독립 결과 둘 이상을 `AreaHarmonySet`으로 묶어 논리·표현 조화와 사람의 인과·선택 결과·판독·피드백을 함께 평가·승인하며, E10은 불변 후보의 제한 운영 관찰이다. E8·E9 발견 사항은 관련 E1~E8의 가장 이른 책임을 다시 여는 왕복을 유지하며 자동 시험만으로 E9를 승격하지 않는다. NPC 생활 연속성은 관련 E9 묶음의 논리·표현 조건 모듈이고, 변경 영향·Migration·호환·회귀는 단계 번호와 분리된 교차 책임이다. 과거 `.e9-work-order.json`과 E8 NPC·E9 변화 적응 자료는 판본화된 읽기 호환 자료이며 활성 작업 명세나 현재 E8·E9 증거로 사용하지 않는다.
- Solo는 `LocalProcess`, Hosted는 `RemoteHost`에서 같은 Simulation Core를 실행하도록 작업 명세에 적는다. H 의미 계층과 배치 통제 계층을 분리하고 canonical `SimulationWorldShell` 외의 새 공식 Scene을 만들지 않는다.
- 원장 또는 업무 node 하나를 골라 저장, 상태 전이, Event, API, UI, test까지 세로로 완성한다.
- Farm·Hub·City는 서로 선후행 종속 관계가 아닌 독립 업무 영역으로 개발한다. 각 영역은 다른 영역의 존재나 화물 인계를 요구하지 않는 내부 플레이 폐루프, 독립 Fixture, 독립 조회·저장 경계를 먼저 가진다.
- 별도 요청이 없으면 `Farm→Hub`, `Hub→City`, `Farm→Hub→City`를 첫 구현·다음 구현·대표 수직 슬라이스로 선택하지 않는다. 이런 경로는 양쪽 영역의 독립 준비가 확인된 뒤 수행하는 영역 간 통합 slice다.
- Farm 성과나 화물을 Hub·City 개발의 필수 시작 상태로 재사용하지 않는다. 영역 간 계약은 선택 가능한 `ExternalConnectorStub`·Command·Event·Projection 경계로 두고 연결이 없어도 각 영역이 실행 가능해야 한다.
- 새 문서·계획·작업 우선순위에서 `City/Hub`를 한 영역처럼 묶지 않는다. 기존 공개 계약·고유 식별자는 호환을 위해 보존하되 새 개발 판단에서는 Hub와 City의 목적·상태·완료 증거를 분리한다.
- 영역 구현 순서는 의존 관계가 아니다. 현재 완성도가 낮은 독립 영역을 선택할 수 있으며, Farm을 먼저 만들었다는 이유만으로 다음 작업을 Farm 출하나 Farm→Hub 운송으로 이어가지 않는다.
- Unity 표현은 공식 Scene을 영역별로 늘리지 않고 canonical `SimulationWorldShell` 안의 독립 영역 모듈로 조립한다. 상세 기준은 [Farm·Hub·City 독립 영역 우선 개발 정책](docs/Architecture/FarmHubCity독립영역우선개발정책.md)을 따른다.
- 기존 naming, DI, repository 경계를 먼저 따르고 abstraction은 실제 중복이나 책임 혼합을 줄일 때만 추가한다.
- 영속 workflow는 contract와 API 경계를 먼저 안정화하고 인증, 조회, 저장, 상태 전이 순으로 연결한다.
- 상태 전이 성공 뒤 client는 같은 원장을 다시 조회해 여러 앱이 같은 상태를 보게 한다.
- 외부 API는 interface, options, typed client, DTO, service 경계를 두고 timeout, cancellation, 오류 응답과 retry 가능성을 고려한다.
- 공공 데이터에는 출처, 기준 시각, 단위, 통화, 지역, 갱신 주기와 제한을 표시한다.
- 공공자료 조사 요청의 기본 완료 범위는 [자료조사 운영의 D429 기본 파이프라인](docs/Architecture/게임자료조사전문운영.md#공공자료-조사의-기본-완료-범위--수집저장재조회-d429)이다. 사용자가 조사/문서만 요청하지 않았다면 공식 출처·이용조건 확인 → 기존 ASP.NET Core 수집/가져오기 → 기존 로컬 Docker MySQL에 비공개·검토보류 저장 → 독립 재조회·동일 입력 중복 방지 → 개발 검토·기획 보고까지 수행한다. 자료 담당이 수집·축적, 개발이 정확 DB/코드·보안·검증을 조율하며 저장 결손을 보고서만으로 완료 처리하지 않는다. 키/권리/대상 확인이 막힌 단계만 보류하고 사용자 조치가 필요하면 먼저 보고한다. 원격 운영 DB·정기/대량/유료 수집·게시·게임 적용은 자동 포함하지 않는다.

## 검증 단계

View 캡처(Game View 및 보조 Scene View)의 실행 담당은 `월드·공간·배치` 스레드다. `개발`은 대상 WI·Scene·판본·입력 절차·필요 화면을 인계하고 반환된 캡처·Console·재현 기록을 검토해 통합한다. 다른 담당은 캡처 필요를 개발에 전달하며 Editor 점유를 중복하지 않는다. 상세는 [View 캡처 분담](docs/Architecture/CodexPlayableLoopGoal운영체계.md#view-캡처-분담)을 따른다. 담당 지정만으로 즉시 캡처·Scene 저장·E 승격을 승인하지 않는다.

상세 output은 `artifacts/local/validation/`에 저장하고 대화에는 성공 여부, 실패 test 이름, 첫 오류만 남긴다.

| 시점 | 기본 명령 | 목적 |
| --- | --- | --- |
| 수정 직후 | `powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-changes.ps1 -Level Fast` | 직접 영향 project와 발견된 관련 test |
| 작업 완료 전 | 같은 명령의 `-Level Task` | 관련 version slice와 targeted/full fallback test |
| release 또는 명시 요청 | 같은 명령의 `-Level Release` | `0.0`, `0.5`, `1.0`, `1.5` build와 전체 test |
| 문서·지침만 변경 | 같은 script의 `Fast` | `git diff --check`, build/test 생략 |

- 작업 트리에 다른 스레드 변경이 섞여 있으면 `-Paths <이번 작업 파일들>`로 검증 범위를 명시한다.
- shared contract나 shared UI 변경은 소비 client 검증 없이 server build만으로 끝내지 않는다.
- 상태 전이·동기화는 원장 저장, RDB 투영, Event 재처리, 권한, 다른 client 재조회를 확인한다.
- UI는 가능한 경우 local server와 browser로 핵심 경로를 확인한다. 불가능하면 제한과 대체 검증을 적는다.
- 현재 Unity 개발 단계의 기본 완료 기준은 소스 정적 검사, 컴파일, 관련 EditMode·단위 시험과 필요한 경우 저장 Scene·조립 코드의 구조 검증이다. 사용자가 실행 화면이나 시각 마감을 명시적으로 요청하지 않은 작업에서는 Play Mode 수동 조작, Game View 확인과 PNG 캡처를 필수로 요구하지 않는다.
- Game View를 생략했을 때는 `코드·시험 검증 완료 / Play Mode·Game View 미검증`처럼 증거 수준을 분리해 보고한다. 화면에서만 판별할 수 있는 입력·카메라·배치 문제, 릴리스 검증, 또는 사용자가 실제 화면 확인을 요청한 경우에는 Play Mode와 Game View 검증을 다시 수행한다.
- test나 build를 실행하지 못하면 이유와 미검증 범위를 보고한다.

## Git과 산출물

- commit과 push는 사용자가 명시적으로 요청한 경우에만 수행한다.
- commit은 feature, refactor, fix, test, docs처럼 되돌릴 수 있는 맥락으로 나누고 다른 스레드 변경을 섞지 않는다.
- stage 전 이번 작업의 `git diff`와 `git status`를 다시 확인한다.
- 화면 변경은 [커밋별 시각 변경 기록](docs/Changes/README.md)에 검증 수준을 기록한다. 현재 단계에서는 코드·시험 검증이 기본이며, 사용자가 실제 화면 확인·시각 마감·릴리스 증거를 요청한 경우에만 Editor/Pipeline에서 최종 Game View를 다시 캡처하고 대표 PNG를 관련 코드·Scene과 같은 맥락의 커밋에 포함한다. 캡처를 생략한 변경은 `화면 미검증` 또는 `간접 확인`으로 명시하며 코드 검증을 실제 화면 검증으로 표현하지 않는다. Scene View는 배선 설명을 위한 보조 증거일 뿐 Game View를 대신하지 않는다.
- 임시 log, browser profile, raw capture, test result는 `artifacts/local/`에 두고 commit하지 않는다.
- 새 worktree는 가능하면 저장소 바깥의 sibling 경로에 만든다. `artifacts/worktrees/`는 탐색·검증 대상에서 제외한다.
- 장기 보존할 대표 화면만 `docs/assets/changes/`로 옮긴다.

## 우선 참고 문서

1. [README](README.md)
2. [0.0 커뮤니티·공공데이터 집중 로드맵](docs/Versions/v0.0/focus-roadmap.md)
3. [커뮤니티 0.0 기반 제품 원칙](docs/Architecture/CommunityFoundationV0Policy.md)
4. [업무 실행 책임 모델](docs/Architecture/BusinessWorkflowResponsibilityModel.md)
5. [첨부 문서 목차](docs/ProjectOverview/00-첨부문서목차.md)
