"""비공개 메뉴 사진의 로컬 보관 준비 경계와 무결성을 검증한다."""
import copy
import hashlib
import importlib.util
import io
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from PIL import Image


SPEC = importlib.util.spec_from_file_location("prepare_menu_blob_archive", Path(__file__).with_name("prepare-menu-blob-archive.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def image_bytes(color):
    stream = io.BytesIO()
    Image.new("RGB", (2, 3), color).save(stream, format="PNG")
    return stream.getvalue()


def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


class MenuBlobArchivePreparationTests(unittest.TestCase):
    def setUp(self):
        MODULE.LOCAL.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="menu-blob-tests-", dir=MODULE.LOCAL)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.catalog_path, self.output = self.root / "catalog.json", self.root / "blob-archive-manifest.json"
        self.payloads = [image_bytes("red"), image_bytes("blue")]
        self.paths = [self.root / "first.png", self.root / "second.png"]
        self.assets = []
        for path, payload in zip(self.paths, self.payloads):
            path.write_bytes(payload)
            sha = hashlib.sha256(payload).hexdigest()
            self.assets.append({"assetId": "asset-" + sha, "sha256": sha, "bytes": len(payload), "mimeType": "image/png",
                                "width": 2, "height": 3, "relativePath": path.relative_to(MODULE.LOCAL).as_posix()})
        self.catalog = {"schemaVersion": 1, "localVerificationOnly": True, "executionModeCode": "Simulation",
                        "operationalRegistration": False, "publicUseApproved": False, "publicDisplayApproved": False,
                        "sourceDatasetSha256": "a" * 64,
                        "sources": [{"kind": "CanonicalDeliveryDataset", "sha256": "a" * 64}],
                        "branches": [{"branchId": "branch-a"}, {"branchId": "branch-b"}],
                        "orders": [{"recordId": "record-a", "branchId": "branch-a", "serviceDate": "2026-09-30"},
                                   {"recordId": "record-b", "branchId": "branch-b", "serviceDate": "2026-10-01"}],
                        "menus": [{"menuId": "menu-a", "branchId": "branch-a", "rawMenu": "음료", "observations": [
                            {"recordId": "record-a", "unitPriceKrw": None, "displayedItemAmountKrw": 7000}],
                            "imageCandidates": [self.candidate("photo-a", 0), self.candidate("photo-b", 1)]},
                                  {"menuId": "menu-b", "branchId": "branch-b", "rawMenu": "음료", "observations": [
                            {"recordId": "record-b", "unitPriceKrw": None, "displayedItemAmountKrw": None}],
                            "imageCandidates": [self.candidate("photo-a", 0)]}], "assets": copy.deepcopy(self.assets)}
        dump(self.catalog_path, self.catalog)
        self.seal()

    def seal(self):
        dump(self.catalog_path.with_name("import-proof.json"),
             {"schemaVersion": 1, "catalogSha256": hashlib.sha256(self.catalog_path.read_bytes()).hexdigest(),
              "sources": self.catalog["sources"], "sourceInputsChanged": False, "unitPriceInferred": False,
              "currentSellingPricesCreated": False, "operationalRegistration": False, "publicDisplayApproved": False})

    def candidate(self, cid, asset_index):
        return {"candidateId": cid, "assetId": self.assets[asset_index]["assetId"], "officialMenuName": "음료",
                "matchLevel": "same_menu_name_candidate", "officialSourceStatus": "verified", "optionsMatch": False,
                "rightsStatus": "unconfirmed", "actualBranchPhotoVerified": False, "historicalProductVersionVerified": False,
                "publicDisplayApproved": False, "publicUseApproved": False, "sourcePageSha256": "b" * 64}

    def prepare(self, **kwargs):
        dump(self.catalog_path, self.catalog)
        self.seal()
        return MODULE.prepare(self.catalog_path, self.output, **kwargs)

    def assert_rejected(self, code, **kwargs):
        with self.assertRaisesRegex(MODULE.PreparationFailure, "^" + code + "$"):
            self.prepare(**kwargs)
        self.assertFalse(self.output.exists())

    def test_all_bytes_verified_and_only_manifest_written(self):
        original = {path: path.read_bytes() for path in [self.catalog_path, self.root / "import-proof.json", *self.paths]}
        manifest, sha = MODULE.prepare(self.catalog_path, self.output)
        self.assertEqual(manifest["summary"], {"assets": 2, "menuImageLinks": 3, "menusWithImages": 2,
                                              "branchesWithImages": 2, "bytes": sum(map(len, self.payloads)),
                                              "uploadedAssets": 0, "remoteVerifiedAssets": 0})
        self.assertEqual(sha, hashlib.sha256(self.output.read_bytes()).hexdigest())
        for path, payload in original.items():
            self.assertEqual(path.read_bytes(), payload)
        self.assertEqual({p.name for p in self.root.iterdir()}, {"catalog.json", "import-proof.json", "first.png", "second.png", "blob-archive-manifest.json"})
        self.assertNotIn("unitPriceKrw", self.output.read_text(encoding="utf-8"))

    def test_repeat_preserves_manifest_bytes_and_metadata(self):
        _, first = MODULE.prepare(self.catalog_path, self.output)
        initial = self.output.read_bytes()
        changed_at = self.output.stat().st_mtime_ns
        _, second = MODULE.prepare(self.catalog_path, self.output)
        self.assertEqual(first, second)
        self.assertEqual(self.output.read_bytes(), initial)
        self.assertEqual(self.output.stat().st_mtime_ns, changed_at)

    def test_names_and_gallery_order_are_source_candidates_without_operational_authority(self):
        manifest, _ = self.prepare()
        for asset in manifest["assets"]:
            self.assertEqual(asset["objectName"], "menu-reference/" + asset["sha256"] + ".png")
            self.assertEqual(asset["access"], "Private")
            self.assertEqual(asset["sourceCatalogSha256"], manifest["sourceCatalogSha256"])
            self.assertEqual(asset["uploadState"], "NotUploaded")
        links = manifest["menuImageLinks"]
        self.assertEqual([x["displayOrder"] for x in links], [1, 2, 1])
        self.assertEqual([x["referenceRole"] for x in links], ["RepresentativeCandidate", "DetailCandidate", "RepresentativeCandidate"])
        self.assertTrue(all(not x["operationalRepresentativeConfirmed"] and not x["publicUseApproved"] and x["rightsUnconfirmed"] for x in links))
        self.assertNotEqual(links[0]["branchId"], links[2]["branchId"])

    def test_preparation_never_claims_upload_configuration_or_acl_verification(self):
        manifest, _ = self.prepare()
        self.assertEqual(manifest["preparationStatus"], "PreparedBlocked")
        self.assertFalse(manifest["uploadPerformed"])
        self.assertFalse(manifest["networkAccessPerformed"])
        self.assertIsNone(manifest["storage"]["serviceUri"])
        self.assertIsNone(manifest["storage"]["containerName"])
        self.assertEqual(manifest["storage"]["observedDefaultContainerName"], "platform-private")
        self.assertFalse(manifest["storage"]["liveVerified"])
        self.assertFalse(manifest["storage"]["privateAclVerified"])
        self.assertIn("MissingVerifiedServiceUri", manifest["blockedReasons"])
        self.assertIn("PrivateContainerAclVerificationRequired", manifest["blockedReasons"])

    def test_supplied_safe_service_origin_is_only_observed_and_still_blocked(self):
        manifest, _ = self.prepare(service_uri="https://exampleaccount.blob.core.windows.net/")
        self.assertEqual(manifest["storage"]["observedServiceUri"], "https://exampleaccount.blob.core.windows.net")
        self.assertIsNone(manifest["storage"]["serviceUri"])
        self.assertIn("MissingVerifiedServiceUri", manifest["blockedReasons"])

    def test_other_container_is_rejected(self):
        self.assert_rejected("PrivateContainerMismatch", private_container_name="community-public")

    def test_credential_query_fragment_userinfo_and_wrong_endpoint_are_rejected(self):
        for value in ["https://exampleaccount.blob.core.windows.net?sig=PRIVATE_SENTINEL",
                      "https://exampleaccount.blob.core.windows.net?anything=1",
                      "https://exampleaccount.blob.core.windows.net#PRIVATE_SENTINEL",
                      "https://name:PRIVATE_SENTINEL@exampleaccount.blob.core.windows.net",
                      "https://exampleaccount.blob.core.windows.net/platform-private",
                      "http://exampleaccount.blob.core.windows.net", "https://evil.example", "https://exampleaccount.blob.core.windows.net:443"]:
            with self.subTest(value=value):
                self.assert_rejected("ServiceUriInvalid", service_uri=value)

    def test_cli_refusal_does_not_echo_secret_query(self):
        result = subprocess.run([sys.executable, str(Path(MODULE.__file__)), "--catalog", str(self.catalog_path),
                                 "--service-uri", "https://exampleaccount.blob.core.windows.net?sig=PRIVATE_SENTINEL"],
                                text=True, capture_output=True, encoding="utf-8")
        self.assertEqual(result.returncode, 1)
        self.assertIn("ServiceUriInvalid", result.stderr)
        self.assertNotIn("PRIVATE_SENTINEL", result.stdout + result.stderr)

    def test_missing_photo_is_rejected(self):
        self.paths[1].unlink()
        self.assert_rejected("SourceFileMissing")

    def test_hash_change_is_rejected(self):
        self.paths[0].write_bytes(image_bytes("green"))
        self.assert_rejected("AssetHashMismatch")

    def test_byte_count_mismatch_is_rejected(self):
        self.catalog["assets"][0]["bytes"] += 1
        self.assert_rejected("AssetByteCountMismatch")

    def test_path_traversal_absolute_drive_and_backslash_are_rejected(self):
        for value in ["../outside.png", "/outside.png", "C:/outside.png", "folder\\outside.png", "folder//outside.png"]:
            with self.subTest(value=value):
                self.catalog["assets"][0]["relativePath"] = value
                self.assert_rejected("AssetPathInvalid")

    def test_symbolic_link_is_rejected(self):
        link = self.root / "linked.png"
        try:
            link.symlink_to(self.paths[0])
        except OSError:
            self.skipTest("This Windows environment cannot create a symlink.")
        self.catalog["assets"][0]["relativePath"] = link.relative_to(MODULE.LOCAL).as_posix()
        self.assert_rejected("LinkPathForbidden")

    def test_windows_reparse_point_is_rejected_without_symlink_privilege(self):
        original = Path.lstat
        def marked(path, *args, **kwargs):
            found = original(path, *args, **kwargs)
            if path == self.paths[0]:
                return SimpleNamespace(st_mode=found.st_mode, st_file_attributes=0x400)
            return found
        with patch.object(Path, "lstat", marked):
            self.assert_rejected("LinkPathForbidden")

    def test_mime_extension_and_decoded_format_must_agree(self):
        renamed = self.root / "renamed.jpg"
        renamed.write_bytes(self.payloads[0])
        self.catalog["assets"][0].update(relativePath=renamed.relative_to(MODULE.LOCAL).as_posix(), mimeType="image/jpeg")
        self.assert_rejected("ImageMimeMismatch")

    def test_mime_extension_mismatch_is_rejected(self):
        self.catalog["assets"][0]["mimeType"] = "image/jpeg"
        self.assert_rejected("ImageExtensionMismatch")

    def test_invalid_image_data_with_matching_hash_is_rejected(self):
        invalid = b"\x89PNG\r\n\x1a\nthis-is-not-a-valid-image"
        self.paths[0].write_bytes(invalid)
        sha = hashlib.sha256(invalid).hexdigest()
        aid = "asset-" + sha
        old = self.catalog["assets"][0]["assetId"]
        self.catalog["assets"][0].update(assetId=aid, sha256=sha, bytes=len(invalid))
        for menu in self.catalog["menus"]:
            for candidate in menu["imageCandidates"]:
                if candidate["assetId"] == old:
                    candidate["assetId"] = aid
        self.assert_rejected("ImageDecodeFailed")

    def test_image_dimensions_must_match_metadata(self):
        self.catalog["assets"][0]["width"] = 99
        self.assert_rejected("ImageDimensionsMismatch")

    def test_permission_flags_on_root_candidate_and_asset_are_rejected(self):
        original = copy.deepcopy(self.catalog)
        for place, key, value in [("root", "publicUseApproved", True), ("root", "operationalRegistration", True),
                                  ("root", "executionModeCode", "Operational"), ("candidate", "optionsMatch", True),
                                  ("candidate", "rightsStatus", "approved"), ("candidate", "actualBranchPhotoVerified", True),
                                  ("asset", "publicDisplayApproved", True)]:
            with self.subTest(place=place, key=key):
                self.catalog = copy.deepcopy(original)
                target = self.catalog if place == "root" else self.catalog["assets"][0] if place == "asset" else self.catalog["menus"][0]["imageCandidates"][0]
                target[key] = value
                self.assert_rejected("PermissionEscalationForbidden")

    def test_source_dataset_hash_binding_is_required(self):
        self.catalog["sources"][0]["sha256"] = "c" * 64
        self.assert_rejected("CatalogSourceBindingInvalid")

    def test_catalog_change_after_import_proof_is_rejected(self):
        self.catalog["menus"][0]["observations"][0]["unitPriceKrw"] = 3500
        dump(self.catalog_path, self.catalog)
        with self.assertRaisesRegex(MODULE.PreparationFailure, "ImportProofCatalogMismatch"):
            MODULE.prepare(self.catalog_path, self.output)
        self.assertFalse(self.output.exists())

    def test_missing_import_proof_is_rejected(self):
        self.catalog_path.with_name("import-proof.json").unlink()
        with self.assertRaisesRegex(MODULE.PreparationFailure, "SourceFileMissing"):
            MODULE.prepare(self.catalog_path, self.output)

    def test_case_variant_permission_flag_cannot_override_boundary(self):
        self.catalog["menus"][0]["imageCandidates"][0]["PUBLICUSEAPPROVED"] = True
        self.assert_rejected("CatalogDuplicateJsonKey")

    def test_duplicate_asset_identity_is_rejected(self):
        self.catalog["assets"].append(copy.deepcopy(self.catalog["assets"][0]))
        self.assert_rejected("CatalogDuplicateIdentifier")

    def test_menu_branch_observation_foreign_key_mismatch_is_rejected(self):
        self.catalog["menus"][0]["observations"][0]["recordId"] = "record-b"
        self.assert_rejected("MenuObservationBranchMismatch")

    def test_missing_candidate_asset_and_unlinked_assets_are_rejected(self):
        original = copy.deepcopy(self.catalog)
        self.catalog["menus"][0]["imageCandidates"][0]["assetId"] = "asset-missing"
        self.assert_rejected("MenuCandidateAssetMissing")
        self.catalog = original
        self.catalog["menus"][0]["imageCandidates"].pop()
        self.assert_rejected("UnlinkedAssetForbidden")

    def test_ambiguous_component_and_unverified_source_are_rejected(self):
        for key, value in [("matchLevel", "component_only"), ("matchLevel", "ambiguous"), ("officialSourceStatus", "unverified")]:
            with self.subTest(key=key, value=value):
                self.catalog["menus"][0]["imageCandidates"][0][key] = value
                self.assert_rejected("MenuCandidateBoundaryInvalid")
                self.catalog["menus"][0]["imageCandidates"][0] = self.candidate("photo-a", 0)

    def test_duplicate_json_keys_are_rejected(self):
        raw = json.dumps(self.catalog).replace('"schemaVersion": 1', '"schemaVersion": 1, "schemaVersion": 1')
        self.catalog_path.write_text(raw, encoding="utf-8")
        with self.assertRaisesRegex(MODULE.PreparationFailure, "CatalogDuplicateJsonKey"):
            MODULE.prepare(self.catalog_path, self.output)

    def test_source_mutation_during_preparation_aborts_before_manifest(self):
        original = MODULE.verify_unchanged
        def changed(hashes):
            self.paths[1].write_bytes(image_bytes("black"))
            return original(hashes)
        with patch.object(MODULE, "verify_unchanged", side_effect=changed):
            self.assert_rejected("SourceChangedDuringPreparation")

    def test_output_cannot_replace_catalog_or_existing_execution_evidence(self):
        original = self.catalog_path.read_bytes()
        with self.assertRaisesRegex(MODULE.PreparationFailure, "OutputOverlapsSource"):
            MODULE.prepare(self.catalog_path, self.catalog_path)
        self.assertEqual(self.catalog_path.read_bytes(), original)
        self.output.write_text('{"uploadPerformed":true}', encoding="utf-8")
        with self.assertRaisesRegex(MODULE.PreparationFailure, "ExistingManifestConflict"):
            MODULE.prepare(self.catalog_path, self.output)
        self.assertEqual(self.output.read_text(encoding="utf-8"), '{"uploadPerformed":true}')

    def test_concurrent_execution_evidence_cannot_be_replaced(self):
        original = MODULE.os.link
        sentinel = b'{"uploadPerformed":true,"remoteVerified":true}'
        def concurrent(source, destination):
            Path(destination).write_bytes(sentinel)
            return original(source, destination)
        with patch.object(MODULE.os, "link", side_effect=concurrent):
            with self.assertRaisesRegex(MODULE.PreparationFailure, "ExistingManifestConflict"):
                self.prepare()
        self.assertEqual(self.output.read_bytes(), sentinel)
        self.assertFalse(list(self.root.glob(".menu-blob-prepare-*")))

    def test_concurrent_identical_preparation_is_idempotent(self):
        original = MODULE.os.link
        def concurrent(source, destination):
            Path(destination).write_bytes(Path(source).read_bytes())
            return original(source, destination)
        with patch.object(MODULE.os, "link", side_effect=concurrent):
            manifest, sha = self.prepare()
        self.assertEqual(hashlib.sha256(self.output.read_bytes()).hexdigest(), sha)
        self.assertFalse(manifest["uploadPerformed"])


if __name__ == "__main__":
    unittest.main()
