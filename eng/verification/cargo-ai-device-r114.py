"""고정 seed의 합성 법정동 사례를 기존 앱 API로 검증한다. 물리폰·실물·실금융 증거가 아니다."""
from __future__ import annotations
import argparse, base64, datetime as dt, hashlib, json, random, sys, urllib.error, urllib.parse, urllib.request, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/local/cargo-ai-device-r114'
PRIVATE = OUT / '.private'
BASE = 'http://127.0.0.1:5364'
DB = 'mirror_ai_device_r114_20261019'
DRIVERS = ['cargo-journey-driver', 'cargo-journey-driver-b']
SHIPPER = 'cargo-journey-shipper'
REGIONS_SHA = '737d57de3bc595aaafaba81b61044f61d76902c0bf3d2391899ac471837a973b'
JSON_ARGS = dict(ensure_ascii=False, indent=2)
PRIVATE.mkdir(parents=True, exist_ok=True)
sys.stdout.reconfigure(encoding='utf-8')

def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def write(path, value):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + '.tmp')
    temp.write_text(json.dumps(value, **JSON_ARGS) + '\n', encoding='utf-8'); temp.replace(path)
def digest(value): return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')).encode()).hexdigest()
def utc(value=None): return (value or dt.datetime.now(dt.timezone.utc)).isoformat().replace('+00:00', 'Z')
def get(obj, key, default=None):
    return next((v for k, v in obj.items() if k.casefold() == key.casefold()), default) if isinstance(obj, dict) else default
def need(condition, code):
    if not condition: raise RuntimeError(code)

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs): return None

