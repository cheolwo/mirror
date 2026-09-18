# [기획 · 운영·재무 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 구현 r2]

## 결과

기존 결제·환불·기사 지급·수기 수익 Command와 API가 어떤 관리 재무 의미를 가질 수 있는지 `Ssalddel재무영향ProfileAttribute`로 표시하고, 승인된 Profile Catalog와 멱등 Projector가 실제 읽기 모델을 만드는 첫 세로 절편을 구현했다.

이 특성은 설명·탐색·검사용이다. 특성이 붙었다는 사실만으로 실제 회계 전표, 지급, 환급, 세무 신고를 실행하지 않는다.

## 처리 흐름

```text
Command/API 재무 영향 특성
  → Profile Stable ID
  → 관리계정 후보·금액 근거·인식 시점·매핑 revision 조회

결제 승인 Command
  → 기존 결제 승인 원장 + 결제승인완료Outbox
  → 기존 Outbox 발행
  → 결제승인재무원장ProjectorEventHandler
  → 재무사건 + 관리계정전기 + 증빙
  → 관리자 읽기 API·원장 기반 운영경제성 Preview
```

`매핑 revision`은 같은 업무 사건을 어떤 관리계정 후보로 해석했는지 재현하기 위한 규칙 판본이다. `Projector`는 Outbox에서 발행된 확정 업무 Event를 읽고 원본을 바꾸지 않은 채 조회용 재무 사건과 전기 사본을 만드는 처리기다.

## 구현 범위

- 관리계정 Catalog: 가용현금, PG 미수, 고객 결제 정산대기, 음식점·기사·지사 미지급, 환불 의무, 수익·비용 후보, 미대사 가계정.
- 재무 영향 Profile: 결제 준비, 결제 승인, 고객 환불 상태 기록, 기사 지급 승인, 기사 지급 정산, 수기 수익 Simulation.
- 결제 승인 투영: `PG 미수 DebitCandidate / 고객 결제 정산대기 CreditCandidate` 두 줄을 만든다. 결제 승인만으로 현금 입금이나 플랫폼 매출을 확정하지 않는다.
- 멱등·대사: 같은 원본 revision은 중복 생성하지 않으며 같은 Stable ID의 금액·통화·hash가 달라지면 기존 사건을 덮어쓰지 않고 대사 예외를 남긴다.
- 관리자 조회: `api/v1/admin/operations/finance` 아래 metadata, 사건, 관리계정 잔액, 대사 예외 조회를 제공한다.
- 운영경제성: `POST api/v1/admin/operations/economics/evaluate-from-ledger`가 서버 재무 사건에서 총거래액을 읽는다. 요청자가 총거래액을 덮어쓸 수 없고 비용·수익 가정만 별도 시나리오 항목으로 제출한다.
- 수기 수익 입력: 기존 `platform-profit-returns/revenues`는 `SsalddelExecution:Mode=Simulation`에서만 허용한다.
- 영속화: `재무사건`, `관리계정전기`, `재무사건증빙`, `재무대사예외`와 MySQL migration을 추가했다.
- 탐색 지도: `eng/Ssalddel.FinancialImpactMap`이 Profile·관리계정·Command/API 결속을 JSON과 Markdown으로 결정적으로 생성한다.

## 경계

- 모든 관리계정은 실제 회계계정 미승인이고 모든 Profile은 `OperationalPostingAllowed=false`다.
- 실제 PG 입금, 은행 잔액, 기사 송금, 음식점 정산, 환급 실행, 본인·대리인 판정, 수익 인식, 세무 신고를 추가하지 않았다.
- 지급 승인과 지급 완료를 구분한다. 현재 Simulation 지급 gateway 성공을 현금 이동으로 투영하지 않는다.
- 기존 결제 원장과 Outbox를 재사용하며 API 특성이나 조회 Projector가 원 업무 상태를 변경하지 않는다.

## 검증

- 재무 Catalog 무결성, API/Command 특성, 결제 승인 두 줄 투영, 재처리 멱등성, 원본 충돌 대사 예외, 원장 기반 총거래액, 총거래액 덮어쓰기 거절, Simulation 수기 수익 경계와 EventHandler 명명 규칙을 포함한 집중 시험 `20/20`이 통과했다.
- `Ssalddel.v3.5.slnx` Fast build와 `git diff --check`가 통과했다.
- 재무 영향 생성 지도의 재생성 후 소스 일치 검사가 통과했다.
- Fast 자동 선택 시험 `120`건 중 `116`건이 통과했다. 실패 4건은 이번 재무 범위 밖의 기존 World 관점 Controller 업무영역·Audience 3건과 기존 `Hs식품국가가격Card조회` 명명 1건이다.
- Task 전체 서버 시험은 `5,330/5,337`이 통과했다. 남은 7건은 위 4건과 기존 공식 재료 좁은 폭 CSS, 업무 책임 문서 호환 문구, WebApp 지도 선택기 capability 누락이다. 이번 EventHandler 명명 실패는 수정 후 회귀 통과했다.
- 실제 MySQL migration 적용, PG·은행 대사, 운영 전표, 관리자 앱 화면, 실제 지급은 검증하지 않았다.

## 다음 우선순위

1. 실제 MySQL 격리 DB에서 결제 승인 Outbox 재처리와 재무 사건 migration을 검증한다.
2. 음식 배달 완료·정산 정책에 근거한 음식점 미지급과 기사 수행대금 의무 Profile을 별도 승인한다.
3. PG 정산 입금 Event가 생긴 뒤에만 가용현금 이동 Projector를 추가한다.
4. 회계사 검토 판본과 본인·대리인 판단이 확정되기 전에는 수익 후보를 실제 매출로 승격하지 않는다.
