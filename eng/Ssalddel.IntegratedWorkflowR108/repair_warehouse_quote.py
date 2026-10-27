"""명확한 FareChanged 409가 기록된 r108 초안만 정상 화주 API로 다시 견적 확인한다.

불확실한 POST를 반복하지 않는다. 기존 거절은 test journal의 expectedRejections에 보존한다.
"""
import argparse
import json
import pathlib

import workflow as w

parser = argparse.ArgumentParser()
parser.add_argument('--output-root', required=True)
options = parser.parse_args()
flow = w.Workflow(argparse.Namespace(output_root=options.output_root, case='warehouse', scenario='warehouse'))
w.check(flow.state.get('requestId') == 'warehouse-outbound-910004', 'SyntheticWarehouseRequestRequired')
prior = flow.state['attempts'].get('cargo-approve')
rejection = next((x for x in reversed(flow.state['trace']) if x.get('method') == 'POST'
                  and x.get('path') == '/api/v1/shipper/requests/warehouse-outbound-910004/settlement/postpay/approve'), {})
w.check(prior is not None and rejection.get('status') == 409 and rejection.get('code') == 'WarehouseReconsignmentFareChanged',
        'ExplicitKnownFareRejectionRequired_UncertainPostsForbidden')
flow.login('shipper')
detail = flow.call('shipper', 'GET', '/api/v1/shipper/requests/warehouse-outbound-910004')
w.check(w.get(detail, '정산상태') != '후불승인완료', 'ApprovedRequestMustNotBeChanged')
quote = flow.call('shipper', 'POST', '/api/v1/shipper/requests/fare-estimate', {
    '차량종류': '1톤 카고', '예상거리Km': 2.5, '상차위도': 37.588, '상차경도': 127.085,
    '하차위도': 37.590, '하차경도': 127.087})
amount = w.get(quote, '최종운임')
w.check(isinstance(amount, (int, float)) and amount > 0, 'PositiveCanonicalQuoteRequired')
flow.mutation('warehouse-quote-reconfirmed-after-rejection', 'shipper', 'PUT',
              '/api/v1/shipper/requests/warehouse-outbound-910004', {
                  '요금옵션': {'예상거리Km': w.get(quote, '예상거리Km'), '기사지급예정운임': amount, '플랫폼수수료': 0}})
flow.state.setdefault('expectedRejections', []).append({'key': 'cargo-approve', 'attempt': prior,
                                                       'http': rejection, 'canonicalReadBeforeRepair': True})
del flow.state['attempts']['cargo-approve']
flow.state['warehouseRoutePriceInput']['canonicalFareKrw'] = amount
flow.state['warehouseRoutePriceInput']['canonicalSyntheticDistanceKm'] = w.get(quote, '예상거리Km')
flow.checked('WarehouseFareChangedGuardPreservedDraftAndNormalQuoteCorrection')
w.save(flow.path, flow.state)
print(json.dumps({'status': 'KnownRejectedDraftQuoteReconfirmed', 'directSqlChanges': False,
                  'previousRejectionPreserved': True, 'noApprovalReplayedHere': True}))
