import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = new URL(process.env.SSALDDEL_AUTO_FULFILLMENT_PREVIEW_URL ?? 'http://127.0.0.1:5575');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password || base.search || base.hash) throw Error('Credential-free loopback only.');
const output = path.resolve(process.env.SSALDDEL_AUTO_FULFILLMENT_PREVIEW_OUTPUT ?? path.join(repo, 'artifacts/local/fulfillment-routing-r25/ui'));
if (!output.startsWith(path.join(repo, 'artifacts/local') + path.sep)) throw Error('Evidence output outside artifacts/local.');
fs.mkdirSync(output, { recursive: true });
const scenarios = ['living-create', 'living-lost', 'living-feature-off', 'living-accepted', 'living-unknown', 'living-review', 'living-paused', 'food-create', 'food-feature-off', 'food-unknown', 'food-review', 'food-paused', 'merchant-support'];
const sourceFiles = ['eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/AutoFulfillmentPreviewFixture.cs', 'eng/RoleWorkspacePreview/AutoFulfillmentPreviewPage.razor', 'eng/RoleWorkspacePreview/verify-auto-fulfillment.mjs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/AutoFulfillmentConditionsViewModel.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/AutoFulfillmentQueryViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/음식주문작성ViewModel.AutoFulfillment.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/주문자음식주문상세ViewModel.AutoFulfillment.cs',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryCreate.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryDetail.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderComposer.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderDetailPanel.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentConditions.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentConditions.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentStatus.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentStatus.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/FoodMerchantFulfillmentSettingsPanel.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Food/FoodMerchantFulfillmentSettingsPanel.razor.css',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll'];
const hashes = () => sourceFiles.filter(p => fs.existsSync(path.join(repo, p))).map(p => ({ path: p, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, p))).digest('hex') }));
const before = hashes(), passes = [], failures = [], requests = [];
const ensure = (ok, message) => { if (!ok) throw Error(message); };
async function enabled(page, text) {
  await page.waitForFunction(name => [...document.querySelectorAll('button')].some(b => b.textContent.trim() === name && !b.disabled), text, { timeout: 10000 });
}
async function conditions(page) {
  await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).waitFor();
  await page.getByRole('spinbutton', { name: '본인 부담 상한 (원)', exact: true }).fill('5000');
  const date = await page.evaluate(() => { const d = new Date(Date.now() + 4 * 3600000); const pad = n => String(n).padStart(2, '0'); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`; });
  await page.getByLabel('희망 완료 시각', { exact: true }).fill(date);
  await page.getByRole('checkbox', { name: /모의 퀵 배송 · 모의/ }).check();
  await page.getByRole('checkbox', { name: /허용한 처리처에 픽업/ }).check();
  await page.getByRole('checkbox', { name: /위 비용·시간·처리처 조건/ }).check();
}
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390]) for (const scenario of scenarios) {
    const scene = `${scenario}-${width}`, context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
    const page = await context.newPage(), errors = [], styles = [], checks = [];
    page.on('pageerror', e => errors.push(e.message)); page.on('response', r => { if (new URL(r.url()).pathname.endsWith('.css')) styles.push({ path: new URL(r.url()).pathname, status: r.status() }); });
    await context.route('**/*', route => { const r = route.request(), u = new URL(r.url()); const external = u.origin !== base.origin && !['data:', 'blob:'].includes(u.protocol), business = u.origin === base.origin && u.pathname.startsWith('/api/'); requests.push({ scene, path: external ? 'external-blocked' : u.pathname, method: r.method(), external, business }); return external || business ? route.abort() : route.continue(); });
    try {
      await page.goto(new URL(`/auto-fulfillment-preview/${scenario}`, base).href, { waitUntil: 'networkidle' });
      await page.locator(`[data-auto-scenario="${scenario}"]`).waitFor();
      if (['living-create', 'living-lost', 'living-feature-off'].includes(scenario)) {
        await page.getByRole('checkbox', { name: '거래 당사자와 거래·개인정보 안내를 확인했습니다.', exact: true }).check();
        await page.getByRole('button', { name: '배송비 확인하기', exact: true }).click();
        if (scenario === 'living-feature-off') {
          await page.getByRole('checkbox', { name: /직접 현장 지급/ }).waitFor();
          ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Feature-off auto form appeared'); checks.push('legacy dispatch and direct-payment form preserved');
        } else {
          const submit = page.getByRole('button', { name: '배송 요청 접수하기', exact: true });
          await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).waitFor(); ensure(await submit.isDisabled(), 'Conditions not acknowledged');
          ensure(await page.getByRole('radio').count() === 0, 'Manual provider selection mixed with automatic routing');
          await conditions(page); await page.getByRole('button', { name: '배송비 확인하기', exact: true }).click();
          await page.getByRole('checkbox', { name: /위 조건으로 배송/ }).check(); await enabled(page, '배송 요청 접수하기');
          await submit.click(); await page.getByRole('button', { name: '같은 요청 다시 확인하기', exact: true }).waitFor();
          await page.waitForFunction(() => document.querySelector('[data-auto-preview-calls]')?.dataset.createCalls === '1');
          ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Pending original submission still offered new automatic consent');
          ensure(await page.getByText('입력이 바뀌었습니다. 배송비를 다시 확인해 주세요.', { exact: true }).count() === 0, 'Pending recovery incorrectly requested a new quote');
          checks.push('platform selection only after cap deadline scope and explicit consent');
          if (scenario === 'living-lost') {
            const id = await page.locator('[data-auto-preview-calls]').getAttribute('data-create-id');
            await page.getByRole('button', { name: '같은 요청 다시 확인하기', exact: true }).click();
            await page.waitForFunction(() => document.querySelector('[data-auto-preview-calls]')?.dataset.createCalls === '2');
            ensure(id === await page.locator('[data-auto-preview-calls]').getAttribute('data-create-id'), 'Lost create ID changed'); checks.push('original submission replay');
          }
        }
      } else if (scenario === 'food-create' || scenario === 'food-feature-off') {
        await page.getByRole('button', { name: '주문 제출', exact: true }).waitFor();
        if (scenario === 'food-feature-off') { ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Food off automatic UI appeared'); checks.push('food legacy composer preserved'); }
        else {
          await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).waitFor();
          ensure(await page.getByRole('button', { name: '주문 제출', exact: true }).isDisabled(), 'Food cap/authorization omitted');
          await page.getByRole('checkbox', { name: '거래 당사자와 거래·개인정보 안내를 확인했습니다.', exact: true }).check();
          await conditions(page); ensure(await page.getByRole('checkbox', { name: /기사에게 직접 지급/ }).count() === 0, 'Living direct payment mixed into food');
          await enabled(page, '주문 제출'); await page.getByRole('button', { name: '주문 제출', exact: true }).click();
          await page.waitForFunction(() => document.querySelector('[data-auto-preview-calls]')?.dataset.createCalls === '1');
          ensure(await page.locator('[data-auto-preview-calls]').getAttribute('data-support-confirmed') === '2000', 'Food support not frozen'); checks.push('one fixed merchant support per full food order');
        }
      } else if (scenario.endsWith('paused')) {
        const status = page.locator('.auto-fulfillment-status');
        await status.getByText('자동 배송 처리가 중지되어 있습니다.', { exact: false }).waitFor();
        ensure(await status.getByRole('button').count() === 0, 'Paused execution offered fulfillment commands');
        ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Paused execution offered new consent');
        ensure(!(await status.innerText()).includes('0원'), 'Unknown paused costs became confirmed zero');
        ensure(await status.locator('dd').filter({ hasText: /^확인 필요$/ }).count() >= 3, 'Paused cap and costs were not unknown');
        ensure(await page.locator('[data-auto-preview-calls]').getAttribute('data-consent-calls') === '0', 'Paused execution sent consent');
        checks.push('feature-off stored automatic request remains read-only and unknown costs are preserved');
      } else if (scenario.endsWith('unknown')) {
        await page.getByText('접수 결과 확인 필요', { exact: true }).first().waitFor();
        ensure((await page.locator('.auto-fulfillment-status').innerText()).includes('확인 필요'), 'Unknown cost became zero');
        ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Unknown permitted reselection');
        await page.getByRole('button', { name: '자동 배송 진행 확인', exact: true }).click();
        await page.getByRole('button', { name: '원 요청 결과 확인', exact: true }).waitFor(); const id = await page.locator('[data-auto-preview-calls]').getAttribute('data-action-id');
        await page.getByRole('button', { name: '원 요청 결과 확인', exact: true }).click();
        await page.getByText('처리처 접수됨 · 기사 배정 확인 대기', { exact: true }).first().waitFor();
        ensure(id === await page.locator('[data-auto-preview-calls]').getAttribute('data-action-id'), 'Recovery changed operation'); checks.push('unknown blocks reselection and original action recovery');
      } else if (scenario.endsWith('review')) {
        await conditions(page); await enabled(page, '자동 접수 조건 다시 확인');
        await page.getByRole('button', { name: '자동 접수 조건 다시 확인', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('[data-auto-preview-calls]')?.dataset.consentCalls === '1'); checks.push('needs-review renews explicit scoped authorization');
      } else if (scenario === 'living-accepted') {
        await page.getByRole('button', { name: '현재 처리처 접수 취소', exact: true }).click(); await page.getByText('취소 결과 확인 필요', { exact: true }).first().waitFor();
        ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Cancellation unknown exposed new consent');
        await page.getByRole('button', { name: '자동 배송 진행 확인', exact: true }).click();
        await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).waitFor(); checks.push('cancel acknowledgement before new authorization');
      } else if (scenario === 'merchant-support') {
        await page.getByRole('spinbutton', { name: '주문당 정액 지원 (원)', exact: true }).fill('2500');
        await page.getByRole('checkbox', { name: /위 금액의 주문 전체/ }).check(); await enabled(page, '지원 설정 저장');
        await page.getByRole('button', { name: '지원 설정 저장', exact: true }).click(); await page.getByText('현재 지원액 2,500원 · 지원 사용', { exact: true }).waitFor(); checks.push('merchant fixed support save and renewed state');
      }
      await page.evaluate(() => document.fonts.ready);
      ensure(!/(Detail|Model)\.AutoRouting\.Error/.test(await page.locator('body').innerText()), 'Raw automatic error property appeared as text');
      ensure(await page.evaluate(() => document.documentElement.scrollWidth) <= width, 'Horizontal overflow');
      ensure(errors.length === 0, 'Page errors: ' + errors.join(';')); ensure(!styles.some(r => r.status >= 400), 'Styles failed');
      await page.screenshot({ path: path.join(output, scene + '.png'), fullPage: true });
      passes.push({ scene, checks: [...checks, 'actual shared Razor and VM', 'mobile overflow and page errors checked'], screenshot: scene + '.png' });
    } catch (e) {
      const diagnostics = await page.evaluate(() => ({ text: document.body.innerText, buttons: [...document.querySelectorAll('button')].map(b => ({ text: b.textContent.trim(), disabled: b.disabled })) })).catch(() => null);
      failures.push({ scene, error: e.message, diagnostics }); await page.screenshot({ path: path.join(output, scene + '-failed.png'), fullPage: true }).catch(() => {});
    } finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = hashes(), drift = before.filter(a => after.find(b => b.path === a.path)?.sha256 !== a.sha256);
const report = { generatedUtc: new Date().toISOString(), mode: 'actual-common-Razor-synthetic-in-process', businessHttpExecuted: false, liveDatabaseExecuted: false, realProviderExecuted: false, realPaymentExecuted: false, passes, failures, blockedRequests: requests.filter(r => r.external || r.business), sourceBefore: before, sourceAfter: after, sourceDrift: drift };
fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify({ pass: passes.length, fail: failures.length, sourceDrift: drift.length, blockedRequests: report.blockedRequests.length, output }));
if (failures.length || drift.length || report.blockedRequests.length) process.exitCode = 1;
