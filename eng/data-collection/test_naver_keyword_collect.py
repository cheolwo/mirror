"""합성 응답의 오프라인 계약 시험. 외부 HTTP나 계정 작업을 실행하지 않는다."""
import copy
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import tempfile
import unittest
from urllib import error, request

from collect_public_snapshots import CollectionError
import naver_keyword_collect as collector
from test_collect_public_snapshots import Client, Response

NOW = datetime(2026, 11, 14, 1, 0, tzinfo=timezone.utc)
ENV = {"NAVER_API_HUB_CLIENT_ID": "test-hub-client-identifier",
       "NAVER_API_HUB_CLIENT_SECRET": "test-hub-client-secret",
       "NAVER_SEARCHAD_API_KEY": "test-searchad-api-key",
       "NAVER_SEARCHAD_SECRET_KEY": "test-searchad-signing-secret",
       "NAVER_SEARCHAD_CUSTOMER_ID": "987654321"}


def manifest(enabled=True):
    value = json.loads(Path(__file__).with_name("naver-keyword.jobs.json").read_text(encoding="utf-8"))
    value["datalab"]["numericPersistenceEnabled"] = enabled
    return value


def datalab_value(source):
    query = source["json"]
    return {**{key: query[key] for key in ("startDate", "endDate", "timeUnit")},
        "results": [{"title": group["groupName"], "keywords": group["keywords"],
                     "data": [{"period": "2025-11-01", "ratio": 50}, {"period": "2026-10-01", "ratio": 100}]}
                    for group in query["keywordGroups"]]}


def searchad_value(source):
    return {"keywordList": [{"relKeyword": term, "monthlyPcQcCnt": "< 10", "monthlyMobileQcCnt": 20,
        "monthlyAvePcClkCnt": 0.2, "monthlyAveMobileClkCnt": 1.5,
        "monthlyAvePcCtr": 1.2, "monthlyAveMobileCtr": 2.5,
        "plAvgDepth": 3, "compIdx": "높음"} for term in source["seedKeywords"]]}


def response(source, value=None):
    if value is None:
        value = datalab_value(source) if source["provider"] == "datalab" else searchad_value(source)
    return Response(json.dumps(value, ensure_ascii=False).encode(), url=source["url"], headers={"Content-Type": "application/json"})


