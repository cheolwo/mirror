# 판매 주문 목록의 긴 주문번호와 상세 버튼

[앞선 모바일 요약 개선](2026-11-06-seller-order-readability.md) 뒤에도 320px에서는 긴 주문번호 한 건만 표시하면 286px 영역 안에서 표가 348px까지 늘어났다. 주문번호와 수량이 잘리고 **상세**를 누르려면 표를 가로로 움직여야 했다. 같은 폭에서 짧은 번호 한 건은 표 너비가 286px이었다.

[판매 주문 목록](../../Ssalddel.Ui.Common/Areas/App/Components/Sales/ShipperSalesOrderWorkspace.razor)의 기존 표에서 채널명과 주문번호, 상품명과 수량을 각각 묶었다. 좁은 화면에서 값이 셀 안에서 줄바꿈되며, 기존 조회 조건과 상세 이동을 그대로 사용한다.

가상 주문 4건으로 다시 본 결과, 320px 표의 내용 너비가 286px에 맞고 모든 **상세** 버튼이 표 안에 표시됐다. 390px에서도 번호와 수량이 잘리지 않았으며, 1280px의 표 너비·열 배치와 말줄임은 유지됐다. [390px 주문 목록](../assets/changes/2026-11-06-seller-order-row-readability/orders-390.png) · [같은 주문 상세](../assets/changes/2026-11-06-seller-order-row-readability/detail-390.png)

실제 판매자 Razor와 공용 스타일을 사용한 격리 브라우저에서 배치 52개, 동작 40개 검사를 통과했다. 320px에서 터치, 390px에서 키보드 Enter, 1280px에서 클릭으로 같은 주문의 상세를 열었다. **주문 목록**으로 돌아왔을 때 적용한 검색 조건을 유지했고, 결과 없음에서 **필터 지우기**로 네 건을 복원했다. 390px에서 상태 필터와 조회 실패 후 **다시 시도**도 확인했다.

Fast 관련 시험 50건과 판매자 앱을 포함한 통합 3.5 Task 전체 시험 17,045건이 통과했다. 별도 환경이 필요한 17건은 생략했다. 빌드 오류는 0개였고 Fast 경고는 2개, 통합 빌드 경고는 63개였다.

제품 서버·DB, 실기기, 주문 상태 변경은 실행하지 않았다. 전후 캡처, 측정과 시험은 `artifacts/local/mirror-ui-assist/20261106T120923946Z-seller-order-row-readability/completion.json`에 연결했다.
