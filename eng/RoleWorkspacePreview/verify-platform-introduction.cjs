const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const base = process.env.SSALDDEL_USABILITY_PREVIEW_URL || 'http://127.0.0.1:5586';
  assert.equal(new URL(base).hostname, '127.0.0.1');
  const output = path.resolve('artifacts/local/integrated-platform-r105/ui');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const checks = [], errors = [], requests = [], captures = [];
  try {
    for (const width of [320, 390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if (url.origin !== base || url.pathname.startsWith('/api/')) {
          requests.push({ method: route.request().method(), kind: 'unexpected' });
          return route.abort();
        }
        return route.continue();
      });
      const page = await context.newPage();
      page.on('pageerror', error => errors.push(String(error)));
      for (const route of ['/services', '/quick']) {
        await page.goto(base + route);
        await page.locator('[data-platform-introduction]').waitFor();
        assert.match(await page.locator('h1').innerText(), /함께 이어가요/);
        assert.equal(await page.locator('.quick-intro__service-rows article').count(), 3);
        assert.ok(await page.locator('body').innerText().then(text => text.includes('운영 준비 중')));
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true);
        checks.push(`${width}:${route}:render-and-overflow`);
      }
      await page.locator('summary').filter({ hasText: '배송료와 작업비는 언제' }).click();
      assert.match(await page.locator('details[open]').innerText(), /현재 주간 자동 입금은 제공하지 않습니다/);
      assert.match(await page.locator('details[open]').innerText(), /당사자 직접 거래/);
      for (const href of await page.locator('a[href]').evaluateAll(links => links.map(link => link.href)))
        assert.equal(new URL(href).origin, base);
      checks.push(`${width}:terms-and-local-links`);
      const capture = path.join(output, `services-${width}.png`);
      await page.screenshot({ path: capture, fullPage: true }); captures.push(capture);
      if (width === 390) {
        await page.evaluate(() => scrollTo(0, 0));
        const viewport = path.join(output, 'services-390-viewport.png');
        await page.screenshot({ path: viewport }); captures.push(viewport);
      }
      await context.close();
    }
    assert.deepEqual(errors, []); assert.deepEqual(requests, []);
    const result = { checks, errors, unexpectedRequests: requests, captures, scope: 'loopback-render-only' };
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify({ passed: checks.length, captures: captures.length, errors: errors.length }));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
