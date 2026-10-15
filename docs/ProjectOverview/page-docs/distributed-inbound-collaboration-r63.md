# 공급자와 공간 제공자의 입고 협의 화면 r63

## r102 목록의 상품별 일정 요약

[r102 승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-screen-integration.design.r102.md). 입고 협의 명령과 원장은 기존 책임을 유지한다.

| 항목 | 책임 |
| --- | --- |
| 사용자·대상 | 기존 공급자/공간 운영자에게 조회 허용된 입고 계획·상품 행 |
| 한 문장 목적 | 어떤 상품이 어느 거점에 언제 도착·보관되며 실제 얼마나 입고됐는지 확인 |
| 기본 정보 | 조회된 SKU·거점/위치, 예정/실입고 수량, 도착 시간창·보관 종료, 협의 상태 |
| 보조 정보 | 변경안과 이전 확정 예약이 다르면 각각의 일정·확보 수량을 분리 |
| 주 행동·독립 업무 | 기존 계획 선택 버튼과 선택 후 협의·사진 실입고 행동을 유지. 요약은 읽기 전용이며 보수·주민 상품 조회를 수행하지 않음 |
| 코드·API·DB | 기존 `분산입고Screen` → `분산입고일정요약Panel`. 동일 workspace DTO 소비, 새 API/권한/재고 변경 없음 |
| 미확인·오류 | 조회된 위치가 없거나 일정이 기본/역전 값이면 확인 대기. 변경안 일정이 실제 입고·기존 예약 갱신을 뜻하지 않음 |
| 검증 | [r102 변경 기록](../../Changes/2026-10-15-community-role-screens-r102.md)에 좁은 렌더와 실제 브라우저 확인 수준을 구분 |


[수용 설계](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.implementation.r63.md)에 따라 기존 역할별 분산 물류 화면을 확장한다. Unity나 새 앱을 만드는 작업이 아니다. 실제 실행 결과와 한계는 [변경 기록](../../Changes/2026-10-12-supplier-space-inbound-r63.md)에 남긴다.

## 페이지 → 코드 → DB 책임

| 기존 화면 | 사용자가 결정할 내용 | 공통 UI / 서버 | 저장 정본 |
|---|---|---|---|
| `/workspace/warehouse/distributed/sites` | 어떤 상품을 어떤 조건·용량으로 받을지 | `분산공간수용조건Panel` / `SiteAcceptancePolicyAsync` | `distributed_sites.AcceptancePolicyJson`과 독립 정책 판본 |
| `/workspace/shipper/distributed/inbound`, `/workspace/warehouse/distributed/inbound` | 선택한 입고 행의 제안·수정·당사자 동의·거절·취소·메모 | `분산입고협의Panel` / `InboundProposalAsync`, `InboundConsentAsync`, `InboundDecisionAsync`, `InboundNoteAsync` | 기존 Plan/Line/Lot와 `distributed_lots.InboundCollaborationJson` |
| `/workspace/{warehouse 또는 shipper}/distributed/agreements` | 현재 보관 합의와 변경안의 판본별 동의 | `분산보관변경협의Panel` / 기존 Agreement 명령 | `distributed_storage_agreements`의 현재 조건과 별도 PendingTerms |
| 위 입고 화면의 검수 영역 | 유효한 승인 범위 안에서 사진·바코드·실제 받은 수량 확인 | `분산입고Screen`, 기존 `분산사진업무Screen` / `PrepareReceive`, `CheckInboundApproval` | 기존 사진 증빙·완료 행·입고상품·재고 이력·당시 보관 합의 사본 |

API는 기존 `/api/v1/distributed-fulfillment` 아래 공간 수용 조건 1개와 입고 행별 협의 명령 4개를 추가한다. 실제 접두사는 `DistributedFulfillmentCodes.BaseRoute`, 화면 진입은 `분산이행Navigation`을 기준으로 한다. 로그인한 actor를 서버에서 다시 확인하고 프런트의 버튼 표시만으로 권한이나 입고 완료를 확정하지 않는다.

## 단계에 필요한 정보

