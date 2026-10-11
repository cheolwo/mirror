"""공개 픽업 관측과 확인된 건별 메뉴로 비공개 지역 배차 시험 자료를 만든다.

고객 위치와 기사 GPS는 읽지 않는다. 실제 배달 거리로 전달점을 역산하지 않으며
방문 요약 메뉴를 단건에 배분하지 않는다. 네트워크·운영 DB·모델 호출은 없다.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import math
import re
import stat
import subprocess
import sys
from decimal import Decimal, InvalidOperation
from datetime import date
from pathlib import Path


REPO = Path(__file__).resolve().parents[3]
LOCAL = REPO / "artifacts/local"
OUTPUT_ROOT = LOCAL / "local-dispatch-ai/regional-management-r50/datasets"
POLICY = "regional-management-r50.dataset.r1"
DESTINATION_POLICY = "sha256-record-id-public-store-offset.r1"
TARGET_DISTRICTS = ("중랑구", "동대문구", "광진구")
KNOWN_DISTRICTS = TARGET_DISTRICTS + ("성북구", "노원구", "성동구", "중구", "강북구", "도봉구", "강동구", "송파구")
SHA = re.compile(r"[0-9a-f]{64}\Z")
ID = re.compile(r"replay-(record|branch|pickup)-[0-9a-f]{64}\Z")
STORE_FIELD = re.compile(r"stores\[(\d+)\]\Z")
PLAN_PATH = re.compile(r"edit-kit/branch-deliveries-all-20261010/2026-\d{2}-\d{2}(?:_2026-\d{2}-\d{2})?/plan\.json\Z")
SENSITIVE = re.compile(r"(?:01[016789][- ]?\d{3,4}[- ]?\d{4}|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|고객\s*(?:성명|이름|전화|연락처)|전달지\s*상세)")


class PreparationFailure(Exception):
    """원문 값과 개인 경로 대신 실패 코드만 보고한다."""


def require(condition, code):
    if not condition:
        raise PreparationFailure(code)


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + "\n").encode("utf-8")


def sha(payload):
    return hashlib.sha256(payload).hexdigest()


def safe_path(path, boundary):
    path, boundary = Path(path).absolute(), Path(boundary).absolute()
    require(path.is_relative_to(boundary), "PATH_OUTSIDE_BOUNDARY")
    # resolve 전에도 junction/reparse point를 검사한다.
    for part in [path, *path.parents]:
        if not part.is_relative_to(boundary):
            break
        if part.exists():
            attributes = part.lstat()
            require(not stat.S_ISLNK(attributes.st_mode)
                    and not (getattr(attributes, "st_file_attributes", 0) & 0x400), "LINK_PATH_FORBIDDEN")
    result = path.resolve()
    require(result.is_relative_to(boundary.resolve()), "PATH_OUTSIDE_BOUNDARY")
    return result


def safe_text(value, code, optional=False):
    if value is None and optional:
        return None
    require(isinstance(value, str) and 0 < len(value) <= 1000
            and not SENSITIVE.search(value) and not re.search(r"[\x00-\x1f\x7f]", value), code)
    return value


def number(value, code, optional=False):
    if value is None and optional:
        return None
    require(value is not None and not isinstance(value, bool), code)
    try:
        parsed = Decimal(str(value))
    except (InvalidOperation, ValueError):
        raise PreparationFailure(code) from None
    require(parsed.is_finite() and parsed >= 0, code)
    return int(parsed) if parsed == parsed.to_integral_value() else float(parsed)


def stable_id(value, kind):
    require(isinstance(value, str) and ID.fullmatch(value)
            and value.startswith("replay-" + kind + "-"), "SOURCE_ID_INVALID")
    return value


def district(address):
    found = [name for name in KNOWN_DISTRICTS if re.search(r"(?:^|\s)" + name + r"(?:\s|$)", address)]
    require(len(found) <= 1, "DISTRICT_AMBIGUOUS")
    return found[0] if found else None


def safe_ref(row):
    require(isinstance(row, dict), "SOURCE_REF_INVALID")
    relative = safe_text(row.get("relativePath"), "SOURCE_REF_INVALID")
    require(not Path(relative).is_absolute() and ".." not in Path(relative).parts
            and ":" not in relative and "\\" not in relative, "SOURCE_REF_INVALID")
    fingerprint = row.get("sha256")
    require(isinstance(fingerprint, str) and SHA.fullmatch(fingerprint), "SOURCE_SHA_INVALID")
    return {"relativePath": relative, "sha256": fingerprint,
            "field": safe_text(row.get("field"), "SOURCE_REF_INVALID"),
            "basis": safe_text(row.get("basis"), "SOURCE_REF_INVALID")}


def menus(items):
    require(isinstance(items, list), "MENU_LIST_INVALID")
    result = []
    for item in items:
        require(isinstance(item, dict), "MENU_ITEM_INVALID")
        quantity = item.get("quantity")
        require(quantity is None or isinstance(quantity, int) and not isinstance(quantity, bool) and quantity > 0,
                "MENU_QUANTITY_INVALID")
        options = item.get("options", [])
        require(isinstance(options, list), "MENU_OPTIONS_INVALID")
        result.append({"name": safe_text(item.get("name"), "MENU_NAME_INVALID"), "quantity": quantity,
                       "unitPriceKrw": number(item.get("unitPriceKrw"), "MENU_PRICE_INVALID", True),
                       "displayedItemAmountKrw": number(item.get("displayedItemAmountKrw"), "MENU_PRICE_INVALID", True),
                       "amountBasis": safe_text(item.get("amountBasis"), "MENU_AMOUNT_BASIS_INVALID"),
                       "options": [safe_text(option, "MENU_OPTION_INVALID") for option in options]})
    return result


class Sources:
    def __init__(self):
        self.files = {}

    def read_json(self, path, boundary, relative, expected=None):
        path = safe_path(path, boundary)
        require(path.is_file(), "SOURCE_FILE_MISSING")
        payload = path.read_bytes()
        fingerprint = sha(payload)
        require(expected is None or fingerprint == expected, "SOURCE_HASH_MISMATCH")
        require(path not in self.files or self.files[path][1] == fingerprint, "SOURCE_CHANGED_DURING_PREPARATION")
        self.files[path] = (relative, fingerprint)
        try:
            data = json.loads(payload.decode("utf-8-sig"))
        except (UnicodeError, ValueError):
            raise PreparationFailure("SOURCE_JSON_INVALID") from None
        require(isinstance(data, dict), "SOURCE_JSON_INVALID")
        return data, fingerprint

    def verify(self):
        require(all(path.is_file() and sha(path.read_bytes()) == details[1]
                    for path, details in self.files.items()), "SOURCE_CHANGED_DURING_PREPARATION")


def synthetic_destination(record_id, latitude, longitude):
    seed = hashlib.sha256(record_id.encode("ascii")).digest()
    east = 500 + int.from_bytes(seed[:2], "big") % 2001
    north = 300 + int.from_bytes(seed[2:4], "big") % 1701
    east *= -1 if seed[4] & 1 else 1
    north *= -1 if seed[5] & 1 else 1
    return {"latitude": round(latitude + north / 111320.0, 7),
            "longitude": round(longitude + east / (111320.0 * math.cos(math.radians(latitude))), 7),
            "basis": "SyntheticOffsetFromPublicStore", "notActualCustomerLocation": True,
            "generationPolicy": DESTINATION_POLICY}


def build_dataset(dataset_path, source_root, enforce_baseline=True):
    dataset_path = safe_path(dataset_path, LOCAL)
    source_root = Path(source_root).absolute()
    require(source_root.is_dir(), "SOURCE_ROOT_MISSING")
    safe_path(source_root, source_root)
    sources = Sources()
    original, dataset_sha = sources.read_json(dataset_path, LOCAL, dataset_path.relative_to(REPO).as_posix())
    require(original.get("schemaVersion") == 1 and original.get("localVerificationOnly") is True
            and original.get("driverId") == "replay-driver", "SOURCE_NOT_LOCAL_REPLAY")
    require(SHA.fullmatch(str(original.get("sourceLedgerSha256", ""))), "SOURCE_LEDGER_SHA_INVALID")
    branches, input_records = original.get("branches"), original.get("records")
    require(isinstance(branches, list) and isinstance(input_records, list), "SOURCE_COLLECTION_INVALID")
    branch_index = {}
    for branch in branches:
        require(isinstance(branch, dict), "BRANCH_INVALID")
        bid = stable_id(branch.get("branchId"), "branch")
        require(bid not in branch_index, "BRANCH_ID_DUPLICATE")
        branch_index[bid] = {"displayName": safe_text(branch.get("displayName"), "STORE_NAME_INVALID"),
                             "address": safe_text(branch.get("publicPickupAddress"), "PUBLIC_PICKUP_ADDRESS_INVALID")}
    seen, selected, excluded, map_cache = set(), [], collections.Counter(), {}
    summary_only, matched_missing, branch_address_variants = 0, 0, 0
    for row in input_records:
        require(isinstance(row, dict), "RECORD_INVALID")
        rid, bid = stable_id(row.get("recordId"), "record"), stable_id(row.get("branchId"), "branch")
        require(rid not in seen and bid in branch_index, "RECORD_RELATION_INVALID")
        seen.add(rid)
        branch = branch_index[bid]
        area = district(branch["address"])
        if area not in TARGET_DISTRICTS:
            excluded["DistrictNotExplicit" if area is None else "OutsideTargetDistricts"] += 1
            continue
        require(isinstance(row.get("sourceRefs"), list), "SOURCE_REFS_INVALID")
        refs = [safe_ref(ref) for ref in row["sourceRefs"]]
        pickup_refs = [ref for ref in refs if ref["basis"] == "VerifiedPublicPickupPlaceNotCustomerLocation"]
        require(len(pickup_refs) == 1, "PICKUP_REF_COUNT_INVALID")
        ref = pickup_refs[0]
        require(PLAN_PATH.fullmatch(ref["relativePath"]), "PICKUP_PLAN_PATH_INVALID")
        field = STORE_FIELD.fullmatch(ref["field"])
        require(field is not None, "PICKUP_FIELD_INVALID")
        index = int(field.group(1))
        if ref["relativePath"] not in map_cache:
            plan_path = source_root / ref["relativePath"]
            plan, _ = sources.read_json(plan_path, source_root, ref["relativePath"], ref["sha256"])
            map_relative = str(Path(ref["relativePath"]).with_name("route-map.json")).replace("\\", "/")
            mapping, map_sha = sources.read_json(source_root / map_relative, source_root, map_relative)
            require(isinstance(plan.get("stores"), list) and isinstance(mapping.get("points"), list)
                    and len(plan["stores"]) == len(mapping["points"]), "PICKUP_MAP_LENGTH_MISMATCH")
            map_cache[ref["relativePath"]] = (plan["stores"], mapping["points"], map_relative, map_sha)
        stores, points, map_relative, map_sha = map_cache[ref["relativePath"]]
        # 같은 계획의 후속 참조에서도 다른 SHA를 받아들이지 않는다.
        require(sources.files[(source_root / ref["relativePath"]).resolve()][1] == ref["sha256"], "SOURCE_HASH_MISMATCH")
        require(0 <= index < len(stores), "PICKUP_INDEX_INVALID")
        store, point = stores[index], points[index]
        require(isinstance(store, dict) and isinstance(point, dict), "PICKUP_POINT_INVALID")
        require(store.get("locationVerified") is True and store.get("eventType") != "personal_purchase", "PICKUP_NOT_VERIFIED")
        require(store.get("name") == point.get("name") and store.get("address") == point.get("address"),
                "PICKUP_IDENTITY_MISMATCH")
        # r54의 지점 대표 주소와 날짜별 관측 주소는 도로명/지번·층호·표기가 다를 수 있다.
        # 이미 hash로 확인된 record→stores[i] 관계를 재사용하며 새 지점 병합은 하지 않는다.
        store_address = safe_text(store.get("address"), "PUBLIC_PICKUP_ADDRESS_INVALID")
        store_area = district(store_address)
        require(store_area is None or store_area == area, "PICKUP_DISTRICT_CONFLICT")
        branch_address_variants += store_address != branch["address"]
        latitude, longitude = number(point.get("lat"), "PICKUP_COORDINATE_INVALID"), number(point.get("lon"), "PICKUP_COORDINATE_INVALID")
        require(33 <= latitude <= 39 and 124 <= longitude <= 132, "PICKUP_COORDINATE_INVALID")
        matched = point.get("matchedAddress")
        require(matched is None or isinstance(matched, str), "MATCHED_ADDRESS_INVALID")
        matched_area = district(matched or "")
        require(matched_area is None or matched_area == area, "PICKUP_DISTRICT_CONFLICT")
        matched_missing += matched_area is None
        menu_items = menus(row.get("menuItems"))
        # 방문 요약은 내용을 읽거나 단건 메뉴로 복제하지 않는다.
        summary_only += not menu_items and bool(row.get("menuSummary"))
        require(row.get("distanceBasis") == "AppDisplayed", "HISTORICAL_DISTANCE_BASIS_INVALID")
        service_date = row.get("serviceDate")
        require(isinstance(service_date, str) and re.fullmatch(r"2026-\d{2}-\d{2}", service_date), "SERVICE_DATE_INVALID")
        try:
            require(date.fromisoformat(service_date).isoformat() == service_date, "SERVICE_DATE_INVALID")
        except ValueError:
            raise PreparationFailure("SERVICE_DATE_INVALID") from None
        sequence = row.get("sequence")
        require(isinstance(sequence, int) and not isinstance(sequence, bool) and sequence > 0, "SEQUENCE_INVALID")
        selected.append({"recordId": rid, "branchId": bid, "pickupVisitId": stable_id(row.get("pickupVisitId"), "pickup"),
                         "serviceDate": service_date, "sequence": sequence, "district": area,
                         "publicStoreName": branch["displayName"], "publicPickupAddress": branch["address"],
                         "pickup": {"latitude": latitude, "longitude": longitude, "basis": "VerifiedPublicStoreObservation"},
                         "historical": {"deliveryFeeKrw": number(row.get("deliveryFeeKrw"), "HISTORICAL_FEE_INVALID"),
                                        "appDisplayedDistanceKm": number(row.get("distanceKm"), "HISTORICAL_DISTANCE_INVALID"),
                                        "menuItems": menu_items,
                                        "orderAmountKrw": number(row.get("foodOrderAmountKrw"), "HISTORICAL_ORDER_AMOUNT_INVALID", True)},
                         "sourceRefs": refs + [{"relativePath": map_relative, "sha256": map_sha,
                                                  "field": f"points[{index}]", "basis": "VerifiedPublicStoreCoordinateObservation"}],
                         "syntheticDestination": synthetic_destination(rid, latitude, longitude)})
    selected.sort(key=lambda row: (row["serviceDate"], row["sequence"], row["recordId"]))
    counts = {"sourceRecords": len(input_records), "sourceBranches": len(branches), "selectedRecords": len(selected),
              "selectedBranches": len({row["branchId"] for row in selected}),
              "recordsWithMenuItems": sum(bool(row["historical"]["menuItems"]) for row in selected),
              "menuItems": sum(len(row["historical"]["menuItems"]) for row in selected),
              "knownOrderAmounts": sum(row["historical"]["orderAmountKrw"] is not None for row in selected),
              "visitSummaryOnlyNotAllocated": summary_only, "matchedAddressDistrictMissing": matched_missing,
              "canonicalBranchAddressDiffersFromPickupObservation": branch_address_variants}
    if enforce_baseline:
        require(tuple(counts[key] for key in ("sourceRecords", "sourceBranches", "selectedRecords", "selectedBranches",
                                            "recordsWithMenuItems", "menuItems", "knownOrderAmounts", "visitSummaryOnlyNotAllocated"))
                == (574, 391, 504, 339, 267, 488, 296, 7), "APPROVED_COVERAGE_MISMATCH")
    sources.verify()
    source_fingerprints = [{"relativePath": value[0], "sha256": value[1]}
                           for value in sorted(sources.files.values(), key=lambda pair: pair[0])]
    tool_sha = sha(Path(__file__).read_bytes())
    fingerprint = sha(canonical({"sourceFingerprints": source_fingerprints, "sourceLedgerSha256": original["sourceLedgerSha256"],
                                 "toolSha256": tool_sha, "policy": POLICY, "syntheticDestinationPolicy": DESTINATION_POLICY}))
    metadata = {"schemaVersion": 1, "policyRevision": POLICY, "localVerificationOnly": True,
                "executionModeCode": "Simulation", "sourceFingerprint": fingerprint,
                "sourceDatasetSha256": dataset_sha, "sourceLedgerSha256": original["sourceLedgerSha256"],
                "toolSha256": tool_sha, "sourceFingerprints": source_fingerprints,
                "realCustomerLocationsUsed": 0, "realDriverLocationsUsed": 0,
                "historicalValuesAreCurrentPrices": False, "visitMenuSummariesAllocated": False,
                "branchAddressBasis": "ExistingReplayBranchIdentitySeparateFromPerVisitCoordinateObservation",
                "syntheticDestinationPolicy": DESTINATION_POLICY, "currentAdministrativeBoundaryClaimed": False}
    dataset = {"metadata": metadata, "records": selected}
    coordinate_variants = collections.defaultdict(set)
    for row in selected:
        coordinate_variants[row["branchId"]].add((row["pickup"]["latitude"], row["pickup"]["longitude"]))
    summary = {"metadata": metadata, "counts": counts, "excludedRecordsByReason": dict(sorted(excluded.items())),
               "districts": [{"district": area, "records": sum(row["district"] == area for row in selected),
                              "branches": len({row["branchId"] for row in selected if row["district"] == area})}
                             for area in TARGET_DISTRICTS],
               "branchesWithCoordinateVariants": sum(len(variants) > 1 for variants in coordinate_variants.values()),
               "perRecordCoordinateObservationsPreserved": True, "sourceFilesUnchanged": True,
               "datasetSha256": sha(canonical(dataset)), "modelCalls": 0, "operationalWrites": 0,
               "excludedFields": ["CustomerIdentity", "CustomerAddress", "PlatformOrderId", "RealDriverGps", "RawUi", "RawTranscript",
                                  "VisitMenuSummaryAllocation", "InferredMenuWeight", "InferredPreparationTime", "ImageAssets"]}
    return dataset, summary


def write_dataset(dataset, summary):
    fingerprint = dataset["metadata"]["sourceFingerprint"]
    require(SHA.fullmatch(fingerprint), "OUTPUT_FINGERPRINT_INVALID")
    output = safe_path(OUTPUT_ROOT / fingerprint, OUTPUT_ROOT)
    relative = output.relative_to(REPO).as_posix()
    checked = subprocess.run(["git", "check-ignore", "-q", "--", relative], cwd=REPO,
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    require(checked.returncode == 0, "OUTPUT_NOT_GIT_IGNORED")
    output.mkdir(parents=True, exist_ok=True)
    for name, value in (("dataset.json", dataset), ("summary.json", summary)):
        path, payload = safe_path(output / name, OUTPUT_ROOT), canonical(value)
        if path.exists():
            require(path.is_file() and path.read_bytes() == payload, "EXISTING_OUTPUT_DIFFERENT")
            continue
        # 같은 fingerprint의 병렬 실행도 기존 바이트를 덮어쓰지 않는다.
        try:
            with path.open("xb") as stream:
                stream.write(payload)
        except FileExistsError:
            require(path.read_bytes() == payload, "EXISTING_OUTPUT_DIFFERENT")
    return output


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", required=True, type=Path)
    parser.add_argument("--source-root", required=True, type=Path)
    args = parser.parse_args(argv)
    try:
        dataset, summary = build_dataset(args.dataset, args.source_root)
        output = write_dataset(dataset, summary)
        print("PREPARED fingerprint=" + output.name + " records=504 branches=339 menuItems=488 modelCalls=0")
        return 0
    except (PreparationFailure, OSError, ValueError) as error:
        print("FAILED " + (str(error) if isinstance(error, PreparationFailure) else "LOCAL_IO_OR_VALUE_FAILURE"), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
