# 음식 배달 4역할 모바일 시험 마감 r27

주문자 수령 확인이 상품 상세 뒤에 묻히던 화면과, 운영자 재조회 실패 뒤 남은 주문 정보의 최신 여부가 불명확하던 표시를 보완했다. 기존 공통 화면·client·서버 업무를 사용해 주문 작성부터 음식점·기사·주문자·운영자 인계를 로컬에서 확인하는 시험 환경도 연결했다. [구현 전 페이지 책임과 범위](../ProjectOverview/page-docs/food-mobile-four-role-r27.md)를 따른다.

이 문서는 **구현 및 로컬 검증 기록**이다. Fast, 공통 화면·HTTP 인계와 SQL/Mongo 상태·식별자 대조를 확인했다. 마지막 표시 보완을 포함한 동결 소스의 전체 Task 시험도 통과했다. 새 화면과 APK·에뮬레이터 설치 인계는 아래 검증 표에서 별도로 구분한다. 사용자가 지정한 `지금은 로컬 검증까지만` 범위이며 Azure는 제외한다. 다른 스레드의 [음식 배송비 선택 옵션 r26](2026-10-07-delivery-cost-sharing-r26.md) 변경은 보존하며 이 기록의 완료 범위로 합치지 않는다.

## 화면과 업무 연결

| 역할·화면 | 보완한 동작 | 유지한 경계 |
| --- | --- | --- |
| 주문자 음식 주문 상세 | 수령 대기에는 현재 단계와 `음식 수령 확인`을 상품 상세 앞에 한 번 표시한다. 수령 메모는 접어서 보조 정보로 제공한다 | 서버의 수령 확인 허용 여부가 권위다. 기사 전달과 주문자 수령 확인을 분리하고 기존 취소·지원·자동 수행처 기능을 보존한다 |
| 운영자 음식 주문 상세 | 재조회 실패 뒤 남은 주문 추적은 이전 확인 정보임을 표시하고 마지막 확인 시각을 구분한다 | 최신 조회 전 업무 행동을 차단한다. 다른 주문·계정·권한으로 전환되면 이전 내용과 확인 시각을 정리한다 |
| 음식점 현재 주문 | 기존 인지·조리·준비 화면과 허용 행동을 같은 주문의 인계 시험에 연결한다 | 유효한 기사 배정 뒤 조리하는 조건을 유지한다. 자동 제안을 기사 수락이나 조리 시작으로 취급하지 않는다 |
| 음식 기사 현재 업무 | 기존 픽업 이동·가게 도착·픽업·전달 단계와 완료 목록·상세를 같은 주문에서 확인하도록 연결한다 | 픽업 전 음식점 정보, 픽업 후 전달지·고객 요청·주문 정보의 순서와 서버 상태별 행동을 유지한다 |

제품 화면은 기존 [페이지 단일 책임](../Architecture/WholeRoadmapPagePrinciple.md)과 [역할 앱 시각 기준](../Architecture/RoleAppVisualDesignStandard.md)을 따른다. UI에서 다음 상태나 금융 결과를 임의로 확정하지 않는다.

실제 주문 작성 화면을 연결하면서 Simulation 서버는 시험 주문을 허용하지만 화면은 판매자 인증 판본을 요구하는 불일치를 확인했다. 안내 응답에 `ExecutionModeCode`를 가산하고 **서버가 최신 안내에서 `Simulation`을 명시한 경우에만** 판매자 판본 없는 안내 확인을 허용했다. 판매자는 미확인으로 표시하며 실제 인증 결과를 만들지 않는다. 운영 모드·구형/알 수 없는 응답·조회 중·조회 실패·화면 종료 뒤에는 이 예외를 허용하지 않는다. 사용자 동의 체크와 기존 운영 서버 검증은 유지한다.

주문 작성의 전송 중 반복 제출은 같은 작성 요청을 한 번만 보내도록 시험을 추가했다. 기존 응답 유실 복구는 원 요청 ID의 결과 조회를 먼저 하고, 사용자의 명시적 재시도에서만 같은 요청을 사용한다. 새 요청 ID로 불명확한 제출을 덮지 않는다.

## 전용 로컬 시험 서버

