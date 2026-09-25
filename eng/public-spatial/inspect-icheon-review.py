"""이천 소규모 DSM/위성영상 검토. 실제 공장 부지나 도로 권위가 아니다."""
import hashlib
import json
from pathlib import Path
import urllib.request
import numpy as np
import rasterio
from rasterio.windows import from_bounds
from rasterio.warp import transform_bounds

root = Path(__file__).resolve().parents[2]
folder = root / 'artifacts/local/public-data/icheon-terrain-20260923-r1'
dem = folder / 'Copernicus_DSM_COG_10_N37_00_E127_00_DEM.tif'
bbox = (127.4175, 37.252, 127.4625, 37.288) # 약4km 검토 창, 행정 경계 아님
receipt = json.loads((folder/'receipt.json').read_text(encoding='utf-8'))
assert hashlib.sha256(dem.read_bytes()).hexdigest() == receipt['Hash']
report = {'bboxWgs84':bbox,'review':'PrivateReviewOnly','siteApproved':False,'traversalReady':False}

def clip(source, destination):
    with rasterio.open(source) as src:
        bounds = transform_bounds('EPSG:4326',src.crs,*bbox)
        window = from_bounds(*bounds,transform=src.transform).round_offsets().round_lengths()
        data=src.read(window=window,masked=True)
        assert data.size > 0
        profile=src.profile.copy()
        profile.update(driver='GTiff',width=data.shape[2],height=data.shape[1],transform=src.window_transform(window),compress='deflate')
        with rasterio.open(destination,'w',**profile) as dst: dst.write(data.filled(src.nodata or 0))
        return {'crs':str(src.crs),'sourceResolution':src.res,'shape':list(data.shape),'validFraction':float(np.mean(~np.ma.getmaskarray(data))),
                'minimum':float(data.min()),'maximum':float(data.max()),'sourceNoData':src.nodata,
                'file':destination.name,'sha256':hashlib.sha256(destination.read_bytes()).hexdigest()}

report['dsm']=clip(str(dem),folder/'icheon-dsm-review.tif')
assert report['dsm']['validFraction']==1
# 동결된 검색 결과 재사용. 전체 연간/전국 영상 수집은 하지 않는다.
search_path=folder/'sentinel-search.json'
if not search_path.exists():
    body=json.dumps({'collections':['sentinel-2-l2a'],'bbox':bbox,'datetime':'2025-01-01T00:00:00Z/2025-12-31T23:59:59Z','limit':3,'query':{'eo:cloud_cover':{'lt':5}}}).encode()
    req=urllib.request.Request('https://earth-search.aws.element84.com/v1/search',data=body,headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=30) as response: payload=response.read(2*1024*1024+1)
    assert len(payload)<=2*1024*1024
    search_path.write_bytes(payload)
items=json.loads(search_path.read_text())['features']
item=min(items,key=lambda x:x['properties']['eo:cloud_cover'])
url=item['assets']['visual']['href']
assert url.startswith('https://sentinel-cogs.s3.us-west-2.amazonaws.com/')
# COG HTTP range로 해당 창만 읽는다. 다운로드한 전체 장면이라고 보고하지 않는다.
with rasterio.Env(GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR',CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif',GDAL_HTTP_TIMEOUT='45'):
    report['imagery']=clip(url,folder/'icheon-sentinel-visual-review.tif')
report['imagery'].update(itemId=item['id'],sourceUrl=url,observedAt=item['properties']['datetime'],sceneCloudPercent=item['properties']['eo:cloud_cover'],localCloudValidated=False)
(folder/'quality.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
