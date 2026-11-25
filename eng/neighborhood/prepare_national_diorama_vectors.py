"""행정동 후보 경계와 전국 차량 도로·하천 원본을 지역별로 찾아 절단한다."""
import argparse
import csv
import gzip
import hashlib
import io
import json
import math
import re
import sqlite3
import struct
import sys
import zipfile
from collections import Counter, defaultdict
from contextlib import ExitStack, contextmanager
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "artifacts/local/public-data"
OUT = DATA / "nationwide-diorama-vectors-20261116-r1"
sys.path.insert(0, str(DATA / "gis-runtime-r1"))
import pyproj
import shapefile
import shapely
import numpy as np
from shapely.geometry import shape

from sagajeong_spatial_presentation import _dbf_text, _read_dbf_header, _read_exact

ROADS = DATA / "its-nodelink-20260914-r1"
BOUNDARY = DATA / "map-precision-boundary-20261116-r1"
WATER = DATA / "map-precision-terrain-rights-20261116-r1"
LAYERS = ("roads", "water")
ROAD_FIELDS = ("LINK_ID", "F_NODE", "T_NODE", "LANES", "ROAD_RANK", "ROAD_TYPE", "ROAD_NAME", "UPDATEDATE")
WATER_FIELDS = ("UFID", "SCLS", "FMTA")
NODE_FIELDS = ("NODE_ID", "NODE_TYPE")
DATABASE = "national-vectors.sqlite"


def sha(path):
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, obj):
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tmp.replace(path)


def rel(path):
    return path.relative_to(ROOT).as_posix()


def inputs():
    b = read(BOUNDARY / "receipt.json")
    t = read(ROADS / "acquisition.json")
    w = read(WATER / "receipt/water-normalization.json")
    paths = {"boundary": ROOT / b["national"]["localGzip"], "roads": ROADS / t["downloadedFile"]["file"],
             "water": ROOT / w["raw"]["path"]}
    expected = {"boundary": b["national"]["gzipSha256"], "roads": t["downloadedFile"]["sha256"],
                "water": w["raw"]["sha256"]}
    for layer, path in paths.items():
        if sha(path) != expected[layer].lower():
            raise ValueError(f"원본 지문 불일치 {path}")
    return paths, {
        "boundary": dict(archive=rel(paths["boundary"]), sha256=expected["boundary"].lower(),
                         decodedSha256=b["national"]["sha256"], date=b["geometryDate"], source=b["national"]["url"],
                         attribution=b["originalRights"], sourceRole=b["role"], crs="OGC:CRS84"),
        "roads": dict(archive=rel(paths["roads"]), sha256=expected["roads"].lower(),
                      catalogDate=t["catalogEntry"]["catalogVisibleDate"], source=t["sourcePageUrl"],
                      officialFileName=t["catalogEntry"]["officialFileName"], rights=t["license"]["value"]),
        "water": dict(archive=rel(paths["water"]), sha256=expected["water"].lower(), providerUpdated=w["providerUpdatedDate"],
                      observationDate=w["observationDate"], source=w["acquisition"]["requestUrl"],
                      rights=w["licenseCode"], attribution=w["attribution"])}


