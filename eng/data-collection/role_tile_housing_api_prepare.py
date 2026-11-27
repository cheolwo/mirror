"""Validate local MOLIT API XML and compare it with the retained housing CSV snapshot."""

import argparse
from calendar import monthrange
from collections import Counter
from contextlib import closing, contextmanager
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from decimal import Decimal
from hashlib import file_digest, sha256
import json
from pathlib import Path, PurePosixPath
import re
import sqlite3
from unicodedata import normalize
from xml.etree import ElementTree

from collect_public_snapshots import CollectionError
from housing_transactions import AREA, MISSING, MOLIT_FILE_SOURCE, PRIORITY_DISTRICTS, REPO, optional_integer, parse_date


DEFAULT_DB = REPO / "artifacts/local/public-data/hongdal-housing-transactions-20261125/housing-transactions.sqlite"
START, END = date(2026, 9, 1), date(2026, 11, 25)
ENDPOINT = "https://apis.data.go.kr/1613000"
PORTAL = "https://www.data.go.kr/data"
KST = timezone(timedelta(hours=9))
MAX_MANIFEST_BYTES = 4_000_000
MAX_XML_BYTES = 8_000_000
MAX_PAGE_SIZE = 1000
MAX_COHORT_ROWS = 100_000
MAX_COHORT_PAGES = 2000


@dataclass(frozen=True)
class Product:
    dataset: str
    service: str
    housing: str
    group: str
    building_field: str

    @property
    def endpoint(self):
        return f"{ENDPOINT}/{self.service}/get{self.service}"

    @property
    def portal(self):
        return f"{PORTAL}/{self.dataset}/openapi.do"


PRODUCTS = (
    Product("15126468", "RTMSDataSvcAptTradeDev", "apartment", "sale", "aptNm"),
    Product("15126474", "RTMSDataSvcAptRent", "apartment", "rent", "aptNm"),
    Product("15126467", "RTMSDataSvcRHTrade2", "villa", "sale", "mhouseNm"),
    Product("15126473", "RTMSDataSvcRHRent", "villa", "rent", "mhouseNm"),
)
COMMERCIAL = Product("15126463", "RTMSDataSvcNrgTrade", "commercial_business", "sale", "")


class CheckError(ValueError):
    """The safe-to-report reason raised at the first untrusted input boundary."""


def require(test, code):
    if not test:
        raise CheckError(code)


