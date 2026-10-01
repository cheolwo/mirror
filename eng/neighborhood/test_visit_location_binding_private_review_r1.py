"""개인 원문 없이 위치 검토의 권위·경계·결정성·변조 거절을 시험한다."""
from __future__ import annotations

import copy
import importlib.util
import json
import math
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch


sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("visit_location_review_under_test",
    Path(__file__).with_name("visit_location_binding_private_review_r1.py"))
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
spatial = ROOT / "artifacts/local/python-packages/spatial"
sys.path.insert(0, str(spatial))
from shapely.geometry import Point, Polygon


def record(points=((1, 1),), sequences=None):
    return {"schemaVersion": "delivery-visit-record.v2", "recordId": "synthetic-record",
            "localReviewOnly": True, "isOperationalState": False,
            "visits": [{"visitId": "synthetic-visit:" + str(index + 1),
                        "storeId": "synthetic-store:" + str(index + 1),
                        "sequence": sequences[index] if sequences else index + 1,
                        "name": "SYNTHETIC_NAME_NEVER_IN_OUTPUT", "menu": "SYNTHETIC_MENU_NEVER_IN_OUTPUT",
                        "address": "SYNTHETIC_ADDRESS_NEVER_IN_OUTPUT",
                        "longitude": point[0], "latitude": point[1],
                        "coordinateStatus": "AddressGeocodeCandidate_NotEntrance", "addressStatus": "Unreviewed",
                        "areaBindingStatus": "Unresolved", "administrativeAreaStableId": None,
                        "stationModuleStableId": None, "areaBindingEvidence": None,
                        "eventKind": "RecordedVisit", "pickupCompletionConfirmed": False,
                        "sourceUrls": ["https://example.invalid/menu-only-never-location"]}
                       for index, point in enumerate(points)]}


def boundaries():
    return [SimpleNamespace(stable_id="synthetic-area:a", geometry=Polygon([(0, 0), (2, 0), (2, 2), (0, 2)])),
            SimpleNamespace(stable_id="synthetic-area:b", geometry=Polygon([(2, 0), (4, 0), (4, 2), (2, 2)]))]


def report(value=None, shapes=None, frame=None):
    source = value or record()
    return module.make_report(source,
        frame or SimpleNamespace(wgs84_to_local=lambda longitude, latitude: (longitude, latitude)),
        boundaries() if shapes is None else shapes, Point,
        {"coordinateFrame": copy.deepcopy(module.FRAME), "sourceHash": "synthetic-source-hash"},
        expected_count=len(source["visits"]))


