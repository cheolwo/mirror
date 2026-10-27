# r108 실제 MySQL/Mongo 로컬 업무 검증

현재 제품의 Controller/UseCase/원장을 같은 실행의 전용 GUID 데이터베이스에 연결하는 하네스다. 기존 r67 합성 자료/제어필터/경로 어댑터를 별도 복사하고 기존 cargo-journey factory/seed를 참조한다. 기존 r67/r106 데이터베이스·키링·계정·설정은 바꾸지 않는다.

- `run.ps1 -Action Prepare`는 기존 r74 격리 MySQL/Mongo 서버에 새로운 검증 DB만 준비한다. 새 PFX/운영 계정·외부 서버를 만들지 않는다. 검증 계정·JWT/AES/해시/제어키와 키링은 해당 실행만 소유한다.
- `run.ps1 -Action Serve -OutputRoot <준비된 artifacts/local/integrated-workflow-r108/...>`는 빌드하지 않는다. root가 조율한 빌드 후에만 실행한다.
- 최초 실행의 `EnsureCreated`는 현재 EF 모델의 신규 DB 검증이다. 운영 migration 검증·적용 증거가 아니다. 재시작은 기존 완료 원장을 다시 seed하지 않는다.
- 전체 HostedService를 제거한다. 실제 FCM·결제·송금·외부 경로 API·메일·클라우드 저장소를 호출하지 않는다. 추천은 제어된 단발 product 후보 엔진 호출이며 무인 자동 운영 증거가 아니다.
- 사진은 명시적인 합성 PNG의 비공개 로컬 저장·HTTP 업로드 계약 검증이며 실물 촬영·기기 카메라 증거가 아니다. 주소/GPS/유가/경로/실사용자 동의도 합성 fixture이다.
- 보수·재원·수령·지급 상태는 각각 조회하며 업무 완료를 실수납·실지급 완료로 바꾸지 않는다.

## 명시적 새 DB migration 시험과 준비 진단

기본 `Prepare`/`Serve`는 종전의 `CurrentModel` 방식이다. 네 context의 실제 migration chain을 확인하려면 root가 `run.ps1 -Action Prepare -SchemaMode MigrationFirst`로 **새 GUID 실행**을 만든다. 준비 결과의 `outputRoot`만 사용해 같은 소스의 하네스를 격리 빌드한 뒤 `run.ps1 -Action Serve -OutputRoot <새 실행> -ConfirmFreshEmptySchema`로 최초 실행한다. 이미 만든 current-model 실행을 이 모드로 바꾸는 옵션은 없다.

신규 MigrationFirst의 DB 이름은 `r108m_` + 전체 32자리 GUID다. 현재 Pomelo 9.0.0은 `__{databaseName}_EFMigrationsLock`으로 서버 잠금을 잡으므로 DB 38자·잠금 57자로 MySQL의 64자 제한을 지킨다. GUID를 자르거나 잠금을 우회하지 않는다. CurrentModel은 기존 `mirror_workflow_r108_` + GUID를 유지한다. 두 방식 모두 artifact 폴더의 전체 GUID와 `integrated-r108:<GUID>`·준비 기록·실제 loopback/포트/DB/사용자가 일치해야 한다. 짧은 DB 이름은 MigrationFirst에서만 허용한다. 이전 긴 이름의 MigrationFirst 실패 실행은 `InspectSchema`로만 읽으며 Serve·재시도·이름 변경·초기화하지 않는다. 이전 DB·기록은 보존한다.

적용 전에 실행 경로·DB 이름·준비 기록의 GUID 결속과 schema의 테이블/뷰·routine·event가 모두 비었는지 검사한다. `schema-migration-attempt.json`의 `Started`를 먼저 고정한 뒤 main → 전통시장 → 농수산 → 공공수집 순서로 제품 context의 `MigrateAsync`를 호출한다. EF가 실제 성공한 migration의 이력을 기록하며 가짜 history나 준비 검사 우회는 사용하지 않는다. 첫 실패에서 context·예외 종류·MySQL 수치 코드/SQLSTATE·적용/남은 개수를 남긴다. 원시 예외 메시지·SQL·연결문자열·사용자 입력은 진단에 복사하지 않는다. `FirstPendingBefore`/`FirstPendingAfter`는 관측 시점의 첫 남은 항목이며 실패한 SQL의 migration ID라고 확정하지 않는다.

`Started` 또는 `Failed` 기록이 남은 실행은 자동 재시도·초기화·재사용하지 않는다. 성공한 같은 실행의 재시작에서는 실제 pending이 모두 0인지 다시 확인하고 schema·키링·기존 seed를 보존한다. 새 migration이 추가돼도 기존 성공 DB에 자동 적용하지 않는다. 기존 seed의 `EnsureCreated` 호출은 migration이 모두 성공한 **뒤** 실행되며, 테이블이 존재하므로 새 schema를 만드는 경로로 쓰지 않는다. 제품 seed가 필요로 하는 current model과 실제 migration schema가 다르면 실패를 보존하며 제품/이력 소스를 임의로 고치지 않는다.

`run.ps1 -Action InspectSchema -OutputRoot <기존 실행>`은 현재 빌드로 잠시 loopback 호스트를 열어 네 context의 실제 applied/pending과 `HealthCheckService`를 읽고 종료한다. port 5378이 사용 중이면 기존 호스트를 보존하고 거절한다. 이 동작은 seed·migration·원장 업무를 실행하지 않으며 `schema-inspection.json`과 `readiness-diagnostic.json`만 새로 남긴다. 준비 검사 진단에는 상태·태그·pending 정수·해당 실행 Mongo 일치 여부와 안전 오류 종류/수치만 기록한다. 일반 `Serve`의 `ready.json`에도 동일 상태를 포함한다. migration 실패 뒤에는 `readiness-after-schema-failure.json`을 남긴다.

