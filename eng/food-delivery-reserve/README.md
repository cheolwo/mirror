# 공동 배달비·예비금 오프라인 재현

고객3,000원+음식점3,000원, 매출 비례 수수료0이라는 **검토 가정**을 실제 개인 배달 기록의 기사 세전 배달료와 비교한다. 시작 자금·보호 하한·입금 지연·추가 비용을 바꿔 전체 잔액과 순서별 자금 결손을 구분한다. 제품 요금·DB·외부 결제·송금·배차 상태를 변경하지 않는다.

```powershell
python eng/food-delivery-reserve/simulate_reserve.py `
  --intake-root '<개인 배달 기록의 delivery-records 절대 경로>' `
  --historical-settlement '<동일 기간 current-settlement-estimate.json 절대 경로>' `
  --output artifacts/local/food-delivery-reserve/20261002-r2
python -m unittest discover -s eng/food-delivery-reserve -p 'test_*.py'
```

입력은 날짜별 최신 `intake-*/dictation-draft.json`만 읽는다. `history/`·정규화 DB·영상 방문 목록을 합치지 않는다. 동일 날짜·중복 배달 ID·중복 순서·금액 누락/무효·합계 불일치는 차단한다. 영상 편집용 전체 접수 확인과 날짜·건수·금액만 사용하는 재무 재현은 범위가 다르며, 전체 접수 미확인 상태를 source snapshot에 그대로 보존한다. 이 도구로 위치/메뉴/촬영 연결을 확인 완료로 바꾸지 않는다. 개인정보가 들어 있는 원문과 개별 결과는 Git 제외 로컬 산출물로 유지한다. B마트는 기록된 이름을 근거로 별도 범위로 둔다.

`simulate()`는 정수 원 단위를 사용하며 기사 지급 총액·기타 비용·기간 보상을 별도로 차감한다. 예비금 적립/인출은 기사 배달료와 같은 차액의 설명이며 추가로 더하거나 빼지 않는다. 원천세/보험 공제 후 기사 수령액을 플랫폼 비용으로 대체하지 않는다. 미확정 비용은 실제0이 아니라 비용 제외 또는 명시적인 가정이다.

실제 완료/PG 입금 시각이 없어 날짜·구술 입력 순서로 재현한다. 즉시 확보는 같은 주문의 배달비를 지급 전에 받는 가정이고 지연 확보는 달력 날짜 기준, 당일 배달 이전 입금 가정이다. 이 결과가 실제 intraday 유동성 보장이나 실입금 증거는 아니다. 잔액 음수는 필요한 시작 자금이며 실제 마이너스 계좌·송금 완료가 아니다. `additional_opening_funding_required_krw`는 주어진 시작 자금에 더해야 전체 경로에서 보호 하한을 지키는 최소 금액이다.

기존 미션/기간 보상은 같은 전체 주문 범위·건수·기본 배달료와 일별 수치를 검산한 뒤 비교 시나리오에만 사용한다. 음식점 주문만으로 축소한 뒤 전체 보상을 임의 배분하거나 실제 다른 플랫폼의 보상 정책을 자체 정책으로 채택하지 않는다.

출력: `scenarios.json`(가정·source SHA·주문별/일별 재현·자금 결손), `공동배달비-예비금-검토.html`(비공개 검토 화면). 기준 문서: [가맹본부 운영 r66](../../docs/AI/Planning/시스템/PLAN-SYSTEM-FRANCHISE-OPERATIONS/fixed-contribution-reserve.simulation.r66.md).

최신 가정 판본은 `food-delivery-fixed-contribution-reserve.hypothesis.r2`다. 이전 각각2,500원은 명시적 비교 시나리오로 보존하며 기본 계산값은 각각3,000원이다.