공간 소유자는 허용·거부 품목, 식품·가공·포장·온도, 수용 단위와 공간 전체 한도, 출입 조건을 설정한다. 공급자는 등록된 입고 원천과 실제 소유 관계를 사용해 상품 사실·수량·도착 시간창·보관 종료·공급대금 또는 무상 조건·책임·반환 조건을 제안한다. 공급자 유형은 소개이며 농가라는 이유만으로 농산물이라고 분류하거나 공장이라는 이유로 동의 권한을 부여하지 않는다.

협의 화면에는 선택한 행의 조건, 현재 판본, 다음 담당자, 대기 이유와 해당 사용자의 허용 행동을 보여준다. 메모는 동의가 아니다. 위탁 판매자의 제안과 실제 물품 소유자의 동의를 구분한다. 현장 관리자가 당사자가 아니라면 공급대금·책임·반환 조건과 협의 대화는 제공하지 않는다.

현재 유효한 보관 합의와 미확정 변경안을 함께 보되 별개로 표시한다. 변경 제안만으로 현재 계약을 없애지 않는다. 같은 변경 판본에 양측이 동의해야 새 조건으로 전환한다. 기존 실제 입고의 당시 조건 사본과 정산 근거는 유지한다.

검수에는 서버가 현재 입고를 허용한 행만 나타낸다. 정책·계약·시간창·공간 전체 용량을 서버에서 재검사하고, 확인되지 않은 단위나 기존 재고 점유량을 0으로 가정하지 않는다. 응답이 끊기면 요청 번호·입력·기대 판본을 보존하고 기존 영수증을 먼저 조회한다.

## 완료의 의미

협의 승인·공간 예약·실제 입고·입고 보수 발생·출고·고객 수령·지급은 서로 다른 결과다. 예약 수량 전체가 실제 재고나 보수가 되지 않는다. 부분 입고는 받은 수량만 기존 입고 정산의 작업 근거에 연결하며 실제 이체는 실행하지 않는다. 이미 입고한 실물을 취소로 지우거나 공급자에게 반송 완료했다고 표시하지 않는다.

로컬 HtmlRenderer 출력과 브라우저 화면 검토는 공통 UI의 검증이며 Android APK·실제 휴대폰 카메라·Azure·운영 DB·실제 계약 체결 검증은 별도다. 기본 OFF와 Simulation 경계는 유지한다.

## r64 시각 정돈과 페이지 책임 유지

[시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)과 [수용 설계 r64](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.implementation.r64.md)를 소비한다. 공간 소유자는 받을 품목·보관 조건을, 공급자와 공간 제공자는 선택한 입고의 조건·동의·다음 행동을, 계약 당사자는 현재 조건과 미확정 변경안을 판단한다. 이번에는 각 페이지의 완료 결과·route·입력 수명·권한과 복구 책임을 바꾸지 않는다.

기본 정보는 현재 상태·예정/실제 수량·다음 담당·대기 이유·협의 조건·양측 동의다. 보조 기록/거절/메모의 기존 details는 유지하며 독립 업무를 새 접기로 숨기지 않는다. 단색 Material Outlined 아이콘은 제목과 버튼 문구를 보조한다. 공통 표시 컴포넌트 분산입고Icon과 분산이행Screen의 inbound-visual 범위 CSS가 시각 표현을 소유하고 기존 VM/서버는 계속 행동과 결과를 결정한다. [변경·화면·검증](../../Changes/2026-10-12-inbound-visual-r64.md).

## r65 구조 정리와 책임 위치

[수용 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.implementation.r65.md)의 정리에서는 기존 route·사용자 질문·주 행동·기본/보조 정보·완료 결과를 유지한다. 분산이행PageViewModel.cs는 계정/수명·조회·명령과 영수증을, .Orders.cs는 원주문 선택과 주문 처리를, .Photos.cs는 사진 업무를, 기존 .Recovery.cs는 보존된 요청의 재확인을 소유한다. 모두 같은 클래스이므로 입력/권한/복구의 실행 경계를 새 서비스로 이동하지 않는다.

표현 문자열은 기존 분산이행표시.cs가 소유하고 각 패널은 그 결과를 렌더한다. 일반 업무 상태와 입고 협의 상태의 차이·문맥별 미확인 문구는 유지한다. [변경·검증·남은 동작 보완](../../Changes/2026-10-12-inbound-cleanup-r65.md)을 참조한다.
