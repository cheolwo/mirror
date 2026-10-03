# SsalddelApp-P03 - 의뢰 상세, 결제/배차/상차/하차/정산 타임라인

[전체 화면 문서](../../README.md) / [SsalddelApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/SsalddelApp/SsalddelApp-P03.png" alt="SsalddelApp-P03 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | SsalddelApp |
| 페이지 ID / 제목 | SsalddelApp-P03 - 의뢰 상세, 결제/배차/상차/하차/정산 타임라인 |
| 라우트 | /shipper/request/{RequestId} |
| 소스 파일 | [SsalddelApp/Components/Pages/ShipperRequestDetail.razor](../../../../../SsalddelApp/Components/Pages/ShipperRequestDetail.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [SsalddelApp-P03 - 의뢰 상세, 결제/배차/상차/하차/정산 타임라인](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 조회 수명 보완)

화주가 경로로 선택한 의뢰 ID의 최신 원장과 후속 상태를 확인한다. 조회 수명은 최신 선택·로그인 세션·화면 이탈을 관리하고 원장·권한·금액은 서버가 소유한다. 다른 ID로 이동하면 이전 상세와 입력을 정리하고 늦은 응답·오류·FakePG 안내를 새 화면에 적용하지 않는다. 실패·재시도와 기존 타임라인·증빙·모의 결제의 역할을 유지한다. 새 기능·실결제나 연락처/시간창 등록을 이번 수명 보완으로 구현했다고 보고하지 않는다. [이번 검증 범위](../../cargo-workflow-recovery-r1.md).

## 왜 필요한가

이 화면은 의뢰 상세, 결제/배차/상차/하차/정산 타임라인을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 화주, 판매자, 물류 의뢰자 / 보조 참여자: 기사, 관리자, 창고 관리자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 의뢰 상세, 결제/배차/상차/하차/정산 타임라인 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 의뢰 상세, 결제/배차/상차/하차/정산 타임라인
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [SsalddelApp-P02-2 - 배차 주소 입력/검증 폼](../SsalddelApp-P02-2/)
- 다음 화면: [SsalddelApp-P04 - 화주 입고 업무 대시보드](../SsalddelApp-P04/)
- 상위 화면: 없음
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 화주가 입력하거나 확인한 의뢰/결제/창고 상태는 기사 앱의 추천, 관리자 원장, 창고 작업 화면으로 이어질 수 있습니다.

## API 경로와 코드 연결

- 화면 소스: [SsalddelApp/Components/Pages/ShipperRequestDetail.razor](../../../../../SsalddelApp/Components/Pages/ShipperRequestDetail.razor)
- 클라이언트 서비스/계약: [SsalddelApp/Services/IShipperOperationsService.cs](../../../../../SsalddelApp/Services/IShipperOperationsService.cs), [SsalddelApp/Services/Samples/SampleShipperOperationsService.cs](../../../../../SsalddelApp/Services/Samples/SampleShipperOperationsService.cs), [SsalddelApp/Services/ServerBackedShipperOperationsService.cs](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 2.0 운송 문서 | - | `api/v1/payments` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | `GET api/v1/payments` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs)<br>`GET api/v1/payments/toss/config` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs)<br>`POST api/v1/payments/prepare` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs)<br>`POST api/v1/payments/confirm` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs) |
| 2.0 운송 문서 | - | `api/v1/shipper/requests` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | `GET api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`GET api/v1/shipper/requests/public` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests/recommend-vehicle` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/shipper/requests/{requestId}` | [SsalddelApp/Services/ServerBackedShipperOperationsService.cs](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs) | `GET api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`POST api/v1/shipper/requests` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`GET api/v1/shipper/requests/{requestId}` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs)<br>`PUT api/v1/shipper/requests/{requestId}` [Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs](../../../../../Ssalddel/Controllers/Shipper/01_Request/화주운송의뢰Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

금액, 계좌, 결제 식별자, 정산 예정일은 마스킹과 권한 검사가 필요합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
