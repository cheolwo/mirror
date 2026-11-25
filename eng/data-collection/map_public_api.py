"""홍달 지도 자료용 SQLite의 초기화, 지역 기준 반입, API 수동 수집."""
from __future__ import annotations

import argparse
import re
from contextlib import closing
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta, timezone
from hashlib import sha256
from pathlib import Path
import sqlite3
import sys

from map_public_api_projection import (PROJECTION_VERSION, location, observations, region_hints, source_time,
                                       subjects)
from map_public_api_transport import (IntakeError, canonical, credentials, decode_page, ensure_no_secret,
                                      load_catalog, parse_parameters, request_page, require)
from map_region_reference import DEFAULT_REGION_REFERENCE, load_region_reference


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB = ROOT / "artifacts/local/public-data/hongdal-map-api/map-public-data.sqlite"
SCHEMA = Path(__file__).with_name("map_public_api_schema.sql")
APP_ID = 1213023553
SCHEMA_VERSION = 1
KST = timezone(timedelta(hours=9))
MAX_TOTAL_BYTES = 32 * 1024 * 1024


@dataclass(frozen=True)
class Page:
    number: int
    query: dict
    rows: list
    response_sha256: str
    retrieved_at: str


def timestamp():
    return datetime.now(UTC).isoformat(timespec="microseconds")


def fingerprint(value):
    return sha256(canonical(value).encode("utf-8")).hexdigest()


def open_database(path, *, initialize=False, read_only=False):
    if initialize:
        path.parent.mkdir(parents=True, exist_ok=True)
    require(initialize or path.is_file(), "map_database_missing")
    database = sqlite3.connect(path.resolve().as_uri() + ("?mode=ro" if read_only else ""),
                               uri=True, timeout=20)
    try:
        database.execute("PRAGMA foreign_keys=ON")
        application = database.execute("PRAGMA application_id").fetchone()[0]
        version = database.execute("PRAGMA user_version").fetchone()[0]
        if application == version == 0 and initialize:
            require(not database.execute("SELECT 1 FROM sqlite_schema LIMIT 1").fetchone(),
                    "existing_database_not_hongdal_map")
            database.executescript("BEGIN IMMEDIATE;\n" + SCHEMA.read_text(encoding="utf-8") + "\nCOMMIT;")
        else:
            require(application == APP_ID and version == SCHEMA_VERSION, "unsupported_map_database")
        return database
    except BaseException:
        database.close()
        raise


def register_sources(database, operations):
    with database:
        for operation in operations.values():
            portal_id = operation["portalId"]
            url = operation.get("officialUrl", f"https://www.data.go.kr/data/{portal_id}/openapi.do")
            database.execute("INSERT INTO api_source VALUES (?,?) ON CONFLICT(portal_id) DO NOTHING",
                             (portal_id, url))
            database.execute(
                """INSERT INTO api_operation VALUES (?,?,?,?,?)
                   ON CONFLICT(operation_id) DO UPDATE SET endpoint=excluded.endpoint,
                   contract_sha256=excluded.contract_sha256
                   WHERE api_operation.portal_id=excluded.portal_id AND api_operation.kind=excluded.kind
                   AND api_operation.contract_sha256<>excluded.contract_sha256""",
                (operation["id"], portal_id, operation["kind"], operation["url"], fingerprint(operation)))
            expected = (portal_id, operation["kind"])
            require(database.execute("SELECT portal_id,kind FROM api_operation WHERE operation_id=?",
                                     (operation["id"],)).fetchone() == expected, "operation_identity_changed")


def seed_regions(database, path):
    rows, relations, evidence = load_region_reference(path)
    with database:
        inserted = database.execute(
            """INSERT INTO region_release(filename,official_url,as_of,sha256) VALUES (?,?,?,?)
               ON CONFLICT(sha256) DO NOTHING RETURNING release_id""",
            (evidence["filename"], evidence["official_url"], evidence["as_of"], evidence["sha256"])).fetchone()
        if inserted:
            release = inserted[0]
            database.executemany(
                """INSERT INTO region(release_id,kind,code,name,parent_kind,parent_code)
                   VALUES (?,?,?,?,?,?)""",
                ((release, row["kind"], row["code"], row["name"], row["parent_kind"], row["parent_code"])
                 for row in rows))
            database.executemany(
                "INSERT INTO region_jurisdiction(release_id,administrative_code,legal_code) VALUES (?,?,?)",
                ((release, row["administrative_code"], row["legal_code"]) for row in relations))
    return {"regionRows": len(rows), "jurisdictionLinks": len(relations), "newRelease": bool(inserted),
            "asOf": evidence["as_of"], "sha256": evidence["sha256"]}


