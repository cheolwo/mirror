"""Guard the parent lattice, equal-area child coverage, and deterministic boundary assignment."""

import math
import unittest

from dong_tile_geometry import EQUIVALENT_SPACING, SLOT_LABELS, child_point_slot, point_key, vertices
from seoul_area_geography import tile_center


def cross(a, b, point):
    return (b[0] - a[0]) * (point[1] - a[1]) - (b[1] - a[1]) * (point[0] - a[0])


def margins(polygon, point):
    return [cross(a, b, point) for a, b in zip(polygon, polygon[1:] + polygon[:1])]


def area(polygon, center):
    return math.fsum(cross(center, a, b) for a, b in zip(polygon, polygon[1:] + polygon[:1])) / 2


def offset(point, dx, dy):
    return point[0] + dx, point[1] + dy


class DongTileGeometry(unittest.TestCase):
    def test_published_directions_and_centers_do_not_move_parent_keys(self):
        self.assertEqual(("center", "east", "northeast", "northwest", "west", "southwest", "southeast"), SLOT_LABELS)
        self.assertEqual((0, 0, 0), point_key(1_000_000, 2_000_000))
        q, r = -32, -179
        center = tile_center(q, r)
        half = EQUIVALENT_SPACING / 2
        high = half * math.sqrt(3)
        vectors = ((0, 0), (2 * half, 0), (half, high), (-half, high), (-2 * half, 0), (-half, -high), (half, -high))
        for slot, vector in enumerate(vectors):
            with self.subTest(slot=slot):
                self.assertEqual((q, r, slot), point_key(*offset(center, *vector)))

    def test_child_areas_partition_the_existing_parent(self):
        for q, r in ((0, 0), (-32, -179), (124, -415)):
            with self.subTest(q=q, r=r):
                center = tile_center(q, r)
                parent = vertices(q, r)
                parent_area = area(parent, center)
                self.assertAlmostEqual(45_000 * math.sqrt(3), parent_area, places=5)
                children = [vertices(q, r, slot) for slot in range(7)]
                for child in children:
                    self.assertAlmostEqual(parent_area / 7, area(child, center), places=5)
                    self.assertTrue(all(min(margins(parent, point)) >= -1e-6 for point in child))
                self.assertAlmostEqual(parent_area, sum(area(child, center) for child in children), places=5)

    def test_child_outline_contains_no_zero_length_render_edge(self):
        for q, r in ((0, 0), (-32, -179), (124, -415)):
            for slot in range(7):
                child = vertices(q, r, slot)
                with self.subTest(q=q, r=r, slot=slot):
                    self.assertTrue(all(math.dist(a, b) > 1e-6 for a, b in zip(child, child[1:] + child[:1])))

    def test_spread_of_interior_points_agrees_with_explicit_polygons(self):
        q, r = -32, -179
        center = tile_center(q, r)
        polygons = [vertices(q, r, slot) for slot in range(7)]
        covered_slots = set()
        for dx in range(-147, 150, 17):
            for dy in range(-165, 168, 17):
                point = offset(center, dx + 0.357, dy + 0.219)
                if min(margins(vertices(q, r), point)) <= 1e-7:
                    continue
                inside = [slot for slot, polygon in enumerate(polygons) if min(margins(polygon, point)) > 0]
                self.assertEqual(len(inside), 1, point)
                expected, = inside
                covered_slots.add(expected)
                self.assertEqual((q, r, expected), point_key(*point))
        self.assertEqual(set(range(7)), covered_slots)

    def test_child_ties_choose_lowest_number_and_split_consistently(self):
        q, r = -32, -179
        center = tile_center(q, r)
        seeds = [center]
        for direction in range(0, 360, 60):
            angle = math.radians(direction)
            seeds.append(offset(center, EQUIVALENT_SPACING * math.cos(angle), EQUIVALENT_SPACING * math.sin(angle)))
        pairs = [(0, i) for i in range(1, 7)] + [(i, i % 6 + 1) for i in range(1, 7)]
        for left, right in pairs:
            with self.subTest(left=left, right=right):
                p, n = seeds[left], seeds[right]
                middle = tuple((a + b) / 2 for a, b in zip(p, n))
                self.assertEqual(min(left, right), child_point_slot(*middle, q, r))
                near_left = tuple(a + (b - a) * 0.501 for a, b in zip(n, p))
                near_right = tuple(a + (b - a) * 0.501 for a, b in zip(p, n))
                self.assertEqual((q, r, left), point_key(*near_left))
                self.assertEqual((q, r, right), point_key(*near_right))

    def test_neighboring_parent_seam_routes_each_side_to_its_own_parent(self):
        q, r = -32, -179
        parent = tile_center(q, r)
        for delta in ((1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1)):
            neighbor_key = (q + delta[0], r + delta[1])
            neighbor = tile_center(*neighbor_key)
            with self.subTest(neighbor=neighbor_key):
                inside_parent = tuple(a + (b - a) * 0.499 for a, b in zip(parent, neighbor))
                inside_neighbor = tuple(a + (b - a) * 0.501 for a, b in zip(parent, neighbor))
                midpoint = tuple((a + b) / 2 for a, b in zip(parent, neighbor))
                self.assertEqual((q, r), point_key(*inside_parent)[:2])
                self.assertEqual(neighbor_key, point_key(*inside_neighbor)[:2])
                self.assertIn(point_key(*midpoint)[:2], ((q, r), neighbor_key))


if __name__ == "__main__":
    unittest.main()
