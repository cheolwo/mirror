"""Compare numbered public Sagajeong entrance candidates without exporting approved coordinates."""

from contextlib import closing
import gzip
from itertools import permutations
import json
from math import dist

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import OUTPUT
from seoul_area_geography import gis, read_only
from seoul_tile_population import LOCAL, PUBLIC, digest


OSM_REVIEW = OUTPUT / "stations/osm-review"
APPROVED = LOCAL / "hongdal-unity-claude-20261112/dong-bake-seoul-official-20261118-r2/11260/inputs/juso-extras.json.gz"
PC_RECEIPT = PUBLIC / "diorama-transit-access-pc-only-20261123/verification.json"
OLD = PUBLIC / "diorama-public-layer-intake-20261123/r20-transit-access-evidence/transit-access-evidence.sqlite"
CANOPY = PUBLIC / "diorama-public-layer-intake-20261123/r9-station-services/station-services.sqlite"


def review():
    pyproj, _ = gis()
    source = OSM_REVIEW / "sagajeong-entrance-candidates.json"
    osm = json.loads(source.read_text(encoding="utf-8"))
    scope, = [r for r in osm["stopAreaRelations"] if r["name"] == "사가정"]
    nodes = sorted((r for r in osm["nodes"] if r["id"] in scope["stationMemberIds"]),
                   key=lambda row: row["tags"]["ref"])
    if [row["tags"]["ref"] for row in nodes] != ["1","2","3","4"]:
        raise ValueError("named_station_and_number_set_not_unique")
    expected = json.loads(PC_RECEIPT.read_text(encoding="utf-8"))["hashes"]
    if digest(APPROVED) != expected["jungnang_bake_input"] or digest(OLD) != expected["public_reference"]:
        raise ValueError("approved_spatial_lineage_mismatch")
    with gzip.open(APPROVED, "rt", encoding="utf-8-sig") as stream:
        overlay = json.load(stream)
    frame = overlay["coordinateFrame"]
    if frame["method"] != "WGS84-ECEF-ENU-at-zero-altitude":
        raise ValueError("unexpected_station_overlay_coordinate_frame")
    local = pyproj.Transformer.from_pipeline(
        f"+proj=pipeline +step +proj=cart +ellps=WGS84 +step +proj=topocentric "
        f"+ellps=WGS84 +lat_0={frame['originLatitude']} +lon_0={frame['originLongitude']} +h_0=0")
    tile = pyproj.Transformer.from_crs(4326,5179,always_xy=True)
    points = [(float(n["lon"]),float(n["lat"])) for n in nodes]
    enu = [local.transform(lon,lat,0,errcheck=True)[:2] for lon,lat in points]
    with closing(read_only(OLD)) as history:
        archived = history.execute("""SELECT longitude,latitude FROM historical_unnumbered_exit
             WHERE metro_station_code='2724' ORDER BY longitude,latitude""").fetchall()
    if len(archived)!=4:
        raise ValueError("unexpected_historical_station_record_count")
    comparison_points = [local.transform(lon,lat,0,errcheck=True)[:2] for lon,lat in archived]
    closest = min(permutations(comparison_points), key=lambda places: sum(dist(a,b) for a,b in zip(enu,places)))
    with closing(read_only(CANOPY)) as assets:
        canopy = dict(assets.execute("SELECT exit_number,entry_width_m FROM canopy WHERE line='7' AND name='사가정'"))
    approved = [(str(number), (x,y)) for dong in overlay["dongs"].values()
                for x,y,number in dong["stationEntrances"]]
    comparisons = []
    for node, geographic, p, historical in zip(nodes,points,enu,closest):
        ref = node["tags"]["ref"]
        ranked = sorted(dist(p,pos) for number,pos in approved if number==ref)
        if not ranked:
            raise ValueError("approved_same_number_is_missing")
        comparisons.append({"number":ref,"osmNodeId":node["id"],"osmNodeUpdatedAt":node["updatedAt"],
            "tileKey":point_key(*tile.transform(*geographic,errcheck=True)),
            "publishedCanopyEntryWidthM":canopy.get(ref) or canopy.get(int(ref)),
            "closestApprovedPointWithSameNumberM":round(ranked[0],2),
            "nextApprovedPointWithSameNumberM":round(ranked[1],2) if len(ranked)>1 else None,
            "paired2020UnnamedPointDistanceM":round(dist(p,historical),2),
            "officialStationExitIdVerified":False})
    result = {"geometryRevision":REVISION,"operatorStationCode":"2724","name":"사가정",
              "publicCandidateSource":"OSM community stop_area", "osmRelationId":scope["id"],
              "license":"ODbL - © OpenStreetMap contributors", "samePcComparisonOnly":True,
              "actualUseOrRoutingAuthority":False,"ownSourceMetadataSha256":digest(source),
              "sourceOverlaySha256":expected["jungnang_bake_input"], "publicReferenceSha256":expected["public_reference"],
              "officialPrivateCoordinatesExported":False,
              "distanceBetweenCommunityExitsThreeAndFourM":round(dist(enu[2],enu[3]),2),
              "candidates":comparisons}
    atomic_json(OSM_REVIEW / "sagajeong-exit-readback.json", result)
    return result


if __name__ == "__main__":
    print(json.dumps(review(),ensure_ascii=False,indent=2))
