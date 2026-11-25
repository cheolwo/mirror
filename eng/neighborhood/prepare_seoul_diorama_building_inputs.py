"""서울 건물 원본을 행정동별로 꺼내 쓰는 읽기용 SQLite·준비표로 정리한다."""
import argparse
import csv
import gzip
import hashlib
import json
import math
import sqlite3
import sys
import zipfile
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT / 'artifacts/local/python-packages/spatial')]
import shapefile
from pyproj import CRS, Transformer
from shapely import from_wkb
from shapely.geometry import shape, mapping
from shapely.ops import transform
from shapely.strtree import STRtree
from sagajeong_spatial_presentation import _read_dbf_header, _read_exact, _dbf_text


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def document(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def number(raw):
    try:
        value = float(raw)
        return value if math.isfinite(value) else None
    except ValueError:
        return None


def boundaries(path):
    with gzip.open(path, 'rt', encoding='utf-8-sig') as stream:
        features = [item for item in json.load(stream)['features'] if item['properties']['sido'] == '11']
    assert len(features) == 427
    transform_xy = Transformer.from_crs(4326, 5186, always_xy=True).transform
    geometries = [transform(transform_xy, shape(item['geometry'])) for item in features]
    assert all(item.is_valid and not item.is_empty for item in geometries)
    codes = [item['properties']['adm_cd2'] for item in features]
    assert len(set(codes)) == 427
    return features, geometries


def build(args):
    if args.source.name != 'AL_D010_11_20261109.zip':
        raise ValueError('이 수집 도구의 범위는 서울 2026-11-09 원본입니다.')
    args.output.mkdir(parents=True, exist_ok=True)
    database = args.output / 'seoul-building-inputs.sqlite'
    if database.exists():
        raise FileExistsError('기존 검토 DB를 덮지 않습니다. 새 출력 경로를 지정하세요.')
    features, geometries = boundaries(args.boundary)
    tree = STRtree(geometries)
    connection = sqlite3.connect(database)
    connection.executescript('''
      PRAGMA foreign_keys=ON;
      CREATE TABLE metadata(key TEXT PRIMARY KEY,value_json TEXT NOT NULL);
      CREATE TABLE dong(code TEXT PRIMARY KEY,name TEXT NOT NULL,district_code TEXT NOT NULL,
                        district_name TEXT NOT NULL,geometry_wkb BLOB NOT NULL);
      CREATE TABLE building(source_row INTEGER PRIMARY KEY,source_id TEXT,pnu TEXT,legal_code TEXT,
             source_fields_json TEXT NOT NULL,height_m REAL,ground_floors INTEGER,geometry_status TEXT NOT NULL,
             geometry_wkb BLOB,area_m2 REAL);
      CREATE VIRTUAL TABLE building_bounds USING rtree(source_row,min_x,max_x,min_y,max_y);
      CREATE TABLE building_dong(source_row INTEGER NOT NULL REFERENCES building(source_row),
             dong_code TEXT NOT NULL REFERENCES dong(code),intersection_area_m2 REAL NOT NULL,
             PRIMARY KEY(source_row,dong_code));
      CREATE INDEX building_dong_code ON building_dong(dong_code,source_row);
      CREATE INDEX building_pnu ON building(pnu);
      CREATE INDEX building_original_id ON building(source_id);
    ''')
    for feature, geometry in zip(features, geometries):
        p = feature['properties']
        connection.execute('INSERT INTO dong VALUES(?,?,?,?,?)',
                           (p['adm_cd2'], p['adm_nm'], p['sgg'], p['sggnm'], geometry.wkb))
    counts, dispositions = Counter(), Counter()
    quarantine = args.output / 'building-review.jsonl'
    with zipfile.ZipFile(args.source) as archive, quarantine.open('w', encoding='utf-8') as review:
        shp_name, = [name for name in archive.namelist() if name.lower().endswith('.shp')]
        assert Path(shp_name).name == 'AL_D010_11_20261109.shp'
        stem = shp_name[:-4]
        prj = archive.read(stem + '.prj').decode('utf-8-sig')
        source_crs = CRS.from_wkt(prj)
        assert source_crs.is_projected and 'AUTHORITY["EPSG","5186"]' in prj.replace(' ', '')
        with archive.open(stem + '.dbf') as dbf, archive.open(shp_name) as shp:
            expected_rows, _, record_bytes, fields = _read_dbf_header(dbf)
            required = ['A1','A2','A3','A4','A5','A6','A7','A8','A9','A16','A26']
            assert set(required).issubset(fields)
            reader = shapefile.Reader(shp=shp)
            for index, shp_geometry in enumerate(reader.iterShapes(), 1):
                record = _read_exact(dbf, record_bytes, 'BuildingDbfTruncated')
                counts['sourceRows'] += 1
                if record[0] == 42:
                    counts['deletedRows'] += 1
                    continue
                assert record[0] == 32
                source = {key:_dbf_text(record, fields[key], 'cp949') for key in required}
                assert source['A3'].startswith('11'), '서울 원본에 다른 지역 코드가 있습니다.'
                height = number(source['A16'])
                floors = number(source['A26'])
                height = height if height is not None and 0 < height <= 1000 else None
                floors = int(floors) if floors is not None and floors.is_integer() and floors > 0 else None
                geometry, status, area, matches = None, 'Unparsed', None, []
                try:
                    parts = list(shp_geometry.parts) + [len(shp_geometry.points)]
                    rings = [shp_geometry.points[a:b] for a,b in zip(parts,parts[1:])]
                    if not any(sum(r[i][0]*r[(i+1)%len(r)][1]-r[(i+1)%len(r)][0]*r[i][1]
                                   for i in range(len(r))) < 0 for r in rings if len(r) >= 4):
                        raise ValueError('NoClockwiseExteriorRing')
                    geometry = shape(shp_geometry.__geo_interface__)
                    if geometry.is_empty or geometry.geom_type not in ('Polygon','MultiPolygon'):
                        status = 'EmptyOrNonPolygon'
                    elif not geometry.is_valid:
                        status = 'InvalidSourceGeometry'
                    else:
                        status, area = 'ValidSourceGeometry', geometry.area
                        for j in tree.query(geometry, predicate='intersects'):
                            clipped_area = area if geometries[j].covers(geometry) else geometry.intersection(geometries[j]).area
                            if clipped_area > 0:
                                matches.append((index, features[j]['properties']['adm_cd2'], clipped_area))
                except Exception as error:
                    status = 'OrphanInteriorRings' if str(error) == 'NoClockwiseExteriorRing' else 'GeometryReadOrIntersectionFailed'
                    review.write(json.dumps({'sourceRow': index, 'status': status,
                                             'error': str(error)[:240]}, ensure_ascii=False) + '\n')
                    matches = []
                dispositions[status] += 1
                if status != 'ValidSourceGeometry':
                    review.write(json.dumps({'sourceRow': index, 'sourceId': source['A1'],
                                             'status': status}, ensure_ascii=False) + '\n')
                connection.execute('INSERT INTO building VALUES(?,?,?,?,?,?,?,?,?,?)',
                     (index,source['A1'],source['A2'],source['A3'],json.dumps(source,ensure_ascii=False),
                      height,floors,status,geometry.wkb if geometry is not None else None,area))
                if geometry is not None and not geometry.is_empty:
                    left,bottom,right,top = geometry.bounds
                    if all(math.isfinite(value) for value in (left,bottom,right,top)):
                        connection.execute('INSERT INTO building_bounds VALUES(?,?,?,?,?)',(index,left,right,bottom,top))
                connection.executemany('INSERT INTO building_dong VALUES(?,?,?)',matches)
                counts['dongMemberships'] += len(matches)
                counts['assignedBuildings'] += bool(matches)
                counts['unassignedBuildings'] += not matches
                counts['positiveSourceHeight'] += height is not None
                counts['positiveSourceFloors'] += floors is not None
                counts['heightAbove1000mReview'] += (number(source['A16']) or 0) > 1000
                if index % 25000 == 0:
                    connection.commit()
                    print(json.dumps({'stage':'SeoulBuildings','sourceRows':index,'assigned':counts['assignedBuildings']}),flush=True)
            assert counts['sourceRows'] == expected_rows
            assert dbf.read(1) in (b'',b'\x1a')
    counts['retainedRows'] = connection.execute('SELECT COUNT(*) FROM building').fetchone()[0]
    assert counts['retainedRows'] + counts['deletedRows'] == counts['sourceRows']
    source_info = {'path':str(args.source.resolve()),'bytes':args.source.stat().st_size,'sha256':sha(args.source),
                   'referenceDate':'2026-11-09','sourceCrs':'EPSG:5186','sourcePrj':prj}
    metadata = {'schemaVersion':'hongdal.seoul-building-inputs.v1','source':source_info,
                'boundary':{'path':str(args.boundary.resolve()),'sha256':sha(args.boundary),'edition':'candidate-20261001',
                            'officialCurrentBoundaryVerified':False},
                'geometryPolicy':'Preserve polygon rings and multipart topology; quarantine invalid geometry; no automatic repair',
                'heightPolicy':'Source field only; no register join, height substitution, floor multiplication or symbolic height generated',
                'identityPolicy':'Source row is tied to exact ZIP SHA; original source_id is not assumed globally unique',
                'distributionApproved':False,'unityApplied':False,'counts':dict(counts),'dispositions':dict(dispositions)}
    for key,value in metadata.items():
        connection.execute('INSERT INTO metadata VALUES(?,?)',(key,json.dumps(value,ensure_ascii=False)))
    connection.commit()
    assert connection.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    assert connection.execute('PRAGMA foreign_key_check').fetchall() == []
    profiles = []
    for code,name,district_code,district_name in connection.execute('SELECT code,name,district_code,district_name FROM dong ORDER BY code'):
        n = connection.execute('SELECT COUNT(*) FROM building_dong WHERE dong_code=?',(code,)).fetchone()[0]
        profile = {'administrativeCode':code,'name':name,'districtCode':district_code,'districtName':district_name,
                   'sourceBuildingCount':n,'database':'../seoul-building-inputs.sqlite','sourceZipSha256':source_info['sha256'],
                   'boundaryEdition':'candidate-20261001','crs':'EPSG:5186','clipping':'AtExtractionTime;SourceGeometryPreserved',
                   'coverageMeaning':'IntersectionWithDatedCandidateBoundary;ZeroDoesNotCertifyEmptyGround',
                   'distributionApproved':False,'unityApplied':False}
        document(args.output/'dong'/f'{code}.json',profile)
        profiles.append(profile)
    connection.close()
    document(args.output/'index.json',{'schemaVersion':metadata['schemaVersion'],'dongCount':len(profiles),
                                     'districtCount':len({p['districtCode'] for p in profiles}), 'dongs':profiles})
    with (args.output/'dong-summary.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer = csv.DictWriter(stream,fieldnames=['administrativeCode','name','districtCode','districtName','sourceBuildingCount'])
        writer.writeheader()
        writer.writerows({k:p[k] for k in writer.fieldnames} for p in profiles)
    metadata.update({'generatedAtUtc':datetime.now(timezone.utc).isoformat(),'database':{'path':database.name,'sha256':sha(database),'bytes':database.stat().st_size},
                     'collectorSha256':sha(Path(__file__)),'profileCount':len(profiles),'districtCount':25})
    document(args.output/'receipt.json',metadata)
    print(json.dumps({'counts':dict(counts),'dispositions':dict(dispositions),'profiles':len(profiles),'receipt':str(args.output/'receipt.json')}),flush=True)


def extract(args):
    if args.destination is not None and args.destination.exists():
        raise FileExistsError('기존 파일은 덮지 않습니다. 새 추출 경로를 지정하세요.')
    database = args.output/'seoul-building-inputs.sqlite'
    receipt = json.loads((args.output/'receipt.json').read_text(encoding='utf-8'))
    assert sha(database) == receipt['database']['sha256']
    if args.destination is None or args.code is None:
        raise ValueError('동 코드와 별도 추출 경로가 필요합니다.')
    connection = sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True)
    connection.row_factory = sqlite3.Row
    boundary = connection.execute('SELECT * FROM dong WHERE code=?',(args.code,)).fetchone()
    if boundary is None:
        raise ValueError('준비하지 않은 행정동 코드')
    clip = from_wkb(boundary['geometry_wkb'])
    to_wgs = Transformer.from_crs(5186,4326,always_xy=True).transform
    rows = connection.execute('''SELECT b.*,l.intersection_area_m2 FROM building b JOIN building_dong l
                              ON b.source_row=l.source_row WHERE l.dong_code=? ORDER BY b.source_row''',(args.code,))
    features = []
    for row in rows:
        original = from_wkb(row['geometry_wkb'])
        geometry = original.intersection(clip)
        if geometry.is_empty or not geometry.is_valid:
            raise ValueError('추출 도형이 유효하지 않습니다.')
        assert math.isclose(geometry.area,row['intersection_area_m2'],rel_tol=1e-8,abs_tol=1e-6)
        features.append({'type':'Feature','id':f'al-d010:20261109:11:{row["source_row"]}',
                         'geometry':mapping(transform(to_wgs,geometry)),
                         'properties':{'sourceRowOneBased':row['source_row'],'sourceId':row['source_id'],'pnu':row['pnu'],
                                       'legalDongCode':row['legal_code'],'administrativeDongCode':args.code,
                                       'sourceFields':json.loads(row['source_fields_json']),'observedHeightM':row['height_m'],
                                       'observedGroundFloors':row['ground_floors'],'sourceZipSha256':receipt['source']['sha256']}})
    connection.close()
    args.destination.parent.mkdir(parents=True,exist_ok=True)
    with gzip.open(args.destination,'xt',encoding='utf-8') as stream:
        json.dump({'type':'FeatureCollection','features':features,'sourceRevision':'AL_D010:Seoul:20261109',
                   'boundaryRevision':'candidate-20261001','axisOrder':'longitude,latitude','distributionApproved':False,
                   'unityApplied':False},stream,ensure_ascii=False,separators=(',',':'))
    print(json.dumps({'code':args.code,'features':len(features),'sha256':sha(args.destination),'path':str(args.destination)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode',choices=['build','extract'],default='build')
    parser.add_argument('--source',type=Path,default=ROOT/'artifacts/local/public-data/nationwide-diorama-buildings-20261116-r1/raw/AL_D010_11_20261109.zip')
    parser.add_argument('--boundary',type=Path,default=ROOT/'artifacts/local/admin-dong-diorama-precision-r3/boundary/sources/derived-20261001.gz')
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/local/public-data/seoul-diorama-building-inputs-20261116-r2')
    parser.add_argument('--code')
    parser.add_argument('--destination',type=Path)
    arguments = parser.parse_args()
    (build if arguments.mode == 'build' else extract)(arguments)
