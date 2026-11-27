"""Build the internal Seoul dong-to-child-tile evidence index from frozen source snapshots."""

import argparse
from collections import Counter
from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, SLOT_LABELS, SEEDS, child_point_slot, vertices
from seoul_area_geography import BOUNDARIES, OUTPUT as AREA_OUTPUT, area_shapes, check_database, gis, read_only, tile_center
from seoul_tile_population import GRID_DB, PUBLIC, ROOT, axial, digest, local_path

OUTPUT = PUBLIC / "dong-tile-intake-20261126"
PARENT = AREA_OUTPUT / "seoul-area.sqlite"
GEOGRAPHY = AREA_OUTPUT / "area-geography.sqlite"
BUS = AREA_OUTPUT / "bus/seoul-area-bus.sqlite"
RAIL = AREA_OUTPUT / "rail/rail-area-index.sqlite"
FOOD_RAW = PUBLIC / "national-restaurant-regional-20261118-r1/index/restaurants.sqlite"
SCHEMA = Path(__file__).with_suffix(".sql")
DBNAME = "dong-tile-evidence.sqlite"
MIN_AREA = 0.0001


def ro_attach(db, alias, path):
    db.execute(f"ATTACH DATABASE ? AS {alias}", (path.resolve().as_uri() + "?mode=ro",))


def load_metadata(db):
    entries = [
        ("dong-tile-parent", "Hongdal Seoul parent tile index", "2026-11-25", None, PARENT),
        ("dong-tile-grid", "OA-22784 reference grid", "2026-11-25", "https://data.seoul.go.kr/dataList/OA-22784/S/1/datasetView.do", GRID_DB),
        ("dong-tile-food-positions", "LOCALDATA license source coordinates", "2026-11-18", "https://www.localdata.go.kr/", FOOD_RAW),
        ("dong-tile-rail-positions", "OA-12252 station crosswalk", "2026-10", "https://data.seoul.go.kr/dataList/OA-12252/S/1/datasetView.do", RAIL),
        ("dong-tile-bus-positions", "OA-12913 and T-DATA stop crosswalk", "2026-10/11", "https://data.seoul.go.kr/dataList/OA-12913/S/1/datasetView.do", BUS),
    ]
    db.executemany("INSERT INTO source_release VALUES (?,?,?,?,?,?)",
                   ((key, name, period, url, path.relative_to(ROOT).as_posix(), digest(path))
                    for key, name, period, url, path in entries))
    db.executemany("INSERT INTO metadata VALUES (?,?)", [
        ("geometry_revision", REVISION), ("distribution", "local-review"),
        ("crs", "EPSG:5179"), ("spacing_m", "300"), ("origin_m", "1000000,2000000"),
    ])


def load_children(db, shapely, shape_by_dong):
    codes = tuple(sorted(shape_by_dong))
    areas = [shape_by_dong[code] for code in codes]
    tree = shapely.STRtree(areas)
    keys, polygons = [], []
    for q, r in db.execute("SELECT q,r FROM tile ORDER BY q,r"):
        centre = tile_center(q, r)
        for slot, label in enumerate(SLOT_LABELS):
            ring = vertices(q, r, slot)
            child = shapely.Polygon(ring)
            intersections = []
            for i in sorted(tree.query(child, predicate="intersects")):
                piece = child.intersection(areas[i])
                if piece.area > MIN_AREA:
                    intersections.append((codes[i], piece))
            seoul = shapely.union_all([p for _, p in intersections]).area if intersections else 0
            x, y = centre[0] + SEEDS[slot][0], centre[1] + SEEDS[slot][1]
            db.execute("INSERT INTO child_tile VALUES (?,?,?,?,?,?,?,?,?)", (
                q, r, slot, label, x, y, child.area, seoul, json.dumps(ring, separators=(",", ":"))
            ))
            db.executemany("INSERT INTO child_admin VALUES (?,?,?,?,?)",
                           ((q, r, slot, code, piece.area) for code, piece in intersections))
            keys.append((q, r, slot))
            polygons.append(child)
    return keys, polygons


