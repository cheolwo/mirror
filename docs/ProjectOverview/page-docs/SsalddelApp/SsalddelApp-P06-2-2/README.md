# SsalddelApp-P06-2-2 - 판매 주문 원장 상세

[전체 화면 문서](../../README.md) / [SsalddelApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

![판매 주문 공용 Screen의 Web 390px stable-ID 상세 인증 경계](../../../../assets/changes/2026-07-22-sales-order-mobile-route-srp/sales-order-detail-mobile.png)

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | SsalddelApp·Ssalddel.WebApp 공용 Screen |
| 페이지 ID / 제목 | SsalddelApp-P06-2-2 - 판매 주문 원장 상세 |
| 라우트 | `/shipper/sales/orders/{OrderId:long}` |
| 소스 파일 | `SsalddelApp/Components/Pages/SalesOrderDetail.razor` |
| 공용 Screen | `ShipperSalesOrderWorkspace`의 `Detail` mode |
| 분류 | 확장·읽기 전용 |
| 캡처 상태 | 공용 Screen의 Web host 390px 인증 경계 확인 |

## 단일책임

stable order ID 한 건의 주문과 출고 투영을 읽는 일만 담당한다. 목록 검색·동기화 범위·상태·페이지를 `from`에 보존하고 뒤로가기에서 그대로 복원한다. 조회 화면은 피킹·포장 또는 외부 주문 동기화 Command를 노출하지 않는다.

## 상세 조회 수명 · 2026-11-07

주소의 주문이 바뀌면 최신 ID를 실제로 조회하고, 늦은 이전 응답·오류·완료 처리는 현재 상세와 로딩을 바꾸지 않는다. 선택 해제·취소·잘못된 ID도 이전 내용을 되살리지 않는다. 현재 요청과 응답의 주문 ID, 주입된 기존 계정·세션·서버 문맥을 대조하며 없는 상세는 기존 404/null 계약을 유지한다. 기존 명령의 중복 제출 방지와 화면·복귀 경로는 보존한다.

실제 상세 VM 경합 20개와 실제 공용 컴포넌트의 매개변수 전환·지연 응답 연결 4개가 통과했다. 브라우저 라우터·업무 버튼과 기기 실행을 뜻하지 않으며 외형을 바꾸지 않아 기존 캡처는 유지했다. [수정 전 실패·구현·검증 기록](../../../../Changes/2026-11-07-seller-detail-late-response.md)

## 모바일 기준

통합 Web host의 390px에서 가로 넘침 없이 단일 열로 표시되며 새 목록 복귀 동작은 48px 터치 영역을 갖는다. `SsalddelApp` shell 자체의 실기기 캡처는 아니며, 공용 Screen의 인증 경계를 확인한 화면으로 영속 주문 데이터나 개인정보는 포함하지 않는다.
