"""중랑구부터 넓히는 자료의 지문·범위·소속 수와 게임 적용 관문을 분리해 검사한다."""
from __future__ import annotations
import argparse
from collections import Counter
import csv
from datetime import datetime, timezone
import gzip
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class InputRejected(ValueError):
    pass


def read_json(path):
    return json.loads(path.read_text('utf-8-sig'))


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def verify_reference(root, reference, cache):
    path = Path(reference['path'])
    path = path if path.is_absolute() else root / path
    if not path.is_file():
        raise InputRejected('MissingReferencedFile:' + str(path))
    if path not in cache:
        cache[path] = digest(path)
    if cache[path].lower() != reference['sha256'].lower():
        raise InputRejected('ReferencedShaMismatch:' + str(path))
    expected_bytes = reference.get('bytes', reference.get('lengthBytes'))
    if expected_bytes is not None and path.stat().st_size != expected_bytes:
        raise InputRejected('ReferencedSizeMismatch:' + str(path))
    return path


def 고정JSON읽기(path, cache):
    if path not in cache:
        raise InputRejected('UnpinnedInputMetadata:' + str(path))
    return read_json(path)


def 지역메타데이터대조(code, index, bundle, supplementary):
    if (bundle.get('code') != code or supplementary.get('administrativeDongCode') != code
            or bundle.get('name') != index['name'] or supplementary.get('name') != index['name']
            or bundle.get('district') != index['district'] or supplementary.get('parent') != index['district']):
        raise InputRejected('DongMetadataIdentityMismatch:' + code)
    layers = supplementary.get('supplements', {})
    if not {'officialStreetFurniture', 'officialWalkingNetwork'} <= set(layers):
        raise InputRejected('MissingExplicitSupplementState:' + code)


def 압축피처읽기(path, expected, label):
    with gzip.open(path, 'rt', encoding='utf-8') as stream:
        features = json.load(stream)['features']
    if len(features) != expected:
        raise InputRejected('FeatureCountMismatch:' + label)
    return features


def 선택지역(stage_map, expected_counts, stage):
    if stage not in range(len(expected_counts)):
        raise InputRejected('UnknownCollectionStage')
    actual = Counter(stage_map.values())
    if actual != dict(enumerate(expected_counts)):
        raise InputRejected('StagePartitionCountsMismatch')
    return sorted(code for code, value in stage_map.items() if value <= stage)


def 소속수량(feature_id_lists):
    counts = Counter(identity for items in feature_id_lists for identity in items)
    return {'membershipRows': sum(counts.values()), 'uniqueSourceFeatures': len(counts),
            'crossDongSourceFeatures': sum(value > 1 for value in counts.values())}


def 수요관문(codes, observations):
    present = [code for code in codes if code in observations and observations[code]['adminObservationRows'] > 0]
    missing_game = [code for code in codes if code not in observations or not observations[code]['currentGameDemandPresent']]
    return {'observationDongCount': len(present), 'requestedDongCount': len(codes),
            'observationsPreparedForEveryRequestedDong': len(present) == len(codes),
            'missingGameDemandCodes': missing_game, 'runtimeUnlockAuthorized': False,
            'observationToOrderModelApproved': False,
            'meaning': 'Observed population is not a generated game order-demand pack'}


