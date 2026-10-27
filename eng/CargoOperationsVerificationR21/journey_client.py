"""명시적으로 시작한 격리 실행의 HTTP·멱등 상태만 소유한다. import는 부작용이 없다."""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import urllib.parse
import uuid

from fixture_catalog import build_request, request_identity
from source_binding import validate as validate_source_binding


def prepayment_belongs_to_request(value, request_id):
    return isinstance(value, dict) and isinstance(request_id, str) and bool(request_id) \
        and next((v for k, v in value.items() if k.casefold() == '의뢰id'.casefold()), None) == request_id


@dataclass(frozen=True)
class RunEnvironment:
    root: Path
    base_url: str = 'http://127.0.0.1:5369'
    database: str = 'mirror_operations_r21_20261022'

    @property
    def output(self):
        return self.root / 'artifacts/local/cargo-operations-r21'

    def validate(self):
        if self.base_url != 'http://127.0.0.1:5369' or self.database != 'mirror_operations_r21_20261022':
            raise ValueError('R21DedicatedEnvironmentRequired')
        if not (self.root / 'eng/CargoOperationsVerificationR21/CargoOperationsVerificationR21.csproj').is_file():
            raise ValueError('R21RepositoryRequired')


def validate_route(method, path, body, actor, control, binary):
    """이 검증 도구가 고정된 로컬 경계 밖의 URL·임의 제어 명령을 받지 않는다."""
    parsed = urllib.parse.urlsplit(path)
    if parsed.scheme or parsed.netloc or parsed.fragment or not path.startswith('/') or path.startswith('//'):
        raise ValueError('R21RelativeApiRequired')
    if parsed.path in ('/health/live', '/health/ready'):
        if method != 'GET' or parsed.query or body is not None or actor or control or binary:
            raise ValueError('R21FixedHealthReadRequired')
        return
    if parsed.path.startswith('/verification/'):
        if not control or actor is not None or binary is not None:
            raise ValueError('R21FixedControlRequired')
        if parsed.path in ('/verification/r114/proof', '/verification/r21/payout-dispute'):
            query = urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
            if method != 'GET' or body is not None or len(query) != 1 or query[0][0] != 'requestId':
                raise ValueError('R21FixedProofReadRequired')
            try:
                value = uuid.UUID(query[0][1])
                if value.int == 0 or str(value) != query[0][1]: raise ValueError()
            except ValueError:
                raise ValueError('R21CanonicalRequestIdRequired') from None
            return
        if parsed.path == '/verification/r21/operations-alert-cycle':
            if method != 'POST' or parsed.query or not isinstance(body, dict) or set(body) != {'requestIdempotencyKey'}:
                raise ValueError('R21FixedAlertCycleCommandRequired')
            try:
                value = uuid.UUID(body['requestIdempotencyKey'])
                if value.int == 0 or str(value) != body['requestIdempotencyKey']: raise ValueError()
            except (ValueError, TypeError, AttributeError):
                raise ValueError('R21CanonicalCommandIdentityRequired') from None
            return
        if parsed.path not in ('/verification/r114/ai-recommend', '/verification/r21/payment-followup'):
            raise ValueError('R21ControlNotAllowed')
        if method != 'POST' or parsed.query or not isinstance(body, dict) or set(body) != {'requestId', 'requestIdempotencyKey'}:
            raise ValueError('R21FixedControlCommandRequired')
        for name in ('requestId', 'requestIdempotencyKey'):
            try:
                value = uuid.UUID(body[name])
                if value.int == 0 or str(value) != body[name]: raise ValueError()
            except (ValueError, TypeError, AttributeError):
                raise ValueError('R21CanonicalCommandIdentityRequired') from None
        return
    if not parsed.path.startswith('/api/v1/') or control:
        raise ValueError('R21ProductApiRequired')


