"""Offline contract checks for bounded public snapshot collection."""
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
from urllib import error
import zipfile

import collect_public_snapshots as collector


NOW = datetime(2026, 11, 11, 1, 0, tzinfo=timezone.utc)
URL = "https://api.worldbank.org/v2/country/KOR/indicator/AG.LND.ARBL.HA?format=json"


def worldbank(value=100):
    return json.dumps([{"lastupdated": "2026-10-01"}, [{"countryiso3code": "KOR",
        "indicator": {"id": "AG.LND.ARBL.HA"}, "date": "2024", "value": value}]]).encode()


def legal_zip(text=None, name="법정동코드 전체자료.txt", **kwargs):
    if text is None:
        text = "법정동코드\t법정동명\t폐지여부\n1111010100\t서울특별시 종로구 청운동\t존재\n"
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(name, text.encode("cp949"), **kwargs)
    return output.getvalue()


class Response(io.BytesIO):
    def __init__(self, body, *, status=200, headers=None, url=URL):
        super().__init__(body)
        self.status = status
        self.headers = HTTPMessage()
        for key, value in (headers or {}).items():
            self.headers[key] = value
        self.url = url

    def geturl(self):
        return self.url


class Client:
    def __init__(self, *responses):
        self.responses = list(responses)
        self.calls = []

    def open(self, req, timeout):
        self.calls.append((req, timeout))
        if not self.responses:
            raise AssertionError("Unexpected HTTP attempt")
        item = self.responses.pop(0)
        if isinstance(item, Exception):
            raise item
        return item


