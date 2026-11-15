"""면목제3·8동 기준 설정으로 행정동을 굽고 원자료 검사·비교 화면을 함께 만든다."""
import argparse
import gzip
import hashlib
import json
import subprocess
import sys
import tempfile
from datetime import datetime
from pathlib import Path

from dong_diorama_reference import load_profile


TOOLS = Path(__file__).resolve().parent
ROOT = TOOLS.parents[1]
PROFILE = TOOLS / "dong_diorama_reference.json"
RUNS = ROOT / "artifacts/local/admin-dong-reference-review"


def read_json(path):
    return json.loads(path.read_bytes())


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def run(tool, *args):
    subprocess.run([sys.executable, "-B", "-X", "utf8", str(TOOLS / tool), *map(str, args)],
                   cwd=ROOT, check=True)


def check_reference(cache, profile):
    folder = cache / profile["referenceDongCode"]
    receipt = read_json(folder / "receipt.json")
    actual = {
        "sourceModuleSha256": receipt["moduleSha256"].lower(),
        "meshSha256": sha256(gzip.decompress((folder / "mesh.bytes.gz").read_bytes())),
        "buildingIndexSha256": sha256(gzip.decompress((folder / "index.json.gz").read_bytes())),
    }
    for key, value in actual.items():
        if value != profile["baseline"][key].lower():
            raise ValueError(f"기준 동의 {key}가 설정에 기록된 검토본과 다릅니다.")
    return actual


