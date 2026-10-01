# Hongdal 공통 작업 지침

이 파일은 Hongdal 저장소에서 작업하는 모든 AI 도구와 스레드가 함께 따르는 단일 공통 지침이다. 세부 제품·설계 문서를 복제하지 않고, 작업 판단의 우선순위와 변경 원칙을 정한다.

## 적용 범위와 우선순위

1. 시스템/개발자 지침과 현재 스레드의 최신 사용자 요청을 가장 먼저 따른다.
2. 하위 폴더에 별도 `AGENTS.md`가 있으면 그 폴더 안에서는 더 가까운 지침을 따른다.
3. 별도 지시가 없으면 이 파일과 [0.0 집중 로드맵](docs/Versions/v0.0/focus-roadmap.md)을 기본 기준으로 삼는다.
4. 문서와 코드가 다르면 실제 코드, 테스트, 실행 설정을 확인한 뒤 차이를 사용자에게 알리고 함께 정합화한다. 추측으로 한쪽을 덮지 않는다.
5. 과거 대화나 오래된 선호보다 현재 저장소 문서와 최신 사용자 요청을 우선한다.

## 스레드 시작과 인계

- 시작할 때 `git status --short --branch`를 확인하고, 관련 문서·코드·테스트를 먼저 읽는다.
- 작업 트리의 기존 변경은 다른 스레드나 사용자의 작업일 수 있다. 관련 없는 변경을 되돌리거나 정리하거나 함께 stage하지 않는다.
- 같은 파일에 기존 변경이 있으면 patch 직전에 다시 읽고 그 변경 위에 최소 범위로 작업한다. 안전하게 병합할 수 없을 때만 사용자에게 묻는다.
- 검색은 우선 `rg`를 사용하고 `bin`, `obj`, `.vs`, vendor asset, 생성 산출물은 제외한다. 한국어 파일은 UTF-8로 읽고 쓴다.
- 종료할 때는 완료한 일, 변경 파일, 실행한 검증, 남은 위험 또는 다음 작업을 짧게 남긴다. 실제로 하지 않은 commit·push·배포를 했다고 표현하지 않는다.

## 현재 제품 기준

- Hongdal의 중심은 **정보 공개형 커뮤니티**다. 대화와 모집이 공동 원장·다이어그램으로 이어지고, 운송·창고·음식·마트·공동주문은 그 위에 붙는 업무 도구다.
- 기본 개발 집중 범위는 `0.0`이다. `1.0` 이후 코드는 미래 자산으로 보존하되, 명시적인 요청 없이 운영 노출이나 실제 외부 효과를 켜지 않는다.
- 유상 화물 추천·자동 배차·계약 중개·운임 수취·보관·정산은 허가·제휴·법률·운영 준비 전 기본 비활성이다. 개발 검증은 샘플 데이터, FakePG, 모의 흐름을 사용한다.
- 실행 효과는 `HongdalExecution:Mode`의 `Simulation`과 `Operational` 경계로 통제한다. 화면이나 API마다 별도 실행 모드를 임의로 만들지 않는다.
- 사용자가 특정 버전이나 업무 모듈을 명시하면 그 범위에서 작업하되, 현재 운영 경계와 기능 플래그를 유지한다.

제품 경계는 [커뮤니티 0.0 기반 제품 원칙](docs/Architecture/CommunityFoundationV0Policy.md), 세부 우선순위는 [0.0 집중 로드맵](docs/Versions/v0.0/focus-roadmap.md)을 따른다.

## 개발 철학

- Hongdal은 가까운 사람의 필요를 알아보고, 사용자가 책임질 수 있는 약속부터 함께 실천하며, 그 신뢰를 더 넓은 공동체로 확장하도록 돕는다. 세부 기준은 [이웃에서 시작하는 공동행동 개발 철학](docs/Architecture/NeighborCenteredDevelopmentPhilosophy.md)을 따른다.
- 예수의 이웃 사랑과 『대학』의 수신·제가·치국은 같은 실천 원리로 본다. 자신을 바로 세우고 가까운 이웃을 사랑하며, 그 돌봄과 책임을 더 넓은 공동체로 확장한다.

## 공동행동 원칙

- 종교, 국적, 언어, 가족 형태와 경제력은 가입, 노출, 신뢰 점수, 검색 순위나 역할 자격의 대리 지표로 사용하지 않는다.
- 글쓰기와 자발적 참여를 먼저 두고, 관심·참여·연락처 공개·가원장·실원장·실행은 각각 명시적 동의와 철회 가능한 상태로 분리한다.
- 공동행동의 절감액과 편익뿐 아니라 비용, 노동, 위험, 담당자와 계산 근거를 함께 드러낸다.
- 지리적 가까움과 배달권은 수요 집계와 물류 효율화 후보로 사용하되 자동 가입·알림·상대 선택·배차·계약 확정의 근거로 사용하지 않는다.
- 효율이 낮은 참여자를 숨기거나 배제하지 않고 더 넓은 모집권, 다른 시간창, 집결 방식 또는 자격 사업자 참여 같은 대안을 표현한다.

