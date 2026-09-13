from __future__ import annotations

import argparse
import json
from pathlib import Path

from sagajeong_mobility_graph import (
    EXPECTED_SOURCE_SHA256,
    MobilityGraphAuditError,
    MobilityGraphBlocked,
    build_package,
    write_package,
)


def main() -> int:
    parser = argparse.ArgumentParser(description="사가정 1km OSM 정적 이동망 검토 후보를 생성합니다.")
    parser.add_argument(
        "--source",
        type=Path,
        default=Path("artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm"),
    )
    parser.add_argument(
        "--output-directory",
        type=Path,
        default=Path("artifacts/local/sagajeong-mobility-graph"),
    )
    parser.add_argument("--expected-source-sha256", default=EXPECTED_SOURCE_SHA256)
    arguments = parser.parse_args()
    try:
        package = build_package(arguments.source, arguments.expected_source_sha256)
        result = write_package(package, arguments.output_directory)
    except MobilityGraphBlocked as error:
        print(json.dumps(error.report(), ensure_ascii=False, sort_keys=True, separators=(",", ":")))
        return 2
    except MobilityGraphAuditError as error:
        print(
            json.dumps(
                {
                    "status": "Rejected",
                    "reasonCode": str(error),
                    "runtimeAuthorized": False,
                },
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
        )
        return 1
    print(json.dumps(result, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
