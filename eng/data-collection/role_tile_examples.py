"""Read example role cards from the preparation DB and the unchanged evidence layers."""

import argparse
import json
from pathlib import Path
import re

from collect_public_snapshots import atomic_json
from dong_tile_bundle import database_path, query, tile_lookup
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from role_tile_catalog import CATALOG_DB, OUTPUT, SOURCES
from seoul_area_geography import gis


CORE = TILE_OUTPUT / DBNAME
SAGAJEONG = (-32, -179, 2)
LINE = {
    "libraryHours": "libraries", "riderRest": "rider_support", "deliveryArea": "delivery_area",
    "commercialArea": "commercial_geometry", "foodChurn": "food_churn", "elevator": "elevator_location",
    "motorcycleHotspot": "motorcycle", "seoulFreightParking": "seoul_freight_parking", "cropLand": "farmmap",
    "pedestrianEntrance": "pedestrian_entrance", "stationWeather": "station_weather",
}


def from_source(name, suffix="", parameters=(), projection="*"):
    path, relation, _source, _receipt = SOURCES[name]
    return query(path, f"SELECT {projection} FROM {relation} {suffix}", parameters)


def warehouse_points(admin_dongs, key, transform):
    counts = []
    districts = sorted({region["district_code"] for region in admin_dongs})
    candidates = from_source("warehouses", f"WHERE district_code IN ({','.join('?' for _ in districts)})",
                             districts, "observation_id,published_name,facility_type,latitude,longitude,"
                                        "district_code,observed_on,source_id,source_url")
    for row in candidates:
        tile = point_key(*transform(row.pop("longitude"), row.pop("latitude")))
        if tile[:len(key)] == key:
            counts.append({**row, "q": tile[0], "r": tile[1], "slot": tile[2]})
    return counts


def matching_region_rows(field, admin, legal):
    if field == "merchantQuarter":
        return from_source("merchant", "WHERE admin_code=? AND quarter='20262' "
                           "AND category_code='CS100010'", (admin,),
                           "admin_code,quarter,category_label,total_store_count,general_store_count,"
                           "opening_store_count,closure_store_count,franchise_store_count,"
                           "source_period_sales_krw,source_period_transaction_count,sales_release_id")
    if field == "salesShape":
        return from_source("sales_shape", "WHERE admin_code=? AND quarter='20262' "
                           "AND category_code='CS100010'", (admin,))
    if field == "workers":
        return from_source("workforce", "WHERE admin_code=? AND quarter='20262'", (admin,))
    if field == "mobilityDong":
        return from_source("mobility", "WHERE admin_code=? AND hour IN (12,18) "
                           "ORDER BY observed_date,direction,hour", (admin,))
    if field == "residentPopulation":
        return query(database_path(field), "SELECT * FROM resident_month WHERE admin_code=? "
                     "ORDER BY period_end DESC LIMIT 1", (admin,))
    if field in {"housingMarket", "landPrice", "buildingRegister"}:
        # A legal-dong observation can be shown in the region panel of more
        # than one administrative dong. Preserve the legal key and its level.
        selections = []
        for code in legal:
            if field == "landPrice":
                selections.extend(from_source("land_price", "WHERE legal_dong_code=?", (code,)))
            elif field == "housingMarket":
                path, relation, _src, _receipt = SOURCES["housing"]
                selections.extend(query(path, f"""SELECT legal_dong_code,deal_kind,housing_type,COUNT(*) AS records,
                    MIN(contract_date) AS first_report_date,MAX(contract_date) AS last_report_date
                    FROM {relation} WHERE legal_dong_code=? GROUP BY 1,2,3 ORDER BY 2,3""", (code,)))
            else:
                selections.extend(from_source("building_register", "WHERE region_code=? AND kind='legal_dong' "
                    "AND measure IN ('registered_dwellings','registered_family_units','registered_premises') "
                    "GROUP BY region_code,region_name,measure,unit,edition ORDER BY measure", (code,),
                    "region_code,region_name,measure,unit,edition,SUM(value_as_printed_sum) AS reported_sum,"
                    "SUM(valid_rows) AS valid_register_rows,SUM(missing_rows) AS missing_register_rows"))
        return selections
    return []


