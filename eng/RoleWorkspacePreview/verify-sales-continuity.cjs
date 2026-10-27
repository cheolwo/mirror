const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const base = process.env.SSALDDEL_USABILITY_PREVIEW_URL || 'http://127.0.0.1:5586';
  assert.equal(new URL(base).hostname, '127.0.0.1', 'Use the isolated loopback preview only.');
  const output = path.resolve(process.env.SSALDDEL_SALES_CONTINUITY_OUTPUT || 'artifacts/local/quick-readiness-20261016/sales-continuity');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
    ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
  const checks = [], failures = [], pageErrors = [], blockedRequests = [], captures = [];
  try {
    for (const width of [320, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR' });
      const page = await context.newPage();
      page.setDefaultTimeout(8000);
      page.setDefaultNavigationTimeout(15000);
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
        const started = Date.now();
        console.log(`[sales ${width}] START ${name}`);
        try { await action(); checks.push(`${width}:${name}`); console.log(`[sales ${width}] PASS ${name} (${Date.now() - started}ms)`); }
        catch (error) { failures.push({ width, name, error: String(error) }); console.log(`[sales ${width}] FAIL ${name}: ${String(error)}`); }
      }
      async function open() {
        await page.goto(`${base}/sales-continuity-preview`);
        await page.locator('.sales-order-filters input').first().waitFor();
      }
      async function trigger(selector) { await page.locator(selector).evaluate(element => element.click()); }
      async function pending() { await trigger('[data-check-start]'); await page.locator('[data-sales-pending="true"]').waitFor(); }
      async function finish(selector) { await trigger(selector); await page.locator('[data-sales-pending="false"]').waitFor(); }
      async function capture(name) {
        const filename = `${name}-${width}.png`;
        await page.screenshot({ path: path.join(output, filename), fullPage: true });
        captures.push({ name, width, file: filename });
      }
      await open();
      await check('pending-recheck-preserves-input-node-focus-and-draft', async () => {
        const input = page.locator('.sales-order-filters input').first();
        await input.fill('계속 쓰는 검색');
        await input.focus();
        await input.evaluate(element => { globalThis.__salesInput = element; element.setSelectionRange(3, 5); });
        await pending();
        assert.ok(await page.evaluate(() => globalThis.__salesInput === document.querySelector('.sales-order-filters input')));
        assert.ok(await page.evaluate(() => document.activeElement === globalThis.__salesInput));
        assert.equal(await input.inputValue(), '계속 쓰는 검색');
        assert.equal(await input.evaluate(element => element.selectionStart), 3);
        assert.ok(await page.getByRole('button', { name: '조회', exact: true }).isDisabled());
        await trigger('[data-change-token]');
        assert.ok(await page.evaluate(() => globalThis.__salesInput === document.querySelector('.sales-order-filters input')));
        assert.ok(await page.evaluate(() => document.activeElement === globalThis.__salesInput));
        assert.equal(await input.inputValue(), '계속 쓰는 검색');
        assert.equal(await input.evaluate(element => element.selectionStart), 3);
        await input.fill('재확인 중 이어 쓴 검색');
        await capture('sales-filter-rechecking');
        await finish('[data-check-success]');
        // Pending 해제는 접근 조회 응답 시점이다. 그 뒤 실행하는 정본 목록 조회까지 완료를 기다린다.
        await page.locator('[data-sales-read-count="2"]').waitFor();
        assert.ok(await page.evaluate(() => globalThis.__salesInput === document.querySelector('.sales-order-filters input')));
        assert.ok(await page.evaluate(() => document.activeElement === globalThis.__salesInput));
        assert.equal(await input.inputValue(), '재확인 중 이어 쓴 검색');
        assert.equal(await page.locator('[data-sales-read-count]').getAttribute('data-sales-read-count'), '2');
      });
      await open();
      await check('returning-to-original-account-does-not-revive-previous-data', async () => {
        await pending(); await trigger('[data-change-account]');
        await page.waitForFunction(() => !document.querySelector('.sales-order-filters'));
        await trigger('[data-change-original]');
        assert.equal(await page.locator('.sales-order-filters').count(), 0);
        await finish('[data-check-success]');
        await page.getByText('로그인 정보가 바뀌었습니다.', { exact: true }).waitFor();
        assert.equal(await page.locator('.sales-order-filters').count(), 0);
        assert.ok(!(await page.locator('body').innerText()).includes('기존 계정 검토용 상품'));
        assert.equal(await page.locator('[data-sales-read-count]').getAttribute('data-sales-read-count'), '1');
      });
      await open();
      await check('feature-off-hides-previous-workspace', async () => {
        await pending(); await finish('[data-check-off]');
        await page.getByText('판매채널 출고 기능이 현재 비활성입니다.', { exact: true }).waitFor();
        assert.equal(await page.locator('.sales-order-filters').count(), 0);
        assert.equal(await page.locator('[data-sales-read-count]').getAttribute('data-sales-read-count'), '1');
      });
      await open();
      await check('failed-recheck-hides-previous-workspace', async () => {
        await pending(); await finish('[data-check-error]');
        await page.getByText('주문 출고 후보 화면을 준비하지 못했습니다.', { exact: true }).waitFor();
        assert.equal(await page.locator('.sales-order-filters').count(), 0);
      });
      await open();
      await check('account-change-blocks-old-data-until-explicit-new-read', async () => {
        await page.locator('.sales-order-filters input').first().fill('이전 계정 작성값');
        await pending(); await trigger('[data-change-account]');
        await page.waitForFunction(() => !document.querySelector('.sales-order-filters'));
        await finish('[data-check-success]');
        await page.getByText('로그인 정보가 바뀌었습니다.', { exact: true }).waitFor();
        assert.ok(!(await page.locator('body').innerText()).includes('기존 계정 검토용 상품'));
        assert.ok(!(await page.locator('body').innerText()).includes('이전 계정 작성값'));
        await trigger('[data-read-error]');
        await page.getByRole('button', { name: '다시 확인', exact: true }).click();
        await page.getByText('주문 출고 후보 화면을 준비하지 못했습니다.', { exact: true }).waitFor();
        assert.equal(await page.locator('.sales-order-filters').count(), 0);
        assert.ok(!(await page.locator('body').innerText()).includes('기존 계정 검토용 상품'));
        await page.getByRole('button', { name: '다시 확인', exact: true }).click();
        await page.getByText('새 계정 검토용 상품', { exact: true }).waitFor();
        assert.equal(await page.locator('.sales-order-filters input').first().inputValue(), '');
        await capture('sales-account-recovered');
      });
      for (const kind of ['role', 'logout']) {
        await open();
        await check(`${kind}-change-hides-previous-workspace`, async () => {
          await pending(); await trigger(`[data-change-${kind}]`);
          await page.waitForFunction(() => !document.querySelector('.sales-order-filters'));
          await finish('[data-check-success]');
          assert.equal(await page.locator('.sales-order-filters').count(), 0);
          const expected = kind === 'role' ? '화주 또는 판매자 역할이 필요한 화면입니다.' : '판매채널 주문 출고 후보는 화주·판매자 로그인이 필요합니다.';
          await page.getByText(expected, { exact: true }).waitFor();
        });
      }
      await context.close();
    }
  } finally { await browser.close(); }
  const result = { scope: 'isolated shared sales workspace; no actual seller account or business API', checks, failures, pageErrors, blockedRequests, captures };
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify({ passed: checks.length, failed: failures.length, pageErrors, blockedRequestAttempts: blockedRequests.length, captures: captures.length, result: path.join(output, 'result.json') }));
  if (failures.length || pageErrors.length || blockedRequests.length) process.exitCode = 1;
})();
