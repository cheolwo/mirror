"""행정동 HDIO를 같은 정사영과 거리로 그려 면목제3·8동과 비교한다."""
import argparse
from concurrent.futures import ProcessPoolExecutor
import gzip
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "artifacts/local/public-data/gis-runtime-r1"))
import numpy as np
from PIL import Image, ImageDraw, ImageFont, __version__ as PILLOW_VERSION

REFERENCE = "1126057500"
DEFAULT_CACHE = ROOT / "artifacts/local/admin-dong-unity-r4/bake/modules"
DEFAULT_OUTPUT = ROOT / "artifacts/local/admin-dong-reference-review-20261115/visual"
PANEL = (570, 390)
WIDTH_METRES = 300
SUPERSAMPLE = 2
YAW = math.radians(30)
PITCH = math.radians(60)
BACKGROUND = (18, 30, 38)
INK = "#e5f1ee"
MUTED = "#adc6c4"
ACCENT = "#73ddcb"
MARGIN = 22
GAP = 18
CARD_WIDTH = PANEL[0] * 2 + MARGIN * 2 + GAP
CARD_HEIGHT = 558


def digest(blob):
    return hashlib.sha256(blob).hexdigest()


def checked(path, expected):
    raw = path.read_bytes()
    if digest(raw) != expected.lower():
        raise ValueError(f"파일 지문 불일치 {path}")
    return raw


def font(size, bold=False):
    names = ["C:/Windows/Fonts/malgunbd.ttf" if bold else "C:/Windows/Fonts/malgun.ttf",
             "/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc" if bold else
             "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"]
    for name in names:
        if Path(name).is_file():
            return ImageFont.truetype(name, size)
    raise OSError("한글 글꼴 맑은 고딕 또는 Noto Sans CJK가 필요합니다.")


def read_hdio(blob, entry):
    if (len(blob) != entry["bytes"] or blob[:8] != b"HDIO\x02\0\0\0" or
            struct.unpack_from("<2i", blob, 56) != (entry["buildings"], entry["roads"])):
        raise ValueError("HDIO v2 본문 또는 행정동 수량 불일치")
    offset = 64

    def array(kind, count):
        nonlocal offset
        if count < 0:
            raise ValueError("음수 배열 길이")
        result = np.frombuffer(blob, kind, count, offset)
        offset += result.nbytes
        return result

    meshes = []
    for shadow in (False, True):
        count = int(array("<i4", 1)[0])
        xyz = array("<f4", count * 3).reshape(-1, 3)
        colors = None
        if not shadow:
            colors = array("u1", count * 4).reshape(-1, 4)[:, :3]
            array("<f4", count * 4)  # 두 UV 채널
        faces = array("<i4", int(array("<i4", 1)[0])).reshape(-1, 3)
        if not count or not len(faces) or not np.isfinite(xyz).all() or faces.min() < 0 or faces.max() >= count:
            raise ValueError("정점 또는 삼각형 인덱스 불일치")
        meshes.append((xyz, faces, colors))
    if blob[offset:] != bytes(20):
        raise ValueError("지원하지 않는 HDIO 꼬리 데이터")
    return meshes


def project(points):
    """동·북 평면에서 시계방향 30도, 고도 60도의 정사영. 깊이의 양수는 카메라 쪽."""
    east, height, north = points.astype("f8").T
    along = east * math.sin(YAW) + north * math.cos(YAW)
    return np.column_stack((east * math.cos(YAW) - north * math.sin(YAW),
                            -(along * math.sin(PITCH) + height * math.cos(PITCH)),
                            height * math.sin(PITCH) - along * math.cos(PITCH)))


