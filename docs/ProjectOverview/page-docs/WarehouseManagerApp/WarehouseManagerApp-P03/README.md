# WarehouseManagerApp-P03 - 입고 검수 목록·상세·실행

[전체 화면 문서](../../README.md) / [WarehouseManagerApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

![입고 검수 공용 목록 desktop 화면](../../../../assets/changes/2026-07-22-inbound-inspection-route-srp/inbound-inspection-list-desktop.png)

![입고 검수 공용 실행 mobile 화면](../../../../assets/changes/2026-07-22-inbound-inspection-route-srp/inbound-inspection-record-form-mobile.png)

2026-11-07 · 로컬 브라우저에서 가상 검수 대기·완료 상품을 조회했다. **상세 확인**으로 보관 위치와 검수 상태를 읽고 **검수 목록** 또는 **목록**으로 돌아온다. [390px 목록과 확인 범위](../../../../Changes/2026-11-07-warehouse-inspection-list-guide.md)

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | WarehouseManagerApp, 통합 WebApp |
| 페이지 ID / 제목 | WarehouseManagerApp-P03 - 입고 검수 목록·상세·실행 |
| 라우트 | `/work/inbound/inspection`, `/work/inbound/inspection/{InboundItemId}`, `/work/inbound/inspection/{InboundItemId}/record` |
| host 소스 | `WarehouseManagerApp/Components/Pages/InboundInspection*.razor`, `Ssalddel.WebApp/Pages/WarehouseInboundInspection*Page.razor` |
| 공용 화면 | `InboundInspectionListScreen`, `InboundInspectionDetailScreen`, `InboundInspectionRecordScreen` |
| 단계 / 실행 경계 | `Beta / Simulation` |
| 캡처 상태 | 공용 Web route desktop·390px 실제 재검증 완료 |

## 단일 책임 경계

입고 검수는 세 Route Page로 나뉜다. 목록은 접근 가능한 창고의 `보관중` 입고상품 검색·필터만, 상세는 정확한 입고상품 ID 읽기만, 검수 실행은 실제 수량과 네 가지 확인을 저장하고 같은 ID를 다시 읽는 일만 담당한다.

- `입고검수대상목록ViewModel`: 검색어·검수 상태·서버 페이징 목록만 담당한다.
- `입고검수대상상세ViewModel`: 사용자가 명시적으로 선택한 `inboundItemId` 한 건의 상세만 조회한다. 첫 항목을 자동 선택하거나 없는 ID를 다른 항목으로 대체하지 않는다.
- `입고검수작성ViewModel`: 수량, 네 가지 현장 확인, 메모와 저장 명령만 담당한다.
- `입고검수실행ViewModel`: 정확한 상세·작성 순서만 조정하고 저장 성공 뒤 같은 ID 상세를 서버에서 다시 조회한다.
- 세 공용 Screen은 각 route 책임의 상태만 렌더링한다. 목록·상세 Screen에는 Command 입력이 없다.
- 각 host는 인증, capability와 stable-ID route 조립만 맡고 기존 `?inboundItemId=`는 상세 route로 호환 이동한다.

적재 위치 확정, 출고, 운송 인계, 계약, 보관 책임, 결제와 정산은 이 페이지 책임이 아니다.

## API와 권한

- `GET /api/v1/warehouse-operations/inventory/inspection-targets`
- `GET /api/v1/warehouse-operations/inventory/{inboundItemId}/inspection-target`
- `POST /api/v1/warehouse-operations/inventory/{inboundItemId}/inspect`

모든 경로는 로그인과 `WarehouseManager` 또는 `WarehouseInboundOperator` 역할을 요구한다. 서버는 창고 소유자 또는 해당 창고에 배정된 사용자만 조회·검수할 수 있게 제한하고, 상품 소유권만으로는 접근을 허용하지 않는다. 범위 밖 ID와 없는 ID는 같은 404로 처리한다.

목록 계약은 상품명, SKU, 창고명, 수량, 상태와 기준 시각만 반환한다. 상세 계약도 사용자 ID, 주소, 연락처, 계좌, 결제 식별자와 증빙 원본을 포함하지 않는다.

## 상태 전이 규칙

- 검수 대상은 현재 상태가 `보관중`인 입고상품이다.
- 실제 검수 수량과 불량 수량은 각각 0~100,000 범위이며 불량 수량은 실제 수량을 넘을 수 없다.
- 메모는 최대 400자다.
- 네 가지 현장 확인을 모두 완료하기 전에는 클라이언트 저장이 비활성이다.
- 최초 성공은 수량·불량 수량과 검수 이력을 기록하고 상태를 `검수완료`로 전이한다.
- 완료 후 같은 수량으로 재시도하면 새 이력·감사·event를 만들지 않고 기존 결과를 반환한다.
- 완료 후 다른 수량으로 재시도하면 서버가 충돌로 거부한다.

## 화면 상태

기능 비활성, 로그인 필요, 역할 거부, 로딩, 원장 없음, 검색 결과 없음, 선택 전, 상세 없음, API 오류, 저장 중과 재시도를 구분한다. `Simulation` 안내는 검수 완료가 적재·출고·운송·계약·결제·정산을 자동 실행하지 않는다는 경계를 함께 표시한다.

## 다른 화면과의 관계

- 앞 단계: [WarehouseManagerApp-P03-1 - 입고상품 수령](../WarehouseManagerApp-P03-1/)
- 현재 단계: 입고상품 실제 수량·불량 수량 검수
- 다음 후보: [WarehouseManagerApp-P04 - 피킹 배치 작업](../WarehouseManagerApp-P04/)

입고상품 수령 페이지가 `입고예정` 요청을 만들고 별도 입고 완료 흐름이 `보관중` 재고를 만든 뒤, 이 페이지가 그 재고만 검수한다. 검수 결과가 이후 작업의 입력이 될 수는 있지만 이 화면이 후속 작업을 자동 시작하지 않는다.

## 검증 범위

- 실제 WebApp에서 개발 계정 로그인, 서버 목록 2건, 정확한 `inboundItemId=2` 상세, 네 가지 확인 전후 저장 버튼 상태와 console 경고·오류 0건을 확인했다.
- 브라우저 검증에서는 개발 DB에 임시 검수 이력을 남기지 않기 위해 저장 직전까지만 진행했다.
- 서버 상태 전이, 권한, 자연 멱등 재시도, 완료 후 다른 수량 거부와 저장 뒤 같은 ID 재조회는 자동화 테스트로 검증한다.
