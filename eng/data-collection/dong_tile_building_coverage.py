"""Measure clipped and dissolved source building footprints without exporting their geometry."""

import argparse
from collections import Counter, defaultdict
from contextlib import closing
from io import BytesIO
import json
import math
import os
from pathlib import Path
import sqlite3
from statistics import mean, median
import sys
from tempfile import TemporaryDirectory
from zipfile import ZipFile

from collect_public_snapshots import atomic_json
from dong_tile_geometry import PARENT_RADIUS, REVISION, SLOT_LABELS, vertices
from seoul_area_geography import area_shapes, gis, read_only, tile_center
from seoul_tile_population import PUBLIC, ROOT, digest, local_path

ROOT_INPUT = PUBLIC / "seoul-diorama-building-inputs-20261116-r2"
AREAS = PUBLIC / "seoul-area-evidence-20261125/seoul-area.sqlite"
BIOTOPE = PUBLIC / "dong-tile-intake-20261126/ecology/dong-tile-ecology.sqlite"
OUTPUT = PUBLIC / "dong-tile-intake-20261126/buildings/coverage"
SOURCE_ID = "AL_D010_11_20261109"
SOURCE_URL = "https://www.data.go.kr/data/15083092/fileData.do"
CHECK_MAX_POLYGONS_PER_BLOCK = 80_000
CHECK_PEAK_BYTES = 3 * (1024 ** 3)
CHECK_AREA_EPSILON_M2 = 1e-4
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (key TEXT PRIMARY KEY, path TEXT NOT NULL, sha256 TEXT NOT NULL, dataset TEXT NOT NULL);
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE building_polygon_parent (
    q INTEGER NOT NULL, r INTEGER NOT NULL,
    official_area_m2 REAL NOT NULL CHECK(official_area_m2>0.0),
    input_polygons_intersecting INTEGER NOT NULL CHECK(input_polygons_intersecting>=0),
    footprint_union_m2 REAL NOT NULL CHECK(footprint_union_m2>=0.0),
    raw_geometry_area_sum_m2 REAL NOT NULL CHECK(raw_geometry_area_sum_m2>=0.0),
    PRIMARY KEY(q,r)
) WITHOUT ROWID;
CREATE TABLE building_polygon_child (
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    official_area_m2 REAL NOT NULL CHECK(official_area_m2>=0.0),
    input_polygons_intersecting INTEGER NOT NULL CHECK(input_polygons_intersecting>=0),
    footprint_union_m2 REAL CHECK(footprint_union_m2>=0.0),
    raw_geometry_area_sum_m2 REAL CHECK(raw_geometry_area_sum_m2>=0.0),
    PRIMARY KEY(q,r,slot),FOREIGN KEY(q,r) REFERENCES building_polygon_parent(q,r),
    CHECK((official_area_m2>0.0)=(footprint_union_m2 IS NOT NULL)),
    CHECK((official_area_m2>0.0)=(raw_geometry_area_sum_m2 IS NOT NULL))
) WITHOUT ROWID;
CREATE VIEW v_building_polygon_child AS
    SELECT c.*,raw_geometry_area_sum_m2-footprint_union_m2 AS overlap_removed_m2,
           100.0*footprint_union_m2/nullif(official_area_m2,0) AS footprint_cover_percent,
           CASE WHEN official_area_m2=0.0 THEN 'outside_official_shape'
                WHEN input_polygons_intersecting=0 THEN 'no_valid_source_geometry_here'
                ELSE 'source_union_observed' END AS observation_state
    FROM building_polygon_child c;
CREATE VIEW v_building_polygon_parent AS
    SELECT p.*,raw_geometry_area_sum_m2-footprint_union_m2 AS overlap_removed_m2,
           100.0*footprint_union_m2/nullif(official_area_m2,0) AS footprint_cover_percent
    FROM building_polygon_parent p;
