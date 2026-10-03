# 음식 배달 예외 처리와 신규 배차 수신 의사 r1

2026-10-03 `dev/mirror-integration` 작업 소스 기준이다. 기존 서버 계약에 연결되지 않았던 주문자 취소, 기사 가게 도착·현장 중단, 운영자 중단 검토를 역할 화면에 연결하고, 기사의 신규 배차 수신 의사를 운행 상태와 분리했다. 구현 사실과 실행 검증 상태는 구별한다.

화면 책임과 정보 배치의 기준은 [전체 로드맵 페이지 원칙](../../Architecture/WholeRoadmapPagePrinciple.md), [페이지 책임 카드](page-responsibility-template.md), [역할 앱 시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)이다. 이 문서는 이번 다섯 업무의 책임과 기존 코드·API·DB 연결을 기록한다.

## 페이지 책임 카드

### 주문자의 선택 주문 취소

| 항목 | 현재 구현 |
| --- | --- |
| 주 사용자·페이지/소스 | 주문자. 기존 [음식 주문 이력 호스트](../../../OrdererApp/Components/Pages/FoodOrderHistory.razor)와 공통 `OrdererFoodOrderWorkspace`의 선택 주문 상세. |
| 대상·진입 문맥 | 로그인한 계정의 선택 주문번호. 다른 주문의 상세 응답은 취소 행동을 열지 않는다. |
| 한 문장 목적 | 주문자가 선택 주문의 취소 가능 여부와 사유를 확인하고 직접 취소를 요청한다. |
| 완료 결과·상태별 주 행동 | 서버 `AvailableActions`에 주문 취소와 주문 revision이 있을 때 `주문 취소 검토`로 진입한다. 사유·확인 후 `주문 취소 확인`; 결과 미확인 시 `취소 결과 다시 확인`. 같은 주문 정본의 취소 상태를 확인해야 완료 안내를 한다. |
| 기본 정보 | 선택 주문의 매장·상품 요약·상태, 취소 사유, 명시적 확인. 기타 사유는 설명이 필요하다. |
| 보조 정보 | 기존 주문 상세의 상품·진행·필요 시 펼치는 수령 정보. 취소 입력은 [독립 패널](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderCancellationPanel.razor)과 전용 상태 소유자가 담당한다. |
| 독립 업무·제외 정보 | 주문 작성·수령 확인·인증 입력은 기존 책임을 유지한다. 이 패널은 환불 완료나 실제 입금을 확정하지 않는다. 선택 상세 전체를 별도 route로 분리한 구현은 아니다. |
| 진입·실패·복귀 | 미확인 요청은 입력을 잠그고 같은 요청 ID·입력·revision을 보존한다. 닫았다 열어도 미확인 시도의 키는 유지한다. 주문/계정 변경·이탈은 기존 응답 수명을 무효화한다. 401은 인증 복귀, 409는 정본을 읽고 다시 검토하며 자동 재전송하지 않는다. |
| 책임 판정 | 같은 선택 주문의 진행 확인에 결속된 취소 구성. 입력·요청 수명은 [주문자음식주문취소ViewModel](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/주문자음식주문취소ViewModel.cs)이 소유하며 기본 조회/작성 VM과 구분한다. |

### 기사 가게 도착 기록

