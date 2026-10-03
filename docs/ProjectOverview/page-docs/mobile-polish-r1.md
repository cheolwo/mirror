# 기존 모바일 앱 검증·보완 r1

2026-10-02 사용자 승인 범위는 기존 앱의 화면을 역할별로 찾아보고, 기사·음식점의 첫 다섯 화면을 실제 Android에서 검증·보완하는 것이다. 새 앱이나 제품 데모 페이지를 만들지 않고 기존 로그인·메뉴·주문·배달 경로를 사용했다.

## 1. 페이지와 역할

[검색·필터 목록](role-pages.html) · [전체 표](role-pages.md) · [검토 원본](role-reviews.json). 기존 Razor 소스 **466개**와 네이티브 `ContentPage` **15개**, 합계 **481개**를 소스별로 한 번씩 센다. 기존 **538개 route·17개 제품 호스트·고정 ID**를 보존했다. 역할 후보/공유 호스트와 실제 로그인 권한은 별개다.

| 주 분류 | 소스 수 |
| --- | ---: |
| 기사 | 53 |
| 음식점 | 13 |
| 주문자 | 55 |
| 화주·판매자 | 107 |
| 창고·마트 작업자 | 67 |
| 플랫폼 관리자 | 62 |
| 커뮤니티·일반 이용자 | 102 |
| 공통 진입·인증·설정 | 19 |
| Unity 검토 담당자 | 2 |
| 인사 담당자 | 1 |

명시적 필요성 검토는 **유지 9개·통합 후보 1개·보류 1개**다. 나머지 470개는 `판단 보류`이며 불필요하거나 미구현이라는 뜻이 아니다. 이번 Android 검증은 아래 다섯 소스의 특정 상태만 확인했다. 전체 화면·권한·단말 완성률로 확대하지 않는다.

| 첫 묶음 | 기존 소스 | 유지 이유·확인한 상태 |
| --- | --- | --- |
| 음식점 로그인 | [Login.razor](../../../RestaurantDeskApp/Components/Pages/Login.razor) | 계정/세션 경계. 빈 입력·연결 실패·정상 로그인·재로그인 |
| 메뉴 관리 | [Menus.razor](../../../RestaurantDeskApp/Components/Pages/Menus.razor) | 가게별 메뉴 revision 경계. 등록→저장→서버 목록 재조회, 비공개 메뉴 9,800원 |
| 주문 수신함 | [OrderInbox.razor](../../../RestaurantDeskApp/Components/Pages/OrderInbox.razor) | 목록 탐색과 실시간 복구. 로그인 후 서버 주문 조회·SignalR 연결·상세 이동 |
| 주문 상세 | [OrderDetail.razor](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor) | 한 주문의 상태 변경. 배정 전 조리 시작 차단→배정 후 시작→준비 완료→수령 확인 재조회 |
| 기사 메인 | [MainPage.xaml](../../../FDriverApp/Pages/MainPage.xaml) | `App.CreateWindow`의 실제 네이티브 진입점. 로그인·위치 권한·제안 총액·수락·픽업·전달·진행 없음 |

목록/상세는 탐색과 상태 변경 책임을 유지한다. 통합 후보는 가게 관리 아래 기본 조리시간 설정을 연결하는 방식이다. 기사 운행·진행/정산·계좌도 메뉴 묶음 후보로 남겼으며, 페이지 삭제나 대규모 병합은 하지 않았다.

## 2. 페이지에 연결된 코드

| 화면 | 기존 Client·서버 경로 | 이번 보완 |
| --- | --- | --- |
| 로그인 | `RestaurantAuthService` → `api/v1/auth/login`, `refresh` → 기존 인증/보안 저장소 | 제품 인증 계약 유지. 검증 계정을 앱 화면에서 입력 |
| 메뉴 | `I음식점메뉴ApiClient`/`Ssalddel음식주문Client` → `음식점메뉴관리Controller` → `음식점메뉴관리UseCase` | 기존 저장·재조회 경로 검증. 새 메뉴 모델/정책 없음 |
| 수신함 | `음식점주문DeskService`/SignalR → 음식주문 수신함 API → `음식점음식주문조회UseCase` | 기존 조회·상세 인계·실시간 연결 검증 |
| 상세 | `주문수락후전표준비Async`/`조리시작Async` → 음식주문 acceptance/progress API → 기존 Command UseCase | 출력 창 실패 시 ‘조리를 시작했다’는 잘못된 성공 안내를 제거. 주문 확인·배차 요청과 배정 후 조리 시작을 구분 |
| 기사 메인 | [MainPageModel](../../../FDriverApp/PageModels/MainPageModel.cs) → `FoodDeliveryDriverApiService` → 음식 배달 workspace/offers API | 활성 배달이 없으면 수행 단계·버튼·수령자 패널 숨김. 저장된 로그인 복원 후 최초 로그인 안내를 실제 조회 결과로 갱신 |

