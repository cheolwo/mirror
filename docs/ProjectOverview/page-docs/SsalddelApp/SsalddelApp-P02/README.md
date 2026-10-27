# SsalddelApp-P02 - 운송 의뢰 작성

[전체 화면 문서](../../README.md) / [SsalddelApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/SsalddelApp/SsalddelApp-P02.png" alt="SsalddelApp-P02 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | SsalddelApp |
| 페이지 ID / 제목 | SsalddelApp-P02 - 운송 의뢰 작성 |
| 라우트 | /shipper/request |
| 소스 파일 | [SsalddelApp/Components/Pages/ShipperRequestWizard.razor](../../../../../SsalddelApp/Components/Pages/ShipperRequestWizard.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [SsalddelApp-P02 - 운송 의뢰 작성](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 현장 정보 연결)

| 항목 | 기록 |
| --- | --- |
| 주 사용자·소스 | MAUI 화주, 기존 `/shipper/request` 작성 시작과 공통 운송 입력 단계 |
| 대상·진입 문맥 | 같은 운송 의뢰 Draft의 화물·상하차 주소·담당자·시간 조건 |
| 한 문장 목적 | 화주가 실제 현장 조건을 검토해 운송 의뢰를 등록한다. |
| 완료 결과·주 행동 | 기존 최종 검토에서 명시적으로 서버 등록. 작성 시작만으로 배차/결제 실행하지 않음 |
| 기본 정보 | 상하차 담당자/연락처와 시간창의 네 시각. 상차 시작/종료 필수, 하차 둘 다 미지정 가능. 한국시간 입력 |
| 보조 정보 | 같은 Draft의 화물·차량·운임 조건과 검토 안내. 공개 커뮤니티 초안에는 상세주소/담당자/전화번호 제외 |
| 독립 업무 | 대량 등록·서버 의뢰 상세·배차·창고 인계·정산은 기존 별도 업무로 인계 |
| 진입·실패·복귀 | 기존 단계/초안 수명 유지. 시간창 한쪽 누락·역전은 오류. 임의 전화번호나 현재 시각으로 미입력을 대체하지 않음 |
| 코드·API·DB | ShipperRequestTransportScreen → 공통 작성 ViewModel/Draft → 등록 서비스 → 기존 POST api/v1/shipper/requests의 LocationContactDTO/TimeWindowDTO. 한국시간을 UTC로 전달하고 기존 의뢰 원장에 기록 |
| 책임 판정·검증 | 같은 목적의 작성 단계. 전체 build·관련132/132; 화주 신규23건은 실제 소스의 입력/mapper/API 시험. 실제 모바일 날짜 입력·작성 UI·실 HTTP/DB 등록·동일 ID 완주는 미검증 |

[이번 현장 연결·검증](../../cargo-contact-window-r1.md) · [변경 기록](../../../../Changes/2026-10-03-cargo-contact-window-r1.md).

### 2026-10-16 초안 표시 범위

최종 검토의 보조 행동은 `이 화면에 초안 유지`로 표시한다. 현재 작성 상태는 앱 세션 안에서 유지되며, 이 버튼은 서버 등록이나 앱 재시작 후 복원을 보장하는 저장이 아니다. 실제 등록은 기존 `운송 의뢰 등록`이 담당하고, 보낸 접수의 불명확한 응답은 기존 요청 번호로 결과를 확인하는 복구 절차를 유지한다. 이번 보완은 버튼·안내 문구만 다루며 저장 구현·접수 동의·서버 권한을 바꾸지 않는다. 실제 재시작·모바일 화면 검증은 별도다.

### PC·모바일 작성 책임 r112

Windows PC도 기존 .NET MAUI `SsalddelApp`이 화주 작성 호스트다. 좁은 창은 기존 세로 입력, 넓은 창은 같은 단계·초안을 더 넓은 읽기폭과 기존 grid/table로 소비하도록 한다. 기기별로 새 의뢰 원장·가격 계산·권한을 만들지 않는다. 별도 웹의 접수 응답이 불확실하면 같은 클라이언트 접수번호를 보존하고 먼저 결과를 조회하며 새 POST로 대체하지 않는다. 작성 성공·서버 접수·결제 확인·배차 진입은 구분한다. [r112 범위와 검증](../../../../Changes/2026-10-18-shipper-pc-closeout-r112.md).

### 모바일 입력 맥락 분리 r72 (2026-10-19)

| 항목 | 현재 책임 |
|---|---|
| 사용자·대상 | 화주가 현재 앱 세션의 같은 운송 의뢰 초안을 입력 |
| 질문·단계 | 물품 → 크기·중량 → 출발지 → 도착지 → 시간 → 차량 → 비용 → 최종 확인 |
| 기본 정보·주 행동 | 현재 맥락의 입력과 기존 필수항목의 보완 이유, 이전·다음. 명시적 등록은 최종 확인에서만 실행 |
| 선택 정보 | 부피·팔레트·부가 비용·알선 절차는 해당 입력 맥락의 보조 정보. PC 기존 통합 입력은 보존 |
| 수명·복귀 | 같은 작성 State/Draft를 공유하고 이전/앞으로 이동에 입력 유지. 일반 초안의 앱 재시작 복구는 보장하지 않음 |
| 실패·접수 | 시간창/포장 규격 등 기존 필수 검증을 각 단계에 투영. 같은 접수 원문·요청 ID·동의·결과 재조회 보호 유지 |
| 경로·코드 | 기존 Cargo/Transport/Procedure/Review 값과 경로 유지. 새 `ShipperRequestMobileStepCatalog`와 각 호스트의 모바일 맥락 Page가 공통 부분 Screen을 조립 |
| 실행 경계 | 단계 이동은 API 접수·배차·계약·결제 완료가 아니며 기존 서버 권한·원장·등록 준비/자격 관문은 그대로 |
| 검증 상태 | APK200 실기기 8단계·초안 유지·접수와 후속 추천/수락·상하차 촬영 완료를 합성 자료로 확인. 후불 API 준비·실제 입금·지도/GPS·외부 푸시는 구분. [결과와 화면](../../../../Changes/2026-10-19-mobile-context-authoring-r72.md) 참조 |

[승인 구현 범위 r72](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/mobile-context-authoring.implementation.r72.md).

### 화물 입력과 추천의 일치 r75 (2026-10-19)

기존 물품·크기와 중량·차량 단계 및 PC 통합 입력을 유지한다. 크기는 같은 포장 1개의 외포장 규격, 수량은 그 포장의 개수, 중량은 포장을 포함한 전량의 합계다. 바닥 방향 변경·위에 쌓기·파손주의는 같은 포장 맥락에서 확인한다. 직접 입력한 부피와 규격으로 계산한 부피를 구분해 보여주고, 차량 확인에는 큰 값을 사용한다. 예전 초안의 누락은 미확인으로 복원한다.

차량 단계의 `차량 추천 확인`은 현재 화물 조건을 서버로 보내고 그 조건과 일치하는 응답만 표시한다. 후보 선택은 별도 행동이며 조회 실패·후보 없음·화물 변경 후 재확인을 안내한다. 고정 차량이나 로컬 간이 추천으로 실패를 채우지 않는다. 추천 이후 조건 변경, 늦은 응답, 계정 변경·초안 교체로 이전 추천을 적용하지 않는다. 서버 추천에서 선택한 차량을 초안에서 복원하면 재확인 표시만 유지하고 응답과 확인 지문은 복원하지 않는다.

취급 형태와 사용자 요청사항은 같은 전송 문구로 추천·App/Web 접수에 보존한다. 추천은 적재 조건 확인이며 시간 약속·운행 경로·기사 배차·계약·결제 적합성의 확정이 아니다. 이 문서는 화면 책임과 구현 범위를 기록한다. 로컬 화면 캡처·시험·APK·실기기 검증 결과는 통합 검증 기록에서 별도로 판단한다.

## 왜 필요한가

이 화면은 운송 의뢰 작성을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

현재 작성 시작은 기존 단계별 작성 화면으로 이동합니다. 현장 정보는 공통 `ShipperRequestTransportScreen`과 `운송의뢰작성ViewModel`에서 같은 `운송모델작성Draft`에 담습니다. 초안 작성과 최종 서버 등록은 구별합니다.

요금이나 차량 선택이 애매한 사용자는 작성 중인 운송 조건을 바탕으로 커뮤니티 상담 글을 바로 등록할 수 있습니다. 이때 글에는 상세주소, 담당자 이름, 전화번호를 넣지 않고 화물, 차량 후보, 상하차 지역, 예상거리, 결제예정금액, 기준운임 같은 판단 정보만 요약합니다.

## 사용자와 참여자

주 사용자: 화주, 판매자, 물류 의뢰자 / 보조 참여자: 기사, 관리자, 창고 관리자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 운송 의뢰 작성 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 운송 의뢰 작성
- 공통 책임: 화물, 상하차지, 차량, 운임, 결제 조건을 `운송모델작성Draft`로 만드는 일
- 2026-10-19 화물 입력 보완: 기존 한 화면 작성과 단계 작성이 함께 사용하는 `ShipperRequestCargoScreen`에서 포장·깔판을 포함한 총중량(kg), 포장한 화물의 길이·너비·높이(mm), 선택 팔레트 수를 입력한다. 이 페이지의 한 가지 결과는 화주가 실제 운송 조건을 확인하여 동일 의뢰로 접수하는 것이며 기사 자격 심사·플랫폼 등록은 다른 책임 영역이다. 수량·부피만 입력한 초안도 삭제하거나 추정값으로 채우지 않고 저장·복원하되 새 접수 전 필요한 항목을 같은 화물 단계에서 안내한다. 모든 수단의 동일한 법정 의무라는 표시 대신 이번 서비스의 내부 운영 등록 기준으로 다룬다.
- 치수·팔레트 입력은 공통 VM/Draft와 통합앱·웹 등록 payload에 보존한다. CSV 일괄등록, 기존 미확정 접수 원문, 내보내기와 과거 자료는 자동 수정하지 않는다. 실제 처리 완료 또는 법정 등록 적합을 뜻하지 않으며 이번 화면 변경의 실제 렌더·APK 검증은 후속 검증 결과에서 별도로 보고한다.
- 상담 보조: 단가/차량 선택이 어려울 때 커뮤니티 `업무 질문` 글로 조건 요약을 등록하는 일
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [SsalddelApp-P01-4 - 탐색/제안성 업무 수신함](../SsalddelApp-P01-4/)
- 다음 화면: [SsalddelApp-P02-1 - 운송 의뢰 대량 등록](../SsalddelApp-P02-1/)
- 상위 화면: 없음
- 하위 화면: [SsalddelApp-P02-1 - 운송 의뢰 대량 등록](../SsalddelApp-P02-1/), [SsalddelApp-P02-2 - 배차 주소 입력/검증 폼](../SsalddelApp-P02-2/)

상호작용 관점에서는 다음 흐름을 우선 봅니다. 화주가 입력하거나 확인한 의뢰/결제/창고 상태는 기사 앱의 추천, 관리자 원장, 창고 작업 화면으로 이어질 수 있습니다.

## API 경로와 코드 연결

- 화면 소스: [SsalddelApp/Components/Pages/ShipperRequestWizard.razor](../../../../../SsalddelApp/Components/Pages/ShipperRequestWizard.razor)
- 공통 컴포넌트: [ShipperRequestTransportScreen](../../../../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestTransportScreen.razor), [운송의뢰작성ViewModel](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/운송의뢰작성ViewModel.cs)
- 공통 Draft: [Ssalddel.Ui.Common/Areas/App/Models/운송모델작성Draft.cs](../../../../../Ssalddel.Ui.Common/Areas/App/Models/운송모델작성Draft.cs)
- 클라이언트 서비스/계약: [SsalddelApp/Services/IShipperOperationsService.cs](../../../../../SsalddelApp/Services/IShipperOperationsService.cs), [SsalddelApp/Services/Samples/SampleShipperOperationsService.cs](../../../../../SsalddelApp/Services/Samples/SampleShipperOperationsService.cs), [SsalddelApp/Services/ServerBackedShipperOperationsService.cs](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 2.0 운송 문서 | - | `api/v1/shipper/requests` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | `GET api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`GET api/v1/shipper/requests/public` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/recommend-vehicle` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |
| 워크플로우 문서 | - | `api/v1/shipper/requests` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`GET api/v1/shipper/requests/public` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/recommend-vehicle` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/shipper/requests` | [SsalddelApp/Services/ServerBackedShipperOperationsService.cs](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs) | `GET api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`GET api/v1/shipper/requests/public` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/recommend-vehicle` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/shipper/requests/recommend-vehicle` | [SsalddelApp/Services/ServerBackedShipperOperationsService.cs](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs) | `GET api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/recommend-vehicle` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |
| 클라이언트 서비스 | POST | `api/v1/shipper/requests` | [SsalddelApp/Services/ServerBackedShipperOperationsService.cs](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs) | `POST api/v1/shipper/requests/recommend-vehicle` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/bulk/preview` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/bulk/confirm` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

화주 주소, 연락처, 의뢰 금액, 결제 조건은 역할 기반으로 제한해 노출해야 합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
