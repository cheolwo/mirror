# 음식 배달 상태 갱신·주문 작성 복귀 r1

[네 역할 운영 보완 제안서](../../Reports/2026-10-03-food-four-role-improvement-proposal.md)의 1순위를 적용한다. 기존 화면에서 같은 주문의 최신 상태를 읽고, 작성 중 로그인 만료·화면 이탈에서 안전하게 복귀하는 범위다.

## 페이지

| 화면 | 사용자 질문 | 보완 |
| --- | --- | --- |
| 음식점 `/orders/{OrderNo}` | 지금 이 주문에서 무엇을 할 수 있나 | 해당 주문의 변경 알림·재연결·30초 조회로 서버 상태를 다시 읽는다. 작성한 조리시간·거절 사유는 보존한다. |
| 주문자 `/food/restaurants` | 선택한 메뉴와 수령 정보로 주문을 이어갈 수 있나 | 최종 401 뒤 로그인 입력을 제공하고 작성 내용과 재시도 요청 ID를 유지한다. 화면 이탈·선택 변경·재로그인 이후의 늦은 응답은 적용하지 않는다. |

기존 페이지 책임과 주 행동·보조 입력·수동 새로고침을 유지한다. 새로운 취소·중단·지급 화면은 이 범위에 포함하지 않는다.

## 코드

- 음식점 [상세 마크업](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor)과 [수명·조회·명령 코드](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor.cs)를 분리했다. 알림의 상태를 그대로 확정하지 않고 기존 DeskService를 통해 서버의 `AvailableActions`와 현재 주문을 다시 읽는다.
- 조회 또는 명령 도중 받은 알림은 후속 재조회로 합친다. 선택 주문 변경·화면 해제·인증 종료 뒤의 이전 응답은 취소/선택 판본으로 차단한다. 일시 조회 실패에는 마지막 성공 내용을 유지하고 명령을 잠그며, 재조회 성공 뒤 복구한다.
- 조리시간은 최초 성공 조회에서 초기화한다. 사용자가 편집하지 않은 값은 다른 기기의 확정 변경을 따라가고, 편집 중인 값은 자동 조회로 덮어쓰지 않는다. 입력은 타이핑 즉시 작성 값에 반영한다. 최초 조회 실패 뒤 첫 성공도 같은 규칙을 적용한다. 연결 시도가 진행 중이면 후속 조회는 새 연결을 겹쳐 시작하지 않으며, 이탈 뒤 늦은 연결 실패도 무시한다.
- [실시간 Client](../../../RestaurantDeskApp/Services/음식점주문SignalRClientService.cs)는 현재 로그인 계정에 연결을 결속한다. 페이지 이탈은 페이지 구독만 해제하고, [명시 로그아웃](../../../RestaurantDeskApp/Components/Layout/MainLayout.razor)은 공유 연결을 종료한다.
- 주문자 [Workspace](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRestaurantWorkspace.razor.cs)는 화면 수명 토큰을 전달한다. [작성 VM](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/음식점탐색페이지ViewModels.cs)은 재로그인 세대·요청 ID·작업 취소를 함께 확인한다. 메뉴/수령 정보 변경은 전송 중인 사본과 구별해 이전 작업을 취소하고 새 요청 ID를 만든다. 같은 값의 입력과 같은 내용의 재로그인 재시도는 ID를 바꾸지 않는다. [작성 화면](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderComposer.razor)은 최종 401의 로그인 복귀를 표시한다. 최초 세션 복원 실패도 무한 진행 표시로 숨기지 않고 오류와 기존 로그인 입력을 보여 준다. 403·429·503을 로그인 종료로 바꾸지 않는다.

화면 요청 취소는 서버 주문 취소와 다르다. 화면을 떠나도 이미 저장된 주문은 있을 수 있으며, 응답 유실 뒤 동일 작성 내용의 재시도는 기존 요청 ID를 사용한다. 앱 재시작 뒤 작성 복원·요청 원장 복구는 이번 구현의 완료 범위가 아니다.

## DB와 API

스키마·상태 전이·요율·권한·공개 계약은 변경하지 않았다. 음식점 상세는 기존 주문 상세 API·서버 revision·허용 행동을 읽고, 주문자 등록은 기존 요청 ID의 멱등 저장 경로를 사용한다. 앱의 표시 갱신이 조리·배차·결제의 권위가 되지 않는다.

## 검증

검증 결과와 실제 화면 근거는 [변경 기록](../../Changes/2026-10-03-food-state-refresh-r1.md)에 정리한다. 음식점 source-linked 코드의 수명/경합 시험은 Razor·기기 렌더 증거와 구별하며, Android 표본은 격리 Development/Simulation 서버의 합성 주문만 사용한다.
