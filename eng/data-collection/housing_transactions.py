"""Collect and compare official housing transaction snapshots in a local SQLite database."""

import argparse
from contextlib import closing
import csv
from dataclasses import dataclass
from datetime import date, datetime
from hashlib import sha256
import io
import itertools
import json
from pathlib import Path
import re
import sqlite3
from tempfile import NamedTemporaryFile
from typing import NamedTuple

from collect_public_snapshots import CollectionError, KST, UTC, atomic_json, global_lock, stamp
from housing_public_download import CONTRACT_GROUPS, HOUSING_TYPES, SOURCE_URL, HousingDownload

REPO = Path(__file__).resolve().parents[2]
LOCAL = REPO / "artifacts/local"
DEFAULT_OUTPUT = LOCAL / "public-data/hongdal-housing-transactions"
REGIONS = LOCAL / "public-data/hongdal-map-api/map-public-data.sqlite"
WAGES = LOCAL / "public-data/national-expansion-20261119-r1/income/residence-wages.sqlite"
SCHEMA = Path(__file__).with_suffix(".sql")
SEOUL = "11"
PRIORITY_DISTRICTS = ("11260", "11215", "11230", "11350")
MOLIT_FILE_SOURCE = "molit-rtms-public-csv"
WAGES_SOURCE = "nts-residence-wages-4-2-15"
PRODUCTS = tuple(itertools.product(HOUSING_TYPES, CONTRACT_GROUPS))
KINDS = {"apartment": "아파트", "villa": "연립다세대"}
GROUPS = {"sale": "매매", "rent": "전월세"}
AMOUNT = re.compile(r"[0-9]+")
AREA = re.compile(r"[0-9]+(?:\.[0-9]{1,10})?")
MISSING = {"", "-", "00000000"}


@dataclass(frozen=True)
class Scope:
    code: str
    district: str
    province: str
    housing: str
    group: str
    start: date
    end: date

    def key(self):
        return self.code, self.housing, self.group, self.start.isoformat(), self.end.isoformat()


class Transaction(NamedTuple):
    district_code: str
    legal_dong_code: str | None
    legal_dong_name: str
    housing_type: str
    contract_group: str
    deal_kind: str
    contract_date: str
    building_name: str | None
    area_m2: float
    floor: int | None
    build_year: int | None
    sale_krw: int | None
    deposit_krw: int | None
    monthly_krw: int | None
    canceled_on: str | None
    contract_status: str | None


def required(condition, code):
    if not condition:
        raise CollectionError(code)


def read_only(path):
    return sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)


def setup(db, districts):
    db.executescript(SCHEMA.read_text(encoding="utf-8"))
    with closing(read_only(REGIONS)) as region_db, db:
        insert = """INSERT INTO housing_region VALUES (?,?,?,?,?,?)
                    ON CONFLICT (code) DO UPDATE SET name=excluded.name, parent_code=excluded.parent_code,
                    kind=excluded.kind, as_of=excluded.as_of, registry_sha256=excluded.registry_sha256"""
        query = "SELECT code,name,parent_code,kind,as_of,registry_sha256 FROM current_regions "
        provinces = region_db.execute(query + "WHERE kind='sido' AND code=?", (SEOUL,)).fetchall()
        if len(provinces) != 1:
            raise CollectionError("housing_province_reference_missing")
        db.executemany(insert, provinces)
        for code in districts:
            area = region_db.execute(query + "WHERE kind='sigungu' AND code=? AND parent_code=?",
                                     (code, SEOUL)).fetchall()
            required(len(area) == 1, "housing_district_reference_missing")
            db.executemany(insert, area)
            db.executemany(insert, region_db.execute(query + "WHERE kind='legal' AND parent_code=?", (code,)))
        db.executemany("INSERT INTO housing_source VALUES (?,?,?) ON CONFLICT (id) DO NOTHING", [
            (MOLIT_FILE_SOURCE, "국토교통부 실거래가 공개시스템 조건별 CSV", SOURCE_URL),
            (WAGES_SOURCE, "국세청 주소지별 근로소득 연말정산 신고현황 4-2-15", "https://tasis.nts.go.kr/"),
        ])
    if WAGES.is_file():
        with closing(read_only(WAGES)) as source, db:
            for code in districts:
                name, = db.execute("SELECT name FROM housing_region WHERE code=?", (code,)).fetchone()
                rows = source.execute("""SELECT tax_year,gross_wage_filers,gross_wage_million_krw,source_sheet,source_row
                                         FROM residence_wages WHERE geographic_level='district'
                                         AND source_sido='서울' AND source_name=?""", (name,))
                db.executemany("""INSERT INTO district_income_reference VALUES (?,?,?,?,?,?,?)
                                  ON CONFLICT (district_code,tax_year) DO NOTHING""",
                               [(code, year, WAGES_SOURCE, filers, total, sheet, row)
                                for year, filers, total, sheet, row in rows])