class Client:
    def __init__(self):
        self.ready = read(OUT / 'host/ready.json')
        need(get(self.ready, 'database') == DB and get(self.ready, 'executionMode') == 'Simulation', 'DedicatedSimulationRequired')
        need(get(self.ready, 'baseUrl', '').rstrip('/') == BASE, 'ExpectedLoopbackHostRequired')
        self.run = get(self.ready, 'runStableId')
        need(self.run, 'RunIdentityRequired')
        private_environment = read(ROOT / 'artifacts/local/usb-device-verification-r74/.private/environment.json')
        self.key = private_environment['USB_R74_ACCESS_KEY']
        self.secret_values = [x for x in private_environment.values() if isinstance(x, str) and len(x) >= 12]
        account_file = read(OUT / 'host/.private/accounts.json')
        self.accounts = {get(a, 'userName'): a for a in get(account_file, 'accounts', [])}
        need(set(DRIVERS + [SHIPPER]).issubset(self.accounts), 'SyntheticAccountsRequired')
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
        self.state_path = PRIVATE / 'api-state.json'
        self.state = read(self.state_path) if self.state_path.exists() else {'runStableId': self.run, 'database': DB, 'commands': {}, 'cases': {}}
        need(self.state['runStableId'] == self.run and self.state['database'] == DB, 'PriorRunPreserved')
        self.token_path = PRIVATE / 'api-token-cache.json'
        cache = read(self.token_path) if self.token_path.exists() else {}
        self.tokens = cache.get('tokens', {}) if cache.get('runStableId') == self.run else {}
        for path in PRIVATE.glob('r114-*-probe-*.json'):
            item = read(path)
            if item.get('baseAddress') == BASE and item.get('driverId') in DRIVERS and any(x.get('requestId') == item.get('requestId') for x in self.state['cases'].values()):
                self.tokens.setdefault(item['driverId'], item.get('accessToken'))
        # 앱처럼 유효한 인증을 재사용한다. 서버 인증 제한을 끄거나 우회하지 않는다.
        now = dt.datetime.now(dt.timezone.utc).timestamp()
        for actor, token in list(self.tokens.items()):
            try:
                payload = token.split('.')[1]; claims = json.loads(base64.urlsafe_b64decode(payload + '=' * (-len(payload) % 4)))
                if claims.get('exp', 0) < now + 30: self.tokens.pop(actor)
            except Exception: self.tokens.pop(actor)
    def save(self): write(self.state_path, self.state)
    def safe(self, value):
        if isinstance(value, dict): return {k: ('[omitted]' if any(t in k.casefold() for t in ('password', 'secret', 'connectionstring')) or k.casefold() in ('token', 'accesstoken', 'refreshtoken') else self.safe(v)) for k, v in value.items()}
        if isinstance(value, list): return [self.safe(v) for v in value]
        if isinstance(value, str):
            for secret in self.secret_values: value = value.replace(secret, '[omitted]')
        return value
    def call(self, method, path, body=None, actor=None, control=False, binary=None, log=True):
        need(path.startswith('/api/v1/') or path.startswith('/verification/r114/'), 'AllowedLocalApiRequired')
        headers = {'Accept': 'application/json'}
        if actor: headers['Authorization'] = 'Bearer ' + self.login(actor)
        if control: headers['x-r114-key'] = self.key
        if binary:
            data, content_type = binary; headers['Content-Type'] = content_type
        else:
            data = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
            if data is not None: headers['Content-Type'] = 'application/json; charset=utf-8'
        request = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
        try:
            with self.opener.open(request, timeout=45) as response: status, raw = response.status, response.read()
        except urllib.error.HTTPError as error: status, raw = error.code, error.read()
        try: result = json.loads(raw) if raw else None
        except (json.JSONDecodeError, UnicodeDecodeError): result = {'errorCode': 'NonJsonResponse', 'bytes': len(raw)}
        if log:
            record = {'atUtc': utc(), 'method': method, 'path': path, 'actor': actor, 'status': status, 'response': self.safe(result)}
            with (OUT / 'api/http.jsonl').open('a', encoding='utf-8') as stream: stream.write(json.dumps(record, ensure_ascii=False) + '\n')
        return status, result
    def login(self, actor):
        if actor in self.tokens: return self.tokens[actor]
        need(actor in self.accounts, 'TestActorRequired')
        status, data = self.call('POST', '/api/v1/auth/login', {'userNameOrEmail': actor, 'password': get(self.accounts[actor], 'password')}, log=False)
        need(status == 200, 'SyntheticLoginFailed_' + str(status))
        token = get(data, 'accessToken') or get(data, 'token'); need(isinstance(token, str), 'LoginTokenMissing')
        self.tokens[actor] = token
        write(self.token_path, {'runStableId': self.run, 'tokens': self.tokens})
        return token
    def once(self, key, method, path, body, actor=None, control=False, binary=None, allowed=(200, 201, 204)):
        signature = digest({'method': method, 'path': path, 'body': body, 'actor': actor, 'binarySha': hashlib.sha256(binary[0]).hexdigest() if binary else None})
        prior = self.state['commands'].get(key)
        if prior:
            need(prior['payloadSha'] == signature, 'CommandPayloadChanged_' + key)
            need(prior['state'] == 'Confirmed', 'PriorMutationRequiresCanonicalReview_' + key)
            return prior.get('response')
        entry = {'state': 'Unknown', 'payloadSha': signature, 'startedAtUtc': utc()}
        self.state['commands'][key] = entry; self.save()
        status, result = self.call(method, path, body, actor, control, binary)
        entry.update(status=status, response=self.safe(result), endedAtUtc=utc(), state='Confirmed' if status in allowed else 'Rejected')
        self.save(); need(status in allowed, 'ApiRejected_' + key + '_' + str(status))
        return result
    def proof(self, case):
        rid = self.state['cases'][case]['requestId']
        status, result = self.call('GET', '/verification/r114/proof?requestId=' + urllib.parse.quote(rid, safe=''), control=True)
        need(status == 200, 'ProofFailed_' + str(status)); return result