class Validators(unittest.TestCase):
    def assert_code(self, expected, action):
        with self.assertRaises(collector.CollectionError) as context:
            action()
        self.assertEqual(expected, context.exception.code)

    def test_worldbank_identity_date_nonnull_and_asof(self):
        result = collector.validate_worldbank(worldbank(), {}, 10)
        self.assertEqual((1, "2024", "hectares"),
                         (result["recordCount"], result["evidenceAsOf"], result["unit"]))
        self.assertEqual("2026-10-01", result["sourceUpdatedAt"])
        self.assertEqual("year", result["evidenceAsOfPrecision"])
        self.assertEqual(["2024"], result["observationYears"])
        for field, value in [("countryiso3code", "USA"), ("date", "now"), ("indicator", None)]:
            with self.subTest(field=field):
                data = json.loads(worldbank())
                data[1][0][field] = value
                self.assert_code("worldbank_identity_invalid", lambda: collector.validate_worldbank(json.dumps(data).encode(), {}, 10))
        self.assert_code("worldbank_no_numeric_observation", lambda: collector.validate_worldbank(worldbank(None), {}, 10))
        self.assert_code("worldbank_value_invalid", lambda: collector.validate_worldbank(worldbank("100"), {}, 10))
        self.assert_code("worldbank_value_invalid", lambda: collector.validate_worldbank(worldbank(True), {}, 10))
        self.assert_code("worldbank_value_invalid", lambda: collector.validate_worldbank(worldbank(float("nan")), {}, 10))
        data = json.loads(worldbank()); data[0]["lastupdated"] = "unknown"
        self.assert_code("worldbank_asof_missing", lambda: collector.validate_worldbank(json.dumps(data).encode(), {}, 10))

    def test_worldbank_shape_and_record_limit(self):
        for value in [{}, [], [{}, []], [None, []]]:
            self.assert_code("worldbank_shape_invalid", lambda: collector.validate_worldbank(json.dumps(value).encode(), {}, 10))
        data = json.loads(worldbank()); data[1] *= 2
        self.assert_code("worldbank_shape_invalid", lambda: collector.validate_worldbank(json.dumps(data).encode(), {}, 1))

    def test_bls_monthly_series_and_unit(self):
        source = {"seriesIds": ["APU0000701312"], "unit": "USD/lb"}
        data = {"status": "REQUEST_SUCCEEDED", "Results": {"series": [{"seriesID": "APU0000701312",
            "data": [{"year": "2026", "period": "M10", "value": "1.23"}]}]}}
        result = collector.validate_bls(json.dumps(data).encode(), source, 10)
        self.assertEqual((1, "2026-10", "USD/lb"), (result["recordCount"], result["evidenceAsOf"], result["unit"]))
        for key, value in [("period", "M13"), ("year", "unknown"), ("value", "NaN")]:
            broken = copy.deepcopy(data); broken["Results"]["series"][0]["data"][0][key] = value
            self.assert_code("bls_monthly_observation_invalid", lambda: collector.validate_bls(json.dumps(broken).encode(), source, 10))
        broken = copy.deepcopy(data); broken["Results"]["series"][0]["seriesID"] = "APU9999999999"
        self.assert_code("bls_series_identity_invalid", lambda: collector.validate_bls(json.dumps(broken).encode(), source, 10))
        self.assert_code("bls_unit_required", lambda: collector.validate_bls(json.dumps(data).encode(), {"seriesIds": source["seriesIds"]}, 10))
        self.assert_code("bls_response_invalid", lambda: collector.validate_bls(b"[]", source, 10))
        broken = copy.deepcopy(data); broken["Results"]["series"][0]["data"] *= 2
        self.assert_code("record_limit_exceeded", lambda: collector.validate_bls(json.dumps(broken).encode(), source, 1))

    def test_bls_explicit_missing_marker_is_preserved_not_zero_or_price(self):
        source = {"seriesIds": ["APU0000701312"], "unit": "USD/lb"}
        rows = [{"year": str(year), "period": f"M{month:02d}", "value": "1.25"}
                for year, months in [(2025, range(1, 13)), (2026, range(1, 11))] for month in months]
        rows[9]["value"] = "-"
        data = {"status": "REQUEST_SUCCEEDED", "Results": {"series": [{"seriesID": "APU0000701312", "data": rows}]}}
        raw = json.dumps(data).encode()
        result = collector.validate_bls(raw, source, 100)
        self.assertEqual((22, 21, 1), (result["recordCount"], result["numericRecords"], result["missingRecords"]))
        self.assertEqual(["2025-10"], result["missingObservationPeriods"])
        self.assertEqual("2026-10", result["evidenceAsOf"])
        self.assertEqual("month", result["evidenceAsOfPrecision"])
        self.assertIsNone(result["sourceUpdatedAt"])
        self.assertEqual("-", json.loads(raw)["Results"]["series"][0]["data"][9]["value"])
        for invalid in ["N/A", "", None, True]:
            data["Results"]["series"][0]["data"][9]["value"] = invalid
            self.assert_code("bls_monthly_observation_invalid", lambda: collector.validate_bls(json.dumps(data).encode(), source, 100))

    def test_bls_all_missing_is_not_successful_price_collection(self):
        source = {"seriesIds": ["APU0000701312"], "unit": "USD/lb"}
        data = {"status": "REQUEST_SUCCEEDED", "Results": {"series": [{"seriesID": "APU0000701312",
                "data": [{"year": "2026", "period": "M10", "value": "-"}]}]}}
        self.assert_code("bls_no_numeric_observation", lambda: collector.validate_bls(json.dumps(data).encode(), source, 100))

    def test_legal_cp949_headers_and_code_rows(self):
        self.assertEqual(1, collector.validate_legal_zip(legal_zip(), {}, 100)["recordCount"])
        self.assert_code("legal_headers_invalid", lambda: collector.validate_legal_zip(legal_zip("code\tname\n"), {}, 100))
        self.assert_code("legal_row_invalid", lambda: collector.validate_legal_zip(legal_zip("법정동코드\t법정동명\t폐지여부\n1\t서울\t존재"), {}, 100))
        self.assert_code("legal_no_records", lambda: collector.validate_legal_zip(legal_zip("법정동코드\t법정동명\t폐지여부\n"), {}, 100))
        self.assert_code("invalid_zip", lambda: collector.validate_legal_zip(b"not a zip", {}, 100))

    def test_legal_archive_traversal_and_expansion_limits(self):
        for name in ["../escape.txt", "C:/escape.txt", "/escape.txt", "..\\escape.txt"]:
            self.assert_code("zip_unsafe_member", lambda: collector.validate_legal_zip(legal_zip(name=name), {}, 100))
        self.assert_code("zip_expanded_limit_exceeded", lambda: collector.validate_legal_zip(legal_zip(), {"maxExpandedBytes": 10}, 100))
        self.assert_code("zip_compressed_limit_exceeded", lambda: collector.validate_legal_zip(b"0" * (5 * 1024 * 1024 + 1), {}, 100))
        repeated = "법정동코드\t법정동명\t폐지여부\n1111010100\t서울\t존재\n1111010200\t서울\t존재\n"
        self.assert_code("record_limit_exceeded", lambda: collector.validate_legal_zip(legal_zip(repeated), {}, 1))

    def test_legal_archive_is_one_txt_exact_columns_unique_codes(self):
        duplicate = "법정동코드\t법정동명\t폐지여부\n" + "1111010100\t서울\t존재\n" * 2
        self.assert_code("legal_code_duplicate", lambda: collector.validate_legal_zip(legal_zip(duplicate), {}, 10))
        extra_header = "법정동코드\t법정동명\t폐지여부\textra\n1111010100\t서울\t존재\tx\n"
        self.assert_code("legal_headers_invalid", lambda: collector.validate_legal_zip(legal_zip(extra_header), {}, 10))
        extra_row = "법정동코드\t법정동명\t폐지여부\n1111010100\t서울\t존재\tx\n"
        self.assert_code("legal_row_invalid", lambda: collector.validate_legal_zip(legal_zip(extra_row), {}, 10))
        self.assert_code("zip_members_invalid", lambda: collector.validate_legal_zip(legal_zip(name="data.csv"), {}, 10))
        output = io.BytesIO()
        with zipfile.ZipFile(output, "w") as archive:
            archive.writestr("one.txt", "법정동코드\t법정동명\t폐지여부".encode("cp949"))
            archive.writestr("two.txt", "법정동코드\t법정동명\t폐지여부".encode("cp949"))
        self.assert_code("zip_members_invalid", lambda: collector.validate_legal_zip(output.getvalue(), {}, 10))

    def test_catalog_requires_nonempty_identifier_and_name(self):
        for data in [{"identifier": "123", "name": "Catalog"}, [{"id": 1, "name": "one"}], {"datasets": [{"datasetId": "a", "title": "A"}]}]:
            self.assertEqual(1, collector.validate_catalog(json.dumps(data).encode(), {}, 10)["recordCount"])
        for data in [[], {}, {"id": None, "name": "bad"}, {"id": "x", "name": ""}, [{"unrelated": 5}]]:
            with self.assertRaises(collector.CollectionError):
                collector.validate_catalog(json.dumps(data).encode(), {}, 10)

    def test_portal_jsonld_binds_dataset_id_and_official_url(self):
        source = {"datasetId": "15062056", "kind": "metadata"}
        data = {"@context": "https://schema.org", "@type": "Dataset", "name": "Official catalog",
                "url": "https://www.data.go.kr/data/15062056/fileData.do", "dateModified": "2026-10-01"}
        result = collector.validate_catalog(json.dumps(data).encode(), source, 10)
        self.assertEqual((1, "15062056", "2026-10-01"),
                         (result["recordCount"], result["catalogDatasetId"], result["evidenceAsOf"]))
        for key, value in [("url", "https://www.data.go.kr/data/99999999/fileData.do"),
                           ("url", "https://attacker.example/data/15062056/fileData.do"),
                           ("@type", "Organization"), ("identifier", "99999999")]:
            bad = dict(data); bad[key] = value
            self.assert_code("catalog_dataset_binding_invalid", lambda: collector.validate_catalog(json.dumps(bad).encode(), source, 10))

    def test_html_is_metadata_not_dataset_observations(self):
        html = b"<!doctype html><html><head><title>Official source</title></head><body>details</body></html>"
        result = collector.validate_html(html, {"kind": "metadata"}, 10)
        self.assertEqual(0, result["recordCount"])
        self.assertIsNone(result["evidenceAsOf"])
        self.assert_code("html_metadata_only", lambda: collector.validate_html(html, {"kind": "dataset"}, 10))
        self.assert_code("html_body_invalid", lambda: collector.validate_html(b"{}", {"kind": "metadata"}, 10))
        self.assert_code("html_provider_challenge", lambda: collector.validate_html(html.replace(b"Official source", b"Access denied"), {"kind": "metadata"}, 10))


