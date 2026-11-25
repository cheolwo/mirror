# 약속과 적재 조건 안에서 B 우선 수행 r20

상태: Approved / ReadyToDispatch. 2026-10-27 사용자는 A 수행 중 B를 수락한 경우에도 A·B의 약속을 지킬 수 있으면 B를 먼저 처리하도록 보완을 지시했다. 수락 순서와 실제 정차 순서를 분리한다. 같은 로컬 checkout의 직접 구현 승인이고 다른 작업실로의 Git 인계가 아니다.

## 작업지시와 수용

- 기준 branch `dev/mirror-integration`, commit `8a97384604e6c68a1b00fc71a612d699a3651825`. 기존 비공개 다운로드 자료를 보존한다.
- 담당은 운영 서버의 화물 추천·수락·현재 업무 projection이다. 기존 일정 삽입 평가·구간 적재·정본 재조회와 typed 정차 자료를 재사용한다. A 종료 뒤 B 상차 가능을 모든 삽입의 필수 조건으로 두지 않는다.
- 선택된 같은 시도의 실제 경로·상하차 시간창·작업시간·남은 절차·선상차후하차·구간 중량/부피/혼적/독차를 대조한다. 새 동적 삽입의 미확인 근거를 현재 GPS나 0분 작업으로 인증하지 않는다.
- 추천 조회와 자동 큐에서 같은 선택의 B 상차 ETA/접근 거리를 사용하고, 수락 직전 최신 원장·위치·화물·차량·약속을 재검사한다. A/B 어느 쪽도 위반하면 보류한다.
- 수락 시 검증한 typed 정차 순서를 기존 원장의 JSON 근거에 결속한다. 새 DB·원장·migration을 만들지 않는다. 완료한 정차는 현재 상태로 제외하고 다른 기사·새 업무·변경 조건·손상 근거를 예전 순서로 자동 승인하지 않는다.
- 일정과 다음 행동 조회는 같은 미완료 정차 순서를 따른다. 기존 운송 상태·완료 시각을 순서 변경 수단으로 덮어쓰지 않는다. 추천 지도는 확인되지 않은 A→B 순서를 그리지 않는다.
- 경로 정차의 주소 공개 보류도 표시용 운송번호나 의뢰 ID 문자열 비교가 아닌 typed 운송 ID로 현재 목록에 대조한다. 보류·연결 불명·중복 ID의 정차는 주소와 좌표 근거를 가려 기존 공개 동의 경계를 보존한다.
- 이 범위는 제한된 기존 일정 삽입이며 모든 기존 정차의 전역 최적화·혼적 정책 완화·자동 수락·고정거점·FCM·운영 플래그 활성화·실운행·배포·commit/push를 포함하지 않는다.
- 통합 회귀에서 확인한 기존 분산 창고의 다중 상차는 전문 Wave 계획을 유지한다. 동일 기사·현재 Wave 판본·A/B 회원 의뢰와 기존 `CargoDigest`·typed 경로 근거가 맞는 전문 인계에만 기존 순차 수락 호환을 한정할 수 있다. 그때도 일반 화물의 새 동적 B 우선이나 0분 작업시간의 안전 인증으로 승격하지 않는다. 다른 Wave·지문 변경·계획 미확인은 보류한다. 공통 실차 조회 provider는 기존 DI를 재사용한다.

## 소유권과 writePaths

- 루트: `Ssalddel/Services/Dispatch/Recommendation/CargoDispatchRecommendationService.cs`, `Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.cs`, 필요한 기존 시도 선택 helper와 새 `Ssalddel/Services/Dispatch/Recommendation/화물운행중삽입평가Service.cs`, `Ssalddel/Services/Dispatch/Recommendation/운송삽입시도Selector.cs`, 직접 관련 모델의 필요한 init 필드.
- 수락/일정 담당: `Ssalddel/Services/Dispatch/Recommendation/화물배차수락적격성Service.cs`, `Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs`, `Ssalddel/Services/Dispatch/Recommendation/기사운송일정구성Service.cs`, 새 `Ssalddel/Services/Dispatch/Recommendation/화물수락정차순서.cs`, 필요한 `국내화물추천저장근거`의 호환 init 필드와 직접 회귀.
- 수락/일정의 좁은 통합 호환: 새 `Ssalddel/Services/Dispatch/Recommendation/화물분산순차인계Policy.cs`(필요할 때), 기존 R77 연속 수락 시험의 동일 provider 인계. 기존 분산 Wave/의뢰의 시각이나 작업시간을 임의로 덮어쓰지 않는다.
- 화면 소비 담당: `Ssalddel/Application/Driver/Transport/Handlers/화물운송작업공간조회QueryHandler.cs`, `Ssalddel/Application/Driver/Transport/Handlers/운송현재조회QueryHandler.cs`, `DriverApp/Services/DriverHomeRoutePlanningService.cs`, 필요한 `DriverApp/Models/Driver/DriverRequestItem.cs`, `DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs`, `DriverApp/NativeDriverRecommendationPage.cs`와 직접 회귀. 새 화면/새 역할은 만들지 않는다.
- 회귀 담당: `Ssalddel.Tests/Services/Dispatch/Queue/CargoNextPickupR64Tests.cs`, 새 `Ssalddel.Tests/Services/Dispatch/Queue/CargoFeasibleStopOrderR20Tests.cs`, 필요한 기존 일정·수락·작업공간/클라이언트의 관련 시험. 각 담당의 시험 경로가 겹치면 쓰기 전에 조율한다.
- 문서/결과는 루트: 이 PLAN, 기존 `cargo-map-first-workloop.design.r3.md`의 대체 관계, `docs/AI/{CURRENT_WORK,PLANNING,DECISIONS}.md`, `docs/Changes/README.md`, `docs/Changes/2026-10-27-cargo-feasible-stop-order-r20.md`. 수정 전 사본/명세/해시·빌드/시험은 `artifacts/local/cargo-feasible-stop-order-r20/`.

## 검증

1. B 우선만 기한을 지키는 추천/큐·수락, B를 넣으면 A가 늦는 차단, 근사 경로·시간/작업시간 누락·옵션 불일치·수신/기존 업무 변경과 독차·구간 초과를 검사한다.
2. 수락 순서 저장→새 context의 일정/다음 행동 재조회→B 상차/하차 뒤 남은 A, 계정/약속/업무 변경·손상/중복 순서를 검사한다. 표시 문자열로 정차를 복원하지 않는다.
3. 관련 집중 회귀와 `validate-changes.ps1 -Paths` Fast/Task, 실제 영향 server/DriverApp 빌드를 수행하고 통과/실패/환경 생략을 별도 기록한다.
4. 실제 서버 HTTP/운영 DB·휴대폰 설치·화면/실제 GPS·음성·실도로 경로 확인은 별도 실행 증거가 있을 때만 완료로 기록한다. 이번 요청으로 폰/기존 서버를 강제 변경하지 않는다.
