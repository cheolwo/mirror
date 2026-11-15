"""실제 보존한 WB 원문의 오프라인 회귀시험. 네트워크나 운영 자료를 바꾸지 않는다."""
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

import collect_public_snapshots as collector
import research_trade_seed as seed
from test_collect_public_snapshots import Client, Response

NOW = datetime(2026, 11, 14, 1, 0, tzinfo=timezone.utc)


def manifest():
    return json.loads(Path(__file__).with_name("research-trade.jobs.json").read_text(encoding="utf-8"))


def source(index=0):
    return manifest()["jobs"][0]["sources"][index]


class TradeWorldBank(unittest.TestCase):
    def assert_code(self, code, callback):
        with self.assertRaises(collector.CollectionError) as caught:
            callback()
        self.assertEqual(code, caught.exception.code)

    def test_real_raw_fixtures_have_pinned_hashes_and_units(self):
        for index, raw, digest, unit in [
            (0, EXCHANGE, "df70617adf4518c6c0fa3b3f46cf0a5e2ea016145533a8f3b251719e1e384119", "KRW_per_USD"),
            (1, EXPORTS, "f32554e82c7873e2e57605e0df4ed3fa3ba1f3192ae8bb7e24b680db2566e15b", "USD_current")]:
            with self.subTest(index=index):
                self.assertEqual(digest, hashlib.sha256(raw).hexdigest())
                result = collector.validate_worldbank(raw, source(index), 12)
                self.assertEqual((11, 11, "2025", unit),
                    (result["recordCount"], result["numericRecords"], result["evidenceAsOf"], result["unit"]))
                self.assertEqual("2026-11-11", result["sourceUpdatedAt"])
                self.assertEqual([str(y) for y in range(2015, 2026)], result["observationYears"])

    def test_existing_arable_raw_result_and_policy_hash_are_unchanged(self):
        self.assertEqual("d2932ef3934c359ac1fc706641037d3428a48f4b903ad45281317065ef8b0a5d", hashlib.sha256(ARABLE).hexdigest())
        result = collector.validate_worldbank(ARABLE, {}, 12)
        self.assertEqual({"recordCount": 1, "numericRecords": 1, "evidenceAsOf": "2024",
            "evidenceAsOfPrecision": "year", "sourceUpdatedAt": "2026-10-08",
            "observationYears": ["2024"], "numericObservationYears": ["2024"], "unit": "hectares"}, result)
        old_manifest = json.loads(Path(__file__).with_name("jobs.json").read_text(encoding="utf-8-sig"))
        old = next(s for j in old_manifest["jobs"] if j["id"] == "production-prices"
                   for s in j["sources"] if s.get("validator") == "worldbank")
        self.assertEqual("6f6bd4f6acc23597894772e5550d45c82778cdbe6d0c5f78f23f8f20e246cbb7",
                         collector.policy_fingerprint(old, NOW))

    def test_only_three_explicit_indicators_are_allowed(self):
        bad = dict(source(), indicatorId="NY.GDP.MKTP.CD")
        self.assert_code("worldbank_indicator_not_allowed", lambda: collector.validate_worldbank(EXCHANGE, bad, 12))
        bad = dict(source(), indicatorId=None)
        self.assert_code("worldbank_indicator_not_allowed", lambda: collector.validate_worldbank(EXCHANGE, bad, 12))
        self.assert_code("worldbank_identity_invalid", lambda: collector.validate_worldbank(EXPORTS, source(), 12))
        self.assert_code("worldbank_identity_invalid", lambda: collector.validate_worldbank(EXCHANGE, {}, 12))

    def test_wrong_country_and_non_year_are_rejected(self):
        for key, value in [("countryiso3code", "USA"), ("date", "2025-Q1")]:
            data = json.loads(EXCHANGE)
            data[1][0][key] = value
            self.assert_code("worldbank_identity_invalid", lambda: collector.validate_worldbank(json.dumps(data).encode(), source(), 12))

    def test_configured_unit_and_response_unit_must_match_the_indicator(self):
        for unit in ["USD_current", "KRW", None]:
            self.assert_code("worldbank_unit_contract_invalid", lambda: collector.validate_worldbank(EXCHANGE, dict(source(), unit=unit), 12))
        data = json.loads(EXCHANGE)
        data[1][0]["unit"] = "USD_current"
        self.assert_code("worldbank_unit_invalid", lambda: collector.validate_worldbank(json.dumps(data).encode(), source(), 12))

    def test_wrong_contract_is_blocked_before_http(self):
        with tempfile.TemporaryDirectory(prefix="hongdal-worldbank-trade-") as directory:
            client = Client()
            budget = {"requests": 0, "bytes": 0}
            item = dict(source(), unit="USD_current")
            result = collector.collect_source(item, Path(directory), [item["url"]],
                {"maxTotalBytes": 524288, "maxRequests": 2, "timeoutSeconds": 30}, NOW, budget,
                open_client=client, resolve_dns=False)
            self.assertEqual(("blocked", "worldbank_unit_contract_invalid"), (result["status"], result["reason"]))
            self.assertEqual(([], 0), (client.calls, budget["requests"]))

    def test_null_is_missing_and_cannot_advance_numeric_evidence_year(self):
        data = json.loads(EXCHANGE)
        data[1][0]["value"] = None
        result = collector.validate_worldbank(json.dumps(data).encode(), source(), 12)
        self.assertEqual((11, 10, "2024"), (result["recordCount"], result["numericRecords"], result["evidenceAsOf"]))
        self.assertIn("2025", result["observationYears"])
        self.assertNotIn("2025", result["numericObservationYears"])
        self.assertIsNone(data[1][0]["value"])
        for row in data[1]:
            row["value"] = None
        self.assert_code("worldbank_no_numeric_observation", lambda: collector.validate_worldbank(json.dumps(data).encode(), source(), 12))

    def test_duplicate_years_are_rejected(self):
        data = json.loads(EXCHANGE)
        data[1][1]["date"] = data[1][0]["date"]
        self.assert_code("worldbank_year_duplicate", lambda: collector.validate_worldbank(json.dumps(data).encode(), source(), 12))

    def test_negative_nonfinite_boolean_and_text_are_not_observations(self):
        for value in [-1, float("nan"), float("inf"), True, "1422.44"]:
            data = json.loads(EXCHANGE)
            data[1][0]["value"] = value
            self.assert_code("worldbank_value_invalid", lambda: collector.validate_worldbank(json.dumps(data).encode(), source(), 12))

    def test_record_limit_and_explicit_policy_binding(self):
        self.assert_code("worldbank_shape_invalid", lambda: collector.validate_worldbank(EXCHANGE, source(), 10))
        first = source()
        second = dict(first, indicatorId="TX.VAL.MRCH.CD.WT", unit="USD_current")
        self.assertNotEqual(collector.policy_fingerprint(first, NOW), collector.policy_fingerprint(second, NOW))


