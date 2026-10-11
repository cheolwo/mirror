"""r52 배차 시점의 합성 정책 초안을 검증·분리한다. 모델 호출·학습·승인은 없다."""
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
ROOT = REPO / "artifacts/local/local-dispatch-ai/dispatch-timing-r52"
DATASET_ROOT = REPO / "artifacts/local/local-dispatch-ai/regional-management-r50/datasets"
DELIVERY_ROOT = Path.home() / "Documents/ChatGPT/배달"
PLAN_ROOT = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/"
DESIGN_REF = PLAN_ROOT + "batch-timing.implementation.r52.md"
WORK_REF = PLAN_ROOT + "batch-timing.r52.work-order.json"
DESIGN_SHA = "a895d005306e9f077173bb235226ec28e6c6afd1e0efce690fae167d1e48a0be"
DATASET_SHA = "6a9c5e01bcc9dd84e2f30353c6cc3d88d5d9aa9853eed441b5bca0158ffa8bff"
LEDGER_REF = "edit-kit/branch-deliveries-all-20261010/delivery-count-ledger.json"
EXPORTER_REF = "eng/local-dispatch-ai/RegionalScenarioRunner/prepare_regional_dataset.py"
TEACHER_FILES = ("Ssalddel.Domain/배차/FoodDispatchTimingPolicy.cs",
                 "Ssalddel.Contracts/Common/Dispatch/FoodDispatchTimingContracts.cs")
RUNNER_REF = "eng/local-dispatch-ai/DispatchTimingScenarioRunner"
RAW_FORMAT = "dispatch-timing-raw/v1"
FORMAT = "dispatch-timing-candidates/v1"
SPLIT_RULE = "sha256-branch-first8-u32-mod100-70-15-15"
SPLITS = ("train", "validation", "test")
FAMILIES = ("collect", "combination-ready", "collection-expired", "urgent", "sparse", "unknown", "no-driver", "retry")
ACTIONS = ("Collect", "DispatchNow", "ExistingFlow")
REASONS = {"NotApplicable", "ActiveOffer", "Retry", "InvalidTimestamps", "InvalidCounts", "SnapshotUnknown",
           "SnapshotFuture", "SnapshotStale", "DemandUnknown", "DriverCapacityUnknown", "NoWaitingOrders",
           "DelayRisk", "DeadlineApproaching", "CollectionElapsed", "NoAvailableDrivers", "PromisingPlanAvailable",
           "CompatibleOrderThresholdReached", "NoExpectedNewOrders", "ShortCollectionWindow"}
DISTRICTS = ("중랑구", "동대문구", "광진구")
EXPECTED_RECORDS = 504
EXPECTED_BRANCHES = 339
SHA = re.compile(r"[0-9a-f]{64}\Z")
RECORD = re.compile(r"replay-record-[0-9a-f]{64}\Z")
BRANCH = re.compile(r"replay-branch-[0-9a-f]{64}\Z")
SYSTEM = ("합성 배차 시점 연습입니다. 익명 상대 시간·수요·기사 여력만 보고 JSON 정책 초안을 반환하세요. "
          "현재 정책 답안은 최적해·사람 승인·실제 배차가 아니며 기사 평균 단가 최적화 자료도 아닙니다.")


class MaterialFailure(Exception):
    """원문이나 고객 정보를 오류에 포함하지 않는다."""


def require(condition, code):
    if not condition:
        raise MaterialFailure(code)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)


def json_bytes(value):
    return (canonical(value) + "\n").encode("utf-8")


def parse_json(data):
    def object_pairs(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "DUPLICATE_JSON_KEY")
            result[key] = value
        return result

    def constant(_):
        raise MaterialFailure("NONFINITE_JSON")

    try:
        return json.loads(data, object_pairs_hook=object_pairs, parse_constant=constant)
    except (ValueError, UnicodeError, RecursionError):
        raise MaterialFailure("INVALID_JSON") from None


def exact(value, fields, code):
    require(type(value) is dict and set(value) == set(fields.split()), code)


