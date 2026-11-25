# 음식점 데스크탑 주문표와 수락 후 기본프린터 출력 r28

2026-10-28. `Implemented / DesktopUiAndStartupValidated / PhysicalPrintPending / OperationalUnchanged`. 사용자는 이전 POS 사진을 참고해 음식점 데스크탑 주문을 표로 조회하고, 신규 주문 알림·조리시간 선택·수락 후 전표 출력·기사 배정 뒤 조리의 흐름을 요청해 `ApprovedByUser / ReadyToDispatch` 범위로 진행했다. [공통 앱 제작 절차](../../시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-input-guide.md), [페이지 원칙](../../../../Architecture/WholeRoadmapPagePrinciple.md), [역할 시각 기준](../../../../Architecture/RoleAppVisualDesignStandard.md)과 기존 [페이지 책임](../../../../ProjectOverview/page-docs/restaurant-order-usability-r1.md)을 따른다. [최종 구현·화면·검증](../../../../Changes/2026-10-28-restaurant-desktop-table-r28.md)을 참조한다.

## 범위와 승인된 동작

기존 MAUI Blazor Hybrid `RestaurantDeskApp`의 `/orders`를 데스크탑 작업대로 보완한다. 별도 Avalonia 시작 템플릿을 새 주문 앱으로 만들지 않는다. 참고는 기존 비공개 첨부 POS 네 장의 표/선택 상세 배치이며 브랜드·실제 주문·고객 정보는 화면 데이터나 문서 자산으로 복사하지 않는다.

- 데스크탑은 접수·주문번호·메뉴/수량·금액·조리예상·처리상태를 표로 비교하고 같은 주문을 우측 상세에서 처리한다. 주문 알림이 현재 선택·입력을 빼앗지 않으며 명령 중 대상 변경·인증/조회 실패·빈 목록·낡은 Revision·기존 필터와 새로고침을 보존한다. 좁은 화면의 기존 카드/독립 상세 route를 유지한다.
- Windows 앱은 모바일 모의 창 대신 데스크탑 크기로 시작한다. 신규 주문 알림과 정본 조회는 기존 Coordinator/알림음 경로를 재사용하며 별도 주문 수신·자동 수락·FCM 자격을 추가하지 않는다.
- 조리시간을 고른 신규 명시 수락의 정상 결과만 Windows 기본프린터에 단일 출력 작업을 보낸다. 기존 WebView2 의존성의 별도 인쇄용 화면을 사용하며 본문을 전표로 이동시키지 않는다. r26의 기본 조리/포장 두 장분·선택 한 장과 Copies=1을 유지하고 용지/프린터 설정을 강제 변경하지 않는다.
- r27 서버 상태 복구에는 자동 인쇄·추가 수락을 하지 않는다. 프린터 사용 불가·결과 미확정은 주문 수락과 별도 안내하고 자동 재출력/대화상자 fallback을 하지 않는다. 출력 전후 실제 인쇄/절단은 장치 확인으로 남긴다. Android/브라우저는 기존 인쇄 경로를 유지한다.
- 기사 배정은 수락 뒤 기존 서버 흐름을 따르며 조리 시작은 확정 배차와 최신 AvailableActions가 허용할 때만 별도로 수행한다. 화면에서 조리/기사 배정을 임의로 확정하지 않는다.

## 소유권과 허용 경로

root는 `RestaurantDeskApp/Components/Pages/OrderInbox.razor`·`.razor.css`, `App.xaml.cs`, `MauiProgram.cs`, `Services/IRestaurantReceiptPrinter.cs`, `Components/Pages/OrderDetail.razor`·`.razor.cs`, 기존 페이지 테스트 fixture·기본 창 크기 architecture 계약, 필요한 Preview 대역과 이번 문서/시험을 소유한다. 프린터 담당은 신규 `RestaurantDeskApp/Platforms/Windows/WindowsRestaurantReceiptPrinter.cs`만 소유한다. 시험 담당은 신규 `Ssalddel.Tests/Clients/RestaurantDesktopTableR28Tests.cs`만 소유한다. 기존 dirty 변경과 r26/r27의 수락·출력·복구를 보존하고 서버/API/DB·인증/자동 배차 규칙·실제 기본프린터/알림 설정을 변경하지 않는다.

## 검증과 한계

표 선택·키보드/명령 잠금·필터·긴 메뉴/주문번호·로딩/실패/빈 상태와 수락 조리시간→전표 단일 요청→기사 배정 전 대기/배정 후 조리 행동을 시험한다. Windows 인쇄 어댑터는 현재 SDK API/컴파일을 확인하고 신규 정상 출력과 복구 무출력·출력 실패 무재수락을 대역으로 검사한다. Fast/Task·Android/Windows 소비 빌드와 실제 Razor 데스크탑/좁은 화면을 확인한다. 실제 Windows 창 생성·로그인·서버 주문·인쇄 장치·소리/FCM·운영 공개는 각각 별도 근거로 기록한다. 실제 주문/프린터에 시험 출력하거나 운영 설정을 켜지 않으며 commit/push·배포를 포함하지 않는다.

최종 Fast536·Task16,681통과/환경17생략·실패0, 전체 소비 빌드와 실제 Razor1440/1280px 표·390px 카드·키보드/입력 유지·단일 출력/복구 무출력을 확인했다. Windows 앱도 실제 실행해 넓은 로그인 화면까지 확인했으나 인증 후 업무·native 인쇄·실물/음향은 수행하지 않았다. 최종 소스13개 지문·HEAD를 보존하고 탭/미리보기/확인용 앱을 정리했다.
