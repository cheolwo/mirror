# Azure 저비용 미리보기 배포

이 구성은 커뮤니티 0.0을 가장 작은 실용 Azure 리소스로 확인하기 위한 미리보기 환경이다. 목표 사양은 `Standard_B1ms` Linux VM 한 대이며, Caddy, ASP.NET Core, MySQL, MongoDB를 Docker Compose로 실행하고 Redis 대신 메모리 상태 저장소를 사용한다. 2026-07-19 배포에서는 한국 중부와 인접 지역의 B1ms 용량을 확보하지 못해, 실제로 생성 가능한 최소 후보였던 `Standard_B2als_v2`를 사용했다.

위 리소스는 과거 배포 기록이며 2026-10-04 현재 계정·구독·VM 상태를 다시 확인한 결과가 아니다. 이번 모바일 준비는 로컬 문서·설정 대조이며 Azure 생성·기동·배포 승인으로 확대하지 않는다. App Service를 선택할 때는 [별도 관리형 배포 기준](AzureAppService.md)을 따른다.

## 경계

- `SsalddelExecution:Mode=Operational`을 사용한다. Toss 비밀키와 각 외부 공급자 자격 증명이 없으면 실패하도록 두며 sample fallback을 사용하지 않는다.
- MySQL과 MongoDB 포트는 VM 외부에 공개하지 않는다.
- Caddy만 80·443 포트를 공개하고 WebApp 정적 파일과 API를 같은 출처로 제공한다.
- 게시글 공개 이미지는 같은 `Korea Central`의 Blob `community-public`, POD·운송 증빙·음성은 비공개 `platform-private`에 저장한다.
- 앱은 VM Managed Identity와 컨테이너 범위 RBAC로 Blob에 접근하며 연결 문자열과 Storage Account Key를 사용하지 않는다.
- 비밀값은 VM의 `/opt/ssalddel/.env`에만 저장하고 Git에 넣지 않는다.
- Data Protection key ring은 `app_data` 볼륨에 유지한다. `compose.yaml`의 `./secrets/ssalddel-data-protection.pfx`는 Compose 파일 디렉터리 기준이므로 `/opt/ssalddel/compose.yaml`로 배포하면 `/opt/ssalddel/secrets/ssalddel-data-protection.pfx`가 대상이다. 컨테이너 인증서 경로·읽기 권한·비밀번호와 함께 확인하고 Git에 넣지 않는다.
- B1ms 2GB는 기능 확인용 목표 최소 사양이고 과거 배포 기록은 B2als_v2 4GB다. 실제 사양·가용성을 다시 확인한 후에만 변경을 판단한다.
- 단일 VM이므로 VM 장애 시 웹·API·DB가 함께 중단된다. 관리형 DB, 백업, 모니터링을 갖춘 운영 구성으로 보지 않는다.

## 배포 순서

1. `Ssalddel` 이미지를 `ssalddel-server:azure-preview`로 빌드한다.
2. 정적 역할 선택 포털과 `Ssalddel.Web.CommunityApp`부터
   `Ssalddel.Web.WarehouseApp`까지 01~05 WebApp을 각각 Release/Production으로 게시한다.
3. VM에 `deploy/azure-vm`, WebApp 게시 파일과 Docker 이미지를 전송한다.
4. Storage Account와 공개·비공개 컨테이너를 만들고 VM Managed Identity에 각 컨테이너 범위의 `Storage Blob Data Contributor`를 부여한다.
5. `.env`의 `SSALDDEL_STORAGE_ACCOUNT_NAME`을 설정한다. 키나 연결 문자열은 넣지 않는다.
6. MySQL과 MongoDB를 먼저 시작한다.
7. 같은 서버 이미지로 `--initialize-database`를 한 번 실행한다. 최초 관리자 계정이 없다면 이 실행에만 `SSALDDEL_BOOTSTRAP_ADMIN_ENABLED=true`와 아이디·이메일·임시 강력 비밀번호를 주입하고, 성공 직후 다시 비활성화하고 비밀번호를 제거한다.
8. 앱과 Caddy를 시작하고 공개 HTTPS URL에서 `/health/live`, `/health/ready`, 게시판과 실제 이미지 첨부를 확인한다.

## Android 앱과 서버 연결 준비

기존 앱과 서버를 재사용한다. 아래 세 환경은 서로 다른 증거이며 주소·DB·계정·실행 모드를 함께 확인한다.

