"""승인된 ITS 배포 ZIP에서 사가정역 교차 구간만 추출. 운영/게임 적용 없음."""
import argparse
import hashlib
import json
import shutil
import sys
import zipfile
from pathlib import Path


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save_once(path, value):
    content = json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n'
    if path.exists():
        if path.read_text(encoding='utf-8') != content:
            raise ValueError('ExistingExtractionDiffers:' + path.name)
    else:
        path.write_text(content, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    sys.path.insert(0, str(root / 'artifacts/local/public-data/gis-runtime-r1'))
    import shapefile
    import pyproj
    from shapely.geometry import box, shape, mapping
    from shapely.ops import transform

    folder = root / 'artifacts/local/public-data/sagajeong-nodelink-20260912-r1'
    acquisition = json.loads((folder / 'acquisition.json').read_text(encoding='utf-8-sig'))
    assert acquisition['status'] == 'Downloaded'
    for receipt in acquisition['files']:
        assert Path(receipt['file']).name == receipt['file']
        assert digest(folder / receipt['file']) == receipt['sha256'].lower()
    archive_hash = digest(folder / 'nodelink.zip')
    source_folder = folder / 'source-link'
    source_folder.mkdir(exist_ok=True)
    members = ['MOCT_LINK.' + ext for ext in ['cpg', 'prj', 'shp', 'shx', 'dbf']]
    with zipfile.ZipFile(folder / 'nodelink.zip') as archive:
        assert len(set(archive.namelist())) == len(archive.namelist())
        assert sum(archive.getinfo(name).file_size for name in members) < 700 * 1024 * 1024
        for name in members:
            target = source_folder / name  # 허용한 고정 파일명만 사용, extractall 금지.
            with archive.open(name) as stream:
                if target.exists():
                    assert digest(target) == hashlib.file_digest(stream, 'sha256').hexdigest()
                else:
                    with target.open('xb') as out:
                        shutil.copyfileobj(stream, out)
    assert (source_folder / 'MOCT_LINK.cpg').read_text().strip() == '949'
    wkt = (source_folder / 'MOCT_LINK.prj').read_text()
    source_crs = pyproj.CRS.from_wkt(wkt)
    to_geo = pyproj.Transformer.from_crs(source_crs, 'EPSG:4326', always_xy=True)
    to_source = pyproj.Transformer.from_crs('EPSG:4326', source_crs, always_xy=True)
    bounds = (127.0825, 37.5762, 127.0942, 37.5854)
    source_bounds = to_source.transform_bounds(*bounds, densify_pts=21)
    region = box(*bounds)
    source_files = [{'file': name, 'sha256': digest(source_folder / name)} for name in members]
    features = []
    candidates = 0
    max_error_m = 0.0
    operations = {}
    with shapefile.Reader(str(source_folder / 'MOCT_LINK.shp'), encoding='cp949', encodingErrors='strict') as reader:
        fields = reader.fields[1:]
        assert {'LINK_ID', 'F_NODE', 'T_NODE', 'LANES', 'ROAD_NAME', 'UPDATEDATE'} <= {f[0] for f in fields}
        national_count = len(reader)
        for record in reader.iterShapeRecords(bbox=source_bounds):
            candidates += 1
            original_geometry = record.shape.__geo_interface__
            geo = transform(to_geo.transform, shape(original_geometry))
            operation = to_geo.get_last_used_operation()
            operations[operation.description] = operation.accuracy
            assert not geo.is_empty and geo.is_valid
            if not region.intersects(geo):
                continue
            props = record.record.as_dict()
            assert isinstance(props['LANES'], int) and 0 <= props['LANES'] <= 30
            assert props['LINK_ID'] and props['F_NODE'] and props['T_NODE']
            original = shape(original_geometry)
            round_trip = transform(to_source.transform, geo)
            max_error_m = max(max_error_m, original.hausdorff_distance(round_trip))
            features.append({'type': 'Feature', 'id': props['LINK_ID'], 'sourceRecordIndex': record.record.oid,
                             'properties': props, 'geometry': mapping(geo), 'sourceGeometry': original_geometry})
        assert reader.numRecords == national_count
    features.sort(key=lambda feature: feature['id'])
    assert 0 < len(features) <= 300 and len({f['id'] for f in features}) == len(features)
    assert max_error_m < 0.01
    # 위치 왕복 검사는 수치 변환의 정합성이지 측량 정확도 보증이 아니다.
    metadata = {'sourceUrl': 'https://www.data.go.kr/data/15025526/fileData.do',
                'fieldDefinitionUrl': 'https://www.its.go.kr/nodelink/intro',
                'archiveSha256': archive_hash, 'releaseLabel': '2026-08-12',
                'sourceWkt': wkt, 'sourceCrsAuthority': source_crs.to_authority(),
                'outputCrs': 'EPSG:4326', 'axisOrder': 'longitude,latitude',
                'coordinateOperations': operations,
                'bbox': bounds, 'selection': 'Full directed links intersecting bbox; not clipped; endpoints may be outside',
                'nationalRecordCount': national_count, 'boundingBoxCandidates': candidates,
                'selectedCount': len(features), 'maxRoundTripErrorMeters': max_error_m,
                'sourceFiles': source_files, 'fieldSchema': fields,
                'parserVersions': {'pyshp': shapefile.__version__, 'pyproj': pyproj.__version__},
                'quality': 'PendingHumanReview',
                'limitations': ['NoPublication', 'NoRuntime', 'NoOsmJoin', 'NoWidthInference',
                                'DirectedLinkNotPhysicalRoad', 'LaneCountNotRoadWidth',
                                'NoSurveyAccuracyGuarantee', 'ReleaseDateNotObservationDate']}
    save_once(folder / 'sagajeong-links.json', {'type': 'FeatureCollection', 'metadata': metadata, 'features': features})
    names = {}
    for feature in features:
        name = feature['properties']['ROAD_NAME']
        names.setdefault(name, []).append(feature['properties']['LANES'])
    summary = {'archiveSha256': archive_hash, 'subsetSha256': digest(folder / 'sagajeong-links.json'),
               'nationalRecordCount': national_count, 'selectedCount': len(features),
               'bboxCandidates': candidates, 'maxRoundTripErrorMeters': max_error_m,
               'sourceCrsAuthority': source_crs.to_authority(), 'coordinateOperations': operations,
               'byRoadName': {name: {'links': len(lanes), 'laneValues': sorted(set(lanes))} for name, lanes in sorted(names.items())}}
    save_once(folder / 'extraction.json', summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
