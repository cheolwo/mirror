"""DB·서버·모델을 실행하지 않는 자료/경계/판정/대표 사례 회귀."""
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import importlib
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from fixture_catalog import fixtures, build_request, validate_case, request_identity
from journey_client import RunEnvironment, JourneyRuntime, validate_route, prepayment_belongs_to_request
from evidence_review import evaluate, representative_cases, save_immutable, get
import source_binding

RUN = 'cargo-operations-r21:' + 'a' * 32
RID = '10000000-0000-0000-0000-000000000001'
COMMAND = '20000000-0000-0000-0000-000000000001'


def observation(case):
    amount = case['agreedDriverPayoutKrw']
    quote = case['customerChargeKrw'] if case['directOffer'] else 35500
    proof = {'schemaVersion': 'r114-ai-cargo-canonical-proof.v1', 'synthetic': True,
             'database': 'mirror_operations_r21_20261022', 'runStableId': RUN,
             'request': {'id': RID}, 'transport': {'id': 1, 'state': '인수완료',
                 'frozenPayout': amount, 'confirmedDriverId': 'test-driver', 'performingDriverId': 'test-driver'},
             'photoEvidence': [{'photoKind': kind, 'bindingVerified': True, 'completionLinked': True,
                 'storageIntegrityVerified': True} for kind in ('pickup-complete-photo', 'dropoff-complete-photo')],
             'mongo': {'available': True, 'exists': True, 'ledgerId': 'transport:' + RID,
                'state': '완료', 'rdbProjectionExists': True}, 'actualPaymentVerified': False,
             'productionDispatchEnabled': False, 'fallbackUsed': True, 'aiRecommendationVerified': False,
             'actualCargoAI': {'status': 'NetworkFailure'}}
    return {'proof': proof, 'observedOffer': {'requestId': RID, 'driverId': 'test-driver', 'driverPayoutKrw': amount},
            'sourceBindingSha256': 'b' * 64,
            'expectedContext': {'runStableId': RUN, 'caseId': case['caseId'], 'requestId': RID, 'transportId': 1,
                'sourceBindingSha256': 'b' * 64, 'clientRequestId': request_identity(RUN, case['caseId']),
                'storedClientRequestId': request_identity(RUN, case['caseId'])},
            'receptionLookup': {'http': 200, 'body': {'의뢰Id': RID}},
            'shipper': {'http': 200, 'body': {'의뢰Id': RID, '운송상태': '인수완료', '결제예정금액': case['customerChargeKrw'],
                '최종운임': quote, '요금옵션': {'최종운임': quote, '기사지급예정운임': amount}}},
            'driver': {'http': 200, 'body': {'id': 1, '운송번호': RID, '상태': '인수완료',
                '도착': '2026-10-22T00:00:00Z', '기사_운송자': 'test-driver', '기사지급예정액': amount}},
            'prepayment': {'http': 200, 'body': {'의뢰Id': RID, 'paymentConfirmed': True, 'simulationPayment': True, 'amount': case['customerChargeKrw']}}}


class FixtureTests(unittest.TestCase):
    def test_normal_catalog_explicit_box_and_pallet(self):
        rows = fixtures()
        self.assertEqual([0, 0, 1], [validate_case(x)['palletCount'] for x in rows])
        self.assertEqual(3, len({x['caseId'] for x in rows}))

    def test_missing_pallet_is_not_silently_zero(self):
        case = fixtures()[0]; case.pop('palletCount')
        with self.assertRaisesRegex(ValueError, 'ExplicitPallet'): validate_case(case)

    def test_unknown_loading_flags_stay_unknown_and_are_rejected(self):
        for key in ('rotateOnFloor', 'stackable'):
            case = fixtures()[0]; case[key] = None
            with self.assertRaisesRegex(ValueError, 'LoadingFlags'): validate_case(case)

    def test_dimensions_volume_mismatch_is_not_normal(self):
        case = fixtures()[0]; case['volumeCbm'] = '0.1'
        with self.assertRaisesRegex(ValueError, 'VolumeDoesNotMatch'): validate_case(case)

    def test_direct_offer_does_not_bypass_equal_payout_rule(self):
        case = fixtures()[1]; case['directOffer'] = True
        with self.assertRaisesRegex(ValueError, 'PreserveProductPolicy'): validate_case(case)

    def test_payload_preserves_separate_customer_payout_and_explicit_conditions(self):
        case = fixtures()[1]
        result = build_request(case, RUN, RID, datetime(2026, 10, 22, tzinfo=timezone.utc))
        self.assertEqual(40000, result['결제예정금액'])
        self.assertEqual(35000, result['요금옵션']['기사지급예정운임'])
        self.assertFalse(result['요금옵션']['화주직접운임제안'])
        self.assertEqual(0, result['화물']['팔레트개수'])
        self.assertIs(result['화물']['바닥회전가능여부'], False)
        self.assertEqual(5, result['픽업']['작업시간분'])
        self.assertEqual(0, result['정산조건']['정산시점'])
        self.assertNotIn('결제상태', result)

    def test_identity_deterministic_and_run_scoped(self):
        self.assertEqual(request_identity(RUN, 'box'), request_identity(RUN, 'box'))
        self.assertNotEqual(request_identity(RUN, 'box'), request_identity(RUN, 'pallet'))
        with self.assertRaisesRegex(ValueError, 'RunIdentity'): request_identity('cargo-operations-r20:old', 'box')

    def test_naive_clock_cannot_create_time_window(self):
        with self.assertRaisesRegex(ValueError, 'UtcClock'): build_request(fixtures()[0], RUN, RID, datetime(2026, 10, 22))


