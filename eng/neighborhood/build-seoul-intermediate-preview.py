"""동결된 역사 경계의 로컬 Unity 관찰 사본. 도형 수선/현행 경계 승격 없음."""
import hashlib, io, json, sys, zipfile
from pathlib import Path
import shapefile
from pyproj import CRS, Transformer
from shapely.geometry import shape, Point
from shapely import constrained_delaunay_triangles

root = Path(sys.argv[1]).resolve()
folder = root / 'artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1'
receipts = json.loads((folder/'receipt.json').read_text())
forward = Transformer.from_crs(4326, 5181, always_xy=True)
backward = Transformer.from_crs(5181, 4326, always_xy=True)
origin = forward.transform(127.0884106, 37.5806971)
assert abs(backward.transform(*origin)[0]-127.0884106) < 1e-8
areas = []
selected_districts = {'11260':16, '11230':14, '11215':15, '11350':19}
for receipt in receipts:
    raw = (folder/receipt['File']).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == receipt['Hash']
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        def entry(ext):
            return archive.read(next(n for n in archive.namelist() if n.endswith(ext)))
        # ESRI WKT의 datum 이름은 EPSG 객체와 다르다. 공식 페이지의 5181 선언과
        # 원본의 투영 파라미터/타원체/단위를 대조하며 이름 동등성을 주장하지 않는다.
        assert CRS.from_wkt(entry('.prj').decode()).to_dict() == CRS.from_epsg(5181).to_dict()
        reader = shapefile.Reader(shp=io.BytesIO(entry('.shp')), dbf=io.BytesIO(entry('.dbf')), encoding='utf-8')
        assert len(reader) == receipt['Records']
        district = receipt['Dataset'] == 'OA-22161'
        ids = set()
        for row in reader.iterShapeRecords():
            values = row.record.as_dict()
            code = values['SIGNGU_CD' if district else 'ADSTRD_CD']
            assert code not in ids
            ids.add(code)
            if not district and code[:5] not in selected_districts: continue
            geometry = shape(row.shape.__geo_interface__)
            assert geometry.is_valid and not geometry.is_empty, code
            geometry = geometry.simplify(8, preserve_topology=True)
            assert geometry.is_valid
            parts = [geometry] if geometry.geom_type == 'Polygon' else list(geometry.geoms)
            triangles = [t for p in parts for t in constrained_delaunay_triangles(p).geoms]
            assert abs(sum(t.area for t in triangles)-geometry.area)/geometry.area < 1e-6
            def pos(p):
                lon, lat = backward.transform(*p)
                return dict(x=round((p[0]-origin[0])/1000,6),z=round((p[1]-origin[1])/1000,6),
                            latitude=round(lat,9),longitude=round(lon,9))
            rings = [dict(points=[pos(p) for p in ring.coords]) for part in parts for ring in [part.exterior,*part.interiors]]
            center = geometry.representative_point()
            lon,lat = backward.transform(center.x,center.y)
            assert 126 < lon < 128 and 37 < lat < 38
            areas.append(dict(code=code,name=values['SIGNGU_NM' if district else 'ADSTRD_NM'],
                level=0 if district else 1, parentCode='' if district else code[:5], center=pos((center.x,center.y)), rings=rings,
                triangles=[pos(p) for t in triangles for p in list(t.exterior.coords)[:3]]))
            if code == '11260': assert geometry.covers(Point(*origin))
assert len([a for a in areas if a['level']==0])==25
for code,count in selected_districts.items():
    assert sum(a['level']==1 and a['parentCode']==code for a in areas)==count, code
snapshot=dict(schemaVersion='seoul-intermediate-preview.v3',revision='historical-seoul-globe-preview.r3',
    sourceDate='2023-10-31',attribution='서울특별시·서울신용보증재단 / 공공누리 제1유형',
    distributionApproved=False,observationPresentationOnly=True,traversalReady=False,
    originLatitude=37.5806971,originLongitude=127.0884106,metersPerUnit=1000,
    sourceHashes=[r['Hash'] for r in receipts],areas=sorted(areas,key=lambda a:(a['level'],a['code'])))
output=folder/'seoul-globe-preview.json'
data=json.dumps(snapshot,ensure_ascii=False,separators=(',',':')).encode()
output.write_bytes(data)
digest=hashlib.sha256(data).hexdigest()
output.with_suffix('.json.sha256').write_text(digest)
print(json.dumps(dict(districts=25,dongs=sum(selected_districts.values()),counts=selected_districts,sha256=digest,bytes=len(data),geometryValid=True,coordinateRoundtrip=True)))
