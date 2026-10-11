"""합성 시나리오의 실제 정책 결과와 프로젝트 규칙을 참고하는 로컬 설명 도구."""
from __future__ import annotations

import argparse
from collections import Counter
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
CAMPAIGN = Path("artifacts/local/local-dispatch-scenarios-r45/campaign")
OUTPUT = Path("artifacts/local/local-dispatch-ai/project-advisory-r46")
RULES = Path("eng/local-dispatch-ai/project-rules.json")
MAX_FILE = 8 * 1024 * 1024
SOURCE_PATHS = {
    "Ssalddel.Domain/배차/음식배달수요참여Policy.cs",
    "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs",
    "eng/local-dispatch-ai/ScenarioRunner/ScenarioCampaign.cs",
    "eng/local-dispatch-ai/ScenarioRunner/ScenarioSelfChecks.cs",
    "eng/local-dispatch-ai/ScenarioRunner/Program.cs",
    "eng/local-dispatch-ai/ScenarioRunner/ScenarioAdvisoryEvidence.cs",
}
RULE_PATHS = SOURCE_PATHS | {
    "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs",
    "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/offline-scenarios.implementation.r45.md",
}
ALTERNATIVES = {"observed", "policy_off", "over", "oracle"}
HEX = re.compile(r"[0-9a-f]{64}\Z")
CASE_ID = re.compile(r"SYN-[A-Z0-9-]{1,100}\Z")


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def pairs(items):
    value = {}
    for key, item in items:
        if key in value:
            raise ValueError("duplicate_json_key")
        value[key] = item
    return value


def decode(raw: bytes):
    def invalid(value):
        raise ValueError("nonfinite_json_number")
    return json.loads(raw.decode("utf-8-sig"), object_pairs_hook=pairs, parse_constant=invalid)


def read(path: Path, limit=MAX_FILE) -> bytes:
    with path.open("rb") as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit:
        raise ValueError("evidence_file_too_large")
    return raw


def inside(root: Path, relative: str | Path) -> Path:
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("path_outside_allowed_root")
    return path


def integer(value, low=0, high=100000):
    if type(value) is not int or not low <= value <= high:
        raise ValueError("invalid_integer")
    return value


def load_evidence(root=ROOT) -> tuple[dict, bytes]:
    """임의 파일 입력은 제공하지 않는다. 실제 생성기 실행 근거와 현재 소스를 대조한다."""
    folder = inside(root, CAMPAIGN)
    raw = read(folder / "advisory-input.json")
    value = decode(raw)
    if (value.get("schemaVersion") != 1 or value.get("syntheticOnly") is not True
            or value.get("policyVersion") != "food-demand-participation/v1"
            or not HEX.fullmatch(value.get("campaignEvidenceId", ""))):
        raise ValueError("invalid_synthetic_evidence")
    run = inside(folder, Path("runs") / value["campaignEvidenceId"])
    if read(run / "advisory-input.json") != raw:
        raise ValueError("canonical_evidence_mismatch")
    cases_raw = read(folder / "advisory-cases.json")
    if (value.get("casesFile") != "advisory-cases.json"
            or read(run / "advisory-cases.json") != cases_raw
            or sha(cases_raw) != value.get("casesDigest")
            or decode(cases_raw) != value.get("cases")):
        raise ValueError("case_digest_mismatch")
    original = decode(read(run / "campaign-results.json", 64 * 1024 * 1024))
    if (original.get("evidenceId") != value["campaignEvidenceId"]
            or original.get("campaignDigest") != value.get("campaignDigest")
            or original.get("fixtureSha256") != value.get("fixtureSha256")
            or original.get("modelCalls") != 0 or original.get("operationalWrites") != 0
            or original.get("safetyPassed") is not True
            or original.get("sourceHashes") != value.get("sourceHashes")):
        raise ValueError("campaign_provenance_mismatch")
    source_hashes = value["sourceHashes"]
    if not isinstance(source_hashes, dict) or set(source_hashes) != SOURCE_PATHS:
        raise ValueError("unexpected_source_paths")
    for path, digest in source_hashes.items():
        if sha(read(inside(root, path))) != digest:
            raise ValueError("scenario_source_stale")
    fixture = read(inside(root, "eng/local-dispatch-ai/ScenarioRunner/scenarios.json"))
    if sha(fixture) != value["fixtureSha256"]:
        raise ValueError("scenario_fixture_stale")
    scenarios = {x["id"] for x in decode(fixture)["scenarios"]}
    cases = value["cases"]
    if not isinstance(cases, list) or not 1 <= len(cases) <= 36:
        raise ValueError("invalid_case_count")
    seen = set()
    counts = Counter()
    for case in cases:
        if (not CASE_ID.fullmatch(case.get("caseId", "")) or case["caseId"] in seen
                or case.get("scenarioId") not in scenarios or case.get("planningThroughput") != 3.5):
            raise ValueError("invalid_case_identity")
        seen.add(case["caseId"])
        counts[case["scenarioId"]] += 1
        integer(case["minute"], 0, 479)
        source, decision = case["input"], case["decision"]
        if (source["권역Key"] != case["scenarioId"] or decision["권역Key"] != case["scenarioId"]
                or source["향후수요예측건수"] is not None
                or decision["정책판본"] != value["policyVersion"]
                or source["판정시각Utc"] != decision["판정시각Utc"]):
            raise ValueError("snapshot_policy_mismatch")
        for driver in source["기사목록"]:
            if not re.fullmatch(r"SYN-D\d{3}", driver["기사Id"]):
                raise ValueError("non_synthetic_driver")
        for driver in decision["기사판정목록"]:
            if not re.fullmatch(r"SYN-D\d{3}", driver["기사Id"]):
                raise ValueError("non_synthetic_decision")
        alternatives = case["alternatives"]
        if len(alternatives) != 4 or {x["alternativeId"] for x in alternatives} != ALTERNATIVES:
            raise ValueError("invalid_comparison_set")
        for alternative in alternatives:
            kind = "known_future_upper_bound" if alternative["alternativeId"] == "oracle" else "synthetic_comparison"
            if alternative["kind"] != kind:
                raise ValueError("oracle_is_not_prediction")
    if set(counts) != scenarios or any(x > 3 for x in counts.values()):
        raise ValueError("incomplete_or_excess_scenarios")
    return value, raw


