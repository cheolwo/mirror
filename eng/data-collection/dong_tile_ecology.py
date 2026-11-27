"""Index Seoul ecological survey areas and public shade observations by tile."""

from collections import Counter
from contextlib import closing
from datetime import date
import gzip
import io
import json
import math
from pathlib import Path
import sqlite3
import sys
from tempfile import TemporaryDirectory
from zipfile import ZipFile

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, SLOT_LABELS, point_key, vertices
from seoul_area_geography import BOUNDARIES, area_shapes, check_database, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest

OUTPUT = PUBLIC / "dong-tile-intake-20261126/ecology"
PARENT_DB = PUBLIC / "seoul-area-evidence-20261125/seoul-area.sqlite"
BIOTOPE = PUBLIC / "diorama-public-layer-intake-20261123/r84-greenland-biotope/raw/seoul-biotope-individual-20250827.zip"
BIOTOPE_SHA = "f58321845ba16ab60aa30385d75ab5ece7b4030ac25b311edf4b7c3206ff1839"
BIOTOPE_URL = "https://data.seoul.go.kr/dataList/OA-21126/F/1/datasetView.do"
BIOTOPE_MEMBER = "개별비오톱(2025)/UPIS_BIOTOP_SRC_2025."
SHADE = "national-shade-15129447"
METRICS = (
    ("green_cover", "녹지율"),
    ("impermeable_paving", "불투포장율"),
    ("permeable_paving", "투수포장율"),
    ("building_cover", "건폐율"),
    ("other_cover", "기타비율"),
)
MIN_AREA = 1e-6
AREA_TOLERANCE = 0.03
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_release(
    source_id TEXT PRIMARY KEY, provider TEXT NOT NULL, dataset_id TEXT NOT NULL,
    basis TEXT NOT NULL, url TEXT, local_input TEXT NOT NULL, sha256 TEXT NOT NULL, rights_state TEXT NOT NULL
);
CREATE TABLE tile_footprint(
    q INTEGER, r INTEGER, slot INTEGER CHECK(slot BETWEEN -1 AND 6),
    tile_area_m2 REAL NOT NULL, surveyed_union_m2 REAL NOT NULL,
    source_area_sum_m2 REAL NOT NULL, overlapping_source_m2 REAL NOT NULL,
    source_feature_count INTEGER NOT NULL, coverage_state TEXT NOT NULL,
    PRIMARY KEY(q,r,slot)
) WITHOUT ROWID;
CREATE TABLE biotope_feature(
    source_row INTEGER PRIMARY KEY, provider_id TEXT, reported_district TEXT NOT NULL,
    land_use TEXT, biotope_class TEXT, vegetation_class TEXT,
    reported_area_m2 REAL, projected_area_m2 REAL, geometry_state TEXT NOT NULL, coverage_check TEXT NOT NULL,
    green_cover_pct REAL, impermeable_paving_pct REAL, permeable_paving_pct REAL,
    building_cover_pct REAL, other_cover_pct REAL
);
CREATE TABLE biotope_child_contribution(
    q INTEGER, r INTEGER, slot INTEGER CHECK(slot BETWEEN 0 AND 6),
    source_row INTEGER REFERENCES biotope_feature(source_row),
    overlap_m2 REAL NOT NULL CHECK(overlap_m2>0),
    PRIMARY KEY(q,r,slot,source_row),
    FOREIGN KEY(q,r,slot) REFERENCES tile_footprint(q,r,slot)
) WITHOUT ROWID;
CREATE TABLE metric(
    id INTEGER PRIMARY KEY, key TEXT NOT NULL UNIQUE, source_field TEXT NOT NULL
);
CREATE TABLE tile_cover_metric(
    q INTEGER, r INTEGER, slot INTEGER, metric_id INTEGER REFERENCES metric(id),
    known_source_area_m2 REAL NOT NULL CHECK(known_source_area_m2>0),
    area_weighted_numerator_m2 REAL NOT NULL,
    PRIMARY KEY(q,r,slot,metric_id),
    FOREIGN KEY(q,r,slot) REFERENCES tile_footprint(q,r,slot)
) WITHOUT ROWID;
CREATE TABLE district_survey_summary(
    district_name TEXT PRIMARY KEY, source_feature_rows INTEGER NOT NULL,
    valid_geometry_rows INTEGER NOT NULL, projected_source_area_sum_m2 REAL NOT NULL
);
CREATE TABLE district_cover_metric(
    district_name TEXT REFERENCES district_survey_summary(district_name),
    metric_id INTEGER REFERENCES metric(id), known_source_area_m2 REAL NOT NULL,
    area_weighted_numerator_m2 REAL NOT NULL, PRIMARY KEY(district_name,metric_id)
) WITHOUT ROWID;
CREATE TABLE shade_observation(
    source_row INTEGER PRIMARY KEY, candidate_key TEXT NOT NULL, provider_code TEXT,
    province TEXT, reported_district TEXT, location_name TEXT, canopy_type TEXT, position_description TEXT,
    installed_year INTEGER, reported_height REAL, reported_unfolded_diameter REAL,
    basis_date TEXT, longitude REAL, latitude REAL, geometry_state TEXT NOT NULL
);
CREATE INDEX shade_candidate_source ON shade_observation(candidate_key,source_row);
CREATE TABLE shade_tile_candidate(
    source_row INTEGER PRIMARY KEY REFERENCES shade_observation(source_row),
    q INTEGER, r INTEGER, slot INTEGER CHECK(slot BETWEEN 0 AND 6),
    admin_dong_code TEXT, boundary_district TEXT, join_state TEXT NOT NULL,
    FOREIGN KEY(q,r,slot) REFERENCES tile_footprint(q,r,slot)
);
CREATE INDEX shade_candidate_cell ON shade_tile_candidate(q,r,slot);
CREATE VIEW v_tile_cover_reference AS
SELECT f.q,f.r,f.slot,m.key,m.source_field,f.coverage_state,f.tile_area_m2,f.surveyed_union_m2,
       f.overlapping_source_m2,f.source_feature_count,v.known_source_area_m2,v.area_weighted_numerator_m2,
       CASE WHEN f.coverage_state<>'overlapping_source_polygons'
            THEN 100.0*v.area_weighted_numerator_m2/v.known_source_area_m2 END AS pct_of_known_survey_source
