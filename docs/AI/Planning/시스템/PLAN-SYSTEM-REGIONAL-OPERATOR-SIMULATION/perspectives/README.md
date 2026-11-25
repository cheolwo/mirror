# [기획 · 홍달 입장별 기획 목차 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r316]

2026-11-20 13:37 KST · Claude 제작실 · `PerspectiveFirstPlanningConfirmed / AllRolesRegrouped / FrameAndIndexOnly / NotReadyToDispatch / ProductUnchanged`

- **사용자(11-20):** 「카페를 경영하는 사람 입장, 뭐 배달 플랫폼을 경영하는 입장, 그리고 뭐 또 운전을 해서 이제 음식을 가져다주는 배달 기사님의 입장, 각각의 입장에 따라서 기획들을 이렇게 나눠두고, 그 나눠둔 것에 따라서 … 유니티 상에서 어떻게 표현을 할지에 대해서 그렇게 결정을 해나가는 그런 방식」
- **고른 것:** 예전 역할을 **전부** 다시 나눈다 · 첫 입장은 **배달 플랫폼 경영자** · 이번에는 **틀과 목차만**.
- 이어받는 기획: [r295 같은 동네를 다른 입장에서](../role-perspective-research-and-relaxed-observation.design.r242.md#r295-보존--같은-동네를-다른-사람의-입장에서-경영하기) · [r296 관점별 경영 패키지](../role-perspective-research-and-relaxed-observation.design.r242.md#r296-보존--관점별-경영-패키지-통합) · [r299 업무 UX와 스토리 구분](../role-perspective-research-and-relaxed-observation.design.r242.md#r299-보존--역할별-업무-ux와-스토리-기획) · [r300 보유 자료로 본 역할 후보](../role-perspective-research-and-relaxed-observation.design.r242.md#r300-보존--보유-자료로-살펴볼-역할-후보) · [r302 역할별 업무 UX 목차](../role-perspective-research-and-relaxed-observation.design.r242.md#r302-보존--역할별-업무-ux-기획서-목차) · [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도) · [r309 역할 선택 · 로딩 · 지도](../role-perspective-research-and-relaxed-observation.design.r242.md). 이 판은 그 역할 목록(r302 47개 + r296의 센터 운영자 후보)을 **사업 단위의 갈래 11개**로 다시 묶고, 입장마다 같은 9칸을 둔다.

## 1. 원칙

여러 역할의 현황을 한 지도로 읽으려면 [총괄 관찰 입장](../role-perspective-research-and-relaxed-observation.design.r242.md#총괄-관찰-r1--구-지도의-역할-레이어)을 고른다. 업무를 확정하는 경로는 각 역할 기획에 이어진다.

1. **한 화면은 한 사람의 자리에서 본다.** 지도 · 게임 시간 · 장부는 같이 쓰고, 입장마다 보이는 대상 · 수치 · 패널 · 할 수 있는 결정이 다르다 [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도).
2. **입장의 경계를 먼저 정한다.** 그 사람이 보는 정보와 못 보는 정보를 정한 뒤 그 안에서 일어나는 변화를 따라간다. 상대의 사정은 「같은 건의 상대 보기」로만 읽는다 [r295 같은 동네를 다른 입장에서](../role-perspective-research-and-relaxed-observation.design.r242.md#r295-보존--같은-동네를-다른-사람의-입장에서-경영하기).
3. **입장 하나씩, 정할 것 하나씩.** 9칸이 한 입장에서 닫히면 그 입장의 화면 시안과 개발 계획으로 넘어간다.
4. **업무 UX와 이야기를 나눈다.** 이 목차는 업무 UX다. 사람 사이의 대화 · 사건은 [r270 첫 시즌 이야기](../first-season-story-synthesis.r101.md) [r299 업무 UX와 스토리 구분](../role-perspective-research-and-relaxed-observation.design.r242.md#r299-보존--역할별-업무-ux와-스토리-기획).
5. **출시판 먼저.** 화면은 구 지도(출시판)에서 먼저 정하고, 동 디오라마(내부판)에서의 모습은 7칸에 따로 적는다 [출시판 · 정밀판 제안](../../../../Handoffs/2026-11-19-Claude-제안-출시판과내부정밀판.md).

## 2. 입장 기획서의 9칸

| 칸 | 답하는 질문 |
| --- | --- |
| 1. 누구 · 카드 한 줄 | 입장 선택 화면에 뜨는 이름과 한 줄(r309) |
| 2. 자리 | 시작 · 위임 · 관찰 · 배경 · 사건 상대 가운데 무엇인가, 언제 열리나 |
| 3. 하는 일 | 받는 것 → 판단 → 결정 → 넘기는 것(r299의 업무 UX) |
| 4. 보는 정보 · 못 보는 정보 | 이 입장이 알 수 있는 범위와 한계. 그 경계 안에서 무엇의 변화를 따라가나 |
| 5. 데이터 | 원천(공공 · 본인 관측 집계 · 역할 앱 장부 · 게임 값)과 상태(있음 · 준비 전 · 자료 미확인), 출시판에 쓸 수 있나 |
| 6. 화면 | 지도 축척(구 지도 출시판 / 동 디오라마 내부판) · 첫 화면 · 상단 수치 2~3개 · 패널 · 화면 번호(r258) |
| 7. 유니티 표현 | 카메라 · 강조 대상 · 빛(명덕) · 움직임 · 휴대폰(역할 앱) · 내부판 디오라마에서의 모습 |
| 8. 같은 건의 상대 | 누구에게 받고 누구에게 넘기나 · 상대 보기와 귀환 |
| 9. 정할 것 | 한 번에 하나. 맨 위가 다음 질문 |

**자리의 뜻**

- **시작** 플레이어가 입장 선택(r309)에서 고를 수 있다.
- **위임** 플레이어가 사람을 들여 맡긴다(육성). 그 사람의 일은 관찰로 보고, 단계가 오르면 직접 자리가 열릴 수 있다.
- **관찰** 같은 주문 · 출고의 상대로 잠깐 본다. 그 사람의 업무를 대신 확정하지 않는다.
- **배경** 인구 · 유동 같은 집계로만 나타난다.
- **사건 상대** 이야기 · 사건에서 만나는 상대.

## 3. 입장 갈래 — 예전 역할 48개를 다시 나눔

나눈 기준: **누구의 가게 · 회사에서 일하나.** 같은 사업의 사람끼리 한 갈래에 둔다.

| 갈래 | 대표 입장 | 함께 있는 입장 | 단계 |
| --- | --- | --- | --- |
| 1. [배달 플랫폼](p01-delivery-platform.r316.md) · [동부권 한 판](p01-delivery-platform-east-seoul.r321.md) | 배달 플랫폼 본사 운영자 ★ | 지사장 · 배차 담당자 · 플랫폼 정산 담당자 | **지금 · 첫 입장** |
| 2. [배달 기사](p02-delivery-rider.r316.md) | 음식 배달기사 | — | **다음** |
| 3. [카페 · 음식점](p03-cafe-restaurant.r316.md) | 카페 점주 | 일반 음식점주 · 제과점주 · 바리스타 · 조리 담당자 · 픽업 인계 담당 · 매장 정산 담당자 | **다음** |
| 4. [가맹](p04-franchise.r316.md) | 가맹점주 | 가맹본부 공급 담당자 · 점포 검수 담당자 | **보류** |
| 5. [홍달 플레이스 · 광고](p05-place-advertising.r316.md) | 홍달 플레이스 운영자 | 입점 점주 · 광고 운영자 · 트래픽 중개업자 · 참여 보상 앱 운영자 · 탈취 계정 유통업자 | **보류(중개 · 공급은 11-19 보류)** |
| 6. [손님 · 동네 사람](p06-customers-residents.r316.md) | 주문자 | 장소를 찾는 고객 · 동네 주민 · 방문자 · 통근자 · 대중교통 이용자 | **관찰(주문자는 다음)** |
| 7. [창고 · 물류](p07-warehouse-logistics.r316.md) | 센터 운영자 | 센터 출고 담당자 · 피킹 작업자 · 포장 작업자 · 상차 작업자 · 화물기사 | **보류** |
| 8. [전자상거래](p08-ecommerce.r316.md) | 온라인 판매자 | 주문 · 출고 담당자 | **보류** |
| 9. [조달 · 무역](p09-procurement-trade.r316.md) | 원두 구매 담당자 | 수입 담당자 · 통관 담당자 · 식품검사 담당자 · 항만 물류 담당자 · 공항 물류 담당자 | **보류** |
| 10. [생산 · 산지](p10-production-origin.r316.md) | 농업인 | 축산 농가 운영자 · 어업인 · 산지유통센터 운영자 · 위판장 운영자 | **보류** |
| 11. [정산 · 회계 지원](p11-settlement-accounting.r316.md) | 회계 검토 담당자 | 세무 검토 담당자 | **보류** |

r302에서 옮긴 것: 픽업 인계 담당(동네 배달 → 카페 · 음식점) · 주문자와 장소를 찾는 고객(→ 손님 · 동네 사람) · 매장 정산 담당자(→ 카페 · 음식점) · 플랫폼 정산 담당자(→ 배달 플랫폼) · 센터 운영자(r296 후보 → 창고 · 물류의 대표).

## 4. 정해 나갈 순서

1. **배달 플랫폼 본사 운영자** — [배달 플랫폼](p01-delivery-platform.r316.md)의 「정할 것」 1번부터
2. 배달 기사 — [배달 기사](p02-delivery-rider.r316.md)
3. 카페 점주 — [카페 · 음식점](p03-cafe-restaurant.r316.md)
4. 위 셋과 같은 건으로 이어진 사람: 지사장 · 배차 담당자(위임), 픽업 인계 담당 · 주문자(관찰)
5. 나머지 갈래는 보류한다. 다시 여는 때(제안): 구 지도 출시판 첫 판과 역 한 곳의 첫 고리가 돌 때.

## 5. 모든 입장이 같이 쓰는 바탕

[r314 개념 구조](../hongdal-place-core-concepts.design.r314.md) · [r311 홍달 플레이스 본편](../hongdal-place-steam-seoul-discovery.proposal.r310.md) · [r312 구 지도 경영](../hongdal-place-district-board-release-map.design.r312.md) · [r315 현실 자료 우선](../hongdal-place-map-observation-first.design.r315.md) · [구 지도는 시안 6](../../../../Handoffs/2026-11-20-Claude-정리-구지도는시안6으로.md) · [동 디오라마 목차](../../PLAN-SYSTEM-ADMIN-DONG-DIORAMA/README.md) · [r287 길을 따르는 배달 선](../soft-road-following-delivery-lines.design.r212.md) · [r258 화면 번호](../current-screen-numbering.catalog.r258.md) · [r259 탑다운 화면](../top-down-screen-hierarchy.design.r259.md) · [r261 00 오행판](../five-element-root-management-board.design.r260.md) · [r174 하루 결산](../live-calendar-daily-management.baseline.r174.md) · [r146 집중 · 이완](../focus-relax-shared-management.design.r146.md) · [r269 함께 밝아지는 세계](../warmth-perspective-taking-and-shared-light.direction.r269.md) · [r270 첫 시즌 이야기](../first-season-story-synthesis.r101.md)

## 6. 유니티에서 입장을 다루는 법(제안)

- 입장 하나 = **입장 프로필** 한 벌: 보이는 레이어 · 강조 대상 · 카메라 위치 · 상단 수치 · 패널 · 할 수 있는 명령. 같은 장면에 프로필만 바꿔 씌운다.
- 출발점은 지금 코드의 패키지 자리(`SagajeongDispatchPlatformPackage.cs` · `배달플랫폼Policy.cs`)와 관찰 전환(`SagajeongDispatchObservation.cs`)이다(r295 · r308의 대조 기록) [패키지별 화면 · 자료 구분표](../../../../Handoffs/2026-11-18-Claude-패키지별-화면자료-구분표.md).
- 프로필을 바꿔도 주문 · 자금 · 명덕은 바뀌지 않는다 [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도).
- 구현은 입장 하나의 9칸이 닫힌 뒤 개발 계획으로 따로 쓴다.

## 8. 내 배달 기록이 들어가는 곳(r320)

사용자 11-20: 「음식점 정보 … 메뉴 정보 … 배달 정산 정보 … 전부 좀 활용」. 원본은 PC의 배달 기록 폴더에 두고, 기획 문서에는 집계만 쓴다.

| 자료 | 무엇에 쓰나 | 입장 | 출시판에는 |
| --- | --- | --- | --- |
| 기사 배달료 항목 1,210건 | 픽업 · 전달 · 거리 · 할증 규칙 | [배달 플랫폼](p01-delivery-platform.r316.md#r320-내-배달-기록으로-맞춘-첫-값) · [배달 기사](p02-delivery-rider.r316.md) | 규칙만 |
| 거리 · 시간대 · 주문 금액 분포 | 게임 주문 만들기 | 배달 플랫폼 · [손님](p06-customers-residents.r316.md) | 분포만 |
| 전달 동 56곳의 건수 | 주문이 나는 곳 | 배달 플랫폼 · 손님 | 동별 비율만 |
| 픽업 매장 · 인허가 연결 | 홍달 플레이스 매장 정보 [r237](../hongdal-place-recorded-pickup-profiles-first.proposal.r237.md) | [카페 · 음식점](p03-cafe-restaurant.r316.md) · [플레이스](p05-place-advertising.r316.md) | 내부판만(출시판은 가명) |
| 메뉴 조합 · 메뉴 가격 | 주문 바구니 · 가게 메뉴판 | 카페 · 음식점 | 업종별 가격대만 |
| 정산(미션비 · 공제) | 기사의 실제 수입 구조 | 배달 기사 | 구조만 |
| 매일 늘어나는 기록 | 첫 값을 다시 맞춤 · 오늘 픽업한 가게가 빛으로 [r235](../personal-delivery-lights-dev-plan-and-mockup.r235.md) | 모두 | 다시 맞춘 값만 |

## 7. 확정 · 미정 · 다음 질문

**확정** · 기획을 입장별로 나눈다. 예전 역할 48개를 11갈래로 다시 나눴다. 첫 입장은 배달 플랫폼 본사 운영자다.

**미정** · 각 입장의 9칸. 첫 화면에 열어 둘 시작 입장의 수(r308의 미정을 잇는다).

**다음 질문 하나** · [동부권 한 판](p01-delivery-platform-east-seoul.r321.md#13-r329-엔진-레벨업은-돈--경험--확정) r329 — 엔진 레벨업은 돈 + 경험으로 확정(사용자 11-20). 고비 카드 한 장 +30 · Lv3에 경험 1,500이라는 가안 값을 그대로 둘까? (r325의 환류 질문은 Codex 기획실에서 `Asked`다.)