def fixtures(client):
    path = ROOT / 'eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json'
    data = path.read_bytes(); need(hashlib.sha256(data).hexdigest() == REGIONS_SHA, 'RegionSourceChanged')
    regions = json.loads(data)['regions']; rng = random.Random(20261019)
    points = [('격리 검증 상차 창고', 37.588, 127.085, '합성 검증 하차지 도로명 주소', 37.590, 127.087),
              ('픽업 장소 4', 37.583, 127.086, '전달 장소 4', 37.585, 127.089),
              ('픽업 장소 5', 37.588, 127.092, '전달 장소 5', 37.590, 127.095)]
    result = []
    for index, coords in enumerate(points, 1):
        pickup, dropoff = rng.sample(regions, 2)
        result.append({'caseId': f'r114-{index:02}', 'pickupRegion': pickup['legalDongCode'], 'pickupLabel': pickup.get('name') or pickup.get('displayName'),
                       'dropoffRegion': dropoff['legalDongCode'], 'dropoffLabel': dropoff.get('name') or dropoff.get('displayName'),
                       'weightKg': rng.choice([1, 2, 3]), 'fareKrw': rng.choice([6000, 6200, 6400]), 'points': list(coords)})
    output = {'seed': 20261019, 'regionSourceSha256': REGIONS_SHA, 'cases': result, 'allOrdersDriversAndCoordinatesSynthetic': True,
              'legalDongTagsAreScenarioLabelsOnly': True, 'coordinatesMappedToActualLegalDong': False, 'physicalDeviceVerified': False}
    file = OUT / 'fixtures.json'
    if file.exists(): need(read(file) == output, 'ExistingFixturesPreserved')
    else: write(file, output)
    return result

def setup(client):
    for index, actor in enumerate(DRIVERS):
        status, work = client.call('GET', '/api/v1/driver/work/status', actor=actor); need(status == 200, 'DriverStatusFailed')
        if get(work, 'status') != '운행중':
            client.once('shift-' + actor, 'POST', '/api/v1/driver/work/start', {'ClientRequestId': str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + actor + ':shift')),
                        '시작모드': '바로시작', '시작위치': 'R114 합성 배차 검증', '커뮤니티운행공개': False}, actor)
        status, participation = client.call('GET', '/api/v1/driver/work-participation', actor=actor); need(status == 200, 'ParticipationLookupFailed')
        settings = get(participation, 'settings', {}); revision = get(settings, 'revision', 0)
        if not get(settings, 'quickDelivery') or not get(settings, 'cargoTransport'):
            client.once('participation-' + actor, 'PUT', '/api/v1/driver/work-participation', {'expectedRevision': revision, 'settings': {
                'quickDelivery': True, 'cargoTransport': True, 'vehicleKind': 'Truck', 'capacityKg': 500, 'capacityLitres': 1000,
                'maxConcurrentJobs': 1, 'receiveAdditionalOffers': True}}, actor)
        client.once('intent-on-' + actor, 'PUT', '/api/v1/driver/operational-dispatch/availability/intent', {
            '클라이언트요청Id': str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + actor + ':on')), '수신의사Code': 'On'}, actor)
    return {'driversPrepared': len(DRIVERS), 'viaProductApis': True, 'synthetic': True}

