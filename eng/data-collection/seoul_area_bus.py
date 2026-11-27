"""Build a citywide Seoul bus stop and card-tag index from checked local inputs."""

import argparse
from collections import Counter, defaultdict
from contextlib import closing
import csv
import json
from pathlib import Path
import re
import sqlite3
from tempfile import TemporaryDirectory
import unicodedata

from seoul_tile_transit import PUBLIC, axial, digest, open_read, projection


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = PUBLIC / "seoul-area-evidence-20261125/bus"
PRIOR_RECEIPT = PUBLIC / "diorama-data-relations-20261123-r2/bus-stop-time-counts/source-receipts.json"
MASTER = PUBLIC / "diorama-public-layer-intake-20261123/r28-national-bus-audit/national-bus-stop-by-dong.sqlite"
DAILY_RECEIPT = PUBLIC / "seoul-tile-evidence-20261125/transit/raw/t-data-40-receipt.json"
MONTH = "2026-10"
MONTH_FILE = "seoul-bus-oa12913-202610.csv"
EARLIER_FILE = "seoul-stop-oa15067-20260902.xlsx"
DBNAME = "seoul-area-bus.sqlite"
POSITION_CHECKED = "same_id_code_name_and_within_3m"
STOP_SUFFIX = re.compile(r"^(.*)\((\d{5})\)$")

SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (
    source_id TEXT PRIMARY KEY, source_url TEXT NOT NULL, local_path TEXT NOT NULL,
    sha256 TEXT NOT NULL, byte_length INTEGER NOT NULL, scope TEXT NOT NULL
) WITHOUT ROWID;
CREATE TABLE stop (
    standard_stop_id TEXT PRIMARY KEY, ars_id TEXT, name TEXT NOT NULL,
    longitude REAL, latitude REAL, q INTEGER, r INTEGER,
    sgis_district_code TEXT, sgis_dong_code TEXT,
    binding_state TEXT NOT NULL, october_position_state TEXT NOT NULL,
    october_reference_distance_m REAL,
    position_source_id TEXT NOT NULL REFERENCES source(source_id),
    CHECK((q IS NULL)=(r IS NULL)),
    CHECK((q IS NOT NULL)=(binding_state='bound')),
    CHECK((q IS NOT NULL)=(longitude IS NOT NULL)),
    CHECK((q IS NOT NULL)=(latitude IS NOT NULL))
) WITHOUT ROWID;
CREATE INDEX stop_tile ON stop(q,r);
CREATE INDEX stop_sgis ON stop(sgis_district_code,sgis_dong_code);
CREATE TABLE monthly_row_review (
    source_row INTEGER PRIMARY KEY, month TEXT NOT NULL, source_stop_id TEXT NOT NULL,
    verified_stop_id TEXT REFERENCES stop(standard_stop_id),
    route_number TEXT NOT NULL, ars_id TEXT, source_name TEXT NOT NULL,
    review_state TEXT NOT NULL, source_id TEXT NOT NULL REFERENCES source(source_id)
);
CREATE TABLE monthly_stop_hour (
    standard_stop_id TEXT NOT NULL REFERENCES stop(standard_stop_id),
    month TEXT NOT NULL, hour INTEGER NOT NULL CHECK(hour BETWEEN 0 AND 23),
    boardings INTEGER CHECK(boardings>=0), alightings INTEGER CHECK(alightings>=0),
    observed_route_stop_rows INTEGER NOT NULL, boarding_rows INTEGER NOT NULL,
    alighting_rows INTEGER NOT NULL, source_id TEXT NOT NULL REFERENCES source(source_id),
    PRIMARY KEY(standard_stop_id,month,hour)
) WITHOUT ROWID;
CREATE TABLE daily_stop_hour (
    standard_stop_id TEXT NOT NULL REFERENCES stop(standard_stop_id),
    service_date TEXT NOT NULL, hour INTEGER NOT NULL CHECK(hour BETWEEN 0 AND 23),
    boardings INTEGER CHECK(boardings>=0), alightings INTEGER CHECK(alightings>=0),
    observed_route_stop_rows INTEGER NOT NULL, boarding_rows INTEGER NOT NULL,
    alighting_rows INTEGER NOT NULL, source_id TEXT NOT NULL REFERENCES source(source_id),
    PRIMARY KEY(standard_stop_id,service_date,hour)
) WITHOUT ROWID;
CREATE TABLE daily_row_status (
    service_date TEXT NOT NULL, review_state TEXT NOT NULL,
    source_rows INTEGER NOT NULL, source_id TEXT NOT NULL REFERENCES source(source_id),
    PRIMARY KEY(service_date,review_state)
) WITHOUT ROWID;
CREATE VIEW v_tile_bus_hour AS
SELECT s.q,s.r,m.month,m.hour,sum(m.boardings) AS boardings,sum(m.alightings) AS alightings,
       count(*) AS stops,sum(m.observed_route_stop_rows) AS route_stop_rows,
       sum(m.boarding_rows) AS boarding_rows,sum(m.alighting_rows) AS alighting_rows,
       'monthly_card_tag_count' AS unit
