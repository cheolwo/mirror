"""단일 건물 내부 ring/roof 검토 계약의 실패 차단 시험. 합성 도형만 사용한다."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import administrative_dong_focused_building_ring_review_r1 as focus

for relative in ("artifacts/local/public-data/gis-runtime-r1", "artifacts/local/python-packages/spatial"):
    import sys
    sys.path.insert(0, str(focus.ROOT / relative))


class FocusedBuildingRingTests(unittest.TestCase):
    def setUp(self):
        self.outer = [[0, 0], [0, 12000], [12000, 12000], [12000, 0], [0, 0]]
        self.holes = [[[3000, 3000], [9000, 3000], [9000, 9000], [3000, 9000], [3000, 3000]]]

    def test_hole_is_excluded_from_full_roof_coverage(self):
        vertices, indices, audit = focus.triangulate_roof(self.outer, self.holes)
        self.assertEqual(8, audit["roofTriangleCount"])
        self.assertEqual(108000000, audit["roofAreaSquareMillimeters"])
        self.assertEqual(0, audit["roofUnionSymmetricDifferenceSquareMillimeters"])
        self.assertEqual(0, audit["roofInteriorOverlapSquareMillimeters"])
        self.assertEqual(0, audit["roofDuplicateAreaSquareMillimeters"])
        self.assertEqual(8, len(vertices))
        self.assertEqual(24, len(indices))

    def test_concave_footprint_preserves_empty_gap(self):
        outer = [[0, 0], [0, 12000], [5000, 12000], [5000, 8000], [10000, 8000], [10000, 0], [0, 0]]
        _, _, audit = focus.triangulate_roof(outer, [])
        self.assertEqual(100000000, audit["roofAreaSquareMillimeters"])
        self.assertEqual(0, audit["roofUnionSymmetricDifferenceSquareMillimeters"])

    def test_hole_outside_fails(self):
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "InteriorRingNotStrictlyContained"):
            focus.triangulate_roof(self.outer, [[[11000, 3000], [14000, 3000], [14000, 9000], [11000, 9000], [11000, 3000]]])

    def test_touching_hole_fails(self):
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "InteriorRingNotStrictlyContained"):
            focus.triangulate_roof(self.outer, [[[0, 3000], [3000, 3000], [3000, 9000], [0, 9000], [0, 3000]]])

    def test_missing_triangle_fails_coverage(self):
        vertices, indices, _ = focus.triangulate_roof(self.outer, self.holes)
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "RoofCoverageMismatch"):
            focus.audit_roof(self.outer, self.holes, vertices, indices[3:])

    def test_duplicate_triangle_fails_overlap(self):
        vertices, indices, _ = focus.triangulate_roof(self.outer, self.holes)
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "RoofTriangleOverlap"):
            focus.audit_roof(self.outer, self.holes, vertices, indices + indices[:3])

    def test_triangle_over_hole_fails(self):
        vertices, _, _ = focus.triangulate_roof(self.outer, self.holes)
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "RoofTriangleOutsideSourcePolygon"):
            focus.audit_roof(self.outer, self.holes, vertices, [4, 6, 5])

    def test_moved_vertex_fails_exact_binding(self):
        vertices, indices, _ = focus.triangulate_roof(self.outer, self.holes)
        vertices = copy.deepcopy(vertices)
        vertices[0][0] = 1
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "RoofVertexBindingMismatch"):
            focus.audit_roof(self.outer, self.holes, vertices, indices)

    def test_reversed_winding_fails(self):
        vertices, indices, _ = focus.triangulate_roof(self.outer, self.holes)
        indices[:3] = reversed(indices[:3])
        with self.assertRaisesRegex(focus.FocusedBuildingRingError, "RoofTriangleWindingInvalid"):
            focus.audit_roof(self.outer, self.holes, vertices, indices)

    def test_duplicate_json_and_nonfinite_json_fail(self):
        for payload in (b'{"x":1,"x":2}', b'{"x":NaN}'):
            with self.assertRaises(focus.FocusedBuildingRingError):
                focus.parse(payload)

    def test_immutable_generation_rerun_is_noop_and_tampering_fails(self):
        candidate = {"sourceRecordOrdinal": 1, "geometryAudit": {}, "contentHashSha256": "A" * 64}
        with tempfile.TemporaryDirectory() as directory, patch.object(focus, "OUTPUT", Path("output")):
            root = Path(directory)
            folder, changed = focus.persist(root, candidate)
            self.assertEqual(3, changed)
            self.assertEqual(0, focus.persist(root, candidate)[1])
            focus.verify(root, candidate)
            (folder / "review.json").write_bytes(b"tampered")
            with self.assertRaisesRegex(focus.FocusedBuildingRingError, "IndependentReconstructionMismatch"):
                focus.verify(root, candidate)
            with self.assertRaisesRegex(focus.FocusedBuildingRingError, "ImmutableGenerationContentMismatch"):
                focus.persist(root, candidate)

    def test_output_escape_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(focus.FocusedBuildingRingError, "PathEscapesRoot"):
                focus.safe(Path(directory), Path("../escape"))


if __name__ == "__main__":
    unittest.main()
