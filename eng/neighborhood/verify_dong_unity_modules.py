"""실행용 Resources 또는 행정동별 압축 카탈로그를 원자료·HDIO v2와 대조한다."""
import argparse
import gzip
import hashlib
import json
import struct
import sys
import tempfile
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / 'artifacts/local/admin-dong-reference-review-20261115/bake'
SOURCE = ROOT / 'artifacts/local/admin-dong-diorama-precision-r3'
sys.path.insert(0, str(ROOT / 'artifacts/local/public-data/gis-runtime-r1'))
import numpy as np
from dong_diorama_reference import DEFAULT_PROFILE, load_profile
from dong_surface_quality import check_surface_coverage, mesh_surfaces


def read(path, expected):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected.lower():
        raise ValueError(f'파일 지문 불일치 {path}')
    return data


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def unique(items, key, label):
    result = {}
    for item in items:
        identity = key(item)
        require(identity not in result, f'{label} 중복: {identity}')
        result[identity] = item
    return result


def source_indexes():
    folders = {'detail':'candidate-detail', 'streets':'streets/candidate-20261001',
               'green':'green/candidate-20261001', 'parcels':'parcels/candidate-20261001'}
    indexes = {}
    for key, folder in folders.items():
        doc = json.loads((SOURCE/folder/'manifest.json').read_bytes())
        indexes[key] = unique(doc['modules'], lambda item: item['code'], folder)
    return indexes


def mesh(blob, frame, entry, profile):
    assert blob[:4] == b'HDIO' and struct.unpack_from('<i', blob, 4) == (2,)
    bounds = tuple(entry[k] for k in ('west','south','east','north'))
    assert struct.unpack_from('<6d',blob,8) == (*frame[:2], *bounds)
    assert struct.unpack_from('<2i',blob,56) == (entry['buildings'],entry['roads'])
    offset = 64

    def array(dtype, count):
        nonlocal offset
        result = np.frombuffer(blob, dtype=dtype, count=count, offset=offset)
        offset += result.nbytes
        return result

    results = {}
    west,south,east,north = bounds
    lat,lon,mz,mx = frame
    style = profile['buildings']
    for kind in ('display','shadow'):
        vertices = int(array('<i4',1)[0])
        assert 0 < vertices < 4_000_000
        xyz = array('<f4',vertices*3).reshape(vertices,3)
        assert np.isfinite(xyz).all()
        # 그림자는 광원 방향으로 최대 7.6m 평행 이동한다.
        shadow_offset = style['shadowMaxHeightMetres'] * style['shadowScale']
        slack = shadow_offset * max(abs(style['shadowNorth']),abs(style['shadowEast'])) + .08 if kind == 'shadow' else .08
        assert (west-lon)*mx-slack <= xyz[:,0].min() <= xyz[:,0].max() <= (east-lon)*mx+slack
        assert (south-lat)*mz-slack <= xyz[:,2].min() <= xyz[:,2].max() <= (north-lat)*mz+slack
        if kind == 'display':
            assert vertices == entry['vertices'] and 0 <= xyz[:,1].min() and xyz[:,1].max() <= style['maxDisplayHeightMetres'] + style['parapetMetres'] + .001
            rgba = array('u1',vertices*4).reshape(vertices,4)
            uv = array('<f4',vertices*2).reshape(vertices,2)
            tags = array('<f4',vertices*2).reshape(vertices,2)
            assert np.isfinite(uv).all() and np.isfinite(tags).all() and (rgba[:,3] == 255).all()
            assert np.isin(tags[:,0],(0,1)).all() and -1 <= tags[:,1].min() <= tags[:,1].max() <= 996
            assert np.any(tags[:,0] == 1)
        else:
            assert np.allclose(xyz[:,1],style['shadowYMetres'],rtol=0,atol=.00001)
        indices_count = int(array('<i4',1)[0])
        assert 0 < indices_count < 12_000_000 and indices_count % 3 == 0
        indices = array('<i4',indices_count)
        assert indices.min() >= 0 and indices.max() < vertices
        if kind == 'display':
            assert indices_count == 3*entry['triangles']
            surfaces = mesh_surfaces(xyz, rgba, indices, profile)
            for layer, (_, count, _, _) in surfaces.items():
                results[layer+'Vertices'] = count
        results[kind+'Vertices'] = vertices
        results[kind+'Triangles'] = indices_count // 3
    assert struct.unpack_from('<5i',blob,offset) == (0,0,0,0,0) and offset + 20 == len(blob) == entry['bytes']
    return results, surfaces


