const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require('playwright');

(async () => {
  const base = process.env.SSALDDEL_MOBILE_CONTEXT_PREVIEW_URL || 'http://127.0.0.1:5595';
  assert.equal(new URL(base).hostname, '127.0.0.1');
  const output = path.resolve(process.env.SSALDDEL_VEHICLE_RECOMMENDATION_PREVIEW_OUTPUT || `artifacts/local/shipper-loading-r75-${Date.now()}`);
  assert.ok(output.startsWith(path.resolve('artifacts/local') + path.sep));
  fs.mkdirSync(output, { recursive: false });
  const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
  const binaryRoot = path.resolve(process.env.SSALDDEL_PREVIEW_BINARY_ROOT || 'eng/RoleWorkspacePreview/bin/Debug/net10.0');
  const inputHashes = Object.fromEntries([
    __filename,
    'eng/RoleWorkspacePreview/ShipperMobileContextPreviewFixture.cs',
    path.join(binaryRoot, 'RoleWorkspacePreview.dll'),
    path.join(binaryRoot, 'Ssalddel.Ui.Common.dll'),
    path.join(binaryRoot, 'Ssalddel.Contracts.dll')
  ].map(file => [path.relative(process.cwd(), file), hash(file)]));
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const startedAtUtc = new Date().toISOString(), checks = [], captures = [], errors = [], blocked = [];
  let page;
  try {
    for (const width of [320, 390]) {
      for (const scenario of ['success', 'empty', 'failure', 'stale']) {
        const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
        page = await context.newPage(); page.setDefaultTimeout(15000);
        page.on('pageerror', error => errors.push(String(error)));
        await context.route('**/*', route => {
          const url = new URL(route.request().url());
          if ((!['data:', 'blob:'].includes(url.protocol) && url.origin !== base) || url.pathname.startsWith('/api/')) {
            blocked.push({ pathname: url.pathname, method: route.request().method() }); return route.abort();
          }
          return route.continue();
        });
        await page.goto(`${base}/shipper/request/vehicle?recommendation=${scenario}`, { waitUntil: 'domcontentloaded' });
        const panel = page.getByTestId('shipper-vehicle-recommendation');
        await panel.waitFor();
        const query = panel.getByRole('button', { name: '차량 추천 확인', exact: true });
        await query.waitFor({ state: 'visible' });
        const status = page.getByTestId('vehicle-recommendation-status');
        if (scenario === 'stale') {
          await page.waitForFunction(() => document.querySelector('[data-testid="vehicle-recommendation-status"]')?.textContent.includes('다시 확인'));
          assert.equal(await page.getByRole('radio').count(), 0);
          assert.ok(await page.locator('.shipper-mobile-navigation__next').isDisabled());
        } else {
          await query.click();
          if (scenario === 'success') {
            const candidate = page.getByRole('radio', { name: /1톤 카고/ });
            await candidate.waitFor(); await candidate.click();
            await page.waitForFunction(() => document.querySelector('[role="radio"][aria-checked="true"]'));
            assert.equal(await candidate.getAttribute('aria-checked'), 'true');
            assert.ok((await status.innerText()).includes('시간·경로'));
          } else {
            await page.waitForFunction(expected => document.querySelector('[data-testid="vehicle-recommendation-status"]')?.textContent.includes(expected),
              scenario === 'empty' ? '찾지 못했습니다' : '다시 확인');
            assert.equal(await page.getByRole('radio').count(), 0);
            assert.ok(await page.locator('.shipper-mobile-navigation__next').isDisabled());
          }
        }
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${width}/${scenario}: horizontal overflow`);
        const file = `vehicle-${scenario}-${width}.png`;
        await page.screenshot({ path: path.join(output, file), fullPage: true, animations: 'disabled' });
        captures.push({ file, width, scenario, sha256: hash(path.join(output, file)) });
        checks.push({ width, scenario, status: await status.innerText(), candidates: await page.getByRole('radio').count() });
        await context.close();
      }
    }
    for (const width of [320, 390]) {
      const mobile = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
      page = await mobile.newPage(); page.on('pageerror', error => errors.push(String(error)));
      await page.goto(`${base}/shipper/request/package?recommendation=success`, { waitUntil: 'domcontentloaded' });
      await page.getByLabel('포장 1개 길이 mm', { exact: true }).waitFor();
      await page.getByTestId('cargo-loading-summary').waitFor();
      assert.ok((await page.getByTestId('cargo-loading-summary').innerText()).includes('0.072'));
      assert.ok(await page.locator('label').filter({ hasText: '바닥에서 방향 바꾸기' }).isVisible());
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      const file = `package-${width}.png`;
      await page.evaluate(() => document.activeElement?.blur());
      await page.screenshot({ path: path.join(output, file), fullPage: true, animations: 'disabled' });
      captures.push({ file, width, scenario: 'package', sha256: hash(path.join(output, file)) });
      checks.push({ width, check: 'package-input-and-calculation-visible' });
      await mobile.close();
    }
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: 'ko-KR' });
    page = await context.newPage(); page.on('pageerror', error => errors.push(String(error)));
    await page.goto(`${base}/mobile-context-preview/desktop?recommendation=success`, { waitUntil: 'domcontentloaded' });
    await page.getByTestId('cargo-loading-summary').waitFor();
    await page.getByRole('button', { name: '차량 추천 확인', exact: true }).click();
    await page.getByRole('radio', { name: /1톤 카고/ }).waitFor();
    assert.ok((await page.getByTestId('cargo-loading-summary').innerText()).includes('0.072'));
    assert.ok(await page.getByLabel('포장 1개 길이 mm', { exact: true }).isVisible());
    assert.ok(await page.locator('label').filter({ hasText: '바닥에서 방향 바꾸기' }).isVisible());
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    const desktopFile = 'desktop-loading-1280.png';
    await page.screenshot({ path: path.join(output, desktopFile), fullPage: true, animations: 'disabled' });
    captures.push({ file: desktopFile, width: 1280, scenario: 'success', sha256: hash(path.join(output, desktopFile)) });
    checks.push({ width: 1280, check: 'desktop-raw-and-effective-volume-rotation-and-server-response' });
    await context.close();
    assert.equal(errors.length, 0, errors.join('\n'));
    assert.equal(blocked.length, 0);
  } catch (error) {
    errors.push(String(error.stack || error));
    if (page && !page.isClosed()) await page.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {});
    throw error;
  } finally {
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ startedAtUtc, completedAtUtc: new Date().toISOString(), base,
      scope: 'Actual common Razor rendering with explicitly synthetic in-memory recommendation responses; no real API, DB, vehicle validation, AI learning, APK or phone.',
      inputHashes, checks, captures, errors, blocked }, null, 2));
    await browser.close();
  }
  console.log(JSON.stringify({ passed: checks.length, captures: captures.length, output }));
})().catch(error => { console.error(error); process.exitCode = 1; });