| 환경 | 현재 설정·근거 | 의미 |
| --- | --- | --- |
| 로컬 APK 검증 | `eng/verification/docker-compose.food-observer.yml`, Development + Simulation, loopback HTTP, 격리 MySQL/Mongo | [2026-10-04 APK 검증](../Changes/2026-10-04-mobile-workflow-readiness-r1.md)은 에뮬레이터 설치·일부 화면과 서버/DB 정상 흐름 근거. 개인 휴대폰 단독 통신·공개 HTTPS 근거는 아님 |
| 공개 모바일 현장 검증 | `compose.yaml` + `compose.food-mobile-field-test.override.yaml`, Staging + Simulation, `MobileFieldTest:Enabled=true` | 모의 계정·자료·결제 범위. 별도 호스트·DB·볼륨·비밀값을 사용하며 기존 Operational DB에 겹쳐 적용하지 않음 |
| 음식 업무 운영 준비 | `compose.yaml` + `compose.food-delivery-v30.override.yaml`, Production + Operational | 음식 업무는 켜지만 외부 결제·입금 성공이나 휴대폰 검증 완료를 보장하지 않음. 운영자 검토가 필요하면 `PlatformOperationsControl`도 명시적으로 활성화 |

기본 Compose의 `FoodDeliveryWorkflow`는 `false`다. 현장 검증 override에는 음식·화물·운영자 기능이 함께 켜져 있으므로 이를 음식 전용 운영 구성으로 부르지 않는다. 현장 검증은 전용 `ssalddel-mobile-field-test` Compose project와 `/opt/ssalddel-mobile-field-test` 루트로 배포한다. 같은 호스트에 기존 `ssalddel-preview` 컨테이너가 있으면 전용 스크립트가 중단하며 Operational DB·볼륨·비밀값을 공유하지 않는다. [네 역할 현장 검증의 범위](../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/four-role-mobile-field-test.r1.md)

### 격리 현장 검증 묶음과 복구

[묶음 생성기](../../deploy/azure-vm/package-mobile-field-test.ps1)는 이미 빌드한 `ssalddel-server.tar`와 게시한 `web.tar.gz`를 받아 `deployment.sha256`과 Staging/Simulation manifest를 만든다. `Release readiness`는 기존 mart-v35 묶음을 보존하면서 같은 서버 이미지·웹에서 `food-mobile-field-test-deployment-<run-number>`를 별도로 생성한다. 이 단계는 VM 생성·실제 배포·APK 서명·휴대폰 검증을 실행하지 않는다.

최초 설치 때만 묶음의 `compose.yaml`과 `Caddyfile`을 전용 루트에 놓고, 비공개 `.env`·PFX 및 읽기 권한을 준비한다. 이 환경의 `SSALDDEL_FIELD_ADMIN_PASSWORD`, `SSALDDEL_FIELD_ORDERER_PASSWORD`, `SSALDDEL_FIELD_DRIVER_PASSWORD`, `SSALDDEL_FIELD_RESTAURANT_PASSWORD`는 검증용 계정 비밀값이다. 공개 도메인, DB/JWT/AES·salt·PFX/Blob 접근 설정은 별도로 확인한다. 기존 운영 설정을 복사해 모의 환경으로 덮어쓰지 않는다.

```bash
bash <release-directory>/deploy-food-mobile-field-test.sh <release-directory> DEPLOY_ISOLATED_MOBILE_FIELD_TEST
```

이 명령은 checksum과 Compose 설정을 확인하고 격리 MySQL/Mongo의 준비를 기다린 후 같은 이미지의 `--initialize-database`를 한 번 실행한다. 시작 시 자동 migration은 끈다. 기본·전통시장·농수산·공공데이터 수집 네 context를 초기화한 뒤 새 앱의 준비 상태를 확인하고 Caddy를 갱신한다. 기존 설치에서는 고정된 base Compose/Caddyfile을 자동 덮어쓰지 않으며, 변경이 필요하면 별도 백업·검토한다.

이미지·웹·profile override 교체와 Caddy 기동 실패는 하나의 rollback 범위다. 기존 이미지 ID, 웹, override 및 이전 Compose 파일 목록을 사용해 복구를 시도한다. 최초 실패 시에는 격리 project만 중단하고 DB 볼륨을 삭제하지 않는다. rollback 명령 자체가 실패할 수 있으므로 이전 앱·공개 HTTPS까지 다시 확인한다. 스키마는 자동 역마이그레이션하지 않는다.

