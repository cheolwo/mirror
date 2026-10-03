# 음식점 주문 목록·상세 사용성 개선 r1

승인 기준은 [구현 r65](../../AI/Planning/시스템/PLAN-SYSTEM-FRANCHISE-OPERATIONS/restaurant-order-usability.implementation.r65.md)다. 기존 페이지를 유지하면서 현재 상태·다음 행동·필수 주문 내용을 먼저 보여준다. [실제 Android 캡처](../../Changes/2026-10-02-restaurant-order-usability-r1.md), [역할별 검토 목록](role-pages.html)에 연결한다.

## 1. 페이지

| 기존 화면 | 변경한 표시 | 유지한 책임 |
| --- | --- | --- |
| [OrderInbox](../../../RestaurantDeskApp/Components/Pages/OrderInbox.razor) · `/`, `/orders` | 상태·메뉴/수량·금액·접수시각·주문 보기. 정상 연결 설명과 주문번호는 상세 접기 | 주문 탐색·알림·30초 재조회·선택 주문 인계 |
| [OrderDetail](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor) · `/orders/{OrderNo}` | 현재 단계와 강조 버튼 하나, 조리 예상시간 선택. 거절/시간 변경과 배차 정보는 보조 영역 | 서버가 허용한 주문 확인·조리·준비 행동, 거절 사유·전표 출력 |

360px·412px에서 가로 넘침이 없고 최종 APK의 강조 버튼은52px, 화면 안 보조 버튼은48px 이상이다. 본문16px, 보조 설명14px이며 메뉴·상호·메시지는 줄바꿈한다. 네이티브 `<details>/<summary>`로 보조 영역을 열고 닫으며 기본 내용과 오류를 가리지 않는다. 변경한 스타일은 [기존 app.css](../../../RestaurantDeskApp/wwwroot/app.css)의 `.restaurant-order-flow` 범위에 한정한다.

## 2. 코드

기존 `I음식점주문DeskService`와 `AvailableActions`에서 만들어진 `수락가능`·`조리시작가능`·`픽업준비가능`을 사용한다. 화면만으로 새 권한·상태를 만들지 않는다. 기사 배정 전에는 조리를 기다리도록 안내하며 배차불가는 상단에 확인 필요를 표시한다. 업무 실패·전표 실패 안내와 기존 조리시간 제한/빠른 선택을 유지한다.

상세 조회 실패는 주문 없음과 구별한다. 마지막 읽은 메뉴를 유지하고 상태 변경 버튼을 비활성화하며 성공 재조회 후 해제한다. 주문 목록은 오류·인증/소유권 처리와 마지막 목록 보존을 유지한다. 주문 확인의 연속 클릭은 화면 처리 중 잠금으로 억제하며 서버의 전체 중복 요청 보장을 새로 입증한 것으로 보지 않는다.

화면 문구 변경에 맞춰 기존 `FoodDeliveryV30PageCompositionTests`와 `DriverFoodDeliveryWorkspacePageTests`의 표시 계약만 정합시켰다. 새로운 요율·업무 서비스·공개 계약·라우트는 추가하지 않았다.

## 3. DB

테이블·관계·마이그레이션 변경은 없다. 기존 [기사 배정 후 조리 시작 연결](../../AI/Planning/시스템/PLAN-SYSTEM-FRANCHISE-OPERATIONS/driver-first-cooking.implementation.r62.md)을 재사용한다. [음식점 DeskService](../../../RestaurantDeskApp/Services/음식점주문DeskService.cs)→[기존 API Client](../../../RestaurantDeskApp/Services/Ssalddel음식주문Client.cs)→[음식주문Controller](../../../Ssalddel/Controllers/Food/음식주문Controller.cs)로 주문과 현재 운송/배달 시도를 읽고 갱신한다. 격리 Development/Simulation 서버·전용 DB의 합성 주문만 조작했다. API의 저장 후 재조회와 관리자 생명주기 추적으로 상태를 대조하며 실제 영업 주문·실송금으로 승격하지 않는다.

## 실제 실행과 판본

같은 합성 주문 `FOOD-20261002060514840`에서 음식점 Android 화면의 주문 확인→기사 배정 대기→기사 배정 뒤 조리 시작→픽업 준비를 조작했다. 기사 수락/픽업/전달과 주문자 수령 확인은 기존 Client를 사용했다. 최종 APK에서 같은 주문을 직접 상세 경로로 다시 읽어 `수령확인`과 완료 안내를 확인했다. 완료 주문은 진행 수신함 목록에서 제외되므로 목록에 남아 있다고 주장하지 않는다.

전체 생명주기 조작은 첫 APK에서 수행했다. 최종 APK는 보조 버튼 높이·강조 버튼 선택자·배차불가 안내를 보완한 뒤 다시 build/설치한 판본이다. 최종본의 확인 필요 화면은 기존 별도 합성 주문을 읽었으며 거절/수락하지 않았다. 최종본에서도 상세 조회 실패→처리 버튼 차단→복구, 360px·412px, 상세 접기와 조리시간 선택을 확인했다. 상태를 변경하는 서비스/메서드는 판본 사이에서 변경하지 않았다.

긴 메뉴16행·긴 문구는 실제 Android WebView의 DOM에 일시적으로 배치해 무넘침을 확인한 뒤 복원했다. 서버에 대량 메뉴 주문을 넣은 증거가 아니다. 최초 표본은 기사 위치 heartbeat가 오래돼 추천 후보가 없었다. 운영 모드를 켜거나 DB 큐를 직접 수정하지 않고 새 합성 주문과 최신 기사 위치로 검증을 다시 시작했다. Client의 반복 로그인 중429가 발생해 정상 제한 만료 후 수령 확인을 완료했다. 제한 설정을 바꾸지 않았다.

## 검증 결과와 남은 범위

- Android API36 x86_64 에뮬레이터 `emulator-5556`. 물리 휴대폰 미검증. 디바이스 폭을 시험 후 원래1080×2400으로 복원했다.
- 최종 Debug APK build: 경고0·오류0. 서명v2/v3·16KiB ZIP 정렬·실제 설치 확인. API24 이상, target36, arm64-v8a/x86_64. 개인 로컬 서버용이며 공개 Release가 아니다.
- 집중27/27 통과. Task 전체5,515/5,522 통과. 선행 `20261002-123318`의7실패와 항목·오류가 같다. API 분류4·명명1·통합 라우트1·공식 재료 CSS1은 다른 범위이며 전체 시험 통과로 표현하지 않는다.
- 전표 출력 창 실패는 기존 안내를 유지했다. 인쇄·실결제·당일 정산/송금·은행 입금·Unity Play Mode·공개 배포는 수행하지 않았다.
- 빈 수신함·최초 로딩 순간·강제 세션 만료·모든 필터/긴 실제 주문·배차불가 실제 발생·거절 제출·조리시간 변경 제출은 이번 Android 확인으로 완료 처리하지 않는다. 목록의 연결 우회 시험에서는 실패 안내가 관찰되지 않아 실제 목록 조회 실패/SignalR 재연결 증거로 채택하지 않았다. 상세의 실패/복구 증거와 구별하며 목록/상세의 기존 경로와 입력은 유지했다.

APK·SHA-256·전이별 JSON/PNG·로그·실패 대조는 `artifacts/local/restaurant-order-usability-r1/`에 있다. 개인정보나 자격 증명은 문서에 저장하지 않는다. commit·push는 하지 않았다.