def load_rules(root=ROOT) -> tuple[list[dict], dict]:
    spec_raw = read(inside(root, RULES))
    spec = decode(spec_raw)
    if spec.get("schemaVersion") != 1 or spec.get("policyVersion") != "food-demand-participation/v1":
        raise ValueError("rule_version_mismatch")
    rules = spec["rules"]
    if len(rules) != 8 or len({x["id"] for x in rules}) != 8:
        raise ValueError("invalid_rule_count")
    refs = {}
    for rule in rules:
        if rule["status"] not in {"implemented", "simulation_assumption", "proposed"}:
            raise ValueError("invalid_rule_status")
        for source in rule["sources"]:
            path = source["path"]
            if path not in RULE_PATHS:
                raise ValueError("rule_path_not_allowlisted")
            raw = read(inside(root, path))
            text = raw.decode("utf-8-sig")
            anchors = []
            for anchor in source["anchors"]:
                position = text.find(anchor)
                if position < 0:
                    raise ValueError("rule_anchor_stale")
                anchors.append({"text": anchor, "line": text[:position].count("\n") + 1})
            refs.setdefault(rule["id"], []).append({"path": path, "sha256": sha(raw), "anchors": anchors})
    return rules, {"rulesFileSha256": sha(spec_raw), "sources": refs}


def facts_for(case: dict) -> dict:
    source, decision = case["input"], case["decision"]
    states = Counter(x["상태Code"] for x in decision["기사판정목록"])
    return {"dataStatus": decision["데이터상태Code"], "reasonCode": decision["사유Code"],
            "pendingOrders": source["대기주문수"], "recentOrders": source["최근시간창신규주문수"],
            "targetActiveDrivers": decision["목표활동인원"],
            "protectedDrivers": decision["보호활동기사수"], "additionalDrivers": decision["추가참여인원"],
            "waitingDrivers": states["WaitingForDemand"],
            "longWaitOpportunity": any(x["사유Code"] == "LongWaitOpportunity" for x in decision["기사판정목록"]),
            "orderDelayRisk": source["지연위험주문존재"]}


