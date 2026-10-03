# OrdererApp 화면 문서

[전체 화면 문서](../README.md) / [앱 전체 카탈로그](../../app-page-catalog.md)

이 문서는 OrdererApp 에 속한 화면별 README를 모은 색인입니다. 각 화면 문서는 캡처 이미지, 화면 책임, 사용자와 참여자, API/서버 연계, 보안 점검을 별도로 설명합니다.

인증과 업무 표시의 기준은 [로그인·업무 책임 카드](../auth-workspace-separation-r1.md)와 [페이지 원칙의 인증 경계](../../../Architecture/WholeRoadmapPagePrinciple.md#인증과-업무의-경계)를 참조합니다. 음식점 탐색·마트 요청의 상세 문서와 함께, 별도 상세 README가 없는 재료 자동집단화·수요 작성은 기존 [GroupPurchaseProducts](../../../../OrdererApp/Components/Pages/GroupPurchaseProducts.razor)와 [GroupPurchaseDemandCreate](../../../../OrdererApp/Components/Pages/GroupPurchaseDemandCreate.razor)의 인증 모드를 확인합니다. 공개 탐색과 로그인 입력을 나누며, 인증 후 기존 선택·수요 초안으로 복귀하고 저장은 사용자가 별도로 실행합니다.

| 페이지 ID / 제목 | 라우트 | 분류 | 화면 책임 | 캡처 |
| --- | --- | --- | --- | --- |
| [OrdererApp-P01 - 주문자 홈](OrdererApp-P01/) | / | 보조 | 주문자 홈 | 완료 |
| [OrdererApp-P02 - 공동구매 개요](OrdererApp-P02/) | `/group-purchase` | 1.0 | 공동구매 화면 진입과 버전 경계 안내 | 재캡처 필요 |
| OrdererApp-P02-1 - 재료 자동집단화 | `/group-purchase/products` | 1.0 | 카드 한 번 클릭으로 기존 집단 합류 또는 새 비구속 집단 시작 | 재캡처 필요 |
| OrdererApp-P02-1-1 - 내 개별주문 의향 | `/group-purchase/wishes` | 0.5 | 본인의 재료별 개별주문 의향 목록과 수정·철회 진입점 | 재캡처 필요 |
| OrdererApp-P02-1-2 - 여러 개별주문 의향 등록 | `/group-purchase/wishes/new` | 0.5 | 여러 재료와 수량을 한 번에 선택하되 재료별 개별주문 의향 원장을 독립 저장 | 재캡처 필요 |
| OrdererApp-P02-1-3 - 내 개별주문 의향 상세 | `/group-purchase/wishes/{WishLedgerId}` | 0.5 | 본인 의향의 수량·거래 문맥과 그 의향에서 이어진 공동 진행 조회·철회 | 재캡처 필요 |
| OrdererApp-P02-1-4 - 내 개별주문 의향 수량 수정 | `/group-purchase/wishes/{WishLedgerId}/edit` | 0.5 | 본인의 활성 비구속 의향에서 희망 수량만 변경 | 재캡처 필요 |
| OrdererApp-P02-1-5 - 내 공동 진행 | `/group-purchase/groups` | 1.0 | 본인 원함이 포함된 공동 집계만 목록으로 조회 | 재캡처 필요 |
| OrdererApp-P02-1-6 - 내 공동 진행 상세 | `/group-purchase/groups/{AutoGroupId}` | 1.0→1.5 | 본인 원함의 집계와 연결된 같이 수입 준비 원장 진입점 조회 | 재캡처 필요 |
| OrdererApp-P02-2 - 공동구매 상품 근거 상세 | `/group-purchase/products/{ProductId}` | 1.0 | 한 상품의 HS·보관·모집 근거 읽기 | 재캡처 필요 |
| OrdererApp-P02-3 - 개별주문 의향 상세 조건 | `/group-purchase/demands/new/{ProductId}` | 0.5 | 상품·배송권·희망 수량·수령 조건을 직접 조정하는 비구속 Action | 재캡처 필요 |
| OrdererApp-P02-4 - 수입 원가 참고 | `/group-purchase/import-review/{ProductId}` | 1.5 준비 | 한 상품의 Simulation 원가 조회 | 재캡처 필요 |
| OrdererApp-P02-4-1 - 같이 수입 준비 현황 | `/group-purchase/imports/{GroupImportLedgerId}?autoGroupId={AutoGroupId}` | 1.5 준비 | 본인이 참여한 집단에 연결된 같이 수입 원장의 준비도와 미확인 항목 읽기 | 재캡처 필요 |
| OrdererApp-P02-4-2 - 같이 수입 공급자 근거 | `/group-purchase/imports/{GroupImportLedgerId}/suppliers?autoGroupId={AutoGroupId}` | 1.5 준비 | 공급자 후보·원출처·확인 시각 읽기 | 재캡처 필요 |
| OrdererApp-P02-4-3 - 같이 수입 견적·예상 비용 | `/group-purchase/imports/{GroupImportLedgerId}/costs?autoGroupId={AutoGroupId}` | 1.5 준비 | 재료별 견적 조건과 예상 도착 비용 근거 읽기 | 재캡처 필요 |
| OrdererApp-P02-4-4 - 같이 수입 품목분류·규제 | `/group-purchase/imports/{GroupImportLedgerId}/classification?autoGroupId={AutoGroupId}` | 1.5 준비 | HSK·HTS 후보와 국가별 공식 검토 항목 읽기 | 재캡처 필요 |
| OrdererApp-P02-4-5 - 같이 수입 포워더 인계 | `/group-purchase/imports/{GroupImportLedgerId}/handoff?autoGroupId={AutoGroupId}` | 1.5 준비 | 사람이 인계한 범위와 기록된 LCL/FCL·견적·일정 회신 읽기 | 재캡처 필요 |
| OrdererApp-P02-4-6 - 정보 제공 근거 | `/group-purchase/imports/{GroupImportLedgerId}/consent?autoGroupId={AutoGroupId}` | 1.5 준비 | 저장된 정보 제공 범위와 별도 동의 근거 확인 상태를 읽기 전용으로 표시 | 재캡처 필요 |
| OrdererApp-P02-5 - 선적 조회 | `/group-purchase/shipments` | 1.5 준비 | 문서관리번호 한 건 조회 | 재캡처 필요 |
| [OrdererApp-P03 - 주문자 화물 주문](OrdererApp-P03/) | /cargo | 확장 | 주문자 화물 주문 | 완료 |
| [OrdererApp-P04 - 음식 주문 홈](OrdererApp-P04/) | /food | 확장 | 음식 주문 홈 | 완료 |
| [OrdererApp-P04-1 - 음식점 주문](OrdererApp-P04-1/) | /food/restaurants | 확장 | 음식점 주문 | 완료 |
| [OrdererApp-P04-2 - 마트 공개 상품 목록](OrdererApp-P04-2/) | `/food/mart` | 확장 | 공개 상품 검색·판매 가능 조건·서버 페이징 | desktop 완료 |
| [OrdererApp-P04-2-1 - 마트 공개 상품 상세](OrdererApp-P04-2-1/) | `/food/mart/products/{ProductId}` | 확장 | stable product ID 공개 설명·재고 투영·구매 근거 읽기 | stable-ID 실제 확인 |
| [OrdererApp-P04-2-2 - 마트 상품 구매후기 작성](OrdererApp-P04-2-2/) | `/food/mart/reviews/{ProductId}` | 확장 | 완료 원장 참여자의 명시적 공개 후기 저장과 같은 상품 재조회 | 390px 완료 |
| [OrdererApp-P04-3 - 마트 비구속 주문 요청](OrdererApp-P04-3/) | `/food/mart/order/{ProductId}` | 확장 | 공개 상품 한 건의 주문 요청 저장과 같은 요청 ID 재조회 | stable-ID·390px 로그인 경계 확인 |
| [OrdererApp-P05 - 내 주문](OrdererApp-P05/) | `/orders` | 0.5 | 로그인한 주문자의 개별주문 원장 목록과 현재 단계 조회 | 재캡처 필요 |
| OrdererApp-P05-1 - 개별주문 원장 상세 | `/orders/{OrderLedgerId}` | 0.5 | 주문 당사자 권한으로 한 개별주문과 연결 이행 원장 조회 | 재캡처 필요 |
| OrdererApp-P05-2 - 음식 주문 이력 | `/orders/food` | 확장 | 기존 음식 주문 목록과 주문번호별 상세 조회 | 재캡처 필요 |
| [OrdererApp-P99 - 미발견 페이지](OrdererApp-P99/) | /not-found | 시스템 | 미발견 페이지 | 완료 |
