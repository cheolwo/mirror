# 지역 협업 화면 책임 · r5

> 중단된 확장 스캐폴드의 이력이다. 당시 구현은 검증이 완료되지 않았으며 실행·DI·EF 모델에서 분리해 로컬 보관했다. 현재 구현은 [생활 교류 최소판 r6](exchange-mvp.r6.md)이다. 아래 페이지/API를 현재 사용 가능한 기능으로 해석하지 않는다.

[페이지 기준](../../../../Architecture/WholeRoadmapPagePrinciple.md) · [시각 기준](../../../../Architecture/RoleAppVisualDesignStandard.md) · [기존 설계](residential-goods-quick-delivery.design.r4.md)

한 계정의 복수 활동은 희망 조건이며 실제 권한은 업무별 신청 → 소유자 지정 → 본인 수락으로 생긴다. 음식 배달과 섞지 않고 기존 화물·상품·재고·출고 원장을 사용한다. 현재 구현은 Simulation 검증이며 실제 지급을 실행하지 않는다.

## 페이지 책임

| 주 사용자·페이지 | 대상·한 문장 목적 | 완료·주 행동 | 기본 정보 / 보조 정보 | 독립 업무 | 진입·실패·복귀 | 코드·API·DB |
|---|---|---|---|---|---|---|
| 일반 참여자 `/community/work` | 지역 업무를 고르고 내 업무로 이동 | 가까운 업무 / 맡은 업무 선택 | 업무명·활동·시간·보수 / 상태 | 활동·공간·상품 등록은 별도 경로 | 로그인 분리, loading/empty/error/retry, 변경 후 재조회 | NeighborhoodHome → NeighborhoodWorkspaceViewModel → 공통 workspace → 지역협업업무/참여 |
| 참여자 `…/profile` | 할 수 있는 활동과 범위만 등록 | 동의 후 저장·참여 중지 | 복수 활동·지역·시간·용량·이동수단·중량 | 업무 수락·계정 권한 부여 제외 | 서버 판본 확인, 오류 시 초안 유지 | NeighborhoodProfile → profile → 지역협업참여프로필 |
| 공간 소유자 `…/spaces` | 공간 제공 동의·철회 | 공간 등록 또는 철회 | 구획·용량·대략 위치 / 비공개 주소 | 상품 등록 별도 | 철회 후 상품/예약 재조회; 보관 중 실제 인계 필요 | NeighborhoodSpaces → spaces → 지역협업공간/생활권물류거점/창고 |
| 구매자 `…/goods` | 지역 상품을 선택 | 구매 신청 페이지 이동 | 상품·가용 수량·가격·지역 | 상품 등록은 `…/goods/new` | 수량 부족·철회 오류를 숨기지 않음 | NeighborhoodGoods → goods → 판매상품/입고상품/상품게시 |
| 판매자 `…/goods/new` | 보유 상품을 등록 | 본인 보유 확인 후 등록 | 제공 공간·상품·수량·가격·중량 | 음식점 메뉴·AI 이미지 제외 | 실패 시 초안 유지 | NeighborhoodGoodsRegistration → goods POST → 기존 입고/판매 원장 |
| 구매자 `…/goods/{id}/request` | 수량·상품/배송 조건을 신청 | 0원 배송도 별도 동의 | 수량·운임·시간창·대략 위치 / 비공개 전달 주소 | 결제 제외 | 재고 판본 충돌 시 재조회 | NeighborhoodPurchaseRequest → purchases POST → 구매요청/재고예약 |
| 판매자·구매자 `…/requests` / `…/requests/{id}` | 신청 목록에서 해당 구매를 확인·진행 | 판매 확인 → 구매 확인 → 준비 → 수령; 취소/반납 | 상품·금액·상태 / 연결 업무 | 배송 담당자 지정·수락은 업무 상세 | 상세 복귀·중복 제출 방지 | NeighborhoodRequests / NeighborhoodPurchaseDetail → 구매 action → 구매요청/출고/운송 |
| 업무 담당자 `…/{id}` | 선택한 한 업무만 신청·지정·수락·완료 | 상태별 서버 허용 행동 | 활동·시간·보수·대략 위치 / 해당 신청자 | 관리자 권한·다른 구매 접근 제외 | 처리 실패 재조회; 결과 불명 시 같은 ID 재확인; 정확 주소는 진행 배송만 | NeighborhoodWorkDetail → work actions/access → 참여/용량예약/기존 운송 |
| 요청자 `…/new` | 보관·포장·인계·조율 업무 요청 | 조건 확인 후 생성 | 시간·중량·보수·0원 동의 | 배송은 구매로 연결 | 입력 검증·초안 보존 | NeighborhoodTaskRegistration → work POST → 지역협업업무 |

책임 판정: 각 입력 업무와 탐색·상세를 별도 경로로 분리한다. 공통 폼·오류 표시·작업 상태는 공유하며 인증 폼을 업무 화면에 넣지 않는다. `SsalddelApp`은 공통 컴포넌트의 소비자이며 기존 전문 앱을 유지한다. Figma 대응은 경로/화면 책임/상태 표 기준이며 실제 Figma 편집·기기 설치 완료와 구별한다. 실행 검증 결과는 구현 완료 보고에 별도로 기록한다.