def relevant_rules(case, rules):
    facts = facts_for(case)
    ids = {"AUTHORITY", "SYNTHETIC_LIMITS", "WAITING_ORDER"}
    ids.add("DATA_FALLBACK" if facts["dataStatus"] != "Current" else "OBSERVED_FORECAST_MAX")
    ids.add("EXCEPTION_OPPORTUNITIES" if facts["longWaitOpportunity"] or facts["orderDelayRisk"] else "INCUMBENT_PROTECTION")
    return [x for x in rules if x["id"] in ids]


def build_request(case: dict, rules: list[dict]) -> dict:
    rules = relevant_rules(case, rules)
    ids = [x["id"] for x in rules]
    facts = facts_for(case)
    text = {"type": "string", "minLength": 1, "maxLength": 120}
    baseline_schema = {"type": "object", "additionalProperties": False,
                       "required": list(facts), "properties": {key: {
                           "type": "null" if value is None else "boolean" if type(value) is bool
                                   else "integer" if type(value) is int else "string",
                           "const": value} for key, value in facts.items()}}
    schema = {"type": "object", "additionalProperties": False,
              "required": ["caseId", "advisoryOnly", "baseline", "explanation", "risks", "alternatives", "sourceIds", "unverifiedProposal"],
              "properties": {
                  "caseId": {"type": "string", "const": case["caseId"]},
                  "advisoryOnly": {"type": "boolean", "const": True},
                  "baseline": baseline_schema, "explanation": text,
                  "risks": {"type": "array", "minItems": 1, "maxItems": 2, "items": text},
                  "alternatives": {"type": "array", "minItems": 1, "maxItems": 2, "items": {
                      "type": "object", "additionalProperties": False, "required": ["alternativeId", "assessment"],
                      "properties": {"alternativeId": {"type": "string", "enum": ["policy_off", "over", "oracle"]}, "assessment": text}}},
                  "sourceIds": {"type": "array", "minItems": 1, "maxItems": 8, "items": {"type": "string", "enum": ids}},
                  "unverifiedProposal": {"type": ["string", "null"], "maxLength": 120}}}
    # 전체 기사 목록·미래 도착표·임의 프로젝트 파일을 보내지 않는다.
    context = {"caseId": case["caseId"], "baseline": facts,
               "rules": [{key: x[key] for key in ("id", "status", "summary")} for x in rules],
               "alternatives": case["alternatives"]}
    baseline = next(x for x in case["alternatives"] if x["alternativeId"] == "observed")
    context["computedComparison"] = [{"alternativeId": x["alternativeId"], **{
        key: "same" if x[key] == baseline[key] else "higher" if x[key] > baseline[key] else "lower"
        for key in ("assignedWaitP95Minutes", "completedLeadP95Minutes", "unassignedOrders", "unfinishedOrders")}}
        for x in case["alternatives"] if x["alternativeId"] != "observed"]
    instructions = (
        "Explain this synthetic food-dispatch policy review in Korean. Read-only advisory, never assign a driver. "
        "Copy baseline exactly. Explain THIS case's actual dataStatus, protected/additional/waiting state, not hypothetical generic policies. "
        "Keep explanation to one short Korean sentence, risks to one short sentence. Assess at most two alternatives, "
        "explicitly using computedComparison same/higher/lower; say 같음 for same metrics, never invent benefit. "
        "Compare already computed whole-campaign metrics; do not claim a new simulation or causal benefit. "
        "oracle is known-future upper-bound, NOT an AI prediction; its assessment MUST include Korean word 상한. "
        "Distinguish implemented, simulation_assumption and proposed rules. "
        "Proposals are untested and not applied. Never claim deployment, training or income guarantees. "
        "Cite only supplied rule IDs, including AUTHORITY and the relevant main rule. "
        "Write Korean prose WITHOUT Arabic digits (numbers are shown separately from verified facts). "
        "Use null for unverifiedProposal if no useful new proposal. Return only the requested JSON."
    )
    return {"model": transport.MODEL, "stream": False, "think": False, "keep_alive": 0,
            "format": schema, "options": {"temperature": 0, "seed": 17, "num_ctx": 4096, "num_predict": 768},
            "messages": [{"role": "system", "content": instructions},
                         {"role": "user", "content": json.dumps(context, ensure_ascii=False, separators=(",", ":"))}]}


def prose(value, optional=False):
    if optional and value is None:
        return
    if (not isinstance(value, str) or not 1 <= len(value.strip()) <= 180
            or not re.search(r"[가-힣]", value) or re.search(r"\d|https?://|[\x00-\x1f]", value)):
        raise ValueError("invalid_korean_prose_or_unverified_numeric_claim")