def load_grid(db, shapely, keys, polygons):
    tree = shapely.STRtree(polygons)
    parent_keys = {(q, r) for q, r, _ in keys}
    rows = db.execute("SELECT cell_id,xmin,ymin,xmax,ymax FROM grid.grid ORDER BY cell_id")
    for code, xmin, ymin, xmax, ymax in rows:
        cell = shapely.box(xmin, ymin, xmax, ymax)
        samples = Counter()
        for dx in range(25, 250, 50):
            for dy in range(25, 250, 50):
                key = axial(xmin + dx, ymin + dy, 300)
                if key in parent_keys:
                    slot = child_point_slot(xmin + dx, ymin + dy, *key)
                    samples[(*key, slot)] += 1
        areas = {}
        for i in sorted(tree.query(cell, predicate="intersects")):
            area = cell.intersection(polygons[i]).area
            if area > MIN_AREA:
                areas[keys[i]] = area
        db.execute("INSERT INTO source_grid VALUES (?,?,?,?,?)", (code, xmin, ymin, xmax, ymax))
        db.executemany("INSERT INTO grid_child_weight VALUES (?,?,?,?,?,?)",
                       ((code, *key, areas.get(key, 0), samples[key]) for key in sorted(areas.keys() | samples.keys())))


def valid_position(east, north, q, r):
    if axial(east, north, 300) != (q, r):
        raise ValueError("point_does_not_reproduce_existing_parent")
    return child_point_slot(east, north, q, r)


def load_positions(db, pyproj):
    food_transform = pyproj.Transformer.from_crs(5174, 5179, always_xy=True).transform
    lon_lat_transform = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    db.execute("""INSERT INTO food_category SELECT category_id,name FROM parent.food_category""")
    db.execute("""INSERT INTO food_source SELECT id,dataset_id,period,url,sha256
        FROM parent.source_release WHERE id IN (SELECT DISTINCT source_id FROM parent.food_license)""")
    foods = db.execute("""SELECT f.source_id,f.source_row,f.q,f.r,f.category_id,l.source_x,l.source_y
        FROM parent.food_license f JOIN food_raw.license_observation l
        ON l.dataset_id=CAST(substr(f.source_id,6) AS INTEGER) AND l.source_record=f.source_row
        WHERE f.q IS NOT NULL ORDER BY f.source_id,f.source_row""")
    for source, record, q, r, category, x, y in foods:
        key = valid_position(*food_transform(float(x), float(y)), q, r)
        db.execute("INSERT INTO food_child_position VALUES (?,?,?,?,?,?)", (source, record, q, r, key, category))
    db.execute("""INSERT INTO transport_mode SELECT DISTINCT mode FROM parent.transport_place""")
    for mode, table, key_id in (("rail", "rail.rail_station", "station_id"), ("bus", "bus.stop", "standard_stop_id")):
        points = db.execute(f"""SELECT p.place_id,p.name,p.line_or_ars,p.q,p.r,s.longitude,s.latitude
            FROM parent.transport_place p JOIN {table} s ON s.{key_id}=p.place_id
            WHERE p.mode=? AND p.q IS NOT NULL ORDER BY p.place_id""", (mode,))
        for identity, name, line, q, r, lon, lat in points:
            slot = valid_position(*lon_lat_transform(lon, lat), q, r)
            db.execute("INSERT INTO transport_child_position VALUES (?,?,?,?,?,?,?)",
                       (mode, identity, name, line, q, r, slot))


