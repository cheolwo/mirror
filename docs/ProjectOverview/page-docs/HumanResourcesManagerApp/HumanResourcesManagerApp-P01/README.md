# HumanResourcesManagerApp-P01 - 역할 지원·배정 검토

[전체 화면 문서](../../README.md) / [HumanResourcesManagerApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/HumanResourcesManagerApp/HumanResourcesManagerApp-P01.png" alt="HumanResourcesManagerApp-P01 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | HumanResourcesManagerApp |
| 페이지 ID / 제목 | HumanResourcesManagerApp-P01 - 역할 지원·배정 검토 |
| 라우트 | / |
| 소스 파일 | [HumanResourcesManagerApp/Components/Pages/Home.razor](../../../../../HumanResourcesManagerApp/Components/Pages/Home.razor) |
| 분류 | 확장 |
| 2.0 운송 필수 연결 | 직접 연결 없음 |
| 캡처 상태 | 완료 |

## 페이지 책임 보완 (2026-10-16 조회 안내)

현재 기능 사용 가능 여부, 접근 권한, 지원·배정 상태와 조회 전용 경계는 유지한다. 화면의 기능 키·영문 조회 진단·서버 페이징 설명은 사용자가 이해할 수 있는 짧은 한국어로 표시한다. 필터·조회·페이지 이동·상세 선택은 그대로 두며 승인·배정·계약·급여 변경 권한을 추가하지 않는다. 빈 결과와 조회 실패는 구별하고 기존 다시 조회 동작을 유지한다. 이번 문구 보완의 실제 모바일 렌더·재진입 검증은 별도다.

## 왜 필요한가

이 화면은 사용자가 자발적으로 남긴 역할 지원·철회 원장과 운영자가 남긴 역할 배정·해제 원장을 같은 검토 ID 기준으로 확인하기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 인사/고용 담당자 / 보조 참여자: 관리자, 현장 근무자, 주문자 집단 운영자

이 화면은 보조 또는 확장 업무 워크플로우 안에서 인사/고용 관리 홈 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 영속 역할 지원·배정 원장의 검색, 서버 페이징과 정확한 검토 ID 상세 조회
- 사용자가 확인해야 하는 것: 지원과 배정 원장의 구분, 검토 상태, 역할·범위, 지원 동의 또는 배정 접근 조건
- 사용자가 조작해야 하는 것: 원장·상태·참여자·범위 필터, 검색, 페이지 이동과 상세 선택
- 화면 밖으로 넘길 일: 지원 승인, 역할 배정·해제, 근무 조건, 계약·급여와 외부 신고 상태 변경

## 다른 화면과의 관계

- 이전 화면: 없음
- 다음 화면: 없음
- 상위 화면: 없음
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 인사/고용 관리 상태는 공동주문 집단 운영, 창고 작업, 경비/분류 보조 인력 운영과 연결될 수 있습니다.

## API 경로와 코드 연결

- 화면 소스: [HumanResourcesManagerApp/Components/Pages/Home.razor](../../../../../HumanResourcesManagerApp/Components/Pages/Home.razor)
- 클라이언트 서비스/계약: 화면 파일에서 직접 확인하거나 정적/라우팅 화면으로 본다.

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 역할 지원·배정 검토 | GET | `api/v1/admin/hr-role-reviews` | `Ssalddel.Ui.Common/Areas/App/Services/인사역할검토Client.cs` | `Ssalddel/Controllers/Admin/HumanResources/인사역할검토Controller.cs` |
| 정확한 검토 상세 | GET | `api/v1/admin/hr-role-reviews/{reviewId}` | `Ssalddel.Ui.Common/Areas/App/ViewModels/인사역할검토페이지ViewModels.cs` | `Ssalddel/Controllers/Admin/HumanResources/인사역할검토Controller.cs` |
| 워크플로우 문서 | - | `api/v1/admin/documents` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/documents/policies` [Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs](../../../../../Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs)<br>`PUT api/v1/admin/documents/policies/{documentCode}` [Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs](../../../../../Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs)<br>`GET api/v1/admin/documents` [Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs](../../../../../Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs)<br>`GET api/v1/admin/documents/logs` [Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs](../../../../../Ssalddel/Controllers/Admin/04_증빙/문서관리Controller.cs) |
| 워크플로우 문서 | - | `api/v1/admin/hr-employment-contracts` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/hr-employment-contracts` [Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs)<br>`GET api/v1/admin/hr-employment-contracts/{contractId:guid}` [Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs)<br>`POST api/v1/admin/hr-employment-contracts` [Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs)<br>`POST api/v1/admin/hr-employment-contracts/{contractId:guid}/sign` [Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/고용계약Controller.cs) |
| 워크플로우 문서 | - | `api/v1/admin/hr-participation-benefits` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/hr-participation-benefits` [Ssalddel/Controllers/Admin/HumanResources/참여혜택Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/참여혜택Controller.cs)<br>`POST api/v1/admin/hr-participation-benefits/transfer` [Ssalddel/Controllers/Admin/HumanResources/참여혜택Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/참여혜택Controller.cs) |
| 워크플로우 문서 | - | `api/v1/admin/hr-roles` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/hr-roles` [Ssalddel/Controllers/Admin/HumanResources/인사역할Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/인사역할Controller.cs)<br>`POST api/v1/admin/hr-roles` [Ssalddel/Controllers/Admin/HumanResources/인사역할Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/인사역할Controller.cs)<br>`DELETE api/v1/admin/hr-roles/{assignmentId:guid}` [Ssalddel/Controllers/Admin/HumanResources/인사역할Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/인사역할Controller.cs) |
| 워크플로우 문서 | - | `api/v1/admin/hr-social-insurance-filings` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/hr-social-insurance-filings` [Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs)<br>`GET api/v1/admin/hr-social-insurance-filings/{id:guid}` [Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs)<br>`POST api/v1/admin/hr-social-insurance-filings/assess` [Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs)<br>`POST api/v1/admin/hr-social-insurance-filings` [Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs](../../../../../Ssalddel/Controllers/Admin/HumanResources/사회보험신고Controller.cs) |
| 워크플로우 문서 | - | `api/v1/admin/hs-codes` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/hs-codes` [Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs](../../../../../Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs)<br>`PUT api/v1/admin/hs-codes/{entryId:long}/business-category` [Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs](../../../../../Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs)<br>`POST api/v1/admin/hs-codes/{entryId:long}/risk-tags` [Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs](../../../../../Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs)<br>`PUT api/v1/admin/hs-codes/risk-tags/{tagId:long}` [Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs](../../../../../Ssalddel/Controllers/Admin/Customs/HS코드운영Controller.cs) |
| 워크플로우 문서 | - | `api/v1/admin/orderer/group-purchase-` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | - |
| 워크플로우 문서 | - | `api/v1/admin/transports` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/admin/transports` [Ssalddel/Controllers/Admin/03_진행/운송진행관리Controller.cs](../../../../../Ssalddel/Controllers/Admin/03_진행/운송진행관리Controller.cs)<br>`GET api/v1/admin/transports/events` [Ssalddel/Controllers/Admin/03_진행/운송진행관리Controller.cs](../../../../../Ssalddel/Controllers/Admin/03_진행/운송진행관리Controller.cs) |
| 워크플로우 문서 | - | `api/v1/community/activity-signals` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/community/activity-signals` [Ssalddel/Controllers/Common/커뮤니티활동신호Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티활동신호Controller.cs) |
| 워크플로우 문서 | - | `api/v1/community/posts` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/community/posts` [Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs)<br>`POST api/v1/community/posts` [Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs)<br>`GET api/v1/community/posts/{id:long}` [Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs)<br>`POST api/v1/community/posts/{id:long}/attachments` [Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티게시글Controller.cs) |
| 워크플로우 문서 | - | `api/v1/community/votes` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/community/votes` [Ssalddel/Controllers/Common/커뮤니티투표Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티투표Controller.cs)<br>`GET api/v1/community/votes/{voteId:guid}` [Ssalddel/Controllers/Common/커뮤니티투표Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티투표Controller.cs)<br>`POST api/v1/community/votes` [Ssalddel/Controllers/Common/커뮤니티투표Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티투표Controller.cs)<br>`POST api/v1/community/votes/{voteId:guid}/votes` [Ssalddel/Controllers/Common/커뮤니티투표Controller.cs](../../../../../Ssalddel/Controllers/Common/커뮤니티투표Controller.cs) |
| 워크플로우 문서 | - | `api/v1/customs` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `POST api/v1/customs/consents` [Ssalddel/Controllers/Common/통관연동Controller.cs](../../../../../Ssalddel/Controllers/Common/통관연동Controller.cs) |
| 워크플로우 문서 | - | `api/v1/driver/dispatch-actions` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `POST api/v1/driver/dispatch-actions/{requestId}/accept` [Ssalddel/Controllers/Driver/03_Action/기사배차액션Controller.cs](../../../../../Ssalddel/Controllers/Driver/03_Action/기사배차액션Controller.cs)<br>`POST api/v1/driver/dispatch-actions/{requestId}/reject` [Ssalddel/Controllers/Driver/03_Action/기사배차액션Controller.cs](../../../../../Ssalddel/Controllers/Driver/03_Action/기사배차액션Controller.cs)<br>`POST api/v1/driver/dispatch-actions/{requestId}/cancel-acceptance` [Ssalddel/Controllers/Driver/03_Action/기사배차액션Controller.cs](../../../../../Ssalddel/Controllers/Driver/03_Action/기사배차액션Controller.cs) |
| 워크플로우 문서 | - | `api/v1/driver/recommendations` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/driver/recommendations` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs)<br>`GET api/v1/driver/recommendations/idle` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs)<br>`GET api/v1/driver/recommendations/driving` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs)<br>`GET api/v1/driver/recommendations/search` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs) |
| 워크플로우 문서 | - | `api/v1/driver/transports` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/driver/transports` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/current` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/{id:long}` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`POST api/v1/driver/transports/{id:long}/arrive-pickup` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs) |
| 워크플로우 문서 | - | `api/v1/files` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `POST api/v1/files/upload` [Ssalddel/Controllers/Common/파일업로드Controller.cs](../../../../../Ssalddel/Controllers/Common/파일업로드Controller.cs) |
| 워크플로우 문서 | - | `api/v1/orderer/group-purchase-logistics-workflows` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/orderer/group-purchase-logistics-workflows/resolve` [Ssalddel/Controllers/Orderer/공동구매물류워크플로우Controller.cs](../../../../../Ssalddel/Controllers/Orderer/공동구매물류워크플로우Controller.cs) |
| 워크플로우 문서 | - | `api/v1/orderer/group-purchase-overseas-shipments` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/orderer/group-purchase-overseas-shipments/lookup` [Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs](../../../../../Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs)<br>`GET api/v1/orderer/group-purchase-overseas-shipments/import-logistics-references` [Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs](../../../../../Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs)<br>`POST api/v1/orderer/group-purchase-overseas-shipments/import-logistics-normalization-simulation` [Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs](../../../../../Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs)<br>`GET api/v1/orderer/group-purchase-overseas-shipments/lookup-normalized` [Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs](../../../../../Ssalddel/Controllers/Orderer/공동구매해외선적추적Controller.cs) |

추가 API 후보 6개는 관련 서비스 파일에서 계속 확인합니다. 이 화면 README는 화면 책임 판단에 필요한 대표 경로만 먼저 노출합니다.

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

서버관리자 정책과 HR 기능 플래그를 모두 확인합니다. 내부 사용자 ID와 연락처, 주소, 계좌는 응답하지 않으며 작업장 IP 범위 원문은 등록 개수로만 표시합니다. 역할 지원은 승인·고용·계약으로 해석하지 않습니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
