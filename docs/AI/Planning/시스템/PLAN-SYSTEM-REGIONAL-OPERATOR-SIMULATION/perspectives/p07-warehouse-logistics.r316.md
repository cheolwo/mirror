# [기획 · 입장 — 창고 · 물류 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r316]

2026-11-20 13:37 KST · Claude 제작실 · `PerspectiveFirstPlanningConfirmed / AllRolesRegrouped / FrameAndIndexOnly / NotReadyToDispatch / ProductUnchanged`

[입장별 기획 목차](README.md) · 9칸의 뜻은 목차 2절. 이 갈래의 단계: **보류**.

게임 테크트리의 창고 갈래와 판매 주문 인계는 [창고와 커머스 r3](../voluntary-rider-recruitment-and-operating-tech-tree.design.r264.md#배달-플랫폼-연구와-정책-r3--창고와-커머스)를 따른다.

## 이 갈래의 입장

| 입장 | 자리 | 하는 일(r302 제목) | 기존 기획 | 단계 |
| --- | --- | --- | --- | --- |
| **센터 운영자** | 시작 후보 | 출고 → 상차 → 운송 → 점포 인계에서 내가 책임질 일 | [r296 관점별 경영 패키지](../role-perspective-research-and-relaxed-observation.design.r242.md#r296-보존--관점별-경영-패키지-통합) · [r164 센터 다층 관찰](../logistics-center-multifloor-cutaway.design.r164.md) | 보류 |
| **센터 출고 담당자** | 위임 → 관찰 | 출고 요청 확인 · 작업 배정 · 운송 인계 | [r71 가맹 · 공급 · 물류](../../PLAN-SYSTEM-FRANCHISE-OPERATIONS/integrated-franchise-logistics-operations.proposal.r71.md) | 보류 |
| **피킹 작업자** | 관찰 | 랙 위치 확인 · 상품 집품 | [r199 물류센터 내부 작업](../warehouse-location-labels-and-interior-workflow.design.r199.md) | 보류 |
| **포장 작업자** | 관찰 | 품목 확인 · 포장 · 출고 전달 | [r199 물류센터 내부 작업](../warehouse-location-labels-and-interior-workflow.design.r199.md) | 보류 |
| **상차 작업자** | 관찰 | 출고품 · 차량 확인과 적재 인계 | [r164 센터 다층 관찰](../logistics-center-multifloor-cutaway.design.r164.md) | 보류 |
| **화물기사** | 관찰 | 운송 의뢰 · 상하차 확인 · 도착 | [r71 가맹 · 공급 · 물류](../../PLAN-SYSTEM-FRANCHISE-OPERATIONS/integrated-franchise-logistics-operations.proposal.r71.md) | 보류 |

자료 근거: 가상 출고 한 건을 센터 → 화물기사 → 점포 검수로 읽는 흐름은 Windows에서 확인됐다. 창고 · 단지 · 터미널 자료는 시설 근거이며 개별 작업자의 작업 기록은 확인 범위에 없다(r300). [전국 물류시설](../../../../DataCollection/Research/2026-11-16-national-logistics-facility-locations.md) · [관점 전환 반환](../../../../Handoffs/2026-11-16-Codex-역할관찰-중화모드-인계반환.md)

## 센터 운영자 — 9칸(틀)

기존 기획에 있는 것만 옮겼다. 비어 있는 칸은 이 입장을 열 때 하나씩 정한다.

| 칸 | 지금 아는 것(근거) |
| --- | --- |
| 1. 누구 · 카드 한 줄 | 물류센터 운영자 |
| 2. 자리 | 시작 후보 [r296 관점별 경영 패키지](../role-perspective-research-and-relaxed-observation.design.r242.md#r296-보존--관점별-경영-패키지-통합) |
| 3. 하는 일 | 출고 대기 · 예정 물량 확인 [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도) |
| 4. 보는 정보 · 못 보는 정보 | 보는 것: 물류 거점, 출고 차량과 연결된 점포 [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도). 점포 검수의 결정은 점주에게 남긴다 [r296 관점별 경영 패키지](../role-perspective-research-and-relaxed-observation.design.r242.md#r296-보존--관점별-경영-패키지-통합) |
| 5. 데이터 | 전국 물류시설 · 게임 출고 |
| 6. 화면 | 11-3 화물기사 · 11-4 센터 출고 [r258 화면 번호](../current-screen-numbering.catalog.r258.md) |
| 7. 유니티 표현 | 센터 다층 관찰 [r164 센터 다층 관찰](../logistics-center-multifloor-cutaway.design.r164.md) |
| 8. 같은 건의 상대 | 화물기사 · 점포 검수 담당자 |
| 9. 정할 것 | 다시 열 때 채운다 |
