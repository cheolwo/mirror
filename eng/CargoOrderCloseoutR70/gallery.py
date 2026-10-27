"""현재 제품이 렌더한 r70 HTML만 제공하는 loopback 읽기 전용 화면 기록."""
import hashlib
import argparse
import html
import json
import re
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/local/cargo-order-closeout-r70'
ASSETS = {
    '/assets/mud.css': Path.home() / '.nuget/packages/mudblazor/9.5.0/staticwebassets/MudBlazor.min.css',
    '/assets/foundation.css': ROOT / 'Ssalddel.Ui.Common/wwwroot/Areas/App/css/role-app-foundation.css',
    '/assets/scoped.css': ROOT / 'Ssalddel.Ui.Common/obj/Debug/net10.0/scopedcss/projectbundle/Ssalddel.Ui.Common.bundle.scp.css'
}
STAGES = {'recommended': '추천·보수 확인', 'accepted': '수락·픽업 이동', 'pickup-arrived': '픽업 도착',
          'picked-up': '픽업 완료', 'dropoff-arrived': '전달 도착', 'completed': '전달 완료',
          'completed-r2': '전달 완료 · 역할별 재조회', 'completed-r3': '전달 완료 · 역할별 최종 확인'}


def digest(value):
    return hashlib.sha256(value).hexdigest()


def prepare(case_id):
    source = (OUT / 'client' / case_id / 'accepted/offer-before.html').read_text(encoding='utf-8')
    theme = ''.join(re.findall(r'<style\b[^>]*>.*?</style>', source, flags=re.S))
    if not theme:
        raise RuntimeError('ProductDefaultThemeMissing')
    routes = {key: (path.read_bytes(), 'text/css; charset=utf-8') for key, path in ASSETS.items()}
    manifest = {'sources': [{'asset': key, 'sha256': digest(data[0])} for key, data in routes.items()],
                'themeSha256': digest(theme.encode()), 'frames': [], 'staticProductSsrOnly': True,
                'android': False, 'interactiveWorkflow': False}
    frames = []
    for role, folder in [('driver', OUT / 'client' / case_id), ('roles', OUT / 'roles')]:
        for path in folder.glob('*/*.html'):
            stage = path.parent.name
            if stage not in STAGES:
                continue
            original = path.read_bytes()
            doc = original.decode('utf-8')
            styles = ''.join(f'<link rel="stylesheet" href="{key}">' for key in ASSETS) + theme
            doc = doc.replace('</head>', styles + '</head>', 1) if '</head>' in doc else doc.replace('<body', styles + '<body', 1)
            doc = doc.replace('<body>', '<body class="role-app">', 1)
            key = f'/frame/{role}/{stage}/{path.name}'
            body = doc.encode()
            routes[key] = (body, 'text/html; charset=utf-8')
            frames.append((role, stage, path.name, key))
            manifest['frames'].append({'path': str(path.relative_to(ROOT)), 'sourceSha256': digest(original), 'styledSha256': digest(body)})
    navigation = ''.join(f'<a href="/view/{role}/{stage}/{name}">{html.escape(STAGES[stage])} · {html.escape(name)}</a>'
                         for role, stage, name, _ in frames)
    for role, stage, name, key in frames:
        label = ('기사' if role == 'driver' else '화주·운영자') + ' · ' + STAGES[stage]
        width = 390 if role == 'driver' else 1024
        page = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><title>{html.escape(label)}</title>
<style>body{{margin:0;background:#eef2f5;color:#18313e;font:15px/1.6 "Segoe UI",sans-serif}}header{{background:white;padding:16px 24px;border-bottom:1px solid #dce5e9}}h1{{font-size:20px;margin:0}}p{{margin:4px 0}}main{{display:flex;gap:20px;padding:20px;align-items:flex-start}}nav{{flex:0 0 210px}}nav a{{display:block;border-radius:8px;background:white;margin:0 0 6px;padding:6px 10px;color:#235263;text-decoration:none;font-size:12px}}iframe{{border:1px solid #dce5e9;background:white;border-radius:12px;width:{width}px;height:1100px;flex-shrink:0;pointer-events:none}}</style>
<header><h1>{html.escape(label)}</h1><p>같은 시험 의뢰의 실제 서버 응답·제품 화면 기록 · 읽기 전용 · Android/지도/알림음/실제 입금 미검증</p></header>
<main><nav>{navigation}</nav><iframe sandbox src="{key}" title="{html.escape(label)}"></iframe></main>'''
        routes[f'/view/{role}/{stage}/{name}'] = (page.encode(), 'text/html; charset=utf-8')
    (OUT / 'gallery-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return routes


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--case', choices=['r70-01', 'r70-02', 'r70-03'], default='r70-02')
    routes = prepare(parser.parse_args().case)

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            key = '/view/driver/accepted/offer-before.html' if self.path == '/' else self.path
            item = routes.get(key)
            if item is None:
                self.send_error(404)
                return
            body, content_type = item
            self.send_response(200)
            self.send_header('Content-Type', content_type)
            self.send_header('Content-Length', str(len(body)))
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *_):
            pass

    print('Read-only current product SSR: http://127.0.0.1:5366/', flush=True)
    HTTPServer(('127.0.0.1', 5366), Handler).serve_forever()