class VisitLocationReviewTests(unittest.TestCase):
    def test_inside_boundary_multiple_and_outside_are_distinct(self):
        result = report(record(((1, 1), (0, 1), (2, 1), (8, 8))))
        self.assertEqual(["SingleInterior", "Boundary", "Multiple", "OutsideScope"],
                         [row["classification"] for row in result["visits"]])
        self.assertEqual(2, result["summary"]["boundaryTouchVisitCount"])
        self.assertEqual(4, result["summary"]["pendingReviewCount"])

    def test_overlapping_interiors_are_not_arbitrarily_assigned(self):
        shapes = boundaries()
        shapes[1].geometry = Polygon([(1, 0), (3, 0), (3, 2), (1, 2)])
        result = report(record(((1.5, 1),)), shapes)
        self.assertEqual("Multiple", result["visits"][0]["classification"])
        self.assertTrue(all(hit["relation"] == "Interior" for hit in result["visits"][0]["historicalBoundaryCandidates"]))

    def test_hole_is_outside_and_near_boundary_is_pending(self):
        shape = SimpleNamespace(stable_id="synthetic-area:hole", geometry=Polygon(
            [(0, 0), (4, 0), (4, 4), (0, 4)], holes=[[(1, 1), (3, 1), (3, 3), (1, 3)]]))
        self.assertEqual("OutsideScope", report(record(((2, 2),)), [shape])["visits"][0]["classification"])
        self.assertEqual("Boundary", report(record(((-0.0000001, 1),)))["visits"][0]["classification"])

    def test_candidate_does_not_become_receipt_or_approval(self):
        result = report()
        self.assertFalse(result["approvalGranted"])
        self.assertFalse(result["canCreatePlaybackReceipt"])
        self.assertFalse(result["runtimeAuthorized"])
        self.assertFalse(result["currentAdministrativeBoundaryEstablished"])
        self.assertEqual("PendingReview", result["reviewStatus"])
        self.assertEqual([], result["visits"][0]["locationEvidenceReferences"])
        text = module.canonical(result).decode()
        for forbidden in ("CanReplay", '"canReplay"', '"Verified"', "SYNTHETIC_NAME_NEVER_IN_OUTPUT",
                          "SYNTHETIC_ADDRESS_NEVER_IN_OUTPUT", "SYNTHETIC_MENU_NEVER_IN_OUTPUT", "menu-only-never-location"):
            self.assertNotIn(forbidden, text)

    def test_source_is_unchanged_and_each_visit_status_is_retained(self):
        source = record()
        before = copy.deepcopy(source)
        result = report(source)
        self.assertEqual(before, source)
        self.assertEqual(source["visits"][0]["areaBindingStatus"], result["visits"][0]["sourceStatuses"]["areaBindingStatus"])
        self.assertEqual(0, result["summary"]["sourceRowsChanged"])

    def test_same_input_and_boundary_order_are_deterministic(self):
        source = record(((1, 1), (3, 1)))
        first = report(source)
        second = report(source, list(reversed(boundaries())))
        self.assertEqual(first, second)
        self.assertEqual(first["contentHashSha256"], second["contentHashSha256"])

    def test_repeated_store_visits_remain_separate(self):
        source = record(((1, 1), (1, 1)))
        source["visits"][1]["storeId"] = source["visits"][0]["storeId"]
        self.assertEqual(2, report(source)["summary"]["visitCount"])

    def test_source_status_upgrade_is_rejected(self):
        for field, value in (("areaBindingStatus", "Verified"), ("coordinateStatus", "Verified"),
                             ("areaBindingEvidence", "claimed"), ("administrativeAreaStableId", "synthetic-area:a")):
            with self.subTest(field=field):
                source = record(); source["visits"][0][field] = value
                with self.assertRaisesRegex(module.ReviewError, "VisitSourceStatusChanged"):
                    report(source)

    def test_operational_and_completion_flags_are_rejected(self):
        source = record(); source["isOperationalState"] = True
        with self.assertRaisesRegex(module.ReviewError, "VisitRecordBoundaryInvalid"):
            report(source)
        source = record(); source["visits"][0]["pickupCompletionConfirmed"] = True
        with self.assertRaisesRegex(module.ReviewError, "VisitEventBoundaryInvalid"):
            report(source)

    def test_order_gap_duplicate_and_boolean_are_rejected(self):
        for values in ((1, 3), (1, 1), (1, True), (1, 0)):
            with self.subTest(values=values), self.assertRaises(module.ReviewError):
                report(record(((1, 1), (3, 1)), values))

    def test_duplicate_visit_is_rejected(self):
        source = record(((1, 1), (3, 1))); source["visits"][1]["visitId"] = source["visits"][0]["visitId"]
        with self.assertRaisesRegex(module.ReviewError, "DuplicateVisitId"):
            report(source)

    def test_coordinate_missing_nonfinite_and_range_are_rejected(self):
        for value in (None, math.nan, math.inf, 91, True):
            with self.subTest(value=value):
                source = record(); source["visits"][0]["latitude"] = value
                with self.assertRaisesRegex(module.ReviewError, "VisitCoordinateInvalid"):
                    report(source)

    def test_nonfinite_transform_is_rejected(self):
        with self.assertRaisesRegex(module.ReviewError, "CommonCoordinateNonFinite"):
            report(frame=SimpleNamespace(wgs84_to_local=lambda x, z: (math.inf, z)))

    def test_scope_frame_change_is_rejected(self):
        with self.assertRaisesRegex(module.ReviewError, "CoordinateFrameChanged"):
            module.make_report(record(), None, [], Point, {"coordinateFrame": {}}, 1)

    def test_duplicate_json_and_nonfinite_are_rejected_without_source_text(self):
        for payload in (b'{"address":"SENSITIVE","address":"SENSITIVE"}', b'{"value":NaN}', b'"SENSITIVE"'):
            with self.subTest(payload=payload), self.assertRaises(module.ReviewError) as caught:
                module.parse_json(payload)
            self.assertNotIn("SENSITIVE", str(caught.exception))

    def test_existing_common_enu_origin_maps_to_zero(self):
        batch = module.load_batch(ROOT)
        frame = batch.frame_from_scope({"coordinateFrame": module.FRAME})
        x, z = frame.wgs84_to_local(module.FRAME["originLongitude"], module.FRAME["originLatitude"])
        self.assertEqual((0, 0), (x, z))

    def test_outputs_are_new_generation_and_rebuild_verify_do_not_write(self):
        self._generation_case()

    def test_tampered_authority_is_not_overwritten_or_accepted_by_verify(self):
        self._generation_case(tamper=True)

    def _generation_case(self, tamper=False):
        test_root = ROOT / module.OUTPUT / "test-work"
        test_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="synthetic-", dir=test_root) as folder:
            root = Path(folder)
            source = report()
            with patch.object(module, "ensure_private_output"), patch.object(module, "OUTPUT", Path("g")):
                first = module.persist(root, source, "build")
                self.assertEqual(2, first["changedFiles"])
                path = root / module.OUTPUT / "generations" / first["generation"] / "report.json"
                before = path.read_bytes()
                if tamper:
                    edited = json.loads(before); edited["approvalGranted"] = True
                    path.write_bytes(module.encoded(edited))
                    for mode in ("build", "verify"):
                        with self.assertRaisesRegex(module.ReviewError, "ReviewGenerationChanged"):
                            module.persist(root, source, mode)
                    self.assertTrue(json.loads(path.read_bytes())["approvalGranted"])
                else:
                    for mode in ("build", "verify"):
                        self.assertEqual(0, module.persist(root, source, mode)["changedFiles"])
                    self.assertEqual(before, path.read_bytes())

    def test_nonignored_output_is_blocked(self):
        with patch.object(module.subprocess, "run", return_value=SimpleNamespace(returncode=1)):
            with self.assertRaisesRegex(module.ReviewError, "PrivateOutputMustBeGitIgnored"):
                module.ensure_private_output(ROOT, module.OUTPUT / "report.json")

    def test_frozen_hash_and_source_changes_are_rejected(self):
        test_root = ROOT / module.OUTPUT / "test-work"
        test_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="synthetic-", dir=test_root) as folder:
            root = Path(folder); relative = Path("source.json")
            (root / relative).write_bytes(b"synthetic-v1")
            before = module.frozen_payload(root, relative, module.sha(b"synthetic-v1"))
            (root / relative).write_bytes(b"synthetic-v2")
            with self.assertRaisesRegex(module.ReviewError, "FrozenSourceHashMismatch"):
                module.frozen_payload(root, relative, module.sha(before))
            with self.assertRaisesRegex(module.ReviewError, "FrozenSourceChangedDuringReview"):
                module.assert_sources_unchanged(root, {relative: before})

    def test_traversal_and_missing_generation_are_rejected(self):
        with self.assertRaisesRegex(module.ReviewError, "PathOutsideRepository"):
            module.safe_path(ROOT, Path("../outside"))
        test_root = ROOT / module.OUTPUT / "test-work"
        test_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="synthetic-", dir=test_root) as folder, patch.object(module, "ensure_private_output"):
            with self.assertRaisesRegex(module.ReviewError, "ReviewGenerationMissing"):
                module.persist(Path(folder), report(), "verify")


if __name__ == "__main__":
    unittest.main(verbosity=2)
