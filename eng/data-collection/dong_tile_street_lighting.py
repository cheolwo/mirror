"""Collect two municipal lighting files and index four separate Seoul lighting sources."""

import argparse
from collections import Counter
from contextlib import closing
import csv
from dataclasses import dataclass
from datetime import datetime, timezone
from hashlib import sha256
from io import BytesIO, StringIO
import json
import math
from pathlib import Path
import re
import sqlite3
from tempfile import TemporaryDirectory
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen
from zipfile import ZipFile

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, SLOT_LABELS, point_key
from seoul_area_geography import area_shapes, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest, local_path

AREAS = PUBLIC / "seoul-area-evidence-20261125/seoul-area.sqlite"
EXISTING = PUBLIC / "sagajeong-urp-realism-20261124/official-data"
OUTPUT = PUBLIC / "dong-tile-intake-20261126/street-lighting"
PORTAL = "https://www.data.go.kr"
MUNICIPAL = {
    "15128093": ("gangdong-security", "11740", "2024-05-16", ("등번호", "위도값", "경도값")),
    "15128039": ("seocho-security", "11650", "2024-05-13", ("연번", "행정동", "도로명주소", "지번주소", "위도", "경도")),
}
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE metadata (key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE lamp_source (
    source_id TEXT PRIMARY KEY,dataset_id TEXT NOT NULL,source_path TEXT NOT NULL,
    source_sha256 TEXT NOT NULL,source_url TEXT NOT NULL,license_note TEXT NOT NULL,
    icon_label TEXT NOT NULL,category TEXT NOT NULL,publisher_scope TEXT NOT NULL,file_period TEXT,
    coordinate_basis TEXT NOT NULL
);
CREATE TABLE dong (code TEXT PRIMARY KEY,district_code TEXT NOT NULL,name TEXT NOT NULL);
CREATE TABLE child (
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    PRIMARY KEY(q,r,slot)
) WITHOUT ROWID;
CREATE TABLE light_site_reference (
    source_id TEXT NOT NULL REFERENCES lamp_source(source_id),
    representative_source_row INTEGER NOT NULL, dong_code TEXT NOT NULL REFERENCES dong(code),
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    rows_at_same_published_position INTEGER NOT NULL CHECK(rows_at_same_published_position>0),
    reported_quantity_rows INTEGER NOT NULL, reported_quantity_sum INTEGER,
    reference_period_min TEXT,reference_period_max TEXT,
    PRIMARY KEY(source_id,representative_source_row),FOREIGN KEY(q,r,slot) REFERENCES child(q,r,slot)
) WITHOUT ROWID;
CREATE INDEX light_sites_by_tile ON light_site_reference(q,r,slot,source_id);
CREATE VIEW v_light_child AS
    SELECT source_id,q,r,slot,count(*) AS distinct_reported_positions,
        sum(rows_at_same_published_position) AS source_rows,sum(reported_quantity_sum) AS reported_quantity_sum,
        min(reference_period_min) AS min_reference_period,max(reference_period_max) AS max_reference_period
    FROM light_site_reference GROUP BY source_id,q,r,slot;
CREATE VIEW v_light_parent AS
    SELECT source_id,q,r,sum(distinct_reported_positions) AS distinct_reported_positions,
        sum(source_rows) AS source_rows
    FROM v_light_child GROUP BY source_id,q,r;
CREATE VIEW v_light_dong AS
    SELECT p.source_id,p.dong_code,d.name,d.district_code,count(*) AS distinct_reported_positions,
        sum(p.rows_at_same_published_position) AS source_rows
    FROM light_site_reference p JOIN dong d ON d.code=p.dong_code GROUP BY p.source_id,p.dong_code;
"""


@dataclass(frozen=True)
class RawLamp:
    row: int
    lon: float
    lat: float
    date: str | None
    quantity: int | None
    district: str | None


def fetch(url, limit, data=None, referer=None):
    headers = {"User-Agent": "Mozilla/5.0"}
    if referer:
        headers.update({"Referer": referer, "Origin": PORTAL, "X-Requested-With": "XMLHttpRequest"})
    request = Request(url, data=urlencode(data).encode() if data else None, headers=headers)
    with urlopen(request, timeout=25) as response:
        parts = urlsplit(response.url)
        if response.status != 200 or parts.scheme != "https" or parts.hostname != "www.data.go.kr":
            raise ValueError("unexpected_public_portal_response")
        body = response.read(limit + 1)
    if len(body) > limit:
        raise ValueError("public_lighting_response_exceeds_size_limit")
    return body


def csv_content(body, expected, encoding=None):
    for candidate in ((encoding,) if encoding else ("utf-8-sig", "cp949")):
        try:
            iterator = csv.reader(StringIO(body.decode(candidate)))
        except UnicodeDecodeError:
            continue
        if tuple(next(iterator)) != expected:
            raise ValueError("lighting_csv_header_changed")
        rows = list(iterator)
        if not rows or any(len(row) != len(expected) for row in rows):
            raise ValueError("lighting_csv_row_shape_invalid")
        return rows, candidate
    raise ValueError("unsupported_lighting_csv_encoding")


def collect_municipal(output):
    raw = output / "raw"
    raw.mkdir(parents=True, exist_ok=True)
    for dataset, (key, _, period, header) in MUNICIPAL.items():
        catalog = f"{PORTAL}/data/{dataset}/fileData.do"
        metadata_url = f"{PORTAL}/catalog/{dataset}/fileData.json"
        metadata_body = fetch(metadata_url, 500_000)
        metadata = json.loads(metadata_body)
        license_entry = metadata["license"]
        license_name = license_entry["name"] if isinstance(license_entry, dict) else license_entry
        if str(metadata["identifier"]) != dataset or license_name != "이용허락범위 제한 없음":
            raise ValueError(f"municipal_lighting_catalog_license_changed: {dataset}")
        page = fetch(catalog, 750_000).decode("utf-8")
        match = re.search(r"fn_fileDataDown\('" + dataset
                          + r"',\s*'([^']+)',\s*'([^']*)',\s*'(\d+)',\s*'(\d+)'\)", page)
        if match is None:
            raise ValueError(f"municipal_lighting_public_download_button_not_available: {dataset}")
        detail, attachment, sequence, _ = match.groups()
        endpoint = "/tcs/dss/selectFileDataDownload.do"
        fields = dict(publicDataPk=dataset, publicDataDetailPk=detail, atchFileId=attachment,
                      fileDetailSn=sequence, publicDataTyCode="PR0051", url=endpoint)
        selection = json.loads(fetch(PORTAL + endpoint, 250_000, fields, catalog))
        if selection["status"] is not True or not re.fullmatch(r"FILE_\d+", selection["atchFileId"]):
            raise ValueError(f"municipal_lighting_public_file_selector_failed: {dataset}")
        title = selection["dataSetFileDetailInfo"]["dataNm"]
        if not title.endswith("_" + period.replace("-", "")):
            raise ValueError(f"municipal_lighting_file_date_changed: {dataset}")
        download = PORTAL + "/cmm/cmm/fileDownload.do?" + urlencode({
            "atchFileId": selection["atchFileId"], "fileDetailSn": selection["fileDetailSn"]})
        body = fetch(download, 8_000_000, referer=catalog)
        rows, encoding = csv_content(body, header)
        (raw / f"{key}.csv").write_bytes(body)
        (raw / f"{key}-catalog.json").write_bytes(metadata_body)
        atomic_json(raw / f"{key}-receipt.json", {
            "datasetId": dataset, "catalogUrl": catalog, "catalogMetadataUrl": metadata_url,
            "catalogMetadataSha256": sha256(metadata_body).hexdigest(), "downloadUrl": download,
            "sourceTitle": title, "sourceFilePeriod": period, "license": license_name,
            "retrievedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            "sha256": sha256(body).hexdigest(), "bytes": len(body), "rowCount": len(rows), "encoding": encoding,
        })


def check_file(path, fingerprint, size):
    if path.stat().st_size != size or digest(path) != fingerprint.lower():
        raise ValueError(f"lighting_reference_fingerprint_failed: {path.name}")
    return path


def degrees(text):
    try:
        return float(text)
    except (ValueError, TypeError):
        return math.nan


def road_file():
    receipt = json.loads((EXISTING / "seoul-street-lamps-source-receipt.json").read_text(encoding="utf-8"))
    path = check_file(EXISTING / receipt["file"], receipt["sha256"], receipt["bytes"])
    period = receipt["fileVersion"]
    rows, _ = csv_content(path.read_bytes(), ("관리번호", "위도", "경도"), "cp949")
    if len(rows) != receipt["rawRows"]:
        raise ValueError("road_lighting_row_count_differs_from_receipt")
    source = ("seoul-road-light", receipt["datasetId"], path, receipt["sourceUrl"],
              "Official portal notes unrestricted for the 2026-11-18 attachment",
              "가로등 위치 기록", "road_lamp_position", "서울시 도로사업소·서울시설공단 관리분", period,
              "Decimal latitude and longitude read as WGS84 in the prior receipt; independent survey accuracy unknown")
    return source, [RawLamp(i, degrees(row[2]), degrees(row[1]), None, None, None)
                    for i, row in enumerate(rows, start=1)]


def safe_walk_file(pyproj, shapely):
    import shapefile

    receipt = json.loads((EXISTING / "seoul-safe-walk-source-receipt.json").read_text(encoding="utf-8"))
    item, = (entry for entry in receipt["files"] if entry["path"].endswith(".zip"))
    path = check_file(EXISTING / item["path"], item["sha256"], item["bytes"])
    rows = []
    with ZipFile(path) as archive:
        stem, = (name[:-4] for name in archive.namelist() if name.lower().endswith(".shp"))
        crs = pyproj.CRS.from_wkt(archive.read(stem + ".prj").decode("ascii"))
        if crs.to_epsg() != 4326:
            raise ValueError("safe_walk_coordinate_system_changed")
        with shapefile.Reader(
            shp=BytesIO(archive.read(stem + ".shp")),
            shx=BytesIO(archive.read(stem + ".shx")),
            dbf=BytesIO(archive.read(stem + ".dbf")),
            encoding="cp949",
        ) as shapes:
            if shapes.numRecords != 11883 or shapes.shapeType != shapefile.POINT:
                raise ValueError("safe_walk_source_geometry_or_count_changed")
            seen = set()
            for i, entry in enumerate(shapes.iterShapeRecords(), start=1):
                if entry.record["시설코드"] != "305":
                    continue
                stable = entry.record["시설물 ID"]
                if stable in seen:
                    raise ValueError("duplicate_safe_walk_facility_id")
                seen.add(stable)
                point, = entry.shape.points
                attribute_point = shapely.from_wkt(entry.record["포인트 WKT"])
                if math.dist(point, (attribute_point.x, attribute_point.y)) > 1e-7:
                    raise ValueError("safe_walk_point_and_wkt_disagree")
                observed = entry.record["데이터 기"]
                try:
                    date = datetime.strptime(observed, "%Y%m%d").date().isoformat() if len(observed) == 8 else None
                except ValueError:
                    date = None
                rows.append(RawLamp(i, *point, date, int(entry.record["설치대수"]), entry.record["시군구 코"][:5]))
    source = ("safe-walk-security", receipt["datasetId"], path, receipt["sourceUrl"],
              "Portal 15112100 notes KOGL Type 1; this linked SHP remains internal review",
              "안심귀갓길 보안등 조사", "historical_walk_route_security", "서울 25구 안심귀갓길 표본", None,
              "WGS84 declared by SHP PRJ, independently checked against the WKT attribute")
    return source, rows


def municipal_files(output):
    for dataset, (key, district, period, header) in MUNICIPAL.items():
        raw = output / "raw"
        receipt = json.loads((raw / f"{key}-receipt.json").read_text(encoding="utf-8"))
        if (receipt["datasetId"], receipt["sourceFilePeriod"]) != (dataset, period):
            raise ValueError(f"municipal_lighting_receipt_dataset_or_date_changed: {dataset}")
        path = check_file(raw / f"{key}.csv", receipt["sha256"], receipt["bytes"])
        if digest(raw / f"{key}-catalog.json") != receipt["catalogMetadataSha256"]:
            raise ValueError("municipal_lighting_license_metadata_changed")
        rows, _ = csv_content(path.read_bytes(), header, receipt["encoding"])
        if len(rows) != receipt["rowCount"]:
            raise ValueError("municipal_lighting_csv_count_changed")
        name = "강동구" if district == "11740" else "서초구"
        source = (key, dataset, path, receipt["catalogUrl"], receipt["license"], f"{name} 보안등 위치 기록",
                  "historical_municipal_security", name, period,
                  "Published latitude and longitude interpreted as WGS84 for comparison; datum and survey accuracy unconfirmed")
        yield source, [RawLamp(i, degrees(row[-1]), degrees(row[-2]), None, None, district)
                       for i, row in enumerate(rows, start=1)]


def index_source(db, evidence, rows, parent_keys, code_to_district, tree, shape_codes, project, shapely):
    source_id, dataset, path, *metadata = evidence
    db.execute("INSERT INTO lamp_source VALUES(?,?,?,?,?,?,?,?,?,?,?)", (
        source_id, dataset, path.relative_to(ROOT).as_posix(), digest(path), *metadata))
    by_position = {}
    withheld = Counter()
    for row in rows:
        if not (math.isfinite(row.lon) and math.isfinite(row.lat) and 124 < row.lon < 132 and 32 < row.lat < 39):
            withheld["invalid_or_missing_coordinate"] += 1
            continue
        position = (row.lon, row.lat)
        if position in by_position:
            by_position[position].append(row)
        else:
            by_position[position] = [row]
    output = []
    local_points = []
    for position, coincident in by_position.items():
        x, y = project(*position, errcheck=True)
        point = shapely.Point(x, y)
        codes = sorted(shape_codes[index] for index in tree.query(point, predicate="covered_by"))
        if not codes:
            withheld["outside_official_seoul"] += len(coincident)
            continue
        claimed = {row.district for row in coincident if row.district is not None}
        if claimed:
            codes = [code for code in codes if claimed == {code_to_district[code]}]
            if not codes:
                withheld["reported_district_disagrees_with_official_shape"] += len(coincident)
                continue
        q, r, slot = point_key(x, y)
        if (q, r) not in parent_keys:
            withheld["outside_canonical_parent"] += len(coincident)
            continue
        dates = [row.date for row in coincident if row.date is not None]
        quantities = [row.quantity for row in coincident if row.quantity is not None]
        output.append((source_id, coincident[0].row, codes[0], q, r, slot, len(coincident), len(quantities),
                       sum(quantities) if quantities else None, min(dates) if dates else None,
                       max(dates) if dates else None))
        local_points.append(point)
    db.executemany("INSERT INTO light_site_reference VALUES(?,?,?,?,?,?,?,?,?,?,?)", output)
    accepted_rows = sum(row[6] for row in output)
    if accepted_rows + sum(withheld.values()) != len(rows):
        raise ValueError(f"lighting_source_rows_not_conserved: {source_id}")
    record_dates = [row.date for row in rows if row.date is not None]
    return {"rawSourceRows": len(rows), "rowsInTileIndex": accepted_rows, "distinctPositionGroups": len(output),
            "coincidentExtraSourceRows": accepted_rows - len(output), "withheld": dict(withheld),
            "recordDateRange": [min(record_dates), max(record_dates)] if record_dates else None,
            "rowsWithoutIndividualReferenceDate": len(rows) - len(record_dates),
            "dongsWithPositions": len({row[2] for row in output}),
            "districtsWithPositions": len({code_to_district[row[2]] for row in output}),
            "childrenWithPositions": len({row[3:6] for row in output}),
            "observedDistricts": sorted({code_to_district[row[2]] for row in output})}, local_points


def build(output):
    pyproj, shapely = gis()
    with closing(read_only(AREAS)) as db:
        parents = set(db.execute("SELECT q,r FROM tile"))
        dongs = db.execute("SELECT code,district_code,name FROM admin_dong ORDER BY code").fetchall()
        boundary_path, boundary_sha = db.execute(
            "SELECT local_input,sha256 FROM source_release WHERE id='area-boundaries'").fetchone()
    path = local_path(ROOT / boundary_path)
    if digest(path) != boundary_sha:
        raise ValueError("lighting_official_boundary_fingerprint_failed")
    shapes = area_shapes(path, pyproj, shapely)
    if set(shapes) != {row[0] for row in dongs}:
        raise ValueError("lighting_official_boundary_keys_disagree")
    codes = sorted(shapes)
    tree = shapely.STRtree([shapes[key] for key in codes])
    district = dict((code, district) for code, district, _ in dongs)
    project = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    inputs = [road_file(), safe_walk_file(pyproj, shapely), *municipal_files(output)]
    results = {}
    positions = {}
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as stage:
        tmp = Path(stage) / "street-lighting-tile-index.sqlite"
        with closing(sqlite3.connect(tmp)) as db, db:
            db.executescript(SCHEMA)
            db.executemany("INSERT INTO metadata VALUES(?,?)",
                           (("grid_revision", REVISION), ("use_boundary", "internal_review_only"),
                            ("official_boundary_sha256", boundary_sha), ("cross_source_deduplication", "none")))
            db.executemany("INSERT INTO dong VALUES(?,?,?)", dongs)
            db.executemany("INSERT INTO child VALUES(?,?,?)",
                           ((q, r, slot) for q, r in sorted(parents) for slot in range(len(SLOT_LABELS))))
            for evidence, rows in inputs:
                results[evidence[0]], positions[evidence[0]] = index_source(
                    db, evidence, rows, parents, district, tree, codes, project, shapely)
            for key, facts in results.items():
                for view in ("v_light_child", "v_light_parent", "v_light_dong"):
                    total, = db.execute(f"SELECT sum(source_rows) FROM {view} WHERE source_id=?", (key,)).fetchone()
                    if total != facts["rowsInTileIndex"]:
                        raise ValueError(f"lighting_source_totals_do_not_match: {view}/{key}")
            if db.execute("PRAGMA foreign_key_check").fetchone() or db.execute("PRAGMA integrity_check").fetchone() != ("ok",):
                raise ValueError("lighting_index_integrity_check_failed")
        target = output / tmp.name
        tmp.replace(target)
    history = shapely.STRtree(positions["safe-walk-security"])
    nearby = {}
    for key in ("gangdong-security", "seocho-security"):
        pairs = history.query(positions[key], predicate="dwithin", distance=3)
        nearby[key] = {"municipalGroupsWithin3mOfSafeWalkPoint": len(set(pairs[0])),
                       "safeWalkGroupsWithin3mOfMunicipalPosition": len(set(pairs[1]))}
    summary = {
        "gridRevision": REVISION, "officialBoundarySha256": boundary_sha, "range": "Seoul 25 districts",
        "sources": results, "sameClassNearnessOnly": nearby, "databaseSha256": digest(target),
        "sampleGaps": {
            "nationalSecurityStandard": "Prior 50,000-row portal sample contained no Seoul assigned positions.",
            "smartStreetLights": "Separate indicator source with a small Seoul sample and datum/terms pending.",
            "pedestrianSignalOrFixture": "Undetermined coordinate definition; not treated as street illumination.",
            "parkLamp": "Separate confirmed park lamp points were not obtained.",
        },
        "scope": "Published positions and timings are separate. Missing positions mean unobserved in this source.",
        "useBoundary": "internal_review_only", "distributionApproved": False,
        "fixtureStatus": "Individual current operation, installation appearance, brightness, and color not verified.",
        "recordPeriodWarning": "The 2024 municipal dates and 2026 road date identify file releases. Individual date is only present in the route survey.",
        "crossSourceReconciliation": "Do not add distinct counts from different sources as physical light counts.",
        "coordinatesExported": False, "integrityCheck": "passed", "foreignKeyCheck": "passed",
    }
    atomic_json(output / "street-lighting-summary.json", summary)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--collect-municipal", action="store_true")
    args = parser.parse_args()
    output = local_path(args.output)
    if args.collect_municipal:
        collect_municipal(output)
    print(json.dumps(build(output), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