def start_run(database, operation, parameters):
    with database:
        cursor = database.execute(
            "INSERT INTO collection_run(operation_id,request_json,started_at,state) VALUES (?,?,?,'running')",
            (operation["id"], canonical(parameters), timestamp()))
        return cursor.lastrowid


def reserve_page(database, run_id, portal_id, number):
    local_date = datetime.now(KST).date().isoformat()
    with database:
        database.execute("BEGIN IMMEDIATE")
        if portal_id == "15076407":
            used = database.execute("SELECT COUNT(*) FROM request_attempt WHERE portal_id=? AND local_date=?",
                                    (portal_id, local_date)).fetchone()[0]
            require(used < 100, "traffic_daily_limit_reached", blocked=True)
        database.execute("INSERT INTO request_attempt VALUES (?,?,?,?)", (run_id, number, portal_id, local_date))


def finish(database, run_id, state, detail, rows, pages):
    database.execute(
        """UPDATE collection_run SET finished_at=?,state=?,detail=?,row_count=?,page_count=?
           WHERE run_id=?""", (timestamp(), state, detail, rows, pages, run_id))
    return {"runId": run_id, "state": state, "detail": detail, "rows": rows, "pages": pages}


def normalized_dong_name(name):
    return re.sub(r"[^가-힣0-9]|제(?=[0-9])", "", name)


def record_projection(database, operation, row, parameters):
    kind = operation["kind"]
    regions = region_hints(kind, row)
    commercial_code = row.get("adongCd", "")
    if kind in {"shop", "market_area"} and re.fullmatch(r"11\d{6}", commercial_code) and row.get("adongNm"):
        candidate = commercial_code + "00"
        official = database.execute("SELECT name FROM current_regions WHERE kind='administrative' AND code=?",
                                    (candidate,)).fetchone()
        if official and normalized_dong_name(official[0]) == normalized_dong_name(row["adongNm"]):
            regions.append(("administrative", candidate, "adongCd_seoul_8digit_name_match"))
    return {
        "sourceTime": source_time(row, kind),
        "regions": regions,
        "subjects": subjects(kind, row, parameters),
        "spatial": location(kind, row),
        "observations": list(observations(operation, row, parameters)),
    }


def put_projection(database, revision, projection):
    database.executemany("INSERT INTO record_region_hint VALUES (?,?,?,?)",
                         ((revision, *hint) for hint in projection["regions"]))
    for scheme, identifier, role in projection["subjects"]:
        database.execute("INSERT INTO map_subject(scheme,source_identifier) VALUES (?,?) ON CONFLICT DO NOTHING",
                         (scheme, identifier))
        database.execute(
            """INSERT INTO record_subject
               SELECT ?,subject_id,? FROM map_subject WHERE scheme=? AND source_identifier=?""",
            (revision, role, scheme, identifier))
    spatial = projection["spatial"]
    if spatial is not None:
        database.execute("INSERT INTO spatial_feature VALUES (?,?,?,?,?,?,?)",
                         (revision, *spatial[:-1], canonical(spatial[-1]) if spatial[-1] is not None else None))
    database.executemany("INSERT INTO map_observation VALUES (?,?,?,?,?,?)",
                         ((revision, *value) for value in projection["observations"]))


