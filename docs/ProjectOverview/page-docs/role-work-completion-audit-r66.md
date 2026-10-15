# 역할별 업무 시작·완료 연결 검증 r66

기준일: 2026-10-12. 기준 HEAD: `08931c2371f29de615f4a4f133b535e5df76bf7f`, 작업 브랜치: `dev/mirror-integration`.

## 판단

정상적인 주문·입고·피킹·전달·수령 확인은 상당 부분 연결돼 있다. 그러나 사용자 화면에서 지급 신고를 시작할 수 없고, 공간 사용 종료나 수락 후 주문 취소처럼 예외를 실제 업무 원장에 반영해 끝내는 절차가 남아 있다. 입고 계획은 최종 사실이 같아도 처리 순서에 따라 완료 상태가 달라지는 문제를 격리 SQLite에서 재현했다.

이번 작업은 현재 코드·기존 시험과 제한된 재현으로 확인한 조사다. 제품 코드 수정, 새 DB 구조, 운영 설정 변경, 실제 계약·송금, APK 설치, 휴대폰·Azure·Unity 검증, 커밋·푸시는 수행하지 않았다. 조사 시작 당시 미커밋 항목 163개와 기존 제품 파일을 보존했다. 전체 앱의 모든 화면·상태 조합을 전수 실행했다고 주장하지 않는다.

## 역할별로 확인한 연결

| 입장 | 시작에서 마무리까지 확인한 절차 | 남은 마무리 |
|---|---|---|
| 음식 주문자 | 주문 조건·접수 확인 → 수락 전 직접 취소 또는 진행 조회 → 기사 전달 뒤 본인 수령 확인 | 음식점 수락 후 취소 협의의 결과를 실제 주문에 반영하는 운영 명령 |
| 음식점 | 주문 인지·수락 → 유효한 기사 배정 후 조리 → 현재 조리 회차의 픽업 준비 → 기사 인계·수령 상태 조회 | 조리·픽업 이후 취소 비용과 작업 책임을 합의해 종결하는 절차 |
| 음식 기사 | 제안 수락 → 가게 도착 → 준비 완료된 음식 픽업 → 전달 → 완료 내역·별도 정산 상태 조회 | 장기간 수령 미확인인 건의 운영자 인계·정산 판단 |
| 공급자·판매자 | 품목·수량 제안 → 같은 판본의 양측 동의 → 입고 예약 → 실제 수량·사진 입고 → 재고와 정산 근거 | 일부 입고/일부 취소의 계획 완료 판정, 남은 재고의 계약 종료 반출 |
| 공간 제공자·관리자 | 수용 조건·보관 계약 → 입고 행 승인 → 실제 수령·재고 확인 → 보관 및 작업 근거 | 공간 제공 중단 뒤 보관 중인 물건을 누가 언제 어디로 반출할지 확정·인계 |
| 입고·피킹 작업자 | 허용된 업무 선택 → 바코드·품목·실수량 확인 → 사진 전송 → 완료 행·재고 이력·요청 영수증 저장 | 실패 배송·장기 미수령 때 이미 수행한 작업비의 예외 종결 |
| 생활 물품 배송기사 | 주문 재고 예약·배송 묶음 → 수락 → 현재 방문지의 사진 피킹 → 상황에 따른 피킹/전달 → 사진 전달 완료 | 최종 수령이 안 된 경우 반품·비용 합의로 끝내는 정책 |
| 생활 물품 수령자 | 본인 주문 조회 → 기사 전달 사실 확인 → 본인 수령 확인 | 수령 문제를 지원 사건과 실제 정산 결정에 연결하는 끝 절차 |
| 화물 기사·화주 | 조사한 완료 구간에서는 하차 사진·실화물 확인 → 운송 완료 인계 → 화주의 별도 인수 확인 | 이번에는 화물 전체 시작 화면·계약·지급 흐름을 전수 실행하지 않음 |
| 보수 지급자·수령자 | 작업 근거 → 보수 발생 → 금액 확인 → 서버의 지급 신고 → 수령자 확인 | 지급 신고 화면 진입, 잘못된 신고의 정정 경로 |
| 운영자 | 사건 접수 → 담당자 검토 → 당사자별 결과 안내 확인 → 사건 종결 | 사건 종결과 주문 취소·재고 반출·정산 확정 사이의 구체적인 업무 명령 |

