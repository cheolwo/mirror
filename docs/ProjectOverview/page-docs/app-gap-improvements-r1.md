# 기존 앱 보완 r1 — 주문 갱신·기사 지도·주문별 정산

2026-10-03 사용자 승인으로 기존 화면과 주문 흐름을 보완했다. 새 앱을 만들거나 실제 은행/PG 송금을 활성화하지 않는다.

## 페이지 → 코드 → DB

| 화면 | 연결 코드 | 저장·권위 |
| --- | --- | --- |
| 주문자 상세 | [기존 컴포넌트](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderWorkspace.razor.cs) → [새로고침 Controller](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/주문자음식주문새로고침Controller.cs) → 기존 ViewModel/Client | 기존 주문 API가 상태를 결정. 새 테이블 없음 |
| 기사 메인 | [MainPage](../../../FDriverApp/Pages/MainPage.xaml) → [MainPageModel](../../../FDriverApp/PageModels/MainPageModel.cs) → food workspace | 본인 최종 완료 주문 정산 최근 40개 조회. 월 이용료와 별도 |
| 관리자 주문 추적 | [기존 페이지](../../../SsalddelAdmin/Components/Pages/FoodOrderOperationsTrace.razor) → Service/Controller → [정산 UseCase](../../../Ssalddel/Application/Admin/Food/음식주문기사정산UseCase.cs) | 기존 주문·시도·운송과 새 정산/모의 증빙 |
| Android 기사 지도 | [지도 handler](../../../FDriverApp/Handlers/FDriverNativeMapViewHandler.Android.cs) → [준비 상태](../../../FDriverApp/Controls/NaverMapReadiness.cs) | 앱 SDK 설정·인증·연결. 서버 Directions 계산과 별도 |

## 주문 자동 갱신

10초 간격을 유지하면서 선택한 진행 주문을 기사 위치 추적 여부와 관계없이 조회한다. 배정 전 `추적전` 상태도 갱신한다. 미선택·로그아웃·접근 차단·찾을 수 없음·전달 완료·수령 확인·거절·취소에서는 자동 HTTP를 생략한다. 전달 완료 뒤 수령 확인은 기존 명시적 버튼으로 처리한다.

자동 조회는 진행 중인 수동 작업과 겹치면 건너뛴다. 선택·인증·수령 확인과 목록 검색·새로고침·필터 초기화·페이지 이동을 직렬화하고 화면 종료 시 요청·타이머를 취소하며 상위 구독도 해제한다. 로그아웃 뒤 대기 순서를 얻은 기존 목록 요청은 인증을 다시 검사해 API를 호출하지 않는다. 일시적 실패는 다음 주기에 재시도한다.

## 지도 설정

`SSALDDEL_NAVER_MAP_SDK_NCP_KEY_ID` 환경 변수 또는 `NaverMapSdkNcpKeyId` 빌드 속성을 `obj` 리소스에 주입한다. 추적 설정·로그에 키 값을 넣지 않는다. 누락/예시값이면 SDK 초기화·타일 요청을 생략한다. 지도 객체 준비와 지도 로딩을 구분하고 설정 누락·인증 실패·오프라인·20초 로딩 실패를 표시한다.

