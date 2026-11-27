"""명세 기반 응답 예시로 수집 경계와 파싱을 검증한다."""
import io
import json
import os
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import Mock, patch
from urllib.error import HTTPError
from urllib.parse import parse_qs, urlsplit

import map_public_api_transport as transport


def payload(value):
    return json.dumps(value, ensure_ascii=False).encode("utf-8")


def standard(items, count):
    return {"response": {"header": {"resultCode": "00"},
                         "body": {"items": {"item": items}, "totalCount": count}}}


class PublicMapTransportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = transport.load_catalog()

    def test_standard_json_keeps_only_map_fields(self):
        row = {"nodeid": "TEST001", "nodenm": "시험 정류소", "gpslati": 37.5, "gpslong": 127.0,
               "serviceKey": "synthetic-query-key", "phone": "synthetic-contact"}
        rows, count, total = transport.decode_page(
            self.catalog["bus_stops_near"], payload(standard([row], 3)), ["synthetic-query-key"])
        self.assertEqual((count, total), (1, 3))
        self.assertEqual(rows, [(("TEST001",), {"nodeid": "TEST001", "nodenm": "시험 정류소",
                                                "gpslati": "37.5", "gpslong": "127.0"})])

    def test_government_xml_preserves_values_and_keys(self):
        xml = """<response xmlns="urn:example"><header><resultCode>00</resultCode></header>
          <body><items>
            <item><priodTitle>2026-09</priodTitle><sggNm>동대문구</sggNm><impUsdAmt>1,025.70</impUsdAmt></item>
            <item><priodTitle>2026-09</priodTitle><sggNm>중랑구</sggNm><impUsdAmt>0</impUsdAmt></item>
          </items><totalCount>2</totalCount></body></response>""".encode()
        rows, count, total = transport.decode_page(self.catalog["trade_sigungu"], xml)
        self.assertEqual((count, total), (2, 2))
        self.assertEqual(dict(rows)[("2026-09", "동대문구")]["impUsdAmt"], "1,025.70")
        self.assertEqual(dict(rows)[("2026-09", "중랑구")]["impUsdAmt"], "0")

    def test_local_license_accepts_documented_and_flat_envelopes(self):
        row = {"OPN_ATMY_GRP_CD": "TEST", "MNG_NO": "A1", "PRMN_LSFBIZ_SPECS": "한우",
               "SALS_STTS_CD": "01", "DAT_UPDT_PNT": "20261119000000",
               "TELNO": "private", "BPLC_NM": "private", "ROAD_NM_ADDR": "private",
               "CRD_INFO_X": "200000", "CRD_INFO_Y": "450000"}
        expected = {key: row[key] for key in (
            "OPN_ATMY_GRP_CD", "MNG_NO", "PRMN_LSFBIZ_SPECS", "SALS_STTS_CD", "DAT_UPDT_PNT")}
        operation = self.catalog["livestock_farms"]
        for value in (standard([row], 5), {"data": [row], "currentCount": 1, "matchCount": 5}):
            with self.subTest(envelope=next(iter(value))):
                self.assertEqual(transport.decode_page(operation, payload(value)),
                                 ([(("TEST", "A1"), expected)], 1, 5))

    def test_traffic_page_count_is_distinct_from_full_count(self):
        operation = self.catalog["traffic_sites"]
        output = {"resultCode": "00", "count": 1, "PageNo": 0, "spot": [{"spot": "S001", "id5": "1"}]}
        self.assertEqual(transport.decode_page(operation, payload(output)),
                         ([(("S001",), {"spot": "S001", "id5": "1"})], 1, None))
        self.assertEqual(transport.decode_page(operation, payload({"resultCode": "00", "count": 0, "spot": []})),
                         ([], 0, None))

    def test_http_200_gateway_error_is_not_an_empty_snapshot(self):
        xml = b"""<OpenAPI_ServiceResponse><cmmMsgHeader>
          <errMsg>echoed-synthetic-key</errMsg><returnReasonCode>30</returnReasonCode>
          </cmmMsgHeader></OpenAPI_ServiceResponse>"""
        with self.assertRaisesRegex(transport.IntakeError, "^provider_30$") as error:
            transport.decode_page(self.catalog["traffic_sites"], xml, ["echoed-synthetic-key"])
        self.assertTrue(error.exception.blocked)

    def test_vworld_keeps_valid_polygon_and_rejects_invalid_ring(self):
        pnu = "1126010100100010000"
        geometry = {"type": "Polygon", "coordinates": [[[127.08, 37.55], [127.081, 37.55],
                    [127.08, 37.551], [127.08, 37.55]]]}
        feature = {"properties": {"pnu": pnu, "private": "ignore"}, "geometry": geometry}
        response = {"response": {"status": "OK", "record": {"total": "12"},
                                 "result": {"featureCollection": {"features": [feature]}}}}
        rows, count, total = transport.decode_page(self.catalog["parcels_bbox"], payload(response))
        self.assertEqual((count, total), (1, 12))
        self.assertEqual(rows, [((pnu,), {"pnu": pnu, "geometry": geometry})])
        geometry["coordinates"][0][-1][0] = 127.0805
        with self.assertRaisesRegex(transport.IntakeError, "^parcel_ring_not_closed$"):
            transport.decode_page(self.catalog["parcels_bbox"], payload(response))

    def test_tombstone_fields_survive_sanitizing(self):
        row = {"bizesId": "TEST", "chgGb": "D", "chgDt": "20261119", "bizesNm": "private"}
        decoded, _, _ = transport.decode_page(self.catalog["shops_by_date"], payload(standard([row], 1)))
        self.assertEqual(decoded, [(("TEST",), {"bizesId": "TEST", "chgGb": "D", "chgDt": "20261119"})])

    def test_secret_echo_in_retained_field_fails_closed(self):
        with self.assertRaisesRegex(transport.IntakeError, "^credential_found_in_response$") as error:
            transport.decode_page(self.catalog["bus_cities"],
                                  payload(standard([{"citycode": "11", "cityname": "synthetic%2Bkey"}], 1)),
                                  ["synthetic+key"])
        self.assertTrue(error.exception.blocked)

    def test_parameter_case_request_coordinate_and_bounds(self):
        region = self.catalog["shops_by_region"]
        market = self.catalog["market_areas"]
        self.assertEqual((region["keyParam"], market["keyParam"]), ("ServiceKey", "servicekey"))
        self.assertEqual(transport.parse_parameters(region, ["divId=adongCd", "key=1126057000"])["key"],
                         "1126057000")
        with self.assertRaisesRegex(transport.IntakeError, "^request_parameter_invalid$"):
            transport.parse_parameters(region, ["divId=adongCd", "key=1126057000", "ServiceKey=synthetic"])
        with self.assertRaisesRegex(transport.IntakeError, "^request_region_code_invalid$"):
            transport.parse_parameters(region, ["divId=adongCd", "key=11260"])
        with self.assertRaisesRegex(transport.IntakeError, "^request_parcel_bbox_too_large$"):
            transport.parse_parameters(self.catalog["parcels_bbox"], ["geomFilter=BOX(127,37,128,38)"])
        with self.assertRaisesRegex(transport.IntakeError, "^request_update_date_invalid$"):
            transport.parse_parameters(self.catalog["shops_by_date"], ["key=20261131"])

    def test_credentials_fail_when_absent_or_preencoded(self):
        with TemporaryDirectory() as directory, patch.dict(os.environ, {}, clear=True), \
                patch.object(transport.Path, "home", return_value=Path(directory)):
            with self.assertRaisesRegex(transport.IntakeError, "^credential_missing_or_invalid$") as error:
                transport.credentials(self.catalog["bus_cities"])
            self.assertTrue(error.exception.blocked)
            with patch.dict(os.environ, {"DATA_GO_KR_KEY": "synthetic%2Bencoded"}):
                with self.assertRaisesRegex(transport.IntakeError, "^use_decoding_key$"):
                    transport.credentials(self.catalog["bus_cities"])
            with patch.dict(os.environ, {"VWORLD_KEY": "synthetic-key"}):
                with self.assertRaisesRegex(transport.IntakeError, "^vworld_domain_missing$"):
                    transport.credentials(self.catalog["parcels_bbox"])

    def test_vworld_app_key_allows_unregistered_domain(self):
        with TemporaryDirectory() as directory, patch.dict(os.environ, {"VWORLD_KEY_TYPE": "APP"}, clear=True), \
                patch.object(transport.Path, "home", return_value=Path(directory)):
            secrets = Path(directory) / "secrets"
            secrets.mkdir()
            (secrets / "vworld_key.txt").write_text("fixture-app-key", encoding="utf-8")
            operation = self.catalog["parcels_bbox"]
            self.assertEqual(transport.credentials(operation), {"key": "fixture-app-key"})
            with patch.dict(os.environ, {"VWORLD_DOMAIN": "https://example.org"}):
                self.assertEqual(transport.credentials(operation),
                                 {"key": "fixture-app-key", "domain": "https://example.org"})

    def test_credentials_can_reuse_existing_government_configuration(self):
        with TemporaryDirectory() as directory, patch.dict(os.environ, {}, clear=True), \
                patch.object(transport.Path, "home", return_value=Path(directory)), \
                patch.object(transport, "existing_government_key", return_value="fixture-government-key") as reuse:
            self.assertEqual(transport.credentials(self.catalog["bus_cities"]),
                             {"serviceKey": "fixture-government-key"})
            with patch.dict(os.environ, {"DATA_GO_KR_KEY": "fixture-explicit-key"}):
                self.assertEqual(transport.credentials(self.catalog["bus_cities"]),
                                 {"serviceKey": "fixture-explicit-key"})
            with self.assertRaises(transport.IntakeError):
                transport.credentials(self.catalog["parcels_bbox"])
            reuse.assert_called_once_with()

    def test_request_once_encodes_key_and_sanitizes_http_error(self):
        operation = self.catalog["shops_by_region"]
        query = {"divId": "adongCd", "key": "1126057000", "type": "json"}
        secret = {"ServiceKey": "synthetic+key/value="}
        provider = Mock()
        provider.open.return_value = io.BytesIO(b"{}")
        with patch.object(transport, "opener", return_value=provider):
            self.assertEqual(transport.request_page(operation, query, secret), b"{}")
            request = provider.open.call_args.args[0]
            self.assertEqual(parse_qs(urlsplit(request.full_url).query)["ServiceKey"], ["synthetic+key/value="])
            provider.open.side_effect = HTTPError("https://apis.data.go.kr/?ServiceKey=synthetic%2Bkey",
                                                 403, "synthetic secret text", None, io.BytesIO())
            with self.assertRaisesRegex(transport.IntakeError, "^http_403$") as error:
                transport.request_page(operation, query, secret)
            self.assertTrue(error.exception.blocked)


if __name__ == "__main__":
    unittest.main()