def legal_context(field, admin_dongs):
    reference_rows = {}
    matches = []
    keys = {
        "landPrice": ("source_id", "basis_date", "legal_dong_code"),
        "housingMarket": ("legal_dong_code", "housing_type", "deal_kind"),
        "buildingRegister": ("region_code", "edition", "measure"),
    }[field]
    for region in admin_dongs:
        code = region["admin_code"]
        legal = [row["legal_code"] for row in query(CORE, "SELECT legal_code FROM admin_legal "
                                                    "WHERE admin_code=? ORDER BY 1", (code,))]
        refs = []
        for observation in matching_region_rows(field, code, legal):
            ref = "/".join(str(observation[key]) for key in keys)
            reference_rows.setdefault(ref, observation)
            refs.append(ref)
        matches.append({"adminCode": code, "adminName": region["name"], "referenceKeys": refs})
    return {"nativeScope": "법정동", "rows": [{"referenceKey": key, **value} for key, value in reference_rows.items()],
            "regionContext": matches}


def distribution_context(legacy):
    rows = []
    for region in legacy["adminDongs"]:
        related = from_source("agri_support", "WHERE sgis_admin_dong_code=? "
            "AND map_candidate_state='working_preview_candidate_datum_unverified'", (region["admin_code"],),
            "source_row,facility_name,reference_date,map_candidate_state,comparison_status,sgis_admin_dong_code")
        if related:
            rows.append({"adminCode": region["admin_code"], "adminName": region["name"], "rows": related})
    return {"regionContext": rows}


def price_context(field):
    if field == "marketPriceContext":
        source = "seoul-garak-auction-15136861"
        label = "가락시장 공표 거래 참고"
        condition = "AND label LIKE '%배추%'"
    else:
        source = "koti-2025-trucking-owner-survey"
        label = "화물운송시장 조사의 공표 참고값"
        condition = ""
    return {"nativeScope": label, "rows": from_source("prices", "WHERE source_id=? " + condition +
        " ORDER BY period_end DESC,series_id,source_ref LIMIT 5", (source,),
        "source_id,region_label,label,metric,unit,currency,grain,period_start,period_end,value,basis,official_url")}


