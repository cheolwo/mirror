"""실제 배달권 후보 정책의 합성 실행을 Ollama와 비교하는 읽기 전용 검토 도구."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import time
import urllib.request
import uuid

import dispatch_advisor as transport

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path("artifacts/local/local-dispatch-ai/candidate-advisory-r47")
RULES = Path("eng/local-dispatch-ai/candidate-project-rules.json")
MAX_ATTEMPTS = 4
MAX_FILE = 8 * 1024 * 1024
HEX = re.compile(r"[0-9a-f]{64}\Z")
DRIVER = re.compile(r"SYN-D\d{3}\Z")
CASE = re.compile(r"SYN-CANDIDATE-[A-Z0-9-]{1,90}\Z")
REASONS = {
    "ENGINE_ORDER": "기존 엔진의 우선순위를 유지합니다.",
    "SAME_AREA": "같은 배달권에서 검토한 후보입니다.",
    "ADJACENT_AREA": "인접 배달권에서 검토한 후보입니다.",
    "BOUNDED_EXPANSION": "제한된 거리 확장에서 검토한 후보입니다.",
    "LONGER_WAIT": "허용 후보 중 대기 점수가 가장 높은 후보입니다.",
    "NO_ELIGIBLE_CANDIDATE": "현재 허용 후보가 없습니다.",
    "LIMITED_DATA": "미확인 자료를 보완한 뒤 다시 검토해야 합니다.",
}


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def encoded(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"),
                      allow_nan=False).encode("utf-8")


def inside(root: Path, relative: str | Path) -> Path:
    result = (root / relative).resolve()
    if not result.is_relative_to(root.resolve()):
        raise ValueError("path_outside_allowed_root")
    return result


def read(path: Path) -> bytes:
    with path.open("rb") as stream:
        raw = stream.read(MAX_FILE + 1)
    if len(raw) > MAX_FILE:
        raise ValueError("file_too_large")
    return raw


def decode(raw: bytes):
    return transport.parse_json(raw.decode("utf-8-sig"))


def write_new(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as stream:
        stream.write(encoded(value))


def load_rules(root=ROOT):
    raw = read(inside(root, RULES))
    value = decode(raw)
    if value.get("schemaVersion") != 1 or value.get("policyVersion") != "food-candidate-review/r47":
        raise ValueError("rules_version_mismatch")
    rules = value["rules"]
    if len({x["id"] for x in rules}) != len(rules):
        raise ValueError("duplicate_rule")
    hashes = {}
    for rule in rules:
        if rule["status"] not in {"implemented", "user_preference", "proposed", "simulation_assumption"}:
            raise ValueError("invalid_rule_status")
        for source in rule["sources"]:
            # Rules are repository-owned; do not accept paths from a model or evidence payload.
            path = source["path"]
            if path not in SOURCE_PATHS | {PREFERENCES_PATH}:
                raise ValueError("unexpected_rule_source")
            source_raw = read(inside(root, path))
            if not all(anchor in source_raw.decode("utf-8-sig") for anchor in source["anchors"]):
                raise ValueError("rule_anchor_stale")
            hashes[path] = sha(source_raw)
    return rules, {"rulesSha256": sha(raw), "sourceHashes": hashes}


# Kept explicit so an edited evidence file cannot request arbitrary project/private files.
PREFERENCES_PATH = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/배차-사례-문답검토절차.md"
SOURCE_PATHS = {
    "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달후보검토결과.cs",
    "Ssalddel/Services/Dispatch/Queue/기사위치신선도정책.cs",
    "Ssalddel/Services/Dispatch/Queue/QueueOptions.cs",
    "Ssalddel/Services/Dispatch/Coordination/음식배달권정책.cs",
    "Ssalddel/Services/Dispatch/Coordination/배달권실행공간Store.cs",
    "Ssalddel/Services/Dispatch/Recommendation/배차추천경로Service.cs",
    "Ssalddel/Services/Dispatch/Common/운영배차공통UseCase.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달수요참여Service.cs",
    "Ssalddel.WorkflowRules/UnityPackage/Runtime/음식배달픽업평가Policy.cs",
    "Ssalddel.Domain/배차/음식배달수요참여Policy.cs",
    "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs",
    "Ssalddel.Domain/배차/운영배차수신상태Policy.cs",
    "Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs",
    "eng/local-dispatch-ai/CandidateScenarioRunner/Program.cs",
    "eng/local-dispatch-ai/CandidateScenarioRunner/CandidateScenarioRunner.csproj",
    "eng/local-dispatch-ai/CandidateScenarioRunner/fixture.json",
}


def validate_case(case):
    if not CASE.fullmatch(case.get("caseId", "")):
        raise ValueError("invalid_case_id")
    drivers = case["input"]["drivers"]
    driver_ids = [x["driverId"] for x in drivers]
    if len(driver_ids) != len(set(driver_ids)) or not all(DRIVER.fullmatch(x) for x in driver_ids):
        raise ValueError("invalid_synthetic_drivers")
    trace = case["trace"]
    allowed = trace["최종허용기사Ids"]
    if len(allowed) != len(set(allowed)) or not set(allowed) <= set(driver_ids):
        raise ValueError("invalid_allowed_candidates")
    rows = trace["후보목록"]
    if len({x["기사Id"] for x in rows}) != len(rows):
        raise ValueError("duplicate_candidate_trace")
    if not all(x["기사Id"] in driver_ids for x in rows):
        raise ValueError("unknown_candidate_trace")
    if any(not any(row["기사Id"] == identifier and row["판정Code"] == "CandidateAllowed" for row in rows)
           for identifier in allowed):
        raise ValueError("allowed_candidate_not_eligible")
    if set(allowed) != {row["기사Id"] for row in rows if row["판정Code"] == "CandidateAllowed"}:
        raise ValueError("allowed_candidate_trace_mismatch")
    chosen = trace["선정후보"]
    if (chosen is None) != (not allowed) or (chosen is not None and chosen["driverId"] not in allowed):
        raise ValueError("baseline_not_allowed")
    if not case["checks"] or not all(x["passed"] is True for x in case["checks"]):
        raise ValueError("scenario_check_failed")


def load_evidence(root=ROOT):
    folder = inside(root, OUTPUT)
    raw = read(folder / "evidence.json")
    value = decode(raw)
    if (value.get("schemaVersion") != 1 or value.get("synthetic") is not True
            or value.get("policyVersion") != "food-candidate-review/r47"
            or value.get("checksPassed") is not True
            or any(type(value.get(key)) is not int or value[key] != 0
                   for key in ("modelCalls", "networkCalls", "operationalWrites"))):
        raise ValueError("invalid_candidate_evidence")
    if set(value["sourceHashes"]) != SOURCE_PATHS:
        raise ValueError("unexpected_source_paths")
    for path, digest in value["sourceHashes"].items():
        if not HEX.fullmatch(digest) or sha(read(inside(root, path))) != digest:
            raise ValueError("candidate_source_stale")
    if value.get("fixtureFile") != "fixture.json" or value.get("casesFile") != "cases.json":
        raise ValueError("unexpected_evidence_file")
    fixture = read(folder / "fixture.json")
    cases = read(folder / "cases.json")
    if (sha(fixture) != value["fixtureSha256"] or sha(cases) != value["casesDigest"]
            or decode(cases) != value["cases"]):
        raise ValueError("candidate_digest_mismatch")
    if sha(fixture) != value["sourceHashes"]["eng/local-dispatch-ai/CandidateScenarioRunner/fixture.json"]:
        raise ValueError("candidate_fixture_source_mismatch")
    fixture_spec = decode(fixture)
    fixture_cases = {x["caseId"]: x for x in fixture_spec["cases"]}
    if (fixture_spec.get("synthetic") is not True or len(fixture_cases) != len(fixture_spec["cases"])
            or set(fixture_cases) != {x["caseId"] for x in value["cases"]}):
        raise ValueError("candidate_fixture_case_mismatch")
    for case in value["cases"]:
        if ({x["driverId"] for x in case["input"]["drivers"]}
                != {x["driverId"] for x in fixture_cases[case["caseId"]]["drivers"]}):
            raise ValueError("candidate_fixture_driver_mismatch")
    if not HEX.fullmatch(value.get("evidenceId", "")):
        raise ValueError("invalid_evidence_id")
    immutable = inside(folder, Path("runs") / value["evidenceId"])
    if any(read(immutable / name) != content for name, content in
           (("evidence.json", raw), ("fixture.json", fixture), ("cases.json", cases))):
        raise ValueError("immutable_evidence_mismatch")
    if not 1 <= len(value["cases"]) <= 24 or len({x["caseId"] for x in value["cases"]}) != len(value["cases"]):
        raise ValueError("invalid_case_count")
    for case in value["cases"]:
        validate_case(case)
    return value, raw


def baseline(case):
    selected = case["trace"]["선정후보"]
    return None if selected is None else selected["driverId"]


def snapshot(case, evidence_digest, rules_meta):
    return sha(encoded({"case": case, "evidenceSha256": evidence_digest, "rules": rules_meta}))


def model_context(case, rules):
    trace = case["trace"]
    # Facts are explicitly projected. No addresses, customer data, source files or future arrival tables.
    candidates = [{key: row[key] for key in
                   ("기사Id", "공간단계", "판정Code", "거리Km", "픽업단계", "도착차이분",
                    "추천점수", "대기점수", "수요우선순위", "수요사유Code")} for row in trace["후보목록"]]
    demand = case["demandResult"]
    source = case["input"].get("demandInput")
    settings = case["input"].get("demandSettings")
    return {
        "caseId": case["caseId"], "primaryArea": trace["주배달권키"],
        "queriedAreas": trace["조회배달권키목록"], "candidates": candidates,
        "allowedCandidateIds": trace["최종허용기사Ids"], "baselineCandidateId": baseline(case),
        "currentDemand": trace["수요자료최신"], "demandFallback": trace["수요예외사용"],
        "pickupFallback": trace["픽업적시성예외사용"],
        "demand": None if demand is None else {key: demand[key] for key in
            ("데이터상태Code", "목표활동인원", "예상업무량건수", "기존기사가용처리건수", "보호활동기사수", "추가참여인원")},
        "observedDemand": None if source is None else {key: source[key] for key in
            ("대기주문수", "최근시간창신규주문수", "관측시간창분", "향후수요예측건수")},
        "planningAssumptions": None if settings is None else {key: settings[key] for key in
            ("시간당목표처리건수", "계획시간창분")},
        "driverWork": [{key: item[key] for key in
            ("driverId", "existingActivity", "waitMinutes", "remainingMinutes", "recentAssignments")}
            for item in case["input"]["drivers"]],
        "rules": [{key: rule[key] for key in ("id", "status", "summary")} for rule in rules],
        "limitations": {"syntheticCoordinates": True, "roadRouteVerified": False,
                        "pickupAnchor": "current_gps", "futureDestinationAnchor": "proposed_only",
                        "finalAuthority": "server", "operationalEffect": False},
    }


def build_request(case, rules, digest):
    expected = {"schemaVersion": 1, "caseId": case["caseId"], "snapshotSha256": digest,
                "advisoryOnly": True, "baselineCandidateId": baseline(case)}
    props = {key: {"const": value} for key, value in expected.items()}
    props.update({
        "decisionCode": {"type": "string", "enum": ["retain_engine", "review_alternative", "insufficient_data"]},
        "selectedCandidateId": {"enum": case["trace"]["최종허용기사Ids"] + [None]},
        "reasonCodes": {"type": "array", "minItems": 1, "maxItems": 3, "uniqueItems": True,
                        "items": {"enum": list(REASONS)}},
        "sourceIds": {"type": "array", "minItems": 1, "maxItems": len(rules), "uniqueItems": True,
                      "items": {"enum": [x["id"] for x in rules]}},
    })
    schema = {"type": "object", "additionalProperties": False, "required": list(props), "properties": props}
    instructions = (
        "Review a SYNTHETIC food-delivery candidate decision, with no actions or tools. "
        "Return only schema JSON. Copy constants exactly. Choose only allowedCandidateIds, never excluded or unqueried drivers. "
        "retain_engine must select baselineCandidateId. review_alternative must select a different allowed non-null driver. "
        "insufficient_data must select null. No invented distances, ETA, prices, confidence or free prose. "
        "ENGINE_ORDER means retain_engine; SAME_AREA/ADJACENT_AREA/BOUNDED_EXPANSION requires selected spatial stage 0/1/2. "
        "LONGER_WAIT requires highest aging score among allowed candidates, and is only a review cue, not proven fairness. "
        "NO_ELIGIBLE_CANDIDATE requires empty allowedCandidateIds; LIMITED_DATA requires no current demand or unknown remaining time. "
        "Demand/capacity matters before time-of-day; prefer a longer waiter only under comparable feasible pickup conditions. "
        "User preferences and proposed rules are not implemented policies. Do not infer future destination routing is implemented. "
        "Include AUTHORITY and relevant implemented source IDs. The server baseline remains effective, any alternative awaits human review."
    )
    context = model_context(case, rules)
    context["snapshotSha256"] = digest
    return {"model": transport.MODEL, "stream": False, "think": False, "keep_alive": 0,
            "format": schema, "options": {"temperature": 0, "seed": 47, "num_ctx": 8192, "num_predict": 384},
            "messages": [{"role": "system", "content": instructions},
                         {"role": "user", "content": json.dumps(context, ensure_ascii=False, separators=(",", ":"))}]}


def validate_advice(value, case, rules, digest):
    schema = build_request(case, rules, digest)["format"]
    if not isinstance(value, dict) or set(value) != set(schema["required"]):
        raise ValueError("invalid_advice_fields")
    for key, prop in schema["properties"].items():
        if "const" in prop and encoded(value[key]) != encoded(prop["const"]):
            raise ValueError("snapshot_or_baseline_changed")
    decision, chosen = value["decisionCode"], value["selectedCandidateId"]
    if decision not in schema["properties"]["decisionCode"]["enum"]:
        raise ValueError("invalid_decision_code")
    if chosen not in case["trace"]["최종허용기사Ids"] + [None]:
        raise ValueError("candidate_not_allowed")
    if (decision == "retain_engine" and chosen != baseline(case)
            or decision == "review_alternative" and (chosen is None or chosen == baseline(case))
            or decision == "insufficient_data" and chosen is not None):
        raise ValueError("decision_selection_mismatch")
    for key, allowed, upper in (("reasonCodes", set(REASONS), 3),
                               ("sourceIds", {x["id"] for x in rules}, len(rules))):
        items = value[key]
        if (not isinstance(items, list) or not 1 <= len(items) <= upper
                or not all(isinstance(x, str) for x in items)
                or len(set(items)) != len(items) or not set(items) <= allowed):
            raise ValueError("invalid_reason_or_source_codes")
    if "AUTHORITY" not in value["sourceIds"]:
        raise ValueError("authority_source_required")
    rows = [x for x in case["trace"]["후보목록"] if x["기사Id"] in case["trace"]["최종허용기사Ids"]]
    selected = next((x for x in rows if x["기사Id"] == chosen), None)
    base_row = next((x for x in rows if x["기사Id"] == baseline(case)), None)
    if (decision == "review_alternative" and selected is not None and base_row is not None
            and selected["픽업단계"] != base_row["픽업단계"]):
        raise ValueError("alternative_must_preserve_pickup_stage")
    for reason in value["reasonCodes"]:
        if reason == "ENGINE_ORDER" and decision != "retain_engine":
            raise ValueError("reason_conflicts_with_facts")
        if reason in {"SAME_AREA", "ADJACENT_AREA", "BOUNDED_EXPANSION"}:
            stage = {"SAME_AREA": 0, "ADJACENT_AREA": 1, "BOUNDED_EXPANSION": 2}[reason]
            if selected is None or selected["공간단계"] != stage:
                raise ValueError("reason_conflicts_with_facts")
        if reason == "LONGER_WAIT" and (selected is None or selected["대기점수"] is None
                or any(row["대기점수"] is not None and row["대기점수"] > selected["대기점수"] for row in rows)):
            raise ValueError("reason_conflicts_with_facts")
        if reason == "NO_ELIGIBLE_CANDIDATE" and case["trace"]["최종허용기사Ids"]:
            raise ValueError("reason_conflicts_with_facts")
        if reason == "LIMITED_DATA" and case["trace"]["수요자료최신"] and all(
                x["remainingMinutes"] is not None for x in case["input"]["drivers"]):
            raise ValueError("reason_conflicts_with_facts")
    source_requirements = {
        "ENGINE_ORDER": "AREA_SEARCH", "SAME_AREA": "AREA_SEARCH", "ADJACENT_AREA": "AREA_SEARCH",
        "BOUNDED_EXPANSION": "AREA_SEARCH", "LONGER_WAIT": "WAITING_ORDER",
        "NO_ELIGIBLE_CANDIDATE": "ELIGIBILITY", "LIMITED_DATA": "DEMAND_CAPACITY",
    }
    if not {source_requirements[code] for code in value["reasonCodes"]} <= set(value["sourceIds"]):
        raise ValueError("reason_source_required")
    return value


def reserve_attempt(folder, run_id, case_id, request_digest):
    """호출 전에 기록한다. 중단·시간초과도 예산을 쓰며 여러 프로세스는 배타 잠금으로 직렬화한다."""
    attempts = folder / "attempts"
    attempts.mkdir(parents=True, exist_ok=True)
    lock = attempts / "reservation.lock"
    lease = lock.open("x", encoding="utf-8")
    try:
        with lease:
            files = list(attempts.glob("attempt-*.json"))
            for path in files:
                decode(read(path))  # damaged budget ledger is not silently ignored
            if len(files) >= MAX_ATTEMPTS:
                raise ValueError("candidate_model_attempt_budget_exhausted")
            record = {"schemaVersion": 1, "runId": run_id, "caseId": case_id,
                      "requestSha256": request_digest, "reservedAtUtc": datetime.now(timezone.utc).isoformat()}
            write_new(attempts / f"attempt-{len(files)+1:03d}.json", record)
            return len(files) + 1
    finally:
        lock.unlink()


def model_metadata(endpoint, timeout):
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), transport.NoRedirects())
    with opener.open(transport.validate_endpoint(endpoint) + "/api/tags", timeout=timeout) as response:
        raw = response.read(transport.MAX_RESPONSE_BYTES + 1)
    if len(raw) > transport.MAX_RESPONSE_BYTES:
        raise ValueError("model_metadata_too_large")
    tags = decode(raw)
    models = [x for x in tags["models"] if x.get("name") == transport.MODEL]
    if len(models) != 1 or not HEX.fullmatch(models[0].get("digest", "").removeprefix("sha256:")):
        raise ValueError("model_manifest_missing")
    return {"name": transport.MODEL, "manifestDigest": models[0]["digest"]}


def report(result):
    lines = ["# 배달권 후보 AI 비교", "", "합성 사례이며 실제 배차·상태 변경·학습은 실행하지 않았습니다.", "",
             "| 사례 | 엔진 기준 | AI 검토 후보 | 결과 |", "| --- | --- | --- | --- |"]
    for item in result["cases"]:
        advice = item.get("advice")
        lines.append(f'| {item["caseId"]} | {item["effectiveCandidateId"] or "없음"} | '
                     f'{(advice or {}).get("selectedCandidateId") or "없음"} | {item["status"]} |')
    for item in result["cases"]:
        facts = item["facts"]
        lines.extend(["", f'## {item["caseId"]}', "", f'주배달권 `{facts["primaryArea"]}` · 허용 후보 {len(facts["allowedCandidateIds"])}명', "",
                      "| 후보 | 공간 단계 | 판정 | 접근 거리(km) | 픽업 단계 | 대기 점수 |", "| --- | --- | --- | --- | --- | --- |"])
        for candidate in facts["candidates"]:
            lines.append("| " + " | ".join(str(candidate[key]) if candidate[key] is not None else "미확인" for key in
                ("기사Id", "공간단계", "판정Code", "거리Km", "픽업단계", "대기점수")) + " |")
        if item.get("advice"):
            lines.extend(["", *[f'- {REASONS[x]}' for x in item["advice"]["reasonCodes"]]])
        if item.get("failureCode"):
            lines.extend(["", f'기준 유지 사유: `{item["failureCode"]}`'])
    lines.extend(["", "구조·사실 관문 통과는 판단 정확도나 사람의 승인이 아닙니다. 대안은 검토 대기이며 엔진 기준을 유지합니다."])
    return "\n".join(lines) + "\n"


def run(execute=False, endpoint="http://127.0.0.1:12681", timeout=60, case_ids=None, root=ROOT):
    transport.validate_endpoint(endpoint)
    if not 0 < timeout <= 60:
        raise ValueError("timeout_must_be_at_most_60_seconds")
    evidence, raw = load_evidence(root)
    rules, rules_meta = load_rules(root)
    cases = evidence["cases"]
    if case_ids:
        if len(case_ids) != len(set(case_ids)) or not set(case_ids) <= {x["caseId"] for x in cases}:
            raise ValueError("unknown_or_duplicate_case_selection")
        cases = [x for x in cases if x["caseId"] in case_ids]
    if execute and len(cases) > MAX_ATTEMPTS:
        raise ValueError("select_at_most_four_cases_for_local_run")
    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:12]
    folder = inside(root, OUTPUT)
    output = folder / "runs" / run_id
    output.mkdir(parents=True)
    result = {"schemaVersion": 1, "runId": run_id, "mode": "local_model" if execute else "dry_run",
              "evidenceSha256": sha(raw), "rules": rules_meta,
              "toolHashes": {name: sha(read(inside(root, Path("eng/local-dispatch-ai") / name)))
                             for name in ("candidate_advisor.py", "dispatch_advisor.py")},
              "modelCalls": 0, "operationalWrites": 0, "humanApprovals": 0, "trainingWrites": 0, "cases": []}
    if execute:
        result["model"] = model_metadata(endpoint, timeout)
    for case in cases:
        digest = snapshot(case, sha(raw), rules_meta)
        request = build_request(case, rules, digest)
        request_raw = encoded(request)
        write_new(output / f'{case["caseId"]}.request.json', request)
        item = {"caseId": case["caseId"], "snapshotSha256": digest, "requestSha256": sha(request_raw),
                "effectiveCandidateId": baseline(case), "advice": None, "humanReview": "pending",
                "status": "not_requested", "facts": model_context(case, rules)}
        if execute:
            attempt = reserve_attempt(folder, run_id, case["caseId"], sha(request_raw))
            item["attempt"] = attempt
            started = time.monotonic()
            result["modelCalls"] += 1
            def sink(response, identifier=case["caseId"]):
                with (output / f"{identifier}.response.json").open("xb") as stream:
                    stream.write(response)
                item["responseSha256"] = sha(response)
            try:
                advice, metrics = transport.request_local_json(endpoint, request, timeout, response_sink=sink)
                # A policy/preferences edit during inference invalidates the old snapshot.
                current, current_raw = load_evidence(root)
                current_rules, current_meta = load_rules(root)
                current_tools = {name: sha(read(inside(root, Path("eng/local-dispatch-ai") / name)))
                                 for name in result["toolHashes"]}
                if (current_raw != raw or current_meta != rules_meta or current_rules != rules
                        or current_tools != result["toolHashes"]
                        or model_metadata(endpoint, timeout) != result["model"]):
                    raise ValueError("candidate_snapshot_stale")
                item["advice"] = validate_advice(advice, case, rules, digest)
                item["metrics"] = metrics
                item["status"] = "validated_advisory_only"
            except (ValueError, KeyError, TypeError, OSError) as error:
                item["status"] = "engine_baseline_retained"
                item["failureCode"] = str(error) if isinstance(error, ValueError) else type(error).__name__
            item["elapsedSeconds"] = round(time.monotonic() - started, 3)
        result["cases"].append(item)
        write_new(output / f'{case["caseId"]}.result.json', item)
    write_new(output / "results.json", result)
    with (output / "검토표.md").open("x", encoding="utf-8") as stream:
        stream.write(report(result))
    return {"output": str(output), "modelCalls": result["modelCalls"],
            "validated": sum(x["status"] == "validated_advisory_only" for x in result["cases"]),
            "fallback": sum(x["status"] == "engine_baseline_retained" for x in result["cases"]),
            "cases": len(cases), "humanApprovals": 0, "operationalWrites": 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--case", action="append", dest="case_ids")
    parser.add_argument("--endpoint", default="http://127.0.0.1:12681")
    parser.add_argument("--timeout", type=float, default=60)
    args = parser.parse_args()
    print(json.dumps(run(**vars(args)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
