"""서울 CSV 수집·재취득 생략·304·오프라인 인계의 통합 계약 시험."""
import copy
from datetime import datetime, timedelta, timezone
import hashlib
from http.client import HTTPMessage
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import zipfile

import collect_public_snapshots as collector
import transfer_snapshot as transfer


NOW = datetime(2026, 11, 12, 8, 0, tzinfo=timezone.utc)
POPULATION = b"STDR_YYQU_CD,AREA_CD,POP\n20263,a,10\n20263,b,\n"
AREA = b"AREA_CD,AREA,X,Y\na,50,201000,551000\n"


class Response(io.BytesIO):
    def __init__(self, body, url, status=200):
        super().__init__(body)
        self.status = status
        self.url = url
        self.headers = HTTPMessage()
        self.headers["ETag"] = '"csv-fixture"'

    def geturl(self):
        return self.url


class OfflineClient:
    def __init__(self, *responses):
        self.responses = list(responses)
        self.calls = []

    def open(self, req, timeout):
        self.calls.append(req)
        if not self.responses:
            raise AssertionError("Unexpected request in offline CSV collection test")
        return self.responses.pop(0)


class CityActivityCollectionTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="hongdal-city-csv-integration-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / "AGENTS.md").write_text("offline test fixture", encoding="utf-8")
        self.path = self.root / "eng/data-collection/jobs.json"
        self.path.parent.mkdir(parents=True)
        self.population = {"id": "population", "sourceId": "seoul-open-data", "datasetId": "population-fixture",
            "kind": "dataset", "status": "ready", "method": "GET", "url": "https://data.seoul.go.kr/population.csv",
            "validator": "seoul_activity_csv", "requiredColumns": ["STDR_YYQU_CD", "AREA_CD", "POP"],
            "periodColumn": "STDR_YYQU_CD", "expectedPeriod": "20263",
            "idColumns": ["STDR_YYQU_CD", "AREA_CD"], "numericColumns": ["POP"],
            "unit": "명", "spatialUnit": "상권", "maxBytes": 4096, "maxRecords": 10, "intervalHours": 24}
        self.area = {"id": "areas", "sourceId": "seoul-open-data", "datasetId": "area-fixture",
            "kind": "dataset", "status": "ready", "method": "GET", "url": "https://data.seoul.go.kr/areas.csv",
            "validator": "seoul_activity_csv", "requiredColumns": ["AREA_CD", "AREA", "X", "Y"],
            "idColumns": ["AREA_CD"], "numericColumns": ["AREA", "X", "Y"],
            "unit": "면적·중심좌표", "spatialUnit": "상권영역", "maxBytes": 4096, "maxRecords": 10,
            "intervalHours": 24}
        self.sources = [self.population, self.area]
        self.manifest = {"schemaVersion": 1, "outputRoot": "../../artifacts/local/data-collection-runs",
            "security": {"allowedUrls": [source["url"] for source in self.sources]},
            "limits": {"maxRequests": 3, "maxTotalBytes": 8192, "timeoutSeconds": 30},
            "jobs": [{"id": "city-activity", "sources": self.sources}]}

    def run_job(self, client, now=NOW):
        self.path.write_text(json.dumps(self.manifest, ensure_ascii=False), encoding="utf-8")
        return collector.run(self.path, "city-activity", now=now, open_client=client, resolve_dns=False)

    def responses(self, status=200):
        return OfflineClient(Response(POPULATION if status == 200 else b"", self.population["url"], status),
                             Response(AREA if status == 200 else b"", self.area["url"], status))

    def latest_path(self, source):
        return self.root / "artifacts/local/data-collection-runs/city-activity" / source["id"] / "latest.json"

    def test_collect_no_period_not_due_no_http_and_transfer_roundtrip(self):
        first = self.run_job(self.responses())
        self.assertEqual(("completed", 2), (first["status"], first["summary"]["datasetSaved"]))
        no_http = OfflineClient()
        second = self.run_job(no_http, NOW + timedelta(hours=1))
        self.assertEqual((0, 2, 0),
                         (len(no_http.calls), second["summary"]["notDue"], second["summary"]["requestCount"]))
        area = json.loads(self.latest_path(self.area).read_text(encoding="utf-8"))
        self.assertEqual((None, []), (area["evidenceAsOf"], area["observationPeriods"]))
        bundle = self.root / "handoff/city.zip"
        with mock.patch.object(collector, "fetch", side_effect=AssertionError("Network forbidden")):
            exported = transfer.export_bundle(self.path, "city-activity", bundle, now=NOW + timedelta(hours=2))
            verified = transfer.verify_bundle(bundle, self.path, "city-activity", expected_sha256=exported["sha256"])
        self.assertEqual("verified", verified["status"])
        self.assertEqual(2, verified["summary"]["datasetSnapshots"])
        self.assertFalse(verified["publicDistributionApproved"])
        with zipfile.ZipFile(bundle) as archive:
            population = json.loads(archive.read("sources/population/latest.json"))
            self.assertEqual(("20263", 1, "상권"),
                             (population["evidenceAsOf"], population["missingNumericCells"], population["spatialUnit"]))
            self.assertEqual(POPULATION, archive.read("sources/population/" + population["body"]))
            self.assertEqual(AREA, archive.read("sources/areas/" + area["body"]))

    def test_required_columns_change_revalidates_before_due_and_preserves_previous_snapshot(self):
        self.run_job(self.responses())
        previous = self.latest_path(self.population).read_bytes()
        self.population["requiredColumns"].append("NEW_REQUIRED")
        client = OfflineClient(Response(POPULATION, self.population["url"]))
        receipt = self.run_job(client, NOW + timedelta(hours=1))
        self.assertEqual((1, 1, 1), (len(client.calls), receipt["summary"]["failed"], receipt["summary"]["notDue"]))
        self.assertEqual("seoul_csv_headers_invalid", receipt["sourceResults"][0]["reason"])
        self.assertIsNone(client.calls[0].get_header("If-none-match"))
        self.assertEqual(previous, self.latest_path(self.population).read_bytes())

    def test_304_retains_period_and_no_period_csv_metadata_and_verifies(self):
        self.run_job(self.responses())
        before = [json.loads(self.latest_path(source).read_text(encoding="utf-8")) for source in self.sources]
        client = self.responses(304)
        receipt = self.run_job(client, NOW + timedelta(hours=25))
        self.assertEqual((2, 0), (receipt["summary"]["datasetUnchanged"], receipt["summary"]["receivedBytes"]))
        fields = ["recordCount", "evidenceAsOf", "observationPeriods", "unit", "spatialUnit",
                  "columns", "encoding", "missingNumericCells", "sha256"]
        for source, old, result, req in zip(self.sources, before, receipt["sourceResults"], client.calls):
            self.assertEqual('"csv-fixture"', req.get_header("If-none-match"))
            after = json.loads(self.latest_path(source).read_text(encoding="utf-8"))
            for field in fields:
                self.assertEqual(old[field], after[field], field)
                self.assertEqual(old[field], result[field], field)
        bundle = self.root / "handoff/after-304.zip"
        exported = transfer.export_bundle(self.path, "city-activity", bundle, now=NOW + timedelta(hours=26))
        self.assertEqual("verified", transfer.verify_bundle(bundle, self.path, "city-activity",
                                                            expected_sha256=exported["sha256"])["status"])

    def test_legacy_policy_hash_is_unchanged_and_csv_contract_changes_hash(self):
        legacy = {"sourceId": "world-bank", "datasetId": "arable-kor", "url": "https://api.worldbank.org/example",
                  "kind": "dataset", "validator": "worldbank", "unit": "ha"}
        old_input = {"sourceId": legacy["sourceId"], "datasetId": legacy["datasetId"], "url": legacy["url"],
            "method": "GET", "json": None, "form": None, "kind": "dataset", "validator": "worldbank",
            "seriesIds": None, "unit": "ha", "validationPolicyVersion": "initial-v1", "policyRevision": "initial-v1"}
        old_hash = hashlib.sha256(json.dumps(old_input, sort_keys=True, ensure_ascii=False,
                                            separators=(",", ":")).encode("utf-8")).hexdigest()
        self.assertEqual(old_hash, collector.policy_fingerprint(legacy, NOW))
        self.assertEqual(old_hash, transfer.policy_fingerprint(legacy, collector.stamp(NOW)))
        original = collector.policy_fingerprint(self.population, NOW)
        for key, value in {"requiredColumns": ["NEW"], "periodColumn": None, "periodFormat": "YYYYMM",
                           "expectedPeriod": "20262", "idColumns": ["AREA_CD"],
                           "numericColumns": [], "spatialUnit": "행정동"}.items():
            source = {**copy.deepcopy(self.population), key: value}
            self.assertNotEqual(original, collector.policy_fingerprint(source, NOW), key)
            self.assertEqual(collector.policy_fingerprint(source, NOW),
                             transfer.policy_fingerprint(source, collector.stamp(NOW)))


if __name__ == "__main__":
    unittest.main()
