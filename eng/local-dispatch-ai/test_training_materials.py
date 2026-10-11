"""실자료·모델·네트워크 없이 합성 파일로 r51 자료 경계를 시험한다."""
from __future__ import annotations

import copy
import importlib.util
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("training_materials", Path(__file__).with_name("training_materials.py"))
tm = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(tm)
ORIGINAL_REPO = tm.REPO


def identifier(kind, seed):
    return "replay-" + kind + "-" + tm.digest(seed.encode())


def demand_input(seed=1):
    return {"planningWindowMinutes": 30, "hourlyTarget": 3.5, "observationWindowMinutes": 15,
            "demandObservationAgeMinutes": 0, "waitingOrders": seed, "recentOrders": 2, "forecastOrders": None,
            "delayRisk": False, "noEligibleActiveDriver": False,
            "drivers": [{"driverToken": "D1", "eligible": True, "active": True, "shiftAgeMinutes": 90,
                         "waitMinutes": 10, "observationAgeMinutes": 0, "remainingMinutes": 5, "recentAssignments": 2},
                        {"driverToken": "D2", "eligible": True, "active": False, "shiftAgeMinutes": 30,
                         "waitMinutes": 20, "observationAgeMinutes": 0, "remainingMinutes": None, "recentAssignments": 0}]}


def demand_target():
    return {"dataStateCode": "Current", "reasonCode": "DemandAndCapacityObserved", "targetActiveDrivers": 3,
            "additionalParticipants": 1,
            "driverDecisions": [{"driverToken": "D2", "statusCode": "AdmissionRecommended", "reasonCode": "DemandCapacityAvailable",
                                 "allowProposal": True, "admitted": True, "priority": 1},
                                {"driverToken": "D1", "statusCode": "ActiveProtected", "reasonCode": "ExistingActivityProtected",
                                 "allowProposal": True, "admitted": False, "priority": 2}]}


def row_for(record, family="shortage", seed=1):
    task = "DemandParticipation"
    return {"schemaVersion": 1, "caseId": tm.case_id([record["recordId"]], task, family), "taskCode": task,
            "scenarioFamily": family, "sourceRecordIds": [record["recordId"]], "sourceBranchIds": [record["branchId"]],
            "split": tm.branch_split(record["branchId"]), "input": demand_input(seed), "target": demand_target(),
            "teacher": {"kind": "CurrentPolicyDraft", "policyVersion": tm.POLICIES[task], "optimalityProven": False},
            "provenance": {"synthetic": True, "routeBasis": "NotApplicable", "realCustomerLocationsUsed": 0},
            "review": {"status": "Pending", "eligibleForTraining": False}}


