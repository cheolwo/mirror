#!/usr/bin/env python3
"""사가정 화면 건물의 결속·주소 후보 증거를 기존 형상과 분리해 생성한다."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import zipfile
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable


AL_D010_HASH = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755"
OVERLAY_FILE_HASH = "3EC6B95DC083F13F99042989E0B58957B3EE031E06C501F94022BE455DEC0A44"
OVERLAY_CONTENT_HASH = "138D1A47B286CE350CF339C1F69C6FFAD7778EBA7B6C95F3CF9F172F8248EBB1"
REFERENCE_MAP_HASH = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3"
REFERENCE_ADDRESS_HASH = "86D318AFDC34BD3AF0E6470CCD49F29692A3F71B888FA0252C0B4AAAC84C80BF"
MOIS_ZIP_HASH = "4AA70C569AAF14F550313B5836346A1BA438491FB4CEC61877E615C9E602AFA7"
MOIS_BUILDING_HASH = "285286D60409E1CC93898146952D9BC4A805C8F955A765C9F0D72E8A6EA16081"
MOIS_RELATED_PARCEL_HASH = "DF8DBCA1823F1404CDE056C2E2255D97B8525164B2E7BEE4B52A0754B7519555"

EXPECTED_BINDING_CONTENT_HASH = "7B47519C1FC9D4D89A04B734710F48FE64BFE8C0EEC9264C947D5DC6206D1411"
EXPECTED_ADDRESS_CONTENT_HASH = "9908B700914DC9CB9AB22592E96A5A699FA3AE108F90063DB010CC9C8B870C33"
EXPECTED_BINDING_FILE_HASH = "A11460EB08BA3D27188AE2BD5FFA7E3BCA22861140EB2973EE869078B5F17149"
EXPECTED_ADDRESS_FILE_HASH = "E751344083E98C28E3F4D6B153FA71E7679A7C47534D71B9721DEBE467596984"

STATION_STABLE_ID = "station:kr:kric:s1107:0722"
REGION_STABLE_ID = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer"
BINDING_REVISION = "sagajeong-presentation-building-binding.r1"
ADDRESS_REVISION = "sagajeong-presentation-building-address-assignment.r1"
SOURCE_VINTAGE = "202608"
BOUNDARY = {
    "observationPresentationOnly": True,
    "distributionApproved": False,
    "deliveryEligible": False,
    "priceObservationEligible": False,
    "businessLocationEligible": False,
    "unityApplyAllowed": False,
    "traversalReady": False,
    "gameplayReady": False,
}


def require(condition: bool, code: str) -> None:
    if not condition:
        raise ValueError(f"SagajeongPresentationBuildingEvidence:{code}")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def content_hash(value: dict[str, Any]) -> str:
    return hashlib.sha256(canonical_bytes(value)).hexdigest().upper()


def read_json(path: Path, expected_hash: str | None = None) -> dict[str, Any]:
    require(path.is_file(), f"InputMissing:{path.name}")
    if expected_hash:
        require(sha256_file(path) == expected_hash, f"InputHashChanged:{path.name}")
    value = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(value, dict), f"JsonObjectExpected:{path.name}")
    return value


def dbf_rows(zip_path: Path, wanted_ids: set[str]) -> dict[str, dict[str, str]]:
    require(sha256_file(zip_path) == AL_D010_HASH, "AlD010HashChanged")
    with zipfile.ZipFile(zip_path, "r") as archive:
        names = [name for name in archive.namelist() if name.endswith("AL_D010_11_20260809.dbf")]
        require(len(names) == 1, "AlD010DbfEntry")
        with archive.open(names[0], "r") as stream:
            header = stream.read(32)
            require(len(header) == 32, "DbfHeaderTruncated")
            record_count = struct.unpack_from("<I", header, 4)[0]
            header_length = struct.unpack_from("<H", header, 8)[0]
            record_length = struct.unpack_from("<H", header, 10)[0]
            require(
                record_count == 695_761 and header_length == 961 and record_length == 1815,
                "DbfHeaderContract",
            )
            fields: dict[str, tuple[int, int]] = {}
            offset = 1
            for index in range(29):
                descriptor = stream.read(32)
                require(len(descriptor) == 32, "DbfFieldDescriptorTruncated")
                name = descriptor[:11].split(b"\0", 1)[0].decode("ascii")
                length = descriptor[16]
                require(name == f"A{index}", f"DbfFieldContract:{index}")
                fields[name] = (offset, length)
                offset += length
            require(stream.read(1) == b"\r" and offset == record_length, "DbfLayout")

            def text(record: bytes, field: str, encoding: str = "ascii") -> str:
                start, length = fields[field]
                return record[start : start + length].decode(encoding, errors="strict").strip(" \0")

            output: dict[str, dict[str, str]] = {}
            for _ in range(record_count):
                record = stream.read(record_length)
                require(len(record) == record_length, "DbfRecordTruncated")
                require(record[0] in (0x20, 0x2A), "DbfDeletionMarker")
                if record[0] == 0x2A:
                    continue
                source_id = text(record, "A1")
                if source_id not in wanted_ids:
                    continue
                require(source_id not in output, f"DuplicateSourceFeature:{source_id}")
                output[source_id] = {
                    "sourceFeatureId": source_id,
                    "parcelIdentifierPnu": text(record, "A2"),
                    "legalDongCode": text(record, "A3"),
                    "jibun": text(record, "A5"),
                    "sourceBuildingIdentifier": text(record, "A19"),
                    "sourceUfid": text(record, "A21"),
                }
    require(set(output) == wanted_ids, "PresentationBuildingSourceCoverage")
    return output


def parcel_identifier(fields: list[str]) -> str:
    require(len(fields) >= 13, "OfficialRowColumns")
    require(re.fullmatch(r"\d{10}", fields[0]) is not None, "OfficialLegalDongCode")
    require(fields[5] in ("0", "1"), "OfficialMountainFlag")
    require(fields[6].isdigit() and fields[7].isdigit(), "OfficialParcelNumber")
    return f"{fields[0]}{int(fields[5]) + 1}{int(fields[6]):04d}{int(fields[7]):04d}"


def official_composite_key(fields: list[str]) -> str:
    require(len(fields) >= 13, "OfficialAddressColumns")
    require(fields[10] in ("0", "1", "2", "3"), "OfficialUndergroundCode")
    require(fields[11].isdigit() and fields[12].isdigit(), "OfficialBuildingNumber")
    return "|".join((fields[0], fields[8], fields[10], f"{int(fields[11]):05d}", f"{int(fields[12]):05d}"))


def related_parcel_composite_key(fields: list[str]) -> str:
    require(len(fields) >= 12, "RelatedAddressColumns")
    require(fields[9] in ("0", "1", "2", "3"), "RelatedUndergroundCode")
    require(fields[10].isdigit() and fields[11].isdigit(), "RelatedBuildingNumber")
    return "|".join((fields[0], fields[8], fields[9], f"{int(fields[10]):05d}", f"{int(fields[11]):05d}"))


def canonical_road_address(fields: list[str]) -> str:
    prefix = {"0": "", "1": "지하 ", "2": "공중 ", "3": "수상 "}[fields[10]]
    number = str(int(fields[11]))
    if int(fields[12]) != 0:
        number += f"-{int(fields[12])}"
    return f"서울특별시 중랑구 {fields[9]} {prefix}{number}"


def load_official_candidates(
    building_path: Path, related_path: Path
) -> tuple[dict[str, set[str]], dict[str, dict[str, Any]], dict[tuple[str, str], set[str]]]:
    require(sha256_file(building_path) == MOIS_BUILDING_HASH, "MoisBuildingHashChanged")
    require(sha256_file(related_path) == MOIS_RELATED_PARCEL_HASH, "MoisRelatedParcelHashChanged")
    parcel_to_keys: dict[str, set[str]] = defaultdict(set)
    key_info: dict[str, dict[str, Any]] = {}
    methods: dict[tuple[str, str], set[str]] = defaultdict(set)
    official_rows = 0
    with building_path.open("r", encoding="cp949", errors="strict") as stream:
        for raw_line in stream:
            fields = raw_line.rstrip("\r\n").split("|")
            require(len(fields) >= 31, "OfficialBuildingColumns")
            if fields[2] != "중랑구":
                continue
            official_rows += 1
            key = official_composite_key(fields)
            pnu = parcel_identifier(fields)
            parcel_to_keys[pnu].add(key)
            methods[(pnu, key)].add("DirectOfficialParcel")
            candidate = key_info.setdefault(
                key,
                {
                    "officialCompositeKey": key,
                    "canonicalRoadAddress": canonical_road_address(fields),
                    "officialBuildingManagementNumbers": set(),
                },
            )
            require(candidate["canonicalRoadAddress"] == canonical_road_address(fields), "CanonicalAddressConflict")
            building_management_number = fields[15]
            require(re.fullmatch(r"\d{25}", building_management_number) is not None, "BuildingManagementNumber")
            candidate["officialBuildingManagementNumbers"].add(building_management_number)
    require(official_rows == 27_489, "JungnangOfficialRecordCount")

    related_rows = 0
    with related_path.open("r", encoding="cp949", errors="strict") as stream:
        for raw_line in stream:
            fields = raw_line.rstrip("\r\n").split("|")
            require(len(fields) >= 13, "RelatedParcelColumns")
            if fields[2] != "중랑구":
                continue
            related_rows += 1
            key = related_parcel_composite_key(fields)
            pnu = parcel_identifier(fields)
            parcel_to_keys[pnu].add(key)
            methods[(pnu, key)].add("OfficialRelatedParcel")
    require(related_rows > 1_000, "JungnangRelatedParcelCoverage")
    return parcel_to_keys, key_info, methods


def candidate_payload(
    pnu: str,
    key: str,
    key_info: dict[str, dict[str, Any]],
    methods: dict[tuple[str, str], set[str]],
) -> dict[str, Any]:
    info = key_info.get(key)
    return {
        "addressStableId": "road-address:kr:" + hashlib.sha256(key.encode("utf-8")).hexdigest()[:24],
        "officialCompositeKey": key,
        "canonicalRoadAddress": "" if info is None else info["canonicalRoadAddress"],
        "officialBuildingManagementNumbers": []
        if info is None
        else sorted(info["officialBuildingManagementNumbers"]),
        "evidenceMethods": sorted(methods[(pnu, key)]),
    }


def add_content_hash(value: dict[str, Any]) -> dict[str, Any]:
    value["contentSha256"] = content_hash(value)
    return value


def validate_stored_content_hash(value: dict[str, Any], file_name: str) -> None:
    stored_hash = value.get("contentSha256")
    require(isinstance(stored_hash, str), f"ContentHashMissing:{file_name}")
    unhashed = dict(value)
    del unhashed["contentSha256"]
    require(content_hash(unhashed) == stored_hash, f"ContentHashMismatch:{file_name}")


def require_false_consumer_flags(value: dict[str, Any], scope: str) -> None:
    for key in (
        "distributionApproved",
        "deliveryEligible",
        "priceObservationEligible",
        "businessLocationEligible",
        "unityApplyAllowed",
        "traversalReady",
        "gameplayReady",
    ):
        require(value.get(key) is False, f"ConsumerAuthorityLeak:{scope}:{key}")


def validate_outputs(output_dir: Path) -> dict[str, Any]:
    binding_path = output_dir / "presentation-building-bindings.json"
    address_path = output_dir / "presentation-building-addresses.json"
    binding = read_json(binding_path)
    address = read_json(address_path)

    validate_stored_content_hash(
        binding,
        binding_path.name,
    )
    validate_stored_content_hash(
        address,
        address_path.name,
    )

    require(binding.get("schemaVersion") == "presentation-building-binding-ledger.v1", "BindingSchema")
    require(binding.get("revision") == BINDING_REVISION, "BindingRevision")
    require(binding.get("stationStableId") == STATION_STABLE_ID, "BindingStation")
    require(binding.get("regionStableId") == REGION_STABLE_ID, "BindingRegion")
    require(
        binding.get("presentationOverlayRevision") == "sagajeong-spatial-presentation.private-review.r2",
        "BindingOverlayRevision",
    )
    require(binding.get("presentationOverlayFileSha256") == OVERLAY_FILE_HASH, "BindingOverlayFileHash")
    require(binding.get("presentationOverlayContentSha256") == OVERLAY_CONTENT_HASH, "BindingOverlayContentHash")
    require(binding.get("referenceMapRevision") == "sagajeong-reference.r3", "BindingReferenceRevision")
    require(binding.get("referenceMapSha256") == REFERENCE_MAP_HASH, "BindingReferenceHash")
    require(
        binding.get("summary")
        == {
            "presentationBuildingCount": 4_062,
            "referenceBuildingCount": 602,
            "bindingCount": 544,
            "unresolvedReferenceBuildingCount": 58,
            "presentationBindingStates": {
                "AmbiguousGlobalAliasExcluded": 88,
                "AmbiguousMultipleFootprintsExcluded": 2,
                "BackdropOnly": 3_423,
                "Bound": 544,
                "WeakFootprintCandidateExcluded": 5,
            },
        },
        "BindingSummary",
    )
    binding_boundary = binding.get("boundary")
    require(isinstance(binding_boundary, dict), "BindingBoundary")
    require_false_consumer_flags(binding_boundary, "BindingBoundary")
    require(binding_boundary.get("observationPresentationOnly") is True, "BindingPresentationOnly")
    require(binding_boundary.get("geometryIncluded") is False, "BindingGeometryLeak")
    require(binding_boundary.get("nearestBindingInferenceAllowed") is False, "BindingNearestInference")

    presentation_bindings = binding.get("presentationBuildings")
    reference_bindings = binding.get("referenceBuildings")
    edges = binding.get("bindings")
    require(isinstance(presentation_bindings, list) and len(presentation_bindings) == 4_062, "BindingRows")
    require(isinstance(reference_bindings, list) and len(reference_bindings) == 602, "ReferenceBindingRows")
    require(isinstance(edges, list) and len(edges) == 544, "BindingEdges")
    presentation_ids = [str(row.get("presentationBuildingStableId", "")) for row in presentation_bindings]
    reference_ids = [str(row.get("referenceBuildingStableId", "")) for row in reference_bindings]
    require(len(set(presentation_ids)) == 4_062 and presentation_ids == sorted(presentation_ids), "BindingStableIds")
    require(len(set(reference_ids)) == 602 and reference_ids == sorted(reference_ids), "ReferenceBindingStableIds")
    observed_binding_states: dict[str, int] = defaultdict(int)
    for row in presentation_bindings:
        observed_binding_states[str(row.get("bindingState", ""))] += 1
    require(
        dict(sorted(observed_binding_states.items()))
        == binding["summary"]["presentationBindingStates"],
        "BindingStatePartition",
    )
    require(
        sum(row.get("bindingState") == "Bound" for row in reference_bindings) == 544
        and sum(row.get("bindingState") == "UnresolvedNoUniquePresentation" for row in reference_bindings) == 58,
        "ReferenceBindingPartition",
    )

    require(address.get("schemaVersion") == "presentation-building-address-assignment-ledger.v1", "AddressSchema")
    require(address.get("revision") == ADDRESS_REVISION, "AddressRevision")
    require(address.get("stationStableId") == STATION_STABLE_ID, "AddressStation")
    require(address.get("regionStableId") == REGION_STABLE_ID, "AddressRegion")
    require(address.get("sourceVintage") == SOURCE_VINTAGE, "AddressSourceVintage")
    require(address.get("evidenceAsOf") == "2026-08-31", "AddressEvidenceAsOf")
    require(address.get("bindingLedgerRevision") == BINDING_REVISION, "AddressBindingRevision")
    require(address.get("bindingLedgerContentSha256") == EXPECTED_BINDING_CONTENT_HASH, "AddressBindingHash")
    require(
        address.get("sourceReceipts")
        == [
            {
                "roleCode": "PresentationBuildingAndParcelIdentifier",
                "fileName": "AL_D010_11_20260809.zip",
                "sha256": AL_D010_HASH,
                "sourceRevision": "AL_D010:Seoul:20260809",
            },
            {
                "roleCode": "RoadAddressBuildingDatabase",
                "fileName": "202608_건물DB_전체분.zip",
                "sha256": MOIS_ZIP_HASH,
                "sourceRevision": SOURCE_VINTAGE,
            },
            {
                "roleCode": "RoadAddressBuildingRows",
                "fileName": "build_seoul.txt",
                "sha256": MOIS_BUILDING_HASH,
                "sourceRevision": SOURCE_VINTAGE,
            },
            {
                "roleCode": "RoadAddressRelatedParcelRows",
                "fileName": "jibun_seoul.txt",
                "sha256": MOIS_RELATED_PARCEL_HASH,
                "sourceRevision": SOURCE_VINTAGE,
            },
            {
                "roleCode": "ReferenceBuildingAddressAssignment",
                "fileName": "building-address-assignments.json",
                "sha256": REFERENCE_ADDRESS_HASH,
                "sourceRevision": "sagajeong-building-address-assignment.r1",
            },
        ],
        "AddressSourceReceipts",
    )
    expected_address_summary = {
        "presentationBuildingCount": 4_062,
        "parcelIdentifierCount": 4_062,
        "uniqueParcelIdentifierCount": 3_774,
        "legalDongAndJibunCount": 4_062,
        "sourceBuildingIdentifierCount": 3_796,
        "sourceUfidCount": 4_034,
        "rawParcelAddressCandidates": {"single": 3_796, "multiple": 60, "none": 206},
        "referenceOfficialAddressCount": 511,
        "crossSourceReconciliation": {"agreement": 491, "conflict": 18, "parcelCandidateMissing": 2},
        "resolutionStates": {
            "CrossSourceConflict": 18,
            "MultipleAddressCandidates": 47,
            "ParcelAddressCandidate": 3_278,
            "ReferenceBindingCandidate": 516,
            "Unresolved": 203,
        },
        "officialPromotedCount": 0,
        "nearestAddressInferenceCount": 0,
    }
    require(address.get("summary") == expected_address_summary, "AddressSummary")
    address_boundary = address.get("boundary")
    require(isinstance(address_boundary, dict), "AddressBoundary")
    require_false_consumer_flags(address_boundary, "AddressBoundary")
    require(address_boundary.get("observationPresentationOnly") is True, "AddressPresentationOnly")
    require(address_boundary.get("parcelGeometryCollected") is False, "AddressParcelGeometryClaim")
    require(address_boundary.get("nearestAddressInferenceAllowed") is False, "AddressNearestInference")
    require(address_boundary.get("officialBuildingIdentityConfirmed") is False, "AddressOfficialIdentity")

    assignments = address.get("assignments")
    require(isinstance(assignments, list) and len(assignments) == 4_062, "AddressRows")
    assignment_ids = [str(row.get("presentationBuildingStableId", "")) for row in assignments]
    require(len(set(assignment_ids)) == 4_062 and assignment_ids == sorted(assignment_ids), "AddressStableIds")
    require(set(assignment_ids) == set(presentation_ids), "AddressBindingCoverage")
    require(len({str(row.get("parcelIdentifierPnu", "")) for row in assignments}) == 3_774, "AddressUniquePnu")
    require(
        all(re.fullmatch(r"\d{19}", str(row.get("parcelIdentifierPnu", ""))) for row in assignments),
        "AddressPnuFormat",
    )
    observed_raw_candidates = {"single": 0, "multiple": 0, "none": 0}
    observed_resolution_states: dict[str, int] = defaultdict(int)
    for row in assignments:
        candidates = row.get("parcelAddressCandidates")
        require(isinstance(candidates, list), "AddressCandidates")
        observed_raw_candidates["none" if not candidates else "single" if len(candidates) == 1 else "multiple"] += 1
        observed_resolution_states[str(row.get("resolutionState", ""))] += 1
        require_false_consumer_flags(row, str(row.get("presentationBuildingStableId", "AddressRow")))
        require(not str(row.get("resolutionState", "")).startswith("Official"), "PrematureOfficialPromotion")
        require(
            row.get("assignmentMethod") == "FrozenPresentationBindingThenExactPnuAndOfficialRelatedParcel",
            "AddressAssignmentMethod",
        )
    require(observed_raw_candidates == expected_address_summary["rawParcelAddressCandidates"], "AddressCandidatePartition")
    require(
        dict(sorted(observed_resolution_states.items())) == expected_address_summary["resolutionStates"],
        "AddressResolutionPartition",
    )
    require(binding["contentSha256"] == EXPECTED_BINDING_CONTENT_HASH, "UnexpectedContentHash:Binding")
    require(address["contentSha256"] == EXPECTED_ADDRESS_CONTENT_HASH, "UnexpectedContentHash:Address")
    require(sha256_file(binding_path) == EXPECTED_BINDING_FILE_HASH, "BindingFileHash")
    require(sha256_file(address_path) == EXPECTED_ADDRESS_FILE_HASH, "AddressFileHash")

    return {
        "status": "Validated",
        "networkRequested": False,
        "stationStableId": STATION_STABLE_ID,
        "bindingRevision": BINDING_REVISION,
        "bindingContentSha256": EXPECTED_BINDING_CONTENT_HASH,
        "bindingFileSha256": EXPECTED_BINDING_FILE_HASH,
        "addressRevision": ADDRESS_REVISION,
        "addressContentSha256": EXPECTED_ADDRESS_CONTENT_HASH,
        "addressFileSha256": EXPECTED_ADDRESS_FILE_HASH,
        "presentationBuildingCount": 4_062,
        "bindingCount": 544,
        "parcelIdentifierCount": 4_062,
        "uniqueParcelIdentifierCount": 3_774,
        "rawParcelAddressCandidates": {"single": 3_796, "multiple": 60, "none": 206},
        "officialPromotedCount": 0,
        "consumerUseAuthorizedCount": 0,
        "parcelGeometryCollected": False,
    }


def build(repository_root: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    overlay_path = repository_root / "artifacts/local/sagajeong-spatial-presentation/private-review.json"
    reference_map_path = Path("C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json")
    address_folder = repository_root / "artifacts/local/public-data/sagajeong-building-address-20260914-r1"
    reference_address_path = address_folder / "building-address-assignments.json"
    al_d010_path = repository_root / "artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip"
    mois_zip_path = address_folder / "202608_건물DB_전체분.zip"
    building_path = address_folder / "build_seoul.txt"
    related_path = address_folder / "jibun_seoul.txt"

    overlay = read_json(overlay_path, OVERLAY_FILE_HASH)
    require(overlay.get("schemaVersion") == "ssalddel.spatial-presentation-overlay.v1", "OverlaySchema")
    require(overlay.get("revision") == "sagajeong-spatial-presentation.private-review.r2", "OverlayRevision")
    require(overlay.get("contentHash") == OVERLAY_CONTENT_HASH, "OverlayContentHash")
    require(overlay.get("status") == "LocalPrivateReview" and not overlay.get("distributionApproved"), "OverlayBoundary")
    reference_map = read_json(reference_map_path, REFERENCE_MAP_HASH)
    require(reference_map.get("revision") == "sagajeong-reference.r3", "ReferenceMapRevision")
    reference_address = read_json(reference_address_path, REFERENCE_ADDRESS_HASH)
    require(reference_address.get("revision") == "sagajeong-building-address-assignment.r1", "ReferenceAddressRevision")
    require(sha256_file(mois_zip_path) == MOIS_ZIP_HASH, "MoisZipHashChanged")

    presentation_buildings = overlay.get("buildings")
    reference_buildings = reference_map.get("buildings")
    reference_assignments = reference_address.get("buildings")
    require(isinstance(presentation_buildings, list) and len(presentation_buildings) == 4_062, "PresentationBuildingCount")
    require(isinstance(reference_buildings, list) and len(reference_buildings) == 602, "ReferenceBuildingCount")
    require(isinstance(reference_assignments, list) and len(reference_assignments) == 602, "ReferenceAddressCount")

    source_ids = [str(item.get("sourceFeatureId", "")) for item in presentation_buildings]
    require(len(set(source_ids)) == 4_062 and all(source_ids), "PresentationSourceFeatureIds")
    gis_rows = dbf_rows(al_d010_path, set(source_ids))

    allowed_match_methods = {
        "FootprintOverlap",
        "None",
        "AmbiguousGlobalAliasExcluded",
        "AmbiguousMultipleFootprintsExcluded",
        "WeakFootprintCandidateExcluded",
    }
    state_for_method = {
        "FootprintOverlap": "Bound",
        "None": "BackdropOnly",
        "AmbiguousGlobalAliasExcluded": "AmbiguousGlobalAliasExcluded",
        "AmbiguousMultipleFootprintsExcluded": "AmbiguousMultipleFootprintsExcluded",
        "WeakFootprintCandidateExcluded": "WeakFootprintCandidateExcluded",
    }
    binding_rows: list[dict[str, Any]] = []
    binding_edges: list[dict[str, Any]] = []
    reference_to_presentation: dict[str, str] = {}
    presentation_to_reference: dict[str, str] = {}
    for item in sorted(presentation_buildings, key=lambda row: str(row["id"])):
        presentation_id = str(item["id"])
        source_id = str(item["sourceFeatureId"])
        require(presentation_id == f"vworld:al-d010:{source_id}", "PresentationStableId")
        match = item.get("legacyMatch")
        require(isinstance(match, dict), f"LegacyMatchMissing:{presentation_id}")
        method = str(match.get("method", ""))
        require(method in allowed_match_methods, f"LegacyMatchMethod:{method}")
        references = sorted(str(value) for value in item.get("legacyOsmIds", []))
        require(len(references) == (1 if method == "FootprintOverlap" else 0), f"BindingReferenceCount:{presentation_id}")
        binding_rows.append(
            {
                "presentationBuildingStableId": presentation_id,
                "sourceFeatureId": source_id,
                "bindingState": state_for_method[method],
                "referenceBuildingStableIds": references,
                "bindingMethod": method,
                "ambiguous": bool(match.get("ambiguous", False)),
            }
        )
        if references:
            reference_id = references[0]
            require(reference_id not in reference_to_presentation, f"DuplicateReferenceBinding:{reference_id}")
            reference_to_presentation[reference_id] = presentation_id
            presentation_to_reference[presentation_id] = reference_id
            binding_edges.append(
                {
                    "presentationBuildingStableId": presentation_id,
                    "referenceBuildingStableId": reference_id,
                    "bindingMethod": method,
                    "centroidDistanceMeters": match.get("centroidDistanceMeters"),
                    "intersectionOverUnion": match.get("intersectionOverUnion"),
                    "smallerFootprintCoverage": match.get("smallerFootprintCoverage"),
                }
            )

    reference_ids = sorted(str(item["id"]) for item in reference_buildings)
    require(len(set(reference_ids)) == 602, "ReferenceStableIds")
    require(set(reference_to_presentation).issubset(reference_ids), "UnknownReferenceBinding")
    reference_rows = [
        {
            "referenceBuildingStableId": reference_id,
            "bindingState": "Bound" if reference_id in reference_to_presentation else "UnresolvedNoUniquePresentation",
            "presentationBuildingStableId": reference_to_presentation.get(reference_id, ""),
        }
        for reference_id in reference_ids
    ]
    binding_state_counts = {
        state: sum(row["bindingState"] == state for row in binding_rows)
        for state in sorted(set(state_for_method.values()))
    }
    require(
        binding_state_counts
        == {
            "AmbiguousGlobalAliasExcluded": 88,
            "AmbiguousMultipleFootprintsExcluded": 2,
            "BackdropOnly": 3_423,
            "Bound": 544,
            "WeakFootprintCandidateExcluded": 5,
        },
        "PresentationBindingPartition",
    )
    require(len(binding_edges) == 544 and len(reference_to_presentation) == 544, "BindingEdgeCount")

    binding_core: dict[str, Any] = {
        "schemaVersion": "presentation-building-binding-ledger.v1",
        "revision": BINDING_REVISION,
        "stationStableId": STATION_STABLE_ID,
        "regionStableId": REGION_STABLE_ID,
        "presentationOverlayRevision": overlay["revision"],
        "presentationOverlayFileSha256": OVERLAY_FILE_HASH,
        "presentationOverlayContentSha256": OVERLAY_CONTENT_HASH,
        "referenceMapRevision": reference_map["revision"],
        "referenceMapSha256": REFERENCE_MAP_HASH,
        "summary": {
            "presentationBuildingCount": 4_062,
            "referenceBuildingCount": 602,
            "bindingCount": 544,
            "unresolvedReferenceBuildingCount": 58,
            "presentationBindingStates": binding_state_counts,
        },
        "presentationBuildings": binding_rows,
        "referenceBuildings": reference_rows,
        "bindings": sorted(
            binding_edges,
            key=lambda row: (row["presentationBuildingStableId"], row["referenceBuildingStableId"]),
        ),
        "boundary": {
            **BOUNDARY,
            "geometryIncluded": False,
            "nearestBindingInferenceAllowed": False,
        },
    }
    binding_ledger = add_content_hash(binding_core)

    parcel_to_keys, key_info, evidence_methods = load_official_candidates(building_path, related_path)
    reference_by_id = {str(item["dioramaBuildingStableId"]): item for item in reference_assignments}
    require(len(reference_by_id) == 602, "ReferenceAddressStableIds")
    address_rows: list[dict[str, Any]] = []
    raw_candidate_counts = {"single": 0, "multiple": 0, "none": 0}
    reconciliation_counts = {"agreement": 0, "conflict": 0, "parcelCandidateMissing": 0}
    reference_official_count = 0
    resolution_counts: dict[str, int] = defaultdict(int)
    for binding in binding_rows:
        presentation_id = binding["presentationBuildingStableId"]
        source = gis_rows[binding["sourceFeatureId"]]
        pnu = source["parcelIdentifierPnu"]
        require(re.fullmatch(r"\d{19}", pnu) is not None, f"ParcelIdentifier:{presentation_id}")
        require(source["legalDongCode"] == pnu[:10] == "1126010100", f"LegalDongCode:{presentation_id}")
        require(source["jibun"] != "", f"JibunMissing:{presentation_id}")
        keys = sorted(parcel_to_keys.get(pnu, set()))
        candidates = [candidate_payload(pnu, key, key_info, evidence_methods) for key in keys]
        raw_candidate_counts["none" if not keys else "single" if len(keys) == 1 else "multiple"] += 1

        reference_id = presentation_to_reference.get(presentation_id, "")
        reference = reference_by_id.get(reference_id) if reference_id else None
        reference_state = "" if reference is None else str(reference["resolutionState"])
        reference_keys = [] if reference is None else sorted(str(value) for value in reference["officialCompositeKeys"])
        reconciliation = "NotBound"
        unresolved_reason = ""
        if reference_state.startswith("Official"):
            reference_official_count += 1
            if set(reference_keys).intersection(keys):
                resolution_state = "ReferenceBindingCandidate"
                reconciliation = "Agreement"
                reconciliation_counts["agreement"] += 1
            elif keys:
                resolution_state = "CrossSourceConflict"
                reconciliation = "Conflict"
                unresolved_reason = "ReferenceOfficialAddressConflictsWithParcelCandidates"
                reconciliation_counts["conflict"] += 1
            else:
                resolution_state = "ReferenceBindingCandidate"
                reconciliation = "ParcelCandidateMissing"
                unresolved_reason = "ReferenceOfficialAddressHasNoParcelCandidate"
                reconciliation_counts["parcelCandidateMissing"] += 1
        elif reference is not None and reference_state == "RoadAddressCandidate":
            resolution_state = "ReferenceBindingCandidate"
            reconciliation = "ReferenceCandidateOnly"
        elif len(keys) == 1:
            resolution_state = "ParcelAddressCandidate"
        elif len(keys) > 1:
            resolution_state = "MultipleAddressCandidates"
            unresolved_reason = "MultipleOfficialRoadAddressCandidatesForParcel"
        else:
            resolution_state = "Unresolved"
            unresolved_reason = "NoOfficialRoadAddressCandidateForParcel"
        resolution_counts[resolution_state] += 1
        address_rows.append(
            {
                "presentationBuildingStableId": presentation_id,
                "sourceFeatureId": source["sourceFeatureId"],
                "parcelIdentifierPnu": pnu,
                "legalDongCode": source["legalDongCode"],
                "legalDongName": "면목동",
                "jibun": source["jibun"],
                "sourceBuildingIdentifier": source["sourceBuildingIdentifier"],
                "sourceUfid": source["sourceUfid"],
                "referenceBuildingStableId": reference_id,
                "referenceAddressResolutionState": reference_state,
                "referenceOfficialCompositeKeys": reference_keys,
                "parcelAddressCandidates": candidates,
                "resolutionState": resolution_state,
                "reconciliationState": reconciliation,
                "assignmentMethod": "FrozenPresentationBindingThenExactPnuAndOfficialRelatedParcel",
                "unresolvedReason": unresolved_reason,
                "sourceVintage": SOURCE_VINTAGE,
                "sourceHashes": {
                    "alD010Sha256": AL_D010_HASH,
                    "moisBuildingDatabaseZipSha256": MOIS_ZIP_HASH,
                    "moisRelatedParcelSha256": MOIS_RELATED_PARCEL_HASH,
                },
                **BOUNDARY,
            }
        )

    require(raw_candidate_counts == {"single": 3_796, "multiple": 60, "none": 206}, "ParcelCandidatePartition")
    require(reference_official_count == 511, "ReferenceOfficialCount")
    require(
        reconciliation_counts == {"agreement": 491, "conflict": 18, "parcelCandidateMissing": 2},
        "CrossSourceReconciliationPartition",
    )
    unique_pnu = len({row["parcelIdentifierPnu"] for row in address_rows})
    require(unique_pnu == 3_774, "UniqueParcelIdentifierCount")
    require(sum(bool(row["sourceBuildingIdentifier"]) for row in address_rows) == 3_796, "SourceBuildingIdentifierCoverage")
    require(sum(bool(row["sourceUfid"]) for row in address_rows) == 4_034, "SourceUfidCoverage")
    require(not any(row["resolutionState"].startswith("Official") for row in address_rows), "PrematureOfficialPromotion")
    require(
        all(not row[key] for row in address_rows for key in (
            "distributionApproved",
            "deliveryEligible",
            "priceObservationEligible",
            "businessLocationEligible",
            "unityApplyAllowed",
            "traversalReady",
            "gameplayReady",
        )),
        "ConsumerAuthorityLeak",
    )

    address_core: dict[str, Any] = {
        "schemaVersion": "presentation-building-address-assignment-ledger.v1",
        "revision": ADDRESS_REVISION,
        "stationStableId": STATION_STABLE_ID,
        "regionStableId": REGION_STABLE_ID,
        "sourceVintage": SOURCE_VINTAGE,
        "evidenceAsOf": "2026-08-31",
        "bindingLedgerRevision": BINDING_REVISION,
        "bindingLedgerContentSha256": binding_ledger["contentSha256"],
        "sourceReceipts": [
            {
                "roleCode": "PresentationBuildingAndParcelIdentifier",
                "fileName": "AL_D010_11_20260809.zip",
                "sha256": AL_D010_HASH,
                "sourceRevision": "AL_D010:Seoul:20260809",
            },
            {
                "roleCode": "RoadAddressBuildingDatabase",
                "fileName": "202608_건물DB_전체분.zip",
                "sha256": MOIS_ZIP_HASH,
                "sourceRevision": SOURCE_VINTAGE,
            },
            {
                "roleCode": "RoadAddressBuildingRows",
                "fileName": "build_seoul.txt",
                "sha256": MOIS_BUILDING_HASH,
                "sourceRevision": SOURCE_VINTAGE,
            },
            {
                "roleCode": "RoadAddressRelatedParcelRows",
                "fileName": "jibun_seoul.txt",
                "sha256": MOIS_RELATED_PARCEL_HASH,
                "sourceRevision": SOURCE_VINTAGE,
            },
            {
                "roleCode": "ReferenceBuildingAddressAssignment",
                "fileName": "building-address-assignments.json",
                "sha256": REFERENCE_ADDRESS_HASH,
                "sourceRevision": "sagajeong-building-address-assignment.r1",
            },
        ],
        "summary": {
            "presentationBuildingCount": 4_062,
            "parcelIdentifierCount": 4_062,
            "uniqueParcelIdentifierCount": unique_pnu,
            "legalDongAndJibunCount": 4_062,
            "sourceBuildingIdentifierCount": 3_796,
            "sourceUfidCount": 4_034,
            "rawParcelAddressCandidates": raw_candidate_counts,
            "referenceOfficialAddressCount": reference_official_count,
            "crossSourceReconciliation": reconciliation_counts,
            "resolutionStates": dict(sorted(resolution_counts.items())),
            "officialPromotedCount": 0,
            "nearestAddressInferenceCount": 0,
        },
        "assignments": sorted(address_rows, key=lambda row: row["presentationBuildingStableId"]),
        "boundary": {
            **BOUNDARY,
            "parcelGeometryCollected": False,
            "nearestAddressInferenceAllowed": False,
            "officialBuildingIdentityConfirmed": False,
        },
    }
    address_ledger = add_content_hash(address_core)
    return binding_ledger, address_ledger


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    temporary = path.with_name(f".{path.name}.pending")
    temporary.write_text(payload, encoding="utf-8", newline="\n")
    temporary.replace(path)


def resolve_output(repository_root: Path, output_dir: str) -> Path:
    value = Path(output_dir)
    resolved = value.resolve() if value.is_absolute() else (repository_root / value).resolve()
    allowed = (repository_root / "artifacts/local").resolve()
    require(resolved == allowed or allowed in resolved.parents, "OutputMustRemainUnderArtifactsLocal")
    return resolved


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", required=True)
    parser.add_argument(
        "--output-dir",
        default="artifacts/local/public-data/sagajeong-presentation-building-evidence-20260914-r1",
    )
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args(argv)
    repository_root = Path(args.repository_root).resolve()
    require((repository_root / ".git").exists(), "RepositoryRoot")
    output_dir = resolve_output(repository_root, args.output_dir)
    if args.validate_only:
        print(json.dumps(validate_outputs(output_dir), ensure_ascii=False, sort_keys=True))
        return 0
    binding, address = build(repository_root)
    write_json(output_dir / "presentation-building-bindings.json", binding)
    write_json(output_dir / "presentation-building-addresses.json", address)
    print(
        json.dumps(
            {
                "bindingRevision": binding["revision"],
                "bindingContentSha256": binding["contentSha256"],
                "addressRevision": address["revision"],
                "addressContentSha256": address["contentSha256"],
                "presentationBuildings": address["summary"]["presentationBuildingCount"],
                "bindingCount": binding["summary"]["bindingCount"],
                "rawParcelAddressCandidates": address["summary"]["rawParcelAddressCandidates"],
            },
            ensure_ascii=False,
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
