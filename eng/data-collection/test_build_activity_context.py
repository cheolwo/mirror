"""Offline checks for context references without area-to-road allocation."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import build_activity_context as context


class ActivityContextTests(unittest.TestCase):
    def test_many_to_many_keeps_area_context_and_never_allocates_population(self):
        relations = [
            {"administrativeCode": "11260510", "legalCode": "1126010100", "legalName": "면목동"},
            {"administrativeCode": "11260520", "legalCode": "1126010100", "legalName": "면목동"},
            {"administrativeCode": "11260510", "legalCode": "1126010200", "legalName": "상봉동"},
        ]
        population = {"11260510": {"총_유동인구_수": "100"}, "11260520": {"총_유동인구_수": "200"}}
        walks = [{"노드링크 유형": "LINK", "읍면동코드": "1126010100", "링크 ID": "1"},
                 {"노드링크 유형": "NODE", "읍면동코드": "1126010100", "링크 ID": "0"}]
        legal, roads, unresolved = context.build_relations(relations, population, walks)
        self.assertEqual(["11260510", "11260520"], legal[0]["administrativeContextCodes"])
        self.assertEqual(1, len(roads))
        self.assertIsNone(roads[0]["roadPopulation"])
        self.assertIsNone(legal[0]["legalDongPopulation"])
        self.assertEqual("WholeAdministrativeDongNotRoad", roads[0]["contextScope"])
        self.assertEqual([], unresolved)

    def test_bad_codes_rejected_and_changed_admin_remains_unresolved(self):
        with self.assertRaisesRegex(context.ContextError, "relation_code_invalid"):
            context.build_relations([{"administrativeCode": "1126010100", "legalCode": "1126010100", "legalName": "면목동"}], {}, [])
        _, roads, unresolved = context.build_relations([], {"11260510": {}},
            [{"노드링크 유형": "LINK", "읍면동코드": "1126010100", "링크 ID": "1"}])
        self.assertEqual("Unresolved", roads[0]["relationStatus"])
        self.assertEqual(2, len(unresolved))
        self.assertIsNone(roads[0]["roadPopulation"])

    def test_source_sha_mismatch_and_path_escape_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "seoul-trading-population"
            p.mkdir()
            (p / "body.raw").write_bytes(b"a,b\n1,2\n")
            pointer = {"id": p.name, "sourceId": "seoul-open-data", "datasetId": "OA-15568",
                       "body": "body.raw", "sha256": "0" * 64, "recordCount": 1}
            (p / "latest.json").write_text(json.dumps(pointer), encoding="utf8")
            with self.assertRaisesRegex(context.ContextError, "source_sha256_mismatch"):
                context.read_snapshot(p)
            pointer["body"] = "../escape.raw"
            (p / "latest.json").write_text(json.dumps(pointer), encoding="utf8")
            with self.assertRaisesRegex(context.ContextError, "snapshot_body_path_invalid"):
                context.read_snapshot(p)

    def test_other_valid_source_identity_is_rejected_before_body_use(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "seoul-trading-population"
            p.mkdir()
            body = b"a,b\n1,2\n"
            (p / "body.raw").write_bytes(body)
            base = {"id": p.name, "sourceId": "seoul-open-data", "datasetId": "OA-15568",
                    "body": "body.raw", "sha256": hashlib.sha256(body).hexdigest(),
                    "recordCount": 1, "encoding": "utf-8"}
            (p / "latest.json").write_text(json.dumps(base), encoding="utf8")
            self.assertEqual(1, len(context.read_snapshot(p)[0]))
            for key, value in (("id", "seoul-trading-areas"), ("sourceId", "other-provider"), ("datasetId", "OA-15560")):
                with self.subTest(key=key):
                    pointer = dict(base, **{key: value})
                    (p / "latest.json").write_text(json.dumps(pointer), encoding="utf8")
                    with self.assertRaisesRegex(context.ContextError, "snapshot_source_identity_mismatch"):
                        context.read_snapshot(p)

    def test_latest_population_uses_admin_codes_not_legal_codes(self):
        rows = [{"기준_년분기_코드": "20261", "행정동_코드": "11260510"},
                {"기준_년분기_코드": "20262", "행정동_코드": "11260510"}]
        period, selected = context.latest_rows(rows, "행정동_코드")
        self.assertEqual("20262", period)
        self.assertEqual(["11260510"], list(selected))
        rows[1]["행정동_코드"] = "1126010100"
        with self.assertRaisesRegex(context.ContextError, "administrative_code_invalid"):
            context.latest_rows(rows, "행정동_코드")

    def test_trading_join_preserves_unmatched_and_does_not_infer_legal_geometry(self):
        areas = [{"상권_코드": "3110001", "상권_코드_명": "A", "행정동_코드": "11260510", "엑스좌표_값": "200000", "와이좌표_값": "450000"},
                 {"상권_코드": "3110002", "상권_코드_명": "B", "행정동_코드": "11260599"}]
        population = {"3110001": {"상권_코드_명": "A", "총_유동인구_수": "900"},
                      "3110003": {"상권_코드_명": "C", "총_유동인구_수": "500"}}
        jurisdictions = [{"administrativeCode": "11260510", "legalCode": "1126010100"},
                         {"administrativeCode": "11260510", "legalCode": "1126010200"}]
        rows, counts = context.build_trading_contexts(areas, population, "20262", jurisdictions)
        self.assertEqual(["1126010100", "1126010200"], rows[0]["legalDongCandidates"])
        self.assertEqual("900", rows[0]["sourceReportedTotal"])
        self.assertFalse(rows[0]["geometryIntersectionPerformed"])
        self.assertIsNone(rows[0]["legalDongPopulation"])
        self.assertEqual("AreaOnly", rows[1]["joinStatus"])
        self.assertIsNone(rows[1]["sourceReportedTotal"])
        self.assertEqual("PopulationOnly", rows[2]["joinStatus"])
        self.assertIsNone(rows[2]["representativeX"])
        self.assertEqual((1, 1, 1), (counts["tradingAreaMatchedCodes"], counts["tradingAreaOnlyCodes"], counts["tradingPopulationOnlyCodes"]))


if __name__ == "__main__":
    unittest.main()