def write_summary(folder, report, profile_path, scope):
    profile, binding = load_profile(profile_path)
    profile_hash = binding["sha256"]
    verified = read_json(report)
    if verified["result"] != "Passed" or verified.get("referenceProfile") != binding or not verified.get("profilePinnedInInput"):
        raise ValueError("자료 검사를 통과하지 못했습니다.")
    rows = verified["modules"]
    if len(rows) != verified["moduleCount"] or not rows:
        raise ValueError("검증 결과의 동 목록을 확인하세요.")
    anchor = profile["referenceDongCode"]
    verified_codes = {row["code"] for row in rows}
    if anchor not in verified_codes or len(verified_codes) != len(rows):
        raise ValueError("검증 목록에 기준 동이 없습니다.")
    previews = read_json(folder / "visual/index.json")
    cards = {card["code"]: card for card in previews["modules"]}
    if (previews["referenceCode"] != anchor or len(cards) != len(previews["modules"])
            or set(cards) != verified_codes or (scope != "all" and set(scope) != verified_codes)):
        raise ValueError("자료 검증과 비교 화면의 행정동이 다릅니다.")
    checked = check_reference(folder / "modules", profile)
    rows = sorted(rows, key=lambda row: (row["code"] != anchor, row["district"], row["name"]))
    lines = [
        "# 행정동 디오라마 공통 품질표", "",
        f"기준은 **{rows[0]['name']}**이며 같은 설정으로 {len(rows)}개 동을 검사했다. "
        "동 이름을 누르면 전체 지형과 300m 확대 그림을 볼 수 있다. "
        "[지도 비교](visual/index.json) · [레이어별 원본 면적과 메시 면적](verification.json)", "",
        "건물·도로·보행은 경계에서 자른 조각 수다. 높이는 실측·층수 환산·상징 높이를 나란히 표시한다. "
        "공원은 계획면, 인허가는 같은 필지에서 연결된 후보 수다. "
        "실측 높이 결측과 원자료가 비어 있는 레이어는 동마다 다를 수 있다.", "",
        "| 구 | 행정동 | 건물 | 높이 자료·층수·상징 | 지번 연결 | 도로 | 지표 도보 / 전체 | 공원 계획면 | 인허가 | 자료 보완 시 참고 |",
        "| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | --- |",
    ]
    for row in rows:
        code = row["code"]
        receipt = read_json(folder / "modules" / code / "receipt.json")
        card = cards[code]
        frame = folder / "visual" / card["image"]
        if (card["meshSha256"].lower() != row["sha256"].lower()
                or card["sourceModuleSha256"].lower() != receipt["moduleSha256"].lower()
                or sha256(frame.read_bytes()) != card["sha256"].lower()):
            raise ValueError(f"{code} 비교 화면과 검증한 메시·원자료의 지문 불일치")
        if receipt["referenceProfile"]["sha256"].lower() != profile_hash:
            raise ValueError(f"{code} 생성 설정 지문 불일치")
        if receipt["entry"]["sha256"].lower() != row["sha256"].lower():
            raise ValueError(f"{code} 검사 메시 지문 불일치")
        notes = []
        labels = {"ground":"지면", "parkPlan":"공원 계획면", "walk":"지표 도보",
                  "roadEdge":"도로 가장자리", "carriage":"차도", "lane":"차선"}
        for key, label in labels.items():
            status = row["surfaceLayers"][key]["status"]
            if status == "source-empty":
                notes.append(f"{label} 대상 자료 0")
            elif status == "clipped-empty":
                notes.append(f"{label} 절단 후 표시 면적 0")
            elif status != "present":
                raise ValueError(f"{code} {key}의 미지원 검사 상태 {status}")
        if row["buildingRowsWithParcelLabel"] < row["buildings"]:
            notes.append("지번 일부 미확인")
        symbolic = row["heightSymbolic"]
        height = f'{row["heightFromSource"]:,} / {row["heightFromFloors"]:,} / {symbolic:,}'
        note = " · ".join(notes) or "—"
        lines.append(f'| {row["district"]} | [{row["name"]}](visual/{card["image"]}) | '
                     f'{row["buildings"]:,} | {height} | {row["buildingRowsWithParcelLabel"]:,} | '
                     f'{row["roads"]:,} | {row["surfaceWalkLinks"]:,} / {row["sourceWalkLinks"]:,} | '
                     f'{row["parkPlanFragments"]:,} | {row["permitKeys"]:,} | {note} |')
    (folder / "QUALITY.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    result = {"referenceProfile": profile["profileId"], "profileSha256": profile_hash,
              "referenceDongCode": anchor, "referenceChecks": checked, "selection": scope,
              "verifiedDongCount": len(rows), "verification": "verification.json", "preview": "visual/index.json",
              "commonQualityTable": "QUALITY.md", "unityRunInThisProcedure": False}
    (folder / "result.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def prepare(dest, requested_codes, all_dongs, profile_path, consumer):
    profile, _ = load_profile(profile_path)
    anchor = profile["referenceDongCode"]
    codes = list(dict.fromkeys([anchor, *requested_codes]))
    scope = "all" if all_dongs else codes
    if dest.exists():
        raise FileExistsError(f"기존 결과가 있습니다. 새 출력 폴더를 선택하세요. {dest}")
    dest.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=dest.parent, prefix=".dong-build-") as scratch:
        folder = Path(scratch) / "result"
        folder.mkdir()
        cache = folder / "modules"
        args = ["--profile", profile_path, "--cache", cache, "--output", folder]
        if consumer:
            args.extend(["--consumer", consumer])
        if all_dongs:
            run("bake_dong_unity_modules.py", *args, "--all")
        select = [part for code in codes for part in ("--code", code)]
        run("bake_dong_unity_modules.py", *args, *select)
        verification = folder / "verification.json"
        run("verify_dong_unity_modules.py", "--profile", profile_path, "--output", folder, "--cache", cache,
            "--report", verification, *(["--all"] if all_dongs else []))
        run("preview_dong_unity_modules.py", "--cache", cache, "--output", folder / "visual",
            *(["--all"] if all_dongs else select))
        write_summary(folder, verification, profile_path, scope)
        folder.rename(dest)
    print(f"공통 품질표 {dest / 'QUALITY.md'}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--code", action="append", help="검사할 10자리 행정동 코드. 여러 번 지정 가능.")
    group.add_argument("--all", action="store_true", help="자료 목록의 모든 동을 검사한다.")
    parser.add_argument("--output", type=Path, help="새 결과 폴더. 기본값은 실행 시각별 폴더.")
    parser.add_argument("--profile", type=Path, default=PROFILE)
    parser.add_argument("--consumer", type=Path, help="현재 Unity WindowsGame의 Resources 폴더")
    args = parser.parse_args()
    if len(set(args.code or [])) != len(args.code or []):
        parser.error("같은 행정동 코드를 여러 번 지정했습니다.")
    destination = args.output or RUNS / datetime.now().strftime("run-%Y%m%d-%H%M%S-%f")
    try:
        prepare(destination.resolve(), args.code or [], args.all, args.profile.resolve(),
                args.consumer.resolve() if args.consumer else None)
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"생성 미완료 · {error}\n")


if __name__ == "__main__":
    main()