class TradeSeed(unittest.TestCase):
    def test_exact_two_seeds_preserve_provenance_then_not_due_has_no_http(self):
        with tempfile.TemporaryDirectory(prefix="hongdal-worldbank-seed-") as directory:
            workspace = Path(directory)
            (workspace / "AGENTS.md").write_text("isolated test workspace", encoding="utf-8")
            config = workspace / "eng/data-collection/research-trade.jobs.json"
            config.parent.mkdir(parents=True)
            config.write_text(json.dumps(manifest()), encoding="utf-8")
            for baseline, body in zip(seed.BASELINES, [EXCHANGE, EXPORTS]):
                original = workspace / seed.BASE / "raw" / (baseline["stem"] + ".json")
                original.parent.mkdir(parents=True, exist_ok=True)
                original.write_bytes(body)
                url = "https://api.worldbank.org/v2/country/KOR/indicator/" + baseline["indicatorId"] + "?format=json&date=2015:2025&per_page=100"
                (workspace / seed.BASE / (baseline["stem"] + ".acquisition.json")).write_text(json.dumps({
                    "rawSha256": baseline["sha256"], "url": url, "status": "saved", "httpStatus": 200,
                    "collectedAtUtc": baseline["collectedAt"]}), encoding="utf-8")
            with mock.patch.object(seed, "ROOT", workspace), mock.patch.object(seed, "MANIFEST", config), \
                    mock.patch.object(collector, "fetch", side_effect=AssertionError("HTTP forbidden")), \
                    mock.patch.object(collector, "public_dns", side_effect=AssertionError("DNS forbidden")):
                result = seed.seed(now=NOW)
                self.assertEqual(("completed", len(EXCHANGE) + len(EXPORTS), 0),
                                 (result["status"], result["copiedBodyBytes"], result["httpRequestCount"]))
                job_dir = workspace / "artifacts/local/data-collection-runs/research-trade"
                before = {path: path.read_bytes() for path in job_dir.glob("*/latest.json")}
                self.assertEqual(2, len(before))
                for raw_pointer in before.values():
                    pointer = json.loads(raw_pointer)
                    self.assertTrue(pointer["seededBaseline"])
                    self.assertIsNone(pointer["httpStatus"])
                    self.assertIsNone(pointer["etag"])
                    self.assertIsNone(pointer["lastModified"])
                    adoption = pointer["baselineAdoption"]
                    self.assertEqual((11, 12, False, False), (adoption["baselineRecordCount"], adoption["requestedRecentRecords"],
                        adoption["httpPerformed"], adoption["scheduledRequestCoverageVerified"]))
                    self.assertEqual(collector.aware_time(adoption["originalCollectedAt"]), collector.aware_time(pointer["collectedAt"]))
                for dry_run in [True, False]:
                    receipt = collector.run(config, "research-trade", now=NOW, dry_run=dry_run)
                    self.assertEqual((2, 0, 0), (receipt["summary"]["notDue"], receipt["summary"]["requestCount"], receipt["summary"]["receivedBytes"]))
                repeated = seed.seed(now=NOW + timedelta(hours=1))
                self.assertEqual(0, repeated["copiedBodyBytes"])
                self.assertTrue(all(s["status"] == "existing_baseline" for s in repeated["sources"]))
                self.assertEqual(before, {path: path.read_bytes() for path in before})
                changed = json.loads(next(iter(before.values())))
                changed["collectedAt"] = collector.stamp(NOW + timedelta(hours=2))
                next(iter(before)).write_text(json.dumps(changed), encoding="utf-8")
                with self.assertRaises(collector.CollectionError) as caught:
                    seed.seed(now=NOW + timedelta(hours=3))
                self.assertEqual("seed_existing_state_conflict", caught.exception.code)
