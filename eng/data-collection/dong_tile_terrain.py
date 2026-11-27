"""Index native NASADEM samples and independent Seoul spot heights by the reviewed child grid."""

import argparse
from collections import defaultdict
from contextlib import closing
from dataclasses import dataclass, field
from io import BytesIO
import json
import math
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory
from zipfile import ZipFile

from collect_public_snapshots import atomic_json
from dong_tile_geometry import PARENT_RADIUS, REVISION, SLOT_LABELS, point_key
from seoul_area_geography import area_shapes, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest, local_path


AREAS = PUBLIC / "seoul-area-evidence-20261125/seoul-area.sqlite"
CORRECTION = PUBLIC / "terrain-grid-phase-correction-20261116-r2/receipt/replacement-table.json"
SPOT_RECEIPT = PUBLIC / "admin-dong-terrain-contour-20260926-r1/raw/receipt.json"
OUTPUT = PUBLIC / "dong-tile-intake-20261126/terrain"
NASA_URL = "https://portal.opentopography.org/datasetMetadata?otCollectionID=OT.032021.4326.2"
NASA_PERIOD = "2000-02-11/2000-02-21"
NASA_VERTICAL_REF = "EGM96 orthometric elevation (EPSG:5773)"
NASA_NODATA = -32768
SPOT_FILENAME = "OA-22241-seoul-contour.zip"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE terrain_source (
    source_id TEXT PRIMARY KEY, dataset_id TEXT NOT NULL, path TEXT NOT NULL,
    sha256 TEXT NOT NULL, url TEXT, reference_period TEXT, horizontal_crs TEXT,
    vertical_reference TEXT, source_kind TEXT NOT NULL
);
CREATE TABLE terrain_metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE dong (code TEXT PRIMARY KEY, district_code TEXT NOT NULL, name TEXT NOT NULL);
CREATE TABLE parent (q INTEGER NOT NULL, r INTEGER NOT NULL, PRIMARY KEY(q,r)) WITHOUT ROWID;
CREATE TABLE child (
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    PRIMARY KEY(q,r,slot), FOREIGN KEY(q,r) REFERENCES parent(q,r)
) WITHOUT ROWID;
CREATE TABLE terrain_dong_child (
    dong_code TEXT NOT NULL REFERENCES dong(code), q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    sample_count INTEGER NOT NULL CHECK(sample_count>0),
    elevation_count INTEGER NOT NULL CHECK(elevation_count BETWEEN 0 AND sample_count),
    elevation_sum_m INTEGER,elevation_min_m INTEGER,elevation_max_m INTEGER,
    slope_count INTEGER NOT NULL CHECK(slope_count BETWEEN 0 AND elevation_count),
    slope_sum_deg REAL,slope_min_deg REAL,slope_max_deg REAL,
    PRIMARY KEY(dong_code,q,r,slot), FOREIGN KEY(q,r,slot) REFERENCES child(q,r,slot)
) WITHOUT ROWID;
CREATE INDEX terrain_dong_child_lookup ON terrain_dong_child(q,r,slot);
CREATE TABLE spot_dong_child (
    dong_code TEXT NOT NULL REFERENCES dong(code),q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    spot_count INTEGER NOT NULL CHECK(spot_count>0),
    spot_height_sum_m REAL NOT NULL,spot_height_min_m REAL NOT NULL,spot_height_max_m REAL NOT NULL,
    PRIMARY KEY(dong_code,q,r,slot), FOREIGN KEY(q,r,slot) REFERENCES child(q,r,slot)
) WITHOUT ROWID;
CREATE INDEX spot_dong_child_lookup ON spot_dong_child(q,r,slot);
"""
DEM_SUMS = """
    sum(sample_count) AS sample_count,sum(elevation_count) AS elevation_count,
    sum(sample_count-elevation_count) AS elevation_nodata_count,
    sum(elevation_sum_m) AS elevation_sum_m,
    1.0*sum(elevation_sum_m)/nullif(sum(elevation_count),0) AS elevation_mean_m,
    min(elevation_min_m) AS elevation_min_m,max(elevation_max_m) AS elevation_max_m,
    sum(slope_count) AS slope_count,sum(slope_sum_deg) AS slope_sum_deg,
    sum(slope_sum_deg)/nullif(sum(slope_count),0) AS slope_mean_deg,
    min(slope_min_deg) AS slope_min_deg,max(slope_max_deg) AS slope_max_deg