def validate_advice(value, case, rules):
    expected = {"caseId", "advisoryOnly", "baseline", "explanation", "risks", "alternatives", "sourceIds", "unverifiedProposal"}
    if not isinstance(value, dict) or set(value) != expected:
        raise ValueError("invalid_project_advice_fields")
    if value["caseId"] != case["caseId"] or value["advisoryOnly"] is not True:
        raise ValueError("invalid_project_advice_authority")
    # true/1의 동치도 허용하지 않고 null과 타입까지 같은 기준 결과를 요구한다.
    if json.dumps(value["baseline"], sort_keys=True) != json.dumps(facts_for(case), sort_keys=True):
        raise ValueError("baseline_changed")
    prose(value["explanation"])
    prose(value["unverifiedProposal"], optional=True)
    if not isinstance(value["risks"], list) or not 1 <= len(value["risks"]) <= 3:
        raise ValueError("invalid_risks")
    for risk in value["risks"]:
        prose(risk)
    alternatives = value["alternatives"]
    if not isinstance(alternatives, list) or not 1 <= len(alternatives) <= 3:
        raise ValueError("invalid_alternative_assessments")
    seen = set()
    for alternative in alternatives:
        if (not isinstance(alternative, dict) or set(alternative) != {"alternativeId", "assessment"}
                or alternative["alternativeId"] not in {"policy_off", "over", "oracle"}
                or alternative["alternativeId"] in seen):
            raise ValueError("unknown_or_duplicate_alternative")
        seen.add(alternative["alternativeId"])
        prose(alternative["assessment"])
        if alternative["alternativeId"] == "oracle" and "상한" not in alternative["assessment"]:
            raise ValueError("oracle_upper_bound_label_required")
    refs = value["sourceIds"]
    if (not isinstance(refs, list) or not 1 <= len(refs) <= 8 or any(type(x) is not str for x in refs)
            or len(set(refs)) != len(refs) or not set(refs) <= {x["id"] for x in relevant_rules(case, rules)}):
        raise ValueError("unknown_or_duplicate_source")
    if "AUTHORITY" in {x["id"] for x in rules} and "AUTHORITY" not in refs:
        raise ValueError("authority_reference_required")
    return value


def normalize_optional_proposal(candidate):
    """제안 없음의 명시적인 어휘만 정규화한다. 근거·수치·실제 제안은 바꾸지 않는다."""
    if (isinstance(candidate, dict) and type(candidate.get("unverifiedProposal")) is str
            and candidate["unverifiedProposal"] in {"none", "null", "없음", "제안 없음"}):
        return candidate | {"unverifiedProposal": None}, ["explicit_no_proposal_token_to_null"]
    return candidate, []


def model_metadata(endpoint, timeout):
    endpoint = transport.validate_endpoint(endpoint)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), transport.NoRedirects())
    with opener.open(endpoint + "/api/tags", timeout=timeout) as response:
        raw = response.read(transport.MAX_RESPONSE_BYTES + 1)
    if len(raw) > transport.MAX_RESPONSE_BYTES:
        raise ValueError("model_metadata_too_large")
    tags = decode(raw)
    if (not isinstance(tags, dict) or not isinstance(tags.get("models"), list)
            or any(not isinstance(x, dict) for x in tags["models"])):
        raise ValueError("invalid_model_metadata_structure")
    models = [x for x in tags["models"] if x.get("name") == transport.MODEL]
    if (len(models) != 1 or not isinstance(models[0].get("digest"), str)
            or not HEX.fullmatch(models[0]["digest"].removeprefix("sha256:"))):
        raise ValueError("model_manifest_missing")
    return {"name": transport.MODEL, "manifestDigest": models[0]["digest"], "details": models[0].get("details", {})}


def write_json(path, value):
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, allow_nan=False)


