import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from map_public_api import collect, open_database, register_sources, reserve_page, start_run, status
from map_public_api_transport import IntakeError, load_catalog


def response(rows, total=None, code="00"):
    return json.dumps({"response": {"header": {"resultCode": code},
                                    "body": {"items": rows, "totalCount": len(rows) if total is None else total}}}
                      ).encode()


class MapPublicApiDatabaseTests(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.database = open_database(Path(temporary.name) / "map.sqlite", initialize=True)
        self.addCleanup(self.database.close)
        self.operations = load_catalog()
        register_sources(self.database, self.operations)

    def import_response(self, op_id, params, *responses, rows=20, from_page=None):
        operation = self.operations[op_id]
        bodies = iter(responses)
        return collect(self.database, operation, params, len(responses), rows, from_page=from_page,
                       fetch=lambda *_: next(bodies),
                       secret_provider=lambda op: {op["keyParam"]: "fixture-key"})

    def scalar(self, query):
        return self.database.execute(query).fetchone()[0]

    def test_building_revisions_measure_and_exact_legal_code(self):
        params = {"sigunguCd": "11260", "bjdongCd": "10100"}
        initial = {"mgmBldrgstPk": "hub-1", **params, "heit": "14.350", "grndFlrCnt": "4"}
        changed = {**initial, "heit": "15.900"}
        self.assertEqual(self.import_response("building_titles", params, response([initial]))["newRevisions"], 1)
        self.assertEqual(self.import_response("building_titles", params, response([initial]))["newRevisions"], 0)
        self.assertEqual(self.import_response("building_titles", params, response([changed]))["newRevisions"], 1)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM map_record"), 1)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM record_revision"), 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM response_snapshot"), 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM run_snapshot"), 3)
        self.assertEqual(self.scalar("SELECT value_decimal FROM current_map_observations WHERE unit='m'"), "15.900")
        self.assertEqual(self.scalar("SELECT region_code FROM current_map_regions WHERE region_kind='legal'"),
                         "1126010100")
        self.assertEqual(self.scalar("SELECT scheme FROM current_map_subjects"), "building-hub-title")

    def test_store_delta_shares_identity_and_does_not_revive_deletion_from_full_list(self):
        row = {"bizesId": "shop-1", "bldMngNo": "1126010100112340001000001", "lnoCd": "1126010100112340001",
               "adongCd": "1126057500", "lon": 127.09, "lat": 37.58}
        src = {"divId": "adongCd", "key": "1126057500"}
        self.import_response("shops_by_region", src, response([row]))
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_features"), 1)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_subjects"), 2)
        self.import_response("shops_by_date", {"key": "20261119"},
                             response([{**row, "chgGb": "D", "chgDt": "20261119"}]))
        self.import_response("shops_by_region", src, response([row]))
        self.import_response("shops_by_date", {"key": "20261118"},
                             response([{**row, "chgGb": "U", "chgDt": "20261118"}]))
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM map_record"), 1)
        self.assertEqual(self.scalar("SELECT is_deleted FROM current_map_records"), 1)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_features"), 0)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM record_revision"), 3)

    def test_stop_and_each_route_at_stop_use_shared_subject(self):
        self.import_response("bus_stops_near", {"gpsLati": "37.58", "gpsLong": "127.09"},
                             response([{"nodeid": "STOP1", "gpslati": "37.58", "gpslong": "127.09"}]))
        for stop in ("STOP1", "STOP2"):
            self.import_response("bus_routes_at_stop", {"nodeid": stop, "cityCode": "11"},
                                 response([{"routeid": "ROUTE1"}]))
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_subjects WHERE source_identifier='STOP1'"), 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_subjects WHERE scheme='tago-bus-route'"), 2)

    def test_bounded_and_later_app_failure_never_write_unverified_partial_rows(self):
        params = {"cityCode": "11"}
        page = response([{"nodeid": "STOP1"}], total=2)
        failure = response([], code="30")
        blocked = self.import_response("bus_stops_city", params, page, failure, rows=1)
        self.assertEqual(blocked["state"], "blocked")
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM request_attempt"), 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM map_record"), 0)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM run_snapshot"), 0)
        bounded = self.import_response("bus_stops_city", params, page, rows=1)
        self.assertEqual(bounded["state"], "bounded")
        self.assertEqual(bounded["nextPage"], 2)
        onward = self.import_response("bus_stops_city", params, response([{"nodeid": "STOP2"}], total=2),
                                       rows=1, from_page=bounded["nextPage"])
        self.assertEqual(onward["detail"], "last_page_of_partial_range")
        self.assertIsNone(onward["nextPage"])
        complete = self.import_response("bus_stops_city", params, page,
                                        response([{"nodeid": "STOP2"}], total=2), rows=1)
        self.assertEqual(complete["state"], "complete")
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM map_record"), 2)
        self.assertEqual(status(self.database)["foreignKeyIssues"], 0)

    def test_traffic_request_cap_is_shared_by_operations(self):
        operation = self.operations["traffic_sites"]
        run = start_run(self.database, operation, {})
        for page in range(100):
            reserve_page(self.database, run, operation["portalId"], page)
        other = self.operations["traffic_hour_month"]
        run = start_run(self.database, other, {})
        with self.assertRaisesRegex(IntakeError, "traffic_daily_limit_reached"):
            reserve_page(self.database, run, other["portalId"], 0)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM request_attempt"), 100)

    def test_seoul_commercial_code_needs_a_matching_official_dong_name(self):
        with self.database:
            self.database.execute("INSERT INTO region_release VALUES (1,'fixture','https://example.test','2026-10-30',?)",
                                  ("a" * 64,))
            self.database.execute("INSERT INTO region VALUES (1,'administrative','1126057500','면목제3.8동',NULL,NULL)")
        shop_rows = [{"bizesId": "shop-1", "adongCd": "11260575", "adongNm": "면목3.8동"},
                     {"bizesId": "shop-2", "adongCd": "11260575", "adongNm": "상봉1동"}]
        collected = self.import_response("shops_by_region", {"divId": "adongCd", "key": "11260575"},
                                         response(shop_rows))
        self.assertEqual(collected["rows"], 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_regions WHERE region_code='1126057500'"), 1)

    def test_port_nested_details_count_by_parent_page_and_share_the_port_subject(self):
        rows = [{"useYm": "202609", "prtAgNm": "부산", "details": {"detail": [
            {"nm": "국적선", "etrVsslCo": "10"}, {"nm": "외국선", "etrVsslCo": "11"}]}}]
        collected = self.import_response("port_vessels_month", {"sym": "202609", "eym": "202609"},
                                         response(rows), rows=1)
        self.assertEqual(collected["state"], "complete")
        self.assertEqual(collected["rows"], 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM map_subject WHERE source_identifier='부산'"), 1)

    def test_trade_sido_aggregate_and_chapters_stay_in_their_request_month(self):
        rows = [{"priodTitle": "총계", "expUsdAmt": "100"},
                {"priodTitle": "2026", "hsSgn": "09", "expUsdAmt": "10"}]
        for month in ("202608", "202609"):
            actual = self.import_response("trade_sido", {"sidoCd": "11", "strtYymm": month, "endYymm": month},
                                          response(rows))
            self.assertEqual(actual["rows"], 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_records"), 4)
        self.assertEqual(self.scalar("SELECT COUNT(DISTINCT period_start) FROM current_map_observations"), 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM map_subject WHERE scheme='hs-chapter'"), 1)

    def test_market_geometry_requires_closed_ring_and_korean_coordinates(self):
        rows = [{"trarNo": "1", "coords": "POLYGON ((127 37, 127.1 37, 127.1 37.1, 127 37))"},
                {"trarNo": "2", "coords": "POLYGON ((127 37, 127.1 37, 127.1 37.1, 127 37.2))"}]
        result = self.import_response("market_areas", {"divId": "signguCd", "key": "11260"}, response(rows))
        self.assertEqual(result["rows"], 2)
        self.assertEqual(self.scalar("SELECT COUNT(*) FROM current_map_features WHERE geometry_json IS NOT NULL"), 1)


if __name__ == "__main__":
    unittest.main()
