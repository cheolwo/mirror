# 모바일 업무 검증 환경 연결 r1

화물 기사·화주·창고 앱도 기존 주문자·음식점·음식 기사·관리자 앱과 같은 서버 주소 계약을 사용한다. APK를 만드는 것과 설치·로그인·업무 정상 흐름을 확인하는 것은 별도 결과다. 지도 SDK·운임·배차·권한·DB 정책은 이 문서의 변경 범위에 포함하지 않는다.

## 페이지 책임과 환경 경계

| 항목 | 기록 |
| --- | --- |
| 주 사용자·페이지/소스 | 화물 기사 `DriverApp`, 화주 `SsalddelApp`, 창고 `WarehouseManagerApp`의 기존 진입 화면과 로그인·업무 화면. 새로운 페이지·route는 추가하지 않는다. |
| 대상·진입 문맥 | 설치한 앱 판본, 그 APK에 결속한 서버, 로그인 계정, 기존 주문·운송의뢰·입출고 원장. |
| 한 문장 목적 | 사용자가 준비된 서버에 연결된 앱에서 자기 역할의 업무 상태와 허용 행동을 확인한다. |
| 완료 결과·주 행동 | 빌드·설치는 준비다. 실제 로그인 후 같은 원장의 현재 상태를 읽고 명시적 행동 뒤 서버를 재조회해야 해당 업무 단계의 검증이 완료된다. |
| 기본·보조 정보 | 기존 상태·오류·복귀 안내를 유지한다. 서버 주소·SDK·포트 같은 구현 정보는 제품 업무 카드에 추가하지 않는다. |
| 독립 업무·제외 정보 | 인증과 업무를 분리한다. Release 서명·배포, 서버 준비, DB 초기 자료, 지도 인증은 별도 책임이다. |
| 진입·실패·복귀 | 주소 누락·부적절한 Release 주소는 시작 단계에서 실패한다. 연결 실패를 샘플 성공으로 바꾸거나 다른 서버에 자동 연결하지 않는다. 기존 업무 입력·세션 복구 규칙은 유지한다. |
| 코드·API·DB | 각 `csproj`의 조립체 메타데이터 → `MauiProgram` → 공통 `SsalddelServerEndpoint` → 기존 인증/HTTP Client → 서버 API·원장. 저장·허용 전이의 최종 권위는 서버다. DB·공개 계약·패키지 ID는 변경하지 않는다. |
| 책임 판정·검증 | 환경 연결만 보완하며 화면을 통합하거나 업무 의미를 바꾸지 않는다. 회귀시험·컴파일·APK·실제 장치 화면·HTTP/DB 왕복·운영 효과는 각각 기록한다. 이번 문서 작성 자체는 실행 성공 근거가 아니다. |

페이지 기준은 [단일 책임 원칙](../../Architecture/WholeRoadmapPagePrinciple.md), 업무 상태의 권위는 [업무 실행 책임 모델](../../Architecture/BusinessWorkflowResponsibilityModel.md)을 따른다.

## 화물 기사 업무 라우트의 인증 복귀 책임

| 항목 | 기록 |
| --- | --- |
| 주 사용자·페이지/소스 | 화물 기사, 기존 `/driver/**` 업무 라우트와 독립 `/login`. `DriverRouteView`와 앱 내부 인증 상태 조립만 보완한다. |
| 대상·진입 문맥 | 현재 앱의 인증 세션과 진입한 기사 업무 경로. 비밀번호·토큰·다른 계정의 업무 정보는 복귀 URL에 넣지 않는다. |
| 한 문장 목적 | 기사가 확인된 로그인 세션으로 자기 업무 화면을 열고, 세션 종료 후 로그인하여 요청한 업무로 돌아간다. |
| 완료 결과·상태별 주 행동 | 인증 복원 중에는 업무를 만들지 않는다. 복원 실패는 다시 확인, 미인증은 독립 로그인으로 이동, 인증 확인 뒤에는 기존 업무 조회를 시작한다. 로그인 성공만으로 업무 명령을 자동 실행하지 않는다. |
| 기본·보조 정보 | 인증 확인·로그인 필요·복원 실패 안내만 표시한다. 계정 입력은 기존 인증 전용 Layout의 `/login`이 소유한다. |
| 독립 업무·제외 정보 | 공개 콘텐츠·로그인 라우트, 운임·배차·정산·서버 권한·기능 활성화는 변경하지 않는다. |
| 진입·실패·복귀 | 미인증 업무 진입과 세션 만료는 안전한 내부 기사 경로를 `returnUrl`로 전달한다. 일반 서버 장애는 세션을 종료하지 않고 기존 업무 화면의 재시도를 유지한다. 계정·세션 판본이 바뀌면 이전 업무 컴포넌트를 폐기하고 다시 조회한다. 같은 세션의 토큰 갱신은 업무 컴포넌트를 초기화하지 않는다. |
| 코드·API·DB | `DriverRouteView` → `DriverRouteAuthenticationState` → 기존 `IAuthSession.RestoreAsync/Changed` → 기존 인증 Client. 업무 상태·허용 행동의 정본은 기존 서버 API와 원장이다. 세션 만료 판정과 보안 저장소는 기존 구현을 유지한다. |
| 책임 판정·검증 | 인증과 업무를 배타적으로 구성한다. 상태·경로·지연 응답·세션 교체 회귀시험, APK 컴파일, 실제 로그인 복귀 화면은 각각 기록한다. 이번 책임 카드 작성은 빌드·기기 실행·화물 업무 완료의 근거가 아니다. |

