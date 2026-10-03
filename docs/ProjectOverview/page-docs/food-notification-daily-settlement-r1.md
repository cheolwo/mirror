# 음식 새 업무 알림·전표 복구·당일 정산 r1

2026-10-03, `dev/mirror-integration`. 기존 음식점·기사 앱에서 새 업무를 알아차리고 같은 서버 상태로 돌아오는 흐름과, 기사 본인의 전달 완료일별 정산 조회를 연결한다. 최종 r7 소스는 동결했으며 관련 시험 97/97·전체 3.5 빌드·검토용 Debug APK 설치와 격리 Android 실행을 확인했다. 전체 Task는 기존 실패 7건이 남은 6,259/6,266이며 전체 게이트 미통과다. 실행 범위와 한계는 [변경 기록](../../Changes/2026-10-03-food-notification-daily-settlement-r1.md)에 있다.

페이지 구성은 [페이지 단일 책임 기준](../../Architecture/WholeRoadmapPagePrinciple.md), [책임 카드 형식](page-responsibility-template.md), [역할 앱 시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 따른다. 주문 확인·조리와 기사 배차·수락·완료 권한은 기존 업무에 두며 알림과 정산 조회가 그 명령을 대신 실행하지 않는다.

## 페이지 책임

| 페이지·영역 | 책임과 기본 표시 | 주 행동·보조 행동 | 실패·복귀 경계 |
| --- | --- | --- | --- |
| 음식점 `/orders` | 음식점 운영자가 서버의 미처리 주문을 찾아 다음 업무로 진입한다. 미처리 상태, 목록, 새로고침, 조회 오류를 표시한다. | 주 행동은 주문 보기다. Android 기기 알림 허용은 보조 행동이다. | 알림 권한 거절은 주문 조회·수락 권한을 바꾸지 않는다. 알림을 받았더라도 서버에서 해당 음식점의 최신 주문을 확인한다. |
| 음식점 `/orders/{OrderNo}` | 선택 주문의 최신 상태에 따라 확인·조리·준비를 진행한다. | 독립 전표 출력은 같은 주문의 보조 행동이다. 실패한 전표 출력만 재시도한다. | 전표 복구는 서버에서 다시 준비한다. 취소·거절·미수락 상태나 조회 실패에서는 오래된 전표를 출력하지 않는다. 인쇄 UI 요청을 물리 출력 완료로 표시하지 않는다. |
| 음식점 `/login` | 인증만 담당한다. | 같은 소유 계정으로 인증한 뒤 보류한 알림 대상의 서버 상태를 조회한다. | 다른 계정은 대상 연결을 버리고 목록으로 간다. 명시 로그아웃은 보류 대상·시스템 알림·중복 대장을 정리한다. 알림 탭은 수락·조리를 자동 실행하지 않는다. |
| 기사 배달 영역 | 신규 수신 의사가 켜져 있고 서버에서 유효한 추천이 있을 때 새 업무를 알린다. 기존 현재 배달과 신규 추천을 구별한다. | 알림 탭은 최신 업무 공간을 조회한 뒤 같은 추천을 찾는다. 제안 수락은 기존 명시 행동이다. | 만료·철회·계정 변경·신규 수신 OFF 뒤의 대상은 제거한다. 로컬 알림 ID를 유효한 배차나 수락 근거로 사용하지 않는다. |
| 기사 정산 영역 | 한국 시간 전달 완료일의 전체 완료 원장과 배달료·공제·수령액 확인 상태를 표시한다. | 날짜 선택과 새로고침이 주 행동이다. 주문별 `요금 구성 보기`는 보조 상세다. | 날짜·계정 변경은 이전 요청을 취소하고 늦은 응답을 제외한다. 전체 조회 계약이나 행 소유가 맞지 않으면 결과를 채택하지 않는다. 조회 실패 시 이전 결과라는 상태를 표시하며 최종 401은 기존 인증 복귀로 처리한다. |

## 페이지 → 코드·API → DB

| 업무 | 페이지·앱 코드 | API·서버 코드 | 저장 근거 |
| --- | --- | --- | --- |
| 음식점 새 주문 알림·복귀 | [알림 Coordinator](../../../RestaurantDeskApp/Services/RestaurantOrderNotifications.cs), [알림 이동](../../../RestaurantDeskApp/Components/RestaurantNotificationNavigation.razor), [주문 목록](../../../RestaurantDeskApp/Components/Pages/OrderInbox.razor), [Android 알림](../../../RestaurantDeskApp/Platforms/Android/AndroidRestaurantOrderNotifications.cs) | 기존 [음식 주문 Client](../../../RestaurantDeskApp/Services/I음식주문ApiClient.cs)와 [음식 주문 Controller](../../../Ssalddel/Controllers/Food/음식주문Controller.cs)의 정본 조회·역할/음식점 권한을 재사용한다. Hub 신호는 재조회 계기다. | 기존 [음식주문](../../../Ssalddel.Domain/음식/음식주문.cs)과 상태가 기준이다. OS 알림 본문은 업무 원장이나 수락 기록이 아니다. |
| 음식점 전표 독립 복구 | [주문 상세](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor), [상세 행동](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor.cs), [DeskService](../../../RestaurantDeskApp/Services/음식점주문DeskService.cs), [Printer 계약](../../../RestaurantDeskApp/Services/IRestaurantReceiptPrinter.cs), [Android Printer](../../../RestaurantDeskApp/Platforms/Android/AndroidRestaurantReceiptPrinter.cs) | `전표준비Async`가 서버 주문을 다시 조회하고 기존 전표 생성기를 사용한다. 수락·배차·조리 명령을 다시 실행하지 않는다. | 수락 시각과 최신 취소/거절 상태를 확인한다. 로컬 `전표출력요청시각`은 출력 UI 요청이며 `전표출력시각` 또는 실제 인쇄 완료 증거로 올리지 않는다. |
| 기사 신규 추천 알림·복귀 | [알림 PageModel](../../../FDriverApp/PageModels/MainPageModel.FoodNotifications.cs), [알림 상태](../../../FDriverApp/PageModels/FDriverFoodNotificationState.cs), [서비스 계약](../../../FDriverApp/Services/IFDriverFoodNotificationService.cs), [Android 구현](../../../FDriverApp/Services/FDriverFoodNotificationService.cs) | 기존 기사 업무 공간·제안 조회를 재사용한다. [기사 API Client](../../../FDriverApp/Services/FoodDeliveryDriverApiService.cs)와 [기사 Controller](../../../Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs)가 정본을 확인한다. | 기존 제안의 소유·만료·현재 상태를 사용한다. 로컬 알림과 중복 억제 상태가 DB의 배차 권위를 대체하지 않는다. |
| 기사 당일 정산·요금 구성 | [당일 PageModel](../../../FDriverApp/PageModels/MainPageModel.FoodDailySettlement.cs), [행 표시](../../../FDriverApp/PageModels/FDriverDailySettlementItem.cs), [Native 화면](../../../FDriverApp/Pages/MainPage.xaml), [당일 DTO](../../../Ssalddel.Contracts/Food/FoodDeliveryDailySettlementDto.cs), [주문별 DTO](../../../Ssalddel.Contracts/Food/FoodDeliveryOrderSettlementDtos.cs) | 기존 기사 Controller의 `GET api/v1/driver/food-deliveries/settlements/daily?date=yyyy-MM-dd` → [당일 UseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.DailySettlement.cs) → [Recorder projection](../../../Ssalddel/Application/Food/음식주문기사정산Recorder.cs) | 기존 [음식주문기사정산·지급 근거](../../../Ssalddel.Domain/음식/음식주문기사정산.cs)와 [EF Context](../../../Ssalddel.Infrastructure/Persistence/SsalddelContext.cs)를 읽는다. 새 원장·migration·요율 변경은 없다. |

## 당일 집계의 날짜·권한·금액

- API는 인증된 기사 본인 ID만 사용한다. 다른 기사 ID를 요청 인자로 받지 않는다. 무인증은 401이며 날짜는 정확한 `yyyy-MM-dd` 형식으로 읽는다. 무효 날짜와 조회 경계를 만들 수 없는 최솟값·최댓값은 400이다.
- 날짜를 생략하면 서버의 한국 시간 오늘을 사용한다. 기준은 원장의 `전달완료시각Utc`이며 한국 자정부터 다음 자정 직전까지의 UTC 반개구간으로 조회한다. 수령 확인일·공제 확정일·지급 테스트일로 완료 원장을 다른 날짜에 옮기지 않는다.
- `DateBasisCode=DeliveryCompletedAt`, `TimeZoneCode=Asia/Seoul`, `IsFullDayQuery=true`로 조회 기준을 전달한다. 기존 업무 공간의 최근 40건과 별도인 **하루 전체 서버 조회**다. 행 목록과 합계는 같은 조회 결과에서 만든다.
- 중복 완료 재시도와 지급 근거 여러 개가 원래 완료 주문의 건수·배달료를 늘리지 않는다. 취소·중단만 된 주문을 완료 원장으로 생성하지 않는다. 후속 수령 확인·공제·모의 지급 상태는 같은 완료일 원장에 반영한다.
- 금액은 저장된 원장 값을 합산한다. `KnownGrossAmountTotal`은 확인된 배달료의 부분합이다. 하나라도 원금이 없으면 `GrossAmountTotal`, 공제나 순액이 미확정이면 각 합계는 `null`이다. 미확정을 0원으로 채우거나 현재 요율로 역산하지 않는다.
- `ReceiptConfirmedOrderCount`와 대기 건수, 모의 지급 성공/실패 건수는 서로 다른 상태다. `SimulationSucceededOrderCount`를 실제 입금 건수로 표시하지 않는다. 현재 은행·PG 연동 근거가 없으므로 `ActualTransferAmountTotal=null`을 유지한다.
- 앱은 응답의 기사 ID·선택 날짜·전체 조회 표시·완료 건수와 행 수·각 행의 기사 ID를 확인한다. 날짜 변경, 인증 종료와 계정 전환은 조회 수명을 끝낸다. 빈 완료일은 성공한 0건 조회이며 조회 전·조회 실패와 구별한다.

## 수락 당시 동결된 요금 구성

[요금 제안 Service](../../../Ssalddel/Services/Dispatch/Recommendation/음식배달기사제안요금Service.cs)와 기존 완료 원장에 저장된 `요금계산근거Json`이 근거다. 상세 조회는 저장된 JSON을 안전한 DTO로 투영하며, 현재 운영 설정이나 거리 API를 다시 호출해 수락 당시 금액을 바꾸지 않는다.

`PricingBreakdown`은 기본·픽업·전달·거리·최소요금 조정과 시간/날씨/수요 할증, 저장 판본·확정 시각·거리 기준을 선택적으로 전달한다. 저장 원금·판본·기사 소유·구성 합계가 일치해야 상세를 확인된 근거로 다룬다. 이전 `LegacyUnsplit` 기본비는 전체 기본비만 보존하고 픽업·전달을 임의 분해하지 않는다.

누락(`MissingEvidence`), 잘못된 근거(`InvalidEvidence`), 세부 구성 누락(`MissingComponents`)은 구별한다. 상세를 확인하지 못해도 기존 원장의 배달료 원본은 보존한다. 원본 JSON·다른 기사 식별자·주문자 주소·연락처 등은 DTO에 그대로 전달하지 않는다. 공제·보험·프로모션 정책은 기존 원장과 정산 확정 업무의 책임이며 이 조회에서 새 정책을 적용하지 않는다.

## 알림·전표의 수명과 개인정보

음식점 알림은 계정·세대와 서버 주문대기 상태를 확인한 뒤 중복을 억제한다. Android 알림 제목·본문에는 주문자 이름·주소·메뉴·가격·연락처를 넣지 않는다. 이동용 Intent는 주문번호와 opaque ticket만 전달하고 ticket과 소유 계정의 연결은 앱 로컬에 한정한다. 로그아웃·계정 전환·Dispose 뒤의 늦은 콜백은 제외한다.

음식점 앱 재개 시 수신함과 선택 상세를 다시 읽으며, 프로세스 재시작 시 기존 인증 복원과 서버 조회로 복귀한다. 기사 알림도 최신 조회·신규 수신 의사·만료 상태를 기준으로 정리한다. 권한 거절이나 알림 유실이 업무 완료·실패를 의미하지 않는다.

현재 OS 알림은 앱 프로세스와 연결이 살아 있는 동안의 로컬 알림이다. 새 FCM receiver/provider, 앱 강제 종료·OS 제거·Doze 중 신규 업무 전달 보장, 상시 백그라운드 위치 전송은 포함하지 않는다. Android 인쇄 대화상자 열기와 실제 프린터·종이 출력은 별도 검증이다.

## 검증 계획과 현재 상태

| 확인 대상 | 준비된 회귀 범위 | 현재 결과 |
| --- | --- | --- |
| 서버 당일 정산 | 40건 초과 전체 조회, 다른 기사, 한국 날짜 경계, 기본 날짜, 미확정/0원, 후속 조정, 중복·취소·중단, 권한·무효 날짜·읽기 전용 | 신규 16/16 통과 |
| 동결 요금 근거 | 저장 금액/판본/기사/합계, 이전 기본비, 누락·잘못된 JSON·거리·시각·음수·숨은 구성·원문 비노출 | 신규 18/18 통과 |
| 기사 알림 | 계정·만료·중복·신규 OFF·정본 복귀·수명 | 신규 23/23 통과 |
| 음식점 알림·전표 | 권한·상태·재조회·계정/로그아웃·늦은 콜백·재시작/재개·전표 독립 재시도와 수락 중복 방지 | 신규 25/25 통과 |
| 전체/관련 시험·APK | 동결 소스의 Fast/Task와 기사·음식점 Debug APK | 실제 결과는 [변경 기록](../../Changes/2026-10-03-food-notification-daily-settlement-r1.md) 참조 |
| 실제 앱 | Android 알림 허용/거절·백그라운드 알림 탭·정본 복귀·인쇄 UI 취소·재시작, 당일 합계·구성 상세 | 실제 결과는 [변경 기록](../../Changes/2026-10-03-food-notification-daily-settlement-r1.md) 참조 |

작성된 시험은 [서버 당일 정산](../../../Ssalddel.Tests/Application/Driver/Food/FoodDeliveryDailySettlementTests.cs), [저장 요금 근거](../../../Ssalddel.Tests/Application/Driver/Food/FoodDeliverySettlementPricingEvidenceTests.cs), [기사 알림](../../../Ssalddel.Tests/Clients/FDriverFoodNotificationTests.cs), [음식점 알림 복구](../../../Ssalddel.Tests/Clients/RestaurantOrderNotificationRecoveryTests.cs), [전표 복구](../../../Ssalddel.Tests/Clients/RestaurantReceiptRecoveryTests.cs)에 있다. 시험 작성과 실제 실행·화면·입금 확인은 별개다.

동결 소스 지문과 변경 전 사본은 Git 제외 `artifacts/local/food-notification-daily-settlement-r1/`에 보관한다. 검증용 합성 주문·모의 지급값을 실주문·운영 지급·은행 입금으로 해석하지 않는다. 현재 문서는 커밋·푸시·출시 완료 증거가 아니다.

최종 기사 당일 화면 복구 12/12를 포함해 신규 94건 통과, 관련 97/97과 전체 3.5 빌드 통과다. 전체 Task는 기존 실패 7건이 그대로 남은 6,259/6,266이며 전체 게이트 미통과다. Android 출력 취소 후 재시도에서는 해제한 Java 객체의 tuple 비교를 피하도록 종료 요청을 먼저 인덱스로 제거한다. 실제 반복 시스템 출력 창과 서버 수락 중복 부재는 변경 기록의 합성 근거다.
