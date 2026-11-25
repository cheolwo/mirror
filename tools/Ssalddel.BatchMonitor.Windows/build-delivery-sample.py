"""Export a local WPF demo sample from verified delivery records, without changing the sources."""

import argparse
import hashlib
import json
import re
import sqlite3
from contextlib import closing
from datetime import datetime
from pathlib import Path


# These are the verified public store points chosen for the companion road graph.
STORE_POINTS = ((37.580093, 127.088016), (37.580202, 127.089298), (37.580534, 127.087419))
ORDERS_PER_STORE = 4
PHONE_PATTERN = re.compile(r"(?<!\d)0\d{1,2}[- .]?\d{3,4}[- .]?\d{4}(?!\d)")
STORE_ORDERS_QUERY = """
    SELECT DISTINCT b.public_store_name, d.id, d.day, i.item_index, m.name
    FROM visit_map_branches b
    JOIN visit_map_records r ON r.branch_id = b.id AND r.completion_state = 'completed'
    JOIN deliveries d ON d.id = r.delivery_id AND d.store_id = b.store_id
    JOIN participant_delivery_links l ON l.delivery_id = d.id AND l.scope = 'app'
    JOIN delivery_items i ON i.delivery_id = d.id AND i.complete = 1
    JOIN menus m ON m.id = i.menu_id AND m.store_id = d.store_id
    WHERE b.held_reason IS NULL
      AND b.coordinate_evidence = 'inherited_digest_verified_public_building_point'
      AND b.address_evidence = 'accepted_recorded_branch_public_address'
      AND ROUND(b.latitude, 6) = ? AND ROUND(b.longitude, 6) = ?
      AND d.scope = 'app' AND d.order_complete = 1 AND d.status = '전달완료'
    ORDER BY d.day DESC, d.id, i.item_index, m.name
"""


def open_readonly(path):
    return closing(sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True))


def public_label(value):
    label = " ".join(value.split()) if isinstance(value, str) else ""
    if not label or len(label) > 80 or "@" in label or PHONE_PATTERN.search(label):
        return None
    return label


def catalog_path(delivery_root):
    directory = delivery_root / "edit-kit/private/shared-store-menu-db"
    pointer = json.loads((directory / "latest.json").read_text(encoding="utf-8-sig"))
    database = (directory / pointer["database"]).resolve()
    if not database.is_relative_to(directory.resolve()):
        raise ValueError("catalog_path_outside_source_directory")
    with database.open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest() != pointer["database_sha256"]:
            raise ValueError("catalog_fingerprint_mismatch")
    return database


def read_orders(database):
    stores, order_groups = [], []
    available = 0
    with open_readonly(database) as db:
        if db.execute("SELECT count(*) FROM participant_actors").fetchone()[0] != 1:
            raise ValueError("single_participant_projection_required")

        for index, (latitude, longitude) in enumerate(STORE_POINTS, 1):
            store_id = f"store-{index}"
            names, orders = set(), {}
            for name, delivery_id, _, _, menu in db.execute(STORE_ORDERS_QUERY, (latitude, longitude)):
                name, menu = public_label(name), public_label(menu)
                if name and menu:
                    names.add(name)
                    orders.setdefault(delivery_id, {"storeId": store_id, "menu": menu})
            if len(names) != 1 or not orders:
                raise ValueError("verified_store_and_menu_required")
            available += len(orders)
            stores.append({"id": store_id, "name": names.pop(), "latitude": latitude, "longitude": longitude})
            order_groups.append(list(orders.items())[:ORDERS_PER_STORE])

    selected = {}
    for offset in range(ORDERS_PER_STORE):
        for group in order_groups:
            if offset < len(group):
                delivery_id, order = group[offset]
                selected[delivery_id] = order
    if len(selected) < 9:
        raise ValueError("too_few_verified_sample_orders")
    return stores, selected, available


def catalog_app_id(source_id):
    # Same internal projection key as delivery-records/unified_catalog_app_projection.py.
    encoded = json.dumps([source_id], ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    return "app-" + hashlib.sha256(encoded).hexdigest()[:24]


def add_observed_context(database, selected):
    marks = ",".join("?" for _ in selected)
    with open_readonly(database) as db:
        db.create_function("catalog_app_id", 1, catalog_app_id, deterministic=True)
        rows = db.execute(
            f"""
            SELECT catalog_app_id(id),
              CASE WHEN json_valid(order_json) THEN json_extract(order_json, '$.order_time') END
            FROM app_deliveries
            WHERE order_complete = 1 AND catalog_app_id(id) IN ({marks})
            """,
            tuple(selected),
        )
        for delivery_id, ordered_at in rows:
            if isinstance(ordered_at, str):
                try:
                    observed = datetime.strptime(ordered_at, "%Y-%m-%d %H:%M:%S")
                except ValueError:
                    continue
                selected[delivery_id]["observedTime"] = f"{observed.hour:02d}시대"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--delivery-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        stores, selected, available = read_orders(catalog_path(args.delivery_root))
        add_observed_context(args.delivery_root / "delivery-records/delivery-app-observations.sqlite3", selected)
        orders = list(selected.values())
        content = json.dumps({"stores": stores, "orders": orders}, ensure_ascii=False, indent=2) + "\n"
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with args.output.open("x", encoding="utf-8", newline="\n") as output:
            output.write(content)
    except (OSError, ValueError, KeyError, sqlite3.Error) as error:
        parser.exit(1, f"Sample export failed ({type(error).__name__}).\n")
    print(json.dumps({
        "stores": len(stores),
        "eligibleOrders": available,
        "exportedOrders": len(orders),
        "withObservedTimeBucket": sum("observedTime" in order for order in orders),
    }))


if __name__ == "__main__":
    main()
