# WarehouseManagerApp-P02 - 일반 창고 작업 보드

[전체 화면 문서](../../README.md) / [WarehouseManagerApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/WarehouseManagerApp/WarehouseManagerApp-P02.png" alt="WarehouseManagerApp-P02 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | WarehouseManagerApp |
| 페이지 ID / 제목 | WarehouseManagerApp-P02 - 일반 창고 작업 보드 |
| 라우트 | /work-board |
| 소스 파일 | [WarehouseManagerApp/Components/Pages/WorkBoard.razor](../../../../../WarehouseManagerApp/Components/Pages/WorkBoard.razor) |
| 분류 | 확장 |
| 2.0 운송 필수 연결 | 직접 연결 없음 |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-16 다음 작업 안내)

| 항목 | 기록 |
| --- | --- |
| 한 문장 목적·주 행동 | 선택한 입고 요청의 현재 상태와 다음 작업을 확인한다. 선택 전에는 `입고 예정 목록 열기`로 안내한다. |
| 기본·보조 정보 | 입고 요청·주문 연결·계약 내용을 사용자 말로 표현하며 내부 식별자 이름이나 저장 구현을 설명하지 않는다. 필요한 참조 값은 기존 상세 영역에 유지한다. |
| 유지 경계 | 같은 입고 요청의 서버 재조회·권한·상태별 다음 작업·다른 작업 영역의 route는 바꾸지 않는다. |
| 검증 범위 | 문구·diff 확인 대상이며 기존 캡처를 새 표시의 실제 검증으로 승격하지 않는다. |

## 왜 필요한가

이 화면은 일반 창고 작업 보드을 담당하므로, 1.0 이후의 해외 물류, 창고, 주문자 집단, 판매채널 확장을 화면 단위로 검증하기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 창고 관리자, 창고 작업자 / 보조 참여자: 화주, 기사, 관리자

이 화면은 창고/출고/피킹 확장 워크플로우 안에서 일반 창고 작업 보드 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 일반 창고 작업 보드
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [WarehouseManagerApp-P01 - 창고 관리자 홈](../WarehouseManagerApp-P01/)
- 다음 화면: [WarehouseManagerApp-P02-1 - 프로세스별 창고 작업 시작](../WarehouseManagerApp-P02-1/)
- 상위 화면: 없음
- 하위 화면: [WarehouseManagerApp-P02-1 - 프로세스별 창고 작업 시작](../WarehouseManagerApp-P02-1/), [WarehouseManagerApp-P02-2 - 작업대 스캔](../WarehouseManagerApp-P02-2/), [WarehouseManagerApp-P02-3 - 범용 스캔 스테이션](../WarehouseManagerApp-P02-3/)

상호작용 관점에서는 다음 흐름을 우선 봅니다. 창고 작업자가 입력한 입고, 스캔, 피킹 결과는 화주 재고/출고 상태와 기사 운송 의뢰 흐름으로 연결됩니다.

## API 경로와 코드 연결

- 화면 소스: [WarehouseManagerApp/Components/Pages/WorkBoard.razor](../../../../../WarehouseManagerApp/Components/Pages/WorkBoard.razor)
- 클라이언트 서비스/계약: 화면 파일에서 직접 확인하거나 정적/라우팅 화면으로 본다.

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 워크플로우 문서 | - | `api/v1/warehouse-operations` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/warehouse-operations/warehouses` [Ssalddel/Controllers/Common/창고작업Controller.cs](../../../../../Ssalddel/Controllers/Common/창고작업Controller.cs)<br>`POST api/v1/warehouse-operations/warehouses` [Ssalddel/Controllers/Common/창고작업Controller.cs](../../../../../Ssalddel/Controllers/Common/창고작업Controller.cs)<br>`GET api/v1/warehouse-operations/warehouses/{warehouseId:long}/users` [Ssalddel/Controllers/Common/창고작업Controller.cs](../../../../../Ssalddel/Controllers/Common/창고작업Controller.cs)<br>`POST api/v1/warehouse-operations/warehouses/{warehouseId:long}/users` [Ssalddel/Controllers/Common/창고작업Controller.cs](../../../../../Ssalddel/Controllers/Common/창고작업Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

개인정보와 업무 상태가 섞이는 화면인지 확인하고, 화면 권한과 로그 정책을 함께 점검합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
