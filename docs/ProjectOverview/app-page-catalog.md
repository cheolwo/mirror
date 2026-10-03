# 코드 프로젝트별 전체 페이지 카탈로그

이 문서는 살뜰 저장소 안의 클라이언트 프로젝트에 선언된 `@page` 라우트를 코드 위치별로 모은 전체 페이지 카탈로그다. 사용자에게 별도 앱을 강제하는 내비게이션 목록이 아니라, 통합 클라이언트 화면의 구현 파일을 찾기 위한 물리 색인이다. 사용자 화면은 [통합 클라이언트 3단계 내비게이션](../Architecture/ThreeStageClientNavigation.md)의 `사방괘 → 다이어그램 → 구체 데이터 페이지` 순서로 이해한다.

현재 전체 목록은 [페이지 소스·호스트 목록](page-docs/current-pages.md)과 [기계 판독 목록](page-docs/current-pages.json)이 소유한다. 2026-10-02 기준 466개 물리 Razor 페이지·538개 route 선언·17개 제품 호스트를 MSBuild 입력과 대조했다. 별칭과 호스트 재사용을 구별하며, 기존 P ID·아래 캡처·변경 기록은 과거 화면 증거로 보존한다. 아래 표의 페이지 수·캡처 완료 수는 현재 소스 전수 집계가 아니다.

[페이지→코드→DB 문서화 결과](page-docs/implementation-r1.md)에서 우선 네 화면의 실제 호출·저장 경로와 수정·미검증 범위를 확인한다.

## 문서 기준

| 기준 | 설명 |
| --- | --- |
| 수집 기준 | `*.razor` 파일의 `@page` 라우트 선언 |
| 페이지 ID / 제목 | 실제 프로젝트명을 접두사로 붙인 `ProjectName-P번호`와 사람이 읽는 화면 제목을 함께 표기 |
| 필수 워크플로우 연결 | 레거시 운송 필수 페이지 문서와 직접 연결되는 경우 같은 ID를 병기 |
| 분류 | `필수`, `보조`, `확장`, `운영`, `시스템` |
| 캡처 | 실제 캡처 파일이 있으면 `완료`, 인증 장벽이 먼저 뜨면 `인증 필요`, 현재 실행 표면이 없으면 `캡처 대기`로 표시 |
| 제외 기준 | `@page`가 없는 공용 컴포넌트, 서비스, 레이아웃, 팝업 내부 섹션 |

전체 페이지 캡처 파일은 `docs/ProjectOverview/assets/app-pages/{앱명}/{페이지ID}.png`에 둔다. 기존 운송 대표 캡처는 링크 안정성을 위해 레거시 경로 `docs/ProjectOverview/assets/v1-pages/`에도 남긴다.

화면별 상세 설명은 [화면별 상세 README](page-docs/README.md)에서 코드 프로젝트별/페이지별로 확인한다. 이 카탈로그는 빠른 색인이고, 개별 README는 캡처와 화면 책임, 사용자/참여자, API/보안 점검을 함께 둔다.

## 통합 클라이언트 3단계 색인

| 단계 | 화면 형태 | 현재 구현 위치 | 문서에서 확인할 것 |
| --- | --- | --- | --- |
| 공통 셸 | 역할 선택, 커뮤니티, 하단 내비게이션 | `SsalddelApp`의 `/`, `/shipper`, `MainLayout` | 역할이 바뀔 때 세 단계의 메뉴와 문맥이 함께 바뀌는가 |
| 1단계 | 후천 사방 이동판 | `PlatformCommunityHome` 내부의 `SsalddelLaterHeavenBaguaNavigator` | 역할별 네 방향과 관련 다이어그램 진입 |
| 2단계 | 원장 관계·흐름 다이어그램 | `PlatformCommunityHome` 다이어그램 모드 | 단일·연결·복합 원장 경계, 노드 요약, 상태, 진행도, 행동 메뉴 |
| 3단계 | 목록·상세·작업 페이지 | 아래 `@page` 카탈로그 | 한 페이지가 한 가지 조회·입력·처리 책임을 갖는가 |

### 대표 원장 구성

| 2단계 표시 단위 | 원장 구성 | 다음 탐색 |
| --- | --- | --- |
| 음식 주문·배달 연결 묶음 | 음식 주문 원장 1건 → 음식 배달 원장 0..N건 | 묶음 개요 → 주문 또는 첫 배달·분할·재배달 회차 → 노드별 데이터 페이지 |
| 공동구매 복합 원장 | 수요 → 수입 결정 → 선적/통관 → 입고/분배 | 복합 개요 → 하위 원장 세부 다이어그램 → 창고·운송을 포함한 데이터 페이지 |

연결 묶음의 원장은 각각 별도 상태와 권한을 유지한다. 복합 원장은 하위 원장의 진행을 요약하고 필요한 외부 원장으로 인계한다. 자세한 원장 관계는 [3단계 내비게이션 문서](../Architecture/ThreeStageClientNavigation.md)의 원장 다이어그램 기준을 따른다.

### 대표 노드에서 3단계 페이지로

| 2단계 노드 | 행동 메뉴 | 현재 3단계 라우트 예시 |
| --- | --- | --- |
| 창고 | 입고 내역 | `/shipper/inbound/requests` |
| 창고 | 재고 목록 | `/shipper/warehouse/inventory` |
| 창고 | 현장 스캔 | `/shipper/warehouse/scan` |
| 운송 | 운송 업무 | `/shipper/transport` |
| 운송 의뢰 | 의뢰 상세 | `/shipper/request/{RequestId}` |
| 상품·판매채널 | 판매 주문 원장·상세 | `/shipper/sales/orders`, `/shipper/sales/orders/{OrderId}` |
| 상품·판매채널 | 로컬 주문 이행 허브·목표별 Simulation | `/shipper/sales/fulfillment`와 `/samples`, `/orders`, `/inventory`, `/picking`, `/packing`, `/restock-policy` 하위 route |
| 마트 주문 | 피킹·포장 | `/mart/picking` |
| 운송 원장 | 이벤트·증빙·정산 | `/transports/{RequestId}/events`, `/proofs`, `/settlement` |

위 표는 내비게이션 계약의 목표 연결이다. 공통 노드 행동 메뉴와 다이어그램 문맥 전달이 아직 연결되지 않은 항목은 기존 페이지가 있어도 3단계 연결 완료로 보지 않는다.

## 이전 코드 프로젝트 요약 (이력 보존)

| 코드 프로젝트 | 페이지 수 | 주 사용자 | 현재 포함 화면 |
| --- | ---: | --- | --- |
| `SsalddelApp` | 30 | 화주, 창고 관리자, 판매자, 물류 의뢰자 | 통합 커뮤니티, 역할 전환, 운송 의뢰, 창고/입고, 꾸미기 상점 |
| `DriverApp` | 23 | 기사 | 운행 시작, 추천, 수락/거절, 상차/하차, 정산, 알림 |
| `SsalddelAdmin` | 42 | 관리자, 운영자 | 배차, 운송 원장, 문서/POD, 결제/정산, 정책 운영 |
| `SsalddelAdminApp` | 6 | 커뮤니티 운영자 | 공통 홈, 커뮤니티 운영, 출처 자료 검토·글쓰기, 반야 선별 운영 |
| `WarehouseManagerApp` | 13 | 창고 관리자, 작업자 | 작업 보드, 입고 검수, 스캔, 피킹 배치, 알뜰살뜰 마트 창고 |
| `OrdererApp` | 11 | 주문자, 공동구매 참여자 | 공동구매, 음식/마트 주문, 화물 주문, 주문 이력 |
| `RestaurantDeskApp` | 5 | 음식점/매장 운영자 | 주변/인기 음식점, 리뷰 관리, 배차 주소 |
| `HumanResourcesManagerApp` | 1 | 인사/고용 담당자 | 인사 관리 홈 |

## 이전 캡처 진행 현황 (이번 재캡처 없음)

| 상태 | 수 | 의미 |
| --- | ---: | --- |
| 완료 | 121 | 현재 문서에서 인라인 이미지 또는 실제 렌더링 변경 기록으로 확인할 수 있는 화면 |
| 인증 필요 | 0 | 관리자 보호 라우트도 개발용 인증 세션과 문서용 메모리 데이터로 운영 화면까지 캡처한 상태 |
| 캡처 대기 | 4 | 운송 업무, 내 꾸미기 만들기, Admin Fake PG/정산, 마트 피킹/포장 전용 캡처 필요 |

## SsalddelApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `SsalddelApp-P00 - 역할·업무 선택` | `/` | `SsalddelApp/Components/Pages/UnifiedHome.razor` | 보조 | 커뮤니티에서 필요할 때 현재 역할에 맞는 화주 또는 창고 관리자 업무 홈 선택 | `SsalddelApp-P00-C` | 기존 화면 보존 |
| `SsalddelApp-P00-C - 공개 커뮤니티 홈` | `/community` · App 시작 경로 | `SsalddelApp/Components/Pages/CommunityHomePage.razor`<br>`Ssalddel.Ui.Common/.../CommunityIndividualOrderJourneyPanel.razor` | 0.0 필수 | 정보·이야기·공공데이터를 먼저 둘러보고 0.5 개별주문으로 선택 진입 | - | [변경 기록](../Changes/2026-07-24-community-orderer-harmony.md)<br><img src="../assets/changes/2026-07-24-community-orderer-harmony/ssalddel-community-order-flow.png" alt="SsalddelApp 공개 커뮤니티 홈" width="160"> |
| `SsalddelApp-P00-C1 - 내 개별주문 시작` | `/community/orders/new`<br>호환 `/community/group-purchase/demand` | `SsalddelApp/Components/Pages/CommunityGroupPurchaseDemandPage.razor`<br>`Ssalddel.Ui.Common/.../OfficialFoodIngredientDemandScreen.razor` | 0.5 | 공개 재료 근거와 개인 조건을 확인하고 별도 동의 뒤 철회 가능한 개별주문 의향 저장 | `SsalddelApp-P00-C` | route·Windows build·공용 Screen test 확인 |
| `SsalddelApp-P00-C2 - 내 개별주문 원장` | `/community/orders` | `SsalddelApp/Components/Pages/CommunityIndividualOrdersPage.razor`<br>`Ssalddel.Ui.Common/.../CommunityIndividualOrderListScreen.razor` | 0.5 | 로그인한 본인의 개별주문과 공동 할인 후보 연결 상태 조회·철회 | `SsalddelApp-P00-C1` | route·Windows build·공용 원장 ViewModel 확인 |
| `SsalddelApp-P01 - 화주 업무 홈, 운송 의뢰/상태/창고/판매 업무 진입` | `/shipper` | `SsalddelApp/Components/Pages/Home.razor`<br>`SsalddelApp/Components/Shared/ShipperHomeAppShell.razor`<br>`Ssalddel.Ui.Common/.../Shipper/ShipperHomeScreen.razor` | 필수 | 커뮤니티와 읽기 전용 화주 업무 요약, 기능 플래그별 목적지 선택 | `SsalddelApp-P01` | 공용 Screen Web host 실제 확인: [변경 기록](../Changes/2026-07-22-shipper-home-route-srp.md)<br><img src="../assets/changes/2026-07-22-shipper-home-route-srp/shipper-home-mobile-narrow.png" alt="SsalddelApp-P01 공용 Screen 좁은 viewport" width="160"> |
| `SsalddelApp-P01-1 - 화주 프로필과 운영 프로필 설정` | `/shipper/settings/profile` | `SsalddelApp/Components/Pages/ShipperProfileSettings.razor` | 보조 | 화주 프로필과 운영 프로필 설정 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P01-1.png" alt="SsalddelApp-P01-1" width="160"> |
| `SsalddelApp-P01-2 - 화주 앱 메뉴/화면 노출 설정` | `/shipper/settings/views` | `SsalddelApp/Components/Pages/ShipperViewSettings.razor` | 보조 | 화주 앱 메뉴/화면 노출 설정 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P01-2.png" alt="SsalddelApp-P01-2" width="160"> |
| `SsalddelApp-P01-3 - 공개 화물 또는 공개 의뢰 확인` | `/shipper/public-cargo` | `SsalddelApp/Components/Pages/PublicCargo.razor` | 확장 | 공개 화물 또는 공개 의뢰 확인 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P01-3.png" alt="SsalddelApp-P01-3" width="160"> |
| `SsalddelApp-P01-4 - 탐색/제안성 업무 수신함` | `/shipper/exploration/inbox` | `SsalddelApp/Components/Pages/ExplorationInbox.razor` | 확장 | 탐색/제안성 업무 수신함 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P01-4.png" alt="SsalddelApp-P01-4" width="160"> |
| `SsalddelApp-P02 - 운송 의뢰 작성` | `/shipper/request` → `/cargo`<br>`/transport`<br>`/procedure`<br>`/review` | `SsalddelApp/Components/Pages/ShipperRequest*Page.razor`<br>`Ssalddel.Ui.Common/.../Transport/ShipperRequest*Screen.razor` | 필수 | 화물·운송·절차·최종 확인을 같은 draft로 단계 작성 | `SsalddelApp-P02` | 재캡처 완료: [변경 기록](../Changes/2026-07-22-shipper-request-authoring-srp.md) |
| `SsalddelApp-P02-1 - 운송 의뢰 대량 등록` | `/shipper/request/bulk` | `SsalddelApp/Components/Pages/ShipperBulkImport.razor` | 보조 | 운송 의뢰 대량 등록 | `SsalddelApp-P02-1` | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P02-1.png" alt="SsalddelApp-P02-1" width="160"> |
| `SsalddelApp-P02-2 - 배차 주소 입력/검증 폼` | `/dispatch/address-form` | `SsalddelApp/Components/Pages/DispatchAddressForm.razor` | 보조 | 배차 주소 입력/검증 폼 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P02-2.png" alt="SsalddelApp-P02-2" width="160"> |
| `SsalddelApp-P03 - 의뢰 상세, 결제/배차/상차/하차/정산 타임라인` | `/shipper/request/{RequestId}` | `SsalddelApp/Components/Pages/ShipperRequestDetail.razor` | 필수 | 의뢰 상세, 결제/배차/상차/하차/정산 타임라인 | `SsalddelApp-P03` | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P03.png" alt="SsalddelApp-P03" width="160"> |
| `SsalddelApp-P04 - 화주 입고 업무 대시보드` | `/shipper/inbound/dashboard` | `SsalddelApp/Components/Pages/InboundDashboard.razor` | 확장 | 화주 입고 업무 대시보드 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P04.png" alt="SsalddelApp-P04" width="160"> |
| `SsalddelApp-P04-1 - 입고 요청 목록과 처리` | `/shipper/inbound/requests` | `SsalddelApp/Components/Pages/InboundRequests.razor` | 확장 | 입고 요청 목록과 처리 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P04-1.png" alt="SsalddelApp-P04-1" width="160"> |
| `SsalddelApp-P05 - 화주 관점 창고 업무 허브` | `/shipper/warehouse/workspace` | `SsalddelApp/Components/Pages/WarehouseWorkspace.razor` | 확장 | 화주 관점 창고 업무 허브 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P05.png" alt="SsalddelApp-P05" width="160"> |
| `SsalddelApp-P05-1 - 창고 재고 조회` | `/shipper/warehouse/inventory` | `SsalddelApp/Components/Pages/WarehouseInventory.razor` | 확장 | 창고 재고 조회 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P05-1.png" alt="SsalddelApp-P05-1" width="160"> |
| `SsalddelApp-P05-2 - 창고 스캔 작업` | `/shipper/warehouse/scan` | `SsalddelApp/Components/Pages/WarehouseScanStation.razor` | 확장 | 창고 스캔 작업 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P05-2.png" alt="SsalddelApp-P05-2" width="160"> |
| `SsalddelApp-P05-3 - 창고 프로세스별 작업 시작` | `/shipper/warehouse/work/{ProcessCode}` | `SsalddelApp/Components/Pages/WarehouseWorkStart.razor` | 확장 | 창고 프로세스별 작업 시작 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P05-3.png" alt="SsalddelApp-P05-3" width="160"> |
| `SsalddelApp-P06 - 판매채널 연결/관리` | `/shipper/sales/channels` | `SsalddelApp/Components/Pages/SalesChannels.razor` | 확장 | 판매채널 연결/관리 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P06.png" alt="SsalddelApp-P06" width="160"> |
| `SsalddelApp-P06-1 - 상품 등록/리스팅` | `/shipper/sales/listings` | `SsalddelApp/Components/Pages/ProductListings.razor` | 확장 | 상품 등록/리스팅 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P06-1.png" alt="SsalddelApp-P06-1" width="160"> |
| `SsalddelApp-P06-2 - 판매 주문 이행 Simulation 허브` | `/shipper/sales/fulfillment` | `SsalddelApp/Components/Pages/OrderFulfillment.razor` | 개발·확장 | 상태 변경 없는 로컬 주문 이행 목표 탐색 | - | 실제 desktop·501px 확인<br><img src="../assets/changes/2026-07-22-order-fulfillment-route-srp/fulfillment-hub-mobile-501.png" alt="SsalddelApp-P06-2" width="160"> |
| `SsalddelApp-P06-2-1 - 판매 주문 원장 목록` | `/shipper/sales/orders` | `SsalddelApp/Components/Pages/SalesOrders.razor` | 확장 | 영속 판매 주문 검색·필터·페이지 조회 | - | 공용 Screen Web host 확인<br><img src="../assets/changes/2026-07-22-sales-order-mobile-route-srp/sales-orders-desktop.png" alt="SsalddelApp-P06-2-1 공용 Screen" width="160"> |
| `SsalddelApp-P06-2-2 - 판매 주문 원장 상세` | `/shipper/sales/orders/{OrderId}` | `SsalddelApp/Components/Pages/SalesOrderDetail.razor` | 확장 | stable order ID의 주문·출고 투영 읽기 | - | 공용 Screen Web host 390px 확인<br><img src="../assets/changes/2026-07-22-sales-order-mobile-route-srp/sales-order-detail-mobile.png" alt="SsalddelApp-P06-2-2 공용 Screen" width="160"> |
| `SsalddelApp-P06-2-3 - Simulation 샘플 반영` | `/shipper/sales/fulfillment/samples` | `SsalddelApp/Components/Pages/OrderFulfillmentSamples.razor` | 개발·확장 | 외부 수집 없는 비식별 로컬 샘플 준비 | - | route·build 확인 |
| `SsalddelApp-P06-2-4 - Simulation 주문 목록` | `/shipper/sales/fulfillment/orders` | `SsalddelApp/Components/Pages/OrderFulfillmentOrders.razor` | 개발·확장 | 로컬 주문 후보 검색·필터 | - | 실제 desktop·501px 확인<br><img src="../assets/changes/2026-07-22-order-fulfillment-route-srp/simulation-orders-mobile-501.png" alt="SsalddelApp-P06-2-4" width="160"> |
| `SsalddelApp-P06-2-4-1 - Simulation 주문 상세` | `/shipper/sales/fulfillment/orders/{OrderKey}` | `SsalddelApp/Components/Pages/OrderFulfillmentOrderDetail.razor` | 개발·확장 | opaque stable 주문 key 한 건 조회 | - | route·build 확인 |
| `SsalddelApp-P06-2-5 - 재고·입고 신호` | `/shipper/sales/fulfillment/inventory` | `SsalddelApp/Components/Pages/OrderFulfillmentInventory.razor` | 개발·확장 | 로컬 재고 snapshot과 입고 검토 신호 조회 | - | route·build 확인 |
| `SsalddelApp-P06-2-6 - 피킹 task 목록` | `/shipper/sales/fulfillment/picking` | `SsalddelApp/Components/Pages/OrderFulfillmentPicking.razor` | 개발·확장 | 피킹 task 목록과 stable ID 선택 | - | route·build 확인 |
| `SsalddelApp-P06-2-6-1 - 피킹 task Action` | `/shipper/sales/fulfillment/picking/{TaskId}` | `SsalddelApp/Components/Pages/OrderFulfillmentPickingTask.razor` | 개발·확장 | 정확한 task의 로컬 스캔·보류·취소 | - | route·build 확인 |
| `SsalddelApp-P06-2-7 - 포장 task 목록` | `/shipper/sales/fulfillment/packing` | `SsalddelApp/Components/Pages/OrderFulfillmentPacking.razor` | 개발·확장 | 포장 task 목록과 stable ID 선택 | - | route·build 확인 |
| `SsalddelApp-P06-2-7-1 - 포장 task Action` | `/shipper/sales/fulfillment/packing/{TaskId}` | `SsalddelApp/Components/Pages/OrderFulfillmentPackingTask.razor` | 개발·확장 | 정확한 task의 로컬 시작·완료 | - | route·build 확인 |
| `SsalddelApp-P06-2-8 - 입고 알림 정책` | `/shipper/sales/fulfillment/restock-policy` | `SsalddelApp/Components/Pages/OrderFulfillmentRestockPolicy.razor` | 개발·확장 | 판매자별 로컬 동의·발송 의도 편집 | - | route·build 확인 |
| `SsalddelApp-P07 - FCL/LCL 해외 물류 계획` | `/shipper/international/fcl-lcl` | `SsalddelApp/Components/Pages/FclLclPlanner.razor` | 확장 | FCL/LCL 해외 물류 계획 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P07.png" alt="SsalddelApp-P07" width="160"> |
| `SsalddelApp-P07-1 - HS 코드/통관 검토` | `/shipper/customs/hs-reviews` | `SsalddelApp/Components/Pages/CustomsHsReviews.razor` | 확장 | HS 코드/통관 검토 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P07-1.png" alt="SsalddelApp-P07-1" width="160"> |
| `SsalddelApp-P08 - 재위탁/재운송 주문` | `/shipper/reconsignment/orders` | `SsalddelApp/Components/Pages/ReconsignmentOrders.razor` | 확장 | 재위탁/재운송 주문 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P08.png" alt="SsalddelApp-P08" width="160"> |
| `SsalddelApp-P09 - 운송 업무 워크스페이스` | `/shipper/transport` | `SsalddelApp/Components/Pages/TransportWorkspace.razor` | 필수 | 의뢰별 결제·배차·운송 진행 상태와 다음 행동 처리 | - | 캡처 대기 |
| `SsalddelApp-P10 - 꾸미기 상점` | `/community/decorations` | `SsalddelApp/.../CommunityDecorationStorePage.razor`<br>`Ssalddel.Ui.Common/.../CommunityDecorationStoreScreen.razor` | 확장 | 홈 테마·노드·괘상 상품 읽기 전용 탐색 | - | Web·모바일 공용 Screen 실제 확인<br><img src="../assets/changes/2026-07-22-community-decoration-route-srp/decoration-store-mobile-390.png" alt="SsalddelApp-P10" width="160"> |
| `SsalddelApp-P10-1 - 꾸미기 상품 상세` | `/community/decorations/products/{ProductKey}`<br>legacy `/{ProductKey}` | `SsalddelApp/.../CommunityDecorationDetailPage.razor`<br>`Ssalddel.Ui.Common/.../CommunityDecorationProductScreen.razor` | 확장 | stable-key 미리보기와 이미 보유한 꾸미기의 명시적 적용 선택, 구매 없음 | - | Web·모바일 공용 Screen 실제 확인<br><img src="../assets/changes/2026-07-22-community-decoration-route-srp/decoration-product-mobile-390.png" alt="SsalddelApp-P10-1" width="160"> |
| `SsalddelApp-P10-2 - 꾸미기 FakePG 결제·완료` | `/community/decorations/checkout/{ProductKey}`<br>legacy `/{ProductKey}/checkout` | `SsalddelApp/.../CommunityDecorationCheckoutPage.razor`<br>`Ssalddel.Ui.Common/.../CommunityDecorationCheckoutScreen.razor` | 개발·확장 | 로그인·명시적 동의 뒤 실제 청구 없는 보유권 Simulation과 별도 적용 선택 | - | Web·모바일 공용 Screen 실제 확인<br><img src="../assets/changes/2026-07-22-community-decoration-route-srp/decoration-checkout-mobile-390.png" alt="SsalddelApp-P10-2" width="160"> |
| `SsalddelApp-P10-3 - 내 꾸미기 만들기` | `/community/decorations/create` | `SsalddelApp/Components/Pages/CommunityDecorationCreatePage.razor` | 확장 | 개인 괘상·다이어그램 노드 이미지 제작 | - | 캡처 대기 |
| `SsalddelApp-P10-4 - 디자이너 홈 테마 패키지 등록` | `/community/decorations/themes/submit` | `SsalddelApp/Components/Pages/CommunityDecorationThemeSubmitPage.razor` | 확장·제작 | 8개 시각 슬롯 패키지 제작과 미리보기 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P10-4.png" alt="SsalddelApp-P10-4" width="160"> |
| `SsalddelApp-P90 - 템플릿/샘플성 날씨 화면` | `/weather` | `SsalddelApp/Components/Pages/Weather.razor` | 시스템 | 템플릿/샘플성 날씨 화면 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P90.png" alt="SsalddelApp-P90" width="160"> |
| `SsalddelApp-P91 - 템플릿/샘플성 카운터 화면` | `/counter` | `SsalddelApp/Components/Pages/Counter.razor` | 시스템 | 템플릿/샘플성 카운터 화면 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P91.png" alt="SsalddelApp-P91" width="160"> |
| `SsalddelApp-P99 - 미발견 페이지` | `/not-found` | `SsalddelApp/Components/Pages/NotFound.razor` | 시스템 | 미발견 페이지 | - | 완료<br><img src="assets/app-pages/SsalddelApp/SsalddelApp-P99.png" alt="SsalddelApp-P99" width="160"> |

## DriverApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `DriverApp-P00 - 기사 앱 시작 라우트 리다이렉트` | `/` | `DriverApp/Components/Pages/RootRedirect.razor` | 시스템 | 기사 앱 시작 라우트 리다이렉트 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P00.png" alt="DriverApp-P00" width="160"> |
| `DriverApp-P01 - 로그인` | `/login` | `DriverApp/Components/Pages/Login.razor` | 시스템 | 로그인 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P01.png" alt="DriverApp-P01" width="160"> |
| `DriverApp-P02 - 기사 앱 메뉴` | `/driver/menu` | `DriverApp/Components/Pages/Driver/04_Settings/메뉴Page.razor` | 보조 | 기사 앱 메뉴 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P02.png" alt="DriverApp-P02" width="160"> |
| `DriverApp-P02-1 - 기사 앱 화면 노출 설정` | `/driver/settings/views` | `DriverApp/Components/Pages/Driver/04_Settings/화면설정Page.razor` | 보조 | 기사 앱 화면 노출 설정 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P02-1.png" alt="DriverApp-P02-1" width="160"> |
| `DriverApp-P03 - 예약 운송 또는 예약 업무` | `/driver/reservations` | `DriverApp/Components/Pages/Driver/04_Reservation/예약Page.razor` | 확장 | 예약 운송 또는 예약 업무 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P03.png" alt="DriverApp-P03" width="160"> |
| `DriverApp-P04 - 탐색 캠페인/추천 확장` | `/driver/exploration/campaigns` | `DriverApp/Components/Pages/Driver/02_Recommendation/탐색캠페인Page.razor` | 확장 | 탐색 캠페인/추천 확장 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P04.png" alt="DriverApp-P04" width="160"> |
| `DriverApp-P05 - 운송/배달 이력 조회` | `/driver/transports/history` | `DriverApp/Components/Pages/Driver/03_Progress/배달내역Page.razor` | 보조 | 운송/배달 이력 조회 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P05.png" alt="DriverApp-P05" width="160"> |
| `DriverApp-P06 - 운행 시작, 위치 송신 시작` | `/driver/work/start` | `DriverApp/Components/Pages/Driver/01_Work/운행시작Page.razor` | 필수 | 운행 시작, 위치 송신 시작 | `DriverApp-P06` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P06.png" alt="DriverApp-P06" width="160"> |
| `DriverApp-P06-1 - 운행 조건과 선호 설정` | `/driver/work/settings` | `DriverApp/Components/Pages/Driver/04_Settings/운행설정Page.razor` | 보조 | 운행 조건과 선호 설정 | `DriverApp-P06-1` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P06-1.png" alt="DriverApp-P06-1" width="160"> |
| `DriverApp-P07 - 지도 홈, 추천 배너, 현재 운송 진입` | `/driver/home` | `DriverApp/Components/Pages/Home.razor` | 필수 | 지도 홈, 추천 배너, 현재 운송 진입 | `DriverApp-P07` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P07.png" alt="DriverApp-P07" width="160"> |
| `DriverApp-P07-1 - 기사 업무 허브/요약` | `/driver/home/summary` | `DriverApp/Components/Pages/Driver/Home/기사홈Page.razor` | 보조 | 기사 업무 허브/요약 | `DriverApp-P07-1` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P07-1.png" alt="DriverApp-P07-1" width="160"> |
| `DriverApp-P08 - 추천 목록` | `/driver/recommendations` | `DriverApp/Components/Pages/Driver/02_Recommendation/추천목록Page.razor` | 필수 | 추천 목록 | `DriverApp-P08` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P08.png" alt="DriverApp-P08" width="160"> |
| `DriverApp-P09 - 추천 상세와 판단 정보` | `/driver/recommendations/{의뢰Id}` | `DriverApp/Components/Pages/Driver/02_Recommendation/추천상세Page.razor` | 필수 | 추천 상세와 판단 정보 | `DriverApp-P09` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P09.png" alt="DriverApp-P09" width="160"> |
| `DriverApp-P10 - 추천 수락/거절/보류 처리` | `/driver/recommendations/{의뢰Id}/decision` | `DriverApp/Components/Pages/Driver/02_Recommendation/배차처리Page.razor` | 필수 | 추천 수락/거절/보류 처리 | `DriverApp-P10` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P10.png" alt="DriverApp-P10" width="160"> |
| `DriverApp-P11 - 진행 중 운송과 다음 행동` | `/driver/transports/current` | `DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor` | 필수 | 진행 중 운송과 다음 행동 | `DriverApp-P11` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P11.png" alt="DriverApp-P11" width="160"> |
| `DriverApp-P12 - 상차 증빙, 상차 예외` | `/driver/transports/{운송Id:long}/pickup` | `DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor` | 필수 | 상차 증빙, 상차 예외 | `DriverApp-P12` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P12.png" alt="DriverApp-P12" width="160"> |
| `DriverApp-P13 - 하차 증빙, POD, 하차 예외` | `/driver/transports/{운송Id:long}/dropoff` | `DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor` | 필수 | 하차 증빙, POD, 하차 예외 | `DriverApp-P13` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P13.png" alt="DriverApp-P13" width="160"> |
| `DriverApp-P14 - 월정산 확인` | `/driver/settlements/current-month` | `DriverApp/Components/Pages/Driver/05_Settlement/월정산Page.razor` | 필수 | 월정산 확인 | `DriverApp-P14` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P14.png" alt="DriverApp-P14" width="160"> |
| `DriverApp-P14-1 - 이용료/정산 정책 안내` | `/driver/settlements/info` | `DriverApp/Components/Pages/Driver/05_Settlement/이용료안내Page.razor` | 보조 | 이용료/정산 정책 안내 | `DriverApp-P14-1` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P14-1.png" alt="DriverApp-P14-1" width="160"> |
| `DriverApp-P14-2 - 기사 정산 계좌 정보` | `/driver/account/bank` | `DriverApp/Components/Pages/Driver/05_Settlement/계좌정보Page.razor` | 보조 | 기사 정산 계좌 정보 | - | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P14-2.png" alt="DriverApp-P14-2" width="160"> |
| `DriverApp-P15 - 알림함` | `/driver/notifications` | `DriverApp/Components/Pages/Driver/06_Notification/알림함Page.razor` | 필수 | 알림함 | `DriverApp-P15` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P15.png" alt="DriverApp-P15" width="160"> |
| `DriverApp-P15-1 - 알림 수신 설정` | `/driver/notifications/settings` | `DriverApp/Components/Pages/Driver/04_Settings/알림설정Page.razor` | 보조 | 알림 수신 설정 | `DriverApp-P15-1` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P15-1.png" alt="DriverApp-P15-1" width="160"> |
| `DriverApp-P15-2 - 푸시 토큰/권한 설정` | `/driver/notifications/push` | `DriverApp/Components/Pages/Driver/06_Notification/푸시설정Page.razor` | 보조 | 푸시 토큰/권한 설정 | `DriverApp-P15-2` | 완료<br><img src="assets/app-pages/DriverApp/DriverApp-P15-2.png" alt="DriverApp-P15-2" width="160"> |

