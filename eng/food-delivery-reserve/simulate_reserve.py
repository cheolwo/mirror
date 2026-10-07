"""주문별 공동 배달비와 예비금의 오프라인 재현. 결제·DB·송금을 실행하지 않는다."""
from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import date, timedelta
import hashlib
import html
import json
from pathlib import Path


def money(value, label):
    if type(value) is not int or value < 0:
        raise ValueError(f'{label}: nonnegative integer KRW required')
    return value


def simulate(orders, *, customer=3000, merchant=3000, opening=0, floor=0,
             cost_per_order=0, fixed_daily_cost=0, customer_lag_days=0,
             merchant_lag_days=0, extra_daily_costs=None):
    """실적 순서의 가정 재현. 음수 cash는 실행된 overdraft가 아닌 필요한 자금이다."""
    for label, value in [('customer', customer), ('merchant', merchant), ('opening', opening),
                         ('floor', floor), ('cost_per_order', cost_per_order),
                         ('fixed_daily_cost', fixed_daily_cost), ('customer_lag_days', customer_lag_days),
                         ('merchant_lag_days', merchant_lag_days)]:
        money(value, label)
    if max(customer_lag_days, merchant_lag_days) > 31:
        raise ValueError('settlement lag must be at most 31 days')
    if not orders:
        raise ValueError('orders required')
    ids = set()
    by_day = defaultdict(list)
    for order in orders:
        key = order['record_id']
        if not isinstance(key, str) or not key or key in ids:
            raise ValueError('missing or duplicate delivery identity')
        ids.add(key)
        day = date.fromisoformat(order['day'])
        if day.isoformat() != order['day'] or type(order['sequence']) is not int or order['sequence'] < 1:
            raise ValueError('invalid date or sequence')
        money(order['fee'], 'driver gross fee')
        by_day[day].append(order)
    extras = {}
    for day, amount in (extra_daily_costs or {}).items():
        parsed = date.fromisoformat(day)
        money(amount, 'extra daily cost')
        if parsed not in by_day:
            raise ValueError('extra cost outside observed delivery days')
        extras[parsed] = amount
    for day, rows in by_day.items():
        if len({row['sequence'] for row in rows}) != len(rows):
            raise ValueError('duplicate daily sequence')
        rows.sort(key=lambda row: row['sequence'])

    cash = opening
    accrued = opening
    minimum_cash = opening
    delayed = defaultdict(int)
    collected = 0
    earned = 0
    driver_total = 0
    cost_total = 0
    extra_total = 0
    surplus_total = 0
    reserve_draw_total = 0
    below_floor_orders = 0
    trace = []
    daily = []
    first = min(by_day)
    last_delivery = max(by_day)
    final = last_delivery + timedelta(days=max(customer_lag_days, merchant_lag_days))
    current = first
    while current <= final:
        cash_before = cash
        accrued_before = accrued
        incoming = delayed.pop(current, 0)
        cash += incoming
        collected += incoming
        minimum_cash = min(minimum_cash, cash)
        day_fees = 0
        day_order_cost = 0
        day_earned = 0
        rows = by_day.get(current, [])
        for order in rows:
            order_cash_before = cash
            for amount, lag in [(customer, customer_lag_days), (merchant, merchant_lag_days)]:
                if lag:
                    delayed[current + timedelta(days=lag)] += amount
                else:
                    cash += amount
                    collected += amount
                    incoming += amount
            contribution = customer + merchant
            earned += contribution
            day_earned += contribution
            driver_total += order['fee']
            day_fees += order['fee']
            day_order_cost += cost_per_order
            cost_total += cost_per_order
            cash -= order['fee'] + cost_per_order
            accrued += contribution - order['fee'] - cost_per_order
            minimum_cash = min(minimum_cash, cash)
            below_floor_orders += cash < floor
            surplus = max(0, contribution - order['fee'])
            draw = max(0, order['fee'] - contribution)
            surplus_total += surplus
            reserve_draw_total += draw
            trace.append({'record_id': order['record_id'], 'day': order['day'], 'sequence': order['sequence'],
                          'contribution_krw': contribution, 'driver_gross_fee_krw': order['fee'],
                          'surplus_before_other_costs_krw': surplus, 'reserve_draw_for_driver_krw': draw,
                          'order_cost_assumption_krw': cost_per_order, 'cash_before_krw': order_cash_before,
                          'cash_after_krw': cash, 'accrued_balance_after_krw': accrued,
                          'cash_floor_shortfall_krw': max(0, floor - cash)})
        fixed = fixed_daily_cost if rows else 0
        extra = extras.get(current, 0)
        cash -= fixed + extra
        accrued -= fixed + extra
        cost_total += fixed
        extra_total += extra
        minimum_cash = min(minimum_cash, cash)
        daily.append({'day': current.isoformat(), 'orders': len(rows), 'cash_before_krw': cash_before,
                      'contribution_earned_krw': day_earned, 'contribution_collected_krw': incoming,
                      'driver_gross_fees_krw': day_fees, 'order_cost_assumption_krw': day_order_cost,
                      'fixed_cost_assumption_krw': fixed, 'extra_cost_krw': extra,
                      'cash_after_krw': cash, 'accrued_change_krw': accrued - accrued_before,
                      'accrued_balance_after_krw': accrued,
                      'collection_only_day': not bool(rows)})
        if current == last_delivery:
            cash_at_delivery_end = cash
            accrued_at_delivery_end = accrued
        current += timedelta(days=1)
    assert not delayed
    assert cash == opening + collected - driver_total - cost_total - extra_total
    assert accrued == opening + earned - driver_total - cost_total - extra_total
    assert cash == accrued and collected == earned
    assert surplus_total - reserve_draw_total == earned - driver_total
    return {'assumptions': {'customer_fee_krw': customer, 'merchant_fee_krw': merchant,
                            'sales_commission_rate': 0, 'opening_funding_krw': opening,
                            'protected_cash_floor_krw': floor, 'cost_per_order_krw': cost_per_order,
                            'fixed_cost_per_delivery_day_krw': fixed_daily_cost,
                            'customer_settlement_lag_days': customer_lag_days,
                            'merchant_settlement_lag_days': merchant_lag_days},
            'summary': {'delivery_count': len(orders), 'contributions_krw': earned,
                        'driver_gross_fees_krw': driver_total, 'other_cost_assumptions_krw': cost_total,
                        'extra_costs_krw': extra_total, 'surplus_deposits_before_costs_krw': surplus_total,
                        'expensive_delivery_reserve_draws_krw': reserve_draw_total,
                        'balance_at_delivery_end_krw': cash_at_delivery_end,
                        'accrued_balance_at_delivery_end_krw': accrued_at_delivery_end,
                        'uncollected_contributions_at_delivery_end_krw': accrued_at_delivery_end - cash_at_delivery_end,
                        'closing_balance_after_all_collections_krw': cash,
                        'minimum_cash_path_krw': minimum_cash,
                        'additional_opening_funding_required_krw': max(0, floor - minimum_cash),
                        'orders_below_protected_floor': below_floor_orders,
                        'observed_delivery_start': first.isoformat(), 'observed_delivery_end': last_delivery.isoformat(),
                        'collection_simulation_end': final.isoformat()},
            'daily': daily, 'orders': trace}


