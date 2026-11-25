# 입고 검수 상세의 시각 안내 줄바꿈

390px에서 가상 입고상품 601번을 열면 입고 시각의 `(기기 시각)`이 두 줄에 걸쳐 단어 중간에서 끊겼다. [이전 390px 상세](../assets/changes/2026-11-06-warehouse-inspection-list-return/detail-390.png)

입고 시각과 검수 완료 시각의 괄호 안내가 한 덩어리로 이동하도록 표시한다. 기존 기기 현지 시각 변환과 값이 없을 때의 `-` 표기를 사용한다. [입고 검수 상세 화면](../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/InboundInspectionDetailScreen.razor) · [2026-11-07 390px 가상 입고 상세](../assets/changes/2026-11-07-warehouse-inspection-timestamp/detail-390.png)

320px·390px·1440px에서 수정 전 재현 검사 69개와 수정 후 동작 검사 81개가 통과했다. 검수 대기 건과 완료 건 모두 입고 시각의 괄호 안내가 한 줄에 들어왔다. 완료 시각과 날짜가 없는 경우의 표기도 확인했다. 390px에서 **검수 실행 화면**에 키보드 초점이 갔고, **목록** 링크에서 Enter로 복귀했을 때 ‘현미’ 검색과 2페이지가 유지됐다.

Fast 관련 시험 45개와 통합 3.5 Task 전체 시험 17,051개가 통과했다. 별도 환경이 필요한 17개는 생략했다. 빌드 오류는 0개였고 Fast 경고는 2개, 통합 빌드 경고는 140개였다.

격리 브라우저는 창고 앱 화면과 가상 조회 응답을 사용했다. 통합 웹은 같은 상세 컴포넌트를 조립하는 경로와 빌드를 확인했다. 제품 서버·DB, 실기기, 검수 저장은 미검증이다. 전후 캡처·측정·시험 근거는 `artifacts/local/mirror-ui-assist/20261106T154626116Z-warehouse-inspection-timestamp/completion.json`에 연결했다.
