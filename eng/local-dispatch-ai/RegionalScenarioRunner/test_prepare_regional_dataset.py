"""지역 파생자료의 공개 픽업 연결·보호 필드·결정성 경계를 검증한다."""
import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SPEC = importlib.util.spec_from_file_location("prepare_regional_dataset", Path(__file__).with_name("prepare_regional_dataset.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def replay(kind, character):
    return "replay-" + kind + "-" + character * 64


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(MODULE.canonical(value))
    return hashlib.sha256(path.read_bytes()).hexdigest()


class RegionalDatasetPreparationTests(unittest.TestCase):
    def setUp(self):
        MODULE.LOCAL.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="regional-dataset-tests-", dir=MODULE.LOCAL)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.source_root = self.root / "source"
        self.relative = "edit-kit/branch-deliveries-all-20261010/2026-10-09/plan.json"
        self.plan_path = self.source_root / self.relative
        self.map_path = self.plan_path.with_name("route-map.json")
        self.dataset_path = self.root / "replay/dataset.json"
        self.addresses = ["서울특별시 중랑구 공개로 10", "서울특별시 광진구 공개로 20", "서울특별시 노원구 공개로 30"]
        self.plan = {"stores": [{"name": "시험매장" + str(index), "address": address, "locationVerified": True}
                                for index, address in enumerate(self.addresses)],
                     "destinationVisits": [{"privateField": "PRIVATE_DESTINATION_SENTINEL"}]}
        self.mapping = {"points": [{"name": store["name"], "address": store["address"],
                                     "matchedAddress": store["address"], "lat": 37.58 + index / 1000,
                                     "lon": 127.08 + index / 1000} for index, store in enumerate(self.plan["stores"])]}
        self.dataset = {"schemaVersion": 1, "localVerificationOnly": True, "driverId": "replay-driver",
                        "sourceLedgerSha256": "f" * 64,
                        "branches": [{"branchId": replay("branch", str(index + 1)), "displayName": store["name"],
                                      "publicPickupAddress": store["address"], "customerName": "PRIVATE_BRANCH_SENTINEL"}
                                     for index, store in enumerate(self.plan["stores"])],
                        "records": [self.record(index) for index in range(3)],
                        "driverGps": "PRIVATE_GPS_SENTINEL"}
        self.dataset["records"][1]["menuItems"] = []
        self.dataset["records"][1]["menuSummary"] = ["PRIVATE_VISIT_SUMMARY_SENTINEL"]
        self.seal()

    def record(self, index):
        return {"recordId": replay("record", str(index + 1)), "branchId": replay("branch", str(index + 1)),
                "pickupVisitId": replay("pickup", str(index + 1)), "serviceDate": "2026-10-09", "sequence": index + 1,
                "deliveryFeeKrw": 4000, "distanceKm": 1.234, "distanceBasis": "AppDisplayed", "foodOrderAmountKrw": None,
                "actualCompletedAtUtc": "PRIVATE_COMPLETION_SENTINEL", "customerName": "PRIVATE_NAME_SENTINEL",
                "deliveryAddress": "PRIVATE_ADDRESS_SENTINEL", "platformOrderId": "PRIVATE_ORDER_SENTINEL",
                "menuItems": [{"name": "확인된 커피", "quantity": 2, "unitPriceKrw": None,
                               "displayedItemAmountKrw": 7000, "amountBasis": "AppDisplayedItemAmountNotInferredUnitPrice",
                               "options": ["아이스"], "customerPhone": "PRIVATE_MENU_SENTINEL"}],
                "menuSummary": [], "sourceRefs": [{"relativePath": self.relative, "sha256": "0" * 64,
                                                     "field": f"stores[{index}]", "basis": "VerifiedPublicPickupPlaceNotCustomerLocation",
                                                     "rawUi": "PRIVATE_REF_SENTINEL"}]}

    def seal(self):
        plan_sha = dump(self.plan_path, self.plan)
        dump(self.map_path, self.mapping)
        for row in self.dataset["records"]:
            row["sourceRefs"][0]["sha256"] = plan_sha
        dump(self.dataset_path, self.dataset)

    def prepare(self):
        return MODULE.build_dataset(self.dataset_path, self.source_root, enforce_baseline=False)

    def failure(self, code):
        with self.assertRaisesRegex(MODULE.PreparationFailure, "^" + code + "$"):
            self.prepare()

    def test_only_explicit_target_districts_are_selected(self):
        dataset, summary = self.prepare()
        self.assertEqual(["중랑구", "광진구"], [row["district"] for row in dataset["records"]])
        self.assertEqual({"OutsideTargetDistricts": 1}, summary["excludedRecordsByReason"])
        self.assertEqual(2, summary["counts"]["selectedBranches"])

    def test_unknown_fields_and_visit_summary_are_never_copied(self):
        dataset, summary = self.prepare()
        self.assertNotIn("PRIVATE_", MODULE.canonical(dataset).decode("utf-8"))
        self.assertNotIn("PRIVATE_", MODULE.canonical(summary).decode("utf-8"))
        self.assertEqual([], dataset["records"][1]["historical"]["menuItems"])
        self.assertEqual(1, summary["counts"]["visitSummaryOnlyNotAllocated"])
        self.assertEqual({"relativePath", "sha256", "field", "basis"}, set(dataset["records"][0]["sourceRefs"][0]))

    def test_displayed_amount_is_not_divided_into_unit_price(self):
        dataset, _ = self.prepare()
        item = dataset["records"][0]["historical"]["menuItems"][0]
        self.assertEqual(2, item["quantity"])
        self.assertEqual(7000, item["displayedItemAmountKrw"])
        self.assertIsNone(item["unitPriceKrw"])

    def test_repeat_has_identical_bytes_and_preserves_all_source_hashes(self):
        before = {path: path.read_bytes() for path in [self.dataset_path, self.plan_path, self.map_path]}
        first, summary = self.prepare()
        second, repeated = self.prepare()
        self.assertEqual(MODULE.canonical(first), MODULE.canonical(second))
        self.assertEqual(MODULE.canonical(summary), MODULE.canonical(repeated))
        self.assertEqual(3, len(first["metadata"]["sourceFingerprints"]))
        self.assertEqual(before, {path: path.read_bytes() for path in before})

    def test_delivery_destination_is_explicit_synthetic_and_not_actual_distance(self):
        dataset, _ = self.prepare()
        row = dataset["records"][0]
        destination = row["syntheticDestination"]
        self.assertTrue(destination["notActualCustomerLocation"])
        self.assertEqual("SyntheticOffsetFromPublicStore", destination["basis"])
        self.assertNotEqual(row["pickup"]["latitude"], destination["latitude"])
        self.dataset["records"][0]["distanceKm"] = 99
        self.seal()
        changed, _ = self.prepare()
        self.assertEqual(destination, changed["records"][0]["syntheticDestination"])

    def test_point_index_is_not_used_instead_of_source_ordinal(self):
        self.mapping["points"][0]["index"] = 999
        self.seal()
        dataset, _ = self.prepare()
        self.assertEqual(37.58, dataset["records"][0]["pickup"]["latitude"])

    def test_wrong_plan_hash_is_rejected(self):
        self.dataset["records"][0]["sourceRefs"][0]["sha256"] = "1" * 64
        dump(self.dataset_path, self.dataset)
        self.failure("SOURCE_HASH_MISMATCH")

    def test_later_reference_cannot_change_same_plan_hash(self):
        self.dataset["records"][1]["sourceRefs"][0]["sha256"] = "1" * 64
        dump(self.dataset_path, self.dataset)
        self.failure("SOURCE_HASH_MISMATCH")

    def test_wrong_store_point_join_is_rejected(self):
        self.mapping["points"][0], self.mapping["points"][1] = self.mapping["points"][1], self.mapping["points"][0]
        self.seal()
        self.failure("PICKUP_IDENTITY_MISMATCH")

    def test_unverified_or_personal_purchase_cannot_become_pickup(self):
        for patch, code in [({"locationVerified": False}, "PICKUP_NOT_VERIFIED"),
                            ({"eventType": "personal_purchase"}, "PICKUP_NOT_VERIFIED")]:
            with self.subTest(patch=patch):
                original = copy.deepcopy(self.plan["stores"][0])
                self.plan["stores"][0].update(patch)
                self.seal()
                self.failure(code)
                self.plan["stores"][0] = original

    def test_explicit_geocoder_district_conflict_is_rejected(self):
        self.mapping["points"][0]["matchedAddress"] = "서울특별시 광진구 공개로 10"
        self.seal()
        self.failure("PICKUP_DISTRICT_CONFLICT")

    def test_existing_branch_address_variant_keeps_hash_bound_visit_coordinate(self):
        self.dataset["branches"][0]["publicPickupAddress"] = "서울 중랑구 공개로 10 1층"
        self.seal()
        dataset, summary = self.prepare()
        self.assertEqual(37.58, dataset["records"][0]["pickup"]["latitude"])
        self.assertEqual("서울 중랑구 공개로 10 1층", dataset["records"][0]["publicPickupAddress"])
        self.assertEqual(1, summary["counts"]["canonicalBranchAddressDiffersFromPickupObservation"])

    def test_missing_geocoder_district_remains_visible_without_guess(self):
        self.mapping["points"][0]["matchedAddress"] = "공개로 10"
        self.seal()
        dataset, summary = self.prepare()
        self.assertEqual("중랑구", dataset["records"][0]["district"])
        self.assertEqual(1, summary["counts"]["matchedAddressDistrictMissing"])

    def test_public_address_without_district_is_excluded(self):
        self.dataset["branches"][0]["publicPickupAddress"] = "공개로 10"
        self.seal()
        dataset, summary = self.prepare()
        self.assertEqual(1, len(dataset["records"]))
        self.assertEqual(1, summary["excludedRecordsByReason"]["DistrictNotExplicit"])

    def test_coordinate_variants_are_preserved_per_record(self):
        self.dataset["records"][1]["branchId"] = self.dataset["records"][0]["branchId"]
        self.plan["stores"][1] = copy.deepcopy(self.plan["stores"][0])
        self.mapping["points"][1].update({"name": self.plan["stores"][0]["name"], "address": self.addresses[0],
                                           "matchedAddress": self.addresses[0]})
        self.seal()
        dataset, summary = self.prepare()
        self.assertEqual(1, summary["branchesWithCoordinateVariants"])
        self.assertNotEqual(dataset["records"][0]["pickup"], dataset["records"][1]["pickup"])

    def test_nan_out_of_bounds_and_boolean_coordinates_are_rejected(self):
        for value in ["NaN", 91, True]:
            with self.subTest(value=value):
                self.mapping["points"][0]["lat"] = value
                self.seal()
                self.failure("PICKUP_COORDINATE_INVALID")

    def test_bad_index_and_duplicate_record_are_rejected(self):
        self.dataset["records"][0]["sourceRefs"][0]["field"] = "stores[999]"
        self.seal()
        self.failure("PICKUP_INDEX_INVALID")
        self.dataset["records"][0]["sourceRefs"][0]["field"] = "stores[0]"
        self.dataset["records"][1]["recordId"] = self.dataset["records"][0]["recordId"]
        self.seal()
        self.failure("RECORD_RELATION_INVALID")

    def test_bad_path_and_nonlocal_dataset_are_rejected(self):
        self.dataset["records"][0]["sourceRefs"][0]["relativePath"] = "../../private.json"
        self.seal()
        self.failure("SOURCE_REF_INVALID")
        with self.assertRaisesRegex(MODULE.PreparationFailure, "PATH_OUTSIDE_BOUNDARY"):
            MODULE.build_dataset(MODULE.REPO / "outside.json", self.source_root, False)

    def test_sensitive_whitelisted_text_is_rejected(self):
        self.dataset["records"][0]["menuItems"][0]["name"] = "010-1234-5678"
        self.seal()
        self.failure("MENU_NAME_INVALID")

    def test_source_change_creates_new_fingerprint(self):
        first, _ = self.prepare()
        self.mapping["points"][0]["lat"] += .00001
        self.seal()
        second, _ = self.prepare()
        self.assertNotEqual(first["metadata"]["sourceFingerprint"], second["metadata"]["sourceFingerprint"])

    def test_production_entry_requires_approved_coverage(self):
        with self.assertRaisesRegex(MODULE.PreparationFailure, "APPROVED_COVERAGE_MISMATCH"):
            MODULE.build_dataset(self.dataset_path, self.source_root)

    def test_invalid_output_fingerprint_cannot_escape_private_directory(self):
        dataset, summary = self.prepare()
        dataset["metadata"]["sourceFingerprint"] = "../outside"
        with self.assertRaisesRegex(MODULE.PreparationFailure, "OUTPUT_FINGERPRINT_INVALID"):
            MODULE.write_dataset(dataset, summary)


if __name__ == "__main__":
    unittest.main()
