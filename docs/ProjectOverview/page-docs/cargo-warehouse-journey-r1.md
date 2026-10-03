# 창고 출고예정에서 화물 기사 완료까지 r1

후속 [화물 견적 저장·기사 표시 r1](cargo-pricing-handoff-r1.md)에서 아래의 미확인 운임·거리 0 표시와 견적 저장/배차 전달을 보완했다. 실제 도로 조회와 전체 역할 앱 UI의 검증 경계는 유지한다. 아래 r3/r4 캡처·결과는 당시 판본의 근거다.

2026-10-03. 기존 출고예정 한 건을 창고·기사·화주가 같은 의뢰 관계로 조회하고 완료하는 실행 검증 범위다. [페이지 원칙](../../Architecture/WholeRoadmapPagePrinciple.md)과 [시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 따른다. 새 업무 페이지나 디자인을 만들지 않고 기존 [연락처·시간창 연결](cargo-contact-window-r1.md), [창고 하차 연락처·메뉴 연결](cargo-menu-handoff-r1.md)을 소비한다. 실행 결과와 남은 검증은 [이번 변경 기록](../../Changes/2026-10-03-cargo-warehouse-journey-r1.md)에 갱신한다.

## 페이지 책임

| 항목 | 기록 |
| --- | --- |
| 주 사용자·페이지/소스 | 창고의 출고예정 검토·운송의뢰 초안은 `WarehouseManagerApp`과 `Ssalddel.WebApp`이 공용 workspace를 소비한다. 기사의 상차·하차는 `DriverApp` 기존 P12/P13과 Web의 별도 PageViewModel이 소비한다. 화주는 MAUI `/shipper/transport`와 공용 의뢰 상세·타임라인을 소비한다. |
| 대상·진입 문맥 | `outboundPlanId` → 출고예정의 `TransportRequestId` → 기사 `transportId` 관계를 보존한다. 숫자 출고예정 ID, 문자열 의뢰 ID, 숫자 운송 ID를 같은 값으로 간주하지 않는다. 의뢰 ID로 같은 관계를 대조한다. |
| 한 문장 목적 | 창고는 선택 출고예정을 실제 운송에 인계하고, 기사는 배정받은 같은 운송의 현장 도착과 상하차를 완료하며, 화주는 그 의뢰의 서버 완료 상태를 확인한다. 각 역할의 완료 결과와 권한은 별개다. |
| 완료 결과·상태별 주 행동 | 미연결 출고예정은 검토→초안 저장, 배차 대기는 재조회, 기사 수락 후 창고는 현장 확인→출고 인계 확정이다. 기사는 상차지 도착→상차 완료→하차지 도착→하차 완료를 기존 서버 명령으로 수행한다. 성공 후 같은 운송 ID를 재조회하며 도착 체크박스만으로 서버 도착을 확정하지 않는다. |
| 기본 정보 | 상품·수량·포장·출발 창고·요청/등록 차량·배정 기사·현재 상태, 상하차 주소·담당자·연락처·한국시간 시간창과 현재 행동에 필요한 사진/인수증 조건. |
| 보조 정보 | 기존 상세/민감 정보 펼침의 화물별 바코드·LCL/FCL 조건·예외·인수증과 이력을 유지한다. 긴 이력은 화주 타임라인에서 확인한다. |
| 독립 업무·제외 정보 | 배차 수락은 기존 추천 상세/결정 화면, 결제·정산·계좌는 기존 독립 업무에 남긴다. 전화 확인·도착 클릭·사진 업로드는 실제 통화·물리 현장·결제·은행 지급의 증거가 아니다. 제품 화면에 DB/테스트 진단을 추가하지 않는다. |
| 진입·실패·복귀 | 기존 로그인 복귀·loading/empty/error/retry를 유지한다. 도착 요청 중 중복 명령과 완료를 막고, 운송 선택 변경·로그아웃·페이지 이탈 뒤 늦은 응답을 버린다. 도착 명령 실패를 성공/빈 상태로 바꾸지 않고 같은 ID 재시도를 제공한다. |
| 코드·API·DB | 아래 기존 경로를 소비한다. 서버가 권한·운송 상태·창고 인계·증빙을 검증하고 기존 출고예정·입고상품·화주운송의뢰·운송원장·운송의뢰상품연결 및 Event를 소유한다. 기사 완료가 창고 재고를 다시 차감하지 않는다. |
| 책임 판정·검증 | 기존 상차/하차 목적 안에 MAUI 도착 버튼과 기존 API·같은 ID 상세 재조회를 연결했다. 기존 Web 도착 버튼과 MAUI의 현장 체크를 구별한다. 서버 HTTP 전체 흐름·DB 대조와 역할 앱의 실제 버튼·사진·서명·완료 목록 검증은 각각 별도 근거로 판정한다. 역할 앱 UI·APK 완주 검증은 대기다. |

## 기존 화면과 검증 순서

아래는 같은 의뢰를 대조하는 검증 순서다. 창고의 출고 인계 완료는 서버에서 `배차확정` 또는 `상차지도착` 상태를 허용한다. 기사 본인의 수락·등록 차량·상품 할당 확인은 필요하지만, 기사 상차지 도착이 창고 인계 명령의 필수 선행 상태는 아니다. 기사 상차 완료는 별도로 서버 상차지 도착과 연결된 창고 출고 완료가 모두 필요하다.

기사 현재 운송의 서버 상태 `확정`은 기존 `배차확정`과 같은 상차 대기 단계다. 서버 상태 값은 보존하면서 다음 행동·주 버튼·지도 이동 대상이 상차지로 이어져야 한다. `상차완료`·`운송중`은 하차 이동으로 이어지고 Web 타임라인에서 인수 완료로 표시하지 않는다. 실제 최신 APK에서 발견한 분류 누락을 기존 페이지 안에서 보완하며, 운임·거리 미입력의 0 표시 문제는 별도 후속 범위다.

| 역할·화면 | 기존 route·행동 | 입력·선행 조건 |
| --- | --- | --- |
| 창고 인계 준비 | `/warehouse/general/transport-handoff?inboundItemId=…` → 출고 인계 준비 확정 | 포장된 가용 재고, 봉인/상품·수량 표찰과 운송 조건 확인. 기존 출고예정이 준비된 검증에서는 다시 생성하지 않는다. |
| 창고 검토 | `/warehouse/general/outbound-plan-review?outboundPlanId=…` → 서버 검토 목록 조회 → 출고 운송의뢰 작성 | 출고예정·포장·수량·창고 근거가 준비되어야 한다. 검토는 읽기 전용이다. |
| 창고 운송 생성 | `/warehouse/general/transport-request-draft?outboundPlanId=…` → 주소·수량·일정 검토 → 운송의뢰 저장 | 실제 하차 도로명 주소, 희망 상차/도착일·시각, 요청 차량, 상품·수량 확인. 하차 상세 위치·담당자·전화·취급 메모는 선택이다. 한국시간을 UTC로 저장한다. |
| 기사 수락 | `/driver/recommendations/{requestId}/decision` → 배차 수락 | 로그인·서버 추천 의뢰·등록 차량, 있으면 차량/화물 주의사항 확인. 수락 후 같은 의뢰에 확정 기사와 운송 관계를 조회한다. |
| 기사 상차 도착 | `/driver/transports/{transportId}/pickup` → 상차지 도착 | 배정된 운송·로그인 세션을 확인한다. 성공 응답만으로 화면 상태를 합성하지 않고 같은 ID의 상세를 다시 조회한다. |
| 창고 실제 인계 | 같은 운송의뢰 초안 route → 인계 상태 새로고침 → 출고 인계 완료 | 서버 `배차확정` 또는 `상차지도착`, 기사 수락·등록 차량과 요청 차량 일치·상품 할당 수량을 확인하고 기사 신원·등록 차량·실제 상품 인계의 세 항목을 현장에서 확인한다. |
| 기사 상차 완료 | 같은 pickup route → 현장/화물/인수증 확인 → 상차 완료 사진 촬영 → 상차 완료 | 창고 연계 출고가 완료되어야 한다. 현장 세 확인과 해당 화물 체크가 필요하며 사진은 기기 카메라로 촬영·업로드한다. 인수증 필요 시 서명 문서 사진 확인 또는 인수자명/서명·기사 서명·인수 확인을 입력한다. 서명 필수면 생략할 수 없다. |
| 기사 하차 완료 | `/driver/transports/{transportId}/dropoff` → 하차지 도착 → 인수/결제 확인·사진 촬영 → 하차 완료 | 하차지·수령자 인계·결제/증빙 방식 확인, 해당 화물 하차 확인, 사진 업로드가 필요하다. 하차 완료는 기존 `complete` 명령이다. |
| 완료 대조 | 기사 `/driver/transports/history`, MAUI 화주 `/shipper/transport` → `/shipper/request/{requestId}` 또는 `/timeline` | 기사 운송 ID와 의뢰 ID를 연결하고 창고 출고·예약/가용 수량과 같은 화주 의뢰의 운송 완료 상태를 재조회한다. Web 화주에는 같은 ID 상세·타임라인이 있으며 MAUI 운송 목록을 Web 결과로 보고하지 않는다. |

## 페이지 → 코드 → API·원장

- 창고 공용 [검토 workspace](../../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/SsalddelOutboundPlanReviewWorkspace.razor) → [초안 workspace](../../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/SsalddelTransportRequestDraftWorkspace.razor) → [초안 ViewModel](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/운송의뢰초안페이지ViewModels.cs). `GET api/v1/warehouse-operations/outbound-plan-reviews[/id]`, `POST api/v1/warehouse-operations/inventory/reconsignment`, `POST api/v1/warehouse-operations/outbound-plan-reviews/{id}/handoff-complete`를 소비한다. [출고 인계 UseCase](../../../Ssalddel/Application/Warehouse/출고운송인계완료UseCase.cs)는 실제 출고 재고 책임을 소유한다.
- 기사 [상차](../../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor)·[하차](../../../DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor) → 각 PageViewModel → [상세 조회 서비스](../../../DriverApp/Services/기사운송상세조회Service.cs)·[운송 API](../../../DriverApp/Services/DriverTransportApiService.cs). `POST api/v1/driver/transports/{id}/arrive-pickup`, `pickup-complete`, `arrive-dropoff`, `complete`를 소비한다. 기존 [상차 완료 Handler](../../../Ssalddel/Application/Driver/Transport/Handlers/운송상차완료CommandHandler.cs)와 [인수 완료 Handler](../../../Ssalddel/Application/Driver/Transport/Handlers/운송인수완료CommandHandler.cs)가 상태 전이와 증빙을 소유한다.
- [사진 서비스](../../../DriverApp/Services/DriverTransportCompletionPhotoService.cs)는 [Platform 파일 업로드 Controller](../../../Ssalddel/Controllers/Platform/파일업로드Controller.cs)의 `api/v1/files/upload`에 `TransportPickupComplete`/`TransportDropoffComplete`와 운송 ID를 전달한다. 업로드된 ObjectName을 완료 명령에 연결하며 업로드 성공과 운송 완료를 구분한다.
- MAUI [화주 운송 목록](../../../SsalddelApp/Components/Pages/TransportWorkspace.razor) → [서버 조회](../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs), 공용 상세/타임라인은 `api/v1/shipper/requests/{requestId}`로 같은 관계를 다시 읽는다.

## 소비 호스트와 검증 상태

Android package는 창고 `com.companyname.warehousemanagerapp`, MAUI 화주 `com.companyname.ssalddelapp`, 화물 기사 `kr.hongdal.driver`다. [공통 주소 계약](../../../Ssalddel.Ui.Common/Areas/App/Services/SsalddelApiEndpoint.cs)의 설정 키는 `SsalddelEndpoints:ServerBaseAddress`이며 이전 `SsalddelApiBaseAddress`를 지원한다. 기사 DEBUG Android 기본값은 `http://10.0.2.2:5104/`이고 창고/MAUI 화주 MauiProgram의 명시 fallback은 `https://localhost:7117/`다. 실행 검증에서는 각 앱의 실제 주소가 같은 격리 서버를 가리키는지 확인해야 한다. Web 소비 호스트는 `Ssalddel.WebApp`이며 개발 UI 기본 포트는5238/7139, 개발 API 설정은7117이다. 이 기본 설정을 현재 실행 주소로 단정하지 않는다.

검증 대상은 별도의 화물 검증 호스트5322이며 기존 food observer5321을 보존한다. 기본 기능 비활성·`Simulation` 경계를 유지하고, 격리 검증에서 feature와 합성 역할/창고 배정·등록 차량·사진 저장·업무 원장을 명시적으로 준비한다.

HTTP 전체 흐름 검증은 기존 API의 의뢰 생성·기사 수락·도착·창고 인계·사진 업로드·상하차 완료와 같은 ID의 서버/DB 재조회 결과를 기록한다. r3/r4의 실제 API·MySQL/Mongo 완주와 r3 재시작 조회, 최종 APK의 상차 주 행동·도착 저장/재조회·같은 의뢰 완료 목록은 [변경 기록](../../Changes/2026-10-03-cargo-warehouse-journey-r1.md)에 결속했다. 역할 앱 UI의 전체 로그인·입력·카메라/서명·화주 완료 목록은 별도 단계이며 전체 세 역할 UI 완주는 대기다. HTTP 성공을 앱 UI 완주로 보고하지 않는다. 빌드·단위 시험·HTTP/DB·실제 앱 입력·APK는 각 실행 근거가 확보될 때만 완료로 갱신한다. 물리 현장·운영 운송·은행 지급의 증거는 없다.
