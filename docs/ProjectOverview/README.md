# 프로젝트 화면 안내

이 문서는 화면, 캡처, 화면 간 업무 흐름과 코드 위치를 찾는 **ProjectOverview 기준 목차**입니다. 처음에는 기술 구조보다 현재 존재하는 화면과 캡처를 먼저 봅니다. 저장소 전체 문서 분류와 기준 문서는 [살뜰 문서 안내](../README.md)를 따릅니다.

루트 README는 현재 집중 범위인 문화교통 0.0 커뮤니티·공공데이터 기반을 먼저 보여 줍니다. 이 폴더에서는 글쓰기와 음식·재료 탐색, 참여 동의, 공동 원장과 완료 사례로 이어지는 통합 클라이언트를 먼저 보고, 0.5 이후 화면은 후속 자산으로 구분해 확인합니다.

GPT Chat과 Codex에서 프로젝트 전체와 Unity World Projection 작업을 시작할 때는 [Ssalddel AI 공용 프로젝트 컨텍스트](GptProjectContext.md)를 먼저 확인합니다. 이 문서는 제품 위치, 서버·Web·Unity 책임, 현재 구현·사용자 보고·계획의 구분과 AI 작업 규칙을 한 곳에서 요약합니다. 현재 인계 상태와 장기 결정은 각각 [CURRENT_WORK](../AI/CURRENT_WORK.md)와 [DECISIONS](../AI/DECISIONS.md)를 따릅니다.

## 화면으로 먼저 보기

첨부 문서도 기술 구조보다 화면을 먼저 봅니다. 장기 보존 화면은 `docs/ProjectOverview/assets/`와 `docs/assets/changes/`를 기준으로 관리합니다. `artifacts/community-sales-preview/`의 로컬 검증 캡처는 저장소 용량 정리 과정에서 제거했습니다.

일반 글, 질문, 판매, 공동구매는 서로 떨어진 제품이 아닙니다. 같은 게시판에서 가볍게 시작하고, 참여 의사가 모일 때 가원장·역할 슬롯·다이어그램을 붙여 공동행동으로 확장합니다.

### 후속 실행 모듈 참고 화면

| 화주 의뢰 상세 | 기사 지도 홈 |
| --- | --- |
| <img src="assets/app-pages/SsalddelApp/SsalddelApp-P03.png" alt="화주 의뢰 상세 화면" width="260"> | <img src="assets/app-pages/DriverApp/DriverApp-P07.png" alt="기사 지도 홈 화면" width="260"> |

| 기사 추천 상세 | 기사 상하차 증빙 |
| --- | --- |
| <img src="assets/app-pages/DriverApp/DriverApp-P09.png" alt="기사 추천 상세 화면" width="260"> | <img src="assets/app-pages/DriverApp/DriverApp-P12.png" alt="기사 상차 증빙 화면" width="220"> <img src="assets/app-pages/DriverApp/DriverApp-P13.png" alt="기사 하차 증빙 화면" width="220"> |

| 관리자 운송 원장 | 창고 피킹 배치 |
| --- | --- |
| <img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P22.png" alt="관리자 운송 원장 화면" width="300"> | <img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P04.png" alt="창고 피킹 배치 화면" width="260"> |

### 새 통합 클라이언트

<img src="assets/app-pages/SsalddelApp/SsalddelApp-P00.png" alt="역할 기반 통합 커뮤니티 홈" width="360">

[통합 커뮤니티 클라이언트와 꾸미기 상점](unified-community-client.md)에서 게시판, 글쓰기, 역할 참여, 모바일 세로 다이어그램, 후천 사방 이동판과 꾸미기 흐름을 확인합니다. 화면을 구성하는 상위 원칙은 [Ssalddel 0.0](../Versions/v0.0/README.md)과 [통합 클라이언트 3단계 내비게이션](../Architecture/ThreeStageClientNavigation.md)에 둡니다.

