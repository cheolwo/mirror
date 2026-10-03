# WarehouseManagerApp-P04-4 - 출고 운송의뢰 작성과 인계

- 경로: `/warehouse/general/transport-request-draft`
- 통합 웹 경로: `/warehouse/general/transport-request-draft`
- 통합 웹 별칭: `/work/outbound/transport-request-draft`
- 상태: 확장 · 기존 화면 캡처 보유
- capability 분류: `Beta / Simulation`(현재 카탈로그 값)

출고예정 검토를 통과한 정확한 `outboundPlanId` 한 건을 다시 조회해 실제 하차지, 선택 연락처, 희망 상차·도착 일시, 차량 유형과 취급 메모를 검토하고 명시적으로 운송의뢰를 저장하는 페이지다. 원장 조회, 입력 교차검증, 저장, 페이지 조정과 host 인증 책임을 각각 분리한다.

출고 원장의 포장·수량·출발 창고 근거는 이전 검토 결과를 재사용한다. 하차 담당자명·연락처는 선택 입력이며 실제 입력만 전달한다. 미입력을 사용자 ID나 출발 창고의 대표번호로 채우지 않는다. 이름 100자·연락처 50자 상한을 적용하고 저장 전 검토에서는 전화번호를 마스킹한다. 취급 메모에는 연락처·계좌를 넣지 않도록 안내한다.

희망 일정의 입력과 검토는 한국시간이다. 도착 시각이 상차 시각보다 뒤인지 확인하고 API 전송 시 `UTC+09:00`에서 UTC로 변환한다. 서버에 이미 UTC를 보내는 다른 클라이언트의 계약은 바꾸지 않는다. 서버는 희망 시각을 시작으로 1시간 시간창을 저장하고 기사 화면은 UTC를 한국시간으로 표시한다.

`입력값 로컬 검토`는 메모리에만 `OUT-{outboundPlanId}-REVIEW` 결과를 만든다. **명시적 저장은 기존 재위탁 API를 호출하여 운송의뢰·상품 연결·운송원장을 생성하고 가용 수량을 예약 수량으로 옮기며 출고예정에 같은 의뢰 ID를 연결한다.** 이후 같은 출고예정 ID를 다시 조회한다. 현재 capability의 Simulation 분류를 무저장·무효력의 근거로 사용하지 않는다. 실제 기사 배차 수락·상차 확인과 운송 수행, 결제·입금·정산은 각 후속 단계의 권한과 서버 전이를 따른다.

## 페이지 책임

[기사 메뉴 축약·창고 하차 연락처 연결 r1](../../cargo-menu-handoff-r1.md)의 입력→기존 원장→기사 조회 관계를 따른다. 시험과 실제 앱 확인은 [변경 기록](../../../../Changes/2026-10-03-cargo-menu-handoff-r1.md)에 구분한다.

| 항목 | 기록 |
| --- | --- |
| 주 사용자·페이지/소스 | 창고 관리자·재고/출고 담당자. 문서 ID `WarehouseManagerApp-P04-4`, capability PageKey `warehouse-app-transport-request-draft`와 route를 유지하며 [Warehouse host](../../../../../WarehouseManagerApp/Components/Pages/TransportRequestDraft.razor), [Web host](../../../../../Ssalddel.WebApp/Pages/WarehouseTransportRequestDraftPage.razor)가 [공용 workspace](../../../../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/SsalddelTransportRequestDraftWorkspace.razor)를 소비한다. |
| 대상·진입 문맥 | 이전 출고예정 검토에서 받은 `outboundPlanId` 한 건. 다른 원장으로 자동 대체하지 않는다. |
| 한 문장 목적 | 창고 담당자가 선택한 출고예정의 실제 운송 조건을 확인해 같은 의뢰 ID로 기사 인계를 이어 간다. |
| 완료 결과·상태별 주 행동 | 입력 가능: 로컬 검토 → 명시 저장. 저장됨: 같은 ID로 인계 상태 조회. 기사 수락·등록 차량 확인됨: 현장 기사 신원·차량·상품 확인 후 출고 인계 완료. |
| 기본 정보 | 상품·수량·출발 창고 근거, 하차지, 선택 담당자·연락처, 한국시간 일정, 차량과 취급 조건. 연락처 변경은 이전 검토를 무효화한다. |
| 보조 정보 | 기존 검토 근거와 인계 상태. 저장 전 전화 요약은 마스킹하며 출발 상세 주소는 서버 창고 설정을 사용한다. |
| 독립 업무·제외 정보 | 기사 배차 수락·운송 수행과 결제·정산은 각 역할 업무로 인계한다. 가짜 담당자·전화번호, 사용자 ID 대입, 공개 요약의 개인정보 추가는 하지 않는다. |
| 진입·실패·복귀 | 미선택·미조회·차단 원장을 구분하고 검토 목록으로 복귀한다. 다른 ID/선택 해제는 입력을 즉시 비우며 늦은 이전 응답을 적용하지 않고 최신 대상 조회를 이어 간다. API 실패는 현재 초안을 보존한다. 직접 재고 인계 입력은 저장 중 잠근다. |
| 코드·API·DB | [공용 ViewModel](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/운송의뢰초안페이지ViewModels.cs) → [원장 조회/인계 서비스](../../../../../Ssalddel.Ui.Common/Areas/App/Services/출고예정검토페이지Service.cs), [기존 입출고 서비스](../../../../../Ssalddel.Ui.Common/Areas/App/Services/공동구매창고Service.cs) → `GET outbound-plan-reviews/{id}`, `POST inventory/reconsignment`, `POST outbound-plan-reviews/{id}/handoff-complete` → [서버 업무](../../../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs). 인증·역할·수량·상태 변경은 서버가 결정한다. |
| 책임 판정·검증 | 같은 출고예정의 작성→확인→인계 목적을 한 workspace에서 유지한다. [회귀 시험](../../../../../Ssalddel.Tests/Ui/Common/WarehouseDropoffContactHandoffTests.cs)은 연락처/초안 수명·늦은 조회·UTC 왕복을 다룬다. 이번 변경의 시험 실행, 실제 창고 UI·API/DB·기사 전체 인계 검증은 별도 결과를 따른다. 기존 캡처를 새 입력의 실행 증거로 삼지 않는다. |

같은 실제 연락처 입력은 [직접 재고 인계](../../../../../WarehouseManagerApp/Components/Warehouse/WarehouseTransportHandoffPanel.razor)와 [화주 재위탁](../../../../../SsalddelApp/Components/Pages/ReconsignmentOrders.razor)에서도 기존 등록 흐름 안에 연결한다. [초안 복사](../../../../../Ssalddel.Ui.Common/Areas/App/Models/WarehouseTransportHandoffDraft.cs)는 UI 입력과 API 사본을 분리하고 미전송 `null`과 명시적 빈 문자열을 구별한다. 서버의 같은 의뢰 재시도에서는 `null`은 기존 값을 보존하며 명시적 값 변경은 충돌로 처리한다.

![기존 로컬 초안 화면: 이번 연락처·UTC 보완의 실제 UI 검증 증거는 아님](../../../../assets/changes/2026-07-20-warehouse-transport-request-draft/desktop.png)