def raster(image, projected, faces, colors, origin, scale, shadow=False):
    """화면 픽셀 중심에서 삼각형의 깊이를 비교한다. 역순으로 그려도 가림 관계가 같다."""
    height, width = image.shape[:2]
    coords = projected.copy()
    coords[:, :2] = (coords[:, :2] - origin) * scale + [width / 2, height / 2]
    tri = coords[faces]
    xmin, ymin = tri[:, :, :2].min(axis=1).T
    xmax, ymax = tri[:, :, :2].max(axis=1).T
    active = (xmin < width) & (xmax >= 0) & (ymin < height) & (ymax >= 0)
    tri = tri[active]
    face_colors = None if shadow else colors[faces[active, 0]]
    if not len(tri):
        return
    # 가로 주사선에서 최소·최대 교차점을 찾아 각 삼각형이 차지하는 픽셀 구간을 펼친다.
    xa, ya, za = tri.transpose(2, 0, 1)
    dx, dy, dz = (np.roll(a, -1, axis=1) - a for a in (xa, ya, za))
    det = dx[:, 0] * dy[:, 1] - dx[:, 1] * dy[:, 0]
    ymin = np.where(abs(det) > 1.e-8, ya.min(axis=1), np.inf)
    ymax = ya.max(axis=1)
    slope = np.divide(dx, dy, out=np.zeros_like(dx), where=dy != 0)
    edge_ymin = np.minimum(ya, ya + dy)
    edge_ymax = np.maximum(ya, ya + dy)
    if not shadow:
        zdx = np.divide(dz[:, 0]*dy[:, 1]-dz[:, 1]*dy[:, 0], det, out=np.zeros_like(det), where=det != 0)
        zdy = np.divide(dx[:, 0]*dz[:, 1]-dx[:, 1]*dz[:, 0], det, out=np.zeros_like(det), where=det != 0)
    for first in range(0, height, 24):
        block = np.flatnonzero((ymin < first + 24) & (ymax >= first))
        for py in range(first, min(first + 24, height)):
            y = py + .5
            ids = block[(ymin[block] <= y) & (ymax[block] > y)]
            if not len(ids):
                continue
            crosses = (edge_ymin[ids] <= y) & (edge_ymax[ids] > y)
            edge_x = xa[ids] + (y - ya[ids]) * slope[ids]
            starts = np.ceil(np.clip(np.where(crosses, edge_x, np.inf).min(axis=1) - .5, 0, width)).astype("i4")
            stops = np.ceil(np.clip(np.where(crosses, edge_x, -np.inf).max(axis=1) - .5, 0, width)).astype("i4")
            visible = stops > starts
            starts, stops, ids = starts[visible], stops[visible], ids[visible]
            if not len(ids):
                continue
            if shadow:
                delta = np.bincount(starts, minlength=width+1) - np.bincount(stops, minlength=width+1)
                image[py] = np.cumsum(delta[:-1]) > 0
                continue
            lengths = stops - starts
            ends = np.cumsum(lengths)
            samples = np.repeat(ids, lengths)
            cols = np.arange(ends[-1]) + np.repeat(starts - (ends-lengths), lengths)
            depth = za[samples, 0] + zdx[samples]*(cols+.5-xa[samples, 0]) + zdy[samples]*(y-ya[samples, 0])
            closest = np.full(width, -np.inf)
            np.maximum.at(closest, cols, depth)
            keep = depth == closest[cols]
            image[py, cols[keep]] = face_colors[samples[keep]]


def anchor_point(mesh):
    xyz, faces, _ = mesh
    bbox_center = (xyz[:, [0, 2]].min(axis=0) + xyz[:, [0, 2]].max(axis=0)) / 2
    heights = xyz[faces, 1]
    roofs = (np.ptp(heights, axis=1) == 0) & (heights[:, 0] > .2)
    if roofs.any():
        centers = xyz[faces[roofs]].mean(axis=1, dtype="f8")[:, [0, 2]]
        point = centers[np.argmin(np.square(centers - bbox_center).sum(axis=1))]
        method = "roof-triangle-nearest-bounds-centre"
    else:
        point, method = bbox_center, "bounds-centre-no-roofs"
    return project(np.array([[point[0], 0, point[1]]]))[0, :2], point.tolist(), method