## 먼저 볼 화면 문서

[OS 생명주기와 상태 카드 조사](page-docs/os-state-card-audit-r17.md)는 실제 업무 상태·허용 행동과 카드 배치를 대조한 결과입니다. 이미 맞는 정상 흐름과 검수·예외·일부 안내의 결손을 구분하고, 후속 카드 결정 순서를 정리합니다.

[역할·상태별 카드 검토표](page-docs/role-state-card-review-r16.md)는 기본 카드와 상세·주행동을 비교하는 초안입니다. [위치·갱신·기본 카드 구현과 화면](../Changes/2026-10-05-role-map-reliability-r16.md)에서 이번 보완과 실제 예시 화면을 확인합니다.

[역할 앱의 지도 중심 구성 조사](role-map-coverage-audit-r15.md)에서 통합 지도와 전용 앱의 차이, 다른 역할의 지도·기본 카드 조정, 현재 위치와 정보 갱신 문제의 보완 순서를 확인합니다.

| 번호 | 문서 | 내용 |
| --- | --- | --- |
| 마무리 | [SsalddelProjectClosureProposal.md](SsalddelProjectClosureProposal.md) | 커뮤니티·공공데이터 공개 프리뷰와 0.5~3.5 시제품 자산을 구분해 프로젝트를 마무리하는 범위·순서·종료 기준 |
| 0.0 | [Ssalddel 0.0](../Versions/v0.0/README.md) | 글쓰기 → 가원장 → 역할 슬롯 → 실원장으로 이어지는 현재 제품 범위 |
| 01 | [page-docs/README.md](page-docs/README.md) | 각 화면별 독립 README와 인라인 캡처, 상세 설명 |
| 01-A | [community-board-field-focus-guide.md](community-board-field-focus-guide.md) | 배달 현장에서 게시판 하나씩 관찰·검토하기 위한 질문, 근거, 안전·개인정보 경계와 16개 업무 게시판 점검표 |
| 02 | [unified-community-client.md](unified-community-client.md) | 통합 홈, 역할, 모바일 다이어그램, 사방 이동, 꾸미기 상점 |
| 03 | [ThreeStageClientNavigation.md](../Architecture/ThreeStageClientNavigation.md) | 사방괘 → 다이어그램 → 구체 데이터 페이지의 사용자 화면 구조 |
| 04 | [app-page-catalog.md](app-page-catalog.md) | 코드 프로젝트에 실제로 선언된 `@page` 화면 전체 카탈로그와 인라인 캡처 |
| 05 | [ssalddel-v1-required-pages.md](ssalddel-v1-required-pages.md) | 레거시 파일명으로 보존된 살뜰 2.0 운송 흐름의 화주, 기사, 관리자 화면 |
| 06 | [ssalddel-v1-page-validation-walkthrough.md](ssalddel-v1-page-validation-walkthrough.md) | 레거시 파일명으로 보존된 2.0 운송 페이지 검증 순서와 확인 항목 |
| 07 | [ssalddel-v1-render-capture-summary.md](ssalddel-v1-render-capture-summary.md) | 실제 화면 캡처 방식, 렌더링 확인 결과, 남은 검증 항목 |
| 08 | [workflow-app-screen-map.md](workflow-app-screen-map.md) | 페이지-워크플로우-UseCase·ProcessManager·API의 현재 연결, 화면 미연결 간극과 앱 간 인계를 설명 |
| 08-A | [business-process-page-map.md](business-process-page-map.md) | 업무 프로세스를 시작·처리·확인·인계로 나누고 관련 현재 페이지와 화면 간극을 순서대로 연결 |
| 09 | [screen-flows.md](screen-flows.md) | 화면의 버튼, 노드 행동, 모드 전환이 다음 행동으로 이어지는 흐름 |

## 업무 흐름 문서

