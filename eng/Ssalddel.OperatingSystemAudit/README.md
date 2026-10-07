# 업무 OS 정적 조사 도구

운영 OS의 안정 식별자, 생명주기 책임 단계, 엔진 결속, 스케줄 정책 선언, OS 간 인계를 현재 공통 계약과 API 메타데이터에서 읽는다. 별도 조사 원장을 지정하면 OS·단계의 조사 범위, 근거 파일의 SHA-256과 소스 anchor도 검사한다.

~~~powershell
dotnet run --project eng/Ssalddel.OperatingSystemAudit -- `
  --root . `
  --audit docs/ProjectOverview/page-docs/os-lifecycle-static-audit-r23.json
~~~

기본 출력은 `artifacts/local/os-lifecycle-audit-r23/catalog-validation.json`이다. `--audit`을 생략하면 현재 카탈로그만 검사한다. 옵션은 `--root <경로>`, `--audit <경로>`, `--output <경로>`이며 알 수 없는 옵션·중복 옵션·빈 값·다음 옵션을 값으로 넘기는 입력은 거절한다. `--root`는 존재하는 디렉터리여야 한다.

`--output`은 저장소 내부에서 `artifacts/local` 하위 파일만 허용한다. 저장소 밖의 명시적 출력 경로도 사용할 수 있으나 감사 입력이나 `sources`에 등록한 근거 파일을 덮어쓸 수 없다. 디렉터리 링크의 실제 경로도 대조하며, 안전한 위치에 임시 파일을 쓴 뒤 결과 파일을 교체한다. 제품 소스·문서·설정으로 출력 경로를 지정하면 거절한다.

카탈로그·해시·anchor 검사를 완료하면 전체 결과를 출력 파일에 저장하고, 표준출력에는 `succeeded`, `summary`, `errors`, `output`을 포함한 JSON 요약을 기록한다. 검사 불일치는 종료 코드 1이고, 모두 일치하면 0이다. 입력·CLI·schema·경로 보호·파일 입출력 오류는 표준출력의 JSON에 `succeeded: false`, `errors`, `errorDetails`, `output: null`을 기록하고 1로 종료한다. 이 오류 경로는 결과 파일을 새로 만들거나 기존 결과를 덮어쓰지 않는다. 이전 실행 파일이 남아 있어도 이번 실행 결과로 읽지 않는다.

조사 입력 schema는 `operating-system-source-audit.r23`이다. `operatingSystems`와 `sources` 배열을 요구하며, `sources`에는 저장소 상대 경로와 64자리 16진수 `sha256`을 기록한다. 각 OS의 `stages`, `procedures`, `stateGroups`는 배열이어야 한다. 절차와 상태묶음의 근거는 `sourceRefs`로 연결한다.

`sourceRefs`, `evidenceRefs`, `uiRefs` 및 다른 `*Refs` 목록의 항목은 객체이며, 비어 있지 않은 저장소 상대 `path`와 `anchor`, 양의 Int32 JSON 정수 `line`을 요구한다. 누락·빈 anchor, 문자열·소수·범위를 넘는 line을 건너뛰지 않는다. `testClasses`는 별도 목록으로 `name`과 `path`를 요구하고, line은 제공한 경우 양의 정수여야 한다. anchor를 제공하면 line도 요구하고 실제 소스에 대조한다. `validation`의 시험 실행 근거는 별도 형식이며 source anchor 목록으로 해석하지 않는다. 시험 클래스의 목록·경로·anchor가 존재해도 시험 실행을 증명하지 않는다.

| 오류 코드 | 원인 |
| --- | --- |
| `cli-option-unknown` / `cli-option-duplicate` / `cli-option-value-missing` | 알 수 없는 옵션·중복·누락된 경로 값 |
| `root-directory-missing` / `audit-path-invalid` | 존재하지 않는 root 또는 잘못된 경로 |
| `audit-json-invalid` / `audit-schema-invalid` | JSON 문법 또는 필수 객체·배열·문자열 형식 오류 |
| `audit-source-invalid` / `audit-reference-invalid` | 근거 파일 hash 또는 path·anchor·line 형식 오류 |
| `output-input-collision` / `output-source-collision` | 감사 입력 또는 근거 파일과 출력 경로가 같음 |
| `output-repository-path-forbidden` / `path-link-unresolved` | 제품 경로 출력 또는 안전하게 해석할 수 없는 링크 |
| `audit-io-error` / `audit-execution-error` | 읽기·저장 실패 또는 조사를 완료할 수 없는 오류 |

조사 근거가 변경된 경우 새 소스를 사람이 대조한 뒤 조사 판본을 갱신한다. 파일 해시만 바꿔 조사 완료로 처리하지 않는다. 서버 시작, DB 접속, 외부 API, 업무 상태 변경, 지도, APK 설치는 수행하지 않는다. 계약 프로젝트와 API 메타데이터 소스만 컴파일한다. `Sequence`는 책임을 설명하는 순서이므로 다음 상태로 넘어가는 실행 규칙이 아니다. `Active`도 카탈로그 선언이며 실제 실행 완료 증거가 아니다.

조사 원장의 `stages`는 기존 카탈로그의 stage ID만 사용한다. 아직 공통 생명주기가 없는 OS는 `stages: []`와 관찰한 `procedures`·`stateGroups`를 별도로 남긴다. 근거 anchor 존재와 해시 일치는 조사 대상의 동일성을 검사하며, 모든 분기·권한·동시성의 정확성을 자동 증명하지 않는다.

현재 조사와 확인된 부족은 [OS 생명주기 정적 조사 r23](../../docs/ProjectOverview/page-docs/os-lifecycle-static-audit-r23.md)을 따른다.

r23 당시 2026-10-06 조사에서는 화면의 역할과 workflow 참여자 대장 사이 불일치 3개가 탐지됐다. `CustomsAndTradeData/ShipperOrSeller`, `FoodDelivery/PlatformOperator`, `SsalddelMart/Orderer`다. 해당 종료 코드 1은 이 정적 불일치를 뜻하며 빌드 오류나 관련 단위시험 실패가 아니다. 누락 역할을 새 권한으로 자동 등록하지 않는다.


## r24 후속

r24에서 참여자 3쌍과 다섯 OS 책임 단계 21개를 보완했다. 현재 카탈로그만 검사할 때는 `--audit`을 생략하고 `--output artifacts/local/os-lifecycle-hardening-r24/catalog-validation.json`을 사용한다. r23 JSON은 이전 소스의 조사 스냅샷이므로 소스가 바뀐 지금 전체 입력으로 재실행하면 오래된 해시/단계 범위를 탐지하는 것이 정상이다. r23의 해시만 현재 값으로 바꿔 새 전체 조사 완료로 처리하지 않는다. 문제별 보완·실행 검증·남은 운영 연결은 [r24 보고서](../../docs/ProjectOverview/page-docs/os-lifecycle-hardening-r24.md)를 따른다.
