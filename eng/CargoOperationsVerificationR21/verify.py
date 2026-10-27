"""r21 명령 조립만 담당한다. 자료/HTTP 상태/정본 판정/출력은 별도 모듈이 소유한다."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from fixture_catalog import catalog, fixtures, STAGES
from journey_client import JourneyRuntime, RunEnvironment
from evidence_review import evaluate, representative_cases, save_immutable


def review(runtime, case):
    observation = runtime.canonical(case)
    raw = save_immutable(runtime.environment.output / '.private/observations', observation)
    result = evaluate(case, observation)
    result['observationSha256'] = raw['sha256']
    saved = save_immutable(runtime.environment.output / 'reviews', result)
    runtime.client.state['cases'][case['caseId']]['latestReview'] = saved
    runtime.client.save()
    return result


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['fixtures', 'status', 'setup', 'create', 'pay', 'followup',
        'recommend', 'complete', 'guards', 'proof', 'review', 'representatives', 'alert-cycle'])
    parser.add_argument('--case', type=int, choices=[1, 2, 3], default=1)
    parser.add_argument('--until', choices=STAGES, default='completed')
    parser.add_argument('--stage', choices=['recommended', 'pickup-arrived', 'dropoff-arrived'], default='recommended')
    args = parser.parse_args(argv)
    root = Path(__file__).resolve().parents[2]
    environment = RunEnvironment(root)
    if args.action == 'fixtures':
        result = catalog()
        saved = save_immutable(environment.output / 'fixtures', result)
        print(json.dumps({'action': 'fixtures', 'caseCount': len(result['cases']), 'artifact': saved,
                          'businessMutation': False, 'synthetic': True}, ensure_ascii=False))
        return
    runtime = JourneyRuntime(environment)
    c, m = runtime.client, runtime.legacy
    case = fixtures()[args.case - 1]
    if args.action == 'status':
        result = {'reads': [{'path': path, 'http': c.call('GET', path)[0]}
                           for path in ('/health/live', '/health/ready', '/api/v1/common/home/participation-summary')]}
    elif args.action == 'setup': result = m.setup(c)
    elif args.action == 'create': result = runtime.create(case)
    elif args.action == 'pay': result = runtime.pay(case)
    elif args.action == 'followup': result = runtime.followup(case)
    elif args.action == 'alert-cycle':
        result = runtime.operations_alert_cycle()
        save_immutable(environment.output / 'operations-alert-cycle', result)
    elif args.action == 'recommend':
        raw = runtime.recommend(case)
        result = {'artifact': save_immutable(environment.output / '.private/recommendations', raw),
                  'modelSelectionApplied': m.get(m.get(raw, 'proof', {}), 'aiRecommendationVerified', False),
                  'rulesFallbackUsed': m.get(m.get(raw, 'proof', {}), 'fallbackUsed', False)}
    elif args.action == 'complete': result = runtime.complete(case, args.until)
    elif args.action == 'guards':
        result = m.guards(c, case, args.stage)
        save_immutable(environment.output / 'exceptions', result)
    elif args.action == 'proof':
        result = {'artifact': save_immutable(environment.output / '.private/proofs', c.proof(case['caseId'])),
                  'canonicalRead': True, 'businessMutation': False}
    elif args.action == 'review': result = review(runtime, case)
    else:
        reviews = []
        for item in c.state['cases'].values():
            saved = item.get('latestReview')
            if saved:
                path = environment.output / 'reviews' / saved['fileName']
                if path.name != saved['sha256'] + '.json': raise ValueError('R21ReviewReferenceInvalid')
                body = path.read_bytes()
                import hashlib
                if hashlib.sha256(body).hexdigest() != saved['sha256']: raise ValueError('R21ReviewHashMismatch')
                reviews.append(json.loads(body))
        result = {'observedCaseCount': len(reviews), 'representatives': representative_cases(reviews),
                  'unobservedCases': [item['caseId'] for item in fixtures() if item['caseId'] not in {row['caseId'] for row in reviews}],
                  'trainingApproved': False}
        save_immutable(environment.output / 'representatives', result)
    print(json.dumps(c.safe({'action': args.action, 'caseId': case['caseId'], 'result': result,
        'sourceBindingSha256': c.source_binding_sha256, 'executionMode': 'Simulation',
        'physicalDeviceVerified': False, 'realCollection': False, 'actualTransfer': False}), ensure_ascii=False))


if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(json.dumps({'success': False, 'errorCode': str(error) if type(error) in (RuntimeError, ValueError) else type(error).__name__,
                          'physicalDeviceVerified': False}, ensure_ascii=False))
        raise SystemExit(1)
