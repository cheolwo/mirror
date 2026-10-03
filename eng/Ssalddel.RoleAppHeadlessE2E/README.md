# 역할 앱 Client headless E2E

Android 장치 UI 전에 주문자·음식점·기사·운영자 앱이 실제로 사용하는 Client 소스를 같은 격리 서버에 연결해 음식 배달 정상 폐루프를 검증한다.

- `OrdererApp`은 공용 `주문자음식주문Client`와 앱 인증 Client를 사용한다.
- `RestaurantDeskApp`은 `Ssalddel음식주문Client`와 앱 인증 Client를 링크한다.
- `FDriverApp`은 `FoodDeliveryDriverApiService`와 앱 인증 Client를 링크한다.
- `SsalddelAdminApp`은 `AdminAuthenticatedApiClient`로 같은 주문의 현재 생명주기 단계·책임 주체·예외 여부를 매 단계 재조회한다.
- 장치 보안 저장소는 headless 전용 메모리 구현으로 대체한다. 업무 HTTP Client와 계약은 앱 소스를 그대로 컴파일한다.
- 음식점 전용 API로 `역할 앱 E2E 합성 한상` 메뉴를 멱등 준비한다. 실제 상호·메뉴·영업 자료가 아니며 격리 DB 밖으로 게시하지 않는다.
- 비밀번호는 명령행 인자로 받지 않고 `FOOD_OBSERVER_ACCOUNT_PASSWORD` 환경 변수에서만 읽으며 출력하지 않는다.
- 실제 UI, 실제 영업, 결제, 외부 알림, Unity Editor·Play Mode·Game View를 검증하지 않는다.
- 기본 r4 실행은 전달 완료 정산 생성→수령 확인→공제 미확정 차단→명시적 100원 시험 공제로 모의 실패/재시도→동일 키 재호출→기사/관리자 재조회를 검증한다. 100원은 법정 공제율이 아닌 `SimulationFixture`다. 익명401·비관리자403도 확인하며 은행/PG 호출·실입금은 없다.
- 격리 observer의 정확한 합성 주소 쌍·지정 오토바이 기사에만 0.250km 거리 입력을 사용한다. 추천의 동결 판본에 `FoodObserverSimulationRouteFixture`가 있는지 검사하고 실행 결과에 출처·범위를 남긴다. 실제 NAVER Directions 요청·주행 측정·기상 관측을 검증한 것이 아니다. 일반 운영 서비스의 거리 확정 요건과 외부 HTTP 차단은 유지한다.
- 각 역할 전이 뒤 실제 `OperationalWorldSceneClient`·Decoder·Interpreter·`FoodDeliveryOsObservationAdapter`·Router를 통해 `operational-world-scene.v2`를 다시 읽는다. 동일 가명 업무의 단계·revision·비식별 정책과 수령 확인 뒤 메모리 제거를 확인하지만, 이는 Unity 어셈블리의 live HTTP 메모리 계약 증거이지 Unity Editor 화면 증거가 아니다.

필수 환경 변수는 `FOOD_OBSERVER_BASE_URL`, `FOOD_OBSERVER_ACCOUNT_PASSWORD`다. `eng/verification/food-observer.ps1`로 준비한 `Development + Simulation` 격리 표본에만 사용한다. 실행 결과의 `serverRuntimeProof`와 `databaseRoundTripProof`는 실제 서버·DB 왕복을 뜻하지만 `deviceUiProof=false`이며 에뮬레이터·물리 기기 UI 증거를 대신하지 않는다.

현재 흐름은 주문 등록 → 음식점 주문 확인·배차 요청 → 기사 제안 수락 → 음식점 조리 시작 → 준비 완료 → 기사 픽업·전달 → 주문자 수령 확인이다. 기사 배정 전 조리 시작/준비 완료가 닫혀 있고 배정 후 조리 시작이 열리는지 함께 확인한다.

`--prepare-ui`는 동일 격리 서버와 앱 Client로 합성 주문 하나를 등록한 뒤 멈춘다. 이후 Android 화면에서 주문 확인과 배차·조리·인계를 조작하기 위한 준비이며, 결과는 `Prepared`, `deviceUiProof=false`다. 기본 실행의 완료 검증과 혼동하지 않는다.

`--confirm-ui-receipt 주문번호`는 Android에서 처리한 전달 완료를 기존 주문자 Client로 조회한 뒤 수령 확인하고 운영자 Client로 종료를 재조회한다. 화면 입력은 이 도구 밖에서 수행하므로 실행 결과 자체는 `deviceUiProof=false`이며, 같은 주문번호의 별도 Android 캡처·조작 기록과 대조한다. 합성 주소는 검증 좌표 대장의 `검증 표본 음식점/주택`을 사용하고 지역 투영은 `unclassified`로 둔다. 법정동으로 추측 분류하지 않는다.
