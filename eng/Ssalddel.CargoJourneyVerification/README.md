# 격리 화물 HTTP·DB 여정 준비 도구

이 콘솔은 현재 `Ssalddel`의 실제 `Program`·Controller·인증·UseCase·MySQL/Mongo 저장을 `WebApplicationFactory`의 실제 Kestrel `127.0.0.1:5322`에 올린다. 기존 `5321` food observer와 컨테이너·볼륨·설정 파일을 공유하지 않는다. 서버 제품 코드, 기본 feature와 실행 모드는 변경하지 않는다.

이 도구의 `Development + Simulation` 호스트만 `DomesticTransportWorkflow`·`WarehouseFulfillmentWorkflow`를 켠다. 전용 MySQL `127.0.0.1:13308`의 `ssalddel_cargo_journey`·사용자 `cargo_journey`, Mongo `127.0.0.1:27019`의 같은 이름 DB만 허용한다. DB 컨테이너는 내부 네트워크에 두고 전용 TCP gateway만 loopback에 게시한다. Quartz·수집·지급·외부 알림의 hosted service는 제거하고 `IHttpClientFactory`의 외부 HTTP는 차단한다. 이 HTTP 필터는 모든 프로세스의 외부 네트워크를 봉쇄하는 운영 방화벽 증거가 아니다.

초기 자료는 `cargo-journey-shipper`·`cargo-journey-driver`·`cargo-journey-warehouse`·`cargo-journey-admin`의 합성 계정 4명, 기사 `1톤 카고`, 창고·입고상품·출고준비중 예정 1건이다. 창고 사용자와 HR 세부 역할 배정은 실제 DB에 저장한다. 합성 재고 9개는 가용 9/예약 0으로 준비하며 포장 근거는 명시적인 fixture다. 입고·검수·포장을 실제 앱으로 수행했다는 증거가 아니다. 운송의뢰·결제 승인·배차 수락·상하차·완료를 초기 자료로 만들지 않는다.

운송의뢰는 기존 창고 API로 같은 출고예정 ID에서 생성하고, 기존 화주 후불 승인·기사·창고 API로 진행해야 한다. 이후 모든 역할의 HTTP와 DB 증거는 `ready.json`의 같은 `runStableId`·출고예정·상품·운송의뢰 ID를 대조한다. 준비 완료는 전체 여정 완료, 장치 UI·운영 운송·실결제·은행 지급 증거가 아니다. 서버 재시작 시 기존 재고·출고·운송 상태를 초기화하지 않는다.

## 실행

