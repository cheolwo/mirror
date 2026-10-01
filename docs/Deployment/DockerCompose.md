# Docker Compose 단일 VM 실행 준비

이 구성은 ASP.NET Core, MySQL, MongoDB를 한 VM에서 실행하고 Redis는 선택적으로 추가한다. Azure 관리형 DB나 관리형 Redis는 사용하지 않는다. 아직 서버에 배포하지 않아도 같은 Compose 파일로 로컬에서 배포 구성을 검증할 수 있다.

## 1. 비밀 설정 준비

`docker-compose.env.example`을 `.env`로 복사한 뒤 비밀번호, JWT, ISMS-P 키, KAMIS와 USDA 키를 실제 값으로 바꾼다. `.env`와 `appsettings.Local.json`은 Git 및 Docker 빌드 컨텍스트에서 제외된다.

예제는 단일 VM 준비 기준으로 `ASPNETCORE_ENVIRONMENT=Production`을 사용한다. 개발 시 시드 데이터와 상세 오류가 필요할 때만 로컬 `.env`에서 `Development`로 바꾼다.

실행 상태 저장소는 다음 두 가지다.

- `HONGDAL_TRANSIENT_STATE_PROVIDER=Redis`: 단일 VM 운영 권장값이다. `redis` Compose 프로필을 함께 실행한다. AOF 볼륨을 사용하므로 컨테이너 재생성 뒤에도 상태를 복구할 수 있다.
- `HONGDAL_TRANSIENT_STATE_PROVIDER=Memory`: Redis 없이 개발할 때 사용한다. 애플리케이션 재시작 시 기사 위치, 대기열, 추천 상태와 Push Token 등 임시 상태가 사라진다.

로컬 개발 기동은 ISMS-P 키가 없어도 상태 확인과 일반 API를 점검할 수 있도록 `HONGDAL_ISMSP_FAIL_WHEN_KEY_MISSING=false`가 기본이다. 운영 배포에서는 반드시 이 값을 `true`로 설정하고 AES-256-GCM 키와 전송 키를 비밀 저장소에서 주입한다.

기존 MySQL·MongoDB 볼륨을 이미 만든 경우에는 최초 생성 때 사용한 비밀번호를 계속 사용해야 한다. `.env`의 비밀번호와 연결 문자열의 비밀번호가 기존 볼륨과 다르면 컨테이너 환경변수만 바꿔도 DB 계정 비밀번호는 바뀌지 않는다.

게시글 번역은 기본적으로 꺼져 있다. Azure Translator 리소스를 준비한 뒤 `HONGDAL_AZURE_TRANSLATOR_KEY`, `HONGDAL_AZURE_TRANSLATOR_REGION`을 비밀 저장소에서 주입하고 `HONGDAL_COMMUNITY_TRANSLATION_ENABLED=true`로 바꾼다. 원문은 MySQL 게시글에 그대로 유지되고 `ko-KR`·`en-US` 번역은 상세 화면에서 처음 요청될 때 생성되어 `platform_community_post_translations`에 캐시된다. 글이 수정되면 원문 해시가 달라져 기존 번역은 재사용되지 않으며, 신고·분쟁 글은 기본 정책상 외부 번역 대상에서 제외된다.

## 2. 이미지 빌드

```powershell
docker compose --progress plain build app
```

Dockerfile은 복원 레이어를 캐시한 뒤 `dotnet publish` 한 번으로 컴파일한다. 완료 후 이미지 ID와 생성 시각을 확인한다.

```powershell
docker image inspect hongdal-app --format 'Id={{.Id}} Created={{.Created}}'
```

## 3. 데이터베이스 초기화

웹 서버가 시작할 때마다 여러 인스턴스에서 마이그레이션하지 않도록 기본값은 `DatabaseInitialization__RunAtStartup=false`다. 이미지 배포 전에 같은 이미지로 초기화 명령을 한 번 실행한다.

Redis를 사용하는 권장 구성은 다음과 같이 초기화한다.

```powershell
docker compose --profile redis up -d mysql mongo redis
docker compose --profile redis run --rm --no-deps app --initialize-database
```

Redis 없이 확인할 때는 `.env`의 Provider를 `Memory`로 바꾸고 `docker compose up -d mysql mongo`를 실행한다.

초기화 명령이 실패하면 웹 서버를 시작하지 말고 누락된 EF Core 마이그레이션이나 연결 정보를 먼저 수정한다.

## 4. 서버 실행과 확인

```powershell
docker compose --profile redis up -d
docker compose ps
curl.exe --fail http://localhost:8080/health/live
curl.exe --fail http://localhost:8080/health/ready
```

- `/health/live`: ASP.NET Core 프로세스가 요청을 처리할 수 있는지 확인한다.
- `/health/ready`: 기본·전통시장·농수산 MySQL DbContext의 연결과 미적용 마이그레이션, MongoDB, 선택한 실행 상태 저장소를 확인한다. Provider가 `Memory`이면 Redis 검사를 생략한다.
- 농수산물 배치는 `HONGDAL_AGRICULTURAL_FISHERIES_BATCH_ENABLED=true`일 때만 등록된다.

`docker compose ps`의 app 상태가 `healthy`가 아니면 다음 순서로 확인한다.

```powershell
docker compose logs --tail 200 app
docker compose config
```

`docker compose config` 출력에는 비밀값이 포함될 수 있으므로 외부에 그대로 공유하지 않는다.

## 5. 단일 VM 보존 범위

다음 명명된 볼륨은 `docker compose down` 후에도 유지된다.

- `mysql_data`: MySQL 데이터
- `mongo_data`: MongoDB 데이터
- `redis_data`: Redis AOF 데이터
- `app_data`, `app_uploads`, `app_community`, `app_logs`: 서버 생성 파일과 로그

볼륨까지 삭제하는 `docker compose down -v`는 데이터 초기화가 명확히 필요한 경우에만 실행한다. 컨테이너 볼륨은 백업이 아니므로 VM 외부 저장소에 MySQL `mysqldump`, MongoDB `mongodump` 결과를 정기적으로 복사해야 한다.

## 6. 네트워크와 VM 기준

MySQL, MongoDB, Redis 포트는 기본적으로 `127.0.0.1`에만 바인딩되어 외부 네트워크에 직접 노출되지 않는다. 앱의 8080 포트도 기본적으로 루프백에만 열리므로 실제 VM에서는 TLS를 종료하는 Caddy 또는 Nginx 같은 역방향 프록시를 앞에 둔다.

앱, MySQL, MongoDB, Redis를 동시에 안정적으로 실행하려면 4GB RAM 이상을 권장한다. 2GB VM에서도 개발 데이터로 시험할 수 있지만 메모리 압박이 발생할 수 있으므로 컨테이너 사용량을 먼저 측정한다.
