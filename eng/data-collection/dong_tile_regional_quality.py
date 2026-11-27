"""Compare four retained regional dongs on the common tile grid without rebaking them."""

import argparse
from collections import Counter, defaultdict
from contextlib import closing
import csv
from dataclasses import dataclass
import gzip
from hashlib import sha256
import json
from pathlib import Path
import sqlite3
import tarfile
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_buildings import BuildingBucket, add_building, projected_points, verify_enu_inverse
from dong_tile_geometry import REVISION, SLOT_LABELS, point_key, vertices
from seoul_area_geography import SPACING, gis, read_only
from seoul_tile_population import LOCAL, PUBLIC, ROOT, axial, digest, local_path

NATIONAL_BAKE = LOCAL / "hongdal-unity-claude-20261112/dong-bake-national-20261120-r1"
QUALITY = NATIONAL_BAKE / "quality/dong-quality-production.csv"
GYEONGGI_CANDIDATE = (NATIONAL_BAKE.parent / "dong-bake-gyeonggi-20261118-r1"
                      / "codex-handoff-r1/data/gyeonggi-cand-wgs84.geojson.gz")
POINTS = PUBLIC / "dong-tile-intake-20261126/portability/national-points"
NATIONAL_AREAS = PUBLIC / "national-admin-stat-map-20261119-r1/official-local/national/official-admin-statistics.sqlite"
RESIDENTS = PUBLIC / "diorama-data-relations-20261123-r2/population-monthly/population-monthly.sqlite"
OUTPUT = PUBLIC / "dong-tile-intake-20261126/portability/region-quality-cases"
AREA_FLOOR = 1e-6
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (
    id TEXT PRIMARY KEY,scope TEXT NOT NULL,path TEXT NOT NULL,sha256 TEXT NOT NULL
);
CREATE TABLE region_case (
    dong_code TEXT PRIMARY KEY, name TEXT NOT NULL, morphology TEXT NOT NULL,
    official_boundary_source TEXT NOT NULL REFERENCES source(id),
    boundary_edition TEXT NOT NULL, official_area_m2 REAL NOT NULL CHECK(official_area_m2>0),
    module_source TEXT NOT NULL REFERENCES source(id), module_sha256 TEXT NOT NULL,
    historical_bake_report TEXT NOT NULL REFERENCES source(id),
    population_date TEXT, registered_people INTEGER, one_person_households INTEGER,
    historic_group_grade TEXT NOT NULL, historic_group_main_route_share REAL
);
CREATE TABLE boundary_comparison (
    dong_code TEXT PRIMARY KEY REFERENCES region_case(dong_code),
    candidate_source TEXT NOT NULL REFERENCES source(id), candidate_edition TEXT NOT NULL,
    candidate_area_m2 REAL NOT NULL, official_intersection_m2 REAL NOT NULL,
    official_iou REAL NOT NULL,
    building_assignment_agreement INTEGER NOT NULL,
    accepted_only_in_official INTEGER NOT NULL, accepted_only_in_candidate INTEGER NOT NULL
);
CREATE TABLE parent (
    dong_code TEXT NOT NULL REFERENCES region_case(dong_code),q INTEGER NOT NULL,r INTEGER NOT NULL,
    official_overlap_m2 REAL NOT NULL CHECK(official_overlap_m2>0),
    PRIMARY KEY(dong_code,q,r)
) WITHOUT ROWID;
CREATE TABLE child (
    dong_code TEXT NOT NULL,q INTEGER NOT NULL,r INTEGER NOT NULL,
    slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    official_overlap_m2 REAL NOT NULL CHECK(official_overlap_m2>=0),
    PRIMARY KEY(dong_code,q,r,slot),FOREIGN KEY(dong_code,q,r) REFERENCES parent(dong_code,q,r)
) WITHOUT ROWID;
CREATE TABLE building_child (
    dong_code TEXT NOT NULL,q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    source_family TEXT NOT NULL,building_count INTEGER NOT NULL CHECK(building_count>0),
    non_symbolic_height_count INTEGER NOT NULL, non_symbolic_height_sum_m REAL,
    source_floor_count INTEGER NOT NULL, source_floors_sum INTEGER,
    PRIMARY KEY(dong_code,q,r,slot,source_family),
    FOREIGN KEY(dong_code,q,r,slot) REFERENCES child(dong_code,q,r,slot)
) WITHOUT ROWID;
CREATE TABLE external_point_child (
    dong_code TEXT NOT NULL,q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    source_family TEXT NOT NULL,category TEXT NOT NULL,source_count INTEGER NOT NULL CHECK(source_count>0),
    PRIMARY KEY(dong_code,q,r,slot,source_family,category),
    FOREIGN KEY(dong_code,q,r,slot) REFERENCES child(dong_code,q,r,slot)
) WITHOUT ROWID;
CREATE TABLE external_point_scope (
    dong_code TEXT PRIMARY KEY REFERENCES region_case(dong_code),
    state TEXT NOT NULL, national_boundary_source TEXT REFERENCES source(id),
    national_geometry_valid INTEGER, national_geometry_warning TEXT,
    accepted_rows INTEGER NOT NULL CHECK(accepted_rows>=0), unsupported_rows INTEGER NOT NULL CHECK(unsupported_rows>=0)
);
CREATE VIEW v_building_child AS
    SELECT dong_code,q,r,slot,sum(building_count) AS building_count,
           sum(non_symbolic_height_count) AS non_symbolic_height_count,
           sum(source_floor_count) AS source_floor_count
    FROM building_child GROUP BY dong_code,q,r,slot;
