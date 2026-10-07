# Azure 서버와 휴대폰 APK 연결 준비 조사

2026-10-04, 1차 종합 조사. 현재 작업 트리의 모바일 앱 7개, 공통 인증·통신 코드, 역할별 API·업무 상태, Azure VM/App Service 설정, 패키징·CI·데이터 초기화·복구 도구와 기존 실행 기록을 대조했다.

**앱과 서버의 연결 코드는 대부분 갖춰져 있지만, 현재 확인된 APK는 PC 서버에 연결하는 개발판이다. 휴대폰에서 내려받아 Azure 서버와 독립적으로 통신하는 Release판의 검증은 아직 없다.** 먼저 음식 배달 네 역할을 격리된 공개 HTTPS 검증 서버에 연결하고, 같은 주문의 처리와 통신 단절·앱 복귀를 확인하는 순서가 적절하다. 화물 업무는 별도 기능 설정과 시나리오로 확인한다.

조사 기준은 `dev/mirror-integration`, HEAD `b1b7cfc39b5183ed56246080c141d63965bf2704` 위의 기존 미커밋 변경을 포함한 소스다. HEAD만 배포하면 이번 조사와 같은 판본이라고 볼 수 없다. 이번에는 제품 코드·설정·키·DB·Azure 리소스를 변경하거나 새 빌드·시험·설치·배포를 실행하지 않았다. 전 페이지의 실기기 조작과 모든 API의 실행 검증을 완료한 보안 감사는 아니다.

## 현재 준비 수준

| 단계 | 갖춰진 부분 | 현재 판정과 남은 확인 |
| --- | --- | --- |
| 서버 주소 연결 | 7개 앱 모두 `SsalddelServerBaseAddress` 빌드 메타데이터와 공통 주소 해소 사용 | 코드 준비. 공개 DNS·공인 인증서·실제 접속은 미확인 |
| Android 패키지 | 7개 Debug APK의 내장 런타임·ARM64/x86_64·서명·정렬·해시 검사 기록 | PC 서버·API36 에뮬레이터 확인. 공개 Release·개인 휴대폰 확인은 없음 |
| 다운로드·설치·업데이트 | 음식 네 앱의 외부 keystore 서명 및 판본·주소·해시 manifest 생성 도구 | 다운로드 게시 근거 없음. 화물 3개 앱은 해당 도구 대상 밖. 최종 서명 APK 선택·검사 필요 |
| 공개 서버 | VM의 Caddy HTTPS 프록시와 Compose, App Service 설정 문서 | 배포 기반 있음. 현재 Azure 리소스·실행 이미지·공개 health는 미확인 |
| 로그인·권한 | JWT·역할 제한·SecureStorage·여러 앱의 갱신/계정 전환 보호 | 로컬 일부 확인. 주문자·창고의 실행 중 토큰 만료 복구 차이와 실기기 장시간 검증 남음 |
| 음식 업무 | 주문→인지→배정→조리→픽업→전달→수령 및 재조리 인계 API·원장 | 로컬 서버/DB와 음식점·기사 APK 일부 실행 확인. 네 역할 APK의 동일 주문 전체 완주는 없음 |
| 화물 업무 | 음식과 별도 API·배차·상하차·증빙·역할 권한 경계 | 화면·기능별 기존 근거만 있음. Azure/실휴대폰 운송 전체 완료는 미확인 |
| 알림·위치·복귀 | 음식점 SignalR·재조회, 음식 기사 전경 조회/위치, 화물 기사 FCM/위치 서비스 코드 | 음식 앱 전용 원격 푸시 미연결. 잠금·절전·권한 거절·강제 종료는 실기기 확인 필요 |
| 데이터·복구·관측 | MySQL/MongoDB·영속 볼륨·키링·로그·health·초기화·복구 도구 | 대상 범위 불일치와 배포/복구 도구 공백 있음. Azure 백업 복원·이전 판본 복귀 근거 없음 |

