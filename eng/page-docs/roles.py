"""기존 페이지 색인의 역할별 읽기 전용 뷰. 필요성 판단과 실행 증거는 수동 검토만 승격한다."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/ProjectOverview/page-docs"
ROLES = {
    "community": "커뮤니티·일반 이용자", "orderer": "주문자", "shipper": "화주·판매자",
    "driver": "기사", "restaurant": "음식점", "warehouse": "창고·마트 작업자",
    "admin": "플랫폼 관리자", "hr": "인사 담당자", "unity": "Unity 검토 담당자",
    "common": "공통 진입·인증·설정", "unknown": "역할 확인 필요",
}
OWNER_ROLE = {
    "DriverApp": "driver", "FDriverApp": "driver", "OrdererApp": "orderer",
    "RestaurantDeskApp": "restaurant", "SellerApp": "shipper", "SsalddelApp": "shipper",
    "WarehouseManagerApp": "warehouse", "HumanResourcesManagerApp": "hr",
    "SsalddelAdmin": "admin", "SsalddelAdminApp": "admin",
    "Ssalddel.Web.CommunityApp": "community", "Ssalddel.Web.OrdererApp": "orderer",
    "Ssalddel.Web.ShipperApp": "shipper", "Ssalddel.Web.DriverApp": "driver",
    "Ssalddel.Web.WarehouseApp": "warehouse", "Ssalddel.Web.UnityReviewApp": "unity",
}
EXCLUDED = {"bin", "obj", ".vs", "artifacts", ".artifacts", "vendor", "node_modules", ".worktrees"}


def role_for(page):
    owner = page["owner"]
    routes = page["routes"]
    available = sorted({OWNER_ROLE[host] for host in page["hosts"] if host in OWNER_ROLE})
    if owner in {"SsalddelAdmin", "SsalddelAdminApp", "RestaurantDeskApp", "FDriverApp"}:
        primary = OWNER_ROLE[owner]
        reason = "역할 전용 프로젝트 소유 문맥"
    elif routes and all(route in {"/", "/login", "/not-found", "/Error"} for route in routes):
        primary, reason = "common", "진입·인증·오류 route; 포함 호스트는 사용 문맥 후보"
    else:
        prefixes = [("/community", "community"), ("/information", "community"),
                    ("/driver", "driver"), ("/food-delivery", "driver"),
                    ("/warehouse", "warehouse"), ("/work", "warehouse"), ("/scan", "warehouse"),
                    ("/shipper", "shipper"), ("/global", "shipper"),
                    ("/orderer", "orderer"), ("/food", "orderer"), ("/hr", "hr")]
        primary = next((role for prefix, role in prefixes
                        if any(route == prefix or route.startswith(prefix + "/") for route in routes)),
                       OWNER_ROLE.get(owner, available[0] if len(available) == 1 else "common" if available else "unknown"))
        reason = "route 업무 계열 및 프로젝트 소유 문맥; 권한 판정은 별도"
    return primary, sorted(set(available + ([primary] if primary != "common" else []))), reason


def task_group(page):
    text = " ".join(page["routes"] + [page["source"]]).lower()
    for group, markers in [
        ("진입·로그인", ["login", "rootredirect", "roleroot", "not-found", "notfound"]),
        ("정산·결제·계좌", ["settlement", "payment", "bank", "정산", "계좌"]),
        ("알림·설정", ["settings", "notification", "policy", "설정", "알림"]),
        ("음식 주문·조리·배달", ["food", "restaurant", "menus", "orders", "preparation"]),
        ("배차·운송 수행", ["driver", "transport", "dispatch", "recommendation", "상차", "하차"]),
        ("창고·재고·출고", ["warehouse", "inbound", "outbound", "picking", "inventory", "packing"]),
        ("참여·공동주문·무역", ["group", "import", "export", "customs", "global"]),
        ("커뮤니티·공공정보", ["community", "information", "regional", "agricultural"]),
    ]:
        if any(marker in text for marker in markers):
            return group
    return "기타 업무·개별 검토"


def build():
    source_catalog = json.loads((DOCS / "current-pages.json").read_text(encoding="utf-8-sig"))
    reviews = json.loads((DOCS / "role-reviews.json").read_text(encoding="utf-8-sig"))
    overrides = {entry["source"]: entry for entry in reviews["pages"]}
    pages = []
    for original in source_catalog["pages"]:
        page = dict(original, kind="RazorRoute", key=original["source"])
        pages.append(page)
    # Application/ContentView/Shell은 페이지 수에 포함하지 않는다. 제품 ContentPage만 추가한다.
    for project in sorted(ROOT.glob("*/*.csproj")):
        if "<UseMaui>true</UseMaui>" not in project.read_text(encoding="utf-8-sig"):
            continue
        for path in sorted(project.parent.rglob("*.xaml")):
            if not path.is_file() or EXCLUDED.intersection(path.parts):
                continue
            text = path.read_text(encoding="utf-8-sig")
            if not re.search(r"<ContentPage\b", text):
                continue
            source = path.relative_to(ROOT).as_posix()
            pages.append(dict(key=source, id="native:" + source, owner=project.stem, source=source,
                              kind="NativeContentPage", routes=[], hosts=[project.stem], detail=None,
                              review="InventoryOnly"))
    for page in pages:
        primary, roles, reason = role_for(page)
        text = (ROOT / page["source"]).read_text(encoding="utf-8-sig")
        page.update(primaryRole=primary, roleContexts=roles, classificationEvidence=reason,
                    taskGroup=task_group(page), sourceSha256=hashlib.sha256(text.encode()).hexdigest(),
                    recommendation="판단 보류", reviewLevel="필요성 미검토",
                    evidenceRefs=[], runtimeVerified=False)
        page.update(recommendation="판단 보류", reviewLevel="필요성 미검토",
                    reviewReason="페이지 존재·호스트 포함은 필요성·접근 권한·실행 완료의 증거가 아닙니다.")
        if page["source"] in overrides:
            review = overrides[page["source"]]
            if review.get("sourceSha256") and review["sourceSha256"] != page["sourceSha256"]:
                page.update(reviewLevel="검토 후 소스 변경", reviewReason="소스가 변경되어 재검토가 필요합니다.")
            else:
                page.update({key: value for key, value in review.items() if key != "source"})
    known = {page["source"] for page in pages}
    missing = set(overrides) - known
    if missing:
        raise ValueError("검토 기록의 소스를 찾을 수 없음: " + ", ".join(sorted(missing)))
    return {"schema": "role-page-review.r1", "basis": "기존 소스 색인 + MAUI ContentPage + 명시적 검토 기록",
            "runtimeVerified": False, "roles": ROLES, "pages": pages, "integrationCandidates": reviews["integrationCandidates"],
            "counts": {"physicalRazorPages": len(source_catalog["pages"]),
                       "nativeContentPages": sum(page["kind"] == "NativeContentPage" for page in pages),
                       "uniqueSources": len(pages), "primaryRoles": dict(Counter(page["primaryRole"] for page in pages)),
                       "recommendations": dict(Counter(page["recommendation"] for page in pages))}}


def markdown(data):
    lines = ["# 역할별 페이지와 통합 판단표", "", "기존 앱을 다듬기 위한 개발 검토 색인입니다. [검색·필터 보기](role-pages.html), [원본 소스·호스트 목록](current-pages.md), [첫 모바일 검증 결과](mobile-polish-r1.md).", "",
             f"Razor {data['counts']['physicalRazorPages']}개 + 네이티브 ContentPage {data['counts']['nativeContentPages']}개. 소스는 전체에서 한 번만 세며 역할 문맥은 중복 참조할 수 있습니다.", "",
             "역할 분류와 유지·통합 판단, 실제 Android 검증은 서로 다른 상태입니다. 미검토는 불필요를 뜻하지 않으며 후속 버전 페이지를 자동 폐기하지 않습니다.", "",
             "## 통합 후보", "", "| 묶음 | 제안 | 유지할 책임 |", "| --- | --- | --- |"]
    for group in data["integrationCandidates"]:
        lines.append(f"| {group['name']} | {group['proposal']} | {group['preserve']} |")
    for role, label in ROLES.items():
        selected = [page for page in data["pages"] if page["primaryRole"] == role]
        if not selected:
            continue
        lines += ["", f"## {label} · 주 분류 {len(selected)}개", "", "| 페이지 | 업무 묶음 | 판단·검토 | 근거 |", "| --- | --- | --- | --- |"]
        for page in selected:
            link = "../../../" + page["source"]
            title = Path(page["source"]).stem
            route = " / ".join(page["routes"])
            lines.append(f"| [{title}]({link})<br>`{page['id']}`<br>{route} | {page['taskGroup']} | {page['recommendation']} · {page['reviewLevel']} | {page['reviewReason']} |")
    return "\n".join(lines) + "\n"


def html_view(data):
    payload = json.dumps(data, ensure_ascii=False).replace("<", "\\u003c")
    return """<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>역할별 기존 페이지 검토</title>
