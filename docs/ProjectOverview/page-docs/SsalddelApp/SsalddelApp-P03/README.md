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

## 페이지 책임 보완 r112 — PC·모바일 같은 의뢰의 다음 행동

2026-10-18 사용자 요청에 따라 기존 **Windows .NET MAUI 통합앱**을 PC 화주 기본 호스트로 재확인했다. 브라우저용 `Ssalddel.Web.ShipperApp`은 별도 소비자이며 같은 공통 상세 표시 정책을 사용한다. 기사 제품은 모바일 우선 기준을 유지한다.

| 항목 | 책임 |
| --- | --- |
| 주 사용자·대상 | 로그인한 화주의 선택 의뢰 ID. MAUI Windows·모바일과 별도 웹의 기존 상세/진행 화면 |
| 한 문장 목적 | 현재 의뢰가 무엇을 기다리는지, 누가 다음 처리를 해야 하는지와 확인된 진행 단계를 알린다. |
| 기본 정보·주 행동 | 전문 원장에서 수신한 운송/정산 상태·보류 여부, 대기 이유·다음 담당, 기존 결과 새로고침과 결제 상세 이동 |
| 보조 정보 | 기존 단계별 이력·증빙·연락 허용 범위. 상세 확인을 실입금/실지급으로 바꾸지 않음 |
| 독립 업무 | 결제 실행·후불 승인·배차·기사 수행·환불·정산 확정은 기존 전문 업무/권한이 소유. 이 표시 컴포넌트는 새 실행 버튼이나 권한을 만들지 않음 |
| 판독 경계 | 새 선결제 의뢰 접수는 결제 확인 대기이며 곧바로 배차 대기가 아님. 후불 승인·인수증 등록·상차 완료의 ‘완료’ 문자를 최종 정산/하차 완료로 해석하지 않음. 알 수 없는 상태는 미확인으로 보존 |
| 실패·복귀 | 조회 실패와 정상 빈 상태를 구분하고 같은 의뢰를 재조회. 다른 의뢰/계정으로 전환한 뒤 늦은 결과를 적용하지 않는 기존 수명 유지 |
| 코드·API·원장 | 공통 ShipperRequestDetailPresentation/상세 Screen → 각 호스트 상세 VM → 기존 화주 의뢰 조회. 전문 운송/정산 상태는 서버 응답의 권위 유지 |
| 책임 판정 | 현재 선택 의뢰의 진행 확인이라는 같은 목적의 구성. 상세 표시 개선은 실제 금융·배차 완료를 뜻하지 않음 |

[이번 조사·변경·실행 범위](../../../../Changes/2026-10-18-shipper-pc-closeout-r112.md).

## 왜 필요한가

### 정렬 책임 보완 r19 (2026-10-22)

[최종208 실제 화면·버튼 크기·복구 검증](../../../../Changes/2026-10-22-alignment-review-r19.md). 기존 문서용 대표 캡처와 이번 실제 휴대폰 캡처를 별도로 보존한다.

선택 의뢰의 상태를 읽는 기존 책임과 문구·행동·권한을 유지한다. 돌아가기는 제목 영역의 보조 탐색으로, 새 의뢰·새로고침은 동일 폭 작업 버튼으로 구분한다. 실패 안내에서는 제목·본문의 왼쪽 읽기 기준선을 유지하고, 닫기·복귀는 같은 폭의 보조 행, 허용된 재조회는 아래 전체 폭의 주 행동으로 배치한다. 재조회/복귀가 없을 때도 빈 버튼 칸을 만들지 않는다. 문단·주소를 강제로 가운데 정렬하지 않으며 새 저장·상태 판정·API·원장은 추가하지 않는다. 실제 모바일의 긴 제목·문구, 버튼 폭/높이/간격·가로 넘침과 기존 닫기·복귀 동작을 검증한다.

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
