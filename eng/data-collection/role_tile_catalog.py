"""Build a read-only data handoff index for the current Hongdal role and tile contract."""

import argparse
from collections import Counter
from contextlib import closing
import json
from pathlib import Path
import re
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import PUBLIC, ROOT, digest


OUTPUT = PUBLIC / "role-tile-intake-20261126"
BASE_CATALOG = TILE_OUTPUT / "field-catalog.json"
CONTRACT = Path(__file__).with_name("role-tile-contract.json")
CATALOG_DB = "role-tile-catalog.sqlite"
PHASES = ("initial", "detail", "action")
BASELINES = PUBLIC / "diorama-data-relations-20261123-r2"
PRICES = PUBLIC / "hongdal-price-freight-20261124/hongdal-price-freight.sqlite"
SUPPORT = OUTPUT / "drivers/role-driver-support.sqlite"
MERCHANT = OUTPUT / "business/seoul-business.sqlite"
COMMERCIAL = OUTPUT / "business/seoul-worker-commercial-area.sqlite"
BUILDINGS = OUTPUT / "building-elevator/seoul-building-elevator.sqlite"
SOURCES = {
    "libraries": (OUTPUT / "libraries/library-tile.sqlite", "v_tile_library", "15013109", "libraries/verification.json"),
    "mobility": (OUTPUT / "mobility/seoul-mobility-dong.sqlite", "v_mobility_dong_hour", "OA-22657", "mobility/verification.json"),
    "rider_support": (SUPPORT, "v_role_tile_support", "15167120_and_seoul_worker_rest", "drivers/verification.json"),
    "freight_support": (SUPPORT, "v_role_district_support", "NLIC_public_truck_depots", "drivers/verification.json"),
    "merchant": (MERCHANT, "v_admin_quarter_food_context", "OA-22172_OA-22175", "business/business-verification.json"),
    "delivery_area": (OUTPUT / "business/delivery-area-coverage.sqlite", "v_tile_delivery_area_observation",
                      "15167544", "business/delivery-area-verification.json"),
    "public_delivery": (MERCHANT, "public_delivery_summary", "Seoul_public_delivery_2025_2026H1", "business/source-receipts.json"),
    "resident_food_survey": (OUTPUT / "business/food-survey-city-context.sqlite", "v_resident_city_survey",
                             "Seoul_food_survey_2022_2025", "business/읽는법.md"),
    "housing": (PUBLIC / "hongdal-housing-transactions-20261125/housing-transactions.sqlite",
                "v_housing_effective_transaction", "MOLIT_observed_file", "../hongdal-housing-transactions-20261125/verification.json"),
    "warehouses": (PUBLIC / "hongdal-map-scale-index-20261123/korea/logistics-points/logistics-points.sqlite",
                    "v_korea_logistics_points", "NLIC_national_status", "../hongdal-map-scale-index-20261123/korea/logistics-points/verification.json"),
    "prices": (PRICES, "v_display_observation", "KAMIS_aT_KOTI_separate_units", "../hongdal-price-freight-20261124/verification.json"),
    "agri_support": (BASELINES / "food-supply-space/normalized/food-supply-space-reference.sqlite",
                     "v_apc_site_context", "MAFRA_APC_reference", None),
    "farm_statistics": (BASELINES / "regional-landuse-and-farm/regional-landuse-and-farm.sqlite",
                        "v_region_farm_census", "KOSIS_agricultural_census", None),
    "farmmap": (OUTPUT / "farm/seoul-farmmap-tile.sqlite", "v_child_footprint", "15104481", "farm/seoul-farmmap-tile.verification.json"),
    "stay": (OUTPUT / "stay/stay-tile.sqlite", "v_child_stay", "OA-22892_OA-22893_OA-22894", "stay/verification.json"),
    "food_churn": (OUTPUT / "food-churn/food-churn.sqlite", "v_food_churn_child", "MOIS_food_license_snapshot_20261118", "food-churn/verification.json"),
    "sales_shape": (MERCHANT, "v_sales_shape_hour", "OA-22175", "business/business-verification.json"),
    "workforce": (COMMERCIAL, "v_workers_dong_context", "OA-22184", "business/worker-commercial-verification.json"),
    "commercial_geometry": (COMMERCIAL, "v_commercial_child_context", "OA-15560", "business/worker-commercial-verification.json"),
    "building_register": (BUILDINGS, "v_building_register_by_region", "MOLIT_register_202609", "building-elevator/field-catalog-additions.json"),
    "elevator_location": (BUILDINGS, "v_elevator_installation_child", "15156424", "building-elevator/field-catalog-additions.json"),
    "pedestrian_entrance": (OUTPUT / "pedestrian-entrance/seoul-pedestrian-entrance-tile.sqlite",
                            "v_pedestrian_entrance_child", "OA-21699", "pedestrian-entrance/verification.json"),
    "station_weather": (TILE_OUTPUT / "ecology/seasonal/seoul-station-season-tile.sqlite", "v_station_hour_tile_term",
                        "KMA_observed_station_context", "../dong-tile-intake-20261126/ecology/seasonal/verification.json"),
    "motorcycle": (OUTPUT / "drivers/motorcycle-hotspots-2025.sqlite", "v_rider_hotspot_tile",
                   "15105286_TAAS_group_2026113", "drivers/motorcycle-verification.json"),
    "seoul_freight_parking": (OUTPUT / "drivers/seoul-freight-curb-parking.sqlite", "v_seoul_freight_tile_support",
                              "Seoul_public_loading_parking", "drivers/seoul-loading-verification.json"),
    "land_price": (OUTPUT / "land-price/seoul-land-price-2026.sqlite", "v_legal_dong_land_price", "OA-1180",
                   "land-price/verification.json"),
}
PORTABLE_LIBRARY = OUTPUT / "portability/library-national-portability.sqlite"
PORTABLE_LINEAGE = OUTPUT / "portability/읽는법.md"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE metadata (id TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE artifact (path TEXT PRIMARY KEY,sha256 TEXT NOT NULL);
CREATE TABLE relation_reference (
    id TEXT PRIMARY KEY,path TEXT NOT NULL REFERENCES artifact(path),relation_name TEXT NOT NULL,
    source_id_or_group TEXT NOT NULL,lineage_path TEXT,column_names_json TEXT NOT NULL,read_scope TEXT NOT NULL
);
CREATE TABLE role (
    id TEXT PRIMARY KEY,label TEXT NOT NULL,question TEXT NOT NULL,
    menu_order INTEGER NOT NULL UNIQUE,had_tile_score INTEGER NOT NULL CHECK(had_tile_score IN (0,1))
);
CREATE TABLE field (
    id TEXT PRIMARY KEY,label TEXT NOT NULL,display_kind TEXT NOT NULL,evidence_class TEXT NOT NULL,
    availability TEXT NOT NULL,native_grain TEXT NOT NULL,unit TEXT NOT NULL,guard TEXT NOT NULL
);
CREATE TABLE field_relation (
    field_id TEXT REFERENCES field(id),relation_id TEXT REFERENCES relation_reference(id),
    PRIMARY KEY(field_id,relation_id)
) WITHOUT ROWID;
CREATE TABLE field_extent_relation (
    field_id TEXT REFERENCES field(id),extent TEXT,relation_id TEXT NOT NULL REFERENCES relation_reference(id),
    PRIMARY KEY(field_id,extent)
) WITHOUT ROWID;
CREATE TABLE role_field (
    role_id TEXT REFERENCES role(id),field_id TEXT REFERENCES field(id),phase TEXT,position INTEGER NOT NULL,
    PRIMARY KEY(role_id,field_id),UNIQUE(role_id,phase,position),CHECK(phase IN('initial','detail','action'))
) WITHOUT ROWID;
CREATE TABLE collection_work (
    order_no INTEGER PRIMARY KEY,label TEXT NOT NULL,state TEXT NOT NULL,coverage TEXT NOT NULL,
    evidence_path TEXT NOT NULL,next_condition TEXT NOT NULL
);
CREATE TABLE work_field (
    order_no INTEGER REFERENCES collection_work(order_no),field_id TEXT REFERENCES field(id),
    PRIMARY KEY(order_no,field_id)
) WITHOUT ROWID;
CREATE VIEW v_role_tile_inventory AS
SELECT r.id AS role_id,r.label AS role_name,r.question,rf.phase,rf.position,
       f.id AS field_key,f.label,f.display_kind,f.evidence_class,f.availability,f.native_grain,f.unit,f.guard,
       s.path AS database_path,s.relation_name,s.source_id_or_group,s.lineage_path,s.read_scope
FROM role_field rf JOIN role r ON r.id=rf.role_id JOIN field f ON f.id=rf.field_id
LEFT JOIN field_relation x ON x.field_id=f.id LEFT JOIN relation_reference s ON s.id=x.relation_id;
CREATE VIEW v_collection_return AS
SELECT w.*,f.id AS field_key,f.availability FROM collection_work w
LEFT JOIN work_field x USING(order_no) LEFT JOIN field f ON f.id=x.field_id;
CREATE VIEW v_field_extent_reference AS
SELECT b.field_id,b.extent,s.path AS database_path,s.relation_name,s.source_id_or_group,s.lineage_path
FROM field_extent_relation b JOIN relation_reference s ON s.id=b.relation_id;
"""


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def columns(path, relation):
    if not path.is_file() or not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9]*", relation):
        raise ValueError(f"missing_readable_role_source:{path}:{relation}")
    with closing(read_only(path)) as db:
        found = [line[1] for line in db.execute(f"PRAGMA table_info({relation})")]
    if not found:
        raise ValueError(f"missing_role_relation:{relation}:{path}")
    return found


def reference(db, hashes, record, key):
    file = ROOT / record["database"]
    if file not in hashes:
        hashes[file] = digest(file)
        db.execute("INSERT INTO artifact VALUES (?,?)", (file.relative_to(ROOT).as_posix(), hashes[file]))
    db.execute("INSERT OR IGNORE INTO relation_reference VALUES (?,?,?,?,?,?,?)", (
        key, record["database"], record["relation"], record["sourceIdOrGroup"], record.get("lineage"),
        json.dumps(columns(file, record["relation"])), "local-review",
    ))


def legacy_fields(db, hashes, catalog, contract):
    inherited = contract["existingTileFields"]
    original = {}
    for layer in catalog["layers"]:
        field = layer["fieldKey"]
        original[field] = layer
        label, visual, evidence, guard = inherited.get(field, (field, "base", "geometry_reference", layer["basis"]))
        db.execute("INSERT INTO field VALUES (?,?,?,?,?,?,?,?)", (
            field, label, visual, evidence, "existing_verified", layer["sourceGrain"], layer["unit"], guard,
        ))
        info = dict(layer)
        if layer.get("lineage"):
            lineage = TILE_OUTPUT / layer["lineage"]
            if lineage.is_file():
                info["lineage"] = lineage.relative_to(ROOT).as_posix()
        reference(db, hashes, info, "original:" + field)
        db.execute("INSERT INTO field_relation VALUES (?,?)", (field, "original:" + field))
    if not set(inherited) <= set(original):
        raise ValueError("role_contract_references_unknown_base_field")


def additional_fields(db, hashes, contract, work):
    layers = []
    coverage = {field: item["coverage"] for item in work for field in item.get("fields", [])}
    for spec in contract["extraFields"]:
        field, key = spec["id"], spec["source"]
        db.execute("INSERT INTO field VALUES (?,?,?,?,?,?,?,?)", (
            field, spec["label"], spec["visual"], spec["scope"], spec["state"], spec["sourceGrain"],
            spec["unit"], spec["guard"],
        ))
        if key is None:
            continue
        file, relation, source, lineage = SOURCES[key]
        receipt = (OUTPUT / lineage).resolve() if lineage else None
        info = {
            "fieldKey": field, "database": file.relative_to(ROOT).as_posix(), "relation": relation,
            "sourceIdOrGroup": source, "sourceGrain": spec["sourceGrain"], "unit": spec["unit"],
            "basis": spec["guard"], "scopeAndPeriod": coverage.get(field, spec["sourceGrain"]), "evidenceClass": spec["scope"],
            "missing": "제공 범위 밖·가림·권위 미확인을 각각 유지한다. 숫자 0은 원문에서 확인된 경우만.",
            "lineage": receipt.relative_to(ROOT).as_posix() if receipt and receipt.is_file() else None,
        }
        reference(db, hashes, info, "intake:" + key)
        db.execute("INSERT INTO field_relation VALUES (?,?)", (field, "intake:" + key))
        layers.append({**info, "columns": columns(file, relation), "sha256": hashes[file]})
    return layers


def portability_reference(db, hashes):
    extent = "outside_seoul_official_dong_point_candidates"
    relation = "intake:national-library-coordinate-check"
    reference(db, hashes, {"database": PORTABLE_LIBRARY.relative_to(ROOT).as_posix(),
        "relation": "v_library_child", "sourceIdOrGroup": "15013109_official_admin_boundary_crosscheck",
        "lineage": PORTABLE_LINEAGE.relative_to(ROOT).as_posix()}, relation)
    db.execute("INSERT INTO field_extent_relation VALUES (?,?,?)", ("libraryHours", extent, relation))


def bind_roles(db, contract, unity):
    known_ids = set()
    for order, role in enumerate(contract["roles"], start=1):
        key = role["key"]
        known_ids.add(key)
        db.execute("INSERT INTO role VALUES (?,?,?,?,?)",
                   (key, role["label"], role["question"], order, role["tileScoreAtBaseline"]))
        for phase in PHASES:
            db.executemany("INSERT INTO role_field VALUES (?,?,?,?)",
                           ((key, field, phase, pos) for pos, field in enumerate(role[phase], start=1)))
    if unity is None:
        return None
    live_keys = {entry["key"] for entry in load(unity)["stances"]}
    if live_keys != known_ids:
        raise ValueError(f"unity_role_baseline_drift:{sorted(live_keys ^ known_ids)}")
    return digest(unity)


def work_rows(db, work):
    for item in work:
        db.execute("INSERT INTO collection_work VALUES (?,?,?,?,?,?)", (
            item["order"], item["label"], item["state"], item["coverage"], item["evidence"], item["next"],
        ))
        db.executemany("INSERT INTO work_field VALUES (?,?)",
                       ((item["order"], field) for field in item.get("fields", [])))


def validate(db):
    check_database(db)
    invalid = db.execute("""SELECT f.id FROM field f LEFT JOIN field_relation s ON s.field_id=f.id
          WHERE f.availability IN ('new_collected','existing_verified','existing_reindexed')
            AND s.relation_id IS NULL""").fetchall()
    if invalid:
        raise ValueError(f"verified_field_without_source:{invalid}")
    invalid = db.execute("""SELECT role_id,field_key FROM v_role_tile_inventory
          WHERE phase='initial' AND evidence_class IN ('authorized_needed','simulation_only')""").fetchall()
    if invalid:
        raise ValueError(f"initial_field_requires_work_authority:{invalid}")


def build(output=OUTPUT, unity=None):
    catalog, contract = load(BASE_CATALOG), load(CONTRACT)
    work = load(output / "collection-work.json")
    if catalog["geometryRevision"] != REVISION or [x["order"] for x in work] != list(range(1, 10)):
        raise ValueError("role_tile_revision_or_handoff_item_set_mismatch")
    hashes = {}
    with TemporaryDirectory(dir=output) as temporary:
        path = Path(temporary) / CATALOG_DB
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                legacy_fields(db, hashes, catalog, contract)
                layers = additional_fields(db, hashes, contract, work)
                portability_reference(db, hashes)
                unity_hash = bind_roles(db, contract, unity)
                work_rows(db, work)
                db.executemany("INSERT INTO metadata VALUES (?,?)", {
                    "schema": contract["schema"], "geometryRevision": REVISION, "sharedCRS": "EPSG:5179",
                    "distribution": "local-review", "baseCatalogSha256": digest(BASE_CATALOG),
                    "contractSha256": digest(CONTRACT), "unityStanceCatalogSha256": unity_hash or "not_checked",
                }.items())
            validate(db)
            counts = {name: db.execute(f"SELECT COUNT(*) FROM {name}").fetchone()[0] for name in (
                "role", "field", "role_field", "relation_reference", "artifact", "collection_work", "field_extent_relation",
            )}
            grouped = dict(db.execute("SELECT availability,COUNT(*) FROM field GROUP BY 1"))
        path.replace(output / CATALOG_DB)
    atomic_json(output / "field-catalog-additions.json", {
        "schema": "hongdal.dong-tile-field-catalog.v1", "geometryRevision": REVISION,
        "crs": "EPSG:5179", "distribution": "local-review", "childKey": ["q", "r", "slot"],
        "baseCatalog": BASE_CATALOG.relative_to(ROOT).as_posix(), "layers": layers,
    })
    proof = {**counts, "availabilityGroups": grouped, "workStates": dict(Counter(item["state"] for item in work)),
             "geometryRevision": REVISION, "databaseSha256": digest(output / CATALOG_DB),
             "unityStanceCatalogSha256": unity_hash, "integrityCheck": "ok", "foreignKeyViolations": 0}
    atomic_json(output / "catalog-verification.json", proof)
    return proof


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--unity-stance-catalog", type=Path)
    args = parser.parse_args()
    print(json.dumps(build(args.output, args.unity_stance_catalog), ensure_ascii=False))
