import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

// r43 verify-ui.mjs / verify-interactions.mjs의 실제 제품 화면·네 단계 클릭 검사를 재사용합니다.
const { chromium } = createRequire(import.meta.url)('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = new URL(process.env.SSALDDEL_MEMBER_DELIVERY_PREVIEW_URL ?? 'http://127.0.0.1:5393');
if (base.protocol !== 'http:' || base.hostname !== '127.0.0.1' || base.pathname !== '/' || base.username || base.password || base.search || base.hash)
  throw Error('Credential-free loopback preview required.');
const output = path.resolve(process.env.SSALDDEL_MEMBER_DELIVERY_PREVIEW_OUTPUT ?? path.join(repo, 'artifacts/local/neighborhood-member-delivery-r46-20261009/ui'));
if (!output.startsWith(path.join(repo, 'artifacts/local') + path.sep)) throw Error('Evidence must stay under artifacts/local.');
fs.mkdirSync(output, { recursive: true });
const scenarios = ['settings-off', 'settings-ready', 'settings-saved', 'feature-off', 'offers-empty', 'offer-ready', 'offer-expired',
  'assigned', 'evidence-ready', 'picked', 'delivered', 'received', 'issue', 'anonymous', 'error', 'request-cancel'];
const sourceFiles = ['eng/RoleWorkspacePreview/Program.cs', 'eng/RoleWorkspacePreview/RoleWorkspacePreview.csproj',
  'eng/RoleWorkspacePreview/NeighborhoodMemberDeliveryPreviewFixture.cs', 'eng/RoleWorkspacePreview/NeighborhoodMemberDeliveryPreviewPage.razor',
  'eng/RoleWorkspacePreview/verify-neighborhood-member-delivery.mjs', 'Ssalddel.Contracts/Common/Community/NeighborhoodMemberDeliveryDtos.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodMemberWorkViewModel.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodMemberOffersViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodMemberParticipationViewModel.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodMemberRequestViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodMemberPageViewModel.cs', 'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodMemberDeliverySession.cs',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodMemberWork.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodMemberWork.razor.css',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodMemberOfferDetail.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodMemberParticipation.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodMemberStateNotice.razor',
  'Ssalddel.Ui.Common/wwwroot/Areas/App/css/neighborhood-delivery.css',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Contracts.dll',
  'eng/RoleWorkspacePreview/bin/Debug/net10.0/Ssalddel.Ui.Common.dll', 'eng/RoleWorkspacePreview/bin/Debug/net10.0/RoleWorkspacePreview.dll'];
const fingerprints = () => sourceFiles.map(file => ({ path: file, ...(fs.existsSync(path.join(repo, file))
  ? { sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, file))).digest('hex') } : { missing: true }) }));
const before = fingerprints(), records = [], interactions = [], failures = [], requests = [], startedAtUtc = new Date().toISOString();
const browser = await chromium.launch({ headless: true, ...(process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM
  ? { executablePath: process.env.SSALDDEL_PLAYWRIGHT_CHROMIUM } : { channel: 'msedge' }) });
