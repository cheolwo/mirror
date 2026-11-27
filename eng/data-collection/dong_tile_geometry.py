"""Review-only seven-way partition of the existing 300 m EPSG:5179 parent grid."""

import math

from seoul_area_geography import SPACING, tile_center
from seoul_tile_population import axial

REVISION = "hongdal-dong-child-candidate-r1"
SLOT_LABELS = ("center", "east", "northeast", "northwest", "west", "southwest", "southeast")
EQUIVALENT_SPACING = SPACING / math.sqrt(7)
PARENT_RADIUS = SPACING / math.sqrt(3)
SEEDS = ((0.0, 0.0),) + tuple(
    (EQUIVALENT_SPACING * math.cos(math.radians(angle)),
     EQUIVALENT_SPACING * math.sin(math.radians(angle))) for angle in range(0, 360, 60)
)
PARENT_OFFSETS = tuple(
    (PARENT_RADIUS * math.cos(math.radians(angle)),
     PARENT_RADIUS * math.sin(math.radians(angle))) for angle in range(30, 390, 60)
)


def clip(vertices, a, b, limit):
    """Clip a convex polygon to the half-plane a*x+b*y <= limit."""
    result = []
    previous = vertices[-1]
    before = a * previous[0] + b * previous[1] - limit
    for point in vertices:
        after = a * point[0] + b * point[1] - limit
        if (before <= 0) != (after <= 0):
            fraction = before / (before - after)
            result.append((previous[0] + fraction * (point[0] - previous[0]),
                           previous[1] + fraction * (point[1] - previous[1])))
        if after <= 0:
            result.append(point)
        previous, before = point, after
    return tuple(result)


def calculate_child_offsets():
    output = []
    for x, y in SEEDS:
        offsets = PARENT_OFFSETS
        for a, b in SEEDS:
            if (a, b) == (x, y):
                continue
            limit = (a * a + b * b - x * x - y * y) / 2
            offsets = clip(offsets, a - x, b - y, limit)
        ring = []
        for point in offsets:
            if not ring or math.dist(point, ring[-1]) > 1e-8:
                ring.append(point)
        if math.dist(ring[0], ring[-1]) <= 1e-8:
            ring.pop()
        output.append(tuple(ring))
    return tuple(output)


CHILD_OFFSETS = calculate_child_offsets()


def child_point_slot(east, north, q, r):
    """Choose the nearest seed in this parent; exact numerical ties go to the first slot."""
    x, y = tile_center(q, r)
    offsets = (east - x, north - y)
    distances = tuple((offsets[0] - a)**2 + (offsets[1] - b)**2 for a, b in SEEDS)
    nearest = min(distances)
    return next(i for i, distance in enumerate(distances) if distance <= nearest + 1e-6)


def point_key(east, north):
    q, r = axial(east, north, SPACING)
    return q, r, child_point_slot(east, north, q, r)


def vertices(q, r, slot=None):
    x, y = tile_center(q, r)
    offsets = PARENT_OFFSETS if slot is None else CHILD_OFFSETS[slot]
    return tuple((x + a, y + b) for a, b in offsets)
