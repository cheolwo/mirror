# 음식점 메뉴 복귀와 주문 안내 보완 r1

2026-10-04 사용자가 역할 앱 기존 흐름의 보완안을 승인한 범위다. [페이지 책임 원칙](../../Architecture/WholeRoadmapPagePrinciple.md)과 [역할 앱 시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 따르며, 기존 [주문 사용성](restaurant-order-usability-r1.md)·[상태 갱신](food-state-refresh-r1.md)·[알림과 전표 복구](food-notification-daily-settlement-r1.md)를 이어간다. 새로운 주문 정책·지도·결제·인쇄 기능을 추가하지 않는다.

## 페이지 책임 카드

| 항목 | 메뉴 관리 `/menus` | 주문 상세 `/orders/{OrderNo}` |
| --- | --- | --- |
| 사용자·대상 | 음식점 사용자가 자기 메뉴의 등록·수정을 확인한다 | 음식점 사용자가 선택 주문을 확인하고 허용된 조리·준비를 완료한다 |
| 한 가지 질문·결과 | 이 메뉴를 어떻게 공개할지 정하고 서버 저장 결과를 확인한다 | 지금 확인할 주문 내용과 다음에 처리할 행동을 판단한다 |
| 기본 정보 | 목록/편집 중 하나, 공개·품절·가격·저장 결과 | 현재 단계·주 행동·메뉴·수량·금액·기존 요청사항. 배차 예외일 때 기존 상태·최근 안내 |
| 보조 정보 | 편집 취소·새로고침. 로그인은 독립 인증 화면 | 조리시간 변경·거절·전표·긴 주문/배차 이력은 기존 보조 영역 |
| 실패·복귀 | 인증 종료 시 목록·입력을 숨기고 로그인. 같은 계정이면 메모리 초안과 미확정 등록 요청 ID를 복원한다. 자동 제출하지 않는다 | 조회 실패는 기존 오류와 행동 잠금을 유지한다. 배차 불가·조리 시작 후 재배정 대기는 펼침 없이 읽는다 |
| 입력 수명 | 명시적 로그아웃·다른 계정·앱 프로세스 종료 시 폐기. 자격 정보와 업무 목록을 초안에 넣지 않는다 | 기존 선택 주문과 자동 갱신 수명을 그대로 사용한다 |

## 1. 페이지

[메뉴 화면](../../../RestaurantDeskApp/Components/Pages/Menus.razor)은 기존 입력 항목·공개/품절·미확정 등록 잠금을 유지한다. 로그인 필요 상태에는 업무 입력과 목록을 렌더하지 않는다. 미확정 등록 중 일반 업무 이동은 계속 막고 인증 종료 뒤 자기 앱의 `/login` 이동만 허용한다. 외부 URL이나 비슷한 이름의 경로를 인증 복귀로 취급하지 않는다.

[로그인 화면](../../../RestaurantDeskApp/Components/Pages/Login.razor)은 기존 인증 전용 Layout을 유지한다. 로그인 후 알림으로 선택한 주문을 먼저 연다. 그 대상이 없으면 동일 계정의 메뉴 복귀를 선택하고, 다른 계정이면 기존 `/orders`로 돌아간다. 로그인 화면에 메뉴 내용·요청 ID·토큰을 표시하지 않는다.

[주문 상세](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor)는 주문 메뉴 카드의 구분선 아래에서 기존 요청 텍스트를 읽는다. 내용에 전달/조리 지시가 함께 있어도 임의로 분류하지 않으며 새로운 수령인 주소·연락처 항목을 추가하지 않는다. 줄바꿈을 유지하고 Razor의 일반 텍스트 바인딩을 사용한다. 확인된 공란과 상세 데이터 미확인을 구별한다.

배차 불가 또는 조리가 시작된 뒤 기사 재배정을 기다리는 동안에는 기존 배차 상태와 최근 안내를 현재 단계 카드에 표시한다. 정상 배차 대기에는 추가 예외 상자를 만들지 않는다. 종료 주문이나 최신 조회 실패에서는 오래된 배차 안내를 현재 예외로 강조하지 않는다. 서버가 허용한 행동은 바꾸지 않는다.

## 2. 코드

- [Menus 코드뒤](../../../RestaurantDeskApp/Components/Pages/Menus.razor.cs)는 세션 종료 구독, 요청 취소·세대 확인, 같은 owner의 초안 복원과 명시적 재시도를 소유한다. 늦은 응답은 다른 페이지/계정의 상태를 바꾸지 않는다.
- [RestaurantMenuDraftStore](../../../RestaurantDeskApp/Services/RestaurantMenuDraftStore.cs)는 앱 메모리의 메뉴 입력과 미확정 요청만 소유한다. owner와 세대가 달라진 저장·복원을 거부하며, 명시적 로그아웃은 화면이 이미 사라진 뒤에도 초안을 폐기한다. 디스크·URL·로그에 저장하지 않는다.
- [MauiProgram](../../../RestaurantDeskApp/MauiProgram.cs)은 named HttpClient와 단일 [RestaurantAuthService](../../../RestaurantDeskApp/Services/RestaurantAuthService.cs)를 연결한다. API Client·알림·레이아웃·메뉴의 `SessionEnding`이 같은 인스턴스에서 전달된다. 별도의 인증 계약을 추가하지 않는다.
- 인증 서비스의 내부 세대와 세션 쓰기 직렬화는 늦은 refresh/login 응답이 새 세션을 덮거나 종료하지 않게 한다. [기존 HTTP Client](../../../RestaurantDeskApp/Services/Ssalddel음식주문Client.cs)는 원래 owner/인증 세대에 요청을 결속하고 각 전송의 access token을 고정한다. 정상 동일 owner의 토큰 갱신과 기존 401 재시도는 유지한다.
- [OrderDetail 코드뒤](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor.cs)는 기존 DTO/AvailableActions에서 요청 텍스트와 배차 예외의 표시 여부만 계산한다. 조리·거절·인계 권한을 화면에서 생성하지 않는다.

## 3. DB·검증 경계

테이블·필드·API route·메뉴의 revision/중복 등록 판정·주문 상태 전이 변경은 없다. 초안 저장소는 영속 멱등 원장이 아니며 앱 종료 이후의 복원을 보장하지 않는다. 같은 요청 ID를 보존하는 것은 기존 메뉴 API의 중복 판정에 재확인 요청을 전달하기 위한 것이다.

신규 회귀시험은 [메뉴 세션 복귀](../../../Ssalddel.Tests/Clients/RestaurantMenuSessionRecoveryTests.cs), [인증 actor 경합](../../../Ssalddel.Tests/Clients/RestaurantAuthActorRecoveryTests.cs), [주문 안내 표시](../../../Ssalddel.Tests/Clients/RestaurantOrderPresentationTests.cs)다. 코드뒤 lifecycle와 메모리 Client/모의 HTTP를 사용하며 실제 Android 화면·운영 DB·사용자 클릭 증거로 해석하지 않는다. 기존 메뉴 미리보기는 링크한 제품 파일과 scoped 인증/초안 DI를 정합시켰으며 API 실패를 합성 자료로 대체하지 않는다.

신규 전용36개와 관련 회귀시험은 최종 Fast409/409에 포함해 통과했다. 전체 역할 앱 빌드, 실제 메뉴 목록·입력·저장 복귀의390px 브라우저 화면, 처리11검사·모의 HTTP34검사를 확인했다. 최종 Task의 기존7실패·새 실패0 및 실제 실행 한계는 [통합 기록](../../Changes/2026-10-04-role-app-polish-r1.md)에 둔다. 원본 사본과 지문은 비공유 `artifacts/local/role-app-polish-r1/restaurant/`에 보존한다. 커밋·푸시는 수행하지 않았다.
