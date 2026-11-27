"""Read back the local tile intake and record reproducible checks across packages."""

from contextlib import closing
from datetime import datetime, timezone
import json

from collect_public_snapshots import atomic_json
from dong_tile_bundle import national_dong_lookup, national_tile_lookup, write_examples
from dong_tile_geometry import REVISION
from dong_tile_index import OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import digest


PINNED_DB_RECEIPTS = {
    "dong-tile-evidence.sqlite": ("core-summary.json", "databaseSha256"),
    "child-presence.sqlite": ("presence-summary.json", "databaseSha256"),
    "bike/bike-child-index.sqlite": ("bike/bike-summary.json", "databaseSha256"),
    "sensors/sensor-tile-reference.sqlite": ("sensors/sensor-summary.json", "databaseSha256"),
    "buildings/building-tile-index.sqlite": ("buildings/building-summary.json", "databaseSha256"),
    "buildings/coverage/building-polygon-coverage.sqlite": ("buildings/coverage/building-polygon-coverage-summary.json", "databaseSha256"),
    "buildings/reconciliation/seoul-building-layer-reconciliation.sqlite": (
        "buildings/reconciliation/seoul-building-layer-reconciliation-summary.json", "fingerprints.reconciliation"),
    "terrain/terrain-tile-index.sqlite": ("terrain/terrain-summary.json", "databaseSha256"),
    "street-lighting/street-lighting-tile-index.sqlite": ("street-lighting/street-lighting-summary.json", "databaseSha256"),
    "stations/station-access.sqlite": ("stations/station-access-summary.json", "databaseSha256"),
    "facilities/tile-facilities.sqlite": ("facilities/facilities-summary.json", "databaseSha256"),
    "transport/bus-latest/bus-tile-daily.sqlite": ("transport/bus-latest/bus-daily-summary.json", "databaseSha256"),
    "transport/tile-transport.sqlite": ("transport/verification.json", "databaseSha256"),
    "transport/surface-coverage/street-surface-coverage.sqlite": ("transport/surface-coverage/verification.json", "database.sha256"),
    "transport/mobility-support/mobility-support.sqlite": ("transport/mobility-support/verification.json", "database.sha256"),
    "ecology/dong-tile-ecology.sqlite": ("ecology/verification.json", "databaseSha256"),
    "ecology/ecology-existing-point-references.sqlite": ("ecology/existing-point-verification.json", "databaseSha256"),
    "ecology/national/national-land-cover.sqlite": ("ecology/national/national-land-cover.receipt.json", "databaseSha256"),
    "ecology/national/national-weather-stations.sqlite": ("ecology/national/national-weather-stations.receipt.json", "databaseSha256"),
    "ecology/national/kma-observation-window.sqlite": ("ecology/national/kma-observation-window-summary.json", "databaseSha256"),
    "ecology/seasonal/seoul-station-season-tile.sqlite": ("ecology/seasonal/verification.json", "databaseSha256"),
    "ecology/water/seoul-water-authority-reference.sqlite": ("ecology/water/verification.json", "databaseFiles.0.sha256"),
    "ecology/water/seoul-water-osm-comparison.sqlite": ("ecology/water/verification.json", "databaseFiles.1.sha256"),
    "ecology/worldcover/worldcover-2021-tile-sample.sqlite": ("ecology/worldcover/verification.json", "databaseSha256"),
    "landmarks/landmark-tile-index.sqlite": ("landmarks/landmark-verification.json", "indexSha256"),
    "portability/national-points/national-point-tiles.sqlite": ("portability/national-points/verification.json", "database.sha256"),
    "portability/module-readiness/module-readiness.sqlite": ("portability/module-readiness/verification.json", "auditDatabaseSha256"),
    "dong-layer-coverage.sqlite": ("coverage-summary.json", "databaseSha256"),
    "portability/guri-sutaek3/guri-sutaek3-tile.sqlite": ("portability/guri-sutaek3/guri-sutaek3-summary.json", "databaseSha256"),
    "portability/region-quality-cases/region-quality-tile-cases.sqlite":
        ("portability/region-quality-cases/region-quality-summary.json", "databaseSha256"),
}


def results(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def expected_digest(receipt, address):
    value = results(OUTPUT / receipt)
    for token in address.split("."):
        value = value[int(token)] if isinstance(value, list) else value[token]
    return value


def audit():
    examples = write_examples()
    portable = national_dong_lookup("4131059000")
    national = national_tile_lookup(-15,-177,5)
    observed_counts = {(row["source"],row["category"]):row["positive_source_count"] for row in national["points"]}
    expected = {("localdata","general_restaurants"):3,("tago","bus_stop_source_row"):1}
    if observed_counts != expected or portable["officialDongAndPopulation"]["one_person_households"] != 3783:
        raise ValueError("guri_independent_lookup_mismatch")
    holdings = {}
    for path in sorted(OUTPUT.rglob("*.sqlite")):
        rel = path.relative_to(OUTPUT).as_posix()
        with closing(read_only(path)) as db:
            check_database(db)
            tables, = db.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='table'").fetchone()
        actual = digest(path)
        source = PINNED_DB_RECEIPTS.get(rel)
        if source is None and path.with_suffix(".receipt.json").is_file():
            meta = results(path.with_suffix(".receipt.json"))
            if "databaseSha256" in meta:
                source = (path.with_suffix(".receipt.json").relative_to(OUTPUT).as_posix(), "databaseSha256")
        if source is not None and expected_digest(*source) != actual:
            raise ValueError(f"local_db_differs_from_origin_receipt:{rel}")
        holdings[rel] = {"bytes": path.stat().st_size,"sha256": actual,"tables": tables,
                         "sqliteChecks": "ok", "matchingOriginReceipt": source[0] if source else None}
    if set(PINNED_DB_RECEIPTS) - holdings.keys():
        raise ValueError("missing_database_in_intake")
    result = {"geometryRevision": REVISION, "scope": "internal_readback", "checkedAtUtc":
              datetime.now(timezone.utc).isoformat(timespec="seconds"),
              "databases": holdings, "dbCount": len(holdings), "databaseBytesTotal": sum(d["bytes"] for d in holdings.values()),
              "seoulExampleChecks": examples, "nationalExampleCheck": "guri_sutaek3_dong_tile_and_households"}
    atomic_json(OUTPUT / "intake-readback.json", result)
    return {k:v for k,v in result.items() if k != "databases"}


if __name__ == "__main__":
    print(json.dumps(audit(), ensure_ascii=False, indent=2))
