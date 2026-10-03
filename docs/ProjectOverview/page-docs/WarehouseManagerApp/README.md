# WarehouseManagerApp 화면 문서

[전체 화면 문서](../README.md) / [앱 전체 카탈로그](../../app-page-catalog.md)

이 문서는 WarehouseManagerApp 에 속한 화면별 README를 모은 색인입니다. 각 화면 문서는 캡처 이미지, 화면 책임, 사용자와 참여자, API/서버 연계, 보안 점검을 별도로 설명합니다.

[창고 하차 연락처 연결](../cargo-menu-handoff-r1.md)은 실제 선택 연락처와 한국시간 입력을 기존 운송 원장에 전달한다. 시험과 실제 역할 앱 확인의 범위는 [변경 기록](../../../Changes/2026-10-03-cargo-menu-handoff-r1.md)에 구분한다.

[창고 출고예정부터 운송 완료까지](../cargo-warehouse-journey-r1.md)는 출고 인계가 서버 `배차확정` 또는 `상차지도착`을 허용하는 조건과 기사 상차 완료의 별도 선행 조건을 정리한다. 서버 HTTP 흐름과 창고 앱 UI 확인은 별도로 판정한다.

| 페이지 ID / 제목 | 라우트 | 분류 | 화면 책임 | 캡처 |
| --- | --- | --- | --- | --- |
| [WarehouseManagerApp-P01 - 창고 관리자 홈](WarehouseManagerApp-P01/) | / | 보조 | 창고 관리자 홈 | 완료 |
| [WarehouseManagerApp-P02 - 일반 창고 작업 보드](WarehouseManagerApp-P02/) | /work-board | 확장 | 일반 창고 작업 보드 | 완료 |
| [WarehouseManagerApp-P02-1 - 프로세스별 창고 작업 시작](WarehouseManagerApp-P02-1/) | /work/{ProcessCode} | 확장 | 프로세스별 창고 작업 시작 | 완료 |
| [WarehouseManagerApp-P02-2 - 작업대 스캔](WarehouseManagerApp-P02-2/) | /work/{ProcessCode}/workbench | 확장 | 작업대 스캔 | 완료 |
| [WarehouseManagerApp-P02-3 - 범용 스캔 스테이션](WarehouseManagerApp-P02-3/) | /scan | 확장 | 범용 스캔 스테이션 | 완료 |
| [WarehouseManagerApp-P03 - 입고 검수 목록·상세·실행](WarehouseManagerApp-P03/) | `/work/inbound/inspection`<br>`/work/inbound/inspection/{InboundItemId}`<br>`/work/inbound/inspection/{InboundItemId}/record` | 확장 | 목록 조회·stable-ID 상세·명시적 검수 Command와 같은 ID 재조회 | Web route desktop·390px 실제 재검증 |
| [WarehouseManagerApp-P03-1 - 입고상품 수령](WarehouseManagerApp-P03-1/) | /work/inbound/products | 확장 | 정확한 입고예정 조회와 현장 반입 요청 | 완료 |
| [WarehouseManagerApp-P03-2 - 일반 재고 현황](WarehouseManagerApp-P03-2/) | /warehouse/general/inventory | 확장 | 창고 범위 최소 재고 목록·서버 집계·정확한 상세 | 실제 캡처 |
| [WarehouseManagerApp-P03-3 - 적재 작업](WarehouseManagerApp-P03-3/) | /work/inbound/put-away | 확장 | 검수 완료 재고의 위치 확정과 같은 ID 재조회 | 실제 캡처 |
| [WarehouseManagerApp-P04 - 피킹 작업 목록·상세·실행](WarehouseManagerApp-P04/) | `/work/picking-batch`<br>`/work/picking-batch/{TaskKey}`<br>`/work/picking-batch/{TaskKey}/execute` | 확장 | 목록 조회·stable-key 상세·명시적 시작/완료와 같은 Key 재조회 | Web route desktop·390px 실제 재검증 |
| [WarehouseManagerApp-P04-1 - 포장 작업](WarehouseManagerApp-P04-1/) | /work/outbound/packing | 확장 | 적재 완료 재고의 전체 가용수량 포장·같은 ID 재조회 | 실제 캡처 |
| [WarehouseManagerApp-P04-2 - 출고 인계 준비](WarehouseManagerApp-P04-2/) | /warehouse/general/transport-handoff | 확장 | 포장 완료 재고의 출고예정 원장 준비·같은 ID 재조회 | 실제 캡처 |
| [WarehouseManagerApp-P04-3 - 출고예정 운송 전 검토](WarehouseManagerApp-P04-3/) | /warehouse/general/outbound-plan-review | 확장 | 준비된 출고예정의 포장·수량·출발 창고 근거와 운송 전 입력 필요 항목 확인 | 실제 캡처 |
| [WarehouseManagerApp-P04-4 - 출고 운송의뢰 작성과 인계](WarehouseManagerApp-P04-4/) | /warehouse/general/transport-request-draft | 확장 | 정확한 출고예정의 현장 조건 검토·명시 저장·같은 ID 인계 조회 | 기존 캡처; 연락처 보완의 실제 UI 확인은 별도 |
| [WarehouseManagerApp-P05 - 알뜰살뜰 마트 창고 홈](WarehouseManagerApp-P05/) | /mart | 확장 | 알뜰살뜰 마트 창고 홈 | 완료 |
| [WarehouseManagerApp-P05-1 - 알뜰살뜰 마트 작업 보드](WarehouseManagerApp-P05-1/) | /mart/work-board | 확장 | 알뜰살뜰 마트 작업 보드 | 완료 |
| [WarehouseManagerApp-P05-2 - 알뜰살뜰 마트 프로세스별 작업 시작](WarehouseManagerApp-P05-2/) | /mart/work/{ProcessCode} | 확장 | 알뜰살뜰 마트 프로세스별 작업 시작 | 완료 |
| [WarehouseManagerApp-P05-3 - 알뜰살뜰 마트 피킹/포장](WarehouseManagerApp-P05-3/) | /mart/picking | 확장 | 주문별 피킹·포장·출고 완료 요청 | 캡처 대기 |
| [WarehouseManagerApp-P99 - 미발견 페이지](WarehouseManagerApp-P99/) | /not-found | 시스템 | 미발견 페이지 | 완료 |