[서버 준비 도구](../../eng/verification/food-mobile-four-role.ps1)와 [전용 Compose](../../eng/verification/docker-compose.food-mobile-test.yml)는 `mirror-r27-food-mobile-check` 프로젝트, 새 고유 물리 볼륨 3개와 논리 DB `ssalddel_food_mobile_test`를 사용한다. 기존 운영·사용자 DB와 r26 시험 리소스를 재사용하지 않는다. MySQL·Mongo·앱은 내부 네트워크에 두고 gateway만 `127.0.0.1:5363`에 게시한다. 비밀번호·토큰·인증서는 Git 제외 `.private/`에 보관한다.

`Staging`·`Simulation`·컨테이너 실행·정확한 DB/호스트·시험 계정·fixture 판본·기능 설정이 모두 맞을 때만 음식 자동 제안 시험을 활성화한다. 기본값은 꺼짐이다. 시험 주문·주소·음식점·기사에 한정해 기존 배차 core의 추천·만료·재추천을 사용한다. 기사 수락·음식점 인지/조리·픽업·전달·주문자 수령 확인은 역할 화면의 명시적 행동으로 진행한다.

거리는 고정 시험 자료 `0.350km`이며 출처에 `IsEstimated=true`, `ActualMapRequest=false`를 보존한다. 실제 지도 API 요청·Directions·GPS·운행 거리의 증거가 아니다. 시험 worker는 처음 준비한 fixture 운행과 위치만 다루며, 사용자의 수신 OFF·운행 종료·다른 앱/위치 상태를 자동으로 되돌리지 않는다. UI 시험용 위치도 측정되지 않은 정확도를 부여하지 않는다.

Docker 이미지 빌드에는 선택 소스의 전후 지문을 결속한다. 추가·수정뿐 아니라 삭제된 untracked 경로도 양방향 집합 비교로 감지한다. 빌드 명령 성공만으로 작업 중 바뀐 소스를 검증했다고 기록하지 않는다.

## 실제 공통 화면 검증의 진행 상태

[시험 준비](../../eng/CommerceProtectionPreview/prepare-four-role-r27.ps1)는 합성 계정 로그인·기존 메뉴·기사 운행/수신/위치를 준비하며 주문을 생성하지 않는다. [공통 화면 시험](../../eng/CommerceProtectionPreview/verify-four-role-r27.cjs)은 실제 `OrdererFoodOrderComposer`에서 주문을 생성하고 기존 공통 Razor·client·HTTP를 사용한다. 운영자 상세도 제품 `FoodOrderDetail.razor`를 링크한다. 인증 어댑터와 입력 자료는 시험용이며 제품 로그인 UI·실제 판매자 신원 확인을 대신 검증하지 않는다.

초기 실패를 최종 성공 기록으로 덮지 않는다.

| 판본·시도 | 확인 사실 | 후속 확인 |
| --- | --- | --- |
| 첫 작성 화면 시험 | 제출 후 완료 안내에서 자동으로 상세에 이동한다고 가정한 harness timeout. 전체 인계 성공으로 기록하지 않음 | 실제 `주문 상세 보기` 행동을 선택하도록 시험 보완 |
| 자동 제안 보완 전 | 주문 `FOOD-20261007063138937` 생성 뒤 시험 worker의 제안을 확인하지 못함 | 픽업 좌표 쌍의 NULL 판정 보완 후 서버 재빌드·재시험 |
| 소스 변경 중 서버 빌드 | Docker exit code는 0이었지만 빌드 중 worker 소스 변경을 감지해 `BuildFailedOrSourcesChanged`로 기록 | 소스 변경 없는 빌드와 준비 응답을 별도 확인 |
| 이전 공통 화면 시험 | 같은 주문의 390/320px 화면 결과가 갱신됨. 로컬 `real-ui/verification.json`에는 `Completed`, 검사 11개·화면 36개·오류 0개가 기록됨 | 서버 이미지와 같은 주문의 HTTP/SQL/Mongo 근거 대조 완료. 별도 운영자 스타일 시험 6화면 추가 |
| 최종 동결 소스의 첫 화면 시험 | 새 주문 `FOOD-20261007082534639`를 실제 작성 화면에서 1건 생성. 시험이 이전 조리시간 입력란 이름을 찾아 멈춤 | 주문 제출 중복 방지 성공·화면 4장·HTTP 원본 보존. 음식점 수락 전 실패이며 추가 주문을 만들지 않음 |
| 최종 동결 소스의 복구 시험 | 같은 주문을 현재 입력란 이름으로 재개. 작성 화면 4장은 원본 해시 일치로 결속하고 이후 역할 흐름을 수행 | `frozen-ui-bbafb7f1/recovered/real-ui/verification.json`: 36화면·11검사·오류 0. 운영자 별도 6화면·3검사 통과. 새 주문의 SQL/Mongo 검사 11개도 통과 |

