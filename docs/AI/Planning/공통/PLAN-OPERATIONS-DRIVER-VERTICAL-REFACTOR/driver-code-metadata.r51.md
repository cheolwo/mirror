# 음식 기사 책임 메타데이터·탐색 지도 r51

- PLAN: `PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR`
- 상태: `Implemented / LocalValidated / DeviceAndOperationalDeferred`
- 승인: 2026-10-09 사용자의 `Implement the proposed plan.`. 선행 선택은 음식 기사 흐름부터, 핵심 연결만 검사다.
- 기준 commit: `d1bab3eec08dc0a49c9a87f882590d394205c9a4`, branch `dev/mirror-integration`.
- [선행 r50](driver-vertical-refactor.r50.md)의 동작과 기존 dirty 변경을 보존한다. 현재 대화의 직접 구현 승인을 범위 근거로 사용하며 새 commit/push를 인계 선행 조건으로 요구하지 않는다.

## 목적과 범위

`food-driver-work` 기능 키로 계약 → 운행 상태·저장 → 명령·조회 → API → 공통 화면의 핵심 책임을 기존 `SsalddelCodeMetadataAttribute`에 연결한다. 정산 없는 현재 업무와 legacy 작업 공간·일일 정산·완료 상세를 별도 단계로 유지한다. FlowOrder는 읽기 순서이며 실제 호출·실행 순서를 증명하지 않는다.

1. 계약, SQL 운행/후속 상태, EF 매핑 정의, 시작/종료/위치 명령, 현재 조회, 수락·픽업·전달, 후속 예약과 API를 분류한다.
2. 공통 UI의 순수 복구·단계·카드 표시와 HTTP 조율, 공통 셸 포트를 구별한다. 효과는 위임된 명령까지 설명하고 순수 정책에는 저장·네트워크 효과를 넣지 않는다.
3. `eng/work-areas/food-driver.json`에 읽기 진입점, 소스 범위, 제외 범위, 필수 단계와 명시적인 소스 결속을 둔다. partial 책임은 실제 파일로, Razor는 원본으로, MAUI XAML은 코드부와 함께 결속한다.
4. 기존 CodeMap CLI에 `--work-area food-driver`를 추가한다. Android DLL이나 가짜 Type 없이 native 소스 결속을 reflection 그래프와 분리한다. 기존 기본 Simulation·Unity CLI/출력은 보존한다.
5. 도구가 `docs/AI/generated/food-driver-code-map.md/json`을 생성한다. 경로·심볼·단계 누락·중복·잘못된 의존·오래된 생성 지도는 실패하고 미분류 보조 형식은 경고한다. 생성 문서는 직접 수정하지 않는다.
6. scoped Fast/Task 검증에 지도 검사를 연결하고 graph/source-binding 회귀 시험, 기본 지도 호환, 기사 Android APK 빌드를 확인한다.

## 정확성 경계

SQL 운행·음식 상태, Mongo 원주문 관계, Redis 위치 투영의 역할을 구분한다. 매핑 정의는 실제 저장 완료가 아니다. 기존 거절의 두 단계 저장과 운행 종료의 인덱스 정리 실패 로그 경계를 사실대로 적는다. common VM의 일시 요청 보존과 native 암호화 복구 저장을 혼동하지 않는다. 소스 줄번호는 도구가 심볼로 찾으며 수작업 고정하지 않는다.

이번 범위는 탐색·검증 기반이다. 업무 동작, API/JSON, DB 스키마, 화면 디자인을 바꾸지 않는다. 실제 단말 설치·GPS·지도·원격 알림·공용 DB·Azure·실운영·지급·Unity·영상·commit/push는 제외한다.

## 완료 기준

필수 reflection 단계와 native 결속이 실제 소스에 연결되고 순수/영속 경계 시험을 통과한다. 두 지도 프로필의 `--check`와 scoped 검증을 실행한다. 빌드·시험·APK와 실제 운영 증거를 분리해 변경 기록에 남긴다. 세부 범위는 [작업지시서](driver-code-metadata.r51.work-order.json), 사용법은 [음식 기사 탐색 안내](../../../../Architecture/FoodDriverCodeNavigation.md)에 둔다.

## 완료 결과

45단계·41소스 결속을 생성하고 scoped Fast 264/264, Task 12,751개(서버·공통 UI 9,780 / Simulation 2,001 / Unity 970)를 통과했다. 환경 의존 Mongo 시험 15개는 미실행이다. 기존 지도·E 책임 검사, 별도 CLI freshness/필수 단계/reparse 차단과 ARM64 기사 APK 서명을 확인했다. 업무 본문 35개는 새 metadata·개행 차이를 제외하면 기준과 동일하고 검증 소스 46개는 전후 불변이다. [이번 변경·검증 기록](../../../../Changes/2026-10-09-food-driver-code-metadata-r51.md).
