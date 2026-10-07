# Ssalddel Azure App Service 배포 준비

이 문서는 `Ssalddel` API를 Azure App Service의 Linux 사용자 지정 컨테이너로 운영할 때의 기준이다. 데이터 계층은 Azure Database for MySQL Flexible Server, Azure Managed Redis, MongoDB 호환 관리형 서비스(Azure Cosmos DB for MongoDB 또는 MongoDB Atlas)를 전제로 한다.

2026-10-04 현재 준비 기준이며 실제 Azure 배포·공개 HTTPS·개인 휴대폰 통신의 완료 기록이 아니다. 기존 단일 VM의 Compose 배포는 [Azure 저비용 미리보기](AzureLowCostVm.md)의 별도 구성을 따른다. VM Compose를 App Service 설정으로 그대로 가져오지 않는다.

## 현재 코드에 반영된 안전장치

- 로컬 비밀 설정인 `appsettings.Local.json`은 Docker 빌드 컨텍스트와 `dotnet publish` 산출물에서 제외한다.
- 컨테이너는 비루트 사용자로 실행하며 HTTP 포트는 `8080` 하나만 사용한다.
- 운영 환경은 시작 시 자동 마이그레이션을 하지 않는다. 같은 컨테이너 이미지를 `--initialize-database` 인수로 한 번만 실행해 기본·전통시장·농수산·공공데이터 수집의 네 MySQL DbContext 마이그레이션을 적용한다.
- `/health/live`는 프로세스 생존 여부를, `/health/ready`는 같은 네 MySQL DbContext의 연결·미적용 마이그레이션과 MongoDB 연결을 확인한다. Redis 검사는 `TransientState:Provider=Redis`일 때만 포함한다.
- Redis는 최초 연결 실패로 프로세스가 즉시 종료되지 않도록 재연결 가능한 설정을 사용한다.
- 컨테이너 로그는 기본적으로 표준 출력에 기록한다. 파일 로그는 경로를 명시한 경우에만 사용한다.

## App Service 설정

비밀값을 이미지나 `appsettings*.json`에 넣지 않는다. App Service의 시스템 할당 관리 ID를 켜고 Key Vault 접근 권한을 부여한 뒤, 아래 App Settings를 Key Vault 참조 또는 일반 환경 변수로 제공한다. App Service에서는 `:` 대신 `__`를 쓴다.

| App Setting | 용도 | 예시 또는 주의사항 |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | 실행 환경 | `Production` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Azure 프록시의 원래 HTTPS 정보를 ASP.NET Core에 전달 | `true` |
| `WEBSITES_PORT` | 기존 사용자 지정 컨테이너의 Azure 요청 전달 포트 | `8080`; sidecar 방식은 아래 기준 적용 |
| `AllowedHosts` | 허용 호스트 | `<app>.azurewebsites.net;api.example.com` |
| `ConnectionStrings__DefaultConnection` | MySQL | TLS와 서버 인증서 검증을 포함한 연결 문자열 사용 |
| `TransientState__Provider` | 임시 상태 저장소 선택 | 이 관리형 구성은 `Redis`; 기본값 `Memory`를 명시적으로 변경 |
| `Redis__ConnectionString` | Redis | TLS 포트, 암호/토큰, `ssl=true` 사용 |
| `MongoDb__ConnectionString` | MongoDB | 관리형 서비스의 TLS 연결 문자열 사용 |
| `MongoDb__Database` | MongoDB 데이터베이스 이름 | 운영 DB 이름 |
| `Jwt__SecretKey` | JWT 서명 키 | Key Vault 참조 권장 |
| `IsmsPProtectedData__Aes256GcmKeyBase64`, `IsmsPProtectedData__HashSalt` | 보호 데이터 암호화·해시 | 기존 자료와 동일한 키를 비밀 저장소에서 제공; 재배포 때 임의 재생성하지 않음 |
| `PersonalDataProtection__ApplicationName` | 키링 격리 이름 | 같은 환경·재배포에서 고정 |
| `PersonalDataProtection__KeyRingPath` | 기존 파일 키링 저장 위치 | 영속 `/home` 또는 명시적으로 마운트한 저장소 경로 |
| `PersonalDataProtection__RequireCertificate` | 키링 암호화 | `true`; 인증서 누락을 숨기기 위해 끄지 않음 |
| `PersonalDataProtection__CertificatePath`, `PersonalDataProtection__CertificatePassword` | 키링 보호 인증서 | 컨테이너에서 읽을 수 있는 비공개 PFX 파일 경로와 Key Vault 비밀번호 참조 |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | Linux 사용자 지정 컨테이너의 `/home` 영속 저장 | `/home` 키링을 쓸 때 `true`; 파일 접근 권한도 확인 |
| `DatabaseInitialization__RunAtStartup` | 앱 시작 시 마이그레이션 | 운영에서는 반드시 `false` |
| `DatabaseInitialization__FailOnError` | 마이그레이션 실패 처리 | `true` |
| `SsalddelLogging__FilePath` | 선택적 파일 로그 | 필요할 때만 `/home/LogFiles/ssalddel-.log` |

