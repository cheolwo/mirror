"""실자료·네트워크·모델 없이 r52 자료의 개인정보/분리/승인 경계를 검증한다."""
from __future__ import annotations

import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("dispatch_timing_materials", Path(__file__).with_name("dispatch_timing_materials.py"))
tm = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(tm)


def identifier(kind, seed):
    return "replay-" + kind + "-" + tm.digest(str(seed).encode("utf-8"))


def input_value(seed=1):
    return {"orderAgeSeconds": seed, "snapshotAgeSeconds": 1, "latestDispatchInSeconds": 100,
            "applicable": True, "hasActiveOffer": False, "isRetry": False, "demandKnown": True,
            "driverCapacityKnown": True, "delayRisk": False, "promisingPlanAvailable": False,
            "waitingOrders": 3, "compatibleOrders": 1, "availableDrivers": 2, "expectedNewOrders": 1.5,
            "settings": {"collectionSeconds": 30, "maxWaitSeconds": 60, "minimumCompatibleOrders": 2,
                         "deadlineBufferSeconds": 15, "maxSnapshotAgeSeconds": 15}}


def row_for(record, seed=1, family="collect"):
    return {"schemaVersion": 1, "caseId": tm.case_id(record["recordId"], family), "taskCode": "DispatchTiming",
            "scenarioFamily": family, "sourceRecordIds": [record["recordId"]], "sourceBranchIds": [record["branchId"]],
            "split": tm.branch_split(record["branchId"]), "input": input_value(seed),
            "target": {"actionCode": "Collect", "reasonCode": "ShortCollectionWindow", "recheckInSeconds": 30 - seed,
                       "hardDeadlineInSeconds": 60 - seed, "allowedActions": ["Collect", "DispatchNow"]},
            "teacher": {"kind": "CurrentPolicyDraft", "policyVersion": "food-dispatch-timing/v1", "optimalityProven": False},
            "provenance": {"synthetic": True, "timingBasis": "SeededRelativeTimingNotHistoricalObservation",
                           "realCustomerLocationsUsed": 0, "realDriverLocationsUsed": 0, "historicalTimingObserved": False},
            "review": {"status": "Pending", "eligibleForTraining": False}}


