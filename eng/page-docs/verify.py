"""현행 페이지 문서 링크와 HEAD/working-tree 경계를 검증한다."""
import json
from pathlib import Path
import re
import subprocess
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/ProjectOverview/page-docs"


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True,
                          encoding="utf-8", errors="replace")


def links(source):
    return re.findall(r'\]\(([^)]+)\)|(?:src|href)="([^"]+)"', source)


def local_targets(path, source):
    for pair in links(source):
        ref = next(value for value in pair if value).strip("<>")
        if ref.startswith(("https:", "http:", "#", "app:", "data:")):
            continue
        yield ref, (path.parent / unquote(ref.split("#")[0])).resolve()


def main():
    catalog = json.loads((DOCS / "current-pages.json").read_text(encoding="utf-8"))
    detailed = {page["detail"] for page in catalog["pages"] if page["review"] == "Detailed"}
    files = [ROOT / path for path in sorted(detailed)] + [
        DOCS / "README.md", DOCS / "current-pages.md", DOCS / "DriverApp/README.md",
        DOCS / "implementation-r1.md"]
    problems, historical = [], []
    refs = set()
    for path in files:
        source = path.read_text(encoding="utf-8-sig")
        if str(path.relative_to(ROOT)).replace("\\", "/") in detailed:
            for heading in ("## 1. 페이지", "## 2. 코드", "## 3. DB"):
                if heading not in source:
                    problems.append(f"{path.name}: missing {heading}")
        previous = git("show", "HEAD:" + path.relative_to(ROOT).as_posix())
        old_broken = {ref for ref, target in local_targets(path, previous.stdout)
                      if not target.exists()} if previous.returncode == 0 else set()
        for ref, target in local_targets(path, source):
            if not target.is_relative_to(ROOT):
                problems.append(f"{path.relative_to(ROOT)}: outside repo {ref}")
            elif not target.exists():
                (historical if ref in old_broken else problems).append(f"{path.relative_to(ROOT)}: {ref}")
            elif target.is_file():
                refs.add(target.relative_to(ROOT).as_posix())
    head = set(git("-c", "core.quotepath=false", "ls-tree", "-r", "--name-only", "HEAD").stdout.splitlines())
    # Git의 경로 인용 설정과 무관하게 한글 경로를 비교한다.
    changes = set(git("-c", "core.quotepath=false", "diff", "--name-only", "HEAD").stdout.splitlines())
    report = {"schema": "page-doc-links.r1", "runtimeVerified": False,
              "checkedDocuments": len(files), "uniqueLocalFileTargets": len(refs),
              "errors": problems, "inheritedBrokenLinks": historical,
              "targetsNotInHead": sorted(refs - head),
              "targetsChangedSinceHead": sorted(refs & changes)}
    output = ROOT / "artifacts/local/page-docs-r1/document-verification.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in report.items()
                      if key not in ("targetsNotInHead", "targetsChangedSinceHead")}, ensure_ascii=False))
    print(f"HEAD boundary: {len(refs - head)} local-only targets, {len(refs & changes)} changed targets")
    return int(bool(problems))


if __name__ == "__main__":
    raise SystemExit(main())