def fingerprint(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest()


def bounded_bytes(path, limit):
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    require(len(data) <= limit, "file_size_limit")
    return data


def digest_json(items):
    return sha256(json.dumps(items, ensure_ascii=False, separators=(",", ":")).encode()).hexdigest()


def strict_json_object(pairs):
    result = dict(pairs)
    require(len(result) == len(pairs), "manifest_duplicate_key")
    return result


def exact_keys(obj, keys, error):
    require(isinstance(obj, dict) and obj.keys() == set(keys), error)


def small_number(value, maximum, error):
    require(re.fullmatch(r"[0-9]{1,7}", value) is not None and int(value) <= maximum, error)
    return int(value)


def document_fields(node, skip=None):
    require(node is not None, "xml_expected_fields_missing")
    fields = {}
    for child in node:
        if child.tag == skip:
            continue
        require(child.tag not in fields and len(child) == 0 and child.attrib == {}, "xml_invalid_field_structure")
        fields[child.tag] = (child.text or "").strip()
    return fields


def parse_page(data):
    require(len(data) <= MAX_XML_BYTES, "xml_size_limit")
    try:
        plain = data.decode("utf-8-sig")
        require("<!DOCTYPE" not in plain.upper() and "<!ENTITY" not in plain.upper(), "xml_dtd_not_allowed")
        root = ElementTree.fromstring(plain)
    except (UnicodeError, ElementTree.ParseError) as exc:
        raise CheckError("xml_decode_or_parse_invalid") from exc
    code = (root.findtext("./header/resultCode") or root.findtext(".//returnReasonCode") or "").strip()
    if code in {"20", "30", "31", "32"}:
        raise CheckError("xml_provider_authorization_required")
    require(root.tag == "response" and code in {"0", "00", "000"}, "xml_provider_error_or_unknown_envelope")
    require(len(root.findall("header")) == 1 and len(root.findall("body")) == 1, "xml_duplicate_envelope")
    document_fields(root.find("header"))
    body = root.find("body")
    require(body is not None and len(body.findall("items")) <= 1, "xml_duplicate_items")
    counters = document_fields(body, skip="items")
    size = small_number(counters.get("numOfRows", ""), MAX_PAGE_SIZE, "xml_page_size_invalid")
    number = small_number(counters.get("pageNo", ""), MAX_COHORT_PAGES, "xml_page_number_invalid")
    total = small_number(counters.get("totalCount", ""), MAX_COHORT_ROWS, "xml_total_count_invalid")
    require(size > 0 and number > 0, "xml_zero_page_number_or_size")
    container = body.find("items")
    if container is None:
        require(total == 0, "xml_items_missing_for_nonempty_page")
        rows = []
    else:
        require(not (container.text or "").strip() and all(row.tag == "item" for row in container),
                "xml_invalid_items")
        rows = [document_fields(row) for row in container]
    return number, size, total, rows


def scope_months():
    return tuple(f"{START.year}{month:02}" for month in range(START.month, END.month + 1))


def month_window(month):
    year, number = int(month[:4]), int(month[4:])
    first = date(year, number, 1)
    last = date(year, number, monthrange(year, number)[1])
    return max(first, START), min(last, END)


def expected_cohorts():
    return {(product.dataset, district, month)
            for product in PRODUCTS for district in PRIORITY_DISTRICTS for month in scope_months()}


def as_utc(value):
    require(isinstance(value, str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)", value),
            "manifest_timestamp_must_be_utc")
    try:
        return datetime.fromisoformat(value).astimezone(timezone.utc)
    except ValueError as exc:
        raise CheckError("manifest_timestamp_invalid") from exc


def load_manifest(manifest):
    try:
        content = json.loads(bounded_bytes(manifest, MAX_MANIFEST_BYTES).decode("utf-8-sig"),
                             object_pairs_hook=strict_json_object)
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise CheckError("manifest_is_not_json") from exc
    exact_keys(content, ("schemaVersion", "inputKind", "cohorts"), "manifest_invalid_shape")
    require(type(content["schemaVersion"]) is int and content["schemaVersion"] == 1, "manifest_schema_version_invalid")
    require(content["inputKind"] in ("fixture", "provider_xml_user_supplied"), "manifest_input_kind_invalid")
    require(isinstance(content["cohorts"], list) and len(content["cohorts"]) <= len(expected_cohorts()),
            "manifest_invalid_cohort_list")
    by_id = {}
    reserved_paths = set()
    for cohort in content["cohorts"]:
        exact_keys(cohort, ("datasetId", "districtCode", "contractMonth", "officialEndpoint",
                            "collectedAtUtc", "numOfRows", "pages"), "manifest_invalid_cohort")
        key = tuple(cohort[k] for k in ("datasetId", "districtCode", "contractMonth"))
        require(all(isinstance(k, str) for k in key) and key in expected_cohorts()
                and key not in by_id, "manifest_cohort_outside_scope_or_duplicate")
        product = next(x for x in PRODUCTS if x.dataset == key[0])
        require(cohort["officialEndpoint"] == product.endpoint, "manifest_endpoint_mismatch")
        captured = as_utc(cohort["collectedAtUtc"]).astimezone(KST)
        require(captured.date() >= month_window(key[2])[1], "manifest_capture_predates_contract_scope_end")
        require(type(cohort["numOfRows"]) is int and 0 < cohort["numOfRows"] <= MAX_PAGE_SIZE, "manifest_page_size_invalid")
        pages = cohort["pages"]
        require(isinstance(pages, list) and 0 < len(pages) <= MAX_COHORT_PAGES, "manifest_page_list_invalid")
        numbers = set()
        for page in pages:
            exact_keys(page, ("pageNo", "file", "sha256"), "manifest_invalid_page")
            number = page["pageNo"]
            require(type(number) is int and 0 < number <= MAX_COHORT_PAGES and number not in numbers,
                    "manifest_invalid_or_duplicate_page_number")
            numbers.add(number)
            path = checked_xml_path(manifest.parent, page["file"])
            require(path not in reserved_paths, "manifest_duplicate_xml_path")
            reserved_paths.add(path)
            require(isinstance(page["sha256"], str) and re.fullmatch(r"[0-9a-f]{64}", page["sha256"]),
                    "manifest_page_hash_invalid")
        by_id[key] = cohort
    return content["inputKind"], by_id


def checked_xml_path(parent, name):
    require(isinstance(name, str) and name.isascii() and re.fullmatch(r"[A-Za-z0-9._/-]{1,255}", name),
            "manifest_unsafe_path")
    relative = PurePosixPath(name)
    require(not relative.is_absolute() and ".." not in relative.parts and relative.suffix == ".xml",
            "manifest_xml_relative_file_required")
    root = parent.resolve()
    path = (root / Path(*relative.parts)).resolve()
    require(path.is_relative_to(root), "manifest_xml_path_escaped_bundle")
    return path


def load_pages(cohort, root):
    size = cohort["numOfRows"]
    combined = []
    total = None
    fingerprints = set()
    evidence = []
    for receipt in sorted(cohort["pages"], key=lambda part: part["pageNo"]):
        data = bounded_bytes(checked_xml_path(root, receipt["file"]), MAX_XML_BYTES)
        require(sha256(data).hexdigest() == receipt["sha256"], "xml_sha256_mismatch")
        number, reported_size, count, rows = parse_page(data)
        require(number == receipt["pageNo"] and reported_size == size, "xml_page_receipt_mismatch")
        total = count if total is None else total
        require(total == count, "xml_changing_total_count")
        page_count = max(1, (count + size - 1) // size)
        require(number <= page_count and len(rows) == min(size, max(0, count - (number - 1) * size)),
                "xml_page_row_count_mismatch")
        if rows:
            content = digest_json(sorted(digest_json(sorted(row.items())) for row in rows))
            require(content not in fingerprints, "xml_repeated_page_items_review_required")
            fingerprints.add(content)
        combined.extend(rows)
        require(len(combined) <= count, "xml_rows_exceed_reported_total")
        evidence.append({"pageNo": number, "rows": len(rows), "sha256": receipt["sha256"]})
    require(total is not None and len(combined) == total
            and [page["pageNo"] for page in evidence] == list(range(1, max(1, (total + size - 1) // size) + 1)),
            "xml_incomplete_page_sequence")
    return combined, evidence


def normalized_name(value):
    return " ".join(normalize("NFKC", value).split()) if value else ""


def parse_amount(value):
    if re.fullmatch(r"(?:[0-9]{1,10}|[0-9]{1,3}(?:,[0-9]{3}){1,3})", value):
        digits = value.replace(",", "")
        if len(digits) <= 10:
            return int(digits) * 10_000
    return None


def canonical_area(raw):
    return format(Decimal(str(raw)).normalize(), "f")


def parse_cancellation(value):
    if value in MISSING:
        return None
    if re.fullmatch(r"[0-9]{2}\.[0-9]{2}\.[0-9]{2}", value):
        # This collection uses 21st-century contracts and captures. The caller
        # still rejects dates before the contract or after the API capture.
        value = "20" + value.replace(".", "-")
    require(re.fullmatch(r"(?:[0-9]{8}|[0-9]{4}-[0-9]{2}-[0-9]{2})", value) is not None,
            "xml_cancel_date_invalid")
    try:
        return date.fromisoformat(value)
    except ValueError as exc:
        raise CheckError("xml_cancel_date_invalid") from exc


def bridge(row, product, district, month, legal):
    require(row.get("sggCd") == district, "xml_row_district_mismatch")
    require(re.fullmatch(r"[0-9]{4}", row.get("dealYear", ""))
            and re.fullmatch(r"[0-9]{1,2}", row.get("dealMonth", "")), "xml_row_date_invalid")
    year_month = row["dealYear"] + row["dealMonth"].zfill(2)
    require(year_month == month, "xml_row_month_mismatch")
    when = parse_date(year_month, row.get("dealDay", ""))
    dong = normalized_name(row.get("umdNm", ""))
    require(dong and dong not in MISSING, "xml_row_legal_dong_missing")
    code = legal.get(dong)
    if product.dataset == "15126468":
        given = row.get("umdCd", "")
        require(re.fullmatch(r"[0-9]{5}", given) is not None, "xml_legal_code_invalid")
        require(code is None or code == district + given, "xml_legal_code_name_conflict")
    building = normalized_name(row.get(product.building_field, ""))
    floor = optional_integer(row.get("floor", ""))
    build_year = optional_integer(row.get("buildYear", ""))
    canceled = parse_cancellation(row.get("cdealDay", ""))
    require(canceled is None or canceled >= when, "xml_cancel_date_precedes_contract")
    hold = None
    if product.group == "sale":
        price = parse_amount(row.get("dealAmount", ""))
        kind, amounts = "sale", (price, None, None)
        if row.get("cdealType", "") not in MISSING and canceled is None:
            hold = "cancellation_mark_without_date"
        elif price is None or price == 0:
            hold = "sale_amount_invalid"
    else:
        require(canceled is None and row.get("cdealType", "") in MISSING, "xml_rent_unmodeled_cancellation")
        deposit, rent = parse_amount(row.get("deposit", "")), parse_amount(row.get("monthlyRent", ""))
        kind = "jeonse" if rent == 0 else "monthly"
        amounts = (None, deposit, rent)
        if deposit is None or rent is None or (deposit == 0 and rent == 0):
            hold = "rent_amount_missing_or_zero_without_deposit"
    area = row.get("excluUseAr", "")
    if AREA.fullmatch(area) is None or not 0 < Decimal(area) < 10000:
        hold = hold or "exclusive_area_absent_or_invalid"
    if code is None:
        hold = hold or "legal_dong_not_joined"
    if hold:
        return when, None, hold, canceled
    signature = (when.isoformat(), code, building if building not in MISSING else "", canonical_area(area), floor,
                 build_year, kind, *amounts, canceled.isoformat() if canceled else None)
    return when, signature, None, canceled


@contextmanager
def frozen_database(path):
    resolved = path.resolve()
    sidecars = [resolved.with_name(resolved.name + ending) for ending in ("-wal", "-shm", "-journal")]
    require(resolved.is_file() and not any(candidate.exists() for candidate in sidecars), "baseline_not_frozen")
    before = fingerprint(resolved)
    try:
        with closing(sqlite3.connect(resolved.as_uri() + "?mode=ro&immutable=1", uri=True)) as db:
            db.execute("PRAGMA query_only=ON")
            require(db.execute("PRAGMA integrity_check").fetchone() == ("ok",)
                    and not db.execute("PRAGMA foreign_key_check").fetchall(), "baseline_integrity_failed")
            yield db, before
    finally:
        require(fingerprint(resolved) == before and not any(candidate.exists() for candidate in sidecars),
                "baseline_changed_during_read")


def unique_legal_dongs(db, district):
    names = {}
    for name, code in db.execute("SELECT name,code FROM housing_region WHERE kind='legal' AND parent_code=?", (district,)):
        names.setdefault(normalized_name(name), []).append(code)
    return {name: codes[0] for name, codes in names.items() if len(codes) == 1}


def baseline_window(db, product, district, month):
    start, end = month_window(month)
    params = (district, product.housing, product.group, start.isoformat(), end.isoformat(), MOLIT_FILE_SOURCE)
    windows = db.execute("""
        SELECT contract_start,contract_end,retrieved_at FROM housing_snapshot
        WHERE district_code=? AND housing_type=? AND contract_group=? AND contract_end>=? AND contract_start<=?
          AND source_id=? ORDER BY contract_start,contract_end DESC
    """, params).fetchall()
    cursor = start
    for first, last, _ in windows:
        if date.fromisoformat(first) > cursor:
            break
        cursor = max(cursor, date.fromisoformat(last) + timedelta(days=1))
    return params, cursor > end, sorted({row[2] for row in windows})


def baseline_cohort(db, product, district, month):
    params, covered, capture_times = baseline_window(db, product, district, month)
    priced = db.execute("""
        SELECT t.contract_date,t.legal_dong_code,t.building_name,t.area_m2,t.floor,t.build_year,t.deal_kind,
               t.sale_krw,t.deposit_krw,t.monthly_krw,t.canceled_on
        FROM housing_transaction t JOIN housing_snapshot s ON s.id=t.snapshot_id
        WHERE t.district_code=? AND t.housing_type=? AND t.contract_group=? AND t.contract_date BETWEEN ? AND ? AND s.source_id=?
    """, params)
    rows = Counter()
    unmapped = 0
    for when, code, name, area, floor, build, kind, sale, deposit, rent, canceled in priced:
        if code is None or area is None:
            unmapped += 1
            continue
        rows[(when, code, normalized_name(name), canonical_area(area), floor, build, kind, sale, deposit, rent, canceled)] += 1
    unpriced, = db.execute("""
        SELECT count(*) FROM housing_unpriced_record t JOIN housing_snapshot s ON s.id=t.snapshot_id
        WHERE t.district_code=? AND t.housing_type=? AND t.contract_group=? AND t.contract_date BETWEEN ? AND ? AND s.source_id=?
    """, params).fetchone()
    return rows, {
        "csvScopeComplete": covered, "csvObservationTimesUtc": capture_times,
        "csvRowsInsideWindow": rows.total() + unpriced + unmapped,
        "csvRowsHeld": unpriced + unmapped, "csvSaleCanceledRows": sum(n for key, n in rows.items() if key[-1]),
    }


def comparison(new, old):
    overlap = (new & old).total()
    added, removed = new - old, old - new
    no_cancel_add, no_cancel_remove = Counter(), Counter()
    shape_add, shape_remove = Counter(), Counter()
    for changes, no_cancel, shape in ((added, no_cancel_add, shape_add), (removed, no_cancel_remove, shape_remove)):
        for signature, count in changes.items():
            no_cancel[signature[:-1]] += count
            shape[signature[:6]] += count
    return {"equivalentComparableRows": overlap, "apiOnlyComparableRows": added.total(),
            "csvOnlyComparableRows": removed.total(),
            "possibleCancellationDateEditsWithoutRowId": (no_cancel_add & no_cancel_remove).total(),
            "signatureShapeCoincidencesWithoutRowId": (shape_add & shape_remove).total()}


def inspect_cohort(db, product, district, month, cohort, root):
    reference, old = baseline_cohort(db, product, district, month)
    first, last = month_window(month)
    result = {
        "datasetId": product.dataset, "housingType": product.housing, "contractGroup": product.group,
        "districtCode": district, "contractMonth": month, "comparisonFrom": first.isoformat(),
        "comparisonThrough": last.isoformat(), **old,
    }
    if cohort is None:
        return {**result, "state": "missing_api_pages"}
    try:
        rows, pages = load_pages(cohort, root)
        legal = unique_legal_dongs(db, district)
        normalized, holds = Counter(), Counter()
        outside = canceled_rows = 0
        kst_day = as_utc(cohort["collectedAtUtc"]).astimezone(KST).date()
        for item in rows:
            observed, signature, problem, canceled = bridge(item, product, district, month, legal)
            require(observed <= kst_day, "xml_contract_after_capture_date")
            require(canceled is None or canceled <= kst_day, "xml_cancellation_after_capture_date")
            if not first <= observed <= last:
                outside += 1
                continue
            canceled_rows += canceled is not None
            if problem:
                holds[problem] += 1
            else:
                normalized[signature] += 1
    except (CheckError, CollectionError, OSError) as exc:
        reason = str(exc) if isinstance(exc, CheckError) else exc.code if isinstance(exc, CollectionError) else "xml_file_unreadable"
        return {**result, "state": "api_page_or_row_validation_failed", "reason": reason}
    differences = comparison(normalized, reference) if old["csvScopeComplete"] else None
    varying = differences and (differences["apiOnlyComparableRows"] or differences["csvOnlyComparableRows"])
    return {**result, "state": "comparison_review_required" if holds or varying or not differences or old["csvRowsHeld"]
            else "matched_comparable_scope", "pages": pages, "apiCaptureUtc": cohort["collectedAtUtc"],
            "apiReportedWholeMonthRows": len(rows), "apiRowsOutsideRetainedCutoff": outside,
            "apiComparableRowsInsideWindow": normalized.total(), "apiRowsHeldByCause": dict(sorted(holds.items())),
            "apiSaleCanceledRowsInsideWindow": canceled_rows,
            "apiSaleCanceledComparableRows": sum(n for key, n in normalized.items() if key[-1]),
            "apiRepeatedComparableSignaturesPreserved": normalized.total()-len(normalized),
            "rentalKindBasis": "reported_deposit_and_monthly_rent_amounts" if product.group == "rent" else None,
            "differences": differences}


def report(database, manifest=None):
    input_kind, supplied = load_manifest(manifest) if manifest else ("no_api_pages", {})
    with frozen_database(database) as (db, digest):
        cohorts = [
            inspect_cohort(db, product, district, month, supplied.get((product.dataset, district, month)),
                           manifest.parent if manifest else None)
            for product in PRODUCTS for district in PRIORITY_DISTRICTS for month in scope_months()
        ]
    counts = Counter(row["state"] for row in cohorts)
    return {
        "inputKind": input_kind, "baselineSha256": digest, "baselineUnchanged": True,
        "captureAuthority": "input_declared_by_caller", "networkRequests": 0,
        "candidateState": "manual_review_only",
        "expectedCohorts": len(expected_cohorts()), "cohortStates": dict(sorted(counts.items())),
        "csvRowsWithinOriginalWindow": sum(row["csvRowsInsideWindow"] for row in cohorts),
        "csvHeldRows": sum(row["csvRowsHeld"] for row in cohorts),
        "csvSaleCanceledRows": sum(row["csvSaleCanceledRows"] for row in cohorts),
        "housingComparedProducts": [{"datasetId": product.dataset, "endpoint": product.endpoint,
                                     "officialSource": product.portal} for product in PRODUCTS],
        "separateCommercialSource": {"datasetId": COMMERCIAL.dataset, "endpoint": COMMERCIAL.endpoint,
                                     "officialSource": COMMERCIAL.portal, "state": "outside_housing_schema"},
        "cohorts": cohorts,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", type=Path, default=DEFAULT_DB, help="frozen local CSV archive SQLite")
    parser.add_argument("--manifest", type=Path, help="relative authorized XML bundle, omit for baseline only")
    args = parser.parse_args(argv)
    try:
        summary = report(args.database, args.manifest)
    except (CheckError, OSError, sqlite3.Error) as exc:
        safe = str(exc) if isinstance(exc, CheckError) else "local_input_unreadable_or_schema_incompatible"
        print(json.dumps({"state": "invalid_input", "reason": safe}, ensure_ascii=False))
        return 2
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    if args.manifest and summary["cohortStates"] != {"matched_comparable_scope": summary["expectedCohorts"]}:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