class DispatchTimingMaterialsTests(unittest.TestCase):
    def setUp(self):
        fixture_root = tm.ROOT / "test-fixtures"
        fixture_root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="t-", dir=fixture_root)
        self.repo = Path(self.temp.name)
        self.root, self.dataset_root, self.delivery = self.repo / "r", self.repo / "d", self.repo / "p"
        self.records = [{"recordId": identifier("record", i), "branchId": identifier("branch", i),
                         "district": tm.DISTRICTS[i % 3], "publicStoreName": "PRIVATE STORE",
                         "publicPickupAddress": "PRIVATE ADDRESS", "historical": {"deliveryFeeKrw": 9999}} for i in range(20)]
        self.design = self.repo / tm.DESIGN_REF
        self.design.parent.mkdir(parents=True)
        self.design.write_bytes(b"accepted synthetic timing design\n")
        for name in (*tm.TEACHER_FILES, tm.EXPORTER_REF):
            p = self.repo / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b"synthetic teacher\n")
        for name in ("Program.cs", "TimingCases.cs", "TimingSources.cs", "DispatchTimingScenarioRunner.csproj"):
            p = self.repo / tm.RUNNER_REF / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b"synthetic generator\n")
        historical = self.repo / "artifacts/local/delivery-history-replay-r54/dataset.json"
        historical.parent.mkdir(parents=True)
        historical.write_bytes(b'{"synthetic":true}\n')
        ledger = self.delivery / tm.LEDGER_REF
        ledger.parent.mkdir(parents=True)
        ledger.write_bytes(b'{"synthetic":true}\n')
        fp = tm.digest(b"synthetic source fingerprint")
        metadata = {"localVerificationOnly": True, "executionModeCode": "Simulation", "realCustomerLocationsUsed": 0,
                    "realDriverLocationsUsed": 0, "historicalValuesAreCurrentPrices": False, "visitMenuSummariesAllocated": False,
                    "sourceFingerprint": fp, "sourceLedgerSha256": tm.digest(ledger.read_bytes()),
                    "toolSha256": tm.digest((self.repo / tm.EXPORTER_REF).read_bytes()),
                    "sourceFingerprints": [{"relativePath": historical.relative_to(self.repo).as_posix(),
                                            "sha256": tm.digest(historical.read_bytes())}]}
        self.dataset = self.dataset_root / fp / "dataset.json"
        self.dataset.parent.mkdir(parents=True)
        self.dataset.write_bytes(tm.json_bytes({"metadata": metadata, "records": self.records}))
        self.dataset_sha = tm.digest(self.dataset.read_bytes())
        self.design_sha = tm.digest(self.design.read_bytes())
        self.dataset.with_name("summary.json").write_bytes(tm.json_bytes({"datasetSha256": self.dataset_sha}))
        self.work = self.repo / tm.WORK_REF
        self.work.write_bytes(tm.json_bytes({"acceptedInCurrentThread": True, "implementationAllowed": True,
            "designSha256": self.design_sha, "sourceDatasetSha256": self.dataset_sha, "modelCallsAllowed": 0,
            "weightTrainingAllowed": False, "operationalChangesAllowed": False}))
        self.patches = [patch.object(tm, "REPO", self.repo), patch.object(tm, "ROOT", self.root),
                        patch.object(tm, "DATASET_ROOT", self.dataset_root), patch.object(tm, "DELIVERY_ROOT", self.delivery),
                        patch.object(tm, "DESIGN_SHA", self.design_sha), patch.object(tm, "DATASET_SHA", self.dataset_sha),
                        patch.object(tm, "EXPECTED_RECORDS", len(self.records)), patch.object(tm, "EXPECTED_BRANCHES", len(self.records)),
                        patch.object(tm, "ensure_ignored", lambda _p: None)]
        for item in self.patches:
            item.start()
        self.rows = [row_for(self.records[i], i + 1) for i in range(10)]
        self.write_raw()

    def tearDown(self):
        for item in reversed(self.patches):
            item.stop()
        self.temp.cleanup()

    def write_raw(self):
        data = tm.parse_json(self.dataset.read_bytes())
        hashes = {self.dataset.relative_to(self.repo).as_posix(): tm.digest(self.dataset.read_bytes()),
                  self.dataset.with_name("summary.json").relative_to(self.repo).as_posix(): tm.digest(self.dataset.with_name("summary.json").read_bytes()),
                  tm.LEDGER_REF: tm.digest((self.delivery / tm.LEDGER_REF).read_bytes())}
        hashes.update({x["relativePath"]: x["sha256"] for x in data["metadata"]["sourceFingerprints"]})
        hashes.update({x: tm.digest(tm.source_path(x).read_bytes()) for x in tm.teacher_paths()})
        self.raw = self.root / "raw" / tm.generator_fingerprint(hashes) / "raw.jsonl"
        self.raw.parent.mkdir(parents=True, exist_ok=True)
        self.raw.write_bytes(b"".join(tm.json_bytes(x) for x in self.rows))
        self.manifest = self.raw.with_name("raw.manifest.json")
        self.manifest.write_bytes(tm.json_bytes({"schemaVersion": 1, "formatVersion": tm.RAW_FORMAT,
            "datasetSha256": self.dataset_sha, "sourceHashes": hashes, "rawSha256": tm.digest(self.raw.read_bytes()),
            "caseCount": len(self.rows), "modelCalls": 0, "weightsTrained": False, "humanApprovals": 0, "operationalWrites": 0,
            "generatorFingerprint": self.raw.parent.name, "designSha256": self.design_sha, "sourceHashesUnchanged": True,
            "splitRule": tm.SPLIT_RULE, "fixedSeed": 52}))

    def compile(self, output=None):
        return tm.compile_materials(self.raw, self.dataset, output)

    def change_manifest(self, field, value):
        manifest = tm.parse_json(self.manifest.read_bytes())
        manifest[field] = value
        self.manifest.write_bytes(tm.json_bytes(manifest))

    def failure(self, code):
        with self.assertRaisesRegex(tm.MaterialFailure, "^" + code + "$"):
            self.compile()

    def invalid_row(self, field, key, value, code):
        self.rows[0][field][key] = value
        self.write_raw()
        self.failure(code)

    def test_compile_keeps_pending_and_ready_empty(self):
        output, summary = self.compile()
        self.assertEqual(10, summary["candidateCases"])
        self.assertEqual({x: 0 for x in tm.SPLITS}, summary["readySplits"])
        self.assertFalse(summary["averageFeeTargetImplemented"])
        self.assertFalse(summary["weightsTrained"])
        for split in tm.SPLITS:
            self.assertEqual(b"", (output / "ready" / (split + ".jsonl")).read_bytes())

    def test_messages_do_not_include_historical_private_fields(self):
        output, _ = self.compile()
        for p in (output / "candidates").glob("*.jsonl"):
            for line in p.read_text(encoding="utf-8").splitlines():
                messages = tm.canonical(tm.parse_json(line)["messages"])
                for forbidden in ("PRIVATE STORE", "PRIVATE ADDRESS", "deliveryFeeKrw", "sourceRecordIds", "replay-record-", "replay-branch-"):
                    self.assertNotIn(forbidden, messages)

    def test_idempotent_output_is_reused(self):
        first, summary = self.compile()
        second, same = self.compile()
        self.assertEqual(first, second)
        self.assertEqual(summary, same)

    def test_tampered_output_is_not_overwritten(self):
        output, _ = self.compile()
        (output / "ready/train.jsonl").write_bytes(b"forged approval\n")
        self.failure("EXISTING_OUTPUT_CHANGED")
        self.assertEqual(b"forged approval\n", (output / "ready/train.jsonl").read_bytes())

    def test_unexpected_output_file_fails(self):
        output, _ = self.compile()
        (output / "unknown.txt").write_bytes(b"unknown")
        self.failure("EXISTING_OUTPUT_SET_CHANGED")

    def test_custom_output_cannot_escape_fingerprint(self):
        with self.assertRaisesRegex(tm.MaterialFailure, "OUTPUT_FINGERPRINT_PATH_REQUIRED"):
            self.compile(self.root / "compiled/other")

    def test_duplicate_inputs_all_quarantined(self):
        self.rows[1]["input"] = copy.deepcopy(self.rows[0]["input"])
        self.rows[1]["target"] = copy.deepcopy(self.rows[0]["target"])
        self.write_raw()
        _, summary = self.compile()
        self.assertEqual(2, summary["quarantinedCases"])
        self.assertEqual(8, summary["candidateCases"])

    def test_cross_split_duplicate_all_quarantined(self):
        a = self.records[0]
        b = next(x for x in self.records if tm.branch_split(x["branchId"]) != tm.branch_split(a["branchId"]))
        self.rows = [row_for(a), row_for(b)]
        self.write_raw()
        _, summary = self.compile()
        self.assertEqual(0, summary["candidateCases"])
        self.assertEqual(2, summary["quarantineReasonCounts"]["CrossSplitInput"])

    def test_conflicting_targets_all_quarantined(self):
        self.rows[1]["input"] = copy.deepcopy(self.rows[0]["input"])
        self.rows[1]["target"] = {"actionCode": "DispatchNow", "reasonCode": "CollectionElapsed",
                                  "recheckInSeconds": None, "hardDeadlineInSeconds": 59, "allowedActions": ["DispatchNow"]}
        self.write_raw()
        _, summary = self.compile()
        self.assertEqual(2, summary["quarantineReasonCounts"]["ConflictingTarget"])

    def test_wrong_branch_fold_fails(self):
        self.rows[0]["split"] = next(x for x in tm.SPLITS if x != self.rows[0]["split"])
        self.write_raw(); self.failure("SOURCE_SPLIT_MISMATCH")

    def test_wrong_branch_link_fails(self):
        self.rows[0]["sourceBranchIds"] = [self.records[1]["branchId"]]
        self.write_raw(); self.failure("SOURCE_SPLIT_MISMATCH")

    def test_unknown_source_fails(self):
        self.rows[0]["sourceRecordIds"] = [identifier("record", "unknown")]
        self.write_raw(); self.failure("SOURCE_RECORD_INVALID")

    def test_duplicate_case_fails(self):
        self.rows.append(copy.deepcopy(self.rows[0]))
        self.write_raw(); self.failure("CASE_DUPLICATED")

    def test_forged_human_approval_fails(self):
        self.invalid_row("review", "status", "Approved", "HUMAN_APPROVAL_FORBIDDEN")

    def test_forged_training_eligibility_fails(self):
        self.invalid_row("review", "eligibleForTraining", True, "HUMAN_APPROVAL_FORBIDDEN")

    def test_forged_optimality_fails(self):
        self.invalid_row("teacher", "optimalityProven", True, "TEACHER_APPROVAL_INVALID")

    def test_forged_historical_timing_fails(self):
        self.invalid_row("provenance", "historicalTimingObserved", True, "SYNTHETIC_BOUNDARY_INVALID")

    def test_real_customer_locations_forbidden(self):
        self.invalid_row("provenance", "realCustomerLocationsUsed", 1, "SYNTHETIC_BOUNDARY_INVALID")

    def test_customer_address_in_input_forbidden(self):
        self.invalid_row("input", "customerAddress", "private address", "INPUT_KEYS")

    def test_absolute_timestamp_in_input_forbidden(self):
        self.invalid_row("input", "nowUtc", "2026-10-11T00:00:00Z", "INPUT_KEYS")

    def test_fee_goal_not_silently_added(self):
        self.invalid_row("input", "targetAverageFeeKrw", 4000, "INPUT_KEYS")

    def test_boolean_count_forbidden(self):
        self.invalid_row("input", "availableDrivers", True, "INPUT_COUNT_INVALID")

    def test_negative_order_age_forbidden(self):
        self.invalid_row("input", "orderAgeSeconds", -1, "ORDER_AGE_INVALID")

    def test_nonfinite_number_forbidden(self):
        raw = self.raw.read_bytes().replace(b'"expectedNewOrders":1.5', b'"expectedNewOrders":NaN', 1)
        self.raw.write_bytes(raw)
        self.change_manifest("rawSha256", tm.digest(raw))
        self.failure("NONFINITE_JSON")

    def test_duplicate_json_keys_forbidden(self):
        raw = self.raw.read_bytes().replace(b'"schemaVersion":1', b'"schemaVersion":1,"schemaVersion":1', 1)
        self.raw.write_bytes(raw)
        self.change_manifest("rawSha256", tm.digest(raw))
        self.failure("DUPLICATE_JSON_KEY")

    def test_unknown_action_forbidden(self):
        self.invalid_row("target", "actionCode", "IgnoreDeadline", "TARGET_CODE_INVALID")

    def test_unapproved_action_forbidden(self):
        self.invalid_row("target", "allowedActions", ["DispatchNow"], "ALLOWED_ACTIONS_INVALID")

    def test_recheck_cannot_extend_deadline(self):
        self.invalid_row("target", "recheckInSeconds", 60, "RECHECK_EXCEEDS_DEADLINE")

    def test_recheck_cannot_extend_collection_window(self):
        self.invalid_row("target", "recheckInSeconds", 35, "RECHECK_EXCEEDS_DEADLINE")

    def test_hard_deadline_cannot_extend_original_queue_age(self):
        self.invalid_row("target", "hardDeadlineInSeconds", 60, "DEADLINE_CONTRACT_MISMATCH")

    def test_noncollect_cannot_recheck(self):
        self.rows[0]["target"].update(actionCode="DispatchNow", allowedActions=["DispatchNow"])
        self.write_raw(); self.failure("UNEXPECTED_RECHECK")

    def test_source_policy_changed_fails(self):
        (self.repo / tm.TEACHER_FILES[0]).write_bytes(b"changed policy")
        self.failure("SOURCE_HASH_PIN_MISMATCH")

    def test_raw_hash_tamper_fails(self):
        self.raw.write_bytes(self.raw.read_bytes() + b" ")
        self.failure("INPUT_HASH_MISMATCH")

    def test_model_calls_claim_forbidden(self):
        self.change_manifest("modelCalls", 1); self.failure("EXECUTION_OR_APPROVAL_FORBIDDEN")

    def test_weight_training_claim_forbidden(self):
        self.change_manifest("weightsTrained", True); self.failure("EXECUTION_OR_APPROVAL_FORBIDDEN")

    def test_operational_writes_claim_forbidden(self):
        self.change_manifest("operationalWrites", 1); self.failure("EXECUTION_OR_APPROVAL_FORBIDDEN")

    def test_unaccepted_work_order_fails(self):
        work = tm.parse_json(self.work.read_bytes())
        work["acceptedInCurrentThread"] = False
        self.work.write_bytes(tm.json_bytes(work))
        self.failure("ACCEPTED_WORK_ORDER_REQUIRED")

    def test_unapproved_dataset_hash_fails(self):
        self.dataset.write_bytes(self.dataset.read_bytes() + b" ")
        self.failure("DATASET_HASH_MISMATCH")

    def test_case_count_mismatch_fails(self):
        self.change_manifest("caseCount", 999); self.failure("CASE_COUNT_MISMATCH")

    def test_wrong_seed_fails(self):
        self.change_manifest("fixedSeed", 51); self.failure("SPLIT_CONTRACT_INVALID")

    def test_wrong_policy_version_fails(self):
        self.invalid_row("teacher", "policyVersion", "food-dispatch-timing/v0", "TEACHER_APPROVAL_INVALID")

    def test_settings_collection_cannot_exceed_maxwait(self):
        self.rows[0]["input"]["settings"]["collectionSeconds"] = 61
        self.write_raw(); self.failure("SETTINGS_ORDER_INVALID")


if __name__ == "__main__":
    unittest.main()
