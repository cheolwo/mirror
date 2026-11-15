"""공용 생성 설정의 잘못된 판본·레이어·치수를 굽기 전에 거부한다."""
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from dong_diorama_reference import DEFAULT_PROFILE, load_profile


class ReferenceProfileTest(unittest.TestCase):
    def test_reference_and_fingerprint(self):
        profile, receipt = load_profile()
        self.assertEqual("1126057500", receipt["referenceDongCode"])
        self.assertEqual(profile["profileId"], receipt["id"])
        self.assertEqual(hashlib.sha256(DEFAULT_PROFILE.read_bytes()).hexdigest(), receipt["sha256"])

    def test_rejects_invalid_profile(self):
        cases = (
            ("다른 동", lambda p: p.update(referenceDongCode="1121571000")),
            ("잘린 기준 지문", lambda p: p["baseline"].update(meshSha256="1")),
            ("없는 녹지 설정", lambda p: p["surfaces"].pop("parkPlan")),
            ("겹친 표면 높이", lambda p: p["surfaces"]["walk"].update(yMetres=p["surfaces"]["roadEdge"]["yMetres"])),
            ("잘못된 색", lambda p: p["surfaces"]["lane"].update(rgb=[255, 300, 0])),
            ("차도보다 넓은 차선", lambda p: p["strokeHalfWidthMetres"].update(lane=99.0)),
            ("층 높이 결측", lambda p: p["buildings"].update(floorHeightMetres=float("nan"))),
            ("지붕 팔레트 누락", lambda p: p["buildings"].update(roofRgbPalette=[])),
        )
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "profile.json"
            for label, change in cases:
                with self.subTest(case=label):
                    profile, _ = load_profile()
                    change(profile)
                    path.write_text(json.dumps(profile), encoding="utf-8")
                    with self.assertRaises(ValueError):
                        load_profile(path)

    def test_rejects_duplicate_dong_keys(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "duplicate.json"
            text = DEFAULT_PROFILE.read_text(encoding="utf-8")
            path.write_text(text.replace('"referenceDongCode":', '"referenceDongCode":"1121571000","referenceDongCode":'), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "중복"):
                load_profile(path)


if __name__ == "__main__":
    unittest.main()
