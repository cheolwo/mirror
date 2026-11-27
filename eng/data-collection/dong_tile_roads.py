"""Build a read-only road, crossing and historical walking evidence index for Seoul tiles."""

from argparse import ArgumentParser
from collections import Counter, defaultdict
from contextlib import closing
import csv
from datetime import date, datetime, timezone
from io import BytesIO, TextIOWrapper
import json
import math
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory
from urllib.request import urlopen
from zipfile import ZipFile

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, point_key, vertices
from seoul_area_geography import gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest

OUTPUT = PUBLIC / "dong-tile-intake-20261126/transport"
INTAKE = PUBLIC / "diorama-public-layer-intake-20261123"
AREA = PUBLIC / "seoul-area-evidence-20261125/seoul-area.sqlite"
ROADS = INTAKE / "r79-road-gis-context/seoul-road-gis-context.sqlite"
SPEED = INTAKE / "r89-road-velocity-observations/seoul-road-velocity.sqlite"
WORKBOOK = SPEED.parent / "raw/topis-velocity-2026-10.xlsx"
CROSSWALKS = INTAKE / "r53-crosswalk-coordinate-review/crosswalk-coordinate-review.sqlite"
WALK = PUBLIC / "diorama-official-local-roads-20261116-r1/normalized/seoul-official-walk-inputs.sqlite"
COUNTS = INTAKE / "r73-traffic-observations/seoul-road-observations.sqlite"
MINUTE_URL = "https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=20001"
MINUTE_RIGHTS = "https://www.data.go.kr/data/15166094/fileData.do"
SMART_FILES = ((16271, "2026-11-22", "2026-11-23"), (16291, "2026-11-24", "2026-11-25"))
TURN_FIELDS = dict(L="LTURN", R="RTURN", S="STURN", U="UTURN")
SMART_HEADER = ["CROSS_ID", "CROSS_NM", "DIR_CD", "DIR_NM", "STND_DT", "STND_TM"] + [
    field + suffix for suffix in ("_VOL", "_VPH") for field in TURN_FIELDS.values()
]
STRUCTURE_FIELDS = ("고가도로", "지하철네트워크", "교량", "터널", "육교", "건물내")

SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_snapshot (
    source_id TEXT PRIMARY KEY, path TEXT NOT NULL, sha256 TEXT NOT NULL, bytes INTEGER NOT NULL,
    period TEXT NOT NULL, source_url TEXT NOT NULL, rights_note TEXT NOT NULL
);
CREATE TABLE tile (
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL CHECK(slot BETWEEN -1 AND 6),
    PRIMARY KEY(q,r,slot)
);
CREATE TABLE road_link (
    link_id TEXT PRIMARY KEY, road_label TEXT, start_label TEXT, end_label TEXT,
    source_reported_m REAL, geometry_m_5179 REAL NOT NULL CHECK(geometry_m_5179>0),
    source_id TEXT NOT NULL REFERENCES source_snapshot, speed_match TEXT NOT NULL
);
CREATE TABLE road_tile_link (
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL, link_id TEXT NOT NULL REFERENCES road_link,
    clipped_m REAL NOT NULL CHECK(clipped_m>0), PRIMARY KEY(q,r,slot,link_id),
    FOREIGN KEY(q,r,slot) REFERENCES tile
);
CREATE INDEX road_tiles_for_link ON road_tile_link(link_id);
CREATE TABLE speed_day (
    link_id TEXT NOT NULL REFERENCES road_link, observed_date TEXT NOT NULL,
    day_kind TEXT NOT NULL CHECK(day_kind IN ('weekday','weekend')), source_day_label TEXT,
    source_id TEXT NOT NULL REFERENCES source_snapshot, source_row INTEGER NOT NULL UNIQUE,
    by_hour_kmh TEXT NOT NULL CHECK(json_array_length(by_hour_kmh)=24), PRIMARY KEY(link_id,observed_date)
);
CREATE TABLE crossing_point (
    source_row INTEGER PRIMARY KEY, management_no TEXT, reported_district TEXT,
    crossing_kind TEXT, signal_report TEXT, coordinate_evidence TEXT NOT NULL,
    historical_comparison_m REAL, lon REAL NOT NULL, lat REAL NOT NULL,
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL CHECK(slot>=0),
    source_id TEXT NOT NULL REFERENCES source_snapshot, FOREIGN KEY(q,r,slot) REFERENCES tile
);
CREATE INDEX crossing_tiles ON crossing_point(q,r,slot);
CREATE TABLE walk_tile (
    q INTEGER NOT NULL, r INTEGER NOT NULL, slot INTEGER NOT NULL CHECK(slot>=0),
    structure_evidence TEXT NOT NULL, source_id TEXT NOT NULL REFERENCES source_snapshot,
    source_links INTEGER NOT NULL CHECK(source_links>0), clipped_m REAL NOT NULL CHECK(clipped_m>0),
    PRIMARY KEY(q,r,slot,structure_evidence),FOREIGN KEY(q,r,slot) REFERENCES tile
);
CREATE TABLE smart_count (
    junction_id TEXT NOT NULL, junction_label TEXT NOT NULL, incoming_code TEXT NOT NULL, incoming_label TEXT NOT NULL,
    observed_date TEXT NOT NULL, observed_time TEXT NOT NULL, turn_code TEXT NOT NULL,
    vehicles INTEGER CHECK(vehicles>=0), hourly_equivalent INTEGER CHECK(hourly_equivalent>=0),
    source_id TEXT NOT NULL REFERENCES source_snapshot, source_row INTEGER NOT NULL,
    PRIMARY KEY(junction_id,incoming_code,observed_date,observed_time,turn_code)
);
CREATE VIEW v_road_tile_coverage AS
    SELECT t.q,t.r,t.slot,count(g.link_id) AS geometry_links,sum(g.clipped_m) AS directional_m,
      count(CASE WHEN l.speed_match='geometry_confirmed_id_names_distance' THEN 1 END) AS speed_matched_links,
      sum(CASE WHEN l.speed_match='geometry_confirmed_id_names_distance' THEN g.clipped_m END) AS speed_matched_m
    FROM tile t LEFT JOIN road_tile_link g USING(q,r,slot) LEFT JOIN road_link l USING(link_id)
    GROUP BY t.q,t.r,t.slot;
CREATE VIEW v_road_tile_speed_day AS
    SELECT g.q,g.r,g.slot,s.observed_date,s.day_kind,CAST(h.key AS INTEGER)+1 AS hour_ending,
       count(h.value) AS observed_links,sum(CASE WHEN h.value IS NOT NULL THEN g.clipped_m END) AS observed_m,
       sum(h.value*g.clipped_m)/sum(CASE WHEN h.value IS NOT NULL THEN g.clipped_m END) AS weighted_kmh
    FROM road_tile_link g JOIN speed_day s USING(link_id) JOIN json_each(s.by_hour_kmh) h
    GROUP BY g.q,g.r,g.slot,s.observed_date,s.day_kind,h.key;
CREATE VIEW v_road_tile_speed_profile AS
    SELECT q,r,slot,day_kind,hour_ending,min(observed_date) AS sample_from,max(observed_date) AS sample_to,
      count(*) AS sample_days,count(weighted_kmh) AS usable_days,avg(weighted_kmh) AS mean_daily_weighted_kmh,
      min(weighted_kmh) AS min_daily_weighted_kmh,max(weighted_kmh) AS max_daily_weighted_kmh,
      avg(observed_m) AS mean_observed_directional_m
    FROM v_road_tile_speed_day GROUP BY q,r,slot,day_kind,hour_ending;