def load_intakes(root):
    paths = sorted(root.glob('intake-*/dictation-draft.json'))
    if not paths:
        raise ValueError('No canonical intake files found')
    orders = []
    sources = []
    ids = set()
    days = set()
    for path in paths:
        raw = path.read_bytes()
        data = json.loads(raw.decode('utf-8-sig'))
        # 영상 편집용 전체 접수 확인과 날짜/배달료만 쓰는 재무 재현의 범위는 다르다.
        # 아래 금액·건수·날짜·중복 검사를 통과해도 원본 전체 확인 상태를 승격하지 않는다.
        day = data['confirmed_service_date']
        if day in days:
            raise ValueError('Duplicate service-day intake requires explicit reconciliation')
        days.add(day)
        rows = data['rows']
        if len(rows) != data['record_count'] or sum(row['delivery_fee_krw'] for row in rows) != data['delivery_fee_sum_krw']:
            raise ValueError('Intake count/fee total mismatch: ' + str(path))
        for row in rows:
            if row.get('confirmed_service_date') not in (None, day):
                raise ValueError('Row day differs from canonical intake day')
            if row['record_id'] in ids:
                raise ValueError('Duplicate intake record')
            ids.add(row['record_id'])
            name = row.get('restaurant_candidate') or row.get('restaurant_spoken')
            if not isinstance(name, str) or not name:
                raise ValueError('Merchant category cannot be reviewed without recorded name')
            orders.append({'record_id': row['record_id'], 'day': day, 'sequence': row['sequence'],
                           'fee': money(row['delivery_fee_krw'], 'driver gross fee'),
                           'scope': 'GroceryBmart' if 'B마트' in name else 'Restaurant'})
        sources.append({'path': str(path.resolve()), 'sha256': hashlib.sha256(raw).hexdigest().upper(),
                        'day': day, 'records': len(rows), 'gross_fee_krw': data['delivery_fee_sum_krw'],
                        'original_intake_status': data.get('status'),
                        'original_whole_intake_confirmed': data.get('input_complete_confirmed'),
                        'analysis_scope': 'RecordedDateCountGrossFeeArithmeticOnly;NoIntakeStatusPromotion'})
    return sorted(orders, key=lambda row: (row['day'], row['sequence'])), sources