운영 추적은 유효한 기사 배정 뒤 조리/준비 단계에서 기사 책임을 표시한다. 배정 없는 기존 경로는 유지하고, 주문·원장·최신 시도/기사의 일치를 확인한다. Unity 관찰 Router가 `주문확인`을 활성 단계로 받아들이도록 보완했다. 새로운 Scene·Prefab·권위 모델은 만들지 않았다.

기존 [역할 앱 headless 실행기](../../../eng/Ssalddel.RoleAppHeadlessE2E/README.md)를 현재 흐름에 맞췄다. 앱 Client 소스를 그대로 연결하고 기사 배정 전 조리 차단, 배정 후 조리/준비, 같은 가명 업무의 단계별 사본을 검증한다. `--prepare-ui`와 `--confirm-ui-receipt`는 장치 조작의 준비/사후 재조회이며 그 출력 자체를 UI 증거로 표시하지 않는다.

## 3. DB·원장과 실행 환경

| 업무 | 기존 저장·조회 | 이번 실행 경계 |
| --- | --- | --- |
| 계정 | 기존 Identity 사용자·역할/갱신 세션 | 격리 검증 계정만 사용. 비밀번호/토큰은 문서·캡처에 저장하지 않음 |
| 메뉴 | MySQL `음식점공개프로필`·`음식점메뉴`, 가게 소유 범위·revision | Android 메뉴 등록 후 독립 서버 목록 재조회 |
| 주문 | 음식 주문 저장소·기존 공동 원장/Outbox | 합성 음식점/주택/메뉴로 생성. 실제 주문/주소 없음 |
| 배차·수행 | MySQL `운송원장`·`음식배달시도`·기존 이행 이벤트 | 제안 수락→픽업→전달 뒤 실제 서버 재조회 |
| 운영/Unity 관찰 | 운영 추적 Query 및 `operational-world-scene.v2` | 서버 HTTP와 Unity 어셈블리의 메모리 해석 확인. Editor/Game View 증거 아님 |

기존 food-observer Docker 구성을 `Development + Simulation`, loopback **5321**로 사용했다. 이 작업 전용 새 볼륨을 쓰고 기존 데이터 볼륨과 기존 AVD는 보존했다. 실제 결제/외부 알림/영업 데이터·운영 DB는 적용하지 않았다. 합성 좌표는 검증 대장의 위치이며 실제 행정동 근거가 없어 `unclassified` 관찰 권역을 사용한다.

## 실제 Android 실행

전용 `mirror_mobile_review` 에뮬레이터, `emulator-5556`, API36·x86_64·1080×2400에서 기존 앱 두 개를 설치했다. 음식점 WebView의 실제 폭은 **412px**, 확인한 메뉴/주문 화면의 문서 폭도 **412px**다. 이 값과 실제 PNG는 단순 합성 HTML 렌더와 구별한다. 지도 위치는 에뮬레이터의 명시적 검증 좌표다.

같은 주문 **`FOOD-20261002031249795`**에서 음식점 주문 확인, 기사 수락, 음식점 조리 시작/준비 완료, 기사 픽업/전달을 Android 화면으로 조작했다. 마지막 주문자 수령 확인과 관리자 종료 재조회는 기존 앱 Client를 사용하는 HTTP 실행기에서 수행했다. 음식점 앱에 다시 로그인한 뒤 같은 주문의 `수령확인`·`배달 완료`를 읽었다. 주문자/관리자 Android UI까지 완주한 것으로 보고하지 않는다.

실제 [화면 변경·PNG](../../Changes/2026-10-02-mobile-page-polish-r1.md)를 보존했다. 기사 진행 PNG는 앞선 별도 합성 주문의 픽업 상태이며 위 주문의 캡처로 바꾸어 설명하지 않는다. 최종 기사 APK는 빈 상태·안내 갱신을 별도로 재검증한다.

## 검증 기록과 APK

