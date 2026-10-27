const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const base = process.env.SSALDDEL_QUALIFICATION_PREVIEW_URL || 'http://127.0.0.1:5593';
  assert.equal(new URL(base).hostname, '127.0.0.1');
  const output = path.resolve(process.env.SSALDDEL_QUALIFICATION_PREVIEW_OUTPUT || `artifacts/local/freight-registration-ui-${Date.now()}`);
  const artifactRoot = path.resolve('artifacts/local') + path.sep;
  assert.ok(output.startsWith(artifactRoot), 'Keep raw evidence inside artifacts/local.');
  fs.mkdirSync(output, { recursive: false });
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const results = [], errors = [], blocked = [];
  let currentPage;
  try {
    for (const width of [320, 390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
      const page = await context.newPage();
      currentPage = page;
      page.setDefaultTimeout(10000);
      page.on('pageerror', error => errors.push(String(error)));
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if ((url.origin !== base && !['data:', 'blob:'].includes(url.protocol)) || url.pathname.startsWith('/api/')) {
          blocked.push({ method: route.request().method(), pathname: url.pathname }); return route.abort();
        }
        return route.continue();
      });
      for (const scenario of ['driver', 'admin', 'platform', 'error']) {
        await page.goto(`${base}/qualification-readiness-preview/${scenario}`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        const area = page.locator(scenario === 'platform' ? '.platform-readiness' : '.qualification-panel');
        await area.waitFor();
        if (scenario === 'platform') {
          await page.getByText('추가 준비 필요', { exact: true }).waitFor();
          assert.equal(await page.getByRole('button', { name: '영업 활성화', exact: true }).count(), 0);
        } else if (scenario === 'error') {
          await page.getByRole('alert').filter({ hasText: '자격 상태를 확인하지 못했습니다' }).waitFor();
          assert.equal(await page.getByRole('button', { name: '검토 요청', exact: true }).count(), 0);
          assert.ok(await page.getByRole('button', { name: '최신 상태 확인', exact: true }).isEnabled());
        } else {
          await page.getByText('공식기관 확인 미완료', { exact: false }).waitFor();
          assert.equal(await page.getByRole('button', { name: '검토 유효로 기록', exact: true }).count(), scenario === 'admin' ? 1 : 0);
          assert.equal(await page.locator('.qualification-panel__evidence').count(), 5);
          if (scenario === 'driver') {
            await page.locator('.qualification-panel select').selectOption('FreightVehicle');
            await page.getByText('화물운송 종사자격', { exact: true }).waitFor();
            assert.equal(await page.locator('.qualification-panel__evidence').count(), 8);
            await page.locator('.qualification-panel select').selectOption('Motorcycle');
            await page.getByText('화물운송 종사자격', { exact: true }).waitFor({ state: 'hidden' });
            assert.equal(await page.locator('.qualification-panel__evidence').count(), 5);
          }
        }
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}:${scenario} overflow`);
        const file = `${scenario}-${width}.png`;
        await page.screenshot({ path: path.join(output, file), fullPage: true });
        results.push({ width, scenario, file });
      }
      await context.close();
    }
  } catch (error) {
    errors.push(String(error.stack || error));
    if (currentPage && !currentPage.isClosed()) {
      await currentPage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {});
      fs.writeFileSync(path.join(output, 'failure-body.txt'), await currentPage.locator('body').innerText().catch(() => 'unavailable'));
    }
    throw error;
  } finally {
    await browser.close();
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ base, results, errors, blocked }, null, 2));
  }
  assert.equal(errors.length, 0, errors.join('\n'));
  assert.equal(blocked.length, 0, 'No external or business API requests expected.');
  console.log(JSON.stringify({ passed: results.length, output }));
})().catch(error => { console.error(error); process.exitCode = 1; });