def boundaries(db, path, provenance):
    with gzip.open(path, "rb") as f:
        blob = f.read()
    if hashlib.sha256(blob).hexdigest() != provenance["decodedSha256"]:
        raise ValueError("경계 GeoJSON 지문 불일치")
    collection = json.loads(blob)
    if collection["crs"]["properties"]["name"] != "urn:ogc:def:crs:OGC:1.3:CRS84":
        raise ValueError("경계 좌표계 불일치")
    db.execute("CREATE TABLE regions(code TEXT PRIMARY KEY, level TEXT, name TEXT, parent TEXT, "
               "statistical TEXT, member_count INTEGER, geometry BLOB)")
    names, stats, parents, groups = {}, {}, {}, defaultdict(list)
    existing = set()
    for f in collection["features"]:
        p = f["properties"]
        code, gu, province, stat = (p[k] for k in ("adm_cd2", "sgg", "sido", "adm_cd"))
        if not (re.fullmatch(r"[0-9]{10}", code) and code.startswith(gu) and gu.startswith(province)
                and len(gu) == 5 and len(province) == 2 and re.fullmatch(r"[0-9]{8}", stat)):
            raise ValueError("행정/통계 코드 형식 오류 " + code)
        if stat in existing:
            raise ValueError("중복 통계 코드 " + stat)
        existing.add(stat)
        geom = shape(f["geometry"])
        if geom.is_empty or not geom.is_valid or geom.geom_type not in ("Polygon", "MultiPolygon"):
            raise ValueError("경계 도형 검토 필요 " + code)
        db.execute("INSERT INTO regions VALUES(?,?,?,?,?,?,?)",
                   (code, "dong", p["adm_nm"], gu, stat, 1, geom.wkb))
        for k, label, parent in [(gu, p["sggnm"], province), (province, p["sidonm"], None)]:
            if names.setdefault(k, label) != label or parents.setdefault(k, parent) != parent:
                raise ValueError("집계 지역 속성 불일치 " + k)
        groups[gu].append(geom)
        stats[province] = stats.get(province, 0) + 1
    provinces = defaultdict(list)
    for gu, geoms in sorted(groups.items()):
        merged = shapely.union_all(geoms)
        provinces[parents[gu]].append(merged)
        db.execute("INSERT INTO regions VALUES(?,?,?,?,?,?,?)",
                   (gu, "sgg", names[gu], parents[gu], None, len(geoms), merged.wkb))
    for code, geoms in sorted(provinces.items()):
        merged = shapely.union_all(geoms)
        db.execute("INSERT INTO regions VALUES(?,?,?,?,?,?,?)",
                   (code, "sido", names[code], None, None, stats[code], merged.wkb))
    return {"dong": len(existing), "sgg": len(groups), "sido": len(provinces), "seoulDong": stats["11"]}


@contextmanager
def records(archive, stem, chosen, expected_type, encoding):
    with archive.open(stem + ".dbf") as dbf_zip, archive.open(stem + ".shp") as shp_zip:
        with io.BufferedReader(dbf_zip, 1048576) as dbf, io.BufferedReader(shp_zip, 1048576) as shp:
            count, _, width, fields = _read_dbf_header(dbf)
            columns = [fields[k] for k in chosen]
            header = _read_exact(shp, 100, "ShpHeader")
            if struct.unpack_from(">i", header)[0] != 9994 or struct.unpack_from("<i", header, 32)[0] != expected_type:
                raise ValueError("SHP 헤더 형식 오류 " + stem)
            extent = struct.unpack_from("<4d", header, 36)

            def rows():
                for n in range(1, count + 1):
                    seq, words = struct.unpack(">ii", _read_exact(shp, 8, "ShpRowHeader"))
                    payload = _read_exact(shp, words * 2, "ShpRow")
                    attr = _read_exact(dbf, width, "DbfRow")
                    if n != seq:
                        raise ValueError(f"{stem} SHP 순번 불일치 {n}/{seq}")
                    if attr[:1] == b"*":
                        yield n, None, None, "DeletedDbf"
                        continue
                    attrs = tuple(_dbf_text(attr, col, encoding) for col in columns)
                    actual_type, = struct.unpack_from("<i", payload)
                    if actual_type not in (0, expected_type):
                        raise ValueError(f"{stem} 미지원 도형 {actual_type}")
                    yield n, payload, attrs, "NullShape" if actual_type == 0 else None
                if shp.read(1):
                    raise ValueError("SHP 행수 초과 " + stem)
            yield count, extent, rows()


