# 홍달 1.0 페이지 검증 순례

이 문서는 홍달 1.0 국내 화물/용달 운송을 실제 화면으로 하나씩 검증하기 위한 순서표다. `hongdal-v1-required-pages.md`가 필요한 화면의 기준을 정한다면, 이 문서는 그 화면들을 어떤 순서로 눌러 보고 무엇을 확인해야 하는지 정리한다.

검증의 목적은 화면이 존재하는지 확인하는 데서 끝나지 않는다. 한 화면에서 상태가 바뀌면 서버 원장이 갱신되고, 다른 앱 화면에서 그 상태가 다시 보이는지 확인해야 한다.

## 검증 원칙

| 기준 | 확인할 것 |
| --- | --- |
| 화면 존재 | 라우트가 열리고 렌더링 오류 없이 주요 UI가 보이는가 |
| 서버 데이터 | 클라이언트 메모리만이 아니라 서버 API 또는 개발 시드/인메모리 스냅샷에서 데이터가 내려오는가 |
| 상태 전파 | 한 화면의 조작 결과가 다른 앱의 화면에 같은 상태로 반영되는가 |
| 실패/예외 | 업로드 실패, 위치 송신 실패, 추천 만료, 현장 예외가 다음 행동과 함께 보이는가 |
| 보안/개인정보 | 연락처, 주소, 위치, 계좌, 결제 식별자, POD 원본이 필요한 권한 안에서만 보이는가 |
| 단일 책임 | 한 화면이 너무 많은 결정을 떠안지 않고 다음 화면으로 넘길 일을 분리하는가 |

## 1차 순례 흐름

가장 먼저 아래 순서로 본다. 이 순서는 실제 운송 의뢰 한 건이 닫히는 흐름이다.

```mermaid
flowchart LR
    S1["HongdalApp-P01<br/>화주 홈"]
    S2["HongdalApp-P02<br/>운송 의뢰 작성"]
    S3["HongdalApp-P03<br/>의뢰 상세/타임라인"]
    A1["HongdalAdmin-P19<br/>배차대기"]
    D1["DriverApp-P06<br/>운행 시작"]
    D2["DriverApp-P07<br/>지도 홈/추천 배너"]
    D3["DriverApp-P08/P09/P10<br/>추천 확인·수락/거절"]
    D4["DriverApp-P11/P12/P13<br/>진행·상차·하차"]
    A2["HongdalAdmin-P22<br/>운송 원장"]
    A3["HongdalAdmin-P22-2/P22-3<br/>증빙·정산"]

    S1 --> S2 --> S3 --> A1 --> D1 --> D2 --> D3 --> D4 --> A2 --> A3
    A2 --> S3
```

## E2E 스모크 실행 기준

1.0 E2E 스모크는 상세 기능을 모두 검수하기 전, 운송 한 건이 화주, 기사, 관리자 화면을 거쳐 끊기지 않고 닫히는지만 빠르게 확인하는 운영 점검이다. 이 점검에서는 렌더링만 통과하거나 샘플 데이터가 보이는 상태를 성공으로 보지 않는다.

| 단계 | 필수 조건 | 실패로 보는 경우 |
| --- | --- | --- |
| 환경 | `AdminData:UseMemory=false`, `ClientDataMode:AllowSampleFallback=false`, `ClientDataMode:RequireServerLedgerForV1Smoke=true` 기준으로 실행한다. | 관리자 화면이 메모리 데이터로 정상처럼 보이거나, 기사 앱이 서버 장애를 샘플 추천/정산으로 대체한다. |
| 인증 | 화주, 기사, 서버관리자 토큰이 각각 발급되고 만료 시 재로그인 경로가 보인다. | 401/403이 빈 화면이나 샘플 화면으로 가려진다. |
| 의뢰 생성 | `POST api/v1/shipper/requests` 이후 `GET api/v1/shipper/requests/{requestId}`에서 같은 의뢰가 보인다. | 생성은 성공했지만 상세, 관리자 목록, 배차대기에 이어지지 않는다. |
| 배차/추천 | `GET api/v1/dispatch/wait`, `GET api/v1/driver/recommendations`, `POST api/v1/driver/dispatch-actions/{requestId}/accept`가 같은 의뢰를 기준으로 이어진다. | 보류/수락/거절 상태가 기사 화면에만 남고 서버 원장에 남지 않는다. |
| 상차/하차 증빙 | `POST api/v1/files/upload` 뒤 `pickup-complete` 또는 `complete` Command가 성공한다. 완료 Command가 실패하면 업로드된 `ObjectName`으로 재시도한다. | 파일 업로드 성공 뒤 완료 처리 실패가 단순 에러로 끝나고, 같은 사진을 중복 업로드해야만 재시도된다. |
| 관리자 확인 | `HongdalAdmin-P21/P22/P22-1/P22-2/P22-3/P26/P26-1`에서 운송, 이벤트, 증빙, 결제, 정산이 같은 `requestId`로 연결된다. | 관리자 페이지가 메모리 문서/POD 또는 메모리 의뢰 목록만 보여준다. |

