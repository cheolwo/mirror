# Codex → Claude · 현실 달력·절기·운영 카드 r172

- 작성일/역할: 2026-11-13 · Codex 기획 작업실.
- 상태: `ApprovedDesign / HandoffPrepared / AwaitingDevelopmentAcceptance / ProductUnchanged`.
- 정본: [r172](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/real-calendar-seasonal-arcana.integration.r172.md).
- 정본 SHA256: `456ce9720a3d73f9dc9c305565865c43c44c7549e46772f3325e5b22e3a3a0c0`.
- 승인 근거: 사용자 명시 현실 날짜 연동 정정과 현실1주 경영·부재중 환경만 갱신·방침/우선순위 선택 후 r172 초안에 “Implement the proposed plan.”.
- 기준 작업본: Hongdal `dev/mirror-integration@7c83c18cf9607b3854120c15f85d5dd8f9ab14d8`, 미커밋 변경 다수. 이 commit만으로 현재 문서/제품 작업본을 재현할 수 없으므로 실제 지문/dirty 경계를 다시 대조한다.
- 소유: 사용자가 구현 중이라고 알린 Claude 결정 순회·가맹점 설치는 유지한다. 이 문서는 작업 인수·다른 개발 중단·Goal 자동 활성화·정확한 코드 쓰기 경로 할당이 아니다.

## 읽을 것과 이번에 달라진 기준

[기존 결정 순회](2026-11-13-Claude-제안-결정순회와한버튼.md)와 [두 겹 턴](2026-11-13-Claude-제안-행정동가맹개설절차와턴틀.md)은 기존 초안/시안으로 보존한다. r172 현실 프로필에서는 ‘가상 영업7일을 빨리 끝내 다음 주로’가 아니라 **현실 월~일의 주간 안에서 짧은 작업을 여러 번 수행**한다. 다음 결정 버튼/배속으로 달력·주차를 진행시키지 않는다. 서울의 역할은 아직 정해지지 않았다는 최신 사용자 답을 보충 회차 승인으로 해석하지 않는다.

정본1~5절을 적용하고 아래 소스는 재사용/충돌 확인 입력으로 읽는다. 인계 문서에 본문 규칙을 새 정본처럼 복제하지 않는다.

| 입력 | 현재 근거와 연결 주의 |
| --- | --- |
| [LocalCampaign](../../../Ssalddel.Sagajeong.Core/UnityPackage/Runtime/LocalCampaign.cs) | 고정 기점·가상Day·3일차 정책. 현실 경과를 그대로 Advance하여 오프라인 영업을 만들지 않음 |
| [자료 고정 r54](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/session-pinned-public-data-revision.r54.md)/[갱신 r55](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/background-regional-data-update.r55.md) | 도로/건물/이동망은 세션 고정/별도 Migration. 관측 문맥 사본의 안전한 다음 작업 적용과 구별 |
| [절기/타로 r15](../절기전략턴-타로카드-기획-2026-09-02.md)/[운영 번역 r59](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/seasonal-delivery-operation-campaign.r59.md) | 시간층·확인·기록 구조 재사용. r172 고정3후보/중립 프로필을 기존 순환 덱 구현 완료로 해석하지 않음 |
| [SimulationTurnClosing](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationTurnClosing.cs) | 기존 Confirm에 Tick 진행이 포함됨. 절기 카드 선택에 그대로 사용하지 않음 |
| [SimulationArcanaTownLife](../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationArcanaTownLife.cs) | 기존 UntilReplaced/동일 활성 카드 재선택 거절과 r172 절기 수명은 다름. 다음 절기의 동일 카드 재활성화 필요 |
| [r68](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/seasonal-campaign-core-remote-implementation.r68.md)/[r79](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/seasonal-campaign-card-npc-source-binding.r79.md) | 조건 기반 Campaign과 읽기 원천 Adapter의 기존 구현. 연도별 달력이나 현재 Windows 플레이 완성 증거가 아님 |

## 개발 수용 시 만들 작은 조각

첫 검토 후보는 **현실 날짜·주간·절기 조회와 재접속 문맥 갱신**이다. 기존 결정 순회가 이 상태를 읽고, 환경 갱신만으로 금액/재고·진행 작업·Tick이 변하지 않는지 확인한다. 캘린더 연동을 이유로 현재 순회 소스를 다시 만드는 과제로 바꾸지 않는다.

그다음 시간 진행 없는 현재 절기 방침 확인·기간별 집계·화면의 상태 표시를 각각 필요한 WI/명세에 결속한다. 기존 실행 원장과 준비 주체를 우선 재사용하고, 개발 수용에서 담당·정확한 허용 쓰기 경로·계약 영향·검증 상한을 반환한다. 이번 기획 승인은 그대로 사용하며 통상 구현 선택마다 추가 사용자 승인을 요구하지 않는다.

## 필수 반환 기준

1. KST 주간/절기 경계·주중 신규 접속·연도 변경의 결정적 시험.
2. 주중 절기 변경 뒤 같은 주·업무 유지, 부재중 환경 갱신의 금액/재고·작업/Tick 불변.
3. 선택·중립 확인의 중복 방지와 저장/복원, 다음 절기 동일 카드 재활성화.
4. 현재 작업 자료/지도 구조 판본 보호와 관측 사본 갱신 실패/복구.
5. 주간/절기 집계의 중복 지급·차감 없음, 직접 조작/카메라 강제 탈취 없음.
6. 실제 수정 파일·시험 결과·Unity compile/Play Mode/Game View·Save/Replay·미통합/남은 문제를 구별한 반환.

현재 문서는 승인 기획/개발 입력까지만 준비했다. 코드 구현·시안/실제 화면·Runtime·DB/수집예약·Goal/WI·연구 Accepted/E·commit/push 수행 결과가 아니다.