class Security(unittest.TestCase):
    def test_urls_are_exact_allowlisted_https_without_credentials(self):
        self.assertEqual("api.worldbank.org", collector.validate_url(URL, [URL]).hostname)
        bad = ["http://example.org/a", "https://user:pw@example.org/a", "https://example.org/a#fragment",
               "https://localhost/a", "https://127.0.0.1/a", "https://10.0.0.1/a", "https://example.org:444/a",
               "https://example.org/a/../b", "https://example.org/a/%252e%252e/b", "https://example.org/a?serviceKey=x",
               "https://example.org/a?api_key=x", "https://example.org/a?access_token=x", "https://example.org/a\n"]
        for value in bad:
            with self.subTest(url=value), self.assertRaises(collector.CollectionError):
                collector.validate_url(value, [value])
        with self.assertRaises(collector.CollectionError):
            collector.validate_url(URL + "&extra=1", [URL])

    def test_private_dns_is_rejected(self):
        with mock.patch.object(collector.socket, "getaddrinfo", return_value=[(2, 1, 6, "", ("127.0.0.1", 443))]):
            with self.assertRaises(collector.CollectionError):
                collector.public_dns("api.worldbank.org")

    def test_path_identifiers_and_containment(self):
        for value in ["../bad", "a/b", "C:\\bad", "CON", "nul", ".", "a..b", "runs"]:
            with self.assertRaises(collector.CollectionError):
                collector.slug(value)
        self.assertEqual("safe-source", collector.slug("safe-source"))
        with self.assertRaises(collector.CollectionError):
            collector.beneath(Path.cwd(), "../escape")

    def test_redirect_handler_refuses_redirect(self):
        self.assertIsNone(collector.NoRedirect().redirect_request(None, None, 302, "", {}, "https://other.example/a"))

    def test_templates_only_two_tokens_and_korean_year_boundary(self):
        boundary = datetime(2025, 12, 31, 15, 0, tzinfo=timezone.utc)
        self.assertEqual({"a": "2026", "b": "2025"}, collector.year_tokens({"a": "{{currentYear}}", "b": "{{previousYear}}"}, boundary))
        with self.assertRaises(collector.CollectionError):
            collector.year_tokens("{{environment.secret}}", NOW)
        with self.assertRaises(collector.CollectionError):
            collector.no_credentials({"registrationkey": "fake"})


