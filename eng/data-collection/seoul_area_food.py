"""Build the Seoul 300m food-license and official region evidence index."""

import argparse
from collections import Counter
from contextlib import closing
from hashlib import file_digest
import json
from math import isfinite
from pathlib import Path
import sqlite3
import sys
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from seoul_tile_population import axial, local_path

ROOT = Path(__file__).resolve().parents[2]
PUBLIC = ROOT / "artifacts/local/public-data"
SOURCE = PUBLIC / "national-restaurant-regional-20261118-r1"
BASE = SOURCE / "index/restaurants.sqlite"
COORDINATE = SOURCE / "coordinate-index/administrative-coordinate-candidates.sqlite"
REFERENCE_PACK = PUBLIC / "floating-250m-20261125/raw/hongdal-tiles-gu.before.json"
APPROVED_OUTPUT = PUBLIC / "seoul-area-evidence-20261125/food"
RELEASE_ID = "food-licenses-20261118"
REGION_REFERENCE = "2026-10-30"
FOODS = ("한식", "고기", "분식·김밥", "중식", "일식", "양식·패스트푸드", "치킨·호프", "주점", "카페·제과", "편의점", "기타")
PREVIOUS_TYPE_GROUPS = (
    ("한식", "냉면집", "탕류(보신용)"),
    ("식육(숯불구이)",),
    ("분식", "김밥(도시락)"),
    ("중국식",),
    ("일식", "횟집", "복어취급"),
    ("경양식", "패밀리레스트랑", "패스트푸드", "외국음식전문점(인도,태국등)"),
    ("호프/통닭", "통닭(치킨)"),
    ("정종/대포집/소주방", "감성주점", "라이브카페", "간이주점", "룸살롱", "단란주점"),
    ("커피숍", "까페", "다방", "전통찻집", "떡카페", "키즈카페", "아이스크림", "제과점영업"),
    ("편의점",),
)
CATEGORY = {name: group for group, names in enumerate(PREVIOUS_TYPE_GROUPS) for name in names}

SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE food_release (
    release_id TEXT PRIMARY KEY, province TEXT NOT NULL,
    region_reference_date TEXT NOT NULL, base_database_sha256 TEXT NOT NULL,
    coordinate_database_sha256 TEXT NOT NULL, coordinate_candidate_boundary_date TEXT NOT NULL
);
CREATE TABLE food_dataset (
    release_id TEXT NOT NULL REFERENCES food_release(release_id),
    dataset_id INTEGER NOT NULL, key TEXT NOT NULL, label TEXT NOT NULL,
    official_url TEXT NOT NULL, original_sha256 TEXT NOT NULL,
    gzip_sha256 TEXT NOT NULL, acquired_at_utc TEXT NOT NULL, nationwide_source_rows INTEGER NOT NULL,
    PRIMARY KEY(release_id,dataset_id)
) WITHOUT ROWID;
CREATE TABLE food_district (
    district_code TEXT PRIMARY KEY, name TEXT UNIQUE NOT NULL
);
CREATE TABLE food_legal_dong (
    code TEXT PRIMARY KEY, name TEXT NOT NULL,
    district_code TEXT NOT NULL REFERENCES food_district(district_code)
);
CREATE TABLE food_admin_dong (
    code TEXT PRIMARY KEY, name TEXT NOT NULL,
    district_code TEXT NOT NULL REFERENCES food_district(district_code)
);
CREATE TABLE food_legal_admin_edge (
    legal_dong_code TEXT NOT NULL REFERENCES food_legal_dong(code),
    admin_dong_code TEXT NOT NULL REFERENCES food_admin_dong(code),
    PRIMARY KEY(legal_dong_code,admin_dong_code)
) WITHOUT ROWID;
CREATE TABLE food_category (
    category_id INTEGER PRIMARY KEY, label TEXT UNIQUE NOT NULL
);
CREATE TABLE food_license (
    release_id TEXT NOT NULL, dataset_id INTEGER NOT NULL, source_record INTEGER NOT NULL,
    district_code TEXT NOT NULL REFERENCES food_district(district_code),
    legal_dong_code TEXT REFERENCES food_legal_dong(code), admin_dong_code TEXT,
    admin_binding TEXT NOT NULL, category_id INTEGER NOT NULL REFERENCES food_category(category_id),
    q INTEGER, r INTEGER, binding_state TEXT NOT NULL,
    PRIMARY KEY(release_id,dataset_id,source_record),
    FOREIGN KEY(release_id,dataset_id) REFERENCES food_dataset(release_id,dataset_id),
    FOREIGN KEY(legal_dong_code,admin_dong_code) REFERENCES food_legal_admin_edge(legal_dong_code,admin_dong_code),
    CHECK((q IS NULL)=(r IS NULL)),
    CHECK((q IS NOT NULL)=(binding_state='source_point_projected')),
    CHECK((admin_dong_code IS NOT NULL)=(admin_binding IN ('official_single_candidate','secondary_boundary_single_candidate')))
) WITHOUT ROWID;
CREATE INDEX food_license_tile_idx ON food_license(q,r,category_id) WHERE q IS NOT NULL;
CREATE INDEX food_license_admin_idx ON food_license(admin_dong_code,category_id) WHERE admin_dong_code IS NOT NULL;
CREATE INDEX food_license_district_idx ON food_license(district_code,category_id);