스모크 결과를 기록할 때는 `requestId`, 기사 ID, 운송 ID, 생성된 파일 `ObjectName`, 결제 ID, 정산 조회 월을 함께 남긴다. 민감한 주소, 연락처, 계좌, POD 원본 URL은 문서에는 마스킹해서 적는다.

서버 원장 상태 전파는 `GET api/v1/transport-request-ledgers/{requestId}/events`도 함께 확인한다. 이 응답의 의뢰, 결제, 배차, 정산, 운송 상태가 화주 상세, 기사 현재 운송, 관리자 운송 상세의 표시값과 어긋나면 1.0 스모크 실패로 본다.

### HongdalApp 1.0 E2E 점검 범위

HongdalApp은 1.0 스모크에서 운송 의뢰를 만들고, 같은 의뢰가 화주 홈과 상세 타임라인에서 다시 조회되는지를 우선 확인한다. 판매채널, HS 검토, 창고 출고 알림처럼 운영 콘솔에 보이는 보조 흐름은 아직 일부 샘플/인메모리 서비스가 남아 있으므로, 아래 표의 핵심 운송 경로와 분리해서 판단한다.

| 화면/기능 | 1.0 확인 API | 판정 기준 |
| --- | --- | --- |
| 홈 `/shipper` | `GET api/v1/shipper/requests?shipperId={userId}`, `GET api/v1/warehouse-operations/warehouses` | 최근 의뢰와 타임라인 진입이 서버 의뢰 목록 기준으로 보인다. |
| 운송 의뢰 작성 `/shipper/request` | `POST api/v1/shipper/requests/recommend-vehicle`, `POST api/v1/shipper/requests/fare-estimate`, `POST api/v1/shipper/requests` | 차량 후보, 기준 운임, 의뢰 생성이 같은 인증 세션으로 이어진다. |
| 의뢰 상세 `/shipper/request/{requestId}` | `GET api/v1/shipper/requests/{requestId}` | 결제, 배차, 수락, 상차, 하차, 정산 상태가 방금 만든 `requestId`로 다시 보인다. |
| CSV 일괄등록 `/shipper/request/bulk` | `POST api/v1/shipper/requests/bulk/preview`, `POST api/v1/shipper/requests/bulk/confirm-preview` | 미리보기와 확정 등록이 인메모리 결과가 아니라 서버 파서/검증 결과로 표시된다. |
| 공개 화물 `/shipper/public-cargo` | `GET api/v1/shipper/requests/public` | 공개 상태 의뢰만 익명 요약으로 조회된다. |
| 결제 진입 | `POST api/v1/payments/prepare`, `POST api/v1/payments/toss/prepare`, 승인 API | 현재 HongdalApp은 결제 상태 표시 중심이다. 실제 결제창 준비/승인 호출이 없으면 1.0 결제 E2E 완료로 보지 않는다. |

## 필수 화면 검증 표