"""


def receipt(path, period, source_url, rights_note):
    return (path.relative_to(ROOT).as_posix(), digest(path), path.stat().st_size, period, source_url, rights_note)


def put_source(db, key, path, period, source_url, rights_note):
    fields = receipt(path, period, source_url, rights_note)
    db.execute("INSERT INTO source_snapshot VALUES (?,?,?,?,?,?,?)", (key, *fields))


def source_snapshots(db):
    put_source(db, "area", AREA, "2026-10", "https://www.data.go.kr/data/15050416/fileData.do",
               "existing indices only; official boundary raw kept in its prior local location")
    put_source(db, "road", ROADS, "retrieved 2026-11-23; provider geometry observation date unconfirmed",
               "https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=2", "T-DATA TOPIS attribution")
    put_source(db, "speed-match", SPEED, "2026-10",
               "https://data.seoul.go.kr/dataList/OA-22817/L/1/datasetView.do", "KOGL type 1 attribution")
    put_source(db, "speed", WORKBOOK, "2026-10; published 2026-11-05",
               "https://topis.seoul.go.kr/refRoom/openRefRoom_1.do?tab=velocityMonthly", "KOGL type 1 attribution")
    put_source(db, "crosswalk", CROSSWALKS, "2026-08-24 points corroborated against 2021 shapes",
               "https://data.seoul.go.kr/dataList/OA-23081/F/1/datasetView.do", "portal states no usage restrictions, checked 2026-11-23")
    put_source(db, "walk", WALK, "2020 geometry; acquired 2026-11-16",
               "https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do", "local review; verify terms before redistribution")
    put_source(db, "smart-hour", COUNTS, "2026-11-12 through 2026-11-22",
               "https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=20004", "T-DATA TOPIS attribution")


def tile_geometries(db, shapely):
    with closing(read_only(AREA)) as data:
        parents = tuple(data.execute("SELECT q,r FROM tile ORDER BY q,r"))
    keys = tuple((q, r, k) for q, r in parents for k in range(7))
    polygons = tuple(shapely.Polygon(vertices(*key)) for key in keys)
    parent_polygons = {key: shapely.Polygon(vertices(*key)) for key in parents}
    db.executemany("INSERT INTO tile VALUES (?,?,?)", ((q, r, -1) for q, r in parents))
    db.executemany("INSERT INTO tile VALUES (?,?,?)", keys)
    return keys, polygons, shapely.STRtree(polygons), parent_polygons


def tile_lengths(geometry, keys, polygons, tree):
    for i in sorted(tree.query(geometry, predicate="intersects")):
        length = geometry.intersection(polygons[i]).length
        if length > 1e-8:
            yield keys[i], length


def road_pieces(db, shapely, project, tiling):
    keys, polygons, tree, parents = tiling
    with closing(read_only(SPEED)) as matches:
        speed_matches = dict(matches.execute("SELECT observed_id,geometry_status FROM link_reference"))
    error_m = 0.0
    with closing(read_only(ROADS)) as roads:
        for link_id, road, start, end, source_m, value in roads.execute("""
                SELECT l.link_id,road_name,start_name,end_name,reported_m,longitude_latitude_json
                FROM service_geometry g JOIN service_link l USING(link_id)
                WHERE variant='02' AND measured_state='both_endpoints_within_10m' ORDER BY l.link_id
                """):
            line = project(shapely.LineString(json.loads(value)))
            children = dict(tile_lengths(line, keys, polygons, tree))
            db.execute("INSERT INTO road_link VALUES (?,?,?,?,?,?,?,?)", (
                link_id, road, start, end, source_m, line.length, "road", speed_matches.get(link_id, "not_in_speed_source")
            ))
            counts = defaultdict(float)
            for (q, r, _), length in children.items():
                counts[q, r] += length
            errors = [abs(line.intersection(parents[key]).length - length) for key, length in counts.items()]
            error_m = max(error_m, max(errors, default=0.0))
            if error_m > 0.01 or sum(counts.values()) - line.length > 0.01:
                raise ValueError(f"nonconservative_tile_length_{link_id}")
            db.executemany("INSERT INTO road_tile_link VALUES (?,?,?,?,?)", (
                (*key, link_id, length) for key, length in children.items()
            ))
            db.executemany("INSERT INTO road_tile_link VALUES (?,?,?,?,?)", (
                (*key, -1, link_id, length) for key, length in counts.items()
            ))
    return error_m


def speed_rows(db):
    from openpyxl import load_workbook

    with closing(read_only(SPEED)) as source:
        (verified_sha,) = source.execute("SELECT file_sha256 FROM source WHERE id='workbook'").fetchone()
    if digest(WORKBOOK) != verified_sha:
        raise ValueError("speed_workbook_differs_from_verified_link_match")
    usable = {row[0] for row in db.execute("SELECT link_id FROM road_link WHERE speed_match=?", (
        "geometry_confirmed_id_names_distance",))}
    book = load_workbook(WORKBOOK, read_only=True, data_only=True)
    stats = Counter()
    try:
        table = iter(book.active.values)
        if next(table)[12:] != tuple(f"~{h:02}시" for h in range(1, 25)):
            raise ValueError("unknown_speed_column_schema")
        for source_row, row in enumerate(table, 2):
            if row[0] is None:
                continue
            stats["rawRows"] += 1
            link_id = str(row[3])
            if link_id not in usable:
                continue
            parsed = date.fromisoformat(str(row[0]))
            values = [v if type(v) in (float, int) and 0 <= v and math.isfinite(v) else None for v in row[12:]]
            day_kind = "weekday" if parsed.weekday() < 5 else "weekend"
            stats["matchedRows"] += 1
            stats["matchedNumericValues"] += sum(v is not None for v in values)
            stats["matchedNullValues"] += values.count(None)
            db.execute("INSERT INTO speed_day VALUES (?,?,?,?,?,?,?)", (
                link_id, parsed.isoformat(), day_kind, row[1], "speed", source_row, json.dumps(values, separators=(",", ":"))
            ))
    finally:
        book.close()
    return dict(stats)


def crossing_rows(db, to_metric, parents):
    allowed = tuple(f"same_management_outline_within_{n}" for n in ("1cm", "1m", "5m"))
    stats = Counter()
    with closing(read_only(CROSSWALKS)) as data:
        for row in data.execute("""
            SELECT source_row, management_no, reported_district, kind_original,signal_original,coordinate_evidence,
                   historical_comparison_m,longitude_candidate,latitude_candidate FROM crossing_point ORDER BY source_row
            """):
            if row[5] not in allowed:
                stats["coordinateNotCorroborated"] += 1
                continue
            key = point_key(*to_metric(*row[-2:]))
            if key[:2] not in parents:
                stats["outsideGrid"] += 1
                continue
            db.execute("INSERT INTO crossing_point VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)", (*row, *key, "crosswalk"))
            stats["placedEvidencePoints"] += 1
    return dict(stats)


def walk_rows(db, shapely, project, tiling):
    keys, polygons, tree, _ = tiling
    counts = Counter()
    lengths = defaultdict(float)
    stats = Counter()
    with closing(read_only(WALK)) as data:
        for source_key, wkb, fields in data.execute("""
                SELECT key,geometry_wkb,source_fields_json FROM occurrences
                WHERE kind='LINK' AND status='ValidHistoricalSourceGeometry' AND source_type_code LIKE '1%'
                ORDER BY key
                """):
            stats["sourceLinks"] += 1
            source_fields = json.loads(fields)
            source_flags = [source_fields.get(field) for field in STRUCTURE_FIELDS]
            evidence = "flag_present" if "1" in source_flags else "no_positive_flag_reported"
            line = project(shapely.from_wkb(wkb))
            pieces = list(tile_lengths(line, keys, polygons, tree))
            if sum(length for _, length in pieces) > line.length + 0.01:
                raise ValueError(f"nonconservative_walk_length_{source_key}")
            if pieces:
                stats["linksTouchingTileGrid"] += 1
            for key, length in pieces:
                group = (*key, evidence)
                counts[group] += 1
                lengths[group] += length
    db.executemany("INSERT INTO walk_tile VALUES (?,?,?,?,?,?,?)", (
        (*key, "walk", counts[key], lengths[key]) for key in sorted(counts)
    ))
    return dict(stats)


def fetch_smart(output, db):
    receipts = []
    for file_id, observed_date, published in SMART_FILES:
        tag = observed_date.replace("-", "")
        source_id = "smart-5m-" + tag
        filename = f"TOPIS_5M_{published.replace('-', '')}_{tag}.zip"
        url = f"https://t-data.seoul.go.kr/dataprovide/download.do?id={file_id}"
        path = output / "raw" / filename
        path.parent.mkdir(parents=True, exist_ok=True)
        existed = path.is_file()
        if existed:
            payload = path.read_bytes()
        else:
            with urlopen(url, timeout=30) as response:
                payload = response.read(1_000_001)
                if response.headers.get_content_type() != "application/zip" or len(payload) > 1_000_000:
                    raise ValueError("unexpected_smart_count_download")
        rows = []
        forms = set()
        with ZipFile(BytesIO(payload)) as archive:
            csv_name = f"VW_SC_TRF_VOL_5M_{tag}.csv"
            if archive.namelist() != [csv_name] or archive.getinfo(csv_name).file_size > 2_000_000:
                raise ValueError("unexpected_smart_count_archive")
            with TextIOWrapper(archive.open(csv_name), encoding="utf-8-sig", newline="") as stream:
                reader = csv.DictReader(stream)
                if reader.fieldnames != SMART_HEADER:
                    raise ValueError("unexpected_smart_count_columns")
                for row in reader:
                    clock = row["STND_TM"]
                    if not clock.isdigit() or len(clock) != 4 or int(clock[:2]) > 23 or int(clock[2:]) not in range(0, 60, 5):
                        raise ValueError("unexpected_smart_count_clock")
                    if row["STND_DT"] != tag:
                        raise ValueError("unexpected_smart_count_date")
                    forms.add((row["CROSS_ID"], row["CROSS_NM"], row["DIR_CD"], row["DIR_NM"]))
                    for code, field in TURN_FIELDS.items():
                        rows.append((row["CROSS_ID"], row["CROSS_NM"], row["DIR_CD"], row["DIR_NM"], observed_date,
                                     clock[:2]+":"+clock[2:], code, int(row[field+"_VOL"]) if row[field+"_VOL"] else None,
                                     int(row[field+"_VPH"]) if row[field+"_VPH"] else None, source_id, reader.line_num))
        if not rows:
            raise ValueError("empty_smart_count_archive")
        if not existed:
            with path.open("xb") as handle:
                handle.write(payload)
        put_source(db, source_id, path, observed_date, url, "KOGL type 1 attribution")
        db.executemany("INSERT INTO smart_count VALUES (?,?,?,?,?,?,?,?,?,?,?)", rows)
        receipts.append({"sourceId": source_id, "catalogUrl": MINUTE_URL, "rightsUrl": MINUTE_RIGHTS,
                         "downloadUrl": url, "fileId": file_id, "rawPath": path.relative_to(ROOT).as_posix(),
                         "sha256": digest(path), "archiveBytes": len(payload), "member": csv_name,
                         "sourceRows": len(rows)//4, "turnRows": len(rows), "observedDate": observed_date,
                         "publisherFileDate": published, "acquisition": "existing_zip" if existed else "http_success_validated",
                         "junctions": sorted({(a,b) for a,b,_,_ in forms}), "directionGroups": len(forms),
                         "tileCoordinateState": "unresolved_same_source_id_mapping"})
    return receipts


def compare_smart_hour(db):
    groups = {(j,d,t,i,k): (n,value) for j,d,t,i,k,n,value in db.execute("""
        SELECT junction_id,observed_date,substr(observed_time,1,2)||':00',incoming_code,turn_code,count(vehicles),sum(vehicles)
        FROM smart_count WHERE observed_date='2026-11-22' GROUP BY 1,2,3,4,5
        """)}
    result = Counter()
    with closing(read_only(COUNTS)) as existing:
        for *key, reference in existing.execute("""
                SELECT junction_code,date,time,incoming_code,turn_code,vehicles FROM smart_hour WHERE date='2026-11-22'
                """):
            count, total = groups.get(tuple(key), (0, None))
            if count != 12 or reference is None:
                result["notFullyComparable"] += 1
            elif total == reference:
                result["completeAndEqual"] += 1
            else:
                result["completeButDifferent"] += 1
    return {key: result[key] for key in ("completeAndEqual", "completeButDifferent", "notFullyComparable")}


def build(output):
    pyproj, shapely = gis()
    from shapely.ops import transform

    to_metric = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform

    def project(geometry):
        return transform(to_metric, geometry)

    output.mkdir(parents=True, exist_ok=True)
    db_path = output / "tile-transport.sqlite"
    with TemporaryDirectory(dir=output) as temporary:
        pending = Path(temporary) / db_path.name
        with closing(sqlite3.connect(pending)) as db, db:
            db.executescript(SCHEMA)
            source_snapshots(db)
            tiling = tile_geometries(db, shapely)
            max_error = road_pieces(db, shapely, project, tiling)
            speeds = speed_rows(db)
            crossings = crossing_rows(db, to_metric, tiling[3])
            walks = walk_rows(db, shapely, project, tiling)
            smart_sources = fetch_smart(output, db)
            smart_matches = compare_smart_hour(db)
            counts = {table: db.execute(f"SELECT count(*) FROM {table}").fetchone()[0] for table in
                      ("tile", "road_link", "road_tile_link", "speed_day", "crossing_point", "walk_tile", "smart_count")}
            assert db.execute("PRAGMA integrity_check").fetchone() == ("ok",)
            assert not db.execute("PRAGMA foreign_key_check").fetchone()
            sample = [dict(q=q, r=r, slot=slot, kind=kind, hourEnding=hour, days=days, usableDays=usable,
                           weightedKmh=round(value, 2), observedM=round(length, 2))
                      for q,r,slot,kind,hour,days,usable,value,length in db.execute("""
                        SELECT q,r,slot,day_kind,hour_ending,sample_days,usable_days,mean_daily_weighted_kmh,
                               mean_observed_directional_m FROM v_road_tile_speed_profile
                        WHERE q=-32 AND r=-179 AND slot=-1 AND hour_ending IN (13,19) ORDER BY day_kind,hour_ending
                        """) if value is not None]
            input_receipts = [dict(zip(("sourceId", "path", "sha256", "bytes", "period", "sourceUrl", "rightsNote"), row))
                              for row in db.execute("SELECT * FROM source_snapshot ORDER BY source_id")]
        pending.replace(db_path)
    report = dict(gridRevision=REVISION, metricCrs="EPSG:5179", generatedAt=datetime.now(timezone.utc).isoformat(),
                  localOnly=True, publicReleaseApproved=False, routeAuthorized=False, counts=counts, roadSpeeds=speeds,
                  crossingCoordinates=crossings, historicalWalks=walks, newSmartCounts=smart_sources,
                  smartHourComparison=smart_matches, sagajeongSpeedSample=sample, sourceSnapshots=input_receipts,
                  maxRoadPartitionErrorM=max_error, databasePath=db_path.relative_to(ROOT).as_posix(),
                  databaseSha256=digest(db_path), databaseBytes=db_path.stat().st_size,
                  integrity="ok", foreignKeys="ok")
    atomic_json(output / "verification.json", report)
    print(json.dumps({"counts": counts, "speed": speeds, "crossings": crossings, "walks": walks,
                      "maxRoadPartitionErrorM": max_error, "smartHour": smart_matches, "sample": sample}, ensure_ascii=False))


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    build(parser.parse_args().output.resolve())