변경 전 원본·SHA-256과 좁은 경로 목록은 Git 제외 `artifacts/local/mobile-workflow-readiness-r1/cargo-auth-route-r1/`에 보존한다. 기존 홈은 인증 오류를 일반 조회 실패로 표시했으므로, 미인증에서는 홈을 포함한 업무 컴포넌트를 만들지 않고 독립 로그인으로 복귀하도록 한다. 정상 로그인 후 홈을 새로 만들면 기존 계정의 준비된 홈 조회를 재사용하지 않는다.

### 로그인 화면의 정보 배치 보완

`DriverApp/Components/Pages/Login.razor`의 독립 인증 목적·입력·명령·업무 복귀 조건은 유지한다. 제목과 상태 안내는 기존 공통 로그인 패널에서 한 번만 보여주고, 로그인 후 돌아갈 업무 안내와 복귀·로그아웃 버튼은 그 패널 안에 둔다. 사용자 판단에 필요하지 않은 개발 계정 활성화 Chip과 소셜 로그인 구현 설명은 화면에서 제외한다. 공유 UI·CSS·로그인/소셜 명령은 수정하지 않는다.

실제 Android 검토에서 로그인 화면 폭404px에 내부 스크롤 폭541px이었고, 줄바꿈되지 않는 개발 안내 Chip의 글자 폭477px이 원인이었다. 이 좁은 배치 변경은 새 기능 시험을 추가하지 않고 같은 APK의 실제 DOM 가로 넘침·입력·복귀 표시로 검증한다. 검증 결과는 통합 담당 기록에 결속하며 소스 변경만으로 가로 넘침 해소를 완료했다고 보고하지 않는다. 변경 전 원본과 해시는 Git 제외 `artifacts/local/mobile-workflow-readiness-r1/cargo-login-layout-r1/`에 보존한다.

## 변경한 코드

화물 기사 [프로젝트](../../../DriverApp/DriverApp.csproj)·[시작 설정](../../../DriverApp/MauiProgram.cs), 화주 [프로젝트](../../../SsalddelApp/SsalddelApp.csproj)·[시작 설정](../../../SsalddelApp/MauiProgram.cs), 창고 [프로젝트](../../../WarehouseManagerApp/WarehouseManagerApp.csproj)·[시작 설정](../../../WarehouseManagerApp/MauiProgram.cs)에 기존 네 앱의 메타데이터 소비 방식을 연결했다.

`-p:SsalddelServerBaseAddress=...`가 있으면 `AssemblyMetadataAttribute`에 주소가 들어가고 시작 시 가장 먼저 소비된다. 없는 경우 기존 `SsalddelEndpoints:ServerBaseAddress`, 그 다음 `SsalddelApiBaseAddress` 설정을 사용한다. 공통 구현은 [SsalddelApiEndpoint.cs](../../../Ssalddel.Ui.Common/Areas/App/Services/SsalddelApiEndpoint.cs)에 있다.

- `Debug` 또는 명시적인 `SsalddelUsbFieldTest=true` 빌드는 로컬 HTTP 시험 주소를 허용한다. 설정 없는 Android Debug의 기존 공통 기본 주소는 `http://10.0.2.2:5104/`다. 격리 서버의 5321/5322로 자동 바꾸지 않는다.
- 일반 Release는 명시적인 공개 HTTPS 주소가 필요하다. HTTP·loopback·Android 에뮬레이터 주소는 시작을 거절한다. 사설 주소를 공용 휴대폰의 접근 가능한 서버로 판정하거나 인증서·방화벽·접근 가능성까지 검증하는 함수는 아니다.
- 화주·창고의 Android 네트워크 정책은 기본 HTTP를 거절하고 `127.0.0.1`, `localhost`, `10.0.2.2`의 정확한 호스트만 예외로 허용한다. 하위 도메인·LAN IP·외부 HTTP는 예외에 넣지 않았다. 기존 권한과 `allowBackup` 값은 유지했다. 화물 기사 앱의 기존 네트워크 정책·기기 권한은 이번 변경에서 유지한다.
- 화주·창고의 기존 Android `https://localhost:7117/` 고정 fallback은 모바일 공통 계약으로 바뀌었다. Debug Windows의 공통 기본 주소는 같은 `https://localhost:7117/`이며 일반 Release에는 공개 HTTPS 설정이 필요하다.

## 일곱 역할 앱의 패키지와 진입

