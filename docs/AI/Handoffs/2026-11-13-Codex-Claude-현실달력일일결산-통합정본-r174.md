# Codex → Claude · 현실 달력·일일 결산 통합 입력 r174

- 역할/상태: 2026-11-13 · Codex 기획 작업실 · `ApprovedDirectionsPreserved / HandoffConsolidated / DevelopmentAcceptancePending / ProductUnchanged`.
- 현재 정본: [r174](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/live-calendar-daily-management.baseline.r174.md).
- 정본 SHA256: `a194611ef66923706029934ed02fc6f843187f80bb15a1a6242e9428e6f50ff9`.
- 이번 사용자 지시: 구현을 수정하지 말고 계획의 어폐·정산 등 용어와 어색한 표현을 다듬는다. 새 게임 규칙/수치 승인으로 확대하지 않는다.
- 대체 관계: [r172 인계](2026-11-13-Codex-Claude-현실달력절기통합-r172.md)와 [r173 인계](2026-11-13-Codex-Claude-일일일생결산-r173.md)는 당시 이력이다. 이 시간/결산/절기 범위의 현재 개발 입력은 r174 하나이며, 두 이전 정본과 hash는 보존한다.

## 현재 읽을 순서

**r174 정본 → 이 인계의 소스 대조 → 개발 수용/검증 명세 결속** 순서로 읽는다. r172/r173 본문을 각각 독립적인 현행 규칙처럼 다시 적용하지 않는다. 사용자 승인 방향과 Claude의 현재 결정 순회/가맹점 구현 소유는 유지한다.

## 문장 정리로 해결한 혼동

| 이전에 다르게 읽힐 수 있던 표현 | 적용할 의미 |
| --- | --- |
| 결산/정산/지급/확인 혼용 | 결산은 보고, 정산은 업무 금액의 산정·확정, 지급/수금은 돈의 이동, 보고 확인은 읽음 상태 |
| 확인 전후 날짜 불변 | 확인 명령의 추가 시간 진행 없음. 현실 시계의 정상 경과는 계속 반영 |
| 일→주→절기→오늘의 직렬 흐름 | 보고를 묶어 제시하는 순서. 과거 보고 미열람을 오늘 업무의 임의 차단 조건으로 만들지 않음 |
| 실제 기록일/수익·비용 | 게임 원장의 항목별 기준시각/금액. 수집·파일 저장·확인 시각과 구별하고 현금 수입/지출과 합치지 않음 |
| 절기일 첫 영업 전 | 현재 적용일의 새 업무 시작 전 방침 확인. 이미 진행 중인 작업은 안전한 종료/인계 뒤 안내 |

## 기존 소스와 대조할 최소 범위

- [LocalCampaign](../../../Ssalddel.Sagajeong.Core/UnityPackage/Runtime/LocalCampaign.cs)의 `Completed` 집계는 `Settlement` 존재를 읽고 `Balance`도 해당 가상 금액을 집계한다. 이를 전달 완료 건수·실제 지급액이라는 이름으로 일괄 해석하지 않는다. 원래 필드/호환 값은 보존하고 표시 의미를 대조한다.
- [SimulationTurnClosing](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationTurnClosing.cs)의 기존 확인은 Tick 진행을 포함한다. 보고/절기 선택 확인이 이 시간 진행을 실행하도록 연결하지 않는다.
- [SimulationArcanaTownLife](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationArcanaTownLife.cs)의 기존 활성 수명/동일 카드 제한은 r174의 절기 기간별 선택과 구별한다. 순수 검증/이력은 재사용하고 현재 프로필 의미를 덮지 않는다.
- 현실 달력 조회·관측 사본과 지도 구조 판본·완료/정산/지급 기록의 실제 시각/금액 필드는 개발 수용 시 각 소유 계약에 연결한다. 없는 필드의 회계정책이나 시각을 추측해 채우지 않는다.

수용 결과에는 담당·정확한 코드 쓰기 경로·기존 WI/검증 명세 연결과 r174의6절 검증 결과를 결속한다. 이번 문서 편집은 개발 활성화·ReadyToDispatch·코드 구현·시각 승인/Runtime·DB/예약·공용 E·commit/push가 아니다. 승인된 의미의 재확인과 통상 구현 선택에 대한 추가 허가를 요구하는 문서도 아니다.