| 순서 | 페이지 | 앱/라우트 | 주 사용자 | 검증할 상태 | 서버/API 확인 | 현재 문서 |
| ---: | --- | --- | --- | --- | --- | --- |
| 1 | `HongdalApp-P01` 화주 홈 | `HongdalApp` `/shipper` | 화주 | 최근 의뢰, 운송 의뢰 작성, 의뢰 상세 진입 | 홈 데이터가 서버 의뢰 목록과 어긋나지 않는지 확인 | [문서](page-docs/HongdalApp/HongdalApp-P01/) |
| 2 | `HongdalApp-P02` 운송 의뢰 작성 | `HongdalApp` `/shipper/request` | 화주 | 의뢰 생성, 결제/정산 조건, 상하차지 | `POST api/v1/shipper/requests`, 차량 추천 API | [문서](page-docs/HongdalApp/HongdalApp-P02/) |
| 3 | `HongdalApp-P03` 의뢰 상세/타임라인 | `HongdalApp` `/shipper/request/{RequestId}` | 화주 | 결제, 배차, 수락, 상차, 하차, 정산 타임라인 | `GET api/v1/shipper/requests/{requestId}` | [문서](page-docs/HongdalApp/HongdalApp-P03/) |
| 4 | `HongdalAdmin-P19` 배차대기 | `HongdalAdmin` `/dispatch/wait` | 관리자 | 배차대기, 추천중, 후보부족, 추천 잠금 | `GET api/v1/dispatch/wait` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P19/) |
| 5 | `DriverApp-P06` 운행 시작 | `DriverApp` `/driver/work/start` | 기사 | 운행 시작, 위치 송신 시작, 복귀/수익 선호 | `api/v1/driver/work/*`, 위치 송신 API | [문서](page-docs/DriverApp/DriverApp-P06/) |
| 6 | `DriverApp-P07` 지도 홈/추천 배너 | `DriverApp` `/driver/home` | 기사 | 신규 추천 배너, 60초 응답 제한, 현재 운송 진입 | `GET api/v1/driver/home`, 추천 API | [문서](page-docs/DriverApp/DriverApp-P07/) |
| 7 | `DriverApp-P08` 추천 목록 | `DriverApp` `/driver/recommendations` | 기사 | 추천 후보 목록, 공동주문/일반화물 구분 | `GET api/v1/driver/recommendations` | [문서](page-docs/DriverApp/DriverApp-P08/) |
| 8 | `DriverApp-P09` 추천 상세 | `DriverApp` `/driver/recommendations/{의뢰Id}` | 기사 | 운임, 추가 시간, 경로 이점, 증빙 조건 | 추천 상세 DTO와 배차 원장 | [문서](page-docs/DriverApp/DriverApp-P09/) |
| 9 | `DriverApp-P10` 배차 처리 | `DriverApp` `/driver/recommendations/{의뢰Id}/decision` | 기사 | 수락, 거절, 만료, 보류 | `POST api/v1/driver/dispatch-actions/{requestId}/accept`, `reject` | [문서](page-docs/DriverApp/DriverApp-P10/) |
| 10 | `DriverApp-P11` 진행 중 운송 | `DriverApp` `/driver/transports/current` | 기사 | 다음 행동, 상차/하차 진입, 진행 상태 | `GET api/v1/driver/transports/current` | [문서](page-docs/DriverApp/DriverApp-P11/) |
| 11 | `DriverApp-P12` 상차 | `DriverApp` `/driver/transports/{운송Id}/pickup` | 기사 | 상차 사진, 인수증, 서명, 상차 예외 | 상차 완료 Command, 파일 업로드 API | [문서](page-docs/DriverApp/DriverApp-P12/) |
| 12 | `DriverApp-P13` 하차 | `DriverApp` `/driver/transports/{운송Id}/dropoff` | 기사 | 하차 사진, POD, 인수 확인, 하차 예외 | 하차 완료 Command, 파일 업로드 API | [문서](page-docs/DriverApp/DriverApp-P13/) |
| 13 | `DriverApp-P14` 월정산 | `DriverApp` `/driver/settlements/current-month` | 기사 | 운임, 추가 수당, 정산 예정일, 입금 완료 | `GET api/v1/driver/settlements/current-month` | [문서](page-docs/DriverApp/DriverApp-P14/) |
| 14 | `DriverApp-P15` 알림함 | `DriverApp` `/driver/notifications` | 기사 | 추천, 수락 후 안내, 입금 요청, 실패 알림 | FCM/알림톡/서버 알림 목록 | [문서](page-docs/DriverApp/DriverApp-P15/) |
| 15 | `HongdalAdmin-P16` 운영 대시보드 | `HongdalAdmin` `/dashboard` | 관리자 | 막힌 배차, 증빙 누락, 정산 지연 요약 | `GET api/v1/admin/dashboard` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P16/) |
| 16 | `HongdalAdmin-P21` 운송 목록 | `HongdalAdmin` `/transports` | 관리자 | 진행중, 완료, 예외 운송 목록 | `GET api/v1/admin/transports` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P21/) |
| 17 | `HongdalAdmin-P22` 운송 상세 원장 | `HongdalAdmin` `/transports/{RequestId}` | 관리자 | 기사, 화주, 의뢰, 증빙, 정산 연결 | 운송 워크플로우 상세 조회 | [문서](page-docs/HongdalAdmin/HongdalAdmin-P22/) |
| 18 | `HongdalAdmin-P22-1` 운송 이벤트 감사 | `HongdalAdmin` `/transports/{RequestId}/events` | 관리자 | 수락, 거절, 만료, 상차, 하차, 예외 로그 | `GET api/v1/admin/transports/events` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P22-1/) |
| 19 | `HongdalAdmin-P22-2` 운송 증빙/POD | `HongdalAdmin` `/transports/{RequestId}/proofs` | 관리자 | 사진, POD, 인수증, 서명, 문서 연결 | 파일/POD와 문서 API | [문서](page-docs/HongdalAdmin/HongdalAdmin-P22-2/) |
| 20 | `HongdalAdmin-P22-3` 운송 정산 상세 | `HongdalAdmin` `/transports/{RequestId}/settlement` | 관리자 | 입금 요청, 입금 완료, 기사 정산 후보 | 결제/정산 API | [문서](page-docs/HongdalAdmin/HongdalAdmin-P22-3/) |
| 21 | `HongdalAdmin-P26` 결제 목록 | `HongdalAdmin` `/payments` | 관리자 | 결제대기, 가상계좌, 입금완료 | `GET api/v1/payments` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P26/) |
| 22 | `HongdalAdmin-P26-1` 정산 목록 | `HongdalAdmin` `/settlements` | 관리자 | 기사별 정산 상태, 월정산 확인 | `GET api/v1/admin/driver-settlements` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P26-1/) |
| 23 | `HongdalAdmin-P27-5` 파일/POD 관리 | `HongdalAdmin` `/files/pod` | 관리자 | POD 업로드 상태, 원본 보호, 검수 | `api/v1/admin/files/pod` | [문서](page-docs/HongdalAdmin/HongdalAdmin-P27-5/) |