서로 다른 완료는 합치지 않는다. 기사 전달과 고객 수령, 작업비 발생과 지급 조건 충족, 지급 신고와 수령 진술, 지원 사건 종결과 실제 업무·입금 완료는 별개다. 지급 신고·수령 진술도 은행 입금 검증은 아니다.

정상 연결의 코드 근거:

- 음식: [음식점 화면 동작](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/RestaurantRoleWorkspaceAdapter.cs) 35–62행, [조리 시작 조건](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) 43–54행, [기사 수행·중단 복구](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) 536–587·673–688·740–749행, [주문자 수령 확인](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) 325–379행. 픽업 전 중단은 조리 이력을 보존해 재배차하고, 픽업 후 중단은 재조리와 새 배달 시도로 연결한다.
- 물품·공간: [입고 협의](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.InboundCollaboration.cs) 19–98행, [사진 수령·피킹·전달·반품](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Batch.cs) 18–210행, [배송 묶음·수락](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Waves.cs) 26–105·186–230행. 예약·승인은 실제 입고·작업비 발생으로 간주하지 않는다. 미피킹 취소와 이미 픽업한 물건의 반품을 구분한다.
- 정산: [작업별 지급 관문](../../../Ssalddel/Application/Settlement/NeighborhoodSettlementUseCase.Workflow.cs) 50·91–118행, [부분 지급·수령](../../../Ssalddel/Application/Settlement/NeighborhoodSettlementUseCase.Payments.cs) 31–106행, [정산 응답 유실 복구](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/활동정산PageViewModel.cs) 129행 이후. 입고·보관 보수와 출고·배송 보수의 지급 시점을 구분하고, 불명확한 응답은 기존 요청 영수증·원장 재조회로 해결한다.

## 우선 보완할 네 곳

### 1. 지급자가 앱에서 지급 신고를 시작할 수 없다

서버 [정산 Controller](../../../Ssalddel/Controllers/Common/NeighborhoodSettlementController.cs) 40행과 [Payments](../../../Ssalddel/Application/Settlement/NeighborhoodSettlementUseCase.Payments.cs) 31–59행에는 지급자 권한·판본·확정 금액·지급 조건·미지급 상한을 검사하는 신고 기능이 있다.

그러나 [앱 Client](../../../Ssalddel.Ui.Common/Areas/App/Services/활동정산Client.cs) 18–30행에는 신고 메서드가 없고, [공통 정산 화면](../../../Ssalddel.Ui.Common/Areas/App/Components/Settlement/활동정산Screen.razor) 115행 이후는 기존 신고의 조회와 수령 확인만 제공한다. 제품 `.cs/.razor/.js`의 `payment-reports`, `NeighborhoodSettlementPaymentReportRequest`, `ReportPaymentAsync` 교차 검색에서도 다른 화면 호출을 찾지 못했다. 서버를 직접 호출하는 기존 시험은 통과하지만 앱 사용자는 이 단계를 시작할 수 없다.

보완 범위는 **이미 지급한 금액·근거를 지급자가 신고하도록 기존 API를 연결**하는 것이다. 실제 송금 기능이나 새 결제 시스템을 추가할 필요는 없다. 조사 분류: 화면 연결 누락.

### 2. 같은 입고 결과의 완료 상태가 처리 순서에 따라 다르다

독립된 인메모리 SQLite 두 개에서 기존 UseCase·시험 fixture를 재사용했다. 두 입고 행 A/B가 각 2개 예정인 상황이다.

| 처리 순서 | 최종 A | 최종 B | 가용 재고 | 계획 상태 |
|---|---|---|---:|---|
| A 전량 수령 → B 취소 | 2/2개 수령 | 0/2개·취소 | 2개 | `PartiallyReceived` |
| B 취소 → A 전량 수령 | 2/2개 수령 | 0/2개·취소 | 2개 | `Received` |

