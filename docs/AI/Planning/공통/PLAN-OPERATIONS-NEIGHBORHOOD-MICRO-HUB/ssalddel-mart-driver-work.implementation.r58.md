# 살뜰마트 기사 수락·픽업·전달 소프트웨어 연결 r58

- 기획 ID: `PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB`
- 상태: `Approved / ReadyToDispatch / LocalSimulationValidation / OperationalDeferred`
- 승인 근거: 2026-10-10 사용자가 r57 다음 우선순위인 마트 기사 앱 수락→픽업→전달 연결 진행을 요청했다.
- 주 책임: `operations`. [r57 연결 기반](ssalddel-mart-dispatch-connection.implementation.r57.md)·[r56 표본](ssalddel-mart-package-pickup.implementation.r56.md)을 보존한다.

## 이번에 닫을 결과

실제 마트 주문·출고·피킹·포장 근거로 만든 배차 제안을 기사 본인이 수락하고, 창고 담당자가 발급한 현재 전표의 QR 문자열 또는 네 자리 번호를 입력해 즉시 픽업하며 전달 완료를 기록한다. FDriverApp에 독립 살뜰마트 배송 진입을 두고 같은 서버 정본을 재조회한다. 카메라·프린터·USB 설치 없이 검증한다. 기존 음식 주문이나 화주 의뢰를 가짜로 생성하지 않는다.

## 권위와 상태 계약

1. 기존 운송 큐·마트 원천 Adapter를 재사용하고 마트 전용 배달시도·현재 전표·멱등 요청 결과·후속 Outbox를 영속화한다. 기존 음식 원천은 별도로 유지하며 음식 Service는 마트 원천을 음식 주문번호로 fallback하지 않는다.
2. `MartDriverWorkflow:Enabled` 기본 OFF, 기존 마트·음식 배달·창고 기능 관문과 `SsalddelExecution:Mode=Simulation`을 모두 요구한다. 이번 구현은 실제 Operational 활성화 승인이 아니다.
3. 기사 ID는 인증 사용자에서만 얻는다. 제안 수락/거절은 현재 추천 기사·만료·수신 의사·진행 업무 상한과 추천 round/큐 갱신 시각을 검사한다. 같은 요청 ID는 같은 의미일 때만 재사용하며 다른 입력은 충돌이다.
4. 수락 이후에는 배정 기사·현재 시도 revision을 검사한다. 픽업은 현재 활성 전표 판본·QR/네 자리·현재 전체 피킹/포장 근거를 검사하며, 재고 차감·출고 인계·시도/큐 전이·요청 결과·후속 예약을 같은 SQL transaction으로 확정한다. 하나의 입고를 여러 출고에 중복 인정하지 않는 r57 안전 범위를 유지한다.
5. 전표 발급은 현재 모든 관련 창고 접근권한을 가진 담당자의 명시 POST다. GET은 발급하거나 회전하지 않는다. QR/번호는 기사 목록과 로그에 공개하지 않으며 인쇄는 필요 없다.
6. 전달은 같은 기사·시도의 픽업 증빙을 요구한다. 픽업으로 재고가 출고됐으므로 포장 준비 Adapter를 다시 요구하지 않는다. 전달 완료·주문자 수령·정산 대상·실지급은 독립이며 이번에 지급 완료를 생성하지 않는다.
7. Mongo·마트 원장·운송 인덱스 후속은 SQL commit 뒤 기존 Adapter를 소비하는 재처리 가능한 Outbox 경계로 처리한다. 후속 실패를 업무 원본 실패나 가짜 성공으로 숨기지 않는다. 명시 요청 결과 GET과 정본 조회로 결과 불명확을 복구하며 자동 POST 재전송하지 않는다.
8. FDriver 전용 화면은 제안/현재 수행과 다음 행동 하나를 표시한다. 음식 화면·지도·정산을 삭제/대체하지 않는다. 로딩·빈 상태·기능 비활성·실패·재조회·뒤로 복귀를 구분한다. 네 자리 제출 또는 QR 문자열 제출이 픽업 의사이며 추가 완료 버튼은 두지 않는다.

## 검증 상한

격리 SQLite에서 실제 Controller/Service/DB·전표·요청 결과·Outbox 경로의 정상 수락→픽업→전달, 새 DbContext 재조회, 중복·다른 기사·기한/추천 변경·낮은 revision·잘못된/이전 전표·미완 포장·부분 저장 실패·후속 재처리와 음식 원천 회귀를 검증한다. 실제 MySQL/Mongo/Redis·인증 HTTP·푸시·외부 업체·실지급·Android 물리 단말·물리 인쇄는 별도 증거다. 새 schema migration은 준비/시험만 하며 공용 DB에 적용하지 않는다. FDriver 컴파일과 가능한 UI 렌더 증거를 별도로 남기며 정적 시험을 실제 기기 화면으로 보고하지 않는다.

## 인계·소유

개발 root는 계약·Controller·DI·공유 Context/migration·기획/변경 기록을 소유한다. 서버 담당은 마트 전용 도메인·Configuration·Service/후속·readiness, 앱 담당은 FDriver 전용 화면/VM/Client, 시험 담당은 연결·권한·복구·음식 회귀 시험을 소유한다. 정확 경로와 기획 SHA-256은 r58 작업 지시에 고정한다. 기존 dirty 작업을 보존하고 commit·push·배포하지 않는다.
