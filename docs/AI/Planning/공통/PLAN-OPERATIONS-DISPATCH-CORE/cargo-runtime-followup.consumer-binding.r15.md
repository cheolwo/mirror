# r15 짧은 시험 DB의 기존 HTTP 도구 결속

상태: Approved / ReadyToDispatch. schema-name-fix의 필수 소비 도구 호환 보완이다. 새 짧은 GUID DB의 실제 네 migration이 성공하고 live/ready HTTP200을 확인했지만 기존 workflow.py의 긴 prefix 전용 guard가 명령 실행 전에 R108LiveBoundaryRequired로 차단했다. 이 실패에는 업무 명령이 없었다.

`eng/Ssalddel.IntegratedWorkflowR108/workflow.py`의 경계 검증만 보완하고 같은 폴더의 좁은 테스트를 추가한다. CurrentModel 기존 이름/과거 준비 기록 호환을 유지한다. 짧은 `r108m_GUID32`는 MigrationFirst 및 실제 준비 성공 표시와 정확한 root/GUID/RunStableId/private environment DB/모드/경로 결속을 요구한다. 기존 loopback/Simulation/외부 효과 차단을 유지한다. 허용 이름을 느슨한 일반 DB regex로 바꾸지 않는다. 요청/재시도/상태 전이/사진/금융 로직은 바꾸지 않는다.

정상 legacy/new, 모드/이름/ID/경로/DB 불일치를 부정 시험으로 검증한다. 이 변경은 새 서버 build를 요구하지 않는 HTTP 시험 도구의 호환 보완이며 root만 실제 workflow·휴대폰·DB를 조작한다.