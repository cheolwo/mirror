"""Combine validated Seoul layers into one local relational index."""

import argparse
from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from seoul_area_geography import BOUNDARIES, OUTPUT, REVISION, area_shapes, check_database, gis, read_only
from seoul_tile_population import OBSERVATION_SCHEMA, PUBLIC, ROOT, digest, local_path

SCHEMA = Path(__file__).with_suffix(".sql")
DATABASE = "seoul-area.sqlite"
RESIDENT = PUBLIC / "diorama-data-relations-20261123-r2/population-monthly/population-monthly.sqlite"
INPUTS = {
    "pop": "population/population-tile-index.sqlite",
    "rail": "rail/rail-area-index.sqlite",
    "bus": "bus/seoul-area-bus.sqlite",
    "food": "food/food-area.sqlite",
}
SOURCE_URLS = {
    "pop": ("OA-22784", "2026-11-01~21", "https://data.seoul.go.kr/dataList/OA-22784/S/1/datasetView.do"),
    "rail": ("OA-12252", "2026-10", "https://data.seoul.go.kr/dataList/OA-12252/S/1/datasetView.do"),
    "bus": ("OA-12913 / T-DATA 40", "2026-10 and 2026-11-19~20", "https://data.seoul.go.kr/dataList/OA-12913/S/1/datasetView.do"),
    "food": ("LOCALDATA food licenses", "2026-11-18", "https://www.localdata.go.kr/"),
}


def select_all(db, statement, parameters=()):
    return [dict(row) for row in db.execute(statement, parameters)]


def load_sources(db, inputs):
    db.executemany("INSERT INTO source_release VALUES (?,?,?,?,?,?)", (
        (name + ":index", *SOURCE_URLS[name], path.relative_to(ROOT).as_posix(), digest(path))
        for name, path in inputs.items()
    ))
    db.execute("""INSERT INTO source_release SELECT 'rail:'||source_id,source_id,'2026-10 collection',
                  source_url,local_input,sha256 FROM rail.evidence_source""")
    db.execute("""INSERT INTO source_release SELECT 'bus:'||source_id,source_id,scope,
                  source_url,local_path,sha256 FROM bus.source""")
    db.execute("""INSERT INTO source_release
                  SELECT 'food:'||dataset_id,CAST(dataset_id AS TEXT),acquired_at_utc,official_url,
                         ?,original_sha256 FROM food.food_dataset""",
               (inputs["food"].with_name("food-summary.json").relative_to(ROOT).as_posix(),))
    db.execute("""INSERT INTO source_release
                  SELECT 'resident:'||dataset_id,dataset_id,snapshot_date,official_url,?,source_sha256
                  FROM residents.source_release""", (RESIDENT.relative_to(ROOT).as_posix(),))


def load_population(db):
    if db.execute("SELECT q,r FROM tile EXCEPT SELECT q,r FROM pop.tile").fetchone() or db.execute(
        "SELECT q,r FROM pop.tile EXCEPT SELECT q,r FROM tile"
    ).fetchone():
        raise ValueError("population_tile_coordinates_do_not_match")
    for table in ("source_day", "grid_tile_weight", "tile_presence_day", "unmapped_source_grid"):
        db.execute(f"INSERT INTO {table} SELECT * FROM pop.{table}")
    db.execute("""INSERT INTO resident_month
        SELECT m.code,m.as_of,m.registered_people,m.one_person_households,
               'resident:3033254',
               CASE WHEN m.one_person_households IS NOT NULL THEN 'resident:15097973' END
        FROM residents.monthly_dong_reference m JOIN admin_dong a ON a.code=m.code
        WHERE m.source_province='서울특별시'""")