def create(client, case):
    existing = client.state['cases'].setdefault(case['caseId'], {})
    cid = 'shipper-app-' + uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId']).hex
    status, prior = client.call('GET', '/api/v1/shipper/requests/by-client-request/' + cid, actor=SHIPPER)
    if status == 200 and get(prior, '의뢰Id'):
        existing['requestId'] = get(prior, '의뢰Id'); client.save(); return existing
    need(status == 404, 'CreateLookupUncertain')
    consent = str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId'] + ':privacy'))
    client.once(case['caseId'] + ':consent', 'POST', '/api/v1/common/application-privacy-consents', {
        '증적Id': consent, '업무Code': 'transport-proxy', '출처Code': 'community-map', '동의문버전': 'application-privacy-consent-draft-2026-08-04',
        '수집이용동의': True, '연령요건확인': True}, SHIPPER)
    p = case['points']; now = dt.datetime.now(dt.timezone.utc)
    def stop(address, lat, lng, name, phone):
        return {'주소': {'도로명주소': address, '상세주소': '', '위도': lat, '경도': lng}, '연락처': {'이름': name, '전화번호': phone},
                '시간창': {'시작일시': utc(now - dt.timedelta(minutes=5)), '종료일시': utc(now + dt.timedelta(hours=3))}}
    marker = 'R114_AI_LEGAL_DONG/pickup=' + case['pickupRegion'] + ';dropoff=' + case['dropoffRegion']
    body = {'신청개인정보동의증적Id': consent, '신청출처Code': 'community-map', '운송방식': '퀵', '차량종류': '1톤 카고',
            '결제수단': '별도정산', '결제예정금액': case['fareKrw'], '정산조건': {'결제수단': 4, '정산시점': 2, '증빙방식': 0, '수납주체': 0},
            '화물': {'화물종류': 'R114 합성 소형 상자', '설명': marker, '수량': 1, '길이Mm': 200, '폭Mm': 200, '높이Mm': 100,
                   '중량Kg': case['weightKg'], '부피Cbm': 0.004, '온도조건': '상온'},
            '픽업': stop(p[0], p[1], p[2], '합성 발송인', '010-0000-0000'), '하차': stop(p[3], p[4], p[5], '합성 수령인', '010-0000-0002'),
            '요금옵션': {'화주직접운임제안': True, '기사지급예정운임': case['fareKrw'], '플랫폼수수료': 0, '요청사항': marker, '서비스레벨': '일반'}, '클라이언트요청Id': cid}
    created = client.once(case['caseId'] + ':create', 'POST', '/api/v1/shipper/requests', body, SHIPPER)
    rid = get(created, '의뢰Id'); need(rid, 'CreatedRequestIdMissing'); existing.update(requestId=rid, clientRequestId=cid); client.save()
    return existing

def locate(client, case):
    p = case['points']
    for index, actor in enumerate(DRIVERS):
        status, _ = client.call('POST', '/api/v1/driver/work/location', {'AppKey': 'CargoYongdalDriverApp', '위도': p[1] + index * .00015,
                                 '경도': p[2] + index * .00015, '정확도_m': 5, '기록시각': utc()}, actor)
        need(status == 200, 'SyntheticLocationRejected')

def recommend(client, case):
    item = client.state['cases'][case['caseId']]; rid = item['requestId']; locate(client, case)
    client.once(case['caseId'] + ':approve', 'POST', '/api/v1/shipper/requests/' + rid + '/settlement/postpay/approve',
                {'승인메모': 'R114 합성 검증 후불 승인 · 실제 입금 아님'}, SHIPPER)
    payload = {'requestId': rid, 'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId'] + ':ai'))}
    result = client.once(case['caseId'] + ':ai', 'POST', '/verification/r114/ai-recommend', payload, control=True)
    proof = client.proof(case['caseId']); item['latestProof'] = proof; client.save()
    return result

def upload(client, actor, transport_id, pickup, key):
    photo = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jmT8AAAAASUVORK5CYII=')
    boundary = 'R114Multipart' + uuid.uuid5(uuid.NAMESPACE_URL, key).hex
    fields = {'CommandName': 'TransportPickupComplete' if pickup else 'TransportDropoffComplete', 'ReferenceId': str(transport_id)}
    chunks = [f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode() for name, value in fields.items()]
    chunks += [f'--{boundary}\r\nContent-Disposition: form-data; name="File"; filename="synthetic-r114.png"\r\nContent-Type: image/png\r\n\r\n'.encode(), photo, f'\r\n--{boundary}--\r\n'.encode()]
    return client.once(key, 'POST', '/api/v1/files/upload', None, actor, binary=(b''.join(chunks), 'multipart/form-data; boundary=' + boundary))