def ingest(db, path, layer, stem, columns, shape_type, encoding):
    db.execute(f"CREATE TABLE {layer}(src_row INTEGER PRIMARY KEY," +
               ",".join(f"{k} TEXT" for k in columns) +
               (",x REAL,y REAL)" if layer == "nodes" else ",shp_xy BLOB)"))
    db.execute("CREATE TABLE IF NOT EXISTS withheld(layer TEXT,src_row INTEGER,reason TEXT,PRIMARY KEY(layer,src_row))")
    if layer != "nodes":
        db.execute(f"CREATE VIRTUAL TABLE {layer}_bbox USING rtree(src_row,xmin,xmax,ymin,ymax)")
    inserts = f"INSERT INTO {layer} VALUES(" + ",".join("?" for _ in range(len(columns)+(3 if layer=="nodes" else 2))) + ")"
    buffer, bounds, held = [], [], Counter()

    def flush():
        db.executemany(inserts, buffer)
        if bounds:
            db.executemany(f"INSERT INTO {layer}_bbox VALUES(?,?,?,?,?)", bounds)
        buffer.clear()
        bounds.clear()

    with zipfile.ZipFile(path) as archive:
        if layer in ("roads", "nodes") and archive.read(stem+".cpg").strip() != b"949":
            raise ValueError("도로 인코딩 변경")
        wkt = archive.read(stem+".prj").decode("ascii")
        crs = pyproj.CRS.from_wkt(wkt)
        if (not crs.is_projected or (layer == "water" and crs.to_epsg() != 5179)
                or (layer != "water" and crs.name != "ITRF2000_Central_Belt_60")):
            raise ValueError(f"{stem} 좌표계 확인 필요 {crs}")
        db.execute("CREATE TABLE IF NOT EXISTS native_crs(layer TEXT PRIMARY KEY,wkt TEXT)")
        db.execute("INSERT INTO native_crs VALUES(?,?)", (layer, wkt))
        with records(archive, stem, columns, shape_type, encoding) as (count, extent, rows):
            for n, payload, attrs, reason in rows:
                if not reason:
                    coords = struct.unpack_from("<2d" if layer=="nodes" else "<4d", payload, 4)
                    if not all(map(math.isfinite, coords)):
                        reason = "NonFiniteBounds"
                if reason:
                    db.execute("INSERT INTO withheld VALUES(?,?,?)", (layer, n, reason))
                    held[reason] += 1
                    continue
                if layer == "nodes":
                    buffer.append((n, *attrs, *coords))
                else:
                    xmin, ymin, xmax, ymax = coords
                    if xmin > xmax or ymin > ymax:
                        raise ValueError(f"{stem}:{n} 뒤집힌 범위")
                    buffer.append((n, *attrs, payload))
                    bounds.append((n, xmin, xmax, ymin, ymax))
                if len(buffer) == 4096:
                    flush()
            flush()
    id_field = columns[0]
    db.execute(f"CREATE INDEX {layer}_source_id ON {layer}({id_field})")
    indexed, missing, distinct = db.execute(
        f"SELECT count(*),sum({id_field}=''),count(distinct nullif({id_field},'')) FROM {layer}"
    ).fetchone()
    if indexed + held.total() != count:
        raise ValueError("원본 수량 보존 실패 " + layer)
    return dict(sourceRows=count, indexedRows=indexed, excludedAtIntake=dict(held), missingKey=missing or 0,
                duplicateNonemptyKeyRows=indexed-(missing or 0)-distinct, shapeType=shape_type,
                bbox=extent, crs=crs.name, epsg=crs.to_epsg(), nativeWkt=wkt, shapeMember=stem+".shp", properties=columns)


def decode(payload, layer):
    """SHP XY 부분과 원본 링 순서를 유지한다. 다중 외곽/구멍은 pyshp 규칙으로 읽는다."""
    n_parts, n_points = struct.unpack_from("<ii", payload, 36)
    end = 44 + n_parts * 4
    if n_parts < 1 or n_points < 1 or end + 16 * n_points != len(payload):
        raise ValueError("SHP XY 길이 불일치")
    parts = struct.unpack_from(f"<{n_parts}i", payload, 44)
    points = list(struct.iter_unpack("<dd", payload[end:]))
    value = shapefile.Shape(shapeType=3 if layer == "roads" else 5, points=points, parts=parts)
    return shape(value.__geo_interface__)