class BoundaryTests(unittest.TestCase):
    def test_pay_refuses_another_request_before_post(self):
        runtime = JourneyRuntime.__new__(JourneyRuntime)
        runtime.legacy = SimpleNamespace(get=get, SHIPPER='synthetic',
            need=lambda condition, code: None if condition else (_ for _ in ()).throw(RuntimeError(code)))
        called = []
        runtime.client = SimpleNamespace(state={'cases': {'r21-box-direct': {'requestId': RID}}},
            call=lambda *args, **kwargs: (200, {'의뢰Id': COMMAND, 'enabled': True, 'simulationAvailable': True, 'canPay': True, 'amount': 6200}),
            once=lambda *args, **kwargs: called.append(args))
        with self.assertRaisesRegex(RuntimeError, 'SimulationPrepaymentRequired'): runtime.pay(fixtures()[0])
        self.assertEqual([], called)

    def test_pay_does_not_confirm_another_requests_final_response(self):
        runtime = JourneyRuntime.__new__(JourneyRuntime)
        runtime.legacy = SimpleNamespace(get=get, SHIPPER='synthetic',
            need=lambda condition, code: None if condition else (_ for _ in ()).throw(RuntimeError(code)))
        replies = iter([(200, {'의뢰Id': RID, 'enabled': True, 'simulationAvailable': True,
                              'paymentConfirmed': True, 'simulationPayment': True, 'amount': 6200}),
                        (200, {'의뢰Id': COMMAND, 'paymentConfirmed': True, 'simulationPayment': True, 'amount': 6200})])
        runtime.client = SimpleNamespace(state={'cases': {'r21-box-direct': {'requestId': RID}}},
            call=lambda *args, **kwargs: next(replies))
        with self.assertRaisesRegex(RuntimeError, 'SimulationPaymentNotConfirmed'): runtime.pay(fixtures()[0])

    def test_import_of_new_modules_does_not_create_files_or_send_http(self):
        with patch.object(Path, 'mkdir', side_effect=AssertionError('ImportCreatedDirectory')):
            for name in ('fixture_catalog', 'journey_client', 'evidence_review', 'verify'):
                importlib.reload(importlib.import_module(name))

    def test_external_absolute_and_network_paths_rejected(self):
        for route in ('https://example.invalid/api/v1/shipper/requests', '//example.invalid/api/v1/x', '/api/v1/x#private'):
            with self.assertRaisesRegex(ValueError, 'RelativeApi'): validate_route('GET', route, None, None, False, None)

    def test_control_is_exact_and_cannot_call_old_r20(self):
        with self.assertRaisesRegex(ValueError, 'ControlNotAllowed'):
            validate_route('POST', '/verification/r20/payment-followup', {}, None, True, None)
        validate_route('POST', '/verification/r21/payment-followup', {'requestId': RID, 'requestIdempotencyKey': COMMAND}, None, True, None)
        with self.assertRaisesRegex(ValueError, 'FixedControlCommand'):
            validate_route('POST', '/verification/r21/payment-followup', {'requestId': RID, 'requestIdempotencyKey': COMMAND, 'take': 999}, None, True, None)

    def test_duplicate_empty_or_noncanonical_proof_query_rejected(self):
        for query in ('requestId=', 'requestId=' + RID + '&requestId=' + RID, 'requestId=' + RID.replace('-', '')):
            with self.assertRaises(ValueError): validate_route('GET', '/verification/r114/proof?' + query, None, None, True, None)

    def test_control_does_not_accept_actor_or_binary(self):
        with self.assertRaisesRegex(ValueError, 'FixedControl'):
            validate_route('GET', '/verification/r21/payout-dispute?requestId=' + RID, None, 'driver', True, None)

    def test_alert_cycle_has_one_fixed_command_no_arbitrary_observation_input(self):
        validate_route('POST', '/verification/r21/operations-alert-cycle', {'requestIdempotencyKey': COMMAND}, None, True, None)
        with self.assertRaisesRegex(ValueError, 'FixedAlertCycle'):
            validate_route('POST', '/verification/r21/operations-alert-cycle',
                           {'requestIdempotencyKey': COMMAND, 'sourceId': 'arbitrary'}, None, True, None)

    def test_health_is_only_fixed_get(self):
        validate_route('GET', '/health/ready', None, None, False, None)
        with self.assertRaisesRegex(ValueError, 'FixedHealth'): validate_route('POST', '/health/ready', {}, None, False, None)

    def test_source_binding_rejects_wrong_run_and_changed_source(self):
        sources = [{'path': 'known.cs', 'sha256': 'a' * 64}]
        digest = hashlib.sha256(('known.cs\t' + 'a' * 64 + '\n').encode()).hexdigest()
        ready = {'schemaVersion': 'cargo-operations-verification-r21.r1', 'executionMode': 'Simulation',
            'database': 'mirror_operations_r21_20261022', 'baseUrl': 'http://127.0.0.1:5369', 'runStableId': RUN,
            'sourceBinding': {'schemaVersion': source_binding.SCHEMA, 'manifestSha256': digest, 'sourceCount': 1}}
        manifest = {'schemaVersion': source_binding.SCHEMA, 'runStableId': RUN, 'database': ready['database'], 'sources': sources, 'manifestSha256': digest}
        with patch.object(source_binding, '_snapshot', return_value=sources):
            self.assertEqual(digest, source_binding.validate(Path('.'), ready, manifest))
            bad = deepcopy(ready); bad['database'] = 'mirror_operations_r20r2_20261022'
            with self.assertRaisesRegex(RuntimeError, 'EnvironmentMismatch'): source_binding.validate(Path('.'), bad, manifest)
        with patch.object(source_binding, '_snapshot', return_value=[]):
            with self.assertRaisesRegex(RuntimeError, 'ChangedWithinRun'): source_binding.validate(Path('.'), ready, manifest)


