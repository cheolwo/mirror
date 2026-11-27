"""Validate suppressed, absent, zero and partial values in the 300 m profile."""

from collections import Counter
import unittest

from seoul_tile_population import BANDS, HOURS, aggregate_day, axial


class SeoulTilePopulation(unittest.TestCase):
    def test_sample_point_at_grid_origin_maps_to_origin_tile(self):
        self.assertEqual((0, 0), axial(1_000_000, 2_000_000, 300))

    def test_partial_mask_and_absence_stay_visible_as_distinct_quality(self):
        key = (0, 0)
        geometry = {"visible": {key: 15}, "absent": {key: 10}}
        by_hour = {hour: {"visible": [0.0, 1, 1, 0]} for hour in HOURS}
        day = list(aggregate_day(by_hour, geometry, Counter({key: 25}), [key]))
        self.assertEqual((0, 0, "lunch", 0.0, 3, 3, 75, 45, 45, 30, 0), day[0])
        del by_hour[BANDS["lunch"][0]]["visible"]
        day = list(aggregate_day(by_hour, geometry, Counter({key: 25}), [key]))
        self.assertEqual((0, 0, "lunch", None, 3, 2, 75, 30, 30, 45, 0), day[0])

    def test_masked_or_missing_tile_never_emits_zero_estimate(self):
        left, right = (0, 0), (1, 0)
        geometry = {"hidden": {left: 25}, "missing": {right: 25}}
        by_hour = {hour: {"hidden": [0.0, 0, 1, 0]} for hour in HOURS}
        records = list(aggregate_day(by_hour, geometry, Counter({left: 25, right: 25}), [left, right]))
        self.assertEqual((0, 0, "lunch", None, 3, 0, 75, 0, 75, 0, 0), records[0])
        self.assertEqual((1, 0, "lunch", None, 3, 0, 75, 0, 0, 75, 0), records[1])


if __name__ == "__main__":
    unittest.main()
