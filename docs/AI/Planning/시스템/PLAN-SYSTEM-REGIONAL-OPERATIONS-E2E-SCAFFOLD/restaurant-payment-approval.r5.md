# 음식 주문 결제 승인 반영 r5

- 기획: PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD의 음식점 모바일 후속.
- 상태: Approved. 승인 근거: 2026-09-27 현재 사용자 요청의 계속 구현·실결제 없는 시험 허용.
- 선행: [업무·결제·정산 점검 r3](restaurant-lifecycle-audit.r3.md). r3의 승인 의도 적재와 실제 주문 반영 사이 결손만 이번에 닫는다.
- 작업 책임: operations. 로컬 개발 수용이며 다른 계정으로의 push 인계나 운영 활성화 승인이 아니다.

## 확정 범위

기존 결제 승인 Command → 결제 및 승인 Outbox → 기존 음식 주문 승인 EventHandler → Command알림Outbox의 FoodOrderPaymentApproved → 음식 주문 승인 내역 → 기존 주문 조회를 연결한다. 기존 외부 PG 인터페이스의 시험 대역만 사용하며 실제 PG·지급을 호출하지 않는다.

- 음식 주문에는 결제 승인 ID·금액·통화·시각을 nullable 승인 내역으로 연결한다. 기존 주문은 승인 내역 없음으로 읽는다.
- 이 내역은 과거 승인 사실이지 환불 후 현재 결제 잔액·정산·지급 완료가 아니다. 요청 DTO나 클라이언트가 승인 사실을 정할 수 없다.
- 결제 정본의 대상 종류·주문번호·주문자·승인 상태·금액·통화·시각을 의도 및 주문과 대조한다. 현재 주문 금액 전액의 KRW 승인만 다루며 부분 결제·추가 배달료·분할 결제 정책을 새로 만들지 않는다.
- 취소·거절, 다른 승인과 충돌, 대상/금액 불일치는 자동 반영하지 않고 실패 상태로 남긴다. 자동 환불·귀책·수수료·지급은 결정하지 않는다.
- 중복 승인 의도는 같은 승인으로 한 번만 반영한다. 승인 내역과 Outbox 성공은 관계형 트랜잭션 안에서 함께 확정한다. 실패 재시도·중단 후 lease 재처리는 기존 Outbox 정책을 따른다.
- 주문 수락·조리·배차·수령확인 상태 및 업무 상태 이력 revision을 바꾸지 않는다.
- 신규 내부 소비자는 `FoodOrderPaymentApproval:Enabled` 기본 false다. 검토된 DB migration 적용 및 별도 활성화 전에는 적재된 의도를 소비하지 않는다.
- 공개 응답에는 승인 ID·금액·통화·시각만 추가한다. PG PaymentKey·원본 응답·계좌는 추가하지 않는다.

## 개발 작업 명세

- 허용 경로: 음식주문 Domain/Configuration, 음식주문Dtos, 기존 음식 주문 Store/주문자 조회 Mapper, Payments 내부 소비자/Options, 해당 Quartz Job·기존 DI, 해당 EF migration/정본 snapshot, 해당 집중시험, 이 기획 폴더·PLANNING·CURRENT_WORK.
- 정확 파일 목록과 기획 hash는 `restaurant-payment-approval.r5.work-order.json`에 결속한다. 기존 다른 작업의 변경을 정리·stage하지 않는다.
- 검증 상한: SQLite 관계형 DB + 실제 승인 Handler/Outbox/Event/조회 코드, 외부 PG만 시험 대역. 실제 인증 HTTP·MySQL·MongoDB·Android 화면·실결제·지급 성공으로 확대하지 않는다.
- 필수 시험: 정상 재조회, 중복/새 DbContext 재처리, 금액·통화·대상·소유자·시각 불일치, 미승인/취소·거절, 다른 승인 충돌, 비대상 Outbox 격리, 저장 실패 rollback/재시도, 기본 비활성, 민감정보 응답 제외.
- EF migration은 생성·검사까지만 한다. 기존 DB에 적용하지 않는다. commit·push·배포 없음.

## 후속

음식 주문 취소/거절 → 환불 검토·상태 연결, 완료 주문 → 음식점 정산 대상·조회, 역할 앱 알림/재조회·실기기 확인은 별도 범위다. 이번 승인을 이들 정책의 승인으로 확장하지 않는다.
