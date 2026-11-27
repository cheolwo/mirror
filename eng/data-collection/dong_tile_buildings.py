"""Index the reviewed Seoul building inputs on the common parent and child tile grid."""

import argparse
from collections import defaultdict
from contextlib import closing
from dataclasses import dataclass
from hashlib import sha256
import json
import math
from pathlib import Path
import sqlite3
import tarfile
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, point_key
from seoul_area_geography import gis, read_only
from seoul_tile_population import LOCAL, PUBLIC, digest, local_path


SOURCE = LOCAL / "hongdal-unity-claude-20261112/dong-bake-seoul-official-20261118-r2"
AREAS = PUBLIC / "seoul-area-evidence-20261125/seoul-area.sqlite"
OUTPUT = PUBLIC / "dong-tile-intake-20261126/buildings"
FRAME_METHOD = "WGS84-ECEF-ENU-at-zero-altitude"
SOURCE_RELEASE = "al-d010:20261109:11"
BUILDING_PROVIDER_URL = "https://www.data.go.kr/data/15083092/fileData.do"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE building_index_metadata (key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE tile (
    q INTEGER NOT NULL, r INTEGER NOT NULL, PRIMARY KEY(q,r)
);
CREATE TABLE building_source_archive (
    district_code TEXT PRIMARY KEY, district_name TEXT NOT NULL,
    archive_path TEXT NOT NULL, archive_sha256 TEXT NOT NULL, scope_sha256 TEXT NOT NULL,
    render_report_sha256 TEXT NOT NULL, source_date TEXT NOT NULL,
    scope_revision TEXT NOT NULL, source_vintage TEXT NOT NULL,
    frame_method TEXT NOT NULL, expected_dongs INTEGER NOT NULL
);
CREATE TABLE building_source_dong (
    dong_code TEXT PRIMARY KEY,
    district_code TEXT NOT NULL REFERENCES building_source_archive(district_code),
    name TEXT NOT NULL, module_sha256 TEXT NOT NULL,
    input_buildings INTEGER NOT NULL, indexed_buildings INTEGER NOT NULL,
    unassigned_buildings INTEGER NOT NULL, parent_dong_relation_missing INTEGER NOT NULL,
    historical_rendered_buildings INTEGER NOT NULL, historical_skipped_buildings INTEGER NOT NULL,
    CHECK(input_buildings=indexed_buildings+unassigned_buildings)
);
CREATE TABLE building_child_by_use (
    dong_code TEXT NOT NULL REFERENCES building_source_dong(dong_code),
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    use_code TEXT NOT NULL,
    building_count INTEGER NOT NULL CHECK(building_count>0),
    source_height_count INTEGER NOT NULL CHECK(source_height_count BETWEEN 0 AND building_count),
    source_height_sum_m REAL,
    source_floor_count INTEGER NOT NULL CHECK(source_floor_count BETWEEN 0 AND building_count),
    source_floors_sum INTEGER,
    source_area_count INTEGER NOT NULL CHECK(source_area_count BETWEEN 0 AND building_count),
    assigned_source_area_sum_m2 REAL,
    parent_dong_relation_missing INTEGER NOT NULL CHECK(parent_dong_relation_missing BETWEEN 0 AND building_count),
    PRIMARY KEY(dong_code,q,r,slot,use_code), FOREIGN KEY(q,r) REFERENCES tile(q,r)
);
CREATE INDEX building_child_by_position ON building_child_by_use(q,r,slot);
"""
MEASURE_SUMS = """
    sum(building_count) AS building_count,
    sum(source_height_count) AS source_height_count,
    sum(source_height_sum_m) AS source_height_sum_m,
    sum(source_floor_count) AS source_floor_count,
    sum(source_floors_sum) AS source_floors_sum,
    sum(source_area_count) AS source_area_count,
    sum(assigned_source_area_sum_m2) AS assigned_source_area_sum_m2,
    sum(parent_dong_relation_missing) AS parent_dong_relation_missing
"""
SCHEMA += f"""
CREATE VIEW v_building_dong_child AS
    SELECT dong_code,q,r,slot,{MEASURE_SUMS}
    FROM building_child_by_use GROUP BY dong_code,q,r,slot;
CREATE VIEW v_building_child AS
    SELECT q,r,slot,{MEASURE_SUMS}
    FROM building_child_by_use GROUP BY q,r,slot;
CREATE VIEW v_building_parent AS
    SELECT q,r,{MEASURE_SUMS}
    FROM building_child_by_use GROUP BY q,r;
"""


@dataclass
class BuildingBucket:
    count: int = 0
    height_count: int = 0
    height_sum_m: float = 0
    floor_count: int = 0
    floor_sum: int = 0
    area_count: int = 0
    area_sum_m2: float = 0
    missing_parent_dong_relations: int = 0


def projected_points(frame, buildings, pyproj):
    """Invert local ENU on the WGS84 ellipsoid before converting to the tile CRS."""
    if frame["method"] != FRAME_METHOD or frame["metersPerUnit"] != 1:
        raise ValueError("unsupported_building_coordinate_frame")
    latitude = frame["originLatitude"]
    longitude = frame["originLongitude"]
    phi = math.radians(latitude)
    lam = math.radians(longitude)
    sin_phi, cos_phi = math.sin(phi), math.cos(phi)
    sin_lam, cos_lam = math.sin(lam), math.cos(lam)
    east = (-sin_lam, cos_lam, 0)
    north = (-sin_phi * cos_lam, -sin_phi * sin_lam, cos_phi)
    up = (cos_phi * cos_lam, cos_phi * sin_lam, sin_phi)
    to_ecef = pyproj.Transformer.from_crs(4979, 4978, always_xy=True)
    to_geodetic = pyproj.Transformer.from_crs(4978, 4979, always_xy=True)
    to_tile = pyproj.Transformer.from_crs(4326, 5179, always_xy=True)
    origin = to_ecef.transform(longitude, latitude, 0, errcheck=True)
    semimajor = 6378137.0
    semiminor = semimajor * (1 - 1 / 298.257223563)
    scale = (semimajor, semimajor, semiminor)
    scaled_up = tuple(u / s for u, s in zip(up, scale))
    quadratic = math.fsum(u * u for u in scaled_up)
    axes = ([], [], [])
    for building in buildings:
        point = building.get("assignmentPoint")
        if point is None:
            raise ValueError("building_assignment_point_missing")
        e = point["x"] - frame["worldOffsetX"]
        n = point["z"] - frame["worldOffsetZ"]
        tangent = tuple(o + e * a + n * b for o, a, b in zip(origin, east, north))
        scaled = tuple(p / s for p, s in zip(tangent, scale))
        linear = 2 * math.fsum(p * u for p, u in zip(scaled, scaled_up))
        constant = math.fsum(p * p for p in scaled) - 1
        depth = -2 * constant / (linear + math.sqrt(linear * linear - 4 * quadratic * constant))
        for output, value, direction in zip(axes, tangent, up):
            output.append(value + depth * direction)
    longitude, latitude, heights = to_geodetic.transform(*axes, errcheck=True)
    if any(abs(height) > 0.01 for height in heights):
        raise ValueError("building_coordinate_round_trip_exceeds_one_cm")
    return zip(*to_tile.transform(longitude, latitude, errcheck=True))


def verify_enu_inverse(frame, pyproj):
    longitude, latitude = frame["originLongitude"], frame["originLatitude"]
    control = ((longitude, latitude), (longitude - 0.018, latitude + 0.016),
               (longitude + 0.014, latitude - 0.013))
    independent = pyproj.Transformer.from_pipeline(
        f"+proj=pipeline +step +proj=cart +ellps=WGS84 +step +proj=topocentric "
        f"+ellps=WGS84 +lat_0={latitude} +lon_0={longitude} +h_0=0"
    )
    to_tile = pyproj.Transformer.from_crs(4326, 5179, always_xy=True)
    test_buildings = []
    projected_reference = []
    for lon, lat in control:
        e, n, _ = independent.transform(lon, lat, 0, errcheck=True)
        test_buildings.append({"assignmentPoint": {"x": e + frame["worldOffsetX"], "z": n + frame["worldOffsetZ"]}})
        projected_reference.append(to_tile.transform(lon, lat, errcheck=True))
    return max(math.dist(observed, reference) for observed, reference in
               zip(projected_points(frame, test_buildings, pyproj), projected_reference))


def add_building(bucket, building, missing_relation):
    bucket.count += 1
    height = building.get("heightMeters")
    evidence = building["evidenceKindCode"]
    if height is not None and 0 < height < 1000 and not evidence.endswith(":SymbolicHeightFallback"):
        bucket.height_count += 1
        bucket.height_sum_m += height
    floors = building.get("aboveGroundFloorCount")
    if floors is not None and floors > 0:
        bucket.floor_count += 1
        bucket.floor_sum += floors
    area = building.get("buildingAreaSquareMeters")
    if area is not None and math.isfinite(area) and area > 0:
        bucket.area_count += 1
        bucket.area_sum_m2 += area
    bucket.missing_parent_dong_relations += int(missing_relation)


def bucket_row(key, bucket):
    return (*key, bucket.count, bucket.height_count, bucket.height_sum_m if bucket.height_count else None,
            bucket.floor_count, bucket.floor_sum if bucket.floor_count else None,
            bucket.area_count, bucket.area_sum_m2 if bucket.area_count else None,
            bucket.missing_parent_dong_relations)


def ingest_district(db, path, name, dongs, keys, relations, seen, pyproj):
    checksums = dict(line.split(maxsplit=1)[::-1] for line in
                     (path / "SHA256SUMS").read_text(encoding="utf-8-sig").splitlines() if line.strip())
    paths = ("./inputs/modules.tar.gz", "./inputs/scope-manifest.json", "./reports/report.json")
    fingerprints = {}
    for name_in_archive in paths:
        input_path = path / name_in_archive
        fingerprints[name_in_archive] = digest(input_path)
        if fingerprints[name_in_archive] != checksums[name_in_archive].lower():
            raise ValueError(f"district_input_sha256_mismatch: {path.name}/{name_in_archive}")
    archive_relative, manifest_relative, report_relative = paths
    manifest = json.loads((path / manifest_relative).read_text(encoding="utf-8-sig"))
    report = json.loads((path / report_relative).read_text(encoding="utf-8-sig"))
    if report["scopeFileSha256"].lower() != fingerprints[manifest_relative]:
        raise ValueError(f"district_report_different_scope: {path.name}")
    historical = {item["code"]: item for item in report["modules"]}
    modules = {item["relativePath"]: item for item in manifest["modules"]}
    if (set(historical) != set(dongs)
            or set(modules) != {f"modules/{dong}.json" for dong in dongs}):
        raise ValueError(f"district_module_scope_mismatch: {path.name}")
    origin = manifest["coordinateFrame"]
    inverse_error = verify_enu_inverse(origin, pyproj)
    if inverse_error > 0.01:
        raise ValueError(f"independent_enu_inverse_check_failed: {path.name}")
    db.execute("INSERT INTO building_source_archive VALUES(?,?,?,?,?,?,?,?,?,?,?)", (
        path.name, name, (path / archive_relative).relative_to(LOCAL).as_posix(), fingerprints[archive_relative],
        fingerprints[manifest_relative], fingerprints[report_relative], manifest["buildingSourceDate"],
        manifest["revision"], manifest["sourceVintage"], manifest["coordinateFrame"]["method"], len(modules)))
    with tarfile.open(path / archive_relative, "r|gz") as archive:
        for member in archive:
            if member.isdir():
                continue
            entry = modules.pop(member.name, None)
            if not member.isfile() or entry is None:
                raise ValueError(f"unexpected_module_archive_member: {path.name}/{member.name}")
            with archive.extractfile(member) as stream:
                payload = stream.read()
            payload_sha256 = sha256(payload).hexdigest()
            if payload_sha256 != entry["sha256"].lower():
                raise ValueError(f"module_sha256_mismatch: {path.name}/{member.name}")
            module = json.loads(payload)
            dong_code = Path(member.name).stem
            retained_build = historical[dong_code]
            stable_id = f"region:kr:hjd:{dong_code}"
            if (module["administrativeAreaStableId"] != stable_id or entry["administrativeAreaStableId"] != stable_id
                    or retained_build["moduleSha256"].lower() != payload_sha256):
                raise ValueError(f"module_identity_mismatch: {dong_code}")
            buildings = module["buildings"]
            if len(buildings) != entry["buildingCount"]:
                raise ValueError(f"module_building_count_mismatch: {dong_code}")
            if len(buildings) != retained_build["buildings"] + retained_build["skippedBuildings"]:
                raise ValueError(f"historical_building_count_not_conserved: {dong_code}")
            if module["coordinateFrame"] != origin:
                raise ValueError(f"module_coordinate_frame_mismatch: {dong_code}")
            buckets = defaultdict(BuildingBucket)
            unassigned = 0
            missing_relations = 0
            for building, point in zip(buildings, projected_points(module["coordinateFrame"], buildings, pyproj)):
                release, separator, row = building["buildingStableId"].rpartition(":")
                if release != SOURCE_RELEASE or not separator or not row.isdigit():
                    raise ValueError(f"unexpected_building_source_release: {dong_code}")
                row = int(row)
                if row in seen:
                    raise ValueError(f"duplicate_building_source_row: {dong_code}")
                seen.add(row)
                if not all(math.isfinite(coord) for coord in point):
                    raise ValueError(f"building_coordinate_invalid: {dong_code}")
                q, r, slot = point_key(*point)
                if (q, r) not in keys:
                    unassigned += 1
                    continue
                missing_relation = (q, r, dong_code) not in relations
                missing_relations += missing_relation
                use = building["categoryCode"].removeprefix("al-d010-kind:")
                add_building(buckets[dong_code, q, r, slot, use], building, missing_relation)
            db.execute("INSERT INTO building_source_dong VALUES(?,?,?,?,?,?,?,?,?,?)", (
                dong_code, path.name, dongs[dong_code], payload_sha256, len(buildings), len(buildings) - unassigned,
                unassigned, missing_relations, retained_build["buildings"], retained_build["skippedBuildings"]))
            db.executemany("INSERT INTO building_child_by_use VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
                           (bucket_row(key, bucket) for key, bucket in sorted(buckets.items())))
    if modules:
        raise ValueError(f"archive_modules_absent: {path.name}")
    return inverse_error


def build(source, area_path, output):
    pyproj, _ = gis()
    with closing(read_only(area_path)) as areas:
        districts = dict(areas.execute("SELECT code,name FROM district ORDER BY code"))
        keys = set(areas.execute("SELECT q,r FROM tile"))
        relations = set(areas.execute("SELECT q,r,admin_code FROM tile_admin"))
        dongs = defaultdict(dict)
        for dong, district, name in areas.execute("SELECT code,district_code,name FROM admin_dong"):
            dongs[district][dong] = name
    published_districts = {path.name for path in source.iterdir() if path.is_dir() and path.name.isdigit()}
    if published_districts != set(districts):
        raise ValueError("building_source_district_coverage_differs_from_area_db")
    area_sha = digest(area_path)
    seen = set()
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as stage:
        temp_db = Path(stage) / "building-tile-index.sqlite"
        with closing(sqlite3.connect(temp_db)) as db, db:
            db.executescript(SCHEMA)
            db.executemany("INSERT INTO building_index_metadata VALUES(?,?)",
                           (("grid_revision", REVISION), ("source_dataset", SOURCE_RELEASE),
                            ("source_provider_url", BUILDING_PROVIDER_URL), ("area_database_sha256", area_sha),
                            ("use_boundary", "internal_review_only"),
                            ("placement", "one retained module assignment point per building")))
            db.executemany("INSERT INTO tile VALUES(?,?)", sorted(keys))
            max_inverse_error = max(ingest_district(db, source / district, name, dongs[district], keys, relations, seen, pyproj)
                                    for district, name in districts.items())
            audit = collect_audit(db, len(seen))
            if db.execute("PRAGMA foreign_key_check").fetchone() is not None:
                raise ValueError("building_foreign_key_check_failed")
            if db.execute("PRAGMA integrity_check").fetchone() != ("ok",):
                raise ValueError("building_sqlite_integrity_check_failed")
        final_db = output / temp_db.name
        temp_db.replace(final_db)
    audit.update({
        "gridRevision": REVISION,
        "sourceDataset": SOURCE_RELEASE,
        "sourceBuild": source.name,
        "sourceProviderUrl": BUILDING_PROVIDER_URL,
        "areaDatabaseRelativeToArtifactsLocal": area_path.relative_to(LOCAL).as_posix(),
        "areaDatabaseSha256": area_sha,
        "databaseSha256": digest(final_db),
        "useBoundary": "internal_review_only",
        "distributionApproved": False,
        "maximumInverseErrorMetresAgainstIndependentProj": max_inverse_error,
        "geometryKind": "one retained module assignment point per building",
        "foreignKeyCheck": "passed",
        "integrityCheck": "passed",
        "notes": [
            "Coordinates and building identifiers are read for validation and are not stored in the index.",
            "Point counts are additive; source area sums are not footprint union or coverage of a tile.",
            "Source heights exclude symbolic and floor-derived height estimates; source floors are kept separately.",
            "Previous rendered counts are historical comparisons, not a new render or asset validation.",
            "No hit in the canonical parent grid was assigned to a nearby tile.",
            "A missing parent/dong intersection retains the module membership and spatial key, with an explicit flag.",
        ],
    })
    atomic_json(output / "building-summary.json", audit)
    return audit


def collect_audit(db, source_rows):
    indexed, heights, floors, areas, relation_missing = db.execute("""
        SELECT sum(building_count),sum(source_height_count),sum(source_floor_count),
        sum(source_area_count),sum(parent_dong_relation_missing) FROM v_building_parent
    """).fetchone()
    input_count, expected_indexed, unassigned, historical_rendered, historical_skipped = db.execute("""
        SELECT sum(input_buildings),sum(indexed_buildings),sum(unassigned_buildings),
        sum(historical_rendered_buildings),sum(historical_skipped_buildings) FROM building_source_dong
    """).fetchone()
    if source_rows != input_count or indexed != expected_indexed or source_rows != indexed + unassigned:
        raise ValueError("building_count_not_conserved")
    counts = dict(db.execute("""
        SELECT 'districts',count(*) FROM building_source_archive
        UNION ALL SELECT 'dongs',count(*) FROM building_source_dong
        UNION ALL SELECT 'parentsWithBuildings',count(*) FROM v_building_parent
        UNION ALL SELECT 'childrenWithBuildings',count(*) FROM v_building_child
        UNION ALL SELECT 'childDongPairs',count(*) FROM v_building_dong_child
        UNION ALL SELECT 'byUseRows',count(*) FROM building_child_by_use
        UNION ALL SELECT 'unknownBuildingUse',coalesce(sum(building_count),0) FROM building_child_by_use WHERE use_code='unknown'
        UNION ALL SELECT 'crossDongChildKeys',count(*) FROM (
            SELECT q,r,slot FROM v_building_dong_child GROUP BY q,r,slot HAVING count(*)>1
        )
    """))
    counts.update({
        "inputBuildings": input_count,
        "uniqueSourceRows": source_rows,
        "indexedBuildings": indexed,
        "unassignedBuildings": unassigned,
        "sourceHeightCount": heights,
        "sourceFloorCount": floors,
        "sourceAreaCount": areas,
        "parentDongRelationMissingBuildings": relation_missing,
        "historicalRenderedBuildings": historical_rendered,
        "historicalSkippedBuildings": historical_skipped,
    })
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE)
    parser.add_argument("--areas", type=Path, default=AREAS)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(build(local_path(args.source), local_path(args.areas), local_path(args.output)),
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
