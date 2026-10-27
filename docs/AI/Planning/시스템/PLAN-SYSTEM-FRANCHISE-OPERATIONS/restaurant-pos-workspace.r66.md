# 음식점 PC 주문 처리 작업대 r66

- 상태: Approved / CompletedLocalValidation / OperationalDisabled
- 승인: 2026-10-22 사용자 “여부 한번 진행해 줘 봐봐.” 직전 음식점 PC POS 제안의 첫 구현 단계 수용.
- 기준: dev/mirror-integration, d3a19163ba96a7dbed31028f639f3bf0c534ae79.

음식점 운영자가 접수된 주문을 선택하고 메뉴·요청사항을 확인해 수락, 기사 배정 후 조리 시작, 픽업 준비까지 처리한다. PC에서는 고정 탐색·주문 목록·선택 상세를 조립하고 좁은 화면은 목록과 상세를 순서대로 제공한다. 기존 /orders, /orders/{OrderNo}와 서버 AvailableActions·정본 조회·보호된 미확정 요청을 유지한다.

목록은 신규/진행/확인 필요를 현재 수신함 범위에서 집계한다. 확인 필요는 신규/진행과 중첩할 수 있는 주의 표시이며 주문 수를 합산하지 않는다. 새 주문·주기 조회는 현재 선택/입력/스크롤을 초기화하지 않는다. 주문 전환 중 늦은 응답을 배제하고 명령 처리 중 대상 전환은 잠근다. 상세 오류는 현재 상태 미확인과 재조회 행동을 남긴다. 인쇄 실패를 수락 실패로 바꾸지 않는다.

새 옵션 계약·과거 주문 검색·실영업 상태 변경·지도 ETA·PC 알림 설정 확대·배포·설치·실결제·외부 발송은 이번 범위 밖이다. 준비 완료, 기사 픽업, 배송/수령 완료를 합치지 않는다.

허용 쓰기: RestaurantDeskApp/Components/Pages/OrderInbox.razor*, OrderDetail.razor*, RestaurantDeskApp/Services/RestaurantPosPresentation.cs, RestaurantDeskApp/Services/음식점주문InboxProjection.cs, RestaurantDeskApp/Components/Layout/MainLayout.razor*, NavMenu.razor, RestaurantDeskApp/wwwroot/app.css, Ssalddel.Tests/Clients/RestaurantPosWorkspaceTests.cs, Ssalddel.Tests/Ssalddel.Tests.csproj, eng/RestaurantPosPreview/**, 이 기획/작업명세, docs/ProjectOverview/page-docs/restaurant-order-usability-r1.md, docs/Changes/2026-10-22-restaurant-pos-workspace-r66.md, docs/Changes/README.md, docs/assets/changes/2026-10-22-restaurant-pos-workspace-r66/**.

검증: 순서/분류/선택 유지/상태 구분·명령 잠금 회귀, 기존 음식점 복구/알림 시험, scoped Fast/Task, Windows 소비 빌드, 실제 연결한 제품 Razor의 합성 자료 브라우저 확인. 화면 검증과 실제 서버·물리 프린터·휴대폰·영업 검증은 구분한다. 기존 dirty 파일과 index를 보존하고 commit/push하지 않는다.

구현 검토에서 목록/상세 동시 조회가 같은 표시 객체를 공유하므로 수신함 투영에 낮은 Revision 응답 배제만 추가한다. 기존 책임·저장·요청 ID는 유지한다. 독립 취소 협의는 PC에서 기존 상세 경로로 인계해 자체 명령과 선택 잠금 책임을 섞지 않는다.

## 실행 반환

[변경·화면 증거](../../../../Changes/2026-10-22-restaurant-pos-workspace-r66.md)에 집중342개·전체15,098개/환경16개생략, 최종 소비 빌드와 실제 제품 Razor 합성12항목/12PNG를 남겼다. 실제 영업·프린터·Windows 앱 조작·단말·배포·commit/push는 수행하지 않았다.