"""
SCHEMA += f"""
CREATE VIEW v_terrain_parent AS SELECT q,r,{DEM_SUMS} FROM terrain_dong_child GROUP BY q,r;
CREATE VIEW v_terrain_child AS SELECT q,r,slot,{DEM_SUMS} FROM terrain_dong_child GROUP BY q,r,slot;
CREATE VIEW v_terrain_dong AS SELECT dong_code,{DEM_SUMS} FROM terrain_dong_child GROUP BY dong_code;
CREATE VIEW v_spot_child AS SELECT q,r,slot,sum(spot_count) AS spot_count,
    sum(spot_height_sum_m)/sum(spot_count) AS spot_height_mean_m,
    min(spot_height_min_m) AS spot_height_min_m,max(spot_height_max_m) AS spot_height_max_m
    FROM spot_dong_child GROUP BY q,r,slot;
"""


@dataclass
class Summary:
    count: int = 0
    total: float = 0
    minimum: float = math.inf
    maximum: float = -math.inf

    def add(self, value):
        self.count += 1
        self.total += value
        self.minimum = min(self.minimum, value)
        self.maximum = max(self.maximum, value)

    def sql(self):
        return (self.count, self.total, self.minimum, self.maximum) if self.count else (0, None, None, None)


@dataclass
class TerrainBucket:
    samples: int = 0
    elevation: Summary = field(default_factory=Summary)
    slope: Summary = field(default_factory=Summary)


@dataclass
class NativeRaster:
    values: object
    west_arcsec: int
    north_arcsec: int


def checked_ref(ref):
    path = local_path(ROOT / ref["path"])
    if path.stat().st_size != ref["lengthBytes"] or digest(path) != ref["sha256"].lower():
        raise ValueError(f"terrain_input_ref_mismatch: {path.name}")
    return path


def read_raster(path, np):
    from PIL import Image

    with Image.open(path) as image:
        tags = image.tag_v2
        geo_keys = tags[34735]
        keys = {geo_keys[i]: geo_keys[i + 3] for i in range(4, len(geo_keys), 4) if geo_keys[i + 1] == 0}
        if keys.get(1025) != 2 or keys.get(2048) != 4326 or image.mode != "I" or tags[339] != (2,):
            raise ValueError(f"terrain_source_must_be_signed_wgs84_pixel_is_point: {path.name}")
        tie = tags[33922]
        if len(tie) != 6 or tie[:3] != (0.0, 0.0, 0.0) or 34264 in tags:
            raise ValueError(f"terrain_rotated_or_unsupported_tiepoint: {path.name}")
        if not all(abs(increment * 3600 - 1) < 1e-8 for increment in tags[33550][:2]):
            raise ValueError(f"terrain_source_must_be_one_arcsecond: {path.name}")
        if int(tags[42113]) != NASA_NODATA:
            raise ValueError(f"unexpected_terrain_nodata: {path.name}")
        west, north = (round(angle * 3600) for angle in tie[3:5])
        if max(abs(west - tie[3] * 3600), abs(north - tie[4] * 3600)) > 1e-6:
            raise ValueError(f"terrain_noninteger_arcsecond_phase: {path.name}")
        data = np.asarray(image, dtype=np.int16)
    return NativeRaster(data, west, north)


def compare_raw(corrected, sources, np):
    covered = np.zeros(corrected.values.shape, dtype=np.uint8)
    compared_cells = {}
    for path in sources:
        raw = read_raster(path, np)
        x_offset = raw.west_arcsec - corrected.west_arcsec
        y_offset = corrected.north_arcsec - raw.north_arcsec
        x0, y0 = max(x_offset, 0), max(y_offset, 0)
        x1 = min(corrected.values.shape[1], x_offset + raw.values.shape[1])
        y1 = min(corrected.values.shape[0], y_offset + raw.values.shape[0])
        if x1 <= x0 or y1 <= y0:
            raise ValueError(f"raw_tile_does_not_cover_corrected_grid: {path.name}")
        direct = raw.values[y0 - y_offset:y1 - y_offset, x0 - x_offset:x1 - x_offset]
        if not np.array_equal(corrected.values[y0:y1, x0:x1], direct):
            raise ValueError(f"corrected_grid_differs_from_raw_point_samples: {path.name}")
        covered[y0:y1, x0:x1] += 1
        compared_cells[path.name] = int(direct.size)
    if not np.all(covered > 0):
        raise ValueError("corrected_grid_has_cells_without_direct_raw_provenance")
    return {"comparedNativeRasterCells": int(covered.size), "rawComparisonCounts": compared_cells,
            "overlappingRawSeamCells": int(np.count_nonzero(covered > 1)),
            "rawComparisonMethod": "exact integer point-grid samples including both sides of the 127 E raw seam"}


def window_and_slopes(corrected, wgs_bounds, pyproj, np):
    west, south, east, north = wgs_bounds
    col0, col1 = (math.floor(west * 3600) - corrected.west_arcsec,
                  math.ceil(east * 3600) - corrected.west_arcsec + 1)
    row0, row1 = (corrected.north_arcsec - math.ceil(north * 3600),
                  corrected.north_arcsec - math.floor(south * 3600) + 1)
    if min(row0, col0) < 1 or row1 >= corrected.values.shape[0] or col1 >= corrected.values.shape[1]:
        raise ValueError("canonical_parent_window_not_fully_covered_with_gradient_halo")
    samples = corrected.values[row0:row1, col0:col1]
    longitude = (corrected.west_arcsec + np.arange(col0, col1)) / 3600
    latitude = (corrected.north_arcsec - np.arange(row0, row1)) / 3600
    lons, lats = np.meshgrid(longitude, latitude)
    geod = pyproj.Geod(ellps="WGS84")
    zeros = np.zeros(latitude.size)
    _, _, dx = geod.inv(zeros, latitude, zeros + 2 / 3600, latitude)
    _, _, dy = geod.inv(zeros, latitude - 1 / 3600, zeros, latitude + 1 / 3600)
    x_before, x_after = (corrected.values[row0:row1, col0 - 1:col1 - 1],
                         corrected.values[row0:row1, col0 + 1:col1 + 1])
    y_before, y_after = (corrected.values[row0 - 1:row1 - 1, col0:col1],
                         corrected.values[row0 + 1:row1 + 1, col0:col1])
    valid = np.logical_and.reduce(tuple(part != NASA_NODATA for part in (samples, x_before, x_after, y_before, y_after)))
    dxz = (x_after.astype(np.float64) - x_before) / dx[:, None]
    dyz = (y_after.astype(np.float64) - y_before) / dy[:, None]
    slopes = np.where(valid, np.degrees(np.arctan(np.hypot(dxz, dyz))), np.nan)
    to_tile = pyproj.Transformer.from_crs(4326, 5179, always_xy=True)
    x, y = to_tile.transform(lons.ravel(), lats.ravel(), errcheck=True)
    return x, y, samples.ravel(), slopes.ravel()


def memberships(x, y, shapes, shapely, np):
    codes, geometries = zip(*sorted(shapes.items()))
    points = shapely.points(x, y)
    source_idx, area_idx = shapely.STRtree(geometries).query(points, predicate="covered_by")
    order = np.lexsort((area_idx, source_idx))
    ordered_sources = source_idx[order]
    first = np.insert(ordered_sources[1:] != ordered_sources[:-1], 0, True) if len(order) else []
    accepted = order[first] if len(order) else order
    return zip(source_idx[accepted].tolist(), (codes[index] for index in area_idx[accepted])), len(order) - len(accepted)


def index_dem(db, corrected, parents, shapes, pyproj, shapely, np):
    centers = db.execute("SELECT min(center_x),min(center_y),max(center_x),max(center_y) FROM canonical_extent").fetchone()
    bbox = (centers[0] - PARENT_RADIUS, centers[1] - PARENT_RADIUS,
            centers[2] + PARENT_RADIUS, centers[3] + PARENT_RADIUS)
    to_wgs = pyproj.Transformer.from_crs(5179, 4326, always_xy=True)
    x, y, elevation, slope = window_and_slopes(corrected, to_wgs.transform_bounds(*bbox), pyproj, np)
    membership, additional_matches = memberships(x, y, shapes, shapely, np)
    buckets = defaultdict(TerrainBucket)
    excluded = 0
    negative = 0
    for index, code in membership:
        q, r, slot = point_key(x[index], y[index])
        if (q, r) not in parents:
            excluded += 1
            continue
        bucket = buckets[code, q, r, slot]
        bucket.samples += 1
        value = int(elevation[index])
        if value != NASA_NODATA:
            bucket.elevation.add(value)
            negative += value < 0
        if math.isfinite(slope[index]):
            bucket.slope.add(float(slope[index]))
    db.executemany("INSERT INTO terrain_dong_child VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
                   ((*key, bucket.samples, *bucket.elevation.sql(), *bucket.slope.sql())
                    for key, bucket in sorted(buckets.items())))
    return {"nativeCandidateSamplesInBoundingWindow": int(len(elevation)), "additionalDongBoundaryMatches": additional_matches,
            "boundarySamplesOutsideParentIndex": excluded, "negativeElevationSamplesRetained": negative}


def index_spots(db, archive_path, expected_rows, parents, shapes, pyproj, shapely, np):
    import shapefile

    with ZipFile(archive_path) as archive:
        stem, = (name[:-4] for name in archive.namelist() if name.endswith("N3P_F002.shp"))
        projection = archive.read(stem + ".prj").decode("ascii")
        source_crs = pyproj.CRS.from_wkt(projection)
        if source_crs.to_epsg() != 5174:
            raise ValueError("seoul_spot_height_source_crs_changed")
        with shapefile.Reader(
            shp=BytesIO(archive.read(stem + ".shp")),
            shx=BytesIO(archive.read(stem + ".shx")),
            dbf=BytesIO(archive.read(stem + ".dbf")),
            encoding="cp949",
        ) as points:
            if points.shapeType != shapefile.POINT or points.numRecords != expected_rows:
                raise ValueError("seoul_spot_height_record_count_or_geometry_changed")
            records = [(record.record["UFID"], record.shape.points[0], record.record["HEIGHT"])
                       for record in points.iterShapeRecords()]
    if len({record[0] for record in records}) != len(records):
        raise ValueError("duplicate_seoul_spot_height_source_identifier")
    pre_x, pre_y = zip(*(item[1] for item in records))
    to_tile = pyproj.Transformer.from_crs(source_crs, 5179, always_xy=True)
    x, y = to_tile.transform(pre_x, pre_y, errcheck=True)
    membership, additional_matches = memberships(x, y, shapes, shapely, np)
    buckets = defaultdict(Summary)
    outside_boundary = len(records)
    outside_parents = 0
    no_height = 0
    for index, code in membership:
        outside_boundary -= 1
        value = records[index][2]
        if value is None or not math.isfinite(value):
            no_height += 1
            continue
        q, r, slot = point_key(x[index], y[index])
        if (q, r) not in parents:
            outside_parents += 1
            continue
        buckets[code, q, r, slot].add(value)
    db.executemany("INSERT INTO spot_dong_child VALUES(?,?,?,?,?,?,?,?)",
                   ((*key, *bucket.sql()) for key, bucket in sorted(buckets.items())))
    return {"seoulSpotSourceRows": len(records), "seoulSpotsOutsideBoundary": outside_boundary,
            "seoulSpotsWithNoHeight": no_height, "seoulSpotsOutsideParentIndex": outside_parents,
            "seoulSpotAdditionalDongMatches": additional_matches}


def source_records(area_path, correction, spot_receipt):
    from collections import namedtuple

    Source = namedtuple("Source", "source_id dataset_id path url period horizontal_crs vertical_ref kind")
    corrections = {item["id"]: item for item in json.loads(correction.read_text(encoding="utf-8-sig"))["replacements"]}
    native = checked_ref(corrections["metro:native-crop"]["corrected"])
    raw_tiles = [checked_ref(ref) for ref in corrections["metro:capital-viewport"]["sourceRefs"]]
    if {path.name for path in raw_tiles} != {"NASADEM_HGT_n37e126.tif", "NASADEM_HGT_n37e127.tif"}:
        raise ValueError("unexpected_seoul_raw_terrain_tile_ids")
    spots = json.loads(spot_receipt.read_text(encoding="utf-8-sig"))
    archived_spots, = (item for item in spots["downloads"] if item["storedFileName"] == SPOT_FILENAME)
    spot_path = local_path(spot_receipt.parent / SPOT_FILENAME)
    if spot_path.stat().st_size != archived_spots["byteLength"] or digest(spot_path) != archived_spots["sha256"].lower():
        raise ValueError("seoul_spot_height_input_sha256_mismatch")
    with closing(read_only(area_path)) as areas:
        path_str, fingerprint, url, boundary_period = areas.execute(
            "SELECT local_input,sha256,url,period FROM source_release WHERE id='area-boundaries'").fetchone()
    boundaries = local_path(ROOT / path_str)
    if digest(boundaries) != fingerprint:
        raise ValueError("terrain_boundary_differs_from_canonical_area_db")
    inputs = [
        Source("canonical-area", "seoul-area-300m", area_path, None, boundary_period, "EPSG:5179", None, "key_scaffold"),
        Source("official-boundaries", "TL_SCCO_GEMD", boundaries, url, boundary_period, "EPSG:4326", None, "internal_scope"),
        Source("native-correction", "terrain-native-point-grid-correction.v2", correction, None,
               "2026-11-16", None, None, "prior_verification_receipt"),
        Source("nasadem-native", "NASADEM_HGT.001", native, NASA_URL, NASA_PERIOD,
               "EPSG:4326", NASA_VERTICAL_REF, "modeled_surface_on_native_points"),
        Source("seoul-spot", "OA-22241", spot_path, spots["officialPageUrl"], "2023 topographic map",
               "EPSG:5174", None, "official_spot_height_source"),
        Source("seoul-spot-receipt", "OA-22241", spot_receipt, spots["officialPageUrl"], archived_spots["pageFileModifiedOn"],
               None, None, "acquisition_receipt"),
    ]
    inputs += [Source(path.stem, "NASADEM_HGT.001", path, NASA_URL, NASA_PERIOD,
                      "EPSG:4326", NASA_VERTICAL_REF, "original_radar_model_tile") for path in raw_tiles]
    return inputs, native, raw_tiles, spot_path, archived_spots["archiveContents"]["elevationPoint"]["recordCount"], boundaries


def build(area_path, correction, spot_receipt, output):
    import numpy as np

    pyproj, shapely = gis()
    inputs, native, raw_tiles, spot_path, spot_rows, boundaries = source_records(area_path, correction, spot_receipt)
    corrected = read_raster(native, np)
    audit = compare_raw(corrected, raw_tiles, np)
    shapes = area_shapes(boundaries, pyproj, shapely)
    with closing(read_only(area_path)) as areas:
        parent_rows = areas.execute("SELECT q,r,center_x,center_y FROM tile ORDER BY q,r").fetchall()
        dongs = areas.execute("SELECT code,district_code,name FROM admin_dong ORDER BY code").fetchall()
    if set(shapes) != {dong[0] for dong in dongs}:
        raise ValueError("terrain_boundary_dong_mismatch")
    parents = {(row[0], row[1]) for row in parent_rows}
    source_signatures = {source.source_id: digest(source.path) for source in inputs}
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output) as stage:
        temp_db = Path(stage) / "terrain-tile-index.sqlite"
        with closing(sqlite3.connect(temp_db)) as db, db:
            db.executescript(SCHEMA)
            db.executemany("INSERT INTO terrain_source VALUES(?,?,?,?,?,?,?,?,?)", (
                (source.source_id, source.dataset_id, source.path.relative_to(ROOT).as_posix(),
                 source_signatures[source.source_id], source.url,
                 source.period, source.horizontal_crs, source.vertical_ref, source.kind) for source in inputs))
            db.executemany("INSERT INTO terrain_metadata VALUES(?,?)",
                           (("grid_revision", REVISION), ("use_boundary", "internal_review_only"),
                            ("statistics_scope", "exact official Seoul dong point membership"),
                            ("vertical_fusion", "not_performed"),
                            ("slope_operator", "native four-neighbor central differences using geodesic metres")))
            db.executemany("INSERT INTO dong VALUES(?,?,?)", dongs)
            db.executemany("INSERT INTO parent VALUES(?,?)", sorted(parents))
            db.executemany("INSERT INTO child VALUES(?,?,?)",
                           ((q, r, slot) for q, r in sorted(parents) for slot in range(len(SLOT_LABELS))))
            db.execute("CREATE TEMP TABLE canonical_extent (center_x REAL,center_y REAL)")
            db.executemany("INSERT INTO canonical_extent VALUES(?,?)", (row[2:] for row in parent_rows))
            audit.update(index_dem(db, corrected, parents, shapes, pyproj, shapely, np))
            audit.update(index_spots(db, spot_path, spot_rows, parents, shapes, pyproj, shapely, np))
            audit.update(audit_relations(db))
            if (audit["seoulSpotSourceRows"] != audit["seoulIndexedSpotHeights"] + audit["seoulSpotsOutsideBoundary"]
                    + audit["seoulSpotsOutsideParentIndex"] + audit["seoulSpotsWithNoHeight"]):
                raise ValueError("spot_height_counts_not_conserved")
        final_db = output / temp_db.name
        temp_db.replace(final_db)
    audit.update({
        "gridRevision": REVISION, "databaseSha256": digest(final_db), "sourceSha256": source_signatures,
        "scope": "Seoul 25 districts, canonical 427 administrative dongs",
        "useBoundary": "internal_review_only", "distributionApproved": False,
        "elevation": {"unit": "m", "verticalReference": NASA_VERTICAL_REF, "observed": NASA_PERIOD,
                      "kind": "reprocessed satellite elevation model including native void fills"},
        "slope": {"unit": "degrees", "kind": "central-difference model slope, not measured road gradient",
                  "neighbors": "native north, south, east and west plus a valid centre",
                  "missing": "NULL for missing or unusable samples"},
        "spotHeight": {"unit": "m", "kind": "official representative topographic points",
                       "verticalReference": None, "combinedWithNASA": False},
        "spatialPolicy": "Original samples inside official Seoul dong shapes only; if multiple match, lexicographically first dong.",
        "coveragePolicy": "Unrepresented slots are sparse evidence, never zero elevation; no interpolation carried out.",
        "rsInfo": {"numpy": np.__version__, "pyproj": pyproj.__version__, "shapely": shapely.__version__},
        "integrityCheck": "passed", "foreignKeyCheck": "passed",
    })
    atomic_json(output / "terrain-summary.json", audit)
    return audit


def audit_relations(db):
    if db.execute("PRAGMA foreign_key_check").fetchone() or db.execute("PRAGMA integrity_check").fetchone() != ("ok",):
        raise ValueError("terrain_database_integrity_failed")
    source_totals = db.execute("""
        SELECT sum(sample_count),sum(elevation_count),sum(slope_count) FROM terrain_dong_child
    """).fetchone()
    for table in ("v_terrain_child", "v_terrain_parent", "v_terrain_dong"):
        if db.execute(f"SELECT sum(sample_count),sum(elevation_count),sum(slope_count) FROM {table}").fetchone() != source_totals:
            raise ValueError("terrain_rollup_not_conserved")
    counts = dict(db.execute("""
        SELECT 'parentKeys',count(*) FROM parent
        UNION ALL SELECT 'childSlots',count(*) FROM child
        UNION ALL SELECT 'parentsWithNativeSamples',count(*) FROM v_terrain_parent
        UNION ALL SELECT 'childrenWithNativeSamples',count(*) FROM v_terrain_child
        UNION ALL SELECT 'dongsWithNativeSamples',count(*) FROM v_terrain_dong
        UNION ALL SELECT 'dongsWithSpotHeights',count(DISTINCT dong_code) FROM spot_dong_child
        UNION ALL SELECT 'childrenWithSpotHeights',count(*) FROM v_spot_child
        UNION ALL SELECT 'seoulIndexedSpotHeights',sum(spot_count) FROM spot_dong_child
    """))
    counts.update(dict(zip(("seoulNativeSamples", "seoulNativeElevations", "seoulNativeSlopes"), source_totals)))
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--areas", type=Path, default=AREAS)
    parser.add_argument("--correction", type=Path, default=CORRECTION)
    parser.add_argument("--spot-receipt", type=Path, default=SPOT_RECEIPT)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(build(local_path(args.areas), local_path(args.correction), local_path(args.spot_receipt),
                           local_path(args.output)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