def locate_transit(db, keys, source, mode):
    pyproj, shapely = gis()
    boundaries = area_shapes(BOUNDARIES, pyproj, shapely)
    codes = tuple(sorted(boundaries))
    tree = shapely.STRtree([boundaries[key] for key in codes])
    project = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    records = []
    for place, name, line, lon, lat, source_q, source_r, upstream, source_id in db.execute(source):
        key = (source_q, source_r)
        if key in keys:
            q, r = key
            tile_status = "bound"
        else:
            q, r = None, None
            tile_status = "outside_geometry_tiles" if source_q is not None else "upstream_unbound"
        admin = None
        area_status = "location_not_verified"
        if source_q is not None and lon is not None and lat is not None:
            point = shapely.Point(*project(lon, lat))
            matches = tree.query(point, predicate="intersects")
            if len(matches) == 1:
                admin, area_status = codes[matches[0]], "2026-10_boundary_single_match"
            else:
                area_status = "outside_2026-10_boundary" if len(matches) == 0 else "boundary_match_ambiguous"
        records.append((mode, place, name, line, q, r, admin, tile_status, area_status, upstream,
                        f"{mode}:{source_id}" if source_id else None))
    db.executemany("INSERT INTO transport_place VALUES (?,?,?,?,?,?,?,?,?,?,?)", records)


def load_transport(db):
    keys = {tuple(row) for row in db.execute("SELECT q,r FROM tile")}
    locate_transit(db, keys, """SELECT station_id,name,line,longitude,latitude,q,r,binding_state,position_source_id
                               FROM rail.rail_station""", "rail")
    locate_transit(db, keys, """SELECT standard_stop_id,name,ars_id,longitude,latitude,q,r,binding_state,position_source_id
                               FROM bus.stop""", "bus")
    db.execute("""INSERT INTO transport_tag
        SELECT 'rail',station_id,'month',month,hour,boardings,alightings,1,'rail:'||source_id FROM rail.rail_hour""")
    for table, period_type, period in (("monthly_stop_hour", "month", "month"), ("daily_stop_hour", "day", "service_date")):
        db.execute(f"""INSERT INTO transport_tag
            SELECT 'bus',standard_stop_id,?,{period},hour,boardings,alightings,observed_route_stop_rows,'bus:'||source_id
            FROM bus.{table}""", (period_type,))


def load_food(db):
    db.execute("INSERT INTO food_category SELECT category_id,label FROM food.food_category")
    db.execute("""INSERT INTO food_license
        SELECT 'food:'||f.dataset_id,f.source_record,f.district_code,f.legal_dong_code,f.admin_dong_code,
               f.admin_binding,f.category_id,t.q,t.r,
               CASE WHEN t.q IS NOT NULL THEN 'bound'
                    WHEN f.q IS NOT NULL THEN 'outside_geometry_tiles' ELSE f.binding_state END
        FROM food.food_license f LEFT JOIN tile t ON f.q=t.q AND f.r=t.r""")


def tile_card(db, q, r):
    tile = db.execute("SELECT * FROM tile WHERE q=? AND r=?", (q, r)).fetchone()
    if tile is None:
        return None
    args = q, r
    return {
        "tile": dict(tile),
        "administrativeAreas": select_all(db, """SELECT a.code,a.name,a.district_code, d.name AS district_name,t.overlap_m2
            FROM tile_admin t JOIN admin_dong a ON a.code=t.admin_code JOIN district d ON d.code=a.district_code
            WHERE t.q=? AND t.r=? ORDER BY t.overlap_m2 DESC""", args),
        "livingPopulation": select_all(db, "SELECT * FROM v_tile_presence_profile WHERE q=? AND r=? ORDER BY day_kind,band", args),
        "transitAtNoon": select_all(db, """SELECT mode,period_type,period,hour,boardings,alightings,places
            FROM v_transport_tile_hour WHERE q=? AND r=? AND hour=12 ORDER BY mode,period_type,period""", args),
        "restaurantLicenses": select_all(db, """SELECT c.name,f.licenses FROM v_food_tile f JOIN food_category c USING(category_id)
            WHERE q=? AND r=? ORDER BY category_id""", args),
        "actualPlatformOrders": None,
    }