재현용 실행 스크립트와 Compose·TCP gateway 설정은 저장소의 `eng/verification/cargo-journey.ps1`, `docker-compose.cargo-journey.yml`, `cargo-journey.haproxy.cfg`에 있다. 비밀 값은 Git 제외 `artifacts/local/cargo-warehouse-journey-r1/environment/.env`에만 보관한다. 루트 담당만 DB 준비·빌드·실행을 소유한다. 명령은 저장소 루트에서 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verification/cargo-journey.ps1 -Action Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verification/cargo-journey.ps1 -Action Up -SampleSuffix r2
dotnet build eng/Ssalddel.CargoJourneyVerification/Ssalddel.CargoJourneyVerification.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verification/cargo-journey.ps1 -Action Serve -SampleSuffix r2
```

첫 빌드의 package 자산이 없으면 루트 담당이 restore를 먼저 실행한다. 중앙 `Directory.Packages.props`의 `Microsoft.AspNetCore.Mvc.Testing` 10.0.9와 .NET 10 SDK를 사용한다. `Serve`는 빌드된 DLL만 실행하며 자동 build·restore를 하지 않는다. `Status`는 컨테이너의 이름·상태·포트만 조회한다. `Stop`은 이 전용 project의 컨테이너만 멈추고 기존 볼륨을 지우지 않는다.

`SampleSuffix`의 기본값은 `r2`이며 소문자 영숫자와 중간 하이픈만 허용한다. 각 sample은 `ssalddel-cargo-journey-r1_mysql_<suffix>`·`ssalddel-cargo-journey-r1_mongo_<suffix>` 전용 볼륨을 쓴다. 새 sample을 명시적으로 고를 때에는 실행 중인 화물 호스트를 먼저 종료하고 기존 `ready.json`과 HTTP/DB 결과를 별도 보관한 뒤 `-Action Up -SampleSuffix <새이름>`을 실행한다. 기존 준비 기록이 남아 있는 다른 sample 전환은 차단한다. 기존 볼륨·비밀 파일·결과를 삭제하거나 자동 초기화하지 않는다. `5321` food observer의 project·설정·볼륨에는 이 스크립트가 접근하지 않는다.

`.env`는 처음 `Prepare`에서 생성하고 재호출 시 보존한다. `CARGO_JOURNEY_ACCOUNT_PASSWORD`·DB 비밀번호·JWT·AES·hash salt·검증 접근 키는 명령행 인자로 받거나 콘솔에 출력하지 않는다. `Load`는 호출 PowerShell의 process 환경 변수만 설정한다. 제품 API의 `POST /api/v1/auth/login`은 이 계정과 기존 역할 권한 검사를 그대로 사용한다.

서버와 DB 준비 뒤 `artifacts/local/cargo-warehouse-journey-r1/ready.json`에 안전한 식별자·base URL을 쓰고 콘솔에 같은 정보를 표시한다. 실패 상세는 비밀 값을 지운 `preparation-failure.txt`에 보관한다.

## HTTP·DB 워크플로 재현

호스트가 실행 중인 상태에서 다른 터미널로 추적 대상 `eng/verification/cargo-journey-workflow.py`를 실행한다. Python 3 표준 라이브러리만 사용한다. `python`이 PATH에 없으면 명령의 실행 파일을 설치된 Python 3의 절대 경로로 바꾼다. 저장소 루트·private artifact 위치는 스크립트 파일 위치에서 결정하므로 다른 작업 폴더에서도 같은 fixture를 읽는다. 비밀은 기존 `.env`에서만 읽으며 로그인 응답·JWT·비밀 값을 출력하거나 결과에 저장하지 않는다. loopback 요청은 시스템 HTTP proxy를 사용하지 않는다. 기존 private `root/workflow.py`와 그 상태·로그는 수정하거나 성공 상태로 대신 사용하지 않는다.

```powershell
python eng/verification/cargo-journey-workflow.py prepare
python eng/verification/cargo-journey-workflow.py arrive --then handoff pickup complete
```

`prepare`는 별도로 실행하여 같은 운송을 실제 Driver 앱에서 조회하고 버튼 동작의 증거를 확보할 수 있게 한다. 이후 후반 단계는 `arrive --then handoff pickup complete`로 연속 실행하는 것을 권장한다. `--then`은 바로 다음 단계만 순서대로 허용하고 한 프로세스 안의 JWT를 메모리에서 재사용한다. 단계별 fixture·상태 검사와 HTTP 결과 파일은 각각 유지하며, 한 단계라도 실패하면 이후 단계는 실행하지 않는다. JWT를 파일에 저장하거나 실패 단계의 guard를 건너뛰지 않는다.

제품의 기본 인증 제한인 5분당 10회는 그대로 유지한다. 단계별 프로세스를 각각 실행하면 매번 네 역할이 다시 로그인하므로 HTTP 429에 도달할 수 있다. 실제 역할 앱 로그인과 별도의 증거 조회도 인증 요청을 추가한다. 호스트 재시작 후 조회 또는 인증 요청 한도에 여유가 있는 별도 확인에서 다음 명령을 사용한다.

```powershell
python eng/verification/cargo-journey-workflow.py proof
```

`prepare`는 의뢰·운송·상품 연결·출고 이동·이력이 없고 가용 재고 9/예약 0인 `출고준비중` fixture에서만 실행할 수 있다. 실제 의뢰 생성과 같은 body의 멱등 재시도, 승인 전 기사 수락·추천 차단, 잘못된 fixture·익명·역할 차단, 명시 후불 승인, 제어 추천, 기사 수락과 재시도를 확인한다. 다음 단계는 직전 단계가 성공한 상태 파일만 사용하며 `runStableId`·의뢰·운송·상품·출고예정 ID를 실제 DB 증거와 대조한다. `handoff`는 동시 두 요청과 추가 재시도 뒤 출고 이동·이력이 정확히 한 번인지 확인한다. `complete`는 기사 현재 운송 해제, 단일 의뢰·운송·상품 연결, 재고 한 번 차감, 운송 완료와 Mongo 문서 한 건을 확인한다.

상하차 희망 일시는 초 단위 UTC `Z` 문자열로 보내고 응답을 UTC 값으로 비교한다. MySQL의 timezone 없는 DateTime 응답은 이 필드의 UTC 저장 계약에 따라 UTC로 해석한다. 시간 차이를 허용하거나 snapshot 획득을 단계 성공으로 바꾸지 않는다. `proof`는 현재 증거 조회이며 실패한 단계 또는 전체 여정의 성공을 인증하지 않는다.

각 호출의 결과와 실패는 Git 제외 `artifacts/local/cargo-warehouse-journey-r1/workflow/http-<단계>-<UTC시각>-<식별자>.json`, 단계 상태는 같은 폴더의 `workflow-state.json`에 기록한다. 실패 시 종료 코드는 1이며 일부 API가 성공했더라도 해당 단계는 실패로 남는다. 로그인 HTTP 429처럼 단계 시작 전에 실패하여 단계 상태가 변경되지 않은 경우에는 인증 제한이 회복된 뒤 같은 명령을 다시 실행할 수 있다. 단계가 이미 시작되었거나 mutation이 실행·실행 여부 불명인 실패는 자동 재개하지 않는다. 이 도구는 실패 단계의 초기화·중복 완료를 하지 않는다. 원인을 검토하고 새 sample을 준비할 때 기존 준비·workflow 결과를 별도 보관한다. 사진 업로드는 명시적인 1×1 합성 PNG이며 실제 화물 사진·카메라 촬영·장치 UI 검증이 아니다.

## 검증 전용 제어

아래 두 경로는 이 하네스의 시작 middleware에만 있으며 제품 Controller에는 추가되지 않는다. loopback 발신과 `.env`의 `CARGO_JOURNEY_ACCESS_KEY`를 담은 `x-cargo-journey-key` 헤더를 모두 요구한다.

- `POST /verification/cargo/offer`, JSON `{ "requestId": "warehouse-outbound-910004" }`: 실제 API에서 만들어 seed 출고예정·상품에 연결된 의뢰만 허용한다. 실제 후불 승인 또는 결제 완료 뒤 기존 `I배차대기원장전환Service.추천시작Async`로 합성 기사에게 추천한다. 임의 원장 배차 상태 변경을 하지 않는다. 기사 후보 선택은 `ControlledSyntheticDriverSelection`이며 생산 후보 추천 알고리즘 검증은 아니다.
- `GET /verification/cargo/database-proof`: 해당 fixture의 현재 재고·출고·의뢰·운송 상태, 동일 의뢰/상품 연결 개수, 출고 재고 이동·이력 개수와 실제 Mongo 운송 문서 개수·상태를 조회한다. 개인정보 연락처·JWT·비밀은 반환하지 않는다.

성공·재시도·차단·앱 재시작의 실제 HTTP 결과와 별도의 장치 UI를 함께 기록해야 한다. 도구 소스나 초기 자료 준비만으로 여정 성공을 선언하지 않는다.