## 아키텍처 원칙

- 기본 호출 방향은 `화면 -> Controller API -> UseCase/Command -> Domain/Infrastructure -> DB/Event/Outbox`다.
- OS는 원장과 규칙을 읽어 순서·정책·handoff를 조율한다. 엔진은 후보·점수·분류 같은 판단을 반환하고 영속 상태를 직접 확정하지 않는다. 실제 상태 변경은 API/UseCase/Command가 검증 후 수행한다.
- MongoDB 원장은 유연한 업무 원본이다. 원장 블록, 다이어그램 노드·연결선·배치·표시 옵션은 MongoDB에서 관리한다.
- RDB는 권한, 조회, 정산, 보고, 트랜잭션이 필요한 업무 블록과 안정 투영을 맡는다. 사용자별 UI 배치 상태를 범용 RDB 테이블로 중복 저장하지 않는다.
- 원장 변경은 Event/Outbox를 통해 필요한 RDB 투영을 갱신하고, RDB 업무 변경은 관련 원장을 다시 구성한다. 재처리 가능하고 멱등해야 하며 양방향 이벤트의 순환 발행을 막는다.
- 하나의 EventHandler는 하나의 후속 관심사만 맡는다. 원본 상태와 반드시 같이 성공해야 하는 처리는 같은 트랜잭션에 두고, 알림·투영·감사·추천 큐처럼 재시도 가능한 후속 처리는 분리한다.
- 새 Controller나 DTO를 먼저 늘리지 않는다. 기존 route, UseCase, metadata, contract, shared component를 재사용할 수 있는지 확인한다.
- 여러 앱이 함께 쓰는 DTO·상수·계약은 `Hongdal.Contracts`, 공통 UI와 workflow는 `Hongdal.Ui.Common`에 둔다. platform 기능만 adapter로 분리한다.
- 여러 프로젝트를 통과하는 기능을 추적할 때는 먼저 `HongdalCodeMetadataAttribute`와 `HongdalCodeFeatureKeys`를 검색한다. 특성이 있는 기능은 `FlowOrder`, `Layer`, `Effects`, `Boundary`, `ContractType` 순으로 읽고, 실제 코드와 다르면 특성을 함께 고친다. 세부 규약은 [코드 탐색 메타데이터](docs/Architecture/HongdalCodeMetadata.md)를 따른다.
- 커뮤니티 0.0 범위 작업은 `[HongdalCommunityV0Module]`을 먼저 검색해 UI·콘텐츠·참여·원장·안전·운영자 글쓰기 모듈과 `0.0-A~E` 단계를 확인한다. 새 대표 진입점을 추가하거나 책임을 옮기면 모듈 특성과 카탈로그 테스트도 갱신한다.
- 기술 용어(`API`, `DTO`, `Command`, `Event`, `Handler`, `UseCase`, `Outbox`, `Service`)는 영어로 쓰고 업무 도메인 용어는 한국어로 쓴다. 요청 DTO는 `...요청`, 응답 DTO는 `...응답` 형태를 우선한다.
- Controller와 service 폴더는 역할과 업무 흐름이 드러나게 정리하되, 단순 정리를 위해 저장소 전체 namespace를 한꺼번에 바꾸지 않는다.
- 개인정보 암호화·복호화는 domain property의 getter/setter가 아니라 persistence와 infrastructure 경계에서 처리한다.

층위와 의존 방향은 [HIOPS Layer Model](docs/Architecture/HIOPSLayerModel.md), 이벤트 경계는 [Command/Event 리팩토링 원칙](docs/Architecture/CommandEvent리팩토링원칙.md)을 따른다.

## 구현 원칙

- 한 번에 기능을 넓게 흩뿌리기보다 원장 또는 업무 노드 하나를 선택해 저장, 상태 전이, Event, API, UI, 테스트까지 세로로 완성한다.
- 기존 패턴과 명명, DI, repository 경계를 먼저 따른다. abstraction은 실제 중복이나 책임 혼합을 줄일 때만 추가한다.
- 영속 workflow는 contract와 API 경계를 먼저 안정화하고 인증, 조회, 저장, 상태 전이 순으로 연결한다. 화면 검증이 목적인 prototype은 교체 가능한 sample adapter를 명시적으로 둘 수 있다.
- 새 Entity를 무분별하게 만들지 말고 기존 Entity, value object, contract를 재사용하거나 책임을 분리할 근거가 있는지 먼저 확인한다.
- 상태 전이는 서버가 권한과 현재 상태를 검증한다. 클라이언트는 성공 응답 또는 서버 event를 받은 뒤 같은 원장을 다시 조회해 여러 앱이 동일한 상태를 보게 한다.
- 샘플 fallback은 개발·시각 검증 경로에서만 명시적으로 사용한다. 운영 저장 실패나 API 실패를 샘플 데이터로 숨기지 않는다.
- 외부 API는 interface, options, typed client, DTO, service 경계를 나누고 timeout, cancellation, retry 가능성, 오류 응답을 고려한다.
- Command에 붙는 알림·로그·부가 서비스는 하드코딩된 단일 설정 대신 전역 기본값, 사용자별 override, 확장 가능한 service catalog 조합을 우선한다.
- API key와 secret은 source, tracked config, 로그, 캡처에 넣지 않는다. `appsettings.Local.json`, user secrets, 환경 변수 등 Git에서 제외된 경로를 사용하고 example에는 빈 값만 둔다.
- 공공 데이터는 출처, 기준 시각, 단위, 통화, 지역, 갱신 주기와 제한을 함께 표시한다. 서로 다른 기준의 수치를 같은 가격처럼 단정하지 않는다.