## 운영 보조 화면

아래 화면은 운송 흐름을 직접 전진시키지는 않지만, 1.0 운영에서 막힌 상태를 찾거나 민원/연락처 확인을 돕는다. 필수 화면 검증 뒤에 같이 본다.

| 페이지 | 라우트 | 검증할 것 |
| --- | --- | --- |
| `HongdalAdmin-P20` 운행 중 기사 현황 | `/drivers/operating` | 위치 최신성, 운행 상태, 후보 기사 확인 |
| `HongdalAdmin-P32` 기사 목록/관리 | `/drivers` | 기사 연락처, 차량, 최근 배차 내역 확인 |
| `HongdalAdmin-P33` 파트너 관리 | `/partners` | 화주 계정과 업체 연락처 확인 |
| `HongdalAdmin-P36` 연락처 통합 검색 | `/contact-search` | 전화번호 뒤 8자리로 기사/화주/창고 사용자 정보를 함께 조회 |
| `HongdalAdmin-P24` 화면/기능 노출 정책 | `/view-policies` | 1.0 필수 화면이 정책 때문에 숨겨지지 않는지 확인 |
| `HongdalAdmin-P35` 보조 기능 설정 | `/auxiliary-feature-settings` | 알림, 기록, 후속 처리 같은 부가 기능이 정책과 맞는지 확인 |

## 한 페이지를 검증할 때 남길 기록

각 페이지 README에는 다음 내용을 계속 보강한다.

| 항목 | 기록 방식 |
| --- | --- |
| 검증일 | 마지막으로 렌더링 또는 API 연동을 확인한 날짜 |
| 샘플 데이터 | 어떤 개발 시드, 인메모리 스냅샷, API 응답으로 화면을 확인했는지 |
| 성공 기준 | 사용자가 다음 행동을 판단할 수 있는지 |
| 실패 기준 | 서버 오류, 인증 오류, 빈 데이터, 업로드 실패 때 안내가 보이는지 |
| 다른 화면 반영 | 이 화면의 상태 변경이 어느 화면에서 다시 보이는지 |
| 보안 메모 | 연락처, 위치, 사진, 계좌, 결제값이 마스킹 또는 권한 통제되는지 |

## 다음 보완 순서

1. `HongdalApp-P01`부터 `DriverApp-P15`까지는 실제 개발 시드 데이터로 다시 열어 본다.
2. `HongdalAdmin-P16`부터 `HongdalAdmin-P27-5`까지는 서버 관리자 로그인 상태에서 다시 캡처한다.
3. 각 페이지 README의 `API 경로와 코드 연결`이 실제 코드와 어긋나면 그 자리에서 고친다.
4. 렌더링만 통과한 페이지와 실제 서버 API 데이터까지 확인한 페이지를 구분해 표시한다.