def render_panel(meshes, projected, origin, scale):
    width, height = (side * SUPERSAMPLE for side in PANEL)
    scale *= SUPERSAMPLE
    rgb = np.empty((height, width, 3), dtype="u1")
    rgb[:] = BACKGROUND
    _, faces, colors = meshes[0]
    raster(rgb, projected[0], faces, colors, origin, scale)
    mask = np.zeros((height, width), dtype=bool)
    raster(mask, projected[1], meshes[1][1], None, origin, scale, shadow=True)
    # 지표 재질에서만 바닥 그림자를 받고 벽·지붕은 자기 정점 색을 유지한다.
    xyz = meshes[0][0]
    shadow_height = meshes[1][0][0, 1]
    ground_colors = np.unique(colors[xyz[:, 1] < shadow_height], axis=0)
    receives = np.zeros_like(mask)
    for color in ground_colors:
        receives |= (rgb == color).all(axis=2)
    rgb[mask & receives] = (rgb[mask & receives] * .74).astype("u1")
    return Image.fromarray(rgb).resize(PANEL, Image.Resampling.LANCZOS)


def scale_bar(draw, x, y, scale, metres):
    length = round(scale * metres)
    draw.rounded_rectangle((x-8, y-21, x+max(length, 45)+8, y+7), radius=4, fill="#17252c")
    draw.line((x, y, x+length, y), fill=INK, width=2)
    for px in (x, x+length):
        draw.line((px, y-4, px, y+1), fill=INK, width=2)
    draw.text((x, y-21), f"{metres} m", fill=INK, font=font(12))


def north_arrow(draw, x, y):
    # 정북 벡터를 같은 카메라에 투영한다.
    u, v, _ = project(np.array([[0, 0, 1]]))[0]
    norm = math.hypot(u, v)
    u, v = u / norm, v / norm
    top = (x+u*12, y+v*12)
    draw.ellipse((x-16,y-16,x+16,y+16), fill="#17252c")
    draw.line((x-u*8,y-v*8,*top), fill=INK, width=2)
    draw.polygon([top,(top[0]-u*5-v*3,top[1]-v*5+u*3),
                  (top[0]-u*5+v*3,top[1]-v*5-u*3)], fill=INK)
    draw.text((x+10,y-11), "N", font=font(12,True), fill=INK)


