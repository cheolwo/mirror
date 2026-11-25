# 주문자 음식 주문 상세 버튼의 접근성 이름

음식 주문 목록에서 서로 다른 주문의 버튼이 모두 **주문 상세**라는 이름으로 전달됐다. [공용 목록](../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderListPanel.razor)은 보조기기에 전달할 이름에 화면의 **주문 상세** 문구와 카드에 보이는 주문번호를 함께 담는다. 예를 들어 42번 가상 주문은 **주문 상세 EX-1106-042**라는 이름을 갖는다. 기존 선택 처리에는 같은 주문번호를 전달한다.

가상 주문 네 건을 실제 Razor로 렌더했다. 두 건은 같은 음식점이고 한 건은 음식점 이름이 없다. 수정 전에는 번호가 붙은 버튼 이름이 0개라 새 회귀 시험이 실패했다. 수정 후에는 네 버튼에 각각 주문번호가 붙고 화면 문구가 모두 **주문 상세**인지 확인했다. [렌더 회귀 시험](../../Ssalddel.Tests/Ui/Common/OrdererFoodOrderRefreshPolishTests.cs)

Fast 관련 시험 43개와 주문자 앱을 포함한 통합 3.5 Task 시험 17,052개가 통과했다. 별도 환경이 필요한 17개는 생략했다. Fast 빌드 경고는 2개, 통합 빌드 경고는 63개였고 두 빌드 모두 오류는 0개였다.

이번 근거는 Razor가 생성한 버튼 마크업이다. 브라우저 접근성 트리, 실제 보조기기 읽기와 상세 버튼 클릭은 미실행이다. 루트 README는 [이전 브라우저 검증 화면](2026-11-06-orderer-food-history-layout.md)을 표시한다.

수정 전 실패와 수정 후 시험 근거는 artifacts/local/mirror-ui-assist/20261106T211601241Z-orderer-order-action-name/validation.json에 연결했다.