| 항목 | 현재 구현 |
| --- | --- |
| 주 사용자·페이지/소스 | 음식 배달 기사. 기존 [MainPage](../../../FDriverApp/Pages/MainPage.xaml)의 현재 수행 배달. |
| 대상·진입 문맥 | 현재 선택 제안과 서버가 제공한 `DeliveryAttemptId`·`AttemptRevision`. 서버 가능 행동에 가게 도착이 있어야 버튼을 연다. |
| 한 문장 목적 | 기사가 현재 수행 배달의 가게 도착을 기록하고 서버가 확인한 시각을 확인한다. |
| 완료 결과·상태별 주 행동 | `가게 도착` 요청 후 업무 정본을 다시 읽는다. 가게 도착 기록이 있거나 시도가 달라지면 대기 요청을 정리한다. |
| 기본·보조 정보 | 매장·현재 배달 상태와 도착 여부. 도착 시각·표시 준비 예정 시각·조리 지연 중단 기준 시각은 이후 현장 중단 판단에 사용한다. |
| 독립 업무·제외 정보 | 도착 기록은 픽업 완료·중단·운행 종료를 대신하지 않는다. 앱이 조리 지연이나 귀책을 임의 확정하지 않는다. |
| 진입·실패·복귀 | 전송 중 중복 입력을 잠그고 같은 시도의 도착 요청 ID와 예상 시도 revision을 보존한다. 응답 실패 후 정본을 조회한다. 이탈·인증 변경 뒤 늦은 응답은 작업 문맥에 적용하지 않는다. |
| 책임 판정 | 현재 배달 진행을 위한 행동이므로 수행 화면의 같은 목적 구성. 행동과 대기 요청은 [MainPageModel.FoodExceptions](../../../FDriverApp/PageModels/MainPageModel.FoodExceptions.cs)가 관리한다. |

### 기사의 독립 현장 중단

| 항목 | 현재 구현 |
| --- | --- |
| 주 사용자·페이지/소스 | 음식 배달 기사. 같은 MainPage의 `현장 중단` 진입 → `현장 배달 중단` 표시 모드. |
| 대상·진입 문맥 | 현재 수행 중이며 서버가 중단 행동을 허용한 배달 시도. 다른 시도로 미확인 입력을 옮기지 않는다. |
| 한 문장 목적 | 기사가 선택 시도의 현장 상황과 중단 사유를 확인하고 배달 중단을 요청한다. |
| 완료 결과·상태별 주 행동 | 사유·메모를 입력하고 `배달 중단 요청`. 결과 미확인 시 `같은 요청 다시 확인`; 최신 수행 목록에서 시도 소멸·변경을 대조한다. |
| 기본 정보 | 대상 매장·상품 요약, 도착·준비 예정·서버 중단 기준 시각, 사유, 최대 500자 메모. 조리 지연은 도착과 기준 시각 확인이 선행된다. |
| 보조 정보 | 서버가 판단한 중단 가능 행동과 안내. 현재 서버는 조리 지연 중단에 픽업 전·가게 도착·준비 예정 10분 초과·픽업 준비 미완료 조건을 적용한다. |
| 독립 업무·제외 정보 | 일반 업무 화면과 상호 배타적으로 표시한다. 중단 입력 상태는 [FDriverDeliveryExceptionState](../../../FDriverApp/PageModels/FDriverDeliveryExceptionState.cs)가 소유한다. 기사 화면에서 악용·귀책·정산을 판정하지 않는다. |
| 진입·실패·복귀 | 전송 후 입력 잠금과 동일 요청 재확인. 400/409 또는 시도 판본 변경 시 기존 입력의 재전송을 해제하고 새 상태를 확인한다. 닫기는 전송 중 제한하며, 화면 이탈은 요청 수명을 취소한다. 최종 401 뒤에는 인증 화면으로 복귀하고 자동 중단 요청을 하지 않는다. |
| 책임 판정 | 독립 표시 모드·입력 소유자·진입/복귀를 둔 분리. 같은 물리 MainPage를 재사용한다. |

### 신규 배차 수신 의사와 운행