CREATE VIEW v_food_tile AS
SELECT release_id,q,r,category_id,count(*) AS licenses FROM food_license
WHERE binding_state='source_point_projected' GROUP BY release_id,q,r,category_id;
CREATE VIEW v_food_district AS
SELECT f.release_id,f.district_code,d.name AS district_name,f.category_id,count(*) AS licenses,
       sum(f.binding_state='source_point_projected') AS located_licenses
FROM food_license f JOIN food_district d USING(district_code)
GROUP BY f.release_id,f.district_code,d.name,f.category_id;
CREATE VIEW v_food_admin AS
SELECT f.release_id,f.admin_dong_code,a.name AS admin_dong_name,a.district_code,f.category_id,
       count(*) AS licenses, sum(f.admin_binding='official_single_candidate') AS official_single_candidate,
       sum(f.admin_binding='secondary_boundary_single_candidate') AS secondary_boundary_single_candidate
FROM food_license f JOIN food_admin_dong a ON a.code=f.admin_dong_code
GROUP BY f.release_id,f.admin_dong_code,a.name,a.district_code,f.category_id;
"""


def digest(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest()


def projected_tile(sx, sy, transform):
    if not sx.strip() or not sy.strip():
        return None, None, "source_xy_missing"
    try:
        x, y = float(sx), float(sy)
    except ValueError:
        return None, None, "source_xy_invalid"
    if not (isfinite(x) and isfinite(y)):
        return None, None, "source_xy_invalid"
    if not (100_000 < x < 400_000 and 300_000 < y < 700_000):
        return None, None, "source_xy_out_of_range"
    east, north = transform(x, y)
    if not (isfinite(east) and isfinite(north)):
        return None, None, "projection_invalid"
    return (*axial(east, north, 300), "source_point_projected")


def region_tables(source, out):
    districts = {name: code[:5] for code, name in source.execute(
        """SELECT code,district FROM legal_region WHERE province='서울특별시'
           AND district<>'' AND substr(code,6)='00000' AND abolished=''""")}
    if len(districts) != 25:
        raise ValueError("unexpected active Seoul district code set")
    out.executemany("INSERT INTO food_district VALUES (?,?)", sorted((code, name) for name, code in districts.items()))
    for table, source_table in (("food_legal_dong", "legal_region"), ("food_admin_dong", "administrative_region")):
        out.executemany(f"INSERT INTO {table} VALUES (?,?,?)", (
            (code, name, districts[district]) for code, name, district in source.execute(
                f"""SELECT code,locality,district FROM {source_table} WHERE province='서울특별시'
                    AND locality<>'' AND abolished='' ORDER BY code""")
        ))
    out.executemany("INSERT INTO food_legal_admin_edge VALUES (?,?)", source.execute("""
        SELECT c.legal_code,c.administrative_code FROM administrative_candidate c
        JOIN legal_region l ON c.legal_code=l.code WHERE l.province='서울특별시'
        ORDER BY c.legal_code,c.administrative_code"""))
    return districts


def record_rows(source, districts, transform):
    for (dataset, number, district, legal, status, official_single, coordinate_candidate, coordinate_status,
         hygiene, business, sx, sy) in source.execute("""
        SELECT l.dataset_id,l.source_record,l.district,l.legal_code,l.administrative_status,
               s.administrative_code,c.candidate_admin_code,c.disposition,
               l.hygiene_type,l.business_type,l.source_x,l.source_y
        FROM license_observation l
        LEFT JOIN administrative_candidate s ON s.legal_code=l.legal_code AND s.is_single_candidate=1
        LEFT JOIN coordinate.coordinate_disposition c USING(dataset_id,source_record)
        WHERE l.province='서울특별시' AND l.reported_status='영업/정상'
        ORDER BY l.dataset_id,l.source_record
    """):
        if legal is not None and not legal.startswith(districts[district]):
            raise ValueError("license district contradicts official legal code")
        if official_single:
            admin, admin_binding = official_single, "official_single_candidate"
        elif coordinate_candidate:
            if coordinate_status != "CoordinateInOneCandidateBoundary":
                raise ValueError("unexpected coordinate selection status")
            admin, admin_binding = coordinate_candidate, "secondary_boundary_single_candidate"
        else:
            admin, admin_binding = None, coordinate_status or status
        category = 8 if dataset == 3 else CATEGORY.get((hygiene or business).strip(), CATEGORY.get(business.strip(), 10))
        yield (
            RELEASE_ID, dataset, number, districts[district], legal, admin, admin_binding, category,
            *projected_tile(sx, sy, transform),
        )


def rows_dict(db, sql, parameters=()):
    result = db.execute(sql, parameters)
    keys = [column[0] for column in result.description]
    return [dict(zip(keys, row)) for row in result]


def compare_pack(out, path):
    pack = json.loads(path.read_text(encoding="utf-8-sig"))
    if pack["spacing"] != 300 or pack["foods"] != list(FOODS):
        raise ValueError("reference tile category or geometry changed")
    counts = {(q, r, category): total for q, r, category, total in out.execute(
        "SELECT q,r,category_id,licenses FROM v_food_tile")}
    tested = 0
    mismatches = []
    for row in pack["tiles"]:
        if row["w"] < 500:
            continue
        found = [counts.get((row["q"], row["r"], category), 0) for category in range(len(FOODS))]
        if found != row["m"] or sum(found) != row["n"]:
            mismatches.append([row["q"], row["r"]])
        tested += 1
    return {
        "revision": pack["revision"], "sha256": digest(path), "comparedTiles": tested,
        "mismatchCount": len(mismatches), "firstMismatches": mismatches[:20],
    }


def build(output):
    identity = json.loads((COORDINATE.parent / "verification.json").read_text(encoding="utf-8-sig"))
    base_hash, coordinate_hash = digest(BASE), digest(COORDINATE)
    if identity["status"] != "Passed" or identity["baseline"]["sha256"] != base_hash:
        raise ValueError("food coordinate proof and source database do not correspond")
    if BASE.parent.joinpath("verification.json").is_file():
        base_proof = json.loads(BASE.parent.joinpath("verification.json").read_text(encoding="utf-8-sig"))
        if base_proof["databaseSha256"] != base_hash:
            raise ValueError("food source DB differs from its acquisition proof")
    try:
        from pyproj import Transformer
    except ModuleNotFoundError:
        sys.path.insert(0, str(PUBLIC / "gis-runtime-r1"))
        from pyproj import Transformer
    transform = Transformer.from_crs(5174, 5179, always_xy=True).transform
    output.mkdir(parents=True, exist_ok=True)
    path = output / "food-area.sqlite"
    with TemporaryDirectory(dir=output) as staging:
        next_path = Path(staging) / path.name
        with closing(sqlite3.connect(BASE.as_uri() + "?mode=ro", uri=True)) as source, closing(sqlite3.connect(next_path)) as out:
            source.execute("ATTACH DATABASE ? AS coordinate", (COORDINATE.as_uri() + "?mode=ro",))
            out.executescript(SCHEMA)
            with out:
                out.execute("INSERT INTO food_release VALUES (?,?,?,?,?,?)", (
                    RELEASE_ID, "서울특별시", REGION_REFERENCE, base_hash, coordinate_hash, identity["boundary"]["edition"],
                ))
                out.executemany("INSERT INTO food_dataset VALUES (?,?,?,?,?,?,?,?,?)", (
                    (RELEASE_ID, *row) for row in source.execute("""SELECT id,dataset,label,official_catalog,original_sha256,
                    gzip_sha256,acquired_at_utc,source_rows FROM dataset ORDER BY id""")
                ))
                districts = region_tables(source, out)
                out.executemany("INSERT INTO food_category VALUES (?,?)", enumerate(FOODS))
                out.executemany("INSERT INTO food_license VALUES (?,?,?,?,?,?,?,?,?,?,?)", record_rows(source, districts, transform))
            expected = source.execute("""SELECT count(*) FROM license_observation WHERE province='서울특별시'
                                        AND reported_status='영업/정상'""").fetchone()[0]
            actual = out.execute("SELECT count(*) FROM food_license").fetchone()[0]
            located = out.execute("SELECT sum(licenses) FROM v_food_tile").fetchone()[0]
            coverage = rows_dict(out, "SELECT binding_state,count(*) AS records FROM food_license GROUP BY binding_state")
            accuracy = Counter({row["binding_state"]: row["records"] for row in coverage})
            if actual != expected or located != accuracy["source_point_projected"] or sum(accuracy.values()) != actual:
                raise ValueError("food records lost or duplicated in location indexing")
            if out.execute("PRAGMA foreign_key_check").fetchone() or out.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                raise ValueError("food database integrity check failed")
            previous = compare_pack(out, REFERENCE_PACK)
            if previous["mismatchCount"]:
                raise ValueError(f"food categories differ on old map: {previous['firstMismatches']}")
            notes = {
                "schema": "hongdal.seoul-food-area.v1", "releaseId": RELEASE_ID,
                "tile": {"crs": "EPSG:5179", "spacingM": 300, "sourceCrs": "EPSG:5174", "origin": [1_000_000, 2_000_000]},
                "scope": "2026-11-18 nationwide license acquisition; all Seoul districts, reported operating or normal",
                "districts": len(districts), "licenseRows": actual, "pointLocatedRows": located,
                "pointTileCount": out.execute("SELECT count(*) FROM (SELECT DISTINCT q,r FROM v_food_tile)").fetchone()[0],
                "coordinateCoverage": coverage,
                "adminCoverage": rows_dict(out, "SELECT admin_binding,count(*) AS records FROM food_license GROUP BY admin_binding"),
                "cafesAndBakeries": out.execute("SELECT count(*) FROM food_license WHERE category_id=8").fetchone()[0],
                "oldPackComparison": previous,
                "sources": rows_dict(out, "SELECT * FROM food_dataset ORDER BY dataset_id"),
                "seoulDistrictCounts": rows_dict(out, "SELECT district_code,district_name,sum(licenses) AS licenses,"
                    "sum(located_licenses) AS located_licenses FROM v_food_district GROUP BY district_code ORDER BY district_code"),
                "integrity": "ok",
                "notes": [
                    "집계 기준은 수집일의 영업/정상 신고다. 현장 영업·주문 발생·플랫폼 가맹은 각각 별도 자료로 확인한다.",
                    "타일 값이 없으면 이번 원천에서 위치가 연결된 인허가 기록이 없는 상태다. 좌표 없는 기록은 구별 집계에 남긴다.",
                    "지역은 인허가 주소의 공식 구·법정동을 보존하고, 행정동은 공식 관할 단일 또는 날짜가 있는 후보 경계 내 단일을 표시한다.",
                    "원천 좌표를 기존 300m 기준으로 변환했다. 주소의 구와 타일의 행정구역 면적은 별개의 관계다.",
                ],
            }
        next_path.replace(path)
    notes["databaseSha256"] = digest(path)
    atomic_json(output / "food-summary.json", notes)
    return notes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=APPROVED_OUTPUT)
    args = parser.parse_args()
    notes = build(local_path(args.output))
    print(json.dumps({key: notes[key] for key in ("districts", "licenseRows", "pointLocatedRows",
                     "pointTileCount", "coordinateCoverage", "oldPackComparison")}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
