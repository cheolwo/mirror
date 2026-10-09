# 생활·음식 배달 자동 수행처 선정

2026-10-07. [승인 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment-routing.implementation.r25.md)과 [페이지 책임](../ProjectOverview/page-docs/fulfillment-routing-r25.md)을 따른다.

## 문제와 보완

기존 수행처 비교는 생활 배송만 지원하고 사용자가 처리처를 직접 확정했다. 이번 변경은 생활·음식 원천을 유지하면서 플랫폼이 시간·비용·동의 조건 안에서 수행처를 선정하고 자동 접수하는 흐름을 연결한다. 음식 주문에는 식당 정액 지원과 주문자 잔여 부담, 주문 확인 뒤 수행 선정과 유효한 기사 확보 뒤 명시적 조리를 연결한다.

## 구현 결과

상태는 `Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`다. 생활·음식 원천 adapter와 공통 Coordinator, 견적 평가, 영속 동의·현재 수행·시도·Outbox, 자체 배차와 모의 공급자 두 개를 연결했다. 시간·가용성·누적 본인 부담 상한을 충족한 후보를 총비용→완료 ETA→고정 공급자 ID 순으로 선정한다. 실행 직전에 원천 상태·동의·견적·현재 수행 판본을 다시 확인한다.

원 요청의 현재 수행은 `(SourceKind, SourceId)`당 하나이며, 같은 동의 판본에서 공급자별 접수는 한 번이다. 응답 유실·재시작은 같은 시도를 재확인한다. 확정 실패/취소 뒤에는 같은 조건에서 재선정하지만 Unknown·취소 미확정·발생비용 미확인에서는 다른 공급자 접수를 막는다. 기사 개인 거절은 공급자 실패로 바꾸지 않는다. 오래된 Outbox·배달권 투영·기사 수락과 기존 수동 변경 경로에도 현재 수행 검사를 적용했다.

음식점 지원 `S`는 원 주문의 최초 동의에 한 번 동결한다. 재선정·동의 갱신에도 유지하며, 누적 확정 비용+새 견적을 `C`로 계산해 식당 부담 `min(S,C)`와 본인 부담 `C-min(S,C)`을 나눈다. 메뉴 금액·배송비·기사 지급액은 구분한다. 음식점 주문 확인과 같은 트랜잭션에서 수행 요청을 예약하고, 유효한 기사 배정 뒤에만 명시적 조리 시작을 허용한다. 업체 Accepted만으로 조리할 수 없고 조리 이후 자동 처리처 변경도 막는다. 모의 업체의 배정·픽업·전달은 명시적 증거 단계로 저장하며 시간 경과로 완료하지 않는다.

기존 요청의 nullable 필드·null 직렬화 제외와 fingerprint를 보존했다. 공통 작성/상세에는 비용·완료 시각·허용 처리처·개인정보 제공·자동 접수 동의, 선정 이유·분담·복구 행동을 추가했다. 응답 유실 때는 원 요청 ID와 원 조건을 유지해 확인하고 새 견적·동의를 덮어쓰지 않는다. 식당 지원 변경 시 주문자가 조건을 다시 확인한다. 기능 OFF의 기존 자동 요청은 실행 정지·조회 전용이며 비용/상한 미확인 값을 null로 유지한다. 이는 현재 접수 취소를 의미하지 않는다.

## 대표 화면

실제 공통 Razor/VM에 합성 API 응답을 연결해 설치된 Chrome에서 렌더했다. 업무 HTTP·운영 DB·실제 업체 접수·휴대폰 화면의 증거와 구분한다.

| 장면 | 화면 |
| --- | --- |
| 생활 요청 응답 유실 후 원 요청 확인 · 320px | [화면](../assets/changes/2026-10-07-fulfillment-routing-r25/living-lost-320.png) |
| 음식 주문의 지원액·본인 상한·자동 동의 · 390px | [화면](../assets/changes/2026-10-07-fulfillment-routing-r25/food-create-390.png) |
| 기능 정지 시 음식 요청 조회 전용 · 320px | [화면](../assets/changes/2026-10-07-fulfillment-routing-r25/food-paused-320.png) |
| 음식점의 주문 전체 정액 지원 설정 · 390px | [화면](../assets/changes/2026-10-07-fulfillment-routing-r25/merchant-support-390.png) |

## 검증 기록