def render_one(task):
    cache, folder, code, expected_receipt = task
    receipt_path = cache / code / "receipt.json"
    raw = checked(receipt_path, expected_receipt) if expected_receipt else receipt_path.read_bytes()
    receipt = json.loads(raw)
    entry = receipt["entry"]
    if code != receipt["code"] or code != entry["code"]:
        raise ValueError("동 코드와 생성 기록 불일치")
    zipped = checked(cache / code / "mesh.bytes.gz", receipt["cacheFiles"]["mesh.bytes.gz"])
    blob = gzip.decompress(zipped)
    if digest(blob) != entry["sha256"].lower():
        raise ValueError(f"압축 해제한 HDIO 지문 불일치 {code}")
    meshes = read_hdio(blob, entry)
    projected = [project(mesh[0]) for mesh in meshes]
    points = projected[0][:, :2]
    low, high = points.min(axis=0), points.max(axis=0)
    overview_scale = float(min((np.asarray(PANEL)-36) / (high-low)))
    near, xz, method = anchor_point(meshes[0])
    whole = render_panel(meshes, projected, (low+high)/2, overview_scale)
    detail_scale = PANEL[0] / WIDTH_METRES
    detail = render_panel(meshes, projected, near, detail_scale)
    name = entry["name"]
    card = Image.new("RGB", (CARD_WIDTH, CARD_HEIGHT), "#1a2a32")
    draw = ImageDraw.Draw(card)
    draw.text((MARGIN, 13), f"{entry['district']}  ·  {name}", font=font(30, True), fill=INK)
    subtitle = ("기준동  ·  " if code == REFERENCE else "") + "행정동 디오라마  /  메시 검토"
    draw.text((CARD_WIDTH-MARGIN, 30), subtitle, font=font(16), fill=ACCENT, anchor="rm")
    detail_x = MARGIN + PANEL[0] + GAP
    draw.text((MARGIN, 65), "동 전체", font=font(17, True), fill=MUTED)
    draw.text((detail_x, 65), f"거리 확대  ·  가로 {WIDTH_METRES} m", font=font(17, True), fill=MUTED)
    y = 96
    card.paste(whole, (MARGIN, y))
    card.paste(detail, (detail_x, y))
    marker = ((near - (low+high)/2) * overview_scale + np.asarray(PANEL)/2)
    w, h = np.asarray(PANEL) / detail_scale * overview_scale / 2
    left, top = np.maximum(marker-[w,h], [0,0])
    right, bottom = np.minimum(marker+[w,h], np.asarray(PANEL)-1)
    draw.rectangle((MARGIN+left, y+top, MARGIN+right, y+bottom), outline=ACCENT, width=1)
    bar = min([10, 20, 50, 100, 200, 500, 1000], key=lambda m: abs(m * overview_scale - 58))
    scale_bar(draw, MARGIN+15, y+PANEL[1]-13, overview_scale, bar)
    scale_bar(draw, detail_x+15, y+PANEL[1]-13, detail_scale, 30)
    north_arrow(draw, MARGIN+PANEL[0]-27, y+24)
    north_arrow(draw, detail_x+PANEL[0]-27, y+24)
    caption = (f"건물 {entry['buildings']:,}조각    도로 {entry['roads']:,}    "
               f"지표 도보선 {entry['surfaceWalkLinks']:,}    공원 계획면 {entry['parkPlanFragments']:,}조각")
    draw.text((MARGIN, 496), caption, font=font(17, True), fill=INK)
    draw.text((MARGIN, 529), "2026년 10월 경계 후보  ·  내부 검토용  ·  정사영 메시 그림", font=font(14), fill=MUTED)
    path = folder / f"{code}.png"
    card.save(path)
    return {"code": code, "name": name, "district": entry["district"], "image": f"dong/{path.name}",
            "sha256": digest(path.read_bytes()), "meshSha256": digest(blob),
            "overviewPixelSha256": digest(whole.tobytes()), "detailPixelSha256": digest(detail.tobytes()),
            "sourceModuleSha256": receipt["moduleSha256"], "overviewPixelsPerMetre": overview_scale,
            "detailCentreXZMetres": xz, "detailCentreMethod": method}


