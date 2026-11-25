# 일반 입고 예정 목록의 제목과 검색칸 분리

창고 작업자가 가상 입고 3건이 로드된 일반 입고 예정 화면을 열었을 때 320px와 390px에서 목록 제목이 두 줄로 접혔고, 검색칸에는 안내 일부만 보였다. 좁은 화면에서 제목 아래에 검색칸을 배치했다. 검색 아이콘 옆에는 **업체·상품·SKU·참조번호**가 보인다. 이 배치는 [일반 입고 예정 화면](../../WarehouseManagerApp/Components/Pages/ExpectedInbounds.razor)에 적용한다.

같은 입고 3건을 로드해 수정 전 49개 확인, 수정 후 51개 검사를 수행했다. 320px와 390px에서 제목이 한 줄에 들어왔고 검색 안내 전체가 표시됐다. 키보드로 검색칸에 들어가 SKU를 입력하고, 결과가 없을 때 지우기 버튼으로 3건을 다시 표시했다. 선택한 입고 번호가 기존 작업 화면 주소로 전달되고, 목록에 돌아오면 같은 3건이 로드됐다. 작업 화면 도착 지점은 검증용이다. 1280px에서는 제목과 검색칸이 같은 줄에 표시됐다. 전체 표의 비교 이미지는 미리보기 외곽만 넓혀 확인했다.

[수정 전 390px](../assets/changes/2026-11-06-warehouse-inbound-readability/before-390.png) · [수정 후 390px](../assets/changes/2026-11-06-warehouse-inbound-readability/after-390.png) · [수정 후 320px](../assets/changes/2026-11-06-warehouse-inbound-readability/after-320.png)

창고 앱 Fast 관련 시험 262건과 통합 3.5 Task 전체 시험 17,045건이 통과했다. 환경 조건이 필요한 17건은 생략했다. 빌드 오류는 0개였다. Fast 경고 0개, Task 경고 140개를 확인했다. 브라우저 검증에는 실제 Razor 화면과 합성 읽기 응답을 사용했다. 실기기와 운영 서버·DB, 입고 작업 실행은 미실행이다. 판본·캡처·시험은 artifacts/local/mirror-ui-assist/20261106T103822637Z-warehouse-inbound-readability/completion.json에 연결했다.