운영자 조회 실패는 개발 preview의 HTTP handler에서 GET 503을 한 번 주입해 이전 정보 표시·새로고침 복귀를 확인하는 방식이다. 최신 조회 전 행동 차단은 `AdminFoodOperationsStateTests`와 제품 버튼의 `Disabled=@(!state.CanReview)` 조건에서 확인했다. 완료 주문의 실제 화면 시험은 중단 검토 행동을 수행하지 않았다. 제품 서버·공개 API·운영 인증을 변경하는 장애 주입이 아니다. 수령 대기의 주 버튼 1개와 전달/수령 분리도 같은 공유 UI에서 확인했다.

완료 상세의 3일 열람 제한은 기존 서버·client 시험에 실제 경과 시간 대신 제어된 시각을 사용한다. 만료 직전·정확 경계·직후, 최신 조회 실패 시 고객/주문 정보 숨김, 계정·역할 전환과 늦은 이전 응답을 다룬다. 금융 내역과 개인정보 열람 수명을 분리하며 이 시험을 실제 3일 대기 또는 휴대폰 확인으로 확대하지 않는다.

원시 근거는 Git 제외 `artifacts/local/food-mobile-four-role-r27/`에 보존한다. 초기 판본과 최종 `frozen-ui-bbafb7f1/`의 준비·실패·복구·스타일 시험을 구분한다. `database-evidence.json`은 이전 주문, `database-evidence-frozen-ui.json`은 최종 새 주문이다. 원문 인증 정보는 문서나 대표 화면에 넣지 않는다. 최종 대표 PNG 6개는 원본 해시 대조 후 `docs/assets/changes/food-mobile-four-role-r27/frozen/`로 선별하고 이전 대표 PNG도 보존했다.

## USB 시험 APK와 인계

[USB 시험 패키징 도구](../../eng/release/publish-mobile-usb-test.ps1)는 음식 주문자·음식점·음식 기사·운영자의 기존 MAUI 앱을 대상으로 한다. 기본은 Debug `0.1.27/27`, 내장 런타임과 `http://127.0.0.1:5363/`이며 USB `adb reverse tcp:5363 tcp:5363`가 필요한 개인 시험용이다. 기존 저장소 밖 Android debug key만 사용하고 서명키를 생성하거나 교체하지 않는다.

도구는 선택 소스 지문·APK 식별자/판본·서명·내장 런타임·전달 사본의 동일성을 검사하도록 구성했다. 자동 설치·삭제·기기 데이터 초기화·Azure 배포를 수행하지 않는다. [패키징 도구 시험](../../eng/release/Test-MobileUsbPackaging.ps1)의 합성 APK fixture 검사 20개 통과는 **실제 APK 빌드나 설치 성공의 증거가 아니다**. APK 산출·설치·로그인 첫 화면·네 역할 업무 완주는 각각 독립 확인 대상으로 둔다.

실제 네 APK는 동결 작업트리 실행 `3df8d6a0d895485ab3a3ba0e007c9565`에서 생성했다. 선택 소스 2,895개의 전후 지문은 `abc17109cc69a5f6a14f13d37b6322add38ce83605f6682513484b3f305b1b8e`로 동일했다. 서명·정렬·Debug 메타데이터·각 앱 assembly와 내장 런타임을 확인하고 canonical 인계 폴더로 복사한 모든 바이트를 SHA-256으로 대조했다. `usb-test-manifest.json`과 `handoff-copy.json`에 각각 빌드·사본 근거를 보존한다. APK에는 arm64-v8a와 x86_64가 들어 있으며 생성만으로 어느 기기의 실행을 검증했다고 판단하지 않는다.