| 항목 | 현재 구현 |
| --- | --- |
| 주 사용자·페이지/소스 | 음식 배달 기사. MainPage의 운행 상태와 별도 `신규 배차 받기 ON/OFF` 행동. |
| 대상·진입 문맥 | 현재 로그인 기사 자신의 서버 수신 상태. 확인 전에는 수신 상태 미확인으로 표시한다. |
| 한 문장 목적 | 기사가 현재 배달을 유지하면서 새 배차를 받을 의사를 명시한다. |
| 완료 결과·상태별 주 행동 | 운행 시작은 운행만 시작한다. 신규 수신은 별도 선택하고 변경 후 서버 수신 상태와 업무를 재조회한다. 응답 미확인 시 `수신 변경 다시 확인`으로 같은 요청 ID를 사용한다. |
| 기본 정보 | 운행 상태, 신규 수신 의사, 수신 변경 결과. ON이어도 운행·위치·서버 배차 조건이 충족되어야 새 제안을 받는다. |
| 보조 정보 | 서버 실효 상태가 배차 가능이 아니면 조건 확인 안내. 일시 제한이 기사의 의사를 바꾸는 것으로 표시하지 않는다. |
| 독립 업무·제외 정보 | OFF는 현재 수행·현재 경로·위치 갱신을 종료하지 않는다. 운행 종료/로그아웃은 수행 중 배달이 있으면 제한한다. 중단 사유 작성 중 수신 변경은 열지 않는다. |
| 진입·실패·복귀 | 상태 조회 실패 시 확인된 ON으로 취급하지 않는다. 변경 요청 재확인은 동일 의사와 요청 ID를 보존하며 정본과 일치하면 정리한다. |
| 책임 판정 | 운행과 신규 제안 수신은 별도 상태·행동으로 구성한다. 수신 OFF를 운행 종료 API에 연결하지 않는다. |

### 운영자의 독립 중단 검토

| 항목 | 현재 구현 |
| --- | --- |
| 주 사용자·페이지/소스 | 서버 관리자. 기존 `/food/order-trace`와 `/admin/food-delivery/order-trace`의 [주문 추적](../../../SsalddelAdmin/Components/Pages/FoodOrderOperationsTrace.razor). |
| 대상·진입 문맥 | 조회한 같은 주문에 중단 시도가 있을 때 `배달 중단 검토`로 진입. `주문 추적으로 돌아가기`는 같은 주문을 재조회한다. |
| 한 문장 목적 | 운영자가 특정 중단 시도의 사실과 판본을 확인하고 명시적인 검토 판정을 기록한다. |
| 완료 결과·상태별 주 행동 | `최신 시도 조회` → `이 시도 검토` → 판정·근거·명시 확인 → `검토 기록 저장`. 저장 응답 뒤 같은 주문 정본을 다시 확인한다. |
| 기본 정보 | 시도 순번·상태·StableId·revision, 중단 사유·기존 책임, 도착·현장 대기·재조리 근거. 최근 시도와 현재 수행 상태를 구별한다. |
| 보조 정보 | 유산 추정 여부, 기존 검토 사유·악용 확정 기록. 보호 대상에서 기사 책임으로 바꾸려면 사람 검토의 악용 확정이 필요하다. |
| 독립 업무·제외 정보 | [ReviewWorkspace](../../../SsalddelAdmin/Components/Pages/FoodDeliveryInterruptionReviewWorkspace.razor)가 추적 폼·모의 지급과 상호 배타적으로 표시된다. 검토는 자동 재배차나 실제 지급을 실행하지 않는다. 새 귀책 정책도 만들지 않는다. |
| 진입·실패·복귀 | 전송 중 입력/대상 변경 잠금. 응답 유실은 정본 재조회 후 같은 입력이면 원래 요청 ID·예상 revision으로 재시도한다. 409는 요청을 새로 만들기 전에 최신 시도와 근거를 다시 확인한다. 401은 추적 정보를 숨기고 독립 로그인으로 복귀한다. 주문/인증 변경·이탈은 늦은 응답을 무효화한다. |
| 책임 판정 | 독립 모드·상태 소유자 [FoodDeliveryInterruptionReviewState](../../../SsalddelAdmin/Services/FoodDeliveryInterruptionReviewState.cs)·진입/복귀를 둔 분리. 기존 관리자 route와 모의 지급 책임을 보존한다. |

## 코드·API에서 DB까지