class NaverKeywordCollector(unittest.TestCase):
    def assert_code(self, code, callback):
        with self.assertRaises(CollectionError) as caught:
            callback()
        self.assertEqual(code, caught.exception.code)

    def run_in(self, root, policy, client, *, now=NOW, env=ENV, dry_run=False):
        return collector.collect(root, policy, now=now, environ=env, open_client=client,
                                 resolve_dns=False, dry_run=dry_run)

    def test_query_plan_uses_current_api_hub_and_exact_thirteen_seeds(self):
        plan = collector.build_plan(manifest())
        self.assertEqual(6, len(plan))
        self.assertEqual(13, len({word for source in plan[:3] for group in source["json"]["keywordGroups"] for word in group["keywords"]}))
        self.assertEqual([5, 5, 3], [len(source["seedKeywords"]) for source in plan[3:]])
        self.assertTrue(all(source["url"] == "https://naverapihub.apigw.ntruss.com/search-trend/v1/search" for source in plan[:3]))
        self.assertTrue(all("openapi.naver.com" not in source["url"] for source in plan))

    def test_manifest_endpoint_env_binding_and_limits_cannot_expand(self):
        for field, value, code in [("maximumRequests", 7, "limits_contract_invalid"),
                                    ("authorizationRevision", "secret", "authorization_revision_invalid")]:
            policy = manifest()
            policy[field] = value
            self.assert_code(code, lambda: collector.build_plan(policy))
        policy = manifest()
        policy["datalab"]["endpoint"] = "https://openapi.naver.com/v1/datalab/search"
        self.assert_code("endpoint_not_allowed", lambda: collector.build_plan(policy))
        policy = manifest()
        policy["datalab"]["clientIdEnvironment"] = "NCP_MAPS_CLIENT_ID"
        self.assert_code("credential_binding_invalid", lambda: collector.build_plan(policy))

    def test_missing_keys_write_blocked_receipt_without_http_or_fake_observations(self):
        with tempfile.TemporaryDirectory() as root:
            client = Client()
            result = self.run_in(root, manifest(False), client, env={"NAVER_MAPS_CLIENT_ID": "unrelated-map-key"})
            self.assertEqual(("blocked", 0, 0, []), (result["status"], result["requestCount"], result["observationRows"], client.calls))
            self.assertEqual({"credential_not_configured"}, {row["reason"] for row in result["sourceResults"]})
            self.assertEqual(1, len(list(Path(root).rglob("receipt.json"))))
            self.assertFalse(list(Path(root).rglob("*.raw.json")))

    def test_api_hub_storage_gate_blocks_even_with_keys(self):
        with tempfile.TemporaryDirectory() as root:
            env = {key: value for key, value in ENV.items() if key.startswith("NAVER_API_HUB")}
            client = Client()
            result = self.run_in(root, manifest(False), client, env=env)
            self.assertEqual("storage_terms_unconfirmed", result["sourceResults"][0]["reason"])
            self.assertEqual((0, []), (result["requestCount"], client.calls))

    def test_dry_run_has_no_writes_or_http(self):
        with tempfile.TemporaryDirectory() as root:
            client = Client()
            result = self.run_in(root, manifest(), client, dry_run=True)
            self.assertEqual("dry_run", result["status"])
            self.assertEqual({"ready"}, {row["status"] for row in result["sourceResults"]})
            self.assertEqual(([], []), (list(Path(root).iterdir()), client.calls))

    def test_success_records_units_sparse_periods_and_no_credential_material(self):
        policy = manifest()
        plan = collector.build_plan(policy)
        with tempfile.TemporaryDirectory() as root:
            client = Client(*(response(source) for source in plan))
            result = self.run_in(root, policy, client)
            self.assertEqual(("completed", 6, 43), (result["status"], result["requestCount"], result["observationRows"]))
            self.assertEqual(0, result["advertiserPerformanceRows"])
            for path in Path(root).rglob("*"):
                if path.is_file():
                    body = path.read_bytes()
                    for secret in ENV.values():
                        self.assertNotIn(secret.encode(), body, str(path))
            rows = result["sourceResults"]
            first = json.loads((Path(root) / rows[0]["structuredPath"]).read_text(encoding="utf-8"))
            self.assertEqual("request_relative_index_0_100", first["unit"])
            self.assertEqual(10, len(first["rows"]))
            last = json.loads((Path(root) / rows[-1]["structuredPath"]).read_text(encoding="utf-8"))
            self.assertEqual("최근30일", last["measurementWindows"]["searchCounts"])
            self.assertIsNone(last["rows"][0]["pcSearchesLast30Days"]["value"])

    def test_headers_use_current_product_and_searchad_path_only_signature(self):
        policy = manifest()
        plan = collector.build_plan(policy)
        with tempfile.TemporaryDirectory() as root:
            client = Client(*(response(source) for source in plan))
            self.run_in(root, policy, client)
            first, last = client.calls[0][0], client.calls[-1][0]
            self.assertEqual(ENV["NAVER_API_HUB_CLIENT_ID"], first.get_header("X-ncp-apigw-api-key-id"))
            self.assertIsNone(first.get_header("X-naver-client-id"))
            self.assertEqual(ENV["NAVER_SEARCHAD_CUSTOMER_ID"], last.get_header("X-customer"))
            timestamp = str(int(NOW.timestamp() * 1000))
            self.assertEqual(collector.signature(ENV["NAVER_SEARCHAD_SECRET_KEY"], timestamp), last.get_header("X-signature"))
        self.assertEqual("BsMJrF4jm1rmuAHnEqsrHkLxCf+2tMSVXsEoNz3VJxs=", collector.signature("secret", "1234"))

    def test_authenticated_client_rejects_wrong_host_before_attaching_headers(self):
        client = Client()
        wrapper = collector.AuthenticatedClient("searchad", tuple(ENV[key] for key in collector.ENVIRONMENTS["searchad"]), NOW, client=client)
        req = request.Request("https://other.example/keywordstool", method="GET")
        self.assert_code("authenticated_endpoint_invalid", lambda: wrapper.open(req, 30))
        self.assertEqual([], client.calls)
        self.assertIsNone(req.get_header("X-api-key"))

    def test_same_policy_success_is_not_due_then_eligible_after_168_hours(self):
        policy = manifest()
        plan = collector.build_plan(policy)
        with tempfile.TemporaryDirectory() as root:
            first = self.run_in(root, policy, Client(*(response(source) for source in plan)))
            client = Client()
            second = self.run_in(root, policy, client, now=NOW + timedelta(hours=167))
            self.assertEqual({"not_due"}, {row["status"] for row in second["sourceResults"]})
            self.assertEqual(0, second["requestCount"])
            third = self.run_in(root, policy, Client(*(response(source) for source in plan)), now=NOW + timedelta(hours=168))
            self.assertEqual((6, first["observationRows"]), (third["requestCount"], third["observationRows"]))

    def test_http_refusal_stops_provider_and_stays_blocked_until_authorization_review(self):
        policy = manifest()
        plan = collector.build_plan(policy)
        env = {key: value for key, value in ENV.items() if key.startswith("NAVER_SEARCHAD")}
        refused = error.HTTPError(plan[3]["url"], 403, "private-server-error", {}, None)
        with tempfile.TemporaryDirectory() as root:
            client = Client(refused)
            first = self.run_in(root, policy, client, env=env)
            self.assertEqual(1, len(client.calls))
            self.assertEqual("http_403", first["sourceResults"][3]["reason"])
            second = self.run_in(root, policy, Client(), env=env, now=NOW + timedelta(days=8))
            self.assertEqual(0, second["requestCount"])
            policy["purpose"] = "안내 문구 변경은 인증 개선이 아님"
            edited = self.run_in(root, policy, Client(), env=env, now=NOW + timedelta(days=8))
            self.assertEqual(0, edited["requestCount"])
            policy["authorizationRevision"] = 1
            restored = self.run_in(root, policy, Client(*(response(source) for source in plan[3:])), env=env, now=NOW + timedelta(days=8))
            self.assertEqual((3, 13), (restored["requestCount"], restored["observationRows"]))
            for path in Path(root).rglob("*.json"):
                self.assertNotIn("private-server-error", path.read_text(encoding="utf-8"))

    def test_reflected_credentials_are_never_saved_and_stop_provider(self):
        policy = manifest()
        plan = collector.build_plan(policy)
        value = datalab_value(plan[0])
        value["unexpectedEcho"] = ENV["NAVER_API_HUB_CLIENT_SECRET"]
        env = {key: value for key, value in ENV.items() if key.startswith("NAVER_API_HUB")}
        with tempfile.TemporaryDirectory() as root:
            client = Client(response(plan[0], value))
            result = self.run_in(root, policy, client, env=env)
            self.assertEqual((1, "credential_reflected_in_response"), (result["requestCount"], result["sourceResults"][0]["reason"]))
            self.assertFalse(list(Path(root).rglob("*.raw.json")))
            self.assertTrue(all(ENV["NAVER_API_HUB_CLIENT_SECRET"].encode() not in p.read_bytes() for p in Path(root).rglob("*") if p.is_file()))

    def test_oversized_or_redirected_response_is_not_saved(self):
        policy = manifest()
        plan = collector.build_plan(policy)
        env = {key: value for key, value in ENV.items() if key.startswith("NAVER_API_HUB")}
        oversized = Response(b"{}", url=plan[0]["url"], headers={"Content-Length": "262145"})
        with tempfile.TemporaryDirectory() as root:
            client = Client(oversized, response(plan[1]), response(plan[2]))
            result = self.run_in(root, policy, client, env=env)
            self.assertEqual("response_too_large", result["sourceResults"][0]["reason"])
            self.assertFalse(list(Path(root).rglob("datalab-01.raw.json")))
        with tempfile.TemporaryDirectory() as root:
            client = Client(error.HTTPError(plan[0]["url"], 302, "redirect", {}, None))
            result = self.run_in(root, policy, client, env=env)
            self.assertEqual((1, "redirect_forbidden"), (result["requestCount"], result["sourceResults"][0]["reason"]))

    def test_unicode_escaped_reflected_credentials_in_unknown_fields_are_not_saved(self):
        policy = manifest()
        source = collector.build_plan(policy)[3]
        env = {key: value for key, value in ENV.items() if key.startswith("NAVER_SEARCHAD")}
        for secret in env.values():
            with self.subTest(credential=secret.split("-")[0]), tempfile.TemporaryDirectory() as root:
                value = searchad_value(source)
                value["unknown"] = [{"echo": secret}]
                body = json.dumps(value, ensure_ascii=False).encode()
                escaped = "".join("\\u%04x" % ord(char) for char in secret).encode()
                body = body.replace(secret.encode(), escaped)
                self.assertNotIn(secret.encode(), body)
                client = Client(Response(body, url=source["url"]))
                result = self.run_in(root, policy, client, env=env)
                self.assertEqual((1, "credential_reflected_in_response"), (result["requestCount"], result["sourceResults"][3]["reason"]))
                self.assertFalse(list(Path(root).rglob("*.raw.json")))
                self.assertEqual(0, result["observationRows"])
                for path in Path(root).rglob("*.json"):
                    saved = path.read_bytes()
                    self.assertNotIn(secret.encode(), saved)
                    self.assertNotIn(escaped, saved)

    def test_duplicate_json_keys_are_rejected_before_unknown_fields_can_be_hidden(self):
        self.assert_code("response_duplicate_json_key", lambda: collector.json_body(b'{"echo":"x","echo":"y"}'))

    def test_datalab_response_requires_exact_groups_periods_and_finite_ratios(self):
        source = collector.build_plan(manifest())[0]
        original = datalab_value(source)
        for change, code in [(lambda x: x.update(timeUnit="date"), "datalab_period_contract_invalid"),
                             (lambda x: x["results"][0].update(title="다른키워드"), "datalab_group_identity_invalid"),
                             (lambda x: x["results"][0]["data"].append(x["results"][0]["data"][0]), "datalab_periods_invalid"),
                             (lambda x: x["results"][0]["data"][0].update(ratio=float("nan")), "numeric_value_invalid"),
                             (lambda x: x["results"][0]["data"][0].update(ratio=101), "numeric_value_invalid")]:
            value = copy.deepcopy(original)
            change(value)
            self.assert_code(code, lambda: collector.validate_datalab(value, source))

    def test_search_count_censoring_null_and_zero_are_distinct(self):
        self.assertEqual({"raw": "< 10", "status": "censored", "value": None, "upperBound": 10, "upperBoundInclusive": False}, collector.search_count("< 10"))
        self.assertEqual("censored", collector.search_count("<10")["status"])
        self.assertEqual("missing", collector.search_count(None)["status"])
        self.assertEqual(0, collector.search_count(0)["value"])
        for value in (-1, 3.5, True, "many"):
            self.assert_code("searchad_search_count_invalid" if value != -1 else "numeric_value_invalid", lambda: collector.search_count(value))

    def test_searchad_no_duplicate_keywords_and_ctr_scale_is_not_invented(self):
        source = collector.build_plan(manifest())[3]
        value = searchad_value(source)
        value["keywordList"].append(value["keywordList"][0])
        self.assert_code("searchad_duplicate_keyword", lambda: collector.validate_searchad(value, source))
        value = searchad_value(source)
        value["keywordList"][0]["monthlyAvePcCtr"] = float("inf")
        self.assert_code("numeric_value_invalid", lambda: collector.validate_searchad(value, source))
        value = searchad_value(source)
        value["keywordList"][0]["monthlyAvePcCtr"] = 101
        result = collector.validate_searchad(value, source)
        self.assertEqual("provider_reported_ctr_scale_unconfirmed", result["units"]["ctr"])
        self.assertEqual(101, result["rows"][0]["monthlyAvePcCtr"])
        value["keywordList"][0]["monthlyAvePcCtr"] = "1.25"
        self.assertEqual("1.25", collector.validate_searchad(value, source)["rows"][0]["monthlyAvePcCtr"])


if __name__ == "__main__":
    unittest.main()