현재 음식 기사 APK 빌드에는 Naver Map SDK ID 미설정 경고가 있다. 실제 지도 대신 설정 안내를 표시하는 판본이며, 로컬 업무·서버 통신 검증을 실제 지도 표시나 경로 검증으로 확대하지 않는다. 실제 지도 키 설정은 이번 APK 빌드에 추가하지 않았다.

## 검증 표

| 항목 | 현재 확인 사실 | 최종 판정 |
| --- | --- | --- |
| 앞선 Fast | 294개 통과·오류 0. 실행별 출력으로 전체 3.5 앱 빌드 완료 | `20261007-160120` 통과. 최종 동결 소스의 전체 Task와 구분하며 초기 284개 판본과 실패 로그도 보존 |
| Task 전체 시험 | 최종 동결 소스 8,397개 통과·실패 0·환경 조건 2개 건너뜀, 전체 3.5 빌드 오류 0 | `20261007-171340` 통과. 이전 main 소스 `20261007-162919`의 8,388개 통과 및 경로 실패 3개를 별도 보존 |
| 조건부 Mongo 통합 시험 2개 | 전용 DB 생성·정리 guard를 확인하고 별도 임시 실행 환경으로 시도 | 미실행. Linux runner 종료 코드 139·TRX 미생성. 기존 건너뜀 상태 유지, 임시 컨테이너 모두 제거 |
| 로컬 서버 Build·Initialize·Start·Probe | 성공, `Ready`·readiness 200. 빌드 중 소스 변화 0개 | 같은 주문의 SQL/Mongo 식별·상태·시간·정산 연결 11개 통과. 아래 원장 요약 단계의 의미 한계는 보존 |
| 실제 공통 UI/client/HTTP | 최종 동결 소스의 동일 주문 작성부터 수령 확인, 390/320px 36화면·검사 11개·오류 0 | 통과. 재개 검사 1개 포함, 최초 작성의 1 POST 근거를 보존. 운영자 스타일·실패/복구 6화면·3검사 추가 통과 |
| 완료 상세·중복 제출·계정/역할·안내 동의 경계 | 기존 동등 시험을 유지하고 부족한 중복 제출/실행 모드 경계 시험 추가 | 앞선 Fast 294개와 최종 동결 Task 8,397개 통과에 포함. 3일 제한은 제어된 시각 시험 |
| USB APK 도구·실제 네 APK | 합성 검사 20개와 별도로 Debug 0.1.27/27 네 APK 생성, 서명·정렬·판본·내장 런타임·사본 해시 대조 | 통과. 선택 소스 2,895개 전후 일치. 인계 manifest 생성·원래 저장소의 canonical handoff 사본 대조 완료 |
| 에뮬레이터 설치·로그인·대표 화면 | Android API 36/x86_64의 네 앱 설치 바이트가 APK와 동일. 실제 로그인·역할 첫 화면 4개 성공, 최종 캡처 18개 해시 확인 | 통과. 기존 AVD 원본 7개와 등록 정보 해시 불변을 확인하고 소유 에뮬레이터 종료. 네이티브 전체 업무 완주는 미검증 |
| 시각 변경 기록 | 주문자 수령 대기·운영자 이전 확인 정보 표시, 제품 scoped CSS 앵커와 중복 라벨·종료 담당·기사 안내 보완 | 최종 대표 PNG 6개를 원본 해시 대조 후 `docs/assets/changes/food-mobile-four-role-r27/frozen/`에 보존 |
| Git | 다른 스레드 배송비 변경과 기존 작업 보존 | commit·push 미수행 |

Azure 조회·배포, 실제 휴대폰 설치·통신, 실제 결제/은행 지급, GPS 관측, 실제 지도/Directions, 공개 운영은 수행하지 않았다. SKU·브랜드 자료 조사나 법령 범위 확대도 이번 작업에 포함하지 않는다. 각 검증은 표의 해당 범위에서만 판단하며, 소스·화면 확인을 APK·운영 완료 판정으로 확대하지 않는다.


## 원장 대조와 화면 근거

SQL은 같은 클라이언트 요청의 주문 1건, 수령확인·배달완료, 기사 수락→조리→준비→픽업→전달→수령의 시각 순서와 정산 연결을 확인했다. 지급은 NotRequested·검증 0건이며 공제와 수령액은 null이다. 정산 행의 실행 모드 빈 문자열은 그대로 보존하고 서버의 Staging/Simulation 근거를 별도로 결속했다.