| 업무 | 화면 → Client/API → 서버 | 기존 저장 관계와 변경 권위 |
| --- | --- | --- |
| 주문자 취소 | 취소 ViewModel → [음식주문Client](../../../Ssalddel.Ui.Common/Areas/App/Services/음식주문Client.cs) → `POST api/v1/food-orders/{orderNo}/cancellation` → [Controller](../../../Ssalddel/Controllers/Food/음식주문Controller.cs) → [CommandHandler](../../../Ssalddel/Application/Food/Handlers/주문자음식주문취소CommandHandler.cs) → [EF Store](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) | `음식주문`의 주문자 소유권·현재 상태·상태이력 개수 revision을 서버가 확인한다. 상태·`음식주문상태이력`과 기존 원장 동기화 요청을 기록하고 `운영배차활동사건`에 주문자 취소·음식점 제안 철회를 추가한다. 직접 취소는 음식점 수락 전 주문대기 상태 범위다. |
| 기사 도착/중단 | MainPageModel → [FoodDeliveryDriverApiService](../../../FDriverApp/Services/FoodDeliveryDriverApiService.cs) → `POST api/v1/driver/food-deliveries/offers/{offerId}/restaurant-arrival` 또는 `/interruption` → [Controller](../../../Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs) → [WorkService](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) | `음식배달시도`는 주문번호·제안Id·기사Id·시도StableId·revision을 결속한다. 도착은 도착 시각과 위치 감사 사건을 기록한다. 중단은 기존 서버 정책으로 시도·주문·배차대기 상태 및 활동 사건을 변경한다. 기존 픽업 후 재조리/재배차 정책을 UI 연결만으로 새로 만든 것으로 보지 않는다. |
| 신규 수신 의사 | MainPageModel → 기사 API Client → `GET api/v1/driver/operational-dispatch/availability`, `PUT .../availability/intent` → [Controller](../../../Ssalddel/Controllers/Driver/05_Settings/기사운영배차공통Controller.cs) → [공통 UseCase](../../../Ssalddel/Services/Dispatch/Common/운영배차공통UseCase.cs) | `운영배차활동사건`에 기사 의사 변경과 서버 실효 변경을 서로 다른 사건으로 기록한다. 사건StableId로 멱등성을 확인하고 원장에서 수신 상태를 재구성한다. 투영은 조회 사본이며 운행·위치 원장을 대체하지 않는다. |
| 운영자 검토 | Review state → [AdminService](../../../SsalddelAdmin/Services/FoodOrderOperationsTraceAdminService.cs) → `PUT api/v1/admin/food-orders/delivery-attempts/{attemptId}/interruption-review` → [Controller](../../../Ssalddel/Controllers/Admin/Food/음식주문운영추적Controller.cs) → [검토 UseCase](../../../Ssalddel/Application/Admin/Food/음식배달중단검토UseCase.cs) | 중단 시도만 검토한다. `음식배달시도`의 책임·악용 확정·검토자·사유·요청 ID·revision을 갱신하고 `운영배차활동사건`에 책임 판정 사건을 추가한다. 같은 요청 ID의 같은 내용은 멱등 처리하고 다른 내용 재사용은 거부한다. |

[SsalddelContext](../../../Ssalddel.Infrastructure/Persistence/SsalddelContext.cs)의 기존 DbSet을 사용한다. [음식주문 구성](../../../Ssalddel.Infrastructure/Persistence/Configurations/Food/음식주문Configuration.cs)은 주문에 상품·상태이력을 연결하며, [활동 사건 구성](../../../Ssalddel.Infrastructure/Persistence/Configurations/Dispatch/운영배차활동사건Configuration.cs)은 주체·주문·제안·업무 시도를 색인한다. 새 화면 전용 DB 원장을 추가한 변경이 아니다.

## 수신 판정과 현재 업무 유지 경계

