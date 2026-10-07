# 기사 완료 배달 목록·상세와 정보 열람 기한 r3

2026-10-04 사용자 승인 범위는 본인 완료 배달의 상세 열람 권한·기한과 정산 목록에서 진입하는 건별 상세 화면이다. 열람 기간은 사용자 확인에 따라 완료 후 3일이다. 후속 요청에 따라 r2는 배달 수행·완료 목록·선택 상세를 독립 화면으로 나눈다. 기존 배차·요금 계산·공제·지급 원장은 유지한다.

## 페이지 책임 카드

| 항목 | 책임 |
| --- | --- |
| 주 사용자·대상 | 인증한 음식 배달 기사와 본인이 완료한 배달의 정산 기록 |
| 목적·질문 | 목록은 특정 완료일의 배달을 찾고, 상세는 선택한 한 건의 금액·요금 근거와 허용된 주문·고객 정보를 확인한다 |
| 결과 | 한 건의 정산 근거를 확인하고 같은 날짜·페이지의 완료 목록으로 돌아간다 |
| 기본 정보 | 목록에는 날짜·매장·완료 시각·배달료·요금 기준 거리와 일간 건수/배달료 합계, 상세에는 선택한 한 건의 공제·수령액·정산/지급 상태 |
| 상세 정보 | 별도 상세 화면에서 수락 당시 저장된 픽업/전달/거리비·할증, 열람 기간 내 주문·고객 정보를 확인한다 |
| 주 행동 | 목록의 `상세 보기`로 한 건을 선택하고, 상세의 `목록으로`에서 원래 목록으로 돌아간다 |
| 독립 업무 | 현재 배달 수행·배차·정산 정책 변경·지급 실행과 분리 |
| 표시 상태 | 조회 중·빈 목록·조회 실패/재시도·상세 열람 가능·기간 종료·근거 미확인 |
| 상태 소유 | 서버가 본인 권한·완료 근거·기간·공개 항목을 판정한다. 앱은 선택·조회 수명·표시만 소유한다 |
| 진입·복귀·수명 | 수행 화면의 `배달 내역`으로 별도 목록에 진입한다. 목록의 날짜·페이지는 같은 계정의 상세 복귀 때 유지하며, 상세 이탈·계정 변경·앱 중단 때 개인정보 표시 수명을 종료한다 |