## SsalddelAdmin

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `SsalddelAdmin-P00 - 관리자 홈` | `/` | `SsalddelAdmin/Components/Pages/Home.razor` | 시스템 | 관리자 홈 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P00.png" alt="SsalddelAdmin-P00" width="160"> |
| `SsalddelAdmin-P00-1 - 관리자 로그인` | `/login` | `SsalddelAdmin/Components/Pages/AdminLogin.razor` | 시스템 | 관리자 로그인 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P00-1.png" alt="SsalddelAdmin-P00-1" width="160"> |
| `SsalddelAdmin-P00-2 - 오류 화면` | `/Error` | `SsalddelAdmin/Components/Pages/Error.razor` | 시스템 | 오류 화면 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P00-2.png" alt="SsalddelAdmin-P00-2" width="160"> |
| `SsalddelAdmin-P16 - 운영 대시보드` | `/dashboard` | `SsalddelAdmin/Components/Pages/Dashboard.razor` | 필수 | 운영 대시보드 | `SsalddelAdmin-P16` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P16.png" alt="SsalddelAdmin-P16" width="160"> |
| `SsalddelAdmin-P17 - 의뢰 목록` | `/requests` | `SsalddelAdmin/Components/Pages/Requests.razor` | 필수 | 의뢰 목록 | `SsalddelAdmin-P17` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P17.png" alt="SsalddelAdmin-P17" width="160"> |
| `SsalddelAdmin-P18 - 의뢰 상세` | `/requests/{RequestId}` | `SsalddelAdmin/Components/Pages/RequestDetail.razor` | 필수 | 의뢰 상세 | `SsalddelAdmin-P18` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P18.png" alt="SsalddelAdmin-P18" width="160"> |
| `SsalddelAdmin-P19 - 배차대기/추천 잠금 상태` | `/dispatch/wait` | `SsalddelAdmin/Components/Pages/DispatchWait.razor` | 필수 | 배차대기/추천 잠금 상태 | `SsalddelAdmin-P19` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P19.png" alt="SsalddelAdmin-P19" width="160"> |
| `SsalddelAdmin-P20 - 운행 중 기사 현황` | `/drivers/operating` | `SsalddelAdmin/Components/Pages/DriverOperatingView.razor` | 필수 | 운행 중 기사 현황 | `SsalddelAdmin-P20` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P20.png" alt="SsalddelAdmin-P20" width="160"> |
| `SsalddelAdmin-P21 - 운송 목록` | `/transports` | `SsalddelAdmin/Components/Pages/Transports.razor` | 필수 | 운송 목록 | `SsalddelAdmin-P21` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P21.png" alt="SsalddelAdmin-P21" width="160"> |
| `SsalddelAdmin-P22 - 운송 상세 원장` | `/transports/{RequestId}` | `SsalddelAdmin/Components/Pages/TransportWorkflowDetail.razor` | 필수 | 운송 상세 원장 | `SsalddelAdmin-P22` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P22.png" alt="SsalddelAdmin-P22" width="160"> |
| `SsalddelAdmin-P22-1 - 운송 이벤트 감사` | `/transports/{RequestId}/events` | `SsalddelAdmin/Components/Pages/TransportWorkflowEvents.razor` | 필수 | 운송 이벤트 감사 | `SsalddelAdmin-P22-1` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P22-1.png" alt="SsalddelAdmin-P22-1" width="160"> |
| `SsalddelAdmin-P22-2 - 운송 증빙/POD` | `/transports/{RequestId}/proofs` | `SsalddelAdmin/Components/Pages/TransportWorkflowProofs.razor` | 필수 | 운송 증빙/POD | `SsalddelAdmin-P22-2` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P22-2.png" alt="SsalddelAdmin-P22-2" width="160"> |
| `SsalddelAdmin-P22-3 - 운송 정산 상세` | `/transports/{RequestId}/settlement` | `SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor` | 필수 | 운송 정산 상세 | `SsalddelAdmin-P22-3` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P22-3.png" alt="SsalddelAdmin-P22-3" width="160"> |
| `SsalddelAdmin-P23 - 관리자 활동 로그` | `/activity-logs` | `SsalddelAdmin/Components/Pages/ActivityLogs.razor` | 운영 | 관리자 활동 로그 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P23.png" alt="SsalddelAdmin-P23" width="160"> |
| `SsalddelAdmin-P24 - 화면/기능 노출 정책` | `/view-policies` | `SsalddelAdmin/Components/Pages/ViewPolicies.razor` | 운영 | 화면/기능 노출 정책 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P24.png" alt="SsalddelAdmin-P24" width="160"> |
| `SsalddelAdmin-P25 - 공통 콘텐츠 관리` | `/common-contents` | `SsalddelAdmin/Components/Pages/CommonContents.razor` | 운영 | 공통 콘텐츠 관리 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P25.png" alt="SsalddelAdmin-P25" width="160"> |
| `SsalddelAdmin-P26 - 결제 목록` | `/payments` | `SsalddelAdmin/Components/Pages/Payments.razor` | 필수 | 결제 목록 | `SsalddelAdmin-P26` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P26.png" alt="SsalddelAdmin-P26" width="160"> |
| `SsalddelAdmin-P26-1 - 정산 목록` | `/settlements` | `SsalddelAdmin/Components/Pages/Settlements.razor` | 필수 | 정산 목록 | `SsalddelAdmin-P26-1` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P26-1.png" alt="SsalddelAdmin-P26-1" width="160"> |
| `SsalddelAdmin-P27 - 문서 목록` | `/documents` | `SsalddelAdmin/Components/Pages/Documents.razor` | 필수 | 문서 목록 | `SsalddelAdmin-P27` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P27.png" alt="SsalddelAdmin-P27" width="160"> |
| `SsalddelAdmin-P27-1 - 문서 업로드` | `/documents/upload` | `SsalddelAdmin/Components/Pages/DocumentUpload.razor` | 필수 | 문서 업로드 | `SsalddelAdmin-P27-1` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P27-1.png" alt="SsalddelAdmin-P27-1" width="160"> |
| `SsalddelAdmin-P27-2 - 문서 정책 목록` | `/documents/policies` | `SsalddelAdmin/Components/Pages/DocumentPolicies.razor` | 필수 | 문서 정책 목록 | `SsalddelAdmin-P27-2` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P27-2.png" alt="SsalddelAdmin-P27-2" width="160"> |
| `SsalddelAdmin-P27-3 - 문서 정책 상세` | `/documents/policies/{DocumentCode}` | `SsalddelAdmin/Components/Pages/DocumentPolicyDetail.razor` | 필수 | 문서 정책 상세 | `SsalddelAdmin-P27-3` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P27-3.png" alt="SsalddelAdmin-P27-3" width="160"> |
| `SsalddelAdmin-P27-4 - 문서 조회 로그` | `/documents/logs` | `SsalddelAdmin/Components/Pages/DocumentLogs.razor` | 필수 | 문서 조회 로그 | `SsalddelAdmin-P27-4` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P27-4.png" alt="SsalddelAdmin-P27-4" width="160"> |
| `SsalddelAdmin-P27-5 - 파일/POD 관리` | `/files/pod` | `SsalddelAdmin/Components/Pages/FilesPod.razor` | 필수 | 파일/POD 관리 | `SsalddelAdmin-P27-5` | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P27-5.png" alt="SsalddelAdmin-P27-5" width="160"> |
| `SsalddelAdmin-P28 - 공개 화물/화물 운영 화면` | `/cargo` | `SsalddelAdmin/Components/Pages/PublicCargo.razor` | 운영 | 공개 화물/화물 운영 화면 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P28.png" alt="SsalddelAdmin-P28" width="160"> |
| `SsalddelAdmin-P29 - HS 코드/통관 운영` | `/customs/hs-codes` | `SsalddelAdmin/Components/Pages/HsCodeOperations.razor` | 운영 | HS 코드/통관 운영 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P29.png" alt="SsalddelAdmin-P29" width="160"> |
| `SsalddelAdmin-P30 - 음식 주문/배달 운영` | `/food/operations` | `SsalddelAdmin/Components/Pages/FoodOperations.razor` | 운영 | 음식 주문/배달 운영 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P30.png" alt="SsalddelAdmin-P30" width="160"> |
| `SsalddelAdmin-P30-1 - 음식점 검색 정책` | `/restaurant-search-policy` | `SsalddelAdmin/Components/Pages/RestaurantSearchPolicySettings.razor` | 운영 | 음식점 검색 정책 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P30-1.png" alt="SsalddelAdmin-P30-1" width="160"> |
| `SsalddelAdmin-P31 - 탐색 캠페인 운영` | `/exploration/campaigns` | `SsalddelAdmin/Components/Pages/ExplorationCampaigns.razor` | 운영 | 탐색 캠페인 운영 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P31.png" alt="SsalddelAdmin-P31" width="160"> |
| `SsalddelAdmin-P32 - 기사 목록/관리` | `/drivers` | `SsalddelAdmin/Components/Pages/Drivers.razor` | 운영 | 기사 목록/관리 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P32.png" alt="SsalddelAdmin-P32" width="160"> |
| `SsalddelAdmin-P32-1 - 차량 관리` | `/vehicle-management` | `SsalddelAdmin/Components/Pages/VehicleManagement.razor` | 운영 | 차량 관리 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P32-1.png" alt="SsalddelAdmin-P32-1" width="160"> |
| `SsalddelAdmin-P33 - 파트너 관리` | `/partners` | `SsalddelAdmin/Components/Pages/Partners.razor` | 운영 | 파트너 관리 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P33.png" alt="SsalddelAdmin-P33" width="160"> |
| `SsalddelAdmin-P34 - 수익/요율 정책` | `/revenue-policies` | `SsalddelAdmin/Components/Pages/RevenuePolicies.razor` | 운영 | 수익/요율 정책 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P34.png" alt="SsalddelAdmin-P34" width="160"> |
| `SsalddelAdmin-P35 - 보조 기능 설정` | `/auxiliary-feature-settings` | `SsalddelAdmin/Components/Pages/AuxiliaryFeatureSettings.razor` | 운영 | 보조 기능 설정 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P35.png" alt="SsalddelAdmin-P35" width="160"> |
| `SsalddelAdmin-P36 - 연락처 통합 검색` | `/contact-search` | `SsalddelAdmin/Components/Pages/ContactSearch.razor` | 운영 | 전화번호 뒤 8자리 기준 인물/역할 통합 조회 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P36.png" alt="SsalddelAdmin-P36" width="160"> |
| `SsalddelAdmin-P37 - 국내화물 AI 배차 검토` | `/dispatch/ai-review` | `SsalddelAdmin/Components/Pages/DomesticCargoDispatchAIReview.razor` | 운영 | 국내화물 AI 배차 검토 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P37.png" alt="SsalddelAdmin-P37" width="160"> |
| `SsalddelAdmin-P38 - 음식배달 AI 배차 검토` | `/dispatch/food-ai-review` | `SsalddelAdmin/Components/Pages/FoodDeliveryDispatchAIReview.razor` | 운영 | 음식배달 AI 배차 검토 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P38.png" alt="SsalddelAdmin-P38" width="160"> |
| `SsalddelAdmin-P39 - 배차 AI 판단 사례` | `/dispatch-ai-judgment-cases` | `SsalddelAdmin/Components/Pages/DispatchAIJudgmentCases.razor` | 운영 | 배차 AI 판단 사례 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P39.png" alt="SsalddelAdmin-P39" width="160"> |
| `SsalddelAdmin-P40 - 개발용 Fake PG/정산 콘솔` | `/development/fake-payment-settlement` | `SsalddelAdmin/Components/Pages/FakePaymentSettlementConsole.razor` | 개발 | 결제 보증·상하차·정산·보류·환불 상태 전이 시뮬레이션 | - | 캡처 대기 |
| `SsalddelAdmin-P90 - 템플릿/샘플성 날씨 화면` | `/weather` | `SsalddelAdmin/Components/Pages/Weather.razor` | 시스템 | 템플릿/샘플성 날씨 화면 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P90.png" alt="SsalddelAdmin-P90" width="160"> |
| `SsalddelAdmin-P91 - 템플릿/샘플성 카운터 화면` | `/counter` | `SsalddelAdmin/Components/Pages/Counter.razor` | 시스템 | 템플릿/샘플성 카운터 화면 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P91.png" alt="SsalddelAdmin-P91" width="160"> |
| `SsalddelAdmin-P99 - 미발견 페이지` | `/not-found` | `SsalddelAdmin/Components/Pages/NotFound.razor` | 시스템 | 미발견 페이지 | - | 완료<br><img src="assets/app-pages/SsalddelAdmin/SsalddelAdmin-P99.png" alt="SsalddelAdmin-P99" width="160"> |

## SsalddelAdminApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `SsalddelAdminApp-P00 - 커뮤니티 운영 홈` | `/` | `SsalddelAdminApp/Components/Pages/Home.razor` | 시스템 | 공통 커뮤니티 현황과 운영 진입 | - | 코드 확인 |
| `SsalddelAdminApp-P01 - 관리자 로그인` | `/login` | `SsalddelAdminApp/Components/Pages/Login.razor` | 시스템 | 서버관리자 인증 | - | 코드 확인 |
| `SsalddelAdminApp-P02 - 커뮤니티 운영` | `/community-management` | `SsalddelAdminApp/Components/Pages/CommunityManagement.razor` | 운영 | 커뮤니티 운영 상태와 관리 작업 | - | 코드 확인 |
| `SsalddelAdminApp-P03 - 자료 검토·글쓰기` | `/information-review` | `SsalddelAdminApp/Components/Pages/CommunityInformationReview.razor` | 운영 | 수집 자료의 출처·기준 검토와 커뮤니티 글 초안 작성 | `SsalddelAdminApp-P01` | 로그인 경계 확인<br><img src="../assets/changes/2026-07-18-admin-information-review/admin-information-review-mobile.png" alt="SsalddelAdminApp 자료 검토 모바일 화면" width="160"> |
| `SsalddelAdminApp-P04 - 반야 운영 허브` | `/prajna` | `SsalddelAdminApp/Components/Pages/PrajnaHub.razor` | 운영 | 지식·성찰 자료 선별 운영 | `SsalddelAdminApp-P01` | 코드 확인 |
| `SsalddelAdminApp-P04-1 - 홍익학당 선별 운영` | `/prajna/hongik-hakdang` | `SsalddelAdminApp/Components/Pages/PrajnaHongikHakdang.razor` | 운영 | 홍익학당 공개 자료 검토와 발행 | `SsalddelAdminApp-P04` | 코드 확인 |

## WarehouseManagerApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `WarehouseManagerApp-P01 - 창고 관리자 홈` | `/` | `WarehouseManagerApp/Components/Pages/Home.razor` | 보조 | 창고 관리자 홈 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P01.png" alt="WarehouseManagerApp-P01" width="160"> |
| `WarehouseManagerApp-P02 - 일반 창고 작업 보드` | `/work-board` | `WarehouseManagerApp/Components/Pages/WorkBoard.razor` | 확장 | 일반 창고 작업 보드 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P02.png" alt="WarehouseManagerApp-P02" width="160"> |
| `WarehouseManagerApp-P02-1 - 프로세스별 창고 작업 시작` | `/work/{ProcessCode}` | `WarehouseManagerApp/Components/Pages/WorkStart.razor` | 확장 | 프로세스별 창고 작업 시작 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P02-1.png" alt="WarehouseManagerApp-P02-1" width="160"> |
| `WarehouseManagerApp-P02-2 - 작업대 스캔` | `/work/{ProcessCode}/workbench` | `WarehouseManagerApp/Components/Pages/WorkbenchScan.razor` | 확장 | 작업대 스캔 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P02-2.png" alt="WarehouseManagerApp-P02-2" width="160"> |
| `WarehouseManagerApp-P02-3 - 범용 스캔 스테이션` | `/scan` | `WarehouseManagerApp/Components/Pages/ScanStation.razor` | 확장 | 범용 스캔 스테이션 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P02-3.png" alt="WarehouseManagerApp-P02-3" width="160"> |
| `WarehouseManagerApp-P03 - 입고 검수 목록·상세·실행` | `/work/inbound/inspection`<br>`/work/inbound/inspection/{InboundItemId}`<br>`/work/inbound/inspection/{InboundItemId}/record` | `WarehouseManagerApp/Components/Pages/InboundInspection*.razor` | 확장 | 목록 조회·stable-ID 상세·명시적 검수 Command와 같은 ID 재조회 | - | Web route desktop·390px 실제 재검증<br><img src="../assets/changes/2026-07-22-inbound-inspection-route-srp/inbound-inspection-list-desktop.png" alt="WarehouseManagerApp-P03 공용 입고 검수 목록" width="160"> |
| `WarehouseManagerApp-P03-1 - 입고상품 수령` | `/work/inbound/products` | `WarehouseManagerApp/Components/Pages/InboundProductScan.razor` | 확장 | 정확한 입고예정 조회와 현장 반입 요청 | - | 완료<br><img src="../assets/changes/2026-07-20-inbound-receiving/warehouse-inbound-receiving-desktop.png" alt="WarehouseManagerApp-P03-1" width="160"> |
| `WarehouseManagerApp-P03-2 - 일반 재고 현황` | `/warehouse/general/inventory` | `WarehouseManagerApp/Components/Pages/GeneralInventory.razor` | 확장 | 창고 범위의 최소 재고 목록·서버 집계·정확한 입고상품 상세 | - | 실제 캡처<br><img src="../assets/changes/2026-07-20-warehouse-inventory/desktop.png" alt="WarehouseManagerApp-P03-2" width="160"> |
| `WarehouseManagerApp-P03-3 - 적재 작업` | `/work/inbound/put-away` | `WarehouseManagerApp/Components/Pages/PutAwayTask.razor` | 확장 | 검수 완료 재고 목록·정확한 상세·위치 확정·같은 ID 재조회 | - | 실제 캡처<br><img src="../assets/changes/2026-07-20-warehouse-put-away/desktop.png" alt="WarehouseManagerApp-P03-3" width="160"> |
| `WarehouseManagerApp-P04 - 피킹 작업 목록·상세·실행` | `/work/picking-batch`<br>`/work/picking-batch/{TaskKey}`<br>`/work/picking-batch/{TaskKey}/execute` | `WarehouseManagerApp/Components/Pages/PickingBatch*.razor` | 확장 | 목록 조회·stable-key 상세·명시적 시작/완료와 같은 Key 재조회 | - | Web route desktop·390px 실제 재검증<br><img src="../assets/changes/2026-07-22-picking-task-route-srp/picking-task-list-desktop.png" alt="WarehouseManagerApp-P04 공용 피킹 작업 목록" width="160"> |
| `WarehouseManagerApp-P04-1 - 포장 작업` | `/work/outbound/packing` | `WarehouseManagerApp/Components/Pages/PackingTask.razor` | 확장 | 적재 완료 재고의 전체 가용수량 포장·같은 ID 재조회 | - | 실제 캡처<br><img src="../assets/changes/2026-07-20-warehouse-packing/desktop.png" alt="WarehouseManagerApp-P04-1" width="160"> |
| `WarehouseManagerApp-P04-2 - 출고 인계 준비` | `/warehouse/general/transport-handoff` | `WarehouseManagerApp/Components/Pages/GeneralTransportHandoff.razor` | 확장 | 포장 완료 재고의 출고예정 원장 준비·같은 ID 재조회 | - | 실제 캡처<br><img src="../assets/changes/2026-07-20-warehouse-outbound-handoff/desktop.png" alt="WarehouseManagerApp-P04-2" width="160"> |
| `WarehouseManagerApp-P04-3 - 출고예정 운송 전 검토` | `/warehouse/general/outbound-plan-review` | `WarehouseManagerApp/Components/Pages/OutboundPlanReview.razor` | 확장 | 준비된 출고예정의 정확한 상세와 운송의뢰 생성 전 읽기 검토 | - | 실제 캡처<br><img src="../assets/changes/2026-07-20-warehouse-outbound-plan-review/desktop.png" alt="WarehouseManagerApp-P04-3" width="160"> |
| `WarehouseManagerApp-P04-4 - 운송의뢰 로컬 초안` | `/warehouse/general/transport-request-draft` | `WarehouseManagerApp/Components/Pages/TransportRequestDraft.razor` | 확장 | 정확한 출고예정의 하차지·희망 일정·차량 조건을 서버 저장 없이 검토 | - | 실제 캡처<br><img src="../assets/changes/2026-07-20-warehouse-transport-request-draft/desktop.png" alt="WarehouseManagerApp-P04-4" width="160"> |
| `WarehouseManagerApp-P05 - 알뜰살뜰 마트 창고 홈` | `/mart` | `WarehouseManagerApp/Components/Pages/MartHome.razor` | 확장 | 알뜰살뜰 마트 창고 홈 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P05.png" alt="WarehouseManagerApp-P05" width="160"> |
| `WarehouseManagerApp-P05-1 - 알뜰살뜰 마트 작업 보드` | `/mart/work-board` | `WarehouseManagerApp/Components/Pages/MartWorkBoard.razor` | 확장 | 알뜰살뜰 마트 작업 보드 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P05-1.png" alt="WarehouseManagerApp-P05-1" width="160"> |
| `WarehouseManagerApp-P05-2 - 알뜰살뜰 마트 프로세스별 작업 시작` | `/mart/work/{ProcessCode}` | `WarehouseManagerApp/Components/Pages/MartWorkStart.razor` | 확장 | 알뜰살뜰 마트 프로세스별 작업 시작 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P05-2.png" alt="WarehouseManagerApp-P05-2" width="160"> |
| `WarehouseManagerApp-P05-3 - 알뜰살뜰 마트 피킹/포장` | `/mart/picking` | `WarehouseManagerApp/Components/Pages/MartPickingPacking.razor` | 확장 | 주문별 피킹·포장·출고 완료 요청 | - | 캡처 대기 |
| `WarehouseManagerApp-P99 - 미발견 페이지` | `/not-found` | `WarehouseManagerApp/Components/Pages/NotFound.razor` | 시스템 | 미발견 페이지 | - | 완료<br><img src="assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P99.png" alt="WarehouseManagerApp-P99" width="160"> |

## OrdererApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `OrdererApp-P01 - 주문자 홈` | `/` | `OrdererApp/Components/Pages/Home.razor` | 보조 | 주문자 홈 | - | 완료<br><img src="assets/app-pages/OrdererApp/OrdererApp-P01.png" alt="OrdererApp-P01" width="160"> |
| `OrdererApp-P02 - 공동구매 개요` | `/group-purchase` | `OrdererApp/Components/Pages/GroupPurchaseHome.razor` | 1.0 | 공동구매 화면 진입과 버전 경계 안내 | - | route SRP 재캡처 필요 |
| `OrdererApp-P02-1 - 재료 자동집단화` | `/group-purchase/products` | `OrdererApp/Components/Pages/GroupPurchaseProducts.razor` | 1.0 | 재료 카드 한 번 클릭으로 서버 배치 미리보기·비구속 저장·카드 내 철회 | `OrdererApp-P02` | 원클릭 집단화 재캡처 필요 |
| `OrdererApp-P02-1-1 - 내 개별주문 의향` | `/group-purchase/wishes` | `OrdererApp/Components/Pages/GroupPurchaseWishes.razor` | 0.5 | 본인의 개별주문 의향 원장 목록과 수정·철회 진입점 | `OrdererApp-P02` | 재캡처 필요 |
| `OrdererApp-P02-1-2 - 여러 개별주문 의향 등록` | `/group-purchase/wishes/new` | `OrdererApp/Components/Pages/GroupPurchaseWishCreate.razor` | 0.5 | 여러 재료를 선택하되 재료별 개별주문 의향을 독립 원장으로 저장 | `OrdererApp-P02-1-1` | 재캡처 필요 |
| `OrdererApp-P02-2 - 공동구매 상품 근거 상세` | `/group-purchase/products/{ProductId}` | `OrdererApp/Components/Pages/GroupPurchaseProductDetail.razor` | 1.0 | 한 상품의 HS·보관·모집 근거 읽기 | `OrdererApp-P02-1` | route SRP 재캡처 필요 |
| `OrdererApp-P02-3 - 개별주문 의향 상세 조건` | `/group-purchase/demands/new/{ProductId}` | `OrdererApp/Components/Pages/GroupPurchaseDemandCreate.razor` | 0.5 | 상품·배송권·희망 수량·수령 조건을 직접 조정하는 비구속 Action | `OrdererApp-P02-1-1` | route SRP 재캡처 필요 |
| `OrdererApp-P02-4 - 수입 단가·도착원가 참고` | `/group-purchase/import-review/{ProductId}` | `OrdererApp/Components/Pages/GroupPurchaseImportReview.razor` | 1.5 준비 | 한 상품의 수입 참고 신호와 Simulation 원가 조회 | `OrdererApp-P02-2` | 운영 효과 없음 · 재캡처 필요 |
| `OrdererApp-P02-5 - 공동구매 선적 조회` | `/group-purchase/shipments` | `OrdererApp/Components/Pages/GroupPurchaseShipments.razor` | 1.5 준비 | 문서관리번호 한 건의 공개 선적 상태 조회 | `OrdererApp-P02` | 운영 효과 없음 · 재캡처 필요 |
| `OrdererApp-P03 - 주문자 화물 주문` | `/cargo` | `OrdererApp/Components/Pages/CargoOrder.razor` | 확장 | 주문자 화물 주문 | - | 완료<br><img src="assets/app-pages/OrdererApp/OrdererApp-P03.png" alt="OrdererApp-P03" width="160"> |
| `OrdererApp-P04 - 음식 주문 홈` | `/food` | `OrdererApp/Components/Pages/FoodOrderHome.razor` | 확장 | 음식 주문 홈 | - | 완료<br><img src="assets/app-pages/OrdererApp/OrdererApp-P04.png" alt="OrdererApp-P04" width="160"> |
| `OrdererApp-P04-1 - 음식점 주문` | `/food/restaurants` | `OrdererApp/Components/Pages/RestaurantOrder.razor` | 확장 | 음식점 주문 | - | 완료<br><img src="assets/app-pages/OrdererApp/OrdererApp-P04-1.png" alt="OrdererApp-P04-1" width="160"> |
| `OrdererApp-P04-2 - 마트 공개 상품 목록` | `/food/mart` | `OrdererApp/Components/Pages/MartOrder.razor` | 확장 | 공개 상품 검색·판매 가능 조건·서버 페이징 | - | 재캡처 완료: [변경 기록](../Changes/2026-07-22-mart-product-route-srp.md)<br><img src="../assets/changes/2026-07-22-mart-product-route-srp/mart-product-list-desktop.png" alt="OrdererApp-P04-2 desktop" width="160"> |
| `OrdererApp-P04-2-1 - 마트 공개 상품 상세` | `/food/mart/products/{ProductId}` | `OrdererApp/Components/Pages/MartProductDetail.razor` | 확장 | stable product ID 공개 설명·재고 투영·구매 근거 읽기 | `OrdererApp-P04-2` | stable-ID 실제 확인: [변경 기록](../Changes/2026-07-22-mart-product-route-srp.md) |
| `OrdererApp-P04-2-2 - 마트 상품 구매후기 작성` | `/food/mart/reviews/{ProductId}` | `OrdererApp/Components/Pages/MartProductReview.razor` | 확장 | 완료 원장 참여자의 명시적 공개 후기 저장과 같은 상품 재조회 | `OrdererApp-P04-2-1` | 390px 완료: [변경 기록](../Changes/2026-07-22-mart-product-route-srp.md)<br><img src="../assets/changes/2026-07-22-mart-product-route-srp/mart-product-review-mobile.png" alt="OrdererApp-P04-2-2 mobile" width="160"> |
| `OrdererApp-P04-3 - 마트 비구속 주문 요청` | `/food/mart/order/{ProductId}` | `OrdererApp/Components/Pages/MartOrderRequest.razor` | 확장 | 공개 상품 한 건의 주문 요청 저장과 같은 요청 ID 재조회 | `OrdererApp-P04-2-1` | stable-ID·390px 로그인 경계 확인: [변경 기록](../Changes/2026-07-22-mart-product-route-srp.md) |
| `OrdererApp-P05 - 내 개별주문 원장` | `/orders` | `OrdererApp/Components/Pages/OrderHistory.razor` | 0.5 | 로그인한 주문자의 개별주문 원장 목록과 현재 단계 조회 | - | 재캡처 필요 |
| `OrdererApp-P99 - 미발견 페이지` | `/not-found` | `OrdererApp/Components/Pages/NotFound.razor` | 시스템 | 미발견 페이지 | - | 완료<br><img src="assets/app-pages/OrdererApp/OrdererApp-P99.png" alt="OrdererApp-P99" width="160"> |

## RestaurantDeskApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `RestaurantDeskApp-P01 - 음식점 데스크 홈` | `/` | `RestaurantDeskApp/Components/Pages/Home.razor` | 보조 | 음식점 데스크 홈 | - | 완료<br><img src="assets/app-pages/RestaurantDeskApp/RestaurantDeskApp-P01.png" alt="RestaurantDeskApp-P01" width="160"> |
| `RestaurantDeskApp-P02 - 주변 음식점 조회` | `/restaurants/nearby` | `RestaurantDeskApp/Components/Pages/NearbyRestaurants.razor` | 확장 | 주변 음식점 조회 | - | 완료<br><img src="assets/app-pages/RestaurantDeskApp/RestaurantDeskApp-P02.png" alt="RestaurantDeskApp-P02" width="160"> |
| `RestaurantDeskApp-P02-1 - 인기 음식점 조회` | `/restaurants/popular` | `RestaurantDeskApp/Components/Pages/PopularRestaurants.razor` | 확장 | 인기 음식점 조회 | - | 완료<br><img src="assets/app-pages/RestaurantDeskApp/RestaurantDeskApp-P02-1.png" alt="RestaurantDeskApp-P02-1" width="160"> |
| `RestaurantDeskApp-P03 - 리뷰 관리` | `/reviews/moderation` | `RestaurantDeskApp/Components/Pages/ReviewModeration.razor` | 운영 | 리뷰 관리 | - | 완료<br><img src="assets/app-pages/RestaurantDeskApp/RestaurantDeskApp-P03.png" alt="RestaurantDeskApp-P03" width="160"> |
| `RestaurantDeskApp-P04 - 배차 주소 입력/검증 폼` | `/dispatch/address-form` | `RestaurantDeskApp/Components/Pages/DispatchAddressForm.razor` | 보조 | 배차 주소 입력/검증 폼 | - | 완료<br><img src="assets/app-pages/RestaurantDeskApp/RestaurantDeskApp-P04.png" alt="RestaurantDeskApp-P04" width="160"> |

## HumanResourcesManagerApp

| 페이지 ID / 제목 | 라우트 | 파일 | 분류 | 화면 책임 | 필수 연결 | 캡처 |
| --- | --- | --- | --- | --- | --- | --- |
| `HumanResourcesManagerApp-P01 - 역할 지원·배정 검토` | `/` | `HumanResourcesManagerApp/Components/Pages/Home.razor` | 확장 | 인증된 서버관리자의 영속 역할 지원·철회와 배정·해제 목록, 정확한 검토 ID 상세 조회 | `GET /api/v1/admin/hr-role-reviews`, `GET /api/v1/admin/hr-role-reviews/{reviewId}` | 기존 캡처 · 갱신 필요<br><img src="assets/app-pages/HumanResourcesManagerApp/HumanResourcesManagerApp-P01.png" alt="HumanResourcesManagerApp-P01 기존 화면" width="160"> |

## 보완 메모

| 항목 | 내용 |
| --- | --- |
| 필수 페이지와 전체 페이지의 관계 | 레거시 필수 페이지 문서는 2.0 운송 루프를 닫는 최소 화면이고, 이 문서는 코드 프로젝트 전체 화면을 찾기 위한 색인이다. |
| 라우트 충돌 후보 | `SsalddelApp`과 `RestaurantDeskApp` 모두 `/dispatch/address-form`을 가진다. 앱이 다르므로 런타임 충돌은 아니지만 문서에서 책임을 구분해야 한다. |
| 템플릿성 화면 | `Counter`, `Weather`는 시스템/샘플 화면으로 분류했다. 출시 전 제거 또는 숨김 여부를 따로 판단한다. |
| 미발견 페이지 | `NotFound`는 사용자 업무 화면이 아니지만 앱 운영에 필요한 라우트로 남긴다. |
| 다음 문서화 단계 | 각 페이지별 상세 README는 `page-docs/{앱명}/{페이지ID}/README.md`에 두고, 캡처는 `assets/app-pages/{앱명}/` 아래 추가한다. |
| 통합 클라이언트 단계 | 새 페이지에는 가능한 경우 진입 사방괘, 다이어그램·노드 행동, 필요한 식별자와 복원 문맥을 기록한다. |
| 관리자 캡처 | 관리자 보호 화면은 개발용 관리자 인증 세션과 문서용 메모리 데이터를 붙여 실제 운영 화면까지 캡처한다. |
| MAUI 캡처 | Android/Windows MAUI 앱의 Blazor 페이지는 문서용 캡처 호스트에서 실제 Razor 컴포넌트를 렌더링하고, Chrome DevTools 전체 페이지 캡처로 내부 스크롤 높이까지 반영해 PNG로 남긴다. |
