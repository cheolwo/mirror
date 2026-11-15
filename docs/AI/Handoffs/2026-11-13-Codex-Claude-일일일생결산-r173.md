# Codex → Claude · 일일일생·하루 결산 r173

- 작성일/역할: 2026-11-13 · Codex 기획 작업실.
- 상태: `UserDirectedDailyClosing / HandoffPrepared / DevelopmentAcceptancePending / ProductUnchanged`.
- 정본: [r173](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/daily-life-and-settlement.integration.r173.md).
- 정본 SHA256: `d36d884289629889be20df55e506d14034da293aa5eeffbf9bb5f4dca1db2258`.
- 선행: [승인 r172](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/real-calendar-seasonal-arcana.integration.r172.md)/[기존 인계](2026-11-13-Codex-Claude-현실달력절기통합-r172.md)는 보존한다. 현실 날짜·주간·절기 방침은 유지하면서 **하루를 기본 결산 주기로** 추가하는 최신 사용자 지시다. 용어는 ‘일일일생’이다.

## 적용할 변화

오늘 준비→실제 수행→일일 결산→다음 날 준비를 기본 리듬으로 둔다. 주간은 같은 일일 기록의 누적 검토, 절기는 방침/대응 검토다. 구체 날짜 경계·집계/확인·자정 작업·미접속·겹친 결산의 정본은 r173의2~4절이다.

개발은 현재 순회/시간 소유를 유지하고 실제 기존 날짜/기간별 보고·확인 계약을 먼저 재사용한다. 가상 `LocalCampaign.Day`를 실제 일일 날짜로 읽거나 기존 시간 진행 Confirm을 결산 확인용으로 호출하지 않는다. 권위 기록의 현실 KST 날짜와 같은 기록/기간의 멱등 확인을 연결한다.

## 확인할 대표 상황

- 당일 반복 작업/잠정 결산 열람 → 같은 날짜 누적, 다음 날 강제 진행/재지급 없음.
- 23:55 시작·00:10 완료 → 전날 미완료 이월, 완료/수익/비용은 각 실제 원본 기록일에 귀속.
- 며칠 후 재접속 → 실제 기록이 있는 미확인 날만 묶어 표시, 빈 날짜의 결산 창/오프라인 영업 없음.
- 일·주·절기 경계 겹침 → 같은 기록의 기간별 보고, 새 지급/차감·일일 카드 재선택 없음.
- 확인/재확인·저장복원 → 작업/Tick·금액/재고 불변, 원본 판본과 이력 보존.

이번 인계는 기획 입력이다. 실제 코드 쓰기 경로·WI/수직 검증 명세·통합/화면 결과는 개발 수용/반환에 결속하고 현재 Claude의 개발·기존 승인/코드·연구/E·DB/예약·commit/push를 이 문서 작성으로 변경하지 않는다.