def station_examples(db, label):
    without_suffix = label.removesuffix("역")
    with_suffix = without_suffix + "역"
    stations = select_all(db, """SELECT place_id,name,line_or_ars AS line,q,r,admin_code,tile_status,area_status
        FROM transport_place WHERE mode='rail'
        AND (name IN (?,?) OR substr(name,1,instr(name,'(')-1) IN (?,?))
        ORDER BY q IS NULL,line_or_ars,place_id""", (without_suffix, with_suffix, without_suffix, with_suffix))
    for station in stations:
        station["evidence"] = tile_card(db, station["q"], station["r"])
    return stations


def district_card(db, label):
    region = db.execute("SELECT * FROM district WHERE code=? OR name=?", (label, label)).fetchone()
    if region is None:
        return None
    key = (region["code"],)
    return {
        "district": dict(region),
        "tileCount": db.execute("SELECT count(*) FROM v_district_tile WHERE district_code=?", key).fetchone()[0],
        "residentPopulation": select_all(db, "SELECT * FROM v_resident_district WHERE district_code=? ORDER BY period_end", key),
        "restaurantLicenses": select_all(db, """SELECT c.name,f.licenses,f.tile_located FROM v_food_district f
            JOIN food_category c USING(category_id) WHERE district_code=? ORDER BY category_id""", key),
    }


def dong_card(db, code):
    region = db.execute("SELECT * FROM admin_dong WHERE code=?", (code,)).fetchone()
    if region is None:
        return None
    key = (code,)
    return {
        "administrativeDong": dict(region),
        "residentPopulation": select_all(db, "SELECT * FROM resident_month WHERE admin_code=? ORDER BY period_end", key),
        "legalDongRelations": select_all(db, """SELECT l.code,l.name FROM legal_dong l
            JOIN admin_legal a ON a.legal_code=l.code WHERE a.admin_code=? ORDER BY l.code""", key),
        "tileIntersections": select_all(db, """SELECT q,r,overlap_m2 FROM tile_admin
            WHERE admin_code=? ORDER BY overlap_m2 DESC,q,r""", key),
    }


def checks(db):
    check_database(db)
    for query, expected in (
        ("SELECT count(*) FROM district", 25),
        ("SELECT count(*) FROM admin_dong", 427),
        ("SELECT count(DISTINCT admin_code) FROM tile_admin", 427),
        ("SELECT count(DISTINCT admin_code) FROM resident_month", 427),
    ):
        if db.execute(query).fetchone()[0] != expected:
            raise ValueError("area_coverage_incomplete")
    day_count = db.execute("SELECT count(*) FROM source_day").fetchone()[0]
    tile_count = db.execute("SELECT count(*) FROM tile").fetchone()[0]
    actual_rows = db.execute("SELECT count(*) FROM tile_presence_day").fetchone()[0]
    if actual_rows != day_count * tile_count * 2:
        raise ValueError("population_tile_record_count_mismatch")
    for mode in ("rail", "bus"):
        origin = db.execute("""SELECT period_type,period,hour,sum(boardings),sum(alightings)
            FROM transport_tag o JOIN transport_place f USING(mode,place_id)
            WHERE mode=? AND q IS NOT NULL GROUP BY 1,2,3 ORDER BY 1,2,3""", (mode,)).fetchall()
        projected = db.execute("""SELECT period_type,period,hour,sum(boardings),sum(alightings)
            FROM v_transport_tile_hour WHERE mode=? GROUP BY 1,2,3 ORDER BY 1,2,3""", (mode,)).fetchall()
        if origin != projected:
            raise ValueError("transport_tile_sums_not_conserved")
    if db.execute("SELECT count(*) FROM food_license").fetchone()[0] != db.execute(
        "SELECT count(*) FROM food.food_license"
    ).fetchone()[0]:
        raise ValueError("food_source_rows_not_conserved")


