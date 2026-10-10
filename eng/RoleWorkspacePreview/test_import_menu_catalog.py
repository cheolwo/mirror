"""메뉴 관측/가격 경계와 비공개 사진 파일의 무결성 회귀 시험."""
import base64
import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SPEC = importlib.util.spec_from_file_location("import_menu_catalog", Path(__file__).with_name("import-menu-catalog.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
PNG = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jX1UAAAAASUVORK5CYII=")


def replay(kind, character):
    return "replay-" + kind + "-" + character * 64


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return hashlib.sha256(path.read_bytes()).hexdigest()


class MenuCatalogImportTests(unittest.TestCase):
    def setUp(self):
        MODULE.LOCAL.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="menu-catalog-tests-", dir=MODULE.LOCAL)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.image_root, self.output = self.root / "private-images", self.root / "output"
        self.dataset_path = self.root / "canonical/dataset.json"
        self.full = self.image_root / "full-collection"
        self.image_root.mkdir(parents=True)
        self.original = self.image_root / "original.png"
        self.original.write_bytes(PNG)
        self.page = self.image_root / "official.html"
        self.page.write_text('<h1>아메리카노</h1><img alt="아메리카노" src="/coffee.png">', encoding="utf-8")
        self.sha = hashlib.sha256(PNG).hexdigest()
        self.asset_id = "asset-" + self.sha
        self.branch_a, self.branch_b = replay("branch", "a"), replay("branch", "b")
        self.record_a, self.record_b, self.record_c = (replay("record", c) for c in "123")
        self.dataset = {"schemaVersion": 1, "localVerificationOnly": True, "driverId": "replay-driver",
                        "branches": [{"branchId": self.branch_a, "displayName": "커피 가게 가점", "publicPickupAddress": "서울 공개로 1"},
                                     {"branchId": self.branch_b, "displayName": "커피 가게 나점", "publicPickupAddress": "서울 공개로 2"}],
                        "records": [self.record(self.record_a, self.branch_a, 1, 7000, 2, 4000, ["샷 추가"]),
                                    self.record(self.record_b, self.branch_a, 2, None, 1, None, ["아이스"]),
                                    self.record(self.record_c, self.branch_b, 3, 6000, None, None, [])]}
        self.dataset["records"][2]["menuItems"] = []
        self.dataset["records"][2]["menuSummary"] = ["카페라떼 × 2"]
        self.candidate = {"candidateId": "image-coffee", "inventoryGroupIds": ["brand-coffee"],
                          "officialMenuName": "아메리카노", "imageUrl": "https://official.example/coffee.png",
                          "sourcePageUrl": "https://official.example/menu/coffee", "sourcePageFile": str(self.page),
                          "sourcePageSha256": hashlib.sha256(self.page.read_bytes()).hexdigest(),
                          "officialSourceStatus": "verified", "associationEvidence": "Same img alt and menu name"}
        self.asset = {"assetId": self.asset_id, "sha256": self.sha, "assetPath": str(self.original),
                      "bytes": len(PNG), "width": 1, "height": 1, "format": "PNG",
                      "candidateIds": ["image-coffee"], "sourceUrls": [self.candidate["imageUrl"]]}
        self.link = {"candidateId": "image-coffee", "assetId": self.asset_id, "officialMenuName": "아메리카노",
                     "matchLevel": "same_menu_name_candidate", "optionsMatch": "unconfirmed"}
        self.registry = {"candidates": [self.candidate]}
        self.manifest = {"assets": [self.asset], "candidateAssetLinks": [{"candidateId": "image-coffee", "assetId": self.asset_id,
                            "imageUrl": self.candidate["imageUrl"], "status": "collected"}]}
        self.review = {"menus": [
            {"menuId": "menu-coffee", "branchId": self.branch_a, "branchName": "커피 가게 가점", "rawMenu": "아메리카노",
             "sourceKind": "structured", "sourceRecordIds": [self.record_a, self.record_b], "inventoryGroupId": "brand-coffee",
             "candidateLinks": [self.link]},
            {"menuId": "menu-latte", "branchId": self.branch_b, "branchName": "커피 가게 나점", "rawMenu": "카페라떼 × 2",
             "sourceKind": "per_order_summary", "sourceRecordIds": [self.record_c], "inventoryGroupId": "brand-coffee",
             "candidateLinks": []}], "candidateReview": [copy.deepcopy(self.candidate)], "assets": [copy.deepcopy(self.asset)]}
        self.seal()

    def record(self, rid, bid, sequence, order, quantity, displayed, options):
        return {"recordId": rid, "branchId": bid, "serviceDate": "2026-09-30", "sequence": sequence,
                "pickupVisitId": replay("pickup", str(sequence)), "foodOrderAmountKrw": order,
                "menuItems": [{"name": "아메리카노", "quantity": quantity, "unitPriceKrw": None,
                               "displayedItemAmountKrw": displayed, "amountBasis": "AppDisplayedItemAmountNotInferredUnitPrice",
                               "options": options}], "menuSummary": [], "customerName": "PRIVATE_SENTINEL",
                "deliveryAddress": "PRIVATE_ADDRESS_SENTINEL", "platformOrderId": "PRIVATE_ORDER_SENTINEL"}

    def seal(self):
        dataset_sha = dump(self.dataset_path, self.dataset)
        self.registry["sourceDatasetSha256"] = dataset_sha
        registry_sha = dump(self.full / "source-registry.json", self.registry)
        self.manifest["sourceRegistrySha256"] = registry_sha
        manifest_sha = dump(self.full / "image-collection-manifest.json", self.manifest)
        self.review["sources"] = [{"path": str(path), "sha256": sha} for path, sha in
                                  [(self.dataset_path, dataset_sha), (self.full / "source-registry.json", registry_sha),
                                   (self.full / "image-collection-manifest.json", manifest_sha)]]
        dump(self.full / "full-review.json", self.review)

    def run_import(self):
        return MODULE.import_catalog(self.dataset_path, self.image_root, self.output)

    def assert_failure(self, code):
        with self.assertRaisesRegex(MODULE.ImportFailure, "^" + code + "$"):
            self.run_import()

    def test_nullable_unit_price_is_not_line_amount_divided_by_quantity_or_order_total(self):
        catalog, proof = self.run_import()
        first, second = catalog["menus"][0]["observations"]
        self.assertEqual((first["quantity"], first["displayedItemAmountKrw"]), (2, 4000))
        self.assertIsNone(first["unitPriceKrw"])
        self.assertIsNone(second["unitPriceKrw"])
        self.assertIsNone(second["displayedItemAmountKrw"])
        self.assertEqual(catalog["orders"][0]["foodOrderAmountKrw"], 7000)
        self.assertIsNone(catalog["orders"][1]["foodOrderAmountKrw"])
        self.assertEqual(proof["summary"]["knownUnitPrices"], 0)
        self.assertFalse(proof["unitPriceInferred"])

    def test_explicit_price_and_zero_are_preserved_without_becoming_current_selling_price(self):
        self.dataset["records"][0]["menuItems"][0]["unitPriceKrw"] = 1234
        self.dataset["records"][1]["menuItems"][0]["unitPriceKrw"] = 0
        self.seal()
        catalog, _ = self.run_import()
        self.assertEqual([x["unitPriceKrw"] for x in catalog["menus"][0]["observations"]], [1234, 0])
        self.assertNotIn("currentSellingPriceKrw", json.dumps(catalog))

    def test_summary_and_options_remain_observations(self):
        catalog, _ = self.run_import()
        coffee, latte = catalog["menus"]
        self.assertEqual([o["options"] for o in coffee["observations"]], [["샷 추가"], ["아이스"]])
        self.assertEqual(latte["rawMenu"], "카페라떼 × 2")
        self.assertIsNone(latte["observations"][0]["quantity"])
        self.assertEqual(latte["observations"][0]["summaryIndex"], 0)
        self.assertEqual(latte["observations"][0]["amountBasis"], "SummaryNotStructured")

    def test_image_is_private_same_name_reference_not_options_or_public_approval(self):
        catalog, _ = self.run_import()
        image = catalog["menus"][0]["imageCandidates"][0]
        self.assertFalse(image["optionsMatch"])
        self.assertFalse(image["publicDisplayApproved"])
        self.assertFalse(image["publicUseApproved"])
        self.assertFalse(image["actualBranchPhotoVerified"])
        self.assertFalse(image["historicalProductVersionVerified"])
        self.assertEqual(image["rightsStatus"], "unconfirmed")
        self.assertEqual(catalog["executionModeCode"], "Simulation")
        self.assertTrue(catalog["localVerificationOnly"])
        self.assertFalse(catalog["operationalRegistration"])
        self.assertEqual((MODULE.LOCAL / catalog["assets"][0]["relativePath"]).read_bytes(), PNG)

    def test_component_ambiguous_and_uncollected_images_are_excluded_without_brand_fallback(self):
        self.review["menus"][1]["candidateLinks"] = [
            {"matchLevel": "component_only", "candidateId": "not-a-source", "assetId": self.asset_id},
            {"matchLevel": "ambiguous", "candidateId": "not-a-source", "assetId": self.asset_id},
            {"matchLevel": "official_same_menu", "candidateId": "not-collected", "assetId": None}]
        self.seal()
        catalog, proof = self.run_import()
        self.assertEqual(catalog["menus"][1]["imageCandidates"], [])
        self.assertEqual(proof["skippedImageLinks"], {"ImageNotCollected": 1, "NotSameMenuName": 2})
        self.assertEqual(len(catalog["assets"]), 1)

    def test_output_allowlist_does_not_copy_customer_fields_or_raw_source_refs(self):
        catalog, _ = self.run_import()
        payload = json.dumps(catalog, ensure_ascii=False)
        for value in ["PRIVATE_SENTINEL", "PRIVATE_ADDRESS_SENTINEL", "PRIVATE_ORDER_SENTINEL", "customerName", "sourcePageFile"]:
            self.assertNotIn(value, payload)

    def test_repeated_import_is_deterministic_and_source_files_are_unchanged(self):
        input_paths = [self.dataset_path, self.full / "full-review.json", self.full / "source-registry.json",
                       self.full / "image-collection-manifest.json", self.original, self.page]
        before = {p: p.read_bytes() for p in input_paths}
        self.run_import()
        output = (self.output / "catalog.json").read_bytes(), (self.output / "import-proof.json").read_bytes()
        self.run_import()
        self.assertEqual(output, ((self.output / "catalog.json").read_bytes(), (self.output / "import-proof.json").read_bytes()))
        self.assertEqual(before, {p: p.read_bytes() for p in input_paths})

    def test_same_menu_in_different_branch_cannot_acquire_record_or_photo(self):
        self.review["menus"][0]["sourceRecordIds"] = [self.record_c]
        self.seal()
        self.assert_failure("REVIEW_RECORD_CANONICAL_MISMATCH")

    def test_review_cannot_create_menu_not_in_canonical_orders(self):
        self.review["menus"][0]["rawMenu"] = "새로운 가짜 메뉴"
        self.seal()
        self.assert_failure("REVIEW_MENU_CANONICAL_MISMATCH")

    def test_incomplete_review_record_set_cannot_claim_all_same_branch_observations(self):
        self.review["menus"][0]["sourceRecordIds"] = [self.record_a]
        self.seal()
        self.assert_failure("REVIEW_RECORD_CANONICAL_MISMATCH")

    def test_canonical_hash_must_match_registry_and_review(self):
        self.dataset_path.write_bytes(self.dataset_path.read_bytes() + b" ")
        self.assert_failure("REGISTRY_DATASET_HASH_MISMATCH")

    def test_changed_official_source_page_is_rejected(self):
        self.page.write_text("different response", encoding="utf-8")
        self.assert_failure("SOURCE_FILE_HASH_MISMATCH")

    def test_changed_original_image_is_rejected(self):
        self.original.write_bytes(PNG + b"changed")
        self.assert_failure("SOURCE_FILE_HASH_MISMATCH")

    def test_existing_corrupt_copy_is_not_silently_repaired(self):
        catalog, _ = self.run_import()
        output = self.output / "catalog.json"
        original_catalog = output.read_bytes()
        (MODULE.LOCAL / catalog["assets"][0]["relativePath"]).write_bytes(b"corrupt")
        self.assert_failure("EXISTING_IMAGE_HASH_MISMATCH")
        self.assertEqual(output.read_bytes(), original_catalog)

    def test_asset_cannot_escape_private_root(self):
        outside = self.root / "outside.png"
        outside.write_bytes(PNG)
        self.asset["assetPath"] = str(outside)
        self.review["assets"] = [copy.deepcopy(self.asset)]
        self.seal()
        self.assert_failure("PATH_OUTSIDE_BOUNDARY")

    def test_output_cannot_escape_ignored_local_artifacts(self):
        with self.assertRaisesRegex(MODULE.ImportFailure, "PATH_OUTSIDE_BOUNDARY"):
            MODULE.import_catalog(self.dataset_path, self.image_root, MODULE.REPO / "wwwroot/private-menu-test")

    def test_image_bytes_must_match_declared_type(self):
        self.asset["format"] = "JPEG"
        self.review["assets"] = [copy.deepcopy(self.asset)]
        self.seal()
        self.assert_failure("IMAGE_SIGNATURE_MISMATCH")

    def test_source_registry_cannot_change_image_link_without_review_match(self):
        self.candidate["imageUrl"] = "https://official.example/different.png"
        self.seal()
        self.assert_failure("REVIEW_CANDIDATE_SOURCE_MISMATCH")

    def test_unverified_official_source_is_not_accepted(self):
        self.candidate["officialSourceStatus"] = "unconfirmed"
        self.seal()
        self.assert_failure("OFFICIAL_ASSOCIATION_UNVERIFIED")

    def test_photo_must_be_linked_by_manifest_not_only_matching_asset_id(self):
        self.manifest["candidateAssetLinks"] = []
        self.seal()
        self.assert_failure("IMAGE_ASSET_LINK_MISMATCH")

    def test_source_url_with_credential_is_rejected(self):
        self.candidate["sourcePageUrl"] = "https://official.example/menu?token=private"
        self.review["candidateReview"] = [copy.deepcopy(self.candidate)]
        self.seal()
        self.assert_failure("SOURCE_URL_INVALID")

    def test_public_cdn_numeric_and_korean_space_filenames_are_not_customer_identifiers(self):
        for value in ["https://official.example/20260101012345678.png",
                      "https://official.example/일반 사이드_계란 야채죽.jpg"]:
            self.assertEqual(MODULE.public_url(value), value)

    def test_invalid_money_is_not_replaced_with_zero(self):
        for value in [-1, True, float("nan")]:
            with self.subTest(value=str(value)):
                self.dataset["records"][0]["menuItems"][0]["unitPriceKrw"] = value
                self.seal()
                self.assert_failure("UNIT_PRICE_INVALID")

    def test_menu_missing_from_image_review_still_comes_only_from_actual_record(self):
        self.review["menus"] = self.review["menus"][:1]
        self.seal()
        catalog, _ = self.run_import()
        self.assertEqual(catalog["menus"][1]["rawMenu"], "카페라떼 × 2")
        self.assertTrue(catalog["menus"][1]["menuId"].startswith("recorded-menu-"))
        self.assertEqual(catalog["menus"][1]["imageCandidates"], [])


if __name__ == "__main__":
    unittest.main()