def run(root, config, stage, output):
    if not output.resolve().is_relative_to((root / 'artifacts/local').resolve()):
        raise InputRejected('OutputMustStayWithinLocalArtifacts')
    if output.exists():
        raise InputRejected('ExistingOutputNotOverwritten')
    cache = {}
    for reference in config['inputReferences']:
        verify_reference(root, reference, cache)
    stage_data = read_json(root / config['stageMap'])
    mapping = stage_data['stage']
    codes = 선택지역(mapping, config['expectedIncrementalDongCounts'], stage)
    with (root / config['baseBundleIndex']).open(encoding='utf-8-sig', newline='') as stream:
        bundles = {row['code']: row for row in csv.DictReader(stream)}
    if set(mapping) != set(bundles):
        raise InputRejected('StageAndBundleCodeSetsMismatch')
    for number, expected in enumerate(config['expectedIncrementalBuildingMembershipCounts']):
        if sum(int(bundles[code]['buildings']) for code, value in mapping.items() if value == number) != expected:
            raise InputRejected('StageBuildingMembershipCountsMismatch')
    demand = {row['code']: row for row in read_json(root / config['demandCoverage'])['dongs']}
    historic_trees = {row['code']: row for row in read_json(root / config['historicalTreeIndex'])['dongs']}
    profiles, all_building_ids, all_walk_ids = [], [], []
    for code in codes:
        bundle = 고정JSON읽기(root / bundles[code]['bundle'], cache)
        for key, wanted in [('buildingSourceDate', config['buildingSourceDate']),
                            ('boundaryDate', config['boundaryCandidateDate']),
                            ('roadSourceDate', config['roadSourceDate'])]:
            if bundle[key] != wanted:
                raise InputRejected('MixedSourceEdition:' + code + ':' + key)
        if set(bundle['sourceLayers']) != {'buildings', 'boundaries', 'roads', 'water', 'terrain'}:
            raise InputRejected('MissingFiveLayerInput:' + code)
        for layer in bundle['sourceLayers'].values():
            verify_reference(root, layer, cache)
        terrain = bundle['sourceLayers']['terrain']
        if terrain['nativeWindowAlignment'] != 'OriginalPointPixelTransformIntegerWindow' or terrain['verticalReference'] != 'EGM96':
            raise InputRejected('TerrainGridPhaseOrVerticalReferenceMismatch:' + code)
        supplementary_metadata = 고정JSON읽기(root / config['supplementRoot'] / 'dongs' / f'{code}.json', cache)
        지역메타데이터대조(code, bundles[code], bundle, supplementary_metadata)
        with gzip.open(root / bundle['sourceLayers']['buildings']['path'], 'rt', encoding='utf-8') as stream:
            buildings = json.load(stream)['features']
        if (len(buildings) != bundle['sourceLayers']['buildings']['featureCount']
                or len(buildings) != int(bundles[code]['buildings'])
                or len({row['id'] for row in buildings}) != len(buildings)):
            raise InputRejected('BuildingMembershipCountMismatch:' + code)
        all_building_ids.append([row['id'] for row in buildings])
        supplementary = supplementary_metadata['supplements']
        furniture = supplementary.get('officialStreetFurniture', {}).get('sourceEntry', {}).get('layers', [])
        for layer in furniture:
            furniture_path = verify_reference(root / supplementary['officialStreetFurniture']['sourcePackage'], layer, cache)
            압축피처읽기(furniture_path, layer['pointMatched'], 'Lighting:' + code)
        walk_ref = supplementary['officialWalkingNetwork']['sourceEntry']
        walk_profile_path = verify_reference(root, walk_ref, cache)
        walk_profile = read_json(walk_profile_path)
        walk_layers = walk_profile.get('layers', {})
        walk_ids = []
        for kind, layer in walk_layers.items():
            path = verify_reference(root, layer, cache)
            with gzip.open(path, 'rt', encoding='utf-8') as stream:
                features = json.load(stream)['features']
            if kind.upper() in ('LINK', 'LINKS'):
                walk_ids.extend(row['id'] for row in features)
        all_walk_ids.append(walk_ids)
        tree = historic_trees.get(code)
        if tree:
            tree_path = verify_reference(root, tree, cache)
            압축피처읽기(tree_path, tree['sourcePointMemberships'], 'HistoricalTree:' + code)
        profiles.append({'code': code, 'name': bundle['name'], 'district': bundle['district'],
                         'collectionStage': mapping[code], 'buildingMembershipRows': len(buildings),
                         'officialLightingSourcePointRows': sum(row['pointMatched'] for row in furniture),
                         'officialLightingSources': [row['sourceId'] for row in furniture],
                         'historicalTreeCandidateRows': tree['sourcePointMemberships'] if tree else 0,
                         'historicalTreeYear': config['historicalTreeYear'],
                         'currentTreePresenceVerified': False,
                         'observationsAvailable': code in demand and demand[code]['adminObservationRows'] > 0,
                         'gameDemandEntryPresent': demand.get(code, {}).get('currentGameDemandPresent', False),
                         'applicationAuthorized': False})
    report = {'schemaVersion': 'hongdal.diorama-stage-input-audit.v1',
              'checkedAtUtc': datetime.now(timezone.utc).isoformat(), 'inputAuditPassed': True,
              'scope': 'DataEvidenceOnly;NotGameBakeOrUnlockApproval', 'cumulativeCollectionStage': stage,
              'validatedDataLayers': ['FiveLayerBase', 'OfficialHistoricalWalking',
                  'OfficialLighting', 'HistoricalTree2012', 'ObservedDemandCoverage'],
              'excludedDataLayers': ['OSMEightAuxiliaryLayers', 'OSMTreeAndLampPoints',
                  'RestaurantSpatialBinding', 'AddressAndEntrance', 'RuntimeAndMeshBake'],
              'dongCount': len(codes), 'verifiedReferencedFiles': len(cache),
              'buildingCounts': 소속수량(all_building_ids), 'historicalWalkLinkCounts': 소속수량(all_walk_ids),
              'demandGate': 수요관문(codes, demand), 'dongProfiles': profiles,
              'openApplicationGates': ['N1-N5Unanswered', 'LegacyBuildingIdCrosswalkRequiresSeparateJ1Evidence',
                 'RuntimeMissingDemandGuardNotVerified', 'PointContactAdjacencyDefinitionUnapproved',
                 'CrossDongBuildingFragmentsMustNotBeDropped', 'PreviewRenderingNotRuntimeProof',
                 'PerSourceRightsAndCRSRestrictionsRemain'],
              'previewShaMeaning': 'PC fingerprint baseline; source input SHAs separately compared; no external transfer/render execution receipt',
              'runtimeStateObserved': False, 'applicationAuthorized': False,
              'productModified': False, 'distributionApproved': False}
    output.mkdir(parents=True)
    (output / 'stage-readiness.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    with (output / 'dong-readiness.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        fields = ['code', 'name', 'district', 'collectionStage', 'buildingMembershipRows',
                  'officialLightingSourcePointRows', 'historicalTreeCandidateRows', 'historicalTreeYear',
                  'observationsAvailable', 'gameDemandEntryPresent', 'applicationAuthorized']
        writer = csv.DictWriter(stream, fieldnames=fields); writer.writeheader()
        writer.writerows({key: row[key] for key in fields} for row in profiles)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, default=ROOT/'eng/data-collection/diorama-intake-priority.json')
    parser.add_argument('--stage', type=int, default=1)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = run(ROOT, read_json(args.manifest), args.stage, args.output)
    print(json.dumps({key: report[key] for key in ['inputAuditPassed', 'dongCount', 'buildingCounts', 'historicalWalkLinkCounts', 'demandGate', 'applicationAuthorized']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