def ring_key(coords):
    return (len(coords), tuple(round(v,4) for v in coords[0]))


def check_footprint(geometry, row, frame):
    lat,lon,mz,mx = frame
    polys = geometry['coordinates'] if geometry['type'] == 'MultiPolygon' else [geometry['coordinates']]
    sources = []
    for poly in polys:
        rings = [[((x-lon)*mx,(z-lat)*mz) for x,z in ring[:-1]] for ring in poly]
        sources.append((rings[0],rings[1:]))
    picked = []
    def pairs(values):
        assert len(values)%2 == 0 and len(values) >= 6
        return list(zip(values[::2],values[1::2]))
    for poly in row.get('pickPolygons',[{'xz':row['xz'],'holes':[]}]):
        picked.append((pairs(poly['xz']),[pairs(r['xz']) for r in poly['holes']]))
    assert len(sources) == len(picked)
    for (outer,holes),(px,ph) in zip(sorted(sources,key=lambda p:ring_key(p[0])),sorted(picked,key=lambda p:ring_key(p[0]))):
        assert len(outer) == len(px) and np.allclose(outer,px,rtol=0,atol=.00001)
        assert len(holes) == len(ph)
        for a,b in zip(sorted(holes,key=ring_key),sorted(ph,key=ring_key)):
            assert len(a) == len(b) and np.allclose(a,b,rtol=0,atol=.00001)
    return int(len(picked)>1), sum(len(p[1]) for p in picked)


def load_sources(module, receipt, indexes):
    code = module['code']
    specs = unique(module['layers'], lambda r: r['key'], f'{code} 자료층')
    required = {'boundary','buildings','roads','streets','green','parcels','walk-codebook'}
    missing = required - specs.keys()
    require(not missing, f'{code} 필수 자료층 누락: {sorted(missing)}')
    expected_inputs = {k: {'path':v['path'], 'sha256':v['sha256']} for k,v in specs.items()}
    require(receipt['inputs'] == expected_inputs, f'{code} 굽기 기록과 자료층 목록 불일치')
    layers = {}
    for key, item in specs.items():
        raw = read(ROOT/item['path'],item['sha256'])
        require(len(raw) == item['bytes'], f'{code} {key} 원본 크기 불일치')
        layers[key] = json.loads(gzip.decompress(raw) if item['path'].endswith('.gz') else raw)
    upstream = {key: rows[code] for key, rows in indexes.items()}
    detail = unique(upstream['detail']['layers'], lambda row: row['layer'], f'{code} 원본 절단 대장')
    for key in ('streets','green','parcels','roads','buildings'):
        row = detail[key] if key in detail else upstream[key]
        require(row['sha256'] == specs[key]['sha256'], f'{code} {key} 원본 대장과 지문 불일치')
    boundary = layers['boundary']
    boundary_sha = specs['boundary']['sha256']
    require(boundary['id'] == boundary['properties']['administrativeCode'] == code and
            boundary['properties']['sourceDate'] == module['clippingBoundaryDate'], f'{code} 경계 코드 또는 판본 불일치')
    for key in ('roads','buildings'):
        layer = layers[key]
        require(layer['administrativeCode'] == code and layer['boundaryCandidateSha256'] == boundary_sha and
                layer['coordinateReference'] == 'RFC7946 WGS84 longitude,latitude', f'{code} {key} 경계·좌표계 불일치')
        require(all(r['properties']['administrativeCode'] == code for r in layer['features']), f'{code} {key} 타 동 도형')
        require(len(layer['features']) == detail[key]['featureCount'], f'{code} {key} 원본 절단 건수 불일치')
        unique(layer['features'], lambda r: r['id'], f'{code} {key} 도형 ID')
    streets, green, parcels = (layers[k] for k in ('streets','green','parcels'))
    require(streets['candidateDongCode'] == green['candidateCode'] == parcels['code'] == code and
            streets['boundaryVintage'] == green['boundaryDate'] == parcels['boundaryDate'] == module['clippingBoundaryDate'] and
            streets['candidateBoundarySha256'] == green['candidateSha256'] == boundary_sha and
            parcels['edition'] == module['edition'] and parcels['buildingGeometrySha256'] == specs['buildings']['sha256'],
            f'{code} 거리·녹지·필지 대상 동 또는 의존 판본 불일치')
    require(streets['frame']['crs'] == green['crs'] == 'EPSG:5186' and
            streets['frame']['axes'] == ['east','north'] and streets['frame']['integerUnitMetres'] > 0,
            f'{code} 거리·녹지 좌표계 또는 단위 불일치')
    require(all(len(streets[key]) == streets['counts'][key] for key in ('walkLinks','laneMarkingFragments','walkLandmarks')),
            f'{code} 거리 자료 건수 불일치')
    require(streets['counts'] == upstream['streets']['counts'] and
            len(green['parkPlanning2018']) == upstream['green']['parkPieces'] and
            len(parcels['restaurantParcelCandidates']) == upstream['parcels']['licenseKeys'] and
            len(parcels['buildingAddresses']) == upstream['parcels']['buildingPieces'],
            f'{code} 거리·공원·주소·인허가 원본 집계 불일치')
    unique(streets['walkLinks'], lambda r: r['id'], f'{code} 도보선 ID')
    unique(streets['laneMarkingFragments'], lambda r: (r['sourceRow'],r['part']), f'{code} 차선 조각')
    unique(green['parkPlanning2018'], lambda r: r['sourceRow'], f'{code} 공원 계획면 원본행')
    unique(parcels['restaurantParcelCandidates'], lambda r: r['managementKey'], f'{code} 음식점 인허가 키')
    return layers


