# [구현 · 운영 재무 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 현금 흐름 요약 r3]

- 기준일: 2026-09-18
- 상태: `Implemented / FocusedTestsPassed / AdminWindowsBuildPassed / RuntimeApiNotExecuted / DeviceNotVerified / NotOperationalAccounting`
- 선행 구현: [재무 영향 특성·원장 투영 구현 r2](financial-impact-ledger.implementation.r2.md)

## 구현 범위

기존 `재무사건`과 `관리계정전기`를 읽어 운영자가 조회 기간의 현금 이동과 아직 현금이 움직이지 않은 관리 후보를 구분하는 읽기 전용 수직 단위를 추가했다.

```text
재무사건·관리계정전기
  → 운영재무조회UseCase
  → GET /api/v1/admin/operations/finance/cash-flow-summary
  → SsalddelAdminApp /operations/finance
```

응답은 다음을 제공한다.

- 실제 `현금이동일시Utc`가 있고 `가용현금` 관리계정 전기가 있는 사건의 현금 유입·유출·순변동
- 조회 기간에 발생한 미수·지급의무·고객 환불의무 관리 후보의 순변동
- 현금 이동 사건 수와 기간 재무사건 수
- 결정적 원장 상태 사본 hash
- 기초 현금 잔액 미포함에 따른 가용현금 총액 확정 불가 표시
- 운영 전표 쓰기 비활성 표시

## 운영 경계

- 결제 승인이나 미수 발생을 현금 유입으로 추정하지 않는다.
- `현금이동일시Utc`만 있어도 현금 전기가 없으면 현금 이동 집계에서 제외한다.
- 기초 잔액 원장이 없으므로 현재 가용현금 총액을 만들지 않고 기간 순변동만 보여 준다.
- 관리계정은 실제 외부 회계 계정과 전표 승인이 아니며 송금·환불·정산을 실행하지 않는다.
- 관리자 모바일 화면은 오늘·최근 7일 읽기만 제공하고 첫 운영 개요에 재무 숫자를 전면 노출하지 않는다.

## 검증

- `재무영향원장PipelineTests`: 8/8 통과
  - 현금 유입 150,000원, 유출 40,000원, 순변동 110,000원
  - 기간 미수 200,000원, 지급의무 90,000원, 환불의무 20,000원 분리
  - 잘못된 기간 거절
  - 기초 잔액을 추정하지 않는 제한 확인
- `SsalddelAdminApp` Windows 대상 build: 성공, 경고 0, 오류 0
- Task 검증의 `Ssalddel.v0.0.slnx` build: 성공
- 재무 원장과 API 판본 메타데이터 집중 시험: 64/64 통과
- 재무 영향 코드 지도 생성기: 현재 소스와 일치

Task 전체 시험은 이번 변경과 직접 관련 없는 기존 6건 실패와 뒤이은 테스트 호스트 CLR 오류로 완료되지 않았다. 대표 실패는 기존 공식 재료 화면의 44px CSS 기대, 기존 World 관점 Controller 업무 영역·Audience 메타데이터, 기존 업무 실행 책임 문구 기대다. 이번 변경 범위의 집중 시험에서는 실패가 없다.

## 아직 검증하지 않은 것

- 실제 인증 HTTP 요청과 운영 DB 결과
- MySQL에서의 집계 성능과 실행 계획
- Android 실제 장치·390px 렌더·터치
- 현금 입금·기사 지급 정산 Event projector의 종단 연결
- 실제 회계 계정과 전표·세무·은행 잔액 대사
- 전체 저장소 시험의 기존 6건 실패와 테스트 호스트 CLR 오류 해소