def load_historical_extra(path, orders):
    raw = path.read_bytes()
    data = json.loads(raw.decode('utf-8-sig'))
    first, last = data['dates']
    matched = [order for order in orders if first <= order['day'] <= last]
    if len(matched) != data['count'] or sum(order['fee'] for order in matched) != data['fee']:
        raise ValueError('Historical extras require the same full-order scope')
    extra = {}
    components = []
    for day in data['days']:
        same = [order for order in matched if order['day'] == day['date']]
        if len(same) != day['count'] or sum(order['fee'] for order in same) != day['fee']:
            raise ValueError('Historical daily extra scope mismatch')
        values = {key: money(day[key], key) for key in ['mission', 'promotion_gross', 'milestone_bonus_gross']}
        extra[day['date']] = sum(values.values())
        components.append({'day': day['date'], **values})
    if sum(extra.values()) != data['mission'] + data['promotion_gross'] + data['milestone_bonus_gross']:
        raise ValueError('Historical extra totals mismatch')
    return extra, {'path': str(path.resolve()), 'sha256': hashlib.sha256(raw).hexdigest().upper(),
                   'period': [first, last], 'same_scope_orders': len(matched), 'components': components,
                   'evidence_status_preserved': data['status'], 'not_adopted_as_own_platform_reward_policy': True}