def verify_module(entry, rows, frame, cache, src_entry, profile, surfaces, indexes):
    code = entry['code']
    receipt_path = cache/code/'receipt.json'
    receipt = json.loads(receipt_path.read_bytes())
    assert entry == receipt['entry'] and entry['moduleSha256'].lower() == src_entry['sha256']
    module = json.loads(read(SOURCE/src_entry['path'],src_entry['sha256']))
    assert module['code'] == src_entry['code'] == receipt['code'] == code
    assert module['edition'] == 'candidate-2026' and module['clippingBoundaryDate'] == '2026-10-01'
    assert src_entry['name'] == module['name'] and entry['name'] == module['name'].replace('.', '·')
    assert src_entry['district'] == module['district'] == entry['district']
    assert entry['regionId'] == 'region:kr:hjd:' + code
    layers = load_sources(module, receipt, indexes)
    boundary = layers['boundary']['geometry']
    polys = boundary['coordinates'] if boundary['type'] == 'MultiPolygon' else [boundary['coordinates']]
    assert len(polys) == 1 and len(polys[0]) == 1
    assert entry['boundary'] == [v for lon,lat in polys[0][0][:-1] for v in (lat,lon)]
    assert entry['roads'] == len(layers['roads']['features'])
    assert entry['parkPlanFragments'] == len(layers['green']['parkPlanning2018'])
    street = layers['streets']
    assert entry['sourceWalkLinks'] == len(street['walkLinks'])
    assert entry['sourceLaneFragments'] == len(street['laneMarkingFragments'])
    assert entry['surfaceWalkLinks'] == sum(all(s=='횡단보도' for s in r['structure']) for r in street['walkLinks'])
    parcels = layers['parcels']
    permits = defaultdict(set)
    for candidate in parcels['restaurantParcelCandidates']:
        for fid in candidate['candidateGeometryIds']:
            permits[fid].add(candidate['managementKey'])
    assert entry['permitKeys'] == len(set().union(*permits.values()))
    addresses = unique(parcels['buildingAddresses'], lambda r:r['featureId'], f'{code} 지번 건물 참조')
    source = {f['id']:f for f in layers['buildings']['features']}
    assert permits.keys() <= source.keys()
    assert entry['buildingRowsWithParcelLabel'] == sum(bool(r['parcelLabel']) for r in rows)
    assert len(rows) == len(source) == len(addresses) == entry['buildings']
    assert set(source) == set(addresses) == {r['sourceFeatureId'] for r in rows}
    assert {r['region'] for r in rows} == {entry['regionId']}
    counts = Counter()
    for row in rows:
        fid = row['sourceFeatureId']
        feature = source[fid]
        p = feature['properties']
        a = addresses[fid]
        assert row['sourceBuildingRow'] == p['sourceRowOneBased'] and row['parcelPermitCount'] == len(permits[fid])
        expected_label = ''
        if a['parcelFieldsConsistent']:
            expected_label = a['legalNameSource'].split()[-1]+' '+('산 ' if a['landTypeSource']=='산' else '')+a['lotSource']
        assert row['parcelLabel'] == expected_label and row['road'] == '' and row['number'] == ''
        assert row['floors'] == (p['aboveGroundFloorCount'] or 0) and row['area'] == round(p['sourceAreaSquareMeters'],1)
        measured = p['observedHeightMeters']
        style = profile['buildings']
        height, reason = (measured,'height') if measured and measured > 0 else ((row['floors']*style['floorHeightMetres'],'floors') if row['floors'] else (style['fallbackHeightMetres'],'symbolic'))
        threshold = style['compressAboveMetres']
        drawn = height if height <= threshold else min(style['maxDisplayHeightMetres'],threshold+(height-threshold)*style['compressionRatio'])
        assert row['height'] == round(drawn,2) and row['heightSource'] == reason
        counts[reason] += 1
        multi,holes = check_footprint(feature['geometry'],row,frame)
        counts['multi'] += multi
        counts['holes'] += holes
    assert tuple(counts[k] for k in ('multi','holes','height','floors','symbolic')) == tuple(entry[k] for k in ('multiPartFootprints','interiorRings','heightFromSource','heightFromFloors','heightSymbolic'))
    return check_surface_coverage(layers, frame, profile, surfaces)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=DEFAULT)
    parser.add_argument('--cache',type=Path,help='압축 모듈 폴더. 기본은 출력과 함께 둔 modules 또는 같은 판본의 공용 캐시다.')
    parser.add_argument('--report', type=Path, help='검증이 끝나면 쓸 JSON 경로')
    parser.add_argument('--profile', type=Path, default=DEFAULT_PROFILE)
    parser.add_argument('--all',action='store_true',help='독립 압축 모듈 65개를 검사한다.')
    args = parser.parse_args()
    if not __debug__:
        parser.error('검증은 -O 옵션 없이 실행하세요')
    profile, binding = load_profile(args.profile)
    resources = args.output/'Resources'
    receipt = json.loads((args.output/'receipt.json').read_bytes())
    manifest = json.loads(read(resources/'hongdal-dong-modules.json',receipt['manifestSha256']))
    frame = tuple(manifest[k] for k in ('originLatitude','originLongitude','metresPerDegreeLatitude','metresPerDegreeLongitude'))
    pinned = manifest.get('referenceProfile')
    if args.cache is None:
        local_cache = args.output/'modules'
        shared = DEFAULT if pinned is not None else ROOT/'artifacts/local/admin-dong-unity-r4/bake'
        args.cache = local_cache if local_cache.is_dir() else shared/'modules'
    if pinned is None:
        _, default_binding = load_profile(DEFAULT_PROFILE)
        require(binding == default_binding and 'referenceProfile' not in receipt and
                manifest['scopeRevision'] == 'candidate-2026:regional:r4', '이전 r4 묶음의 기본 기준과 불일치')
    else:
        require(pinned == receipt['referenceProfile'] == binding, '실행용 자료와 생성 기준 지문 불일치')
    catalog = unique(json.loads((SOURCE/'catalog.json').read_bytes())['editions']['candidate-2026'],
                     lambda r:r['code'], '행정동 원본 목록')
    ids,source_ids,source_rows = set(),set(),set()
    indexes = source_indexes()
    reports = []
    compressed = 0
    groups = defaultdict(list)
    if args.all:
        library = json.loads((args.cache/'catalog-all.json').read_bytes())
        assert library['scopeRevision'] == manifest['scopeRevision'] and library.get('referenceProfile') == pinned
        assert len(library['codes']) == len(set(library['codes'])) == library['moduleCount'] == len(catalog)
        assert set(library['codes']) == set(catalog)
        entries = []
        for item in library['modules']:
            require(item['path'] == item['code']+'/receipt.json', '모듈 목록의 코드·경로 불일치')
            cached = json.loads(read(args.cache/item['path'],item['sha256']))
            require(cached['bakerFingerprint'] == library['bakerFingerprint'] and
                    cached['code'] == item['code'], f"{item['code']} 카탈로그와 제작 기록 불일치")
            entries.append(cached['entry'])
        assert [e['code'] for e in entries] == library['codes']
    else:
        entries = manifest['modules']
        unique(entries, lambda e:e['code'], '실행판 모듈')
        assert receipt['modules'] == entries
        index = json.loads(read(resources/(manifest['buildingIndexResource']+'.json'),manifest['buildingIndexSha256']))
        assert index['origin'] == {'latitude':frame[0],'longitude':frame[1]}
        assert index['metresPerDegreeLatitude'] == frame[2] and index['metresPerDegreeLongitude'] == frame[3]
        for row in index['buildings']:
            groups[row['region']].append(row)
        assert set(groups) == {e['regionId'] for e in entries}
    for entry in entries:
        code = entry['code']
        folder = args.cache/code
        cached = json.loads((folder/'receipt.json').read_bytes())
        require(cached.get('referenceProfile') == pinned, f'{code} 굽기 기록과 생성 기준 불일치')
        mesh_z = read(folder/'mesh.bytes.gz',cached['cacheFiles']['mesh.bytes.gz'])
        index_z = read(folder/'index.json.gz',cached['cacheFiles']['index.json.gz'])
        compressed += len(mesh_z)+len(index_z)+(folder/'receipt.json').stat().st_size
        raw = gzip.decompress(mesh_z)
        rows = json.loads(gzip.decompress(index_z))
        assert hashlib.sha256(raw).hexdigest() == entry['sha256'].lower()
        if not args.all:
            assert raw == (resources/(entry['resource']+'.bytes')).read_bytes() and rows == groups[entry['regionId']]
        try:
            results, surfaces = mesh(raw,frame,entry,profile)
            results['surfaceLayers'] = verify_module(entry,rows,frame,args.cache,catalog[code],profile,surfaces,indexes)
        except (AssertionError, KeyError, ValueError) as error:
            raise ValueError(f'{code} {entry["name"]} 검증 실패: {error}') from error
        for row in rows:
            assert row['id'] not in ids and row['sourceFeatureId'] not in source_ids
            ids.add(row['id'])
            source_ids.add(row['sourceFeatureId'])
            source_rows.add(row['sourceBuildingRow'])
        reports.append({k:entry[k] for k in ('code','name','district','sha256','buildings','heightFromSource','heightFromFloors',
                       'heightSymbolic','buildingRowsWithParcelLabel','roads','sourceWalkLinks','surfaceWalkLinks','sourceLaneFragments',
                       'parkPlanFragments','permitKeys','interiorRings','multiPartFootprints')} | results)
    if not args.all:
        assert len(ids) == receipt['buildings'] and len(entries) == manifest['scopeModuleCount']
        assert all(r['to'] in ids for r in manifest['roleMap'])
    report = {'result':'Passed','scopeRevision':manifest['scopeRevision'],'moduleCount':len(entries),'buildingsChecked':len(ids),
              'distinctSourceBuildingRows':len(source_rows),'allLayerHashesChecked':True,'allSelectionRingsAndHolesChecked':True,
              'allVerticesAndTriangleIndicesChecked':True,'allPermitCandidateCountsChecked':True,'compressedModuleBytes':compressed,
              'rolesInReviewPackage':len(manifest['roleMap']) if not args.all else None,'commonFrame':frame,'modules':reports,'actualUnityExecution':False}
    report.update(referenceProfile=binding, profilePinnedInInput=pinned is not None, allSurfaceAreasChecked=True)
    dest = args.report or args.output/('library-verification.json' if args.all else 'verification.json')
    dest.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile('w', dir=dest.parent, encoding='utf8', delete=False) as tmp:
        json.dump(report,tmp,ensure_ascii=False,indent=2)
        tmp.write('\n')
    temp_path = Path(tmp.name)
    try:
        temp_path.replace(dest)
    finally:
        temp_path.unlink(missing_ok=True)
    print(f"{len(entries)}동, 건물 조각 {len(ids):,}개, 표시·선택·출처 검사 통과. {dest}",flush=True)


if __name__=='__main__':
    main()