NAVER Cloud Dynamic Map 활성화 및 `kr.ssalddel.fdriver` 패키지 등록은 [공식 SDK 안내](https://navermaps.github.io/android-map-sdk/guide-ko/1.html)를 따른다. 서버 Directions Secret이나 Google Maps 키로 대체하지 않는다. 현재 PC에는 유효한 Android SDK ID가 없으며 실제 지도 타일 성공은 미검증이다.

## 주문별 정산·모의 지급

전달 완료와 같은 DB transaction에서 최종 유효 시도·확정 기사·수락 당시 동결 대금/근거를 정산에 결속한다. 새 요율로 재계산하지 않는다. 수령 확인도 같은 저장에서 정산을 갱신하며 EventHandler는 멱등 복구를 지원한다. 공제 근거가 없으면 공제·수령액은 null이다. 날짜별 법정 보험/세금을 임의 계산하거나 월 이용료로 대체하지 않는다.

기존 workspace의 `OrderSettlements`, 관리자 operations-trace의 `DriverSettlement`를 확장했다. 새 `POST api/v1/admin/food-orders/{orderNo}/simulate-driver-payout`는 관리자·서버 Simulation·수령 확인·최종 기사/시도·동결 금액·명시적 시험 공제 근거·revision을 검사한다. 실패 이력을 보존하고 같은 근거로 재시도한다. 동일 키/입력은 기존 증빙을 반환하고 다른 입력·이미 성공한 지급·오래된 판본은 차단한다. Operational에서는 409이며 실송금 호출은 없다. `IsActualTransferCompleted=false`다.

새 [모델](../../../Ssalddel.Domain/음식/음식주문기사정산.cs)의 `음식주문기사정산`·`음식주문기사지급검증`은 주문/시도/운송 FK, 고유 키, revision 동시성 token을 사용한다. [Migration](../../../Ssalddel/Migrations/20261003103000_AddFoodOrderDriverSettlement.cs)과 기존 snapshot을 함께 변경했다. 운영 DB에는 적용하지 않는다.

기사/운영자는 공통 [표시 모델](../../../Ssalddel.Ui.Common/Areas/App/Models/FoodDeliverySettlementDisplay.cs)로 미확정 금액을 0원과 구분한다. 모의 수령액에는 시험 공제·실제 입금 아님을 표시한다. 소수 시험 금액도 반올림 없이 표시한다. 관리자 입력 주문번호가 조회 결과와 달라지면 이전 결과임을 알리고 지급을 차단한다. 재조회는 저장된 시험 근거를 복원하고 통신 실패 재시도에는 같은 입력/멱등 키를 유지한다.

`ServerExecutionModeCode`는 현재 응답 서버의 실행 모드다. 저장된 `ExecutionModeCode`와 모의 지급 이력은 과거 증빙으로 보존하며 버튼 허용 근거로 사용하지 않는다. 모드 정보가 없으면 `Unknown`으로 닫는다. 이전 Simulation 기록을 Operational 서버에서 읽어도 모의 지급 버튼을 열지 않는다.

## 격리 검증 환경

기존 food-observer는 실제 외부 HTTP를 차단하므로 현재 운영 경로 요금 서비스로는 추천을 확정할 수 없었다. 검증 Hosting에만 [합성 경로 요금 adapter](../../../Ssalddel/Services/Development/FoodObserver/검증표본기사제안요금Service.cs)를 등록하고 합성 기사 두 명의 정상 음식배달 오토바이 프로필을 추가한다. 기존 프로필과 검증 볼륨은 덮어쓰지 않는다.

Simulation·명시적 observer 활성화·지정 기사·정확한 합성 좌표 쌍에만 0.250km를 입력한다. 현재 음식 요율로 산정하고 미관측 기상 할증을 만들지 않는다. 알 수 없는 좌표·일반 기사·자동차·Operational은 차단한다. 동결 원장 JSON, 요금 판본, 관찰 결과와 역할 Client 실행 결과에 `FoodObserverSimulationRouteFixture` 출처를 남긴다. 실제 NAVER Directions 호출·주행 거리·기상 관측 증거가 아니다. 운영 경로 실패를 이 값으로 보충하지 않는다.

## 검증

| 범위 | 결과 | 근거 |
| --- | --- | --- |
| Fast 집중 시험 | 105/105 통과. 새로고침 18사례, 지도 준비, 정산, observer fixture, UI 계약 포함 | `artifacts/local/validation/20261003-103223/` |
| Task 전체 제품 | 전체 3.5 build 통과. 5,592/5,599 통과, 작업 전과 같은 7개 실패 | `artifacts/local/validation/20261003-103258/`, `baseline-comparison.json` |
| 역할 앱 Client HTTP | 주문 확인→기사 배정→조리→픽업→전달→수령 확인→모의 실패/재시도/멱등→기사·관리자 재조회 완료. 익명401·비관리자403·공제 누락409 확인 | `headless-result.json`, 합성 주문 `FOOD-20261003012748001` |
| 독립 MySQL 조회 | 정산1개, 실패/성공 증빙2개. 2,500원−100원=2,400원, revision4, 합성 거리 출처·0.25km 보존 | `mysql-readback.log` |
| Android 기사 UI | API36 에뮬레이터, 1080×2400/420dpi. 로그인 복원·현재 원장 재조회·스크롤 후 같은 주문 금액/모의 안내, 지도 설정 안내의 상단 버튼 비겹침 확인 | [실제 화면 기록](../../Changes/2026-10-03-app-gap-improvements-r1.md) |

Task의 기존 실패는 업무 실행 책임 명명1개, WebApp capability 미분류 route1개, 역할 Controller 분류3개, 공공자료 업무 action 명명1개, 공식 재료 UI 최소 높이1개다. 전체 게이트는 미통과이며 이번 변경에 추가된 실패는 없다.

격리 DB는 현재 EF 모델로 새 전용 표본을 준비하고 실제 저장·재조회를 확인했다. 운영 DB migration 적용·기존 운영 자료 backfill은 수행하지 않았다. Android UI 검증은 기사 화면에 한정하며 주문자/관리자 페이지의 실제 UI 입력·물리 휴대폰·유효 SDK 지도 타일 성공·은행/PG 지급은 미검증이다. Unity 어셈블리의 live HTTP 상태 해석은 확인했지만 Unity Editor/Play Mode/Game View 증거가 아니다.

원시 로그·검증 APK는 `artifacts/local/app-gap-improvements-r1/`에 둔다. 기존 데이터·영상·관련 없는 dirty 작업을 보존했고 commit·push·운영 배포·실송금은 수행하지 않았다.