def clip(original, cover, layer):
    if not cover.intersects(original):
        return None
    cut = original if cover.covers(original) else original.intersection(cover)
    types = ("LineString", "MultiLineString") if layer == "roads" else ("Polygon", "MultiPolygon")
    if cut.geom_type == "GeometryCollection":
        pieces = [g for g in cut.geoms if g.geom_type in types and not g.is_empty]
        if not pieces:
            return None
        cut = shapely.union_all(pieces)
    if cut.is_empty or cut.geom_type not in types or (cut.length if layer == "roads" else cut.area) <= 0:
        return None
    return cut


@contextmanager
def feature_writer(path):
    tmp = path.with_suffix(".tmp")
    with tmp.open("wb") as raw, gzip.GzipFile(filename="", fileobj=raw, mode="wb", compresslevel=4, mtime=0) as stream:
        stream.write(b'{"type":"FeatureCollection","features":[')
        count = 0

        def append(obj):
            nonlocal count
            if count:
                stream.write(b",")
            stream.write(obj.encode("utf-8"))
            count += 1
        yield append
        stream.write(b"]}\n")
    tmp.replace(path)


def feature(identity, props, geometry):
    prefix = json.dumps(dict(type="Feature", id=identity, properties=props), separators=(",", ":"), ensure_ascii=False)
    return prefix[:-1] + ',"geometry":' + shapely.to_geojson(geometry) + "}"


def bbox_sql(layer):
    return f" FROM {layer}_bbox b JOIN {layer} r ON r.src_row=b.src_row WHERE b.xmin<=? AND b.xmax>=? AND b.ymin<=? AND b.ymax>=?"


def bbox_args(geometry):
    xmin,ymin,xmax,ymax=geometry.bounds
    return xmax,xmin,ymax,ymin


def extract(db, code, out):
    base = db.execute("SELECT * FROM regions WHERE code=?", (code,)).fetchone()
    if base is None:
        raise ValueError("등록되지 않은 행정 구역 " + code)
    _, level, name, _, _, member_count, blob = base
    folder = out / "regions" / code
    folder.mkdir(exist_ok=True, parents=True)
    geom = shapely.from_wkb(blob)
    if level == "dong":
        children = [base]
    elif level == "sgg":
        children = db.execute("SELECT * FROM regions WHERE parent=? ORDER BY code", (code,))
    else:
        children = db.execute("SELECT * FROM regions WHERE level='dong' AND substr(code,1,2)=? ORDER BY code", (code,))
    summary = dict(code=code, level=level, name=name, files={}, withheld={})
    with feature_writer(folder/"boundaries.geojson.gz") as put:
        children_seen = 0
        for dong, _, full_name, parent, stat, _, child_blob in children:
            put(feature(dong, dict(adm_cd2=dong, adm_cd=stat, sgg=parent, sido=dong[:2], adm_nm=full_name),
                        shapely.from_wkb(child_blob)))
            children_seen += 1
    if children_seen != member_count:
        raise ValueError("경계 소속 수량 불일치")
    summary["dongCount"] = children_seen
    for layer in LAYERS:
        native = pyproj.CRS.from_wkt(db.execute("SELECT wkt FROM native_crs WHERE layer=?", (layer,)).fetchone()[0])
        forward = pyproj.Transformer.from_crs(4326, native, always_xy=True).transform
        reverse = pyproj.Transformer.from_crs(native, 4326, always_xy=True).transform
        cover = shapely.transform(geom, forward, interleaved=False)
        shapely.prepare(cover)
        columns = ROAD_FIELDS if layer=="roads" else WATER_FIELDS
        selected = ",".join("r."+k for k in columns)
        sql = "SELECT r.src_row," + selected + ",r.shp_xy" + bbox_sql(layer) + " ORDER BY r.src_row"
        counts = Counter()
        review = []
        with feature_writer(folder/(layer+".geojson.gz")) as put:
            for src_row, *attributes, payload in db.execute(sql, bbox_args(cover)):
                counts["bboxCandidates"] += 1
                src = decode(payload, layer)
                if src.is_empty or not src.is_valid:
                    review.append(dict(sourceRow=src_row, reason=shapely.is_valid_reason(src)))
                    continue
                cut = clip(src, cover, layer)
                if cut is None:
                    counts["outsideOrOnlyTouch"] += 1
                    continue
                geographic = shapely.transform(cut, reverse, interleaved=False)
                if not geographic.is_valid:
                    review.append(dict(sourceRow=src_row, reason="AfterTransform:"+shapely.is_valid_reason(geographic)))
                    continue
                props = dict(zip(columns, attributes))
                props["sourceRowOneBased"] = src_row
                put(feature(f"{layer}:{src_row}", props, geographic))
                counts["exportedRows"] += 1
        counts["withheldInvalid"] = len(review)
        if counts["bboxCandidates"] != counts["exportedRows"] + counts["outsideOrOnlyTouch"] + len(review):
            raise ValueError("도형 추출 수량 불일치")
        summary[layer] = {k: counts[k] for k in ("bboxCandidates", "exportedRows", "outsideOrOnlyTouch", "withheldInvalid")}
        summary["withheld"][layer] = review
    for name in ("boundaries", *LAYERS):
        path = folder/(name+".geojson.gz")
        summary["files"][path.name] = sha(path)
    write_json(folder/"review.json", summary)
    return summary


