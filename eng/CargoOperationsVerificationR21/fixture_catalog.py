"""순수 합성 시험자료. 접수·서버 설정·파일 저장·HTTP를 실행하지 않는다."""
from __future__ import annotations

from copy import deepcopy
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import uuid

SCHEMA = 'cargo-operations-r21.fixtures.v1'
REGIONS_SHA = '737d57de3bc595aaafaba81b61044f61d76902c0bf3d2391899ac471837a973b'
STAGES = ('accepted', 'pickup-arrived', 'picked-up', 'dropoff-arrived', 'completed')


def fixtures():
    """값은 현실 운임/차량 승인 근거가 아닌 반복 가능한 정상 입력이다."""
    common = {'quantity': 1, 'rotateOnFloor': False, 'stackable': False,
              'pickupHandlingMinutes': 5, 'dropoffHandlingMinutes': 5}
    return [
        {**common, 'caseId': 'r21-box-direct', 'kind': 'normal-box', 'directOffer': True,
         'pickupRegion': '1126010100', 'dropoffRegion': '1121510100',
         'lengthMm': 200, 'widthMm': 200, 'heightMm': 100, 'weightKg': 2,
         'volumeCbm': '0.004', 'palletCount': 0,
         'customerChargeKrw': 6200, 'agreedDriverPayoutKrw': 6200, 'platformFeeKrw': 0,
         'points': ['격리 검증 상차 창고', 37.588, 127.085, '합성 검증 하차지 도로명 주소', 37.590, 127.087]},
        {**common, 'caseId': 'r21-box-separated-fare', 'kind': 'normal-separated-fare', 'directOffer': False,
         'pickupRegion': '1126010100', 'dropoffRegion': '1121510100',
         'lengthMm': 400, 'widthMm': 300, 'heightMm': 200, 'weightKg': 20,
         'volumeCbm': '0.024', 'palletCount': 0,
         'customerChargeKrw': 40000, 'agreedDriverPayoutKrw': 35000, 'platformFeeKrw': 5000,
         'points': ['픽업 장소 4', 37.583, 127.086, '전달 장소 4', 37.585, 127.089]},
        {**common, 'caseId': 'r21-pallet', 'kind': 'normal-pallet', 'directOffer': False,
         'pickupRegion': '1126010100', 'dropoffRegion': '1121510100',
         'lengthMm': 1100, 'widthMm': 1100, 'heightMm': 200, 'weightKg': 150,
         'volumeCbm': '0.242', 'palletCount': 1,
         'customerChargeKrw': 50000, 'agreedDriverPayoutKrw': 45000, 'platformFeeKrw': 5000,
         'points': ['픽업 장소 5', 37.588, 127.092, '전달 장소 5', 37.590, 127.095]}
    ]


def validate_case(case):
    for key in ('lengthMm', 'widthMm', 'heightMm', 'quantity', 'weightKg',
                'customerChargeKrw', 'agreedDriverPayoutKrw'):
        if isinstance(case.get(key), bool) or Decimal(str(case.get(key, 0))) <= 0:
            raise ValueError('FixturePositiveFieldRequired_' + key)
    if type(case.get('palletCount')) is not int or case['palletCount'] < 0:
        raise ValueError('FixtureExplicitPalletCountRequired')
    if type(case.get('rotateOnFloor')) is not bool or type(case.get('stackable')) is not bool:
        raise ValueError('FixtureExplicitLoadingFlagsRequired')
    dimensions = Decimal(case['lengthMm']) * Decimal(case['widthMm']) * Decimal(case['heightMm']) * case['quantity'] / Decimal(10**9)
    if Decimal(case['volumeCbm']) != dimensions:
        raise ValueError('FixtureVolumeDoesNotMatchDimensions')
    for key in ('pickupHandlingMinutes', 'dropoffHandlingMinutes', 'platformFeeKrw'):
        if key not in case or case[key] is None or isinstance(case[key], bool) or Decimal(str(case[key])) < 0:
            raise ValueError('FixtureExplicitNonNegativeFieldRequired_' + key)
    if case['customerChargeKrw'] != case['agreedDriverPayoutKrw'] + case['platformFeeKrw']:
        raise ValueError('FixtureFareAllocationMismatch')
    if case['directOffer'] and (case['platformFeeKrw'] != 0 or case['customerChargeKrw'] != case['agreedDriverPayoutKrw']):
        raise ValueError('FixtureDirectOfferMustPreserveProductPolicy')
    if len(case['points']) != 6:
        raise ValueError('FixtureSyntheticPointsRequired')
    return case


