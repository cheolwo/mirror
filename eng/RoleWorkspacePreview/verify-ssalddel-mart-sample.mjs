// 실제 UseCase/격리 SQLite를 사용하는 전용 표본 화면 검증. 실제 주문·배차·지급·단말 검증이 아닙니다.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = new URL(process.env.SSALDDEL_MART_SAMPLE_URL ?? 'http://127.0.0.1:5588/');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password || base.search || base.hash)
  throw new Error('LOOPBACK_ORIGIN_REQUIRED');
const local = path.join(repo, 'artifacts/local');
const output = path.resolve(process.env.SSALDDEL_MART_SAMPLE_OUTPUT ?? path.join(local, 'ssalddel-mart-sample-r52/ui'));
if (!output.startsWith(local + path.sep)) throw new Error('LOCAL_EVIDENCE_PATH_REQUIRED');
const binary = path.resolve(process.env.SSALDDEL_MART_SAMPLE_BINARY_DIR ?? path.join(local, 'ssalddel-mart-sample-r52/host'));
if (!binary.startsWith(local + path.sep) && binary !== path.join(repo, 'eng/RoleWorkspacePreview/bin/Debug/net10.0'))
  throw new Error('LOCAL_BINARY_PATH_REQUIRED');
const expected = process.env.SSALDDEL_MART_SAMPLE_EXPECT ?? 'loaded';
if (!['loaded', 'missing', 'invalid', 'operational'].includes(expected)) throw new Error('EXPECTATION_INVALID');
const positiveInteger = (name, fallback) => {
  const raw = process.env[name];
  if (raw === undefined) return fallback;
  const value = Number(raw);
  if (!/^[1-9][0-9]*$/.test(raw) || !Number.isSafeInteger(value)) throw new Error('EXPECTED_COUNT_INVALID');
  return value;
};
const expectedLineCount = positiveInteger('SSALDDEL_MART_EXPECT_LINE_COUNT', 22);
const expectedUnitCount = positiveInteger('SSALDDEL_MART_EXPECT_UNIT_COUNT', 36);
const sources = [
  'eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/RoleWorkspacePreview.csproj',
  'eng/RoleWorkspacePreview/SsalddelMartSampleInput.cs', 'eng/RoleWorkspacePreview/SsalddelMartSampleRuntime.cs',
  'eng/RoleWorkspacePreview/SsalddelMartSamplePage.razor', 'eng/RoleWorkspacePreview/SsalddelMartSamplePage.razor.css',
  'eng/RoleWorkspacePreview/verify-ssalddel-mart-sample.mjs',
  path.relative(repo, path.join(binary, 'RoleWorkspacePreview.dll')),
  path.relative(repo, path.join(binary, 'Ssalddel.dll'))
];
const fingerprint = () => sources.filter(value => fs.existsSync(path.join(repo, value))).map(value => ({
  path: value, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, value))).digest('hex')
}));
const before = fingerprint(), passes = [], failures = [];
fs.mkdirSync(output, { recursive: true });
const ensure = (value, code) => { if (!value) throw new Error(code); };
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390, 1280]) {
    const context = await browser.newContext({ viewport: { width, height: 900 }, locale: 'ko-KR', timezoneId: 'Asia/Seoul', reducedMotion: 'reduce' });
    const blocked = [], errors = [], screenshots = [], checks = [];
    await context.route('**/*', route => {
      const url = new URL(route.request().url());
      const external = url.origin !== base.origin && !['data:', 'blob:'].includes(url.protocol);
      const businessApi = url.origin === base.origin && url.pathname.startsWith('/api/');
      if (external || businessApi) { blocked.push({ external, businessApi }); return route.abort(); }
      return route.continue();
    });
    const page = await context.newPage();
    page.on('pageerror', () => errors.push('PAGE_ERROR'));
    page.on('console', message => { if (message.type() === 'error') errors.push('CONSOLE_ERROR'); });
    page.on('websocket', socket => { if (new URL(socket.url()).host !== base.host) blocked.push({ externalWebSocket: true }); });
    const primary = () => page.locator('[data-action="primary"]');
    const stage = async () => page.locator('[data-selected-order]').getAttribute('data-stage');
    const readyPrimary = async kind => {
      await page.locator(`[data-action="primary"][data-action-kind="${kind}"]`).waitFor();
    };
    const waitEnabled = async () => page.waitForFunction(() => {
      const button = document.querySelector('[data-action="primary"]'); return button && !button.disabled;
    });
    const inspect = async () => {
      await page.evaluate(() => document.fonts.ready);
      ensure(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'HORIZONTAL_OVERFLOW');
      const dimensions = await page.locator('.mart-sample button:visible,.mart-sample summary:visible,.mart-sample__check:visible,.mart-sample__field input:visible')
        .evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height));
      ensure(dimensions.every(height => height >= 47.5), 'TOUCH_TARGET_TOO_SHORT');
      ensure(errors.length === 0, 'BROWSER_ERROR'); ensure(blocked.length === 0, 'EXTERNAL_OR_BUSINESS_REQUEST');
      // Source IDs, prior business names and financial amounts must not become display information.
      ensure(await page.locator('main').evaluate(node => !/replay-(?:record|branch|pickup)-[a-f0-9]{64}|B\s*마트|비마트|₩|[\d,]+\s*원/.test(node.innerText)), 'PRIVATE_PROVENANCE_OR_AMOUNT_DISPLAYED');
      ensure(await primary().count() <= 1, 'MULTIPLE_PRIMARY_ACTIONS');
    };
    const capture = async name => {
      await inspect(); const file = `${expected}-${name}-${width}.png`;
      await page.screenshot({ path: path.join(output, file), fullPage: true }); screenshots.push(file);
    };
    try {
      await page.goto(new URL('/workspace/warehouse/ssalddel-mart-sample', base).href);
      await page.locator('[data-mart-sample-ready="true"]').waitFor({ timeout: 60000 });
      await page.getByRole('heading', { name: '살뜰마트', exact: true }).waitFor();
      if (expected !== 'loaded') {
        ensure(await page.locator('[data-mart-sample-loaded="false"]').count() === 1, 'INVALID_INPUT_ACCEPTED');
        ensure(await page.locator('[data-mart-input-unavailable]').count() === 1, 'INPUT_FAILURE_HIDDEN');
        ensure(await page.locator('[data-order-id]').count() === 0 && await primary().count() === 0, 'FAILED_INPUT_FALLBACK');
        await capture('input-blocked'); checks.push('input failure stays unavailable; no fallback or work controls');
      } else {
        ensure(await page.locator('[data-mart-sample-loaded="true"]').count() === 1, 'INPUT_NOT_LOADED');
        const orderIds = await page.locator('[data-order-id]').evaluateAll(nodes => nodes.map(node => node.dataset.orderId));
        ensure(orderIds.length === 3 && new Set(orderIds).size === 3, 'THREE_INDEPENDENT_ORDERS_REQUIRED');
        let lineCount = 0, unitCount = 0;
        // Selection is exercised through normal pointer actions, not generated business commands.
        for (const id of orderIds) {
          const index = orderIds.indexOf(id);
          await page.locator('[data-order-id]').nth(index).click();
          await page.waitForFunction(selected => document.querySelector('[data-selected-order]')?.dataset.selectedOrder === selected, id);
          const quantities = await page.locator('[data-mart-line]').evaluateAll(nodes => nodes.map(node => Number(node.dataset.lineQuantity)));
          lineCount += quantities.length; unitCount += quantities.reduce((sum, value) => sum + value, 0);
          ensure(await stage() === 'picking', 'ORDER_NOT_INITIAL_PICKING');
          ensure(await page.locator('[data-action-kind="pack"],[data-action-kind="handoff"]').count() === 0, 'EARLY_PACK_OR_HANDOFF_AVAILABLE');
        }
        ensure(lineCount === expectedLineCount && unitCount === expectedUnitCount, 'EXPECTED_ITEM_COUNTS_CHANGED');
        checks.push(`three independent orders preserve ${expectedLineCount} goods lines and ${expectedUnitCount} units; bag charge excluded`, 'pack and handoff unavailable before picking');
        await page.locator('[data-order-id]').first().click();
        await page.waitForFunction(selected => document.querySelector('[data-selected-order]')?.dataset.selectedOrder === selected, orderIds[0]);
        await capture('orders-ready');
        for (let orderIndex = 0; orderIndex < orderIds.length; orderIndex++) {
          const orderId = orderIds[orderIndex];
          await page.locator('[data-order-id]').nth(orderIndex).click();
          await page.waitForFunction(selected => document.querySelector('[data-selected-order]')?.dataset.selectedOrder === selected, orderId);
          let safety = 0;
          while (await stage() === 'picking') {
            ensure(++safety <= 40, 'PICKING_DID_NOT_CONVERGE');
            await readyPrimary('start'); await primary().click(); await readyPrimary('pick');
            const currentId = await page.locator('[data-current-line]').getAttribute('data-current-line');
            const rack = await page.locator('[data-current-rack]').innerText();
            ensure(await primary().isDisabled(), 'PICKING_WITHOUT_FIELD_CHECKS');
            await page.locator('[data-rack-input]').fill(orderIndex === 0 && safety === 1 ? 'WRONG-RACK' : rack);
            await page.locator('[data-product-confirm]').check(); await page.locator('[data-quantity-confirm]').check(); await waitEnabled();
            if (orderIndex === 0 && safety === 1) {
              const completedBefore = await page.locator('[data-line-picked="true"]').count();
              await primary().click(); await page.locator('[data-mart-operation-error]').waitFor();
              ensure(await stage() === 'picking' && await page.locator('[data-current-line]').getAttribute('data-current-line') === currentId, 'WRONG_RACK_ADVANCED_ORDER');
              ensure(await page.locator('[data-line-picked="true"]').count() === completedBefore, 'WRONG_RACK_CHANGED_PICKED_QUANTITY');
              await capture('wrong-rack');
              await page.locator('[data-rack-input]').fill(rack); await waitEnabled();
            }
            await primary().click();
            await page.waitForFunction(previous => document.querySelector('[data-current-line]')?.dataset.currentLine !== previous, currentId);
            await inspect();
          }
          ensure(await stage() === 'packing', 'PICKING_NOT_FOLLOWED_BY_PACKING');
          ensure(await page.locator('[data-action-kind="handoff"]').count() === 0, 'EARLY_HANDOFF_AVAILABLE');
          await readyPrimary('pack'); ensure(await primary().isDisabled(), 'PACKING_WITHOUT_CONFIRMATION');
          if (orderIndex === 0) await capture('packing');
          await page.locator('[data-inventory-confirm]').check(); await page.locator('[data-package-confirm]').check(); await waitEnabled();
          await primary().click(); await readyPrimary('handoff');
          ensure(await stage() === 'handoff' && await page.locator('[data-line-packed="true"]').count() === await page.locator('[data-mart-line]').count(), 'INCOMPLETE_PACKING_ALLOWED_HANDOFF');
          ensure(await primary().isDisabled(), 'HANDOFF_WITHOUT_MOCK_DRIVER_CONFIRMATION');
          if (orderIndex === 0) await capture('handoff');
          await page.locator('[data-driver-confirm]').check(); await waitEnabled(); await primary().click();
          await page.locator('[data-mart-order-completed]').waitFor();
          ensure(await stage() === 'completed' && await primary().count() === 0, 'MOCK_HANDOFF_NOT_TERMINAL');
          if (orderIndex === 0 || orderIndex === orderIds.length - 1) await capture(`completed-${orderIndex + 1}`);
        }
        checks.push('wrong rack keeps current line unchanged', 'each line starts and completes through real work controls', 'all lines picked before packing; all lines packed before mock handoff', 'explicit confirmations required; terminal state has no repeated completion');
        for (let index = 0; index < orderIds.length; index++) {
          await page.locator('[data-order-id]').nth(index).click();
          await page.waitForFunction(selected => document.querySelector('[data-selected-order]')?.dataset.selectedOrder === selected, orderIds[index]);
          ensure(await stage() === 'completed', 'ORDER_SELECTION_LOST_OTHER_ORDER_STATE');
        }
        await page.locator('[data-reset-open]').click(); await page.locator('[data-reset-confirm]').click();
        await readyPrimary('start');
        ensure(await page.locator('[data-line-picked="true"],[data-line-packed="true"]').count() === 0, 'RESET_DID_NOT_CLEAR_WORK_STATE');
        await capture('reset'); checks.push('order switching retains independent completion; explicit reset restores sample');
      }
      await inspect();
      passes.push({ width, expected, checks: [...checks, 'no overflow; 48px controls', 'no browser errors or external/business requests'], screenshots });
    } catch (error) {
      const code = /^[A-Z0-9_]+$/.test(error.message) ? error.message : 'UI_VERIFICATION_FAILED';
      failures.push({ width, expected, code, browserErrorCount: errors.length, blockedRequestCount: blocked.length });
      await page.screenshot({ path: path.join(output, `${expected}-failure-${width}.png`), fullPage: true }).catch(() => {});
    } finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = fingerprint(), sourceFilesUnchanged = JSON.stringify(before) === JSON.stringify(after);
if (!sourceFilesUnchanged) failures.push({ code: 'SOURCE_OR_BINARY_CHANGED_DURING_VERIFICATION' });
fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ schemaVersion: 1, expected, expectedLineCount, expectedUnitCount, sourceFilesUnchanged, sourceFingerprints: before, passes, failures, boundary: 'isolated-sqlite-loopback-ui; no operational database, native APK, device, physical scan, real dispatch or payment' }, null, 2));
process.stdout.write(JSON.stringify({ expected, passed: passes.length, failed: failures.length, sourceFilesUnchanged }) + '\n');
if (failures.length) process.exitCode = 1;
