# 배차 제안은 공제 전 총 배달료만 표시 r7

- 기획 ID: `PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL`
- 판본: `dispatch-total-only.direction.r7`
- 상태: `DispatchGrossTotalOnlyConfirmed / ExistingCalculationAndDisplayVerifiedInSource / ExistingLedgerExtensionPreferred / RuntimeNotVerified`
- 사용자 최신 확인: 배차 전에 기사에게 보여 줄 배달료를 계산한다. 배차 화면은 총 배달료만 보여 주며, 기존 원장 기록을 활용한다.

## 확정 기준

1. 서버가 배차 제안을 만들기 전에 건별 지급 구성과 총 배달료를 계산한다. 제안 총액과 가격 판본을 기존 운송 원장에 동결한다.
2. 배차 제안 카드에는 **공제 전 총 배달료 한 개**만 표시한다. 픽업/전달/거리/할증별 금액이나 보험·세금 계산식을 제안 카드에 늘리지 않는다. 주문자가 낸 배달비·음식 주문금액과 구분한다.
3. 해당 건에 이미 포함된 할증·매장 프로모션은 총액 안에 한 번만 반영한다. 별도 기간 미션·건수/달성 보상과 기간 공제는 배차 총액에 예상으로 덧붙이거나 차감하지 않는다.
4. 세부 구성·거리 근거·정책 판본·계산 시각은 원장 및 완료 내역의 정산 상세에서 확인한다. 수락 뒤에는 현재 정책으로 제안 금액을 소급 재계산하지 않는다.
5. 기존 운송·지급 원장을 먼저 재사용한다. 원장을 새로 만들어 모든 총액을 다시 기록하는 것을 첫 작업으로 삼지 않으며, 항목별 근거나 기간 공제에서 확인된 결손만 보완한다.

## 현재 코드 확인

| 단계 | 현재 근거 | 확인 범위 |
| --- | --- | --- |
| 서버 지급액 계산 | `Ssalddel/Services/Dispatch/Recommendation/음식배달기사제안요금Service.cs`, `Ssalddel.Domain/음식/음식배달기사제안요금Policy.cs` | 현재 기본+거리·기상·한시 수요 할증의 총액 계산 |
| 제안 금액 동결 | `Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs`의 `음식배달제안요금동결Async` | 기존 `기사지급예정액`, 기본거리 합산, 기상/수요 할증, 가격 판본·계산 시각 저장; 이미 판본이 있으면 유지 |
| 기사 조회 | `FoodDeliveryDriverWorkService` → `FoodDeliveryDriverWorkspaceUseCase` → `FoodDeliveryDriverOfferDto.DriverPayout` | 원장 지급 총액을 전달. 과거 총액 결손의 기존 fallback은 별도이며 이번에 확정 금액으로 승격하지 않음 |
| 제안 카드 | `DriverApp/Components/Pages/Driver/03_Progress/음식배달업무Page.razor` | 현재 `offer.DriverPayout` 총액 한 개 표시; 항목별 계산식을 배차 카드에 표시하지 않음 |

확인은 소스 대조다. 현재 서버·기기에서 특정 실제 배차를 수행한 증거가 아니다. 세부 픽업/전달/거리 지급액이 모두 별도 영속 필드에 저장됐다는 뜻도 아니다. 현행 `기사기본거리지급액`은 기본+거리 합산이다.

## r6 계산 기반의 다음 연결

[r6 계산 검토](calculation-review.implementation.r6.md)는 항목별 요금과 기간 정산을 비교하는 관리자 검토 API다. 아직 배차 제안 생성기에 새 요율을 적용하지 않았다. 다음 구현에서는 기존 제안 계산 Service와 기존 원장을 중심으로 승인된 정책·거리·동시 픽업 근거를 결속하고, 계산 총액을 기존 `DriverPayout`으로 전달한다. 기존 기사 제안 카드의 총액 표시를 그대로 사용한다. 원장 사본과 정산 상세의 결손 보완은 이 연결에 필요한 범위로 진행한다.

이번 확인에서는 제품 코드·운영 요율·기존 저장 금액을 변경하지 않았다. r5 완료 상세의 항목별 표시는 그대로이며 배차 카드의 총액 전용 기준과 구분한다. r6의 계산·HTTP 검증을 새 배차 요율의 적용 검증으로 확대하지 않는다.
