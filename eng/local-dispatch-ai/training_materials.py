"""정책이 만든 배차 학습 후보를 검증한다. 승인·학습·모델 호출 기능은 없다.

실제 주문은 messages 밖의 출처 참조로만 사용한다. 정답은 현재 정책의 초안이며,
기하 근사와 합성 업무 조건의 한계를 보존한다. 동일 입력은 전부 격리한다.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile


REPO = Path(__file__).resolve().parents[2]
LOCAL = REPO / "artifacts/local"
ROOT = LOCAL / "local-dispatch-ai/training-materials-r51"
DATASET_ROOT = LOCAL / "local-dispatch-ai/regional-management-r50/datasets"
DELIVERY_ROOT = Path.home() / "Documents/ChatGPT/배달"
DESIGN_REF = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.implementation.r51.md"
WORK_ORDER_REF = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.r51.work-order.json"
EXPORTER_REF = "eng/local-dispatch-ai/RegionalScenarioRunner/prepare_regional_dataset.py"
LEDGER_REF = "edit-kit/branch-deliveries-all-20261010/delivery-count-ledger.json"
DESIGN_SHA = "4604b837968a7e303d3e43d916f28b7c8d520d5c75caf8d82d70947a3b8b44f8"
EXPECTED_DATASET_SHA = "6a9c5e01bcc9dd84e2f30353c6cc3d88d5d9aa9853eed441b5bca0158ffa8bff"
FORMAT = "dispatch-training-candidates/v1"
SPLIT_RULE = "sha256-branch-first8-u32-mod100-70-15-15"
SPLITS = ("train", "validation", "test")
SHA = re.compile(r"[0-9a-f]{64}\Z")
CASE = re.compile(r"r51-[0-9a-f]{24}\Z")
RECORD = re.compile(r"replay-record-[0-9a-f]{64}\Z")
BRANCH = re.compile(r"replay-branch-[0-9a-f]{64}\Z")
TOKEN = {kind: re.compile(kind + r"[1-9][0-9]{0,2}\Z") for kind in ("D", "O", "P")}
DISTRICTS = ("중랑구", "동대문구", "광진구")
POLICIES = {"DemandParticipation": "food-demand-participation/v1",
            "BundleSelection": "food-regional-dispatch-review/r50",
            "ArrivalAnchor": "food-arrival-anchor/r50"}
FAMILIES = {"DemandParticipation": ("shortage", "surplus", "long-wait", "stale", "missing", "no-eligible"),
            "BundleSelection": ("single", "two-order"), "ArrivalAnchor": ("valid", "late", "missingproof")}
REASONS = {"DemandSnapshotUnavailable", "DemandSnapshotStale", "DriverCapacityUnavailable", "DriverCapacityStale",
           "DemandAndCapacityObserved", "ExistingEligibilityNotMet", "ExistingActivityProtected", "LongWaitOpportunity",
           "OrderDelayCapacityException", "DemandCapacityAvailable", "ExistingCapacitySufficient"}
STATUSES = {"ActiveProtected", "AdmissionRecommended", "WaitingForDemand", "ExistingGateRequired", "Ineligible"}
SYSTEM = ("합성 배차 연습 자료입니다. 입력의 익명 수치와 코드만 사용해 JSON 정책 초안을 반환하세요. "
          "현재 정책의 초안은 최적해나 사람의 승인이 아니며 운영 배차를 실행하지 않습니다.")
TEACHER_FILES = (
    "Ssalddel.Domain/배차/음식배달수요참여Policy.cs", "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs",
    "Ssalddel/Services/Dispatch/AI/음식권역배차관리Service.cs", "Ssalddel/Services/Dispatch/AI/Ollama음식권역배차AI.cs",
    "Ssalddel/Services/Dispatch/AI/음식권역배차OllamaOptions.cs", "Ssalddel/Services/Dispatch/AI/FoodDispatchOllamaOptions.cs",
    "Ssalddel/Services/Dispatch/Recommendation/멀티배차조합Service.cs", "Ssalddel/Services/Dispatch/Recommendation/픽업하차경로최적화Service.cs",
    "Ssalddel/Services/Dispatch/Recommendation/음식권역복귀일정평가Service.cs", "Ssalddel/Services/Dispatch/Queue/QueueOptions.cs",
    "Ssalddel/Services/Food/음식주문현재조리Policy.cs", "Ssalddel/Services/Options/FoodDriverRoutePricingOptions.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달후보검토결과.cs", "Ssalddel/Services/Dispatch/Queue/음식배달수요참여Service.cs")
TEACHER_GLOBS = (
    ("eng/local-dispatch-ai/TrainingMaterialRunner", "*.cs"), ("eng/local-dispatch-ai/TrainingMaterialRunner", "*.csproj"),
    ("Ssalddel/Services/Dispatch/Queue", "음식배달배차업무정책*.cs"),
    ("Ssalddel.Tests/Services/Dispatch/Queue", "음식배달배차업무정책Tests*.cs"),
    ("Ssalddel/Services/Dispatch/Coordination", "*판단근거*.cs"),
    ("Ssalddel/Services/Dispatch/Coordination", "음식배달권정책.cs"),
    ("Ssalddel/Services/Dispatch/Coordination", "국내행정구역배달권Catalog*.cs"),
    ("Ssalddel/Services/DeliveryZones", "*.cs"))


class MaterialFailure(Exception):
    """오류에는 원문·개인정보를 싣지 않고 고정 코드만 반환한다."""


def require(condition, code):
    if not condition:
        raise MaterialFailure(code)


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (canonical(value) + "\n").encode("utf-8")


def reject_constant(_value):
    raise MaterialFailure("NONFINITE_JSON")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "DUPLICATE_JSON_KEY")
        result[key] = value
    return result


def parse_json(data):
    try:
        return json.loads(data, object_pairs_hook=unique_object, parse_constant=reject_constant)
    except (ValueError, UnicodeError, RecursionError):
        raise MaterialFailure("INVALID_JSON") from None


def exact(value, keys, code):
    require(type(value) is dict and set(value) == set(keys.split()), code)


def boolean(value, code):
    require(type(value) is bool, code)


def number(value, code, nullable=False, integer=False, minimum=0, maximum=100000):
    if value is None and nullable:
        return
    require(type(value) in ((int,) if integer else (int, float)), code)
    require(minimum <= value <= maximum and math.isfinite(value), code)


def choice(value, allowed, code):
    require(type(value) is str and value in allowed, code)


def hash_value(value, code):
    require(type(value) is str and SHA.fullmatch(value), code)


def token(value, kind):
    require(type(value) is str and TOKEN[kind].fullmatch(value), "ANONYMOUS_TOKEN_INVALID")


def tokens(values, kind, maximum=100):
    require(type(values) is list and len(values) <= maximum, "TOKEN_LIST_INVALID")
    for value in values:
        token(value, kind)
    require(len(set(values)) == len(values), "TOKEN_DUPLICATED")


def safe_path(path, boundary):
    path, boundary = Path(path).absolute(), Path(boundary).absolute()
    require(path.is_relative_to(boundary), "PATH_OUTSIDE_BOUNDARY")
    for part in (path, *path.parents):
        if not part.is_relative_to(boundary):
            break
        if part.exists() or part.is_symlink():
            info = part.lstat()
            require(not stat.S_ISLNK(info.st_mode) and not (getattr(info, "st_file_attributes", 0) & 0x400),
                    "LINK_PATH_FORBIDDEN")
    require(path.resolve().is_relative_to(boundary.resolve()), "PATH_OUTSIDE_BOUNDARY")
    return path.resolve()


def branch_split(branch_id):
    bucket = int(digest(branch_id.encode("utf-8"))[:8], 16) % 100
    return "train" if bucket < 70 else "validation" if bucket < 85 else "test"


def case_id(source_records, task, family):
    return "r51-" + digest(("|".join(source_records) + "|" + task + "|" + family).encode("utf-8"))[:24]


def validate_demand(value):
    exact(value, "planningWindowMinutes hourlyTarget observationWindowMinutes demandObservationAgeMinutes waitingOrders recentOrders forecastOrders delayRisk noEligibleActiveDriver drivers", "DEMAND_INPUT_KEYS")
    number(value["planningWindowMinutes"], "PLANNING_WINDOW", minimum=0.001, maximum=120)
    require(type(value["hourlyTarget"]) in (int, float) and value["hourlyTarget"] == 3.5, "HOURLY_BASELINE_CHANGED")
    number(value["observationWindowMinutes"], "OBSERVATION_WINDOW", minimum=0.001, maximum=240)
    number(value["demandObservationAgeMinutes"], "DEMAND_AGE", nullable=True)
    for field in ("waitingOrders", "recentOrders"):
        number(value[field], "DEMAND_COUNT", nullable=True, integer=True)
    number(value["forecastOrders"], "FORECAST", nullable=True)
    for field in ("delayRisk", "noEligibleActiveDriver"):
        boolean(value[field], "DEMAND_BOOLEAN")
    require(type(value["drivers"]) is list and 1 <= len(value["drivers"]) <= 100, "DRIVERS_INVALID")
    ids = []
    for driver in value["drivers"]:
        exact(driver, "driverToken eligible active shiftAgeMinutes waitMinutes observationAgeMinutes remainingMinutes recentAssignments", "DRIVER_KEYS")
        token(driver["driverToken"], "D")
        ids.append(driver["driverToken"])
        for field in ("eligible", "active"):
            boolean(driver[field], "DRIVER_BOOLEAN")
        for field in ("shiftAgeMinutes", "waitMinutes"):
            number(driver[field], "DRIVER_MINUTES")
        for field in ("observationAgeMinutes", "remainingMinutes"):
            number(driver[field], "DRIVER_MINUTES", nullable=True)
        number(driver["recentAssignments"], "DRIVER_COUNT", integer=True)
    require(len(set(ids)) == len(ids), "DRIVER_TOKEN_DUPLICATED")
    return set(ids)


def validate_demand_target(value, driver_tokens):
    exact(value, "dataStateCode reasonCode targetActiveDrivers additionalParticipants driverDecisions", "DEMAND_TARGET_KEYS")
    choice(value["dataStateCode"], ("Current", "Unknown", "Stale"), "DATA_STATE_INVALID")
    choice(value["reasonCode"], REASONS, "REASON_INVALID")
    number(value["targetActiveDrivers"], "TARGET_COUNT", nullable=True, integer=True)
    number(value["additionalParticipants"], "ADDITIONAL_COUNT", integer=True)
    require(type(value["driverDecisions"]) is list and len(value["driverDecisions"]) == len(driver_tokens), "DRIVER_DECISIONS_INVALID")
    seen, priorities = set(), set()
    for decision in value["driverDecisions"]:
        exact(decision, "driverToken statusCode reasonCode allowProposal admitted priority", "DRIVER_TARGET_KEYS")
        token(decision["driverToken"], "D")
        require(decision["driverToken"] in driver_tokens and decision["driverToken"] not in seen, "TARGET_TOKEN_UNKNOWN")
        seen.add(decision["driverToken"])
        choice(decision["statusCode"], STATUSES, "STATUS_INVALID")
        choice(decision["reasonCode"], REASONS, "REASON_INVALID")
        for field in ("allowProposal", "admitted"):
            boolean(decision[field], "TARGET_BOOLEAN")
        number(decision["priority"], "PRIORITY_INVALID", integer=True, minimum=1, maximum=100)
        require(decision["priority"] not in priorities, "PRIORITY_DUPLICATED")
        priorities.add(decision["priority"])
    require(priorities == set(range(1, len(driver_tokens) + 1)), "PRIORITY_SEQUENCE_INVALID")


def validate_bundle(value, target):
    exact(value, "demand orders plans limits", "BUNDLE_INPUT_KEYS")
    driver_tokens = validate_demand(value["demand"])
    require(type(value["orders"]) is list and 1 <= len(value["orders"]) <= 2, "BUNDLE_ORDERS_INVALID")
    order_tokens, ordinals = set(), set()
    for order in value["orders"]:
        exact(order, "orderToken requestOrdinal", "ORDER_KEYS")
        token(order["orderToken"], "O")
        number(order["requestOrdinal"], "REQUEST_ORDINAL_INVALID", integer=True, minimum=1, maximum=2)
        require(order["orderToken"] not in order_tokens and order["requestOrdinal"] not in ordinals,
                "REQUEST_ORDER_DUPLICATED")
        order_tokens.add(order["orderToken"])
        ordinals.add(order["requestOrdinal"])
    require(ordinals == set(range(1, len(order_tokens) + 1)), "REQUEST_ORDINAL_SEQUENCE_INVALID")
    exact(value["limits"], "maximumBundleSize maximumPickupSeparationKm maximumDropoffSeparationKm maximumApproximateRouteKm recommendedPickupSeparationKm recommendedDropoffSeparationKm recommendedPickupWindowGapMinutes", "BUNDLE_LIMIT_KEYS")
    require(type(value["limits"]["maximumBundleSize"]) is int and value["limits"]["maximumBundleSize"] == 2, "BUNDLE_SIZE_CHANGED")
    for key, val in value["limits"].items():
        if key != "maximumBundleSize":
            number(val, "BUNDLE_LIMIT_INVALID")
    require(type(value["plans"]) is list and 1 <= len(value["plans"]) <= 50, "PLANS_INVALID")
    plan_tokens = set()
    used_orders = set()
    for plan in value["plans"]:
        exact(plan, "planToken orderTokens orderCount pickupSeparationKm dropoffSeparationKm routeDistanceKm samePickup samePickupRegion sameDropoffRegion regionAdjacent pickupWindowGapMinutes", "PLAN_KEYS")
        token(plan["planToken"], "P")
        require(plan["planToken"] not in plan_tokens, "PLAN_TOKEN_DUPLICATED")
        plan_tokens.add(plan["planToken"])
        tokens(plan["orderTokens"], "O", maximum=2)
        require(set(plan["orderTokens"]).issubset(order_tokens), "PLAN_ORDER_UNKNOWN")
        used_orders.update(plan["orderTokens"])
        number(plan["orderCount"], "ORDER_COUNT_INVALID", integer=True, minimum=1, maximum=2)
        require(len(plan["orderTokens"]) == plan["orderCount"], "ORDER_COUNT_MISMATCH")
        for field in ("pickupSeparationKm", "dropoffSeparationKm", "routeDistanceKm", "pickupWindowGapMinutes"):
            number(plan[field], "PLAN_NUMBER_INVALID", nullable=True)
        for field in ("samePickup", "samePickupRegion", "sameDropoffRegion", "regionAdjacent"):
            boolean(plan[field], "PLAN_BOOLEAN_INVALID")
    require(used_orders == order_tokens, "PLAN_ORDER_COVERAGE_MISMATCH")
    exact(target, "actionCode aiStatusCode selectedPlanToken allowedDriverTokens requiresCurrentGates operationalApplied capacityVerified promisesVerified", "BUNDLE_TARGET_KEYS")
    choice(target["actionCode"], ("DemandUnknown", "AdditionalParticipationNeeded", "ExistingCapacitySufficient"), "ACTION_INVALID")
    choice(target["aiStatusCode"], ("Disabled",), "MODEL_RESULT_FORBIDDEN")
    if target["selectedPlanToken"] is not None:
        token(target["selectedPlanToken"], "P")
        require(target["selectedPlanToken"] in plan_tokens, "TARGET_PLAN_UNKNOWN")
    tokens(target["allowedDriverTokens"], "D")
    require(set(target["allowedDriverTokens"]).issubset(driver_tokens), "TARGET_DRIVER_UNKNOWN")
    for field, expected in (("requiresCurrentGates", True), ("operationalApplied", False),
                            ("capacityVerified", False), ("promisesVerified", False)):
        require(target[field] is expected, "OPERATIONAL_OR_VERIFICATION_CLAIM_FORBIDDEN")


def validate_arrival(value, target):
    exact(value, "activeJobCount driverObservationAgeMinutes hasAttemptProof remainingRouteMinutes nextPickupRouteMinutes pickupReadyInMinutes vehicleRouteOption earlyToleranceMinutes lateToleranceMinutes driverToken", "ARRIVAL_INPUT_KEYS")
    number(value["activeJobCount"], "ACTIVE_JOB_INVALID", integer=True, minimum=1, maximum=1)
    number(value["driverObservationAgeMinutes"], "ARRIVAL_OBSERVATION_INVALID", maximum=0)
    boolean(value["hasAttemptProof"], "ATTEMPT_PROOF_INVALID")
    for field in ("remainingRouteMinutes", "nextPickupRouteMinutes", "earlyToleranceMinutes", "lateToleranceMinutes"):
        number(value[field], "ARRIVAL_MINUTES_INVALID")
    number(value["pickupReadyInMinutes"], "ARRIVAL_READY_INVALID", minimum=-100000)
    choice(value["vehicleRouteOption"], ("traavoidcaronly",), "VEHICLE_OPTION_INVALID")
    token(value["driverToken"], "D")
    exact(target, "decisionCode selectedDriverToken", "ARRIVAL_TARGET_KEYS")
    choice(target["decisionCode"], ("CandidateAllowed", "ActiveSchedulePickupLate", "ActiveScheduleAttemptUnverified"), "ARRIVAL_DECISION_INVALID")
    if target["selectedDriverToken"] is not None:
        require(target["selectedDriverToken"] == value["driverToken"], "ARRIVAL_TOKEN_UNKNOWN")


def validate_row(row, records):
    exact(row, "schemaVersion caseId taskCode scenarioFamily sourceRecordIds sourceBranchIds split input target teacher provenance review", "RAW_ROW_KEYS")
    require(type(row["schemaVersion"]) is int and row["schemaVersion"] == 1, "ROW_VERSION_INVALID")
    task, family = row["taskCode"], row["scenarioFamily"]
    choice(task, POLICIES, "TASK_INVALID")
    choice(family, FAMILIES[task], "FAMILY_INVALID")
    choice(row["split"], SPLITS, "SPLIT_INVALID")
    for field, pattern in (("sourceRecordIds", RECORD), ("sourceBranchIds", BRANCH)):
        values = row[field]
        require(type(values) is list and 1 <= len(values) <= 2, "SOURCE_REFERENCES_INVALID")
        require(all(type(x) is str and pattern.fullmatch(x) for x in values), "SOURCE_ID_INVALID")
        require(len(set(values)) == len(values), "SOURCE_ID_DUPLICATED")
    require(all(x in records for x in row["sourceRecordIds"]), "SOURCE_RECORD_UNKNOWN")
    actual_branches = {records[x]["branchId"] for x in row["sourceRecordIds"]}
    require(set(row["sourceBranchIds"]) == actual_branches, "SOURCE_BRANCH_MISMATCH")
    require(all(branch_split(x) == row["split"] for x in actual_branches), "SPLIT_LEAKAGE")
    require(type(row["caseId"]) is str and CASE.fullmatch(row["caseId"])
            and row["caseId"] == case_id(row["sourceRecordIds"], task, family), "CASE_ID_INVALID")
    exact(row["teacher"], "kind policyVersion optimalityProven", "TEACHER_KEYS")
    require(row["teacher"]["kind"] == "CurrentPolicyDraft" and row["teacher"]["optimalityProven"] is False
            and row["teacher"]["policyVersion"] == POLICIES[task], "TEACHER_CLAIM_INVALID")
    exact(row["provenance"], "synthetic routeBasis realCustomerLocationsUsed", "PROVENANCE_KEYS")
    require(row["provenance"]["synthetic"] is True and type(row["provenance"]["realCustomerLocationsUsed"]) is int
            and row["provenance"]["realCustomerLocationsUsed"] == 0, "REAL_CUSTOMER_DATA_FORBIDDEN")
    expected_basis = {"DemandParticipation": "NotApplicable", "BundleSelection": "GeometryApproximation",
                      "ArrivalAnchor": "SyntheticVehicleRoutePort"}[task]
    require(row["provenance"]["routeBasis"] == expected_basis, "ROUTE_BASIS_INVALID")
    exact(row["review"], "status eligibleForTraining", "REVIEW_KEYS")
    require(row["review"]["status"] == "Pending" and row["review"]["eligibleForTraining"] is False,
            "SELF_APPROVAL_FORBIDDEN")
    if task == "DemandParticipation":
        validate_demand_target(row["target"], validate_demand(row["input"]))
    elif task == "BundleSelection":
        validate_bundle(row["input"], row["target"])
        orders = set(t for plan in row["input"]["plans"] for t in plan["orderTokens"])
        require(len(orders) == len(row["sourceRecordIds"]), "SOURCE_ORDER_COUNT_MISMATCH")
    else:
        validate_arrival(row["input"], row["target"])
    return [{"role": "system", "content": SYSTEM},
            {"role": "user", "content": canonical({"taskCode": task, "input": row["input"]})},
            {"role": "assistant", "content": canonical(row["target"])}]


def load_dataset(path):
    path = safe_path(path, DATASET_ROOT)
    require(path.name == "dataset.json" and path.is_file(), "DATASET_PATH_INVALID")
    raw = path.read_bytes()
    dataset = parse_json(raw)
    require(type(dataset) is dict and set(dataset) == {"metadata", "records"}, "DATASET_KEYS")
    metadata = dataset["metadata"]
    require(type(metadata) is dict and metadata.get("policyRevision") == "regional-management-r50.dataset.r1"
            and metadata.get("localVerificationOnly") is True and metadata.get("executionModeCode") == "Simulation"
            and metadata.get("visitMenuSummariesAllocated") is False
            and type(metadata.get("realCustomerLocationsUsed")) is int and metadata["realCustomerLocationsUsed"] == 0
            and type(metadata.get("realDriverLocationsUsed")) is int and metadata["realDriverLocationsUsed"] == 0,
            "DATASET_BOUNDARY_INVALID")
    hash_value(metadata.get("sourceFingerprint"), "DATASET_FINGERPRINT_INVALID")
    require(path.parent.name == metadata["sourceFingerprint"], "DATASET_FINGERPRINT_PATH_MISMATCH")
    records = {}
    require(type(dataset["records"]) is list and len(dataset["records"]) > 0, "DATASET_RECORDS_INVALID")
    for item in dataset["records"]:
        require(type(item) is dict and type(item.get("recordId")) is str and RECORD.fullmatch(item["recordId"])
                and type(item.get("branchId")) is str and BRANCH.fullmatch(item["branchId"])
                and item.get("district") in DISTRICTS, "DATASET_RECORD_INVALID")
        require(item["recordId"] not in records, "DATASET_RECORD_DUPLICATED")
        records[item["recordId"]] = {"branchId": item["branchId"], "district": item["district"]}
    return path, raw, metadata, records


def source_path(relative):
    require(type(relative) is str and relative and "\\" not in relative and not relative.startswith("/")
            and all(x not in ("", ".", "..") for x in relative.split("/")) and ":" not in relative,
            "SOURCE_PATH_INVALID")
    return safe_path(DELIVERY_ROOT / relative, DELIVERY_ROOT) if relative.startswith("edit-kit/") else safe_path(REPO / relative, REPO)


def teacher_source_paths():
    result = set(TEACHER_FILES) | {DESIGN_REF, EXPORTER_REF}
    for folder, pattern in TEACHER_GLOBS:
        matches = list((REPO / folder).glob(pattern))
        require(matches, "TEACHER_SOURCE_FILES_MISSING")
        result.update(x.relative_to(REPO).as_posix() for x in matches if x.is_file())
    return result


def generator_fingerprint(hashes):
    # C#의 UTF-8/UnsafeRelaxedJsonEscaping 계약과 같은 속성 순서다.
    payload = {"sourceHashes": dict(sorted(hashes.items())), "fixedSeed": 51, "formatVersion": "dispatch-training-raw/v1"}
    return digest(json.dumps(payload, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8"))


def validate_manifest(manifest, raw, dataset_path, dataset_raw, metadata):
    exact(manifest, "schemaVersion formatVersion datasetSha256 sourceHashes rawSha256 caseCount modelCalls weightsTrained humanApprovals generatorFingerprint designSha256 sourceHashesUnchanged splitRule fixedSeed", "MANIFEST_KEYS")
    require(type(manifest["schemaVersion"]) is int and manifest["schemaVersion"] == 1
            and manifest["formatVersion"] == "dispatch-training-raw/v1", "MANIFEST_VERSION_INVALID")
    for field in ("modelCalls", "humanApprovals"):
        require(type(manifest[field]) is int and manifest[field] == 0, "EXECUTION_OR_APPROVAL_FORBIDDEN")
    require(manifest["weightsTrained"] is False and manifest["sourceHashesUnchanged"] is True, "MANIFEST_BOUNDARY_INVALID")
    require(type(manifest["fixedSeed"]) is int and manifest["fixedSeed"] == 51 and manifest["splitRule"] == SPLIT_RULE,
            "SPLIT_CONTRACT_INVALID")
    require(manifest["designSha256"] == DESIGN_SHA, "DESIGN_APPROVAL_MISMATCH")
    require(digest(safe_path(REPO / DESIGN_REF, REPO).read_bytes()) == DESIGN_SHA, "DESIGN_CHANGED")
    work = parse_json(safe_path(REPO / WORK_ORDER_REF, REPO).read_bytes())
    require(type(work) is dict and work.get("acceptedInCurrentThread") is True and work.get("implementationAllowed") is True
            and work.get("designSha256") == DESIGN_SHA and work.get("datasetSha256") == EXPECTED_DATASET_SHA
            and type(work.get("modelCallsAllowed")) is int and work["modelCallsAllowed"] == 0
            and work.get("weightTrainingAllowed") is False and work.get("operationalChangesAllowed") is False,
            "ACCEPTED_WORK_ORDER_REQUIRED")
    for field in ("datasetSha256", "rawSha256", "generatorFingerprint"):
        hash_value(manifest[field], "MANIFEST_HASH_INVALID")
    require(manifest["datasetSha256"] == EXPECTED_DATASET_SHA == digest(dataset_raw)
            and manifest["rawSha256"] == digest(raw), "INPUT_HASH_MISMATCH")
    number(manifest["caseCount"], "CASE_COUNT_INVALID", integer=True, minimum=1, maximum=100000)
    hashes = manifest["sourceHashes"]
    require(type(hashes) is dict and len(hashes) > 0, "SOURCE_HASHES_MISSING")
    for name, expected in hashes.items():
        source_path(name)
        hash_value(expected, "SOURCE_HASH_INVALID")
    required = {dataset_path.relative_to(REPO).as_posix(): digest(dataset_raw)}
    fingerprints = metadata.get("sourceFingerprints")
    require(type(fingerprints) is list and len(fingerprints) > 0, "SOURCE_FINGERPRINTS_MISSING")
    for fingerprint in fingerprints:
        exact(fingerprint, "relativePath sha256", "SOURCE_FINGERPRINT_KEYS")
        hash_value(fingerprint["sha256"], "SOURCE_FINGERPRINT_HASH_INVALID")
        require(fingerprint["relativePath"] not in required, "SOURCE_FINGERPRINT_DUPLICATED")
        required[fingerprint["relativePath"]] = fingerprint["sha256"]
    required[LEDGER_REF] = metadata.get("sourceLedgerSha256")
    required[EXPORTER_REF] = metadata.get("toolSha256")
    required[DESIGN_REF] = DESIGN_SHA
    summary_path = dataset_path.with_name("summary.json")
    required[summary_path.relative_to(REPO).as_posix()] = digest(summary_path.read_bytes())
    for name in teacher_source_paths():
        if name not in required:
            required[name] = digest(source_path(name).read_bytes())
    for name, expected in required.items():
        hash_value(expected, "SOURCE_FINGERPRINT_HASH_INVALID")
        require(hashes.get(name) == expected, "SOURCE_HASH_PIN_MISSING")
    require(set(hashes) == set(required), "SOURCE_HASH_SET_MISMATCH")
    require(manifest["generatorFingerprint"] == generator_fingerprint(hashes), "GENERATOR_FINGERPRINT_MISMATCH")
    checked = []
    for name, expected in hashes.items():
        hash_value(expected, "SOURCE_HASH_INVALID")
        path = source_path(name)
        require(name in required and path.is_file(), "SOURCE_PATH_NOT_APPROVED")
        require(digest(path.read_bytes()) == expected, "SOURCE_HASH_CHANGED")
        checked.append((path, expected))
    return checked


def ensure_ignored(path):
    result = subprocess.run(["git", "check-ignore", "--quiet", "--", str(path)], cwd=REPO,
                            capture_output=True, check=False)
    require(result.returncode == 0, "OUTPUT_NOT_GIT_IGNORED")


def display(value, suffix=""):
    return "미확인" if value is None else f"{value:g}{suffix}" if type(value) in (int, float) else str(value)


def review_detail(row):
    value, target, task = row["input"], row["target"], row["taskCode"]
    reason_names = {
        "DemandSnapshotUnavailable": "수요 관측이 없어 기존 안전 조건을 다시 확인합니다",
        "DemandSnapshotStale": "수요 관측이 만료되어 기존 안전 조건을 다시 확인합니다",
        "DriverCapacityUnavailable": "기사의 잔여 업무 근거가 없어 다시 확인합니다",
        "DriverCapacityStale": "기사 관측이 만료되어 다시 확인합니다",
        "DemandAndCapacityObserved": "확인된 수요와 기사 여력을 비교했습니다",
        "ExistingEligibilityNotMet": "기존 적격 조건을 충족하지 못했습니다",
        "ExistingActivityProtected": "기존 활동 기사의 참여를 유지합니다",
        "LongWaitOpportunity": "오래 기다린 기사에게 참여 기회를 줍니다",
        "OrderDelayCapacityException": "주문 지연 위험 때문에 추가 참여를 권고합니다",
        "DemandCapacityAvailable": "기존 기사 여력보다 수요가 많아 추가 참여를 권고합니다",
        "ExistingCapacitySufficient": "기존 기사 여력으로 충분하여 신규 참여를 기다리도록 합니다"}
    lines = []
    if task in ("DemandParticipation", "BundleSelection"):
        demand = value if task == "DemandParticipation" else value["demand"]
        drivers = demand["drivers"]
        active = sum(x["active"] for x in drivers)
        eligible = sum(x["eligible"] for x in drivers)
        lines.extend([f"현재 활동 기사 {active}명, 신규 대기 기사 {len(drivers) - active}명이며 적격 기사는 {eligible}명입니다. "
                      f"계획 시간은 {display(demand['planningWindowMinutes'], '분')}, 기준 처리량은 기사 1명당 시간당 3.5건입니다.", "",
                      "| 수요·관측 | 값 |", "| --- | --- |",
                      f"| 현재 대기 주문 | {display(demand['waitingOrders'], '건')} |",
                      f"| 최근 {display(demand['observationWindowMinutes'], '분')} 주문 | {display(demand['recentOrders'], '건')} |",
                      f"| 향후 수요 예측 | {display(demand['forecastOrders'], '건')} |",
                      f"| 수요 관측 경과 | {display(demand['demandObservationAgeMinutes'], '분')} |",
                      f"| 주문 지연 위험 | {'있음' if demand['delayRisk'] else '없음'} |", "",
                      "| 기사 | 구분 | 대기 | 남은 업무 | 관측 경과 | 최근 배차 |",
                      "| --- | --- | --- | --- | --- | --- |"])
        for driver in drivers:
            lines.append(f"| {driver['driverToken']} | {'활동 중' if driver['active'] else '신규 대기'}"
                         f"{'·적격 미충족' if not driver['eligible'] else ''} | {display(driver['waitMinutes'], '분')}"
                         f" | {display(driver['remainingMinutes'], '분')} | {display(driver['observationAgeMinutes'], '분')}"
                         f" | {driver['recentAssignments']}건 |")
        lines.append("")
    if task == "DemandParticipation":
        states = {"Current": "최신", "Unknown": "미확인", "Stale": "만료"}
        lines.extend([f"정책 초안: 자료 상태는 **{states[target['dataStateCode']]}**, 목표 활동 기사 수는 "
                      f"**{display(target['targetActiveDrivers'], '명')}**, 추가 참여 권고는 **{target['additionalParticipants']}명**입니다.",
                      f"선택 근거: {reason_names[target['reasonCode']]}", "",
                      "| 기사 | 새 제안 | 신규 참여 권고 | 순서 | 근거 |", "| --- | --- | --- | --- | --- |"])
        for decision in sorted(target["driverDecisions"], key=lambda x: x["priority"]):
            lines.append(f"| {decision['driverToken']} | {'허용' if decision['allowProposal'] else '대기'}"
                         f" | {'예' if decision['admitted'] else '아니오'} | {decision['priority']}"
                         f" | {reason_names[decision['reasonCode']]} |")
    elif task == "BundleSelection":
        actions = {"DemandUnknown": "수요 재확인", "AdditionalParticipationNeeded": "추가 참여 필요",
                   "ExistingCapacitySufficient": "기존 여력 충분"}
        lines.extend([f"정책 초안: **{actions[target['actionCode']]}**. 선택 조합은 **{target['selectedPlanToken'] or '보류'}**, "
                      f"제안 허용 기사는 **{', '.join(target['allowedDriverTokens']) or '없음'}**입니다.",
                      "선택 근거: 현재 정책이 조합 검토 조건에 맞는 후보 중 기준 조합을 선택했습니다. "
                      "실제 적재·모든 약속 시간은 검증되지 않았으며 운영 수락도 별도입니다. 모델 호출은 하지 않았습니다.",
                      "합성 요청 순서: " + " → ".join(f"{order['requestOrdinal']}번째 {order['orderToken']}"
                                                          for order in sorted(value["orders"], key=lambda x: x["requestOrdinal"])) + ".", "",
                      "| 조합 | 주문 | 픽업 간격 | 전달 간격 | 기하 경로 근사 | 같은 픽업 |", "| --- | --- | --- | --- | --- | --- |"])
        for plan in value["plans"]:
            lines.append(f"| {plan['planToken']} | {', '.join(plan['orderTokens'])} ({plan['orderCount']}건)"
                         f" | {display(plan['pickupSeparationKm'], 'km')} | {display(plan['dropoffSeparationKm'], 'km')}"
                         f" | {display(plan['routeDistanceKm'], 'km')} | {'예' if plan['samePickup'] else '아니오'} |")
        limits = value["limits"]
        lines.append(f"검토 한도: 최대 2건, 픽업 간격 {display(limits['maximumPickupSeparationKm'], 'km')}, "
                     f"전달 간격 {display(limits['maximumDropoffSeparationKm'], 'km')}, "
                     f"기하 경로 {display(limits['maximumApproximateRouteKm'], 'km')}. 실제 적재·모든 약속 검증과 수락은 별도입니다.")
        selected = next((plan for plan in value["plans"] if plan["planToken"] == target["selectedPlanToken"]), None)
        if row["scenarioFamily"] == "two-order" and selected and selected["orderCount"] == 1:
            lines.append("**예외 검토: 두 주문을 함께 검토했지만 실제 정책은 단건을 선택했습니다.**")
    else:
        decisions = {"CandidateAllowed": "현재 업무의 마지막 전달 이후 후속 픽업 후보로 검토할 수 있습니다",
                     "ActiveSchedulePickupLate": "후속 픽업 약속에 늦으므로 후보에서 제외합니다",
                     "ActiveScheduleAttemptUnverified": "현재 수행 업무의 근거가 확인되지 않아 후보에서 제외합니다"}
        lines.extend([f"활동 중인 기사 {value['driverToken']}에게 진행 업무 1건이 있습니다. 현재 GPS 대신 합성 이동 시간을 사용합니다.", "",
                      "| 후속 픽업 조건 | 값 |", "| --- | --- |",
                      f"| 현재 업무 근거 | {'확인됨' if value['hasAttemptProof'] else '없음'} |",
                      f"| 남은 전달 이동 | {display(value['remainingRouteMinutes'], '분')} |",
                      f"| 마지막 전달 후 다음 픽업 이동 | {display(value['nextPickupRouteMinutes'], '분')} |",
                      f"| 다음 픽업 준비까지 | {display(value['pickupReadyInMinutes'], '분')} |",
                      f"| 조기 도착 허용·지연 허용 | {display(value['earlyToleranceMinutes'], '분')}·{display(value['lateToleranceMinutes'], '분')} |", "",
                      f"정책 초안·선택 근거: **{decisions[target['decisionCode']]}** "
                      f"선택 기사는 **{target['selectedDriverToken'] or '없음'}**입니다."])
    return lines


def make_review(rows, records, candidate_ids):
    """각 구·family의 대표를 먼저 고르며 동일 사례를 중복 표시하지 않는다."""
    selected, selected_ids = [], set()
    sorted_rows = sorted(rows, key=lambda x: x["caseId"])
    for district in DISTRICTS:
        for task, families in FAMILIES.items():
            for family in families:
                match = next((row for row in sorted_rows if row["taskCode"] == task and row["scenarioFamily"] == family
                              and row["caseId"] not in selected_ids
                              and any(records[x]["district"] == district for x in row["sourceRecordIds"])), None)
                if match:
                    selected.append(match)
                    selected_ids.add(match["caseId"])
    # 33개 조합 중 최대24개: 각 구/각 family를 우선 보존한다.
    if len(selected) > 24:
        chosen, covered = [], set()
        for row in selected:
            labels = {("district", records[x]["district"]) for x in row["sourceRecordIds"]}
            labels.add(("family", row["taskCode"] + "/" + row["scenarioFamily"]))
            if labels - covered:
                chosen.append(row)
                covered.update(labels)
        selected = chosen + [x for x in selected if x["caseId"] not in {y["caseId"] for y in chosen}][:24 - len(chosen)]
    for row in sorted_rows:
        if len(selected) >= min(12, len(rows)):
            break
        if row["caseId"] not in {x["caseId"] for x in selected}:
            selected.append(row)
    fallback = next((row for row in sorted_rows if row["taskCode"] == "BundleSelection"
                     and row["scenarioFamily"] == "two-order" and any(
                         plan["planToken"] == row["target"]["selectedPlanToken"] and plan["orderCount"] == 1
                         for plan in row["input"]["plans"])), None)
    if fallback and fallback["caseId"] not in {row["caseId"] for row in selected}:
        if len(selected) < 24:
            selected.append(fallback)
        else:
            def coverage(items):
                return ({("district", records[x]["district"]) for item in items for x in item["sourceRecordIds"]}
                        | {("family", item["taskCode"] + "/" + item["scenarioFamily"]) for item in items})
            required_coverage = coverage(selected)
            index = next(i for i in range(len(selected) - 1, -1, -1)
                         if required_coverage.issubset(coverage(selected[:i] + selected[i + 1:] + [fallback])))
            selected[index] = fallback
    text = ["# 배차 학습 후보 검토", "", "현재 정책의 초안이며 최적해·실제 결과·사람 승인이 아닙니다. 학습 활성 자료는 0행입니다.",
            "주소·메뉴·개인정보는 표시하지 않습니다. 공개 매장 자료는 출처이며 주문·기사·전달 위치·시간은 합성입니다.", ""]
    descriptions = {"shortage": "현재 활동 기사 여력보다 수요가 많은 상황", "surplus": "기존 여력으로 수요를 처리할 수 있는 상황",
                    "long-wait": "장기 대기 기사에게 기회를 주는 상황", "stale": "관측 자료가 만료된 상황",
                    "missing": "관측 자료가 없는 상황", "no-eligible": "적격 활동 기사가 없는 상황", "single": "단건 조합 검토",
                    "two-order": "두 주문의 조합 검토", "valid": "마지막 전달 이후 후속 픽업 가능성 검토",
                    "late": "후속 픽업 약속에 늦는 상황", "missingproof": "현재 업무의 수행 근거가 없는 상황"}
    for index, row in enumerate(selected, 1):
        districts = "·".join(sorted({records[x]["district"] for x in row["sourceRecordIds"]}))
        text.extend([f"## {index}. {districts} — {descriptions[row['scenarioFamily']]}", "",
                     *review_detail(row), "",
                     f"- 합성 경계: `{row['provenance']['routeBasis']}`. 실제 고객 위치·기사 GPS·실제 도로 경로가 아닙니다.",
                     f"- 자료 상태: {'후보' if row['caseId'] in candidate_ids else '격리'} / 검토: **Pending** / 학습 사용: **false**",
                     f"- 필요 참조: `{row['caseId']}` / `{row['split']}` / `{row['taskCode']}/{row['scenarioFamily']}`", ""])
    return ("\n".join(text) + "\n").encode("utf-8"), len(selected)


def compile_materials(raw_path, dataset_path, output=None):
    raw_path = safe_path(raw_path, ROOT / "raw")
    require(raw_path.name == "raw.jsonl" and raw_path.is_file(), "RAW_PATH_INVALID")
    manifest_path = safe_path(raw_path.with_name("raw.manifest.json"), ROOT / "raw")
    require(manifest_path.is_file(), "RAW_MANIFEST_MISSING")
    raw, manifest_raw = raw_path.read_bytes(), manifest_path.read_bytes()
    manifest = parse_json(manifest_raw)
    dataset_path, dataset_raw, metadata, records = load_dataset(dataset_path)
    sources = validate_manifest(manifest, raw, dataset_path, dataset_raw, metadata)
    require(raw_path.parent.name == manifest["generatorFingerprint"], "RAW_FINGERPRINT_PATH_MISMATCH")
    require(raw and raw.endswith(b"\n") and b"\r" not in raw, "RAW_LINE_FORMAT_INVALID")
    lines = raw.splitlines()
    require(len(lines) == manifest["caseCount"] and all(lines), "RAW_CASE_COUNT_MISMATCH")
    rows, entries, ids, input_groups = [], [], set(), collections.defaultdict(list)
    for line in lines:
        row = parse_json(line)
        messages = validate_row(row, records)
        require(row["caseId"] not in ids, "CASE_ID_DUPLICATED")
        ids.add(row["caseId"])
        rows.append(row)
        input_hash = digest(json_bytes(messages[:2]))
        target_hash = digest(json_bytes(row["target"]))
        audit = {key: row[key] for key in ("caseId", "taskCode", "scenarioFamily", "sourceRecordIds", "sourceBranchIds", "split", "teacher", "provenance", "review")}
        audit.update({"inputSha256": input_hash, "targetSha256": target_hash, "rawRowSha256": digest(line),
                      "datasetSha256": manifest["datasetSha256"], "rawSha256": manifest["rawSha256"],
                      "districts": sorted({records[x]["district"] for x in row["sourceRecordIds"]})})
        entry = {"messages": messages, "auditMetadata": audit}
        input_groups[input_hash].append(entry)
        entries.append(entry)
    grouped = {split: [] for split in SPLITS}
    quarantine, quarantine_counts = [], collections.Counter()
    for entry in sorted(entries, key=lambda x: x["auditMetadata"]["caseId"]):
        group = input_groups[entry["auditMetadata"]["inputSha256"]]
        if len(group) > 1:
            reasons = ["DuplicateInput"]
            if len({x["auditMetadata"]["targetSha256"] for x in group}) > 1:
                reasons.append("ConflictingTargets")
            if len({x["auditMetadata"]["split"] for x in group}) > 1:
                reasons.append("CrossSplitInput")
            entry["auditMetadata"]["quarantineReasonCodes"] = reasons
            quarantine.append(entry)
            quarantine_counts.update(reasons)
        else:
            grouped[entry["auditMetadata"]["split"]].append(entry)
    compiler_sha = digest(Path(__file__).read_bytes())
    fingerprint = digest(json_bytes({"formatVersion": FORMAT, "rawSha256": digest(raw), "manifestSha256": digest(manifest_raw),
                                     "datasetSha256": digest(dataset_raw), "compilerSha256": compiler_sha}))
    target = safe_path(ROOT / "compiled" / fingerprint, ROOT / "compiled")
    if output is not None:
        require(safe_path(output, ROOT / "compiled") == target, "OUTPUT_FINGERPRINT_PATH_REQUIRED")
    ensure_ignored(target / "summary.json")
    files = {}
    for split in SPLITS:
        files[f"candidates/{split}.jsonl"] = b"".join(json_bytes(x) for x in grouped[split])
        files[f"ready/{split}.jsonl"] = b""
    files["quarantine.jsonl"] = b"".join(json_bytes(x) for x in quarantine)
    candidate_ids = {x["auditMetadata"]["caseId"] for values in grouped.values() for x in values}
    files["review.md"], review_count = make_review(rows, records, candidate_ids)
    summary = {"schemaVersion": 1, "formatVersion": FORMAT, "fingerprint": fingerprint,
               "rawSha256": digest(raw), "rawManifestSha256": digest(manifest_raw), "datasetSha256": digest(dataset_raw),
               "datasetSourceFingerprint": metadata["sourceFingerprint"], "sourceHashes": manifest["sourceHashes"],
               "compilerSha256": compiler_sha, "generatorFingerprint": manifest["generatorFingerprint"], "designSha256": DESIGN_SHA,
               "rawCases": len(rows), "candidateCases": sum(map(len, grouped.values())), "quarantinedCases": len(quarantine),
               "candidateSplits": {k: len(v) for k, v in grouped.items()}, "readySplits": {k: 0 for k in SPLITS},
               "quarantineReasonCounts": dict(sorted(quarantine_counts.items())), "reviewRepresentatives": review_count,
               "rawTaskFamilyCounts": dict(sorted(collections.Counter(x["taskCode"] + "/" + x["scenarioFamily"] for x in rows).items())),
               "candidateTaskFamilyCounts": dict(sorted(collections.Counter(x["auditMetadata"]["taskCode"] + "/" + x["auditMetadata"]["scenarioFamily"] for v in grouped.values() for x in v).items())),
               "candidateDistrictCounts": dict(sorted(collections.Counter(d for v in grouped.values() for x in v for d in x["auditMetadata"]["districts"]).items())),
               "teacherKind": "CurrentPolicyDraft", "reviewStatus": "Pending", "eligibleForTraining": False,
               "humanApprovals": 0, "modelCalls": 0, "weightsTrained": False, "operationalWrites": 0,
               "realCustomerLocationsUsed": 0, "realDriverLocationsUsed": 0, "splitRule": SPLIT_RULE,
               "fileHashes": {name: digest(data) for name, data in sorted(files.items())}}
    files["summary.json"] = json_bytes(summary)
    # 제작 중 입력이 바뀌었으면 아무 출력도 확정하지 않는다.
    for path, expected in sources + [(raw_path, digest(raw)), (manifest_path, digest(manifest_raw)), (dataset_path, digest(dataset_raw))]:
        require(digest(path.read_bytes()) == expected, "SOURCE_CHANGED_DURING_COMPILE")
    if target.exists():
        for item in target.rglob("*"):
            safe_path(item, ROOT / "compiled")
            require(item.is_file() or item.relative_to(target).as_posix() in ("candidates", "ready"),
                    "EXISTING_OUTPUT_TAMPERED")
        existing = {x.relative_to(target).as_posix() for x in target.rglob("*") if x.is_file()}
        require(existing == set(files), "EXISTING_OUTPUT_TAMPERED")
        for name, data in files.items():
            path = safe_path(target / name, ROOT / "compiled")
            require(path.read_bytes() == data, "EXISTING_OUTPUT_TAMPERED")
        return target, summary
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".compile-", dir=target.parent) as staging_name:
        staging = safe_path(staging_name, ROOT / "compiled")
        for name, data in files.items():
            path = safe_path(staging / name, ROOT / "compiled")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        staging.rename(target)
        # TemporaryDirectory의 원래 빈 경로를 복원해 정상 종료 시 자체 경로만 정리한다.
        staging.mkdir()
    return target, summary


def main(argv=None):
    parser = argparse.ArgumentParser(description="합성 배차 정책 학습 후보 검증·격리 (승인·학습 기능 없음)")
    commands = parser.add_subparsers(dest="command", required=True)
    compile_parser = commands.add_parser("compile")
    compile_parser.add_argument("--raw", required=True)
    compile_parser.add_argument("--dataset", required=True)
    compile_parser.add_argument("--output")
    args = parser.parse_args(argv)
    try:
        path, summary = compile_materials(args.raw, args.dataset, args.output)
        print(canonical({"outputRelativePath": path.relative_to(REPO).as_posix(), "candidateCases": summary["candidateCases"],
                         "quarantinedCases": summary["quarantinedCases"], "readyCases": 0}))
        return 0
    except (MaterialFailure, OSError, UnicodeError) as error:
        print(canonical({"errorCode": str(error) if isinstance(error, MaterialFailure) else "FILESYSTEM_FAILURE"}), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
