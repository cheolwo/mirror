# 검증 서버의 유휴 Mongo 연결 재사용 오류 보완 r6

이번 범위는 기존 r108 로컬 Simulation 호스트와 같은 격리 DB의 조회 안정성이다. 제품 전체의 Mongo 설정·원격 운영·업무 계약은 바꾸지 않는다. [이전 r5](2026-11-04-shipper-authoring-storage-errors-r5.md)의 정상 APK143과 기존 입력/인증 수정은 보존한다.

## 발견과 최소 수정

- 홈의 `공개 참여 현황을 확인하지 못했습니다.`는 누적 참여 집계와 다른 경로인 `GET /api/v1/common/space-cooperations/public?page=1`에서 발생했다. 같은 실행에서 실제500과 안전한 감사 로그의 `MongoConnectionException`을 대응했다. page0은400으로 거절돼 Controller 생성까지 통과했고, 기존 원장 인덱스와 공간 제안 일치 문서0개도 조회했다. 집계 API의200이나 빈 원장을 이 오류의 해결 근거로 사용하지 않는다.
- 2026-11-04 05:16:24 UTC의 **실제 ready503**을 r4 관측기로 포착했다. report는 `Unhealthy`, `persistence`는 `MongoConnectionException`, 네 MySQL pending 값은 모두0, `simulation-persistence`는 `Healthy`였다. 이전 로그 없는503의 원인을 소급 확정하지 않는다.
- 기존 검증용 HAProxy는 client/server 유휴 연결을60초에 닫지만 Mongo 드라이버의 기본 최대 유휴시간은600초였다. 같은 선택 드라이버·같은 loopback의 독립 연결 비교에서 초기 ping 두 건은 성공했고66초 유휴 뒤 기본 연결은 `MongoConnectionException → EndOfStreamException`, 최대 유휴30초 연결은 성공했다. 이는 서버의 실제 연결 풀을 대신한 증거가 아니므로 수정 호스트에서 별도로 확인한다.
- `CargoJourneySettings.MongoConnection`에 `maxIdleTimeMS=30000`만 명시했다. 주소·포트·DB·계정·모드·정확한 설정 대조와 외부효과 차단을 유지한다. gateway/컨테이너를 바꾸지 않고 오류를0건이나 성공으로 숨기거나 업무 명령을 자동 재전송하지 않는다.

## 현재 검증과 재개 위치

- 새 격리 출력의 빌드는 오류0·경고행64개이며 선택 소스8,771개가 전후 동일했다. 전체453파일을 SHA로 대조해 보존한 새 실행본으로 같은 run/DB를 한 번 재개했다. gateway·DB·기존 출력은 재생성하지 않았다. 현재 서버는 PID2964/실행기34112이며 시작 UTC는 `2026-11-04T05:28:44.6747960Z`다. 다음 회차에는 이 과거 PID를 현재 실행으로 단정하지 말고 실제 경로·시작 시각·해시를 다시 대조한다.
- 실제 새 호스트 DLL의 상수와 같은 드라이버 해석, 나머지 연결 설정 동등성·전체 사본·원래 Settings 원문 복원 SHA를45항목으로 확인했다. 첫 도구는 메타데이터 의존성 로딩과 추가 IL 파서에서 실패했다. 그 실패 판본/결과는 보존하고, 추가 파서 대신 실제 빌드에 결속된 **전체 원문 SHA 복원 일치**로 보호 조건 보존을 확인한 r4 도구가45/45 통과했다. 실패 도구를 통과로 집계하지 않는다.
- 실제 수정 서버의 초기 확인과 **66초씩 두 번 유휴** 뒤 준비 검사/공동운영 조회는 대상6/6 HTTP200이었다. 마지막 유휴 뒤에는 공동운영 조회를 먼저 호출해 준비 검사의 선행 연결 효과와 분리했다. 공개 응답은 실제 조회된 `Simulation/simulation-excluded`·집계0·빈 제안 형식을 검증했다. 실패를 빈 응답으로 바꾼 것이 아니다.
- 후속 live/ready·기존 누적 집계·업무 proof도 정상이고 두 업무 proof SHA가 수정 전과 같다. 같은 run/DB·미지급 금융 행0·초기화/성공 migration 기록과 옛 journal·이번 실제503을 보존했다. 이번 재현 조건의 검증 환경 결함을 보완했으며 과거 미관측503 전부나 앱 전체 안정화·출시를 완료한 것은 아니다.
- 이번5경로의 Fast(`20261104-142017`)와 Task(`20261104-142051`)는 현재 도구가 `GuidanceOnly`로 분류해 diff 검사만 수행했다. build/tests skipped를 시험 통과로 집계하지 않으며 위 실제 호스트 빌드·설정45항목·유휴 HTTP6건을 별도 검증 근거로 사용한다. 정상 APK143/앱 소스·기존 회귀 소스는 그대로라 APK/전체 시험을 반복하지 않았다.

재개 근거는 `artifacts/local/app-stability-r6/410b66fec57a4a02856e842714814561/completion.json`이다. 같은 폴더의 `runtime-identity.json`/`runtime-before.json`, `public-cooperation-before.json`, `api-audit-errors-route-r4.json`, `mongo-ledger-before.json`/`mongo-space-count.json`, `actual-ready503.json`, `mongo-idle-result.json`에서 실패·예외형·읽기 비교를 구분한다. 수정 실행은 `host-build-r1/proof.json`, `built-mongo-settings-r4.json`, `runtime-revised-identity.json`, `revised-idle-http.json`, `runtime-after.json`에 연결한다. 보조 읽기 도구의 구문/컬럼명/route 형태 보정도 이전 도구를 보존한 새 판본으로 진행했다.

실제 등록 USB의 OS 잠금·기기 일치와 설치143 SHA는 읽기로 대조했다. 다른 앱이 전면이어서 전환·재설치·화면 입력은 하지 않았다. 최종 검토 화면의 기기 진입/복귀와 저장소 오류 재시도, 다른 역할 전체와 원격 운영은 여전히 미검증이다. 원시 UI·스크린샷·계정·예외 원문은 새 근거에 보관하지 않는다.

다음 한 가지는 휴대폰이 다른 업무에 사용되지 않을 때 기존143의 화주 최종 검토 진입과 입력 복귀를 확인하는 것이다. 새 실패가 생기면 실제 report/검사를 먼저 대응한다. 실제 금융·배차/운행·운영 활성화·원격 배포·휴대폰 배달내역 수집·영상·유료 API·commit/push는 수행하지 않았다.
