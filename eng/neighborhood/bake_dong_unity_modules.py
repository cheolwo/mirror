"""행정동별 출처가 고정된 r3 자료를 Unity의 HDIO v2 검토 모듈로 굽는다."""
import argparse
import gzip
import hashlib
import json
import math
import os
import struct
import sys
import tempfile
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "artifacts/local/admin-dong-diorama-precision-r3"
DEFAULT_OUT = ROOT / "artifacts/local/admin-dong-reference-review-20261115/bake"
DEFAULT_CONSUMER = Path("C:/Users/user/ssalddel/Assets/Ssalddel/WindowsGame/Resources")
DEFAULT_CODES = ["1126057500", "1126056500", "1121571000", "1123051500", "1123053300", "1135056000"]
EDITION = "candidate-2026"
REVISION = "candidate-2026:regional:r4"
sys.path.insert(0, str(ROOT / "artifacts/local/public-data/gis-runtime-r1"))

import shapely
from pyproj import Transformer
from shapely.affinity import affine_transform
from shapely.geometry import LineString, shape
from shapely.ops import transform, unary_union
import dong_hdio_mesh
import dong_diorama_reference
from dong_diorama_reference import DEFAULT_PROFILE, load_profile
from dong_hdio_mesh import Mesh, extrusion, polygons, ring_values


def digest(data):
    return hashlib.sha256(data).hexdigest()