Mongo의 food-order는 기존 준비 원장 요약 규칙 때문에 완료 상태에서도 현재단계Key가 픽업대기이며, transport는 완료/인수완료다. 실제 음식 앱의 업무 권위와 현재 행동은 SQL에서 확인한다. 원시 단계가 일반 현재 단계로 표시되는 경우의 의미 혼동은 후속 후보로 남겨 두었고 lifecyclePhaseConsistencyVerified=false를 성공으로 바꾸지 않았다. 소스·스키마·기존 DB를 자동 교정하지 않았다.

조건부 Mongo 시험의 추가 시도는 `mongo-integration/result.json`에 보존했다. 현재 MySQL/Mongo 상태 대조 11개 통과와 별개로, 두 통합 시험의 case 실행·DB 생성·cleanup assertion에 도달하지 못했으므로 시험 통과나 실제 시험 DB 정리를 인증하지 않는다. 기존 Mongo 노출·설정·DB·볼륨을 변경하지 않았고 자체 임시 컨테이너의 부재를 확인했다.

제품 운영자 페이지의 MudStack 루트에 scoped CSS 속성이 없던 문제는 HTML 래퍼로 보완했다. 기존 정보·동작은 유지했고 실제 selector 일치, 48px 버튼, 24px 제목, 흰 카드·1px 경계, 모바일 가로 넘침 없음을 확인했다. preview에는 제품 관리자 테마와 원 CSS를 연결했다. 초기 인증 토큰 만료와 스타일 검사기의 CSS 수집 오류도 실패 로그에 보존했다.

| 역할·상태 | 대표 화면 |
| --- | --- |
| 음식점 · 기사 배정 전 | [화면](../assets/changes/food-mobile-four-role-r27/frozen/restaurant-before-assignment.png) |
| 기사 · 제안/직접 수락 | [화면](../assets/changes/food-mobile-four-role-r27/frozen/driver-offer.png) |
| 기사 · 고객에게 전달 중 | [화면](../assets/changes/food-mobile-four-role-r27/frozen/driver-delivery.png) |
| 주문자 · 수령 확인 대기 | [화면](../assets/changes/food-mobile-four-role-r27/frozen/orderer-receipt.png) |
| 운영자 · 조회 실패/이전 확인 정보 | [화면](../assets/changes/food-mobile-four-role-r27/frozen/admin-stale.png) |
| 운영자 · 최신 조회 복구 | [화면](../assets/changes/food-mobile-four-role-r27/frozen/admin-refreshed.png) |

## 에뮬레이터 실행 근거

세션 `f8daa60142424eb5962302aeffd756e5`는 기존 AVD의 `read-only`·`no-snapshot` 실행이다. `installed-packages.json`에서 네 APK의 device base.apk 바이트와 판본을 대조했다. `four-role-homes-recovered.json`은 실제 로그인 제출·현재 앱·계정 확인·원래 화면 크기의 역할 홈을 4역할 모두 성공으로 기록한다. 최종 18개 PNG/XML의 해시와 설치 APK 지문을 결속했다.

초기 Failed aggregate와 실패 3회, 성공 4회 기록은 각각 보존한다. 주문자 시험은 바뀐 로그인 버튼·원장 탐색 문구를 찾지 못했고, 음식점 시험은 모바일 CSS가 계정명을 숨기는 것을 실패로 판정했다. 제품·동결 소스·APK는 바꾸지 않고 관찰한 입력/탐색 문구에 맞춰 Git 제외 시험 도구를 조정했다. 음식점의 계정 확인 때만 화면을 720px로 넓혔으며 finally에서 원래 `320x640`·density `160` 복구를 대조한 뒤 대표 화면을 찍었다. 계정 확인을 생략하거나 실패를 무조건 성공으로 바꾸지 않았다.

주문자 앱의 일반 거래 원장과 음식 주문 이력은 별도 화면이다. 네이티브 시험은 로그인·역할 첫 화면까지이며, 음식 주문 전체 인계 성공은 공통 제품 UI/client/HTTP 시험의 근거다. 기사 홈에는 지도 설정과 위치 권한 확인이 필요하다는 실제 안내가 표시된다. 실지도·GPS·실운행을 대신 인증하지 않는다.

