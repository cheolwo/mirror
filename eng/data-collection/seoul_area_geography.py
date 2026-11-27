"""Index Seoul's official area boundaries on the existing EPSG:5179 tile lattice."""

from contextlib import closing
import gzip
import json
import math
from pathlib import Path
import sqlite3
import sys
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from seoul_tile_population import PUBLIC, ROOT, axial, digest

OUTPUT = PUBLIC / "seoul-area-evidence-20261125"
BOUNDARIES = ROOT / "artifacts/local/hongdal-unity-claude-20261112/dong-bake-seoul-official-20261118-r1/boundary/seoul-gemd-wgs84.geojson.gz"
REGIONS = PUBLIC / "hongdal-map-api/map-public-data.sqlite"
BOUNDARY_URL = "https://www.data.go.kr/data/15050416/fileData.do"
REGION_URL = "https://www.mois.go.kr/frt/bbs/type001/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000052&nttId=130674"
SPACING = 300
REVISION = "seoul-area-300m-202610-r1"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_release (
    id TEXT PRIMARY KEY, dataset_id TEXT NOT NULL, period TEXT NOT NULL,
    url TEXT, local_input TEXT NOT NULL, sha256 TEXT NOT NULL
);
CREATE TABLE district (
    code TEXT PRIMARY KEY, name TEXT NOT NULL, source_id TEXT REFERENCES source_release(id)
);
CREATE TABLE admin_dong (
    code TEXT PRIMARY KEY, district_code TEXT NOT NULL REFERENCES district(code),
    name TEXT NOT NULL, source_id TEXT REFERENCES source_release(id)
);
CREATE TABLE legal_dong (
    code TEXT PRIMARY KEY, district_code TEXT NOT NULL REFERENCES district(code),
    name TEXT NOT NULL, source_id TEXT REFERENCES source_release(id)
);
CREATE TABLE admin_legal (
    admin_code TEXT REFERENCES admin_dong(code), legal_code TEXT REFERENCES legal_dong(code),
    source_id TEXT NOT NULL REFERENCES source_release(id), PRIMARY KEY(admin_code,legal_code)
);
CREATE TABLE tile (
    q INTEGER, r INTEGER, center_x REAL NOT NULL,center_y REAL NOT NULL,
    seoul_area_m2 REAL NOT NULL CHECK(seoul_area_m2>0),
    seoul_share_permille INTEGER NOT NULL CHECK(seoul_share_permille BETWEEN 0 AND 1000),
    PRIMARY KEY(q,r)
);
CREATE TABLE tile_admin (
    q INTEGER,r INTEGER,admin_code TEXT REFERENCES admin_dong(code),
    overlap_m2 REAL NOT NULL CHECK(overlap_m2>0), source_id TEXT NOT NULL REFERENCES source_release(id),
    PRIMARY KEY(q,r,admin_code), FOREIGN KEY(q,r) REFERENCES tile(q,r)
);
CREATE INDEX tile_admin_by_dong ON tile_admin(admin_code,q,r);
CREATE VIEW v_district_tile AS
    SELECT d.district_code,t.q,t.r,sum(t.overlap_m2) AS overlap_m2
    FROM tile_admin t JOIN admin_dong d ON d.code=t.admin_code
    GROUP BY d.district_code,t.q,t.r;