def decode_csv(data):
    for encoding in ("utf-8-sig", "cp949"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    raise CollectionError("housing_csv_encoding_invalid")


def parse_date(year_month, day):
    required(re.fullmatch(r"[0-9]{6}", year_month) and re.fullmatch(r"[0-9]{1,2}", day),
             "housing_contract_date_invalid")
    try:
        return date(int(year_month[:4]), int(year_month[4:]), int(day))
    except ValueError:
        raise CollectionError("housing_contract_date_invalid") from None


def won(value):
    number = value.replace(",", "")
    required(AMOUNT.fullmatch(number) and len(number) <= 10, "housing_amount_invalid")
    return int(number) * 10_000


def optional_integer(value):
    required(value in MISSING or re.fullmatch(r"-?[0-9]{1,4}", value), "housing_numeric_field_invalid")
    return None if value in MISSING else int(value)


def cancel_day(value):
    if value in MISSING:
        return None
    try:
        return date.fromisoformat(value).isoformat()
    except ValueError:
        raise CollectionError("housing_cancel_date_invalid") from None


def clean_row(row, scope, legal_codes):
    prefix = f"{scope.province} {scope.district} "
    location = row["시군구"]
    required(location.startswith(prefix) and location[len(prefix):].strip(), "housing_row_region_mismatch")
    dong = location[len(prefix):].strip()
    observed = parse_date(row["계약년월"], row["계약일"])
    required(scope.start <= observed <= scope.end, "housing_row_date_outside_scope")
    raw_area = row["전용면적(㎡)"]
    required(AREA.fullmatch(raw_area) and 0 < float(raw_area) < 10000, "housing_area_invalid")
    exclusion = None
    if scope.group == "sale":
        kind, sale, deposit, monthly = "sale", won(row["거래금액(만원)"]), None, None
        required(sale > 0, "housing_sale_price_invalid")
    else:
        sale, deposit, monthly = None, won(row["보증금(만원)"]), won(row["월세금(만원)"])
        kind = {"전세": "jeonse", "월세": "monthly"}.get(row["전월세구분"])
        required(kind is not None, "housing_rent_type_unknown")
        if (kind == "jeonse" and (monthly != 0 or deposit == 0)) or (kind == "monthly" and monthly == 0):
            exclusion = "source_rent_group_amount_conflict"
    name = row["단지명" if scope.housing == "apartment" else "건물명"]
    status = row.get("계약구분", "")
    values = Transaction(scope.code, legal_codes.get(dong), dong, scope.housing, scope.group, kind,
                         observed.isoformat(), name if name not in MISSING else None, float(raw_area),
                         optional_integer(row["층"]), optional_integer(row["건축년도"]), sale, deposit, monthly,
                         cancel_day(row.get("해제사유발생일", "")), status if status not in MISSING else None)
    return values, exclusion


def parse_rows(data, count, scope, legal_codes):
    if data is None:
        required(count == 0, "housing_missing_csv_for_nonzero_count")
        return []
    source = csv.reader(io.StringIO(decode_csv(data), newline=""))
    metadata = {}
    header = None
    for cells in source:
        if cells and cells[0].strip() == "NO":
            header = [value.strip() for value in cells]
            break
        if len(cells) == 1 and " : " in cells[0]:
            key, value = cells[0].split(" : ", 1)
            metadata[key.strip()] = value.strip()
    required(metadata.get("계약일자") == f"{scope.start} ~ {scope.end}"
             and metadata.get("시도") == scope.province and metadata.get("시군구") == scope.district,
             "housing_csv_scope_mismatch")
    label = re.sub(r"[\s/·]", "", metadata.get("실거래구분", ""))
    required(label == f"{KINDS[scope.housing]}({GROUPS[scope.group]})", "housing_csv_product_mismatch")
    columns = {"NO", "시군구", "전용면적(㎡)", "계약년월", "계약일", "층", "건축년도",
               "단지명" if scope.housing == "apartment" else "건물명"}
    columns.update({"거래금액(만원)", "해제사유발생일"} if scope.group == "sale"
                   else {"전월세구분", "보증금(만원)", "월세금(만원)"})
    required(header is not None and columns <= set(header) and len(header) == len(set(header)),
             "housing_csv_columns_changed")
    rows = []
    for cells in source:
        if not any(cells):
            continue
        required(len(cells) == len(header), "housing_csv_row_width_invalid")
        row = dict(zip(header, (value.strip() for value in cells)))
        rows.append(clean_row(row, scope, legal_codes))
    required(len(rows) == count, "housing_csv_count_mismatch")
    return rows


def archive(output, data):
    if data is None:
        return None
    digest = sha256(data).hexdigest()
    raw = output / "raw" / f"{digest}.csv"
    raw.parent.mkdir(exist_ok=True)
    if not raw.exists():
        temporary = None
        try:
            with NamedTemporaryFile(dir=raw.parent, suffix=".tmp", delete=False) as stream:
                temporary = Path(stream.name)
                stream.write(data)
            temporary.replace(raw)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    required(sha256(raw.read_bytes()).hexdigest() == digest, "housing_raw_sha_mismatch")
    return digest


def install_snapshot(db, scope, rows, digest, retrieved):
    priced = []
    unpriced = []
    for index, (values, exclusion) in enumerate(rows, 1):
        if exclusion:
            unpriced.append((index, values.district_code, values.housing_type, values.contract_group,
                             values.contract_date, values.deal_kind, values.deposit_krw, values.monthly_krw, exclusion))
        else:
            priced.append((index, *values))
    with db:
        snapshot = db.execute("""INSERT INTO housing_snapshot
            (source_id,district_code,housing_type,contract_group,contract_start,contract_end,retrieved_at,row_count,raw_sha256)
            VALUES (?,?,?,?,?,?,?,?,?)""", (MOLIT_FILE_SOURCE, *scope.key(), retrieved, len(rows), digest)).lastrowid
        db.execute("""DELETE FROM housing_transaction
                      WHERE district_code=? AND housing_type=? AND contract_group=? AND contract_date BETWEEN ? AND ?""",
                   scope.key())
        db.execute("""DELETE FROM housing_unpriced_record
                      WHERE district_code=? AND housing_type=? AND contract_group=? AND contract_date BETWEEN ? AND ?""",
                   scope.key())
        db.executemany("INSERT INTO housing_transaction VALUES (" + ",".join(["?"] * 18) + ")",
                       [(snapshot, *row) for row in priced])
        db.executemany("INSERT INTO housing_unpriced_record VALUES (?,?,?,?,?,?,?,?,?,?)",
                       [(snapshot, *row) for row in unpriced])
        db.execute("""INSERT INTO housing_collection_attempt
                      (district_code,housing_type,contract_group,contract_start,contract_end,finished_at,status,snapshot_id)
                      VALUES (?,?,?,?,?,?,'stored',?)""", (*scope.key(), retrieved, snapshot))
    return snapshot, len(unpriced)


def collect(db, output, districts, start, end):
    setup(db, districts)
    downloader = HousingDownload()
    attempts = []
    for code in districts:
        district, province = db.execute("""SELECT d.name,p.name FROM housing_region AS d
            JOIN housing_region AS p ON p.code=d.parent_code WHERE d.code=?""", (code,)).fetchone()
        legal_codes = {name: item for name, item in db.execute("""
            SELECT name,code FROM housing_region WHERE parent_code=? AND kind='legal'
            GROUP BY name HAVING count(*)=1""", (code,))}
        for housing, group in PRODUCTS:
            scope = Scope(code, district, province, housing, group, start, end)
            result = {"districtCode": code, "housingType": housing, "contractGroup": group}
            try:
                count, data = downloader.download(code, province, district, housing, group, start, end)
                retrieved = stamp(datetime.now(UTC))
                rows = parse_rows(data, count, scope, legal_codes)
                digest = archive(output, data)
                snapshot, unpriced = install_snapshot(db, scope, rows, digest, retrieved)
                result.update(status="stored", rows=count, unpriced=unpriced, rawSha256=digest, snapshotId=snapshot)
            except (CollectionError, UnicodeError, csv.Error, OSError) as exc:
                problem = exc.code if isinstance(exc, CollectionError) else "housing_csv_or_storage_invalid"
                with db:
                    db.execute("""INSERT INTO housing_collection_attempt
                                  (district_code,housing_type,contract_group,contract_start,contract_end,finished_at,status,detail)
                                  VALUES (?,?,?,?,?,?,'failed',?)""", (*scope.key(), stamp(datetime.now(UTC)), problem))
                result.update(status="failed", reason=problem)
                if isinstance(exc, CollectionError) and exc.blocked:
                    attempts.append(result)
                    return attempts
            attempts.append(result)
    return attempts


def period_is_covered(db, code, housing, group, start, end):
    windows = db.execute("""SELECT contract_start,contract_end FROM housing_snapshot WHERE district_code=?
        AND housing_type=? AND contract_group=? AND contract_end>=? AND contract_start<=?
        ORDER BY contract_start,contract_end DESC""", (code, housing, group, start.isoformat(), end.isoformat()))
    cursor = start.toordinal()
    for first, last in windows:
        if date.fromisoformat(first).toordinal() > cursor:
            break
        cursor = max(cursor, date.fromisoformat(last).toordinal() + 1)
        if cursor > end.toordinal():
            return True
    return False


def lookup(db, args):
    from housing_price_queries import compare

    report = compare(db, args.district[0], args.start, args.end, args.income_krw,
                     legal_dong_code=args.legal_dong, area_min=args.area_min, area_max=args.area_max,
                     savings_krw=args.savings_krw)
    wage_fields = ("taxYear", "filers", "grossWagesMillionKrw", "averageReportedSalaryKrw")
    wages = db.execute("""SELECT tax_year,filer_count,gross_wage_million_krw,mean_gross_wage_krw
        FROM v_district_income_reference WHERE district_code=? ORDER BY tax_year DESC""", (args.district[0],))
    report["districtReportedWageReference"] = [dict(zip(wage_fields, row)) for row in wages]
    report["coverage"] = [{"housingType": housing, "contractGroup": group,
                           "covered": period_is_covered(db, args.district[0], housing, group, args.start, args.end)}
                          for housing, group in PRODUCTS]
    report["basis"] = "reported_transactions_by_contract_date"
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    commands = parser.add_subparsers(dest="command", required=True)
    collect_args = commands.add_parser("collect")
    compare_args = commands.add_parser("compare")
    for command in (collect_args, compare_args):
        command.add_argument("--district", action="append")
        command.add_argument("--start", required=True, type=date.fromisoformat)
        command.add_argument("--end", required=True, type=date.fromisoformat)
    compare_args.add_argument("--legal-dong")
    compare_args.add_argument("--area-min", type=float)
    compare_args.add_argument("--area-max", type=float)
    compare_args.add_argument("--income-krw", type=int)
    compare_args.add_argument("--savings-krw", type=int)
    args = parser.parse_args(argv)
    output = args.output.resolve()
    if not output.is_relative_to(LOCAL.resolve()):
        parser.error("output must be beneath artifacts/local")
    args.district = list(dict.fromkeys(args.district or
                        (PRIORITY_DISTRICTS if args.command == "collect" else PRIORITY_DISTRICTS[:1])))
    if (not all(re.fullmatch(r"11[0-9]{3}", code) for code in args.district) or len(args.district) > 8
            or not 0 <= (args.end - args.start).days < 366 or args.end > datetime.now(KST).date()
            or (args.command == "compare" and len(args.district) != 1)):
        parser.error("choose valid Seoul districts (one in compare) and up to one year of past contract dates")
    database = output / "housing-transactions.sqlite"
    try:
        if args.command == "collect":
            with global_lock(output / "collect.lock") as locked:
                required(locked, "housing_collection_already_running")
                with closing(sqlite3.connect(database)) as db:
                    attempts = collect(db, output, args.district, args.start, args.end)
                complete = len(attempts) == len(args.district) * len(PRODUCTS) and all(
                    item["status"] == "stored" for item in attempts)
                result = {"status": "completed" if complete else "partial", "source": SOURCE_URL,
                          "retrievedAt": stamp(datetime.now(UTC)), "from": str(args.start), "to": str(args.end),
                          "attempts": attempts}
                atomic_json(output / "last-collection.json", result)
        else:
            with closing(read_only(database)) as db:
                result = lookup(db, args)
            complete = True
    except (CollectionError, OSError, sqlite3.Error, ValueError) as exc:
        print(json.dumps({"status": "failed",
                          "reason": exc.code if isinstance(exc, CollectionError) else "housing_input_or_storage_invalid"}))
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if complete else 2


if __name__ == "__main__":
    raise SystemExit(main())
