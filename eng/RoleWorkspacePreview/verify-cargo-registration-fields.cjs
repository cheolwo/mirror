const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const base = process.env.SSALDDEL_CARGO_FIELDS_PREVIEW_URL || 'http://127.0.0.1:5596';
  assert.equal(new URL(base).hostname, '127.0.0.1', 'Use the isolated preview only.');
  const output = path.resolve(process.env.SSALDDEL_CARGO_FIELDS_PREVIEW_OUTPUT || `artifacts/local/cargo-registration-fields-${Date.now()}`);
  assert.ok(output.startsWith(path.resolve('artifacts/local') + path.sep));
  fs.mkdirSync(output, { recursive: false });
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const checks = [], errors = [], blocked = [];
  let currentPage;
  try {
    for (const width of [390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 950 }, locale: 'ko-KR' });
      const page = await context.newPage(); currentPage = page; page.setDefaultTimeout(10000);
      page.on('pageerror', error => errors.push(String(error)));
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if ((url.origin !== base && !['data:', 'blob:'].includes(url.protocol)) || url.pathname.startsWith('/api/')) {
          blocked.push({ method: route.request().method(), pathname: url.pathname }); return route.abort();
        }
        return route.continue();
      });
      await page.goto(`${base}/shipper-pc-layout-preview/input`, { waitUntil: 'domcontentloaded', timeout: 15000 });
      const cargo = page.locator('.shipper-request-screen[data-step="cargo"]');
      await cargo.waitFor();
      const fields = {
        weight: cargo.getByLabel('전체 중량 kg (포장·깔판 포함)', { exact: true }),
        length: cargo.getByLabel('포장한 화물 길이 mm', { exact: true }),
        width: cargo.getByLabel('포장한 화물 너비 mm', { exact: true }),
        height: cargo.getByLabel('포장한 화물 높이 mm', { exact: true })
      };
      for (const field of Object.values(fields)) await field.waitFor();
      await cargo.getByText('포장한 화물의 길이(mm)를 0보다 크게 입력해 주세요.', { exact: true }).waitFor();
      await cargo.getByText('포장한 화물의 너비(mm)를 0보다 크게 입력해 주세요.', { exact: true }).waitFor();
      await cargo.getByText('포장한 화물의 높이(mm)를 0보다 크게 입력해 주세요.', { exact: true }).waitFor();
      checks.push({ width, check: 'packaging-inclusive-weight-and-three-dimensions-with-missing-field-guidance' });
      await fields.length.fill('300'); await fields.width.fill('200'); await fields.height.fill('100');
      await fields.height.press('Tab');
      await cargo.getByText('화물 종류·전체 중량·포장 크기를 입력했습니다. 다음 단계에서 상하차 구간을 확인해 주세요.', { exact: true }).waitFor();
      await fields.weight.fill('0'); await fields.weight.press('Tab');
      await cargo.getByText('포장·깔판을 포함한 전체 중량(kg)을 0보다 크게 입력해 주세요.', { exact: true }).waitFor();
      await fields.weight.fill('3.5'); await fields.weight.press('Tab');
      await cargo.getByText('화물 종류·전체 중량·포장 크기를 입력했습니다. 다음 단계에서 상하차 구간을 확인해 주세요.', { exact: true }).waitFor();
      assert.equal(await fields.length.inputValue(), '300');
      assert.equal(await fields.width.inputValue(), '200');
      assert.equal(await fields.height.inputValue(), '100');
      assert.ok(await cargo.getByText('크기는 포장한 상태의 전체 길이·너비·높이입니다. 1cm는 10mm입니다.', { exact: false }).isVisible());
      const geometry = await cargo.evaluate(element => ({
        pageOverflow: document.documentElement.scrollWidth > innerWidth + 1,
        contentOverflow: element.scrollWidth > element.clientWidth + 1
      }));
      assert.equal(geometry.pageOverflow, false); assert.equal(geometry.contentOverflow, false);
      if (width < 768) {
        // 기존 셸은 본문을 따로 스크롤합니다. 포커스에 따른 자동 스크롤 대신 새 입력 항목의 시작을 보여 줍니다.
        await fields.weight.evaluate(input => {
          const main = input.closest('main');
          const control = input.closest('.mud-input-control');
          if (main && control) main.scrollTop += control.getBoundingClientRect().top - main.getBoundingClientRect().top - 24;
        });
        await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => resolve())));
      }
      const file = `cargo-input-${width}.png`;
      await page.screenshot({ path: path.join(output, file), fullPage: true });
      checks.push({ width, check: 'editable-fields-positive-weight-guidance-input-preserved-and-no-overflow', geometry, file });
      await context.close();
    }
    assert.equal(errors.length, 0, errors.join('\n')); assert.equal(blocked.length, 0);
  } catch (error) {
    errors.push(String(error.stack || error));
    if (currentPage && !currentPage.isClosed()) {
      await currentPage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {});
      fs.writeFileSync(path.join(output, 'failure-body.txt'), await currentPage.locator('body').innerText().catch(() => 'unavailable'));
    }
    throw error;
  } finally {
    await browser.close();
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ base, scope: 'Actual common CargoScreen with in-memory draft only; no order submission, business API, DB or device.', checks, errors, blocked }, null, 2));
  }
  console.log(JSON.stringify({ passed: checks.length, screenshots: 2, output }));
})().catch(error => { console.error(error); process.exitCode = 1; });
