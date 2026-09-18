# 재무 영향 코드 지도

> `Ssalddel재무영향ProfileAttribute`와 관리계정·프로필 Catalog에서 자동 생성한다. 직접 수정하지 않는다.

설명·검증용 관리 메타데이터이며 실제 전표·지급·세무 신고 권한이 아닙니다.

## 처리 구조

```text
Command/API 특성 (설명·검사)
  → 재무 영향 Profile Stable ID
  → 승인된 매핑 판본과 관리계정 후보
  → Outbox가 발행한 업무 Event
  → 멱등 Projector
  → 재무 사건·관리계정 전기·대사 예외 읽기 모델
```

## 재무 영향 프로필

- `financial.courier-payout-authorized.v1` · CourierPayoutAuthorized · `AuthorizationOnly / BusinessOccurrence` · 매핑 `management-financial-impact.v1` · 운영 전표 쓰기 `False`
  - RelatedAccount: `management.payable.courier.normal`
- `financial.courier-payout-settled.v1` · CourierPayoutSettled · `SettlementCandidate / CashMovement` · 매핑 `management-financial-impact.v1` · 운영 전표 쓰기 `False`
  - DebitCandidate: `management.payable.courier.normal`
  - CreditCandidate: `management.cash.available`
- `financial.customer-refund-recorded.v1` · CustomerRefundRecorded · `ReversalCandidate / BusinessOccurrence` · 매핑 `management-financial-impact.v1` · 운영 전표 쓰기 `False`
  - RelatedAccount: `management.clearing.customer-payment`
  - RelatedAccount: `management.liability.customer-refund`
- `financial.manual-revenue-scenario.v1` · ManualRevenueScenario · `RecognitionCandidate / ReportingAdjustment` · 매핑 `management-financial-impact.v1` · 운영 전표 쓰기 `False`
  - RelatedAccount: `management.revenue.platform-commission`
- `financial.payment-approved.v1` · CustomerPaymentApproved · `RecognitionCandidate / BusinessOccurrence` · 매핑 `management-financial-impact.v1` · 운영 전표 쓰기 `False`
  - DebitCandidate: `management.receivable.pg`
  - CreditCandidate: `management.clearing.customer-payment`
- `financial.payment-prepared.v1` · PaymentPrepared · `AuthorizationOnly / BusinessOccurrence` · 매핑 `management-financial-impact.v1` · 운영 전표 쓰기 `False`

## Command·API 결속

- `Ssalddel.Application.Admin.Settlement.기사지급승인UseCase` → `financial.courier-payout-authorized.v1`
- `Ssalddel.Application.Shipper.Payment.공통결제승인Command` → `financial.payment-approved.v1`
- `Ssalddel.Application.Shipper.Payment.공통결제준비Command` → `financial.payment-prepared.v1`
- `Ssalddel.Application.Shipper.Payment.토스결제승인Command` → `financial.payment-approved.v1`
- `Ssalddel.Application.Shipper.Payment.토스결제준비Command` → `financial.payment-prepared.v1`
- `Ssalddel.Application.Shipper.Payment.페이크결제승인Command` → `financial.payment-approved.v1` · 조건 `SimulationOnly`
- `Ssalddel.Application.Shipper.Request.관리자운송의뢰취소환불Command` → `financial.customer-refund-recorded.v1` · 조건 `RefundRequired`
- `Ssalddel.Controllers.Admin.Settlement.플랫폼이익환원Controller.수익기록` → `financial.manual-revenue-scenario.v1` · 조건 `ManualScenarioInput`
- `Ssalddel.Controllers.Admin.Settlement05.기사지급승인Controller.승인` → `financial.courier-payout-authorized.v1`
- `Ssalddel.Controllers.Shipper.Payment02.화주결제Controller.공통결제승인` → `financial.payment-approved.v1`
- `Ssalddel.Controllers.Shipper.Payment02.화주결제Controller.공통결제준비` → `financial.payment-prepared.v1`
- `Ssalddel.Controllers.Shipper.Payment02.화주결제Controller.토스결제승인` → `financial.payment-approved.v1`
- `Ssalddel.Controllers.Shipper.Payment02.화주결제Controller.토스결제준비` → `financial.payment-prepared.v1`
- `Ssalddel.Controllers.Shipper.Payment02.화주결제Controller.페이크결제승인` → `financial.payment-approved.v1` · 조건 `SimulationOnly`
- `Ssalddel.Controllers.Shipper.Request01.화주운송의뢰Controller.관리자취소환불` → `financial.customer-refund-recorded.v1` · 조건 `RefundRequired`
