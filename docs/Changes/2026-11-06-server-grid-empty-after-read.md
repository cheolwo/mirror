# 입고 예정 목록에서 조회 실패와 정상 빈 결과 구분

창고 관리자가 입고 예정 목록을 처음 열거나 검색할 때 조회가 실패하면, 오류 안내와 함께 ‘조건에 맞는 입고 예정이 없습니다’와 0건이 표시됐다. 공통 서버 표는 이제 조회에 성공한 뒤에만 결과 건수를 표시하고, 그 결과가 비었을 때만 기존 빈 목록 문구를 표시한다. [입고 예정](../../WarehouseManagerApp/Components/Pages/ExpectedInbounds.razor), [수입 화물 반입](../../WarehouseManagerApp/Components/Pages/ImportArrival.razor), [공동주택 반입 예정](../../WarehouseManagerApp/Components/Pages/ApartmentArrivals.razor)이 같은 [공통 표](../../Ssalddel.Ui.Common/Areas/App/Components/Crud/SsalddelServerDataGrid.razor)를 사용한다. 검색·페이지 이동·오류 안내와 다시 조회는 기존 동작을 사용한다.

실제 창고 목록 컴포넌트와 조회 모델을 브라우저에서 열고 합성 읽기 응답을 사용했다. 320px·390px에서 수정 전 10개 불일치를 재현했고, 수정 후 92개 검사가 통과했다. 조회 중, 첫 조회 실패, 재조회 성공 0건, 검색 실패, 같은 검색어로 다시 조회해 1건을 표시하는 흐름을 확인했다. 두 폭에서 페이지 전체의 가로 넘침과 브라우저 오류, 외부 및 업무 API 요청은 없었다. 기존 검색 안내 문구는 좁은 폭에서 입력칸 너비에 맞춰 일부만 보인다. [조회 실패 320px](../assets/changes/2026-11-06-server-grid-empty-after-read/initial-failure-320.png) · [정상 0건 390px](../assets/changes/2026-11-06-server-grid-empty-after-read/successful-empty-390.png)

공통 UI 빌드와 관련 시험 761건, 창고 앱을 포함한 통합 3.5 빌드와 전체 시험 17,034건이 통과했다. 환경 조건이 필요한 17건은 생략했다. 빌드 오류는 0개다(Fast 경고 2개, Task 경고 149개). 제품 API·DB와 네이티브 화면은 이 회차에서 실행하지 않았다. 전후 화면·빌드·시험과 소스 판본은 artifacts/local/mirror-ui-assist/20261106T083120776Z-server-grid-empty-after-read/completion.json에 연결했다.

다음 회차에는 화주 운송의뢰 상세의 첫 조회 실패에서 기존 재조회 진입점과 의뢰 번호 보존을 대조한다.