| 역할 | 프로젝트 | 유지한 Android 패키지 | 진입 |
| --- | --- | --- | --- |
| 주문자 | `OrdererApp` | `com.ssalddel.ordererapp` | 기존 `/` 주문·로그인 화면 |
| 음식점 | `RestaurantDeskApp` | `com.ssalddel.restaurantdeskapp` | 기존 `/` 주문 수신·로그인 화면 |
| 음식 기사 | `FDriverApp` | `kr.ssalddel.fdriver` | 기존 네이티브 로그인·Shell 수행 화면 |
| 관리자 | `SsalddelAdminApp` | `com.ssalddel.adminapp` | 기존 `/`와 `/login` |
| 화물 기사 | `DriverApp` | `kr.hongdal.driver` | 기존 `/` 업무·로그인 화면 |
| 화주 | `SsalddelApp` | `com.companyname.ssalddelapp` | 기존 `/community`에서 화주 업무 진입 |
| 창고 | `WarehouseManagerApp` | `com.companyname.warehousemanagerapp` | 기존 `/warehouse` |

`SellerApp`은 별도 판매자 전문 앱이다. 화주 업무가 있는 `SsalddelApp`과 패키지·목적을 혼동하지 않는다.

## USB·에뮬레이터 시험과 독립 휴대폰

| 환경 | 필요한 주소·연결 | 별도 확인 |
| --- | --- | --- |
| PC 격리 서버 + USB/에뮬레이터 reverse | 빌드에 `http://127.0.0.1:5321/` 또는 `:5322/`를 명시하고 해당 장치의 같은 포트만 `adb reverse`로 연결 | 장치 serial·ABI, 서버/DB·Simulation 모드·전용 계정, 설치와 로그인 |
| Android 에뮬레이터에서 PC 직접 연결 | `http://10.0.2.2:<포트>/` 명시 | 해당 PC 포트·서버가 실제 준비되었는지 |
| PC 없는 독립 휴대폰 | 접근 가능한 HTTPS 서버와 인증서, 일반 Release 서명 | 모바일 네트워크에서 API 연결·인증·권한·업무 재조회 |

`localhost`와 `127.0.0.1`은 휴대폰 자신을 뜻한다. reverse 없는 독립 휴대폰에서 PC 서버를 가리키지 않는다. APK에 비밀번호·토큰·서명 비밀을 넣지 않는다. 기존 앱과 서명이 달라 설치가 실패하면 자동 삭제·데이터 초기화·강제 다운그레이드를 하지 않는다.

기존 [음식 observer](../../../eng/verification/food-observer.ps1)·[역할 Client headless E2E](../../../eng/Ssalddel.RoleAppHeadlessE2E/README.md)는 Development/Simulation의 전용 5321 환경을 제공한다. [화물·창고 검증 도구](../../../eng/Ssalddel.CargoJourneyVerification/README.md)는 별도 DB와 5322 환경을 쓴다. 두 표본의 원장·계정·검증 ID를 합치지 않으며 앱 주소를 임의 전환하지 않는다. 관리자는 각 표본에 맞춘 별도 주소 판본으로 확인한다.

개발 서명 APK 명령은 [기존 USB 준비 절차](../../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/restaurant-usb-apk-preparation.r1.md)의 `SignAndroidPackage`, `EmbedAssembliesIntoApk=true`, `AndroidPackageFormats=apk`를 재사용한다. 서명·공개 HTTPS가 필요한 [공개 현장 패키징 스크립트](../../../eng/release/publish-mobile-field-test.ps1)는 현재 음식 네 역할만 지원한다. 지원 범위 밖 세 앱을 완료한 것으로 표시하지 않는다.

## 검증과 현재 준비 상태

[MobileWorkflowEnvironmentTests](../../../Ssalddel.Tests/Clients/MobileWorkflowEnvironmentTests.cs)는 일곱 앱의 빌드 주소→메타데이터→시작 소비 연결, 실제 공통 resolver의 주소 우선순위·USB 주소·Release 거절·설정 보존, 두 앱의 로컬 전용 HTTP 정책을 검증한다. 소스 구성 검사는 실제 Android 설치·네트워크 왕복을 대신하지 않는다.

2026-10-04 조사 시 SDK API35/36·build-tools36.0.0과 `mirror_mobile_review` API36/x86_64 AVD가 있었다. 당시 `adb devices -l` 목록과 5321/5322/5104/7117 listen은 비어 있었다. 개인 휴대폰 설치·서버 시작·새 APK 빌드는 이 환경 연결 구현에서 수행하지 않았다. 10월2일 이전 APK와 화면 근거는 10월4일 변경의 검증 결과로 재사용하지 않는다.

변경 전 파일·경로·SHA-256은 Git 제외 `artifacts/local/mobile-workflow-readiness-r1/environment/`에 보존한다. 최종 빌드·시험·설치·상태별 실제 UI 결과는 통합 담당의 별도 실행 기록에 결속한다.