def build(out):
    paths, sources = inputs()
    out.mkdir(parents=True, exist_ok=True)
    tmp = out / (DATABASE+".tmp")
    if tmp.exists():
        tmp.unlink()
    db = sqlite3.connect(tmp)
    try:
        db.execute("PRAGMA journal_mode=OFF")
        db.execute("PRAGMA synchronous=OFF")
        db.execute("PRAGMA cache_size=-96000")
        counts = boundaries(db, paths["boundary"], sources["boundary"])
        print("행정구역", counts, flush=True)
        for layer, stem, fields, kind, enc in (("nodes", "MOCT_NODE", NODE_FIELDS, 1, "cp949"),
                                                ("roads", "MOCT_LINK", ROAD_FIELDS, 3, "cp949"),
                                                ("water", "N3A_E0010001", WATER_FIELDS, 5, "utf-8")):
            counts[layer] = ingest(db, paths["roads" if layer=="nodes" else layer], layer, stem, fields, kind, enc)
            db.commit()
            print(layer, counts[layer]["indexedRows"], flush=True)
        for field in ("F_NODE", "T_NODE"):
            counts["roads"]["missing"+field] = db.execute(
                f"SELECT count(*) FROM roads r WHERE NOT EXISTS (SELECT 1 FROM nodes n WHERE n.NODE_ID=r.{field})"
            ).fetchone()[0]
        db.execute("CREATE TABLE source_info(layer TEXT PRIMARY KEY, receipt_json TEXT)")
        db.executemany("INSERT INTO source_info VALUES(?,?)", [(k, json.dumps(v, ensure_ascii=False)) for k,v in sources.items()])
        db.commit()
        if db.execute("PRAGMA quick_check").fetchone()[0] != "ok":
            raise ValueError("SQLite 확인 실패")
    finally:
        db.close()
    database = out / DATABASE
    tmp.replace(database)
    with sqlite3.connect(database.resolve().as_uri()+"?mode=ro", uri=True) as db:
        db.execute("PRAGMA cache_size=-64000")
        listing = out / "region-index.csv"
        with listing.with_suffix(".tmp").open("w", newline="", encoding="utf-8-sig") as f:
            writer = csv.writer(f)
            writer.writerow(("code","level","name","parent","statisticalCode","dongCount","west","south","east","north"))
            for *fields, blob in db.execute("SELECT * FROM regions ORDER BY code"):
                writer.writerow([*fields,*shapely.from_wkb(blob).bounds])
        listing.with_suffix(".tmp").replace(listing)
        seoul = []
        for code, in db.execute("SELECT code FROM regions WHERE parent='11' ORDER BY code").fetchall():
            seoul.append(extract(db, code, out))
            print("서울", code, seoul[-1]["roads"]["exportedRows"], seoul[-1]["water"]["exportedRows"], flush=True)
    summary = recheck_seoul(out, seoul)
    write_json(out/"seoul-summary.json", summary)
    files = {f.name:dict(bytes=f.stat().st_size,sha256=sha(f)) for f in (database, listing, out/"seoul-summary.json")}
    bytes_used = sum(f.stat().st_size for f in out.rglob("*") if f.is_file())
    if bytes_used > 2_000_000_000:
        raise ValueError("출력 예산 초과")
    write_json(out/"receipt.json", dict(schema="nationwide-diorama-vector-intake.v1", sources=sources,
        generator=rel(Path(__file__)),generatorSha256=sha(Path(__file__)), counts=counts, files=files,
        seoulDistrictReceipts={r["code"]:sha(out/"regions"/r["code"]/"review.json") for r in seoul},
        bytesBeforeReceipt=bytes_used, distributionApproved=False, unityApplied=False,
        scope="Republic of Korea. Source administrative-dong candidates with source parent codes; sgg/sido are dissolved from candidate dongs. They are not legal-dong or an official current administrative boundary.",
        selection="R-tree bounding boxes in native metric CRS are a first pass. Region extraction checks positive line length or polygon area after exact clip against the projected candidate boundary. Invalid candidate geometries are reported by source row and withheld. Source geometry and IDs are preserved in the database once per source row, including parallel/directed links. A cross-boundary record can occur in multiple local views.",
        limitations=["MOCT_LINK is a directed road traffic network. Small alleys, footpaths, driveability and street width cannot be inferred from visual intersection. F_NODE/T_NODE refer to original endpoints, not local clipped endpoints. No links or pedestrian network were synthesized.",
                     "Water is N3A_E0010001 river-boundary polygons. It is not coastline, ocean, flood limits or every water feature. National index is dependent on the source archive coverage. Validity is checked on actual extraction; prebuilt Seoul regional products include their rejection lists.",
                     "Source dates differ; region attribution comes only from the selected dated geometry. Ascertain local and public release rights before distribution."],
        extractCommand="python eng/neighborhood/prepare_national_diorama_vectors.py extract --region <2 digit sido | 5 digit sgg | 10 digit administrative dong>",
        verifyCommand="python eng/neighborhood/prepare_national_diorama_vectors.py verify"))