def verify(db):
    check_database(db)
    if db.execute("SELECT count(*) FROM (SELECT q,r FROM child_tile GROUP BY q,r HAVING count(*)<>7)").fetchone()[0]:
        raise ValueError("incorrect_child_count")
    for sql, error in (
        ("""SELECT count(*) FROM (
            SELECT q,r FROM child_tile GROUP BY q,r HAVING max(full_area_m2)-min(full_area_m2)>0.0001)""",
         "unequal_full_child_areas"),
        ("""SELECT count(*) FROM tile t JOIN (SELECT q,r,sum(seoul_area_m2) a FROM child_tile GROUP BY q,r) c
        USING(q,r) WHERE abs(t.seoul_area_m2-c.a)>0.05""", "seoul_overlap_not_conserved"),
        ("""SELECT count(*) FROM tile_admin t LEFT JOIN (
            SELECT q,r,admin_code,sum(overlap_m2) a FROM child_admin GROUP BY q,r,admin_code
        ) c USING(q,r,admin_code) WHERE abs(t.overlap_m2-coalesce(c.a,0))>0.05""", "dong_overlap_not_conserved"),
    ):
        if db.execute(sql).fetchone()[0]:
            raise ValueError(error)
    for sql, reference, error in (
        ("""SELECT grid_code,q,r,sum(sample_points) FROM grid_child_weight WHERE sample_points>0
            GROUP BY grid_code,q,r ORDER BY 1,2,3""",
         "SELECT grid_code,q,r,samples FROM parent.grid_tile_weight ORDER BY 1,2,3", "grid_sample_weights_not_conserved"),
        ("""SELECT q,r,category_id,count(*) FROM food_child_position GROUP BY 1,2,3 ORDER BY 1,2,3""",
         "SELECT q,r,category_id,licenses FROM parent.v_food_tile ORDER BY 1,2,3", "food_counts_not_conserved"),
        ("""SELECT mode,q,r,count(*) FROM transport_child_position GROUP BY 1,2,3 ORDER BY 1,2,3""",
         """SELECT mode,q,r,count(*) FROM parent.transport_place WHERE q IS NOT NULL
            GROUP BY 1,2,3 ORDER BY 1,2,3""", "transit_places_not_conserved"),
    ):
        if db.execute(sql).fetchall() != db.execute(reference).fetchall():
            raise ValueError(error)


def inventory(db, path):
    names = ("district", "admin_dong", "legal_dong", "tile", "child_tile", "child_admin", "source_grid", "grid_child_weight",
             "food_child_position", "transport_child_position")
    return {
        "schema": "hongdal.dong-tile-intake.v1", "geometryRevision": REVISION, "distribution": "local-review",
        "counts": {name: db.execute(f"SELECT count(*) FROM {name}").fetchone()[0] for name in names},
        "transportCounts": dict(db.execute("SELECT mode,count(*) FROM transport_child_position GROUP BY mode")),
        "slotsWithoutSeoulOverlap": db.execute("SELECT count(*) FROM child_tile WHERE seoul_area_m2=0").fetchone()[0],
        "seoulParentHash": digest(PARENT), "databaseSha256": digest(path),
        "verifiedConservation": ["child areas", "city overlap", "administrative overlap", "legacy 50m grid samples",
                                 "food counts by parent and category", "transport counts by parent and mode"],
    }


def build(output=OUTPUT):
    output.mkdir(parents=True, exist_ok=True)
    pyproj, shapely = gis()
    shapes = area_shapes(BOUNDARIES, pyproj, shapely)
    with TemporaryDirectory(dir=output) as temporary:
        path = Path(temporary) / DBNAME
        with closing(read_only(GEOGRAPHY)) as geo, closing(sqlite3.connect(path, uri=True)) as db:
            geo.backup(db)
            db.execute("PRAGMA foreign_keys=ON")
            db.executescript(SCHEMA.read_text(encoding="utf-8"))
            for alias, source in (("grid", GRID_DB), ("parent", PARENT), ("food_raw", FOOD_RAW), ("rail", RAIL), ("bus", BUS)):
                ro_attach(db, alias, source)
            with db:
                load_metadata(db)
                keys, polygons = load_children(db, shapely, shapes)
                load_grid(db, shapely, keys, polygons)
                load_positions(db, pyproj)
            verify(db)
            summary = inventory(db, path)
        path.replace(output / DBNAME)
    atomic_json(output / "core-summary.json", summary)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(build(local_path(args.output)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
