"""Reproduce one official Guri dong on the same parent and child lattice as Seoul."""

import argparse
from collections import Counter
from contextlib import closing
import gzip
from hashlib import sha256
import json
import math
from pathlib import Path
import sqlite3
import tarfile
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_buildings import projected_points, verify_enu_inverse
from dong_tile_geometry import REVISION, SLOT_LABELS, point_key, vertices
from seoul_area_geography import SPACING, gis, read_only
from seoul_tile_population import LOCAL, PUBLIC, ROOT, axial, digest, local_path


DONG = "4131059000"
GURI = LOCAL / "hongdal-unity-claude-20261112/dong-bake-gyeonggi-20261118-r1/codex-official-r1"
BAKE = GURI / "out/gg-gemd-first-r1/41310-3"
BOUNDARY = GURI / "gg-official-5186.json.gz"
FOOD = PUBLIC / "national-restaurant-regional-20261118-r1/index/restaurants.sqlite"
BUS = PUBLIC / "diorama-public-layer-intake-20261123/r28-national-bus-audit/national-bus-stop-by-dong.sqlite"
PEOPLE = PUBLIC / "diorama-data-relations-20261123-r2/population-monthly/population-monthly.sqlite"
OUTPUT = PUBLIC / "dong-tile-intake-20261126/portability/guri-sutaek3"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (
    source_id TEXT PRIMARY KEY,path TEXT NOT NULL,sha256 TEXT NOT NULL,scope TEXT NOT NULL
);
CREATE TABLE source_release (
    source_id TEXT NOT NULL REFERENCES source(source_id),release_id TEXT NOT NULL,
    period TEXT NOT NULL,url TEXT,original_sha256 TEXT,
    PRIMARY KEY(source_id,release_id)
);
CREATE TABLE dong (code TEXT PRIMARY KEY,name TEXT NOT NULL,official_area_m2 REAL NOT NULL);
CREATE TABLE resident_dong (
    code TEXT NOT NULL REFERENCES dong(code),as_of TEXT NOT NULL,
    registered_people INTEGER NOT NULL,one_person_households INTEGER NOT NULL,match_state TEXT NOT NULL,
    PRIMARY KEY(code,as_of)
);
CREATE TABLE parent (
    dong_code TEXT NOT NULL REFERENCES dong(code),
    q INTEGER NOT NULL,r INTEGER NOT NULL,overlap_m2 REAL NOT NULL CHECK(overlap_m2>0),
    PRIMARY KEY(q,r)
);
CREATE TABLE child (
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    overlap_m2 REAL NOT NULL CHECK(overlap_m2>=0),
    PRIMARY KEY(q,r,slot), FOREIGN KEY(q,r) REFERENCES parent(q,r)
);
CREATE TABLE child_count (
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    source_id TEXT NOT NULL REFERENCES source(source_id),measure TEXT NOT NULL,
    observed_record_count INTEGER NOT NULL CHECK(observed_record_count>0),
    PRIMARY KEY(q,r,slot,source_id,measure), FOREIGN KEY(q,r,slot) REFERENCES child(q,r,slot)
);
"""


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_dong(pyproj, shapely):
    from shapely.ops import transform

    if digest(BOUNDARY) != read_json(GURI / "prep-receipt.json")["officialBoundarySha256"]:
        raise ValueError("guri_official_boundary_sha_differs_from_bake_receipt")
    with gzip.open(BOUNDARY, "rt", encoding="utf-8-sig") as stream:
        boundary = json.load(stream)
    if boundary["crs"] != "EPSG:5186":
        raise ValueError("guri_official_boundary_coordinate_system_mismatch")
    ring, = boundary["rings"][DONG]
    to_tile = pyproj.Transformer.from_crs(5186, 5179, always_xy=True)
    shape = transform(to_tile.transform, shapely.Polygon(ring))
    if not shape.is_valid or shape.is_empty:
        raise ValueError("guri_official_dong_geometry_invalid")
    return shape


def allocate_tiles(db, shape, shapely):
    west, south, east, north = shape.bounds
    coordinates = [axial(x, y, SPACING) for x in (west, east) for y in (south, north)]
    qs, rs = zip(*coordinates)
    parents = {}
    children = {}
    for q in range(min(qs) - 2, max(qs) + 3):
        for r in range(min(rs) - 2, max(rs) + 3):
            area = shape.intersection(shapely.Polygon(vertices(q, r))).area
            if area <= 1e-6:
                continue
            parents[q, r] = area
            for slot in range(len(SLOT_LABELS)):
                children[q, r, slot] = shape.intersection(shapely.Polygon(vertices(q, r, slot))).area
            if abs(sum(children[q, r, slot] for slot in range(len(SLOT_LABELS))) - area) >= 1e-5:
                raise ValueError(f"guri_child_areas_do_not_preserve_parent: {q},{r}")
    if abs(sum(parents.values()) - shape.area) >= 1e-5:
        raise ValueError("guri_parent_areas_do_not_preserve_dong")
    db.executemany("INSERT INTO parent VALUES(?,?,?,?)", ((DONG, *key, area) for key, area in parents.items()))
    db.executemany("INSERT INTO child VALUES(?,?,?,?)", ((*key, area) for key, area in children.items()))
    return children


def building_evidence(pyproj):
    scope_path = BAKE / "inputs/scope-manifest.json"
    report = read_json(BAKE / "reports/report.json")
    if digest(scope_path) != report["scopeFileSha256"].lower():
        raise ValueError("guri_bake_scope_differs_from_verification_report")
    scope = read_json(scope_path)
    module, = scope["modules"]
    historical, = report["modules"]
    archive_path = BAKE / "inputs/modules.tar.gz"
    with tarfile.open(archive_path, "r:gz") as archive:
        member = archive.getmember(module["relativePath"])
        with archive.extractfile(member) as stream:
            content = stream.read()
    module_sha = sha256(content).hexdigest()
    if module_sha != module["sha256"].lower() or module_sha != historical["moduleSha256"].lower():
        raise ValueError("guri_bake_module_sha_mismatch")
    payload = json.loads(content)
    buildings = payload["buildings"]
    if (payload["administrativeAreaStableId"] != f"region:kr:hjd:{DONG}"
            or len(buildings) != module["buildingCount"]
            or len(buildings) != historical["buildings"] + historical["skippedBuildings"]
            or len({entry["buildingStableId"] for entry in buildings}) != len(buildings)):
        raise ValueError("guri_bake_module_building_lineage_mismatch")
    frame = payload["coordinateFrame"]
    projected = projected_points(frame, buildings, pyproj)
    stats = {"buildingInputCandidates": len(buildings), "historicalBakedBuildings": historical["buildings"],
             "historicalSkippedBuildingInputs": historical["skippedBuildings"], "bakeModuleSha256": module_sha,
             "inverseFrameErrorMetres": verify_enu_inverse(frame, pyproj)}
    if stats["inverseFrameErrorMetres"] > 0.01:
        raise ValueError("guri_bake_module_projection_control_points_disagree")
    return payload["displayName"], projected, stats


def transport_points(shape, pyproj, db):
    to_wgs = pyproj.Transformer.from_crs(5179, 4326, always_xy=True)
    minimum_lon, minimum_lat, maximum_lon, maximum_lat = to_wgs.transform_bounds(*shape.bounds)
    with closing(read_only(BUS)) as source:
        rows = source.execute("""
            SELECT longitude,latitude FROM stop
            WHERE longitude BETWEEN ? AND ? AND latitude BETWEEN ? AND ?
            ORDER BY source_row
        """, (minimum_lon, maximum_lon, minimum_lat, maximum_lat)).fetchall()
        period, url, raw_sha = source.execute(
            "SELECT ref_date,url,sha256 FROM source WHERE source_key='tago-original'").fetchone()
    db.execute("INSERT INTO source_release VALUES ('bus','TAGO',?,?,?)", (period, url, raw_sha))
    to_tile = pyproj.Transformer.from_crs(4326, 5179, always_xy=True)
    return [to_tile.transform(*row, errcheck=True) for row in rows]


def food_points(pyproj, db):
    to_tile = pyproj.Transformer.from_crs(5174, 5179, always_xy=True)
    with closing(read_only(FOOD)) as source:
        rows = source.execute("""
            SELECT d.dataset,o.source_x,o.source_y
            FROM license_observation o JOIN dataset d ON d.id=o.dataset_id
            WHERE o.province='경기도' AND o.city_county='구리시'
              AND o.authority_code='3980000' AND o.reported_status='영업/정상'
            ORDER BY o.dataset_id,o.source_record
        """).fetchall()
        db.executemany("INSERT INTO source_release VALUES ('food',?,?,?,?)", source.execute("""
            SELECT dataset,substr(acquired_at_utc,1,10),official_catalog,original_sha256 FROM dataset ORDER BY id
        """))
    points = []
    unavailable = 0
    for dataset, x, y in rows:
        try:
            x, y = float(x), float(y)
        except ValueError:
            unavailable += 1
            continue
        if not (math.isfinite(x) and math.isfinite(y)):
            unavailable += 1
            continue
        points.append((dataset, to_tile.transform(x, y, errcheck=True)))
    return points, len(rows), unavailable


def store_residents(db):
    with closing(read_only(PEOPLE)) as source:
        row = source.execute("""
            SELECT code,as_of,registered_people,one_person_households,match_state
            FROM monthly_dong_reference WHERE code=?
        """, (DONG,)).fetchone()
        if row is None or row[-1] != "verified_current_code_and_name":
            raise ValueError("guri_resident_code_not_verified")
        db.executemany("INSERT INTO source_release VALUES ('people',?,?,?,?)", source.execute("""
            SELECT dataset_id,snapshot_date,official_url,source_sha256 FROM source_release ORDER BY dataset_id
        """))
    db.execute("INSERT INTO resident_dong VALUES(?,?,?,?,?)", row)
    return {"residentPeriod": row[1], "residents": row[2], "onePersonHouseholds": row[3]}


def place(db, shape, shapely, children, observations):
    counts = Counter()
    outside = Counter()
    for source, measure, point in observations:
        if not shape.covers(shapely.Point(*point)):
            outside[source] += 1
            continue
        key = point_key(*point)
        if key not in children or children[key] <= 0:
            raise ValueError("guri_coordinate_in_dong_without_child_overlap")
        counts[(*key, source, measure)] += 1
    db.executemany("INSERT INTO child_count VALUES(?,?,?,?,?,?)",
                   ((*key, value) for key, value in sorted(counts.items())))
    return outside


def build(output):
    pyproj, shapely = gis()
    shape = read_dong(pyproj, shapely)
    name, buildings, facts = building_evidence(pyproj)
    input_rows = (
        ("boundary", BOUNDARY, "Gyeonggi TL_SCCO_GEMD October 2026, EPSG:5186"),
        ("buildings", BAKE / "inputs/modules.tar.gz", "2026-11-09 source buildings before mesh filtering"),
        ("bake_scope", BAKE / "inputs/scope-manifest.json", "scope and module signatures verified against the report"),
        ("historical_bake", BAKE / "reports/report.json", "retained past render verification only"),
        ("food", FOOD, "Guri operating licenses, direct EPSG:5174 coordinates"),
        ("bus", BUS, "national TAGO records, direct WGS84 coordinates"),
        ("people", PEOPLE, "national resident totals, explicit administrative code only"),
    )
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as stage:
        temp_db = Path(stage) / "guri-sutaek3-tile.sqlite"
        with closing(sqlite3.connect(temp_db)) as db, db:
            db.executescript(SCHEMA)
            db.executemany("INSERT INTO source VALUES(?,?,?,?)",
                           ((key, path.relative_to(ROOT).as_posix(), digest(path), scope)
                            for key, path, scope in input_rows))
            db.execute("INSERT INTO dong VALUES(?,?,?)", (DONG, name, shape.area))
            children = allocate_tiles(db, shape, shapely)
            raw_stops = transport_points(shape, pyproj, db)
            food, food_candidates, missing_food_coords = food_points(pyproj, db)
            facts.update(store_residents(db))
            outside = place(db, shape, shapely, children, [
                *(("buildings", "building_input", point) for point in buildings),
                *(("bus", "bus_stop_source_row", point) for point in raw_stops),
                *(("food", dataset, point) for dataset, point in food),
            ])
            summary = dict(db.execute("SELECT source_id,SUM(observed_record_count) FROM child_count GROUP BY source_id"))
            expected = {"buildings": facts["buildingInputCandidates"], "bus": len(raw_stops), "food": len(food)}
            if any(summary.get(source, 0) + outside[source] != count for source, count in expected.items()):
                raise ValueError("guri_source_or_child_counts_not_conserved")
            if db.execute("PRAGMA foreign_key_check").fetchone() or db.execute("PRAGMA integrity_check").fetchone() != ("ok",):
                raise ValueError("guri_example_database_integrity_failed")
            facts.update({
                "countsInsideOfficialDong": summary, "outsideOfficialDong": dict(outside),
                "foodCandidatesInGuri": food_candidates, "guriFoodMissingOrInvalidCoordinates": missing_food_coords,
                "sourceSha256": dict(db.execute("SELECT source_id,sha256 FROM source")),
                "parentKeys": db.execute("SELECT count(*) FROM parent").fetchone()[0],
            })
        final_db = output / temp_db.name
        temp_db.replace(final_db)
    facts.update({
        "gridRevision": REVISION, "dongCode": DONG, "displayName": name, "officialBoundaryPeriod": "2026-10",
        "databaseSha256": digest(final_db), "candidateSlots": len(children),
        "slotsIntersectingDong": sum(value > 0 for value in children.values()),
        "officialAreaM2": shape.area, "useBoundary": "internal_review_only", "distributionApproved": False,
        "dataScope": "one portability example, not a coverage claim for Gyeonggi or Korea",
        "residentCellEstimation": "not performed; population source ends at administrative dong",
        "foodStatus": "operating in source, individual operating status not field-verified",
        "busStatus": "counts of source records, not capacity or validated physical stop shelters",
        "coordinatesExported": False, "integrityCheck": "passed", "foreignKeyCheck": "passed",
    })
    atomic_json(output / "guri-sutaek3-summary.json", facts)
    return facts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(build(local_path(args.output)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
