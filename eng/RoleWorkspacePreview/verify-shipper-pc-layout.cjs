const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const origin = new URL(process.env.SSALDDEL_USABILITY_PREVIEW_URL || 'http://127.0.0.1:5594');
  assert.equal(origin.hostname, '127.0.0.1');
  const base = origin.origin;
  const output = path.resolve(process.env.SSALDDEL_SHIPPER_LAYOUT_OUTPUT ||
    `artifacts/local/shipper-pc-closeout-r112/layout-ui-${Date.now()}`);
  assert.equal(fs.existsSync(output), false, 'Use a new result directory to preserve prior evidence.');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const checks = [], errors = [], blockedRequests = [], captures = [];
  try {
    for (const width of [320, 390, 768, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 900 }, locale: 'ko-KR' });
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if (url.origin !== base || url.pathname.startsWith('/api/')) {
          blockedRequests.push({ method: route.request().method(), path: url.pathname });
          return route.abort();
        }
        return route.continue();
      });
      const page = await context.newPage();
      page.on('pageerror', error => errors.push(String(error)));
      for (const scenario of ['input', 'summary', 'completed', 'list']) {
        await page.goto(`${base}/shipper-pc-layout-preview/${scenario}`);
        await page.locator(`[data-shipper-pc-layout-preview="${scenario}"]`).waitFor();
        await page.locator('.shipper-mobile-shell__bottom-link').last().waitFor();
        assert.equal(await page.locator('.shipper-mobile-shell__bottom-link').count(), 4);
        const geometry = await page.locator('.shipper-mobile-shell').evaluate(element => ({
          width: element.getBoundingClientRect().width,
          pageOverflow: document.documentElement.scrollWidth > innerWidth + 1,
          contentOverflow: element.querySelector('main').scrollWidth > element.querySelector('main').clientWidth + 1
        }));
        assert.equal(geometry.pageOverflow, false, `${width}/${scenario}: page overflow`);
        assert.equal(geometry.contentOverflow, false, `${width}/${scenario}: content overflow`);
        assert.ok(Math.abs(geometry.width - Math.min(width, width >= 768 ? 1180 : 520)) < 2);
        checks.push({ width, scenario, check: 'existing-shell-width-navigation-and-overflow', geometry });
        if (scenario === 'input') {
          const fields = page.locator('.shipper-request-screen .mud-grid-item');
          const first = await fields.nth(0).boundingBox(), second = await fields.nth(1).boundingBox();
          assert.ok(first && second);
          assert.equal(second.x > first.x + 20, width >= 960, 'Existing Mud grid must respond to viewport.');
          const name = page.getByLabel('화물 종류', { exact: true });
          await name.fill('입력 유지 확인용 예시');
          await page.getByRole('button', { name: '화주 메뉴 열기', exact: true }).click();
          assert.equal(await page.locator('.shipper-mobile-shell__drawer nav a').count(), 12);
          await page.getByRole('button', { name: '메뉴 닫기', exact: true }).click();
          assert.equal(await name.inputValue(), '입력 유지 확인용 예시');
          checks.push({ width, scenario, check: 'existing-input-grid-drawer-and-draft-retained' });
        } else if (scenario === 'summary' || scenario === 'completed') {
          const columns = await page.locator('.shipper-summary__grid').evaluate(element =>
            getComputedStyle(element).gridTemplateColumns.split(' ').filter(Boolean).length);
          assert.equal(columns, width <= 720 ? 1 : 2);
          const actionsDisplay = await page.locator('.shipper-detail-screen__actions').evaluate(element =>
            getComputedStyle(element).display);
          assert.equal(actionsDisplay, width < 768 ? 'grid' : 'flex');
          checks.push({ width, scenario, check: 'shared-summary-grid-and-actions', columns, actionsDisplay });
        } else {
          assert.equal(await page.locator('[data-preview-request-table]').isVisible(), width >= 768);
          assert.equal(await page.locator('[data-preview-request-cards]').isVisible(), width < 768);
          checks.push({ width, scenario, check: 'shared-table-and-card-visibility' });
        }
        const capture = path.join(output, `${scenario}-${width}.png`);
        await page.screenshot({ path: capture });
        captures.push(capture);
      }
      await context.close();
    }
    assert.deepEqual(errors, []);
    assert.deepEqual(blockedRequests, []);
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({
      scope: 'Browser consumption of linked MAUI layout/CSS and existing common components only; not Windows native/API/device validation.',
      checks, errors, blockedRequests, captures
    }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, captures: captures.length, output }));
  } catch (error) {
    fs.writeFileSync(path.join(output, 'failed.json'), JSON.stringify({
      error: String(error), checks, errors, blockedRequests, captures
    }, null, 2));
    throw error;
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