def save_revision(database, operation, row_key, row, parameters, run_id):
    scope = operation["kind"] if operation["kind"] in {"shop", "bus_stop"} else operation["id"]
    context = {name: parameters[name] for name in operation.get("identityParams", []) if name in parameters}
    source_key = canonical([context, row_key])
    record_key = (operation["portalId"], scope, source_key)
    database.execute(
        """INSERT INTO map_record(portal_id,identity_scope,kind,source_key) VALUES (?,?,?,?)
           ON CONFLICT(portal_id,identity_scope,source_key) DO NOTHING""",
        (record_key[0], record_key[1], operation["kind"], record_key[2]))
    record_id = database.execute(
        "SELECT record_id FROM map_record WHERE portal_id=? AND identity_scope=? AND source_key=?", record_key
    ).fetchone()[0]
    projection = record_projection(database, operation, row, parameters)
    digest = fingerprint([PROJECTION_VERSION, row, projection])
    inserted = database.execute(
        """INSERT INTO record_revision(record_id,content_sha256,data_json,source_updated_at) VALUES (?,?,?,?)
           ON CONFLICT(record_id,content_sha256) DO NOTHING RETURNING revision_id""",
        (record_id, digest, canonical(row), projection["sourceTime"])).fetchone()
    if inserted:
        revision = inserted[0]
        put_projection(database, revision, projection)
    else:
        revision = database.execute("SELECT revision_id FROM record_revision WHERE record_id=? AND content_sha256=?",
                                    (record_id, digest)).fetchone()[0]
    current = database.execute(
        """SELECT c.revision_id,c.run_id,r.source_updated_at,
                  COALESCE(json_extract(r.data_json,'$.chgGb'),'')='D' AS deletion
           FROM record_current c JOIN record_revision r USING(revision_id) WHERE c.record_id=?""",
        (record_id,)).fetchone()
    if current is not None:
        known_time = projection["sourceTime"]
        previous_time = current[2]
        if known_time and previous_time and known_time < previous_time:
            return revision, bool(inserted)
        if not known_time and current[3]:
            return revision, bool(inserted)
        if run_id < current[1] and (not known_time or not previous_time or known_time <= previous_time):
            return revision, bool(inserted)
    if current is None or revision != current[0]:
        database.execute(
            """INSERT INTO record_current VALUES (?,?,?) ON CONFLICT(record_id)
               DO UPDATE SET revision_id=excluded.revision_id,run_id=excluded.run_id""", (record_id, revision, run_id))
    elif run_id > current[1]:
        # Advance the ordering watermark for concurrent requests without adding a data revision.
        database.execute("UPDATE record_current SET run_id=? WHERE record_id=?", (run_id, record_id))
    return revision, bool(inserted)


def store_run(database, operation, pages, run_id, complete, parameters, next_page):
    records = 0
    added = 0
    contract = fingerprint(operation)
    with database:
        for page in pages:
            scope = canonical(page.query)
            digest = fingerprint(page.rows)
            key = (operation["id"], contract, scope, digest)
            database.execute(
                """INSERT INTO response_snapshot(operation_id,contract_sha256,request_json,content_sha256,row_count)
                   VALUES (?,?,?,?,?) ON CONFLICT DO NOTHING""", (*key, len(page.rows)))
            snapshot_id = database.execute(
                """SELECT snapshot_id FROM response_snapshot WHERE operation_id=? AND contract_sha256=?
                   AND request_json=? AND content_sha256=?""", key).fetchone()[0]
            database.execute("INSERT INTO run_snapshot VALUES (?,?,?,?,?)",
                             (run_id, page.number, snapshot_id, page.response_sha256, page.retrieved_at))
            for row_key, row in page.rows:
                revision, inserted = save_revision(database, operation, row_key, row, parameters, run_id)
                database.execute("INSERT INTO snapshot_record VALUES (?,?) ON CONFLICT DO NOTHING",
                                 (snapshot_id, revision))
                records += 1
                added += inserted
        state = "no_data" if not records and complete else "complete" if complete else "bounded"
        detail = None if complete else "more_pages" if next_page is not None else "last_page_of_partial_range"
        result = finish(database, run_id, state, detail, records, len(pages))
    return {**result, "newRevisions": added, "nextPage": next_page}


