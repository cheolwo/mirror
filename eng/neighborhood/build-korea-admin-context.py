"""광역 참고 경계와 서울 상세 경계를 병합하지 않고 같은 사본의 별도 층으로 결속한다."""
import hashlib, io, json, sys, zipfile
from pathlib import Path
import shapefile
from shapely.geometry import shape, Point, box
from shapely import constrained_delaunay_triangles
from shapely.ops import unary_union

root=Path(sys.argv[1]).resolve()
folder=root/'artifacts/local/public-data/korea-admin1-context-r1'
receipt=json.loads((folder/'receipt.json').read_text())
raw=(folder/'admin1.zip').read_bytes()
assert hashlib.sha256(raw).hexdigest()==receipt['Hash']
with zipfile.ZipFile(io.BytesIO(raw)) as z:
    assert sum(e.file_size for e in z.infolist())<100*1024*1024
    read=lambda ext:z.read(next(n for n in z.namelist() if n.endswith(ext)))
    reader=shapefile.Reader(shp=io.BytesIO(read('.shp')),dbf=io.BytesIO(read('.dbf')),encoding='utf-8')
    areas=[];geometries={}
    for record in reader.iterShapeRecords():
        a=record.record.as_dict()
        if a['adm0_a3'] not in ('KOR','PRK'):continue
        g=shape(record.shape.__geo_interface__)
        assert g.is_valid and not g.is_empty,a['adm1_code']
        geometries[a['adm1_code']]=g
        # 광역 원본도 일반화된 참고 자료다. 여기서 다시 단순화하지 않는다.
        parts=[g] if g.geom_type=='Polygon' else list(g.geoms)
        def pos(p):
            assert 123<p[0]<132 and 32<p[1]<44
            return dict(latitude=round(p[1],7),longitude=round(p[0],7))
        center=g.representative_point()
        areas.append(dict(code=a['adm1_code'],name=a['name_ko'] or a['name'],parentCode=a['adm0_a3'],level=-1,
            center=pos((center.x,center.y)),rings=[dict(points=[pos(p) for p in ring.coords]) for part in parts for ring in [part.exterior,*part.interiors]],triangles=[]))
assert len(areas)==28
for code,point in [('KOR-2497',(126.978,37.5665)),('KOR-2495',(126.7052,37.4563)),('KOR-2498',(127.0286,37.2636))]:
    assert geometries[code].covers(Point(*point)),code
seoul=root/'artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1/seoul-globe-preview.json'
source=seoul.read_bytes();assert hashlib.sha256(source).hexdigest()==Path(str(seoul)+'.sha256').read_text().strip()
snapshot=json.loads(source)
# 수도권과 서해 주변만 교체한다. 0.25도 기존 격자에 맞춘 절단 범위.
land=unary_union(list(geometries.values())).intersection(box(125,36,128,39))
vertices=[];indices=[];lookup={};covered=0.0
for iy in range(360,390):
    for ix in range(1250,1280):
        part=land.intersection(box(ix/10,iy/10,(ix+1)/10,(iy+1)/10))
        if part.is_empty:continue
        tris=constrained_delaunay_triangles(part)
        for t in tris.geoms:
            assert part.covers(t), 'TriangleOutsideLand'
            covered+=t.area
            for x,y in list(t.exterior.coords)[:3]:
                key=(round(x,7),round(y,7))
                if key not in lookup:
                    lookup[key]=len(vertices);vertices.append(dict(latitude=key[1],longitude=key[0]))
                indices.append(lookup[key])
assert abs(covered-land.area)<1e-8, 'LandCoverageMismatch'
patch=dict(west=125,east=128,south=36,north=39,vertices=vertices,indices=indices)
snapshot['context']=dict(schemaVersion='korea-admin1-context.v1',sourceHash=receipt['Hash'],
    attribution='Natural Earth Admin 1 / 1:10M / Public Domain / 현행 공식 경계 아님',
    areas=sorted(areas,key=lambda a:a['code']),landPatch=patch)
data=json.dumps(snapshot,ensure_ascii=False,separators=(',',':')).encode()
assert len(data)<4*1024*1024
output=folder/'seoul-with-admin-context.json';output.write_bytes(data)
digest=hashlib.sha256(data).hexdigest();Path(str(output)+'.sha256').write_text(digest)
print(json.dumps(dict(contextCount=len(areas),capitalReferencePointsInside=True,
    boundaryPointCount=sum(len(r['points']) for a in areas for r in a['rings']),
    landVertices=len(vertices),landTriangles=len(indices)//3,coverageError=abs(covered-land.area),
    additionalSimplification=False,bytes=len(data),sha256=digest)))
