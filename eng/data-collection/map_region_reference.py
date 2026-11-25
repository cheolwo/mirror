"""Read the verified MOIS national code and jurisdiction snapshot for the map catalog."""

from hashlib import sha256
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile


DEFAULT_REGION_REFERENCE = Path(
    "artifacts/local/dong-map-source-intake-20261113/raw-reference/jscode20261026.zip"
)
REGION_REFERENCE_AS_OF = "2026-10-30"
SNAPSHOT_REVISION = "20261026"
SNAPSHOT_SHA256 = "cce7299a01bfb29edc234884dbd4afa87b4a3a899ae3aefb9d6e4d0fe7b82509"
SNAPSHOT_BYTES = 2_114_284
OFFICIAL_URL = (
    "https://www.mois.go.kr/frt/bbs/type001/commonSelectBoardArticle.do"
    "?bbsId=BBSMSTR_000000000052&nttId=130674"
)
IDENTITY_FIELDS = ((0, 10), (11, 30), (42, 30), (73, 30))


def _table(archive, member, header, fields):
    lines = archive.read(f"jscode{SNAPSHOT_REVISION}/{member}.{SNAPSHOT_REVISION}").splitlines()
    if not lines or not lines[0].decode("cp949").startswith(header):
        raise ValueError("region_reference_header_invalid")
    return [
        tuple(line[offset:offset + width].decode("cp949").strip() for offset, width in fields)
        for line in lines[1:]
        if line.strip()
    ]


def _current(created, abolished):
    as_of = REGION_REFERENCE_AS_OF.replace("-", "")
    return bool(created and created <= as_of and (not abolished or abolished > as_of))


def _region(kind, code, name, parent_kind=None, parent_code=None):
    return {
        "kind": kind, "code": code, "name": name,
        "parent_kind": parent_kind, "parent_code": parent_code,
    }


def load_region_reference(path):
    """Return region rows, active MOIS B/H jurisdiction edges and source evidence.

    The B and H tables retain distinct code systems. Upper regions use the official
    two- and five-digit prefixes of their own upper-region rows. A locality is
    attached only when its prefix has a corresponding upper-region record.
    """
    path = Path(path)
    with path.open("rb") as stream:
        body = stream.read(SNAPSHOT_BYTES + 1)
    checksum = sha256(body).hexdigest()
    if len(body) != SNAPSHOT_BYTES or checksum != SNAPSHOT_SHA256:
        raise ValueError("region_reference_sha256_mismatch")

    with ZipFile(BytesIO(body)) as archive:
        legal = _table(archive, "KIKcd_B", "법정동코드", IDENTITY_FIELDS + ((104, 30), (135, 8), (144, 8)))
        administrative = _table(archive, "KIKcd_H", "행정동코드", IDENTITY_FIELDS + ((104, 8), (113, 8)))
        crosswalk = _table(archive, "KIKmix", "행정동코드", ((0, 10), (104, 10), (146, 8), (155, 8)))

    provinces = {}
    districts = {}
    localities = {}
    for kind, rows in (("legal", legal), ("administrative", administrative)):
        for code, province, district, locality, *suffix in rows:
            if not _current(*suffix[-2:]):
                continue
            if len(code) != 10 or not code.isascii() or not code.isdigit():
                raise ValueError("region_reference_code_invalid")
            if code[2:] == "00000000":
                provinces[code[:2]] = province
            elif code[5:] == "00000":
                districts[code[:5]] = district or province
            else:
                village = suffix[0] if kind == "legal" else ""
                if (kind, code) in localities or not (village or locality):
                    raise ValueError("region_reference_locality_invalid")
                localities[kind, code] = village or locality

    regions = [_region("sido", code, name) for code, name in sorted(provinces.items())]
    for code, name in sorted(districts.items()):
        parent = code[:2] if code[:2] in provinces else None
        regions.append(_region("sigungu", code, name, "sido" if parent else None, parent))
    for (kind, code), name in sorted(localities.items()):
        parent = code[:5] if code[:5] in districts else None
        regions.append(_region(kind, code, name, "sigungu" if parent else None, parent))

    pairs = {
        (admin, legal_code) for admin, legal_code, created, abolished in crosswalk
        if _current(created, abolished)
        and ("administrative", admin) in localities and ("legal", legal_code) in localities
    }
    relationships = [
        {"administrative_code": admin, "legal_code": legal_code}
        for admin, legal_code in sorted(pairs)
    ]
    evidence = {
        "sha256": checksum, "filename": path.name, "official_url": OFFICIAL_URL,
        "as_of": REGION_REFERENCE_AS_OF,
    }
    return regions, relationships, evidence