class TrainingMaterialsTests(unittest.TestCase):
    def setUp(self):
        # 임시 파일도 기존 비공개 산출물 경계 아래에만 만든다.
        fixture_root = tm.ROOT / "test-fixtures"
        fixture_root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="synthetic-", dir=fixture_root)
        self.repo = Path(self.temp.name)
        # Windows의 짧은 경로 제한 안에서, 실제 비공개 경계 아래의 합성 저장소만 쓴다.
        self.local = self.repo / "a"
        self.root = self.repo / "r"
        self.dataset_root = self.repo / "d"
        self.delivery = self.repo / "p"
        self.design = self.repo / tm.DESIGN_REF
        self.design.parent.mkdir(parents=True)
        self.design.write_bytes(b"synthetic approved design\n")
        source = self.repo / "artifacts/local/delivery-history-replay-r54/dataset.json"
        source.parent.mkdir(parents=True)
        source.write_bytes(b'{"syntheticFixture":true}\n')
        self.source = source
        self.public_source = self.delivery / "edit-kit/branch-deliveries-all-20261010/synthetic/plan.json"
        self.public_source.parent.mkdir(parents=True)
        self.public_source.write_bytes(b'{"publicSyntheticStore":true}\n')
        for relative in (*tm.TEACHER_FILES, tm.EXPORTER_REF):
            path = self.repo / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"synthetic teacher source\n")
        for folder, pattern in tm.TEACHER_GLOBS:
            path = self.repo / folder / pattern.replace("*", "Synthetic")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"synthetic teacher glob source\n")
        ledger = self.delivery / tm.LEDGER_REF
        ledger.write_bytes(b'{"syntheticLedger":true}\n')
        self.records = [{"recordId": identifier("record", str(i)), "branchId": identifier("branch", str(i)),
                         "district": tm.DISTRICTS[i % 3], "publicStoreName": "테스트 공개 매장",
                         "publicPickupAddress": "공개 주소 원문은 messages에 들어가면 안 됨",
                         "historical": {"menuItems": [{"name": "비공개 출처 메뉴"}]}} for i in range(60)]
        fp = tm.digest(b"synthetic dataset fingerprint")
        dataset = {"metadata": {"policyRevision": "regional-management-r50.dataset.r1", "localVerificationOnly": True,
                                "executionModeCode": "Simulation", "visitMenuSummariesAllocated": False,
                                "realCustomerLocationsUsed": 0, "realDriverLocationsUsed": 0, "sourceFingerprint": fp,
                                "sourceLedgerSha256": tm.digest(ledger.read_bytes()),
                                "toolSha256": tm.digest((self.repo / tm.EXPORTER_REF).read_bytes()),
                                "sourceFingerprints": [{"relativePath": source.relative_to(self.repo).as_posix(),
                                                        "sha256": tm.digest(source.read_bytes())},
                                                       {"relativePath": self.public_source.relative_to(self.delivery).as_posix(),
                                                        "sha256": tm.digest(self.public_source.read_bytes())}]}, "records": self.records}
        self.dataset = self.dataset_root / fp / "dataset.json"
        self.dataset.parent.mkdir(parents=True)
        self.dataset.write_bytes(tm.json_bytes(dataset))
        self.dataset.with_name("summary.json").write_bytes(tm.json_bytes({"datasetSha256": tm.digest(self.dataset.read_bytes())}))
        (self.repo / tm.WORK_ORDER_REF).write_bytes(tm.json_bytes({"acceptedInCurrentThread": True, "implementationAllowed": True,
            "designSha256": tm.digest(self.design.read_bytes()), "datasetSha256": tm.digest(self.dataset.read_bytes()),
            "modelCallsAllowed": 0, "weightTrainingAllowed": False, "operationalChangesAllowed": False}))
        self.raw = self.root / "raw" / tm.digest(b"synthetic generator fingerprint") / "raw.jsonl"
        self.raw.parent.mkdir(parents=True)
        self.manifest_path = self.raw.with_name("raw.manifest.json")
        self.patches = [patch.object(tm, "REPO", self.repo), patch.object(tm, "LOCAL", self.local),
                        patch.object(tm, "ROOT", self.root), patch.object(tm, "DATASET_ROOT", self.dataset_root),
                        patch.object(tm, "DELIVERY_ROOT", self.delivery), patch.object(tm, "DESIGN_SHA", tm.digest(self.design.read_bytes())),
                        patch.object(tm, "EXPECTED_DATASET_SHA", tm.digest(self.dataset.read_bytes())),
                        patch.object(tm, "ensure_ignored", lambda _path: None)]
        for item in self.patches:
            item.start()
        self.rows = [row_for(self.records[i], seed=i + 1) for i in range(15)]
        self.write_raw()

    def tearDown(self):
        for item in reversed(self.patches):
            item.stop()
        self.temp.cleanup()

    def write_raw(self):
        hashes = {self.dataset.relative_to(self.repo).as_posix(): tm.digest(self.dataset.read_bytes()),
                  self.source.relative_to(self.repo).as_posix(): tm.digest(self.source.read_bytes()),
                  self.public_source.relative_to(self.delivery).as_posix(): tm.digest(self.public_source.read_bytes()),
                  tm.LEDGER_REF: tm.digest((self.delivery / tm.LEDGER_REF).read_bytes()),
                  self.dataset.with_name("summary.json").relative_to(self.repo).as_posix(): tm.digest(self.dataset.with_name("summary.json").read_bytes())}
        hashes.update({name: tm.digest(tm.source_path(name).read_bytes()) for name in tm.teacher_source_paths()})
        self.raw = self.root / "raw" / tm.generator_fingerprint(hashes) / "raw.jsonl"
        self.raw.parent.mkdir(parents=True, exist_ok=True)
        self.manifest_path = self.raw.with_name("raw.manifest.json")
        self.raw.write_bytes(b"".join(tm.json_bytes(row) for row in self.rows))
        manifest = {"schemaVersion": 1, "formatVersion": "dispatch-training-raw/v1", "datasetSha256": tm.digest(self.dataset.read_bytes()),
                    "sourceHashes": hashes,
                    "rawSha256": tm.digest(self.raw.read_bytes()), "caseCount": len(self.rows), "modelCalls": 0,
                    "weightsTrained": False, "humanApprovals": 0, "generatorFingerprint": self.raw.parent.name,
                    "designSha256": tm.DESIGN_SHA, "sourceHashesUnchanged": True, "splitRule": tm.SPLIT_RULE, "fixedSeed": 51}
        self.manifest_path.write_bytes(tm.json_bytes(manifest))

    def manifest_change(self, key, value):
        manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
        manifest[key] = value
        self.manifest_path.write_bytes(tm.json_bytes(manifest))

    def compile(self):
        return tm.compile_materials(self.raw, self.dataset)

    def failure(self, code):
        with self.assertRaisesRegex(tm.MaterialFailure, "^" + code + "$"):
            self.compile()
        self.assertFalse((self.root / "compiled").exists())

    def test_candidate_contract_ready_empty_and_private_messages(self):
        path, summary = self.compile()
        self.assertEqual(15, summary["candidateCases"])
        self.assertEqual(0, summary["quarantinedCases"])
        self.assertEqual(0, summary["modelCalls"])
        self.assertEqual(0, summary["humanApprovals"])
        self.assertFalse(summary["weightsTrained"])
        self.assertTrue(12 <= summary["reviewRepresentatives"] <= 24)
        for split in tm.SPLITS:
            self.assertEqual(b"", (path / "ready" / f"{split}.jsonl").read_bytes())
            for line in (path / "candidates" / f"{split}.jsonl").read_bytes().splitlines():
                candidate = tm.parse_json(line)
                self.assertEqual({"messages", "auditMetadata"}, set(candidate))
                messages = tm.canonical(candidate["messages"])
                for forbidden in ("replay-record-", "replay-branch-", "publicPickupAddress", "테스트 공개 매장", "비공개 출처 메뉴", "latitude", "longitude"):
                    self.assertNotIn(forbidden, messages)
                self.assertFalse(candidate["auditMetadata"]["review"]["eligibleForTraining"])

    def test_identical_run_is_byte_identical(self):
        first, _ = self.compile()
        before = {x.relative_to(first).as_posix(): x.read_bytes() for x in first.rglob("*") if x.is_file()}
        second, _ = self.compile()
        self.assertEqual(first, second)
        self.assertEqual(before, {x.relative_to(second).as_posix(): x.read_bytes() for x in second.rglob("*") if x.is_file()})

    def test_originals_unchanged(self):
        paths = (self.dataset, self.raw, self.manifest_path, self.source, self.public_source)
        before = [x.read_bytes() for x in paths]
        self.compile()
        self.assertEqual(before, [x.read_bytes() for x in paths])

    def test_raw_tamper_fails_before_output(self):
        before = self.raw.read_bytes()
        after = before.replace(b'"waitingOrders":1}', b'"waitingOrders":2}', 1)
        self.assertNotEqual(before, after)
        self.raw.write_bytes(after)
        self.failure("INPUT_HASH_MISMATCH")

    def test_dataset_tamper_even_rehashed_manifest_fails(self):
        self.dataset.write_bytes(self.dataset.read_bytes().replace("중랑구".encode(), "광진구".encode(), 1))
        self.write_raw()
        self.failure("INPUT_HASH_MISMATCH")

    def test_source_tamper_fails(self):
        self.source.write_bytes(b"changed")
        self.failure("SOURCE_HASH_CHANGED")

    def test_public_source_tamper_fails(self):
        self.public_source.write_bytes(b"changed")
        self.failure("SOURCE_HASH_CHANGED")

    def test_missing_source_pin_fails(self):
        manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
        del manifest["sourceHashes"][self.source.relative_to(self.repo).as_posix()]
        self.manifest_path.write_bytes(tm.json_bytes(manifest))
        self.failure("SOURCE_HASH_PIN_MISSING")

    def test_teacher_policy_pin_and_generator_pin_cannot_be_omitted(self):
        for relative in (tm.TEACHER_FILES[0], tm.EXPORTER_REF, tm.DESIGN_REF,
                         "Ssalddel/Services/Food/음식주문현재조리Policy.cs", "Ssalddel/Services/Options/FoodDriverRoutePricingOptions.cs",
                         "eng/local-dispatch-ai/TrainingMaterialRunner/Synthetic.cs"):
            with self.subTest(relative=relative):
                self.write_raw()
                manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
                del manifest["sourceHashes"][relative]
                self.manifest_path.write_bytes(tm.json_bytes(manifest))
                self.failure("SOURCE_HASH_PIN_MISSING")

    def test_fixed_teacher_source_contract_matches_generator_source(self):
        # 공개 코드 계약만 읽고 실제 원장·원시 자료는 읽거나 실행하지 않는다.
        source = (ORIGINAL_REPO / "eng/local-dispatch-ai/TrainingMaterialRunner/TrainingSources.cs").read_text(encoding="utf-8")
        block = source.split("foreach (var relative in new[] {", 1)[1].split("})", 1)[0]
        generator_files = set(re.findall(r'"([^"\n]+\.cs)"', block))
        self.assertEqual(generator_files, set(tm.TEACHER_FILES))

    def test_teacher_policy_change_is_detected(self):
        (self.repo / tm.TEACHER_FILES[0]).write_bytes(b"changed policy")
        self.failure("SOURCE_HASH_PIN_MISSING")

    def test_generator_fingerprint_cannot_be_forged(self):
        self.manifest_change("generatorFingerprint", "0" * 64)
        self.failure("GENERATOR_FINGERPRINT_MISMATCH")

    def test_work_order_permission_cannot_be_removed(self):
        path = self.repo / tm.WORK_ORDER_REF
        work = tm.parse_json(path.read_bytes())
        work["implementationAllowed"] = False
        path.write_bytes(tm.json_bytes(work))
        self.failure("ACCEPTED_WORK_ORDER_REQUIRED")

    def test_source_path_traversal_fails(self):
        manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
        manifest["sourceHashes"]["../private.json"] = tm.digest(b"nothing")
        self.manifest_path.write_bytes(tm.json_bytes(manifest))
        self.failure("SOURCE_PATH_INVALID")

    def test_unknown_source_path_fails(self):
        secret = self.repo / "customer.json"
        secret.write_bytes(b"synthetic only")
        manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
        manifest["sourceHashes"]["customer.json"] = tm.digest(secret.read_bytes())
        self.manifest_path.write_bytes(tm.json_bytes(manifest))
        self.failure("SOURCE_HASH_SET_MISMATCH")

    def test_manifest_model_and_approval_claims_fail(self):
        for key, value, code in (("modelCalls", 1, "EXECUTION_OR_APPROVAL_FORBIDDEN"),
                                 ("humanApprovals", True, "EXECUTION_OR_APPROVAL_FORBIDDEN"),
                                 ("weightsTrained", True, "MANIFEST_BOUNDARY_INVALID")):
            with self.subTest(key=key):
                self.write_raw()
                self.manifest_change(key, value)
                self.failure(code)

    def test_self_approval_and_optimality_fail(self):
        for area, key, value, code in (("review", "status", "Approved", "SELF_APPROVAL_FORBIDDEN"),
                                       ("review", "eligibleForTraining", True, "SELF_APPROVAL_FORBIDDEN"),
                                       ("teacher", "optimalityProven", True, "TEACHER_CLAIM_INVALID"),
                                       ("teacher", "kind", "ModelAnswer", "TEACHER_CLAIM_INVALID")):
            with self.subTest(key=key):
                before = copy.deepcopy(self.rows[0])
                self.rows[0][area][key] = value
                self.write_raw()
                self.failure(code)
                self.rows[0] = before

    def test_privacy_extra_fields_fail(self):
        for key, value in (("customerAddress", "합성 금지 주소"), ("latitude", 37.5), ("longitude", 127.1),
                           ("menuName", "합성 메뉴"), ("phone", "010-0000-0000"), ("driverId", "real-driver"),
                           ("baselineRank", 1)):
            with self.subTest(key=key):
                self.rows[0]["input"][key] = value
                self.write_raw()
                self.failure("DEMAND_INPUT_KEYS")
                del self.rows[0]["input"][key]

    def test_freeform_reason_and_token_leak_fail(self):
        self.rows[0]["target"]["reasonCode"] = "고객 전화 01000000000"
        self.write_raw()
        self.failure("REASON_INVALID")
        self.rows[0]["target"] = demand_target()
        self.rows[0]["input"]["drivers"][0]["driverToken"] = self.records[0]["recordId"]
        self.write_raw()
        self.failure("ANONYMOUS_TOKEN_INVALID")

    def test_boolean_is_not_integer_or_numeric(self):
        self.rows[0]["input"]["waitingOrders"] = True
        self.write_raw()
        self.failure("DEMAND_COUNT")
        self.rows[0]["input"]["waitingOrders"] = "1"
        self.write_raw()
        self.failure("DEMAND_COUNT")

    def test_numeric_is_not_boolean(self):
        self.rows[0]["input"]["delayRisk"] = 1
        self.write_raw()
        self.failure("DEMAND_BOOLEAN")

    def test_large_integer_and_negative_duration_fail(self):
        self.rows[0]["input"]["drivers"][0]["waitMinutes"] = 10 ** 1000
        self.write_raw()
        self.failure("DRIVER_MINUTES")
        self.rows[0]["input"]["drivers"][0]["waitMinutes"] = -1
        self.write_raw()
        self.failure("DRIVER_MINUTES")

    def test_nan_infinity_and_overflow_fail(self):
        for literal in (b"NaN", b"Infinity", b"-Infinity", b"1e999"):
            with self.subTest(literal=literal):
                self.write_raw()
                data = self.raw.read_bytes().replace(b'"hourlyTarget":3.5', b'"hourlyTarget":' + literal, 1)
                self.raw.write_bytes(data)
                self.manifest_change("rawSha256", tm.digest(data))
                self.failure("HOURLY_BASELINE_CHANGED" if literal == b"1e999" else "NONFINITE_JSON")

    def test_duplicate_json_keys_fail(self):
        data = self.raw.read_bytes().replace(b'"schemaVersion":1', b'"schemaVersion":1,"schemaVersion":1', 1)
        self.raw.write_bytes(data)
        self.manifest_change("rawSha256", tm.digest(data))
        self.failure("DUPLICATE_JSON_KEY")

    def test_changed_split_fails(self):
        self.rows[0]["split"] = next(x for x in tm.SPLITS if x != self.rows[0]["split"])
        self.write_raw()
        self.failure("SPLIT_LEAKAGE")

    def test_branch_source_mismatch_fails(self):
        self.rows[0]["sourceBranchIds"] = [self.records[1]["branchId"]]
        self.write_raw()
        self.failure("SOURCE_BRANCH_MISMATCH")

    def test_unknown_source_record_fails(self):
        self.rows[0]["sourceRecordIds"] = [identifier("record", "unknown")]
        self.write_raw()
        self.failure("SOURCE_RECORD_UNKNOWN")

    def test_duplicate_and_conflicting_inputs_all_quarantined(self):
        self.rows[1]["input"] = copy.deepcopy(self.rows[0]["input"])
        self.rows[2]["input"] = copy.deepcopy(self.rows[0]["input"])
        self.rows[2]["target"]["targetActiveDrivers"] += 1
        self.write_raw()
        path, summary = self.compile()
        self.assertEqual(3, summary["quarantinedCases"])
        self.assertEqual(12, summary["candidateCases"])
        quarantined = [tm.parse_json(x) for x in (path / "quarantine.jsonl").read_bytes().splitlines()]
        self.assertEqual({x["caseId"] for x in self.rows[:3]}, {x["auditMetadata"]["caseId"] for x in quarantined})
        self.assertTrue(all("ConflictingTargets" in x["auditMetadata"]["quarantineReasonCodes"] for x in quarantined))

    def test_cross_split_same_input_all_quarantined(self):
        pair = next((a, b) for a in self.rows for b in self.rows if a["split"] != b["split"])
        pair[1]["input"] = copy.deepcopy(pair[0]["input"])
        self.write_raw()
        path, summary = self.compile()
        self.assertEqual(2, summary["quarantineReasonCounts"]["CrossSplitInput"])
        for split in tm.SPLITS:
            self.assertNotIn(tm.json_bytes(pair[0]["input"]), (path / "candidates" / f"{split}.jsonl").read_bytes())

    def test_duplicate_case_ids_fail(self):
        self.rows.append(copy.deepcopy(self.rows[0]))
        self.write_raw()
        self.failure("CASE_ID_DUPLICATED")

    def test_ready_tampering_is_not_self_approved(self):
        path, _ = self.compile()
        (path / "ready/train.jsonl").write_bytes(b'{"approved":true}\n')
        with self.assertRaisesRegex(tm.MaterialFailure, "^EXISTING_OUTPUT_TAMPERED$"):
            self.compile()

    def test_added_or_modified_output_fails(self):
        path, _ = self.compile()
        (path / "extra.json").write_bytes(b"{}")
        with self.assertRaisesRegex(tm.MaterialFailure, "^EXISTING_OUTPUT_TAMPERED$"):
            self.compile()

    def test_extra_output_directory_fails(self):
        path, _ = self.compile()
        (path / "approved").mkdir()
        with self.assertRaisesRegex(tm.MaterialFailure, "^EXISTING_OUTPUT_TAMPERED$"):
            self.compile()

    def test_output_escape_rejected(self):
        with self.assertRaisesRegex(tm.MaterialFailure, "^PATH_OUTSIDE_BOUNDARY$"):
            tm.compile_materials(self.raw, self.dataset, self.repo / "public/train")

    def test_arbitrary_output_inside_root_rejected(self):
        with self.assertRaisesRegex(tm.MaterialFailure, "^OUTPUT_FINGERPRINT_PATH_REQUIRED$"):
            tm.compile_materials(self.raw, self.dataset, self.root / "compiled/approved")

    def test_split_is_stable_branch_unit(self):
        for record in self.records:
            expected = int(tm.digest(record["branchId"].encode())[:8], 16) % 100
            self.assertEqual("train" if expected < 70 else "validation" if expected < 85 else "test", tm.branch_split(record["branchId"]))

    def bundle(self):
        row = row_for(self.records[0])
        row["taskCode"], row["scenarioFamily"] = "BundleSelection", "single"
        row["caseId"] = tm.case_id(row["sourceRecordIds"], row["taskCode"], row["scenarioFamily"])
        row["teacher"]["policyVersion"] = tm.POLICIES[row["taskCode"]]
        row["provenance"]["routeBasis"] = "GeometryApproximation"
        row["input"] = {"demand": demand_input(100), "orders": [{"orderToken": "O1", "requestOrdinal": 1}],
                        "plans": [{"planToken": "P2", "orderTokens": ["O1"], "orderCount": 1,
                        "pickupSeparationKm": None, "dropoffSeparationKm": None, "routeDistanceKm": 1.5,
                        "samePickup": True, "samePickupRegion": True, "sameDropoffRegion": True,
                        "regionAdjacent": False, "pickupWindowGapMinutes": None}],
                        "limits": {"maximumBundleSize": 2, "maximumPickupSeparationKm": 3, "maximumDropoffSeparationKm": 3,
                                   "maximumApproximateRouteKm": 8, "recommendedPickupSeparationKm": 1,
                                   "recommendedDropoffSeparationKm": 1, "recommendedPickupWindowGapMinutes": 5}}
        row["target"] = {"actionCode": "AdditionalParticipationNeeded", "aiStatusCode": "Disabled", "selectedPlanToken": "P2",
                         "allowedDriverTokens": ["D2", "D1"], "requiresCurrentGates": True, "operationalApplied": False,
                         "capacityVerified": False, "promisesVerified": False}
        return row

    def test_bundle_and_arrival_supported(self):
        bundle = self.bundle()
        arrival = row_for(self.records[1])
        arrival["taskCode"], arrival["scenarioFamily"] = "ArrivalAnchor", "valid"
        arrival["caseId"] = tm.case_id(arrival["sourceRecordIds"], arrival["taskCode"], arrival["scenarioFamily"])
        arrival["teacher"]["policyVersion"] = tm.POLICIES[arrival["taskCode"]]
        arrival["provenance"]["routeBasis"] = "SyntheticVehicleRoutePort"
        arrival["input"] = {"activeJobCount": 1, "driverObservationAgeMinutes": 0, "hasAttemptProof": True,
                            "remainingRouteMinutes": 1.2, "nextPickupRouteMinutes": 0.4, "pickupReadyInMinutes": 5,
                            "vehicleRouteOption": "traavoidcaronly", "earlyToleranceMinutes": 3, "lateToleranceMinutes": 5,
                            "driverToken": "D1"}
        arrival["target"] = {"decisionCode": "CandidateAllowed", "selectedDriverToken": "D1"}
        self.rows = [bundle, arrival]
        self.write_raw()
        _, summary = self.compile()
        self.assertEqual(2, summary["candidateCases"])

    def test_bundle_unknown_selected_plan_and_operation_claim_fail(self):
        self.rows = [self.bundle()]
        self.rows[0]["target"]["selectedPlanToken"] = "P1"
        self.write_raw()
        self.failure("TARGET_PLAN_UNKNOWN")
        self.rows[0]["target"]["selectedPlanToken"] = "P2"
        self.rows[0]["target"]["operationalApplied"] = True
        self.write_raw()
        self.failure("OPERATIONAL_OR_VERIFICATION_CLAIM_FORBIDDEN")

    def test_bundle_labels_may_not_be_input_features(self):
        self.rows = [self.bundle()]
        self.rows[0]["input"]["plans"][0]["baselineRank"] = 1
        self.write_raw()
        self.failure("PLAN_KEYS")

    def test_review_includes_two_order_single_fallback(self):
        row = self.bundle()
        other = next(x for x in self.records if x["recordId"] != row["sourceRecordIds"][0]
                     and tm.branch_split(x["branchId"]) == row["split"])
        row["sourceRecordIds"].append(other["recordId"])
        row["sourceBranchIds"].append(other["branchId"])
        row["scenarioFamily"] = "two-order"
        row["caseId"] = tm.case_id(row["sourceRecordIds"], row["taskCode"], row["scenarioFamily"])
        row["input"]["orders"].append({"orderToken": "O2", "requestOrdinal": 2})
        additional = copy.deepcopy(row["input"]["plans"][0])
        additional.update(planToken="P1", orderTokens=["O2"])
        row["input"]["plans"].append(additional)
        self.rows.append(row)
        self.write_raw()
        path, _ = self.compile()
        text = (path / "review.md").read_text(encoding="utf-8")
        self.assertIn("실제 정책은 단건을 선택했습니다", text)
        self.assertIn("합성 요청 순서: 1번째 O1 → 2번째 O2", text)

    def test_review_does_not_hide_ninth_driver(self):
        self.rows = self.rows[:1]
        for index in range(3, 10):
            driver = copy.deepcopy(self.rows[0]["input"]["drivers"][1])
            driver.update(driverToken=f"D{index}", waitMinutes=30 + index)
            self.rows[0]["input"]["drivers"].append(driver)
            decision = copy.deepcopy(self.rows[0]["target"]["driverDecisions"][0])
            decision.update(driverToken=f"D{index}", priority=index)
            self.rows[0]["target"]["driverDecisions"].append(decision)
        self.write_raw()
        path, _ = self.compile()
        text = (path / "review.md").read_text(encoding="utf-8")
        self.assertIn("| D9 | 신규 대기 | 39분 |", text)

    def test_request_order_must_be_contiguous_and_tokens_must_match(self):
        self.rows = [self.bundle()]
        self.rows[0]["input"]["orders"][0]["requestOrdinal"] = 2
        self.write_raw()
        self.failure("REQUEST_ORDINAL_SEQUENCE_INVALID")
        self.rows[0]["input"]["orders"][0]["requestOrdinal"] = 1
        self.rows[0]["input"]["orders"][0]["orderToken"] = "O2"
        self.write_raw()
        self.failure("PLAN_ORDER_UNKNOWN")

    def test_multi_branch_must_share_split(self):
        bundle = self.bundle()
        second = next(x for x in self.records if tm.branch_split(x["branchId"]) != bundle["split"])
        bundle["sourceRecordIds"].append(second["recordId"])
        bundle["sourceBranchIds"].append(second["branchId"])
        bundle["caseId"] = tm.case_id(bundle["sourceRecordIds"], bundle["taskCode"], bundle["scenarioFamily"])
        self.rows = [bundle]
        self.write_raw()
        self.failure("SPLIT_LEAKAGE")

    def test_token_mapping_permutation_does_not_rewrite_teacher(self):
        self.rows[0]["input"]["drivers"].reverse()
        self.rows[0]["target"]["driverDecisions"].reverse()
        self.write_raw()
        path, _ = self.compile()
        entries = [tm.parse_json(x) for split in tm.SPLITS for x in (path / "candidates" / f"{split}.jsonl").read_bytes().splitlines()]
        match = next(x for x in entries if x["auditMetadata"]["caseId"] == self.rows[0]["caseId"])
        self.assertEqual(self.rows[0]["target"], json.loads(match["messages"][2]["content"]))

    def test_review_covers_districts_and_families_and_is_pending(self):
        self.rows = [row_for(self.records[i], family=family, seed=i + 1) for i, family in enumerate(tm.FAMILIES["DemandParticipation"] * 3)]
        self.write_raw()
        path, summary = self.compile()
        text = (path / "review.md").read_text(encoding="utf-8")
        self.assertTrue(12 <= summary["reviewRepresentatives"] <= 24)
        for district in tm.DISTRICTS:
            self.assertIn(district, text)
        self.assertEqual(summary["reviewRepresentatives"], text.count("검토: **Pending**"))
        self.assertIn("| 기사 | 구분 | 대기 |", text)
        self.assertIn("추가 참여 권고", text)
        self.assertNotIn('"planningWindowMinutes":', text)


if __name__ == "__main__":
    unittest.main()