def render_report(bundle):
    scenarios = bundle['scenarios']
    table = []
    for item in scenarios:
        s = item['result']['summary']
        table.append('<tr><td>' + html.escape(item['label']) + '</td>' + ''.join(
            f'<td>{s[key]:,}</td>' for key in ['delivery_count', 'contributions_krw', 'driver_gross_fees_krw',
                                             'other_cost_assumptions_krw', 'extra_costs_krw',
                                             'closing_balance_after_all_collections_krw',
                                             'additional_opening_funding_required_krw']) + '</tr>')
    baseline = next(item for item in scenarios if item['id'] == 'restaurant-base')['result']
    values = [0] + [row['cash_after_krw'] for row in baseline['orders']]
    lo = min(values)
    hi = max(values)
    span = max(1, hi - lo)
    points = ' '.join(f'{30+i*900/(len(values)-1):.2f},{240-(v-lo)*200/span:.2f}' for i, v in enumerate(values))
    zero = 240 - (0-lo)*200/span
    daily_rows = ''.join('<tr><td>' + row['day'] + '</td>' + ''.join(f'<td>{row[key]:,}</td>' for key in
                         ['orders', 'contribution_earned_krw', 'driver_gross_fees_krw', 'accrued_change_krw', 'cash_after_krw']) + '</tr>'
                         for row in baseline['daily'])
    return '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>공동 배달비 예비금 검토</title><style>body{font:16px/1.65 system-ui,sans-serif;max-width:1180px;margin:auto;padding:24px;color:#17243a;background:#f4f6fa}h1{font-size:26px}section{background:white;padding:20px;margin:20px 0;border-radius:12px}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums}td,th{padding:9px;border-bottom:1px solid #dce2ea;text-align:right;white-space:nowrap}td:first-child,th:first-child{text-align:left}.scroll{overflow:auto}svg{width:100%;height:auto}small{color:#46536a}a{color:#215fa8}</style>
