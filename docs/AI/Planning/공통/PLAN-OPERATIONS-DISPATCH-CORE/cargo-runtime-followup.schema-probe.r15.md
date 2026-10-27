# r15 서버 준비 검사 후속의 좁은 실행 명세

r15 사용자의 연속 보완 요청과 [구현 범위](cargo-runtime-followup.implementation.r15.md)의 서버 준비 항목에 따른다. r14의 EnsureCreated DB는 네 migration history가 없고 main 모델 테이블 232개가 존재한다. 이 DB에 migration을 혼용하지 않는다.

기존 `eng/Ssalddel.IntegratedWorkflowR108` 하네스에 다음 시험 전용 기능만 추가한다.

- 기존 기본 준비는 유지한다. 명시적 migration-first 선택은 새 GUID DB와 비어 있는 schema에서만 허용한다. 시도 전 상태를 남기고 부분 실패한 schema를 자동 재사용/초기화/재시도하지 않는다. 이미 성공한 같은 실행의 재시작은 기존 schema/DB/키링/seed를 보존한다.
- main·전통시장·농수산·공공수집의 현재 migration을 실제 실행하되 실패 시 해당 context·오류 종류/안전한 코드·적용/남은 개수를 기록한다. migration history를 꾸미거나 pending 검사를 우회하지 않는다. 모든 외부 효과와 hosted worker 차단은 유지한다.
- `HealthCheckService`의 실제 상태·의존성별 안전한 수치/태그를 ready와 별도 진단 자료에 기록한다. 원시 예외·연결문자열·키·사용자 입력은 쓰지 않는다. 건강 검사의 제품 정책/응답을 성공으로 바꾸지 않는다.
- 제품 코드·migration 소스는 변경하지 않는다. 성공·실패 모두 전용 새 DB를 보존하고 결과를 실제 앱 업무/운영 migration 준비와 구분한다. runtime root만 DB 준비·호스트 실행·중지를 소유한다.

수정 소유는 `eng/Ssalddel.IntegratedWorkflowR108/Program.cs`, 같은 폴더의 새 schema/readiness helper, `run.ps1`, `README.md`와 필요한 좁은 검증뿐이다. 기존 factory·seed·설정의 실행/외부 경계는 약화하지 않는다. root가 현재 실제 하네스 입력/명세 해시를 확인한 뒤 새 GUID 준비·빌드·실행을 수행한다. 부분 실패를 근거 없이 고치거나 같은 실패를 반복하지 않고, 별도 보완 범위가 필요하면 정확한 원인·후속 작업으로 반환한다.