def number(value, code, *, nullable=False, integer=False, minimum=0, maximum=100000):
    if value is None and nullable:
        return
    require(type(value) in ((int,) if integer else (int, float)), code)
    require(math.isfinite(value) and minimum <= value <= maximum, code)


def safe_path(path, boundary):
    path, boundary = Path(path).absolute(), Path(boundary).absolute()
    require(path.is_relative_to(boundary), "PATH_OUTSIDE_BOUNDARY")
    for part in (path, *path.parents):
        if not part.is_relative_to(boundary):
            break
        if part.exists() or part.is_symlink():
            value = part.lstat()
            require(not stat.S_ISLNK(value.st_mode) and not (getattr(value, "st_file_attributes", 0) & 0x400),
                    "LINK_PATH_FORBIDDEN")
    require(path.resolve().is_relative_to(boundary.resolve()), "PATH_OUTSIDE_BOUNDARY")
    return path.resolve()


def source_path(name):
    require(type(name) is str and name and "\\" not in name and ":" not in name
            and all(p not in ("", ".", "..") for p in name.split("/")), "SOURCE_PATH_INVALID")
    boundary = DELIVERY_ROOT if name.startswith("edit-kit/") else REPO
    return safe_path(boundary / name, boundary)


def branch_split(branch):
    bucket = int(digest(branch.encode("utf-8"))[:8], 16) % 100
    return "train" if bucket < 70 else "validation" if bucket < 85 else "test"


def case_id(record, family):
    return "r52-" + digest((record + "|DispatchTiming|" + family).encode("utf-8"))[:24]


