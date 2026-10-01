[기획·구현 · 음식점 모바일 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · restaurant-mobile-workflow r1]

# 음식점 앱 우선: 기존 주문 처리와 조리시간 화면 보완

## 승인 범위와 현재 위치

2026-09-27 사용자는 운영자 앱보다 음식점 앱을 먼저 완성하고, 기존 기능과 UI를 재사용해 상품 등록→주문 알림→조리시간 선택→수락→픽업 준비를 연결하도록 요청했다. 당면 입력은 우리 플랫폼 주문이다. 다른 플랫폼 주문 연동·실영업·배포 승인이 아니다. 기존 네 역할 앱은 유지하고 음식점 흐름 검증에 재사용한다.

이번 첫 변경은 주문함과 조리시간 화면에 한정한다. 전체 음식점 앱 완성이나 상품 등록 완료로 표시하지 않는다.

## 현재 재사용 가능한 것

- `RestaurantDeskApp`의 `/orders`, `/orders/{OrderNo}`, `/settings/preparation-times`와 기존 인증·SignalR·30초 서버 보완 조회.
- 주문 상세의 조리시간 선택·수락·조리시간 변경·픽업 준비 API 연결. 이 경로를 새 로컬 상태 전이로 대체하지 않았다.
- 서버 `api/v1/restaurant/menus`의 목록 GET·등록 POST·수정 PUT. 음식점 운영자 권한과 서버 음식점 범위를 사용한다. 아직 RestaurantDeskApp에 판매 메뉴 관리 화면을 연결하지 않았다.
- 기존 조리시간 설정은 기기 Preferences 기반이며 판매 상품 등록·서버 동기화가 아니다. 이를 명시하고 ‘상품 추가’를 ‘상품별 시간 기준 추가’로 수정했다.

## 이번 변경

1. 주문함에 수동 새로고침·마지막 성공 조회 시각·초기 로딩을 표시한다. 실패/권한 차단을 정상 빈 목록으로 표시하지 않고 기존 목록을 유지한다.
2. 중첩 목록 조회와 잠긴 계정의 새 조회를 막는다. 재조회 성공 시 이전 조회 실패 안내를 지운다. 주문 실행·알림 transport는 변경하지 않는다.
3. 조리시간 5·10·15·20분 빠른 선택, 기존 1~180분 직접 입력을 유지한다. 저장 중 입력/삭제/추가와 중복 저장을 막는다.
4. 기존 음식점 hero·카드 스타일과 좁은 화면 줄바꿈을 재사용한다. 새 디자인 시스템·지도·이미지를 추가하지 않는다.

## 검증과 한계

- 최종 Android `dotnet build -f net10.0-android --no-restore`: 경고0·오류0. 기록 `artifacts/restaurant-mobile-build-final.log`. APK 설치·서명 배포와 구분한다.
- 기존 화면 구성 시험에 조회 실패/빈 상태 구분, 빠른 조리시간과 기기 저장 경계 검사 2개를 추가했다. 이 시험은 소스 구성 검사이며 실제 알림 수신·버튼 조작·상태 전이를 증명하지 않는다.
- `FoodDeliveryV30PageCompositionTests` 23/23 통과(신규2 포함), 실패/건너뜀0. TRX는 `artifacts/local/validation/restaurant-mobile-ui/restaurant-mobile-ui.trx`. 시험 build에는 기존 `개체시각대응SchemaTests`의 xUnit2031 경고가 있었다.
- 경로 지정 Task 검증은 `Ssalddel.v3.5.slnx` 전체 build를 선택했다. build 오류0·경고69(DriverApp 패키지 제약/nullable 등), 전체 시험5409 중5402 통과·7 실패로 **Task 미통과**다. 실패는 `OfficialFoodIngredientJourneyTests` 1, `SsalddelApiClassificationTests` 4, `ConventionalArchitectureNamingTests` 1, `IntegratedBetaCatalogTests` 1이며 이번 화면2개 밖의 재료 UI/API metadata/명명 문서/WebApp route 분류다. 변경 전 전체 기준선을 실행하지 않았으므로 기존 실패라고 확정하지 않으며 범위 밖 파일은 수정하지 않았다. 로그 `artifacts/local/validation/20260927-103125/`. `git diff --check` 통과.
- 작업 시작 때 연결 Android 장치/에뮬레이터는 없었다. 실제 렌더·터치·스크린샷·백그라운드 알림·신규 APK 설치는 이번 완료 증거에 포함하지 않는다. 시각 검증은 대기다.
- 원문 오류 메시지의 사용자용 정제, 판매 메뉴 화면 연결, 주문함 단계 분류, 실제 동일 주문의 전체 앱 조작은 후속이다. 20분 초과 주문자 알림의 전체 연결도 이번에 새로 구현/검증하지 않았다.
- 서버·DB·기능 플래그·공개 정책·Unity·다른 역할 앱을 수정하지 않았다. commit·push·배포 없음.

## 다음 한 묶음

기존 메뉴 API의 권한·멱등/수정 충돌 계약을 재사용하여 음식점 메뉴 목록·등록·수정 화면을 연결한다. 현재 DTO는 메뉴명·설명·판매가·대표이미지Url·공개/품절·표시순서, 등록 `클라이언트요청Id`, 수정 `예상Revision`을 제공한다. 이미지 URL은 사진 파일 업로드 완료를 뜻하지 않으며 옵션 DTO는 이 계약에 없다. 이후 합성 메뉴1개→주문 알림→조리시간 선택·수락→픽업 준비를 같은 주문번호로 에뮬레이터에서 검증한다. 상품별 로컬 조리시간을 메뉴 등록으로 대체하지 않는다.
