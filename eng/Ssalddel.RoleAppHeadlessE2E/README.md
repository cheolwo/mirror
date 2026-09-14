# 역할 앱 Client headless E2E

Windows 장치 UI 전에 주문자·음식점·기사 앱이 실제로 사용하는 Client 소스를 같은 격리 서버에 연결해 음식 배달 정상 폐루프를 검증한다.

- `OrdererApp`은 공용 `주문자음식주문Client`와 앱 인증 Client를 사용한다.
- `RestaurantDeskApp`은 `Ssalddel음식주문Client`와 앱 인증 Client를 링크한다.
- `FDriverApp`은 `FoodDeliveryDriverApiService`와 앱 인증 Client를 링크한다.
- 장치 보안 저장소는 headless 전용 메모리 구현으로 대체한다. 업무 HTTP Client와 계약은 앱 소스를 그대로 컴파일한다.
- 비밀번호는 명령행 인자로 받지 않고 `FOOD_OBSERVER_ACCOUNT_PASSWORD` 환경 변수에서만 읽으며 출력하지 않는다.
- 실제 UI, 실제 영업, 결제, 외부 알림, Unity Editor·Play Mode·Game View를 검증하지 않는다.
- 각 역할 전이 뒤 실제 `OperationalWorldSceneClient`·Decoder·Interpreter·`FoodDeliveryOsObservationAdapter`·Router를 통해 `operational-world-scene.v2`를 다시 읽는다. 동일 가명 업무의 단계·revision·비식별 정책과 수령 확인 뒤 메모리 제거를 확인하지만, 이는 Unity 어셈블리의 live HTTP 메모리 계약 증거이지 Unity Editor 화면 증거가 아니다.

필수 환경 변수는 `FOOD_OBSERVER_BASE_URL`, `FOOD_OBSERVER_ACCOUNT_PASSWORD`, `FOOD_OBSERVER_RESTAURANT_ID`, `FOOD_OBSERVER_MENU_ID`다. `eng/verification/food-observer.ps1`로 준비한 새 격리 표본과 합성 메뉴에만 사용한다.
