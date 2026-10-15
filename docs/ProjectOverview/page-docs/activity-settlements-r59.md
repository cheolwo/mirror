# 활동·정산 내역의 페이지·코드·DB 책임 r59

## r102 보수 확인 표시 보완

[r102 승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-screen-integration.design.r102.md). 기존 상세의 약정 동의 목적은 유지하고 그 판단에 필요한 본인 보수를 먼저 보여준다.

| 항목 | 책임 |
| --- | --- |
| 사용자·대상 | `/activity-settlements/{agreementId}`에 접근할 수 있는 본인, 서버가 반환한 선택 약정 |
| 목적·주 행동 | 자신의 기본보수·수량·기간·지급 조건을 확인하고 기존 `AllowedActions`에 따른 약정 동의/후속 행동 판단 |
| 기본 표시 | 본인 수령 기본보수 행, 포장 보수 포함, 서버 단가/약정 수량, 약정 유효기한과 지급 기준 |
| 상세 표시 | 전체 약정과 배송비 포함 참고 항목. 약정 유효기한을 작업 완료기한으로 바꾸지 않음 |
| 제외·독립 업무 | 타인의 보수·실제 지급 원본·원시 ID를 요약에 노출하지 않음. 단지 기여금 계산·관리비 납부는 이 패널의 책임이 아님 |
| 코드·권위 | `활동정산Screen` → 표시 전용 `사전보수요약Panel`/helper. 기존 VM/Client/서버 동의·지급 명령 유지 |
| 실패·미확정 | 기존 인증/복구/오류 상태 유지. 모르는 금액·수량은 미확정, 명시 무료와 구분, 배송 포함을 별도 지급액으로 더하지 않음 |
| 검증 | [r102 변경 기록](../../Changes/2026-10-15-community-role-screens-r102.md)의 범위만 확인. 실제 일감 시장·계약 분류·입금 완료 증거 아님 |


[승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/activity-compensation.implementation.r59.md)·[검증 반환](../../Changes/2026-10-12-activity-compensation-r59.md)을 따른다. 기존 [생활 거래 정산 r38](neighborhood-settlement-r38.md)의 원장과 지급/수령 구분을 유지한다.

| 화면 | 한 가지 책임 | 필요한 정보와 행동 |
| --- | --- | --- |
| `/activity-settlements` | 본인 관련 약정 찾기 | 업무 종류·판본/상태·본인에게 지급될/지급할 금액·미확정/분쟁 구분, 상세 진입 |
| `/activity-settlements/{agreementId}` | 선택 약정의 현재 확인과 후속 행동 | 약정 동의·철회, 작업비 확인·이의, 지급 신고에 대한 누적 수령 확인, 근거를 갖춘 성과금 발생 기록 |

`Ssalddel.WebApp/Pages/활동정산Page.razor`와 `SsalddelApp/Components/Pages/활동정산Page.razor`가 공통 `활동정산Screen.razor`를 사용한다. 생활협업·분산이행 작업공간은 내역 링크를 제공하고 수행 카드에 전체 정산을 끼워 넣지 않는다. `활동정산PageViewModel`은 본인·선택 약정·서버 `AllowedActions`를 확인하며 응답 유실 시 명령 영수증/상세를 재조회한다. 새로운 요청으로 자동 재전송하지 않는다.

금액 미확정은 무료나 0원으로 표시하지 않는다. 내부 배분·통과자금·기존 운임 참고는 받을 돈으로 다시 합산하지 않는다. 완료·발생·양측 확인·지급 신고·수령자 확인·은행 확인은 서로 다른 사실이다. 화면에서 원증빙 주소나 사용자 ID를 노출하지 않는다.

`NeighborhoodSettlementController`의 기존 인증 경로와 `NeighborhoodSettlementUseCase`가 약정·현재 원천·동의·Revision·요청 영수증을 소유한다. 새 `GET /api/v1/neighborhood-settlements/{id}/activity-reward-preview`와 `POST /{id}/activity-reward-accruals`는 성과금 미리보기/발생 기록만 수행한다. 지급 명령이 아니다. 지급자는 배분 전체를, 참여자는 본인 배분·기여만 받는다. 별도 정산 약정에 참여했다고 기존 업무 접근권이 새로 생기지 않는다.

`활동성과배분Policy`는 입력과 근거를 검사하는 순수 계산기다. `NeighborhoodSettlementSourceResolver`가 실제 분산 작업 원장을 조회하고 새 행별 완료 근거로 주문별 수행을 연결한다. 구형/미귀속 입고·보관 기록은 추정하지 않는다. 새 컬럼은 기존 `distributed_receipts`에 보호 저장하고, 성과금 항목/동결 계산은 기존 `neighborhood_settlement_entries`/`neighborhood_settlement_receipts`에 기록한다.

기본 OFF·Simulation·정확한 profile 관문을 유지한다. 외부 송금·은행 확인·운영 활성화·공용 DB migration 적용·휴대폰 사용 검증은 별도다.