def collect(database, operation, parameters, page_limit, rows, *, from_page=None,
            fetch=request_page, secret_provider=credentials):
    paging = operation.get("paging")
    require(1 <= page_limit <= 20 and 1 <= rows <= operation.get("pageMax", 100),
            "request_budget_invalid")
    require(paging or (page_limit == 1 and from_page is None), "provider_pagination_unsupported")
    origin = paging["start"] if paging else 1
    first = origin if from_page is None else from_page
    require(origin <= first <= 1_000_000, "request_first_page_invalid")
    secret = secret_provider(operation)
    private_values = [secret[operation["keyParam"]]]
    ensure_no_secret(parameters, private_values)
    run_id = start_run(database, operation, {"params": parameters, "firstPage": first, "pages": page_limit,
                                             "pageSize": rows if paging else None})
    pages = []
    seen = set()
    provider_rows = 0
    byte_count = 0
    reached_end = not paging
    expected_total = None
    try:
        for index in range(page_limit):
            number = first + index
            query = dict(parameters)
            if paging:
                query.update({paging["page"]: str(number), paging["size"]: str(rows)})
            reserve_page(database, run_id, operation["portalId"], number)
            body = fetch(operation, query, secret)
            byte_count += len(body)
            require(byte_count <= MAX_TOTAL_BYTES, "collection_byte_budget_exceeded")
            normalized, count, total = decode_page(operation, body, private_values)
            provider_rows += count
            if total is not None:
                require(expected_total is None or total == expected_total, "response_total_changed_between_pages")
                expected_total = total
            keys = {key for key, _ in normalized}
            require(not keys.intersection(seen), "response_key_repeated_between_pages")
            seen.update(keys)
            pages.append(Page(number, query, normalized, sha256(body).hexdigest(), timestamp()))
            if paging:
                require(count <= rows, "provider_page_size_not_honored")
                if expected_total is not None:
                    remaining = max(0, expected_total - (first - origin) * rows)
                    require(provider_rows <= remaining, "response_total_inconsistent")
                    require(count or provider_rows == remaining, "response_page_missing")
                    reached_end = provider_rows == remaining
                else:
                    reached_end = count < rows
                if reached_end:
                    break
        next_page = None if reached_end else pages[-1].number + 1
        return store_run(database, operation, pages, run_id, reached_end and first == origin, parameters, next_page)
    except (IntakeError, sqlite3.Error, ValueError, TypeError, OverflowError) as exc:
        message = str(exc) if isinstance(exc, IntakeError) else "collection_processing_failed"
        state = "blocked" if isinstance(exc, IntakeError) and exc.blocked else "failed"
        with database:
            return finish(database, run_id, state, message, 0, len(pages))


def status(database):
    counts = {}
    for table in ("api_source", "api_operation", "region_release", "region", "region_jurisdiction", "map_record",
                  "record_revision", "current_map_features", "current_map_observations"):
        counts[table] = database.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
    counts["collections"] = dict(database.execute("SELECT state,COUNT(*) FROM collection_run GROUP BY state"))
    counts["recordsByKind"] = dict(database.execute("SELECT kind,COUNT(*) FROM map_record GROUP BY kind"))
    counts["foreignKeyIssues"] = len(database.execute("PRAGMA foreign_key_check").fetchall())
    return counts


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", default=DEFAULT_DB, type=Path)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("init")
    commands.add_parser("list")
    commands.add_parser("status")
    region_command = commands.add_parser("seed-regions")
    region_command.add_argument("--reference", type=Path, default=ROOT / DEFAULT_REGION_REFERENCE)
    collect_command = commands.add_parser("collect")
    collect_command.add_argument("operation")
    collect_command.add_argument("--param", action="append", default=[])
    collect_command.add_argument("--pages", type=int, default=1)
    collect_command.add_argument("--rows", type=int, default=20)
    collect_command.add_argument("--from-page", type=int)
    args = parser.parse_args(argv)
    try:
        operations = load_catalog()
        if args.command == "list":
            print(canonical([{"id": op["id"], "portalId": op["portalId"], "kind": op["kind"],
                              "required": op["required"], "optional": op["optional"]} for op in operations.values()]))
            return 0
        with closing(open_database(args.db, initialize=args.command == "init",
                                   read_only=args.command == "status")) as database:
            if args.command == "init":
                register_sources(database, operations)
                result = status(database)
            elif args.command == "seed-regions":
                result = seed_regions(database, args.reference)
            elif args.command == "status":
                result = status(database)
            else:
                require(args.operation in operations, "unknown_operation")
                operation = operations[args.operation]
                parameters = parse_parameters(operation, args.param)
                register_sources(database, {args.operation: operation})
                result = collect(database, operation, parameters, args.pages, args.rows, from_page=args.from_page)
        print(canonical(result))
        return 2 if result.get("state") in {"blocked", "failed"} else 0
    except IntakeError as exc:
        print(canonical({"state": "blocked" if exc.blocked else "failed", "detail": str(exc)}), file=sys.stderr)
        return 2
    except (OSError, sqlite3.Error, ValueError, KeyError):
        print(canonical({"state": "failed", "detail": "local_input_or_database_error"}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
