# 국내화물 AI 배차 검토의 조회 실패와 정상 빈 상태 구분

운영자가 [국내화물 AI 배차 검토](../ProjectOverview/page-docs/SsalddelAdmin/SsalddelAdmin-P37/README.md)에서 검토할 운송 의뢰를 읽을 때, 기존 화면은 조회가 실패해도 빈 지도와 판단 양식을 열었다. 이전 자료를 본 뒤 다시 조회에 실패하면 이전 의뢰와 저장 버튼도 남았다.

조회가 끝나기 전에는 진행 상태를 표시한다. 조회 오류에는 상단의 **다시 조회**, 로그인 필요에는 이 화면으로 돌아오는 로그인 링크, 권한 오류에는 권한 확인 안내를 표시한다. 정상 응답에서 의뢰가 0건이면 **검토할 운송 의뢰가 없습니다.**를 표시한다. 의뢰가 있고 AI 묶음이 없으면 기존 수동 검토 화면을 열며, 나중에 조회가 성공하면 작성 중이던 판단 메모를 다시 표시한다. [화면 코드](../../SsalddelAdmin/Components/Pages/DomesticCargoDispatchAIReview.razor)

320px·390px의 실제 페이지와 클라이언트에 합성 GET 응답을 연결했다. 수정 전 같은 128개 검사 중 52개가 실패했고 수정 후 128개가 통과했다. 첫 조회, 다시 조회, 정상 0건, 수동 검토, 이전 자료를 읽은 뒤의 실패와 복구, JSON null 응답, 401·403, 가로 넘침을 확인했다. [조회 오류 320px](../assets/changes/2026-11-06-operator-dispatch-review-read-state/failure-320.png) · [정상 빈 결과 390px](../assets/changes/2026-11-06-operator-dispatch-review-read-state/empty-390.png)

Fast 관련 시험 51건과 Task 전체 시험 17,043건이 통과했다. 환경 조건이 필요한 17건은 생략했다. Fast 빌드 경고와 오류는 0개였고 통합 빌드는 경고 7개, 오류 0개였다. 실제 기기·제품 서버·DB 및 판단 저장은 미실행이다. 소스 판본과 로그는 `artifacts/local/mirror-ui-assist/20261106T100122060Z-operator-dispatch-review-read-state/completion.json`에 기록했다.