def report_text(results):
    lines = ["# 프로젝트 규칙 참고 AI 검토", "", "모든 설명은 사람 검토 전 참고 의견입니다. 합성 비교이며 실배차·모델 학습·효과 검증이 아닙니다.",
             "대안은 같은 시나리오 전체 기간의 사후 비교입니다. oracle은 알려진 미래량의 모의 상한이며 AI 예측 성능이 아닙니다.", ""]
    for result in results:
        baseline = result["baseline"]
        lines += ["## " + result["caseId"], "", f"자료 {baseline['dataStatus']} / 대기 {baseline['pendingOrders']}건 / 보호 {baseline['protectedDrivers']}명 / 추가 권고 {baseline['additionalDrivers']}명",
                  "", "상태: " + result["status"], ""]
        if result["advice"] is None:
            lines += ["AI 설명을 채택하지 않고 기존 정책 결과를 유지합니다.", ""]
            continue
        advice = result["advice"]
        lines += [advice["explanation"], "", "주의: " + " / ".join(advice["risks"]), ""]
        lines += ["- " + x["alternativeId"] + ": " + x["assessment"] for x in advice["alternatives"]]
        if advice["unverifiedProposal"]:
            lines += ["", "미검증 제안: " + advice["unverifiedProposal"]]
        lines += ["", "참고 규칙: " + ", ".join(advice["sourceIds"]), ""]
    return "\n".join(lines)


def run(execute=False, max_cases=36, timeout=90, endpoint="http://127.0.0.1:12681", output=OUTPUT, root=ROOT):
    transport.validate_endpoint(endpoint)
    integer(max_cases, 1, 36)
    if not 0 < timeout <= 120:
        raise ValueError("invalid_timeout")
    folder = inside(root, output)
    if not folder.is_relative_to(inside(root, "artifacts/local")):
        raise ValueError("output_must_be_local_artifact")
    evidence, raw = load_evidence(root)
    rules, references = load_rules(root)
    tool_hashes = {x: sha(read(inside(root, Path("eng/local-dispatch-ai") / x)))
                   for x in ("project_advisor.py", "dispatch_advisor.py")}
    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:12]
    run_folder = folder / "runs" / run_id
    (run_folder / "requests").mkdir(parents=True, exist_ok=False)
    (run_folder / "responses").mkdir()
    (run_folder / "input.json").write_bytes(raw)
    write_json(run_folder / "references.json", references)
    metadata = None
    metadata_failure = None
    if execute:
        try:
            metadata = model_metadata(endpoint, timeout)
        except (OSError, ValueError, KeyError, TypeError) as error:
            metadata_failure = type(error).__name__
    results = []
    calls = 0
    started = time.monotonic()
    try:
        for case in evidence["cases"][:max_cases]:
            request = build_request(case, rules)
            write_json(run_folder / "requests" / (case["caseId"] + ".json"), request)
            result = {"caseId": case["caseId"], "baseline": facts_for(case), "status": "not_requested",
                      "advice": None, "metrics": {}, "humanReview": "pending", "operationalWrites": 0}
            tick = time.monotonic()
            if execute and metadata_failure:
                result.update(status="rule_reference_retained", failureType=metadata_failure)
            elif execute:
                calls += 1
                try:
                    envelope_path = run_folder / "responses" / (case["caseId"] + ".envelope.json")
                    def save_envelope(response_raw):
                        with envelope_path.open("xb") as stream:
                            stream.write(response_raw)
                        result["envelopeSha256"] = sha(response_raw)
                    candidate, metrics = transport.request_local_json(endpoint, request, timeout,
                                                                       response_sink=save_envelope)
                    result["responseSha256"] = sha(json.dumps(candidate, ensure_ascii=False, sort_keys=True, allow_nan=False).encode())
                    # 합성 입력의 파싱된 응답만 비공개 보존한다. 거절된 내용도 채택·승인과 구분한다.
                    write_json(run_folder / "responses" / (case["caseId"] + ".json"), candidate)
                    result["metrics"] = metrics
                    normalized, changes = normalize_optional_proposal(candidate)
                    result["normalizations"] = changes
                    result["advice"] = validate_advice(normalized, case, rules)
                    result.update(status="validated_advisory_only", metrics=metrics)
                except (OSError, ValueError, KeyError, TypeError) as error:
                    result.update(status="rule_reference_retained", failureType=type(error).__name__)
                    if isinstance(error, ValueError) and re.fullmatch(r"[a-z_]{1,80}", str(error)):
                        result["failureCode"] = str(error)
            result["elapsedSeconds"] = round(time.monotonic() - tick, 3)
            results.append(result)
            with (run_folder / "progress.jsonl").open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(result, ensure_ascii=False, allow_nan=False) + "\n")
            print(json.dumps({"caseId": case["caseId"], "status": result["status"], "elapsedSeconds": result["elapsedSeconds"]}, ensure_ascii=False), flush=True)
    finally:
        metadata_record = {"schemaVersion": 1, "runId": run_id, "syntheticOnly": True, "execute": execute,
            "model": metadata, "campaignEvidenceId": evidence["campaignEvidenceId"], "campaignDigest": evidence["campaignDigest"],
            "inputSha256": sha(raw), "ruleReferences": references,
            "toolHashes": tool_hashes,
            "toolSourcesStable": all(sha(read(inside(root, Path("eng/local-dispatch-ai") / x))) == digest
                                     for x, digest in tool_hashes.items()),
            "caseCount": len(results), "modelCalls": calls, "metadataCalls": int(execute),
            "validatedResponses": sum(x["status"] == "validated_advisory_only" for x in results),
            "retainedRuleResults": sum(x["status"] == "rule_reference_retained" for x in results),
            "elapsedSeconds": round(time.monotonic() - started, 3), "operationalApiCalls": 0, "stateChanges": 0,
            "weightsTrained": False, "humanReview": "pending", "semanticCorrectness": "not_automatically_verified"}
        write_json(run_folder / "run.json", metadata_record)
        write_json(run_folder / "results.json", results)
        (run_folder / "report.md").write_text(report_text(results), encoding="utf-8")
        (folder / "latest.json").write_text(json.dumps({"runId": run_id}, indent=2), encoding="utf-8")
    return metadata_record | {"output": str(run_folder)}


