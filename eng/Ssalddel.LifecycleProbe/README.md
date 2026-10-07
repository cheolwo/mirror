# 실제 Mongo 생명주기 회귀 probe

교육기관 제출 대기열과 공동구매 선적 저장소의 실패·재시도·상태 회귀 방어를 `127.0.0.1:27027`의 전용 실제 Mongo에서 확인하는 로컬 전용 콘솔입니다. 기존 인증된 제품 Mongo(27017)의 자격 증명을 사용하지 않습니다. 제품 서버를 시작하지 않으며 appsettings, user secrets, 환경의 Mongo 연결문자열을 읽지 않습니다. 실행 전에 이 포트에 격리된 Mongo를 준비해야 합니다.

저장소 루트에서 실행합니다.

```powershell
dotnet run --project eng/Ssalddel.LifecycleProbe -- --report artifacts/local/os-lifecycle-hardening-r24/mongo-probe.json
```

`--report`를 생략하면 위 경로를 사용합니다. `--help`는 실행 안내만 출력합니다. 보고서는 저장소의 `artifacts/local/` 아래 JSON만 허용하며 UNC, URI, 경로 이탈, 링크를 통한 우회와 디렉터리 출력은 거절합니다. 제품 파일 경로를 출력 대상으로 사용할 수 없습니다.

실행마다 `r24-lifecycle-{Guid:N}` 이름의 새 DB 하나를 배정합니다. 모든 제품 저장소에 이 이름을 명시하고, 성공·실패에 관계없이 `finally`에서 이 실행이 만든 정확한 이름만 삭제합니다. assertion 실패, Mongo 실행 실패, 정리 실패 또는 보고서 저장 실패는 exit 1입니다. 성공은 exit 0이며 stdout에 상태·검사 수·정리 상태·보고서 경로를 출력합니다. 입력 오류는 DB를 사용하지 않고 stdout에 `PROBE_INPUT` 구조화 오류를 출력합니다. 보고서 오류는 `PROBE_REPORT_WRITE`, 정리 오류는 `PROBE_CLEANUP`, assertion/실행 오류는 각 probe 이름을 붙여 기록합니다. 연결문자열, credential, 원시 예외 메시지/stack은 출력하지 않습니다.

검증 범위는 다음과 같습니다.

- 교육 대기열: 신규 확보, 전송 영수증 저장, 같은 예약의 완료 상태 멱등성, 다른 원장의 같은 제출 ID 거절, 투영 실패 뒤 영수증 보존, 새 대기열 인스턴스의 AlreadySent 확보, 새 `원장반영완료` 필드가 없는 기존 완료 문서의 AlreadySent 재확보, 원장 반영 완료 뒤 재확보 차단, 늦은 실패의 완료 상태 보존.
- 교육 worker: 실제 Mongo 커뮤니티 원장과 실제 현장 체험 UseCase를 사용합니다. 가짜 sender가 성공한 다음 통제된 wrapper에서 원장 투영만 한 번 실패하고, 실제 worker 재시도에서 추가 전송 없이 원장을 학교심사중으로 복구합니다. 학교 출석 인정·미인정 결정이 먼저 저장된 두 경우에는 실제 worker 처리 뒤 상태와 revision을 보존합니다.
- 선적: 실제 Upsert/Append로 과거 이벤트를 이력에 보존하며 현재 상태·위치·시각 회귀를 차단합니다. Completed 뒤 새 통관·예외 이벤트는 이력만 추가하며, 과거 또는 새 이벤트를 포함한 metadata Upsert는 기존 이력과 Completed를 보존하고 상품 요약을 수정합니다. 같은 full provenance의 Upsert 반복은 중복 이력을 만들지 않습니다. 별도 문서에 동시 세 이벤트를 Append해 이력 전체와 최종 Completed를 확인합니다. 동시 케이스의 Completed 발생시각은 세 이벤트 중 최신입니다.

교육의 backoff를 기다리지 않도록 본인 DB의 특정 제출 문서 `다음시도시각Utc`만 직접 due로 조정합니다. worker의 internal `ProcessPendingAsync` 한 배치는 reflection으로 호출하며 BackgroundService를 시작하거나 제품 visibility를 바꾸지 않습니다. 전송 sender는 횟수를 세는 가짜 구현이며 SMTP·교육기관 HTTP API를 호출하지 않습니다.

선적의 `DomesticCarrierPickup → Exception → DocumentRegistered → Completed`도 별도로 확인합니다. 예외 뒤의 문서등록 이벤트는 이력에 남으며 현재 Exception과 저장된 마지막 정상 상태를 보존합니다. 이후 최신 Completed는 정상적으로 진행합니다. Mongo 8에서 제출 ID의 기본 `_id_` 인덱스와 두 조회 인덱스도 확인합니다. 총 명시적 assertion은 32개입니다.

보고서의 source SHA-256은 실행 당시 소스 판본을 결속합니다. 모든 분기 검증, 운영 기관 전송, 서버 HTTP/API 검증, APK 화면·실제 기기 사용 검증을 뜻하지 않습니다. 구현 담당자는 빌드/실행을 하지 않았으며 중앙 검증 실행 결과로 완료 여부를 판단합니다.