| 번호 | 문서 | 내용 |
| --- | --- | --- |
| 08 | [dispatch-flows.md](dispatch-flows.md) | 화물/용달 배차와 음식 배달 배차의 경계 |
| 09 | [warehouse-flows.md](warehouse-flows.md) | 입고, 적재, 출고, 주문 발생 시 창고 알림 흐름 |
| 10 | [orderer-group-commerce-flows.md](orderer-group-commerce-flows.md) | 같이 주문, 해외 선적/통관, 국내 운송, 판매채널 출고 흐름 |
| 11 | [Versions/README.md](../Versions/README.md) | 0.0부터 3.5까지의 단계별 제품 방향과 실행 경계 |

## 기술 참고 문서

기술 용어와 내부 구조는 처음 화면을 파악한 뒤에 봅니다.

| 번호 | 문서 | 내용 |
| --- | --- | --- |
| T-00 | [CommunityFoundationV0Policy.md](../Architecture/CommunityFoundationV0Policy.md) | 커뮤니티 선행 기반과 유상 배차·주선 실운영 경계 |
| T-00-1 | [CultureTransportProductLine.md](../Architecture/CultureTransportProductLine.md) | 문화와 음식 근거에서 공동 수요와 공급·이동 준비로 이어지는 제품 이름과 실행 경계 |
| T-01 | [workflow-api-policy.md](workflow-api-policy.md) | API를 화면과 업무 절차 기준으로 관리하는 기준 |
| T-02 | [BusinessWorkflowResponsibilityModel.md](../Architecture/BusinessWorkflowResponsibilityModel.md) | Business Case, Policy, 실행 조율, 판단 도구와 상태 변경의 책임 경계 |
| T-02-1 | [GroupPurchaseDemandProcessManager.md](../Architecture/GroupPurchaseDemandProcessManager.md) | 0.5 개별주문을 입력으로 받는 1.0 주문자 집단화와 1.5 인계 경계 |
| T-03 | [DomesticCargoTransportOS.md](../Architecture/DomesticCargoTransportOS.md) | 국내 화물 운송을 운영하는 내부 기준 |
| T-04 | [EngineOverview.md](../Architecture/EngineOverview.md) | Process Manager, 워크플로우와 판단 도구의 관계 |
| T-05 | [HIOPSAI.md](../Architecture/HIOPSAI.md) | 참여자 입장 해석과 배차 조율을 돕는 AI 방향 |
| T-06 | [OutboundBatchEngine.md](../Architecture/OutboundBatchEngine.md) | 출고 배치와 피킹 배치 판단 기준 |
| T-07 | [DispatchQueueResponsibility.md](../Architecture/DispatchQueueResponsibility.md) | 배차 상태 저장과 실행 자료의 책임 경계 |
| T-08 | [hiops-ai-judgment-cases.md](hiops-ai-judgment-cases.md) | AI 판단 보조를 만들기 위한 상황별 판단 사례 |
| T-09 | [glossary.md](glossary.md) | POD, BL, 3PL, 레그, RAG 같은 주요 용어 정의 |
| T-10 | [Blazor_Maui_공통화_1차.md](../Architecture/Blazor_Maui_공통화_1차.md) | 네이티브 기능이 꼭 필요한 경우를 제외하고 MudBlazor 컴포넌트 UI를 기본으로 삼는 기준 |
| T-11 | [ISMS-P-readiness.md](../Compliance/ISMS-P-readiness.md) | 보호 데이터와 인증·보안 준비 항목 |
| T-12 | [TransportPaymentSettlementPolicy.md](../Architecture/TransportPaymentSettlementPolicy.md) | 운송 안심결제와 조건부 정산의 실행 경계 |
| T-13 | [CommunityInformationCollection.md](../Architecture/CommunityInformationCollection.md) | 외부 출처 정보를 검토 후보로 모으고 보강 조회하는 경계 |
| T-14 | [CommunityDynamicTopicDiscovery.md](../Architecture/CommunityDynamicTopicDiscovery.md) | 음식·화물 글을 동적 주제로 투영하고 관련 정보를 연결하는 경계 |
| T-15 | [UnitedStatesThirdPartyLogisticsProviderDirectory.md](../Architecture/UnitedStatesThirdPartyLogisticsProviderDirectory.md) | 미국 3PL 후보와 규제·제휴·실계약을 분리하는 기준 |
| T-16 | [ExportLedgerModel.md](../Architecture/ExportLedgerModel.md) | 개별수출과 공동 선적에서 수출자별 원장·서류·실적을 보존하는 기준 |
| T-17 | [GoogleApiAdoptionProposal.md](../Architecture/GoogleApiAdoptionProposal.md) | 현재 Google 지도·YouTube·FCM·Gemini·Storage 연동을 페이지·업무 프로세스에 연결하고 API별 도입·보류·보안·비용 단계를 정리한 제안 |
| T-18 | [ContractRelationshipMapVisualizationProposal.md](../Architecture/ContractRelationshipMapVisualizationProposal.md) | 현재 계약·양측 합의·발주·운송·고용 데이터를 공개·당사자·운영자 경계로 나누고 한 개 지도에 점·선·면·상태 배지로 조화롭게 투영하는 제안 |
| T-19 | [UnityCooperativeExperiencePlatformProposal.md](../Architecture/UnityCooperativeExperiencePlatformProposal.md) | 생산·유통·협력 도메인, 권위 서버, REST·SignalR, 협동조합 격리와 재사용 Prefab·design system을 묶은 Unity 상위 제안 |
| T-20 | [UnityAgricultureDistributionSimulationProposal.md](../Architecture/UnityAgricultureDistributionSimulationProposal.md) | Unity 데이터 계약, provenance, 결정적 계산과 감자 golden 시나리오의 세부 설계·현재 구현 상태 |
| T-21 | [UnityWorldLedgerProjectionArchitectureProposal.md](../Architecture/UnityWorldLedgerProjectionArchitectureProposal.md) | WorldManager·DataManager·UseCase·원장 projection·GameObject 표현 책임을 분리하고 기존 살뜰 업무 도메인을 Unity World에 투영하는 기준 |
| T-22 | [UnityClientLayeredArchitecture.md](../Architecture/UnityClientLayeredArchitecture.md) | Unity API Client·Mapper·Repository·UseCase·Presenter·SceneController·View·Prefab·Inspector와 Editor·AI 자동화 경계를 정의한 클라이언트 구조 기준 |
| T-23 | [UnityServerStateToWorldProjectionDesign.md](../Architecture/UnityServerStateToWorldProjectionDesign.md) | 실제 EF DbSet·Mongo 원장을 전수 조사하고 서버 실체를 aggregate projection·UseCase·Zone Controller·View로 변환하는 기준 |

## 관리 원칙

1. 루트 README에는 커뮤니티 0.0 기반, 0.5 개별주문과 1.0 같이 주문의 제품 의도·대표 화면을 먼저 두고, 1.5 이후 화면은 후속 모듈로 구분한다.
2. 화면별 README와 전체 페이지 카탈로그를 첨부 문서의 앞순위에 둔다.
3. OS, 엔진, AI, API 같은 기술 설명은 뒤쪽 참고 문서로 둔다.
4. 새 화면을 추가하면 `app-page-catalog.md`, `page-docs/`, 캡처 이미지부터 갱신한다.
5. 화면 간 상태 전파나 시퀀스는 `workflow-app-screen-map.md`에 둔다.
6. 사용자 내비게이션은 3단계 화면 구조로 먼저 설명하고, 프로젝트별 표는 코드 위치를 찾는 용도로 사용한다.
7. 날짜별 구현·검증 기록은 `docs/Changes/`, 현재의 장기 기준은 `docs/Architecture/`와 `docs/Versions/`에 둔다.
