"""확인된 배달 자료를 로컬 읽기 전용 검증 계약으로만 옮긴다.

원본·운영 DB·기기에는 쓰지 않는다. 고객 위치, 플랫폼 주문번호, 원문
전사와 화면은 출력 계약에 없으며, 앱 후보 연결은 주문 연결로 승격하지 않는다.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import re
import sqlite3
import subprocess
import sys
from datetime import date
from decimal import Decimal, InvalidOperation
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
LOCAL = (REPO / "artifacts/local").resolve()
LEDGER = "edit-kit/branch-deliveries-all-20261010/delivery-count-ledger.json"
EXPECTED_COUNT = 574
EXPECTED_FEE = Decimal("2255300")
EXPECTED_DISTANCE = Decimal("1115.8978")
START, END = "2026-09-23", "2026-10-09"
SENSITIVE_TEXT = re.compile(r"(?:01[016789][- ]?\d{3,4}[- ]?\d{4}|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|고객\s*(?:성명|이름|전화|연락처)|전달지\s*상세)")


class ImportFailure(Exception):
    """값·경로·주문 식별값을 콘솔 오류에 노출하지 않는 실패 코드."""


def require(condition, code):
    if not condition:
        raise ImportFailure(code)


def digest(payload):
    return hashlib.sha256(payload).hexdigest()


def replay_id(kind, value):
    payload = json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    return "replay-" + kind + "-" + digest(payload)


def text_value(value, code, optional=False):
    if value is None and optional:
        return None
    require(isinstance(value, str), code)
    value = " ".join(value.split())
    require(bool(value) and len(value) <= 1000 and not SENSITIVE_TEXT.search(value), code)
    return value


def number(value, code, optional=False, integer=False):
    if value is None and optional:
        return None
    require(not isinstance(value, bool) and value is not None, code)
    try:
        parsed = Decimal(str(value))
    except (InvalidOperation, ValueError):
        raise ImportFailure(code) from None
    require(parsed.is_finite() and parsed >= 0, code)
    require(not integer or parsed == parsed.to_integral_value(), code)
    return int(parsed) if parsed == parsed.to_integral_value() else float(parsed)


def quantity(value):
    result = number(value, "MENU_QUANTITY_INVALID", optional=True, integer=True)
    require(result is None or result > 0, "MENU_QUANTITY_INVALID")
    return result


def local_path(path):
    resolved = Path(path).resolve()
    require(resolved.is_relative_to(LOCAL) and resolved != LOCAL, "OUTPUT_OUTSIDE_LOCAL_ARTIFACTS")
    relative = resolved.relative_to(REPO).as_posix()
    ignored = subprocess.run(["git", "check-ignore", "-q", "--", relative], cwd=REPO,
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    require(ignored.returncode == 0, "OUTPUT_NOT_GIT_IGNORED")
    return resolved


class Sources:
    def __init__(self, root):
        self.root = root
        self.hashes = {}
        self.paths = {}

    def register(self, path, project=False):
        path = path.resolve()
        base = REPO if project else self.root
        require(path.is_relative_to(base), "SOURCE_PATH_OUTSIDE_ROOT")
        relative = path.relative_to(base).as_posix()
        payload = path.read_bytes()
        sha = digest(payload)
        require(relative not in self.hashes or self.hashes[relative] == sha, "SOURCE_CHANGED_DURING_IMPORT")
        self.hashes[relative], self.paths[relative] = sha, path
        return relative, sha, payload

    def read(self, relative):
        name, sha, payload = self.register(self.root / relative)
        return json.loads(payload.decode("utf-8-sig")), name, sha

    def ref(self, relative, field, basis):
        return {"relativePath": relative, "sha256": self.hashes[relative], "field": field, "basis": basis}

    def verify_unchanged(self):
        require(all(digest(path.read_bytes()) == self.hashes[name] for name, path in self.paths.items()),
                "SOURCE_CHANGED_DURING_IMPORT")


def menu_items(items, app=False):
    require(isinstance(items, list), "MENU_LIST_INVALID")
    result = []
    for item in items:
        require(isinstance(item, dict), "MENU_ITEM_INVALID")
        name = item.get("name") if app else item.get("menu_display") or item.get("menu_spoken") or item.get("name")
        amount = item.get("displayed_item_amount_krw") if app else item.get("stated_line_amount_krw")
        options = item.get("options") or []
        require(isinstance(options, list), "MENU_OPTIONS_INVALID")
        result.append({
            "name": text_value(name, "MENU_NAME_INVALID"),
            "quantity": quantity(item.get("quantity")),
            "unitPriceKrw": number(item.get("unit_price_krw"), "MENU_UNIT_PRICE_INVALID", optional=True),
            "displayedItemAmountKrw": number(amount, "MENU_DISPLAYED_AMOUNT_INVALID", optional=True),
            "amountBasis": "AppDisplayedItemAmountNotInferredUnitPrice" if app else "UserStatedLineAmountNotInferredUnitPrice" if amount is not None else "Unknown",
            "options": [text_value(option, "MENU_OPTION_INVALID") for option in options],
        })
    return result


def menu_summary(row, items):
    # 같은 주문의 구조화된 메뉴와 요약을 중복하지 않고 방문 메뉴는 옮기지 않는다.
    if items:
        return []
    summaries = []
    for item in row.get("menus") or []:
        if isinstance(item, dict):
            name = text_value(item.get("name"), "MENU_SUMMARY_INVALID")
            count = quantity(item.get("quantity"))
            summaries.append(name if count is None else name + " × " + str(count))
        else:
            summaries.append(text_value(item, "MENU_SUMMARY_INVALID"))
    if not summaries and row.get("menu_summary"):
        summaries.append(text_value(row["menu_summary"], "MENU_SUMMARY_INVALID"))
    return summaries


def fee_components(items, flat=False):
    require(isinstance(items, list), "FEE_COMPONENT_LIST_INVALID")
    result = []
    for index, item in enumerate(items):
        require(isinstance(item, dict), "FEE_COMPONENT_INVALID")
        result.append({
            "code": "AppDisplayedFeeLine" + str(index + 1).zfill(3) if flat else text_value(item.get("code"), "FEE_COMPONENT_CODE_INVALID"),
            "label": text_value(item.get("label"), "FEE_COMPONENT_LABEL_INVALID"),
            "amountKrw": number(item.get("amount_krw"), "FEE_COMPONENT_AMOUNT_INVALID", optional=True),
            "children": [] if flat else fee_components(item.get("children") or []),
        })
    return result


def import_dataset(source_root, output, overrides_path=None):
    output = local_path(output)
    require(output.suffix.lower() == ".json" and output.name != "import-proof.json", "OUTPUT_NAME_INVALID")
    proof_path = local_path(output.with_name("import-proof.json"))
    sources = Sources(Path(source_root).resolve())
    ledger, ledger_path, ledger_sha = sources.read(LEDGER)
    require((ledger.get("coverageStart"), ledger.get("coverageEnd")) == (START, END), "LEDGER_COVERAGE_INVALID")
    require(ledger.get("deliveryCount") == EXPECTED_COUNT and ledger.get("pickupSceneCount") == 510,
            "LEDGER_COUNTS_INVALID")

    overrides, override_ref = {}, None
    if overrides_path:
        path = local_path(overrides_path)
        name, sha, payload = sources.register(path, project=True)
        value = json.loads(payload.decode("utf-8-sig"))
        require(set(value) == {"recordBranchOverrides"} and isinstance(value["recordBranchOverrides"], dict),
                "REVIEWED_OVERRIDES_INVALID")
        overrides = value["recordBranchOverrides"]
        override_ref = sources.ref(name, "recordBranchOverrides", "UserConfirmedSharedPickupDistinctStore")

    plans, records = {}, {}
    for path in sorted((sources.root / LEDGER).parent.glob("*/plan.json")):
        relative = path.relative_to(sources.root).as_posix()
        plan, _, _ = sources.read(relative)
        plans[path.parent.name] = (plan, relative)
        intake, _, _ = sources.read(path.with_name("edit-intake.json").relative_to(sources.root).as_posix())
        metrics = intake.get("deliveryMetrics") or {}
        require(intake.get("inputCompleteConfirmed") is True and metrics.get("inputCompleteConfirmed") is True
                and metrics.get("mappingConfirmed") is True, "PLAN_INTAKE_NOT_CONFIRMED")
        for index, row in enumerate(plan.get("deliveryRecords") or []):
            identity = row.get("record_id")
            require(isinstance(identity, str) and identity not in records, "PLAN_RECORD_ID_DUPLICATE")
            records[identity] = (plan, relative, index, row)
    require(len(plans) == 14 and len(records) == EXPECTED_COUNT, "PLAN_COUNTS_INVALID")
    require(set(overrides).issubset(records), "OVERRIDE_RECORD_NOT_FOUND")

    legacy, intake_files = {}, 0
    for path in sorted((sources.root / "delivery-records").glob("intake-*/dictation-draft.json")):
        raw = json.loads(path.read_text(encoding="utf-8-sig"))
        if not (START <= (raw.get("confirmed_service_date") or "") <= END):
            continue
        data, relative, _ = sources.read(path.relative_to(sources.root).as_posix())
        intake_files += 1
        for index, row in enumerate(data.get("rows") or []):
            identity = row.get("record_id")
            require(identity in records and identity not in legacy, "INTAKE_RECORD_RELATION_INVALID")
            legacy[identity] = (row, relative, index, row.get("confirmed_service_date") or data["confirmed_service_date"])
    require(intake_files == 16 and len(legacy) == 533, "INTAKE_COUNTS_INVALID")

    db_path = sources.root / "delivery-records/delivery-app-observations.sqlite3"
    require(not db_path.with_name(db_path.name + "-wal").exists()
            or db_path.with_name(db_path.name + "-wal").stat().st_size == 0, "APP_DATABASE_WAL_PENDING")
    db_relative, _, _ = sources.register(db_path)
    with sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True) as connection:
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA query_only=ON")
        apps = {row["id"]: dict(row) for row in connection.execute(
            "SELECT id,service_date,delivery_fee,distance_km,order_complete,order_json,detail_complete,detail_json,food_order_amount_krw "
            "FROM app_deliveries WHERE service_date=?", (END,))}
    require(set(records) - set(legacy) == set(apps) and len(apps) == 41, "APP_DIRECT_ID_RELATION_INVALID")

    breakdown, fee_path, _ = sources.read("delivery-records/intake-20261001-001/fee-breakdowns.json")
    fees = {row["record_id"]: (row, index) for index, row in enumerate(breakdown.get("records") or [])}
    require(len(fees) == 40 and len(breakdown["records"]) == 40 and set(fees).issubset(records),
            "FEE_BREAKDOWN_COUNTS_INVALID")

    branches, result, seen, sequence, day_totals = {}, [], set(), collections.Counter(), {}
    stats = collections.Counter()
    last_day = START
    for event_index, event in enumerate(ledger["events"]):
        day = event["day"]
        require(START <= day <= END and day >= last_day and date.fromisoformat(day).isoformat() == day,
                "EVENT_DATE_INVALID")
        last_day = day
        old_path = Path(event["planSource"])
        old_relative = old_path.resolve().relative_to(sources.root).as_posix()
        old_plan, _, old_sha = sources.read(old_relative)
        require(old_sha == event["planSha256"], "LEDGER_PLAN_SHA_MISMATCH")
        plan, plan_path = plans[old_path.parent.name]
        require(plan == old_plan, "LATEST_PLAN_SOURCE_MISMATCH")
        event_rows = []
        for range_index, group in enumerate(event["ranges"]):
            require(group["count"] == len(group["recordIds"]), "BRANCH_RANGE_COUNT_INVALID")
            for identity in group["recordIds"]:
                require(identity in records and identity not in seen, "LEDGER_RECORD_RELATION_INVALID")
                record_plan, record_path, record_index, record = records[identity]
                require(record_plan is plan, "EVENT_PLAN_RELATION_INVALID")
                event_rows.append((record_index, identity, record, range_index, group))
        require(len(event_rows) == event["count"], "PICKUP_RECORD_COUNT_INVALID")
        visits = {row[2]["visit"] for row in event_rows}
        require(len(visits) == 1, "PICKUP_VISIT_RELATION_INVALID")
        for record_index, identity, record, range_index, group in sorted(event_rows):
            seen.add(identity)
            visit = record["visit"]
            require(isinstance(visit, int) and 1 <= visit <= len(plan["stores"]), "PICKUP_VISIT_INVALID")
            store = plan["stores"][visit - 1]
            require(store.get("locationVerified") is True and store.get("eventType") != "personal_purchase",
                    "PUBLIC_PICKUP_NOT_CONFIRMED")
            require(record.get("distanceBasis") == "app_display", "DISTANCE_BASIS_NOT_CONFIRMED")
            branch_key = group["branchKey"]
            require(isinstance(branch_key, list) and len(branch_key) == 2, "BRANCH_KEY_INVALID")
            branch_key = [text_value(part, "BRANCH_KEY_INVALID") for part in branch_key]
            source_refs = [sources.ref(ledger_path, f"events[{event_index}].ranges[{range_index}]", "ConfirmedOrderToPickupAndBranchRelation"),
                           sources.ref(plan_path, f"deliveryRecords[{record_index}]", "ConfirmedDeliveryFeeAndAppDisplayedDistance"),
                           sources.ref(plan_path, f"stores[{visit - 1}]", "VerifiedPublicPickupPlaceNotCustomerLocation")]
            legacy_row, source_menu, summaries, source_amount = {}, [], [], None
            if identity in legacy:
                legacy_row, intake_path, intake_index, source_day = legacy[identity]
                require(source_day == day, "INTAKE_SERVICE_DATE_MISMATCH")
                require(legacy_row.get("delivery_fee_krw") == record["delivery_fee_krw"]
                        and Decimal(str(legacy_row.get("distance_km"))) == Decimal(str(record["distance_km"])),
                        "INTAKE_METRICS_MISMATCH")
                source_menu = menu_items(legacy_row.get("menu_items") or [])
                summaries = menu_summary(legacy_row, source_menu)
                source_amount = legacy_row.get("food_order_amount_krw", legacy_row.get("food_order_total_krw"))
                source_refs.append(sources.ref(intake_path, f"rows[{intake_index}]", "ExistingUserInputPreservedNoAppCandidateMerge"))
            else:
                app = apps[identity]
                require(app["service_date"] == day and app["delivery_fee"] == record["delivery_fee_krw"]
                        and Decimal(app["distance_km"]) == Decimal(str(record["distance_km"])), "APP_METRICS_MISMATCH")
                require(app["order_complete"] == 1 and app["detail_complete"] == 1, "APP_ORDER_NOT_COMPLETE")
                order = json.loads(app["order_json"])
                require(order.get("complete") is True, "APP_ORDER_NOT_COMPLETE")
                require(order.get("menu_amount_krw") == app["food_order_amount_krw"],
                        "APP_ORDER_AMOUNT_MISMATCH")
                source_menu = menu_items(order.get("menu_items") or [], app=True)
                source_amount = app["food_order_amount_krw"]
                source_refs.append(sources.ref(db_relative, "app_deliveries.order_json.menu_items", "ExactAppIdOnlyDisplayedAmountNotUnitPrice"))
                stats["directAppIdRecords"] += 1

            display = store["name"]
            if len(event["ranges"]) > 1:
                candidate = legacy_row.get("restaurant_display_name") or legacy_row.get("restaurant_candidate") or legacy_row.get("restaurant_spoken")
                display = candidate if isinstance(candidate, str) and re.sub(r"[^a-z0-9가-힣]", "", candidate.lower()) == branch_key[0] else branch_key[0]
            if identity in overrides:
                override = overrides[identity]
                require(set(override) == {"displayName", "branchKey", "basis"}
                        and override["basis"] == "UserConfirmedSharedPickupDistinctStore"
                        and isinstance(override["branchKey"], list) and len(override["branchKey"]) == 2
                        and override["branchKey"][1] == branch_key[1], "REVIEWED_OVERRIDE_INVALID")
                display = override["displayName"]
                branch_key = [text_value(part, "OVERRIDE_BRANCH_KEY_INVALID") for part in override["branchKey"]]
                source_refs.append(override_ref)
                stats["reviewedBranchOverrides"] += 1
            branch_id = replay_id("branch", branch_key)
            branches[branch_id] = {"branchId": branch_id, "displayName": text_value(display, "BRANCH_DISPLAY_INVALID"),
                                   "publicPickupAddress": text_value(store["address"], "PUBLIC_PICKUP_ADDRESS_INVALID")}

            amount = record.get("food_order_amount_krw", record.get("menu_amount_krw"))
            require(amount is None or source_amount is None or Decimal(str(amount)) == Decimal(str(source_amount)),
                    "ORDER_AMOUNT_CONFLICT_REVIEW_REQUIRED")
            if amount is None and source_amount is not None:
                amount = source_amount
                stats["orderAmountsSupplementedFromExistingInput"] += 1
            components = []
            if identity in fees:
                fee, fee_index = fees[identity]
                require(fee["delivery_fee_krw"] == record["delivery_fee_krw"]
                        and fee.get("reconciliation", {}).get("difference_from_recorded_fee_krw") == 0,
                        "FEE_BREAKDOWN_RECONCILIATION_INVALID")
                components = fee_components(fee["components"])
                source_refs.append(sources.ref(fee_path, f"records[{fee_index}].components", "UserReconciledHierarchyParentsAndChildrenNotAddedTogether"))
                stats["hierarchicalFeeRecords"] += 1
            elif identity in apps:
                components = fee_components(json.loads(apps[identity]["detail_json"]).get("fee_lines") or [], flat=True)
                source_refs.append(sources.ref(db_relative, "app_deliveries.detail_json.fee_lines", "AppDisplayedFlatOrderPreservedNoComponentSumInferred"))
                stats["flatDisplayedFeeRecords"] += 1
            sequence[day] += 1
            result.append({"recordId": replay_id("record", identity), "serviceDate": day, "sequence": sequence[day],
                           "branchId": branch_id, "pickupVisitId": replay_id("pickup", event["eventId"]),
                           "deliveryFeeKrw": number(record["delivery_fee_krw"], "DELIVERY_FEE_INVALID"),
                           "distanceKm": number(record["distance_km"], "DISTANCE_INVALID"),
                           "distanceBasis": "AppDisplayed", "foodOrderAmountKrw": number(amount, "ORDER_AMOUNT_INVALID", optional=True),
                           "actualCompletedAtUtc": None, "menuItems": source_menu, "menuSummary": summaries,
                           "feeComponents": components, "sourceRefs": source_refs})
            totals = day_totals.setdefault(day, {"serviceDate": day, "recordCount": 0, "deliveryFeeKrw": 0, "distanceKm": Decimal(0)})
            totals["recordCount"] += 1
            totals["deliveryFeeKrw"] += record["delivery_fee_krw"]
            totals["distanceKm"] += Decimal(str(record["distance_km"]))

    require(seen == set(records) and len({row["recordId"] for row in result}) == EXPECTED_COUNT,
            "EXPORTED_RECORD_COVERAGE_INVALID")
    require(sum(Decimal(str(row["deliveryFeeKrw"])) for row in result) == EXPECTED_FEE
            and sum(Decimal(str(row["distanceKm"])) for row in result) == EXPECTED_DISTANCE, "EXPORTED_TOTALS_INVALID")
    require(len({row["pickupVisitId"] for row in result}) == 510, "EXPORTED_PICKUP_COUNT_INVALID")
    # 이 자료 판본은 확인된 공동 픽업의 서로 다른 상호까지 분리해야 완료된다.
    require(len(branches) == 391, "BRANCH_IDENTITY_REVIEW_REQUIRED")
    comparisons = {row["day"]: row for row in ledger["dateComparisons"]}
    require(set(day_totals) == set(comparisons), "DAILY_COVERAGE_INVALID")
    for day, totals in day_totals.items():
        require((totals["recordCount"], totals["deliveryFeeKrw"]) ==
                (comparisons[day]["deliveryCount"], comparisons[day]["feeKrw"]), "DAILY_TOTALS_INVALID")
        totals["distanceKm"] = number(totals["distanceKm"], "DAILY_DISTANCE_INVALID")
    dataset = {"schemaVersion": 1, "localVerificationOnly": True, "driverId": "replay-driver",
               "sourceLedgerSha256": ledger_sha, "coverageStart": START, "coverageEnd": END,
               "branches": sorted(branches.values(), key=lambda row: row["branchId"]), "records": result}
    payload = (json.dumps(dataset, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    encoded = json.loads(payload, parse_float=Decimal)
    require(sum(Decimal(str(row["distanceKm"])) for row in encoded["records"]) == EXPECTED_DISTANCE,
            "JSON_DISTANCE_PRECISION_LOST")
    sources.verify_unchanged()
    proof = {"schemaVersion": 1, "localVerificationOnly": True, "deterministicWithoutImportClock": True,
             "datasetSha256": digest(payload), "sourceLedgerSha256": ledger_sha,
             "recordCount": len(result), "pickupVisitCount": 510, "branchCount": len(branches),
             "deliveryFeeKrw": int(EXPECTED_FEE), "distanceKm": float(EXPECTED_DISTANCE),
             "legacyIntakeFileCount": intake_files, "legacyRecordCount": len(legacy), **dict(sorted(stats.items())),
             "recordsWithMenuItems": sum(bool(row["menuItems"]) for row in result),
             "recordsWithMenuSummaryOnly": sum(bool(row["menuSummary"]) for row in result),
             "recordsWithKnownOrderAmount": sum(row["foodOrderAmountKrw"] is not None for row in result),
             "missing": {"actualCompletedAtUtc": len(result),
                         "foodOrderAmountKrw": sum(row["foodOrderAmountKrw"] is None for row in result),
                         "menuItems": sum(not row["menuItems"] for row in result),
                         "unitPriceKrw": sum(item["unitPriceKrw"] is None for row in result for item in row["menuItems"]),
                         "menuQuantity": sum(item["quantity"] is None for row in result for item in row["menuItems"]),
                         "feeComponents": sum(not row["feeComponents"] for row in result)},
             "dailyTotals": [day_totals[day] for day in sorted(day_totals)],
             "sourceHashes": [{"relativePath": name, "sha256": sources.hashes[name]} for name in sorted(sources.hashes)],
             "whitelist": {"dataset": list(dataset), "branch": ["branchId", "displayName", "publicPickupAddress"],
                           "record": list(result[0]), "menuItem": ["name", "quantity", "unitPriceKrw", "displayedItemAmountKrw", "amountBasis", "options"],
                           "feeComponent": ["code", "label", "amountKrw", "children"], "sourceRef": ["relativePath", "sha256", "field", "basis"]},
             "excluded": ["CustomerIdentity", "CustomerAddress", "PlatformOrderNumber", "RawScreen", "RawTranscript",
                          "AppCandidateLinks", "PickupVisitMenuAllocation", "InferredUnitPrice", "InferredActualCompletionTime", "MissionAndTaxAndPayout"],
             "feeComponentTotalsRecomputed": False, "sourceFilesUnchanged": True}
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    temporary.write_bytes(payload)
    temporary.replace(output)
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("IMPORTED records=" + str(len(result)) + " branches=" + str(len(branches)) + " pickupVisits=510 feeKrw=2255300 distanceKm=1115.8978")
    return dataset


def main():
    parser = argparse.ArgumentParser(description="Import verified local delivery history without modifying sources.")
    parser.add_argument("--source-root", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--reviewed-overrides")
    args = parser.parse_args()
    try:
        import_dataset(args.source_root, args.output, args.reviewed_overrides)
    except ImportFailure as error:
        print("IMPORT_FAILED " + str(error), file=sys.stderr)
        return 1
    except (OSError, ValueError, KeyError, TypeError, sqlite3.Error, subprocess.SubprocessError):
        print("IMPORT_FAILED SOURCE_OR_ENVIRONMENT_INVALID", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
