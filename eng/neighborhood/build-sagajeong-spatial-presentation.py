#!/usr/bin/env python3
"""로컬 원본에서 사가정 공간 표현 오버레이 JSON을 만든다."""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

sys.dont_write_bytecode = True

from sagajeong_spatial_presentation import (
    SpatialPresentationError,
    build_overlay,
    load_json,
    require,
    require_manager_promotion_token,
    run_self_tests,
    write_json_deterministic,
)


def repository_path(repository_root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (repository_root / path).resolve()


def local_output_path(repository_root: Path, value: str) -> Path:
    path = repository_path(repository_root, value)
    local_root = (repository_root / "artifacts" / "local").resolve()
    try:
        path.relative_to(local_root)
    except ValueError as exc:
        raise SpatialPresentationError(f"OutputMustRemainUnderArtifactsLocal:{path}") from exc
    return path


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument(
        "--building-zip",
        default="artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip",
    )
    parser.add_argument(
        "--osm",
        default="artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm",
    )
    parser.add_argument("--base-map", default="")
    parser.add_argument("--unity-root", default=os.environ.get("SSALDDEL_UNITY_ROOT", ""))
    parser.add_argument("--mode", choices=("PrivateReview", "Promote"), default="PrivateReview")
    parser.add_argument("--license-receipt")
    parser.add_argument("--promotion-token", help=argparse.SUPPRESS)
    parser.add_argument("--output")
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    try:
        if args.self_test:
            print(json.dumps(run_self_tests(), ensure_ascii=False, sort_keys=True))
            return 0
        repository_root = Path(args.repository_root).resolve()
        if args.base_map:
            base_map_path = repository_path(repository_root, args.base_map)
        else:
            require(bool(args.unity_root), "UnityRootMissing:SetSSALDDEL_UNITY_ROOTOrBaseMap")
            base_map_path = (
                Path(args.unity_root).resolve()
                / "Assets"
                / "Ssalddel"
                / "Resources"
                / "SagajeongReference.json"
            )
        default_output = (
            "artifacts/local/sagajeong-spatial-presentation/.public.pending-direct.json"
            if args.mode == "Promote"
            else "artifacts/local/sagajeong-spatial-presentation/private-review.json"
        )
        output_path = local_output_path(repository_root, args.output or default_output)
        if args.mode == "Promote":
            require_manager_promotion_token(
                args.promotion_token,
                os.environ.get("SSALDDEL_SPATIAL_PROMOTION_TOKEN"),
            )
            require(".pending-" in output_path.name, "PromotionGate:OutputMustBePendingFile")
        license_receipt = (
            load_json(repository_path(repository_root, args.license_receipt))
            if args.license_receipt
            else None
        )
        document, statistics = build_overlay(
            repository_path(repository_root, args.building_zip),
            repository_path(repository_root, args.osm),
            base_map_path,
            mode="PromotionCandidate" if args.mode == "Promote" else "PrivateReview",
            license_receipt=license_receipt,
        )
        write_json_deterministic(output_path, document)
        print(
            json.dumps(
                {
                    "status": "Generated",
                    "mode": args.mode,
                    "output": str(output_path),
                    "buildingCount": len(document["buildings"]),
                    "surfaceCount": len(document["surfaces"]),
                    **statistics,
                },
                ensure_ascii=False,
                sort_keys=True,
            )
        )
        return 0
    except (SpatialPresentationError, RuntimeError, OSError, ValueError) as exc:
        print(f"SagajeongSpatialPresentationBuildFailed:{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
