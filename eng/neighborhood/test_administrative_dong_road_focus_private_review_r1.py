"""작은 합성 사본만 쓰는 공간 선택 회귀. 실제 30개 동 생성기를 호출하지 않는다."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import administrative_dong_road_focus_private_review_r1 as focus


class RoadFocusTests(unittest.TestCase):
    def setUp(self):
        parent = focus.safe(focus.ROOT, focus.OUTPUT / "synthetic-tests")
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="fixture-", dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        # 합성 저장 경로는 이미 허용 산출물 안이다. Windows 경로 길이 중첩을 피한다.
        output_patch = patch.object(focus, "OUTPUT", Path("output"))
        output_patch.start()
        self.addCleanup(output_patch.stop)
        self.base = self.root / "input"
        (self.base / "bundles").mkdir(parents=True)
        self.bundle = self.make_bundle()
        self.pin, self.entry = self.persist()

    @staticmethod
    def make_bundle():
        def p(x, z):
            return {"x": x, "z": z}
        building = {"buildingStableId": focus.BUILDING, "evidenceKindCode": "FixtureLargestExteriorOnly",
                    "footprint": [p(-2, -2), p(2, -2), p(2, 2), p(-2, 2)]}
        road = {"roadStableId": "fixture:road:part:0", "from": p(-10, 0), "to": p(40, 0),
                "evidenceKindCode": "SyntheticFixtureNotRealPlace"}
        tiles, summaries = [], []
        for x in (0, 1):
            tile_id = f"tile:{focus.AREA}:500m:x{x}:z0"
            tiles.append({"schemaVersion": "administrative-dong-diorama.v1", "administrativeAreaStableId": focus.AREA,
                          "projectionHashSha256": "A" * 64, "tileStableId": tile_id, "tileHashSha256": str(x + 1) * 64,
                          "tileIndexX": x, "tileIndexZ": 0,
                          "bounds": {"minX": x * 500, "maxX": (x + 1) * 500, "minZ": 0, "maxZ": 500},
                          "buildings": [building] if x == 0 else [], "roads": [road] if x == 0 else []})
            summaries.append({"tileStableId": tile_id, "tileHashSha256": str(x + 1) * 64,
                              "tileIndexX": x, "tileIndexZ": 0, "buildingCount": int(x == 0),
                              "roadSegmentCount": int(x == 0)})
        authority = {"observationPresentationOnly": True, "distributionApproved": False,
                     "traversalReady": False, "gameplayReady": False}
        manifest = {"schemaVersion": "administrative-dong-diorama.v1", "administrativeAreaStableId": focus.AREA,
                    "dataPolicyCode": "ObservationPresentationOnly", "projectionHashSha256": "A" * 64,
                    "coordinateFrame": copy.deepcopy(focus.FRAME), "tiles": summaries,
                    "sources": [{"sourceId": "SyntheticFixture", "licenseCode": "NotRealPlaceEvidence"}], **authority}
        return {"schemaVersion": "administrative-dong-diorama-unity-review-bundle.v7",
                "administrativeAreaStableId": focus.AREA, "projectionHashSha256": "A" * 64,
                "publishBlocked": True, "currentPointerUsed": False, "currentPointerUpdated": False,
                "manifest": manifest, "tiles": tiles, "buildingCount": 1, "roadSegmentCount": 1, "tileCount": 2,
                "displayOverlays": {"items": []}, "missingLayerCodes": ["BuildingEntrance", "RoadWidth"],
                "sourceVintage": "SyntheticFixture", **authority}

    def persist(self):
        self.bundle["contentHashSha256"] = focus.content_hash(self.bundle)
        payload = focus.encoded(self.bundle)
        relative = f"bundles/{focus.AREA.split(':')[-1]}.json"
        (self.base / relative).write_bytes(payload)
        entry = {"administrativeAreaStableId": focus.AREA, "relativePath": relative,
                 "sha256": focus.sha(payload), "byteLength": len(payload),
                 "contentHashSha256": self.bundle["contentHashSha256"], "projectionHashSha256": "A" * 64,
                 "buildingCount": 1, "roadSegmentCount": 1, "tileCount": 2}
        # 다른 동은 목록에만 있다. 해당 파일을 열면 이 시험이 실패한다.
        other = {"administrativeAreaStableId": "region:kr:hjd:1126057000", "relativePath": "bundles/1126057000.json"}
        index = {"schemaVersion": "administrative-dong-diorama-unity-review-index.v7",
                 "generationHashSha256": "B" * 64, "bundles": [entry, other],
                 "observationPresentationOnly": True, "distributionApproved": False,
                 "traversalReady": False, "gameplayReady": False}
        index["contentHashSha256"] = focus.content_hash(index)
        index_bytes = focus.encoded(index)
        (self.base / "index.json").write_bytes(index_bytes)
        complete = {"schemaVersion": "administrative-dong-diorama-unity-review-completion.v7",
                    "generationHashSha256": "B" * 64, "fileCount": 3,
                    "files": [{"relativePath": relative, "sha256": entry["sha256"], "byteLength": len(payload)},
                              {"relativePath": "index.json", "sha256": focus.sha(index_bytes), "byteLength": len(index_bytes)}]}
        complete["contentHashSha256"] = focus.content_hash(complete)
        complete_bytes = focus.encoded(complete)
        (self.base / "complete.json").write_bytes(complete_bytes)
        return focus.SourcePin(Path("input"), "B" * 64, focus.sha(index_bytes), focus.sha(complete_bytes)), entry

    def selected(self, **kwargs):
        bundle, buildings, roads, binding = focus.load_selected(self.root, pin=self.pin)
        return focus.select_focus(bundle, buildings, roads, binding, **kwargs)

    def rejects_bundle(self, code):
        with self.assertRaisesRegex(focus.RoadFocusError, code):
            focus.validate_bundle(self.bundle, self.entry)

    def test_only_selected_complete_bundle_is_read(self):
        value = self.selected(width=25)
        self.assertEqual(value["sourceBinding"]["otherDongBundlesRead"], 0)
        self.assertEqual(value["sourceBinding"]["sourceFilesRead"], 3)
        self.assertEqual(value["counts"]["sourceTiles"], 2)

    def test_road_intersection_not_midpoint(self):
        value = self.selected(width=25)
        self.assertEqual(value["counts"]["selectedRoads"], 1)
        self.assertTrue(focus.segment_intersects_box((-100, 0), (1, 0), (-2, -2, 2, 2)))

    def test_segment_boundary_touch_and_disjoint(self):
        box = (-2, -2, 2, 2)
        self.assertTrue(focus.segment_intersects_box((-4, 2), (4, 2), box))
        self.assertFalse(focus.segment_intersects_box((-4, 3), (4, 3), box))
        self.assertFalse(focus.segment_intersects_box((3, 3), (4, 4), box))

    def test_polygon_contains_entire_box(self):
        polygon = [(-10, -10), (10, -10), (10, 10), (-10, 10)]
        self.assertTrue(focus.polygon_intersects_box(polygon, (-2, -2, 2, 2)))

    def test_polygon_inside_box_and_disjoint(self):
        polygon = [(-1, -1), (1, -1), (0, 1)]
        self.assertTrue(focus.polygon_intersects_box(polygon, (-2, -2, 2, 2)))
        self.assertFalse(focus.polygon_intersects_box(polygon, (5, 5, 8, 8)))

    def test_polygon_edges_cross_without_inside_vertices(self):
        self.assertTrue(focus.polygon_intersects_box([(-10, -1), (10, -1), (10, 1), (-10, 1)], (-2, -2, 2, 2)))

    def test_missing_anchor_id_rejected(self):
        with self.assertRaisesRegex(focus.RoadFocusError, "AnchorBuildingNotFound"):
            self.selected(building_id="missing")

    def test_missing_area_rejected(self):
        with self.assertRaisesRegex(focus.RoadFocusError, "SelectedAreaNotFound"):
            focus.load_selected(self.root, "region:kr:hjd:9999999999", self.pin)

    def test_complete_tile_set_required(self):
        self.bundle["tiles"].pop()
        self.rejects_bundle("CompleteTileSetRequired")

    def test_origin_mismatch_rejected(self):
        self.bundle["manifest"]["coordinateFrame"]["originLatitude"] = 0
        self.rejects_bundle("CoordinateFrameMismatch")

    def test_world_offset_and_unit_mismatch_rejected(self):
        for field in ("worldOffsetX", "metersPerUnit"):
            with self.subTest(field=field):
                self.bundle = self.make_bundle()
                self.bundle["manifest"]["coordinateFrame"][field] = 2
                self.rejects_bundle("CoordinateFrameMismatch")

    def test_bundle_byte_tamper_rejected(self):
        path = self.base / self.entry["relativePath"]
        path.write_bytes(path.read_bytes() + b" ")
        with self.assertRaisesRegex(focus.RoadFocusError, "InputLengthMismatch"):
            focus.load_selected(self.root, pin=self.pin)

    def test_index_hash_tamper_rejected(self):
        path = self.base / "index.json"
        path.write_bytes(path.read_bytes().replace(b'"bundle', b'"Bundle', 1))
        with self.assertRaisesRegex(focus.RoadFocusError, "InputHashMismatch"):
            focus.load_selected(self.root, pin=self.pin)

    def test_tile_hash_mismatch_rejected(self):
        self.bundle["tiles"][0]["tileHashSha256"] = "F" * 64
        self.rejects_bundle("TileVersionMismatch")

    def test_duplicate_tile_rejected(self):
        self.bundle["tiles"].append(copy.deepcopy(self.bundle["tiles"][0]))
        self.rejects_bundle("TileSetInvalid")

    def test_wrong_tile_index_rejected(self):
        self.bundle["tiles"][0]["tileIndexX"] = 100
        self.rejects_bundle("TileGridMismatch")

    def test_authority_escalation_rejected(self):
        self.bundle["traversalReady"] = True
        self.rejects_bundle("AuthorityFlagInvalid")

    def test_nonfinite_and_duplicate_json_rejected(self):
        for payload in (b'{"x":NaN}', b'{"x":1,"x":2}'):
            with self.subTest(payload=payload), self.assertRaises(focus.RoadFocusError):
                focus.parse(payload)

    def test_generated_hash_survives_decimal_roundtrip_at_zero(self):
        value = self.selected()
        loaded = focus.parse(focus.encoded(value))
        self.assertEqual(value["contentHashSha256"], focus.content_hash(loaded))
        self.assertEqual(focus.canonical({"zero": -0.0, "small": 1e-8}),
                         '{"small":0.00000001,"zero":0}')

    def test_nonfinite_source_coordinate_rejected(self):
        self.bundle["tiles"][0]["roads"][0]["from"]["x"] = float("inf")
        self.rejects_bundle("CoordinateInvalid")

    def test_width_is_bounded(self):
        for value in (float("nan"), 0, 501, True):
            with self.subTest(value=value), self.assertRaises(focus.RoadFocusError):
                self.selected(width=value)

    def test_deterministic_nochange_and_source_unchanged(self):
        paths = [self.base / "index.json", self.base / "complete.json", self.base / self.entry["relativePath"]]
        before = [p.read_bytes() for p in paths]
        first, second = self.selected(), self.selected()
        self.assertEqual(focus.encoded(first), focus.encoded(second))
        self.assertEqual(focus.materialize(self.root, first, "build")["changedFiles"], 2)
        self.assertEqual(focus.materialize(self.root, second, "build")["changedFiles"], 0)
        self.assertEqual(focus.materialize(self.root, second, "verify")["changedFiles"], 0)
        self.assertEqual(before, [p.read_bytes() for p in paths])
        self.assertNotIn("tiles", first)
        self.assertFalse(first["authority"]["completeTilePayloadReplaced"])

    def test_output_tamper_is_not_overwritten(self):
        value = self.selected()
        result = focus.materialize(self.root, value, "build")
        path = self.root / result["generationRelativePath"] / "focus.json"
        path.write_bytes(b"changed")
        for mode in ("build", "verify"):
            with self.subTest(mode=mode), self.assertRaisesRegex(focus.RoadFocusError, "GenerationContentMismatch"):
                focus.materialize(self.root, value, mode)
        self.assertEqual(path.read_bytes(), b"changed")

    def test_path_escape_rejected(self):
        with self.assertRaisesRegex(focus.RoadFocusError, "PathEscapesRoot"):
            focus.safe(self.root, Path("../other"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
