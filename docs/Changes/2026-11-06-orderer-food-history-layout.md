# 주문자 음식 주문 내역의 창 너비와 실제 본문 폭

주문자 앱의 본문은 최대 520px로 표시된다. 1280px 창에서 기존 음식 주문 화면은 창 전체 너비를 기준으로 목록과 상세를 두 열로 배치했다. 가상 주문 4건을 넣어 확인하니 목록에 142px만 할당됐고 검색 입력이 상세 영역과 겹쳤다.

주문자 앱의 [음식 주문 내역 화면](../../OrdererApp/Components/Pages/FoodOrderHistory.razor)에서는 실제 본문에 맞춰 한 열로 표시한다. 같은 1280px 창에서 목록은 486px를 사용했고 검색칸이 영역 안에 들어왔다. **주문 상세**로 선택한 한 건을 확인하고 **주문 목록**으로 돌아온다.

320px·390px·1280px에서 가상 주문 4건을 로드해 48개 브라우저 검사를 통과했다. 긴 음식점명·상태·금액·상세 버튼, 검색·결과 없음·조건 해제, 같은 주문번호와 금액의 상세 확인 및 검색 조건을 유지한 복귀를 확인했다. 390px에서는 별도로 **조리중** 상태 필터를 실행해 해당 주문 한 건이 표시되는 것도 확인했다.

[수정 전 1280px](../assets/changes/2026-11-06-orderer-food-history-layout/before-1280.png) · [수정 후 1280px](../assets/changes/2026-11-06-orderer-food-history-layout/after-1280.png) · [390px 주문 카드](../assets/changes/2026-11-06-orderer-food-history-layout/orders-390.png) · [같은 주문 상세](../assets/changes/2026-11-06-orderer-food-history-layout/detail-390.png)

주문자 앱 Fast 관련 시험 62건과 통합 1.5 Task 전체 시험 17,045건이 통과했다. 환경 조건이 필요한 17건은 생략했다. 빌드 오류는 0개였다. Fast 경고 0개, Task 경고 7개를 확인했다. 브라우저는 현재 Razor·CSS와 가상 조회 응답을 사용했다. 실기기, 제품 서버·DB, 주문 변경은 미실행이다. 판본·측정·시험은 artifacts/local/mirror-ui-assist/20261106T110923077Z-orderer-ledger-readability/completion.json에 연결했다.
