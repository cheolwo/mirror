"""새 격리 r20 DB의 제품 API만 사용한다. 실제 수납·배송·송금 검증이 아니다."""
import argparse
import importlib.util
import json
import pathlib
import uuid
from source_binding import validate as validate_source_binding

ROOT = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('reused_r114', ROOT / 'eng/verification/cargo-ai-device-r114.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
m.OUT = ROOT / 'artifacts/local/cargo-operations-r20-r2'
m.PRIVATE = m.OUT / '.private'
m.BASE = 'http://127.0.0.1:5368'
m.DB = 'mirror_operations_r20r2_20261022'
m.PRIVATE.mkdir(parents=True, exist_ok=True)
(m.OUT / 'api').mkdir(parents=True, exist_ok=True)

class Client(m.Client):
    def __init__(self):
        self._validate_sources()
        super().__init__()

    def _validate_sources(self):
        ready = m.read(m.OUT / 'host/ready.json')
        manifest = m.read(m.OUT / 'host/source-binding.json')
        self.source_binding_sha256 = validate_source_binding(ROOT, ready, manifest)

    def call(self, method, path, body=None, actor=None, control=False, binary=None, log=True):
        self._validate_sources()
        parsed = m.urllib.parse.urlsplit(path)
        if parsed.path in ('/verification/r20/payout-dispute', '/verification/r20/payment-followup'):
            m.need(control and actor is None and binary is None and not parsed.scheme and not parsed.netloc
                   and not parsed.fragment, 'R20FixedControlRequired')
            if parsed.path.endswith('/payout-dispute'):
                query = m.urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
                m.need(method == 'GET' and body is None and len(query) == 1 and query[0][0] == 'requestId'
                       and str(uuid.UUID(query[0][1])) == query[0][1], 'R20FixedDisputeReadRequired')
            else:
                m.need(method == 'POST' and not parsed.query and isinstance(body, dict), 'R20FixedFollowupRequired')
            data = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
            headers = {'Accept': 'application/json', 'x-r114-key': self.key}
            if data is not None: headers['Content-Type'] = 'application/json; charset=utf-8'
            request = m.urllib.request.Request(m.BASE + path, data=data, method=method, headers=headers)
            try:
                with self.opener.open(request, timeout=45) as response: status, raw = response.status, response.read(262144)
            except m.urllib.error.HTTPError as error: status, raw = error.code, error.read(262144)
            try: result = json.loads(raw) if raw else None
            except (json.JSONDecodeError, UnicodeDecodeError): result = {'errorCode': 'NonJsonResponse', 'bytes': len(raw)}
            if log:
                with (m.OUT / 'api/http.jsonl').open('a', encoding='utf-8') as stream:
                    stream.write(json.dumps(self.safe({'atUtc': m.utc(), 'method': method, 'path': path,
                        'actor': None, 'status': status, 'response': result}), ensure_ascii=False) + '\n')
            return status, result
        return super().call(method, path, body, actor, control, binary, log)

    def once(self, key, method, path, body, *args, **kwargs):
        if method == 'POST' and path == '/api/v1/shipper/requests':
            body = dict(body)
            body['결제수단'] = '카드'
            body['정산조건'] = {'결제수단': 0, '정산시점': 0, '증빙방식': 0, '수납주체': 0}
        return super().once(key, method, path, body, *args, **kwargs)

def pay(client, case):
    rid = client.state['cases'][case['caseId']]['requestId']
    route = '/api/v1/payments/requests/' + rid + '/prepayment'
    status, prior = client.call('GET', route, actor=m.SHIPPER)
    m.need(status == 200 and m.get(prior, 'enabled') and m.get(prior, 'simulationAvailable'), 'SimulationPrepaymentRequired')
    if not m.get(prior, 'paymentConfirmed'):
        m.need(m.get(prior, 'canPay') and m.get(prior, 'amount', 0) > 0, 'PrepaymentNotPayable')
        client.once(case['caseId'] + ':prepay', 'POST', '/api/v1/payments/fake/confirm',
                    {'의뢰Id': rid, 'Amount': m.get(prior, 'amount'), '결제수단': '카드',
                     'IdempotencyKey': 'r20-' + uuid.uuid5(uuid.NAMESPACE_URL, client.run + rid).hex}, m.SHIPPER)
    status, state = client.call('GET', route, actor=m.SHIPPER)
    m.need(status == 200 and m.get(state, 'paymentConfirmed') and m.get(state, 'simulationPayment'), 'SimulationPaymentNotConfirmed')
    return {'requestId': rid, 'paymentConfirmed': True, 'simulationPayment': True,
            'amount': m.get(state, 'amount'), 'dispatchStatus': m.get(state, 'dispatchStatus'), 'realCollection': False}

def recommend(client, case):
    rid = client.state['cases'][case['caseId']]['requestId']
    m.locate(client, case)
    result = client.once(case['caseId'] + ':ai', 'POST', '/verification/r114/ai-recommend',
        {'requestId': rid, 'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId'] + ':ai'))}, control=True)
    client.state['cases'][case['caseId']]['latestProof'] = client.proof(case['caseId'])
    client.save()
    return result

def followup(client, case):
    rid = client.state['cases'][case['caseId']]['requestId']
    return client.once(case['caseId'] + ':payment-followup', 'POST', '/verification/r20/payment-followup',
        {'requestId': rid, 'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + rid + ':payment-followup'))}, control=True)

def status(client):
    rows = []
    for path in ['/health/live', '/health/ready', '/api/v1/common/home/participation-summary']:
        if path in ('/health/live', '/health/ready'):
            # 일반 API 허용 목록은 넓히지 않고 고정 loopback 상태 조회만 분리한다.
            request = m.urllib.request.Request(m.BASE + path, method='GET', headers={'Accept': 'application/json'})
            try:
                with client.opener.open(request, timeout=15) as response:
                    code = response.status
                    response.read(262144)
            except m.urllib.error.HTTPError as error:
                code = error.code
                error.read(262144)
        else:
            code, _ = client.call('GET', path)
        rows.append({'path': path, 'http': code})
    return {'reads': rows, 'executionMode': 'Simulation', 'freshModelNotOperationalMigration': True}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['status','setup','create','pay','followup','recommend','complete','proof','final'])
    parser.add_argument('--case', type=int, choices=[1,2,3], default=1)
    parser.add_argument('--until', choices=['accepted','pickup-arrived','picked-up','dropoff-arrived','completed'], default='completed')
    args = parser.parse_args()
    client = Client()
    case = m.fixtures(client)[args.case - 1]
    result = {
        'status': lambda: status(client), 'setup': lambda: m.setup(client),
        'create': lambda: m.create(client, case), 'pay': lambda: pay(client, case), 'followup': lambda: followup(client, case),
        'recommend': lambda: recommend(client, case),
        'complete': lambda: m.complete(client, case, args.until),
        'proof': lambda: client.proof(case['caseId']), 'final': lambda: m.final_review(client, case)
    }[args.action]()
    print(json.dumps(client.safe({'action': args.action, 'result': result, 'sourceBindingSha256': client.source_binding_sha256, 'physicalDeviceVerified': False,
                                 'realCollection': False, 'actualTransfer': False}), ensure_ascii=False))

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(json.dumps({'success': False, 'errorCode': str(error) if type(error) is RuntimeError else type(error).__name__,
                          'physicalDeviceVerified': False}, ensure_ascii=False))
        raise SystemExit(1)
