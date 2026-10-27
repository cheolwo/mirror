# 화물 입력과 추천 갱신의 페이지 책임 r77

[승인 계획](../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-input-refresh.implementation.r77.md)을 따른다. 기존 route·화면·정본을 사용한다.

| 기존 화면 | 한 가지 질문과 결과 | 정보·실패·복귀 |
|---|---|---|
| 생활배송 간편/상세 포장 | 어떤 포장을 몇 개 옮기는가? | 같은 포장 치수·수량·전체 중량/부피·팔레트·회전/적층. 내부 기사 배송의 누락을 보완하되 외부/회원/구형 입력을 추정하지 않는다. |
| 화주 운송 정보/모바일 일정 | 상하차까지 약속시간 안에 끝나는가? | 상하차 예상 작업 분과 시간창. 미입력/0분/음수를 구분하고 초안·고정 접수 사본에 보존한다. 작업시간 수정의 null 생략은 기존 값을 지우지 않으며 명시 초기화로 미확인으로 되돌린다. |
| 기사 추천 참여 | 최신 조건으로 이 운송을 맡을 수 있는가? | 보수·위치·주의사항·추천 판본. 조건이 바뀌면 확인을 해제하고 새 조건을 표시한다. 수락 POST를 자동 반복하지 않는다. |

실차 기록은 [관리자 차량 화면](vehicle-management-actual-loading-r77.md)이 소유하며 본인 차량 선택·법정 자격을 대신하지 않는다.

## 코드와 정본

- 생활배송 `NeighborhoodDeliveryRequest` → 공용 `NeighborhoodDeliveryLoadingInput` → 기존 UseCase → `CargoDTO`/`LocationContactDTO` → 의뢰 생성.
- 작업시간 `LocationContactDTO.작업시간분` → 생성/수정 Command → `화주운송의뢰.픽업_작업시간분/하차_작업시간분` → 조회/Mongo 사본 → 조율 Factory·일정·수락 검사.
- 화주 UI `운송의뢰작성ViewModel`/`운송모델작성Draft` → App handoff mapper / Web 등록 adapter.
- 기존 추천 조회·수락 후속은 본인 유효 추천의 현재 경로·수행·적재·약속을 재검증한다. 과거 계획에 새 지문만 붙이지 않는다.

PC/390px 실제 로컬 렌더와 합성 입력은 기기·실차·운영 DB 확인과 별개다. 검증/캡처는 [변경 기록](../../Changes/2026-10-19-cargo-input-refresh-r77.md)에 연결한다.
