# 기사 메뉴 축약·창고 하차 연락처 연결 r1

사용자가 기존 기사 메뉴의 나열을 줄이고 다음 보완을 진행하도록 요청했다. 기존 업무 화면과 stable route를 유지하고, 메뉴 진입 허브와 창고 운송 입력의 부족한 연결을 정리한다. [페이지 단일 책임 기준](../../Architecture/WholeRoadmapPagePrinciple.md)과 [시각 디자인 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 적용한다. 실행 결과는 [변경 기록](../../Changes/2026-10-03-cargo-menu-handoff-r1.md)에 분리한다.

## 화면 책임과 메뉴 구성

기사의 질문은 “지금 필요한 운행 업무나 내 정보로 어디서 들어가는가”다. [메뉴 P02 책임 카드](DriverApp/DriverApp-P02/README.md)가 허브를 소유하고, 실제 운송·문의·정산·계좌 입력은 각각의 기존 화면이 소유한다.

- 하단의 홈·추천·운송·정산 네 목적지는 유지한다.
- 펼친 메뉴는 기존 14링크와 지도 버튼에서 운행 지도·전체 메뉴·로그인 및 계정 세 진입으로 줄인다.
- 전체 메뉴의 네 계정 카드와 평면 목록을 하나의 행 목록으로 정리한다. 기본 11행을 운행 준비·일정, 문의·배달, 기록·계좌, 알림·설정 네 묶음에 둔다. 하단 목적지를 반복하지 않는다.
- 기존 가시성 항목과 사용자 숨김을 유지한다. 보충 링크도 숨김 부모/경로를 확인하며, 미등록 가시성 항목은 기타 메뉴에 남긴다. 업무의 삭제·권한 변경·새 페이지 생성으로 처리하지 않는다.

[MainLayout](../../../DriverApp/Components/Layout/MainLayout.razor)의 기존 하단 선택·지도 호출·오류·닫기·경로 변경 수명은 유지한다. [메뉴 페이지](../../../DriverApp/Components/Pages/Driver/04_Settings/메뉴Page.razor) → [NavMenu](../../../DriverApp/Components/Layout/NavMenu.razor) → 기존 [가시성 서비스](../../../DriverApp/Services/DriverViewVisibilityService.cs)의 `VisibleItems`와 `Changed`를 소비한다. 현재 가시성 서비스는 로컬 기본 항목과 사용자 선택을 관리하며 서버에서 설정을 조회하는 구현은 아니다.

[DriverApp 빌드 설정](../../../DriverApp/DriverApp.csproj)은 기존 다른 MAUI 앱처럼 `EnableDefaultCssItems=false`를 사용한다. Razor 전용 CSS가 native MAUI CSS로 수집되어 웹의 scoped bundle에서 빠지던 문제를 실제 APK 확인 중 발견해 바로잡았다. 메뉴 CSS는 기존 `DriverApp.styles.css` 링크로 전달한다.

## 창고 입력 → 코드 → 같은 원장

창고 사용자의 질문은 “선택한 재고·출고예정을 실제 하차 현장 정보와 함께 기존 운송에 인계하는가”다. [출고 운송 초안 책임 카드](WarehouseManagerApp/WarehouseManagerApp-P04-4/README.md)를 기준으로 기존 입력에 선택 담당자·연락처를 연결한다.

| 소비 진입 | 전달 경로 |
| --- | --- |
| 창고 재고 직접 인계 | [WarehouseTransportHandoffPanel](../../../WarehouseManagerApp/Components/Warehouse/WarehouseTransportHandoffPanel.razor) → 기존 출고운송인계 ViewModel → 운송 생성 API |
| 출고예정 검토 후 작성 | [공용 초안 화면](../../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/SsalddelTransportRequestDraftWorkspace.razor) → [초안 작성·검토·저장 ViewModel](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/운송의뢰초안페이지ViewModels.cs) → 같은 출고예정 재조회 |
| 기존 MAUI 재위탁 목록 | [ReconsignmentOrders](../../../SsalddelApp/Components/Pages/ReconsignmentOrders.razor) → 기존 재위탁 생성 API |

기존 [요청 DTO](../../../Ssalddel.Contracts/Common/Inventory/InventoryDtos.cs)에 nullable `하차담당자명`·`하차연락처`를 추가하고 [WarehouseOperationService](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs)가 기존 화주 의뢰의 하차 연락처 열에 입력값을 Trim하여 저장한다. 기존 개인정보 persistence 보호를 사용하며 새 DB 테이블이나 별도 연락처 원장은 만들지 않는다. 담당자명은100자, 연락처는50자 이하이고 미입력은 빈 값이다. 사용자 ID나 출고 창고 전화번호를 하차 정보로 대신 넣지 않는다. 기존 상차 창고 담당자와 전화번호는 보존한다.

이미 연결된 출고예정의 재시도는 같은 의뢰 ID와 재고 예약을 유지한다. 이전 클라이언트가 새 필드를 보내지 않은 null은 기존 값을 보존한다. 명시한 값이 기존 저장값과 다르면409 충돌로 처리하며 자동 수정·공백 삭제를 하지 않는다. 과거 잘못 저장된 연락처를 추정하여 일괄 변경하지 않는다.

선택 재고·출고예정이 바뀌면 이전 담당자 입력을 즉시 비우며, 늦은 이전 조회를 적용하지 않고 최신 대상 조회를 이어 간다. 검토 후 담당자를 변경하면 검토 결과를 무효화하고 다시 검토하도록 한다. API에 보낼 초안은 화면 입력과 분리된 사본이고 제출 중 입력은 잠근다. 공용 초안의 날짜·시각은 한국시간으로 입력/검토하며 API 저장 직전에 UTC로 변환한다. 기존 기사 표시가 UTC를 한국시간으로 돌리는 계약과 맞춰 09:00 입력이18:00으로 표시되는 오류를 막는다. 서버의 기존 희망 시각 기반 각1시간창 정책은 유지한다.

저장 후 기존 [기사 상세조회](../../../Ssalddel/Application/Driver/Transport/Handlers/운송상세조회QueryHandler.cs)가 같은 의뢰의 수령자명·연락처를 전달한다. [앞선 현장 정보 연결](cargo-contact-window-r1.md)의 기사 표시·배정 권한·창고 인계 선행 조건을 재사용한다.

## 검증 경계

소스·단위 시험, 실제 앱 화면, APK, 실 HTTP/DB 왕복과 운송 완주는 각각 기록한다. UI 가시성은 업무 권한을 대신하지 않는다. 기존 기능 플래그 기본 비활성과 Simulation 경계를 유지한다. 전화 입력·조회가 통화 성공의 증거는 아니며 이번 변경을 실제 배차·운송·입금으로 표현하지 않는다.
