# 관리자 음식 주문 상세의 상태 표시 너비

390px 가상 주문 상세에서 `전달 중` 표시가 324px 너비를 차지했다. 상태 표시 하나를 왼쪽으로 정렬해 문구에 맞는 너비를 사용한다. [상세 화면 코드](../../SsalddelAdminApp/Components/Pages/FoodOrderDetail.razor) · [이전 390px 상세](../assets/changes/2026-11-06-admin-food-order-touch/detail-390.png)

같은 390px에서 `전달 중`은 약 70px, 확인이 필요한 `조리·픽업 준비`는 약 115px로 표시됐다. 두 상태의 색·문구와 상세의 다음 담당을 확인했다. [2026-11-07 390px 가상 주문 상세](../assets/changes/2026-11-07-admin-food-order-detail-status/detail-390.png)

320px·390px·1440px에서 수정 전후 각각 90개 브라우저 검사를 통과했다. 가상 주문 4건을 검색하고 같은 주문의 상세를 열었다. 390px에서 키보드로 **최신 상태 확인**과 **음식 주문 목록으로**를 선택해 같은 주문의 재조회와 전체 목록 복귀를 확인했다.

Fast 관련 시험 58개와 통합 1.5 Task 전체 시험 17,051개가 통과했다. 별도 환경이 필요한 17개는 생략했다. 빌드 오류는 0개였고 Fast 경고는 5개, 통합 빌드 경고는 7개였다.

격리 브라우저에서 관리자 앱의 실제 목록·상세와 가상 조회를 연결했다. 실기기, 운영 서버·DB, 주문 상태 변경은 미검증이다. 전후 캡처·측정·시험 근거는 `artifacts/local/mirror-ui-assist/20261106T164627123Z-admin-food-order-detail-status/completion.json`에 연결했다.
