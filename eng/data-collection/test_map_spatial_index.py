from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest

from map_public_api import collect, open_database, register_sources
from map_public_api_transport import load_catalog
from map_spatial_index import build, digest, geometry_bounds


class MapSpatialIndexTests(unittest.TestCase):
    def test_region_bbox_and_closed_geometry(self):
        polygon = {"type": "MultiPolygon", "coordinates": [
            [[[127, 37], [127.1, 37], [127.1, 37.1], [127, 37]]]
        ]}
        self.assertEqual(geometry_bounds(polygon), (127, 127.1, 37, 37.1))
        polygon["coordinates"][0][0][3] = [127, 37.1]
        with self.assertRaisesRegex(ValueError, "open_polygon"):
            geometry_bounds(polygon)

    def test_snapshot_indexes_places_and_periods_without_inventing_coordinates(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.sqlite"
            operations = load_catalog()
            with closing(open_database(source, initialize=True)) as database:
                register_sources(database, operations)
                with database:
                    database.execute("INSERT INTO region_release VALUES (1,'fixture','https://example.test','2026-10-30',?)",
                                     ("a" * 64,))
                    database.executemany("INSERT INTO region VALUES (1,?,?,?,?,?)", [
                        ("sido", "11", "서울특별시", None, None),
                        ("sigungu", "11260", "중랑구", "sido", "11"),
                        ("administrative", "1126057500", "면목제3.8동", "sigungu", "11260"),
                    ])
                fixtures = [
                    ("shops_by_region", {"divId": "adongCd", "key": "11260575"},
                     {"bizesId": "SHOP1", "adongCd": "11260575", "adongNm": "면목3.8동",
                      "lon": 127.09, "lat": 37.58, "flrNo": "1"}),
                    ("trade_sido", {"strtYymm": "202610", "endYymm": "202610", "sidoCd": "11"},
                     {"priodTitle": "총계", "expUsdAmt": "123.50"}),
                    ("traffic_sites", {"year": "2025", "dtype": "2"},
                     {"spot": "A01", "xcode": "37.1", "ycode": "127.1"}),
                ]
                for name, params, row in fixtures:
                    operation = operations[name]
                    envelope = {"spot": [row], "resultCode": "00"} if name == "traffic_sites" else {
                        "response": {"header": {"resultCode": "00"}, "body": {"items": [row], "totalCount": 1}}}
                    raw = json.dumps(envelope, ensure_ascii=False).encode()
                    result = collect(database, operation, params, 1, 20, fetch=lambda *_: raw,
                                     secret_provider=lambda op: {op["keyParam"]: "fixture-credential"})
                    self.assertEqual(result["rows"], 1)
            original = digest(source)
            shops = root / "shops.jsonl"
            shops.write_text('{"shopRecordId":1,"administrativeCode":"1126057500","buildingId":"fixture-1",'
                             '"evidenceKind":"Fixture"}\n', encoding="utf-8")
            args = SimpleNamespace(db=source, output=root/"index.sqlite", shop_pairs=shops, boundaries=None,
                                   references=None, boundary_receipt=None, reference_receipt=None, proj_library=None)
            result = build(args)
            self.assertEqual(result["totals"]["map_entry"], 3)
            self.assertEqual(result["totals"]["entry_geometry"], 1)
            self.assertEqual(result["totals"]["shop_building_candidate"], 1)
            self.assertEqual(digest(source), original)
            with closing(sqlite3.connect(args.output)) as index:
                self.assertEqual(index.execute("SELECT COUNT(*) FROM entry_bounds").fetchone()[0], 1)
                self.assertEqual(index.execute("SELECT placement FROM map_entry WHERE layer_id='traffic_sites'").fetchone()[0],
                                 "unlocated")
                self.assertEqual(index.execute("SELECT value_decimal FROM map_metric WHERE period_start='202610'").fetchone()[0],
                                 "123.50")
                self.assertEqual(index.execute("SELECT region_code FROM entry_region WHERE record_id=2").fetchone()[0], "11")
            with self.assertRaisesRegex(ValueError, "existing_index"):
                build(args)


if __name__ == "__main__":
    unittest.main()
