"""제품 Razor 페이지의 소스/호스트 색인. 실행/캡처 검증과 구별한다."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/ProjectOverview/page-docs"
REGISTRY = OUT / "page-identities.json"
ROUTE = re.compile(r'^\s*@page\s+"([^"]+)"', re.M)
EXCLUDED = {"bin", "obj", ".vs", "vendor", "artifacts", "node_modules", ".worktrees"}


def relative(path):
    return path.resolve().relative_to(ROOT).as_posix()


def razor_files(folder):
    return sorted(p for p in folder.rglob("*.razor") if not EXCLUDED.intersection(p.parts))


def discover_projects():
    projects = []
    for project in sorted(ROOT.glob("*/*.csproj")):
        source = project.read_text(encoding="utf-8-sig")
        # 제품 host만 포함한다. 공용 Razor library와 eng/test preview는 별도다.
        if ("<UseMaui>true</UseMaui>" in source
                or "Microsoft.NET.Sdk.BlazorWebAssembly" in source
                or ("Microsoft.NET.Sdk.Web" in source and "Admin" in project.stem)):
            projects.append(project)
    return projects


def host_pages(project):
    command = ["dotnet", "msbuild", str(project), "-getItem:Content,RazorComponent", "-nologo"]
    source = project.read_text(encoding="utf-8-sig")
    if "<UseMaui>true</UseMaui>" in source:
        # outer multi-target 평가에는 Content가 없을 수 있으므로 현행 Android target을 지정한다.
        command.append("-p:TargetFramework=net10.0-android")
    result = subprocess.run(command, cwd=ROOT, capture_output=True, encoding="utf-8", timeout=60)
    if result.returncode:
        raise RuntimeError(f"{project.stem}: MSBuild 평가 실패\n{result.stderr or result.stdout}")
    data = json.loads(result.stdout)
    items = [item for group in data.get("Items", {}).values() for item in group]
    paths = {Path(item["FullPath"]).resolve() for item in items
             if item.get("Extension") == ".razor"}
    return project.stem, paths


def seed_registry():
    """최초 전환에만 옛 물리 ID를 읽는다. 이후 JSON이 ID의 기준이다."""
    records = {}
    for doc in sorted(OUT.glob("*/*/README.md")):
        source = doc.read_text(encoding="utf-8-sig")
        page_id = doc.parent.name
        for path in re.findall(r'\]\(([^)]+\.razor)\)', source):
            target = (doc.parent / path).resolve()
            if target.exists():
                records[relative(target)] = {"id": page_id, "detail": relative(doc)}
    catalog = (OUT.parent / "app-page-catalog.md").read_text(encoding="utf-8-sig")
    for line in catalog.splitlines():
        if not line.startswith("| `"):
            continue
        match = re.search(r'`([^`]+?) - ', line)
        if not match:
            continue
        page_id = match.group(1)
        for path in re.findall(r'`([^`]+\.razor)`', line):
            if "..." in path:
                continue
            for target in ROOT.glob(path):
                key = relative(target)
                if key not in records:
                    doc = OUT / page_id.split("-P")[0] / page_id / "README.md"
                    records[key] = {"id": page_id, "detail": relative(doc) if doc.exists() else None}
    records["DriverApp/Components/Pages/Driver/03_Progress/음식배달업무Page.razor"] = {
        "id": "DriverApp-FoodDeliveries",
        "detail": "docs/ProjectOverview/page-docs/DriverApp/DriverApp-FoodDeliveries/README.md"}
    return records


def build(registry):
    projects = discover_projects()
    with ThreadPoolExecutor(max_workers=4) as pool:
        hosts = dict(pool.map(host_pages, projects))
    physical = {p for project in projects for p in razor_files(project.parent)}
    physical.update(p for paths in hosts.values() for p in paths)
    pages = []
    identities = dict(registry)
    for path in sorted(physical):
        routes = ROUTE.findall(path.read_text(encoding="utf-8-sig"))
        if not routes:
            continue
        key = relative(path)
        owner = key.split("/")[0]
        if key not in identities:
            # 이름이 바뀌어도 다음 실행부터 보존한다. 기존 P 번호를 재사용하지 않는다.
            identity = owner + "-" + "-".join(path.relative_to(ROOT / owner).with_suffix("").parts)
            identities[key] = {"id": identity, "detail": None}
        entry = identities[key]
        pages.append({"id": entry["id"], "owner": owner, "source": key,
                      "routes": routes, "hosts": sorted(name for name, files in hosts.items() if path in files),
                      "detail": entry.get("detail"), "review": "Detailed" if entry.get("detail") in DETAILS else "InventoryOnly"})
    active_sources = {page["source"] for page in pages}
    retired = [{"source": source, **entry,
                "status": "ExistingNonRouteReference" if (ROOT / source).exists() else "MissingSource"}
               for source, entry in sorted(identities.items())
               if source not in active_sources]
    return {"schema": "page-source-host-catalog.r1", "basis": "Razor @page + evaluated MSBuild Content/RazorComponent",
            "runtimeVerified": False, "pages": pages, "retired": retired,
            "hosts": [{"host": host, "pageFiles": sum(host in p["hosts"] for p in pages),
                       "routeDeclarations": sum(len(p["routes"]) for p in pages if host in p["hosts"])}
                      for host in sorted(hosts)]}, identities


DETAILS = {
    "docs/ProjectOverview/page-docs/SsalddelAdmin/SsalddelAdmin-P30/README.md",
    "docs/ProjectOverview/page-docs/DriverApp/DriverApp-FoodDeliveries/README.md",
    "docs/ProjectOverview/page-docs/DriverApp/DriverApp-P05/README.md",
    "docs/ProjectOverview/page-docs/DriverApp/DriverApp-P14/README.md",
}


def markdown(data):
    pages = data["pages"]
    lines = ["# 현재 페이지 소스·호스트 목록", "",
             "이 파일은 `eng/page-docs/catalog.py --write`로 생성한다. 소스와 MSBuild Content/RazorComponent 평가 결과이며 실제 사용자 조작·캡처·출시 완료를 뜻하지 않는다.", "",
             f"물리 페이지 파일 **{len(pages)}개**, route 선언 **{sum(len(p['routes']) for p in pages)}개**. 별칭 route는 같은 페이지에 묶고, 여러 호스트에서 재사용하는 소스는 한 번만 센다.", "",
             "기존 P ID와 캡처는 보존한다. 새 파일에는 경로 기반 ID를 부여하고 `page-identities.json`에 고정한다. 과거 문서가 있다는 사실과 이번 페이지→코드→DB 상세 검토 완료는 별개다.", "",
             "## 실행 호스트의 소스 포함", "", "| 호스트 | 페이지 파일 | route 선언 |", "| --- | ---: | ---: |"]
    for host in data["hosts"]:
        lines.append(f"| `{host['host']}` | {host['pageFiles']} | {host['routeDeclarations']} |")
    lines.extend(["", "호스트 표는 컴파일 입력이다. 기능 플래그·인증·실행 모드의 실제 접근 가능 여부는 각 화면에서 확인한다. `eng/web-role-app`의 공통 진입 화면은 제품 역할 호스트에 포함되므로 목록에 남긴다.", ""])
    for owner in sorted({p["owner"] for p in pages}):
        lines.extend([f"## {owner}", "", "| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |", "| --- | --- | --- | --- | --- |"])
        for page in (p for p in pages if p["owner"] == owner):
            source_link = "../../../" + page["source"]
            doc = page["detail"]
            link = f"[이전 화면 문서]({Path(doc).relative_to(OUT.relative_to(ROOT)).as_posix()})" if doc else "미검토"
            if page["review"] == "Detailed":
                link = link.replace("이전 화면 문서", "페이지→코드→DB")
            routes = "<br>".join(f"`{r}`" for r in page["routes"])
            lines.append(f"| `{page['id']}` | {routes} | [{Path(page['source']).name}]({source_link}) | {', '.join(page['hosts']) or 'Content 포함 미확인'} | {link} |")
        lines.append("")
    lines.extend(["## 과거 문서의 비라우트 참조·누락 소스", "", "기존 README가 함께 연결한 공용/접근 제어 컴포넌트는 페이지에서 제외한다. 파일 없음과 파일은 있으나 @page가 없는 경우를 구별하며 기존 ID·문서는 삭제하지 않는다. 삭제/이동의 원인을 확정할 때는 Git 이력을 대조한다.", ""])
    for entry in data["retired"]:
        label = "기존 비라우트 컴포넌트" if entry["status"] == "ExistingNonRouteReference" else "현재 파일 없음"
        lines.append(f"- `{entry['id']}`: `{entry['source']}` — {label}")
    return "\n".join(lines) + "\n"


def serialize(value):
    return json.dumps(value, ensure_ascii=False, indent=2) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    registry = json.loads(REGISTRY.read_text(encoding="utf-8")) if REGISTRY.exists() else seed_registry()
    data, identities = build(registry)
    outputs = {REGISTRY: serialize(identities), OUT / "current-pages.json": serialize(data),
               OUT / "current-pages.md": markdown(data)}
    differences = []
    for path, content in outputs.items():
        if args.write:
            path.write_text(content, encoding="utf-8", newline="\n")
        elif not path.exists() or path.read_text(encoding="utf-8") != content:
            differences.append(relative(path))
    if differences:
        print("STALE: " + ", ".join(differences))
        return 1
    print(f"{'WROTE' if args.write else 'VALID'}: {len(data['pages'])} page files, "
          f"{sum(len(p['routes']) for p in data['pages'])} routes, {len(data['hosts'])} hosts")
    return 0


if __name__ == "__main__":
    sys.exit(main())