행별 사실과 재고는 동일하고 계획 상태만 다르다. [취소 처리](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.InboundCollaboration.cs) 113–114행은 모든 행이 취소·거절된 경우만 계획 상태를 바꾼다. [수령 처리](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Batch.cs) 61–62행은 취소·거절 행을 제외한 수량으로 완료를 계산한다.

수령·취소·거절 뒤 같은 최종 사실로 계획 상태를 계산하도록 모으고, 두 처리 순서를 모두 회귀 시험으로 보호하는 보완이 필요하다. 새 원장·테이블은 필요하지 않다. 조사 분류: **현재 동작 결함·격리 실행 재현 완료**. 실제 카메라 대신 명시적 합성 사진 증빙을 사용했으며 외부 DB/API나 운영 설정은 사용하지 않았다.

### 3. 공간 사용 종료 뒤 남은 재고의 반출을 마무리할 길이 부족하다

[공간 동의 철회](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.cs) 177–191행은 물건이 남으면 `RevocationPendingCustody`로 보호한다. [새 예약](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Waves.cs) 48–52행과 [계약 검사](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Agreements.cs) 106–112행은 비활성 공간의 새 업무를 차단한다. 물건을 지우거나 보관 책임을 없애지 않는 정상 보호다.

현재 [반품](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Batch.cs) 170–194행은 배송 예약과 `ReturnRequired`를 바탕으로 원래 공간에 돌려놓는 절차다. 공간 사용 종료로 **미판매 재고를 공급자에게 돌려주거나 다른 공간으로 옮겨 최종 인계하는 절차**와 다르다. 이 내용은 [기존 공간 검토 r48](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/apartment-shared-space.review.r48.md) 27행에서도 후속으로 남았다.

반출 요청·담당자·기한·실수량·사진 인계·남은 보관비를 연결하는 종료 절차가 필요하다. 조사 분류: 알려진 예외 종결 미구현. 기존 보호를 해제해 해결할 문제가 아니다.

### 4. 음식점 수락 후 취소 협의가 실제 주문 취소까지 이어지지 않는다

[주문 상세](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderDetailPanel.razor) 123–127행은 수락 후 취소를 지원 협의로 안내한다. [직접 취소](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) 409–422행은 주문대기만 허용한다. [사건 종결](../../../Ssalddel/Services/PrivacySupport/보호지원UseCase.cs) 236–247행은 당사자 안내 뒤 사건만 닫는다.

[현재 음식 운영 명령](../../../Ssalddel/Controllers/Admin/Food/음식주문운영추적Controller.cs) 41–82행과 [수행 취소 Adapter](../../../Ssalddel/Services/Food/음식수행원천Adapter.cs) 123–145행을 대조했으나, 협의 결과가 취소일 때 음식 주문·현재 배차·기수행 작업·비용 책임을 함께 확정하는 명령은 조사 범위에서 찾지 못했다. 수행 취소와 원주문 취소도 다르다.

조리 전·조리 후·픽업 후의 책임과 보상 기준을 먼저 정하고 운영자가 근거를 확인해 해당 업무를 종결하는 절차가 필요하다. 지원 사건을 닫았다는 이유로 자동 취소하면 안 된다. 조사 분류: 운영 종결 명령·정책 공백.

## 그다음 정리할 부분

