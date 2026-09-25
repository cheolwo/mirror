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
- 각 역할 전이 뒤 실제 `OperationalWorldSceneClient`·Decoder·Interpreter·`FoodDeliveryOsObservationAdapter`·Router를 통해 `operational-world-scene.v2`를 다시 읽는다. 동일 가명 업무의 단계·revision·비식별 정책과 수령 확인 뒤 메모리 제거를 확인하지만, 이는 Unity 어셈블리의 live HTTP 메모리 계약 증거이지 Unity Editor 화면 증거가 아니다.

필수 환경 변수는 `FOOD_OBSERVER_BASE_URL`, `FOOD_OBSERVER_ACCOUNT_PASSWORD`다. `eng/verification/food-observer.ps1`로 준비한 `Staging + Simulation` 격리 표본에만 사용한다. 실행 결과의 `serverRuntimeProof`와 `databaseRoundTripProof`는 실제 서버·DB 왕복을 뜻하지만 `deviceUiProof=false`이며 에뮬레이터·물리 기기 UI 증거를 대신하지 않는다.