def serialized(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode()


def read_checked(path, expected):
    raw = path.read_bytes()
    if digest(raw) != expected.lower():
        raise ValueError(f"파일 지문 불일치 {path}")
    return raw


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as stream:
        tmp = Path(stream.name)
        stream.write(data)
    try:
        os.replace(tmp, path)
    finally:
        tmp.unlink(missing_ok=True)


def indexed(features, key):
    result = {row[key]: row for row in features}
    if len(result) != len(features):
        raise ValueError(f"중복 {key}")
    return result


def load_module(meta):
    layers = {}
    for info in meta["layers"]:
        data = read_checked(ROOT / info["path"], info["sha256"])
        if len(data) != info["bytes"]:
            raise ValueError(info["path"])
        layers[info["key"]] = json.loads(gzip.decompress(data) if info["path"].endswith(".gz") else data)
    building_sha = next(i["sha256"] for i in meta["layers"] if i["key"] == "buildings")
    if layers["parcels"]["buildingGeometrySha256"] != building_sha:
        raise ValueError("필지 자료와 건물 도형 판본이 다름")
    return layers


def building_identity_ambiguities(catalog):
    """현재 묶음의 크기와 관계없이 65동 전체에서 같은 도형은 항상 같은 ID를 쓴다."""
    count_ids, count_rows = Counter(), Counter()
    for entry in catalog.values():
        meta = json.loads(read_checked(SOURCE / entry["path"], entry["sha256"]))
        info = next(x for x in meta["layers"] if x["key"] == "buildings")
        geoms = json.loads(gzip.decompress(read_checked(ROOT / info["path"], info["sha256"])))
        for feature in geoms["features"]:
            props = feature["properties"]
            count_ids[props["sourceId"]] += 1
            count_rows[props["sourceRowOneBased"]] += 1
    return {k for k,v in count_ids.items() if v > 1}, {k for k,v in count_rows.items() if v > 1}


def source_building_id(props, code, ambiguities):
    source_id = props["sourceId"]
    row = props["sourceRowOneBased"]
    ident = "vworld:al-d010:" + source_id
    if source_id in ambiguities[0]:
        ident += f":record:{row}"
    if row in ambiguities[1]:
        ident += f":dong:{code}"
    return ident


def height_of(props, style):
    measured = props["observedHeightMeters"]
    if measured is not None and measured > 0:
        return measured, "height"
    if props["aboveGroundFloorCount"]:
        return props["aboveGroundFloorCount"] * style["floorHeightMetres"], "floors"
    return style["fallbackHeightMetres"], "symbolic"


def bake(meta, entry_sha, layers, frame, ambiguities, profile):
    lat0, lon0 = frame["originLatitude"], frame["originLongitude"]
    mx, mz = frame["metresPerDegreeLongitude"], frame["metresPerDegreeLatitude"]
    to_wgs = Transformer.from_crs(5186,4326,always_xy=True)
    def local(g):
        return affine_transform(g, [mx,0,0,mz,-mx*lon0,-mz*lat0])
    def plane(g):
        return local(transform(to_wgs.transform,g))

    code = meta["code"]
    region = "region:kr:hjd:" + code
    name = meta["name"].replace(".","·")
    boundary_wgs = shape(layers["boundary"]["geometry"])
    rings = list(polygons(boundary_wgs))
    if len(rings) != 1 or len(rings[0].interiors):
        raise ValueError(f"이 판의 Unity 외곽 계약으로는 표현할 수 없는 경계 {code}")
    boundary = local(boundary_wgs)
    mesh, shadow = Mesh(), Mesh()
    surfaces = profile["surfaces"]
    widths = profile["strokeHalfWidthMetres"]
    style = profile["buildings"]
    def flat_layer(geometry, key, tag=0):
        surface = surfaces[key]
        mesh.flat(geometry, surface["yMetres"], surface["rgb"], tag)
    flat_layer(boundary, "ground")
    structures = layers["buildings"]["features"]
    footprints = [local(shape(f["geometry"])) for f in structures]
    obstacles = unary_union(footprints)
    parks = [plane(shape(f["geometry"])) for f in layers["green"]["parkPlanning2018"]]
    flat_layer(unary_union(parks).intersection(boundary), "parkPlan", 2)
    streets = layers["streets"]
    ox, oy = streets["frame"]["originMetres"]
    scale = streets["frame"]["integerUnitMetres"]
    def street_path(row):
        return plane(LineString([(ox+x*scale,oy+y*scale) for x,y in row["pathMm"]]))
    def stroke(line, halfwidth):
        return line.buffer(halfwidth,cap_style="flat",join_style="bevel")
    def draw(strokes, key, within=None):
        geom = unary_union(strokes).intersection(boundary).difference(obstacles)
        if within is not None:
            geom = geom.intersection(within)
        flat_layer(geom, key)
        return geom

    walks = [r for r in streets["walkLinks"] if not set(r["structure"])-{"횡단보도"}]
    draw([stroke(street_path(r), widths["walkMixed"] if r["modeGroup"]=="pedestrianAndVehicle" else widths["walkOther"])
          for r in walks], "walk")
    roads = [local(shape(f["geometry"])) for f in layers["roads"]["features"]]
    draw([stroke(g, widths["roadEdge"]) for g in roads], "roadEdge")
    carriage = draw([stroke(g, widths["carriage"]) for g in roads], "carriage")
    draw([stroke(street_path(r), widths["lane"]) for r in streets["laneMarkingFragments"]], "lane", carriage)
    parcel = layers["parcels"]
    addresses = indexed(parcel["buildingAddresses"],"featureId")
    permits = defaultdict(set)
    for row in parcel["restaurantParcelCandidates"]:
        for fid in row["candidateGeometryIds"]:
            permits[fid].add(row["managementKey"])
    if set(addresses) != {f["id"] for f in structures} or set(permits) - set(addresses):
        raise ValueError(f"지번·음식점 참조가 건물 목록과 다름 {code}")
    index, heights = [], Counter()
    multipart, holes = 0, 0
    for f, footprint in zip(structures,footprints):
        p, fid = f["properties"], f["id"]
        height, reason = height_of(p, style)
        threshold = style["compressAboveMetres"]
        retained = height if height <= threshold else min(style["maxDisplayHeightMetres"], threshold+(height-threshold)*style["compressionRatio"])
        heights[reason] += 1
        # 경계에서 나뉜 같은 원본 건물은 두 동에서 같은 외관을 갖는다.
        seed = int(digest(str(p["sourceRowOneBased"]).encode())[:8],16)
        extrusion(mesh,shadow,footprint,height,retained,seed,style)
        a = addresses[fid]
        label = ""
        if a["parcelFieldsConsistent"]:
            label = f'{a["legalNameSource"].split()[-1]} {"산 " if a["landTypeSource"]=="산" else ""}{a["lotSource"]}'
        parts = sorted(polygons(footprint),key=lambda part:-part.area)
        row = {"id": source_building_id(p,code,ambiguities), "sourceFeatureId":fid,
               "sourceBuildingRow":p["sourceRowOneBased"],"parcelLabel":label,"parcelPermitCount":len(permits[fid]),
               "fill":False,"kind":"al-d010:"+(p["sourceCategoryCode"] or "unknown"),
               "height":round(retained,2),"heightSource":reason,"xz":ring_values(parts[0].exterior),
               "road":"","number":"","postcode":"","dong":name,"district":meta["district"],"city":"서울특별시",
               "name":"","region":region,"floors":p["aboveGroundFloorCount"] or 0,"area":round(p["sourceAreaSquareMeters"],1)}
        if len(parts)>1 or any(poly.interiors for poly in parts):
            row["pickPolygons"] = [{"xz":ring_values(poly.exterior),"holes":[{"xz":ring_values(r)} for r in poly.interiors]} for poly in parts]
        index.append(row)
        multipart += int(len(parts)>1)
        holes += sum(len(poly.interiors) for poly in parts)
    indexed(index,"id")
    bounds = boundary_wgs.bounds
    blob = (b"HDIO" + struct.pack("<i6d2i",2,lat0,lon0,*bounds,len(index),len(roads))
            +mesh.encode(True)+shadow.encode(False)+struct.pack("<5i",0,0,0,0,0))
    west,south,east,north = bounds
    entry = {"code":code,"regionId":region,"name":name,"district":meta["district"],"resource":"hongdal-dong-"+code,
             "bytes":len(blob),"sha256":digest(blob).upper(),"moduleSha256":entry_sha.upper(),"moduleContentHash":entry_sha.upper(),
             "buildings":len(index),"skippedBuildings":0,"roads":len(roads),"heightFromSource":heights["height"],
             "heightFromFloors":heights["floors"],"heightSymbolic":heights["symbolic"],"west":west,"south":south,
             "east":east,"north":north,"centerLatitude":boundary_wgs.centroid.y,"centerLongitude":boundary_wgs.centroid.x,
             "boundary":[v for lon,lat in list(rings[0].exterior.coords)[:-1] for v in (lat,lon)],
             "separatedEast":0,"separatedNorth":0,"vertices":len(mesh.xyz),"triangles":len(mesh.indices)//3,
             "permitKeys":len({k for keys in permits.values() for k in keys}),"surfaceWalkLinks":len(walks),
             "parkPlanFragments":len(parks),"sourceWalkLinks":len(streets["walkLinks"]),
             "sourceLaneFragments":len(streets["laneMarkingFragments"]),"buildingRowsWithParcelLabel":sum(bool(i["parcelLabel"]) for i in index),
             "multiPartFootprints":multipart,"interiorRings":holes}
    return blob, index, entry


def cache_module(cache, item, baseline, ambiguities, fingerprint, profile, profile_ref):
    code = item["code"]
    folder = cache / code
    meta = json.loads(read_checked(SOURCE / item["path"], item["sha256"]))
    if (meta["code"],meta["edition"],meta["clippingBoundaryDate"]) != (code,EDITION,"2026-10-01"):
        raise ValueError("코드와 경계 판본 불일치")
    # 매번 원천 해시를 검사해야 고정 판본이 성공한 것으로 잘못 보이지 않는다.
    layers = load_module(meta)
    receipt_path = folder / "receipt.json"
    if receipt_path.is_file():
        receipt = json.loads(receipt_path.read_bytes())
        if receipt.get("referenceProfile") != profile_ref:
            raise ValueError(f"다른 기준으로 구운 캐시입니다. 새 --cache 경로를 사용하세요: {folder}")
        if (receipt["bakerFingerprint"],receipt["moduleSha256"]) == (fingerprint,item["sha256"]):
            if set(receipt["cacheFiles"]) != {"mesh.bytes.gz", "index.json.gz"}:
                raise ValueError(f"동별 캐시 파일 목록 불일치 {code}")
            for name,sha in receipt["cacheFiles"].items():
                cached = read_checked(folder/name,sha)
                if code == profile["referenceDongCode"]:
                    key = "meshSha256" if name == "mesh.bytes.gz" else "buildingIndexSha256"
                    if digest(gzip.decompress(cached)) != profile["baseline"][key]:
                        raise ValueError(f"면목제3·8동 캐시와 기준 설정의 {key} 불일치")
            return receipt
    mesh,rows,entry = bake(meta,item["sha256"],layers,baseline,ambiguities,profile)
    index_data = serialized(rows)
    if code == profile["referenceDongCode"]:
        produced = {"sourceModuleSha256": item["sha256"], "meshSha256": digest(mesh),
                    "buildingIndexSha256": digest(index_data)}
        if produced != profile["baseline"]:
            raise ValueError(f"면목제3·8동 원본·메시·건물 색인이 기준 설정과 다릅니다. 생성 지문: {produced}")
    cache_files = {}
    for name,data in (("mesh.bytes.gz",mesh),("index.json.gz",index_data)):
        payload = gzip.compress(data,compresslevel=4,mtime=0)
        write(folder / name,payload)
        cache_files[name] = digest(payload)
    receipt = {"entry":entry,"code":code,"bakerFingerprint":fingerprint,"moduleSha256":item["sha256"],"referenceProfile":profile_ref,
               "cacheFiles":cache_files,"boundaryBasis":meta["boundaryBasis"],
               "inputs":{x["key"]:{"path":x["path"],"sha256":x["sha256"]} for x in meta["layers"]}}
    write(receipt_path,serialized(receipt))
    return receipt


def package(out, cache, receipts, baseline, old_hash, profile_ref):
    target = out / "Resources"
    if target.exists():
        raise ValueError(f"기존 묶음 경로입니다. 새 --output 폴더를 지정하세요: {target}")
    entries, records = [], []
    for receipt in receipts:
        if receipt["referenceProfile"] != profile_ref:
            raise ValueError("서로 다른 기준 설정으로 생성한 동이 한 묶음에 들어 있습니다.")
        entry = receipt["entry"]
        entries.append(entry)
        folder = cache / entry["code"]
        rows = json.loads(gzip.decompress(read_checked(folder/"index.json.gz",receipt["cacheFiles"]["index.json.gz"])))
        records.extend(rows)
    by_id = indexed(records,"id")
    indexed(records,"sourceFeatureId")
    roles = [r for r in baseline["roleMap"] if r["to"] in by_id]
    index = {"schema":"hongdal-building-index.v1","source":"국토교통부 GIS건물통합정보 AL_D010 · 지번과 인허가 동일 필지 후보",
             "sourceDate":"2026-08-09","licence":"내부 검토용","origin":{"latitude":baseline["originLatitude"],"longitude":baseline["originLongitude"]},
             "metresPerDegreeLatitude":baseline["metresPerDegreeLatitude"],"metresPerDegreeLongitude":baseline["metresPerDegreeLongitude"],
             "buildings":records}
    shared = serialized(index)
    manifest = {k:baseline[k] for k in ("schema","originLatitude","originLongitude","metresPerDegreeLatitude","metresPerDegreeLongitude")}
    manifest.update(scopeRevision=REVISION,scopeStableId="regional-review:r4",
                    referenceProfile=profile_ref,
                    scopeContentHash=digest(serialized([profile_ref,[(r["code"],r["moduleSha256"]) for r in receipts]])).upper(),
                    sourceVintage="2026-10-01 SGIS 가공 경계 후보 · 2026-08 건물/도로 · 2020 도보 · 2021 차선 · 2018 공원 계획면",
                    readiness="CandidateBoundaryInternalReview",distributionApproved=False,traversalReady=False,gameplayReady=False,
                    label="2026 경계 후보 · 행정동 내부 검토",attribution=receipts[0]["boundaryBasis"]+" · 국토교통부 · 서울시",
                    scopeModuleCount=len(entries),west=min(m["west"] for m in entries),south=min(m["south"] for m in entries),
                    east=max(m["east"] for m in entries),north=max(m["north"] for m in entries),buildingIndexResource="hongdal-dong-buildings",
                    buildingIndexSha256=digest(shared).upper(),modules=entries,roleMap=roles)
    with tempfile.TemporaryDirectory(dir=out) as staging:
        resources=Path(staging)/"Resources"
        write(resources/"hongdal-dong-buildings.json",shared)
        for receipt in receipts:
            entry=receipt["entry"]
            raw=gzip.decompress(read_checked(cache/entry["code"]/"mesh.bytes.gz",receipt["cacheFiles"]["mesh.bytes.gz"]))
            if digest(raw)!=entry["sha256"].lower():
                raise ValueError(f"캐시와 HDIO 지문 불일치 {entry['code']}")
            write(resources/(entry["resource"]+".bytes"),raw)
        data=serialized(manifest)
        write(resources/"hongdal-dong-modules.json",data)
        resources.rename(target)
    summary = {"scopeRevision":REVISION,"edition":EDITION,"consumerManifestSha256":old_hash,"referenceProfile":profile_ref,
               "manifestSha256":digest(data),"indexSha256":digest(shared),"packageBytes":sum(p.stat().st_size for p in target.iterdir()),
               "moduleCount":len(entries),"buildings":len(records),"sourceBuildingRows":len({r["sourceBuildingRow"] for r in records}),
               "roads":sum(m["roads"] for m in entries),"surfaceWalkLinks":sum(m["surfaceWalkLinks"] for m in entries),
               "parkPlanFragments":sum(m["parkPlanFragments"] for m in entries),"modules":entries,
               "actualUnityExecution":False}
    write(out/"receipt.json",serialized(summary))
    print(f"{len(entries)}개 동 조립, 건물 {len(records):,}조각, {summary['packageBytes']:,}B",flush=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    group=parser.add_mutually_exclusive_group()
    group.add_argument("--code",action="append",help="10자리 행정동 코드. 여러 개면 옵션을 반복한다.")
    group.add_argument("--all",action="store_true",help="65동 전체를 개별 캐시에 굽는다. 플레이어 묶음은 만들지 않는다.")
    group.add_argument("--list",action="store_true",help="선택 가능한 동을 출력한다.")
    parser.add_argument("--cache",type=Path,help="동별 압축 파일 보관 위치. 기본값은 --output 아래 modules다.")
    parser.add_argument("--output",type=Path,default=DEFAULT_OUT)
    parser.add_argument("--cache-only",action="store_true")
    parser.add_argument("--consumer",type=Path,default=DEFAULT_CONSUMER)
    parser.add_argument("--profile",type=Path,default=DEFAULT_PROFILE,help="면목제3·8동 공통 생성 설정 JSON")
    args=parser.parse_args()
    cache = args.cache or args.output/"modules"
    catalog_path=SOURCE/"catalog.json"
    catalog_raw=catalog_path.read_bytes()
    catalog=indexed(json.loads(catalog_raw)["editions"][EDITION],"code")
    if args.list:
        for code,item in catalog.items():
            print(code,item["district"],item["name"])
        return
    profile, profile_ref = load_profile(args.profile)
    reference = catalog.get(profile["referenceDongCode"])
    if reference is None or reference["sha256"] != profile["baseline"]["sourceModuleSha256"]:
        parser.error("원자료 목록의 기준 행정동과 설정에 기록한 지문이 일치하지 않습니다")
    codes=list(catalog) if args.all else (args.code or DEFAULT_CODES)
    if len(codes)!=len(set(codes)) or set(codes)-set(catalog):
        parser.error("현재 경계 후보의 고유 행정동 코드를 지정하세요")
    if not args.all and not args.cache_only and (args.output/"Resources").exists():
        parser.error("다른 --output 경로를 사용하세요. 기존 실행용 묶음은 보존합니다.")
    baseline_raw=(args.consumer/"hongdal-dong-modules.json").read_bytes()
    baseline=json.loads(baseline_raw)
    read_checked(args.consumer/(baseline["buildingIndexResource"]+".json"),baseline["buildingIndexSha256"])
    fingerprint=digest(serialized([digest(Path(__file__).read_bytes()),digest(Path(dong_hdio_mesh.__file__).read_bytes()),
                                   digest(Path(dong_diorama_reference.__file__).read_bytes()),profile_ref["sha256"],
                                   digest(baseline_raw),digest(catalog_raw),shapely.__version__]))
    ambiguities=building_identity_ambiguities(catalog)
    # 다른 동 하나만 뽑아도 이 생성기가 기준동과 같은 결과를 내는지 먼저 확인한다.
    reference_receipt = cache_module(cache, reference, baseline, ambiguities, fingerprint, profile, profile_ref)
    receipts=[]
    for code in codes:
        r = (reference_receipt if code == profile["referenceDongCode"] else
             cache_module(cache,catalog[code],baseline,ambiguities,fingerprint,profile,profile_ref))
        receipts.append(r)
        print(f"{code} {r['entry']['name']} 건물 {r['entry']['buildings']:,}",flush=True)
    if not args.all and not args.cache_only:
        args.output.mkdir(parents=True,exist_ok=True)
        package(args.output,cache,receipts,baseline,digest(baseline_raw),profile_ref)
    else:
        index={"scopeRevision":REVISION,"codes":codes,"moduleCount":len(receipts),"bakerFingerprint":fingerprint,"referenceProfile":profile_ref,
               "modules":[{"code":r["code"],"path":r["code"]+"/receipt.json","sha256":digest(serialized(r))} for r in receipts]}
        name="catalog-all.json" if args.all else "catalog-"+digest(serialized(codes))[:12]+".json"
        write(cache/name,serialized(index))


if __name__=="__main__":
    main()
