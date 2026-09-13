#!/usr/bin/env python3
"""동결 원천으로 용마산역 중심 1km 실제 공간 사본을 만든다."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True

from station_spatial_snapshot import (
    SpatialPresentationError,
    build_snapshot,
    local_output_path,
    run_self_tests,
    write_json_deterministic,
    yongmasan_descriptor,
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument(
        "--building-zip",
        default="artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip",
    )
    parser.add_argument(
        "--nodelink-zip",
        default="artifacts/local/public-data/sagajeong-nodelink-20260912-r1/nodelink.zip",
    )
    parser.add_argument(
        "--nodelink-source-directory",
        default="artifacts/local/public-data/sagajeong-nodelink-20260912-r1/source-link",
    )
    parser.add_argument(
        "--administrative-boundary",
        default=(
            "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/"
            "seoul-administrative-dong-boundary.zip"
        ),
    )
    parser.add_argument(
        "--osm",
        default="artifacts/local/neighborhood-source-acquisition/yongmasan-station-0723-r1/map.osm",
    )
    parser.add_argument(
        "--osm-receipt",
        default="artifacts/local/neighborhood-source-acquisition/yongmasan-station-0723-r1/receipt.json",
    )
    parser.add_argument(
        "--output",
        default="artifacts/local/yongmasan-station-spatial-snapshot/private-review.json",
    )
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    try:
        if args.self_test:
            print(json.dumps(run_self_tests(), ensure_ascii=False, sort_keys=True))
            return 0
        repository_root = Path(args.repository_root).resolve()
        descriptor = yongmasan_descriptor(
            building_path=args.building_zip,
            nodelink_path=args.nodelink_zip,
            nodelink_source_directory=args.nodelink_source_directory,
            administrative_boundary_path=args.administrative_boundary,
            osm_path=args.osm,
            osm_receipt_path=args.osm_receipt,
        )
        output_path = local_output_path(repository_root, args.output)
        document, statistics = build_snapshot(repository_root, descriptor)
        write_json_deterministic(output_path, document)
        print(
            json.dumps(
                {
                    "status": "GeneratedLocalPrivateReview",
                    "output": str(output_path),
                    "revision": document["revision"],
                    "contentHash": document["contentHash"],
                    "buildingCount": len(document["buildings"]),
                    "roadCount": len(document["roads"]),
                    "surfaceCount": len(document["surfaces"]),
                    "administrativeAreaCount": len(document["administrativeAreas"]),
                    "tileCount": len(document["tiles"]),
                    "coverage": document["coverage"]["summary"],
                    "missingCoverageCodes": sorted(
                        {item["code"] for item in document["missingCoverage"]}
                    ),
                    "statistics": statistics,
                    "distributionApproved": False,
                    "gameplayReady": False,
                    "traversalReady": False,
                },
                ensure_ascii=False,
                sort_keys=True,
            )
        )
        return 0
    except (SpatialPresentationError, RuntimeError, OSError, ValueError) as exc:
        print(f"YongmasanStationSpatialSnapshotBuildFailed:{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
