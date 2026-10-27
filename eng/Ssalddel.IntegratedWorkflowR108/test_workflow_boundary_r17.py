"""r17의 의뢰·운송·사례·준비 기록 결속만 합성 검사한다. HTTP/DB/기기를 실행하지 않는다."""
import importlib.util
import json
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("r108_workflow_r17", Path(__file__).with_name("workflow.py"))
workflow = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(workflow)


class RunIdentityTests(unittest.TestCase):
    def test_pascal_and_camel_are_each_accepted(self):
        for name in ("RunStableId", "runStableId"):
            self.assertEqual("owned-run", workflow.canonical_run_id({name: "owned-run"}))

    def test_missing_invalid_and_duplicate_aliases_are_rejected(self):
        for value in ({}, None, {"runStableId": ""}, {"RunStableId": None},
                      {"RunStableId": "a", "runStableId": "a"},
                      {"RunStableId": "a", "runStableId": "b"}):
            with self.subTest(value=value), self.assertRaisesRegex(RuntimeError, "R108SingleRunStableIdRequired"):
                workflow.canonical_run_id(value)

    def test_schema_boundary_reports_each_actual_preparation_mode(self):
        current = workflow.cargo_schema_boundaries({"schemaMode": "CurrentModel"})
        migrated = workflow.cargo_schema_boundaries({"schemaMode": "MigrationFirst"})
        self.assertTrue(current["freshEnsureCreatedNotMigration"])
        self.assertFalse(current["isolatedMigrationChainApplied"])
        self.assertFalse(migrated["freshEnsureCreatedNotMigration"])
        self.assertTrue(migrated["isolatedMigrationChainApplied"])
        self.assertEqual("MigrationFirst", migrated["schemaMode"])
        self.assertFalse(migrated["operationalMigrationVerified"])
        self.assertEqual(current, workflow.cargo_schema_boundaries({}))
        with self.assertRaisesRegex(RuntimeError, "R108SchemaModeBindingMismatch"):
            workflow.cargo_schema_boundaries({"schemaMode": "Unknown"})


class CargoCaseTests(unittest.TestCase):
    def test_new_supported_cases_have_distinct_existing_server_markers(self):
        actual = {workflow.standalone_cargo_case({"attempts": {}, "journalCase": key}, key)
                  for key in workflow.STANDALONE_CARGO_CASES}
        self.assertEqual(workflow.STANDALONE_CARGO_CASES, actual)

    def test_new_named_journal_keeps_compatible_default_case(self):
        self.assertEqual("cargo-phone-quick-02", workflow.standalone_cargo_case(
            {"journalCase": "cargo-r17-phone", "attempts": {}}, "cargo-r17-phone"))

    def test_old_journal_never_changes_its_original_fixed_marker(self):
        for key in ("cargo-r15-phone", "cargo-phone-quick-03"):
            self.assertEqual("cargo-phone-quick-02", workflow.standalone_cargo_case(
                {"requestId": "existing-rid", "attempts": {"cargo-create": {}}}, key))

    def test_persisted_marker_and_journal_must_agree_for_new_cases(self):
        for state, key, error in (
            ({"cargoCaseKey": "cargo-phone-quick-03", "journalCase": "cargo-phone-quick-04"}, "cargo-phone-quick-03", "CargoJournalCaseMismatch"),
            ({"cargoCaseKey": "cargo-phone-quick-04", "journalCase": "cargo-phone-quick-03"}, "cargo-phone-quick-03", "CargoMarkerJournalBindingMismatch"),
            ({"cargoCaseKey": "arbitrary"}, "arbitrary", "CargoCaseKeyNotAllowed"),
            ({"cargoCaseKey": []}, "arbitrary", "CargoCaseKeyNotAllowed"),
        ):
            with self.subTest(state=state), self.assertRaisesRegex(RuntimeError, error):
                workflow.standalone_cargo_case(state, key)


class CargoIdentityTests(unittest.TestCase):
    def test_product_transport_number_is_the_request_identity(self):
        self.assertEqual(7, workflow.cargo_transport_identity({"id": 7, "운송번호": "rid"}, "rid"))
        self.assertEqual(7, workflow.cargo_transport_identity({"Id": 7, "운송번호": "rid", "의뢰Id": "rid"}, "rid", 7))

    def test_other_present_transport_is_not_a_recovered_acceptance(self):
        for value in ({"id": 7, "운송번호": "other"}, {"id": 7}, None,
                      {"id": 0, "운송번호": "rid"}, {"id": True, "운송번호": "rid"},
                      {"id": "7", "운송번호": "rid"}, {"id": 7, "운송번호": "rid", "의뢰Id": "other"}):
            with self.subTest(value=value), self.assertRaisesRegex(RuntimeError, "CargoTransportRequestIdentityMismatch"):
                workflow.cargo_transport_identity(value, "rid")

    def test_same_request_cannot_silently_switch_transport_id(self):
        with self.assertRaisesRegex(RuntimeError, "CargoTransportIdChanged"):
            workflow.cargo_transport_identity({"id": 8, "운송번호": "rid"}, "rid", 7)