def recheck_seoul(out, reviews):
    ids = {k: Counter() for k in ("boundaries", *LAYERS)}
    check = []
    for r in reviews:
        entry = {"sgg":r["code"],"name":r["name"]}
        for k in ids:
            file = out/"regions"/r["code"]/(k+".geojson.gz")
            if sha(file) != r["files"][file.name]:
                raise ValueError("재조회 지문 불일치 "+str(file))
            with gzip.open(file, "rt", encoding="utf-8") as f:
                collection = json.load(f)
            keys = [v["id"] for v in collection["features"]]
            if len(keys) != len(set(keys)) or len(keys) != (r["dongCount"] if k=="boundaries" else r[k]["exportedRows"]):
                raise ValueError("지역 내 중복·수량 오류 "+str(file))
            ids[k].update(keys)
            entry[k]=len(keys)
        check.append(entry)
    if len(check)!=25 or len(ids["boundaries"])!=427:
        raise ValueError("서울 25구/427동 확인 실패")
    return dict(districts=check, counts={k:dict(occurrences=sum(c.values()),unique=len(c),
                crossingMultipleDistricts=sum(n>1 for n in c.values())) for k,c in ids.items()})


def source_shapes(rows, layer):
    if layer == "water":
        return [decode(r[2], layer) for r in rows]
    wkbs = []
    for _, _, payload in rows:
        parts, points = struct.unpack_from("<ii", payload, 36)
        if parts == 1 and points >= 2 and len(payload) == 48 + points * 16 and payload[44:48] == bytes(4):
            # SHP XY and little-endian WKB LineString use identical double pairs.
            wkbs.append(b"\x01\x02\x00\x00\x00" + payload[40:44] + payload[48:])
        else:
            wkbs.append(decode(payload, layer).wkb)
    return shapely.from_wkb(wkbs)


