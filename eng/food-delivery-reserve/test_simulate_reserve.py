"""잔액 보존·순서·입금 지연·중복 비용 방지에 대한 독립 금액 사례."""
import unittest
import json
from pathlib import Path
import tempfile
from simulate_reserve import simulate as simulate_model, load_intakes


def simulate(*args, **kwargs):
    kwargs.setdefault("customer", 2500)
    kwargs.setdefault("merchant", 2500)
    return simulate_model(*args, **kwargs)


def orders(*fees):
    return [{'record_id': f'd-{i}', 'day': '2026-09-23', 'sequence': i, 'fee': fee}
            for i, fee in enumerate(fees, 1)]


class ReserveTests(unittest.TestCase):
    def test_latest_default_contribution_is_6000(self):
        s = simulate_model(orders(4000))['summary']
        self.assertEqual(2000, s['closing_balance_after_all_collections_krw'])

    def test_new_contribution_adds_1000_per_order_to_previous_case(self):
        selected = orders(7000, 4000, 3000)
        old = simulate(selected)['summary']
        new = simulate_model(selected)['summary']
        self.assertEqual(3000, new['closing_balance_after_all_collections_krw'] - old['closing_balance_after_all_collections_krw'])
        self.assertEqual(1000, new['additional_opening_funding_required_krw'])

    def test_expensive_first_needs_starting_cash_even_with_positive_final_margin(self):
        expensive_first = simulate(orders(8000, 1000))['summary']
        cheap_first = simulate(orders(1000, 8000))['summary']
        self.assertEqual(1000, expensive_first['closing_balance_after_all_collections_krw'])
        self.assertEqual(3000, expensive_first['additional_opening_funding_required_krw'])
        self.assertEqual(0, cheap_first['additional_opening_funding_required_krw'])

    def test_one_day_delay_is_receivable_not_spendable_cash(self):
        result = simulate(orders(4000), customer_lag_days=1, merchant_lag_days=1)
        s = result['summary']
        self.assertEqual(-4000, s['balance_at_delivery_end_krw'])
        self.assertEqual(5000, s['uncollected_contributions_at_delivery_end_krw'])
        self.assertEqual(1000, s['closing_balance_after_all_collections_krw'])
        self.assertEqual(4000, s['additional_opening_funding_required_krw'])
        self.assertEqual(0, result['daily'][-1]['orders'])

    def test_opening_funding_and_floor_are_not_profit(self):
        s = simulate(orders(8000, 1000), opening=3500, floor=1000)['summary']
        self.assertEqual(4500, s['closing_balance_after_all_collections_krw'])
        self.assertEqual(500, s['additional_opening_funding_required_krw'])

    def test_operating_cost_and_rewards_are_counted_once(self):
        s = simulate(orders(4000, 4000), cost_per_order=100, fixed_daily_cost=200,
                     extra_daily_costs={'2026-09-23': 500})['summary']
        self.assertEqual(1100, s['closing_balance_after_all_collections_krw'])
        self.assertEqual(400, s['other_cost_assumptions_krw'])
        self.assertEqual(500, s['extra_costs_krw'])

    def test_surplus_and_draw_do_not_double_count_gross_fee(self):
        s = simulate(orders(3000, 7000))['summary']
        self.assertEqual(2000, s['surplus_deposits_before_costs_krw'])
        self.assertEqual(2000, s['expensive_delivery_reserve_draws_krw'])
        self.assertEqual(0, s['closing_balance_after_all_collections_krw'])

    def test_duplicate_identity_or_sequence_rejected(self):
        bad = orders(4000, 4000)
        bad[1]['record_id'] = bad[0]['record_id']
        with self.assertRaises(ValueError):
            simulate(bad)
        bad = orders(4000, 4000)
        bad[1]['sequence'] = 1
        with self.assertRaises(ValueError):
            simulate(bad)

    def test_missing_fee_not_treated_as_zero(self):
        bad = orders(None)
        with self.assertRaises(ValueError):
            simulate(bad)

    def test_day_end_positive_can_hide_intraday_shortfall(self):
        result = simulate(orders(7000, 1000))
        self.assertEqual(2000, result['daily'][0]['cash_after_krw'])
        self.assertEqual(-2000, result['summary']['minimum_cash_path_krw'])

    def test_extra_cost_scope_is_explicit(self):
        with self.assertRaises(ValueError):
            simulate(orders(4000), extra_daily_costs={'2026-09-24': 1000})

    def test_financial_scope_does_not_promote_whole_video_intake(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / 'intake-test'
            path.mkdir()
            data = {'confirmed_service_date': '2026-09-23', 'record_count': 1,
                    'delivery_fee_sum_krw': 4000, 'input_complete_confirmed': False,
                    'status': 'location_review_pending',
                    'rows': [{'record_id': 'x', 'sequence': 1, 'delivery_fee_krw': 4000,
                              'restaurant_candidate': '가상 음식점'}]}
            file = path / 'dictation-draft.json'
            file.write_text(json.dumps(data), encoding='utf-8')
            rows, sources = load_intakes(Path(root))
            self.assertEqual(4000, rows[0]['fee'])
            self.assertFalse(sources[0]['original_whole_intake_confirmed'])
            self.assertEqual(data, json.loads(file.read_text(encoding='utf-8')))


if __name__ == '__main__':
    unittest.main()
