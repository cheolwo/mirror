from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from sagajeong_mobility_graph import (
    MobilityGraphAuditError,
    MobilityGraphBlocked,
    audit_package,
    build_package,
    canonical_json_bytes,
    finalize_content_hash,
    sha256_bytes,
    write_package,
)


SYNTHETIC_OSM = """<?xml version="1.0" encoding="UTF-8"?>
<osm version="0.6" generator="ssalddel-test">
  <bounds minlat="37.579" minlon="127.087" maxlat="37.582" maxlon="127.090"/>
  <node id="1" lat="37.5806971" lon="127.0875000"/>
  <node id="6046633864" lat="37.5806971" lon="127.0884106">
    <tag k="highway" v="crossing"/>
    <tag k="crossing" v="uncontrolled"/>
  </node>
  <node id="2" lat="37.5806971" lon="127.0893000"/>
  <node id="10" lat="37.5809000" lon="127.0886000"/>
  <node id="11" lat="37.5809000" lon="127.0887000"/>
  <node id="12" lat="37.5810000" lon="127.0887000"/>
  <node id="13" lat="37.5810000" lon="127.0886000"/>
  <node id="20" lat="37.5803000" lon="127.0886000"/>
  <node id="21" lat="37.5803000" lon="127.0887000"/>
  <node id="22" lat="37.5804000" lon="127.0887000"/>
  <node id="23" lat="37.5804000" lon="127.0886000"/>
  <node id="24" lat="37.5803000" lon="127.0886500">
    <tag k="entrance" v="yes"/>
  </node>
  <way id="100">
    <nd ref="1"/><nd ref="6046633864"/><nd ref="2"/>
    <tag k="highway" v="residential"/>
    <tag k="name" v="검토길"/>
    <tag k="oneway" v="yes"/>
    <tag k="access" v="destination"/>
  </way>
  <way id="1256772531">
    <nd ref="10"/><nd ref="11"/><nd ref="12"/><nd ref="13"/><nd ref="10"/>
    <tag k="building" v="yes"/>
  </way>
  <way id="1256772606">
    <nd ref="20"/><nd ref="24"/><nd ref="21"/><nd ref="22"/><nd ref="23"/><nd ref="20"/>
    <tag k="building" v="yes"/>
  </way>
</osm>
"""


class SagajeongMobilityGraphTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.source = self.root / "map.osm"
        self.source.write_text(SYNTHETIC_OSM, encoding="utf-8", newline="\n")
        self.source_hash = hashlib.sha256(self.source.read_bytes()).hexdigest().upper()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_same_frozen_source_produces_identical_package(self) -> None:
        first = build_package(self.source, self.source_hash)
        second = build_package(self.source, self.source_hash)
        self.assertEqual(canonical_json_bytes(first), canonical_json_bytes(second))
        self.assertFalse(first["manifest"]["administrativeBoundaryClipped"])
        self.assertEqual("SagajeongOneKilometerWindow", first["manifest"]["coverageCode"])
        self.assertEqual(4, len(first["manifest"]["tiles"]))
        self.assertGreater(len(first["manifest"]["stitches"]), 0)

    def test_osm_identity_direction_access_crossing_and_entrance_are_preserved(self) -> None:
        package = build_package(self.source, self.source_hash)
        serialized = canonical_json_bytes(package)
        self.assertNotIn(b"addr:", serialized)
        self.assertNotIn(str(self.source).encode("utf-8"), serialized)
        edges = [edge for tile in package["tiles"].values() for edge in tile["edges"]]
        road_edges = [edge for edge in edges if edge["sourceOsmWayId"] == "100"]
        self.assertGreater(len(road_edges), 0)
        self.assertTrue(all(edge["directionCode"] == "Forward" for edge in road_edges))
        self.assertTrue(all(edge["sourceOneway"] == "yes" for edge in road_edges))
        self.assertTrue(all(edge["sourceAccess"] == "destination" for edge in road_edges))
        self.assertTrue(all("Motorcycle" in edge["candidateModeCodes"] for edge in road_edges))
        self.assertTrue(all(edge["accessReviewCode"] == "PendingHumanReview" for edge in road_edges))

        nodes = [node for tile in package["tiles"].values() for node in tile["nodes"]]
        crossing = [node for node in nodes if node["sourceOsmNodeId"] == "6046633864"]
        entrance = [node for node in nodes if node["sourceOsmNodeId"] == "24"]
        self.assertTrue(crossing and all("Crossing" in node["roleCodes"] for node in crossing))
        self.assertTrue(entrance and all("Entrance" in node["roleCodes"] for node in entrance))

    def test_vertical_slice_nearest_connectors_remain_unmaterialized_review_candidates(self) -> None:
        manifest = build_package(self.source, self.source_hash)["manifest"]
        candidates = {
            item["sourceBuildingOsmWayId"]: item for item in manifest["verticalSliceCandidates"]
        }
        self.assertEqual("NoTaggedEntrance", candidates["1256772531"]["entranceEvidenceCode"])
        self.assertEqual("TaggedEntrance", candidates["1256772606"]["entranceEvidenceCode"])
        self.assertEqual(["24"], candidates["1256772606"]["taggedEntranceOsmNodeIds"])
        for item in candidates.values():
            self.assertEqual(1, len(item["connectorCandidates"]))
            connector = item["connectorCandidates"][0]
            self.assertEqual("PendingHumanReview", connector["reviewStatusCode"])
            self.assertFalse(connector["generatedAsGraphEdge"])
            self.assertFalse(connector["runtimeAuthorized"])
        edges = [edge for tile in build_package(self.source, self.source_hash)["tiles"].values() for edge in tile["edges"]]
        self.assertTrue(all(edge["edgeKindCode"] == "SourceRoadSegment" for edge in edges))

    def test_missing_source_is_explicitly_blocked_without_network_fallback(self) -> None:
        with self.assertRaises(MobilityGraphBlocked) as error:
            build_package(self.root / "missing.osm", self.source_hash)
        self.assertEqual("MobilityGraphSourceMissing", error.exception.reason_code)
        self.assertFalse(error.exception.report()["networkRequested"])

    def test_audit_rebuilds_from_source_and_rejects_rehashed_mutation(self) -> None:
        package = build_package(self.source, self.source_hash)
        output = self.root / "package"
        write_package(package, output)
        passed = audit_package(self.source, output, self.source_hash)
        self.assertEqual("PassedPendingHumanReview", passed["status"])

        manifest = copy.deepcopy(package["manifest"])
        first_summary = next(summary for summary in manifest["tiles"] if summary["edgeCount"] > 0)
        first_file_name = first_summary["fileName"]
        tile_path = output / first_file_name
        tile = json.loads(tile_path.read_text(encoding="utf-8"))
        tile["edges"][0]["sourceOsmWayId"] = "999"
        finalize_content_hash(tile)
        tile_bytes = canonical_json_bytes(tile)
        tile_path.write_bytes(tile_bytes)
        first_summary["contentHashSha256"] = tile["contentHashSha256"]
        first_summary["fileHashSha256"] = sha256_bytes(tile_bytes)
        finalize_content_hash(manifest)
        (output / "manifest.json").write_bytes(canonical_json_bytes(manifest))

        with self.assertRaisesRegex(MobilityGraphAuditError, "SourceRebuildMismatch"):
            audit_package(self.source, output, self.source_hash)


if __name__ == "__main__":
    unittest.main()