<style>body{font:16px/1.6 system-ui;background:#f4f6f8;color:#172b37;margin:0}main{max-width:1150px;margin:auto;padding:24px}h1{font-size:25px}aside,.card{background:white;border:1px solid #d4dde3;border-radius:12px;padding:18px;margin:12px 0}label{display:inline-block;margin:6px}input,select{font:inherit;padding:8px;max-width:100%;box-sizing:border-box}nav{position:sticky;top:0;background:#f4f6f8;padding:10px 0}small,.muted{color:#536675}.route{overflow-wrap:anywhere}a{color:#075a8c}h2{font-size:19px;margin:4px 0}.badge{display:inline-block;background:#e6eff4;border-radius:5px;padding:2px 7px;font-size:13px;margin-right:8px}.card{overflow-wrap:anywhere}summary{cursor:pointer}#cards{min-width:0}@media(max-width:600px){main{padding:12px}label{display:block}input,select{width:100%}}</style>
<main><h1>역할별 기존 페이지 검토</h1><aside>기존 앱의 화면을 찾아보고 유지·통합·보류 판단을 확인합니다. 소스 분류와 실행 검증은 별개입니다. <a href="mobile-polish-r1.md">첫 모바일 검증 결과</a> · <a href="role-pages.md">전체 표</a></aside>
<nav><label>역할 <select id="role"><option value="">전체</option></select></label><label>판단 <select id="decision"><option value="">전체</option><option>유지</option><option>통합 후보</option><option>보류</option><option>판단 보류</option><option>폐기 후보</option></select></label><label>이름·경로 검색 <input id="q" type="search"></label><p id="count" aria-live="polite"></p></nav><section id="cards"></section></main>
<script type="application/json" id="data">""" + payload + """</script><script>
const data=JSON.parse(document.querySelector('#data').textContent), role=document.querySelector('#role'), decision=document.querySelector('#decision'), q=document.querySelector('#q');
for(const [value,label] of Object.entries(data.roles)){const o=document.createElement('option');o.value=value;o.textContent=label;role.append(o)}
const el=(tag,text,cls)=>{const n=document.createElement(tag);n.textContent=text;if(cls)n.className=cls;return n};
function render(){const pages=data.pages.filter(p=>(!role.value||p.primaryRole===role.value||p.roleContexts.includes(role.value))&&(!decision.value||p.recommendation===decision.value)&&(!q.value||JSON.stringify(p).toLowerCase().includes(q.value.toLowerCase())));document.querySelector('#count').textContent=`${pages.length}개 소스 · 역할 후보에 공통 참조 포함 · 전체 ${data.counts.uniqueSources}개`;const cards=document.querySelector('#cards');cards.replaceChildren();for(const p of pages){const c=el('article','','card');c.append(el('span',data.roles[p.primaryRole],'badge'),el('span',p.recommendation+' · '+p.reviewLevel,'badge'),el('h2',p.source.split('/').at(-1)),el('p',p.taskGroup+' · '+p.kind),el('p',p.routes.join(' | ')||'네이티브 화면: 앱 진입 경로는 소스에서 확인','route'),el('p',p.reviewReason));const d=el('details','');d.append(el('summary','소스·근거·호스트 보기'));const a=el('a',p.source);a.href='../../../'+p.source;d.append(a,el('p','포함 호스트: '+p.hosts.join(', ')),el('p',p.classificationEvidence),el('p','식별자: '+p.id));for(const ref of p.evidenceRefs){const a=el('a',ref);a.href='../../../'+ref;d.append(el('p',''),a)}if(p.detail){const a=el('a','기존 페이지→코드→DB 문서');a.href='../../../'+p.detail;d.append(el('p',''),a)}c.append(d);cards.append(c)}}for(const n of [role,decision,q])n.addEventListener('input',render);render();</script></html>"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    data = build()
    outputs = {DOCS / "role-pages.json": json.dumps(data, ensure_ascii=False, indent=2) + "\n",
               DOCS / "role-pages.md": markdown(data), DOCS / "role-pages.html": html_view(data)}
    for path, value in outputs.items():
        if args.write:
            path.write_text(value, encoding="utf-8")
        elif not path.exists() or path.read_text(encoding="utf-8-sig") != value:
            print("역할 색인 갱신 필요: " + path.relative_to(ROOT).as_posix())
            return 1
    print(json.dumps(data["counts"], ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