class EvidenceTests(unittest.TestCase):
    def test_other_or_missing_prepayment_request_never_counts_as_confirmed(self):
        self.assertTrue(prepayment_belongs_to_request({'의뢰Id': RID}, RID))
        self.assertFalse(prepayment_belongs_to_request({'의뢰Id': COMMAND}, RID))
        self.assertFalse(prepayment_belongs_to_request({}, RID))
        case = fixtures()[0]
        for value in (COMMAND, None):
            data = observation(case); data['prepayment']['body']['의뢰Id'] = value
            self.assertIn('simulation-prepayment-confirmed', evaluate(case, data)['failedChecks'])

    def test_different_same_prefix_run_cannot_match_expected_execution(self):
        case = fixtures()[0]; data = observation(case)
        data['proof']['runStableId'] = 'cargo-operations-r21:' + 'c' * 32
        self.assertIn('source-bound-simulation', evaluate(case, data)['failedChecks'])

    def test_missing_or_changed_source_context_cannot_pass(self):
        case = fixtures()[0]
        for key, value in (('sourceBindingSha256', 'c' * 64), ('caseId', 'other'),
                           ('storedClientRequestId', request_identity(RUN, 'other'))):
            data = observation(case); data['expectedContext'][key] = value
            self.assertIn('source-bound-simulation', evaluate(case, data)['failedChecks'])
        data = observation(case); data.pop('expectedContext')
        self.assertFalse(evaluate(case, data)['passed'])

    def test_stored_request_and_client_request_canonical_lookup_must_match(self):
        case = fixtures()[0]; data = observation(case)
        data['receptionLookup']['body']['의뢰Id'] = COMMAND
        self.assertIn('same-request-across-roles', evaluate(case, data)['failedChecks'])
        data = observation(case); data['expectedContext']['requestId'] = COMMAND
        self.assertIn('same-request-across-roles', evaluate(case, data)['failedChecks'])

    def test_mongo_exists_alone_does_not_confirm_completed_projection(self):
        case = fixtures()[0]
        for key, value in (('rdbProjectionExists', False), ('ledgerId', 'transport:' + COMMAND), ('state', '진행중')):
            data = observation(case); data['proof']['mongo'][key] = value
            self.assertIn('mongo-projection-observed', evaluate(case, data)['failedChecks'])

    def test_full_canonical_normal_case_passes_but_never_claims_real_operation(self):
        case = fixtures()[1]; result = evaluate(case, observation(case))
        self.assertTrue(result['passed'], result['failedChecks'])
        self.assertFalse(result['actualTransferVerified'])
        self.assertFalse(result['physicalDeviceVerified'])
        self.assertFalse(result['trainingApproved'])
        self.assertEqual('unreviewed', result['humanReviewStatus'])

    def test_completion_without_actual_arrival_does_not_pass(self):
        case = fixtures()[0]; data = observation(case); data['driver']['body']['도착'] = None
        self.assertIn('completion-with-arrival', evaluate(case, data)['failedChecks'])

    def test_customer_amount_cannot_substitute_driver_payout(self):
        case = fixtures()[1]; data = observation(case)
        data['driver']['body']['기사지급예정액'] = case['customerChargeKrw']
        self.assertIn('agreed-driver-payout-preserved', evaluate(case, data)['failedChecks'])

    def test_missing_or_other_request_cannot_pass_cross_role_check(self):
        case = fixtures()[0]; data = observation(case); data['driver']['body']['운송번호'] = COMMAND
        self.assertIn('same-request-across-roles', evaluate(case, data)['failedChecks'])

    def test_http_failure_is_not_a_valid_empty_or_completed_response(self):
        case = fixtures()[0]; data = observation(case); data['shipper']['http'] = 503
        self.assertFalse(evaluate(case, data)['passed'])

    def test_unbound_photo_or_unverified_mongo_cannot_pass(self):
        case = fixtures()[0]; data = observation(case)
        data['proof']['photoEvidence'][0]['bindingVerified'] = False
        data['proof']['mongo'] = {'available': False}
        result = evaluate(case, data)
        self.assertIn('two-bound-photo-receipts', result['failedChecks'])
        self.assertIn('mongo-projection-observed', result['failedChecks'])

    def test_fallback_and_model_application_are_separate_facts(self):
        case = fixtures()[0]; result = evaluate(case, observation(case))
        self.assertTrue(result['ai']['rulesFallbackUsed'])
        self.assertFalse(result['ai']['modelSelectionApplied'])
        self.assertTrue(result['passed'])

    def test_representatives_are_deterministic_and_not_human_approval(self):
        rows = [evaluate(case, observation(case)) for case in fixtures()]
        self.assertEqual(representative_cases(rows), representative_cases(reversed(rows)))
        self.assertEqual(3, len(representative_cases(rows)))
        self.assertTrue(all(row['trainingApproved'] is False for row in representative_cases(rows)))
        with self.assertRaisesRegex(ValueError, 'Duplicate'): representative_cases([rows[0], rows[0]])

    def test_failure_has_priority_and_unobserved_cases_are_not_invented(self):
        self.assertEqual([], representative_cases([]))
        case = fixtures()[0]; data = observation(case); data['proof']['transport']['frozenPayout'] = None
        row = evaluate(case, data)
        self.assertEqual('needs-review', representative_cases([row])[0]['reason'])

    def test_immutable_evidence_reuses_hash_and_does_not_overwrite_prior(self):
        with tempfile.TemporaryDirectory() as temporary:
            a = save_immutable(temporary, {'first': True})
            self.assertEqual(a, save_immutable(temporary, {'first': True}))
            b = save_immutable(temporary, {'first': False})
            self.assertNotEqual(a, b)
            self.assertEqual(2, len(list(Path(temporary).glob('*.json'))))


if __name__ == '__main__': unittest.main()
