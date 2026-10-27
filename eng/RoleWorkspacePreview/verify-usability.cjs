const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const base = process.env.SSALDDEL_USABILITY_PREVIEW_URL || 'http://127.0.0.1:5586';
  assert.equal(new URL(base).hostname, '127.0.0.1', 'Use the isolated loopback preview only.');
  const output = path.resolve('artifacts/local/usability-review-20261016/browser');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const checks = [], failures = [], pageErrors = [], blockedRequests = [], captures = [];
  try {
    for (const width of [320, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
      const page = await context.newPage();
      page.on('pageerror', error => pageErrors.push(String(error)));
      await context.route('**/*', route => {
        const url = new URL(route.request().url());
        if ((url.origin !== base && !['data:', 'blob:'].includes(url.protocol)) || url.pathname.startsWith('/api/')) {
          blockedRequests.push({ method: route.request().method(), kind: url.origin !== base ? 'external' : 'business-api' });
          return route.abort();
        }
        return route.continue();
      });
      async function check(name, action) {
        try { await action(); checks.push(`${width}:${name}`); }
        catch (error) { failures.push({ width, name, error: String(error) }); }
      }
      async function open(scenario) {
        await page.goto(`${base}/usability-preview/${scenario}`);
        await page.locator(`[data-usability-scenario="${scenario}"]`).waitFor();
      }
      async function capture(name) {
        await check(`${name}:no-horizontal-overflow`, async () => assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)));
        const filename = `${name}-${width}.png`;
        await page.screenshot({ path: path.join(output, filename), fullPage: true });
        captures.push({ name, width, file: filename });
      }
      await open('mart-off');
      await check('feature-off-is-clear-and-has-return', async () => {
        await page.getByText('아직 상품을 이용할 수 없어요.', { exact: true }).waitFor();
        assert.equal(await page.locator('[data-product-body]').count(), 0);
        assert.equal(await page.getByRole('link', { name: '돌아가기', exact: true }).getAttribute('href'), '/usability-preview/home');
        assert.ok(!(await page.locator('body').innerText()).includes('SsalddelMartWorkflow'));
      });
      await capture('mart-off');
      await check('feature-off-return-reaches-valid-page', async () => {
        await page.getByRole('link', { name: '돌아가기', exact: true }).click();
        await page.getByRole('heading', { name: '화면 검토 시작', exact: true }).waitFor();
      });
      await open('mart-error');
      await check('read-error-retains-retry-and-return-without-raw-exception', async () => {
        await page.getByText('마트 상품 화면을 준비하지 못했습니다.', { exact: true }).waitFor();
        assert.ok(!(await page.locator('body').innerText()).includes('PreviewInternalFeatureReadFailure'));
        assert.equal(await page.locator('[data-product-body]').count(), 0);
        assert.equal(await page.getByRole('link', { name: '돌아가기', exact: true }).count(), 1);
      });
      await capture('mart-error');
      await check('explicit-retry-recovers-product-body', async () => {
        await page.getByRole('button', { name: '다시 확인', exact: true }).click();
        await page.locator('[data-product-body]').waitFor();
        assert.equal(await page.getByText('마트 상품 화면을 준비하지 못했습니다.', { exact: true }).count(), 0);
        assert.ok(!(await page.locator('body').innerText()).includes('PUBLIC MART'));
        await page.getByText('상품 정보 확인', { exact: true }).waitFor();
        const requestHint = page.getByText('주문 확정 전', { exact: true });
        await requestHint.waitFor();
        assert.ok(await requestHint.evaluate(element => element.scrollWidth <= element.clientWidth), 'The request hint must fit its tile.');
      });
      await capture('mart-recovered');
      await open('mart-sample');
      await check('sample-boundary-remains-visible', async () => {
        await page.getByText('화면 검토용 샘플입니다. 표시된 상품·가격·재고는 실제 주문이나 운영 원장에 연결되지 않습니다.', { exact: true }).waitFor();
        await page.locator('[data-product-body]').waitFor();
      });
      await capture('mart-sample');
      await open('driver-bank');
      await check('bank-is-unconfirmed-with-real-return-destinations', async () => {
        await page.getByText('계좌 정보 미확인', { exact: true }).waitFor();
        const text = await page.locator('body').innerText();
        assert.ok(!text.includes('홍길동') && !text.includes('국민은행') && !text.includes('123-456'));
        assert.equal(await page.getByRole('button', { name: '본인 확인', exact: true }).count(), 0);
        assert.equal(await page.getByRole('link', { name: '운송대금 목록 보기', exact: true }).getAttribute('href'), '/driver/payouts');
        assert.equal(await page.getByRole('link', { name: '전체 메뉴로', exact: true }).getAttribute('href'), '/driver/menu');
      });
      await capture('driver-bank');
      await page.goto(`${base}/auto-fulfillment-preview/food-create`);
      await page.locator('[data-auto-preview-ready]').waitFor();
      await check('orderer-required-optional-fields-and-disabled-reason', async () => {
        const recipient = page.getByLabel('수령인 이름 (필수)', { exact: true });
        await recipient.waitFor();
        await page.getByLabel('상세 주소 (선택)', { exact: true }).waitFor();
        await recipient.fill('');
        await page.getByText('수령인 이름 입력을 완료해 주세요.', { exact: true }).waitFor();
        const submit = page.getByRole('button', { name: '주문 제출', exact: true });
        assert.ok(await submit.isDisabled());
        const describedBy = await submit.getAttribute('aria-describedby');
        assert.ok(describedBy && await page.locator(`[id="${describedBy}"]`).isVisible());
        assert.equal(await page.locator('[data-auto-preview-calls]').getAttribute('data-create-calls'), '0');
      });
      await capture('orderer-readiness');
      const formFile = `orderer-required-fields-${width}.png`;
      await page.locator('.food-order-composer__form').screenshot({ path: path.join(output, formFile) });
      captures.push({ name: 'orderer-required-fields', width, file: formFile, kind: 'component-region', selector: '.food-order-composer__form' });
      await context.close();
    }
    assert.equal(pageErrors.length, 0, 'No browser script errors.');
    assert.equal(blockedRequests.length, 0, 'These previews must not attempt business or external requests.');
  } catch (error) {
    failures.push({ name: 'overall', error: String(error) });
  } finally {
    await browser.close();
    const result = { base, checks, failures, pageErrors, blockedRequests, captures, claimBoundary: 'Product component browser rendering and isolated local read/retry only; not APK/device/server-payment evidence.' };
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify({ passed: checks.length, failed: failures.length, browserErrors: pageErrors.length, blockedRequests: blockedRequests.length, captures: captures.length }));
    if (failures.length) process.exitCode = 1;
  }
})();