공통 기사 수신 API에는 파생 실행 기능 `OperationalDispatchCore`를 명시한다. 이 키는 기존 음식 기능(`FoodDeliveryWorkflow`/`FoodDeliveryV30`) 또는 기존 화물 기능(`DomesticTransportWorkflow`/`CargoYongdalV1`과 기존 커뮤니티 선행조건) 중 하나가 켜진 경우에만 활성화된다. 두 업무가 모두 꺼지면 공통 API도 차단한다. 별도 기본 옵션을 추가하거나 음식 기능으로 화물 기능을 켜지 않으며, 자동 작업의 화물 기능 및 Operational 모드 관문도 변경하지 않는다. 제품 도입 버전만 있고 실행 기능이 없어 공통 API가 `FeatureBoundaryUnclassified` 404로 차단되던 경로를 명시 분류한 변경이다.

초기 공통 수신 상태는 `Off`/`Ineligible`이며 의사 변경 시각이 없다. 새 후보 추천과 제안 수락은 [신규배차수신허용 helper](../../../Ssalddel.Domain/배차/운영배차수신상태Policy.cs)를 함께 사용한다.

- 기사 의사가 명시적 `On`이고 의사 변경 시각이 있어야 한다.
- 기록된 서버 일시정지는 거부한다. 서버 실효 변경 시각이 있다면 `Eligible`만 허용한다. 기록된 `Ineligible`·`ConnectionUnavailable`은 신규 수신을 허용하지 않는다.
- 서버 실효 판정 기록이 없는 초기 `Ineligible`은 명시 ON 이후에만 helper를 통과할 수 있다. 이것으로 실제 운행·위치·업무 적합성 검사를 생략하지 않는다.
- [음식 후보 정책](../../../Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs)과 기사 WorkService의 제안 조회·수락 경로가 공통 helper를 사용한다. 기존 후보의 운행·좌표·위치 시각·업무 조건 검사는 계속 적용한다.
- 서버 제한·연결 실패가 기사 의사를 OFF로 덮어쓰지 않는다. 신규 수신 OFF는 새 제안·수락을 제한하고, 이미 수락한 배달의 도착·픽업·중단·전달은 해당 업무 권한과 상태 조건으로 처리한다.
- 앱의 수신 변경은 운행 종료와 위치 전송 중지를 호출하지 않는다. 기존 수행 배달·경로와 운행 중 위치 갱신은 유지한다.

## 판본·재시도·인증의 공통 경계

서버 계약의 필드명을 유지한다. 주문자 취소와 운영자 검토는 `클라이언트요청Id`·`예상Revision`, 기사 도착/중단은 `클라이언트요청Id`·`예상시도Revision`, 수신 의사는 `클라이언트요청Id`·`수신의사Code`를 사용한다. 요청 ID는 서버 변경 시도의 키이며 화면마다 클릭할 때 새 키를 만드는 단순 재전송으로 바꾸지 않는다. 각 업무의 재확인·판본 변경 처리는 위 책임 카드의 범위를 따른다.

서버 성공 응답과 최신 정본 조회 성공은 별개다. 저장 응답만으로 조회 실패를 숨기거나 실제 지급·환불·재배차 완료를 만들어 표시하지 않는다. 최종 401은 기존 인증 복귀로 처리하고, 409는 최신 상태·가능 행동을 확인한 뒤 사람의 재검토를 요구한다. 이탈·주문/시도 변경·계정 변경 뒤의 응답은 취소 토큰과 문맥 수명으로 차단한다. 토큰·비밀번호를 초안·URL·로그에 옮기지 않는다.

## 검증

[변경 기록과 실행 검증](../../Changes/2026-10-03-food-exception-intent-r1.md)에 최종 소스·시험, 세 Debug APK 설치 지문, 주문자 취소·기사 OFF/도착/중단·운영자 검토·다른 역할 명시 재조회의 실제 화면을 결속했다. 실제 앱 실행과 자동 시험·물리 단말·운영 지급의 범위는 해당 기록을 따른다. 전체 시험의 기존 실패와 초기 앱 시작·알림·지도 한계도 구별한다.