`emulator-stop.json`은 소유 PID 종료, 원본 AVD backing/config 7개 파일과 registry 해시 불변, 앱 자동 삭제·데이터 초기화 미수행을 기록한다. 에뮬레이터 설치 결과를 실제 휴대폰 설치나 영구 설치로 확대하지 않는다.

| 네이티브 역할 첫 화면 | 근거 |
| --- | --- |
| 주문자 | [화면](../assets/changes/food-mobile-four-role-r27/native/orderer-home.png) |
| 음식점 | [화면](../assets/changes/food-mobile-four-role-r27/native/restaurant-home.png) |
| 음식 기사 | [화면](../assets/changes/food-mobile-four-role-r27/native/driver-home.png) |
| 운영자 | [화면](../assets/changes/food-mobile-four-role-r27/native/admin-home.png) |

USB 시험 조건과 수동 설치 순서는 [설치 안내](../../eng/release/README.mobile-usb-test.md)에 있다. 초기 공유 obj 경로의 파일 잠김·중간 DLL 누락과 긴 Android native 출력 경로 오류는 보존했다. APK 재시도는 UseArtifactsOutput·짧은 실행별/앱별 ArtifactsPath·기존 root Directory.Build.props 선택으로 참조 19개까지 출력 폴더를 격리한다. 기존 obj나 사용자 데이터를 지우지 않는다.

전체 시험 `20261007-161242`의 실패 3개는 `AppContext.BaseDirectory`에서 고정 네 단계 위를 저장소로 가정한 시험의 경로 문제였다. 격리 출력에서는 그 위치가 `Hongdal/artifacts/local/food-mobile-four-role-r27`이며 소스와 시험 프로젝트가 없다. [시험 루트 탐색](../../Ssalddel.Tests/TestRepositoryRoot.cs)을 추가해 저장소 solution과 시험 csproj 표식을 확인하도록 3곳만 보완했다. 제품 운임 저장·SQL 생성·고유성·외래키 assertion은 유지했고 제품 동작이나 스키마를 바꾸지 않았다. 실패 로그와 보완 뒤 재검증을 구분한다.

## 화면 표시 보완과 소스 고정

대표 화면에서 운영자 완료 주문의 `공제 공제 미확정`·`수령액 수령액 미확정` 중복을 확인해, 미확정 문구에 같은 라벨을 다시 붙이지 않도록 보완했다. 수치가 있으면 0을 포함해 기존 라벨을 유지한다. 종료된 주문의 빈 담당란은 `없음 · 주문 종료`, 다른 상태의 미확인은 `미확인`으로 표시한다. 기사 제안 안내는 `현재 위치를 갱신해 주세요.`로 바꿨다. 업무 상태·권한·정산 계산은 변경하지 않았다.

첫 네 APK 빌드는 앱별 검사를 통과했지만, 다른 스레드의 공통 UI 변경을 최종 소스 지문 검사에서 감지해 완료 manifest와 설치 인계를 만들지 않았다. 해당 실패·출력은 보존했다. 이후 HEAD `9336637bde31ec97b5740e2a82a802dd490127ca`의 별도 detached 작업트리에 현재 non-ignored 소스 12,874개를 복사하고 원본 전후·사본 SHA-256 집합의 일치를 확인했다. 이 고정 소스로 전체 Task와 APK를 검증했으며, 원래 작업트리의 다른 변경을 정리하거나 되돌리지 않았다. 전체 시험 수에는 고정 시점에 존재한 다른 작업의 시험도 포함되므로 8,397개 모두를 이번에 새로 작성한 시험으로 해석하지 않는다.

고정 근거는 `artifacts/local/food-mobile-four-role-r27/frozen-source.json`이다. 로컬 API 이미지의 선택 소스 4,514개와 대조했을 때 차이는 공통 UI C# 파일 3개였으며 서버·contracts 파일은 동일했다. 서버 이미지 전체를 고정 소스와 동일한 판본으로 표시하지 않고 `server/frozen-source-comparison.json`에 차이를 남긴다. 새 공통 UI는 고정 소스에서 별도로 빌드한다.
