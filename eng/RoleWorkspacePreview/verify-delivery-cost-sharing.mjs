import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = new URL(process.env.SSALDDEL_COST_SHARING_PREVIEW_URL ?? 'http://127.0.0.1:5576');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password || base.search || base.hash) throw Error('Credential-free loopback only.');
const output = path.resolve(process.env.SSALDDEL_COST_SHARING_PREVIEW_OUTPUT ?? path.join(repo, 'artifacts/local/delivery-cost-sharing-r26/ui'));
if (!output.startsWith(path.join(repo, 'artifacts/local') + path.sep)) throw Error('Evidence output outside artifacts/local.');
fs.mkdirSync(output, { recursive: true });
const scenarios = ['merchant-new', 'merchant-fixed', 'merchant-percentage', 'merchant-restaurant', 'merchant-lost', 'merchant-legacy',
  'food-customer', 'food-fixed', 'food-percentage', 'food-restaurant', 'food-policy-change', 'detail-percentage', 'detail-restaurant-held', 'detail-waiting', 'detail-unknown', 'detail-lost',
  'role-percentage', 'role-held', 'role-waiting', 'role-paused'];
const sources = ['eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/DeliveryCostSharingPreviewFixture.cs', 'eng/RoleWorkspacePreview/DeliveryCostSharingPreviewPage.razor', 'eng/RoleWorkspacePreview/verify-delivery-cost-sharing.mjs',
  'Ssalddel.Contracts/Common/Fulfillment/DeliveryCostSharingPolicy.cs', 'Ssalddel.Contracts/Common/Fulfillment/AutoFulfillmentDtos.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/DeliveryCostSharingPresentation.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/FoodMerchantFulfillmentSettingsViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/AutoFulfillmentConditionsViewModel.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/AutoFulfillmentQueryViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/음식주문작성ViewModel.AutoFulfillment.cs', 'Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderComposer.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Food/FoodMerchantFulfillmentSettingsPanel.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Food/FoodMerchantFulfillmentSettingsPanel.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/DeliveryCostSharingExample.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/DeliveryCostSharingExample.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentConditions.razor', 'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentConditions.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentStatus.razor', 'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/RestaurantRoleWorkspaceAdapter.cs',
  'Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Contracts.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll'];
