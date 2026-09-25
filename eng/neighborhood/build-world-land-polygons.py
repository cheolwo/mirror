"""기존 배포 승인 세계 Catalog의 모든 영역을 육지 삼각형으로 재현한다."""
import hashlib, json, math, sys
from pathlib import Path
from shapely.geometry import Polygon, box
from shapely import constrained_delaunay_triangles, make_valid
from shapely.validation import explain_validity

unity=Path(sys.argv[1]).resolve()
source=unity/'Assets/Ssalddel/Resources/WorldGlobeCountryCatalog.json'
raw=source.read_bytes();catalog=json.loads(raw)
assert catalog['distributionApproved'] and catalog['observationPresentationOnly']
vertices=[];indices=[];lookup={};area_error=0;polygon_count=0;hole_count=0
repairs=[]
def emit(triangle):
    for x,y in list(triangle.exterior.coords)[:3]:
        key=(round(x,7),round(y,7))
        if key not in lookup:
            lookup[key]=len(vertices)//2;vertices.extend(key)
        indices.append(lookup[key])
for country in catalog['countries']:
    for outer in country['rings']:
        if outer['isHole']:continue
        holes=[r for r in country['rings'] if r['isHole'] and r['polygonIndex']==outer['polygonIndex']]
        ring=lambda r:[(p['longitude'],p['latitude']) for p in r['points']]
        polygon=Polygon(ring(outer),[ring(h) for h in holes])
        assert not polygon.is_empty, country['stableId']
        if not polygon.is_valid:
            fixed=make_valid(polygon);delta=abs(fixed.area-polygon.area)
            assert delta<1e-9,(country['stableId'],'MaterialGeometryRepairRefused',delta)
            repairs.append(dict(country=country['stableId'],reason=explain_validity(polygon),areaDelta=delta))
            polygon=fixed
        polygon_count+=1;hole_count+=len(holes);covered=0
        # 원본은 날짜변경선에서 분리되어 있다. 1도 절단으로 긴 구면 현과 지역 교체 경계를 제한한다.
        west,south,east,north=polygon.bounds
        for lat in range(math.floor(south),math.ceil(north)):
            for lon in range(math.floor(west),math.ceil(east)):
                part=polygon.intersection(box(lon,lat,lon+1,lat+1))
                if part.is_empty or part.area==0:continue
                for triangle in constrained_delaunay_triangles(part).geoms:
                    assert part.covers(triangle),country['stableId']
                    covered+=triangle.area;emit(triangle)
        error=abs(covered-polygon.area);assert error<1e-7,(country['stableId'],error)
        area_error=max(area_error,error)
result=dict(schemaVersion='world-land-polygons.v1',catalogHash=hashlib.sha256(raw).hexdigest(),
    countryCount=len(catalog['countries']),polygonCount=polygon_count,holeCount=hole_count,
    maxCellDegrees=1,repairs=repairs,coordinates=vertices,indices=indices)
data=json.dumps(result,separators=(',',':')).encode()
assert len(data)<16*1024*1024 and len(indices)<1500000
target=unity/'Assets/Ssalddel/Resources/WorldLandPolygons.json';target.write_bytes(data)
print(json.dumps(dict(countries=result['countryCount'],polygons=polygon_count,holes=hole_count,
    vertices=len(vertices)//2,triangles=len(indices)//3,bytes=len(data),maxAreaError=area_error,
    repairs=repairs,sha256=hashlib.sha256(data).hexdigest())))