def review(run_id, case_id, verdict, reviewer, note, output=OUTPUT, root=ROOT):
    """사용자가 제공한 검토만 기록한다. 모델이 자기 응답을 승인하는 경로는 없다."""
    if (not re.fullmatch(r"\d{8}T\d{6}Z-[a-f0-9]{12}", run_id) or not CASE_ID.fullmatch(case_id)
            or verdict not in {"approved", "needs_correction"}
            or not 1 <= len(reviewer.strip()) <= 80 or not 1 <= len(note.strip()) <= 1400):
        raise ValueError("invalid_review")
    folder = inside(root, output)
    if not folder.is_relative_to(inside(root, "artifacts/local")):
        raise ValueError("output_must_be_local_artifact")
    run_folder = inside(folder, Path("runs") / run_id)
    result = next((x for x in decode(read(run_folder / "results.json")) if x["caseId"] == case_id), None)
    if result is None or result["status"] != "validated_advisory_only":
        raise ValueError("only_validated_response_can_be_reviewed")
    event = {"runId": run_id, "caseId": case_id, "verdict": verdict, "reviewer": reviewer,
             "note": note, "atUtc": datetime.now(timezone.utc).isoformat(),
             "resultSha256": sha(json.dumps(result, sort_keys=True, ensure_ascii=False).encode()),
             "requestSha256": sha(read(run_folder / "requests" / (case_id + ".json"))),
             "runSha256": sha(read(run_folder / "run.json"))}
    # 동시 쓰기를 거절한다. 비정상 종료로 남은 잠금은 조용히 삭제하지 않는다.
    lock = folder / ".review.lock"
    lock.open("x", encoding="utf-8").close()
    candidate_file = folder / "training-candidates.jsonl"
    candidate_meta = folder / "training-candidates.meta.json"
    try:
        events = []
        history = folder / "reviews.jsonl"
        if history.exists():
            events = [decode(x.encode()) for x in read(history).decode("utf-8").splitlines() if x]
        latest = {(x["runId"], x["caseId"]): x for x in events + [event]}
        candidates = []
        for (rid, cid), approval in latest.items():
            if approval["verdict"] != "approved":
                continue
            source = inside(folder, Path("runs") / rid)
            accepted = next(x for x in decode(read(source / "results.json")) if x["caseId"] == cid)
            if sha(json.dumps(accepted, sort_keys=True, ensure_ascii=False).encode()) != approval["resultSha256"]:
                raise ValueError("reviewed_response_changed")
            if (sha(read(source / "requests" / (cid + ".json"))) != approval.get("requestSha256")
                    or sha(read(source / "run.json")) != approval.get("runSha256")):
                raise ValueError("reviewed_input_or_run_changed")
            candidates.append({"review": approval, "run": decode(read(source / "run.json")),
                "request": decode(read(source / "requests" / (cid + ".json"))), "advice": accepted["advice"],
                "trainingStatus": "candidate_only_requires_separate_training_and_privacy_review"})
        temp = folder / ("training-candidates-" + uuid.uuid4().hex + ".tmp")
        temp.write_text("".join(json.dumps(x, ensure_ascii=False) + "\n" for x in candidates), encoding="utf-8")
        # 승인 철회가 기록되는 순간 이전 후보의 유효성 표시는 제거한다.
        candidate_meta.unlink(missing_ok=True)
        with history.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(event, ensure_ascii=False) + "\n")
        temp.replace(candidate_file)
        write_json(candidate_meta, {"reviewsSha256": sha(read(history)),
            "candidatesSha256": sha(read(candidate_file)), "count": len(candidates), "weightsTrained": False})
    except (OSError, ValueError, KeyError, TypeError, StopIteration):
        # 재생성 가능한 후보만 무효화하고 불변 실행·검토 이력은 보존한다.
        candidate_meta.unlink(missing_ok=True)
        candidate_file.unlink(missing_ok=True)
        raise
    finally:
        lock.unlink()
    return {"reviewRecorded": True, "trainingCandidates": len(candidates), "weightsTrained": False}