"""


def read_only(path):
    return sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)


def gis():
    try:
        import pyproj
        import shapely
    except ModuleNotFoundError:
        path = str(PUBLIC / "gis-runtime-r1")
        if path not in sys.path:
            sys.path.insert(0, path)
        import pyproj
        import shapely
    return pyproj, shapely


def area_shapes(path, pyproj, shapely):
    from shapely.ops import transform

    to_meters = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        features = json.load(stream)["features"]
    records = {}
    for feature in features:
        props = feature["properties"]
        key = str(props["adm_cd2"])
        if key in records or props["sido"] != "11":
            raise ValueError("invalid_seoul_boundary_keys")
        geometry = transform(to_meters, shapely.geometry.shape(feature["geometry"]))
        if not geometry.is_valid:
            geometry = shapely.make_valid(geometry)
        if geometry.is_empty or not geometry.area:
            raise ValueError("invalid_seoul_boundary_geometry")
        records[key] = geometry
    return records


def load_regions(db, regions, boundaries):
    with closing(read_only(regions)) as src:
        code_rows = src.execute("""SELECT code,name,parent_code,kind,as_of,release_id FROM current_regions
                                   WHERE code LIKE '11%' AND kind IN ('sigungu','administrative','legal')""").fetchall()
        dates = {(row[4], row[5]) for row in code_rows}
        if len(dates) != 1:
            raise ValueError("ambiguous_region_release")
        (as_of, release), = dates
        if {row[0] for row in code_rows if row[3] == "administrative"} != set(boundaries):
            raise ValueError("registry_boundary_code_disagreement")
        db.execute("INSERT INTO source_release VALUES (?,?,?,?,?,?)", (
            "area-codes", "MOIS registry", as_of, REGION_URL, regions.relative_to(ROOT).as_posix(), digest(regions)
        ))
        db.executemany("INSERT INTO district VALUES (?,?,'area-codes')",
                       ((code, name) for code, name, _, kind, _, _ in code_rows if kind == "sigungu"))
        for key, table in (("administrative", "admin_dong"), ("legal", "legal_dong")):
            db.executemany(f"INSERT INTO {table} VALUES (?,?,?,'area-codes')",
                           ((code, parent, name) for code, name, parent, kind, _, _ in code_rows if kind == key))
        db.executemany("INSERT INTO admin_legal VALUES (?,?,'area-codes')", src.execute("""
            SELECT administrative_code,legal_code FROM region_jurisdiction
            WHERE release_id=? AND administrative_code LIKE '11%' AND legal_code LIKE '11%'""", (release,)))


def tile_center(q, r):
    return (1_000_000 + SPACING * (q + r / 2), 2_000_000 + SPACING * math.sqrt(3) / 2 * r)


def tile_intersections(shapely, shapes):
    keys = tuple(sorted(shapes))
    geoms = tuple(shapes[key] for key in keys)
    tree = shapely.STRtree(geoms)
    xmin, ymin, xmax, ymax = shapely.total_bounds(geoms)
    corners = [axial(x, y, SPACING) for x in (xmin, xmax) for y in (ymin, ymax)]
    q_values, r_values = zip(*corners)
    radius = SPACING / math.sqrt(3)
    offsets = [(radius * math.cos(math.radians(30 + 60 * h)),
                radius * math.sin(math.radians(30 + 60 * h))) for h in range(6)]
    for q in range(min(q_values) - 2, max(q_values) + 3):
        for r in range(min(r_values) - 2, max(r_values) + 3):
            x, y = tile_center(q, r)
            polygon = shapely.Polygon([(x + dx, y + dy) for dx, dy in offsets])
            hits = []
            for i in sorted(tree.query(polygon, predicate="intersects")):
                piece = polygon.intersection(geoms[i])
                if piece.area > 0.01:
                    hits.append((keys[i], piece))
            if hits:
                area = shapely.union_all([piece for _, piece in hits]).area
                weight = min(1000, round(1000 * area / polygon.area))
                yield (q, r, x, y, area, weight), [(key, piece.area) for key, piece in hits]


def check_database(db):
    if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok" or db.execute("PRAGMA foreign_key_check").fetchone():
        raise ValueError("sqlite_integrity_error")


def build(output=OUTPUT):
    output.mkdir(parents=True, exist_ok=True)
    pyproj, shapely = gis()
    shapes = area_shapes(BOUNDARIES, pyproj, shapely)
    pack_tiles = []
    with TemporaryDirectory(dir=output) as temporary:
        path = Path(temporary) / "area-geography.sqlite"
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                load_regions(db, REGIONS, shapes)
                db.execute("INSERT INTO source_release VALUES (?,?,?,?,?,?)", (
                    "area-boundaries", "TL_SCCO_GEMD", "2026-10", BOUNDARY_URL,
                    BOUNDARIES.relative_to(ROOT).as_posix(), digest(BOUNDARIES)
                ))
                for (q, r, x, y, area, weight), overlaps in tile_intersections(shapely, shapes):
                    db.execute("INSERT INTO tile VALUES (?,?,?,?,?,?)", (q, r, x, y, area, weight))
                    db.executemany("INSERT INTO tile_admin VALUES (?,?,?,?,'area-boundaries')",
                                   ((q, r, code, area) for code, area in overlaps))
                    pack_tiles.append({"q": q, "r": r, "w": weight})
            check_database(db)
            counts = {name: db.execute(f"SELECT count(*) FROM {name}").fetchone()[0]
                      for name in ("district", "admin_dong", "legal_dong", "admin_legal", "tile", "tile_admin")}
            mapped_dongs = db.execute("SELECT count(DISTINCT admin_code) FROM tile_admin").fetchone()[0]
            if mapped_dongs != counts["admin_dong"] or not pack_tiles:
                raise ValueError("incomplete_geography_projection")
        path.replace(output / path.name)
    pack = {"schema": "hongdal-hex-tiles.v1", "revision": REVISION, "grid": "EPSG:5179", "spacing": SPACING,
            "distribution": "local-review", "foods": [""] * 8 + ["카페·제과"], "tiles": pack_tiles}
    atomic_json(output / "population-geometry-pack.json", pack)
    return counts


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