<h1>고객 3,000원 + 음식점 3,000원 · 매출 수수료 0%</h1>
<p>실제 기록의 기사 세전 배달료를 그대로 지급한다고 가정한 오프라인 재현입니다. 실제 결제·은행 입금·제품 앱 실행 결과가 아닙니다.</p>
<section><h2>주문 순서에 따른 예비금</h2><p>음식점 주문만 · 시작 자금 0원 · 같은 주문의 배달비를 지급 전에 확보하는 가정</p>
<svg viewBox="0 0 960 280" role="img" aria-label="음식점 배달 순서에 따른 예비금 잔액"><line x1="30" y1="''' + f'{zero:.2f}' + '''" x2="930" y2="''' + f'{zero:.2f}' + '''" stroke="#8793a4"/><polyline points="''' + points + '''" fill="none" stroke="#246f9b" stroke-width="2"/><text x="30" y="20">''' + f'최고 {hi:,}원 · 최저 {lo:,}원' + '''</text><text x="30" y="270">첫 배달 → 마지막 배달</text></svg></section>
<section><h2>가정을 바꿨을 때</h2><p>모든 금액은 원입니다. 운영비 250/500/1,000원은 실제 요율이 아닌 부담 범위를 보는 가정입니다.</p>
<div class="scroll"><table><thead><tr><th>시나리오</th><th>건수</th><th>확보 총액</th><th>기사 세전 배달료</th><th>비용 가정</th><th>별도 보상</th><th>최종 잔액</th><th>추가 시작 자금</th></tr></thead><tbody>''' + ''.join(table) + '''</tbody></table></div>
<p>추가 시작 자금은 재현된 순서에서 잔액을 보호 하한 이상으로 유지하는 최소 금액입니다. 최종 이익과 지급 시점의 현금은 다릅니다.</p></section>
<section><h2>음식점 기준 날짜별 잔액</h2><div class="scroll"><table><thead><tr><th>날짜</th><th>건수</th><th>발생 배달비</th><th>기사 배달료</th><th>그날 차액</th><th>누적 잔액</th></tr></thead><tbody>''' + daily_rows + '''</tbody></table></div></section>
<section><h2>운영에 연결할 기준</h2><ul><li>남는 배달비는 예비금으로 적립하고 비싼 배달의 부족액은 같은 예비금에서 충당합니다.</li><li>기사 제안·수락 때 동결한 지급액을 예비금 사정으로 감액하지 않습니다.</li><li>PG 미입금액과 음식점에 지급할 음식대금은 사용 가능한 예비금에서 제외합니다.</li><li>보호 하한과 시작 자금은 미확정입니다. 부족하면 새 주문의 접수를 검토하고 기존 기사 지급 채무를 보존합니다.</li><li>기사 원천세·보험 공제 후 수령액을 플랫폼의 기사 총비용으로 대체하지 않습니다.</li></ul>
<small>실제 배달 완료 시각과 PG 입금 시각이 없으므로 날짜·구술 입력 순서로 재현했습니다. 지연 대금은 해당 날짜의 배달 전에 입금된다고 가정합니다. 지연은 달력 날짜이며 실제 PG 영업일 규칙이 아닙니다. 고정비는 배달이 있는 날짜에만 반영합니다. 미션·보상은 전체 주문 범위에서 별도 비교하며 음식점 주문만의 비용으로 임의 배분하지 않습니다. 부가세·고용주 부담 보험·환불·지사 보수 등 미확정 비용을 계산 완료로 취급하지 않습니다.</small></section></html>'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--intake-root', type=Path, required=True)
    parser.add_argument('--historical-settlement', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    orders, sources = load_intakes(args.intake_root)
    restaurants = [order for order in orders if order['scope'] == 'Restaurant']
    extras, extra_source = load_historical_extra(args.historical_settlement, orders)
    week = [order for order in orders if extra_source['period'][0] <= order['day'] <= extra_source['period'][1]]
    configurations = [
        ('restaurant-base', '음식점 · 즉시 확보 · 비용 제외', restaurants, {}),
        ('restaurant-cost250', '음식점 · 건당 비용250원 가정', restaurants, {'cost_per_order': 250}),
        ('restaurant-cost500', '음식점 · 건당 비용500원 가정', restaurants, {'cost_per_order': 500}),
        ('restaurant-cost1000', '음식점 · 건당 비용1,000원 가정', restaurants, {'cost_per_order': 1000}),
        ('restaurant-lag1', '음식점 · 양쪽 배달비1일 뒤 확보', restaurants, {'customer_lag_days': 1, 'merchant_lag_days': 1}),
        ('restaurant-driver-first', '음식점 · 고객 즉시/점주1일 뒤', restaurants, {'merchant_lag_days': 1}),
        ('restaurant-previous5000', '이전 비교 · 고객/음식점 각2,500원', restaurants, {'customer': 2500, 'merchant': 2500}),
        ('all-base', 'B마트 포함 · 비용 제외', orders, {}),
        ('historical-week-rewards', '09.23~29 전체 · 기존 별도 보상 지급', week, {'extra_daily_costs': extras}),
    ]
    scenarios = [{'id': key, 'label': label, 'result': simulate(selected, **options)} for key, label, selected, options in configurations]
    for source in sources + [extra_source]:
        if hashlib.sha256(Path(source['path']).read_bytes()).hexdigest().upper() != source['sha256']:
            raise ValueError('Source changed while simulating')
    bundle = {'schemaVersion': 'food-delivery-reserve-offline-replay.v1',
              'policyRevision': 'food-delivery-fixed-contribution-reserve.hypothesis.r2',
              'executionEvidence': 'OfflineHistoricalReplay;NoPaymentOrOperationalDatabaseEffects',
              'orderingEvidence': 'ConfirmedServiceDateThenDictatedSequence;NoExactCompletionTimestamps',
              'sources': sources, 'historicalExtraCostSource': extra_source,
              'limitations': ['gross funding model, not tax/VAT/accounting advice',
                             'whole intake/location/menu verification is not promoted by financial arithmetic checks',
                             'unknown operating/refund/employer costs remain unresolved, not verified zero',
                             'lagged receipts arrive before that day deliveries in this calendar-day scenario',
                             'Bmart excluded only by explicit recorded B마트 name; classification is observation based',
                             'confirmed historical extra rewards are a stress comparison, not the own-platform policy'],
              'scenarios': scenarios}
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'scenarios.json').write_text(json.dumps(bundle, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (args.output / '공동배달비-예비금-검토.html').write_text(render_report(bundle), encoding='utf-8')
    print(json.dumps({'scenarios': [{item['id']: item['result']['summary']} for item in scenarios],
                      'sourceHashChecksPassed': True, 'operationalEffects': False}, ensure_ascii=False))


if __name__ == '__main__':
    main()