class FakePausedCargo(workflow.Workflow):
    def __init__(self, directory, current=None, detail=None, proof_changes=None):
        self.path = Path(directory) / "case.json"
        self.options = SimpleNamespace(runtime_controls=False, pause_after_accept=True)
        self.run_id = "integrated-r108:" + "a" * 32
        self.ready = {"database": "r108m_" + "a" * 32}
        self.journal_case = "cargo-phone-quick-03"
        self.state = {"requestId": "rid", "runStableId": self.run_id, "cargoCaseKey": self.journal_case,
                      "journalCase": self.journal_case, "checks": [], "trace": [],
                      "attempts": {key: {"state": "RunningOrUncertain"} for key in
                                   ("cargo-create", "cargo-approve", "cargo-offer", "cargo-accept")}}
        self.current = current if current is not None else {"id": 7, "운송번호": "rid"}
        self.detail = detail if detail is not None else {"id": 7, "운송번호": "rid"}
        self.proof = {"runStableId": self.run_id, "database": self.ready["database"],
                      "requestId": "rid", "transportId": 7, "caseKey": self.journal_case}
        self.proof.update(proof_changes or {})
        self.calls = []

    def driver_start(self, food=False):
        pass

    def login(self, actor):
        pass

    def call(self, actor, method, path, *args, **kwargs):
        self.calls.append((method, path))
        if method != "GET":
            raise AssertionError("A prior uncertain command must not be posted again")
        if path.startswith("/verification/"):
            return self.proof
        if path.endswith("/current"):
            return self.current
        if path == "/api/v1/driver/transports/7":
            return self.detail
        raise AssertionError("Unexpected path: " + path)


class PausedCargoRecoveryTests(unittest.TestCase):
    def test_current_detail_and_proof_match_without_reposting_any_command(self):
        with tempfile.TemporaryDirectory() as directory:
            flow = FakePausedCargo(directory)
            flow.cargo(False)
            self.assertEqual(7, flow.state["transportId"])
            self.assertTrue(flow.state["pausedAfterAccept"])
            self.assertEqual(["CargoAcceptLostResponseRecoveredCanonical"], [row["code"] for row in flow.state["checks"]])
            self.assertTrue(all(method == "GET" for method, _ in flow.calls))
            saved = json.loads(flow.path.read_text(encoding="utf-8"))
            self.assertEqual("RunningOrUncertain", saved["attempts"]["cargo-accept"]["state"])

    def test_wrong_current_or_detail_stops_before_transport_actions(self):
        for arguments in ({"current": {"id": 8, "운송번호": "other"}},
                          {"detail": {"id": 7, "운송번호": "other"}}):
            with self.subTest(arguments=arguments), tempfile.TemporaryDirectory() as directory:
                flow = FakePausedCargo(directory, **arguments)
                with self.assertRaisesRegex(RuntimeError, "CargoTransportRequestIdentityMismatch"):
                    flow.cargo(False)
                self.assertNotIn("transportId", flow.state)
                self.assertFalse(flow.state["checks"])
                self.assertTrue(all(method == "GET" for method, _ in flow.calls))

    def test_wrong_request_or_case_or_database_proof_is_not_success(self):
        for changes, error in (({"requestId": "other"}, "CargoProofRequestIdentityMismatch"),
                               ({"caseKey": "cargo-phone-quick-04"}, "CargoProofCaseMismatch"),
                               ({"database": "other"}, "CanonicalDatabaseBindingMismatch")):
            with self.subTest(changes=changes), tempfile.TemporaryDirectory() as directory:
                flow = FakePausedCargo(directory, proof_changes=changes)
                with self.assertRaisesRegex(RuntimeError, error):
                    flow.cargo(False)
                self.assertFalse(flow.state["checks"])
                self.assertTrue(all(method == "GET" for method, _ in flow.calls))

    def test_matching_request_but_different_proof_transport_is_not_success(self):
        with tempfile.TemporaryDirectory() as directory:
            flow = FakePausedCargo(directory, proof_changes={"transportId": 8})
            with self.assertRaisesRegex(RuntimeError, "CargoProofTransportIdentityMismatch"):
                flow.cargo(False)
            self.assertFalse(flow.state["checks"])
            self.assertTrue(all(method == "GET" for method, _ in flow.calls))

    def test_uncertain_mutation_is_not_reposted(self):
        with tempfile.TemporaryDirectory() as directory:
            flow = FakePausedCargo(directory)
            with self.assertRaisesRegex(RuntimeError, "PriorPostAttempt_ReadCanonical_DoNotRepost:cargo-accept"):
                flow.mutation("cargo-accept", "driver", "POST", "/api/v1/driver/dispatch-actions/rid/accept", {})
            self.assertFalse(flow.calls)


if __name__ == "__main__":
    unittest.main()