const hashes = () => sources.filter(p => fs.existsSync(path.join(repo, p))).map(p => ({ path: p, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, p))).digest('hex') }));
const before = hashes(), passes = [], failures = [], requests = [];
const ensure = (ok, message) => { if (!ok) throw Error(message); };
async function enabled(page, name) {
  await page.waitForFunction(text => [...document.querySelectorAll('button')].some(b => b.textContent.trim() === text && !b.disabled), name, { timeout: 10000 });
}
async function conditions(page) {
  await page.getByRole('spinbutton', { name: '본인 부담 상한 (원)', exact: true }).fill('5000');
  const date = await page.evaluate(() => { const d = new Date(Date.now() + 4 * 3600000), pad = n => String(n).padStart(2, '0'); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`; });
  await page.getByLabel('희망 완료 시각', { exact: true }).fill(date);
  await page.getByRole('checkbox', { name: /모의 퀵 배송 · 모의/ }).check();
  await page.getByRole('checkbox', { name: /허용한 처리처에 픽업/ }).check();
  await page.getByRole('checkbox', { name: /위 비용·시간·처리처 조건/ }).check();
}
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : {}) });
try {
  for (const width of [320, 390]) for (const scenario of scenarios) {
    const scene = `${scenario}-${width}`, context = await browser.newContext({ viewport: { width, height: 844 }, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
    const page = await context.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    await context.route('**/*', route => { const r = route.request(), u = new URL(r.url()), external = u.origin !== base.origin && !['data:', 'blob:'].includes(u.protocol), business = u.origin === base.origin && u.pathname.startsWith('/api/');
      requests.push({ scene, path: external ? 'external-blocked' : u.pathname, method: r.method(), external, business }); return external || business ? route.abort() : route.continue(); });
    try {
      await page.goto(new URL(`/delivery-cost-sharing-preview/${scenario}`, base).href, { waitUntil: 'networkidle' });
      await page.locator(`[data-cost-sharing-scenario="${scenario}"]`).waitFor();
      const marker = page.locator('[data-cost-sharing-calls]');
      if (scenario.startsWith('merchant-')) {
        const panel = page.locator('.merchant-fulfillment-settings');
        await panel.getByRole('radio', { name: /^주문자 부담/ }).waitFor();
        ensure(await panel.locator('.merchant-policy-card').count() === 4, 'Four burden cards absent');
        const mode = scenario === 'merchant-new' ? 'CustomerPays' : scenario === 'merchant-fixed' || scenario === 'merchant-legacy' ? 'FixedContribution' : scenario === 'merchant-restaurant' ? 'RestaurantPays' : 'PercentageShare';
        const name = { CustomerPays: /^주문자 부담/, FixedContribution: /^정액 지원/, PercentageShare: /^비율 분담/, RestaurantPays: /^음식점 전액/ }[mode];
        if (scenario === 'merchant-legacy') ensure(await panel.getByRole('spinbutton', { name: '주문당 정액 지원 (원)', exact: true }).inputValue() === '2000', 'Legacy fixed support lost');
        await panel.getByRole('radio', { name }).check();
        if (mode === 'FixedContribution') {
          if (scenario !== 'merchant-legacy') ensure(await panel.getByRole('spinbutton', { name: '주문당 정액 지원 (원)', exact: true }).inputValue() === '', 'New fixed amount became implicit zero');
          await panel.getByRole('spinbutton', { name: '주문당 정액 지원 (원)', exact: true }).fill('2500');
          ensure(await panel.getByRole('spinbutton', { name: '음식점 부담 비율 (%)', exact: true }).count() === 0, 'Unused percentage field shown');
        } else if (mode === 'PercentageShare') {
          ensure(await panel.getByRole('spinbutton', { name: '음식점 부담 비율 (%)', exact: true }).inputValue() === '', 'New percent became implicit zero');
          await panel.getByRole('spinbutton', { name: '음식점 부담 비율 (%)', exact: true }).fill('33.33');
          ensure(await panel.getByRole('button', { name: '지원 설정 저장', exact: true }).isDisabled(), 'Missing explicit limit choice allowed save');
          await panel.getByRole('radio', { name: '한도 설정', exact: true }).check();
          ensure(await panel.getByRole('spinbutton', { name: '음식점 최대 부담 (원)', exact: true }).inputValue() === '', 'New limit became implicit zero');
          await panel.getByRole('spinbutton', { name: '음식점 최대 부담 (원)', exact: true }).fill('2000');
        } else if (mode === 'RestaurantPays') {
          await panel.getByRole('radio', { name: '한도 설정', exact: true }).check();
          await panel.getByRole('spinbutton', { name: '음식점 최대 부담 (원)', exact: true }).fill('5000');
          await panel.getByRole('spinbutton', { name: '음식점 최대 부담 (원)', exact: true }).press('Tab');
          await panel.getByText('음식점 전액 부담 한도를 넘어 이 조건의 접수는 보류됩니다.', { exact: false }).waitFor();
          ensure(await panel.getByText('주문당 정액 지원 (원)', { exact: true }).count() === 0, 'Full payment shown as fixed support');
        } else ensure(await panel.getByRole('spinbutton').count() === 0, 'Customer-pays required invented merchant amount');
        await panel.getByRole('checkbox', { name: '신규 주문에 자동 배송 사용', exact: true }).check();
        await panel.getByRole('checkbox', { name: /조건을 확인했습니다\.$/ }).check();
        await enabled(page, '지원 설정 저장'); await panel.getByRole('button', { name: '지원 설정 저장', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('[data-cost-sharing-calls]')?.dataset.saveCalls === '1');
        ensure(await marker.getAttribute('data-save-mode') === mode, 'Saved policy differs from selected burden policy');
        ensure(await marker.getAttribute('data-auto-enabled') === 'true', 'Automatic enable became dependent on merchant support');
        if (scenario === 'merchant-lost') {
          await panel.getByRole('button', { name: '원 설정 변경 결과 확인', exact: true }).waitFor(); const id = await marker.getAttribute('data-save-id');
          ensure(await panel.getByRole('spinbutton', { name: '음식점 부담 비율 (%)', exact: true }).isDisabled(), 'Pending policy was editable');
          await panel.getByRole('button', { name: '원 설정 변경 결과 확인', exact: true }).click();
          await page.waitForFunction(() => document.querySelector('[data-cost-sharing-calls]')?.dataset.saveCalls === '2');
          ensure(id === await marker.getAttribute('data-save-id'), 'Lost policy request changed ID');
          ensure(await marker.getAttribute('data-save-percent') === '33.33', 'Lost policy request changed percent'); checks.push('original policy JSON and ID replay');
        }
        checks.push('four burden choices separated from automatic enable', 'required fields and explicit limit choice', 'shared calculator clearly labeled example');
      } else if (scenario.startsWith('food-')) {
        await page.getByRole('heading', { name: '내 배송비 조건', exact: true }).waitFor();
        await page.getByRole('heading', { name: '자동 배송 처리 조건', exact: true }).waitFor();
        ensure(await page.getByRole('radio').count() === 0, 'Customer chose merchant policy or actual provider');
        await page.getByRole('region', { name: '배송비 분담 계산 예시', exact: true }).waitFor();
        await page.getByRole('checkbox', { name: '거래 당사자와 거래·개인정보 안내를 확인했습니다.', exact: true }).check();
        await conditions(page); await enabled(page, '주문 제출');
        if (scenario === 'food-policy-change') {
          await page.getByRole('button', { name: '합성 식당 정책 변경 사건', exact: true }).click();
          await page.getByText('음식점 전액 부담 · 음식점 한도 5,000원', { exact: true }).waitFor();
          ensure(await page.getByRole('checkbox', { name: /위 비용·시간·처리처 조건/ }).isChecked() === false, 'Changed policy retained old auto consent');
          ensure(await page.getByRole('button', { name: '주문 제출', exact: true }).isDisabled(), 'Changed policy submitted without new confirmation');
          await conditions(page); await enabled(page, '주문 제출'); checks.push('changed merchant policy invalidates prior authorization');
        }
        await page.getByRole('button', { name: '주문 제출', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('[data-cost-sharing-calls]')?.dataset.submitCalls === '1');
        const expected = scenario === 'food-customer' ? 'CustomerPays' : scenario === 'food-fixed' ? 'FixedContribution' : scenario === 'food-restaurant' || scenario === 'food-policy-change' ? 'RestaurantPays' : 'PercentageShare';
        ensure(await marker.getAttribute('data-confirmed-mode') === expected, 'Authoritative merchant policy not included in consent');
        ensure(await marker.getAttribute('data-confirmed-revision') === (scenario === 'food-policy-change' ? '8' : '7'), 'Merchant policy revision changed');
        checks.push('customer cap and merchant condition separate from fulfillment scope', 'confirmed policy and version sent', 'example differs from actual quote');
      } else if (scenario.startsWith('role-')) {
        const details = page.locator('#role-workspace-details'); await details.waitFor();
        const automatic = details.locator('section').filter({ has: page.getByRole('heading', { name: '자동 배송 처리', exact: true }) });
        await automatic.waitFor();
        if (scenario === 'role-percentage') { await automatic.getByText('1,999원', { exact: true }).waitFor(); await automatic.getByText('4,001원', { exact: true }).waitFor(); }
        else {
          ensure(await automatic.getByText('0원', { exact: true }).count() === 0, 'Role card unknown burden became zero');
          if (scenario === 'role-waiting') ensure(await automatic.getByText('견적 대기', { exact: true }).count() === 3, 'Role card pending quote appeared as free');
          if (scenario === 'role-held') { await automatic.getByText('견적 미확정', { exact: true }).waitFor(); await automatic.getByText('전액 부담 한도의 초과분을 주문자에게 자동으로 넘기지 않습니다.', { exact: false }).waitFor(); }
          if (scenario === 'role-paused') await automatic.getByText('자동 배송 처리가 중지되어 원 요청을 조회만 할 수 있습니다.', { exact: true }).waitFor();
        }
        if (scenario !== 'role-paused') { await automatic.getByText('7', { exact: true }).waitFor(); ensure(await automatic.getByText('배송비 조건', { exact: true }).count() === 1, 'Role card policy absent'); }
        checks.push('actual RestaurantRoleWorkspaceAdapter and RoleMapWorkspace render', 'role burden policy and pending/held/paused cost distinctions');
      } else if (scenario === 'detail-lost') {
        await conditions(page); await enabled(page, '자동 접수 조건 다시 확인');
        await page.getByRole('button', { name: '자동 접수 조건 다시 확인', exact: true }).click();
        await page.getByRole('button', { name: '원 요청 결과 확인', exact: true }).waitFor(); const id = await marker.getAttribute('data-consent-id');
        ensure(await page.getByRole('heading', { name: '자동 배송 접수 조건', exact: true }).count() === 0, 'Pending consent offered new policy authorization');
        await page.getByRole('button', { name: '원 요청 결과 확인', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('[data-cost-sharing-calls]')?.dataset.consentCalls === '2');
        ensure(id === await marker.getAttribute('data-consent-id'), 'Lost consent changed request ID');
        ensure(await marker.getAttribute('data-confirmed-mode') === 'PercentageShare', 'Lost consent changed frozen policy'); checks.push('original frozen policy authorization replay');
      } else {
        const status = page.locator('.auto-fulfillment-status'); await status.waitFor();
        if (scenario === 'detail-waiting') {
          ensure(await status.locator('dd').filter({ hasText: /^견적 대기$/ }).count() === 3, 'Before quote food costs appeared as free/zero');
        } else if (scenario === 'detail-unknown') {
          for (const label of ['주문 전체 배송 비용', '음식점 부담', '본인 부담']) {
            const row = status.locator('dl > div').filter({ has: page.getByText(label, { exact: true }) });
            ensure(await row.locator('dd').innerText() === '확인 필요', 'Unknown nullable burden changed into awaiting/free quote: ' + label);
          }
          ensure(await status.getByText('견적 대기', { exact: true }).count() === 0, 'Unknown quote described as initial pending quote');
        } else if (scenario === 'detail-restaurant-held') {
          await status.getByText('음식점의 부담 조건을 충족하지 못해 접수가 보류됩니다.', { exact: false }).waitFor();
          await status.getByText('견적 미확정', { exact: true }).waitFor();
          ensure(await status.locator('dd').filter({ hasText: /^확인 필요$/ }).count() >= 2, 'Held merchant/user cost became zero');
          ensure(await status.getByText('0원', { exact: true }).count() === 0, 'Full cap excess treated as zero');
        } else {
          await status.getByText('1,999원', { exact: true }).waitFor(); await status.getByText('4,001원', { exact: true }).waitFor();
          ensure(await status.getByRole('region', { name: '배송비 분담 계산 예시', exact: true }).count() === 0, 'Actual breakdown relabeled as synthetic example');
        }
        checks.push('actual total merchant and customer burden separated', 'unknown held and awaiting-quote states do not imply free delivery');
      }
      await page.evaluate(() => document.fonts.ready);
      ensure(await page.evaluate(() => document.documentElement.scrollWidth) <= width, 'Horizontal overflow');
      ensure(errors.length === 0, 'Page errors: ' + errors.join(';'));
      ensure(!/(Detail|Model|Fixture)\.[A-Za-z]+\.Error/.test(await page.locator('body').innerText()), 'Raw property rendered as text');
      await page.screenshot({ path: path.join(output, scene + '.png'), fullPage: true });
      passes.push({ scene, checks: [...checks, 'actual shared Razor and VM', 'mobile overflow checked'], screenshot: scene + '.png' });
    } catch (e) {
      const diagnostics = await page.evaluate(() => ({ text: document.body.innerText, buttons: [...document.querySelectorAll('button')].map(b => ({ text: b.textContent.trim(), disabled: b.disabled })) })).catch(() => null);
      failures.push({ scene, error: e.message, diagnostics }); await page.screenshot({ path: path.join(output, scene + '-failed.png'), fullPage: true }).catch(() => {});
    } finally { await context.close(); }
  }
} finally { await browser.close(); }
const after = hashes(), drift = before.filter(a => after.find(b => b.path === a.path)?.sha256 !== a.sha256);
const report = { generatedUtc: new Date().toISOString(), mode: 'actual-common-Razor-synthetic-in-process', businessHttpExecuted: false, liveDatabaseExecuted: false, realProviderExecuted: false, realPaymentExecuted: false, passes, failures,
  blockedRequests: requests.filter(r => r.external || r.business), sourceBefore: before, sourceAfter: after, sourceDrift: drift };
fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify({ pass: passes.length, fail: failures.length, sourceDrift: drift.length, blockedRequests: report.blockedRequests.length, output }));
if (failures.length || drift.length || report.blockedRequests.length) process.exitCode = 1;