def inventory(db):
    names = ("district","admin_dong","legal_dong","admin_legal","tile","tile_admin","resident_month",
             "source_day","tile_presence_day","grid_tile_weight","unmapped_source_grid","food_license")
    return {
        "schema": "hongdal.seoul-area-evidence.v1", "tileRevision": REVISION, "crs": "EPSG:5179",
        "spacingM": 300, "distribution": "local-review",
        "tables": {name: db.execute(f"SELECT count(*) FROM {name}").fetchone()[0] for name in names},
        "presencePeriod": list(db.execute("SELECT min(day),max(day) FROM source_day").fetchone()),
        "transportPlaceCoverage": select_all(db, """SELECT mode,tile_status,area_status,count(*) count
            FROM transport_place GROUP BY 1,2,3 ORDER BY 1,2,3"""),
        "transportObservationCoverage": select_all(db, """SELECT t.mode,t.period_type,t.period,
            count(DISTINCT t.place_id) observation_places,count(DISTINCT CASE WHEN p.q IS NOT NULL THEN t.place_id END) mapped_places,
            count(*) AS hourly_observations FROM transport_tag t JOIN transport_place p USING(mode,place_id)
            GROUP BY 1,2,3 ORDER BY 1,2,3"""),
        "foodLocationCoverage": select_all(db, "SELECT tile_status,count(*) count FROM food_license GROUP BY 1 ORDER BY 1"),
        "sourceCount": db.execute("SELECT count(*) FROM source_release").fetchone()[0],
        "integrity": "ok",
    }


def build(output=OUTPUT):
    inputs = {name: output / rel for name, rel in INPUTS.items()}
    with TemporaryDirectory(dir=output) as temp:
        target = Path(temp) / DATABASE
        with closing(read_only(output / "area-geography.sqlite")) as base, closing(sqlite3.connect(target, uri=True)) as db:
            base.backup(db)
            db.row_factory = sqlite3.Row
            db.execute("PRAGMA foreign_keys=ON")
            for alias, path in {**inputs, "residents": RESIDENT}.items():
                db.execute(f"ATTACH DATABASE ? AS {alias}", (path.resolve().as_uri() + "?mode=ro",))
            db.executescript(OBSERVATION_SCHEMA + SCHEMA.read_text(encoding="utf-8"))
            with db:
                load_sources(db, inputs)
                load_population(db)
                load_transport(db)
                load_food(db)
            checks(db)
            summary = inventory(db)
            examples = {"districts": [district_card(db, label) for label in ("은평구", "강서구", "강남구")],
                        "administrativeDongs": [dong_card(db, code) for code in ("1144055500", "1126057000")],
                        "stations": {label: station_examples(db, label)
                                     for label in ("서울역", "불광", "홍대입구", "여의도", "강남", "사가정")}}
            if any(not data for data in (*examples["administrativeDongs"], *examples["stations"].values())):
                raise ValueError("expected_example_area_or_station_missing")
        target.replace(output / DATABASE)
    summary["databaseSha256"] = digest(output / DATABASE)
    atomic_json(output / "area-summary.json", summary)
    atomic_json(output / "query-examples.json", examples)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    selectors = parser.add_mutually_exclusive_group(required=True)
    selectors.add_argument("--build", action="store_true")
    selectors.add_argument("--station")
    selectors.add_argument("--district")
    selectors.add_argument("--dong")
    selectors.add_argument("--tile", nargs=2, type=int)
    args = parser.parse_args()
    output = local_path(args.output)
    if args.build:
        result = build(output)
    else:
        with closing(read_only(output / DATABASE)) as db:
            db.row_factory = sqlite3.Row
            if args.station:
                result = station_examples(db, args.station)
            elif args.district:
                result = district_card(db, args.district)
            elif args.dong:
                result = dong_card(db, args.dong)
            else:
                result = tile_card(db, *args.tile)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
