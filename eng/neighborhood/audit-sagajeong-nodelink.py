"""추출기와 별도로 SHP/DBF 바이너리를 대조하는 읽기 전용 표본 검증."""
import hashlib
import json
import struct
import sys
from pathlib import Path

root=Path(sys.argv[1]).resolve()
folder=root/'artifacts/local/public-data/sagajeong-nodelink-20260912-r1'
sys.path.insert(0,str(root/'artifacts/local/public-data/gis-runtime-r1'))
from pyproj import CRS, Transformer
subset=json.loads((folder/'sagajeong-links.json').read_text(encoding='utf-8'))
metadata=subset['metadata']
transformer=Transformer.from_crs(4326,CRS.from_wkt(metadata['sourceWkt']),always_xy=True)
xmin,ymin,xmax,ymax=transformer.transform_bounds(*metadata['bbox'],densify_pts=21)
expected={f['sourceRecordIndex']:f for f in subset['features']}
assert len(expected)==len(subset['features'])==160
candidate_indices=set()
scanned=0
with (folder/'source-link/MOCT_LINK.shp').open('rb') as shp:
    header=shp.read(100)
    assert struct.unpack('>I',header[:4])[0]==9994 and struct.unpack('<I',header[32:36])[0]==3
    while record_header:=shp.read(8):
        number,words=struct.unpack('>II',record_header)
        begin=shp.tell()
        shape_type=struct.unpack('<I',shp.read(4))[0]
        assert shape_type==3
        left,bottom,right,top=struct.unpack('<4d',shp.read(32))
        if not (right<xmin or left>xmax or top<ymin or bottom>ymax):
            candidate_indices.add(number-1)
        if number-1 in expected:
            parts_count,points_count=struct.unpack('<II',shp.read(8))
            parts=list(struct.unpack('<'+'I'*parts_count,shp.read(4*parts_count)))+[points_count]
            points=[list(struct.unpack('<dd',shp.read(16))) for _ in range(points_count)]
            lines=[points[parts[i]:parts[i+1]] for i in range(parts_count)]
            source=expected[number-1]['sourceGeometry']
            assert source['coordinates']==(lines[0] if source['type']=='LineString' else lines)
        shp.seek(begin+words*2)
        scanned+=1
assert scanned==metadata['nationalRecordCount']==1557364
assert candidate_indices==set(expected)  # 이번 표본은 bbox 후보 전부가 실제 교차했다.
with (folder/'source-link/MOCT_LINK.dbf').open('rb') as dbf:
    header=dbf.read(32)
    count,header_bytes,record_bytes=struct.unpack('<IHH',header[4:12])
    assert count==scanned
    fields={}
    offset=1
    while (field:=dbf.read(32))[0]!=13:
        name=field[:11].split(b'\0')[0].decode('ascii')
        fields[name]=(offset,field[16]);offset+=field[16]
    assert offset==record_bytes
    for index,feature in expected.items():
        dbf.seek(header_bytes+index*record_bytes)
        record=dbf.read(record_bytes)
        assert record[0]==32
        for name in ['LINK_ID','F_NODE','T_NODE','ROAD_NAME','LANES','UPDATEDATE']:
            start,length=fields[name]
            value=record[start:start+length].decode('cp949',errors='strict').strip()
            if name=='LANES': value=int(value)
            assert value==feature['properties'][name],(index,name)
report={'status':'Passed','independentBinaryScanRecords':scanned,'verifiedDirectedLinks':len(expected),
        'checks':['SHP header and full record count','Independent bounding boxes','Exact source point coordinates',
                  'DBF record count and field layout','Selected DBF identifiers, names, lanes and update dates'],
        'subsetSha256':hashlib.sha256((folder/'sagajeong-links.json').read_bytes()).hexdigest()}
text=json.dumps(report,ensure_ascii=False,indent=2)+'\n'
target=folder/'independent-audit.json'
if target.exists(): assert target.read_text(encoding='utf-8')==text
else: target.write_text(text,encoding='utf-8')
print(text)
