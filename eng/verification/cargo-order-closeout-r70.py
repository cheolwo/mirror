"""기존 r114 접수/증빙 도구를 재사용하는 r70 한 건 마감. 기기·운영·실지급 증거가 아니다."""
from __future__ import annotations
import argparse
import datetime as dt
import hashlib
import importlib.util
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/local/cargo-order-closeout-r70'
PRIVATE = OUT / '.private'
SOURCE = ROOT / 'artifacts/local/cargo-ai-device-r114'
spec = importlib.util.spec_from_file_location('r114', Path(__file__).with_name('cargo-ai-device-r114.py'))
r114 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r114)
r114.OUT, r114.PRIVATE = OUT, PRIVATE


def prepare(case_index=1):
    PRIVATE.mkdir(parents=True, exist_ok=True)
    (OUT / 'api').mkdir(exist_ok=True)
    # 기존 실행 자료는 바꾸지 않고 현재 호스트 경계와 시험 인증 자료만 새 비공개 판본에 참조한다.
    for relative in ['host/ready.json', 'host/.private/accounts.json']:
        source, target = SOURCE / relative, OUT / relative
        r114.need(source.is_file(), 'CurrentR114HostManifestMissing')
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != source.read_bytes():
            before, after = r114.read(target), r114.read(source)
            r114.need(relative == 'host/ready.json' and all(before.get(key) == after.get(key) for key in
                ['database', 'runStableId', 'baseUrl', 'executionMode', 'synthetic']) and
                after.get('executionMode') == 'Simulation' and after.get('synthetic') is True,
                'HostManifestChangedDuringR70')
            archive = target.with_name('ready-' + hashlib.sha256(target.read_bytes()).hexdigest()[:12] + '.json')
            r114.need(not archive.exists() or archive.read_bytes() == target.read_bytes(), 'HostArchiveConflict')
            if not archive.exists():
                shutil.copyfile(target, archive)
            shutil.copyfile(source, target)
        else:
            shutil.copyfile(source, target)
    token_source, token_target = SOURCE / '.private/api-token-cache.json', PRIVATE / 'api-token-cache.json'
    if token_source.exists() and not token_target.exists():
        shutil.copyfile(token_source, token_target)
    data = (ROOT / 'eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json').read_bytes()
    r114.need(hashlib.sha256(data).hexdigest() == r114.REGIONS_SHA, 'RegionSourceChanged')
    codes = [x['legalDongCode'] for x in json.loads(data)['regions']]
    case = {'caseId': f'r70-{case_index:02}', 'pickupRegion': codes[0], 'dropoffRegion': codes[1],
            'weightKg': 2, 'fareKrw': 6200,
            'points': ['격리 검증 상차 창고', 37.588, 127.085, '합성 검증 하차지 도로명 주소', 37.590, 127.087]}
    path = OUT / ('fixtures.json' if case_index == 1 else f'fixtures-r70-{case_index:02}.json')
    value = {'schema': 'cargo-order-closeout-r70.fixture.v1', 'cases': [case],
             'sourceRegionSha256': r114.REGIONS_SHA, 'allSynthetic': True,
             'legalDongTagsAreScenarioLabelsOnly': True, 'actualCoordinatesOrEtaVerified': False}
    if path.exists():
        r114.need(r114.read(path) == value, 'PriorR70FixturePreserved')
    else:
        r114.write(path, value)
    return case


def probe_input(client, case, stage, mode):
    item = client.state['cases'][case['caseId']]
    proof = client.proof(case['caseId'])
    owner = item.get('driverId') or r114.get(r114.get(proof, 'transport'), 'selectedDriverId')
    r114.need(owner in r114.DRIVERS, 'SelectedSyntheticDriverMissing')
    item['driverId'] = owner
    client.save()
    path = PRIVATE / f'{case["caseId"]}-{mode}-{stage}.json'
    body = {'baseAddress': r114.BASE, 'accessToken': client.login(owner), 'requestId': item['requestId'],
            'driverId': owner, 'transportId': item.get('transportId'), 'stage': stage, 'mode': mode,
            'outputRoot': str(OUT / 'client' / case['caseId'] / stage)}
    r114.need(not path.exists(), 'ProbeInputAlreadyPrepared')
    r114.write(path, body)
    return {'inputFile': str(path), 'outputRoot': body['outputRoot'], 'mode': mode}