FROM tile_footprint f CROSS JOIN metric m
LEFT JOIN tile_cover_metric v ON v.q=f.q AND v.r=f.r AND v.slot=f.slot AND v.metric_id=m.id;
CREATE VIEW v_shade_tile_reference AS
SELECT t.q,t.r,t.slot,t.admin_dong_code,t.boundary_district,t.join_state,o.*
FROM shade_tile_candidate t JOIN shade_observation o USING(source_row);
"""


def number(raw):
    try:
        value = float(raw)
    except (TypeError, ValueError):
        return None
    return value if math.isfinite(value) else None


def percent(raw):
    value = number(raw)
    return value if value is not None and 0 <= value <= 100 else None


def cover_check(p):
    green, impermeable, permeable, building, other = p
    surfaces = (green, impermeable, permeable, other)
    if None in surfaces:
        return "value_review"
    surface_sum = sum(surfaces)
    if abs(surface_sum - 100) <= 1:
        return "surface_sum_close_to_100"
    if building is not None and abs(surface_sum + building - 100) <= 1:
        return "surface_plus_building_sum_close_to_100"
    return "sum_difference_over_one_point"


def load_biotope(db, shapely, pyproj, shapefile):
    from shapely.ops import transform

    if digest(BIOTOPE) != BIOTOPE_SHA:
        raise ValueError("Biotope survey source SHA-256 changed")
    records = []
    geoms = []
    identities = []
    fractions = []
    districts = {}
    counts = Counter()
    with ZipFile(BIOTOPE) as source:
        crs = pyproj.CRS.from_wkt(source.read(BIOTOPE_MEMBER + "prj").decode("ascii"))
        if crs.to_epsg() != 5174:
            raise ValueError("Biotope survey CRS not EPSG:5174")
        convert = pyproj.Transformer.from_crs(crs, 5179, always_xy=True).transform
        shapes = shapefile.Reader(encoding="cp949", **{
            ext: io.BytesIO(source.read(BIOTOPE_MEMBER + ext)) for ext in ("dbf", "shp", "shx")
        })
        for source_row, item in enumerate(shapes.iterShapeRecords(), 1):
            r = item.record.as_dict()
            fractions_row = tuple(percent(r[field]) for _, field in METRICS)
            check = cover_check(fractions_row)
            district = r["자치구"] or "지역 미기재"
            stats = districts.setdefault(district, [0, 0, 0.0, [[0.0, 0.0] for _ in METRICS]])
            stats[0] += 1
            geom = shapely.geometry.shape(item.shape.__geo_interface__)
            state = "invalid_or_empty_geometry"
            area = None
            if geom.is_valid and geom.area > MIN_AREA:
                projected = transform(convert, geom)
                if projected.is_valid and projected.area > MIN_AREA:
                    geoms.append(projected)
                    identities.append(source_row)
                    fractions.append(fractions_row)
                    area = projected.area
                    state = "valid"
                    stats[1] += 1
                    stats[2] += area
                    for pct, pair in zip(fractions_row, stats[3]):
                        if pct is not None:
                            pair[0] += area
                            pair[1] += area * pct / 100.0
                else:
                    state = "invalid_after_projection"
            counts[state] += 1
            counts[check] += 1
            records.append((source_row, str(r["고유번호"]) if r["고유번호"] else None, district, r["토지이용"],
                            r["비오톱유형"], r["현존식생"], number(r["면적"]), area, state, check, *fractions_row))
    db.executemany("INSERT INTO biotope_feature VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", records)
    for district, stats in sorted(districts.items()):
        db.execute("INSERT INTO district_survey_summary VALUES (?,?,?,?)", (district, *stats[:3]))
        db.executemany("INSERT INTO district_cover_metric VALUES (?,?,?,?)", (
            (district, metric_id, known, weighted)
            for metric_id, (known, weighted) in enumerate(stats[3], 1) if known > 0
        ))
    return geoms, identities, fractions, dict(counts)


def measure(db, shapely, np, coordinates, polygon, pieces, areas, source_rows, percentages):
    surveyed = float(shapely.union_all(pieces).area) if len(pieces) else 0.0
    summed = float(areas.sum())
    overlap = max(0.0, summed - surveyed)
    if surveyed <= MIN_AREA:
        state = "no_survey_intersection"
    elif overlap > AREA_TOLERANCE:
        state = "overlapping_source_polygons"
    elif polygon.area - surveyed > AREA_TOLERANCE:
        state = "partial_survey"
    else:
        state = "full_survey"
    db.execute("INSERT INTO tile_footprint VALUES (?,?,?,?,?,?,?,?,?)", (
        *coordinates, polygon.area, surveyed, summed, overlap, len(pieces), state,
    ))
    metrics = []
    for metric_id, metric_values in enumerate(percentages.T, 1):
        mask = ~np.isnan(metric_values)
        known = float(areas[mask].sum())
        weighted = float((areas[mask] * metric_values[mask] / 100.0).sum())
        metrics.extend((known, weighted))
        if known > 0:
            db.execute("INSERT INTO tile_cover_metric VALUES (?,?,?,?,?,?)", (
                *coordinates, metric_id, known, weighted,
            ))
    if coordinates[2] >= 0:
        db.executemany("INSERT INTO biotope_child_contribution VALUES (?,?,?,?,?)", (
            (*coordinates, int(row), float(area)) for row, area in zip(source_rows, areas)
        ))
    return [polygon.area, surveyed, summed, *metrics]


def load_surfaces(db, shapely, tile_keys, geoms, source_rows, percentages):
    import numpy as np

    geoms = np.asarray(geoms, dtype=object)
    source_rows = np.asarray(source_rows)
    percentages = np.asarray(percentages, dtype=float)
    tree = shapely.STRtree(geoms)
    maximum_deltas = np.zeros(3 + 2 * len(METRICS))
    for q, r in sorted(tile_keys):
        polygon = shapely.Polygon(vertices(q, r))
        hits = np.sort(tree.query(polygon, predicate="intersects"))
        pieces = shapely.intersection(polygon, geoms[hits])
        areas = shapely.area(pieces)
        present = areas > MIN_AREA
        pieces, areas, hits = pieces[present], areas[present], hits[present]
        parent = measure(db, shapely, np, (q, r, -1), polygon, pieces, areas, source_rows[hits], percentages[hits])
        child_totals = np.zeros(len(parent))
        for slot in range(len(SLOT_LABELS)):
            child = shapely.Polygon(vertices(q, r, slot))
            child_pieces = shapely.intersection(child, pieces)
            child_areas = shapely.area(child_pieces)
            present = child_areas > MIN_AREA
            child_hits = hits[present]
            split = measure(db, shapely, np, (q, r, slot), child, child_pieces[present], child_areas[present],
                            source_rows[child_hits], percentages[child_hits])
            child_totals += split
        deltas = np.abs(child_totals - parent)
        maximum_deltas = np.maximum(maximum_deltas, deltas)
        if np.any(deltas > AREA_TOLERANCE):
            raise ValueError(f"Biotope parent-child conservation failed at ({q},{r}): {deltas}")
    return maximum_deltas.tolist()


def read_shades(db, output, shapely, pyproj, tile_keys, districts, dongs):
    from hashlib import sha256

    receipt = json.loads((output / f"{SHADE}.receipt.json").read_text(encoding="utf-8"))
    input_path = output / receipt["retainedFile"]
    if digest(input_path) != receipt["retainedSha256"]:
        raise ValueError("Shade responses retained SHA-256 changed")
    codes = sorted(dongs)
    tree = shapely.STRtree([dongs[code] for code in codes])
    convert = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    source_rows = []
    joins = []
    with gzip.open(input_path, "rt", encoding="utf-8") as lines:
        for index, line in enumerate(lines, 1):
            shade = json.loads(line)
            province, district = shade["CTPV_NM"].strip(), shade["SGG_NM"].strip()
            lat, lon = number(shade["LAT"]), number(shade["LOT"])
            coordinate_ok = lat is not None and lon is not None and 32 <= lat <= 40 and 123 <= lon <= 133
            precision = "published_coordinate" if coordinate_ok else "missing_or_out_of_range_coordinates"
            y = number(shade.get("INSTL_YR"))
            year = int(y) if y is not None and y.is_integer() and 1900 <= y <= 2100 else None
            try:
                basis = date.fromisoformat(shade["DATA_CRTR_YMD"]).isoformat()
            except (ValueError, TypeError):
                basis = None
            key_values = [province, district, shade.get("INSTT_CODE"), shade.get("INSTL_PLC_NM"),
                          shade.get("SHADE_CANOPY_TYPE"), lon, lat]
            candidate_key = sha256(json.dumps(key_values, ensure_ascii=False).encode("utf-8")).hexdigest()
            source_rows.append((index, candidate_key, shade.get("INSTT_CODE"), province, district,
                                shade["INSTL_PLC_NM"], shade.get("SHADE_CANOPY_TYPE"), shade.get("ACTL_PSTN"), year,
                                number(shade.get("WHOL_HGT")), number(shade.get("WHOL_WDTH_LEN")), basis, lon, lat, precision))
            if province != "서울특별시":
                continue
            q = r = slot = dong_code = boundary_district = None
            state = "missing_or_out_of_range_coordinates"
            if coordinate_ok:
                east, north = convert(lon, lat)
                point = shapely.Point(east, north)
                membership = tree.query(point, predicate="covered_by")
                if len(membership) == 1:
                    dong_code = codes[int(membership[0])]
                    boundary_district = districts[dong_code]
                    state = "district_agrees" if boundary_district == district else "reported_district_disagrees"
                elif len(membership) > 1:
                    state = "ambiguous_dong_boundary"
                else:
                    state = "outside_seoul_boundary"
                key = point_key(east, north)
                if key[:2] in tile_keys:
                    q, r, slot = key
                elif state == "district_agrees":
                    raise ValueError("Shade boundary match absent from tile keys")
            joins.append((index, q, r, slot, dong_code, boundary_district, state))
    if len(source_rows) != receipt["rows"]:
        raise ValueError("Shade row count mismatch")
    db.executemany("INSERT INTO shade_observation VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", source_rows)
    db.executemany("INSERT INTO shade_tile_candidate VALUES (?,?,?,?,?,?,?)", joins)
    return input_path, receipt, dict(Counter(row[-1] for row in joins))


def load_parents():
    with closing(read_only(PARENT_DB)) as db:
        tile_keys = set(db.execute("SELECT q,r FROM tile"))
        districts = dict(db.execute("""
            SELECT a.code,d.name FROM admin_dong a JOIN district d ON d.code=a.district_code
        """))
        boundary_hash = db.execute("SELECT sha256 FROM source_release WHERE id='area-boundaries'").fetchone()
    if len(tile_keys) != 8127 or not boundary_hash or digest(BOUNDARIES) != boundary_hash[0]:
        raise ValueError("Parent tile key count or official boundary SHA changed")
    return tile_keys, districts


def build(output=OUTPUT):
    sys.path.insert(0, str(PUBLIC / "gis-runtime-r1"))
    import shapefile

    pyproj, shapely = gis()
    tile_keys, districts = load_parents()
    dongs = area_shapes(BOUNDARIES, pyproj, shapely)
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as temporary:
        temporary_db = Path(temporary) / "dong-tile-ecology.sqlite"
        with closing(sqlite3.connect(temporary_db)) as db:
            db.executescript(SCHEMA)
            db.executemany("INSERT INTO metric VALUES (?,?,?)", (
                (index, *item) for index, item in enumerate(METRICS, 1)
            ))
            geoms, source_rows, percent_rows, bio_stats = load_biotope(db, shapely, pyproj, shapefile)
            conservation = load_surfaces(db, shapely, tile_keys, geoms, source_rows, percent_rows)
            shade_path, shade_receipt, shade_stats = read_shades(db, output, shapely, pyproj, tile_keys, districts, dongs)
            db.executemany("INSERT INTO source_release VALUES (?,?,?,?,?,?,?,?)", (
                ("seoul-biotope", "서울특별시", "OA-21126", "2025년 기준, 2025-08-27 첨부",
                 BIOTOPE_URL, BIOTOPE.relative_to(ROOT).as_posix(), BIOTOPE_SHA, "portal_unrestricted_local_review"),
                ("national-shade", shade_receipt["provider"], shade_receipt["datasetId"], "관측 행 DATA_CRTR_YMD",
                 shade_receipt["url"], shade_path.relative_to(ROOT).as_posix(), shade_receipt["retainedSha256"],
                 "provider_policy_reference_local_review"),
                ("seoul-tile-lattice", "Hongdal", REVISION, "서울 300 m 모칸 기준점 및 세부칸 후보 규격",
                 None, PARENT_DB.relative_to(ROOT).as_posix(), digest(PARENT_DB), "internal_projection")
            ))
            db.commit()
            check_database(db)
            stats = {
                "biotopeSource": bio_stats,
                "tileCoverage": {str(level): dict(db.execute("""
                    SELECT coverage_state,count(*) FROM tile_footprint
                    WHERE (slot=-1)=? GROUP BY coverage_state
                """, (level == "parent",))) for level in ("parent", "child")},
                "conservationMaximumAcrossParentTilesM2": {key: delta for key, delta in zip(
                    ["tileArea", "surveyedUnionArea", "sourceAreaSum"] +
                    [f"{key}_{part}" for key, _ in METRICS for part in ("knownArea", "weightedNumerator")],
                    conservation,
                )},
                "districtCount": len(db.execute("SELECT * FROM district_survey_summary").fetchall()),
                "childContributionRows": db.execute("SELECT count(*) FROM biotope_child_contribution").fetchone()[0],
                "shadeNationalRows": shade_receipt["rows"],
                "shadeNationalUniqueCandidateKeys": db.execute(
                    "SELECT count(DISTINCT candidate_key) FROM shade_observation").fetchone()[0],
                "shadeSeoulJoinStates": shade_stats,
                "shadeSeoulCandidateDongCount": db.execute(
                    "SELECT count(DISTINCT admin_dong_code) FROM shade_tile_candidate WHERE join_state='district_agrees'"
                ).fetchone()[0],
                "shadeJungnangDistrictAgrees": db.execute(
                    "SELECT count(*) FROM shade_tile_candidate WHERE join_state='district_agrees' AND boundary_district='중랑구'"
                ).fetchone()[0],
                "shadeSeoulMinMaxBasisDate": db.execute("""
                    SELECT min(basis_date),max(basis_date) FROM shade_observation WHERE province='서울특별시'
                """).fetchone(),
                "databaseIntegrity": "ok",
                "redistributionApproved": False,
                "unityBinding": "not_run",
            }
        destination = output / "dong-tile-ecology.sqlite"
        temporary_db.replace(destination)
    stats["databaseSha256"] = digest(destination)
    stats["databaseBytes"] = destination.stat().st_size
    atomic_json(output / "verification.json", stats)
    print(json.dumps(stats, ensure_ascii=False))


if __name__ == "__main__":
    build()
