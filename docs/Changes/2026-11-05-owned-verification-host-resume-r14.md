# 같은 격리 검증 서버와 기존 원장의 재개 r14

11/5 재개 점검에서 기존5378 서버와 전용 MySQL·Mongo·gateway 컨테이너가 모두 중지돼 있었다. 이전 ready나 PID를 현재 성공으로 쓰지 않고, 기존 컨테이너·볼륨·DB·실행본을 확인한 뒤 같은 Simulation 환경을 재개했다. 제품 코드나 설정을 바꾸는 리팩토링은 이번에 하지 않았다.

## 기존 실행을 재사용한 근거

- 같은 `mirror-usb-r74` 프로젝트의 기존 세 컨테이너 ID·볼륨·내부 네트워크·loopback13310/27021 포트와 gateway 설정을 확인했다. MySQL/Mongo를 각각1회 시작해 healthy를 관측하고 기존 DB·294개 MySQL 테이블·Mongo DB의 존재를 읽기로 확인한 뒤 기존 gateway를1회 시작했다. 컨테이너·볼륨·DB를 새로 만들거나 초기화하지 않았다.
- r6 실행본453파일의 SHA와 파일 수가 모두 같았다. 넓은8771입력 중 변경7개는 화주·기사·관리자 클라이언트/클라이언트 시험이며, 실제 `.deps.json`의 프로젝트18개 밖이었다. 기존 선택 서버·하네스·Settings·Factory·참조 프로젝트의 변경은0이다. 넓은8771개 전체의 현재 일치를 주장하지 않는다.
- 같은 exact GUID 실행 `integrated-r108:be09622360b74a42afb695cdbf27eeea`와 기존 DB `r108m_be09622360b74a42afb695cdbf27eeea`, 환경 파일·키링·성공 migration·initialized의 연결을 확인했다. 기존 성공 기록을 대조하는 재개 경로만 사용하며 신규 schema 확인 옵션·seed·migration을 실행하지 않았다.
- 예전 `ready.json`, 준비 진단·실패·초기화·migration·준비 기록은 새 r14 폴더에 원문/SHA로 보존했다. 기존 ready/진단 파일의 재시작 갱신과 과거 사본을 구분하며 예전 교체 실행기·중단 영수증은 덮어쓰지 않았다.

## 이번 실제 재개·검증

재빌드 없이 검증된 기존 `serve_existing_run.ps1`을 새 실행 기록으로1회 호출했다. 현재 관측 서버는 PID12912/부모27468, 시작 UTC `2026-11-04T23:02:46.0912530Z`다. 같은 하네스 DLL SHA `950600a732931cc1159c3992aadff8fdff87e54549f46d9a5c62f109318a5873`·서버 DLL SHA `a6280a94bb3994109f00c849f40196f0542c6f9abcfa2065c3131c46a6888d8e`·5378 리스너를 대조했다. 다음 회차에는 이 PID/상태도 다시 확인한다.

새로 쓰인 ready의 시각·동일 실행/DB·MigrationFirst·Development/Simulation·HostedService 제거·외부 HTTP/Push/자동 지급/선결제 차단·보존 재시작과 기존 schema 성공을13항목으로 확인했다. 현재 실제 GET은 live/ready·누적 참여·공개 공동운영·기존 업무 proof2개로6/6 HTTP200이었다. 약98초 유휴 뒤에는 Mongo를 사용하는 공개 공동운영 조회를 먼저 호출하고 같은6개를 다시 확인해6/6 HTTP200이었다. 준비 검사로 연결을 먼저 열어 유휴 문제를 가리지 않았다.

두 업무 proof의 SHA는 전후 및 r13과 같고 금융 행은0이다. 이전 readiness21개를 보존했고 새 관측에서도 실패를 발견하지 않았다. 이 조건에서의 재개·조회 성공이며 모든 과거503이나 앱 전체의 안정화·운영 준비 완료는 아니다. 새 시험·빌드·APK·기기 조작·로그인/토큰 갱신·업무 명령·commit/push는 실행하지 않았다. 문서 Fast/Task의 build/tests skipped는 시험 통과 수로 집계하지 않는다.

## 남은 결함과 다음 한 가지

현재 `ShipperOperationsWorkspace`는 목록을 빈값으로 시작하고 조회 성공 여부 없이 메트릭·업무 제목·출고 알림의 빈 안내를 표시한다. 첫 입고 조회가403/통신 오류로 끝나면 나머지 조회가 실행되지 않아도 기본0과 ‘알림 없음’이 남는다. r13의 실제403 관측과 현재 소스를 대응한 표시 상태 결함이며 이번에 수정한 것으로 쓰지 않는다.

다음은 이 첫 조회 실패를 정상0건과 구분하는 최소 표시 교정이다. 기존 카드·경로·재조회 동작을 유지하고 최초 미조회/오류·정상 빈 결과·명시 재조회 후 복구를 검증한다. 성공 후 일부 재조회 실패의 전체 원자 갱신은 별도다. 권한 있는 창고 데이터 조회·실제 휴대폰의 새 서버 왕복·기사 직접 로그인은 아직 확인하지 않았다.

재개 근거는 `artifacts/local/app-stability-r14/55714113d1994ea7b81bf09b6bb23056/completion.json`이다. `backend-reuse-audit.json`, `existing-container-identities.json`, `start-existing-databases.json`, `start-existing-gateway.json`, `server-launch.json`, `runtime-identity.json`, `current-runtime-contracts.json`, `runtime-before.json`/`runtime-after.json`에서 재사용 범위·시작·현재 조회·보존을 구분한다.
