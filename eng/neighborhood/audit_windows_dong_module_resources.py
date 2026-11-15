"""Windows 행정동 표현 자원의 읽기 전용 감사. Unity 실행/자료 권리/현행성 검증은 아니다."""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import tempfile


CATALOG = "hongdal-dong-modules.json"
HASH = re.compile(r"[0-9a-fA-F]{64}\Z")
STEM = re.compile(r"[A-Za-z0-9_-]+\Z")
REGION = re.compile(r"region:kr:hjd:[0-9]{10}\Z")


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


class AuditFailure(Exception):
    def __init__(self, code: str, detail: str):
        super().__init__(detail)
        self.code = code


class Snapshot:
    """읽은 바이트를 고정하고 종료 시 재대조한다. 입력에는 쓰지 않는다."""
    def __init__(self, root: Path):
        self.root = root.resolve()
        self.inputs: dict[str, dict] = {}

    def path(self, name: str) -> Path:
        target = (self.root / name).resolve()
        if target.parent != self.root or Path(name).name != name:
            raise AuditFailure("UnsafeResourcePath", name)
        return target

    def read(self, name: str) -> bytes:
        path = self.path(name)
        try:
            data = path.read_bytes()
        except FileNotFoundError:
            self.inputs[name] = {"name": name, "bytes": None, "sha256": None}
            raise AuditFailure("ResourceMissing", name) from None
        self.inputs[name] = {"name": name, "bytes": len(data), "sha256": sha(data)}
        return data

    def changed(self) -> list[str]:
        changed = []
        for name, recorded in self.inputs.items():
            try:
                current = sha(self.path(name).read_bytes())
            except (OSError, AuditFailure):
                current = None
            if current != recorded["sha256"]:
                changed.append(name)
        return changed


def resource(stem: object, extension: str) -> str:
    if not isinstance(stem, str) or not STEM.fullmatch(stem):
        raise AuditFailure("UnsafeResourcePath", str(stem))
    return stem + extension


def decoded(data: bytes, code: str) -> dict:
    try:
        value = json.loads(data.decode("utf-8-sig"),
                           parse_constant=lambda value: (_ for _ in ()).throw(ValueError(value)))
    except (UnicodeError, ValueError) as exc:
        raise AuditFailure(code, str(exc)) from None
    if not isinstance(value, dict):
        raise AuditFailure(code, "JSON 최상위 객체 필요")
    return value


def finite(value: object) -> bool:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return False
    try:
        return math.isfinite(value)
    except OverflowError:
        return False


def close(a: object, b: object) -> bool:
    return finite(a) and finite(b) and abs(a - b) <= 1e-8


def count(value: object) -> bool:
    return type(value) is int and value >= 0


def mesh_header(data: bytes) -> dict:
    """HDIO v2를 끝까지 제한 범위로 읽는다. 폴리곤/자료의 현실 정확성은 판정하지 않는다."""
    cursor = 0

    def take(size: int) -> memoryview:
        nonlocal cursor
        if size < 0 or cursor + size > len(data):
            raise AuditFailure("MeshInvalid", "메시 바이트가 잘렸음")
        value = memoryview(data)[cursor:cursor + size]
        cursor += size
        return value

    def integer() -> int:
        return struct.unpack("<i", take(4))[0]

    def floats(size: int) -> None:
        if any(not math.isfinite(v[0]) for v in struct.iter_unpack("<f", take(size * 4))):
            raise AuditFailure("MeshInvalid", "유한하지 않은 메시 좌표/UV")

    def mesh(coloured: bool) -> tuple[int, int]:
        vertices = integer()
        if not 0 <= vertices <= 4_000_000:
            raise AuditFailure("MeshInvalid", "정점 수가 읽기 한도 밖")
        floats(vertices * 3)
        if coloured:
            take(vertices * 4)  # Color32: RGBA
            floats(vertices * 2)
            floats(vertices * 2)
        indices = integer()
        if not 0 <= indices <= 12_000_000 or indices % 3:
            raise AuditFailure("MeshInvalid", "삼각형 색인 수가 읽기 한도/형식 밖")
        if any(not 0 <= v[0] < vertices for v in struct.iter_unpack("<i", take(indices * 4))):
            raise AuditFailure("MeshInvalid", "삼각형 색인이 정점 범위 밖")
        return vertices, indices // 3

    if bytes(take(4)) != b"HDIO" or integer() != 2:
        raise AuditFailure("MeshInvalid", "HDIO v2 머리 필요")
    values = struct.unpack("<6d", take(48))
    if not all(math.isfinite(v) for v in values):
        raise AuditFailure("MeshInvalid", "유한하지 않은 원점/범위")
    buildings, roads = integer(), integer()
    if min(buildings, roads) < 0:
        raise AuditFailure("MeshInvalid", "음수 건물/도로 수")
    vertices, triangles = mesh(True)
    mesh(False)
    filler_buildings = integer()
    fill_vertices, fill_triangles = mesh(True)
    shadow_vertices, shadow_triangles = mesh(False)
    if filler_buildings != 0 or any((fill_vertices, fill_triangles, shadow_vertices, shadow_triangles)):
        raise AuditFailure("MeshInvalid", "현재 동 모듈에는 예시 채움 건물이 없어야 함")
    if cursor != len(data):
        raise AuditFailure("MeshInvalid", "메시 끝에 해석하지 않은 바이트")
    return dict(zip(("originLatitude", "originLongitude", "west", "south", "east", "north"), values),
                buildings=buildings, roads=roads, vertices=vertices, triangles=triangles)


