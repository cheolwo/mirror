# OrdererApp-P04-3 - 마트 비구속 주문 요청

[전체 화면 문서](../../README.md) / [OrdererApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | OrdererApp, Ssalddel.WebApp |
| 페이지 ID / 제목 | OrdererApp-P04-3 - 마트 비구속 주문 요청 |
| canonical route | `/food/mart/order/{ProductId}` |
| legacy route | `/food/mart/order?productId=...`, `/orderer/mart/order?...` |
| 공용 화면 | `OrdererMartOrderRequestWorkspace` |
| capability | Beta / PlatformPersistence / 인증 필요 |
| 캡처 상태 | stable-ID·390px 로그인 경계 실제 확인, [변경 기록](../../../../Changes/2026-07-22-mart-product-route-srp.md) |

## 한 가지 책임

한 공개 상품의 수량과 비구속 안내 확인을 받아 구매 의향 원장만 멱등 저장하고, 성공 응답의 같은 request ID를 다시 조회해 영수증을 표시합니다. 재고 예약·차감, 결제, 주문 확정, 피킹·포장, 배송과 계약은 실행하지 않습니다.

기능 접근은 목록·상세·후기와 같은 `MartProductAccessFrame`이 담당하며, 주문 요청 Workspace는 상품·인증·작성·정확한 저장 영수증만 조립합니다.

## 인증과 업무 표시

[OrdererMartOrderRequestWorkspace](../../../../../Ssalddel.Ui.Common/Areas/App/Components/Mart/OrdererMartOrderRequestWorkspace.razor)는 복원·로그인 단계에서 상품·작성 폼·영수증을 함께 표시하지 않습니다. [인증 패널](../../../../../Ssalddel.Ui.Common/Areas/App/Components/Mart/OrdererMartOrderAuthenticationPanel.razor)과 [로그인된 계정 표시](../../../../../Ssalddel.Ui.Common/Areas/App/Components/Mart/OrdererMartOrderAccountHeader.razor)를 분리하며, 기존 수량·동의·멱등 ID와 로그아웃 초기화는 유지합니다. 표시·복귀 및 검증 범위는 [로그인·업무 책임 카드](../../auth-workspace-separation-r1.md), [페이지 원칙의 인증 경계](../../../../Architecture/WholeRoadmapPagePrinciple.md#인증과-업무의-경계)를 참조합니다.

## API와 검증

- `GET /api/v1/orderer/mart/products/{productId}`
- `POST /api/v1/orderer/mart/order-requests`
- `GET /api/v1/orderer/mart/order-requests/{requestId}`
- 기존 query link를 stable product ID route로 호환 이동
- 저장 뒤 같은 request ID 재조회와 멱등 client request ID 경계 유지
- `/food/mart/order/1`의 상품 한 건·로그인 경계와 legacy query redirect 실제 확인
- 390px에서 horizontal overflow 없음, 로그인 입력 69px·버튼 48px 실제 확인
- 로그인·주문 요청 저장 Command는 실행하지 않음