`Listening`은 소켓 상태이고 `healthStatus`는 실제 의존성 검사 결과다. 격리 migration 성공·ready 200과 운영 migration 준비·실제 앱 업무 완주·기기 확인을 각각 구분한다. 이 기능은 실행 준비이며 현재 migration chain의 성공 자체를 뜻하지 않는다. DB 생성·빌드·실행·중지는 root만 조율하고 실패 DB와 과거 current-model DB도 그대로 보존한다.

일반 회원 수행의 실제 경로 관문은 r46과 같이 명시적 합성 제공자 응답으로 분기만 검증한다. 고정된 회원 픽업/전달 합성 좌표와 `traavoidcar`만 허용하고 `IsActual=true`를 제공하는 테스트 adapter이다. 실제 경로/GPS 확인은 항상 false로 보고하며 제품의 Actual guard는 변경하지 않는다. 첫 r67 경로 adapter(`IsActual=false`)로 수락이 차단된 초기 결과도 보존한다.

2026-10-25 화물 후보 검토의 대기 기사 전체 일정 관문도 같은 명시적 합성 제공자 경계에서 검증한다. `workflow.py`가 게시하는 합성 운행 위치/상차 `(37.588, 127.085)`에서 같은 상차점 또는 선언된 하차 `(37.590, 127.087)`로 가는 두 조합과 `trafast`만 해당한다. 같은 점은 0km/0분, 상차→하차는 합성 2.5km/2분이며 출처는 `R108DeclaredCargoProviderGuardEmulated_NotActualRoad`다. 역방향·다른 좌표·옵션에는 실제 경로 확인을 부여하지 않는다. `실제경로여부=true`는 이 호스트의 제공자 응답 관문을 연습하기 위한 입력이며 실제 도로·GPS·운영 증거가 아니다. 앞선 `IdleSchedulePickupRouteUnverified`/공개배차 전환 실패 원장과 결과는 보존하며 그 원장을 강제로 다시 추천시키지 않는다.

합성 소형 상자는 팔레트 미사용 `0개`, 바닥 회전·적층 가능을 신규 접수 입력에 명시한다. 창고 재위탁의 규격 확인 PUT도 같은 조건을 명시한다. 이는 누락값을 실측으로 추정하는 정책이 아니라 이 검증 사례의 선언 입력이다. 팔레트 수가 NULL이었던 과거 요청은 구간 적재 근거가 부족하여 수락이 거절된 기록으로 보존하고, 불명확한 이전 수락 POST를 재전송하거나 그 원장의 조건을 소급 수정하지 않는다. 기사 실차·장비 측정이나 실제 적재 승인을 뜻하지 않는다.

`/verification/r108/notifications/send`는 실제 제품의 추천 Outbox 처리와 지역 가짜 전송기만 호출한다. Google·기기 전달을 성공으로 표시하지 않는다. `/verification/r108/acceptance/recover`는 현재 수락이 있는 원장에 제품 수락 후속 복구를 두 번 요청해 알림 Outbox가 중복 적재되지 않는지 관측한다. 두 제어와 `/proof`는 loopback·전용 키·전용 DB 설정 검사를 모두 요구한다.

기존 검증 결과·원본 DB는 보존한다. 이 도구의 새 DB도 검토 증거와 함께 보존하며 자동 삭제·초기화하지 않는다.

`workflow.py food|quick|warehouse`는 실제 제품 HTTP 흐름을 사용한다. `--pause-after-accept`로 수락 결과를 잃은 상태에서 중지한 뒤, 같은 DB·키링·journal을 사용해 서버를 재시작하고 이어갈 수 있다. 일반 실행은 불명확한 POST를 재전송하지 않는다. `--runtime-controls --inject-acceptance-failure`는 퀵 수락 후속 알림의 SQL INSERT를 한 번 실패시키고, 실제 복구 예정시간 뒤 제품 복구 서비스를 두 번 호출해 Outbox 중복을 비교한다.

창고 사례는 입고·피킹·포장이 끝난 **합성 재고**에서 시작한다. 출고 재위탁 초안의 하차 위치·규격·견적·기사 보수를 기존 화주 수정 API로 확인한 후 후불 승인한다. 사전 견적과 저장 견적에는 같은 좌표를 사용한다. `repair_warehouse_quote.py`는 보존된 명시적 `WarehouseReconsignmentFareChanged` 409가 있는 합성 초안만 정상 화주 API로 다시 견적 확인한다. 거절 기록을 옮겨 보존하며 불확실한 승인이나 이미 승인된 원장을 바꾸지 않는다.

`prepare-mobile.ps1`는 별도 최신 공통 VM/HTTP 검증기에 비공개 입력을 전달한다. `member_receipt.py`는 배송 완료 뒤 제공자 완료 제안과 실제 수령자의 확인을 별도 HTTP 명령으로 검증한다. 같은 수령 확인의 명시적 replay는 idempotency 시험이며 기기 재시작·SecureStorage·실제 카메라 확인이 아니다.

실행 결과의 `Passed`는 각각의 명시된 검사 범위만 뜻한다. 음식 API의 현재 흐름에는 사진 필수 단계가 없으며, 기존 화물의 인수증 API를 고객 수령 확인으로 보고하지 않는다. 회원 배송의 별도 수령 확인과 실수납·실지급을 합치지 않는다. 일시적 Mongo 접속 실패는 기존 DB를 보존한 서버 재시작 후 새 case가 성공한 사실과 원인 미확인을 함께 기록한다.
