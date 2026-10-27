"""GUID 실행 결속만 합성 검사한다. HTTP·DB·서버·휴대폰을 사용하지 않는다."""
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("r108_boundary_workflow", Path(__file__).with_name("workflow.py"))
workflow = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(workflow)


class WorkflowBoundaryTests(unittest.TestCase):
    def fixture(self, short=True):
        guid = "0123456789abcdef0123456789abcdef"
        root = workflow.REPO / "artifacts/local/integrated-workflow-r108" / guid
        database = ("r108m_" if short else "mirror_workflow_r108_") + guid
        mode = "MigrationFirst" if short else "CurrentModel"
        ready = {"schemaVersion": "integrated-workflow-r108.ready.r1", "baseUrl": workflow.BASE,
                 "executionMode": "Simulation", "externalHttpBlocked": True, "database": database,
                 "RunStableId": "integrated-r108:" + guid, "schemaMode": mode,
                 "isolatedMigrationChainApplied": short}
        environment = {"R108_DATABASE": database, "R108_ARTIFACT_ROOT": str(root), "R108_SCHEMA_MODE": mode}
        preparation = {"schemaVersion": "integrated-workflow-r108.preparation.r1",
                       "status": "FreshDedicatedDatabasePrepared", "database": database,
                       "runStableId": "integrated-r108:" + guid, "schemaMode": mode, "outputRoot": str(root)}
        return root, ready, environment, preparation

    def test_migration_first_exact_binding_is_accepted(self):
        workflow.validate_live_boundary(*self.fixture())

    def test_current_model_exact_preparation_is_accepted(self):
        workflow.validate_live_boundary(*self.fixture(False))

    def test_legacy_current_model_without_mode_or_preparation_is_accepted(self):
        root, ready, environment, _ = self.fixture(False)
        ready.pop("schemaMode")
        ready.pop("isolatedMigrationChainApplied")
        environment.pop("R108_SCHEMA_MODE")
        workflow.validate_live_boundary(root, ready, environment)

    def test_short_name_requires_applied_marker_and_preparation(self):
        root, ready, environment, preparation = self.fixture()
        with self.assertRaisesRegex(RuntimeError, "R108AppliedMigrationPreparationRequired"):
            workflow.validate_live_boundary(root, ready, environment)
        for marker in (False, None, "true", 1):
            with self.subTest(marker=marker):
                changed = dict(ready, isolatedMigrationChainApplied=marker)
                with self.assertRaisesRegex(RuntimeError, "R108AppliedMigrationPreparationRequired"):
                    workflow.validate_live_boundary(root, changed, environment, preparation)

    def test_ready_binding_mismatches_are_rejected(self):
        changes = {"schemaVersion": "other", "baseUrl": "https://outside.example/", "executionMode": "Operational",
                   "externalHttpBlocked": False, "database": "production",
                   "RunStableId": "integrated-r108:" + "f" * 32, "schemaMode": "CurrentModel"}
        for field, value in changes.items():
            with self.subTest(field=field):
                root, ready, environment, preparation = self.fixture()
                ready[field] = value
                with self.assertRaises(RuntimeError):
                    workflow.validate_live_boundary(root, ready, environment, preparation)

    def test_private_environment_mismatches_are_rejected(self):
        changes = {"R108_DATABASE": "r108m_" + "f" * 32, "R108_ARTIFACT_ROOT": str(workflow.REPO),
                   "R108_SCHEMA_MODE": "CurrentModel"}
        for field, value in changes.items():
            with self.subTest(field=field):
                root, ready, environment, preparation = self.fixture()
                environment[field] = value
                with self.assertRaises(RuntimeError):
                    workflow.validate_live_boundary(root, ready, environment, preparation)

    def test_preparation_mismatches_are_rejected(self):
        changes = {"database": "r108m_" + "f" * 32, "schemaMode": "CurrentModel", "outputRoot": str(workflow.REPO),
                   "runStableId": "integrated-r108:" + "f" * 32, "status": "Failed", "schemaVersion": "other"}
        for field, value in changes.items():
            with self.subTest(field=field):
                root, ready, environment, preparation = self.fixture()
                preparation[field] = value
                with self.assertRaisesRegex(RuntimeError, "R108PreparationBindingMismatch"):
                    workflow.validate_live_boundary(root, ready, environment, preparation)

    def test_nested_wrong_or_outside_root_is_rejected(self):
        root, ready, environment, preparation = self.fixture()
        for changed in (root / "nested", root.parent / ("f" * 32), workflow.REPO / root.name):
            with self.subTest(root=str(changed)):
                with self.assertRaises(RuntimeError):
                    workflow.validate_live_boundary(changed, ready, environment, preparation)

    def test_prefix_mode_confusion_is_rejected(self):
        for short, mode in ((True, "CurrentModel"), (False, "MigrationFirst"), (True, "Other")):
            with self.subTest(short=short, mode=mode):
                root, ready, environment, preparation = self.fixture(short)
                ready["schemaMode"] = environment["R108_SCHEMA_MODE"] = preparation["schemaMode"] = mode
                with self.assertRaisesRegex(RuntimeError, "R108SchemaModeBindingMismatch"):
                    workflow.validate_live_boundary(root, ready, environment, preparation)

    def test_malformed_or_partial_guid_is_rejected(self):
        for database in ("r108m_" + "a" * 31, "r108m_" + "A" * 32, "r108m_" + "g" * 32,
                         "other_" + "a" * 32, "r108m_" + "a" * 32 + "_suffix"):
            with self.subTest(database=database):
                root, ready, environment, preparation = self.fixture()
                ready["database"] = database
                with self.assertRaisesRegex(RuntimeError, "R108LiveBoundaryRequired"):
                    workflow.validate_live_boundary(root, ready, environment, preparation)


if __name__ == "__main__":
    unittest.main()