FROM monthly_stop_hour m JOIN stop s USING(standard_stop_id)
GROUP BY s.q,s.r,m.month,m.hour;
CREATE VIEW v_tile_bus_daily_hour AS
SELECT s.q,s.r,d.service_date,d.hour,sum(d.boardings) AS boardings,sum(d.alightings) AS alightings,
       count(*) AS stops,sum(d.observed_route_stop_rows) AS route_stop_rows,
       sum(d.boarding_rows) AS boarding_rows,sum(d.alighting_rows) AS alighting_rows,
       'daily_card_tag_count' AS unit
FROM daily_stop_hour d JOIN stop s USING(standard_stop_id)
GROUP BY s.q,s.r,d.service_date,d.hour;
"""


def ars_code(value):
    match = re.fullmatch(r"([0-9]{1,5})([AB]?)", str(value or "").strip())
    if not match:
        return None
    return match[1].zfill(5) + match[2]


def label_key(value):
    return "".join(unicodedata.normalize("NFC", value).split())


def read_earlier(path):
    from openpyxl import load_workbook

    book = load_workbook(path, read_only=True, data_only=True)
    result = {}
    try:
        rows = iter(book.active.values)
        if next(rows) != ("NODE_ID", "ARS_ID", "정류소명", "X좌표", "Y좌표", "정류소타입"):
            raise ValueError("Unexpected September stop schema")
        for identifier, ars, name, lon, lat, _ in rows:
            identifier = str(identifier or "").strip()
            if not identifier or identifier in result:
                raise ValueError("Invalid September stop identifier")
            result[identifier] = ars_code(ars), str(name or ""), lon, lat
    finally:
        book.close()
    return result


def within_korea(lon, lat):
    return isinstance(lon, (int, float)) and isinstance(lat, (int, float)) and 124 < lon < 131 and 33 < lat < 40


def read_stops(master, older):
    transform = projection()
    from pyproj import Geod

    distance_check = Geod(ellps="WGS84")
    result = {}
    with closing(open_read(master)) as db:
        for evidence in db.execute("""SELECT s.stop_id,s.ars_code,s.name,s.longitude,s.latitude,
            s.dong_code,d.district_code,d.province_code,e.state,e.local_ars
            FROM stop s LEFT JOIN seoul_evidence e USING(stop_id)
            LEFT JOIN dong d ON s.dong_code=d.dong_code
            WHERE s.owner_label='서울BIS' AND s.stop_id LIKE 'SEB%' ORDER BY s.stop_id"""):
            identifier = evidence["stop_id"][3:]
            ars = ars_code(evidence["ars_code"])
            name, lon, lat = (evidence[key] for key in ("name", "longitude", "latitude"))
            district, dong = evidence["district_code"], evidence["dong_code"]
            state, october, x, y, distance = "position_not_verified", "position_not_verified", None, None, None
            if (evidence["state"] == POSITION_CHECKED and ars
                    and ars == ars_code(evidence["local_ars"]) and within_korea(lon, lat)):
                if evidence["province_code"] == "11":
                    x, y = axial(*transform(lon, lat))
                    state = "bound"
                elif evidence["province_code"] is not None:
                    state = "outside_seoul"
                october = state
                if state == "bound":
                    old = older.get(identifier)
                    if old is None:
                        october = "september_reference_absent"
                    elif not within_korea(old[2], old[3]):
                        october = "september_position_unverified"
                    else:
                        distance = distance_check.inv(old[2], old[3], lon, lat)[2]
                        if (old[0], label_key(old[1])) != (ars, label_key(name)):
                            october = "september_identity_changed"
                        elif distance > 3.0:
                            october = "position_changed_over_3m"
                        else:
                            october = "bound"
            if state != "bound":
                lon = lat = district = dong = None
            result[identifier] = (
                identifier, ars, name, lon, lat, x, y, district, dong, state, october,
                round(distance, 6) if distance is not None else None, "november_positions",
            )
    return result


def compare_row(stops, identifier, ars, name, *, october):
    stop = stops.get(identifier)
    if stop is None:
        return "no_position_reference"
    if not ars:
        return "ars_missing"
    if ars != stop[1]:
        return "ars_mismatch"
    if label_key(name) != label_key(stop[2]):
        return "name_mismatch"
    return stop[10] if october else stop[9]


def count_value(value):
    if value == "":
        return None
    if not re.fullmatch(r"[0-9]+", value):
        raise ValueError("Unexpected card-tag count")
    return int(value)


def accumulate(bins, values):
    for hour, (boards, alights) in enumerate(values):
        target = bins[hour]
        if boards is not None:
            target[0] += boards
            target[2] += 1
        if alights is not None:
            target[1] += alights
            target[3] += 1
        target[4] += 1


def empty_bins():
    return [[0, 0, 0, 0, 0] for _ in range(24)]


def write_hours(db, table, source, aggregates):
    db.executemany(f"INSERT INTO {table} VALUES(?,?,?,?,?,?,?,?,?)", (
        (stop, period, hour, board if seen_board else None, alight if seen_alight else None,
         rows, seen_board, seen_alight, source)
        for (stop, period), periods in sorted(aggregates.items())
        for hour, (board, alight, seen_board, seen_alight, rows) in enumerate(periods)
    ))


def scan_month(db, path, stops, expected_rows):
    aggregated, reviews, states, seen = defaultdict(empty_bins), [], Counter(), set()
    attributed_totals = [[0, 0] for _ in range(24)]
    fields = ["사용년월", "노선번호", "노선명", "표준버스정류장ID", "버스정류장ARS번호", "역명"]
    for hour in ["00", *(str(i) for i in range(1, 24))]:
        fields.extend((f"{hour}시승차총승객수", f"{hour}시하차총승객수"))
    fields.extend(("교통수단타입코드", "교통수단타입명", "등록일자"))
    with path.open(encoding="cp949", newline="") as stream:
        reader = csv.reader(stream)
        if next(reader) != fields:
            raise ValueError("Unexpected monthly schema")
        for index, row in enumerate(reader, 1):
            if len(row) != len(fields) or row[0] != "202610":
                raise ValueError("Invalid monthly period or row")
            key = tuple(row[1:6] + [row[-3]])
            if key in seen:
                raise ValueError("Repeated monthly route-stop source identity")
            seen.add(key)
            identifier, ars = row[3].strip(), ars_code(row[4])
            suffix = STOP_SUFFIX.fullmatch(row[5])
            if not suffix:
                raise ValueError("Unexpected monthly stop-name format")
            name = suffix[1]
            state = compare_row(stops, identifier, ars, name, october=True)
            states[state] += 1
            reviews.append((index, MONTH, identifier, identifier if state == "bound" else None,
                            row[1], ars, name, state, "monthly_tags"))
            if state == "bound":
                values = [(count_value(row[6 + 2*h]), count_value(row[7 + 2*h])) for h in range(24)]
                accumulate(aggregated[identifier, MONTH], values)
                for hour, (board, alight) in enumerate(values):
                    attributed_totals[hour][0] += board or 0
                    attributed_totals[hour][1] += alight or 0
    if len(seen) != expected_rows:
        raise ValueError("Monthly source row count differs from its receipt")
    db.executemany("INSERT INTO monthly_row_review VALUES(?,?,?,?,?,?,?,?,?)", reviews)
    write_hours(db, "monthly_stop_hour", "monthly_tags", aggregated)
    bound_sums = db.execute("""SELECT coalesce(sum(boardings),0),coalesce(sum(alightings),0)
        FROM v_tile_bus_hour GROUP BY hour ORDER BY hour""").fetchall()
    if [tuple(row) for row in bound_sums] != [tuple(row) for row in attributed_totals]:
        raise ValueError("Monthly tile totals differ from verified source rows")
    return dict(sorted(states.items()))


def scan_daily(db, path, stops, expected_dates):
    aggregated, states, dates, seen = defaultdict(empty_bins), Counter(), Counter(), set()
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        counters = [(f"승차승객수량{hour:02d}H", f"하차승객수량{hour:02d}H") for hour in range(24)]
        fields = {"기준일", "노선ID", "정류장ID", "정류장명", "정류장번호", "정류장순서"}
        fields.update(field for pair in counters for field in pair)
        if not fields <= set(reader.fieldnames or ()):
            raise ValueError("Unexpected daily bus schema")
        for row in reader:
            day, route, identifier, sequence = (row[field] for field in ("기준일", "노선ID", "정류장ID", "정류장순서"))
            key = day, route, identifier, sequence
            if key in seen or day not in expected_dates:
                raise ValueError("Invalid daily bus sample range or repeated route-stop")
            seen.add(key)
            dates[day] += 1
            date_label = f"{day[:4]}-{day[4:6]}-{day[6:]}"
            state = compare_row(stops, identifier, ars_code(row["정류장번호"]), row["정류장명"], october=False)
            states[date_label, state] += 1
            if state == "bound":
                values = [(count_value(row[boarding]), count_value(row[alighting])) for boarding, alighting in counters]
                accumulate(aggregated[identifier, date_label], values)
    if dict(dates) != expected_dates:
        raise ValueError("Daily bus source rows differ from their receipt")
    db.executemany("INSERT INTO daily_row_status VALUES(?,?,?,?)",
                   ((day, state, count, "daily_tags") for (day, state), count in sorted(states.items())))
    write_hours(db, "daily_stop_hour", "daily_tags", aggregated)


def source_tuple(source_id, url, path, scope, expected=None):
    checksum = digest(path)
    if expected is not None and checksum != expected:
        raise ValueError(f"Input hash changed for {source_id}")
    local_name = path.resolve().relative_to(ROOT).as_posix()
    return source_id, url, local_name, checksum, path.stat().st_size, scope


def records(db, query):
    return [dict(row) for row in db.execute(query)]


def build(output):
    prior = json.loads(PRIOR_RECEIPT.read_text(encoding="utf-8"))
    daily = json.loads(DAILY_RECEIPT.read_text(encoding="utf-8"))
    raw_month, raw_earlier = output / "raw" / MONTH_FILE, output / "raw" / EARLIER_FILE
    raw_daily = DAILY_RECEIPT.parents[1] / daily["path"]
    sources = [
        source_tuple("monthly_tags", prior["monthly_original"]["catalogue_url"], raw_month,
                     "2026-10 서울 노선·정류소 월 누계", prior["monthly_original"]["sha256"]),
        source_tuple("september_positions", prior["pre_month_positions"]["catalogue_url"], raw_earlier,
                     "2026-09-02 서울 정류소 위치 대조", prior["pre_month_positions"]["sha256"]),
        source_tuple("november_positions", "https://www.data.go.kr/data/15067528/fileData.do", MASTER,
                     "2026-11 TAGO·서울 BIS·SGIS 대조색인"),
        source_tuple("daily_tags", daily["page"], raw_daily, "2026-11-19~20 서울 T-DATA 일별 표본", daily["sha256"]),
    ]
    stops = read_stops(MASTER, read_earlier(raw_earlier))
    with TemporaryDirectory(dir=output) as work:
        path = Path(work) / DBNAME
        with closing(sqlite3.connect(path)) as db:
            db.row_factory = sqlite3.Row
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO source VALUES(?,?,?,?,?,?)", sources)
                db.executemany("INSERT INTO stop VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)", stops.values())
                month_status = scan_month(db, raw_month, stops, prior["monthly_original"]["public_rows"])
                scan_daily(db, raw_daily, stops, daily["rowsByDate"])
            if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok" or db.execute("PRAGMA foreign_key_check").fetchone():
                raise ValueError("Bus database failed integrity checks")
            districts = records(db, """SELECT s.sgis_district_code,
                count(DISTINCT m.standard_stop_id) AS stops,sum(m.boardings) AS boardings,sum(m.alightings) AS alightings
                FROM monthly_stop_hour m JOIN stop s USING(standard_stop_id)
                GROUP BY s.sgis_district_code ORDER BY s.sgis_district_code""")
            samples = records(db, """SELECT s.name,s.standard_stop_id,s.q,s.r,m.hour,m.boardings,m.alightings
                FROM monthly_stop_hour m JOIN stop s USING(standard_stop_id) WHERE hour=12 AND standard_stop_id
                IN ('100000001','105000001','106000151') ORDER BY standard_stop_id""")
            summary = {
                "schema": "hongdal.seoul-area-bus.v1", "crs": "EPSG:5179", "spacingM": 300,
                "bindingRule": "exact ID + ARS + name; November source agreement; October also September within 3 m",
                "scope": "서울특별시 시내 위치 확인, 서울BIS 지자체 연계와 SGIS 2025 행정지역 기준",
                "sources": records(db, "SELECT * FROM source ORDER BY source_id"),
                "stationRegisterRows": len(stops),
                "stationRegisterStates": records(db, """SELECT binding_state,october_position_state,count(*) AS stops
                    FROM stop GROUP BY 1,2 ORDER BY 1,2"""),
                "monthlySourceRows": sum(month_status.values()), "monthlyReviewStates": month_status,
                "monthlyDistricts": districts, "monthlyDistrictCount": len(districts),
                "monthlyStops": db.execute("SELECT count(DISTINCT standard_stop_id) FROM monthly_stop_hour").fetchone()[0],
                "monthlyTileCount": db.execute("SELECT count(*) FROM (SELECT DISTINCT q,r FROM v_tile_bus_hour)").fetchone()[0],
                "monthlyAtNoonExamples": samples,
                "dailyCoverage": records(db, """SELECT service_date,count(DISTINCT standard_stop_id) AS stops
                    FROM daily_stop_hour GROUP BY service_date ORDER BY service_date"""),
                "dailyReviewStates": records(db, "SELECT * FROM daily_row_status ORDER BY 1,2"),
                "sqlIntegrity": True, "verifiedMonthlyTagConservation": True,
            }
        path.replace(output / DBNAME)
    summary["inputLimits"] = [
        "서울 내에서 위치와 자료 식별을 확인한 건을 정류소 점에 귀속. 미연결·충돌·서울 밖을 0으로 채우지 않음.",
        "10월 월 누계와 11월 19~20일 일별 표본은 별도로 읽음. 중복 탑승·환승 포함 가능한 태그 건수.",
        "공식 원본 ID가 반복되는 순환 노선은 원래의 노선·정류장 순서 관측을 유지. 실제로 확인되지 않은 출입구·보행 경로는 미배정.",
    ]
    (output / "bus-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    summary = build(args.output)
    print(json.dumps({key: summary[key] for key in ("monthlySourceRows", "monthlyDistrictCount", "monthlyStops",
                     "monthlyTileCount", "monthlyAtNoonExamples", "dailyCoverage", "monthlyReviewStates")},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