async function openPage(width, scene) {
  const context = await browser.newContext({ viewport: { width, height: 844 }, deviceScaleFactor: 1, locale: 'ko-KR', timezoneId: 'Asia/Seoul' });
  const page = await context.newPage(), errors = [], styles = [], styleReads = [];
  page.on('pageerror', error => errors.push(String(error)));
  page.on('response', response => {
    const pathname = new URL(response.url()).pathname;
    if (pathname.endsWith('.css')) styleReads.push(response.body().then(bytes => styles.push({ path: pathname, status: response.status(),
      sha256: crypto.createHash('sha256').update(bytes).digest('hex') })).catch(() => styles.push({ path: pathname, status: response.status(), unreadable: true })));
  });
  await context.route('**/*', async route => {
    const request = route.request(), url = new URL(request.url());
    const external = url.origin !== base.origin && !['data:', 'blob:'].includes(url.protocol);
    const mapRuntime = url.origin === base.origin && url.pathname === '/api/v1/platform/runtime/google-maps';
    const business = url.origin === base.origin && url.pathname.startsWith('/api/') && !mapRuntime;
    requests.push({ scene, method: request.method(), path: external ? 'external-blocked' : url.pathname, external, business, mapRuntime });
    if (external || business) return route.abort();
    // 지도 키·외부 타일 호출 없이 실제 제품 지도 host의 준비 안내를 확인합니다.
    if (mapRuntime) return route.fulfill({ status: 204 });
    return route.continue();
  });
  return { context, page, errors, styles, styleReads };
}
async function loaded(page, scenario) {
  await page.locator('[data-member-delivery-preview-ready]').waitFor();
  if (scenario === 'anonymous') await page.getByRole('heading', { name: '로그인이 필요해요', exact: true }).waitFor();
  else if (scenario === 'error') await page.locator('[role="alert"]').first().waitFor();
  else if (scenario.startsWith('settings') || scenario === 'feature-off') await page.getByRole('button', { name: '참여 설정 저장', exact: true }).waitFor();
  else if (scenario.startsWith('offer-')) await page.getByRole('heading', { name: '책 한 상자', exact: true }).waitFor();
  else if (scenario === 'offers-empty') await page.getByRole('heading', { name: '받은 배송 제안', exact: true }).waitFor();
  else if (scenario === 'request-cancel') await page.getByRole('button', { name: '픽업 전 배송 취소', exact: true }).waitFor();
  // 업무 물품 제목은 닫힌 보조 카드 안에 있으므로 보이는 현재 상태 카드로 준비를 확인합니다.
  else await page.locator('.member-delivery-map__card > summary').first().waitFor();
  await page.evaluate(() => document.fonts.ready);
}
async function enabled(page, label) {
  await page.waitForFunction(value => [...document.querySelectorAll('button')].some(button => button.textContent.trim() === value && !button.disabled), label);
}
async function mutations(page, expected) {
  await page.waitForFunction(value => document.querySelector('[data-member-delivery-preview-calls]')?.dataset.mutations === String(value), expected);
}
async function checkLayout(state, width) {
  const { page, errors, styles, styleReads } = state;
  await Promise.all(styleReads);
  const layout = await page.evaluate(() => ({ width: innerWidth, scrollWidth: document.documentElement.scrollWidth,
    map: document.querySelector('.member-delivery-map__canvas')?.getBoundingClientRect().toJSON(),
    buttons: [...document.querySelectorAll('button')].map(button => ({ label: button.innerText, disabled: button.disabled, height: button.getBoundingClientRect().height })) }));
  assert.ok(layout.scrollWidth <= width, `Horizontal overflow at ${width}px`);
  assert.equal(errors.length, 0, `Browser runtime errors: ${errors.join('; ')}`);
  assert.ok(!styles.some(style => style.status >= 400), 'Stylesheet failed');
  assert.ok(styles.some(style => style.path.endsWith('neighborhood-delivery.css') && style.status === 200), 'Product stylesheet absent');
  return { ...layout, errors: [...errors], styles: [...styles] };
}
try {
  assert.ok(before.every(file => !file.missing), `Missing source/binary: ${before.filter(file => file.missing).map(file => file.path).join(', ')}`);
  for (const width of [320, 390]) for (const scenario of scenarios) {
    const scene = `${scenario}-${width}`, state = await openPage(width, scene), checks = [];
    try {
      const { page } = state;
      await page.goto(new URL(`/neighborhood-member-delivery-preview/${scenario}`, base).href, { waitUntil: 'networkidle' });
      await loaded(page, scenario);
      // 닫힌 보조 카드의 지급 안내·개인정보도 검사하여 단순히 접힌 상태를 삭제로 오인하지 않습니다.
      const text = await page.locator('body').textContent() ?? '';
      if (scenario === 'assigned' || scenario === 'picked') {
        const primary = page.getByRole('button', { name: scenario === 'assigned' ? '사진 보내고 픽업 완료' : '사진 보내고 전달 완료', exact: true });
        assert.ok(await primary.isDisabled(), 'Missing evidence permitted completion');
        assert.ok(await page.getByRole('button', { name: '사진 촬영', exact: true }).isEnabled(), 'First photograph unavailable without completion permission');
        assert.ok(text.includes(scenario === 'assigned' ? '픽업 장소 1' : '전달 장소 1') && text.includes('010-0000-0000'), 'Current location/contact hidden before first photo');
        if (scenario === 'picked') assert.ok(!text.includes('픽업 장소 1'), 'Pickup detail remained after pickup');
        checks.push('first photo available before evidence', 'completion disabled without evidence', 'current location and contact visible');
      } else if (scenario === 'evidence-ready') {
        assert.ok(await page.getByRole('button', { name: '사진 보내고 픽업 완료', exact: true }).isEnabled(), 'Saved evidence did not enable explicit completion');
        assert.equal(await page.getByRole('button', { name: '사진 촬영', exact: true }).count(), 0, 'Saved evidence requested another photo');
        checks.push('saved evidence permits explicit completion');
      } else if (scenario === 'offer-expired') {
        assert.ok(!text.includes('현재 위치 확인하고 수락'), 'Expired accept exposed'); checks.push('expired offer blocks acceptance');
      } else if (scenario === 'anonymous') {
        assert.ok(!text.includes('픽업 장소 1') && !text.includes('010-0000-0000') && !text.includes('책 한 상자'), 'Anonymous private detail');
        checks.push('anonymous detail hidden');
      } else if (scenario === 'delivered' || scenario === 'received') {
        assert.ok(!text.includes('픽업 장소 1') && !text.includes('전달 장소 1') && !text.includes('010-0000-0000'), 'Completed private detail');
        assert.equal(await page.getByRole('button', { name: '사진 촬영', exact: true }).count(), 0, 'Completed work offered capture');
        if (scenario === 'received') { assert.ok(text.includes('수령자의 수령 확인을 받았습니다') && text.includes('전달 완료 · 수령 확인됨'), 'Receipt confirmation absent'); assert.ok(!text.includes('수령 확인 대기') && !text.includes('수령자의 확인을 기다려'), 'Confirmed receipt still waiting'); }
        else assert.ok(text.includes('수령자의 확인을 기다려'), 'Delivery became receipt confirmation');
        assert.ok(text.includes('배송비 지급 확인은 전달 완료와 별도'), 'Receipt became payment confirmation');
        checks.push('private detail hidden after delivery', scenario === 'received' ? 'recipient confirmation displayed' : 'recipient confirmation remains pending', 'payment separate');
      } else if (scenario === 'issue') {
        assert.equal(await page.getByRole('button', { name: '사진 촬영', exact: true }).count(), 0, 'Issue work offered capture');
        assert.ok(text.includes('preview-support-case'), 'Support case absent'); checks.push('issue blocks capture and completion');
      } else if (scenario === 'error') { assert.ok(!text.includes('책 한 상자'), 'Read failure became success'); checks.push('read failure remains an error'); }
      const layout = await checkLayout(state, width), screenshot = `${scene}.png`;
      await page.screenshot({ path: path.join(output, screenshot), fullPage: true });
      if (scenario === 'assigned') {
        const beforeMap = await page.locator('.member-delivery-map__canvas').boundingBox();
        await page.locator('.member-delivery-map__card summary').first().click();
        assert.deepEqual(await page.locator('.member-delivery-map__canvas').boundingBox(), beforeMap, 'Folding card changed map geometry');
        checks.push('folding preserves map geometry');
      }
      records.push({ scene, scenario, width, screenshot, checks, layout });
    } catch (error) {
      failures.push({ scene, error: error.message });
      await state.page.screenshot({ path: path.join(output, `${scene}-failed.png`), fullPage: true }).catch(() => {});
    } finally { await state.context.close(); }
  }
  for (const width of [320, 390]) {
    const scene = `four-step-flow-${width}`, state = await openPage(width, scene), steps = [];
    try {
      const { page } = state;
      await page.goto(new URL('/neighborhood-member-delivery-preview/settings-off', base).href, { waitUntil: 'networkidle' });
      await loaded(page, 'settings-off'); await enabled(page, '참여 설정 저장');
      await page.getByRole('button', { name: '참여 설정 저장', exact: true }).click(); await mutations(page, 1);
      steps.push({ step: 'participation-save', mutations: 1, layout: await checkLayout(state, width), screenshot: `flow-settings-saved-${width}.png` });
      await page.screenshot({ path: path.join(output, steps.at(-1).screenshot), fullPage: true });
      await page.goto(new URL('/neighborhood-member-delivery-preview/offer-ready', base).href, { waitUntil: 'networkidle' });
      await loaded(page, 'offer-ready'); await enabled(page, '현재 위치 확인하고 수락');
      await page.getByRole('button', { name: '현재 위치 확인하고 수락', exact: true }).click();
      await page.getByText('픽업 이동 중', { exact: true }).waitFor();
      const pickup = page.getByRole('button', { name: '사진 보내고 픽업 완료', exact: true });
      assert.ok(await pickup.isDisabled(), 'Acceptance enabled pickup before photo');
      steps.push({ step: 'offer-accept', completionDisabledWithoutPhoto: true, layout: await checkLayout(state, width), screenshot: `flow-assigned-${width}.png` });
      await page.screenshot({ path: path.join(output, steps.at(-1).screenshot), fullPage: true });
      await page.getByRole('button', { name: '사진 촬영', exact: true }).click(); await enabled(page, '사진 보내고 픽업 완료');
      await pickup.click(); await page.getByText('전달 이동 중', { exact: true }).waitFor();
      const delivery = page.getByRole('button', { name: '사진 보내고 전달 완료', exact: true });
      assert.ok(await delivery.isDisabled(), 'Pickup enabled delivery before its first photo');
      assert.ok(await page.getByRole('button', { name: '사진 촬영', exact: true }).isEnabled(), 'First delivery photo unavailable');
      assert.ok(!(await page.locator('body').textContent()).includes('픽업 장소 1'), 'Pickup detail remained after pickup');
      steps.push({ step: 'pickup-photo-and-complete', nextCompletionDisabledWithoutPhoto: true, layout: await checkLayout(state, width), screenshot: `flow-picked-${width}.png` });
      await page.screenshot({ path: path.join(output, steps.at(-1).screenshot), fullPage: true });
      await page.getByRole('button', { name: '사진 촬영', exact: true }).click(); await enabled(page, '사진 보내고 전달 완료');
      await delivery.click(); await page.getByText('전달 완료 · 수령 확인 대기', { exact: true }).first().waitFor();
      const text = await page.locator('body').textContent() ?? '';
      assert.ok(!text.includes('픽업 장소 1') && !text.includes('전달 장소 1') && !text.includes('010-0000-0000'), 'Delivery retained private details');
      assert.ok(text.includes('수령자의 확인을 기다려'), 'Delivery falsely confirmed receipt');
      steps.push({ step: 'delivery-photo-and-complete', receiptPending: true, layout: await checkLayout(state, width), screenshot: `flow-delivered-${width}.png` });
      await page.screenshot({ path: path.join(output, steps.at(-1).screenshot), fullPage: true });
      interactions.push({ scene, width, passed: true, steps });
    } catch (error) { failures.push({ scene, error: error.message, steps }); }
    finally { await state.context.close(); }
  }
} catch (error) { failures.push({ scene: 'setup', error: error.message }); }
finally {
  await browser.close();
  const after = fingerprints(), sourceDrift = before.filter(file => !after.some(next => next.path === file.path && next.sha256 === file.sha256 && next.missing === file.missing));
  const businessRequests = requests.filter(request => request.business), externalRequests = requests.filter(request => request.external);
  const result = { status: failures.length === 0 && sourceDrift.length === 0 && businessRequests.length === 0 ? 'PassedWithinListedScope' : 'Failed',
    scope: 'Actual product Razor/VM in loopback preview with in-process synthetic fixtures; map runtime supplied no key and external HTTP blocked.',
    startedAtUtc, finishedAtUtc: new Date().toISOString(), browserVersion: browser.version(), nodeVersion: process.version,
    productRazorHost: true, syntheticFixtureOnly: true, sourceBefore: before, sourceAfter: after, sourceDrift, records, interactions, failures,
    captures: records.length + interactions.reduce((sum, flow) => sum + flow.steps.length, 0),
    businessRequests: businessRequests.length, blockedExternalRequests: externalRequests.length,
    substitutedMapRuntimeRequests: requests.filter(request => request.mapRuntime).length,
    actualDispatch: false, actualGps: false, actualPhotoStorage: false, realMapTiles: false, actualDatabaseVerified: false,
    actualReceiptCommandVerified: false, actualPaymentVerified: false, apkVerified: false, physicalDeviceVerified: false };
  fs.writeFileSync(path.join(output, 'verification.json'), JSON.stringify(result, null, 2), 'utf8');
  fs.writeFileSync(path.join(output, 'request-evidence.json'), JSON.stringify(requests, null, 2), 'utf8');
  console.log(JSON.stringify({ status: result.status, scenarios: records.length, interactionFlows: interactions.length, captures: result.captures, failures, sourceDrift, output }));
  if (result.status !== 'PassedWithinListedScope') process.exitCode = 1;
}