def validate_candidates(output=OUTPUT, root=ROOT):
    folder = inside(root, output)
    if not folder.is_relative_to(inside(root, "artifacts/local")):
        raise ValueError("output_must_be_local_artifact")
    try:
        metadata = decode(read(folder / "training-candidates.meta.json"))
    except FileNotFoundError as error:
        raise ValueError("training_candidates_unavailable") from error
    raw = read(folder / "training-candidates.jsonl")
    if (metadata.get("reviewsSha256") != sha(read(folder / "reviews.jsonl"))
            or metadata.get("candidatesSha256") != sha(raw)):
        raise ValueError("training_candidates_stale")
    candidates = [decode(x.encode()) for x in raw.decode("utf-8").splitlines() if x]
    if metadata.get("count") != len(candidates):
        raise ValueError("training_candidates_count_mismatch")
    for candidate in candidates:
        approval = candidate["review"]
        if (not re.fullmatch(r"\d{8}T\d{6}Z-[a-f0-9]{12}", approval["runId"])
                or not CASE_ID.fullmatch(approval["caseId"])):
            raise ValueError("invalid_candidate_identity")
        source = inside(folder, Path("runs") / approval["runId"])
        accepted = next(x for x in decode(read(source / "results.json")) if x["caseId"] == approval["caseId"])
        if (sha(read(source / "run.json")) != approval["runSha256"]
                or sha(read(source / "requests" / (approval["caseId"] + ".json"))) != approval["requestSha256"]
                or sha(json.dumps(accepted, sort_keys=True, ensure_ascii=False).encode()) != approval["resultSha256"]):
            raise ValueError("reviewed_input_or_run_changed")
    return len(candidates)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    launch = sub.add_parser("run")
    launch.add_argument("--execute", action="store_true")
    launch.add_argument("--max-cases", type=int, default=36)
    launch.add_argument("--timeout", type=float, default=90)
    launch.add_argument("--endpoint", default="http://127.0.0.1:12681")
    launch.add_argument("--output", default=str(OUTPUT))
    feedback = sub.add_parser("review")
    feedback.add_argument("--run", required=True)
    feedback.add_argument("--case", required=True)
    feedback.add_argument("--verdict", choices=("approved", "needs_correction"), required=True)
    feedback.add_argument("--reviewer", required=True)
    feedback.add_argument("--note", required=True)
    feedback.add_argument("--output", default=str(OUTPUT))
    candidate_check = sub.add_parser("check-candidates")
    candidate_check.add_argument("--output", default=str(OUTPUT))
    args = parser.parse_args()
    try:
        if args.command == "run":
            result = run(args.execute, args.max_cases, args.timeout, args.endpoint, args.output)
        elif args.command == "review":
            result = review(args.run, args.case, args.verdict, args.reviewer, args.note, args.output)
        else:
            result = {"trainingCandidates": validate_candidates(args.output), "weightsTrained": False}
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (OSError, ValueError, KeyError, TypeError, StopIteration) as error:
        parser.exit(2, "검토 도구 중단: " + type(error).__name__ + "\n")


if __name__ == "__main__":
    main()
