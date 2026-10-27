# r15 migration-first 시험 DB 이름 보완

상태: Approved / ReadyToDispatch. 현재 사용자의 서버 결손 연속 보완 범위다.

실제 새 GUID 시험 `6a6db65f28f64df79c2995870d767ec2`는 MySQL 4163/42000, 적용 0/남은 main 72에서 중단됐다. 실제 로드된 Pomelo 9.0.0의 원래 잠금 이름 `__{databaseName}_EFMigrationsLock`은 기존 53자 DB 이름에서 72자가 되어 MySQL 64자 한도를 넘는다. 실패 DB와 시도 기록을 그대로 보존한다.

명시적 MigrationFirst 신규 준비에서만 `r108m_` + 32자리 GUID(38자 DB, 57자 잠금)를 사용한다. 기본 CurrentModel의 기존 이름과 과거 데이터는 유지한다. GUID artifact 폴더와 RunStableId, preparation의 DB/모드/경로, 실제 연결의 loopback/포트/DB/사용자 일치를 계속 검사한다. 짧은 이름은 MigrationFirst 모드에서만 허용한다. 실패했던 긴 이름 DB는 InspectSchema의 읽기 관측에만 사용할 수 있고 새 migration 대상으로 재사용하지 않는다.

소유 범위는 R108 `CargoJourneySettings.cs`, `run.ps1`, `R108SchemaReadiness.cs`, `README.md`의 DB 이름 생성·검사·GUID 추출 및 기존 경계 회귀다. provider·제품 migration/history·readiness는 변경하지 않는다. 네 실제 migration 실행, 실패/중단 보존, 외부 통신/hosted worker 차단은 이전 명세 그대로 유지한다. root만 새 GUID 준비·격리 build·실행을 수행한다. 실제 새 chain·ready 200과 앱/DB 업무 검증 전에는 성공으로 기록하지 않는다.