def generator_fingerprint(hashes):
    value = {"sourceHashes": dict(sorted(hashes.items())), "fixedSeed": 52, "formatVersion": RAW_FORMAT}
    return digest(json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8"))


def teacher_paths():
    names = set(TEACHER_FILES) | {DESIGN_REF, EXPORTER_REF}
    for pattern in ("*.cs", "*.csproj"):
        matches = list((REPO / RUNNER_REF).glob(pattern))
        require(matches, "GENERATOR_SOURCE_MISSING")
        names.update(p.relative_to(REPO).as_posix() for p in matches if p.is_file())
    return names


def load_dataset(path):
    path = safe_path(path, DATASET_ROOT)
    require(path.name == "dataset.json" and path.is_file(), "DATASET_PATH_INVALID")
    raw = path.read_bytes()
    require(digest(raw) == DATASET_SHA, "DATASET_HASH_MISMATCH")
    data = parse_json(raw)
    exact(data, "metadata records", "DATASET_KEYS")
    metadata = data["metadata"]
    require(type(metadata) is dict and metadata.get("localVerificationOnly") is True
            and metadata.get("executionModeCode") == "Simulation"
            and metadata.get("realCustomerLocationsUsed") == 0 and type(metadata.get("realCustomerLocationsUsed")) is int
            and metadata.get("realDriverLocationsUsed") == 0 and type(metadata.get("realDriverLocationsUsed")) is int
            and metadata.get("historicalValuesAreCurrentPrices") is False
            and metadata.get("visitMenuSummariesAllocated") is False, "DATASET_BOUNDARY_INVALID")
    require(type(metadata.get("sourceFingerprint")) is str and SHA.fullmatch(metadata["sourceFingerprint"])
            and path.parent.name == metadata["sourceFingerprint"], "DATASET_FINGERPRINT_INVALID")
    require(type(data["records"]) is list and len(data["records"]) == EXPECTED_RECORDS, "DATASET_SCOPE_INVALID")
    records = {}
    for row in data["records"]:
        require(type(row) is dict and type(row.get("recordId")) is str and RECORD.fullmatch(row["recordId"])
                and type(row.get("branchId")) is str and BRANCH.fullmatch(row["branchId"])
                and row.get("district") in DISTRICTS, "DATASET_RECORD_INVALID")
        require(row["recordId"] not in records, "DATASET_RECORD_DUPLICATED")
        records[row["recordId"]] = {"branchId": row["branchId"], "district": row["district"]}
    require(len({x["branchId"] for x in records.values()}) == EXPECTED_BRANCHES, "DATASET_BRANCH_SCOPE_INVALID")
    return path, raw, metadata, records


def validate_manifest(manifest, raw, dataset_path, dataset_raw, metadata):
    exact(manifest, "schemaVersion formatVersion datasetSha256 sourceHashes rawSha256 caseCount modelCalls weightsTrained humanApprovals operationalWrites generatorFingerprint designSha256 sourceHashesUnchanged splitRule fixedSeed", "MANIFEST_KEYS")
    require(type(manifest["schemaVersion"]) is int and manifest["schemaVersion"] == 1 and manifest["formatVersion"] == RAW_FORMAT,
            "MANIFEST_VERSION_INVALID")
    require(all(type(manifest[k]) is int and manifest[k] == 0 for k in ("modelCalls", "humanApprovals", "operationalWrites"))
            and manifest["weightsTrained"] is False and manifest["sourceHashesUnchanged"] is True, "EXECUTION_OR_APPROVAL_FORBIDDEN")
    require(type(manifest["fixedSeed"]) is int and manifest["fixedSeed"] == 52 and manifest["splitRule"] == SPLIT_RULE,
            "SPLIT_CONTRACT_INVALID")
    require(manifest["designSha256"] == DESIGN_SHA and digest(source_path(DESIGN_REF).read_bytes()) == DESIGN_SHA,
            "DESIGN_APPROVAL_MISMATCH")
    work = parse_json(source_path(WORK_REF).read_bytes())
    require(type(work) is dict and work.get("acceptedInCurrentThread") is True and work.get("implementationAllowed") is True
            and work.get("designSha256") == DESIGN_SHA and work.get("sourceDatasetSha256") == DATASET_SHA
            and type(work.get("modelCallsAllowed")) is int and work["modelCallsAllowed"] == 0
            and work.get("weightTrainingAllowed") is False and work.get("operationalChangesAllowed") is False,
            "ACCEPTED_WORK_ORDER_REQUIRED")
    require(manifest["datasetSha256"] == DATASET_SHA == digest(dataset_raw) and manifest["rawSha256"] == digest(raw),
            "INPUT_HASH_MISMATCH")
    number(manifest["caseCount"], "CASE_COUNT_INVALID", integer=True, minimum=1)
    required = {dataset_path.relative_to(REPO).as_posix(): DATASET_SHA, DESIGN_REF: DESIGN_SHA}
    fingerprints = metadata.get("sourceFingerprints")
    require(type(fingerprints) is list and fingerprints, "HISTORICAL_FINGERPRINTS_MISSING")
    for item in fingerprints:
        exact(item, "relativePath sha256", "SOURCE_FINGERPRINT_KEYS")
        name = item["relativePath"]
        source_path(name)
        require(name.startswith(("artifacts/local/delivery-history-replay-r54/", "edit-kit/branch-deliveries-all-20261010/"))
                and name not in required, "HISTORICAL_SOURCE_INVALID")
        required[name] = item["sha256"]
    required[LEDGER_REF] = metadata.get("sourceLedgerSha256")
    required[EXPORTER_REF] = metadata.get("toolSha256")
    summary_path = dataset_path.with_name("summary.json")
    require(parse_json(summary_path.read_bytes()).get("datasetSha256") == DATASET_SHA, "DATASET_SUMMARY_MISMATCH")
    required[summary_path.relative_to(REPO).as_posix()] = digest(summary_path.read_bytes())
    for name in teacher_paths():
        if name not in required:
            required[name] = digest(source_path(name).read_bytes())
    hashes = manifest["sourceHashes"]
    require(type(hashes) is dict and set(hashes) == set(required), "SOURCE_HASH_SET_MISMATCH")
    checked = []
    for name, expected in required.items():
        require(type(expected) is str and SHA.fullmatch(expected) and hashes[name] == expected, "SOURCE_HASH_PIN_MISMATCH")
        path = source_path(name)
        require(path.is_file() and digest(path.read_bytes()) == expected, "SOURCE_HASH_CHANGED")
        checked.append((path, expected))
    require(manifest["generatorFingerprint"] == generator_fingerprint(hashes), "GENERATOR_FINGERPRINT_MISMATCH")
    return checked


def validate_row(row, records):
    exact(row, "schemaVersion caseId taskCode scenarioFamily sourceRecordIds sourceBranchIds split input target teacher provenance review", "ROW_KEYS")
    require(type(row["schemaVersion"]) is int and row["schemaVersion"] == 1 and row["taskCode"] == "DispatchTiming", "ROW_VERSION_INVALID")
    family = row["scenarioFamily"]
    require(family in FAMILIES, "FAMILY_INVALID")
    require(type(row["sourceRecordIds"]) is list and len(row["sourceRecordIds"]) == 1
            and type(row["sourceRecordIds"][0]) is str and row["sourceRecordIds"][0] in records, "SOURCE_RECORD_INVALID")
    record = row["sourceRecordIds"][0]
    branch = records[record]["branchId"]
    require(row["sourceBranchIds"] == [branch] and row["split"] == branch_split(branch), "SOURCE_SPLIT_MISMATCH")
    require(row["caseId"] == case_id(record, family), "CASE_ID_INVALID")
    require(row["teacher"] == {"kind": "CurrentPolicyDraft", "policyVersion": "food-dispatch-timing/v1", "optimalityProven": False},
            "TEACHER_APPROVAL_INVALID")
    require(type(row["teacher"]["optimalityProven"]) is bool, "TEACHER_APPROVAL_INVALID")
    require(row["review"] == {"status": "Pending", "eligibleForTraining": False}
            and type(row["review"]["eligibleForTraining"]) is bool, "HUMAN_APPROVAL_FORBIDDEN")
    exact(row["provenance"], "synthetic timingBasis realCustomerLocationsUsed realDriverLocationsUsed historicalTimingObserved", "PROVENANCE_KEYS")
    p = row["provenance"]
    require(p["synthetic"] is True and p["timingBasis"] == "SeededRelativeTimingNotHistoricalObservation"
            and p["historicalTimingObserved"] is False
            and type(p["realCustomerLocationsUsed"]) is int and p["realCustomerLocationsUsed"] == 0
            and type(p["realDriverLocationsUsed"]) is int and p["realDriverLocationsUsed"] == 0, "SYNTHETIC_BOUNDARY_INVALID")
    value = row["input"]
    exact(value, "orderAgeSeconds snapshotAgeSeconds latestDispatchInSeconds applicable hasActiveOffer isRetry demandKnown driverCapacityKnown delayRisk promisingPlanAvailable waitingOrders compatibleOrders availableDrivers expectedNewOrders settings", "INPUT_KEYS")
    number(value["orderAgeSeconds"], "ORDER_AGE_INVALID", integer=True, maximum=10000)
    number(value["snapshotAgeSeconds"], "SNAPSHOT_AGE_INVALID", integer=True, nullable=True, maximum=10000)
    number(value["latestDispatchInSeconds"], "LATEST_DISPATCH_INVALID", integer=True, nullable=True, minimum=-10000, maximum=10000)
    for key in ("applicable", "hasActiveOffer", "isRetry", "demandKnown", "driverCapacityKnown", "delayRisk", "promisingPlanAvailable"):
        require(type(value[key]) is bool, "INPUT_BOOLEAN_INVALID")
    for key in ("waitingOrders", "compatibleOrders", "availableDrivers"):
        number(value[key], "INPUT_COUNT_INVALID", integer=True)
    number(value["expectedNewOrders"], "EXPECTED_ORDERS_INVALID", nullable=True)
    settings = value["settings"]
    exact(settings, "collectionSeconds maxWaitSeconds minimumCompatibleOrders deadlineBufferSeconds maxSnapshotAgeSeconds", "SETTINGS_KEYS")
    for key, val in settings.items():
        number(val, "SETTINGS_INVALID", integer=True, minimum=0, maximum=100000)
    require(settings["collectionSeconds"] <= settings["maxWaitSeconds"]
            and 0 < settings["maxWaitSeconds"] <= 120 and settings["minimumCompatibleOrders"] >= 1
            and settings["deadlineBufferSeconds"] <= settings["maxWaitSeconds"]
            and 0 < settings["maxSnapshotAgeSeconds"] <= 120, "SETTINGS_ORDER_INVALID")
    target = row["target"]
    exact(target, "actionCode reasonCode recheckInSeconds hardDeadlineInSeconds allowedActions", "TARGET_KEYS")
    require(type(target["actionCode"]) is str and target["actionCode"] in ACTIONS
            and type(target["reasonCode"]) is str and target["reasonCode"] in REASONS, "TARGET_CODE_INVALID")
    number(target["hardDeadlineInSeconds"], "DEADLINE_INVALID", integer=True, minimum=-10000, maximum=10000)
    number(target["recheckInSeconds"], "RECHECK_INVALID", integer=True, nullable=True, minimum=1, maximum=10000)
    expected_deadline = settings["maxWaitSeconds"] - value["orderAgeSeconds"]
    if value["latestDispatchInSeconds"] is not None:
        expected_deadline = min(expected_deadline, value["latestDispatchInSeconds"])
    require(target["hardDeadlineInSeconds"] == expected_deadline, "DEADLINE_CONTRACT_MISMATCH")
    allowed = target["allowedActions"]
    require(type(allowed) is list and allowed and all(type(x) is str and x in ACTIONS for x in allowed)
            and len(set(allowed)) == len(allowed) and target["actionCode"] in allowed, "ALLOWED_ACTIONS_INVALID")
    require(allowed == (["Collect", "DispatchNow"] if target["actionCode"] == "Collect" else [target["actionCode"]]),
            "ALLOWED_ACTIONS_INVALID")
    if target["actionCode"] == "Collect":
        require(target["recheckInSeconds"] is not None
                and target["recheckInSeconds"] <= target["hardDeadlineInSeconds"] - settings["deadlineBufferSeconds"]
                and target["recheckInSeconds"] <= settings["collectionSeconds"] - value["orderAgeSeconds"], "RECHECK_EXCEEDS_DEADLINE")
    else:
        require(target["recheckInSeconds"] is None, "UNEXPECTED_RECHECK")
    return [{"role": "system", "content": SYSTEM},
            {"role": "user", "content": canonical({"taskCode": "DispatchTiming", "input": value})},
            {"role": "assistant", "content": canonical(target)}]


def ensure_ignored(path):
    result = subprocess.run(["git", "check-ignore", "-q", "--", str(path)], cwd=REPO, capture_output=True, check=False)
    require(result.returncode == 0, "OUTPUT_NOT_GIT_IGNORED")


def make_review(rows, records):
    family_names = {"collect": "짧게 수집", "combination-ready": "조합 발견", "collection-expired": "수집 시간 종료",
                    "urgent": "지연 위험", "sparse": "추가 주문 희박", "unknown": "자료 미확인",
                    "no-driver": "가용 기사 없음", "retry": "재추천"}
    action_names = {"Collect": "잠시 수집", "DispatchNow": "기존 배차 진행", "ExistingFlow": "기존 흐름 유지"}
    reason_names = {"ShortCollectionWindow": "짧은 수집 여유가 남음", "PromisingPlanAvailable": "유망한 조합을 찾음",
                    "CompatibleOrderThresholdReached": "호환 주문 기준 충족", "CollectionElapsed": "수집 시간이 끝남",
                    "DeadlineApproaching": "고정 마감이 임박함", "DelayRisk": "지연 위험을 확인함",
                    "NoExpectedNewOrders": "추가 주문의 근거가 없음", "DemandUnknown": "수요 자료가 미확인",
                    "NoAvailableDrivers": "현재 제안 가능한 기사가 없음", "Retry": "재추천은 새 수집 대기 제외"}
    chosen = []
    for family in FAMILIES:
        for district in DISTRICTS:
            item = next((row for row in rows if row["scenarioFamily"] == family
                         and records[row["sourceRecordIds"][0]]["district"] == district), None)
            if item:
                chosen.append(item)
    lines = ["# 배차 시점 학습 후보 검토 r52", "", "모든 답안은 현재 정책 초안이며 사람 검토 대기입니다. ready 자료는 0행입니다.",
             "실제 주문 도착·기사 상황·고객 위치를 재현한 자료가 아닙니다. 평균 단가 4,000원 목표도 포함하지 않습니다.", "",
             "| 번호 | 상황 | 기다린 시간(초) | 호환 주문 | 가용 기사 | 예상 신규 주문 | 정책 행동 | 재검토까지(초) | 사유 |", "| --- | --- | ---: | ---: | ---: | ---: | --- | ---: | --- |"]
    for i, row in enumerate(chosen, 1):
        x, y = row["input"], row["target"]
        lines.append(f"| {i} | {family_names[row['scenarioFamily']]} | {x['orderAgeSeconds']} | {x['compatibleOrders']} | {x['availableDrivers']} | "
                     f"{x['expectedNewOrders'] if x['expectedNewOrders'] is not None else '미확인'} | {action_names[y['actionCode']]} | "
                     f"{y['recheckInSeconds'] if y['recheckInSeconds'] is not None else '-'} | {reason_names.get(y['reasonCode'], y['reasonCode'])} |")
    lines.extend(["", "번호별로 기다림/즉시 진행 판단과 대기 상한이 적절한지 검토합니다. 의견만으로 자동 승인하거나 학습하지 않습니다.", "",
                  "## 출처와 검토 연결", ""])
    lines.extend(f"- {i}. `{row['caseId']}` · {row['split']} · {records[row['sourceRecordIds'][0]]['district']} · Pending"
                 for i, row in enumerate(chosen, 1))
    return ("\n".join(lines) + "\n").encode("utf-8"), len(chosen)


def compile_materials(raw_path, dataset_path, output=None):
    raw_path = safe_path(raw_path, ROOT / "raw")
    require(raw_path.name == "raw.jsonl" and raw_path.is_file(), "RAW_PATH_INVALID")
    raw = raw_path.read_bytes()
    manifest = parse_json(raw_path.with_name("raw.manifest.json").read_bytes())
    dataset_path, dataset_raw, metadata, records = load_dataset(dataset_path)
    checked = validate_manifest(manifest, raw, dataset_path, dataset_raw, metadata)
    require(raw_path.parent.name == manifest["generatorFingerprint"], "RAW_FINGERPRINT_PATH_MISMATCH")
    rows, entries, ids = [], [], set()
    for line in raw.splitlines():
        require(line.strip(), "BLANK_RAW_LINE")
        row = parse_json(line)
        messages = validate_row(row, records)
        require(row["caseId"] not in ids, "CASE_DUPLICATED")
        ids.add(row["caseId"]); rows.append(row)
        audit = {k: row[k] for k in ("caseId", "scenarioFamily", "sourceRecordIds", "sourceBranchIds", "split", "teacher", "provenance", "review")}
        audit.update(inputSha256=digest(json_bytes(messages[:2])), targetSha256=digest(json_bytes(row["target"])),
                     rawRowSha256=digest(line), district=records[row["sourceRecordIds"][0]]["district"])
        entries.append({"messages": messages, "auditMetadata": audit})
    require(len(rows) == manifest["caseCount"], "CASE_COUNT_MISMATCH")
    groups = collections.defaultdict(list)
    for item in entries:
        groups[item["auditMetadata"]["inputSha256"]].append(item)
    candidates, quarantine = [], []
    for group in groups.values():
        if len(group) == 1:
            candidates.extend(group)
            continue
        reasons = ["DuplicateInput"]
        if len({x["auditMetadata"]["split"] for x in group}) > 1:
            reasons.append("CrossSplitInput")
        if len({x["auditMetadata"]["targetSha256"] for x in group}) > 1:
            reasons.append("ConflictingTarget")
        quarantine.extend({**item, "quarantineReasons": reasons} for item in group)
    compiler_sha = digest(Path(__file__).read_bytes())
    fingerprint = digest(json_bytes({"rawSha256": digest(raw), "rawManifestSha256": digest(raw_path.with_name("raw.manifest.json").read_bytes()),
                                     "datasetSha256": DATASET_SHA, "compilerSha256": compiler_sha, "formatVersion": FORMAT}))
    target = safe_path(ROOT / "compiled" / fingerprint, ROOT / "compiled")
    if output is not None:
        require(safe_path(output, ROOT / "compiled") == target, "OUTPUT_FINGERPRINT_PATH_REQUIRED")
    ensure_ignored(target / "summary.json")
    payloads = {}
    for split in SPLITS:
        payloads[f"candidates/{split}.jsonl"] = b"".join(json_bytes(x) for x in candidates if x["auditMetadata"]["split"] == split)
        payloads[f"ready/{split}.jsonl"] = b""
    payloads["quarantine.jsonl"] = b"".join(json_bytes(x) for x in quarantine)
    retained_ids = {x["auditMetadata"]["caseId"] for x in candidates}
    payloads["review.md"], review_count = make_review([row for row in rows if row["caseId"] in retained_ids], records)
    summary = {"schemaVersion": 1, "formatVersion": FORMAT, "fingerprint": fingerprint,
               "generatorFingerprint": manifest["generatorFingerprint"], "datasetSha256": DATASET_SHA,
               "rawSha256": digest(raw), "compilerSha256": compiler_sha, "designSha256": DESIGN_SHA,
               "rawCases": len(rows), "candidateCases": len(candidates), "quarantinedCases": len(quarantine),
               "candidateSplits": dict(collections.Counter(x["auditMetadata"]["split"] for x in candidates)),
               "candidateFamilyCounts": dict(collections.Counter(x["auditMetadata"]["scenarioFamily"] for x in candidates)),
               "candidateActions": dict(collections.Counter(parse_json(x["messages"][2]["content"])["actionCode"] for x in candidates)),
               "quarantineReasonCounts": dict(collections.Counter(reason for x in quarantine for reason in x["quarantineReasons"])),
               "reviewRepresentatives": review_count, "reviewStatus": "Pending", "teacherKind": "CurrentPolicyDraft", "optimalityProven": False,
               "materialPurpose": "PurePolicyReviewDraft", "runtimePromptCompatible": False, "runtimeTrainingAdapterPrepared": False,
               "readySplits": {s: 0 for s in SPLITS}, "humanApprovals": 0, "eligibleForTraining": False,
               "modelCalls": 0, "weightsTrained": False, "operationalWrites": 0,
               "historicalTimingObserved": False, "realCustomerLocationsUsed": 0, "realDriverLocationsUsed": 0,
               "averageFeeTargetImplemented": False, "splitRule": SPLIT_RULE, "sourceHashes": manifest["sourceHashes"],
               "fileHashes": {name: digest(data) for name, data in payloads.items()}}
    payloads["summary.json"] = json_bytes(summary)
    if target.exists():
        present = {p.relative_to(target).as_posix() for p in target.rglob("*") if p.is_file()}
        require(present == set(payloads), "EXISTING_OUTPUT_SET_CHANGED")
        for name, data in payloads.items():
            path = safe_path(target / name, ROOT / "compiled")
            require(path.read_bytes() == data, "EXISTING_OUTPUT_CHANGED")
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix=".compile-", dir=target.parent) as temp:
            staging = Path(temp)
            for name, data in payloads.items():
                path = staging / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            verify_sources(checked)
            staging.rename(target)
    verify_sources(checked)
    return target, summary


def verify_sources(checked):
    for path, expected in checked:
        require(digest(path.read_bytes()) == expected, "SOURCE_CHANGED_DURING_COMPILATION")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command")
    compile_parser = sub.add_parser("compile")
    compile_parser.add_argument("--raw", required=True)
    compile_parser.add_argument("--dataset", required=True)
    compile_parser.add_argument("--output")
    args = parser.parse_args()
    if args.command is None:
        print("No preparation, dispatch, model execution or training. Use compile --raw <path> --dataset <path>.")
        return 0
    try:
        target, summary = compile_materials(args.raw, args.dataset, args.output)
        print(json.dumps({"output": str(target), "candidateCases": summary["candidateCases"],
                          "quarantinedCases": summary["quarantinedCases"], "readyRows": 0, "modelCalls": 0, "weightsTrained": False}))
        return 0
    except (MaterialFailure, OSError, KeyError, TypeError) as ex:
        code = str(ex) if isinstance(ex, MaterialFailure) else type(ex).__name__
        print(json.dumps({"error": "TimingMaterialPreparationFailed", "code": code}), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
