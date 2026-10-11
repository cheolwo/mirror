"""후보 제약·근거 보존·실패 복귀·누적 호출 예산의 집중 검증."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import candidate_advisor as advisor


def example():
    def row(identifier, score, stage=0, code="CandidateAllowed"):
        return {"기사Id": identifier, "공간단계": stage, "공간설명": "합성",
                "판정Code": code, "거리Km": 0.2, "픽업단계": 0, "도착차이분": 0,
                "추천점수": 99, "대기점수": score, "수요우선순위": 1, "수요사유Code": "LongWaitOpportunity"}
    return {"caseId": "SYN-CANDIDATE-TEST", "title": "합성 후보",
            "input": {"drivers": [{"driverId": identifier, "existingActivity": False,
                       "waitMinutes": 20, "remainingMinutes": 0, "recentAssignments": 0}
                       for identifier in ("SYN-D001", "SYN-D002", "SYN-D003")]},
            "demandResult": None,
            "trace": {"선정후보": {"driverId": "SYN-D001"}, "주배달권키": "food-cell:v1:1500:5080",
                      "조회배달권키목록": ["food-cell:v1:1500:5080"],
                      "후보목록": [row("SYN-D001", 20), row("SYN-D002", 5), row("SYN-D003", 40, code="ReceivingOff")],
                      "최종허용기사Ids": ["SYN-D001", "SYN-D002"], "수요자료최신": True,
                      "수요예외사용": False, "픽업적시성예외사용": False},
            "checks": [{"name": "actual", "passed": True, "detail": "합성"}]}


RULES = [{"id": "AUTHORITY", "status": "implemented", "summary": "서버 권위"},
         {"id": "AREA_SEARCH", "status": "implemented", "summary": "배달권"},
         {"id": "ELIGIBILITY", "status": "implemented", "summary": "적격"},
         {"id": "WAITING_ORDER", "status": "implemented", "summary": "대기 순서"},
         {"id": "DEMAND_CAPACITY", "status": "implemented", "summary": "수요 여력"},
         {"id": "USER_LONG_WAIT", "status": "user_preference", "summary": "사용자 방향"},
         {"id": "FUTURE_ANCHOR", "status": "proposed", "summary": "후속 제안"}]
DIGEST = "a" * 64


def valid_advice(case=None):
    case = case or example()
    return {"schemaVersion": 1, "caseId": case["caseId"], "snapshotSha256": DIGEST,
            "advisoryOnly": True, "baselineCandidateId": advisor.baseline(case),
            "decisionCode": "retain_engine", "selectedCandidateId": advisor.baseline(case),
            "reasonCodes": ["ENGINE_ORDER", "SAME_AREA"], "sourceIds": ["AUTHORITY", "AREA_SEARCH"]}


class CandidateContractTests(unittest.TestCase):
    def setUp(self):
        self.case = example()
        self.advice = valid_advice(self.case)

    def validate(self):
        return advisor.validate_advice(self.advice, self.case, RULES, DIGEST)

    def test_valid_baseline(self):
        self.assertEqual(self.validate(), self.advice)

    def test_actual_case(self):
        advisor.validate_case(self.case)

    def test_excluded_driver_not_in_schema(self):
        schema = advisor.build_request(self.case, RULES, DIGEST)["format"]
        self.assertNotIn("SYN-D003", schema["properties"]["selectedCandidateId"]["enum"])

    def test_explicit_context_projection(self):
        self.case["input"]["customerPhone"] = "PRIVATE"
        self.case["trace"]["rawAddress"] = "PRIVATE"
        context = advisor.build_request(self.case, RULES, DIGEST)
        self.assertNotIn("PRIVATE", json.dumps(context))

    def test_preferences_remain_distinct(self):
        context = advisor.model_context(self.case, RULES)
        statuses = {x["id"]: x["status"] for x in context["rules"]}
        self.assertEqual(statuses["USER_LONG_WAIT"], "user_preference")
        self.assertEqual(statuses["FUTURE_ANCHOR"], "proposed")
        self.assertEqual(context["limitations"]["pickupAnchor"], "current_gps")

    def test_unknown_driver(self):
        self.advice["selectedCandidateId"] = "SYN-D999"
        with self.assertRaisesRegex(ValueError, "candidate_not_allowed"): self.validate()

    def test_excluded_driver(self):
        self.advice["selectedCandidateId"] = "SYN-D003"
        with self.assertRaisesRegex(ValueError, "candidate_not_allowed"): self.validate()

    def test_snapshot_changed(self):
        self.advice["snapshotSha256"] = "b" * 64
        with self.assertRaisesRegex(ValueError, "snapshot_or_baseline"): self.validate()

    def test_baseline_changed(self):
        self.advice["baselineCandidateId"] = "SYN-D002"
        with self.assertRaisesRegex(ValueError, "snapshot_or_baseline"): self.validate()

    def test_integer_not_boolean(self):
        self.advice["advisoryOnly"] = 1
        with self.assertRaisesRegex(ValueError, "snapshot_or_baseline"): self.validate()

    def test_boolean_not_version(self):
        self.advice["schemaVersion"] = True
        with self.assertRaisesRegex(ValueError, "snapshot_or_baseline"): self.validate()

    def test_no_free_prose(self):
        self.advice["explanation"] = "임의 주장"
        with self.assertRaisesRegex(ValueError, "invalid_advice_fields"): self.validate()

    def test_bad_decision(self):
        self.advice["decisionCode"] = "assign_now"
        with self.assertRaisesRegex(ValueError, "invalid_decision_code"): self.validate()

    def test_alternative_review(self):
        self.advice.update(decisionCode="review_alternative", selectedCandidateId="SYN-D002", reasonCodes=["SAME_AREA"])
        self.validate()

    def test_retain_must_match(self):
        self.advice["selectedCandidateId"] = "SYN-D002"
        with self.assertRaisesRegex(ValueError, "decision_selection_mismatch"): self.validate()

    def test_alternative_cannot_be_baseline(self):
        self.advice["decisionCode"] = "review_alternative"
        with self.assertRaisesRegex(ValueError, "decision_selection_mismatch"): self.validate()

    def test_insufficient_requires_null(self):
        self.advice["decisionCode"] = "insufficient_data"
        with self.assertRaisesRegex(ValueError, "decision_selection_mismatch"): self.validate()

    def test_empty_candidates(self):
        self.case["trace"]["선정후보"] = None
        self.case["trace"]["최종허용기사Ids"] = []
        self.case["trace"]["후보목록"] = []
        self.advice = valid_advice(self.case)
        self.advice["reasonCodes"] = ["NO_ELIGIBLE_CANDIDATE"]
        self.advice["sourceIds"].append("ELIGIBILITY")
        advisor.validate_case(self.case)
        self.validate()

    def test_false_empty_reason(self):
        self.advice["reasonCodes"] = ["NO_ELIGIBLE_CANDIDATE"]
        with self.assertRaisesRegex(ValueError, "reason_conflicts"): self.validate()

    def test_wrong_spatial_reason(self):
        self.advice["reasonCodes"] = ["ADJACENT_AREA"]
        with self.assertRaisesRegex(ValueError, "reason_conflicts"): self.validate()

    def test_longer_wait_fact(self):
        self.advice["reasonCodes"] = ["LONGER_WAIT"]
        self.advice["sourceIds"].append("WAITING_ORDER")
        self.validate()  # excluded higher score does not join allowed set

    def test_false_longer_wait(self):
        self.advice.update(decisionCode="review_alternative", selectedCandidateId="SYN-D002", reasonCodes=["LONGER_WAIT"])
        with self.assertRaisesRegex(ValueError, "reason_conflicts"): self.validate()

    def test_alternative_preserves_pickup_stage(self):
        self.case["trace"]["후보목록"][1]["픽업단계"] = 2
        self.advice.update(decisionCode="review_alternative", selectedCandidateId="SYN-D002", reasonCodes=["SAME_AREA"])
        with self.assertRaisesRegex(ValueError, "alternative_must_preserve_pickup_stage"): self.validate()

    def test_limited_data_null_preserved(self):
        self.case["input"]["drivers"][0]["remainingMinutes"] = None
        self.advice["reasonCodes"] = ["LIMITED_DATA"]
        self.advice["sourceIds"].append("DEMAND_CAPACITY")
        self.validate()
        self.assertIsNone(advisor.model_context(self.case, RULES)["driverWork"][0]["remainingMinutes"])

    def test_false_limited_data(self):
        self.advice["reasonCodes"] = ["LIMITED_DATA"]
        with self.assertRaisesRegex(ValueError, "reason_conflicts"): self.validate()

    def test_duplicate_reason(self):
        self.advice["reasonCodes"] = ["ENGINE_ORDER", "ENGINE_ORDER"]
        with self.assertRaisesRegex(ValueError, "invalid_reason_or_source"): self.validate()

    def test_missing_authority(self):
        self.advice["sourceIds"] = ["USER_LONG_WAIT"]
        with self.assertRaisesRegex(ValueError, "authority_source"): self.validate()

    def test_missing_reason_source(self):
        self.advice["sourceIds"] = ["AUTHORITY"]
        with self.assertRaisesRegex(ValueError, "reason_source_required"): self.validate()

    def test_duplicate_trace_row(self):
        self.case["trace"]["후보목록"].append(self.case["trace"]["후보목록"][0])
        with self.assertRaisesRegex(ValueError, "duplicate_candidate_trace"): advisor.validate_case(self.case)

    def test_allowed_trace_set_mismatch(self):
        self.case["trace"]["최종허용기사Ids"].remove("SYN-D002")
        with self.assertRaisesRegex(ValueError, "allowed_candidate_trace_mismatch"): advisor.validate_case(self.case)

    def test_duplicate_driver(self):
        self.case["input"]["drivers"].append(self.case["input"]["drivers"][0])
        with self.assertRaisesRegex(ValueError, "invalid_synthetic_drivers"): advisor.validate_case(self.case)

    def test_non_synthetic_driver(self):
        self.case["input"]["drivers"][0]["driverId"] = "REAL-PERSON"
        with self.assertRaisesRegex(ValueError, "invalid_synthetic_drivers"): advisor.validate_case(self.case)

    def test_disallowed_trace(self):
        self.case["trace"]["후보목록"][0]["판정Code"] = "GpsStale"
        with self.assertRaisesRegex(ValueError, "allowed_candidate_not_eligible"): advisor.validate_case(self.case)

    def test_duplicate_json(self):
        with self.assertRaisesRegex(ValueError, "duplicate_json_key"): advisor.decode(b'{"x":1,"x":2}')

    def test_nonfinite_json(self):
        with self.assertRaisesRegex(ValueError, "nonfinite"): advisor.decode(b'{"x":NaN}')

    def test_no_path_escape(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(ValueError, "path_outside"): advisor.inside(Path(folder), "../escape")

    def test_digest_binds_preferences(self):
        self.assertNotEqual(advisor.snapshot(self.case, DIGEST, {"a": 1}), advisor.snapshot(self.case, DIGEST, {"a": 2}))


class ExecutionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        tools = self.root / "eng/local-dispatch-ai"
        tools.mkdir(parents=True)
        for name in ("candidate_advisor.py", "dispatch_advisor.py"):
            (tools / name).write_bytes(b"synthetic-test-tool")
        self.case = example()
        self.evidence = {"cases": [self.case]}
        self.raw = b"synthetic-evidence"
        self.load = patch.object(advisor, "load_evidence", return_value=(self.evidence, self.raw))
        self.rules = patch.object(advisor, "load_rules", return_value=(RULES, {}))
        self.meta = patch.object(advisor, "model_metadata", return_value={"name": advisor.transport.MODEL, "manifestDigest": DIGEST})
        self.load.start(); self.rules.start(); self.meta.start()

    def tearDown(self):
        self.load.stop(); self.rules.stop(); self.meta.stop(); self.temp.cleanup()

    def test_dry_run_calls_no_model(self):
        with patch.object(advisor.transport, "request_local_json") as model:
            result = advisor.run(root=self.root)
        model.assert_not_called()
        self.assertEqual(result["modelCalls"], 0)

    def test_failed_call_preserves_baseline(self):
        with patch.object(advisor.transport, "request_local_json", side_effect=TimeoutError):
            result = advisor.run(execute=True, root=self.root)
        recorded = advisor.decode((Path(result["output"]) / "results.json").read_bytes())
        self.assertEqual(recorded["cases"][0]["effectiveCandidateId"], "SYN-D001")
        self.assertEqual(result["fallback"], 1)
        self.assertEqual(len(list((self.root / advisor.OUTPUT / "attempts").glob("attempt-*.json"))), 1)

    def test_alternative_does_not_change_effective_driver(self):
        def respond(endpoint, payload, timeout, response_sink):
            digest = payload["format"]["properties"]["snapshotSha256"]["const"]
            value = valid_advice(self.case)
            value.update(snapshotSha256=digest, decisionCode="review_alternative",
                         selectedCandidateId="SYN-D002", reasonCodes=["SAME_AREA"])
            response_sink(advisor.encoded(value))
            return value, {}
        with patch.object(advisor.transport, "request_local_json", side_effect=respond):
            result = advisor.run(execute=True, root=self.root)
        recorded = advisor.decode((Path(result["output"]) / "results.json").read_bytes())
        self.assertEqual(result["validated"], 1)
        self.assertEqual(recorded["cases"][0]["effectiveCandidateId"], "SYN-D001")
        self.assertEqual(recorded["humanApprovals"], 0)

    def test_timeout_continues_next_case(self):
        other = copy.deepcopy(self.case); other["caseId"] = "SYN-CANDIDATE-NEXT"
        self.evidence["cases"].append(other)
        with patch.object(advisor.transport, "request_local_json", side_effect=TimeoutError):
            result = advisor.run(execute=True, root=self.root)
        self.assertEqual(result["modelCalls"], 2)
        self.assertEqual(result["fallback"], 2)

    def test_budget_cumulative_and_before_transport(self):
        with patch.object(advisor.transport, "request_local_json", side_effect=TimeoutError) as model:
            for _ in range(advisor.MAX_ATTEMPTS): advisor.run(execute=True, root=self.root)
            with self.assertRaisesRegex(ValueError, "budget_exhausted"):
                advisor.run(execute=True, root=self.root)
        self.assertEqual(model.call_count, advisor.MAX_ATTEMPTS)

    def test_reservation_lock_is_not_bypassed(self):
        folder = self.root / advisor.OUTPUT
        attempts = folder / "attempts"; attempts.mkdir(parents=True)
        (attempts / "reservation.lock").write_text("busy")
        with self.assertRaises(FileExistsError): advisor.reserve_attempt(folder, "run", self.case["caseId"], DIGEST)

    def test_unknown_selection(self):
        with self.assertRaisesRegex(ValueError, "unknown_or_duplicate"):
            advisor.run(case_ids=["SYN-CANDIDATE-OTHER"], root=self.root)

    def test_source_drift_during_inference(self):
        def mutate(endpoint, payload, timeout, response_sink):
            (self.root / "eng/local-dispatch-ai/candidate_advisor.py").write_bytes(b"changed")
            return valid_advice(), {}
        with patch.object(advisor.transport, "request_local_json", side_effect=mutate):
            result = advisor.run(execute=True, root=self.root)
        self.assertEqual(result["fallback"], 1)


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        for relative in advisor.SOURCE_PATHS:
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"synthetic source")
        self.folder = self.root / advisor.OUTPUT
        self.folder.mkdir(parents=True)
        fixture = {"schemaVersion": 1, "synthetic": True, "cases": [
            {"caseId": example()["caseId"], "drivers": [{"driverId": x["driverId"]}
              for x in example()["input"]["drivers"]]}]}
        self.fixture = advisor.encoded(fixture)
        (self.root / "eng/local-dispatch-ai/CandidateScenarioRunner/fixture.json").write_bytes(self.fixture)
        self.cases = advisor.encoded([example()])
        self.value = {"schemaVersion": 1, "synthetic": True, "policyVersion": "food-candidate-review/r47",
                      "checksPassed": True, "modelCalls": 0, "networkCalls": 0, "operationalWrites": 0,
                      "sourceHashes": {x: advisor.sha((self.root / x).read_bytes()) for x in advisor.SOURCE_PATHS},
                      "fixtureFile": "fixture.json", "fixtureSha256": advisor.sha(self.fixture),
                      "casesFile": "cases.json", "casesDigest": advisor.sha(self.cases),
                      "cases": [example()], "evidenceId": "b" * 64}
        self.preserve()

    def preserve(self):
        self.immutable = self.folder / "runs" / self.value["evidenceId"]
        self.immutable.mkdir(parents=True, exist_ok=True)
        for folder in (self.folder, self.immutable):
            (folder / "evidence.json").write_bytes(advisor.encoded(self.value))
            (folder / "fixture.json").write_bytes(self.fixture)
            (folder / "cases.json").write_bytes(self.cases)

    def tearDown(self):
        self.temp.cleanup()

    def test_current_evidence_loads(self):
        value, raw = advisor.load_evidence(self.root)
        self.assertEqual(value, self.value)

    def test_source_changed(self):
        (self.root / "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "candidate_source_stale"): advisor.load_evidence(self.root)

    def test_unexpected_source_path(self):
        self.value["sourceHashes"]["private.json"] = "a" * 64
        self.preserve()
        with self.assertRaisesRegex(ValueError, "unexpected_source_paths"): advisor.load_evidence(self.root)

    def test_case_bytes_changed(self):
        (self.folder / "cases.json").write_bytes(b"[]")
        with self.assertRaisesRegex(ValueError, "candidate_digest_mismatch"): advisor.load_evidence(self.root)

    def test_immutable_copy_changed(self):
        (self.immutable / "evidence.json").write_bytes(b"{}")
        with self.assertRaisesRegex(ValueError, "immutable_evidence_mismatch"): advisor.load_evidence(self.root)

    def test_boolean_zero_does_not_count_as_zero_writes(self):
        self.value["operationalWrites"] = False
        self.preserve()
        with self.assertRaisesRegex(ValueError, "invalid_candidate_evidence"): advisor.load_evidence(self.root)

    def test_fixture_is_bound_to_source_copy(self):
        self.fixture = b'{"schemaVersion":1,"synthetic":true,"cases":[]}'
        self.value["fixtureSha256"] = advisor.sha(self.fixture)
        self.preserve()
        with self.assertRaisesRegex(ValueError, "candidate_fixture_source_mismatch"): advisor.load_evidence(self.root)

    def test_failed_scenario_not_accepted(self):
        self.value["cases"][0]["checks"][0]["passed"] = False
        self.cases = advisor.encoded(self.value["cases"])
        self.value["casesDigest"] = advisor.sha(self.cases)
        self.preserve()
        with self.assertRaisesRegex(ValueError, "scenario_check_failed"): advisor.load_evidence(self.root)


if __name__ == "__main__":
    unittest.main()
