"""기존 DSM과 검증한 한강 벡터를 같은 지구본 좌표의 로컬 검토 사본으로 만든다."""
import hashlib, io, json, sys, zipfile
from pathlib import Path
import numpy as np
import rasterio
from rasterio.warp import reproject, Resampling
from rasterio.transform import from_origin
import shapefile
from shapely.geometry import shape, box

root=Path(sys.argv[1]);unity=Path(sys.argv[2])
folder=root/'artifacts/local/public-data/icheon-terrain-20260923-r1'
receipt=json.loads((folder/'receipt.json').read_text())
dem=folder/'Copernicus_DSM_COG_10_N37_00_E127_00_DEM.tif'
assert hashlib.sha256(dem.read_bytes()).hexdigest()==receipt['Hash']
manifest=json.loads((unity/'Assets/Ssalddel/Resources/KoreanPeninsulaTerrainPreviewManifest.json').read_text(encoding='utf-8-sig'))
river_source=manifest['sources'][1]
archive=(unity/'artifacts/local/natural-earth-korean-peninsula-preview/rivers-10m/ne_10m_rivers_lake_centerlines.zip').read_bytes()
assert hashlib.sha256(archive).hexdigest()==river_source['sourceArchiveHashSha256'].lower()
west,east,south,north=127.05,127.95,37.05,37.95
n=201;step=(east-west)/(n-1)
height=np.full((n,n),np.nan,dtype='float32')
with rasterio.open(dem) as src:
    reproject(rasterio.band(src,1),height,src_transform=src.transform,src_crs=src.crs,
        dst_transform=from_origin(west-step/2,north+step/2,step,step),dst_crs='EPSG:4326',
        dst_nodata=np.nan,resampling=Resampling.bilinear)
assert np.isfinite(height).all() and height.min()>=-100 and height.max()<3000
with zipfile.ZipFile(io.BytesIO(archive)) as z:
    read=lambda ext:z.read(next(name for name in z.namelist() if name.endswith(ext)))
    reader=shapefile.Reader(shp=io.BytesIO(read('.shp')),dbf=io.BytesIO(read('.dbf')))
    record=reader.shapeRecord(506);assert record.record.as_dict()['name']=='Han'
    line=shape(record.shape.__geo_interface__).intersection(box(west,south,east,north))
    segments=[line] if line.geom_type=='LineString' else list(line.geoms)
    rivers=[]
    for segment in segments:
        if segment.geom_type!='LineString':continue
        # 넓은 선자료의 높이는 DSM에 투영할 뿐 수리학적 흐름/하상 정확도를 뜻하지 않는다.
        segment=segment.segmentize(.002)
        rivers.append(dict(points=[dict(longitude=round(x,7),latitude=round(y,7)) for x,y in segment.coords]))
assert rivers
base=root/'artifacts/local/public-data/korea-admin1-context-r1/seoul-with-admin-context.json'
raw=base.read_bytes();assert hashlib.sha256(raw).hexdigest()==Path(str(base)+'.sha256').read_text().strip()
data=json.loads(raw)
data['context']['terrainPatch']=dict(schemaVersion='globe-relief-review.v1',sourceHash=receipt['Hash'],
    riverSourceHash=river_source['sourceArchiveHashSha256'].lower(),review='LocalEditorOnly_NotForDistribution',
    west=west,east=east,south=south,north=north,width=n,heightMeters=np.round(height,2).flatten().tolist(),rivers=rivers)
out=base.parent/'globe-relief-review.json';encoded=json.dumps(data,ensure_ascii=False,separators=(',',':')).encode()
assert len(encoded)<4*1024*1024
out.write_bytes(encoded);digest=hashlib.sha256(encoded).hexdigest();Path(str(out)+'.sha256').write_text(digest)
print(json.dumps(dict(vertices=n*n,triangles=(n-1)**2*2,heightMin=float(height.min()),heightMax=float(height.max()),rivers=len(rivers),bytes=len(encoded),sha256=digest)))