def sync_acceptance(client, case):
    item = client.state['cases'][case['caseId']]
    result = r114.read(OUT / 'client' / case['caseId'] / 'accepted/result.json')
    r114.need(result.get('passed') and result.get('saved') and result.get('requestId') == item['requestId'], 'SharedViewModelAcceptanceNotConfirmed')
    tid = int(result['transportId'])
    status, canonical = client.call('GET', f'/api/v1/driver/transports/{tid}', actor=item['driverId'])
    r114.need(status == 200 and r114.get(canonical, '운송번호') == item['requestId'], 'AcceptedTransportBindingMismatch')
    item.update(transportId=tid, acceptedThroughSharedViewModel=True)
    client.save()
    return {'transportId': tid, 'canonicalRequestMatches': True, 'acceptedThroughSharedViewModel': True}


def roles_input(client, case, stage):
    item = client.state['cases'][case['caseId']]
    r114.need(stage.replace('-', '').isalnum(), 'RoleStageInvalid')
    path = PRIVATE / f'{case["caseId"]}-roles-{stage}.json'
    r114.need(not path.exists(), 'RoleInputAlreadyPrepared')
    admin = 'cargo-journey-admin'
    r114.need(admin in client.accounts, 'ExistingAdminAccountMissing')
    body = {'baseAddress': r114.BASE, 'requestId': item['requestId'],
            'shipperAccessToken': client.login(r114.SHIPPER), 'adminAccessToken': client.login(admin),
            'outputRoot': str(OUT / 'roles' / stage), 'stage': stage}
    r114.write(path, body)
    return {'inputFile': str(path), 'outputRoot': body['outputRoot']}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['prepare', 'setup', 'create', 'recommend', 'probe-input', 'roles-input', 'sync-acceptance', 'complete', 'proof', 'guards', 'final'])
    parser.add_argument('--stage', default='recommended')
    parser.add_argument('--case', type=int, choices=[1, 2, 3], default=1)
    parser.add_argument('--mode', choices=['inspect', 'accept'], default='inspect')
    parser.add_argument('--until', choices=['accepted', 'pickup-arrived', 'picked-up', 'dropoff-arrived', 'completed'], default='completed')
    args = parser.parse_args()
    case = prepare(args.case)
    client = r114.Client()
    action = {'prepare': lambda: {'fixture': case, 'localSimulationOnly': True},
              'setup': lambda: r114.setup(client), 'create': lambda: r114.create(client, case),
              'recommend': lambda: r114.recommend(client, case),
              'probe-input': lambda: probe_input(client, case, args.stage, args.mode),
              'roles-input': lambda: roles_input(client, case, args.stage),
              'sync-acceptance': lambda: sync_acceptance(client, case),
              'complete': lambda: r114.complete(client, case, args.until),
              'proof': lambda: client.proof(case['caseId']),
              'guards': lambda: r114.guards(client, case, args.stage),
              'final': lambda: r114.final_review(client, case)}[args.action]
    result = action()
    filename = args.action + '-' + args.stage + '-' + dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '.json'
    r114.write(OUT / 'api' / filename, client.safe(result))
    print(json.dumps({'action': args.action, 'stage': args.stage, 'saved': str(OUT / 'api' / filename),
                      'physicalDeviceVerified': False}, ensure_ascii=False))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(json.dumps({'success': False, 'errorCode': str(error) if type(error) is RuntimeError else type(error).__name__}, ensure_ascii=False))
        raise SystemExit(1)
