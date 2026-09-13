#!/usr/bin/env python3
"""사가정 공간 표현 오버레이와 100 m coverage를 독립 감사한다."""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

sys.dont_write_bytecode = True

from sagajeong_spatial_presentation import (
    SpatialPresentationError,
    PROMOTION_CANDIDATE_REVISION,
    PROMOTION_PENDING_STATUS,
    PUBLIC_REVISION,
    approve_promotion_candidate,
    build_coverage_audit,
    independently_verify_overlay_sources,
    load_json,
    render_coverage_html,
    require,
    require_manager_promotion_token,
    run_self_tests,
    sha256_file,
    validate_promotion_receipt,
    validate_overlay_document,
    write_json_deterministic,
    write_text_atomic,
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
        raise SpatialPresentationError(f"AuditOutputMustRemainUnderArtifactsLocal:{path}") from exc
    return path


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument(
        "--overlay",
        default="artifacts/local/sagajeong-spatial-presentation/private-review.json",
    )
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
    parser.add_argument(
        "--audit-json",
        default="artifacts/local/sagajeong-spatial-presentation/coverage-audit.json",
    )
    parser.add_argument(
        "--audit-html",
        default="artifacts/local/sagajeong-spatial-presentation/coverage-audit.html",
    )
    parser.add_argument("--promotion-receipt")
    parser.add_argument("--promotion-token", help=argparse.SUPPRESS)
    parser.add_argument("--approved-output", help=argparse.SUPPRESS)
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    approved_output_path: Path | None = None
    approved_output_written = False
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
        overlay_path = repository_path(repository_root, args.overlay)
        document = load_json(overlay_path)
        promotion_receipt = (
            load_json(repository_path(repository_root, args.promotion_receipt))
            if args.promotion_receipt
            else None
        )
        frame, buildings, surfaces, legacy_buildings = validate_overlay_document(
            document,
            repository_path(repository_root, args.building_zip),
            repository_path(repository_root, args.osm),
            base_map_path,
        )
        source_audit = independently_verify_overlay_sources(
            document,
            repository_path(repository_root, args.building_zip),
            repository_path(repository_root, args.osm),
            base_map_path,
            frame,
        )
        promotion_review = None
        audit_document = document
        audit_overlay_hash = sha256_file(overlay_path)
        if document["revision"] == PROMOTION_CANDIDATE_REVISION:
            require(document["status"] == PROMOTION_PENDING_STATUS, "PromotionGate:CandidateStatusInvalid")
            require_manager_promotion_token(
                args.promotion_token,
                os.environ.get("SSALDDEL_SPATIAL_PROMOTION_TOKEN"),
            )
            require(bool(args.approved_output), "PromotionGate:ApprovedTemporaryOutputMissing")
            approved_output_path = local_output_path(repository_root, args.approved_output)
            require(
                ".approved-" in approved_output_path.name
                and approved_output_path.name.endswith(".tmp"),
                "PromotionGate:ApprovedOutputMustBeTemporary",
            )
            audit_document = approve_promotion_candidate(document, promotion_receipt)
            promotion_review = document["promotionReview"]
            validate_overlay_document(
                audit_document,
                repository_path(repository_root, args.building_zip),
                repository_path(repository_root, args.osm),
                base_map_path,
            )
            write_json_deterministic(approved_output_path, audit_document)
            approved_output_written = True
            audit_overlay_hash = sha256_file(approved_output_path)
        else:
            require(not args.approved_output, "PromotionGate:ApprovedOutputOnlyForCandidate")
            if document["revision"] == PUBLIC_REVISION:
                validated_receipt = validate_promotion_receipt(promotion_receipt)
                promotion_review = {
                    "reviewer": validated_receipt["reviewer"],
                    "reviewedAtUtc": validated_receipt["reviewedAtUtc"],
                    "evidenceHash": validated_receipt["evidenceHash"],
                }
        audit = build_coverage_audit(
            audit_document,
            audit_overlay_hash,
            frame,
            source_audit["buildings"],
            source_audit["surfaces"],
            source_audit["legacyBuildings"],
            source_audit["omittedSourceFeatures"],
            source_audit["summary"],
            promotion_review,
        )
        audit_json_path = local_output_path(repository_root, args.audit_json)
        audit_html_path = local_output_path(repository_root, args.audit_html)
        html_text = render_coverage_html(audit)
        write_json_deterministic(audit_json_path, audit)
        write_text_atomic(audit_html_path, html_text)
        print(
            json.dumps(
                {
                    "status": audit["status"],
                    "auditJson": str(audit_json_path),
                    "auditHtml": str(audit_html_path),
                    "auditHash": audit["auditHash"],
                    **audit["summary"],
                },
                ensure_ascii=False,
                sort_keys=True,
            )
        )
        return 0
    except (SpatialPresentationError, RuntimeError, OSError, ValueError, KeyError, TypeError) as exc:
        if approved_output_written and approved_output_path is not None:
            approved_output_path.unlink(missing_ok=True)
        print(f"SagajeongSpatialPresentationAuditFailed:{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