class Collection(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="hongdal-public-snapshot-test-")
        self.workspace = Path(self.temporary.name).resolve()
        (self.workspace / "AGENTS.md").write_text("test workspace", encoding="utf-8")
        self.manifest_path = self.workspace / "eng/data-collection/jobs.json"
        self.manifest_path.parent.mkdir(parents=True)
        self.source = {"id": "worldbank-kor", "sourceId": "world-bank", "datasetId": "arable-kor",
                       "status": "ready", "kind": "dataset", "method": "GET", "url": URL,
                       "validator": "worldbank", "maxBytes": 4096, "maxRecords": 10, "intervalHours": 0}
        self.manifest = {"outputRoot": "../../artifacts/local/data-collection-runs", "security": {"allowedUrls": [URL]},
                         "limits": {"maxRequests": 6, "maxTotalBytes": 12000, "timeoutSeconds": 30},
                         "jobs": [{"id": "test-job", "sources": [self.source]}]}

    def tearDown(self):
        assert self.workspace.is_relative_to(Path(tempfile.gettempdir()).resolve())
        assert self.workspace.name.startswith("hongdal-public-snapshot-test-")
        self.temporary.cleanup()

    @property
    def output(self):
        return self.workspace / "artifacts/local/data-collection-runs"

    @property
    def source_dir(self):
        return self.output / "test-job/worldbank-kor"

    def run_job(self, client=None, now=NOW, **kwargs):
        self.manifest_path.write_text(json.dumps(self.manifest), encoding="utf-8")
        return collector.run(self.manifest_path, "test-job", now=now, open_client=client,
                             resolve_dns=False, **kwargs)

    def test_saved_identical_deduplicated_and_prior_bodies_preserved(self):
        first = self.run_job(Client(Response(worldbank(), headers={"ETag": '"a"'})))
        self.assertEqual(1, first["summary"]["datasetSaved"])
        original = json.loads((self.source_dir / "latest.json").read_text())
        second_client = Client(Response(worldbank()))
        second = self.run_job(second_client, now=NOW + timedelta(hours=1))
        self.assertEqual(1, second["summary"]["datasetUnchanged"])
        self.assertEqual('"a"', second_client.calls[0][0].get_header("If-none-match"))
        self.assertEqual(1, len(list((self.source_dir / "bodies").glob("*.raw"))))
        third = self.run_job(Client(Response(worldbank(101))), now=NOW + timedelta(hours=2))
        self.assertEqual(1, third["summary"]["datasetSaved"])
        self.assertEqual(2, len(list((self.source_dir / "bodies").glob("*.raw"))))
        self.assertTrue((self.source_dir / "bodies" / (original["sha256"] + ".raw")).exists())
        self.assertEqual(3, len(list((self.source_dir / "runs").glob("*.json"))))

    def test_not_due_is_no_request_and_timezone_aware(self):
        self.source["intervalHours"] = 24
        self.run_job(Client(Response(worldbank())))
        client = Client()
        receipt = self.run_job(client, now=NOW + timedelta(hours=23))
        self.assertEqual((0, 1), (len(client.calls), receipt["summary"]["notDue"]))
        latest = json.loads((self.source_dir / "latest.json").read_text())
        latest["collectedAt"] = "2026-11-11T01:00:00"
        (self.source_dir / "latest.json").write_text(json.dumps(latest))
        receipt = self.run_job(Client(), now=NOW + timedelta(hours=25))
        self.assertEqual("timestamp_timezone_required", receipt["sourceResults"][0]["reason"])

    def test_304_requires_valid_prior_raw(self):
        receipt = self.run_job(Client(Response(b"", status=304)))
        self.assertEqual("not_modified_without_previous_body", receipt["sourceResults"][0]["reason"])
        self.run_job(Client(Response(worldbank())))
        receipt = self.run_job(Client(Response(b"", status=304)), now=NOW + timedelta(hours=1))
        self.assertEqual(1, receipt["summary"]["datasetUnchanged"])
        self.assertEqual(0, receipt["summary"]["receivedBytes"])
        body = next((self.source_dir / "bodies").glob("*.raw")); body.unlink()
        receipt = self.run_job(Client())
        self.assertEqual("previous_body_missing", receipt["sourceResults"][0]["reason"])

    def test_invalid_new_payload_preserves_latest(self):
        self.run_job(Client(Response(worldbank())))
        previous = (self.source_dir / "latest.json").read_bytes()
        receipt = self.run_job(Client(Response(b'{"error":"provider failed"}')))
        self.assertEqual("failed", receipt["status"])
        self.assertEqual(1, receipt["summary"]["failed"])
        self.assertEqual(previous, (self.source_dir / "latest.json").read_bytes())
        self.assertEqual(1, len(list((self.source_dir / "bodies").glob("*.raw"))))

    def test_response_limits_apply_to_content_length_and_stream(self):
        self.source["maxBytes"] = 50
        for response in [Response(worldbank(), headers={"Content-Length": "10000"}), Response(worldbank())]:
            receipt = self.run_job(Client(response))
            self.assertEqual("response_too_large", receipt["sourceResults"][0]["reason"])
        self.assertFalse((self.source_dir / "latest.json").exists())

    def test_redirect_is_not_followed_and_receipt_omits_url(self):
        client = Client(Response(b"redirect", status=302, headers={"Location": "https://other.example/secret"}))
        receipt = self.run_job(client)
        self.assertEqual("redirect_forbidden", receipt["sourceResults"][0]["reason"])
        self.assertEqual(1, len(client.calls))
        self.assertNotIn("https://", json.dumps(receipt))

    def test_explicit_unready_source_is_blocked_without_network(self):
        self.source["status"] = "blocked"
        client = Client()
        receipt = self.run_job(client)
        self.assertEqual(("blocked", 0, 1), (receipt["status"], len(client.calls), receipt["summary"]["blocked"]))
        self.assertEqual(0, receipt["summary"]["datasetSaved"])

    def test_auth_refusal_persists_until_request_or_policy_revision_changes(self):
        refusal = error.HTTPError(URL, 403, "sensitive error content must not appear", {}, io.BytesIO(b"secret"))
        first = self.run_job(Client(refusal))
        self.assertEqual("http_403", first["sourceResults"][0]["reason"])
        self.assertTrue((self.source_dir / "blocked-outcome.json").exists())
        client = Client()
        second = self.run_job(client, now=NOW + timedelta(days=30))
        self.assertEqual(0, len(client.calls))
        self.assertTrue(second["sourceResults"][0]["reusedBlockedOutcome"])
        self.assertNotIn("sensitive", json.dumps(second))
        self.source["maxBytes"] = 8192  # Response cap alone cannot lift provider refusal.
        self.assertTrue(self.run_job(Client())["sourceResults"][0]["reusedBlockedOutcome"])
        self.source["policyRevision"] = "reviewed-v2"
        third = self.run_job(Client(Response(worldbank())))
        self.assertEqual("saved", third["sourceResults"][0]["status"])

    def test_transient_failure_has_no_same_run_retry_and_can_be_rechecked(self):
        client = Client(error.URLError("must-not-log"))
        receipt = self.run_job(client)
        self.assertEqual(1, len(client.calls))
        self.assertEqual("network_failed", receipt["sourceResults"][0]["reason"])
        self.assertFalse((self.source_dir / "blocked-outcome.json").exists())
        self.assertEqual("saved", self.run_job(Client(Response(worldbank())))["sourceResults"][0]["status"])

    def test_metadata_saved_is_not_dataset_saved(self):
        self.source.update(kind="metadata", validator="html")
        html = b"<!doctype html><html><head><title>Official source</title></head><body>description</body></html>"
        receipt = self.run_job(Client(Response(html)))
        self.assertEqual((1, 1, 0), (receipt["summary"]["metadataChecked"], receipt["summary"]["metadataSaved"], receipt["summary"]["datasetSaved"]))
        self.assertIsNone(receipt["sourceResults"][0]["evidenceAsOf"])

    def test_global_os_lock_blocks_a_different_job(self):
        self.output.mkdir(parents=True)
        self.manifest["jobs"].append({"id": "other-job", "sources": [copy.deepcopy(self.source)]})
        self.manifest_path.write_text(json.dumps(self.manifest), encoding="utf-8")
        with collector.global_lock(self.output / "collector.lock") as first:
            self.assertTrue(first)
            receipt = collector.run(self.manifest_path, "other-job", now=NOW, open_client=Client(), resolve_dns=False)
            self.assertEqual("skipped_locked", receipt["status"])
            self.assertEqual(0, receipt["summary"]["requestCount"])
        receipt = collector.run(self.manifest_path, "other-job", now=NOW, open_client=Client(Response(worldbank())), resolve_dns=False)
        self.assertEqual("saved", receipt["sourceResults"][0]["status"])

    def test_dry_run_has_no_http_or_files(self):
        receipt = self.run_job(Client(), dry_run=True)
        self.assertEqual(1, receipt["summary"]["dryRun"])
        self.assertFalse(self.output.exists())

    def test_duplicate_source_and_output_escape_are_refused(self):
        self.manifest["jobs"][0]["sources"].append(copy.deepcopy(self.source))
        with self.assertRaises(collector.CollectionError):
            self.run_job(Client())
        self.manifest["jobs"][0]["sources"].pop()
        self.manifest["outputRoot"] = "../../../../outside"
        with self.assertRaises(collector.CollectionError):
            self.run_job(Client())

    def test_global_request_budget_applies_to_all_sources(self):
        self.manifest["limits"]["maxRequests"] = 1
        second = copy.deepcopy(self.source); second["id"] = "second-source"
        self.manifest["jobs"][0]["sources"].append(second)
        client = Client(Response(worldbank()))
        receipt = self.run_job(client)
        self.assertEqual(1, len(client.calls))
        self.assertEqual("job_request_or_byte_budget_reached", receipt["sourceResults"][1]["reason"])

    def test_bls_effective_post_body_uses_only_requested_years(self):
        self.source.update(method="POST", json={"seriesid": ["APU0000701312"], "startyear": "{{previousYear}}", "endyear": "{{currentYear}}"})
        client = Client(Response(worldbank()))
        self.run_job(client)
        data = json.loads(client.calls[0][0].data)
        self.assertEqual(("2025", "2026"), (data["startyear"], data["endyear"]))
        self.assertEqual("POST", client.calls[0][0].method)

    def test_changed_identity_or_request_cannot_reuse_due_headers_or_304(self):
        self.source["intervalHours"] = 24
        self.run_job(Client(Response(worldbank(), headers={"ETag": '"old"'})))
        self.source["datasetId"] = "new-reviewed-dataset"
        client = Client(Response(b"", status=304))
        receipt = self.run_job(client, now=NOW + timedelta(hours=1))
        self.assertEqual(1, len(client.calls))
        self.assertIsNone(client.calls[0][0].get_header("If-none-match"))
        self.assertEqual("not_modified_without_previous_body", receipt["sourceResults"][0]["reason"])
        client = Client(Response(worldbank()))
        receipt = self.run_job(client, now=NOW + timedelta(hours=1))
        self.assertEqual("saved", receipt["sourceResults"][0]["status"])
        self.assertEqual(1, len(list((self.source_dir / "bodies").glob("*.raw"))))

    def test_request_deadline_limits_header_wait(self):
        client = Client(Response(worldbank()))
        # Request creation/open consume the absolute thirty-second budget.
        with mock.patch.object(collector.time, "monotonic", side_effect=[0, 0, 0, 31]):
            receipt = self.run_job(client)
        self.assertEqual("request_timeout", receipt["sourceResults"][0]["reason"])
        self.assertFalse((self.source_dir / "latest.json").exists())

    def test_request_deadline_limits_dripping_body_and_updates_socket_timeout(self):
        response = Response(worldbank())
        response.fp = mock.Mock()
        response.fp.raw._sock = mock.Mock()
        # The first bounded read crosses the absolute deadline even though the
        # individual socket operation itself returned successfully.
        with mock.patch.object(collector.time, "monotonic", side_effect=[0, 0, 0, 0, 1, 31]):
            receipt = self.run_job(Client(response))
        self.assertEqual("request_timeout", receipt["sourceResults"][0]["reason"])
        response.fp.raw._sock.settimeout.assert_called_once_with(29)
        self.assertFalse((self.source_dir / "latest.json").exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