def sido_memberships(db, layer, sinks, held, province_counts):
    native = pyproj.CRS.from_wkt(db.execute("SELECT wkt FROM native_crs WHERE layer=?", (layer,)).fetchone()[0])
    project = pyproj.Transformer.from_crs(4326, native, always_xy=True).transform
    covers, codes = [], []
    for code, wkb in db.execute("SELECT code,geometry FROM regions WHERE level='sido' ORDER BY code"):
        geom = shapely.transform(shapely.from_wkb(wkb), project, interleaved=False)
        if not geom.is_valid:
            raise ValueError("시도 투영 경계 오류 "+code)
        shapely.prepare(geom)
        codes.append(code)
        covers.append(geom)
    tree = shapely.STRtree(covers)
    cells = {}
    counts = Counter()
    key_field = "LINK_ID" if layer=="roads" else "UFID"
    cursor = db.execute(f"SELECT src_row,{key_field},shp_xy FROM {layer} ORDER BY src_row")
    while rows := cursor.fetchmany(2048):
        geometries = source_shapes(rows, layer)
        valid = shapely.is_valid(geometries) & ~shapely.is_empty(geometries)
        positive = (shapely.length(geometries) if layer=="roads" else shapely.area(geometries)) > 0
        bounds = shapely.bounds(geometries)
        tiles = np.floor(bounds / 5000).astype(int)
        for j, (row, key, _) in enumerate(rows):
            counts["sourceRows"] += 1
            if not (valid[j] and positive[j]):
                counts["withheldInvalidOrZero"] += 1
                reason = "ZeroMeasure" if valid[j] else shapely.is_valid_reason(geometries[j])
                held.writerow((layer,row,key,reason))
                continue
            left,bottom,right,top = tiles[j]
            if left == right and bottom == top:
                cell = (left,bottom)
                if cell not in cells:
                    box = shapely.box(left*5000,bottom*5000,(left+1)*5000,(bottom+1)*5000)
                    whole, partial = [], []
                    for i in tree.query(box):
                        if covers[i].covers(box):
                            whole.append(i)
                        elif covers[i].intersects(box):
                            partial.append(i)
                    cells[cell] = (whole, partial)
                contained, check = cells[cell]
                members = list(contained)
            else:
                members = []
                check = tree.query(geometries[j])
            for i in check:
                if clip(geometries[j], covers[i], layer) is not None:
                    members.append(i)
            if not members:
                counts["outsideOrOnlyTouch"] += 1
                held.writerow((layer,row,key,"OutsideOrOnlyTouch"))
                continue
            counts["uniqueAssignedRows"] += 1
            counts["provinceOccurrences"] += len(members)
            counts["inMultipleProvinces"] += len(members) > 1
            for i in sorted(members):
                province_counts[codes[i]][layer] += 1
                sinks[codes[i]].writerow((layer,row,key))
    if counts["uniqueAssignedRows"] + counts["outsideOrOnlyTouch"] + counts["withheldInvalidOrZero"] != counts["sourceRows"]:
        raise ValueError("시도 귀속 수량 보존 실패")
    return {k:counts[k] for k in ("sourceRows","uniqueAssignedRows","provinceOccurrences","inMultipleProvinces",
                                   "outsideOrOnlyTouch","withheldInvalidOrZero")}