## 클라이언트와 UI

- 새 공용 사용자 흐름은 우선 `HongdalApp`과 `Hongdal.Ui.Common`에 통합한다. 기존 전문 앱은 명시적인 통합 작업이 아니면 삭제하거나 기능을 축소하지 않는다.
- 공통 셸은 커뮤니티, 업무, 다이어그램, 정보 흐름을 같은 문맥에서 연결한다. 내비게이션은 기본적으로 `사방괘 -> 다이어그램 -> 구체 데이터 페이지` 구조를 따른다.
- MAUI Blazor 화면은 View, ViewModel, navigation 책임을 나누고 기존 MVVM CommunityToolkit 패턴을 따른다. 모바일 목록은 넓은 다중 열 table보다 빠르게 훑을 수 있는 compact card와 detail 전환을 우선한다.
- 기존 화면을 수정할 때 사용자가 요청하지 않은 메뉴·상점·업무 진입·샘플 기능을 없애지 않는다.
- 초기 화면에 필요한 데이터를 먼저 보여 주고, 무거운 보조 데이터는 단계적으로 로드한다. loading, empty, error, retry, disabled 상태를 제공한다.
- 모바일과 desktop에서 텍스트 잘림, 겹침, 터치 영역, 고정 내비게이션, drawer, dialog, diagram 연결선을 확인한다.
- 시각 변경은 실제 렌더링으로 검증한다. 가능하면 대표 desktop/mobile 캡처를 남기고 실제 개인정보, 주소, 연락처, 계좌, 결제 식별자, 위치, 증빙 원본은 마스킹한다.

화면 구조는 [통합 클라이언트 3단계 내비게이션](docs/Architecture/ThreeStageClientNavigation.md), 화면·문서 색인은 [첨부 문서 목차](docs/ProjectOverview/00-첨부문서목차.md)를 따른다.

## 검증 기준

- 변경 위험에 맞는 최소 단위 테스트를 추가하고, 영향을 받은 project를 직접 build한다. shared contract나 shared UI 변경은 server build만으로 끝내지 않고 소비 client도 확인한다.
- 상태 전이나 동기화 변경은 원장 저장, RDB 투영, Event 재처리, 권한, 다른 client의 재조회까지 검증한다.
- UI 변경은 가능한 경우 local server를 실행하고 browser 조작으로 핵심 경로를 확인한다. 실행할 수 없는 환경이면 그 제한과 대체 검증을 명시한다.
- 문서만 바뀐 경우에도 link, 경로, `git diff --check`를 확인한다.
- 테스트나 build를 실행하지 못했으면 이유와 미검증 범위를 최종 보고에 적는다.

## Git과 변경 문서

- commit과 push는 사용자가 명시적으로 요청한 경우에만 수행한다.
- commit은 feature, refactor, fix, test, docs처럼 되돌릴 수 있는 맥락으로 나눈다. 다른 스레드의 변경을 한 commit에 섞지 않는다.
- stage 전 `git diff`와 `git status`를 다시 확인하고 이번 작업 파일만 지정한다.
- 화면이 바뀐 commit은 [커밋별 시각 변경 기록](docs/Changes/README.md)에 실제 PNG와 함께 기록한다. 화면이 없는 API·DB·문서 commit도 `화면 없음` 또는 `간접 확인`으로 남긴다.
- 생성 cache, browser profile, 임시 검증 파일은 `artifacts/` 또는 ignore 경로에 두고 commit하지 않는다.

## 우선 참고 문서

1. [README](README.md)
2. [0.0 집중 로드맵](docs/Versions/v0.0/focus-roadmap.md)
3. [이웃에서 시작하는 공동행동 개발 철학](docs/Architecture/NeighborCenteredDevelopmentPhilosophy.md)
4. [커뮤니티 0.0 기반 제품 원칙](docs/Architecture/CommunityFoundationV0Policy.md)
5. [HIOPS Layer Model](docs/Architecture/HIOPSLayerModel.md)
6. [Command/Event 리팩토링 원칙](docs/Architecture/CommandEvent리팩토링원칙.md)
7. [첨부 문서 목차](docs/ProjectOverview/00-첨부문서목차.md)
8. [커밋별 시각 변경 기록](docs/Changes/README.md)