def sheet(output, slug, title, rows):
    thumb = (295, 202)
    cols = min(len(rows), 3 if len(rows) <= 6 else 4)
    cell_h = thumb[1] + 45
    canvas = Image.new("RGB", (cols*(thumb[0]+12)+12, 86+math.ceil(len(rows)/cols)*cell_h), "#1a2a32")
    draw = ImageDraw.Draw(canvas)
    draw.text((16, 10), title, font=font(26, True), fill=INK)
    draw.text((16, 53), f"같은 거리 확대  ·  가로 {WIDTH_METRES} m  ·  구운 메시에서 생성한 내부 검토 그림", font=font(15), fill=MUTED)
    for i, row in enumerate(rows):
        x = 12 + i % cols * (thumb[0]+12)
        y = 85 + i // cols * cell_h
        with Image.open(output / row["image"]) as card:
            ox = MARGIN + PANEL[0] + GAP
            detail = card.crop((ox, 96, ox + PANEL[0], 96 + PANEL[1]))
            canvas.paste(detail.resize(thumb, Image.Resampling.LANCZOS), (x, y))
        label = row["name"] + ("  ·  기준" if row["code"] == REFERENCE else "")
        draw.text((x+3, y+thumb[1]+5), label, font=font(18, True), fill=ACCENT if row["code"] == REFERENCE else INK)
    path = output / (slug+".png")
    canvas.save(path)
    return path.name


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--all", action="store_true", help="캐시 카탈로그 전 동을 그린다.")
    group.add_argument("--code", action="append", help="선택한 행정동과 기준동을 그린다. 반복 가능.")
    parser.add_argument("--cache", type=Path, default=DEFAULT_CACHE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--workers", type=int, default=3)
    args = parser.parse_args()
    if args.workers < 1:
        parser.error("병렬 작업 수는 1 이상이어야 합니다.")
    catalog_file = args.cache / "catalog-all.json"
    catalog = {}
    if args.all or catalog_file.is_file():
        doc = json.loads(catalog_file.read_bytes())
        for item in doc["modules"]:
            code = item["code"]
            if not re.fullmatch(r"[0-9]{10}", code) or item["path"] != f"{code}/receipt.json" or code in catalog:
                parser.error("카탈로그 코드·경로가 잘못되었습니다.")
            catalog[code] = item["sha256"]
        if list(catalog) != doc["codes"] or len(catalog) != doc["moduleCount"]:
            parser.error("카탈로그 동 목록이 일치하지 않습니다.")
    codes = list(catalog) if args.all else args.code
    if args.all and REFERENCE not in codes:
        parser.error("전 동 카탈로그에 기준동이 없습니다.")
    if len(set(codes)) != len(codes) or any(not re.fullmatch(r"[0-9]{10}", code) for code in codes):
        parser.error("고유한 10자리 행정동 코드를 지정하세요.")
    codes = [REFERENCE] + [code for code in codes if code != REFERENCE]
    if catalog and set(codes) - catalog.keys():
        parser.error("선택한 동이 이 캐시 카탈로그에 없습니다.")
    if args.output.exists():
        parser.error("기존 비교판을 보존하도록 새 --output 디렉터리를 지정하세요.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=args.output.parent) as tmp:
        stage = Path(tmp) / "visual"
        folder = stage / "dong"
        folder.mkdir(parents=True)
        tasks = [(args.cache.resolve(), folder, code, catalog.get(code)) for code in codes]
        with ProcessPoolExecutor(max_workers=min(args.workers,len(codes))) as pool:
            rows = list(pool.map(render_one, tasks))
        districts = sorted({r["district"] for r in rows})
        sheets = {}
        for district in districts:
            others = [r for r in rows[1:] if r["district"] == district]
            sheets[district] = sheet(stage, district, district + "  /  면목제3·8동과 비교", [rows[0], *others])
        # 실제 실행판에서 이미 방문한 네 구의 견본. 다른 범위 선택 시 그 구의 첫 동으로 대체한다.
        preferred = {"광진구":"1121571000", "동대문구":"1123051500", "노원구":"1135056000", "중랑구":"1126056500"}
        examples = [rows[0]]
        for district in districts:
            members = [r for r in rows[1:] if r["district"] == district]
            if members:
                examples.append(next((r for r in members if r["code"] == preferred.get(district)), members[0]))
        summary = sheet(stage, "reference-comparison", "행정동별 거리 비교", examples)
        report = {"referenceCode":REFERENCE, "scope":"HDIO v2 software projection", "actualUnityExecution":False,
                  "rendererSha256":digest(Path(__file__).read_bytes()),
                  "libraries":{"numpy":np.__version__,"pillow":PILLOW_VERSION},
                  "source":"행정동별 구운 본체·그림자 메시", "lighting":"구운 정점 RGB와 그림자 메시 · 지표 음영 26%",
                  "view":{"orthographic":True,"yawDegrees":30,"elevationDegrees":60,"panelPixels":PANEL,"supersampling":SUPERSAMPLE,
                          "detailWidthMetres":WIDTH_METRES,"detailPixelsPerMetre":PANEL[0]/WIDTH_METRES},
                  "catalogSha256":digest(catalog_file.read_bytes()) if catalog_file.is_file() else None,
                  "moduleCount":len(rows),"comparison":summary,"districtSheets":sheets,"modules":rows}
        (stage/"index.json").write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n",encoding="utf-8")
        os.rename(stage, args.output)
    print(f"{len(rows)}개 행정동의 HDIO 화면 대조 그림을 저장했습니다. {args.output}")


if __name__ == "__main__":
    main()