| 검증 | 결과와 실제 범위 |
| --- | --- |
| 집중 시험 | 자동 수행·기존 생활 수행 관련 138개 통과. SQLite의 조건·비용·판본·동시 실행·재시작·응답 유실·재선정·취소와 역할 UI VM을 포함한다. |
| Fast | 전체 `Ssalddel.v3.5.slnx` 및 소비 앱 빌드와 관련 344개 시험 통과. 최종 로그/집계는 아래 로컬 근거에 결속한다. |
| Task | 전체 제품·소비 앱 빌드와 시험 8,234개 통과, 실패 0. 기존 환경 조건 Mongo 통합 2개는 건너뜀. 실제 Mongo 연결 시험으로 확대하지 않는다. |
| 음식 흐름 | 격리 SQLite에서 실제 Handler/Store/Coordinator/Adapter·가격 계산·명시적 조리·음식 기사 수락을 검증했다. 주소·경로·날씨·알림과 대부분 큐 생성은 대역이며, 취소 후 재개 1건은 실제 큐 생성·배달권 투영 서비스를 사용했다. 실제 배차 후보 생성·현장 운행은 미검증이다. |
| HTTP 계약 | Loopback HTTP의 실제 Controller/UseCase/SQLite와 권한·입력·no-store 경계. 인증과 Coordinator는 대역이므로 운영 로그인 또는 전체 주문 HTTP→배차 완주가 아니다. |
| PG·정산 경계 | 실제 결제 승인 진입점과 지연 승인 Outbox의 모의 혼입 차단, 관리자 취소/기존 경로 회귀를 확인했다. 실제 PG 호출·지원금 청구·은행 입금·기사 정산은 실행하지 않았다. |
| 스키마 | r24 세 테이블/ID 유지, SourceKind 기본값·backfill·키 확장 및 동의/지원/Outbox/모의 단계 표 추가. SQLite에 실제 Up SQL 적용, MySQL SQL 생성, 관련 model/snapshot 차이 0을 검사했다. 실제 공용 MySQL 적용은 하지 않았다. |
| 모바일 UI | 13상태×320/390px의 26장면 통과. 복구 버튼·원 동의 보존·정지·Unknown·지원 재확인·오류 속성의 실제 값 표시를 검사했다. 가로 넘침·페이지 오류·업무/외부 요청·소스/바이너리 지문 변경 0. |

원시 근거는 `artifacts/local/fulfillment-routing-r25/`의 집중 TRX, Fast/Task 로그, `ui-final/verification.json`, `final-verification.json`, `preservation-after.json`에 보관한다. 모바일 검수에서 원 요청 복구가 견적 상태에 가려지는 문제와 Razor 오류 문자열 바인딩 문제를 고친 뒤 다시 검증했다. 초기 실패 증거도 보존했다. 관련 없는 기존 dirty 작업·UI csproj 해시·branch·HEAD를 보존했고 commit/push는 실행하지 않았다.

## 로컬 사용과 남은 경계

`FulfillmentRouting:Enabled`는 기본 false이고 `SsalddelExecution:Mode=Simulation`에서만 켤 수 있다. Quartz Outbox 작업은 이 조건에서 등록하며 실행 때도 재확인한다. 모의 단계는 인증된 관리자용 `POST api/v1/admin/fulfillment-simulation/{attemptId}/evidence`에서 명시적으로 입력한다. 이 API는 실제 업체 webhook이나 자동 완료 서비스가 아니다.

DB 연결 전에 r24 migration과 `20261007160000_AddAutomaticFulfillmentRouting`을 적용해야 한다. OFF에서 신규 수행 표 조회를 생략해도 신규 원천 컬럼의 스키마 요건은 남는다. r25 Down은 원천별 키·동의 데이터 손실을 막기 위해 DDL 전에 차단한다. 되돌리려면 별도 검토한 migration이 필요하다.

생활 이동 ETA는 저장된 거리×3분의 Simulation 모델이며 음식 경로 응답의 이동시간에는 모의 기사 확보·조리·픽업 여유를 더한다. 시험의 경로 HTTP는 대역이고 실제 업체 견적·실시간 ETA·SLA를 보장하지 않는다. 실제 업체 API/webhook·개인정보 송신·공용 DB·운영 로그인·실기기/APK·Azure·DOT 상시 운영·Unity·배포는 별도다.