공통 주소 검사는 Release의 주소 누락·HTTP·loopback을 차단한다. 이 검사가 DNS·TLS 신뢰·서버 가용성을 확인하는 것은 아니다. `127.0.0.1`은 휴대폰 자신의 주소이므로 기존 `http://127.0.0.1:5361/` APK를 그대로 웹에 게시해도 Azure로 연결되지 않는다. [공통 주소 코드](../../Ssalddel.Ui.Common/Areas/App/Services/SsalddelApiEndpoint.cs#L57)

### 실제 Azure 조회 결과

읽기 전용 `az account list`에는 기본 계정 한 개가 캐시상 `Enabled`로 나타났다. 그러나 실제 `az resource list`는 `AADSTS50132 / invalid_grant / InteractionRequired`로 실패했다. 로그인 세션이 만료 또는 무효인 상태여서 현재 구독·VM/App Service·DB·호스트를 확인하지 못했다.

**현재 Azure 상태는 미확인이다.** 계정 캐시의 Enabled를 정상 구독·현재 쓰기 권한으로 해석하지 않고, 조회 실패를 서버나 리소스가 없다는 근거로도 사용하지 않는다. 재로그인·구독 변경·VM 시작·배포는 하지 않았다. 이전 미리보기 배포 설명은 현재 리소스 상태와 별개다.

## 역할별 준비와 부족한 부분

| 역할 / 프로젝트 | 현재 연결·복구 | 먼저 보완하거나 검증할 부분 |
| --- | --- | --- |
| 음식 주문자 / `OrdererApp` | 공개 주소 주입, 보안 세션 저장, 주문 제출의 요청 ID·입력 저장, 정본 조회 후 명시 재제출 | 실행 중 access token 만료 때 갱신 경로. 실제 휴대폰 주문→수령 확인, 앱 종료 후 제출 결과 복구 |
| 음식점 / `RestaurantDeskApp` | 인증 갱신, 주문 hub·재연결 조회, 상태별 조리/준비 행동, 재조리 차수 표시 | 진행 변경의 미확정 요청은 프로세스 메모리. 종료 후 원래 ID·결과 재결속, 백그라운드 신규 주문 알림 |
| 음식 기사 / `FDriverApp` | 인증 갱신, 현재 수행·완료 내역, 전경 위치·재조회, 준비 전 픽업 차단 | 음식 전용 원격 푸시, 잠금/절전 복귀, 일부 미확정 도착·의사 요청의 종료 후 복구 |
| 운영자 / `SsalddelAdminApp` | 서버관리자 인증·갱신, 관제·할증 조회 | 홈·관제에 화물 API가 결합됨. 음식 ON/화물 OFF에서도 음식 주문 추적·중단 검토를 독립 처리해야 함 |
| 화물 기사 / `DriverApp` | 별도 운송 API, 인증 갱신, FCM·위치 foreground service·증빙 코드 | 공통 Release 패키징 도구 대상 밖. 광역 cleartext 설정 정리, 실제 잠금·위치·수신·증빙 업로드 검증 |
| 화주 / `SsalddelApp` | 별도 운송 의뢰·인증 갱신·요청/계정 판본 보호 | 공통 Release 패키징 대상 밖. 앱 복귀·연결 변경·의뢰 제출/증빙/정산의 실기기 연결 확인 |
| 창고 / `WarehouseManagerApp` | 기능·인증·역할·계정 확인 후 보호 조회와 업무 진입 | 공통 Release 패키징 대상 밖. 실행 중 인증 만료 복구. 상단 Android 상태 표시줄과 제목·메뉴 겹침이 기존 캡처에 남음. 비활성 안내 확인은 창고 업무 완료와 별개 |

일곱 앱의 Android 대상은 `net10.0-android`다. 현재 기록된 APK는 ARM64와 x86_64를 포함하며, API36 x86_64 AVD에서 확인했다. 실제 대상 휴대폰의 Android 버전·ABI·저장 공간과 Release의 trimming/내장 런타임 결과는 별도 확인한다. 기본 최소 API는 24, 화물 기사 앱은 23이며 32bit ARM 기기 지원은 확인되지 않았다. 현재 package ID와 서명 관계를 보존하고, 업데이트마다 versionCode를 올려야 한다.

## 개인 휴대폰의 첫 테스트 전에 필요한 작업

### 1. 공개 HTTPS 검증 서버와 배포 판본을 연결한다

VM Compose와 App Service는 별도 배포 방식이다. 기존 [VM 기준](AzureLowCostVm.md)과 [App Service 기준](AzureAppService.md) 중 사용할 호스트·DB·비밀 경로·공개 원점을 하나로 확정한다. 현재 리소스 조회가 복구된 뒤 기존 환경 재사용 여부를 판단한다.

최초 음식 앱 테스트는 **격리 Staging + Simulation**을 기준으로 한다. 현재 [현장 검증 override](../../deploy/azure-vm/compose.food-mobile-field-test.override.yaml#L4)는 이미 그 환경을 지정하지만, 음식·화물·운영자 기능을 함께 켠다. 따라서 음식 전용 분리 검증을 통과한 설정으로 볼 수 없다. 공통 [배포 스크립트](../../deploy/azure-vm/deploy-preview-profile.sh#L18)는 이 profile을 허용하지 않고, [Release CI](../../.github/workflows/release-readiness.yml#L207)는 `mart-v35` 묶음만 생성한다. 격리 현장 검증용 묶음·사전점검·배포 경로의 연결이 필요하다.

기존 Operational profile을 사용해 이 공백을 우회하지 않는다. 서버는 Operational에서 Toss secret이 없으면 시작을 거절하고, 현장 검증 앱의 공통 배너는 Simulation을 표시한다. 운영 서버 연결만으로 검증판이 운영판으로 바뀌지 않는다. [실행 모드 검증](../../Ssalddel/Program.cs#L146), [기존 환경 기준](AzureLowCostVm.md#android-앱과-서버-연결-준비)

완료 근거는 정확한 소스/이미지·override·DB 대상과 공개 `/health/live`, `/health/ready`, 로그인·역할 API·음식점 hub의 실제 HTTPS 연결이다. 첫 테스트에서 모든 업무 기능을 동시에 켤 필요는 없다.

### 2. 최종 서명 APK를 만들고 다운로드·업데이트까지 확인한다

[현재 패키징 도구](../../eng/release/publish-mobile-field-test.ps1#L18)는 음식 주문자·음식점·음식 기사·운영자 네 앱만 지원한다. 공개 주소·저장소 밖 keystore·비밀번호 환경 변수·판본·해시를 연결하는 기반은 있다. 처음에는 필요한 네 앱부터 검증하고, 화물 테스트 전에 화물 기사·화주·창고의 같은 Release 경로를 마련한다.

현재 도구는 `apk;aab`와 `env:` 서명 비밀번호를 함께 넘긴다. Microsoft 문서는 AAB에서 `env:`를 지원하지 않는다고 명시하므로 공식 제한과 충돌하는 경로다. 이번에 publish를 실행하지 않았으므로 실제 실패로 단정하지 않는다. 휴대폰 직접 설치 목적은 APK 단독 산출부터 확인하는 것이 적절하다. 또한 모든 `.apk`/`.aab`를 모아 해시만 기록하므로 unsigned 산출물을 포함할 수 있고, 최종 서명·정렬·내장 런타임 검사가 배포 게이트에 연결돼 있지 않다. 기존 Debug APK의 수동 검사 성공은 이 Release 검사를 대신하지 않는다. [도구 121~155행](../../eng/release/publish-mobile-field-test.ps1#L121), [Microsoft 공식 패키징·서명 기준](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0)

APK 다운로드/업데이트 게시 경로는 조사한 제품·배포 도구에서 확인하지 못했다. 개인 테스트에는 검증된 최종 APK를 HTTPS 파일 링크로 제공하고, 역할·판본·해시·설치/업데이트 안내를 연결하면 된다. 별도 앱스토어나 자동 업데이트 기능을 먼저 새로 만들 필요는 없다. 서명 인증서가 다른 Debug/Release의 동일 ID 업데이트를 자동 앱 삭제로 해결하지 않는다. APK의 웹/서버 배포는 [Microsoft의 직접 배포 방식](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-ad-hoc?view=net-maui-10.0)을 따른다.

### 3. 음식 운영자 화면을 화물 기능에서 독립시킨다

[관제 서비스](../../SsalddelAdminApp/Services/AdminOperationsService.cs#L18)는 화물 운송·기사 GET와 음식 할증 GET를 `Task.WhenAll`로 결합한다. 화물 기능을 끄면 그 실패가 음식 관제 전체를 막는다. 홈도 DomesticTransportWorkflow가 필요한 관리자 대시보드를 호출한다. 음식 기능과 화물 기능을 분리한 서버 배포에서 바로 드러나는 코드 공백이다.

음식 주문의 [추적·중단 검토 API](../../Ssalddel/Controllers/Admin/Food/음식주문운영추적Controller.cs#L18)는 이미 있다. 이 API를 모바일의 독립 음식 관제로 연결하고, 음식 ON/화물 OFF에서 홈·음식 목록·상세·중단 검토가 열리며 화물 API 실패가 전파되지 않는지 확인한다. 서버관리자 권한과 기존 업무 상태 검증은 유지한다.

### 4. 같은 주문을 네 역할 APK로 끝까지 처리한다

주문자 APK의 주문 접수 → 음식점 APK의 인지 → 기사 배정 → 조리/준비 → 기사 APK의 픽업/전달 → 주문자 APK의 수령 → 운영자 APK의 최종 조회를 **같은 주문 식별자·Azure DB 상태**로 결속한다. Simulation 배정과 정산을 실제 자동 배차·실결제·입금으로 보고하지 않는다.

한 휴대폰에 역할 APK를 각각 설치하고 역할 계정으로 전환하는 방식으로도 단계별 검증을 시작할 수 있다. 여러 역할이 동시에 알림을 받는 상황과 백그라운드 수신은 이후 별도로 확인한다. 사용자 화면은 현재 상태의 주 행동만 제공하고, GET 재조회는 업무를 자동 수락·픽업·전달하지 않아야 한다.

## 장시간 사용과 업무 복구의 다음 순서

| 항목 | 현재 확인한 조건 | 보완·검증 완료 조건 |
| --- | --- | --- |
| 주문자·창고 인증 갱신 | 공통 세션은 첫 복원 결과를 캐시하고 `IsAuthenticated`는 현재 만료시각을 재판정하지 않음. 두 앱은 복원 결과가 RefreshRequired일 때 갱신하며, 업무 토큰 제공에는 401 갱신 wrapper가 없음 | 실행 중 만료·동시 요청·갱신 응답 유실·계정 전환에서 원래 요청/입력을 보존하며 인증을 복구. 자동 업무 POST 추가 없음 |
| 음식점·기사 미확정 요청 | 음식점 진행 변경 Dictionary, 기사 도착/수신의사 등의 pending 필드는 메모리. 주문자 제출은 SecureStorage를 사용하므로 같은 상태가 아님 | 응답 유실 직후 강제 종료→재실행에서 정본을 먼저 조회하고 원래 owner·요청 ID·입력·revision에 재결속. 명시 재제출·로그아웃 정리 확인 |
| 음식 앱 신규 알림 | 음식점은 SignalR·재개 조회, 음식 기사는 전경 재조회·로컬 알림. 음식 AppKey의 별도 토큰 등록/Android FCM 수신은 없음 | 전용 설치 토큰→잠금/백그라운드 수신→알림 클릭→정본 조회. 화물 토큰 대체와 자동 업무 수락 없음 |
| 위치·권한·절전 | 음식 기사는 foreground 위치, 화물 기사는 foreground service 코드. 실제 단말의 잠금·권한 변경·절전은 미검증 | 수신·위치 권한 거절/철회와 절전·앱 복귀를 실제 휴대폰에서 확인. 필요한 추적 범위를 업무별로 명시 |
| 서버 재시작 | VM 기본은 `TransientState=Memory`. 기사 위치·큐·일부 토큰/추천 상태가 process-local이며 MySQL 업무 원장은 별도 | 컨테이너 재생성 뒤 로그인·운행·위치·추천·원장 재조회. 필요하면 기존 Redis adapter 선택·TLS·복구 확인. 원장 소실과 임시 상태 소실을 혼동하지 않음 |
| Android 설정·업데이트 | 화물 기사 manifest의 광역 cleartext, 일부 앱의 allowBackup=true. 공개 주소 resolver는 정상 Release HTTP를 차단함 | debug 예외와 HTTPS 정책 정렬, 보안 저장소의 백업/재설치 복구 정책, 같은 인증서·증가 versionCode 업데이트 확인 |

인증 근거: [공통 세션](../../Ssalddel.Client.Infrastructure/Security/ClientAuthSession.cs#L36), [주문자 복원](../../OrdererApp/Services/OrdererSessionService.cs#L15), [창고 복원](../../WarehouseManagerApp/ViewModels/Warehouse/창고로그인ViewModel.cs#L69). 미확정 요청 근거: [음식점 진행 서비스](../../RestaurantDeskApp/Services/음식점주문DeskService.cs#L16), [기사 예외 요청](../../FDriverApp/PageModels/MainPageModel.FoodExceptions.cs#L13). 임시 상태 근거: [VM Compose](../../deploy/azure-vm/compose.yaml#L61), [저장소 선택](../../Ssalddel/Extensions/ServiceCollectionExtensions.TransientState.cs#L23).

창고의 상단 겹침은 [기존 화면 검토](../Changes/2026-10-04-mobile-workflow-readiness-r1.md#L48)에 명시돼 있다. 가로 넘침 0과 세로 안전 영역은 다른 문제다. 휴대폰에서는 상태 표시줄·키보드·뒤로 가기·앱 복귀와 처리 중 중복 누름을 역할별 대표 화면에서 확인한다. 새로운 UI 디자인보다 현재 업무 화면의 정상 배치와 상태 전이가 우선이다.

### 개인정보 열람과 로그

기사 완료 상세의 **본인만·72시간 열람·no-store**는 이미 구현돼 있다. 이 기간은 고객/주문 상세를 기사에게 제공하는 제한이며 DB·정산·백업·행위 로그의 자동 삭제 기간이 아니다. 이를 Azure에 올리는 것만으로 저장소 파기까지 구현됐다고 볼 수 없다. [열람 정책](../../Ssalddel/Application/Driver/Food/FoodDeliveryCompletedDetailAccessPolicy.cs#L5), [본인·만료 재검사](../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs#L17)

행위 로그에는 이메일·전화 마스킹이 있으나, `/api/v1` 요청의 raw query와 전체 URL을 metadata에 저장하고 예외 메시지도 길이만 줄여 저장한다. query/예외에 주소·연락처 등 민감값이 들어갈 경우 저장될 수 있는 조건이다. 실제 유출을 관측한 결과나 `/hubs` 토큰 저장의 증거로 확대하지 않는다. 실제 고객 자료를 사용하기 전에 허용 query만 남기는 방식·민감값 제거·안전한 오류 코드와 로그/백업 보관 범위를 보완한다. TraceId·HTTP 상태·업무 ID는 진단에 유지한다. [행위 로그 middleware](../../Ssalddel/Middleware/사용자행위로그Middleware.cs#L74), [로그 저장](../../Ssalddel/Services/Audit/사용자행위로그Service.cs#L29)

## 배포·복구 도구에서 발견한 구체적인 공백

아래는 실제 장애를 재현한 결과가 아니라 소스의 조건·호출 순서를 대조한 결과다. 개인 테스트를 위해 Redis·다중 인스턴스 등 모든 운영 설비를 먼저 만들 필요는 없지만, 해당 복구·키 생성·배포 도구를 사용하기 전에 그 도구의 공백은 보완해야 한다.

| 대상 | 현재 공백과 조건 | 먼저 보완할 경계 |
| --- | --- | --- |
| DB 복구 도구 | source/restore DB 이름의 비동일을 검사하지 않음. 원본도 `_restore_verify`로 끝나고 대상에 같은 이름을 입력하면 MySQL DROP 및 Mongo `--drop` 경로에 진입 가능 | 외부 쓰기 명령 전에 원본과 대상의 분리·허용 대상 확인. 잘못된 입력에서 쓰기 호출 0 |
| 비밀 초기 생성 | AES 키·hash salt 중 하나만 있으면 두 값을 새로 append함. 기존 정상 키를 교체할 수 있음 | 둘 다 없을 때만 초기 생성. 부분 설정이면 기존 값을 보존하고 중단 |
| 배포 rollback | 새 override를 먼저 덮어쓰고 실패 시 예전 image/web만 복원. 복구에도 새 override 사용 | image/web/override를 같은 판본으로 복구. config·health·Caddy 실패도 동일 복구 경계 |
| DB 초기화·health·복구 범위 | 실제 initializer는 4 DbContext, readiness는 3개, staging migration은 main 1개. App Service 문서는 아직 3개로 설명 | 활성 저장 영역 기준으로 초기화·health·복구·문구 일치. schema 변경 전 백업과 보호 데이터 재읽기 확인 |

직접 근거: [복구 대상 검사/쓰기](../../eng/staging/Invoke-StagingReadiness.ps1#L36), [비밀 생성](../../deploy/azure-vm/provision-preview-secrets.sh#L13), [override 교체/rollback](../../deploy/azure-vm/deploy-preview-profile.sh#L50), [4개 초기화](../../Ssalddel/Startup/DatabaseCompatibilityInitializer.cs#L47), [3개 readiness](../../Ssalddel/Infrastructure/HealthChecks/PersistenceReadinessHealthCheck.cs#L48), [main staging migration](../../eng/staging/Invoke-StagingReadiness.ps1#L81).

현재 4개는 `SsalddelContext`, `TraditionalMarketDbContext`, `AgriculturalFisheriesDbContext`, `PublicDataIngestionDbContext`다. readiness에서 마지막 context가 빠져 있다. 별도 영속 볼륨이 있다는 사실과 migration/백업 복구의 전체 범위 검증을 구분한다.

## 실운영 전에 별도로 완료할 항목

| 항목 | 현재 기반과 제한 | 운영 전 판단 |
| --- | --- | --- |
| 키·파일·DB 지속성 | VM named volume, PFX 보호 파일 키링, AES/hash 설정과 Blob 코드가 있음 | 현재 host의 권한·백업·재배포 후 동일 자료 복호화·복원은 미확인. DB뿐 아니라 키링/PFX/보호 키/업로드 저장소를 함께 보존 |
| 감시·복구 | stdout·trace·live/ready, staging 복구 도구가 있음 | 민감 query/오류 제거, Azure 로그 수집·알림·별도 복구 대상의 주문/이력/보호 데이터 판독·복구 시간 근거 필요 |
| 배치·확장 | Quartz RAM scheduler와 웹 프로세스 내부 BackgroundService, 기본 SignalR | 첫 검증은 단일 인스턴스. 확장/slot 전에 배치 중복·hub 일관성·worker/cluster 경계를 해결 |
| 실제 돈의 흐름 | Simulation/FakePG, 배달료·정산 원장과 재시도 기반 | 실제 PG·당일 송금·은행 입금 완료는 별도이며 개인 APK 통신 테스트의 선행 요건으로 확대하지 않음 |
| 전체 검증 게이트 | 직전 Task는 전체 빌드 통과, 6,812/6,819·기존 실패 7개 동일 | 전체 게이트 미통과. metadata·명명·공식 재료 UI·route 분류 실패를 별도 마감해야 함 |

App Service를 선택하면 `/home` 지속성·PFX 파일 배치·포트·HTTPS Only를 실제 설정한다. Linux 사용자 지정 컨테이너의 영속 저장은 기본 비활성이며, 비밀값 참조만으로 PFX 파일이 생성되지는 않는다. [Microsoft 컨테이너 저장 기준](https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container#use-persistent-shared-storage)

상시 배치가 필요한 App Service는 지원 요금제의 Always On을 확인한다. 꺼진 상태의 idle unload와 단일 활성 인스턴스의 health 감시는 상시 처리·즉시 장애 우회를 보장하지 않는다. 첫 검증에서 인스턴스를 늘리면 현재 배치/SignalR 한계가 추가된다. [Always On 기준](https://learn.microsoft.com/en-us/azure/app-service/configure-common#configure-general-settings), [단일 인스턴스 health 기준](https://learn.microsoft.com/en-us/azure/app-service/monitor-instances-health-check#what-happens-if-my-app-runs-on-a-single-instance)

지도는 사용자 요청에 따라 후속 작업으로 유지한다. 기존 지도 인증/타일 미확인은 남아 있지만 지도 키 변경을 일반 인증·주문·업무 흐름 검증의 필수 작업으로 확대하지 않는다.

## 확인된 실행 기록의 정확한 범위

- [모바일 업무 준비](../Changes/2026-10-04-mobile-workflow-readiness-r1.md): 일곱 Debug APK·API36 에뮬레이터와 격리 서버/DB의 음식 정상 여정. 일곱 역할의 모든 업무를 APK로 끝까지 처리한 결과는 아니다.
- [재조리·모바일 인계](../Changes/2026-10-04-food-recooking-mobile-r1.md): 격리 서버/DB의 재조리 차수 2·수령 확인·이력 12개, 음식점 APK의 준비 완료→기사 APK 픽업/전달. 주문자의 수령 확인과 관리자의 최종 조회는 Role Client HTTP다.
- 최근 두 APK는 앞선 일곱 중 음식점/음식 기사만 다시 만든 판본이다. 나머지 다섯 앱이 최신 후속 소스와 같은 빌드라고 자동 판정하지 않는다.
- 직전 전체 시험 7개 실패는 기준 실행과 이름·메시지·전체 스택까지 동일하고 새 실패 0이라는 기록이 있다. 이번 조사에서 시험을 다시 실행하지 않았다.

원시 근거는 Git 제외 `artifacts/local/mobile-workflow-readiness-r1/`, `artifacts/local/food-recooking-mobile-r1/`, 이번 조사 `artifacts/local/azure-mobile-readiness-audit-20261004/`에 보존한다. 공개 문서에는 키·토큰·계정/구독 식별자·복구 데이터 원문을 복사하지 않는다.

### 남은 전체 시험 7개

| 시험 | 기록된 실패 범위 |
| --- | --- |
| `CommonAndAdminDomainControllerActions_UseKoreanOrApprovedTechnicalPrefixes` | 공공데이터 가격 Card 조회 action 접두사 |
| `RoleAppControllers_HaveAudienceAndBusinessCapability` — Driver | 기사 주거공동체 World 관점 업무 분류 |
| 같은 시험 — Orderer | 주문자 주거공동체 World 관점 업무 분류 |
| 같은 시험 — Shipper | 농장 생산자 World 관점 audience 불일치 |
| `업무실행책임모델은_HIOPS와_OS를_호환용어로만_유지한다` | 기준 문서의 명명 금지 문구 |
| `공식재료화면은_재료이름과_좁은폭동작영역을_실제값으로연결한다` | 좁은 화면 행동 영역의 CSS 계약 |
| `통합_WebApp의_모든_컴파일된_라우트는_capability_규칙으로_분류된다` | `/community/map-application-chooser` 분류 누락 |

현재 `Ssalddel.Tests` 결과와 비교 JSON에서 확인했다. 이 실패들은 실제 APK 로그인·결제 실패를 관측한 결과와 다르지만 전체 Release 검증이 통과했다는 주장도 허용하지 않는다. 시험 삭제나 assertion 약화로 해결할 항목은 아니다.

## 다음 개발 순서 제안

1. **음식 전용 운영자 홈·관제와 격리 현장 검증 배포 경로를 먼저 연결한다.** 기존 API·Compose·패키징을 재사용한다. 배포 도구를 사용하기 전에 override 복구와 키 생성 경계를 고친다.
2. **공개 주소를 넣은 음식 네 역할 Release APK를 검사하고 다운로드·실휴대폰 동일 주문을 검증한다.** APK 단독 서명 경로부터 확인하며 Wi-Fi와 모바일 데이터 모두 사용한다.
3. **인증 만료·응답 유실·앱 강제 종료·서버 재시작을 처리한다.** 주문자/창고 갱신 차이와 음식점/기사 pending 영속 복구부터 좁게 보완한다.
4. **음식 전용 원격 알림을 연결하고 화물 세 앱의 별도 현장 검증을 진행한다.** 전경 조회와 잠금 수신을 따로 확인한다.
5. **백업/복구·개인정보 로그·전체 시험 실패를 마감한 뒤 실운영 전환을 판단한다.** 실제 결제/입금·확장·상시 운영은 별도 단계다.

다음 실행 때 필요한 설정은 사용할 Azure 구독/호스트, 공개 도메인, 검증용 DB 분리, 운영 시간, 저장소 밖 서명 키의 준비 여부다. 비밀값을 대화나 문서로 전달할 필요는 없다. 이번 보고서는 조사와 보완 순서이며 실제 배포·과금·실결제·입금의 완료 기록이 아니다.
