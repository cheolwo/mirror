# 준비 상태 검사의 진단 누락 보완

2026-11-04 정기 안정화 회차. 간헐 ready503을 추적할 때 개별 검사 결과가 비어 있던 검증 호스트의 결손을 고쳤다. **실제 503의 발생 원인은 아직 미확정**이다. 제품의 상태 판정·15초 제한·응답 본문/헤더·업무 API·Serilog 설정은 바꾸지 않았다.

## 확인한 문제와 수정

기존 관측은 ILoggerProvider의 framework health 이벤트 전달에 의존했다. 현재 호스트의 Serilog 경로에서는 이벤트가 관측되지 않았고, HTTP 200도 검사 결과가 없다는 이유로 nonhealthy 기록을 소비했다. 기존 flat journal16개는 **200 14개/503 2개이며 전부 Checks가 비어 있다.** 이 파일들이 한도를 채워 새 실패가 기록되지 않았다. 과거 파일의 상태를 모두200으로 일반화하지 않는다.

[R108ReadinessDiagnosticStartupFilter.cs](../../eng/Ssalddel.IntegratedWorkflowR108/R108ReadinessDiagnosticStartupFilter.cs) 한 파일을 보완했다.

- 원래 HealthCheckService를 같은 수명의 비공개 keyed DI 등록으로 보존하고 실제 HealthReport를 반환 직전에 관찰한다. 원래 predicate·취소 토큰·실행 횟수·report 객체·예외를 유지하며, inner의 생성·정리는 DI가 계속 소유한다. 등록이 불명확하면 원래 service collection을 유지하고 관측을 붙이지 않는다. 중복 등록은 한 번만 적용한다.
- 실제 report 관측과 로그 이벤트 관측을 별도로 표시한다. 허용한 두 검사 이름·상태·시간·예외 종류·고정된 네 pending-migration 값만 기록한다. Description·예외 Message·임의 Data·본문·비밀값은 기록하지 않는다.
- 새 프로세스별 r4 세대에 정상2/실패16/미관측4개로 나누어 제한한다. 기존 journal은 삭제하거나 고치지 않는다. 정상/미관측 응답이 실패 슬롯을 소비하지 않는다.

## 이번에 실제 검증한 범위

| 검사 | 결과 |
| --- | --- |
| 프레임워크 콘솔 계약 검사 | **29항목 통과**, 입력 SHA 변경0. 실제 framework MapHealthChecks와 fake 검사 사용. 네트워크/DB/휴대폰을 실행한 시험은 아님 |
| 비침습·오류 회귀 | logger 완전 제거/None 차단, 원 report/predicate/취소/예외, 200·503·Degraded 본문/전체 헤더/실행 횟수, 동시/중첩·HTTP 밖 호출, DI 수명/소유권·멱등/no-op, 제한/IO 실패·비밀 제외 |
| 현재 호스트 빌드 | 선택 소스 전후 변경0, 새 전체 출력453개 복사/SHA 일치. 원래 실행본·부분 출력·기존 run 자료 보존. 기존 CodeMetadata 공용 출력 경로의 한계는 유지 |
| 실제 HTTP | 새 호스트에서 live200, ready **12/12 200**, 홈 조회200, control GET2개200. 같은 업무/DB proof의 정규화 SHA가 교체 전과 일치 |
| 실제 진단 기록 | 새 세대의 정상 기록2개에 **HealthReportObserved=true, logger 이벤트=false**, 알려진 두 검사 Healthy와 네 pending 값0 수집. 기존16개 SHA 불변·허용 필드/비밀 제외 확인 |
| 실제 휴대폰 읽기 | 공용 OS 잠금·등록 단일 기기 확인. 설치된 APK142 바이트 SHA가 기존 검증본과 일치하고 현재 운송 화면에 같은 합성 의뢰·연결 오류 없음. 입력·화면 전환·설치·깨우기·스크린샷 저장은 하지 않음 |

eng/validate-changes.ps1의 이번 eng 경로 선택은 Fast/Task 모두 **GuidanceOnly**로 분류되어 diff 검사만 실행하고 빌드·시험은 생략한다. 이것을 코드 시험 통과로 세지 않았다. 위29항목의 별도 실제 실행과 전체 검증 호스트 빌드로 해당 C#을 컴파일·검증했다. 앱 선택 입력3,273개가 변하지 않아 APK를 다시 만들지 않았고 과거16,899시험을 이번 판본의 새 결과로 승계하지 않았다.

실행 보조도구의 첫 소유권 확인은 JSON 날짜 역직렬화에 따른 형식 비교로 프로세스 전환 전에 중단했다. PID·부모·선택 경로·UTC 시작 ticks를 대조한 뒤 같은 소유 호스트만 교체했으며 첫 중단 기록도 보존했다. 새 DB·migration·seed·업무 명령 재실행은 없다.

## 현재 상태와 다음 한 가지

같은 integrated-r108:be09622360b74a42afb695cdbf27eeea 실행과 기존 격리 DB의 로컬 Simulation을 재사용했다. 현재 호스트는 PID29596·부모21200이며 실제 선택 경로와 파일 SHA를 대조했다. 선택 harness SHA는 `28562eb8c177eb05ed58e26e4b4708a623f8dde28a97874148e4b68a264c8974`다. 다음 회차에는 이 숫자를 현재 실행으로 가정하지 않고 다시 소유권을 확인한다.

이번12회에는 실제503이 재현되지 않았다. 단위 검사의 fake503과 실제 운영 실패를 구분하며 진단 기록 보완을 503 해결로 보고하지 않는다. **다음은 실제503이 생긴 요청의 HTTP 상태와 report 관측/검사별 결과를 대응해 실패 지점을 좁히는 것**이다. 상위 미들웨어의 응답인지 실제 health 검사 실패인지도 관측 없이 단정하지 않는다.

이번 변경은 검증 도구의 진단 누락 보완이다. 다른 역할 전체 기기 완주·지도/GPS/Push·원격 Operational·실제 금융·배차/운행은 미검증으로 유지한다. 배민 내역 수집·영상·유료 API·배포·commit/push·거부된 중계 프로그램 재실행은 하지 않았다.

새 로컬 근거는 `artifacts/local/readiness-observer-r4/0bd6c05c339e4799be94febe0fed6726/completion.json`에서 연결한다. selftest-attempt1.json/.log, host-build-r1/proof.json, runtime-before.json, runtime-after.json, journal-verification.json, runtime-start/runtime.json, phone-after-server.json, client-input-binding.json에 각 범위를 나누어 보존했다.
