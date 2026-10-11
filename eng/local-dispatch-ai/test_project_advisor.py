"""프로젝트 근거·로컬 설명·사람 검토의 경계를 합성 자료로 검증합니다."""
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import project_advisor as advisor
import project_review_report as review_report

RULE_IDS = ("AUTHORITY", "INCUMBENT_PROTECTION", "WAITING_ORDER", "DATA_FALLBACK",
            "OBSERVED_FORECAST_MAX", "EXCEPTION_OPPORTUNITIES", "SYNTHETIC_LIMITS", "FUTURE_PICKUP_ANCHOR")


def json_bytes(value):
    return json.dumps(value, ensure_ascii=False, allow_nan=False).encode("utf-8")


def write_fixture(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(value if isinstance(value, bytes) else json_bytes(value))


def case_for(scenario="SYN-TEST-A", minute=0):
    at = "2026-10-11T00:00:00+00:00"
    return {
        "caseId": scenario + "-" + str(minute).zfill(3), "scenarioId": scenario,
        "title": "합성 시험", "minute": minute, "planningThroughput": 3.5,
        "selectionReasons": ["start"],
        "input": {"권역Key": scenario, "판정시각Utc": at,
                  "수요관측시각Utc": at, "대기주문수": 2, "최근시간창신규주문수": 3,
                  "향후수요예측건수": None, "지연위험주문존재": False,
                  "기사목록": [{"기사Id": "SYN-D001", "기존활동여부": True}]},
        "decision": {"정책판본": "food-demand-participation/v1", "권역Key": scenario,
                     "판정시각Utc": at, "데이터상태Code": "Current",
                     "사유Code": "DemandAndCapacityObserved", "목표활동인원": 4,
                     "보호활동기사수": 1, "추가참여인원": 3,
                     "기사판정목록": [{"기사Id": "SYN-D001", "상태Code": "ActiveProtected",
                                       "사유Code": "ExistingActivityProtected"}]},
        "alternatives": [
            {"alternativeId": name,
             "kind": "known_future_upper_bound" if name == "oracle" else "synthetic_comparison",
             "assignedWaitP95Minutes": 1.5, "completedLeadP95Minutes": 20,
             "unassignedOrders": 0, "unfinishedOrders": 0, "reviewStatus": "review_required"}
            for name in ("observed", "policy_off", "over", "oracle")],
    }


def advice_for(case, rules):
    return {"caseId": case["caseId"], "advisoryOnly": True, "baseline": advisor.facts_for(case),
            "explanation": "기존 활동을 보호하면서 확인된 수요에 따라 참여를 권고합니다.",
            "risks": ["가상 비교이므로 실제 이동과 소득 효과를 입증하지 않습니다."],
            "alternatives": [{"alternativeId": "policy_off", "assessment": "관측 정책과 별도로 비교해야 합니다."}],
            "sourceIds": [rules[0]["id"]], "unverifiedProposal": None}


class FixtureTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="project-advisor-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = advisor.OUTPUT
        self.cases = [case_for(), case_for("SYN-TEST-B")]
        self.fixture = {"scenarios": [{"id": case["scenarioId"]} for case in self.cases]}
        self.source_hashes = {}
        for path in advisor.SOURCE_PATHS:
            raw = ("합성 source fixture " + path + "\nANCHOR\n").encode()
            write_fixture(self.root / path, raw)
            self.source_hashes[path] = advisor.sha(raw)
        for path in ("project_advisor.py", "dispatch_advisor.py"):
            write_fixture(self.root / "eng/local-dispatch-ai" / path, b"synthetic test tool")
        self.rules = [{"id": rule_id,
                       "status": "proposed" if rule_id == "FUTURE_PICKUP_ANCHOR" else
                                 "simulation_assumption" if rule_id == "SYNTHETIC_LIMITS" else "implemented",
                       "summary": "합성 규칙을 참고합니다.",
                       "sources": [{"path": sorted(advisor.SOURCE_PATHS)[0], "anchors": ["ANCHOR"]}]}
                      for rule_id in RULE_IDS]
        self.spec = {"schemaVersion": 1, "policyVersion": "food-demand-participation/v1", "rules": self.rules}
        write_fixture(self.root / advisor.RULES, self.spec)
        self.fixture_raw = json_bytes(self.fixture)
        write_fixture(self.root / "eng/local-dispatch-ai/ScenarioRunner/scenarios.json", self.fixture_raw)
        self.evidence_id = "a" * 64
        self.save_evidence()

    def save_evidence(self, original_changes=None):
        """생성기 파일·최신 사본·불변 run 사본을 모두 구성합니다."""
        cases_raw = json_bytes(self.cases)
        self.evidence = {"schemaVersion": 1, "syntheticOnly": True,
                         "campaignEvidenceId": self.evidence_id,
                         "policyVersion": "food-demand-participation/v1",
                         "campaignDigest": "b" * 64,
                         "fixtureSha256": advisor.sha(self.fixture_raw),
                         "sourceHashes": self.source_hashes,
                         "casesFile": "advisory-cases.json", "casesDigest": advisor.sha(cases_raw),
                         "cases": self.cases}
        original = {"evidenceId": self.evidence_id, "campaignDigest": self.evidence["campaignDigest"],
                    "fixtureSha256": self.evidence["fixtureSha256"], "modelCalls": 0,
                    "operationalWrites": 0, "safetyPassed": True, "sourceHashes": self.source_hashes}
        original.update(original_changes or {})
        self.folder = self.root / advisor.CAMPAIGN
        self.run_folder = self.folder / "runs" / self.evidence_id
        for folder in (self.folder, self.run_folder):
            write_fixture(folder / "advisory-input.json", self.evidence)
            write_fixture(folder / "advisory-cases.json", cases_raw)
        write_fixture(self.run_folder / "campaign-results.json", original)

    def invoke(self, **kwargs):
        with patch("sys.stdout", new=io.StringIO()):
            return advisor.run(root=self.root, **kwargs)

    def test_default_dry_run_has_no_http_and_preserves_policy_facts(self):
        with patch.object(advisor, "model_metadata") as metadata, patch.object(
                advisor.transport, "request_local_json") as request:
            record = self.invoke()
        metadata.assert_not_called()
        request.assert_not_called()
        self.assertEqual(0, record["modelCalls"])
        self.assertEqual(0, record["metadataCalls"])
        self.assertEqual(0, record["stateChanges"])
        self.assertFalse(record["weightsTrained"])
        results = advisor.decode(advisor.read(Path(record["output"]) / "results.json"))
        self.assertEqual([advisor.facts_for(case) for case in self.cases], [x["baseline"] for x in results])
        self.assertTrue(all(x["status"] == "not_requested" for x in results))

    def test_endpoint_and_output_rejected_before_any_input_or_network(self):
        for endpoint in ("http://example.com:12681", "http://localhost:12681",
                         "http://127.0.0.1:12681/path", "http://user@127.0.0.1:12681"):
            with self.subTest(endpoint=endpoint), patch.object(advisor, "load_evidence") as load:
                with self.assertRaises(ValueError):
                    self.invoke(endpoint=endpoint)
                load.assert_not_called()
        for output in ("docs/test-output", "../escape", self.root.parent / "escape"):
            with self.subTest(output=output), patch.object(advisor, "load_evidence") as load:
                with self.assertRaises(ValueError):
                    self.invoke(output=output)
                load.assert_not_called()

    def test_load_evidence_checks_producer_files_and_source_provenance(self):
        evidence, raw = advisor.load_evidence(self.root)
        self.assertEqual(self.evidence_id, evidence["campaignEvidenceId"])
        self.assertEqual(self.evidence, advisor.decode(raw))
        original = advisor.decode(advisor.read(self.run_folder / "campaign-results.json"))
        original["operationalWrites"] = 1
        write_fixture(self.run_folder / "campaign-results.json", original)
        with self.assertRaisesRegex(ValueError, "campaign_provenance_mismatch"):
            advisor.load_evidence(self.root)

    def test_load_evidence_rejects_changed_latest_copy_source_and_fixture(self):
        write_fixture(self.folder / "advisory-input.json", dict(self.evidence, syntheticOnly=False))
        with self.assertRaises(ValueError):
            advisor.load_evidence(self.root)
        self.save_evidence()
        source = self.root / sorted(advisor.SOURCE_PATHS)[0]
        original = source.read_bytes()
        source.write_bytes(original + b"changed")
        with self.assertRaisesRegex(ValueError, "scenario_source_stale"):
            advisor.load_evidence(self.root)
        source.write_bytes(original)
        write_fixture(self.root / "eng/local-dispatch-ai/ScenarioRunner/scenarios.json", b"{}")
        with self.assertRaisesRegex(ValueError, "scenario_fixture_stale"):
            advisor.load_evidence(self.root)

    def test_load_evidence_rejects_case_digest_mismatch(self):
        changed = copy.deepcopy(self.cases)
        changed[0]["minute"] = 99
        write_fixture(self.folder / "advisory-cases.json", changed)
        with self.assertRaisesRegex(ValueError, "case_digest_mismatch"):
            advisor.load_evidence(self.root)

    def test_load_evidence_rejects_oracle_classified_as_prediction(self):
        self.cases[0]["alternatives"][-1]["kind"] = "ai_prediction"
        self.save_evidence()
        with self.assertRaisesRegex(ValueError, "oracle_is_not_prediction"):
            advisor.load_evidence(self.root)

    def test_load_evidence_rejects_real_driver_future_forecast_or_missing_scenario(self):
        original = copy.deepcopy(self.cases)
        for change in ("real_driver", "future_forecast", "missing_scenario"):
            self.cases = copy.deepcopy(original)
            if change == "real_driver":
                self.cases[0]["input"]["기사목록"][0]["기사Id"] = "real-driver"
            elif change == "future_forecast":
                self.cases[0]["input"]["향후수요예측건수"] = 100
            else:
                self.cases = self.cases[:1]
            self.save_evidence()
            with self.subTest(change=change), self.assertRaises(ValueError):
                advisor.load_evidence(self.root)

    def test_rules_anchor_and_path_allowlist_are_enforced(self):
        rules, refs = advisor.load_rules(self.root)
        self.assertEqual(8, len(rules))
        self.assertEqual(64, len(refs["rulesFileSha256"]))
        source = refs["sources"][rules[0]["id"]][0]
        self.assertEqual(2, source["anchors"][0]["line"])
        self.rules[0]["sources"][0]["anchors"] = ["MISSING_ANCHOR"]
        write_fixture(self.root / advisor.RULES, self.spec)
        with self.assertRaisesRegex(ValueError, "rule_anchor_stale"):
            advisor.load_rules(self.root)
        self.rules[0]["sources"][0] = {"path": "private/customer.json", "anchors": ["ANCHOR"]}
        write_fixture(self.root / advisor.RULES, self.spec)
        with self.assertRaisesRegex(ValueError, "rule_path_not_allowlisted"):
            advisor.load_rules(self.root)

    def test_model_metadata_failure_keeps_rules_and_does_not_call_model(self):
        with patch.object(advisor, "model_metadata", side_effect=ValueError("broken")), patch.object(
                advisor.transport, "request_local_json") as request:
            record = self.invoke(execute=True)
        request.assert_not_called()
        self.assertEqual(0, record["modelCalls"])
        self.assertEqual(2, record["retainedRuleResults"])
        results = advisor.decode(advisor.read(Path(record["output"]) / "results.json"))
        self.assertEqual([advisor.facts_for(case) for case in self.cases], [x["baseline"] for x in results])

    def test_failed_case_does_not_prevent_next_valid_case(self):
        response = advice_for(self.cases[1], self.rules)
        with patch.object(advisor, "model_metadata", return_value={"name": advisor.transport.MODEL}), patch.object(
                advisor.transport, "request_local_json", side_effect=[TimeoutError(), (response, {"eval_count": 20})]):
            record = self.invoke(execute=True)
        self.assertEqual(2, record["modelCalls"])
        self.assertEqual(1, record["validatedResponses"])
        self.assertEqual(1, record["retainedRuleResults"])
        results = advisor.decode(advisor.read(Path(record["output"]) / "results.json"))
        self.assertEqual(["rule_reference_retained", "validated_advisory_only"], [x["status"] for x in results])
        self.assertEqual(advisor.facts_for(self.cases[0]), results[0]["baseline"])

    def test_model_prompt_contains_no_raw_drivers_future_schedule_or_source_paths(self):
        case = copy.deepcopy(self.cases[0])
        case["input"]["futureArrivalTable"] = ["future-secret"]
        case["input"]["기사목록"][0]["phone"] = "phone-secret"
        case["privateRawText"] = "raw-secret"
        case["alternatives"][1]["assignedWaitP95Minutes"] = 2
        case["alternatives"][2]["completedLeadP95Minutes"] = 10
        request = advisor.build_request(case, self.rules)
        text = json.dumps(request, ensure_ascii=False)
        for forbidden in ("SYN-D001", "futureArrivalTable", "future-secret", "phone-secret", "raw-secret"):
            self.assertNotIn(forbidden, text)
        context = json.loads(request["messages"][1]["content"])
        self.assertEqual({"caseId", "baseline", "rules", "alternatives", "computedComparison"}, set(context))
        comparisons = {item["alternativeId"]: item for item in context["computedComparison"]}
        self.assertEqual("higher", comparisons["policy_off"]["assignedWaitP95Minutes"])
        self.assertEqual("lower", comparisons["over"]["completedLeadP95Minutes"])
        self.assertEqual("same", comparisons["oracle"]["unassignedOrders"])
        for source_path in advisor.SOURCE_PATHS:
            self.assertNotIn(source_path, text)
        supplied_ids = {rule["id"] for rule in context["rules"]}
        self.assertIn("AUTHORITY", supplied_ids)
        self.assertIn("OBSERVED_FORECAST_MAX", supplied_ids)
        self.assertNotIn("DATA_FALLBACK", supplied_ids)
        self.assertNotIn("FUTURE_PICKUP_ANCHOR", supplied_ids)
        self.assertFalse(request["think"])
        self.assertFalse(request["stream"])
        self.assertNotIn("tools", request)

    def make_review_run(self, status="validated_advisory_only"):
        run_id = "20261011T000000Z-123456abcdef"
        folder = self.root / self.output / "runs" / run_id
        case = self.cases[0]
        result = {"caseId": case["caseId"], "status": status, "advice": advice_for(case, self.rules),
                  "baseline": advisor.facts_for(case), "humanReview": "pending"}
        write_fixture(folder / "results.json", [result])
        write_fixture(folder / "run.json", {"runId": run_id, "weightsTrained": False, "syntheticOnly": True})
        write_fixture(folder / "requests" / (case["caseId"] + ".json"), advisor.build_request(case, self.rules))
        return run_id, case["caseId"], folder

    def test_review_approval_then_correction_removes_candidate_keeps_history(self):
        run_id, case_id, folder = self.make_review_run()
        before = advisor.read(folder / "results.json")
        first = advisor.review(run_id, case_id, "approved", "실제 검토자", "사용자가 제공한 승인", root=self.root)
        self.assertEqual(1, first["trainingCandidates"])
        candidate_path = self.root / self.output / "training-candidates.jsonl"
        candidate = advisor.decode(candidate_path.read_bytes())
        self.assertEqual("candidate_only_requires_separate_training_and_privacy_review", candidate["trainingStatus"])
        second = advisor.review(run_id, case_id, "needs_correction", "실제 검토자", "정정 필요", root=self.root)
        self.assertEqual(0, second["trainingCandidates"])
        self.assertEqual(b"", candidate_path.read_bytes())
        history = (self.root / self.output / "reviews.jsonl").read_text(encoding="utf-8").splitlines()
        self.assertEqual(["approved", "needs_correction"], [json.loads(x)["verdict"] for x in history])
        self.assertEqual(before, advisor.read(folder / "results.json"))
        self.assertFalse((self.root / self.output / ".review.lock").exists())

    def test_review_rejects_unvalidated_result(self):
        run_id, case_id, _ = self.make_review_run("rule_reference_retained")
        with self.assertRaisesRegex(ValueError, "only_validated_response_can_be_reviewed"):
            advisor.review(run_id, case_id, "approved", "검토자", "승인", root=self.root)
        self.assertFalse((self.root / self.output / "reviews.jsonl").exists())

    def test_review_rejects_changed_request_or_run_of_existing_approval(self):
        run_id, case_id, folder = self.make_review_run()
        second_case = self.cases[1]
        results = advisor.decode(advisor.read(folder / "results.json"))
        results.append({"caseId": second_case["caseId"], "status": "validated_advisory_only",
                        "advice": advice_for(second_case, self.rules),
                        "baseline": advisor.facts_for(second_case), "humanReview": "pending"})
        write_fixture(folder / "results.json", results)
        write_fixture(folder / "requests" / (second_case["caseId"] + ".json"),
                      advisor.build_request(second_case, self.rules))
        advisor.review(run_id, case_id, "approved", "검토자", "첫 사례 승인", root=self.root)
        history = self.root / self.output / "reviews.jsonl"
        approved_history = advisor.read(history)
        for changed in (folder / "requests" / (case_id + ".json"), folder / "run.json"):
            original = advisor.read(changed)
            write_fixture(changed, {"changedAfterApproval": True})
            try:
                with self.subTest(path=changed.name), self.assertRaises(ValueError):
                    advisor.review(run_id, second_case["caseId"], "approved", "검토자", "다음 사례 승인", root=self.root)
                self.assertEqual(approved_history, advisor.read(history))
            finally:
                changed.write_bytes(original)

    def test_review_replace_failure_removes_candidates_and_preserves_correction_history(self):
        run_id, case_id, _ = self.make_review_run()
        advisor.review(run_id, case_id, "approved", "검토자", "첫 승인", root=self.root)
        output = self.root / self.output
        self.assertEqual(1, advisor.validate_candidates(root=self.root))
        with patch.object(Path, "replace", side_effect=OSError("simulated derived-file lock")):
            with self.assertRaises(OSError):
                advisor.review(run_id, case_id, "needs_correction", "검토자", "승인 정정", root=self.root)
        history = (output / "reviews.jsonl").read_text(encoding="utf-8").splitlines()
        self.assertEqual(["approved", "needs_correction"], [json.loads(x)["verdict"] for x in history])
        self.assertFalse((output / "training-candidates.jsonl").exists())
        self.assertFalse((output / "training-candidates.meta.json").exists())
        with self.assertRaises(ValueError):
            advisor.validate_candidates(root=self.root)

    def test_review_rejects_path_traversal_and_output_outside_artifacts(self):
        run_id, case_id, _ = self.make_review_run()
        for rid, cid, output in (("../../escape", case_id, self.output),
                                 (run_id, "../escape", self.output),
                                 (run_id, case_id, "docs/reviews"),
                                 (run_id, case_id, "../outside")):
            with self.subTest(run_id=rid, case_id=cid, output=output), self.assertRaises(ValueError):
                advisor.review(rid, cid, "approved", "검토자", "승인", output=output, root=self.root)

    def test_comparison_report_uses_actual_numbers_without_http_or_approving_ai_prose(self):
        answers = [(advice_for(case, self.rules), {}) for case in self.cases]
        with patch.object(advisor, "model_metadata", return_value={"name": advisor.transport.MODEL}), patch.object(
                advisor.transport, "request_local_json", side_effect=answers):
            record = self.invoke(execute=True)
        with patch.object(advisor, "model_metadata") as metadata, patch.object(
                advisor.transport, "request_local_json") as request:
            report = review_report.render(record["runId"], root=self.root)
        metadata.assert_not_called()
        request.assert_not_called()
        document = Path(report["report"]).read_text(encoding="utf-8")
        self.assertIn("| observed | 1.5 | 20 | 0 | 0 |", document)
        self.assertIn("| policy_off | 1.5 | 20 | 0 | 0 |", document)
        self.assertIn("| oracle | 1.5 | 20 | 0 | 0 |", document)
        self.assertIn("의미 정확성과 사람 승인 미확인", document)
        self.assertNotIn(answers[0][0]["explanation"], document)
        self.assertFalse(report["humanApproval"])
        self.assertFalse(report["semanticAccuracyMeasured"])
        self.assertEqual(0, report["modelCalls"])
        self.assertEqual(0, report["operationalWrites"])
        self.assertFalse(report["reused"])
        self.assertEqual(advisor.sha(Path(report["report"]).read_bytes()), report["reportSha256"])
        repeated = review_report.render(record["runId"], root=self.root)
        self.assertTrue(repeated["reused"])
        self.assertEqual(report["reportSha256"], repeated["reportSha256"])

    def test_comparison_report_rejects_baseline_and_result_count_tampering(self):
        record = self.invoke()
        result_path = Path(record["output"]) / "results.json"
        original = advisor.read(result_path)
        for change in ("baseline", "count"):
            results = advisor.decode(original)
            if change == "baseline":
                results[0]["baseline"]["protectedDrivers"] = True
            else:
                results.append(copy.deepcopy(results[0]))
            write_fixture(result_path, results)
            try:
                with self.subTest(change=change), self.assertRaises(ValueError):
                    review_report.render(record["runId"], root=self.root)
                self.assertFalse((Path(record["output"]) / "verified-comparison.md").exists())
            finally:
                result_path.write_bytes(original)

    def test_comparison_report_displays_unknown_null_as_unconfirmed_not_zero(self):
        case = self.cases[0]
        case["input"]["대기주문수"] = None
        case["decision"].update(데이터상태Code="Unknown", 사유Code="DemandSnapshotUnavailable",
                                목표활동인원=None, 추가참여인원=0)
        self.save_evidence()
        record = self.invoke()
        report = review_report.render(record["runId"], root=self.root)
        document = Path(report["report"]).read_text(encoding="utf-8")
        self.assertIn("| Unknown | 미확인 | 미확인 | 1 | 0 | 0 |", document)
        self.assertNotIn("| Unknown | 0 |", document)
        self.assertNotIn("None", document)

    def test_comparison_report_rejects_existing_mismatch_without_overwriting(self):
        record = self.invoke()
        report = review_report.render(record["runId"], root=self.root)
        path = Path(report["report"])
        changed = b"preserve existing mismatch"
        path.write_bytes(changed)
        with self.assertRaisesRegex(ValueError, "review_report_existing_content_mismatch"):
            review_report.render(record["runId"], root=self.root)
        self.assertEqual(changed, path.read_bytes())

    def test_comparison_report_rejects_stale_source_and_path_escape(self):
        record = self.invoke()
        source = self.root / sorted(advisor.SOURCE_PATHS)[0]
        source.write_bytes(source.read_bytes() + b"changed after run")
        with self.assertRaisesRegex(ValueError, "scenario_source_stale"):
            review_report.render(record["runId"], root=self.root)
        for run_id, output in (("../../escape", self.output), (record["runId"], "../escape"),
                               (record["runId"], "docs/escaped-output")):
            with self.subTest(run_id=run_id, output=output), self.assertRaises(ValueError):
                review_report.render(run_id, output=output, root=self.root)


class AdviceBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.case = case_for()
        self.rules = [{"id": "AUTHORITY", "status": "implemented", "summary": "서버 규칙만 업무를 확정합니다."}]

    def test_valid_advice_preserves_baseline(self):
        value = advice_for(self.case, self.rules)
        self.assertEqual(value, advisor.validate_advice(value, self.case, self.rules))

    def test_model_metadata_rejects_malformed_envelope_models_and_digest_types(self):
        model = {"name": advisor.transport.MODEL, "digest": "a" * 64}
        invalid = [None, [], {}, {"models": None}, {"models": {}}, {"models": [None]},
                   {"models": [123]}, {"models": [dict(model, digest=None)]},
                   {"models": [dict(model, digest=123)]}, {"models": [model, model]}]
        for envelope in invalid:
            with self.subTest(envelope=envelope), patch.object(advisor.urllib.request, "build_opener") as opener:
                opener.return_value.open.return_value.__enter__.return_value = io.BytesIO(json_bytes(envelope))
                with self.assertRaises(ValueError):
                    advisor.model_metadata("http://127.0.0.1:12681", 1)
        with patch.object(advisor.urllib.request, "build_opener") as opener:
            opener.return_value.open.return_value.__enter__.return_value = io.BytesIO(json_bytes({"models": [model]}))
            metadata = advisor.model_metadata("http://127.0.0.1:12681", 1)
        self.assertEqual(model["digest"], metadata["manifestDigest"])

    def test_optional_proposal_normalization_is_exact_and_preserves_original_and_other_fields(self):
        for text in ("none", "null", "없음", "제안 없음"):
            value = advice_for(self.case, self.rules)
            value["unverifiedProposal"] = text
            value["explanation"] = "none"
            original = copy.deepcopy(value)
            normalized, normalizations = advisor.normalize_optional_proposal(value)
            with self.subTest(text=text):
                self.assertIsNot(value, normalized)
                self.assertEqual(original, value)
                self.assertIsNone(normalized["unverifiedProposal"])
                self.assertTrue(normalizations)
                expected = dict(original, unverifiedProposal=None)
                self.assertEqual(expected, normalized)
        for text in (None, "NONE", " none ", "없음?", "추가 제안 없음", "대기 기록을 별도로 검토하는 방안을 제안합니다."):
            value = advice_for(self.case, self.rules)
            value["unverifiedProposal"] = text
            original = copy.deepcopy(value)
            normalized, normalizations = advisor.normalize_optional_proposal(value)
            with self.subTest(text=text):
                self.assertEqual(original, value)
                self.assertEqual(original, normalized)
                self.assertFalse(normalizations)

    def test_transport_preserves_raw_response_before_duplicate_json_rejection(self):
        request = advisor.build_request(self.case, self.rules)
        raw = b'{"model":"qwen3:1.7b","done":false,"done":true,"message":{"content":"{}"}}'
        captured = []
        with patch.object(advisor.transport.urllib.request, "build_opener") as opener:
            opener.return_value.open.return_value.__enter__.return_value = io.BytesIO(raw)
            with self.assertRaisesRegex(ValueError, "duplicate_json_key"):
                advisor.transport.request_local_json("http://127.0.0.1:12681", request, 1, response_sink=captured.append)
        self.assertEqual([raw], captured)

    def test_altered_baseline_type_or_extra_authority_field_rejected(self):
        for change in ("number", "bool", "extra", "advisory_false"):
            value = advice_for(self.case, self.rules)
            if change == "number":
                value["baseline"]["targetActiveDrivers"] = 99
            elif change == "bool":
                value["baseline"]["protectedDrivers"] = True
            elif change == "extra":
                value["assignDriver"] = "SYN-D001"
            else:
                value["advisoryOnly"] = False
            with self.subTest(change=change), self.assertRaises(ValueError):
                advisor.validate_advice(value, self.case, self.rules)

    def test_unknown_duplicate_sources_and_alternatives_rejected(self):
        for change in ("unknown_source", "duplicate_source", "unknown_alternative", "duplicate_alternative", "extra_alternative_field"):
            value = advice_for(self.case, self.rules)
            if change == "unknown_source":
                value["sourceIds"] = ["MADE_UP"]
            elif change == "duplicate_source":
                value["sourceIds"] *= 2
            elif change == "unknown_alternative":
                value["alternatives"][0]["alternativeId"] = "new_simulation"
            elif change == "duplicate_alternative":
                value["alternatives"] *= 2
            else:
                value["alternatives"][0]["effect"] = "benefit"
            with self.subTest(change=change), self.assertRaises(ValueError):
                advisor.validate_advice(value, self.case, self.rules)

    def test_numeric_prose_external_url_and_non_korean_rejected(self):
        for text in ("대기를 5분 줄입니다.", "수입이 ２０원 늘어납니다.", "근거 https://example.com 입니다.",
                     "English prose only", "새로운\n상태를 적용합니다."):
            value = advice_for(self.case, self.rules)
            value["explanation"] = text
            with self.subTest(text=text), self.assertRaises(ValueError):
                advisor.validate_advice(value, self.case, self.rules)

    def test_strict_decode_rejects_duplicate_keys_and_nonfinite_numbers(self):
        for raw in (b'{"a":1,"a":2}', b'{"a":NaN}', b'{"a":Infinity}', b'{"a":-Infinity}'):
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                advisor.decode(raw)

    def test_transport_rejects_duplicate_envelope_content_and_nonfinite(self):
        request = advisor.build_request(self.case, self.rules)
        valid_content = json.dumps(advice_for(self.case, self.rules), ensure_ascii=False)
        raw_values = [
            ('{"model":"' + advisor.transport.MODEL + '","done":false,"done":true,"message":{"content":' + json.dumps(valid_content) + '}}').encode(),
            json_bytes({"model": advisor.transport.MODEL, "done": True,
                        "message": {"content": '{"caseId":"a","caseId":"b"}'}}),
            json_bytes({"model": advisor.transport.MODEL, "done": True,
                        "message": {"content": '{"score":NaN}'}}),
        ]
        for raw in raw_values:
            with self.subTest(raw=raw[:80]), patch.object(advisor.transport.urllib.request, "build_opener") as opener:
                opener.return_value.open.return_value.__enter__.return_value = io.BytesIO(raw)
                with self.assertRaises(ValueError):
                    advisor.transport.request_local_json("http://127.0.0.1:12681", request, 1)


if __name__ == "__main__":
    unittest.main()
