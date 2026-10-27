"""완료된 합성 회원 배송의 수령 확인을 실제 협업 HTTP 명령으로 분리 검증한다."""
import argparse
import json
import pathlib
import urllib.parse
import uuid

import workflow as w


def run(options):
    fixture = w.read(pathlib.Path(options.output_root) / '.private/cases' / options.fixture_case / 'prepared.json')
    flow = w.Workflow(argparse.Namespace(output_root=options.output_root, case=options.case, scenario='member-receipt'))
    w.NAMES['receiver'] = 'shipper1'
    w.APPS['receiver'] = 'SsalddelApp'
    flow.login('shipper')
    flow.login('receiver')
    request_id = w.get(fixture, 'requestId')
    collaboration_id = w.get(fixture, 'collaborationId')
    path = '/api/v1/common/neighborhood-collaborations/' + urllib.parse.quote(collaboration_id, safe='')
    member_path = '/api/v1/common/neighborhood-deliveries/member/work/' + urllib.parse.quote(request_id, safe='')
    # The member token is never copied into this public trace or journal.
    flow.tokens['member'] = w.read(pathlib.Path(options.member_input))['accessToken']
    w.APPS['member'] = 'SsalddelApp'
    delivery = flow.call('member', 'GET', member_path)
    w.check(w.get(delivery, 'stateCode') == 'Delivered', 'MemberDeliveryNotCompleted')
    flow.state['requestId'] = request_id
    flow.state['collaborationId'] = collaboration_id
    flow.state.setdefault('beforeReceiptConfirmed', bool(w.get(delivery, 'receiptConfirmedAtUtc')))
    collaboration = flow.call('shipper', 'GET', path)
    if 'propose-completion' not in flow.state['attempts']:
        flow.mutation('propose-completion', 'shipper', 'POST', path + '/commands', {
            'clientRequestId': str(uuid.uuid4()), 'expectedRevision': w.get(collaboration, 'revision'),
            'action': 'propose-completion'})
    collaboration = flow.call('receiver', 'GET', path)
    if 'confirm-completion' not in flow.state['attempts']:
        command = {'clientRequestId': str(uuid.uuid4()), 'expectedRevision': w.get(collaboration, 'revision'),
                   'action': 'confirm-completion'}
        flow.state['confirmationCommand'] = command
        w.save(flow.path, flow.state)
        flow.mutation('confirm-completion', 'receiver', 'POST', path + '/commands', command, discard=True)
    confirmed = flow.call('receiver', 'GET', path)
    flow.checked('ReceiptLostSuccessResponseRecoveredCanonical', w.get(confirmed, 'statusCode') == 'completed')
    # Intentional idempotency test of the identical confirmed command, not uncertain automatic retry.
    replay = flow.call('receiver', 'POST', path + '/commands', flow.state['confirmationCommand'])
    flow.checked('ExplicitConfirmedReceiptReplayNoRevisionChange',
                 w.get(replay, 'idempotentReplay') is True and w.get(replay, 'revision') == w.get(confirmed, 'revision'))
    delivery = flow.call('member', 'GET', member_path)
    flow.checked('DeliveryAndRecipientReceiptSeparate',
                 w.get(delivery, 'stateCode') == 'Delivered' and bool(w.get(delivery, 'receiptConfirmedAtUtc')))
    flow.state['status'] = 'Passed'
    flow.state['boundaries'] = {'realFinancialEffects': False, 'actualRouteVerified': False,
                                'providerGuardEmulated': True, 'deviceUiVerified': False,
                                'syntheticFixture': True, 'independentDatabaseEvidenceSeparate': True}
    w.save(flow.path, flow.state)
    print(json.dumps({'status': 'Passed', 'result': str(flow.path), 'checks': len(flow.state['checks'])}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-root', required=True)
    parser.add_argument('--fixture-case', required=True)
    parser.add_argument('--member-input', required=True)
    parser.add_argument('--case', default='member-recipient-receipt')
    options = parser.parse_args()
    w.check(bool(w.re.fullmatch(r'[a-z0-9][a-z0-9-]{0,47}', options.fixture_case)), 'SafeFixtureCaseRequired')
    input_path = pathlib.Path(options.member_input).resolve()
    allowed = w.REPO / 'artifacts/local/local-operations-r108/mobile/.private'
    w.check(input_path.is_relative_to(allowed), 'PrivateMobileInputBoundaryRequired')
    run(options)