def probe_input(client, case, stage):
    item = client.state['cases'][case['caseId']]
    owner = item.get('driverId')
    if owner not in DRIVERS:
        status, proof = client.call('GET', '/verification/r114/proof?requestId=' + item['requestId'], control=True)
        need(status == 200, 'ProbeOwnerLookupFailed')
        owner = get(get(proof, 'transport'), 'selectedDriverId')
    need(owner in DRIVERS and stage.replace('-', '').isalnum(), 'ProbeActorOrStageInvalid')
    path = PRIVATE / (case['caseId'] + '-probe-' + stage + '.json')
    body = {'baseAddress': BASE, 'accessToken': client.login(owner), 'requestId': item['requestId'],
            'driverId': owner, 'transportId': item.get('transportId'), 'stage': stage,
            'outputRoot': str(OUT / 'client' / case['caseId'] / stage)}
    write(path, body)
    return {'inputFile': str(path), 'outputRoot': body['outputRoot'], 'credentialsInPrivateFileOnly': True}

def guards(client, case, stage):
    item = client.state['cases'][case['caseId']]; rid = item['requestId']
    before = client.proof(case['caseId']); checks = []
    def rejected(name, method, path, body=None, actor=None, control=False, statuses=(400, 403, 409)):
        status, result = client.call(method, path, body, actor, control)
        need(status in statuses, 'NegativeGuardUnexpectedResult_' + name + '_' + str(status))
        checks.append({'name': name, 'status': status, 'errorCode': get(result, 'errorCode') or get(result, 'code'), 'passed': True})
    if stage == 'recommended':
        transport = get(before, 'transport'); owner = get(transport, 'selectedDriverId'); need(owner in DRIVERS, 'ActiveRecommendationRequired')
        other = next(x for x in DRIVERS if x != owner); round_id = get(transport, 'round')
        rejected('other-driver-cannot-accept', 'POST', '/api/v1/driver/dispatch-actions/' + rid + '/accept', {'expectedRecommendationRound': round_id}, other)
        rejected('stale-round-cannot-accept', 'POST', '/api/v1/driver/dispatch-actions/' + rid + '/accept', {'expectedRecommendationRound': round_id + 99}, owner)
        rejected('control-requires-private-key', 'GET', '/verification/r114/proof?requestId=' + rid, statuses=(401,))
        payload = {'requestId': rid, 'requestIdempotencyKey': str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId'] + ':ai'))}
        status, replay = client.call('POST', '/verification/r114/ai-recommend', payload, control=True)
        need(status == 200 and get(replay, 'replay') is True, 'SameAiCommandDidNotReplay')
        need(get(get(get(replay, 'result'), 'model'), 'runId') == get(get(get(before, 'aiAttempt'), 'model'), 'runId'), 'ReplayRepeatedInference')
        checks.append({'name': 'same-ai-command-reads-existing-result', 'passed': True})
        payload['requestIdempotencyKey'] = str(uuid.uuid5(uuid.NAMESPACE_URL, client.run + case['caseId'] + ':conflict'))
        rejected('new-key-cannot-repeat-request-inference', 'POST', '/verification/r114/ai-recommend', payload, control=True)
    elif stage in ('pickup-arrived', 'dropoff-arrived'):
        owner = item['driverId']; tid = item['transportId']
        action = 'pickup-complete' if stage == 'pickup-arrived' else 'complete'
        rejected('completion-requires-uploaded-photo-' + stage, 'POST', f'/api/v1/driver/transports/{tid}/{action}', {}, owner)
    else: raise RuntimeError('GuardStageInvalid')
    after = client.proof(case['caseId'])
    need(get(before, 'transport') == get(after, 'transport'), 'NegativeTestChangedTransport')
    need(get(before, 'notificationOutbox') == get(after, 'notificationOutbox'), 'ReplayDuplicatedNotificationOutbox')
    result = {'caseId': case['caseId'], 'stage': stage, 'checks': checks, 'stateUnchanged': True, 'physicalDeviceVerified': False}
    write(OUT / 'api' / (case['caseId'] + '-guards-' + stage + '.json'), result)
    return result

def final_review(client, case):
    item = client.state['cases'][case['caseId']]; rid = item['requestId']; owner = item['driverId']; tid = item['transportId']
    status, shipper = client.call('GET', '/api/v1/shipper/requests/' + rid, actor=SHIPPER)
    need(status == 200 and get(shipper, '의뢰Id') == rid and get(shipper, '운송상태') == '인수완료', 'ShipperCompletionProjectionMismatch')
    status, workspace = client.call('GET', '/api/v1/driver/transports/workspace', actor=owner)
    need(status == 200 and not get(workspace, '활성운송목록'), 'CompletedTransportStillActive')
    status, current = client.call('GET', '/api/v1/driver/transports/current', actor=owner)
    need(status == 404, 'UnexpectedActiveTransport')
    other = next(x for x in DRIVERS if x != owner)
    status, _ = client.call('GET', f'/api/v1/driver/transports/{tid}', actor=other)
    need(status in (403, 404), 'OtherDriverCanReadTransport')
    proofs = [client.proof(cid) for cid in sorted(client.state['cases'])]
    result = {'synthetic': True, 'source': 'actual_product_http_apis', 'cases': proofs,
              'shipper': {'requestId': rid, 'transportState': get(shipper, '운송상태'), 'requestHeaderState': get(shipper, '의뢰상태'),
                          'settlementState': get(shipper, '정산상태'), 'confirmedDriverId': get(shipper, '확정기사Id')},
              'driverWorkspace': {'activeTransportCount': len(get(workspace, '활성운송목록', [])), 'nextAction': get(workspace, '다음행동')},
              'otherDriverDetailDenied': True, 'weightsTrained': False, 'physicalDeviceVerified': False,
              'notificationDeliveryVerified': False, 'actualCameraCaptureVerified': False, 'actualPaymentVerified': False}
    write(OUT / 'final-review.json', result)
    return {'cases': len(proofs), 'aiAppliedCount': sum(get(p, 'aiRecommendationVerified', False) for p in proofs),
            'shipper': result['shipper'], 'driverWorkspace': result['driverWorkspace'], 'otherDriverDetailDenied': True}

def complete(client, case, until='completed'):
    item = client.state['cases'][case['caseId']]; rid = item['requestId']
    if item.get('completedViaLocalApi'):
        need(item.get('driverId') in DRIVERS and item.get('transportId'), 'CompletedCaseBindingMissing')
        status, canonical = client.call('GET', f"/api/v1/driver/transports/{item['transportId']}", actor=item['driverId'])
        need(status == 200 and get(canonical, '운송번호') == rid and get(canonical, '상태') == '인수완료', 'CompletedCanonicalMismatch')
        return {'caseId': case['caseId'], 'state': '인수완료', 'alreadyCompleted': True, 'mutationRepeated': False, 'physicalDeviceVerified': False}
    owner = None; offer = None
    for actor in ([item['driverId']] if item.get('driverId') in DRIVERS else DRIVERS):
        status, offers = client.call('GET', '/api/v1/driver/recommendations', actor=actor); need(status == 200, 'RecommendationReadFailed')
        for row in offers if isinstance(offers, list) else []:
            if get(row, '의뢰Id') == rid: owner, offer = actor, row
    if owner is None:
        owner = item.get('driverId')
        need(owner in DRIVERS, 'NoActualOfferToAccept')
    item['driverId'] = owner; client.save()
    if offer:
        client.once(case['caseId'] + ':accept', 'POST', '/api/v1/driver/dispatch-actions/' + rid + '/accept', {
            'expectedRecommendationRound': get(offer, '추천라운드'),
            'acknowledgedWarningCodes': ['FreightVehicleAdvisory'] if get(offer, '차량경고', []) else []}, owner)
    status, current = client.call('GET', '/api/v1/driver/transports/current', actor=owner); need(status == 200, 'CurrentTransportMissing')
    current_rows = current if isinstance(current, list) else [current]
    match = next((row for row in current_rows if get(row, '운송번호') == rid), None)
    need(match and get(match, '기사_운송자') == owner, 'CurrentTransportOwnershipMismatch')
    tid = get(match, 'id'); need(tid, 'TransportIdMissing'); item['transportId'] = tid; client.save()
    def stage(name):
        item['lastApiStage'] = name; client.save()
        status, detail = client.call('GET', f'/api/v1/driver/transports/{tid}', actor=owner)
        need(status == 200 and get(detail, '운송번호') == rid, 'StageTransportMismatch')
        return {'caseId': case['caseId'], 'requestId': rid, 'driverId': owner, 'transportId': tid,
                'stage': name, 'state': get(detail, '상태'), 'availableActions': get(detail, '가능한행동'),
                'physicalCameraUsed': False, 'physicalDeviceVerified': False}
    if until == 'accepted': return stage('accepted')
    client.once(case['caseId'] + ':arrive-pickup', 'POST', f'/api/v1/driver/transports/{tid}/arrive-pickup', {}, owner)
    if until == 'pickup-arrived': return stage('pickup-arrived')
    pickup = upload(client, owner, tid, True, case['caseId'] + ':pickup-photo')
    client.once(case['caseId'] + ':pickup', 'POST', f'/api/v1/driver/transports/{tid}/pickup-complete', {
        '상차사진ObjectName': get(pickup, 'objectName'), '상차사진Url': get(pickup, 'url')}, owner)
    if until == 'picked-up': return stage('picked-up')
    client.once(case['caseId'] + ':arrive-dropoff', 'POST', f'/api/v1/driver/transports/{tid}/arrive-dropoff', {}, owner)
    if until == 'dropoff-arrived': return stage('dropoff-arrived')
    dropoff = upload(client, owner, tid, False, case['caseId'] + ':dropoff-photo')
    client.once(case['caseId'] + ':complete', 'POST', f'/api/v1/driver/transports/{tid}/complete', {
        '하차사진ObjectName': get(dropoff, 'objectName'), '하차사진Url': get(dropoff, 'url')}, owner)
    status, final = client.call('GET', f'/api/v1/driver/transports/{tid}', actor=owner)
    need(status == 200 and get(final, '상태') == '인수완료', 'FinalTransportNotComplete')
    item['completedViaLocalApi'] = True; item['latestProof'] = client.proof(case['caseId']); client.save()
    return {'caseId': case['caseId'], 'requestId': rid, 'driverId': owner, 'transportId': tid, 'state': get(final, '상태'),
            'syntheticPhotoUploads': 2, 'physicalCameraUsed': False, 'physicalDeviceVerified': False}

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('action', choices=['fixtures', 'setup', 'create', 'locate', 'recommend', 'complete', 'proof', 'probe-input', 'guards', 'final'])
    parser.add_argument('--case', type=int, choices=[1, 2, 3], default=1)
    parser.add_argument('--until', choices=['accepted', 'pickup-arrived', 'picked-up', 'dropoff-arrived', 'completed'], default='completed')
    parser.add_argument('--stage', default='recommended')
    args = parser.parse_args()
    (OUT / 'api').mkdir(exist_ok=True); client = Client(); cases = fixtures(client); case = cases[args.case - 1]
    result = {'fixtures': lambda: cases, 'setup': lambda: setup(client), 'create': lambda: create(client, case),
              'locate': lambda: locate(client, case), 'recommend': lambda: recommend(client, case),
              'complete': lambda: complete(client, case, args.until), 'proof': lambda: client.proof(case['caseId']),
              'probe-input': lambda: probe_input(client, case, args.stage), 'guards': lambda: guards(client, case, args.stage),
              'final': lambda: final_review(client, case)}[args.action]()
    print(json.dumps(client.safe({'action': args.action, 'result': result, 'physicalDeviceVerified': False}), ensure_ascii=False))

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(json.dumps({'success': False, 'errorCode': str(error) if type(error) is RuntimeError else type(error).__name__,
                          'physicalDeviceVerified': False}, ensure_ascii=False)); raise SystemExit(1)