class JourneyRuntime:
    """r114 단계 수행을 명시적인 r21 환경에 결속하는 어댑터. 제품 정책은 변경하지 않는다."""
    def __init__(self, environment):
        environment.validate()
        self.environment = environment
        # 과거 모듈의 전역 환경 의존은 이 명시적 실행 생성 경계 안에만 둔다.
        spec = importlib.util.spec_from_file_location('r21_reused_r114', environment.root / 'eng/verification/cargo-ai-device-r114.py')
        m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(m)
        m.OUT, m.PRIVATE, m.BASE, m.DB = environment.output, environment.output / '.private', environment.base_url, environment.database
        m.PRIVATE.mkdir(parents=True, exist_ok=True)
        (m.OUT / 'api').mkdir(parents=True, exist_ok=True)
        self.legacy = m

        class BoundClient(m.Client):
            def __init__(self):
                self.validate_sources()
                super().__init__()

            def validate_sources(self):
                ready = m.read(m.OUT / 'host/ready.json')
                manifest = m.read(m.OUT / 'host/source-binding.json')
                self.source_binding_sha256 = validate_source_binding(environment.root, ready, manifest)

            def call(self, method, path, body=None, actor=None, control=False, binary=None, log=True):
                self.validate_sources()
                validate_route(method, path, body, actor, control, binary)
                if path.startswith('/api/v1/') or path.startswith('/verification/r114/'):
                    return super().call(method, path, body, actor, control, binary, log)
                data = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
                headers = {'Accept': 'application/json'}
                if control: headers['x-r114-key'] = self.key
                if data is not None: headers['Content-Type'] = 'application/json; charset=utf-8'
                request = m.urllib.request.Request(m.BASE + path, data=data, method=method, headers=headers)
                try:
                    with self.opener.open(request, timeout=45) as response: status, raw = response.status, response.read(262144)
                except m.urllib.error.HTTPError as error: status, raw = error.code, error.read(262144)
                try: result = json.loads(raw) if raw else None
                except (ValueError, UnicodeError): result = {'errorCode': 'NonJsonResponse', 'bytes': len(raw)}
                if log:
                    with (m.OUT / 'api/http.jsonl').open('a', encoding='utf-8') as stream:
                        stream.write(json.dumps(self.safe({'atUtc': m.utc(), 'method': method, 'path': path,
                            'actor': None, 'status': status, 'response': result}), ensure_ascii=False) + '\n')
                return status, result

        self.client = BoundClient()

    def create(self, case):
        m, client = self.legacy, self.client
        item = client.state['cases'].setdefault(case['caseId'], {})
        cid = request_identity(client.run, case['caseId'])
        status, prior = client.call('GET', '/api/v1/shipper/requests/by-client-request/' + cid, actor=m.SHIPPER)
        if status == 200 and m.get(prior, '의뢰Id'):
            item.update(requestId=m.get(prior, '의뢰Id'), clientRequestId=cid)
            client.save()
            return {'alreadyReceived': True, 'mutationRepeated': False}
        m.need(status == 404, 'R21CreateLookupUncertain')
        consent = str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId'] + ':privacy'))
        client.once(case['caseId'] + ':consent', 'POST', '/api/v1/common/application-privacy-consents', {
            '증적Id': consent, '업무Code': 'transport-proxy', '출처Code': 'community-map',
            '동의문버전': 'application-privacy-consent-draft-2026-08-04', '수집이용동의': True, '연령요건확인': True}, m.SHIPPER)
        # 시간창을 포함한 첫 payload를 내구성 상태에 보관하고 재실행 때 그대로 사용한다.
        if 'preparedRequest' not in item:
            item['preparedRequest'] = build_request(case, client.run, consent, datetime.now(timezone.utc))
            client.save()
        created = client.once(case['caseId'] + ':create', 'POST', '/api/v1/shipper/requests', item['preparedRequest'], m.SHIPPER)
        rid = m.get(created, '의뢰Id')
        m.need(rid, 'R21CreatedRequestIdMissing')
        item.update(requestId=rid, clientRequestId=cid)
        client.save()
        return {'requestId': rid, 'received': True, 'paymentConfirmed': False}

    def pay(self, case):
        m, c = self.legacy, self.client
        rid = c.state['cases'][case['caseId']]['requestId']
        route = '/api/v1/payments/requests/' + rid + '/prepayment'
        status, state = c.call('GET', route, actor=m.SHIPPER)
        m.need(status == 200 and prepayment_belongs_to_request(state, rid)
            and m.get(state, 'enabled') and m.get(state, 'simulationAvailable'), 'R21SimulationPrepaymentRequired')
        if not m.get(state, 'paymentConfirmed'):
            m.need(m.get(state, 'canPay') and m.get(state, 'amount', 0) > 0, 'R21PrepaymentNotPayable')
            c.once(case['caseId'] + ':prepay', 'POST', '/api/v1/payments/fake/confirm',
                   {'의뢰Id': rid, 'Amount': m.get(state, 'amount'), '결제수단': '카드',
                    'IdempotencyKey': 'r21-' + uuid.uuid5(uuid.NAMESPACE_URL, c.run + rid).hex}, m.SHIPPER)
        status, state = c.call('GET', route, actor=m.SHIPPER)
        m.need(status == 200 and prepayment_belongs_to_request(state, rid)
            and m.get(state, 'paymentConfirmed') and m.get(state, 'simulationPayment'), 'R21SimulationPaymentNotConfirmed')
        return {'paymentConfirmed': True, 'simulationPayment': True, 'amount': m.get(state, 'amount'), 'realCollection': False}

    def followup(self, case):
        c = self.client
        rid = c.state['cases'][case['caseId']]['requestId']
        return c.once(case['caseId'] + ':payment-followup', 'POST', '/verification/r21/payment-followup',
                      {'requestId': rid, 'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, c.run + rid + ':payment-followup'))}, control=True)

    def operations_alert_cycle(self):
        c = self.client
        result = c.once('operations-alert-cycle', 'POST', '/verification/r21/operations-alert-cycle',
            {'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, c.run + ':operations-alert-cycle'))}, control=True)
        self.legacy.need(self.legacy.get(self.legacy.get(result, 'result'), 'passed') is True, 'R21AlertCycleNotVerified')
        return result

    def recommend(self, case):
        m, c = self.legacy, self.client
        m.locate(c, case)
        rid = c.state['cases'][case['caseId']]['requestId']
        result = c.once(case['caseId'] + ':ai', 'POST', '/verification/r114/ai-recommend',
            {'requestId': rid, 'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, c.run + case['caseId'] + ':ai'))}, control=True)
        c.state['cases'][case['caseId']]['latestProof'] = c.proof(case['caseId'])
        c.save()
        self.capture_offer(case)
        return result

    def capture_offer(self, case):
        """수락 전 기사에게 실제 제시한 보수도 보관한다. 고객 청구액으로 대체하지 않는다."""
        m, c = self.legacy, self.client
        item = c.state['cases'][case['caseId']]
        found = []
        for actor in m.DRIVERS:
            status, rows = c.call('GET', '/api/v1/driver/recommendations', actor=actor)
            m.need(status == 200 and isinstance(rows, list), 'R21OfferListUnavailable')
            found += [(actor, row) for row in rows if m.get(row, '의뢰Id') == item['requestId']]
        m.need(len(found) == 1, 'R21OneCanonicalOfferRequired')
        actor, row = found[0]
        amount = m.get(row, '기사지급예정운임')
        m.need(amount == case['agreedDriverPayoutKrw'], 'R21OfferedDriverPayoutMismatch')
        item['observedOffer'] = {'requestId': item['requestId'], 'driverId': actor,
            'round': m.get(row, '추천라운드'), 'driverPayoutKrw': amount}
        c.save()
        return item['observedOffer']

    def complete(self, case, until='completed'):
        item = self.client.state['cases'][case['caseId']]
        if not item.get('transportId') and not item.get('completedViaLocalApi'):
            self.capture_offer(case)
        return self.legacy.complete(self.client, case, until)

    def canonical(self, case):
        """읽기 결과를 모으기만 한다. 판정·대표 사례 분류는 evidence_review가 소유한다."""
        m, c = self.legacy, self.client
        item = c.state['cases'][case['caseId']]
        rid = item['requestId']
        routes = {'shipper': ('/api/v1/shipper/requests/' + rid, m.SHIPPER),
                  'receptionLookup': ('/api/v1/shipper/requests/by-client-request/' + request_identity(c.run, case['caseId']), m.SHIPPER),
                  'prepayment': ('/api/v1/payments/requests/' + rid + '/prepayment', m.SHIPPER)}
        owner = item.get('driverId')
        if owner and item.get('transportId'):
            routes['driver'] = ('/api/v1/driver/transports/' + str(item['transportId']), owner)
        result = {name: {'http': status, 'body': body} for name, (route, actor) in routes.items()
                  for status, body in [c.call('GET', route, actor=actor)]}
        result['proof'] = c.proof(case['caseId'])
        result['observedOffer'] = item.get('observedOffer')
        result['sourceBindingSha256'] = c.source_binding_sha256
        result['expectedContext'] = {'runStableId': c.run, 'sourceBindingSha256': c.source_binding_sha256,
            'caseId': case['caseId'], 'requestId': rid, 'transportId': item.get('transportId'),
            'clientRequestId': request_identity(c.run, case['caseId']), 'storedClientRequestId': item.get('clientRequestId')}
        return result
