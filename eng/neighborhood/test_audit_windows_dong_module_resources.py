"""Windows 행정동 자원 감사기의 독립 합성 입력 회귀 검사.

실제 Unity 자원이나 원본 모듈은 읽거나 수정하지 않는다.
"""

from __future__ import annotations

import copy
from contextlib import redirect_stderr
import hashlib
import importlib.util
import io
import json
import math
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock


_MODULE_PATH = Path(__file__).with_name("audit_windows_dong_module_resources.py")
_SPEC = importlib.util.spec_from_file_location("audit_windows_dong_module_resources", _MODULE_PATH)
assert _SPEC is not None and _SPEC.loader is not None
AUDITOR = importlib.util.module_from_spec(_SPEC)
sys.modules[_SPEC.name] = AUDITOR
_SPEC.loader.exec_module(AUDITOR)


REGION = "region:kr:hjd:1126056500"
CODE = "1126056500"
MESH_RESOURCE = "hongdal-dong-" + CODE
INDEX_RESOURCE = "hongdal-dong-buildings"
ORIGIN_LATITUDE = 37.580912
ORIGIN_LONGITUDE = 127.088502
LATITUDE_SCALE = 111_200.0
LONGITUDE_SCALE = LATITUDE_SCALE * math.cos(math.radians(ORIGIN_LATITUDE))
BOUNDS = (127.08, 37.58, 127.09, 37.59)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _json_bytes(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def _mesh_bytes(*, latitude: float = ORIGIN_LATITUDE, indices: tuple[int, ...] = (0, 1, 2)) -> bytes:
    """HDIO v2: 본체·그림자·채움 수·빈 채움 본체/그림자를 모두 기록한다."""
    positions = ((0.0, 0.0, 0.0), (10.0, 0.0, 0.0), (0.0, 0.0, 10.0))
    coordinates = tuple(value for point in positions for value in point)
    uv = (0.0, 0.0, 1.0, 0.0, 0.0, 1.0)
    main = (
        struct.pack("<i", 3)
        + struct.pack("<9f", *coordinates)
        + bytes((128, 128, 128, 255)) * 3
        + struct.pack("<6f", *uv)
        + struct.pack("<6f", *uv)
        + struct.pack("<i", len(indices))
        + struct.pack("<" + "i" * len(indices), *indices)
    )
    shadow = (
        struct.pack("<i", 3)
        + struct.pack("<9f", *coordinates)
        + struct.pack("<i3i", 3, 0, 1, 2)
    )
    return (
        b"HDIO"
        + struct.pack("<i", 2)
        + struct.pack("<6d", latitude, ORIGIN_LONGITUDE, *BOUNDS)
        + struct.pack("<2i", 1, 1)
        + main
        + shadow
        + struct.pack("<i", 0)  # 예시 채움 건물 수
        + struct.pack("<2i", 0, 0)  # 빈 채움 메시
        + struct.pack("<2i", 0, 0)  # 빈 채움 그림자
    )


class WindowsDongModuleResourceAuditTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.resources = Path(self.temporary.name) / "Resources"
        self.resources.mkdir()
        self.manifest_path = self.resources / "hongdal-dong-modules.json"
        self.index_path = self.resources / (INDEX_RESOURCE + ".json")
        self.mesh_path = self.resources / (MESH_RESOURCE + ".bytes")
        mesh = _mesh_bytes()
        self.mesh_path.write_bytes(mesh)
        self.index = {
            "schema": "hongdal-building-index.v1",
            "source": "synthetic-test-only",
            "sourceDate": "2023-10-31",
            "sourceSha256": "b" * 64,
            "origin": {"latitude": ORIGIN_LATITUDE, "longitude": ORIGIN_LONGITUDE},
            "metresPerDegreeLatitude": LATITUDE_SCALE,
            "metresPerDegreeLongitude": LONGITUDE_SCALE,
            "buildings": [
                {
                    "id": "building:test:1",
                    "region": REGION,
                    "xz": [0.0, 0.0, 10.0, 0.0, 0.0, 10.0],
                    "height": 4.0,
                    "heightSource": "height",
                    "fill": False,
                }
            ],
        }
        self.index_path.write_bytes(_json_bytes(self.index))
        self.manifest = {
            "schema": "hongdal-dong-modules.v1",
            "scopeRevision": "synthetic-scope.r1",
            "scopeStableId": "scope:synthetic:unit-test",
            "scopeContentHash": "b" * 64,
            "sourceVintage": "synthetic-test-only",
            "distributionApproved": False,
            "traversalReady": False,
            "gameplayReady": False,
            "originLatitude": ORIGIN_LATITUDE,
            "originLongitude": ORIGIN_LONGITUDE,
            "metresPerDegreeLatitude": LATITUDE_SCALE,
            "metresPerDegreeLongitude": LONGITUDE_SCALE,
            "west": BOUNDS[0],
            "south": BOUNDS[1],
            "east": BOUNDS[2],
            "north": BOUNDS[3],
            "buildingIndexResource": INDEX_RESOURCE,
            "buildingIndexSha256": _sha256(self.index_path.read_bytes()),
            "modules": [
                {
                    "regionId": REGION,
                    "code": CODE,
                    "name": "합성 검사 동",
                    "resource": MESH_RESOURCE,
                    "bytes": len(mesh),
                    "sha256": _sha256(mesh),
                    "moduleSha256": "a" * 64,
                    "moduleContentHash": "c" * 64,
                    "buildings": 1,
                    "roads": 1,
                    "heightFromSource": 1,
                    "heightFromFloors": 0,
                    "heightSymbolic": 0,
                    "west": BOUNDS[0],
                    "south": BOUNDS[1],
                    "east": BOUNDS[2],
                    "north": BOUNDS[3],
                    "vertices": 3,
                    "triangles": 1,
                }
            ],
            "roleMap": [{"from": "role:test:old", "to": "building:test:1"}],
        }
        self.write_manifest()

    def write_manifest(self) -> None:
        self.manifest_path.write_bytes(_json_bytes(self.manifest))

    def write_index(self, *, update_hash: bool = True) -> None:
        self.index_path.write_bytes(_json_bytes(self.index))
        if update_hash:
            self.manifest["buildingIndexSha256"] = _sha256(self.index_path.read_bytes())
            self.write_manifest()

    def write_mesh(self, data: bytes) -> None:
        self.mesh_path.write_bytes(data)
        self.manifest["modules"][0]["bytes"] = len(data)
        self.manifest["modules"][0]["sha256"] = _sha256(data)
        self.write_manifest()

    def assert_issue(self, code: str) -> dict:
        report = AUDITOR.audit(self.resources)
        self.assertEqual("Failed", report["status"], report)
        self.assertIn(code, {issue["code"] for issue in report["issues"]}, report)
        return report

    def test_valid_full_hdio_fixture_passes(self) -> None:
        report = AUDITOR.audit(self.resources)
        self.assertEqual("Passed", report["status"], report)
        self.assertEqual([], report["issues"])
        self.assertEqual(1, report["summary"]["buildings"])
        self.assertEqual(1, report["summary"]["moduleCount"])
        self.assertEqual(0, report["summary"]["symbolicHeightBuildings"])
        self.assertTrue(report["inputs"])

    def test_tampered_index_hash_is_rejected(self) -> None:
        self.index["buildings"][0]["height"] = 9.0
        self.write_index(update_hash=False)
        self.assert_issue("BuildingIndexHashMismatch")

    def test_index_origin_mismatch_is_rejected_with_valid_hash(self) -> None:
        self.index["origin"]["latitude"] += 0.01
        self.write_index()
        self.assert_issue("BuildingIndexOriginMismatch")

    def test_huge_manifest_origin_is_reported_without_overflow(self) -> None:
        self.manifest["originLatitude"] = 10**400
        self.write_manifest()
        self.assert_issue("ManifestInvalid")

    def test_huge_index_origin_is_reported_without_overflow(self) -> None:
        self.index["origin"]["latitude"] = 10**400
        self.write_index()
        self.assert_issue("BuildingIndexOriginMismatch")

    def test_index_scale_mismatch_is_rejected_with_valid_hash(self) -> None:
        self.index["metresPerDegreeLatitude"] *= 2.0
        self.write_index()
        self.assert_issue("BuildingIndexScaleMismatch")

    def test_duplicate_building_id_is_rejected(self) -> None:
        self.index["buildings"].append(copy.deepcopy(self.index["buildings"][0]))
        self.write_index()
        self.assert_issue("DuplicateBuildingId")

    def test_building_from_unknown_region_is_rejected(self) -> None:
        self.index["buildings"][0]["region"] = "region:kr:hjd:1126052000"
        self.write_index()
        self.assert_issue("BuildingRegionUnknown")

    def test_per_module_building_count_mismatch_is_rejected(self) -> None:
        second = copy.deepcopy(self.index["buildings"][0])
        second["id"] = "building:test:2"
        self.index["buildings"].append(second)
        self.write_index()
        self.assert_issue("BuildingCountMismatch")

    def test_missing_index_is_reported(self) -> None:
        self.index_path.unlink()
        self.assert_issue("BuildingIndexMissing")

    def test_wrong_index_schema_is_rejected_with_valid_hash(self) -> None:
        self.index["schema"] = "hongdal-building-index.v999"
        self.write_index()
        self.assert_issue("BuildingIndexInvalid")

    def test_malformed_manifest_is_reported(self) -> None:
        self.manifest_path.write_bytes(b"{not valid JSON")
        self.assert_issue("ManifestInvalid")

    def test_index_parent_path_is_rejected(self) -> None:
        self.manifest["buildingIndexResource"] = "../outside"
        self.write_manifest()
        self.assert_issue("UnsafeResourcePath")

    def test_module_parent_path_is_rejected(self) -> None:
        self.manifest["modules"][0]["resource"] = "../outside"
        self.write_manifest()
        self.assert_issue("UnsafeResourcePath")

    def test_absolute_resource_path_is_rejected(self) -> None:
        self.manifest["buildingIndexResource"] = str(self.resources.parent / "outside")
        self.write_manifest()
        self.assert_issue("UnsafeResourcePath")

    def test_mesh_origin_mismatch_is_rejected_with_valid_hash(self) -> None:
        self.write_mesh(_mesh_bytes(latitude=ORIGIN_LATITUDE + 0.01))
        self.assert_issue("ModuleHeaderMismatch")

    def test_tampered_module_hash_is_rejected(self) -> None:
        data = bytearray(self.mesh_path.read_bytes())
        # 헤더 64 + 정점 수 4 + 위치 36 뒤 첫 색만 바꿔 구조는 보존한다.
        data[104] ^= 1
        self.mesh_path.write_bytes(data)
        self.assert_issue("ModuleHashMismatch")

    def test_missing_module_hash_is_reported(self) -> None:
        del self.manifest["modules"][0]["sha256"]
        self.write_manifest()
        self.assert_issue("ManifestInvalid")

    def test_missing_module_resource_is_reported(self) -> None:
        self.mesh_path.unlink()
        self.assert_issue("ResourceMissing")

    def test_mesh_out_of_range_triangle_index_is_rejected(self) -> None:
        self.write_mesh(_mesh_bytes(indices=(0, 1, 3)))
        self.assert_issue("MeshInvalid")

    def test_oversized_vertex_count_is_rejected_without_allocation(self) -> None:
        data = bytearray(_mesh_bytes())
        # HDIO 4 + version 4 + six doubles 48 + buildings/roads 8.
        struct.pack_into("<i", data, 64, 4_000_001)
        self.write_mesh(bytes(data))
        self.assert_issue("MeshInvalid")

    def test_changed_input_is_reported_instead_of_passed(self) -> None:
        original_changed = AUDITOR.Snapshot.changed
        changed_once = False

        def mutate_then_check(snapshot: object) -> list[str]:
            nonlocal changed_once
            if not changed_once:
                changed_once = True
                self.index_path.write_bytes(self.index_path.read_bytes() + b" ")
            return original_changed(snapshot)

        with mock.patch.object(AUDITOR.Snapshot, "changed", new=mutate_then_check):
            report = AUDITOR.audit(self.resources)
        self.assertEqual("InputChangedDuringAudit", report["status"], report)
        self.assertIn("InputChangedDuringAudit", {issue["code"] for issue in report["issues"]}, report)

    def test_cli_rejects_report_in_resources_without_writing(self) -> None:
        def contents() -> dict[str, bytes | None]:
            return {
                str(path.relative_to(self.resources)): path.read_bytes() if path.is_file() else None
                for path in self.resources.rglob("*")
            }

        before = contents()
        for report_path in (self.index_path, self.resources / "new-directory" / "audit.json"):
            with self.subTest(report_path=report_path):
                arguments = [str(_MODULE_PATH), "--resources", str(self.resources), "--report", str(report_path)]
                with (mock.patch.object(sys, "argv", arguments),
                      mock.patch.object(AUDITOR, "audit") as audit_call,
                      redirect_stderr(io.StringIO()),
                      self.assertRaises(SystemExit) as raised):
                    AUDITOR.main()
                self.assertEqual(2, raised.exception.code)
                audit_call.assert_not_called()
                self.assertEqual(before, contents())


if __name__ == "__main__":
    unittest.main()
