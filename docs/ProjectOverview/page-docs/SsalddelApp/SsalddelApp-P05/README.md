# SsalddelApp-P05 - 화주 관점 창고 업무 허브

[전체 화면 문서](../../README.md) / [SsalddelApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/SsalddelApp/SsalddelApp-P05.png" alt="SsalddelApp-P05 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | SsalddelApp |
| 페이지 ID / 제목 | SsalddelApp-P05 - 화주 관점 창고 업무 허브 |
| 라우트 | /shipper/warehouse/workspace |
| 소스 파일 | [SsalddelApp/Components/Pages/WarehouseWorkspace.razor](../../../../../SsalddelApp/Components/Pages/WarehouseWorkspace.razor) |
| 분류 | 확장 |
| 2.0 운송 필수 연결 | 직접 연결 없음 |
| 캡처 상태 | 기존 캡처 보존 · r17 실제 화면 검수 보류 |

## 왜 필요한가

화주가 입고 예정·가용 재고와 후속 업무를 확인하고 입고 현황·재고 목록 등 필요한 화면으로 이동하는 허브다. 현장 검수·적재·포장 확인은 WarehouseManagerApp이 담당한다.

## 사용자와 참여자

주 사용자: 화주, 판매자, 물류 의뢰자 / 보조 참여자: 기사, 관리자, 창고 관리자

이 화면은 창고/출고/피킹 확장 워크플로우 안에서 화주 관점 창고 업무 허브 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 화주 관점 창고 업무 허브
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [SsalddelApp-P04-1 - 입고 요청 목록과 처리](../SsalddelApp-P04-1/)
- 다음 화면: [SsalddelApp-P05-1 - 창고 재고 조회](../SsalddelApp-P05-1/)
- 상위 화면: 없음
- 하위 화면: [SsalddelApp-P05-1 - 창고 재고 조회](../SsalddelApp-P05-1/), [SsalddelApp-P05-2 - 창고 스캔 작업](../SsalddelApp-P05-2/), [SsalddelApp-P05-3 - 창고 프로세스별 작업 시작](../SsalddelApp-P05-3/)

상호작용 관점에서는 다음 흐름을 우선 봅니다. 화주가 입력하거나 확인한 의뢰/결제/창고 상태는 기사 앱의 추천, 관리자 원장, 창고 작업 화면으로 이어질 수 있습니다.

## API 경로와 코드 연결

- 화면 소스: [SsalddelApp/Components/Pages/WarehouseWorkspace.razor](../../../../../SsalddelApp/Components/Pages/WarehouseWorkspace.razor)
- 현황 조회: [ShipperOperationsWorkspace](../../../../../SsalddelApp/Components/Shared/ShipperOperationsWorkspace.razor) → [ServerBackedShipperOperationsService](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs). 작업대는 입고·재고를 조회하며, 같은 서비스의 창고 목록은 기존 화주 창고 기능 ViewModel에 연결된다.
- 기존 목록 API: `GET api/v1/warehouse-operations/warehouses`, `inbounds`, `inventory`. 서버의 [WarehouseOperationService](../../../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs)가 사용자 접근 범위의 원장을 조회해 `Items` 목록으로 반환한다. 정상 빈 결과는 `{"items":[]}`다.

응답 전체나 `Items`가 null이거나 null 항목이 섞였으면 정상 빈목록으로 반환하지 않고 기존 오류 안내로 보낸다. 해당 조회가 실패하면 그 목록의 새 대입을 수행하지 않으며, 사용자가 `새로고침`으로 다시 조회한다. 권한·통신 오류와 자동 재요청 금지는 유지한다. 여러 목록의 전체 원자 갱신이나 최초 오류의 기본0 메트릭 표시는 r11 보완 범위 밖이었다. [응답 경계의 재현·보완과 검증](../../../../Changes/2026-11-04-shipper-warehouse-response-contract-r11.md)을 따른다.

후속 r15에서는 최초 로딩/조회 실패를 정상0건과 구분한다. 전체 조회가 성공하기 전의 수치와 업무 제목은 `—`로 표시하고 출고 알림도 조회 중/확인 실패를 안내한다. 정상 빈 결과는0건이며 같은 새로고침으로 다시 확인한다. [최초 오류 상태와 실제 Razor 회귀](../../../../Changes/2026-11-05-warehouse-initial-load-state-r15.md)를 따른다.

r17에서는 이전 성공 뒤 후속 조회가 실패해도 마지막 전체 표시를 유지한다. 다섯 조회를 기존 순서로 수행하고 모두 성공한 뒤 입고·재고·출고 알림·HS 검토·판매채널 표시를 함께 교체한다. 대기 중 일부 새 값만 표시하거나 실패 뒤 자동 재요청하지 않는다. 이는 화면 반영의 경계이며 서버/DB 전체를 같은 시점으로 동결한 조회는 아니다. [재현·최소 보완과 검증](../../../../Changes/2026-11-05-warehouse-refresh-snapshot-r17.md)을 따른다. 브라우저 폭/시각과 이번 소스의 휴대폰 확인은 남는다. 기존 준비147은 r15 소스이며 이번 수정본으로 사용하지 않는다.

좁은 화면에서 `새로고침`이 한 글자씩 꺾이지 않도록 기존 버튼의 줄바꿈과 축소를 막았다. 등록 휴대폰의 화주146에서 한 줄 표시·카드 안 배치와 잘림/겹침 없음을 확인했다. 현재 합성 화주 계정의 조회는 HTTP403이며, 명시 재조회 뒤 같은 권한 안내를 유지하고 정상 홈으로 돌아왔다. 빈 재고나 성공한 창고 조회로 해석하지 않는다. [실제 표시·오류 후 복귀 검증](../../../../Changes/2026-11-04-warehouse-refresh-button-r13.md)을 따른다.

기존 캡처는 보존한다. 내부 응답 검증의 합성 HTTP·ViewModel 시험, 146 포장·설치, 등록 기기 세로 화면의 실제 확인을 구분한다. 다른 폭/가로 화면과 권한 있는 계정의 창고 조회는 별도다.

## 보안과 개인정보 점검

화주 주소, 연락처, 의뢰 금액, 결제 조건은 역할 기반으로 제한해 노출해야 합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
