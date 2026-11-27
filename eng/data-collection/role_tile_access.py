"""Index the retained Seoul pedestrian-entrance survey on the district and child grid."""

import argparse
from collections import Counter
from contextlib import closing
from hashlib import sha256
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_facilities import official_seoul
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest


OUTPUT = PUBLIC / "role-tile-intake-20261126/pedestrian-entrance"
SOURCE = PUBLIC / "diorama-public-layer-intake-20261123/r2-access-street/seoul-access-street.sqlite"
CORE = TILE_OUTPUT / DBNAME
DATASET = "OA-21699"
DB = "seoul-pedestrian-entrance-tile.sqlite"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (
    id TEXT PRIMARY KEY,label TEXT NOT NULL,url TEXT NOT NULL,basis TEXT NOT NULL,source_sha256 TEXT NOT NULL
);
CREATE TABLE child_admin (
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT,
    PRIMARY KEY(q,r,slot,admin_code)
) WITHOUT ROWID;
CREATE TABLE surveyed_entrance (
    source_record_id INTEGER PRIMARY KEY,source_id TEXT NOT NULL REFERENCES source(id),
    source_key TEXT NOT NULL,surveyed_facility_key TEXT,reported_entrance_type TEXT,
    source_position_group TEXT NOT NULL,reported_ramp INTEGER,reported_tactile_paving INTEGER,
    reported_door_width_min_m REAL,reported_door_width_upper_exclusive_m REAL,
    reported_threshold_code TEXT,placement_state TEXT NOT NULL,prior_polygon_check TEXT NOT NULL,
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT,
    FOREIGN KEY(q,r,slot,admin_code) REFERENCES child_admin
);
CREATE INDEX surveyed_entrance_by_child ON surveyed_entrance(q,r,slot,source_position_group);
CREATE VIEW v_pedestrian_entrance_child AS
SELECT e.q,e.r,e.slot,e.admin_code,e.source_id,s.basis AS survey_date,
       COUNT(*) AS source_survey_rows,COUNT(DISTINCT e.source_position_group) AS source_coordinate_groups,
       COUNT(DISTINCT e.surveyed_facility_key) AS source_facility_groups,
       SUM(e.reported_ramp=1) AS reported_ramp_rows,COUNT(e.reported_ramp) AS rows_with_ramp_description,
       SUM(e.reported_tactile_paving=1) AS reported_tactile_rows,
       COUNT(e.reported_tactile_paving) AS rows_with_tactile_description
FROM surveyed_entrance e JOIN source s ON e.source_id=s.id WHERE e.q IS NOT NULL
GROUP BY 1,2,3,4,5,6;
"""


def build(output):
    output.mkdir(parents=True, exist_ok=True)
    pyproj, shapely = gis()
    to_map = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    labels, tree = official_seoul(shapely)
    with closing(read_only(CORE)) as core:
        allowed_keys = set(core.execute("SELECT q,r,slot,admin_code FROM child_admin"))
    checked = Counter()
    with TemporaryDirectory(dir=output) as temporary, closing(read_only(SOURCE)) as original:
        path = Path(temporary) / DB
        row = original.execute("""SELECT source_id,publisher_title,source_url,basis,sha256,source_crs
               FROM source WHERE source_id=?""", (DATASET,)).fetchone()
        if not row or row[-1] != "EPSG:4326":
            raise ValueError("seoul_entrance_projection_unverified")
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.execute("INSERT INTO source VALUES (?,?,?,?,?)", row[:-1])
                for (record, key, lon, lat, prior, facility, entrance, ramp, tactile, width_min, width_max,
                     threshold) in original.execute("""SELECT p.id,p.source_key,p.lon,p.lat,p.placement_state,
                     e.facility_key,e.entrance_type_code,e.ramp,e.tactile_paving,e.door_width_min_m,
                     e.door_width_upper_exclusive_m,e.threshold_code
                     FROM reference_point p JOIN entrance e ON p.id=e.point_id
                     WHERE p.source_id=? ORDER BY p.id""", (DATASET,)):
                    hits = tree.query(shapely.Point(lon, lat), predicate="within")
                    mkey = (None,) * 4
                    status = "no_single_official_dong_polygon"
                    if len(hits) == 1:
                        candidate = (*point_key(*to_map(lon, lat)), labels[int(hits[0])][0])
                        if candidate in allowed_keys:
                            mkey = candidate
                            status = "source_point_current_dong_agreement"
                            db.execute("INSERT OR IGNORE INTO child_admin VALUES (?,?,?,?)", mkey)
                        else:
                            status = "child_admin_intersection_missing"
                    coord = sha256(f"{lon!r},{lat!r}".encode("ascii")).hexdigest()
                    checked[status] += 1
                    db.execute("INSERT INTO surveyed_entrance VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (
                        record, DATASET, key, facility, entrance, coord, ramp, tactile,
                        width_min, width_max, threshold, status, prior, *mkey,
                    ))
            check_database(db)
            support = db.execute("""SELECT COUNT(*),COUNT(DISTINCT source_position_group),COUNT(DISTINCT admin_code),
                                    COUNT(DISTINCT printf('%d/%d/%d',q,r,slot))
                                    FROM surveyed_entrance WHERE slot IS NOT NULL""").fetchone()
            sample = db.execute("""SELECT admin_code,source_survey_rows,source_coordinate_groups,reported_ramp_rows
                FROM v_pedestrian_entrance_child WHERE q=-32 AND r=-179 AND slot=2 ORDER BY admin_code""").fetchall()
        path.replace(output / DB)
    result = {"sourceDatabase": SOURCE.relative_to(ROOT).as_posix(), "sourceSha256": digest(SOURCE),
              "surveyDate": row[3], "officialSource": row[2], "geometryRevision": REVISION,
              "placement": dict(checked), "mappedSurveyRows": support[0], "mappedCoordinateGroups": support[1],
              "administrativeDongs": support[2], "childTiles": support[3], "sagajeongChild": sample,
              "databaseSha256": digest(output / DB), "integrityCheck": "ok", "foreignKeyViolations": 0,
              "guard": "survey reference only; the current door and usable paths are not observed"}
    atomic_json(output / "verification.json", result)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    print(json.dumps(build(parser.parse_args().output), ensure_ascii=False))
