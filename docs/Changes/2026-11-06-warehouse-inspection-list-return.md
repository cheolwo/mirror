# 입고 검수 상세에서 목록으로 돌아갈 때의 검색 조건

입고 검수 목록에서 새 검색어를 입력하면 화면에는 마지막 조회 결과가 남는다. 이때 **상세 확인**의 복귀 주소가 입력 중인 검색어를 사용해, **검수 목록**으로 돌아오는 순간 조회하지 않았던 검색어로 목록을 다시 읽을 수 있었다.

목록 조회가 성공할 때 결과에 사용한 검색어와 검수 상태를 기억한다. 상세와 목록을 연결하는 주소는 이 조건과 서버 응답의 페이지를 사용한다. [입고 검수 목록 화면](../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/InboundInspectionListScreen.razor)

320px·390px·1440px의 로컬 브라우저에서 가상 입고 건을 조회했다. 수정 전에는 ‘현미’ 목록 2페이지에서 ‘없는포장재’를 입력만 하고 상세를 거치면 돌아온 목록이 비었다. 수정 후 같은 입고 건의 상세를 열고 돌아오면 ‘현미’·검수 상태 전체·2페이지가 복원되고 두 건이 표시됐다. 새 검색어는 **조회**를 눌렀을 때 적용됐다. [390px 복귀 목록](../assets/changes/2026-11-06-warehouse-inspection-list-return/list-390.png) · [같은 입고 건 상세](../assets/changes/2026-11-06-warehouse-inspection-list-return/detail-390.png)

브라우저에서 수정 전 재현 검사 54개와 수정 후 동작 검사 57개를 확인했다. Fast 관련 시험 24개와 창고 앱·통합 웹을 포함한 3.5 Task 전체 시험 17,046개가 통과했다. 별도 환경이 필요한 17개는 생략했다. 빌드 오류는 0개였다. Fast 경고는 230개, 통합 빌드 경고는 140개였다.

검증 범위는 로컬 브라우저의 가상 조회와 읽기 전용 상세 복귀까지다. 전후 화면, 요청 기록과 시험 결과는 `artifacts/local/mirror-ui-assist/20261106T134625533Z-warehouse-inspection-return/completion.json`에 연결했다.
