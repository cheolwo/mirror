# 게임 규칙·시나리오·성장

플레이어가 어떤 선택을 하고, 결과는 어떻게 달라지나요?

이미 구현한 배차·정산과 후속 기획을 구분해 반복 플레이의 규칙을 살펴보는 묶음입니다.

## 지금 있는 자료

- WPF와 공유하는 배차 후보·지정·수락·픽업·수령·가상 정산 엔진
- 가상 시계·일시정지·배속·캠페인과 기존 실행 검증 안내
- 주차·주말·시간대 물량, 기사 출근·모집·교육·성장, 가맹점 확대의 기획

## 게임과 연결할 맥락

같은 초기 상태와 선택을 재생하고, 주문·기사 수·준비 시간의 균형 및 다음 운영 선택을 검토합니다.

## 상태를 읽는 기준

- 이 묶음은 기존 기획과 코드의 참조입니다. 새 규칙이나 숫자를 확정하거나 게임에 적용하지 않습니다.
- 구현·실제 실행·기획상 방향은 문서별 상태를 따릅니다. 과거 구현 대기를 일괄 완료로 바꾸지 않습니다.
- 시간당 3~4건은 기획의 기준 예시입니다. 교육·무념은 이 게임의 성장 설정이며 현실 안전·소득의 보장 수치가 아닙니다.
- 교육 필수라는 옛 기록보다 r131의 운영자 선택(바로 투입/교육 후 투입)을 함께 읽습니다.

## 하나씩 생각할 점

1. 처음 반복해서 재미를 확인할 선택 한 가지는 무엇일까요?
2. 기사 수와 주문 수의 균형을 어떤 결산으로 이해하게 할까요?

## 기존 기준 문서·구현

- [현재 실행 기능·검증 안내](../../../../../tools/Ssalddel.Sagajeong.Engine/README.md)
- [기획 목차와 상태](../../../PLANNING.md)
- [캠페인 구현](../../../../../tools/Ssalddel.Sagajeong.Engine/LocalCampaign.cs)
- [배차 회차 구현](../../../../../tools/Ssalddel.BatchMonitor.Windows/FoodManagementRound.cs)
- [가상 경영 세션 구현](../../../../../tools/Ssalddel.BatchMonitor.Windows/FoodManagementSession.cs)
- [주차·주말 수요 r129](../../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/weekly-delivery-demand.proposal.r129.md)
- [시간대 물량·출근 r130](../../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/daypart-dispatch-and-rider-attendance.proposal.r130.md)
- [모집·교육 선택 r131](../../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/map-rider-recruitment-and-training.proposal.r131.md)
- [가맹점과 기사 흐름 r132](../../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/restaurant-coverage-and-rider-flow.proposal.r132.md)
- [개인 타로 방향의 확정 기획 r124](../../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/rider-munyeom-tarot-direction.decision.r124.md)

## 참조 목록

2026-11-11 감사 색인의 주 분류 0행, 다른 묶음에서 연결한 참조 10행입니다. 고유 데이터셋·수집 성공 건수가 아닙니다. sourceId·datasetId·원래 status는 보존하며 현재 상태의 판단은 출처 감사와 기존 수집 기록을 따릅니다.

[이 묶음의 참조 CSV](../../../../../artifacts/local/game-material-context-groups-20261111/03-rules-scenarios/references.csv)

연결해서 읽기: [운영 대상·관계](../02-operations-relations/README.md) · [인구·이동·교통 관측](../04-population-traffic/README.md) · [생산·가공·물류·원가](../05-production-logistics-costs/README.md) · [시각·음향·카드·학습 참고](../06-visual-audio-learning/README.md)

[전체 묶음으로](../README.md)