프로젝트에서 사용하는 `IsmsPProtectedData`, OAuth, 결제, 공공데이터 및 외부 API 키도 모두 같은 방식으로 Key Vault에 보관한다. Key Vault 참조 문법과 관리 ID 권한 설정은 [App Service Key Vault references](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)를 따른다.

기존 Docker 방식과 sidecar 방식의 설정을 구분한다. 기존 방식은 `WEBSITES_PORT=8080`을 명시해 앱의 HTTP 수신 포트와 맞춘다. sidecar 방식은 이 App Setting이 적용되지 않으므로 외부 요청을 받는 주 컨테이너(`isMain=true`)의 대상 포트를 `8080`에 맞춘다. [사용자 지정 컨테이너 포트](https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container#configure-port-number), [sidecar 설정 차이](https://learn.microsoft.com/en-us/azure/app-service/configure-sidecar#what-are-the-differences-for-sidecar-enabled-custom-containers)

현재 `AddPersonalDataProtection`은 파일 시스템 키링과 `LoadPkcs12FromFile`을 사용한다. Key Vault 비밀번호 참조만으로 PFX 파일이 생성되거나 파일 키링이 Blob 저장소로 바뀌지 않는다. 인증서의 안전한 배치·읽기 권한과 영속 경로를 별도로 준비해야 하며, 기본 설정은 인증서 경로가 없으면 서버 시작을 거절한다. [Linux 컨테이너 영속 저장](https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container#use-persistent-shared-storage)

App Service의 **HTTPS Only**를 켜고, Health Check 경로를 `/health/ready`로 설정한다. 여러 활성 인스턴스에서는 연속 실패한 인스턴스를 라우팅에서 제외할 수 있지만, 단일 활성 인스턴스는 서비스 전체 중단을 피하기 위해 제외하지 않는다. 따라서 현재 1개 인스턴스 기준을 자동 장애 우회로 설명하지 않는다. [App Service Health check](https://learn.microsoft.com/en-us/azure/app-service/monitor-instances-health-check#what-happens-if-my-app-runs-on-a-single-instance), [ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)

## Android 앱과 음식 배달 검증 환경

APK의 공개 서버 주소·서명·역할 화면→Client→서버/DB 연결 및 `Staging + Simulation`과 `Production + Operational`의 구분은 [모바일 서버 연결 준비](AzureLowCostVm.md#android-앱과-서버-연결-준비)를 따른다. App Service에서도 음식 업무의 `FoodDeliveryWorkflow`와 운영자 검토의 `PlatformOperationsControl`을 필요한 범위에서 명시해야 한다. 기본 설정은 음식 업무를 닫으며, 실제 결제 공급자 자격 증명이 없는 `Operational`을 모의 결제 성공 환경으로 취급하지 않는다.

음식점은 `/hubs/restaurant-orders`의 인증된 SignalR에 연결하고 재연결 후 정본을 다시 조회한다. 음식 기사는 현재 10초 재조회와 위치 전송을 사용하므로 모든 기사 업무에 SignalR이 필수라고 설명하지 않는다. Linux App Service의 WebSocket 지원·요금제 연결 한도와 실제 음식점 hub 인증·재연결을 별도로 검증한다. 현재 단일 인스턴스에서 시작하며 확장 시 hub 연결의 인스턴스 일관성도 다시 판단한다. [Linux WebSocket 기준](https://learn.microsoft.com/en-us/troubleshoot/azure/app-service/faqs-app-service-linux-new#are-websockets-supported)

## 데이터 서비스 연결 기준

### MySQL

- Azure Database for MySQL Flexible Server의 TLS를 유지하고 연결 문자열에 최소 `SslMode=Required`를 둔다. 가능한 경우 CA 인증서 검증까지 적용한다.
- App Service와 MySQL을 VNet 통합 및 Private Endpoint로 연결하고 공개 네트워크 접근을 제한한다.
- 서버 버전과 코드의 Pomelo 공급자 버전 호환성을 실제 Azure 서버에서 검증한다.

Azure의 TLS 요구사항과 연결 예시는 [Connect with TLS/SSL](https://learn.microsoft.com/en-us/azure/mysql/flexible-server/security-tls-how-to-connect)에 있다.

### Redis

- 신규 구축은 Azure Cache for Redis 대신 Azure Managed Redis를 우선 검토한다.
- TLS 연결만 허용하고 접근 키 또는 Microsoft Entra 인증을 Key Vault/App Settings로 주입한다.
- Redis는 캐시 손실을 견딜 수 있게 사용한다. Data Protection 키 저장소로도 쓸 경우에는 데이터 지속성과 백업 정책을 별도로 켠다.

### MongoDB

- Azure Cosmos DB for MongoDB 또는 Atlas에서 제공한 인증·TLS 연결 문자열을 사용한다.
- 계정의 방화벽/Private Endpoint와 App Service VNet 통합을 맞춘다.
- Cosmos DB를 선택하면 현재 MongoDB 드라이버와 대상 API 버전, 인덱스 및 쿼리 호환성을 사전 부하 테스트한다. 연결 예시는 [Connect a MongoDB application to Azure Cosmos DB](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/connect-account)를 참고한다.

## 배포 순서

1. 같은 커밋에서 컨테이너 이미지를 한 번 빌드하여 Azure Container Registry에 올린다.
2. 운영 비밀값과 네트워크에 접근할 수 있는 일회성 배포 작업에서 같은 이미지를 `dotnet Ssalddel.dll --initialize-database`로 실행한다.
3. 작업이 성공하지 않으면 배포를 중단한다. 이 명령은 기본, 전통시장, 농수산, 공공데이터 수집 DbContext 마이그레이션을 모두 적용하고 오류 시 실패 코드로 종료한다. 적용 전에 DB 백업을 남기고 격리 대상의 복원 시험을 확인한다. 이미지 롤백을 DB 스키마 롤백으로 취급하지 않는다.
4. App Service 슬롯에 이미지를 배포하고 `/health/live`, `/health/ready`가 모두 `200`인지 확인한다. 같은 운영 DB에 쓰는 두 슬롯에서 Operational 배치를 동시에 실행하지 않도록 배치 격리·비활성화 방법을 먼저 확정한다.
5. 준비 상태가 확인된 뒤 슬롯을 교환하거나 트래픽을 전환한다.

여러 앱 인스턴스가 동시에 시작되며 마이그레이션하는 방식은 잠금 경합과 부분 실패 위험이 있으므로 사용하지 않는다.

## 수평 확장 전에 남은 운영 항목

현재 Quartz는 RAM 저장소와 비클러스터 모드이고 여러 `BackgroundService`도 웹 프로세스 안에서 실행된다. 따라서 그대로 여러 App Service 인스턴스로 확장하면 동일 작업이 중복 실행될 수 있다. 다음 중 하나를 완료하기 전에는 인스턴스를 1개로 유지한다.

배차·만료 등 웹 프로세스의 배치가 요청이 없는 동안에도 실행되어야 한다. 선택한 요금제에서 `Always On`을 지원하는지 확인하고 켠다. 기본 꺼짐 상태에서 유휴 앱이 언로드되는 동작을 상시 배차 운영으로 해석하지 않는다. [App Service 일반 설정](https://learn.microsoft.com/en-us/azure/app-service/configure-common#configure-general-settings)

- 배경 작업을 별도 Worker/WebJob/Function으로 분리하고 웹 앱에서는 비활성화한다.
- Quartz를 영속 저장소와 클러스터 모드로 전환하고, 나머지 작업에도 분산 잠금 또는 멱등성을 보장한다.

또한 다음 항목은 운영 전 반드시 결정한다.

- 현재 파일 키링은 영속 경로와 PFX로 보존한다. Blob/Key Vault 또는 Redis 키링 제공자로 변경하려면 해당 제공자 연결 코드를 별도로 구현·검증한다. 파일을 잃으면 인스턴스 교체 때 보호 데이터 복호화가 실패할 수 있다. [Data Protection key storage providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)
- `community/posts`, `App_Data` 등 상대 경로 파일을 Azure Blob Storage로 옮기거나 App Service의 영속 `/home` 저장소로 명시적으로 매핑한다. 컨테이너의 일반 파일 시스템은 재시작·교체 때 보존을 기대하지 않는다. [Configure a custom container for Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container)
- Application Insights/OpenTelemetry를 연결하고 요청 실패율, `/health/ready`, DB 연결 실패, 배경 작업 실패, Redis 지연 시간을 경보로 설정한다. [Monitor App Service](https://learn.microsoft.com/en-us/azure/app-service/monitor-app-service)

## 배포 직전 확인표

- [ ] `dotnet publish -c Release` 산출물에 `appsettings.Local.json`, 인증서, 서비스 계정 JSON이 없다.
- [ ] 모든 비밀값이 Key Vault 참조 또는 App Settings에 있고 저장소/이미지에 없다.
- [ ] MySQL, Redis, MongoDB가 TLS 및 사설 네트워크로 연결된다.
- [ ] `--initialize-database` 작업이 성공했다.
- [ ] `/health/live`와 `/health/ready`가 `200`이다.
- [ ] HTTPS Only, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, 제한된 `AllowedHosts`가 설정됐다.
- [ ] 선택한 컨테이너 방식의 대상 포트가 앱의 HTTP `8080`과 일치한다.
- [ ] 파일 키링·PFX는 재시작/이미지 교체 후에도 유지되고 비루트 앱 사용자가 읽고 쓸 수 있다.
- [ ] 음식점 hub 인증·재연결과 앱의 같은 서버 재조회가 공개 HTTPS에서 동작한다.
- [ ] `Always On`, 1개 인스턴스, 슬롯별 배치 실행 경계가 확인됐다.
- [ ] 수평 확장 시 배경 작업 중복 실행과 Data Protection 키 공유가 해결됐다.
- [ ] 업로드·생성 파일이 영속 저장소에 기록된다.
