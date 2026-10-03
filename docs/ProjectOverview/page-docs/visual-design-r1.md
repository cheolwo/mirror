# 역할 앱 시각 디자인 적용 r1

2026-10-03. 기존 내용·기능을 유지하고 모던하고 단순하며 읽고 조작하기 편한 외형으로 다듬는다. 공통 판단과 출처는 [시각 디자인 기준](../../Architecture/RoleAppVisualDesignStandard.md), 업무 책임은 [페이지 단일 책임 기준](../../Architecture/WholeRoadmapPagePrinciple.md)이 소유한다. 정책을 이 문서에 복제하지 않는다.

## 페이지 책임과 적용 범위

아래 현재 소스에 정확히 대응하는 개별 상세 README는 없으므로 이 적용 문서에 책임 카드를 둔다. 비슷한 과거 이름의 `DriverApp-FoodDeliveries`·`SsalddelAdmin-P30`·음식점P01은 다른 페이지이며 수정 대상의 문서로 사용하지 않는다.

| 항목 | 주문자 음식 내역 | 음식점 목록/상세 | 네이티브 기사 메인 | 관리자 주문 추적 |
| --- | --- | --- | --- | --- |
| 주 사용자·대상 | 주문자·내 주문 | 음식점·선택 주문 | 기사·추천/현재 배달/본인 정산 | 운영자·입력한 주문 |
| 진입·소스 | `/orders/food`, [FoodOrderHistory](../../../OrdererApp/Components/Pages/FoodOrderHistory.razor), 공용 Food Workspace | `/orders`, `/orders/{OrderNo}`, [Inbox](../../../RestaurantDeskApp/Components/Pages/OrderInbox.razor)·[Detail](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor) | [MainPage.xaml](../../../FDriverApp/Pages/MainPage.xaml), 기존 Shell 진입 | `/food/order-trace`, [Trace](../../../SsalddelAdmin/Components/Pages/FoodOrderOperationsTrace.razor) |
| 한 문장 목적·완료 결과 | 내 주문 진행과 수령 상태를 확인 | 현재 주문의 다음 이행 단계를 수행 | 추천 배차와 현재 배달을 수행하고 정산 진행 확인 | 한 주문의 배달·정산 진행과 복구 필요 확인 |
| 기본·보조 정보 | 대상/메뉴/상태/금액, 기존 수령정보 접기 | 현재 단계/상품/금액/주 행동, 기존 상세 접기 | 기존 지도/업무/금액/미확정/오류, 기존 내 정보 영역 | 상태/정산/진행, 기존 상세/지급 테스트 접기 |
| 주 행동·실패·복귀 | 기존 조회·상세·수령, 오류 재시도/인증 복귀 | 서버가 허용한 다음 단계, 조회 실패 차단/재시도 | 기존 선택/수락/거절/픽업/전달, 실패 재조회/인증 복귀 | 기존 조회, 찾지 못함/오류/입력 변경 안내 |
| 책임 판정 | 동일 업무의 시각 정돈 | 동일 업무의 시각 정돈 | 동일 업무의 시각·반응형 정돈 | 동일 업무의 시각 정돈 |

기존 [책임 점검](page-responsibility-review-r1.md), [음식점 사용성](restaurant-order-usability-r1.md), [흐름 안정화](app-flow-stability-r1.md)의 판단과 API·DB 연결을 유지한다. 역할 분리/독립 업무 통합·새 페이지·상태·요율·실제 결제/입금은 추가하지 않는다.

## 코드의 표현 책임

- 공통 RoleAppTheme·role-app-foundation과 BackOfficeTheme는 같은 색/글자 체계를 소비한다. 기존 역할 enum과 Create API는 유지한다.
- 주문자 shell의 그림자·작은 글자·터치 크기를 정돈하고 음식 목록은 한 표면 안의 행, 선택 표시와 숫자 강조로 구분한다. 상세/개인정보/오류/수령 행동은 유지한다.
- 음식점은 흰 상단과 중립 배경, 절제된 표면·구분선·조작 크기를 적용한다. 넓은 화면은 목록2열, 모바일은1열이며 기존 문구·행동·조건은 동일하다.
- 기사는 기존 light/dark 자원을 유지하고 명시적 표시 토큰을 사용한다. 상단 조작 줄바꿈과 가로 목록의 내용 측정을 적용한다. 큰 글씨에서 날짜/버튼이 설명 폭을 독점하지 않도록 요약과 정산은 전체 폭 행을 쓰고 조작 묶음은 줄바꿈한다. 추천/현재 배달의 바인딩·문구·명령·조건을 보존한다.
- 기사 `App.xaml.cs`는 전역 스타일 초기화 뒤 같은 MainPage를 생성하도록 조립 순서를 보완했다. 기존 DI 등록·모델·Window 이벤트·업무 동작은 유지한다. 페이지에 자원을 중복 병합하던 중간안은 제거했다.
- 관리자는 공통 테마를 소비하고 반복 진행 카드를 구분선 행으로 표현한다. 기존 상세/지급 테스트의 접기와 미확정/테스트 안내는 유지한다.

공통 테마 상속은 전수 페이지의 배치·실행 검증을 뜻하지 않는다. `SsalddelApp` Community/Figma는 별도 범위다. 이번 작업은22개 표현 경로·기사 초기화1개·기존 조작 높이 시험1개다. 기존 dirty 작업이 섞인 파일이 있으므로 HEAD 전체 차이를 이번 변경만의 규모로 해석하지 않는다.

## 검증 기록

실제 Android3앱·관리자 웹의 변경 전후 화면과 검증은 [시각 변경 기록](../../Changes/2026-10-03-visual-design-r1.md)에 있다. 주문자/음식점412px·320px, 관리자 데스크톱·320px/키보드 포커스·미발견, 기사320 DIP·Android 글자 설정2.0/정산 스크롤을 확인했다. 최종24경로/설치 APK3개 지문은 Git 제외 `artifacts/local/visual-design-r1/verification.json`, 음식점/기사 보존 검사는 각각 `ui-design-r1/`·`ui-modern-design-r1/`에 둔다.

관련131/131·전체3.5 build 통과. 최종 Task `20261003-131243`은5,648/5,655로 기준과 같은7실패가 남고 새 실패0이며 전체 게이트는 미통과다. 전수 페이지·모든 상태·Web200% 글자 확대·다크 모드·전체 접근성 적합·물리 단말·실경로/실송금은 미검증이다. 공통 테마 상속을 이 실행 범위보다 넓은 완료 증거로 해석하지 않는다.
