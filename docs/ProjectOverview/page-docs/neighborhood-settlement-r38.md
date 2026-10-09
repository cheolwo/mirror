# 생활 거래 정산의 페이지·서버·DB 책임 r38

[승인 기획 r32](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/neighborhood-settlement.implementation.r32.md)와 [실제 검증 기록](../../Changes/2026-10-08-neighborhood-settlement-r38.md)을 따른다. 이번에는 앱 화면을 바꾸지 않고 후속 연결에 필요한 계약과 서버 원장을 구현한다.

## 화면의 후속 책임

| 후속 화면 | 한 가지 질문 | 서버 근거 |
| --- | --- | --- |
| 약정 상세 | 누가 누구에게 어떤 기준으로 지급하는가? | 본인 관련 항목·해당 약정 판본·동의 상태 |
| 작업 비용 상세 | 실제 어떤 작업에 얼마가 발생했는가? | 원본 완료 수량·처리자·사진 결속·조정 이력 |
| 지급 확인 | 지급 신고와 수령 확인이 어디까지 됐는가? | 금액별 배분·수령자 확인·남은 금액 |

업무 카드에 전체 정산을 넣거나 기존 음식·화물 수행 화면을 통합하지 않는다. 앱 구현은 보류했으며 이 표는 화면 완료 증거가 아니다.

## 서버와 계약

`Ssalddel.Contracts/Common/Settlement/NeighborhoodSettlementDtos.cs`는 `PreparedFood / StoredGoods / TransportOnly`, 약정·비용·지급 진술을 구별한다. `Ssalddel/Controllers/Common/NeighborhoodSettlementController.cs`의 인증된 `/api/v1/neighborhood-settlements`가 아래 명령을 제공한다.

| 동작 | 경로 | 책임 |
| --- | --- | --- |
| 본인 목록·상세 | GET 기본 경로 / `{id}` | 본인 payer/payee 금융 범위와 잔액 |
| 약정·새 판본 | POST 기본 경로 / `{id}/terms` | 원본 당사자·활동 종류·숫자 조건 검사 |
| 동의 | `{id}/consents` | 같은 판본의 영향받는 지급자·수령자 동의 |
| 비용 발생 | `{id}/accruals` | 실제 완료 origin과 수량/기간 검사 |
| 비용 확인·분쟁·조정 | `{id}/entry-confirmations`, `disputes`, `adjustments` | 기존 금액 보존·양측 확인·추가 조정 |
| 지급 신고·수령 확인 | `{id}/payment-reports`, `payment-receipts` | 부분 금액 배분·지급자 신고·수령자 확인 |
| 응답 유실 확인 | GET `receipts/{requestId}` | 같은 actor/request/payload의 명령 영수증 |

명령은 GUID `RequestId`·`ExpectedRevision`을 받는다. 같은 요청 재실행은 원래 영수증으로 복구하며 다른 payload나 동시 판본 충돌은 재조회하도록 실패한다. Controller는 인증만 전달하고 `NeighborhoodSettlementUseCase*.cs`가 업무 조건과 트랜잭션을 소유한다.

`NeighborhoodSettlementSourceResolver.cs`는 분산 Lot/주문·음식 주문·화물 의뢰를 읽는다. 상품 주문총액과 기사에게 제안한 운임은 다르며, 분산 Wave 전체 운임을 개별 주문에 반복하지 않는다. 분산 주문의 `OrderReference`는 자유 문자열이므로 상품 가격 연결 키로 쓰지 않는다. 입고/피킹은 실제 재고이력·actor·사진 결속·완료 영수증을 대조한다. 주문별 수량을 복원할 수 없는 묶음 피킹은 주문별 실적으로 임의 분배하지 않는다.

## DB 책임

`Ssalddel.Domain/정산/생활거래정산Record.cs`와 `Ssalddel.Infrastructure/Persistence/Configurations/Settlement/생활거래정산Configuration.cs`가 다음 다섯 테이블을 연결한다. 기존 저장 보호 변환을 적용해 금융 금액·당사자·동의·배분·근거 JSON을 평문으로 복제하지 않는다.

| 테이블 | 소유하는 사실 |
| --- | --- |
| `neighborhood_settlement_agreements` | 현재 약정·판본·상태·Revision |
| `neighborhood_settlement_versions` | 각 판본의 불변 약정과 원본 근거·동의 |
| `neighborhood_settlement_entries` | 발생 항목·업무 origin·양측 확인·조정 |
| `neighborhood_settlement_payments` | 지급 신고·항목별 배분·수령자 확인 |
| `neighborhood_settlement_receipts` | actor/request와 입력 지문의 멱등 영수증 |

새 migration은 코드로만 제공한다. 공용 DB에 적용하지 않는다. 기존 음식 기사 정산·화물 지급 요청·outbox는 읽기만 하며 기존 배송 canonical origin의 중복 참조와 신규 지급을 막는다.

## 금액과 완료의 의미

경제 합계는 같은 사람의 내부 항목, 소유자에게 전달하는 통과자금, 기존 기사 정산 참고 항목을 제외한다. 통과자금의 실제 신고·수령 내역과 의무는 항목·지급 목록에서 따로 유지한다. 경제 합계는 본인 관련 채무 규모이며 방향 없이 본인 순수입으로 읽으면 안 된다. 후속 화면은 지급할 금액과 받을 금액을 구별해야 한다.

모르는 금액은 null, 무료는 명시적으로 합의한 0이다. 음식점 운영 접근권을 상품 소유권으로 바꾸지 않는다. 과거 상품대금이나 소유권 근거가 없는 업무는 해당 금액/지급 경로를 보류한다. 보관 기간은 사진·완료 영수증으로 대조한 입고/피킹/반환 이력이 현재 물품 수량과 일치하는 연속 구간만 사용한다. 전량 피킹 뒤 반환 전의 공백은 보관 기간이 아니며, 구간의 확인된 최소 수량을 넘겨 청구하지 않는다. 개별 수령 확인 시각이 없는 분산 주문은 그 시각을 null로 유지한다.

배송비 공동 부담은 기존 기사 운임 하나에 연결한 `DeliveryCostShares` 합의 계획으로 구분하고 분담자의 동의를 받는다. 이 계획 자체가 새 기사 지급이나 실제 송금이 되지는 않는다. 배송료에 포함한 입고/피킹은 별도 유료 항목으로 받지 않는다. Lot와 주문에서 같은 피킹을 조회해도 동일한 재고 이력 ID를 사용해 중복 청구를 막는다.

감액은 원본을 지우지 않는 음수 조정이다. 환급 신고는 기존 수령자가 원래 지급자에게 하는 별도 진술이며, 양측이 확인한 감액과 이미 수령 확인된 돈의 한도를 넘지 못한다. 환급 수령 확인액을 빼서 순수령액과 잔액을 계산한다. 미지급 비용을 감액했다고 가상의 환급 입금이 생기지는 않는다.

기능은 `NeighborhoodSettlement.Enabled=false`가 기본이고 정확한 `neighborhood-settlement-r38` profile·Simulation·관계형 DB에서만 작동한다. 실제 입금·은행 확인·일반 세법이나 보험 적용을 인증하지 않는다. 앱·APK·휴대폰·Azure·실운영은 후속이다.