def request_identity(run_stable_id, case_id):
    if not isinstance(run_stable_id, str) or not run_stable_id.startswith('cargo-operations-r21:'):
        raise ValueError('R21RunIdentityRequired')
    return 'shipper-app-' + uuid.uuid5(uuid.NAMESPACE_URL, run_stable_id + case_id).hex


def build_request(case, run_stable_id, consent_id, now):
    """현재 시각은 호출자가 한 번 정해 저장한다. 미확인 명령의 payload를 다시 만들지 않는다."""
    validate_case(case)
    if not isinstance(now, datetime) or now.utcoffset() != timedelta(0):
        raise ValueError('FixtureUtcClockRequired')
    now = now.astimezone(timezone.utc)
    stamp = lambda t: t.isoformat().replace('+00:00', 'Z')
    marker = 'R114_AI_LEGAL_DONG/pickup=' + case['pickupRegion'] + ';dropoff=' + case['dropoffRegion']
    p = case['points']
    def stop(address, lat, lng, name, phone, handling):
        return {'주소': {'도로명주소': address, '상세주소': '', '위도': lat, '경도': lng},
                '연락처': {'이름': name, '전화번호': phone}, '작업시간분': handling,
                '시간창': {'시작일시': stamp(now - timedelta(minutes=5)), '종료일시': stamp(now + timedelta(hours=3))}}
    return {'신청개인정보동의증적Id': consent_id, '신청출처Code': 'community-map',
            '운송방식': '퀵', '차량종류': '1톤 카고', '결제수단': '카드',
            '결제예정금액': case['customerChargeKrw'],
            '정산조건': {'결제수단': 0, '정산시점': 0, '증빙방식': 0, '수납주체': 0},
            '화물': {'화물종류': 'R114 합성 ' + ('팔레트' if case['palletCount'] else '상자'),
                     '설명': marker, '수량': case['quantity'], '길이Mm': case['lengthMm'],
                     '폭Mm': case['widthMm'], '높이Mm': case['heightMm'], '중량Kg': case['weightKg'],
                     '부피Cbm': float(Decimal(case['volumeCbm'])), '팔레트개수': case['palletCount'],
                     '바닥회전가능여부': case['rotateOnFloor'], '적층가능여부': case['stackable'], '온도조건': '상온'},
            '픽업': stop(p[0], p[1], p[2], '합성 발송인', '010-0000-0000', case['pickupHandlingMinutes']),
            '하차': stop(p[3], p[4], p[5], '합성 수령인', '010-0000-0002', case['dropoffHandlingMinutes']),
            '요금옵션': {'화주직접운임제안': case['directOffer'], '기사지급예정운임': case['agreedDriverPayoutKrw'],
                       '플랫폼수수료': case['platformFeeKrw'], '요청사항': marker, '서비스레벨': '일반'},
            '클라이언트요청Id': request_identity(run_stable_id, case['caseId'])}


def catalog():
    return {'schemaVersion': SCHEMA, 'cases': deepcopy(fixtures()), 'synthetic': True,
            'regionSourceSha256': REGIONS_SHA, 'legalDongTagsAreScenarioLabelsOnly': True,
            'coordinatesMappedToActualLegalDong': False, 'realVehicleCapacityVerified': False,
            'realGoodsVerified': False, 'realPaymentVerified': False}
