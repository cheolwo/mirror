"""Resolve building use labels from the matching GIS source edition."""

from collections import defaultdict
from contextlib import closing
import json

from collect_public_snapshots import atomic_json
from dong_tile_buildings import OUTPUT, SOURCE_RELEASE
from seoul_area_geography import read_only
from seoul_tile_population import PUBLIC, ROOT, digest


PROFILE = PUBLIC / "diorama-public-layer-intake-20261123/r56-building-profiles-local/seoul-building-profile-local.sqlite"


def build():
    labels = defaultdict(dict)
    with closing(read_only(PROFILE)) as original:
        for code, name, rows in original.execute("""SELECT raw_use_code,raw_use_name,SUM(records)
            FROM observed_building_profile WHERE level='sido' AND raw_use_code<>'(all)'
            GROUP BY 1,2 ORDER BY 1,2"""):
            if code and name:
                labels[code][name] = rows
    index = OUTPUT / "building-tile-index.sqlite"
    with closing(read_only(index)) as target:
        counts = dict(target.execute("SELECT use_code,SUM(building_count) FROM building_child_by_use GROUP BY 1"))
    result = []
    for code, count in sorted(counts.items()):
        candidates = labels.get(code, {})
        state = ("one_label_in_matching_source" if len(candidates) == 1 else
                 "multiple_labels_in_source" if candidates else "source_label_missing")
        result.append({"useCode": code, "indexedBuildings": count, "state": state,
                       "reportedLabel": next(iter(candidates)) if len(candidates) == 1 else None,
                       "observedSourceLabels": candidates})
    output = {"sourceRelease": SOURCE_RELEASE,"meaning": "original_code_labels_observed_in_gis_edition",
              "officialSource": "https://www.data.go.kr/data/15083092/fileData.do",
              "labelProfilePath": PROFILE.relative_to(ROOT).as_posix(), "labelProfileSha256": digest(PROFILE),
              "targetDbSha256": digest(index), "distribution": "local-review",
              "summedIndexedBuildings": sum(counts.values()), "codes": result,
              "uniqueMappedCodes": sum(row["reportedLabel"] is not None for row in result),
              "uniqueMappedBuildings": sum(row["indexedBuildings"] for row in result if row["reportedLabel"] is not None)}
    atomic_json(OUTPUT / "building-use-labels.json", output)
    return {k:v for k,v in output.items() if k != "codes"}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
