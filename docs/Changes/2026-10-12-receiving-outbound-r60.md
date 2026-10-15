# 2026-10-12 입고·보관과 출고·배송 정산 분리 r60

입고·보관은 재고 묶음, 출고·배송은 주문을 기준으로 관리한다. 새 약정에서 서로 다른 흐름의 보수를 섞지 못하게 하고, 출고 보수는 수행 기록을 보존하면서 고객 수령 후 정상 지급 신고하도록 분리했다.

- [수용 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.implementation.r60.md)·[작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.r60.work-order.json)·[페이지 책임](../ProjectOverview/page-docs/activity-settlements-r60.md).
- 부분 입고·수량별 보관 보수는 실제 입고 완료 수량을 사용한다. 보관 구간의 피킹·반환 감사 근거는 별도로 보존한다.
- 다른 주문의 피킹자·입고자가 현재 출고 정산 권한을 얻지 않고, 출고 기사 추가가 입고 약정 동의를 바꾸지 않게 했다.
- 목록을 업무별로 구분하고 상세에 현재 단계·현재 약정 기준·항목별 당시 지급 조건과 대기 사유를 표시한다.
- 과거 채권·판본·지급 진술·요청 재조회는 보존한다. 환불은 기존 실수령 상한을 유지하며 배송 완료 관문으로 막지 않는다.

## 검증

- `eng/validate-changes.ps1 -Level Fast -Paths <r60 source paths> -NoRestore -TestFilter <settlement/reward/UI/evidence>`: 집중 159건 통과, 실패·건너뜀 0. 증거 `artifacts/local/validation/20261012-120203/`.
- 같은 경로 범위의 `-Level Task -NoRestore`: 전체 11,005건 통과, 실패 0, MongoDB 환경 의존 15건 건너뜀(전체 11,020건). 증거 `artifacts/local/validation/20261012-120438/`.
- 전체 `Ssalddel.v3.5.slnx` 빌드 오류 0. 마지막 Task 빌드 경고 61건이며 기존 의존성 버전 제약·일반 회원 push 미설정 등의 경고는 해소했다고 보고하지 않는다.
- 격리 SQLite/실제 UseCase 통합: 입고 사진과 실제 재고 변화 뒤 입고비 독립 지급 신고, 출고 피킹 보수 보존, 전달 후 고객 수령 전 지급 차단, 고객 수령 후 같은 요청의 정상 지급 신고, 재고 연결·기존 성과금과 당사자 보호를 검증했다. 시험 사진·지급 진술이며 실제 이체나 실물 업무 증거가 아니다.
- UI는 HtmlRenderer로 업무별 목록과 현재 단계·당시 항목별 조건·과거 약정 표시를 검증했다. 실제 브라우저/단말 화면 사용은 미검증이다.
- 최초 Fast는 154건 통과·5건 실패했다(`20261012-115639/`). 과거 약정 시험 준비가 저장된 불변 판본을 수정하려다 보호 규칙에 막혔다. 시험 초기 상태에 과거 판본을 생성하도록 수정했고 제품 원장의 불변 보호를 유지했다. 수정 후 Fast와 전체 Task가 모두 통과했다.
- 기존 r59 명세의 지문은 역사적 증거로 보존하고, 현재 r60 소스 17개 지문과 수용 문서 해시는 r60 작업 명세에 별도로 기록한다. 새 테이블/migration은 없으며 공유 DB에는 적용하지 않았다.

## 남은 범위

기본 OFF·Simulation·기존 profile을 유지하며 실운영 활성화·공용 DB 적용·실제 송금·은행 대사·단말·Azure·Unity·커밋/푸시는 수행하지 않는다. 분산 입고의 파손/반려 전용 절차, 실패 배송의 예외 정산, 입고 원가의 주문별 배분은 이번 완료 범위가 아니다. 기존 r53/r54/r59 변경을 보존한다.