def observations(field, legacy, key, cache, transform):
    if field in cache:
        return cache[field]
    if field == "transitBoardings":
        values = {"rows": legacy.get("transitBoardingsAt12", []), "cut": "published noon table only"}
    elif field in legacy:
        values = {"rows": legacy[field]}
    elif field in LINE:
        condition = "WHERE q=? AND r=?" + (" AND slot=?" if len(key) == 3 else "")
        if field == "foodChurn":
            condition += " AND years=3"
        projection = ("id,name,code_system,area_code,area_name,source_as_of,operating_hours_label,official_url"
                      if field == "riderRest" else "*")
        values = {"rows": from_source(LINE[field], condition + " LIMIT 80", key, projection),
                  "rowLimit": 80, "cut": "3-year events" if field == "foodChurn" else None}
        if field == "motorcycleHotspot":
            hotspot_db = SOURCES["motorcycle"][0]
            hotspot_filter = "WHERE j.q=? AND j.r=?" + (" AND j.slot=?" if len(key) == 3 else "")
            values["sourceAreas"] = query(hotspot_db, """SELECT p.source_fid,p.reported_district,
                p.observation_start_year,p.observation_end_year,p.accidents,p.casualties,
                SUM(j.overlap_m2) AS area_intersection_m2
                FROM hotspot_child_overlap j JOIN v_hotspot_details p USING(source_fid) """
                + hotspot_filter + " GROUP BY p.source_fid ORDER BY p.source_fid", key)
        if field == "seoulFreightParking":
            parking_db = SOURCES["seoul_freight_parking"][0]
            schedules = []
            for city_row in sorted({row["city_row"] for row in values["rows"]}):
                schedules.extend(query(parking_db, "SELECT * FROM weekday_hours WHERE city_row=? "
                    "ORDER BY day_category", (city_row,)))
            values["publishedSchedules"] = schedules
    elif field == "stay":
        condition = "WHERE q=? AND r=?" + (" AND slot=?" if len(key) == 3 else "")
        condition += " AND duration_code='060' ORDER BY source_key,day_kind,band,slot"
        values = {"rows": from_source("stay", condition, key), "cut": "60–119 minutes observed stay-duration band"}
    elif field == "warehouseLocations":
        values = {"rows": warehouse_points(legacy["adminDongs"], key, transform)}
    elif field == "agriDistribution":
        values = distribution_context(legacy)
    elif field == "freightDepot":
        values = {"state": "seoul_not_listed_in_this_nlic_release", "rows": None}
    elif field in {"marketPriceContext", "freightPrice"}:
        values = price_context(field)
    elif field in {"publicDeliveryCity", "residentFoodSurvey"}:
        label = "public_delivery" if field == "publicDeliveryCity" else "resident_food_survey"
        values = {"nativeScope": "서울시", "rows": from_source(label, "LIMIT 25")}
    elif field in {"housingMarket", "landPrice", "buildingRegister"}:
        values = legal_context(field, legacy["adminDongs"])
    elif field in {"merchantQuarter", "salesShape", "workers", "mobilityDong", "residentPopulation"}:
        values = {"regionContext": []}
        for region in legacy["adminDongs"]:
            admin = region["admin_code"]
            rows = matching_region_rows(field, admin, ())
            values["regionContext"].append({"adminCode": admin, "adminName": region["name"], "rows": rows})
    else:
        values = {"rows": None, "state": "available_at_source_not_selected_in_example"}
    cache[field] = values
    return values


def case_state(field, values):
    if "state" in values:
        return values["state"]
    if "regionContext" in values or "nativeScope" in values:
        if values.get("rows") or any(r.get("rows") for r in values.get("regionContext", [])):
            return "native_region_context"
        return "no_matching_region_observation"
    rows = values.get("rows")
    if not rows:
        return "not_observed_at_this_key"
    if field in {"presence", "stay"}:
        value_fields = {"presence": ("allocated_visible_mean", "known_presence_average"),
                        "stay": ("mean_known_allocated_per_start_hour",)}[field]
        if any(row.get(measure) is not None for row in rows for measure in value_fields):
            mask_names = ("masked_or_partial_weighted_grid_hours", "suppressed_sample_hours", "masked_hours")
            if any((row.get(mask) or 0) > 0 for row in rows for mask in mask_names):
                return "published_component_allocated_with_masking"
            return "published_component_allocated"
        return "numeric_component_not_available"
    if field == "roadSpeed" and not any(row["mean_daily_weighted_kmh"] is not None for row in rows):
        return "no_speed_value_at_this_key"
    if field == "cropLand" and not any(row["cultivated_union_m2"] for row in rows):
        return "no_qualifying_crop_geometry_reported"
    return "reference_returned"


def case(key, roles, output=OUTPUT):
    legacy = tile_lookup(*key)
    result = {
        "geometryRevision": REVISION, "tileKey": list(key), "geoState": legacy["geoState"],
        "intersectingDongs": legacy.get("adminDongs", []), "roles": {}, "kind": "local_review_case",
        "displayMeaning": "여기에는 자료 조회를 기록한다. 화면 적용·등급·운영 명령의 실행 근거는 별도다.",
    }
    db = output / CATALOG_DB
    pyproj, _shapely = gis()
    convert = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    cache = {}
    for role in roles:
        if not re.fullmatch(r"[a-z]+", role):
            raise ValueError("invalid_role_identifier")
        fields = query(db, "SELECT * FROM v_role_tile_inventory WHERE role_id=? "
                       "ORDER BY CASE phase WHEN 'initial' THEN 0 WHEN 'detail' THEN 1 ELSE 2 END,position", (role,))
        if not fields:
            raise ValueError(f"unknown_role:{role}")
        result["roles"][role] = {"label": fields[0]["role_name"], "question": fields[0]["question"], "layers": []}
        for row in fields:
            field = row["field_key"]
            item = {"id": field, "label": row["label"], "phase": row["phase"],
                    "availability": row["availability"], "evidenceClass": row["evidence_class"],
                    "nativeGrain": row["native_grain"], "unit": row["unit"], "visual": row["display_kind"]}
            if row["database_path"] is None:
                item.update(state=row["availability"], rows=None)
            elif legacy["geoState"] == "outside_seoul":
                item.update(state="outside_this_seoul_case_pack", rows=None)
            else:
                found = observations(field, legacy, key, cache, convert)
                item.update(state=case_state(field, found), **{name: value for name, value in found.items() if name != "state"})
            result["roles"][role]["layers"].append(item)
    return result


