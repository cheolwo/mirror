"""이미 저장한 실제 공통 Razor SSR만 보여주는 loopback 읽기 전용 검토기."""
import hashlib, html, json, re
from pathlib import Path
from http.server import BaseHTTPRequestHandler, HTTPServer

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/local/cargo-ai-device-r114'
STAGES = {'recommended': '추천 · 보수 표시 확인 필요', 'accepted': '수락 · 픽업지로 이동',
          'pickup-arrived': '픽업지 도착 · 사진 필요', 'picked-up': '픽업 완료 · 전달지로 이동',
          'dropoff-arrived': '전달지 도착 · 사진 필요', 'completed': '전달 완료 · 다음 업무 대기'}
ASSETS = {
    '/assets/mud.css': Path.home() / '.nuget/packages/mudblazor/9.5.0/staticwebassets/MudBlazor.min.css',
    '/assets/foundation.css': ROOT / 'Ssalddel.Ui.Common/wwwroot/Areas/App/css/role-app-foundation.css',
    '/assets/scoped.css': ROOT / 'Ssalddel.Ui.Common/obj/Debug/net10.0/scopedcss/projectbundle/Ssalddel.Ui.Common.bundle.scp.css'}

def sha(data): return hashlib.sha256(data).hexdigest()
def prepare():
    theme = (OUT / 'client/default-theme.html').read_text(encoding='utf-8')
    styles = ''.join(re.findall(r'<style\b[^>]*>.*?</style>', theme, flags=re.S))
    if not styles: raise RuntimeError('ActualDefaultThemeStyleMissing')
    routes = {key: (path.read_bytes(), 'text/css; charset=utf-8') for key, path in ASSETS.items()}
    manifest = {'sources': [{'path': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else path.name,
                             'sha256': sha(routes[key][0])} for key, path in ASSETS.items()],
                'themeSha256': sha(theme.encode()), 'frames': [], 'actualAndroid': False, 'interactive': False}
    for stage, label in STAGES.items():
        directory = OUT / 'client/r114-03' / stage
        for path in directory.glob('*.html'):
            source = path.read_bytes(); document = source.decode('utf-8')
            document = document.replace('<body>', '<body class="role-app">', 1)
            document = document.replace('<body class="role-app">', '<link rel="stylesheet" href="/assets/mud.css"><link rel="stylesheet" href="/assets/foundation.css"><link rel="stylesheet" href="/assets/scoped.css">' + styles + '<body class="role-app">', 1)
            body = document.encode('utf-8'); key = f'/frame/{stage}/{path.name}'
            routes[key] = (body, 'text/html; charset=utf-8')
            manifest['frames'].append({'path': str(path.relative_to(ROOT)), 'originalSha256': sha(source), 'styledSha256': sha(body)})
        primary = 'offer.html' if stage == 'recommended' else ('workspace.html' if stage in ('accepted', 'picked-up') else ('dropoff.html' if stage in ('dropoff-arrived', 'completed') else 'pickup.html'))
        navigation = ''.join(f'<a href="/view/{key}" aria-current="{str(key == stage).lower()}">{html.escape(value)}</a>' for key, value in STAGES.items())
        page = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><title>화물 업무 단계별 로컬 검토</title><style>
body{{margin:0;background:#eef2f5;color:#172b36;font:16px/1.6 "Segoe UI",sans-serif}}header{{padding:24px 32px;background:white;border-bottom:1px solid #dce5e9}}h1{{font-size:22px;margin:0}}header p{{margin:4px 0;color:#586b78}}main{{display:flex;gap:28px;max-width:1050px;margin:24px auto;align-items:flex-start}}nav{{width:260px}}nav a{{display:block;color:#234657;padding:12px;background:white;border:1px solid #dce5e9;text-decoration:none;margin-bottom:8px;border-radius:12px}}a[aria-current=true]{{border-color:#0f766e;background:#eaf5f3}}.frame{{width:414px;background:white;border-radius:16px;overflow:hidden;border:1px solid #dce5e9}}iframe{{display:block;border:0;width:414px;height:940px;pointer-events:none}}.note{{font-size:14px;max-width:210px;color:#586b78}}</style>
<header><h1>AI 추천부터 전달까지 · 실제 서버 응답과 공통 화면</h1><p>합성 자료의 읽기 전용 검토본 · Android 화면·지도·소리·버튼 조작 검증과 다릅니다.</p></header>
<main><nav>{navigation}</nav><div class="frame"><iframe sandbox src="/frame/{stage}/{primary}" title="{html.escape(label)}"></iframe></div><aside class="note"><b>{html.escape(label)}</b><p>1톤 가상 화물 한 건입니다. 추천의 기사 보수는 미확인으로 표시되어 보완이 필요합니다.</p><p>수락 이후 상태는 실제 API와 제품 ViewModel로 대조했습니다. 증빙은 시험용 PNG이며 실제 카메라 촬영이 아닙니다.</p></aside></main>'''
        page = page.replace('</style>', '.frame{flex:0 0 414px}nav{flex:0 0 180px}@media(max-width:1000px){main{margin:20px auto;max-width:650px;gap:20px}.note{display:none}header{padding:20px}}' + '</style>', 1)
        routes['/view/' + stage] = (page.encode(), 'text/html; charset=utf-8')
    (OUT / 'client/gallery-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return routes

if __name__ == '__main__':
    routes = prepare()
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            key = '/view/recommended' if self.path == '/' else self.path
            item = routes.get(key)
            if not item: self.send_error(404); return
            body, kind = item; self.send_response(200); self.send_header('Content-Type', kind)
            self.send_header('Cache-Control', 'no-store'); self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('Content-Length', str(len(body))); self.end_headers(); self.wfile.write(body)
        def log_message(self, *args): pass
    print('Read-only SSR gallery: http://127.0.0.1:5365/', flush=True)
    HTTPServer(('127.0.0.1', 5365), Handler).serve_forever()