def provinces(out):
    receipt = read(out/"receipt.json")
    if sha(out/DATABASE) != receipt["files"][DATABASE]["sha256"]:
        raise ValueError("전국 공간 인덱스 버전 불일치")
    folder = out/"sido-index"
    folder.mkdir(exist_ok=True)
    with sqlite3.connect((out/DATABASE).resolve().as_uri()+"?mode=ro", uri=True) as db:
        labels = dict(db.execute("SELECT code,name FROM regions WHERE level='sido' ORDER BY code"))
        counters = {code:Counter() for code in labels}
        with ExitStack() as stack:
            def writer(name, header):
                data = stack.enter_context((folder/(name+".tmp")).open("wb"))
                gz = stack.enter_context(gzip.GzipFile(filename="",fileobj=data,mode="wb",mtime=0,compresslevel=4))
                f = stack.enter_context(io.TextIOWrapper(gz,encoding="utf-8-sig",newline=""))
                csv_file = csv.writer(f, lineterminator="\n")
                csv_file.writerow(header)
                return csv_file
            fields = ("layer","sourceRowOneBased","sourceId")
            sinks = {code:writer(code,fields) for code in labels}
            held = writer("withheld",(*fields,"reason"))
            results = {}
            for layer in LAYERS:
                results[layer] = sido_memberships(db, layer, sinks, held, counters)
                print("전국 시도별 교차", layer, results[layer], flush=True)
    files = {}
    for code in [*labels, "withheld"]:
        dest = folder/(code+".csv.gz")
        (folder/(code+".tmp")).replace(dest)
        files[dest.name] = sha(dest)
    result = dict(counts=results, regions={c:dict(name=labels[c],**counters[c]) for c in labels},
                  files=files, rows="Unique native features once per intersected province. File keys are source rows in national-vectors.sqlite, without geometry duplication.",
                  selection="Valid source geometry with positive line length or polygon area inside the projected province union. Containment of a whole 5 km box establishes inclusion; all potential edge cases use the actual source geometry. No boundary ownership is synthesized for uncovered offshore geometry.")
    write_json(folder/"summary.json", result)
    receipt["sidoMembership"] = dict(path="sido-index/summary.json", sha256=sha(folder/"summary.json"),counts=results)
    receipt["generatorSha256"] = sha(Path(__file__))
    receipt["bytesBeforeReceipt"] = sum(f.stat().st_size for f in out.rglob("*") if f.is_file() and f.name!="receipt.json")
    if receipt["bytesBeforeReceipt"] > 2_000_000_000:
        raise ValueError("출력 예산 초과")
    write_json(out/"receipt.json", receipt)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("build", "extract", "provinces", "verify"))
    parser.add_argument("--region")
    args = parser.parse_args()
    if args.action == "build":
        build(OUT)
        provinces(OUT)
        return
    if args.action == "provinces":
        provinces(OUT)
        return
    receipt = read(OUT/"receipt.json")
    if args.action == "extract":
        if not args.region or not re.fullmatch(r"(?:[0-9]{2}|[0-9]{5}|[0-9]{10})", args.region):
            parser.error("시도(2)·시군구(5)·행정동(10) 중 하나의 지역 코드가 필요합니다.")
        with sqlite3.connect((OUT/DATABASE).resolve().as_uri()+"?mode=ro", uri=True) as db:
            result = extract(db, args.region, OUT)
            print(json.dumps({k:result[k] for k in ("code","name","dongCount","roads","water")}, ensure_ascii=False))
        return
    for name, value in receipt["files"].items():
        if sha(OUT/name) != value["sha256"]:
            raise ValueError("출력 파일 지문 불일치 "+name)
    reviews = []
    for code, checksum in receipt["seoulDistrictReceipts"].items():
        path = OUT/"regions"/code/"review.json"
        if sha(path)!=checksum:
            raise ValueError("지역 판본 불일치 "+code)
        reviews.append(read(path))
    if recheck_seoul(OUT, reviews) != read(OUT/"seoul-summary.json"):
        raise ValueError("서울 재검산 실패")
    province = receipt["sidoMembership"]
    path = OUT/province["path"]
    if sha(path) != province["sha256"]:
        raise ValueError("시도 소속표 판본 불일치")
    result = read(path)
    for name, checksum in result["files"].items():
        if sha(path.parent/name)!=checksum:
            raise ValueError("시도 소속 파일 지문 불일치 "+name)
    print("전국 16개 시도와 서울 25개 구 재조회 완료.")


if __name__ == "__main__":
    main()