CREATE TABLE biotope_area_comparison (
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL,
    official_clip_area_m2 REAL NOT NULL, biotope_survey_known_area_m2 REAL NOT NULL,
    al_d010_footprint_percent REAL NOT NULL, biotope_source_building_percent REAL NOT NULL,
    PRIMARY KEY(q,r,slot),FOREIGN KEY(q,r,slot) REFERENCES building_polygon_child(q,r,slot)
) WITHOUT ROWID;
"""


def peak_working_set_bytes():
    if os.name != "nt":
        import resource

        result = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
        return result if sys.platform == "darwin" else result * 1024

    import ctypes
    from ctypes import wintypes

    class MemoryCounters(ctypes.Structure):
        _fields_ = [("size", wintypes.DWORD), ("page_faults", wintypes.DWORD)] + [
            (name, ctypes.c_size_t) for name in (
                "peak_working_set", "working_set", "quota_peak_paged_pool", "quota_paged_pool",
                "quota_peak_nonpaged_pool", "quota_nonpaged_pool", "pagefile_usage", "peak_pagefile_usage",
            )
        ]

    current_process = ctypes.windll.kernel32.GetCurrentProcess
    current_process.restype = wintypes.HANDLE
    get_usage = ctypes.windll.psapi.GetProcessMemoryInfo
    get_usage.argtypes = [wintypes.HANDLE, ctypes.POINTER(MemoryCounters), wintypes.DWORD]
    get_usage.restype = wintypes.BOOL
    memory = MemoryCounters()
    memory.size = ctypes.sizeof(memory)
    if not get_usage(current_process(), ctypes.byref(memory), memory.size):
        raise OSError("cannot_read_process_memory")
    return memory.peak_working_set


def reference(path, expected_sha, expected_bytes=None):
    path = local_path(path)
    actual = digest(path)
    if expected_bytes is not None and path.stat().st_size != expected_bytes:
        raise ValueError(f"footprint_input_byte_count_mismatch: {path.name}")
    if expected_sha and actual != expected_sha.lower():
        raise ValueError(f"footprint_input_sha256_mismatch: {path.name}")
    return path, actual


def read_inputs(district, pyproj, shapely):
    receipt_path = ROOT_INPUT / "receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    if receipt["source"]["sourceCrs"] != "EPSG:5186" or receipt["source"]["referenceDate"] != "2026-11-09":
        raise ValueError("unexpected_building_geometry_source")
    building_input = reference(ROOT_INPUT / receipt["database"]["path"], receipt["database"]["sha256"],
                               receipt["database"]["bytes"])
    raw = reference(Path(receipt["source"]["path"]), receipt["source"]["sha256"], receipt["source"]["bytes"])
    with closing(read_only(AREAS)) as index:
        release, = index.execute("""SELECT local_input,sha256,period FROM source_release WHERE id='area-boundaries'""")
        if release[2] != "2026-10":
            raise ValueError("official_seoul_boundary_period_changed")
        boundary = reference(ROOT / release[0], release[1])
        city_dongs = tuple(row[0] for row in index.execute("SELECT code FROM admin_dong ORDER BY code"))
        if district:
            selected = tuple(row[0] for row in index.execute(
                "SELECT code FROM admin_dong WHERE district_code=? ORDER BY code", (district,)))
            parent_keys = index.execute("""SELECT DISTINCT t.q,t.r
                         FROM tile_admin t JOIN admin_dong d ON d.code=t.admin_code
                         WHERE d.district_code=? ORDER BY t.q,t.r""", (district,)).fetchall()
        else:
            selected = city_dongs
            parent_keys = index.execute("SELECT q,r FROM tile ORDER BY q,r").fetchall()
    features = area_shapes(boundary[0], pyproj, shapely)
    if set(features) != set(city_dongs) or not selected:
        raise ValueError("official_seoul_boundary_scope_changed")
    mask = shapely.union_all([features[key] for key in selected])
    if mask.is_empty or not mask.is_valid:
        raise ValueError("official_seoul_scope_not_valid")
    source_rows = (
        ("gis-building-original", *raw, SOURCE_ID),
        ("gis-building-wkb", *building_input, SOURCE_ID),
        ("gis-building-receipt", receipt_path, digest(receipt_path), SOURCE_ID),
        ("official-area-index", AREAS, digest(AREAS), "2026-10 Seoul parent grid and administrative keys"),
        ("official-gemd", *boundary, "TL_SCCO_GEMD 2026-10"),
        ("biotope-comparison-only", BIOTOPE, digest(BIOTOPE), "Seoul ecological survey building-cover field"),
    )
    return building_input[0], mask, parent_keys, source_rows, receipt["dispositions"], len(selected)


def block_keys(parent_keys, span):
    by_block = defaultdict(list)
    for q, r in parent_keys:
        by_block[q // span, r // span].append((q, r))
    return by_block


def verify_source_topology(source, raw_path, shapely):
    import shapefile

    # WKB starts with byte-order and geometry type, then the ring or part count.
    complex_types = {
        "multipart": "substr(geometry_wkb,1,5)=x'0106000000'",
        "polygon_with_holes": "substr(geometry_wkb,1,5)=x'0103000000' AND substr(geometry_wkb,6,4)<>x'01000000'",
    }
    groups = {
        key: source.execute(f"""SELECT source_row,geometry_wkb FROM building
          WHERE geometry_status='ValidSourceGeometry' AND {condition} ORDER BY source_row""").fetchall()
        for key, condition in complex_types.items()
    }
    groups["evenly_spaced_rows"] = source.execute("""
        SELECT source_row,geometry_wkb FROM building
        WHERE geometry_status='ValidSourceGeometry' AND source_row % 4877=1 ORDER BY source_row
    """).fetchall()
    max_delta = 0.0
    with ZipFile(raw_path) as packed:
        member, = (name for name in packed.namelist() if name.lower().endswith(".shp"))
        with shapefile.Reader(shp=BytesIO(packed.read(member)),
                              shx=BytesIO(packed.read(member[:-4] + ".shx"))) as native:
            for group in groups.values():
                for row, wkb in group:
                    raw = native.shape(row - 1)
                    geometry = shapely.from_wkb(wkb)
                    if set(map(tuple, raw.points)) != set(map(tuple, shapely.get_coordinates(geometry))):
                        raise ValueError("footprint_raw_polygon_coordinate_set_differs_from_normalized")
                    ring_edges = [*raw.parts, len(raw.points)]
                    areas = []
                    for first, stop in zip(ring_edges, ring_edges[1:]):
                        ring = raw.points[first:stop]
                        origin_x, origin_y = ring[0]
                        positions = [(x - origin_x, y - origin_y) for x, y in ring]
                        areas.append(math.fsum(x * v - y * u for (x, y), (u, v) in
                                              zip(positions, positions[1:] + positions[:1])) / 2)
                    difference = abs(abs(math.fsum(areas)) - geometry.area)
                    if difference > 1e-5:
                        raise ValueError("footprint_raw_polygon_signed_ring_area_differs_from_normalized")
                    max_delta = max(max_delta, difference)
    return {"verifiedRowsByType": {name: len(rows) for name, rows in groups.items()},
            "maximumAreaDifferenceM2": max_delta, "publishedCoordinateSetDifferences": 0}


def source_block(db, keys, to_native, to_projected, shapely, np):
    xs, ys = zip(*(tile_center(*key) for key in keys))
    xmin, ymin, xmax, ymax = min(xs) - PARENT_RADIUS, min(ys) - PARENT_RADIUS, max(xs) + PARENT_RADIUS, max(ys) + PARENT_RADIUS
    west, south, east, north = to_native.transform_bounds(xmin, ymin, xmax, ymax, densify_pts=41)
    rows = db.execute("""
        SELECT b.source_row,b.geometry_wkb
        FROM building_bounds rt CROSS JOIN building b ON b.source_row=rt.source_row
        WHERE rt.min_x <= ? AND rt.max_x >= ? AND rt.min_y <= ? AND rt.max_y >= ?
          AND b.geometry_status='ValidSourceGeometry'
        ORDER BY b.source_row LIMIT ?
    """, (east + 2, west - 2, north + 2, south - 2, CHECK_MAX_POLYGONS_PER_BLOCK + 1)).fetchall()
    if len(rows) > CHECK_MAX_POLYGONS_PER_BLOCK:
        raise ValueError("footprint_block_too_large_reduce_block_span")
    if not rows:
        return np.array([], dtype=np.int64), np.array([], dtype=object), set(), 0
    ids, wkb = zip(*rows)
    native = shapely.from_wkb(np.asarray(wkb, dtype=object))
    projected = shapely.transform(native, to_projected.transform, interleaved=False)
    valid = shapely.is_valid(projected) & ~shapely.is_empty(projected) & (shapely.area(projected) > 0)
    accepted_ids = np.asarray(ids)[valid]
    return accepted_ids, projected[valid], {ids[i] for i in np.nonzero(~valid)[0]}, len(rows)


def positive_pieces(footprints, mask, shapely):
    pieces = shapely.intersection(footprints, mask)
    areas = shapely.area(pieces)
    selected = areas > 0.0
    return pieces[selected], areas[selected], selected


def parent_measure(q, r, official, tree, shapes, row_ids, shapely, audit, tested):
    parent = shapely.Polygon(vertices(q, r))
    mask = official.intersection(parent)
    if mask.area <= 0:
        raise ValueError(f"footprint_parent_present_without_official_area: {q},{r}")
    hits = tree.query(mask, predicate="intersects")
    pieces, raw_areas, selected = positive_pieces(shapes[hits], mask, shapely)
    raw_total = float(raw_areas.sum())
    actual = shapely.union_all(pieces)
    if not actual.is_valid:
        raise ValueError(f"dissolved_parent_polygon_invalid: {q},{r}")
    parent_total = actual.area
    if parent_total > mask.area + CHECK_AREA_EPSILON_M2 or raw_total < parent_total - CHECK_AREA_EPSILON_M2:
        raise ValueError(f"bad_footprint_union_parent_area: {q},{r}")
    if raw_total - parent_total > CHECK_AREA_EPSILON_M2:
        audit["parentsWithOverlapRemoved"] += 1
    children = []
    for slot in range(len(SLOT_LABELS)):
        child = shapely.Polygon(vertices(q, r, slot))
        child_scope = mask.intersection(child).area
        if child_scope <= 0.0:
            children.append((q, r, slot, 0.0, 0, None, None))
            continue
        shapely.prepare(child)
        subset = pieces[shapely.intersects(child, pieces)]
        clips, sums, _ = positive_pieces(subset, child, shapely)
        sum_m2 = float(sums.sum())
        union_m2 = actual.intersection(child).area
        if union_m2 > child_scope + CHECK_AREA_EPSILON_M2 or sum_m2 < union_m2 - CHECK_AREA_EPSILON_M2:
            raise ValueError(f"bad_footprint_union_child_area: {q},{r},{slot}")
        if tested:
            direct = shapely.union_all(clips).area
            difference = abs(direct - union_m2)
            audit["maximumIndependentChildUnionDifferenceM2"] = max(audit["maximumIndependentChildUnionDifferenceM2"], difference)
            audit["independentChildUnionChecks"] += 1
            if difference > CHECK_AREA_EPSILON_M2:
                raise ValueError(f"independent_child_union_mismatch: {q},{r},{slot}")
        children.append((q, r, slot, child_scope, len(clips), union_m2, sum_m2))
    errors = {
        "maxChildOfficialAreaErrorM2": abs(math.fsum(row[3] for row in children) - mask.area),
        "maxChildUnionAreaErrorM2": abs(math.fsum(row[5] or 0.0 for row in children) - parent_total),
        "maxChildRawSumAreaErrorM2": abs(math.fsum(row[6] or 0.0 for row in children) - raw_total),
    }
    if max(errors.values()) > CHECK_AREA_EPSILON_M2:
        raise ValueError(f"footprint_children_do_not_conserve_parent: {q},{r}: {errors}")
    for key, err in errors.items():
        audit[key] = max(audit[key], err)
    return (q, r, mask.area, len(pieces), parent_total, raw_total), children, row_ids[hits[selected]].tolist()


def compare_biotope(db):
    db.execute("ATTACH DATABASE ? AS biotope", (BIOTOPE.resolve().as_uri() + "?mode=ro",))
    db.execute("""
        INSERT INTO biotope_area_comparison
        SELECT p.q,p.r,p.slot,p.official_area_m2,b.known_source_area_m2,
               p.footprint_cover_percent,b.pct_of_known_survey_source
        FROM v_building_polygon_child p JOIN biotope.v_tile_cover_reference b USING(q,r,slot)
        WHERE b.key='building_cover' AND b.coverage_state='full_survey'
          AND b.pct_of_known_survey_source IS NOT NULL AND p.official_area_m2>0
          AND abs(p.official_area_m2-b.tile_area_m2)<=1.0
          AND abs(p.official_area_m2-b.known_source_area_m2)<=1.0
    """)
    rows = db.execute("""SELECT al_d010_footprint_percent-biotope_source_building_percent
                         FROM biotope_area_comparison ORDER BY q,r,slot""").fetchall()
    deltas = [row[0] for row in rows]
    examples = []
    for name, q, r, slot in (("사가정", -32, -179, 2), ("홍대입구", -75, -189, 3),
                             ("여의도", -68, -204, 2), ("면목7동 산지", -28, -183, 2)):
        row = db.execute("SELECT al_d010_footprint_percent,biotope_source_building_percent FROM biotope_area_comparison "
                         "WHERE q=? AND r=? AND slot=?", (q, r, slot)).fetchone()
        if row:
            examples.append({"name": name, "key": [q, r, slot], "geometryPercent": row[0], "biotopePercent": row[1]})
    return {"comparableFullDenominatorChildren": len(deltas),
            "meanSignedDifferencePercentagePoints": mean(deltas) if deltas else None,
            "medianAbsoluteDifferencePercentagePoints": median(abs(value) for value in deltas) if deltas else None,
            "examples": examples,
            "agreementMeaning": "Different surveys and definitions, with like-area denominators. No values were merged."}


def compute(db, source, official, keys, block_span, pyproj, shapely, np):
    to_native = pyproj.Transformer.from_crs(5179, 5186, always_xy=True)
    to_grid = pyproj.Transformer.from_crs(5186, 5179, always_xy=True)
    blocks = block_keys(keys, block_span)
    used_ids = set()
    unprojected = set()
    excess = Counter()
    tile_count = 0
    largest_block = 0
    for block_index, block in enumerate(sorted(blocks), start=1):
        ids, shapes, invalid_ids, fetched = source_block(source, blocks[block], to_native, to_grid, shapely, np)
        unprojected.update(invalid_ids)
        largest_block = max(largest_block, fetched)
        tree = shapely.STRtree(shapes)
        for q, r in blocks[block]:
            checked = tile_count % 137 == 0 or (q, r) == (-32, -179)
            parent, children, rows = parent_measure(q, r, official, tree, shapes, ids, shapely, excess, checked)
            used_ids.update(rows)
            db.execute("INSERT INTO building_polygon_parent VALUES(?,?,?,?,?,?)", parent)
            db.executemany("INSERT INTO building_polygon_child VALUES(?,?,?,?,?,?,?)", children)
            tile_count += 1
        if peak_working_set_bytes() > CHECK_PEAK_BYTES:
            raise MemoryError("footprint_process_3gib_limit_reached")
        if block_index % 20 == 0 or block_index == len(blocks):
            print(f"Footprint coverage: blocks {block_index}/{len(blocks)}, parents {tile_count}/{len(keys)}", file=sys.stderr, flush=True)
    count, area, union, raw = db.execute("""
        SELECT count(*),sum(official_area_m2),sum(footprint_union_m2),sum(raw_geometry_area_sum_m2)
        FROM building_polygon_parent
    """).fetchone()
    if count != len(keys) or abs(area - official.area) > 1e-3:
        raise ValueError("official_area_not_conserved_by_parent_tiles")
    child_counts = db.execute("""
        SELECT count(*),sum(official_area_m2=0.0),sum(input_polygons_intersecting=0 AND official_area_m2>0.0)
        FROM building_polygon_child
    """).fetchone()
    if child_counts[0] != count * len(SLOT_LABELS):
        raise ValueError("footprint_child_slot_count_changed")
    return {
        "parentTiles": count, "childSlots": child_counts[0], "outsideScopeSlots": child_counts[1],
        "inScopeChildrenWithoutValidSourceGeometry": child_counts[2],
        "officialScopeBoundaryAreaM2": official.area, "summedParentOfficialAreaM2": area,
        "districtUnionAbsoluteAreaErrorM2": abs(area - official.area),
        "trueFootprintUnionM2": union, "separatelySummedClippedRawGeometryM2": raw,
        "overlapRemovedM2": raw - union, "footprintUnionSharePercent": 100.0 * union / area,
        "distinctOriginalFootprintRowsIntersectingScope": len(used_ids),
        "projectionInvalidDistinctRowsWithheld": len(unprojected),
        "maxSourcePolygonsLoadedInOneBlock": largest_block, "blocks": len(blocks), **excess,
    }


def build(output, district, block_span):
    import numpy as np

    pyproj, shapely = gis()
    source_path, mask, keys, sources, invalidity, dong_count = read_inputs(district, pyproj, shapely)
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as stage:
        temp = Path(stage) / "building-polygon-coverage.sqlite"
        with (closing(read_only(source_path)) as source,
              closing(sqlite3.connect(temp.resolve().as_uri() + "?mode=rwc", uri=True)) as db, db):
            source.execute("PRAGMA cache_size=-32768")
            indexed_valid, = source.execute("""SELECT count(*) FROM building_bounds rt
                CROSS JOIN building b ON b.source_row=rt.source_row WHERE b.geometry_status='ValidSourceGeometry'""").fetchone()
            if indexed_valid != invalidity["ValidSourceGeometry"]:
                raise ValueError("footprint_spatial_index_point_count_differs_from_receipt")
            db.executescript(SCHEMA)
            db.executemany("INSERT INTO source VALUES(?,?,?,?)", (
                (source_key, path.relative_to(ROOT).as_posix(), hash_value, dataset) for source_key, path, hash_value, dataset in sources))
            db.executemany("INSERT INTO metadata VALUES(?,?)", (
                ("grid_revision", REVISION), ("source_crs", "EPSG:5186"), ("tile_crs", "EPSG:5179"),
                ("boundary_release", "TL_SCCO_GEMD 2026-10"), ("scope", district or "Seoul all districts"),
                ("use_boundary", "same_pc_internal_review"), ("source_url", SOURCE_URL),
            ))
            raw_path = next(path for key, path, _, _ in sources if key == "gis-building-original")
            raw_topology_check = verify_source_topology(source, raw_path, shapely)
            audit = compute(db, source, mask, keys, block_span, pyproj, shapely, np)
            overlap_difference = compare_biotope(db)
            if db.execute("PRAGMA foreign_key_check").fetchone() or db.execute("PRAGMA integrity_check").fetchone() != ("ok",):
                raise ValueError("footprint_database_integrity_failed")
        final = output / temp.name
        temp.replace(final)
    peak = peak_working_set_bytes()
    if peak > CHECK_PEAK_BYTES:
        raise MemoryError("footprint_process_3gib_limit_reached")
    audit.update({
        "boundaryScope": district or "Seoul all districts", "dongCount": dong_count,
        "gridRevision": REVISION, "databaseSha256": digest(final), "databaseBytes": final.stat().st_size,
        "sourceDataset": SOURCE_ID, "sourceUrl": SOURCE_URL, "rawGeometryInventory": invalidity,
        "rawTopologyCheck": raw_topology_check,
        "sourceFingerprints": {key: hash_value for key, _, hash_value, _ in sources},
        "officialBoundaryEdition": "TL_SCCO_GEMD 2026-10", "biotopeComparison": overlap_difference,
        "peakProcessWorkingSetBytes": peak, "peakProcessBudgetBytes": CHECK_PEAK_BYTES,
        "validPolygonPolicy": "Only unchanged valid source footprints. No geometry repair or source polygon ID exported.",
        "areaMeasurement": "Measure true union and raw geometric area sum within the official clipped tile, in EPSG:5179.",
        "countMeaning": "Source polygon intersections, not unique across cells. Areas are additive across child slots.",
        "useBoundary": "same_pc_internal_review_only", "distributionApproved": False,
        "rawBuildingGeometryExported": False, "officialBoundaryGeometryExported": False,
        "integrityCheck": "passed", "foreignKeyCheck": "passed",
    })
    atomic_json(output / "building-polygon-coverage-summary.json", audit)
    return audit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--district")
    parser.add_argument("--block-span", type=int, choices=range(1, 13), default=9)
    args = parser.parse_args()
    output = args.output
    if args.district and output == OUTPUT:
        if len(args.district) != 5 or not args.district.isdigit():
            raise ValueError("invalid_district_code")
        output = output / f"pilot-{args.district}"
    print(json.dumps(build(local_path(output), args.district, args.block_span), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