def build_examples(output):
    example_dir = output / "role-cases"
    example_dir.mkdir(exist_ok=True)
    all_roles = [line["id"] for line in query(output / CATALOG_DB, "SELECT id FROM role ORDER BY menu_order")]
    sample_library = from_source("libraries", "WHERE name LIKE '중랑%' ORDER BY report_id LIMIT 1", projection="q,r,slot")[0]
    sample_farm = from_source("farmmap", "ORDER BY cultivated_union_m2 DESC,q,r,slot LIMIT 1", projection="q,r,slot")[0]
    sample_parking = from_source("seoul_freight_parking", "ORDER BY q,r,slot LIMIT 1", projection="q,r,slot")[0]
    sample_garak = query(CORE, "SELECT q,r,slot FROM transport_child_position WHERE mode='rail' "
                         "AND name='가락시장' ORDER BY q,r,slot LIMIT 1")[0]
    sample_hotspot = from_source("motorcycle", "ORDER BY public_hotspot_count DESC,q,r,slot LIMIT 1",
                                 projection="q,r,slot")[0]
    outside = query(CORE, "SELECT q,r,slot FROM child_tile WHERE seoul_area_m2=0 ORDER BY q,r,slot LIMIT 1")[0]
    samples = (
        ("sagajeong-same-child-nine-roles", SAGAJEONG, all_roles),
        ("hongdae-three-roles", (-75, -189, 3), ["rider", "store", "resident"]),
        ("yeouido-three-roles", (-68, -204, 2), ["store", "cargo", "owner"]),
        ("yongmasan-slope", (-28, -183, 2), ["rider", "resident", "commander"]),
        ("jungnang-rider-rest", (-38, -172, 5), ["rider"]),
        ("rider-overlapping-historical-hotspots", tuple(sample_hotspot.values()), ["rider"]),
        ("jungnang-library", tuple(sample_library.values()), ["resident"]),
        ("farmmap-strong-case", tuple(sample_farm.values()), ["farm"]),
        ("garak-wholesale", tuple(sample_garak.values()), ["wholesale"]),
        ("seoul-shared-curb-parking", tuple(sample_parking.values()), ["cargo"]),
        ("outside-seoul-missing", tuple(outside.values()), ["rider", "store", "owner"]),
    )
    report = []
    for name, key, roles in samples:
        output_path = example_dir / (name + ".json")
        payload = case(key, roles, output)
        atomic_json(output_path, payload)
        initial = {role: [field["id"] for field in row["layers"] if field["phase"] == "initial"]
                   for role, row in payload["roles"].items()}
        report.append({"file": output_path.name, "key": key, "geoState": payload["geoState"], "initialFields": initial})
    atomic_json(example_dir / "index.json", report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--tile", nargs=3, type=int)
    parser.add_argument("--role", action="append")
    args = parser.parse_args()
    if args.tile:
        print(json.dumps(case(tuple(args.tile), args.role or ["store"], args.output), ensure_ascii=False, indent=2))
    else:
        print(json.dumps(build_examples(args.output), ensure_ascii=False))


if __name__ == "__main__":
    main()