CREATE VIEW v_building_parent AS
    SELECT dong_code,q,r,sum(building_count) AS building_count,
           sum(non_symbolic_height_count) AS non_symbolic_height_count,
           sum(source_floor_count) AS source_floor_count
    FROM v_building_child GROUP BY dong_code,q,r;
"""


@dataclass(frozen=True)
class Case:
    code: str
    config: str
    group: str
    morphology: str
    compare_candidate: bool


CASES = (
    Case("4111760000", "kr41-41117-p2", "41117-2", "수원 신도시·업무와 주거", True),
    Case("4182035000", "kr41-41820-p2", "41820-6", "가평 산지·농촌", True),
    Case("4159151000", "kr41-41591-p2", "41591-10", "화성 수변 신도시·전자지도 건물 보완", True),
    Case("2818586000", "kr28-28185-p3", "28185-4", "인천 송도 매립지·건물 원천 공백 검토", False),
)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def insert_source(db, key, scope, path, expected_sha=None):
    path = local_path(path)
    signature = digest(path)
    if expected_sha is not None and signature != expected_sha.lower():
        raise ValueError(f"regional_source_hash_mismatch: {key}")
    db.execute("INSERT INTO source VALUES(?,?,?,?)", (key, scope, path.relative_to(ROOT).as_posix(), signature))
    return signature


def geo_feature(path, code, pyproj, shapely, official):
    from shapely.ops import transform

    with gzip.open(path, "rt", encoding="utf-8-sig") as stream:
        collection = json.load(stream)
    matches = [entry for entry in collection["features"] if entry["properties"]["adm_cd2"] == code]
    feature, = matches
    props = feature["properties"]
    if official and props.get("source") != "juso TL_SCCO_GEMD 2026-10":
        raise ValueError(f"regional_expected_official_source_mismatch: {code}")
    to_tile = pyproj.Transformer.from_crs(4326, 5179, always_xy=True)
    shape = transform(to_tile.transform, shapely.geometry.shape(feature["geometry"]))
    if not shape.is_valid or shape.is_empty or not shape.area:
        raise ValueError(f"regional_boundary_geometry_invalid: {code}/{path.name}")
    return props["adm_nm"], shape


def partition(shape, shapely):
    min_x, min_y, max_x, max_y = shape.bounds
    qs, rs = zip(*(axial(x, y, SPACING) for x in (min_x, max_x) for y in (min_y, max_y)))
    parents = {}
    children = {}
    for q in range(min(qs) - 2, max(qs) + 3):
        for r in range(min(rs) - 2, max(rs) + 3):
            area = shape.intersection(shapely.Polygon(vertices(q, r))).area
            if area <= AREA_FLOOR:
                continue
            parents[q, r] = area
            for slot in range(len(SLOT_LABELS)):
                children[q, r, slot] = shape.intersection(shapely.Polygon(vertices(q, r, slot))).area
            if abs(sum(children[q, r, slot] for slot in range(len(SLOT_LABELS))) - area) > 1e-5:
                raise ValueError(f"regional_child_area_rollup_mismatch: {q}/{r}")
    if abs(sum(parents.values()) - shape.area) > 1e-4:
        raise ValueError("regional_parent_area_rollup_mismatch")
    return parents, children


def source_geometry(db, case, pyproj, shapely):
    config_root = NATIONAL_BAKE / f"codex-official-{case.config}"
    config_path = config_root / f"bake-config-{case.config[-2:]}.json"
    config = read_json(config_path)
    if config["profile"] != "juso-gemd-202610":
        raise ValueError(f"regional_bake_config_not_official: {case.code}")
    path = local_path(config_root / config["geo"])
    insert_source(db, f"official:{case.code}", "TL_SCCO_GEMD official WGS84 projection for the bake",
                  path, config["inputSha256"]["geo"])
    names, shape = geo_feature(path, case.code, pyproj, shapely, official=True)
    group = config_root / "out" / config["name"] / case.code[:5] / case.group
    return names, shape, group


def region_buildings(db, case, group):
    group_receipt_path = group / "group-receipt.json"
    receipt = read_json(group_receipt_path)
    if receipt["state"] != "끝" or receipt["group"] != case.group or case.code not in receipt["requestedCodes"]:
        raise ValueError(f"regional_bake_group_not_complete: {case.code}")
    insert_source(db, f"group:{case.code}", "saved completion and input fingerprints", group_receipt_path)
    for key, file, scope in (
        ("scope", "inputs/scope-manifest.json", "bake source scope and exact per-dong signatures"),
        ("archive", "inputs/modules.tar.gz", "saved exact per-dong source modules"),
        ("report", "reports/report.json", "saved historical render and diagnostics"),
    ):
        insert_source(db, f"{key}:{case.code}", scope, group / file, receipt["files"][file])
    scope = read_json(group / "inputs/scope-manifest.json")
    report = read_json(group / "reports/report.json")
    if report["scopeFileSha256"].lower() != receipt["files"]["inputs/scope-manifest.json"].lower():
        raise ValueError(f"regional_bake_report_scope_mismatch: {case.code}")
    stable_id = f"region:kr:hjd:{case.code}"
    manifest, = (item for item in scope["modules"] if item["administrativeAreaStableId"] == stable_id)
    rendered, = (item for item in report["modules"] if item["code"] == case.code)
    with tarfile.open(group / "inputs/modules.tar.gz", "r:gz") as archive:
        member = archive.getmember(manifest["relativePath"])
        if not member.isfile():
            raise ValueError(f"regional_module_archive_entry_not_file: {case.code}")
        with archive.extractfile(member) as stream:
            raw = stream.read()
    output = json.loads(raw)
    buildings = output["buildings"]
    module_hash = sha256(raw).hexdigest()
    if (not output["sourceVintage"].startswith("juso-gemd-202610+") or output["administrativeAreaStableId"] != stable_id
            or module_hash != manifest["sha256"].lower() or module_hash != rendered["moduleSha256"].lower()
            or len(buildings) != manifest["buildingCount"] or len(buildings) != rendered["buildings"] + rendered["skippedBuildings"]
            or output["coordinateFrame"] != scope["coordinateFrame"]
            or len(set(item["buildingStableId"] for item in buildings)) != len(buildings)):
        raise ValueError(f"regional_building_module_lineage_mismatch: {case.code}")
    return output["coordinateFrame"], buildings, rendered, scope, module_hash


def building_source_family(stable_id):
    if stable_id.startswith("al-d010:20261109:"):
        return "al-d010-20261109"
    if stable_id.startswith("juso-electronic:building:"):
        return "juso-electronic-202610-supplement"
    raise ValueError("regional_unknown_building_family")


def place_buildings(db, case, official, candidate, children, frame, buildings, pyproj, shapely):
    error = verify_enu_inverse(frame, pyproj)
    if error > 0.01:
        raise ValueError(f"regional_module_coordinate_validation_failed: {case.code}")
    buckets = defaultdict(BuildingBucket)
    raw_counts = Counter()
    comparison = Counter()
    outside = []
    for building, xy in zip(buildings, projected_points(frame, buildings, pyproj)):
        point = shapely.Point(*xy)
        inside = official.covers(point)
        if candidate is not None:
            older = candidate.covers(point)
            comparison["in_both" if (inside and older) else "in_official_only" if inside else
                       "in_candidate_only" if older else "in_neither"] += 1
        family = building_source_family(building["buildingStableId"])
        raw_counts[family] += 1
        if not inside:
            outside.append(official.distance(point))
            continue
        key = point_key(*xy)
        if key not in children or children[key] <= 0:
            raise ValueError(f"regional_official_point_has_no_child_overlap: {case.code}")
        add_building(buckets[(*key, family)], building, False)
    db.executemany("INSERT INTO building_child VALUES(?,?,?,?,?,?,?,?,?,?)", (
        (case.code, *key, bucket.count, bucket.height_count, bucket.height_sum_m if bucket.height_count else None,
         bucket.floor_count, bucket.floor_sum if bucket.floor_count else None)
        for key, bucket in sorted(buckets.items())))
    counts = dict(db.execute("SELECT source_family,sum(building_count) FROM building_child WHERE dong_code=? GROUP BY source_family",
                             (case.code,)))
    values = {"moduleInputs": len(buildings), "moduleSourceFamilies": dict(raw_counts),
              "placedInputs": sum(counts.values()), "placedSourceFamilies": counts,
              "outsideOfficialBoundary": len(outside),
              "maxOutsideDistanceM": max(outside) if outside else None,
              "childrenWithBuildings": len({key[:3] for key in buckets}),
              "withDirectHeightEvidence": sum(bucket.height_count for bucket in buckets.values()),
              "withFloorEvidence": sum(bucket.floor_count for bucket in buckets.values()),
              "enuControlErrorM": error}
    if values["placedInputs"] + values["outsideOfficialBoundary"] != len(buildings):
        raise ValueError(f"regional_building_counts_not_conserved: {case.code}")
    if candidate is not None:
        intersection = official.intersection(candidate).area
        union = official.area + candidate.area - intersection
        db.execute("INSERT INTO boundary_comparison VALUES(?,?,?,?,?,?,?,?,?)", (
            case.code, "gg-candidate", "editorial candidate 2026-10-01", candidate.area, intersection,
            intersection / union, comparison["in_both"], comparison["in_official_only"], comparison["in_candidate_only"]))
        values["candidateComparison"] = {
            "edition": "editorial candidate 2026-10-01", "areaM2": candidate.area,
            "officialIntersectionOverUnion": intersection / union,
            "sameOfficialModulePoints": dict(comparison),
        }
    return values


def attach_external_counts(db, case, official, children, points, areas, pyproj, shapely):
    from shapely.ops import transform

    area_row = areas.execute("""SELECT topology_valid,import_warning,geometry_wkb FROM boundary
                              WHERE code=? AND level='dong'""", (case.code,)).fetchone()
    valid, warning, geometry_wkb = area_row if area_row else (None, None, None)
    national_mask_iou = None
    if valid:
        to_projected = pyproj.Transformer.from_crs(4326, 5179, always_xy=True)
        shape = transform(to_projected.transform, shapely.from_wkb(geometry_wkb))
        if shape.is_valid and not shape.is_empty:
            national_mask_iou = official.intersection(shape).area / official.union(shape).area
    rows = points.execute("""SELECT q,r,slot,source,category,positive_source_count
                             FROM point_count WHERE official_dong_code=? ORDER BY q,r,slot,source,category""",
                          (case.code,)).fetchall()
    eligible = [] if not valid or warning else [row for row in rows if children.get(row[:3], 0) > 0]
    if warning is not None and rows:
        raise ValueError(f"regional_expected_national_source_hold_has_rows: {case.code}")
    unsupported = len(rows) - len(eligible)
    external_state = "national_boundary_review_hold" if warning or not valid else (
        "some_counts_not_linked_to_official_child" if unsupported else "linked_to_official_child")
    db.execute("INSERT INTO external_point_scope VALUES(?,?,?,?,?,?,?)", (
        case.code, external_state, "national-boundaries" if area_row else None,
        valid, warning, len(eligible), unsupported))
    db.executemany("INSERT INTO external_point_child VALUES(?,?,?,?,?,?,?)", ((case.code, *row) for row in eligible))
    summary = Counter()
    for row in eligible:
        summary[row[3]] += row[-1]
    return {"state": external_state, "nationalBoundaryWarning": warning, "officialMaskIoU": national_mask_iou,
            "nationalSourceRows": len(rows), "acceptedPointCountRows": len(eligible), "unsupportedPointCountRows": unsupported,
            "countsBySource": dict(summary)}


def load_case(db, case, quality_rows, points, areas, residents, pyproj, shapely):
    name, official, group = source_geometry(db, case, pyproj, shapely)
    frame, buildings, rendered, scope, input_sha = region_buildings(db, case, group)
    row, = (item for item in quality_rows if item["dongCode"] == case.code and item["revision"] == scope["revision"])
    if row["boundaryEdition"] != "공식 2026-10" or int(row["buildings"]) != rendered["buildings"]:
        raise ValueError(f"regional_quality_report_mismatch: {case.code}")
    official_source = f"official:{case.code}"
    stats = residents.execute("""SELECT as_of,registered_people,one_person_households,match_state
                                 FROM monthly_dong_reference WHERE code=?""", (case.code,)).fetchone()
    if stats is not None and stats[-1] != "verified_current_code_and_name":
        raise ValueError(f"regional_population_code_not_verified: {case.code}")
    resident_fields = stats[:3] if stats else (None, None, None)
    largest_route_component_share = float(row["groupMainShare"]) if row["groupMainShare"] else None
    db.execute("INSERT INTO region_case VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (
        case.code, name, case.morphology, official_source, "2026-10 TL_SCCO_GEMD", official.area,
        f"archive:{case.code}", input_sha, f"report:{case.code}", *resident_fields, row["grade"],
        largest_route_component_share))
    parents, children = partition(official, shapely)
    db.executemany("INSERT INTO parent VALUES(?,?,?,?)", ((case.code, *key, area) for key, area in parents.items()))
    db.executemany("INSERT INTO child VALUES(?,?,?,?,?)", ((case.code, *key, area) for key, area in children.items()))
    candidate = None
    if case.compare_candidate:
        _, candidate = geo_feature(GYEONGGI_CANDIDATE, case.code, pyproj, shapely, official=False)
    summary = {
        "dong": case.code, "name": name, "morphology": case.morphology, "areaM2": official.area,
        "officialBoundaryEdition": "2026-10 TL_SCCO_GEMD", "parentCount": len(parents),
        "childSlotCount": len(children), "childSlotsWithOfficialOverlap": sum(area > 0 for area in children.values()),
        "absoluteChildAreaConservationErrorM2": abs(sum(children.values()) - official.area),
        "historicalRendered": rendered["buildings"], "historicalSkipped": rendered["skippedBuildings"],
        "historicBakingGrade": row["grade"], "historicGroupMainRouteShare": largest_route_component_share,
        "residents": dict(zip(("period", "registeredPeople", "onePersonHouseholds"), resident_fields)),
        "externalPointEvidence": attach_external_counts(db, case, official, children, points, areas, pyproj, shapely),
    }
    summary.update(place_buildings(db, case, official, candidate, children, frame, buildings, pyproj, shapely))
    summary["externalPointEvidence"]["countsInSlotsWithoutBuildingInput"] = dict(db.execute("""
        SELECT e.source_family,sum(CASE WHEN b.building_count IS NULL THEN e.source_count ELSE 0 END)
        FROM external_point_child e LEFT JOIN v_building_child b USING(dong_code,q,r,slot)
        WHERE e.dong_code=? GROUP BY e.source_family
    """, (case.code,)))
    checks = ("building_child", "v_building_child", "v_building_parent")
    if any(db.execute(f"SELECT coalesce(sum(building_count),0) FROM {table} WHERE dong_code=?", (case.code,)).fetchone()[0]
           != summary["placedInputs"] for table in checks):
        raise ValueError(f"regional_building_fraction_not_conserved: {case.code}")
    return summary


def build(output):
    pyproj, shapely = gis()
    points_db = POINTS / "national-point-tiles.sqlite"
    point_receipt_path = POINTS / "verification.json"
    point_receipt = read_json(point_receipt_path)
    if point_receipt["gridRevision"] != REVISION or point_receipt["gridCrs"] != "EPSG:5179":
        raise ValueError("regional_national_point_grid_revision_mismatch")
    with QUALITY.open("r", encoding="utf-8-sig", newline="") as stream:
        quality_rows = list(csv.DictReader(stream))
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as stage:
        tmp = Path(stage) / "region-quality-tile-cases.sqlite"
        with (closing(sqlite3.connect(tmp)) as db, db, closing(read_only(points_db)) as points,
              closing(read_only(NATIONAL_AREAS)) as areas, closing(read_only(RESIDENTS)) as residents):
            db.executescript(SCHEMA)
            insert_source(db, "quality", "historical quality and group route share", QUALITY)
            insert_source(db, "gg-candidate", "2026-10-01 edited candidate; diagnostic comparison only", GYEONGGI_CANDIDATE)
            insert_source(db, "national-boundaries", "2026-10 internal official national boundary index", NATIONAL_AREAS,
                          point_receipt["inputs"]["boundary"]["sha256"])
            insert_source(db, "national-point-receipt", "local and non-distributable point ingest receipt", point_receipt_path)
            insert_source(db, "national-points", "national food, TAGO, and railway positive observation counts",
                          points_db, point_receipt["database"]["sha256"])
            insert_source(db, "residents", "2026-10 official resident and one-person household dong totals", RESIDENTS)
            area_meta = json.loads(areas.execute(
                "SELECT details_json FROM metadata WHERE key='regional_map'").fetchone()[0])
            if (area_meta["geometryMonth"], area_meta["mapCrs"], area_meta["geometryClass"]) != (
                "2026-10", "OGC:CRS84", "official_juso"
            ):
                raise ValueError("regional_national_boundary_edition_or_coordinate_system_changed")
            summaries = [load_case(db, case, quality_rows, points, areas, residents, pyproj, shapely) for case in CASES]
            if db.execute("PRAGMA foreign_key_check").fetchone() or db.execute("PRAGMA integrity_check").fetchone() != ("ok",):
                raise ValueError("regional_quality_database_integrity_failed")
        target = output / tmp.name
        tmp.replace(target)
    summary = {
        "gridRevision": REVISION, "databaseSha256": digest(target), "cases": summaries,
        "scope": "four official morphology cases, not a population sample or nationwide readiness claim",
        "authority": "official shapes support current spatial rows; candidate shapes support diagnostics only",
        "usage": "same-PC internal review only", "distributionApproved": False,
        "officialBoundaryGeometryExported": False, "restrictedBuildingCoordinatesExported": False,
        "notes": [
            "A published building record is counted by its module assignment point.",
            "No external points are imputed in a national boundary review hold.",
            "Resident totals remain at dong level.",
            "Historical map checks describe a retained run, not a fresh Unity verification.",
        ],
        "integrityCheck": "passed", "foreignKeyCheck": "passed",
    }
    atomic_json(output / "region-quality-summary.json", summary)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(build(local_path(args.output)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
