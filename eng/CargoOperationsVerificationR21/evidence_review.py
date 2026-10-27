"""순수 정본 판정·대표 사례 선별과 append-only 증거 저장. 업무 명령은 호출하지 않는다."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

from fixture_catalog import request_identity
from journey_client import prepayment_belongs_to_request

SCHEMA = 'cargo-operations-r21.case-review.v1'


def get(value, key, default=None):
    return next((v for k, v in value.items() if k.casefold() == key.casefold()), default) if isinstance(value, dict) else default


def evaluate(case, observation):
    """응답 한 개를 완료로 승격하지 않는다. 서로 다른 역할의 정본과 원장을 대조한다."""
    proof = observation.get('proof') or {}
    shipper = observation.get('shipper') or {}
    driver = observation.get('driver') or {}
    prepay = observation.get('prepayment') or {}
    lookup = observation.get('receptionLookup') or {}
    context = observation.get('expectedContext') or {}
    s, d, p = shipper.get('body') or {}, driver.get('body') or {}, prepay.get('body') or {}
    transport = get(proof, 'transport', {})
    request = get(proof, 'request', {})
    try:
        expected_context_valid = context.get('caseId') == case['caseId'] \
            and context.get('clientRequestId') == context.get('storedClientRequestId') == request_identity(context.get('runStableId'), case['caseId']) \
            and bool(re.fullmatch('[0-9a-f]{64}', str(context.get('sourceBindingSha256', '')))) \
            and context.get('sourceBindingSha256') == observation.get('sourceBindingSha256')
    except (ValueError, TypeError):
        expected_context_valid = False
    expected = case['agreedDriverPayoutKrw']
    photos = get(proof, 'photoEvidence', [])
    verified_photos = {get(row, 'photoKind') for row in photos
                       if get(row, 'bindingVerified') is True and get(row, 'completionLinked') is True
                       and get(row, 'storageIntegrityVerified') is True}
    checks = {
        'source-bound-simulation': get(proof, 'schemaVersion') == 'r114-ai-cargo-canonical-proof.v1'
            and get(proof, 'synthetic') is True and get(proof, 'database') == 'mirror_operations_r21_20261022'
            and expected_context_valid and get(proof, 'runStableId') == context.get('runStableId'),
        'same-request-across-roles': shipper.get('http') == 200 and driver.get('http') == 200
            and bool(context.get('requestId')) and get(request, 'id') == get(s, '의뢰Id') == get(d, '운송번호') == context.get('requestId')
            and get(transport, 'id') == get(d, 'id') == context.get('transportId')
            and lookup.get('http') == 200 and get(lookup.get('body'), '의뢰Id') == context.get('requestId'),
        'simulation-prepayment-confirmed': prepay.get('http') == 200 and get(p, 'paymentConfirmed') is True
            and get(p, 'simulationPayment') is True and get(p, 'amount') == case['customerChargeKrw']
            and prepayment_belongs_to_request(p, context.get('requestId')),
        'customer-charge-preserved': get(s, '결제예정금액') == case['customerChargeKrw'],
        'server-quote-confirmed': isinstance(get(s, '최종운임'), (int, float))
            and 0 < get(s, '최종운임') <= case['customerChargeKrw']
            and get(s, '최종운임') == get(get(s, '요금옵션', {}), '최종운임'),
        'agreed-driver-payout-preserved': get(get(s, '요금옵션', {}), '기사지급예정운임') == expected
            and get(d, '기사지급예정액') == expected and get(transport, 'frozenPayout') == expected,
        'recommendation-payout-preserved': get(observation.get('observedOffer'), 'requestId') == get(request, 'id')
            and get(observation.get('observedOffer'), 'driverPayoutKrw') == expected
            and get(observation.get('observedOffer'), 'driverId') == get(transport, 'confirmedDriverId'),
        'confirmed-performing-driver-match': bool(get(transport, 'confirmedDriverId'))
            and get(transport, 'confirmedDriverId') == get(transport, 'performingDriverId') == get(d, '기사_운송자'),
        'completion-with-arrival': get(s, '운송상태') == '인수완료' and get(d, '상태') == '인수완료'
            and get(transport, 'state') == '인수완료' and bool(get(d, '도착')),
        'two-bound-photo-receipts': {'pickup-complete-photo', 'dropoff-complete-photo'}.issubset(verified_photos),
        'mongo-projection-observed': get(get(proof, 'mongo', {}), 'available') is True
            and get(get(proof, 'mongo', {}), 'exists') is True
            and get(get(proof, 'mongo', {}), 'rdbProjectionExists') is True
            and get(get(proof, 'mongo', {}), 'ledgerId') == 'transport:' + str(context.get('requestId'))
            and get(get(proof, 'mongo', {}), 'state') == '완료',
        'actual-finance-not-claimed': get(proof, 'actualPaymentVerified') is False
            and get(proof, 'productionDispatchEnabled') is False
    }
    ai = get(proof, 'actualCargoAI', {})
    ai_applied = get(proof, 'aiRecommendationVerified') is True
    fallback = get(proof, 'fallbackUsed') is True
    return {'schemaVersion': SCHEMA, 'caseId': case['caseId'], 'kind': case['kind'],
            'observedAtUtc': get(proof, 'atUtc'), 'checks': checks,
            'passed': all(checks.values()), 'failedChecks': [name for name, passed in checks.items() if not passed],
            'amounts': {'customerChargeKrw': get(s, '결제예정금액'),
                        'serverQuoteKrw': get(get(s, '요금옵션', {}), '최종운임'),
                        'agreedDriverPayoutKrw': get(get(s, '요금옵션', {}), '기사지급예정운임'),
                        'frozenDriverPayoutKrw': get(transport, 'frozenPayout')},
            'ai': {'status': get(ai, 'status'), 'modelSelectionApplied': ai_applied, 'rulesFallbackUsed': fallback},
            'exceptionChecks': [], 'humanReviewStatus': 'unreviewed', 'trainingApproved': False,
            'physicalDeviceVerified': False, 'actualCameraCaptureVerified': False,
            'realCollectionVerified': False, 'actualTransferVerified': False,
            'automaticOperationVerified': False, 'operationalMigrationVerified': False}


def representative_cases(reviews, limit=5):
    """실패/규칙대체/금액분리/팔레트/기본 정상 사례를 결정적으로 한 건씩 고른다."""
    if type(limit) is not int or limit < 1 or limit > 100:
        raise ValueError('R21RepresentativeLimitInvalid')
    rows = list(reviews)
    if any(row.get('schemaVersion') != SCHEMA or not row.get('caseId') for row in rows):
        raise ValueError('R21ReviewSchemaRequired')
    if len({row['caseId'] for row in rows}) != len(rows):
        raise ValueError('R21DuplicateCaseReview')
    ordered = sorted(rows, key=lambda row: row['caseId'])
    predicates = [('needs-review', lambda row: not row.get('passed')),
                  ('rules-fallback', lambda row: row.get('ai', {}).get('rulesFallbackUsed') is True),
                  ('separated-fare', lambda row: row.get('kind') == 'normal-separated-fare'),
                  ('pallet', lambda row: row.get('kind') == 'normal-pallet'),
                  ('normal', lambda row: row.get('passed') is True)]
    selected, used = [], set()
    for reason, predicate in predicates:
        match = next((row for row in ordered if row['caseId'] not in used and predicate(row)), None)
        if match is not None:
            selected.append({'caseId': match['caseId'], 'reason': reason, 'passed': match['passed'],
                             'humanReviewStatus': 'unreviewed', 'trainingApproved': False})
            used.add(match['caseId'])
        if len(selected) == limit: break
    return selected


def save_immutable(directory, value):
    """내용 주소를 사용하는 새 증거만 저장한다. 기존 실행/관측은 덮어쓰지 않는다."""
    body = (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')
    sha = hashlib.sha256(body).hexdigest()
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / (sha + '.json')
    try:
        with path.open('xb') as stream:
            stream.write(body)
            stream.flush()
            import os
            os.fsync(stream.fileno())
    except FileExistsError:
        if path.read_bytes() != body:
            raise ValueError('R21EvidenceHashMismatch') from None
    return {'sha256': sha, 'fileName': path.name}