검증 로그·XML·DOM 상태·APK·해시 목록은 `artifacts/local/mobile-page-polish-r1/`에 둔다. 이 폴더는 Git 제외다. 실제 장기 보존 PNG와 결과 문서는 별도로 저장한다.

| 검사 | 결과 |
| --- | --- |
| Fast `20261002-115514` | 서버/제품·Unity build와 변경 범위 집중 시험 **서버 15/15·Unity 7/7** 통과 |
| Task `20261002-121700` | 제품/Unity build 통과, Unity **959/959**. 서버 **5,514/5,522**, 기존 7실패 외에 새 안내 문구를 기대하지 않는 이전 시험 1실패 발견 후 수정 |
| 최종 Task `20261002-123318` | 제품/Unity build 통과, Unity **959/959**, 서버 **5,515/5,522·7실패**. 기존 `20261002-103106`과 실패 이름·오류 메시지 동일, 추가 실패 없음. 전체 suite 통과로 표시하지 않음 |
| 최종 집중 | 생명주기·역할 실행기·기사 권한/수령자·음식점 화면 구성 **48/48** 통과. 마지막 기사 안내 수정은 APK build와 실제 Android 재검증으로 확인 |
| Android APK | 두 앱 build **오류 0·경고 0**. 기존 package·버전·ABI, v2/v3 개발 서명·16KB native 정렬·최종 SHA-256 확인. 최종 기사 APK의 로그인 복원과 로그아웃→로그인 모두 조회 안내/빈 상태 확인 |
| 역할 목록 | 고정 소스/route 보존·링크·생성 결과 일치, 실제 브라우저 검색/역할/판단 필터 검증 결과를 별도 JSON에 저장 |
| 실제 HTTP | `FOOD-20261002033241806` 수령 확인 완료. 네 역할 Client·서버/DB와 주문대기/주문확인/기사배정/조리중/픽업대기/픽업완료/전달완료의 **7단계 Unity 사본** 확인, 수령 확인 후 관찰 제거. `deviceUiProof=false`를 유지하고 Android 증거와 분리 |

[음식점 개인 Debug APK](../../../artifacts/local/mobile-page-polish-r1/RestaurantDeskApp-debug.apk) · [기사 개인 Debug APK](../../../artifacts/local/mobile-page-polish-r1/FDriverApp-debug.apk). 기존 package ID·0.1.0(1)·최소 API24/목표 API36·arm64-v8a/x86_64·개발 서명을 유지한다. 서버 주소는 loopback5321이며 물리 휴대폰에는 연결된 검증 서버와 `adb reverse`가 필요하다. 공개 Release·스토어 배포·실제 휴대폰 검증은 하지 않았다.

## 남은 문제와 다음 묶음

1. 기사 지도는 검증 위치·핀·경로 선을 표시했지만 바탕 타일이 빈 격자다. [Android 지도 설정](../../../FDriverApp/Platforms/Android/Resources/values/strings.xml)과 최종 APK의 `naver_map_sdk_ncp_key_id`·legacy 값에 `YOUR_NAVER_MAP_SDK_NCP_KEY_ID`가 들어 있음을 확인했다. 유효한 SDK 키/앱 등록 연결과 외부 접속을 확인해야 하며 지도 시각 완성으로 보고하지 않는다.
2. Android WebView에서 전표 출력 창을 열지 못했다. 주문 상태는 저장되지만 모바일 인쇄 기능은 후속 대응이 필요하다.
3. 서버/Android 빌드를 함께 실행할 때 기사 앱 첫 실행에서 ANR을 한 차례 겪고 대기 후 회복했다. 시작 성능·실제 단말 안정성은 미검증이다.
4. 검증 실행기와 장치가 같은 합성 계정에 로그인한 뒤 장치의 갱신 세션이 만료되는 경우를 확인했다. 재로그인 복구는 확인했으며 이를 실사용 세션 동시성 검증으로 확대하지 않는다.
5. 전체 시험의 기존 7실패와 음식 배달 통합 이력/실지급 증빙 결손은 [앞선 문서화 결과](implementation-r1.md)의 범위를 유지한다.

다음 검증 묶음도 기존 화면 최대 다섯 개로 진행한다. 먼저 지도 바탕·앱 시작·전표의 이 묶음 미완료 항목을 보완하고, 이후 기사 음식 배달 이력/월 이용료와 음식점 기본 조리시간 설정을 검토한다. 실제 PG·은행 지급·운영 배포는 별도 작업이다. 이번 작업에서 commit·push는 수행하지 않았다.