# 2026-11-14 실제 취득 원문의 bytes를 그대로 동결한 공개 통계 fixture다.
EXCHANGE = b'[{"page":1,"pages":1,"per_page":100,"total":11,"sourceid":"2","lastupdated":"2026-11-11"},[{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2025","value":1422.44083333333,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2024","value":1363.375,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2023","value":1305.6625,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2022","value":1291.44666666666,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2021","value":1143.95166666666,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2020","value":1180.26583333333,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2019","value":1165.3575,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2018","value":1100.16333333333,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2017","value":1131.00083333333,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2016","value":1160.7675,"unit":"","obs_status":"","decimal":2},{"indicator":{"id":"PA.NUS.FCRF","value":"Official exchange rate (LCU per US$, period average)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2015","value":1130.9525,"unit":"","obs_status":"","decimal":2}]]'
EXPORTS = b'[{"page":1,"pages":1,"per_page":100,"total":11,"sourceid":"2","lastupdated":"2026-11-11"},[{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2025","value":709330000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2024","value":683609000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2023","value":632226000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2022","value":683585000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2021","value":644400000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2020","value":512498000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2019","value":542233000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2018","value":604860000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2017","value":573694000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2016","value":495426000000,"unit":"","obs_status":"","decimal":0},{"indicator":{"id":"TX.VAL.MRCH.CD.WT","value":"Merchandise exports (current US$)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2015","value":526757000000,"unit":"","obs_status":"","decimal":0}]]'
ARABLE = b'[{"page":1,"pages":1,"per_page":100,"total":1,"sourceid":"2","lastupdated":"2026-10-08"},[{"indicator":{"id":"AG.LND.ARBL.HA","value":"Arable land (hectares)"},"country":{"id":"KR","value":"Korea, Rep."},"countryiso3code":"KOR","date":"2024","value":1302000,"unit":"","obs_status":"","decimal":0}]]'


if __name__ == "__main__":
    unittest.main(verbosity=2)
