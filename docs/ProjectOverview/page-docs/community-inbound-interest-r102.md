# 주민 입고 희망 작성의 페이지 책임 r102

[현재 승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-screen-integration.design.r102.md). 기존 생활 교류·필요글과 공개 동네 선택을 재사용한다.

| 항목 | 책임 |
| --- | --- |
| 사용자·페이지 | 주민/일반 이용자, `/community/exchange` → `/community/exchange/write?purpose=inbound-interest`; Web와 일반 살뜰 앱이 같은 `ExchangeWrite`를 소비 |
| 목적 | 원하는 상품·대략 수량·시기를 공개 필요글로 알린다 |
| 완료·주 행동 | 기존 게시 API가 글 ID를 반환하면 해당 글로 이동. 글 작성은 주문·입고 확정이 아님 |
| 기본 정보 | 입고 희망의 비구속 안내, 제목·설명, 선택 공개 동네, 글 관리 비밀번호 |
| 보조 정보 | 상품/규격·수량·시기 작성 안내. 알 수 없는 조건은 미정으로 남김 |
| 독립 업무 | 상품 판매·입고 협의·재고 예약·업무 수락·정산은 기존 전문 페이지/서버 소유. 주민에게 창고 운영 권한을 추가하지 않음 |
| 진입·초안 | 빈 초안의 처음 진입에서만 필요글로 준비. 기존 제목/본문/비밀번호가 있으면 분류·내용 유지. query 변경으로 작성 중 초안을 재분류하지 않음 |
| 실패·복귀 | 공개 동네 조회 loading/error/retry, 목록의 확인된 동네만 선택. 저장 응답 유실은 초안·오류·기존 목록 확인 경로 유지, 자동 재POST 없음 |
| 코드·API·DB | 기존 route query → `NeighborhoodExchangeViewModel` → `NeighborhoodExchangeClient` → `POST api/v1/community/posts` → 기존 게시글 원장. SalesOffer/공동 원장/거래 관심모집을 만들지 않음 |
| 검증 | [변경 기록](../../Changes/2026-10-15-community-role-screens-r102.md)에 실제 시험·브라우저·DB 확인과 한계를 기록 |

`purpose`는 화면 작성 문맥이며 별도 서버 업무 유형·상품 수요 집계 키가 아니다. 새 제목/본문이나 개인 정보를 URL에 넣지 않는다. 공개 글만으로 단지 주민 관계를 인증하거나 공급업체에 실제 주문량을 제공했다고 표시하지 않는다. Figma와 물리 Android 확인은 이번 검증 범위가 아니다.