Operational 버전 profile은 같은 이미지의 migration·백업 검증을 마친 뒤 `SSALDDEL_DEPLOY_MIGRATIONS_VERIFIED=true`를 실행 환경에 명시해야 배포를 진행한다. 이 확인값은 자동 복원 증거가 아니다. [Staging 복구 도구](../../eng/staging/Invoke-StagingReadiness.ps1)는 변경 전 MySQL backup을 남기고 네 context를 migration하며, 연결 문자열의 DB·서버·포트·사용자가 backup 대상과 같은지 확인한다. MySQL과 MongoDB 모두 원본과 복구 DB가 같거나 대소문자만 다르면 외부 명령 전에 중단한다. 복구 대상 이름은 별도 `*_restore_verify`여야 한다. 복원 후 일부 테이블 조회·Redis PING/DBSIZE는 자료 전체의 업무 의미 검증을 대체하지 않는다.

현재 [Pomelo 9.0.0 잠금 이름](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/9.0.0/src/EFCore.MySql/Migrations/Internal/MySqlHistoryRepository.cs#L55)은 DB 이름에 고정 20자를 더하며, [MySQL 8.4 잠금 이름 상한](https://dev.mysql.com/doc/refman/8.4/en/locking-functions.html)은 64자다. 따라서 Staging 도구는 마이그레이션 원본 DB 이름을 44자 이하로 사전 확인한다. 기본 Compose의 `ssalddel`은 이 조건을 충족하며, 마이그레이션하지 않는 복원 대상은 별도 식별자 64자 제한을 유지한다.

[암호화 키 준비 도구](../../deploy/azure-vm/provision-preview-secrets.sh)는 AES와 salt가 모두 있으면 보존하고, 하나만 있으면 파일을 바꾸지 않고 실패한다. 두 값이 모두 없는 최초 설정에만 새 값을 생성하며 출력하지 않는다. 기존 암호문이 있는 환경에서 없어졌다면 이 도구로 재생성하지 말고 비밀 백업에서 원래 쌍을 복구한다.

### 화면·코드·DB 연결 확인

| 앱 화면 | 기존 연결 코드 | 서버 정본·확인 조건 |
| --- | --- | --- |
| 주문자 음식점 탐색·주문 상세 | `OrdererApp/MauiProgram.cs`, 공통 `SsalddelServerEndpoint`, 음식 주문 Client | 같은 서버의 음식 주문 MySQL 원장으로 접수·진행·수령 상태 확인 |
| 음식점 수신·조리·픽업 준비 | `RestaurantDeskApp/MauiProgram.cs`, `음식점주문SignalRClientService` | JWT 인증 후 `/hubs/restaurant-orders` 연결, 재연결 후 주문 원장을 다시 조회 |
| 음식 기사 배차·수행·완료 | `FDriverApp/MauiProgram.cs`, 10초 서버 재조회·위치 전송 | 음식 배달 시도/상태 원장과 허용 행동 확인. 실제 FCM 수신은 별도 미구현 범위 |
| 운영자 음식 예외·검토 | `SsalddelAdminApp/MauiProgram.cs`, 역할 제한 Client | 운영자 계정·기능 플래그·같은 주문/배달 원장 연결 확인 |

기사의 별도 음식 앱 토큰 등록·실제 Android FCM 수신은 아직 완료되지 않았다. 화물 앱 토큰으로 대체하지 않으며 전경 재조회 성공을 화면이 꺼진 동안의 배차 수신 성공으로 확대하지 않는다. [음식·화물 알림 경계](../Changes/2026-10-04-food-cargo-separation-r1.md)

### 공개 주소와 서명

`eng/release/publish-mobile-field-test.ps1`의 `-ServerBaseAddress https://<공개호스트>/`는 일곱 역할 앱의 `SsalddelServerBaseAddress` assembly metadata로 들어간다. Release 앱은 주소가 없거나 HTTP·loopback·에뮬레이터 주소이면 시작을 거절한다. `127.0.0.1`은 휴대폰 자신의 주소이므로 PC와 USB `adb reverse`를 사용하는 Debug 검증 주소를 공개 배포 주소로 재사용하지 않는다. 기존 Caddy는 호스트 루트의 `/api/*`, `/hubs/*`, `/health/*`를 `app:8080`으로 전달하므로 이 VM의 APK 주소는 공개 호스트 루트로 맞춘다.

서명에는 저장소 밖 keystore·alias와 `SSALDDEL_ANDROID_KEYSTORE_PASSWORD`, `SSALDDEL_ANDROID_KEY_PASSWORD` 환경 변수가 필요하다. 값은 `env:` 참조로 APK 전용 빌드에 전달하며 문서·manifest·로그에 넣지 않는다. 기본 역할은 `orderer`, `restaurant`, `driver`(기존 음식 기사 alias), `admin`, `shipper`, `cargo-driver`, `warehouse`다. `-AppNames`로 선택할 수 있고 AAB는 이 도구에서 생성하지 않는다. `-PlanOnly`는 계획 점검이고 실제 빌드·설치·서버 통신은 별도다. 기존 Debug 서명 앱에 다른 서명의 Release를 덮어쓸 때 생기는 설치 오류를 자동 삭제로 해결하지 않는다.

최종 `*-Signed.apk`를 `apksigner`로 확인하고 요청한 keystore alias의 인증서와 비교한다. package ID·version·Release metadata, 16KB alignment와 arm64/x64의 포함된 .NET runtime도 검사한다. 모든 요청 앱이 통과한 뒤에만 `release-manifest.json`과 `handoff/downloads/mobile/<version-versionCode>/<role>.apk`를 만든다. 사본 SHA-256은 검증한 파일과 같아야 하고 완료한 판본은 덮어쓰지 않는다. manifest의 `physicalDeviceVerified`·`serverCommunicationVerified`는 계속 `false`이며 실제 설치·통신 증거로 확대하지 않는다.

다운로드 제공은 별도 승인된 게시 단계다. 검증한 `handoff/downloads`를 게시할 웹 루트에 포함해 `web.tar.gz`를 만들면 기존 Caddy의 정적 파일 경로에서 `/downloads/mobile/<version-versionCode>/<role>.apk`로 제공할 수 있다. bundle/publisher는 자동 업로드하지 않는다. 게시 후 실제 HTTPS 파일의 SHA-256과 휴대폰 다운로드·설치를 다시 확인한다. 서명 키·원본 입력·비공개 artifact 전체를 웹 루트로 복사하지 않는다.

현재 패키징은 내부 현장 검증판이다. 공통 `MobileFieldTestBanner`는 서버 응답과 무관하게 `Simulation`을 표시하고, 주문자·음식점·운영자 Release 라우트는 검증 화면 목록으로 제한된다. 첫 공개 검증은 이에 맞는 Staging + Simulation 서버로 진행한다. Operational 서버를 연결한 것만으로 앱의 환경 표시·허용 화면이 운영판으로 바뀌지 않으며, 실제 운영판 전환에는 별도 표시·배포 구분 검토가 필요하다.

### 최초 공개 연결의 완료 조건

- 공개 DNS·공인 TLS와 Caddy의 `/health/live`, `/health/ready`, `/api`·`/hubs` 전달을 확인한다. DB 포트는 계속 비공개로 유지한다.
- 같은 서버 주소의 네 APK로 로그인·토큰 갱신, 정상 주문 1건의 접수→인지→배정→조리→픽업→전달→수령을 확인한다. 네 역할의 주문 식별자와 최종 DB 상태를 함께 남긴다.
- 음식점 SignalR 인증·단절·재연결, 기사 앱의 서버 재조회, Wi-Fi/모바일 네트워크 변경과 앱 복귀를 실제 휴대폰에서 확인한다.
- 컨테이너 재생성 후 MySQL·MongoDB·키링 자료가 유지되고 동일 보호 데이터가 읽히는지 확인한다. 명명된 볼륨은 백업이 아니므로 DB 백업·복구 시험도 별도로 둔다.
- `Memory` 임시 상태는 서버 재시작 시 사라진다. 이 저비용 구성의 한계를 기록하며 관리형 Redis 구성과 동일한 지속성을 주장하지 않는다.

실제 Azure 단계 전에 사용할 구독·재사용할 리소스 또는 새 호스트, 공개 도메인, 지역·예산·운영 시간, 검증용/운영용 DB 분리와 서명 키 준비 여부를 확정한다. 비밀값은 대화로 제출받지 않고 기존 비밀 저장소에 설정한다. 이 확인표 자체는 생성·배포·과금·실제 결제에 대한 승인이나 완료 증거가 아니다.

### WebApp만 갱신

서버 API와 DB를 바꾸지 않고 기존 Azure 미리보기의 브라우저 화면만 갱신할 때는
다음 스크립트를 사용한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File deploy/azure-vm/package-web-preview.ps1
```

이 명령은 `artifacts/local/azure-web-preview/` 아래에 JavaScript 없는 정적 역할 선택
포털과 Release/Production 01~05 역할 WebApp, `preview-build.json`,
`web-preview.tar.gz`와 SHA-256 파일을 만든다. 역할 앱은 `/roles/01/`부터
`/roles/05/`까지 서로 다른 base path를 사용한다. 작업 트리에 WebApp 또는 공유
UI·contract 변경이 있으면 공개 빌드 표시에 `working-tree`를 남긴다. scoped stylesheet
주소에는 release ID를 붙여 기존 방문 세션도 새 화면 CSS를 다시 받게 한다.

묶음과 `deploy/azure-vm/Caddyfile`을 VM으로 전송한 뒤 `deploy-web-preview.sh`에
archive, SHA-256, 배포 루트와 Caddyfile 경로를 전달한다.

```bash
sudo bash deploy-web-preview.sh \
  web-preview.tar.gz \
  <sha256> \
  /opt/ssalddel \
  Caddyfile
```

스크립트는 `/opt/ssalddel/web`을 타임스탬프 백업으로 이동하고 새 WebApp을 원자적으로
교체하며, Caddyfile도 별도 타임스탬프 백업 후 Caddy만 다시 만든다. 공개
`preview-build.json` 확인이 실패하면 새 web을 `web-failed-<timestamp>`로 보존하고
이전 web과 Caddyfile을 복구한다. API, MySQL, MongoDB, 볼륨과 `.env`는 변경하지 않는다.
역할 분리 구조와 route 경계는
[01~05 역할 분리 WebApp](../Architecture/RoleSeparatedWebApps.md)을 따른다.

### Unity 산출물 검토 WebApp 별도 갱신

Unity 촬영 산출물의 H1·H2·H3 후보 검토 화면은 일반 01~05 WebApp 묶음에
포함하지 않는다. `Ssalddel.Web.UnityReviewApp`만 게시하는 전용 명령을 사용한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File deploy/azure-vm/package-unity-review-preview.ps1
```

산출물은 `artifacts/local/azure-unity-review-preview/` 아래의
`unity-review.tar.gz`, SHA-256 파일과 `unity-review/preview-build.json`이다.
manifest는 `/unity-review/` base path, H1·H2·H3 검토 범위와
`ServerAdministratorCandidateReview` 권한 경계를 기록한다. 운영 설정의 API 주소는
`same-origin`이며 브라우저의 현재 host 루트에 있는 기존 관리자 API를 사용한다.

기존 VM을 재사용해 비용을 억제하되 원격 교체 단위는 분리한다. 다음 스크립트는
`/opt/ssalddel/web/unity-review`만 타임스탬프 백업 후 교체하고 Caddy 설정을 검증한다.
일반 `/roles/01/`~`/roles/05/` 파일과 API·MySQL·MongoDB 볼륨은 바꾸지 않는다.

```bash
sudo bash deploy-unity-review-preview.sh \
  unity-review.tar.gz \
  <sha256> \
  /opt/ssalddel \
  Caddyfile
```

검토 API나 Mongo·Blob 저장 코드가 함께 바뀐 배포는 정적 WebApp 교체 전에 새
`ssalddel-server:azure-preview` 이미지를 적재하고 app health를 확인해야 한다.
정적 배포 스크립트가 서버 이미지를 암묵적으로 바꾸지는 않는다. 공개 주소는
`https://<SSALDDEL_SITE_HOST>/unity-review/`이며 화면은 서버관리자 로그인을 요구한다.
Blob 이미지 object는 현재 공개 읽기이므로 URL 보유자는 로그인 없이 이미지를 열 수
있고, 촬영 PNG에는 개인정보·주문·인증·Console 정보를 넣지 않는다.

현재 비용 통제 운영창은 매일 한국 시간 19:00~23:00이다. 이 시간 밖에서 VM을
수동 기동하거나 운영창을 바꾸는 것은 별도 명시 승인을 받은 뒤 수행한다.

배포 전에는 CLI의 로컬 account 표시만 보지 말고 ARM subscription 상태와 실제 VM
쓰기 가능 여부를 확인한다. Free Trial 크레딧 만료나 spending limit 도달 뒤
subscription이 `Warned` 또는 `ReadOnlyDisabledSubscription`이면 VM 시작·Blob 쓰기와
배포가 차단된다. 이 경우 종량제 전환이나 spending limit 제거를 자동 수행하지 않는다.
유효한 결제 수단과 향후 사용량 청구에 대한 별도 명시 승인을 받은 뒤 Azure Portal에서
구독을 다시 활성화해야 한다. 기준은 Microsoft의
[Azure spending limit](https://learn.microsoft.com/azure/cost-management-billing/manage/spending-limit)과
[무료 계정 비용 방지](https://learn.microsoft.com/azure/cost-management-billing/manage/avoid-charges-free-account)를 따른다.

### 주문자 1.0 미리보기

비구속 공동구매 수요와 주문자 집단화까지만 확인할 때는
`compose.orderer-v10.override.yaml`을 두 번째 Compose 파일로 사용한다. 기본
`compose.yaml`은 후속 업무 플래그를 모두 닫으며, 1.0 override만
`GroupPurchaseDemandWorkflow`를 추가로 활성화한다.

GitHub `Release readiness` workflow가 생성한
`orderer-v10-deployment-<run-number>` 산출물에는 서버 image, Web 정적 파일,
Compose와 공통 rollback script가 함께 들어간다. 배포와 확인 절차는
[문화교통 1.0 배포 준비 절차](../Versions/v1.0/deployment-runbook.md)를 따른다.

### 주문자 1.5 미리보기

기존 VM의 비밀값·볼륨·보안 설정을 그대로 유지하면서 주문자 1.5 기능을 점검할 때는
`compose.orderer-v15.override.yaml`을 두 번째 Compose 파일로 함께 지정한다. 이 override는
`GroupPurchaseDemandWorkflow`와 `CustomsAndTradeDataWorkflow`만 활성화하며
`SsalddelExecution:Mode=Operational`을 사용한다. 계약·결제·신고 제출·포워더 자동
선정·외부 전송은 각 기능 플래그와 공급자 자격 증명을 추가로 통과해야 한다. 현재 미리보기 VM이 인증서 기반 Data
Protection 전환 전이라면 override의 `PersonalDataProtection:RequireCertificate=false`는
기존 key ring 호환을 위한 임시 설정이다. 인증서 배치와 기존 key ring 복호화 검증을
마친 뒤에는 기본 구성의 `true`로 복귀해야 한다.

### 국내 운송 2.0 미리보기

화주·기사 운송 페이지를 배포 상태에서 점검할 때는 기존 Compose와
`compose.orderer-v15.override.yaml` 뒤에 `compose.transport-v20.override.yaml`을 추가한다.
2.0 override는 선행 커뮤니티 기반과 `DomesticTransportWorkflow`를 활성화하고
`SsalddelExecution:Mode=Operational`을 사용한다.

페이지 책임은 `화주 작성 → 의뢰별 요약·이력·결제 상태·증빙`,
`기사 추천 목록 → 추천 상세 → 수락·거절 판단 → 현재 운송 → 상차 → 하차`로 분리한다.
추천 목록과 상세 화면은 상태를 바꾸지 않으며, 수락·거절과 상·하차 Command는 전용
화면에서만 서버 권한·선행 상태 검증을 거쳐 요청한다. 현재 운송은 조회와 다음 단계
안내만 맡고, 이전 `/driver/transport/proof` 링크도 읽기 전용 단계 선택 화면으로
동작한다. 브라우저 타이머는 만료를 표시할 뿐 자동 거절 Command를 전송하지 않는다.

운영 승인 전에는 자동 배차, 운송 계약 중개, 실결제, 운임 수취, 기사 지급과 외부
운송사 전달을 활성화하지 않는다. 2.0 화면이 공개되더라도 이 경계가 바뀌는 것은 아니다.

Blob 이전이 완료되면 Caddy와 앱에서 `app_community` 볼륨 연결을 제거한다. 롤백 확인 전에는 기존 명명된 볼륨 자체를 즉시 삭제하지 않는다.

배포 후에는 Azure Portal의 비용 분석과 무료 크레딧 잔액을 확인한다. Blob도 저장 용량·요청·외부 전송량에 따라 소액이 발생할 수 있어 완전 무료로 표현하지 않는다. 미리보기가 필요 없으면 VM을 중지하는 것만으로는 OS 디스크와 공인 IP 비용이 남을 수 있으므로, 보존할 데이터가 없다면 리소스 그룹 전체 삭제를 검토한다.