단일 책임 기준은 [전체 로드맵 조화형 페이지 원칙](../../Architecture/WholeRoadmapPagePrinciple.md), 글자·색·카드·여백은 [역할 앱 시각 디자인 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 따른다. 현재 배달 화면의 세 카드는 수행 목적이며, 이 문서의 정산 상세 카드는 완료 내역 조회 목적이다.

## 정보 공개 경계

### 별도 목록·상세 route r2 — 현행 책임 카드

사용자 후속 요청에 따라 현재 배달 수행 화면에서 `배달 내역`으로 별도 목록 route에 진입하고, 한 행의 `상세 보기`에서 별도 상세 route로 이동한다. 기존 동일 페이지의 목록/상세 표시 전환은 r1 이력이다.

| 페이지 | 목적·주 행동 | 기본 정보 | 실패·복귀·수명 |
| --- | --- | --- | --- |
| 완료 배달 목록 | 기사가 날짜별로 완료한 배달을 찾아 한 건을 선택한다 | 날짜·매장명·완료 시각·배달료·요금 기준 거리. 최신 완료순으로 20건씩 이전/다음 페이지 | 빈 목록·조회 실패·재시도·인증 복귀. 상세에서 돌아오면 같은 날짜·목록 페이지를 유지하며 금융 목록만 보관 |
| 완료 배달 상세 | 기사가 선택한 한 건의 정산과 허용된 주문/고객 정보를 확인한다 | 기존 정산/주문/고객 카드와 열람 기한 | 상세 이탈·앱 중단·계정 변경 때 개인정보 제거, 재개 시 서버 권한 재조회. 뒤로는 원래 목록, 직접 진입은 목록으로 인계 |
| 배달 수행 | 기사가 현재 배달을 처리한다 | 기존 지도·현재 수행·상태별 행동 | 내역 이동은 수행 화면의 표시·도로 경로 조회만 중단한다. 앱이 활성인 동안 업무 갱신·위치 수신·신규 추천 감시는 유지하고, 수행 복귀 때 서버 정본을 다시 확인한다 |

### 수행과 내역의 수명 분리 r3 — 현행 책임 카드

2026-10-04 사용자는 진행 중 배달의 내역 왕복과 기존 지도 SDK 설정 확인을 승인했다. 목록·상세는 완료 기록 조회만 소유하며, 진행 중 배달의 앱 내 갱신 수명은 특정 페이지의 표시 수명과 분리한다. 운행·신규 수신 의사와 서버가 허용한 수행 상태를 변경하지 않는다.

앱이 활성인 동안에는 내역·상세에서도 기존 업무 재조회·위치 수신/전송·추천 알림을 유지한다. 숨겨진 수행 화면의 도로 경로 요청과 새 위치 권한 대화상자는 실행하지 않는다. 앱 중단·인증 종료·계정 변경·기사 권한 상실 때 업무 수신을 취소하고, 재개 시 현재 계정의 서버 정본을 확인한 뒤 하나의 감시와 위치 리스너만 유지한다. 상세 개인정보는 기존 이탈·중단·72시간 기한을 그대로 따른다.

검증 대상은 진행 중 건과 유효 추천이 있는 상태의 목록→상세→수행 복귀, 내역에서 새 추천 수신, 위치 갱신, 숨겨진 경로 조회 억제, 중단/재개와 계정·역할 경계다. OS 강제 종료 후 신규 수신이나 백그라운드 위치 서비스 신설은 포함하지 않는다. 지도는 기존 SDK·설정 근거를 확인하고 실제 인증·타일·핀·경로·제스처의 실행 여부를 각각 기록한다.

구현은 [수행 PageModel](../../../FDriverApp/PageModels/MainPageModel.cs), [위치·지도 수명](../../../FDriverApp/PageModels/MainPageModel.FoodMap.cs), [추천 알림](../../../FDriverApp/PageModels/MainPageModel.FoodNotifications.cs), [Window 수명](../../../FDriverApp/App.xaml.cs), [페이지 표시 수명](../../../FDriverApp/Pages/MainPage.xaml.cs)에 연결한다. 같은 계정의 토큰 갱신은 업무 수명을 유지하고, 중단된 재개 요청의 늦은 완료는 새 업무 감시를 시작하지 않는다. [업무 수명 시험](../../../Ssalddel.Tests/Clients/FDriverWorkspaceLifetimeTests.cs)과 [r3 실행·지도 확인 기록](../../Changes/2026-10-04-driver-history-lifetime-map-r1.md)에 현재 검증 범위를 기록한다.

r3 Fast 관련224건 통과, 전체3.5 빌드 통과·6,437/6,444건·기존7실패 동일이다. 진행2건·유효 추천1건의 격리 환경에서 내역 조회 중 업무/위치 요청, 상세→같은2페이지 복귀, 새 단계/추천 표시, HOME 중단/재개와72시간 후 개인정보 제외를 실제 Android 에뮬레이터에서 확인했다. 기존 자격으로 네이버 자동차 경로3개를 조회했으나 SDK의 현재/legacy 인증 방식 모두에서 지도 인증 실패 화면이 나타났다. 실제 지도 타일·핀·선·제스처·물리 단말 검증은 완료되지 않았다.

목록 route는 `food-delivery-history`, 상세 route는 `food-delivery-history-detail`이다. 목록과 상세는 독립 PageModel이 표시/조회 수명을 소유한다. URL에는 정산 stable ID·날짜만 사용하고, 목록에서 상세로 넘기는 메모리 인계에는 선택한 정산·주문·배달 시도의 식별자만 담는다. 고객명·연락처·주소·메뉴를 navigation에 넣지 않는다. 실제 서버 API·72시간 정책·금융 원장·요율과 공제는 변경하지 않는다.

날짜 선택·새로고침·일간 합계와 이전/다음 페이지는 목록에만 둔다. 목록은 서버의 전체 일간 정산을 완료 시각 내림차순으로 정렬하고, 같은 완료 시각은 정산 stable ID로 정렬한 뒤 20건씩 표시한다. 화면의 페이지를 나눠도 일간 합계는 서버가 반환한 전체 날짜 값이며 현재 페이지의 부분 합계가 아니다. 날짜를 바꾸면 1페이지에서 다시 조회하고, 같은 날짜에서 상세를 열었다가 `목록으로` 또는 뒤로 돌아오면 기존 목록의 페이지를 유지한다. 새로고침도 가능한 경우 현재 페이지를 유지하되 조회 건수가 줄어들면 존재하는 마지막 페이지로 조정한다.

목록은 주문·고객 상세 API를 호출하지 않으며 금융 기록만 메모리에 보관한다. 주문·고객 정보와 열람 기한은 별도 상세에서만 조회하고, 상세 화면에 날짜 선택이나 전체 완료 목록을 다시 담지 않는다. 상세 route로 직접 진입한 경우 돌아갈 목록이 없으면 전달받은 완료일의 목록으로 인계한다. 목록 조회 실패 시 이미 확인한 금융 목록을 유지할 수 있지만 이전 조회 결과임을 표시하며, 계정 변경·인증 종료 때는 그 목록도 비운다.

- 정산 기록과 고객·주문 상세의 열람 수명을 분리한다. 상세 기한이 끝나도 본인의 배달료·요금 구성·요금 기준 거리·공제·수령액은 기존 권한으로 조회한다.
- 완료 정산 ID와 최종 배달 시도의 기사·주문·완료 관계를 서버에서 확인한다. 다른 기사·미완료 건의 상세를 제공하지 않는다.
- 고객명·연락처·정확한 전달 주소·요청사항·주문 상세는 허용 기간 안에서만 반환한다. 만료 시 화면을 숨기는 데 그치지 않고 응답에서도 제외한다.
- 완료 시각과 서버 UTC를 기준으로 기한을 판정한다. 기한의 정확한 경계에서 접근을 닫는다. 열람 기간이 명시적 null·0·음수이거나 사용할 수 없는 경우, 또는 완료 근거가 없거나 잘못된 경우 개인정보를 제공하지 않는다. 설정을 생략하면 기본72시간을 적용한다.
- 사용자 답변에 따라 열람 기간은 배달 완료 후 **3일(72시간)**이다. 서버 운영 설정으로 관리하고, 정확한 만료 시각을 응답과 화면에 전달한다. 운영 설정은 본인 열람 기한이며 법정 보관기간이라는 의미가 아니다.
- 직접 API 호출·일반 운송 조회·앱의 늦은 응답과 캐시도 같은 경계를 지킨다. 개인정보를 URL·로그·영속 캐시에 넣지 않는다. 재개 시 서버를 다시 조회한다.
- 서버의 원본 보관·법정 보존·파기는 별도 정책이다. 이번 구현은 DB 원장·고객 기록의 일괄 삭제가 아니다.

## 금액과 거리

기존 [당일 정산·동결 요금 근거](food-notification-daily-settlement-r1.md)를 재사용한다. 현재 운영 설정으로 과거 금액을 다시 계산하지 않는다. 공제 미확정과 실제 입금 미확인을 0원 또는 입금 완료로 바꾸지 않는다.

현재 완료 원장의 거리는 요금 기준 거리다. 현재 위치에서 목적지까지의 안내 경로 거리·실제 GPS 누적 주행거리와 구별하며, 없는 실제 주행 기록을 생성하지 않는다.

## 검증 범위

본인/다른 기사·완료 시도 연결·기한 직전/정확한 경계/직후·미확인 근거·직접 조회와 일반 운송 경로·기간 후 금융 정보 유지·선택/날짜/계정/영역 변경·화면 이탈/재개·조회 실패·늦은 응답을 확인한다. 소스·시험·HTTP·설치 APK·실제 앱 화면·실입금은 각각 구별해서 기록한다.

## 화면 → 코드 → API → DB

| 경계 | 연결 | 책임 |
| --- | --- | --- |
| 수행 화면·진입 | [기사 MainPage](../../../FDriverApp/Pages/MainPage.xaml), [수행 화면 이동](../../../FDriverApp/Pages/MainPage.xaml.cs) | 현재 배달 수행을 유지하고 `배달 내역`으로 별도 완료 목록에 진입 |
| 완료 배달 목록 | [목록 Page](../../../FDriverApp/Pages/CompletedDeliveryListPage.xaml), [목록 수명·날짜 인계](../../../FDriverApp/Pages/CompletedDeliveryListPage.xaml.cs), [목록 PageModel](../../../FDriverApp/PageModels/FDriverCompletedDeliveryListPageModel.cs) | 날짜별 금융 기록을 최신 완료순·20건씩 표시. 날짜·페이지·목록 조회 상태를 소유하고 선택한 식별자를 상세로 인계 |
| 완료 배달 상세 | [상세 Page](../../../FDriverApp/Pages/CompletedDeliveryDetailPage.xaml), [상세 수명·복귀](../../../FDriverApp/Pages/CompletedDeliveryDetailPage.xaml.cs), [상세 PageModel](../../../FDriverApp/PageModels/FDriverCompletedDeliveryDetailPageModel.cs), [표시 모델](../../../FDriverApp/PageModels/FDriverCompletedDeliveryDetailItem.cs) | 선택한 한 건을 정산·주문·고객 카드로 표시. 개인정보 표시 수명·재조회·원래 목록 복귀를 관리 |
| 화면 이동 | [Navigator](../../../FDriverApp/Services/FDriverCompletedDeliveryNavigator.cs), [이동 계약·선택 식별자](../../../FDriverApp/Services/IFDriverCompletedDeliveryNavigator.cs), [Shell route 등록](../../../FDriverApp/AppShell.xaml.cs) | 목록/상세 route 조립과 복귀만 담당. URL과 메모리 인계에 개인정보를 넣지 않음 |
| API Client·Contract | [기사 API Client](../../../FDriverApp/Services/FoodDeliveryDriverApiService.cs), [완료 상세 DTO](../../../Ssalddel.Contracts/Food/FoodDeliveryCompletedDeliveryDetailDto.cs) | `GET api/v1/driver/food-deliveries/settlements/{settlementId}/detail`. 본인·선택한 정산/주문/시도 검증 후 허용된 상세만 표시 |
| Controller·UseCase | [기사 Controller](../../../Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs), [상세 UseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs), [열람 정책](../../../Ssalddel/Application/Driver/Food/FoodDeliveryCompletedDetailAccessPolicy.cs) | 인증한 기사 본인과 완료 근거를 확인하고 서버 UTC로 72시간 기한을 판단. 응답 캐시 저장 금지 |
| DB | [Context](../../../Ssalddel.Infrastructure/Persistence/SsalddelContext.cs), [기사 정산](../../../Ssalddel.Domain/음식/음식주문기사정산.cs), [배달 시도](../../../Ssalddel.Domain/음식/음식배달시도.cs), [운송 원장](../../../Ssalddel.Domain/운송/운송원장.cs), [주문](../../../Ssalddel.Domain/음식/음식주문.cs), [주문 상품](../../../Ssalddel.Domain/음식/음식주문상품.cs) | 읽기 전용 조회. 기존 원장과 지급 근거를 변경하지 않고 고객·주문 정보와 금융 투영을 분리 |
| 다른 조회 경로 | [일반 운송 상세](../../../Ssalddel/Application/Driver/Transport/Handlers/운송상세조회QueryHandler.cs), [일반 운송 이벤트](../../../Ssalddel/Application/Admin/Progress/Handlers/운송원장이벤트조회QueryHandler.cs) | 음식 기사 전용 조회를 일반 운송 상세·고객 좌표 이벤트로 우회하지 못하게 음식 기사 접근을 제외. 기존 화물·관리자 권한 유지 |

서버 설정 `FoodDelivery:CompletedDetailAccess:WindowMinutes`의 기본값은 `4320`이다. 환경 변수 `FoodDelivery__CompletedDetailAccess__WindowMinutes`로 운영 설정을 바꿀 수 있으며 관리자 앱의 새 설정 화면을 만든 것은 아니다. 명시적 null·0·음수 또는 사용할 수 없는 기간은 고객·주문 상세를 제공하지 않는다.

앱은 서버가 보낸 현재/만료 시각에서 요청 경과 시간을 제외하고 단조 시간 기준으로 남은 표시 시간을 계산한다. 화면 만료·이탈·계정 변경 때 개인정보 문자열을 비우고 재개 시 서버 권한을 다시 조회한다. 같은 기사 토큰 갱신은 기한을 연장하지 않는다. 완료 목록의 `상세 보기`로 별도 상세에 진입하며 긴 요금 구성은 선택 상세에서 제공한다.

[서버 회귀 시험](../../../Ssalddel.Tests/Application/Driver/Food/FoodDeliveryCompletedDetailTests.cs)과 이전 완료 상세의 실제 실행·HTTP·화면 결과는 [r1 변경 기록](../../Changes/2026-10-04-driver-completed-detail-r1.md)에 보존한다. r2의 [독립 목록 시험](../../../Ssalddel.Tests/Clients/FDriverCompletedDeliveryListTests.cs)33건, [독립 상세 수명 시험](../../../Ssalddel.Tests/Clients/FDriverCompletedDeliveryDetailTests.cs)44건, 목록 → 상세 → 같은 날짜·페이지 복귀와 실제 APK 화면 검증 결과는 [r2 변경 기록](../../Changes/2026-10-04-driver-history-routes-r1.md)에 기록했다. Fast 관련205건 통과, 전체 시험의 기존7실패는 동일하다. 화면은 격리 SQLite와 예시 인증·데이터를 사용한 전용 Android 에뮬레이터 검증이며 운영 DB·실제 지도·물리 단말·은행 입금·배포를 확인한 것으로 확대하지 않는다.