| 항목 | 현재 보호와 남은 부분 | 근거 |
|---|---|---|
| 장기 수령 미확인·실패 배송의 보수 | 수행 사실은 보존하지만 수령 전 지급을 대기한다. 수령을 위조하지 않고 운영자에게 인계해 작업비·손실을 합의 종결할 기준이 필요하다. | [음식 정산](../../../Ssalddel/Application/Food/음식주문기사정산Recorder.cs) 80–90행, [지연 판정](../../../Ssalddel/Application/Admin/Food/음식배달운영지연판정Policy.cs) 48–75행, [활동 정산 관문](../../../Ssalddel/Application/Settlement/NeighborhoodSettlementUseCase.Workflow.cs) 91–118행, [r60 후속](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.implementation.r60.md) 28행 |
| 잘못된 지급 신고의 정정 | 신고 금액은 추가 신고 한도를 차감하고 수령 확인은 누적만 한다. 실제로 지급하지 않은 금액을 잘못 신고했을 때 원신고를 보존하면서 철회·교정할 명령이 없다. 보수 채무 조정과는 별개다. | [Payments](../../../Ssalddel/Application/Settlement/NeighborhoodSettlementUseCase.Payments.cs) 57–59·99–106행 |
| 보관 협의 입력 중 새 판본 수신 | 실제 조건이 같아도 입고·예약에 따른 판본 증가가 초안을 초기화할 수 있다. 작성 중인 입력을 보존하고 새 정본과 재확인하도록 정리할 여지가 있다. | [보관 합의 화면](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/DistributedFulfillment/분산보관합의Screen.razor) 62–77행, [예약](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Waves.cs) 95행 |
| 입고 가능 안내와 실제 공간 용량 | 실행 시 실제 용량 검사는 있으나 조회 안내에 같은 조건이 반영되지 않아 버튼을 누른 뒤 차단될 수 있다. 재고 보호는 유지하고 사전 안내를 맞출 필요가 있다. | [입고 조건·조회 투영](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.InboundCollaboration.cs) 262–295·312행, [조회](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Read.cs) 99–102행 |

## 실행 증거와 한계

| 검사 | 이번 결과 | 뜻하는 범위 |
|---|---|---|
| 현재 OS 카탈로그 검사 | 10 OS·10 생명주기·62 책임 단계, 63개 검사 통과·오류 0 | 선언의 일관성. 실행 전이표나 모든 역할의 완결 증거는 아님. 별도 단계 감사 입력은 사용하지 않아 `auditedOsCount=0`·`mappedStageCount=0` |
| 기존 집중 시험 | 39개 시험 클래스, **620건 통과·실패 0·건너뜀 0** | 음식 정상/중단/수령, 분산 입고·재고·정산, 공통 화면·복구, 조사한 화물 완료/인계 구간. 없는 화면·미구현 예외는 시험 통과로 입증할 수 없음 |
| 입고 순서 재현 | 독립 SQLite 두 시나리오 모두 실행 성공, 동일 행·재고/상이한 계획 상태 확인 | 결함 재현. 제품 수정·회귀 시험 추가 완료는 아님 |
| 변경 보존 | 제품 파일 해시·HEAD·index를 조사 시작 기준과 대조 | 보고서·현재 작업 안내 외 기존 작업을 변경하지 않음 |

집중 시험은 직전 r65에서 빌드·전체 시험이 성공한 산출물을 사용해 `--no-build --no-restore`로 실행했다. 이번에는 제품을 재빌드하지 않았다. 실제 입고·배송·외부 송금·운영 중 장애 복구나 휴대폰 사용성까지 검증한 결과가 아니다. 일부 기능의 기본 OFF·Simulation 관문은 그대로 유지했다.

로컬 실행 기록은 `artifacts/local/role-work-completion-r66/`에 보관한다. `catalog-validation.json`, `test-filter.txt`, `test-evidence.json`, `test-results/role-completion.trx`, `order-probe/result.json`에 범위와 결과가 있다. 사진 재현은 합성 증빙이며 개인정보·실제 고객 화면을 사용하지 않았다.

보완 순서는 **기존 지급 신고의 화면 연결 → 입고 계획 상태 일관성 → 공간 재고 반출 → 음식 수락 후 취소 및 예외 정산 종결**이 적절하다. 앞의 두 항목은 이미 있는 기능과 상태를 정리하는 작업이고, 뒤의 두 항목은 책임·비용 정책을 확정해야 하는 작업이다. 이번 조사는 보완 구현의 승인이나 운영 활성화로 대신하지 않는다.