def audit(resources: Path) -> dict:
    snapshot = Snapshot(Path(resources))
    report = {"schema": "hongdal-windows-dong-resource-audit.v1",
              "auditedAtUtc": datetime.now(timezone.utc).isoformat(),
              "resourceDirectory": str(snapshot.root), "status": "Failed", "issues": [],
              "summary": {}, "modules": [], "inputs": [],
              "evidenceLimit": "OfflineResourceConsistencyOnly; NoUnityRuntime; NoReadinessPromotion"}

    def issue(code: str, detail: str) -> None:
        report["issues"].append({"code": code, "detail": detail})

    def verify(ok: bool, code: str, detail: str) -> None:
        if not ok:
            issue(code, detail)

    def run() -> None:
        try:
            catalog = decoded(snapshot.read(CATALOG), "ManifestInvalid")
        except AuditFailure as exc:
            if exc.code == "ResourceMissing":
                raise AuditFailure("ManifestMissing", CATALOG) from None
            raise
        modules = catalog.get("modules")
        if (catalog.get("schema") != "hongdal-dong-modules.v1" or
                not isinstance(modules, list) or not modules or
                any(not isinstance(m, dict) for m in modules)):
            raise AuditFailure("ManifestInvalid", "목록 형식·schema·빈 모듈 확인 필요")
        for key in ("originLatitude", "originLongitude", "metresPerDegreeLatitude", "metresPerDegreeLongitude"):
            if not finite(catalog.get(key)):
                raise AuditFailure("ManifestInvalid", key + " 유한값 필요")
        if (not -90 <= catalog["originLatitude"] <= 90 or not -180 <= catalog["originLongitude"] <= 180 or
                min(catalog["metresPerDegreeLatitude"], catalog["metresPerDegreeLongitude"]) <= 0):
            raise AuditFailure("ManifestInvalid", "원점/미터 환산값 범위 오류")

        regions = {}
        resources_seen = set()
        for module in modules:
            region = module.get("regionId")
            code = module.get("code")
            if (not isinstance(region, str) or not REGION.fullmatch(region) or
                    region != "region:kr:hjd:" + str(code) or region in regions):
                raise AuditFailure("ManifestInvalid", "지역 고유 식별자·코드·중복 오류: " + str(region))
            name = resource(module.get("resource"), ".bytes")
            if name in resources_seen:
                raise AuditFailure("ManifestInvalid", "여러 동이 같은 메시 자원 참조: " + name)
            resources_seen.add(name)
            if any(not count(module.get(k)) for k in ("bytes", "buildings", "roads", "vertices", "triangles",
                                                       "heightFromSource", "heightFromFloors", "heightSymbolic")):
                raise AuditFailure("ManifestInvalid", "모듈 수량 누락/오류: " + region)
            if not isinstance(module.get("sha256"), str) or not HASH.fullmatch(module["sha256"]):
                raise AuditFailure("ManifestInvalid", "메시 SHA256 형식 오류: " + region)
            if (any(not finite(module.get(k)) for k in ("west", "south", "east", "north")) or
                    not -180 <= module["west"] < module["east"] <= 180 or
                    not -90 <= module["south"] < module["north"] <= 90):
                raise AuditFailure("ManifestInvalid", "동 범위 오류: " + region)
            regions[region] = module

        for module in modules:
            region = module["regionId"]
            try:
                data = snapshot.read(resource(module["resource"], ".bytes"))
                verify(len(data) == module["bytes"] and sha(data) == module["sha256"].upper(),
                       "ModuleHashMismatch", region)
                header = mesh_header(data)
                verify(all(close(header[k], catalog[k]) for k in ("originLatitude", "originLongitude")) and
                       all(close(header[k], module[k]) for k in ("west", "south", "east", "north")) and
                       all(header[k] == module[k] for k in ("buildings", "roads", "vertices", "triangles")),
                       "ModuleHeaderMismatch", region)
                report["modules"].append({"regionId": region, **header})
            except AuditFailure as exc:
                issue(exc.code, region + ": " + str(exc))

        name = resource(catalog.get("buildingIndexResource"), ".json")
        try:
            data = snapshot.read(name)
        except AuditFailure as exc:
            if exc.code == "ResourceMissing":
                raise AuditFailure("BuildingIndexMissing", name) from None
            raise
        expected = catalog.get("buildingIndexSha256")
        verify(isinstance(expected, str) and HASH.fullmatch(expected) is not None and sha(data) == expected.upper(),
               "BuildingIndexHashMismatch", name)
        index = decoded(data, "BuildingIndexInvalid")
        buildings = index.get("buildings")
        if (index.get("schema") != "hongdal-building-index.v1" or not isinstance(buildings, list) or
                any(not isinstance(b, dict) for b in buildings)):
            raise AuditFailure("BuildingIndexInvalid", "건물 색인 형식 오류")
        origin = index.get("origin")
        verify(isinstance(origin, dict) and close(origin.get("latitude"), catalog["originLatitude"]) and
               close(origin.get("longitude"), catalog["originLongitude"]), "BuildingIndexOriginMismatch", name)
        verify(all(close(index.get(k), catalog[k]) for k in ("metresPerDegreeLatitude", "metresPerDegreeLongitude")),
               "BuildingIndexScaleMismatch", name)
        if "scopeContentHash" in catalog:
            verify(index.get("sourceSha256") == catalog["scopeContentHash"], "BuildingIndexScopeMismatch", name)
        seen = set()
        totals = Counter()
        heights = Counter()
        addresses = 0
        for building in buildings:
            stable_id, region = building.get("id"), building.get("region")
            if not isinstance(stable_id, str) or not stable_id:
                issue("BuildingIndexInvalid", "빈 건물 고유 식별자")
                continue
            verify(stable_id not in seen, "DuplicateBuildingId", stable_id)
            seen.add(stable_id)
            if not isinstance(region, str) or region not in regions:
                issue("BuildingRegionUnknown", stable_id + ": " + str(region))
                continue
            totals[region] += 1
            height = building.get("heightSource")
            if height not in ("height", "floors", "symbolic"):
                issue("BuildingIndexInvalid", stable_id + ": 높이 근거 없음")
            else:
                heights[(region, height)] += 1
            xz = building.get("xz")
            verify(isinstance(xz, list) and len(xz) >= 6 and len(xz) % 2 == 0 and all(finite(v) for v in xz),
                   "BuildingGeometryInvalid", stable_id)
            if building.get("road") and building.get("number"):
                addresses += 1
        for region, module in regions.items():
            verify(totals[region] == module["buildings"], "BuildingCountMismatch", region)
            verify(all(heights[(region, kind)] == module[field] for kind, field in
                       (("height", "heightFromSource"), ("floors", "heightFromFloors"), ("symbolic", "heightSymbolic"))),
                   "BuildingHeightCountMismatch", region)
        links = catalog.get("roleMap", [])
        if not isinstance(links, list) or any(not isinstance(link, dict) for link in links):
            raise AuditFailure("RoleMapInvalid", "역할 대응표 형식 오류")
        role_sources = set()
        for link in links:
            source, target = link.get("from"), link.get("to")
            if not isinstance(source, str) or not source or source in role_sources or not isinstance(target, str) or target not in seen:
                issue("RoleMapInvalid", str(source) + " → " + str(target))
            if isinstance(source, str):
                role_sources.add(source)
        report["summary"] = {"moduleCount": len(modules), "buildings": len(buildings),
                             "symbolicHeightBuildings": sum(heights[(r, "symbolic")] for r in regions),
                             "addressedBuildings": addresses, "roleMappings": len(links),
                             "distributionApproved": catalog.get("distributionApproved"),
                             "traversalReady": catalog.get("traversalReady"), "gameplayReady": catalog.get("gameplayReady"),
                             "sourceVintage": catalog.get("sourceVintage"), "scopeRevision": catalog.get("scopeRevision")}

    try:
        run()
    except AuditFailure as exc:
        issue(exc.code, str(exc))
    except OSError as exc:
        issue("InputReadFailed", str(exc))
    report["inputs"] = list(snapshot.inputs.values())
    changed = snapshot.changed()
    if changed:
        issue("InputChangedDuringAudit", ", ".join(changed))
        report["status"] = "InputChangedDuringAudit"
    elif not report["issues"]:
        report["status"] = "Passed"
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", type=Path, required=True, help="WindowsGame/Resources 실제 경로")
    parser.add_argument("--report", type=Path, help="입력 폴더 밖의 감사 JSON 경로")
    args = parser.parse_args()
    if args.report and args.report.resolve().is_relative_to(args.resources.resolve()):
        parser.error("입력 Resources 안에는 결과를 쓰지 않습니다")
    report = audit(args.resources)
    encoded = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", newline="\n", dir=args.report.parent,
                                         prefix=args.report.name + ".", suffix=".tmp", delete=False) as stream:
            stream.write(encoded)
            temporary = Path(stream.name)
        temporary.replace(args.report)
        print(json.dumps({"status": report["status"], "summary": report["summary"],
                          "issues": report["issues"], "report": str(args.report)}, ensure_ascii=False))
    else:
        print(encoded, end="")
    return {"Passed": 0, "Failed": 1, "InputChangedDuringAudit": 2}[report["status"]]


if __name__ == "__main__":
    raise SystemExit(main())
