"""Offline checks for portable public-data collaboration bundles."""
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import warnings
import zipfile

import collect_public_snapshots as collector
import transfer_snapshot as transfer


NOW = datetime(2026, 11, 11, 1, 0, tzinfo=timezone.utc)


def json_body(value):
    return json.dumps(value, ensure_ascii=False).encode("utf-8")


def price_body():
    return json_body([{"lastupdated": "2026-10-08"}, [{"countryiso3code": "KOR",
        "indicator": {"id": "AG.LND.ARBL.HA"}, "date": "2024", "value": 1234}]])


class SnapshotTransfer(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "AGENTS.md").write_text("fixture", encoding="utf-8")
        self.manifest_path = self.root / "eng/data-collection/jobs.json"
        self.manifest_path.parent.mkdir(parents=True)
        self.source_root = self.root / "artifacts/local/data-collection-runs"
        self.job_id = "prices"
        self.source = {"id": "worldbank-test", "sourceId": "world-bank-indicators",
            "datasetId": "wdi-ag-lnd-arbl-ha", "kind": "dataset", "status": "ready",
            "method": "GET", "url": "https://api.worldbank.org/example", "validator": "worldbank",
            "unit": "ha", "maxBytes": 65536, "maxRecords": 100}
        self.metadata = {"id": "official-reference", "sourceId": "official-page",
            "datasetId": "reference", "kind": "metadata", "status": "ready", "method": "GET",
            "url": "https://example.org/reference", "validator": "html", "unit": "metadata only"}
        self.blocked = {"id": "restricted-reference", "kind": "dataset", "status": "blocked",
                        "reason": "Existing provider not ready; do not call."}
        self.manifest = {"schemaVersion": 1, "outputRoot": "../../artifacts/local/data-collection-runs",
            "security": {"allowedUrls": [self.source["url"], self.metadata["url"]]},
            "jobs": [{"id": self.job_id, "sources": [self.source, self.metadata, self.blocked]}]}
        self.write_manifest()
        self.make_snapshot(self.source, price_body())
        self.make_snapshot(self.metadata, b"<!doctype html><html><head><title>Official information</title></head><body>metadata</body></html>")
        self.bundle = self.root / "handoff/prices.zip"

    def write_manifest(self):
        self.manifest_path.write_bytes(json_body(self.manifest))

    def local(self, source, *parts):
        return self.source_root.joinpath(self.job_id, source["id"], *parts)

    def make_snapshot(self, source, body):
        sha = hashlib.sha256(body).hexdigest()
        pointer = {**transfer.source_identity(source), "sha256": sha, "body": f"bodies/{sha}.raw",
                   "collectedAt": collector.stamp(NOW), "requestPolicyFingerprint": transfer.policy_fingerprint(source, collector.stamp(NOW)),
                   "httpStatus": 200, "bytes": len(body), "etag": None, "lastModified": None,
                   "previousSha256": None,
                   **collector.VALIDATORS[source["validator"]](body, source, 100)}
        raw = self.local(source, "bodies", sha + ".raw")
        raw.parent.mkdir(parents=True, exist_ok=True)
        raw.write_bytes(body)
        self.local(source, "latest.json").write_bytes(json_body(pointer))
        run = {key: value for key, value in pointer.items() if key in transfer.RUN_FIELDS}
        run["status"] = "saved"
        run_path = self.local(source, "runs", "20261111T010000Z-123456abcdef.json")
        run_path.parent.mkdir(parents=True, exist_ok=True)
        run_path.write_bytes(json_body(run))
        return pointer

    def export(self):
        return transfer.export_bundle(self.manifest_path, self.job_id, self.bundle, now=NOW)

    def verify(self, path=None, **kwargs):
        return transfer.verify_bundle(path or self.bundle, self.manifest_path, self.job_id, **kwargs)

    def contents(self):
        with zipfile.ZipFile(self.bundle) as archive:
            return {name: archive.read(name) for name in archive.namelist()}

    def altered(self, change):
        blobs = self.contents()
        change(blobs)
        path = self.root / "altered.zip"
        with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for name, body in blobs.items():
                archive.writestr(name, body)
        return path

    def assert_code(self, expected, action):
        with self.assertRaises((transfer.TransferError, collector.CollectionError)) as context:
            action()
        self.assertEqual(expected, context.exception.code)

    def tree_hashes(self):
        return {str(p.relative_to(self.source_root)): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in self.source_root.rglob("*") if p.is_file() and p.name != "collector.lock"}

    def test_roundtrip_preserves_source_bytes_metadata_and_separates_counts(self):
        before = self.tree_hashes()
        with mock.patch.object(collector, "fetch", side_effect=AssertionError("Network forbidden")):
            result = self.export()
            verified = self.verify(expected_sha256=result["sha256"])
        self.assertEqual("exported", result["status"])
        self.assertEqual("verified", verified["status"])
        self.assertFalse(verified["publicDistributionApproved"])
        self.assertEqual({"datasetSnapshots": 1, "metadataSnapshots": 1, "missingSnapshots": 0,
                          "policyBlockedSources": 1, "blockedOutcomes": 0}, verified["summary"])
        self.assertEqual(before, self.tree_hashes())
        blobs = self.contents()
        self.assertNotIn("collector.lock", blobs)
        self.assertEqual(self.local(self.source, "latest.json").read_bytes(), blobs["sources/worldbank-test/latest.json"])
        pointer = json.loads(blobs["sources/worldbank-test/latest.json"])
        self.assertEqual(("2024", "2026-10-08", "hectares", transfer.policy_fingerprint(self.source, collector.stamp(NOW))),
                         (pointer["evidenceAsOf"], pointer["sourceUpdatedAt"], pointer["unit"], pointer["requestPolicyFingerprint"]))

    def test_verify_is_read_only(self):
        self.export()
        expected = {str(p): p.stat().st_mtime_ns for p in self.root.rglob("*") if p.is_file()}
        self.verify()
        self.assertEqual(expected, {str(p): p.stat().st_mtime_ns for p in self.root.rglob("*") if p.is_file()})

    def test_missing_snapshot_is_preserved_as_missing(self):
        self.local(self.metadata, "latest.json").unlink()
        result = self.export()
        self.assertEqual(1, result["summary"]["missingSnapshots"])
        self.assertEqual(0, result["summary"]["metadataSnapshots"])
        self.assertEqual(1, self.verify()["summary"]["missingSnapshots"])

    def test_existing_blocked_outcome_and_latest_can_coexist(self):
        blocked = {"requestPolicyFingerprint": transfer.policy_fingerprint(self.source, collector.stamp(NOW)), "reason": "http_403", "blockedAt": collector.stamp(NOW),
                   "sourceId": self.source["sourceId"], "datasetId": self.source["datasetId"]}
        self.local(self.source, "blocked-outcome.json").write_bytes(json_body(blocked))
        result = self.export()
        self.assertEqual(1, result["summary"]["blockedOutcomes"])
        self.assertEqual(1, result["summary"]["datasetSnapshots"])
        self.assertEqual(json_body(blocked), self.contents()["sources/worldbank-test/blocked-outcome.json"])

    def test_duplicate_source_policy_is_rejected(self):
        self.manifest["jobs"][0]["sources"].append(dict(self.source))
        self.write_manifest()
        self.assert_code("duplicate_source_id", self.export)
        self.assertFalse(self.bundle.exists())

    def test_repeated_export_never_overwrites_existing_file(self):
        result = self.export()
        original = self.bundle.read_bytes()
        self.assert_code("output_already_exists", self.export)
        self.assertEqual(original, self.bundle.read_bytes())
        self.assertEqual(result["sha256"], self.verify()["sha256"])

    def test_output_cannot_be_inside_source_tree(self):
        self.assert_code("output_inside_source_root", lambda: transfer.export_bundle(
            self.manifest_path, self.job_id, self.source_root / "copy.zip"))

    def test_real_os_lock_prevents_export(self):
        with collector.global_lock(self.source_root / "collector.lock") as acquired:
            self.assertTrue(acquired)
            self.assert_code("collector_busy", self.export)
        self.assertFalse(self.bundle.exists())
        self.assertEqual("exported", self.export()["status"])

    def test_source_body_corruption_fails_without_output(self):
        pointer = json.loads(self.local(self.source, "latest.json").read_bytes())
        self.local(self.source, pointer["body"]).write_bytes(b"changed")
        self.assert_code("body_digest_mismatch", self.export)
        self.assertFalse(self.bundle.exists())

    def test_missing_source_body_fails_without_synthesizing(self):
        pointer = json.loads(self.local(self.source, "latest.json").read_bytes())
        self.local(self.source, pointer["body"]).unlink()
        self.assert_code("input_file_missing", self.export)
        self.assertFalse(self.bundle.exists())

    def test_pointer_path_escape_is_rejected(self):
        path = self.local(self.source, "latest.json")
        pointer = json.loads(path.read_bytes()); pointer["body"] = "../../private.db"
        path.write_bytes(json_body(pointer))
        self.assert_code("unsafe_body_reference", self.export)

    def test_private_metadata_fields_are_rejected(self):
        path = self.local(self.source, "latest.json")
        pointer = json.loads(path.read_bytes()); pointer["customerAddress"] = "private"
        path.write_bytes(json_body(pointer))
        self.assert_code("unrecognized_snapshot_field", self.export)
        pointer.pop("customerAddress"); pointer["accessToken"] = "private"
        path.write_bytes(json_body(pointer))
        self.assert_code("credential_field_forbidden", self.export)

    def test_source_identity_and_validation_metadata_must_match(self):
        path = self.local(self.source, "latest.json")
        original = json.loads(path.read_bytes())
        for key, value, code in [("datasetId", "other", "source_identity_mismatch"),
                                 ("recordCount", 0, "snapshot_validation_metadata_mismatch")]:
            pointer = dict(original); pointer[key] = value
            path.write_bytes(json_body(pointer))
            self.assert_code(code, self.export)

    def test_archive_body_tampering_is_detected(self):
        self.export()
        path = self.altered(lambda blobs: blobs.__setitem__(next(n for n in blobs if n.endswith(".raw")), b"tampered"))
        self.assert_code("member_digest_or_size_mismatch", lambda: self.verify(path))

    def test_traversal_and_extra_files_are_rejected_without_extraction(self):
        self.export()
        self.assert_code("unsafe_archive_path", lambda: transfer.member_name("sources\\private.db"))
        for name, code in [("../private.db", "unsafe_archive_path"), ("/absolute", "unsafe_archive_path"),
                           ("unlisted.json", "unexpected_archive_member")]:
            with self.subTest(name=name):
                path = self.altered(lambda blobs: blobs.__setitem__(name, b"data"))
                self.assert_code(code, lambda: self.verify(path))

    def test_duplicate_zip_members_and_case_collisions_are_rejected(self):
        self.export()
        for duplicate in ["bundle.json", "BUNDLE.JSON"]:
            path = self.root / "duplicates.zip"
            with zipfile.ZipFile(self.bundle) as source, zipfile.ZipFile(path, "w") as dest:
                for entry in source.infolist():
                    dest.writestr(entry, source.read(entry.filename))
                with warnings.catch_warnings():
                    warnings.simplefilter("ignore", UserWarning)
                    dest.writestr(duplicate, b"{}")
            self.assert_code("duplicate_archive_member", lambda: self.verify(path))

    def test_symlink_member_is_rejected(self):
        self.export()
        path = self.root / "symlink.zip"
        with zipfile.ZipFile(self.bundle) as source, zipfile.ZipFile(path, "w") as dest:
            for entry in source.infolist():
                dest.writestr(entry, source.read(entry.filename))
            entry = zipfile.ZipInfo("link")
            entry.create_system = 3; entry.external_attr = 0o120777 << 16
            dest.writestr(entry, "../private")
        self.assert_code("invalid_archive_member_type", lambda: self.verify(path))

    def test_duplicate_json_keys_are_rejected(self):
        self.export()
        path = self.altered(lambda blobs: blobs.__setitem__(transfer.INDEX, b'{"schema":"a","schema":"b"}'))
        self.assert_code("duplicate_json_key", lambda: self.verify(path))

    def test_manifest_change_and_wrong_job_are_rejected(self):
        self.export()
        self.manifest["schemaVersion"] = 2; self.write_manifest()
        self.assert_code("manifest_or_job_mismatch", self.verify)
        self.assert_code("unique_job_required", lambda: transfer.verify_bundle(self.bundle, self.manifest_path, "unknown"))

    def test_expected_bundle_hash_is_checked(self):
        self.export()
        self.assert_code("bundle_digest_mismatch", lambda: self.verify(expected_sha256="0" * 64))

    def test_summary_and_public_approval_cannot_be_changed(self):
        self.export()
        def set_field(blobs, field, value):
            index = json.loads(blobs[transfer.INDEX]); index[field] = value
            blobs[transfer.INDEX] = json_body(index)
        path = self.altered(lambda b: set_field(b, "summary", {}))
        self.assert_code("summary_mismatch", lambda: self.verify(path))
        path = self.altered(lambda b: set_field(b, "publicDistributionApproved", True))
        self.assert_code("invalid_bundle_purpose", lambda: self.verify(path))

    def test_archive_size_and_json_limits_are_enforced(self):
        self.export()
        with mock.patch.object(transfer, "MAX_BYTES", 1000):
            self.assert_code("archive_size_limit", self.verify)
        self.assert_code("json_too_large", lambda: transfer.decode_json(b" " * (transfer.MAX_JSON_BYTES + 1)))

    def test_stale_request_policy_is_rejected(self):
        self.source["validationPolicyVersion"] = "changed-policy"
        self.write_manifest()
        self.assert_code("snapshot_policy_mismatch", self.export)

    def test_year_tokens_are_resolved_at_observation_time(self):
        source = dict(self.source, json={"startyear": "{{previousYear}}", "endyear": "{{currentYear}}"})
        prior = transfer.policy_fingerprint(source, "2025-12-31T14:59:59Z")
        current = transfer.policy_fingerprint(source, "2025-12-31T15:00:00Z")
        self.assertNotEqual(prior, current)
        expanded = dict(source, json={"startyear": "2025", "endyear": "2026"})
        self.assertEqual(current, transfer.policy_fingerprint(expanded, collector.stamp(NOW)))

    def test_malformed_pointer_json_is_rejected_cleanly(self):
        for pointer in [None, [], "text", 3]:
            with self.subTest(pointer=pointer):
                self.local(self.source, "latest.json").write_bytes(json_body(pointer))
                self.assert_code("invalid_source_metadata", self.export)

    def test_collector_version_must_match(self):
        self.export()
        def change(blobs):
            index = json.loads(blobs[transfer.INDEX]); index["collectorSha256"] = "0" * 64
            blobs[transfer.INDEX] = json_body(index)
        self.assert_code("collector_version_mismatch", lambda: self.verify(self.altered(change)))


if __name__ == "__main__":
    unittest.main()
