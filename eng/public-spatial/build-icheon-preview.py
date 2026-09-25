"""로컬 검토 전용. 동결 DSM/RGB를 같은 30m 미터 격자로 정렬한다."""
import json, hashlib
from pathlib import Path
import numpy as np
import rasterio
from rasterio.warp import reproject, Resampling, transform
from rasterio.transform import from_origin

folder=Path(__file__).resolve().parents[2]/'artifacts/local/public-data/icheon-terrain-20260923-r1'
q=json.loads((folder/'quality.json').read_text(encoding='utf-8'))
for key in ('dsm','imagery'):
    assert hashlib.sha256((folder/q[key]['file']).read_bytes()).hexdigest()==q[key]['sha256'], 'SourceHashMismatch'
# 검토 창 가장자리 보간 결측을 피하는 내부 3km 정사각형. EPSG32652 동/북 방향.
x,y=transform('EPSG:4326','EPSG:32652',[127.44],[37.27])
n=101; spacing=30.; affine=from_origin(x[0]-1515,y[0]+1515,spacing,spacing)
arrays=[]
for key in ('dsm','imagery'):
    with rasterio.open(folder/q[key]['file']) as src:
        dest=np.full((src.count,n,n),np.nan,dtype='float32')
        for band in range(src.count):
            reproject(rasterio.band(src,band+1),dest[band],src_transform=src.transform,src_crs=src.crs,
                      dst_transform=affine,dst_crs='EPSG:32652',dst_nodata=np.nan,resampling=Resampling.bilinear)
        assert np.isfinite(dest).all(), 'MissingCells'
        arrays.append(dest)
height,rgb=arrays
assert height.max()>height.min() and rgb.min()>0
east,north=np.meshgrid(x[0]+(np.arange(n)-50)*spacing,y[0]+(50-np.arange(n))*spacing)
lon,lat=transform('EPSG:32652','EPSG:4326',east.flatten().tolist(),north.flatten().tolist())
# 지형은 30m, 영상은 10m를 유지한다. 정점색으로 해상도를 낮추지 않는다.
tn=301
texture=np.full((3,tn,tn),np.nan,dtype='float32')
with rasterio.open(folder/q['imagery']['file']) as src:
    for band in range(3):
        reproject(rasterio.band(src,band+1),texture[band],src_transform=src.transform,src_crs=src.crs,
                  dst_transform=from_origin(x[0]-1505,y[0]+1505,10,10),dst_crs='EPSG:32652',
                  dst_nodata=np.nan,resampling=Resampling.bilinear)
assert np.isfinite(texture).all() and texture.min()>0
data={'schema':'icheon-local-relief.r1','width':n,'spacing':spacing,'originEasting':x[0],
      'originNorthing':y[0],'crs':'EPSG:32652','baseHeight':float(height.min()),'heightScale':1,
      'review':'LocalEditorOnly_NotForDistribution','verticalDatum':'SourceDSM_NoVerticalDatumConversion',
      'sourceHashes':[q[k]['sha256'] for k in ('dsm','imagery')],
      'latitudes':lat,'longitudes':lon,
      'textureWidth':tn,'textureRgb':np.moveaxis(np.rint(texture).astype('uint8'),0,-1).flatten().tolist(),
      'heights':height[0].flatten().tolist(),'rgb':np.moveaxis(rgb,0,-1).flatten().tolist()}
payload=json.dumps(data,separators=(',',':'),allow_nan=False).encode()
(folder/'unity-preview.json').write_bytes(payload)
print(json.dumps({'vertices':n*n,'triangles':(n-1)**2*2,'heightMin':float(height.min()),'heightMax':float(height.max()),
                  'sha256':hashlib.sha256(payload).hexdigest(),'file':str(folder/'unity-preview.json')}))
